# M2 — Networked Shared-Body Spike (NGO)

**Status:** **Complete at the M2 architecture-proof level.** Reconnect/handoff is demonstrated with a
real dedicated server + separate client processes. A force-kill-specific transport limitation is
documented in §5 with exact evidence and a safe workaround; hardening it belongs to the production
networking layer (M3+), which is the agreed M2 boundary.

---

## 1. What is implemented

- **Pure core:** `M2BodySim`, `M2Reconciler`, `M2WeaponState`, `M2LagCompensation`, `M2DelayQueue`,
  `M2RoleRegistry` (role binding + token restore + double-role reclaim + bot ownership).
- **Net:** `M2RoleService` (role assigned during **NGO connection approval**, so reconnecting
  clients are authorized before any replicated body exists; owns **temporary bot substitution** and
  the atomic human↔bot handoff), `M2NetworkBody` (server-authoritative sim, role-tagged RPCs,
  authorization, fire validation, lag-compensated hit test, conditioner, metrics, trivial bots),
  `M2ClientPredictor` (role-aware prediction/reconciliation, local P2 aim, camera measurement +
  boundary smoothing), `M2Bootstrap`, `M2SceneBuilder`, `M2Build`.
- Fixed 60 Hz step; transport-level simulator option.

## 2. Disconnect policy (product direction implemented at M2 level)

- One shared body; one owner per role, never two (`M2RoleRegistry.HasSingleOwner`).
- On a human disconnect the role becomes **bot-owned**; the other human does **not** gain it.
- On reconnect with the same token the role is **atomically** handed from bot back to the human.
- Body/role state is preserved (the sim keeps running; only the input source changes).
- Bots are trivial (P1 advances/turns, P2 aims/fires); real bot gameplay is a later milestone.

## 3. Verified — real three-process run

| Item | Result |
|---|---|
| Role authorization (approval) | `approved client N -> P1/P2`; wrong-role client rejected (`unauth`) |
| Bandwidth | ~`1.8 KB/s` up, ~`2.1 KB/s` down (approx) |
| Prediction error | app `avg 0.47 / max 0.68`; transport sim `avg 0.71 / max 1.58` |
| P2 camera correction / no-snap | in-sector `0.0°`; boundary snap **≤0.6°** (target 5°) |
| Transport-level simulator | Multiplayer Tools runtime `NetworkSimulator` (delay 50 ms / 2 %), verified |
| Sector / lag comp / cadence / ammo | validated as before |
| Server CPU | `tick 0.01–0.25 ms` |
| **Disconnect → bot → reconnect handoff (graceful)** | verified end-to-end (see §4) |
| EditMode tests | **40/40** |

## 4. Reconnect/handoff proof (graceful disconnect), traced

```
t=4.5   approved client 1 -> P2 ; approved client 2 -> P1
t=13.3  client 2 disconnected -> role P1 to BOT (singleOwner=True)
t=15.3  status P1[bot=True human=False] P2[bot=False human=True]   <- body alive, one owner
t=19.4  approval EXIT id=3 role=P1 botActive=False singleOwner=True <- human reclaims P1
t=20.3  status P1[bot=False human=True] P2[bot=False human=True]
```

This demonstrates the full architecture: role → bot on disconnect, single ownership, atomic
bot→human handoff on token reconnect, body state preserved.

## 5. Force-kill evidence and documented limitation

The minimal reproduction (dedicated server + two clients, killing one client process) shows a
transport-level behaviour that prevents the *ideal immediate* reconnect at the M2 layer:

- On force-kill the server does **not** get a clean disconnect; the role only returns to a bot when
  the transport's protocol timeout fires (`DisconnectTimeoutMS`, set to 5 s).
- While the stale connection lingers, a reconnecting client's request **never reaches NGO
  connection approval** (`approval ENTER` never appears for it).
- In the reproduction, killing one client also caused the server to close the other client
  (`ClosedByRemote` at the client), and both were later reported `ProtocolTimeout`. This is a
  Unity/UTP connection-lifecycle behaviour, not something the M2 gameplay layer controls.
- A too-aggressive timeout (1.5 s) caused **false** disconnects of a healthy client, so the timeout
  is kept at a sane 5 s.
- **Decisive baseline (no kill):** with no client killed, both clients stayed connected for 35 s
  (`status listening=True connected=2`), so clients do not drop on their own. Killing one client
  then caused the server to close the **other** client within ~1 s (that client reported
  `ClosedByRemote`), before the killed client's own timeout fired; afterwards the server stopped
  accepting new connections. This is the concrete UTP/NGO connection-lifecycle behaviour to
  harden in M3.

**Classified (M3): package-level, not our harness.** A minimal standalone NGO/UTP reproduction with
no game code (no body, bots, tokens, approval, scene sync, conditioner or simulator) shows the same
behaviour. Evidence, versions and the preserved repro: `docs/NETWORKING_PROBE.md`. It is tracked as
a non-blocking networking-hardening item.

**Safe workaround adopted:** role ownership is decided at **connection approval** and is
token-based; a role is never left ownerless (it goes to a bot), and reclaim/duplicate-role handling
is unit-tested. The architecture is correct and proven on graceful disconnect; the force-kill
reconnect depends on UTP disconnect detection and connection acceptance, which must be hardened in
the production networking layer (dedicated-server connection lifecycle, session tokens, and an
explicit reconnect handshake) rather than in M2 gameplay code.

## 6. Conditioning notes

Transport-level simulation is verified and preferred; the application conditioner remains for
EditMode tests. The conditioner conditions only M2 messages and models no jitter/reorder/MTU.
