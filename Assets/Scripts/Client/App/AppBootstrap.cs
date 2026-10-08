using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.Client
{
    /// <summary>
    /// Scene-0 entry point for the whole build. As a dedicated server process (launched with
    /// `-match-role server -client-arena &lt;scene&gt;`) it loads the requested arena; otherwise it shows the
    /// player-facing main menu.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7AppBootstrap")]
    public class AppBootstrap : MonoBehaviour
    {
        static bool _clientEntryConsumed;
        void Awake()
        {
            if (PrivateMatch.IsServerLaunch(out string arenaScene))
            {
                int ownerPid = PrivateMatch.OwnerPidArg();
                if (ownerPid > 0)
                {
                    var watchdog = new GameObject("Client_ServerWatchdog");
                    watchdog.AddComponent<ServerWatchdog>().Configure(ownerPid);
                }
                Debug.Log($"[Client] launching dedicated server on arena '{arenaScene}' (ownerPid={ownerPid})");
                SceneManager.LoadScene(arenaScene, LoadSceneMode.Single);
                return;
            }

            // Launch arguments are a process entry, not a command to re-enter when returning to menu.
            if (_clientEntryConsumed) { ShowMenu(); return; }
            _clientEntryConsumed = true;
            string join = PrivateMatch.GetArg("-client-join");
            if (!string.IsNullOrEmpty(join) && ushort.TryParse(PrivateMatch.GetArg("-client-port"), out ushort port))
            {
                PrivateMatch.Begin(new MatchRequest
                {
                    Mode = MapFamily.Duel, ArenaScene = "DuelArena", Duo = true,
                    JoinExisting = true, Address = join, Port = port,
                    Role = PrivateMatch.GetArg("-client-join-role") == "p2" ? 1 : 0
                });
                return;
            }

            // QA/automation entry: drives the exact same private-match flow as the lobby button,
            // launching the local dedicated server transparently. Used for headless verification.
            string qa = PrivateMatch.GetArg("-client-qa-private");
            if (!string.IsNullOrEmpty(qa))
            {
                bool twoVsTwo = qa.Equals("2v2", System.StringComparison.OrdinalIgnoreCase);
                string role = PrivateMatch.GetArg("-client-qa-role") ?? "p1";
                PrivateMatch.Begin(new MatchRequest
                {
                    Mode = twoVsTwo ? MapFamily.TwoVsTwo : MapFamily.Duel,
                    ArenaScene = twoVsTwo ? "TwoVsTwoArena" : "DuelArena",
                    Team = 0,
                    Body = 0,
                    Role = role.Equals("p2", System.StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                    Port = PrivateMatch.DefaultPort
                }, autoDrive: true);
                return;
            }

            ShowMenu();
        }

        void ShowMenu()
        {
            EnsureCamera();
            gameObject.AddComponent<MenuController>();
        }

        static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("MenuCamera");
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            camera.tag = "MainCamera";
        }
    }
}
