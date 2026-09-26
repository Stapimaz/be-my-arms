using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.M7
{
    /// <summary>
    /// Scene-0 entry point for the whole build. As a dedicated server process (launched with
    /// `-m3-role server -m7-arena &lt;scene&gt;`) it loads the requested arena; otherwise it shows the
    /// player-facing main menu.
    /// </summary>
    public class M7AppBootstrap : MonoBehaviour
    {
        void Awake()
        {
            if (M7PrivateMatch.IsServerLaunch(out string arenaScene))
            {
                int ownerPid = M7PrivateMatch.OwnerPidArg();
                if (ownerPid > 0)
                {
                    var watchdog = new GameObject("M7_ServerWatchdog");
                    watchdog.AddComponent<M7ServerWatchdog>().Configure(ownerPid);
                }
                Debug.Log($"[M7] launching dedicated server on arena '{arenaScene}' (ownerPid={ownerPid})");
                SceneManager.LoadScene(arenaScene, LoadSceneMode.Single);
                return;
            }

            // QA/automation entry: drives the exact same private-match flow as the lobby button,
            // launching the local dedicated server transparently. Used for headless verification.
            string qa = M7PrivateMatch.GetArg("-m7-qa-private");
            if (!string.IsNullOrEmpty(qa))
            {
                bool twoVsTwo = qa.Equals("2v2", System.StringComparison.OrdinalIgnoreCase);
                string role = M7PrivateMatch.GetArg("-m7-qa-role") ?? "p1";
                M7PrivateMatch.Begin(new M7MatchRequest
                {
                    Mode = twoVsTwo ? M7MapFamily.TwoVsTwo : M7MapFamily.Duel,
                    ArenaScene = twoVsTwo ? "M7TwoVsTwoArena" : "M7DuelArena",
                    Team = 0,
                    Body = 0,
                    Role = role.Equals("p2", System.StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                    Port = M7PrivateMatch.DefaultPort
                }, autoDrive: true);
                return;
            }

            EnsureCamera();
            gameObject.AddComponent<M7MenuController>();
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
