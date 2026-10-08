# Be My Arms local Transport patch

Base: Unity Transport **2.7.2**, embedded through Unity Package Manager. Original license and package
contents are retained. The project pins this embedded package so a clean checkout builds the same fix.

## UDP receive buffer ownership

`Runtime/UDPNetworkInterface.cs`, `ReceiveJob.Execute`: Baselib completion results that fail, have no
payload, or have an invalid payload length do not enter `ReceiveQueue`. Their acquired buffers must
be returned directly through `ReceiveQueue.ReleaseBuffer(bufferIndex)`; `ReceiveQueue.Clear()` can
only release buffers that actually reached the queue.

Without this release, transient socket errors can exhaust the pool. On Windows loopback the Be My Arms
dedicated-server check reproduced this after abruptly terminating one client: the remaining client
timed out and subsequent connections could not be established. The socket itself remained valid, so
the existing socket-recreation path did not restore the missing receives.

The same branches remain in the registry's 2.7.4 source (checked when establishing the control baseline). This patch changes only
the buffer-release paths; socket recreation, protocol, reliable delivery and public APIs are unchanged.

The regression is exercised by `tools/qa/test-session-lifecycle.ps1`: a dedicated server, two role clients,
abrupt termination, unaffected-partner checks, token reconnect to the exchanged role, voluntary leave
and a new guest. Run this when updating Transport; remove the local patch/embedding when an upstream
version fixes these ownership paths and the regression passes.
