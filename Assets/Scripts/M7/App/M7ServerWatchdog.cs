using System.Diagnostics;
using UnityEngine;

namespace BeMyArms.M7
{
    /// <summary>
    /// Runs inside a locally launched dedicated server. It watches the owning client process and
    /// quits if that process disappears, so a hard client termination (crash, task kill, power loss)
    /// cannot leave an orphan server holding a port or preserving an old match for the next launch.
    /// </summary>
    public class M7ServerWatchdog : MonoBehaviour
    {
        int _ownerPid;
        float _nextCheck;
        bool _loggedFirst;

        public void Configure(int ownerPid)
        {
            _ownerPid = ownerPid;
            DontDestroyOnLoad(gameObject);
            UnityEngine.Debug.Log($"[M7] server watchdog armed (ownerPid={ownerPid})");
        }

        void Update()
        {
            if (_ownerPid <= 0) return;
            if (Time.realtimeSinceStartup < _nextCheck) return;
            _nextCheck = Time.realtimeSinceStartup + 1f;

            bool alive = OwnerAlive();
            if (!_loggedFirst)
            {
                _loggedFirst = true;
                UnityEngine.Debug.Log($"[M7] server watchdog running (ownerPid={_ownerPid} alive={alive})");
            }

            if (!alive)
            {
                UnityEngine.Debug.Log($"[M7] owner client (pid {_ownerPid}) is gone; terminating private server");
                Application.Quit();
                // Guarantee termination even if the graceful quit is ignored (batchmode/-nographics).
                try { Process.GetCurrentProcess().Kill(); } catch { /* best effort */ }
            }
        }

        static bool OwnerAlive(int pid)
        {
            try
            {
                // Mono's GetProcessById may not throw for an exited PID, so probe HasExited too.
                using (var process = Process.GetProcessById(pid))
                    return !process.HasExited;
            }
            catch { return false; }
        }

        bool OwnerAlive() => OwnerAlive(_ownerPid);
    }
}
