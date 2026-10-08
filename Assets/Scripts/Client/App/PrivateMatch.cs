using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using BeMyArms.Match;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.Client
{
    /// <summary>
    /// Provider-neutral server allocation seam. The local development implementation launches a
    /// dedicated server process of this same build; a production allocator (backend/hosting) can
    /// replace it without touching the lobby or match code.
    /// </summary>
    public interface IMatchServerAllocator
    {
        void Allocate(MatchRequest request);
        void Shutdown();
        bool IsAllocated { get; }
    }

    /// <summary>
    /// Launches a local dedicated server process of this build (transparent to the user). The server
    /// is given the owning client's PID so it can shut itself down if the client dies without a clean
    /// quit, and the client kills it explicitly on shutdown.
    /// </summary>
    public class LocalProcessAllocator : IMatchServerAllocator
    {
        Process _process;

        public bool IsAllocated => _process != null && !_process.HasExited;

        public void Allocate(MatchRequest request)
        {
            Shutdown();

            int ownerPid = 0;
            try { ownerPid = Process.GetCurrentProcess().Id; }
            catch { /* best effort */ }

            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string logPath = Path.Combine(Application.persistentDataPath, "private-server.log");
            string mode = request.Mode == MapFamily.TwoVsTwo ? "2v2" : "duel";
            string args = new StringBuilder()
                .Append("-batchmode -nographics ")
                .Append($"-logFile \"{logPath}\" ")
                .Append($"-client-owner-pid {ownerPid} ")
                .Append($"-match-role server -match-port {request.Port} ")
                .Append($"-client-arena {request.ArenaScene} ")
                .Append($"-queue-mode {mode} -queue-matchmaker 0 ")
                .Append($"-match-bot-difficulty {(request.BotDifficulty == BotDifficulty.Hard ? "hard" : "easy")} ")
                .Append($"-match-required-players {request.RequiredHumans} -match-start-delay 0 ")
                .Append("-match-practice 1 -match-strict-slots 1 -match-delay 0 -match-loss 0 -match-buy 3 ")
                .ToString();

            _process = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true });
            UnityEngine.Debug.Log($"[Client] local dedicated server launched (mode={mode} arena={request.ArenaScene} port={request.Port} ownerPid={ownerPid})");
        }

        public void Shutdown()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(2000);
                }
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[Client] server shutdown: " + e.Message); }
            _process = null;
        }
    }

    /// <summary>
    /// The private/custom match flow: a normal client chooses mode + role, the allocator brings up a
    /// dedicated server, and the client connects and plays the real server-authoritative match.
    /// </summary>
    public static class PrivateMatch
    {
        public const ushort DefaultPort = 7780;
        public const string MenuScene = "MainMenu";

        /// <summary>Replace with the production allocator later; gameplay/lobby code does not change.</summary>
        public static IMatchServerAllocator Allocator = new LocalProcessAllocator();

        public static MatchRequest Current;
        public static bool InMatch { get; private set; }

        static bool _quitHooked;

        public static bool IsServerLaunch(out string arenaScene)
        {
            arenaScene = GetArg("-client-arena");
            string role = GetArg("-match-role");
            return !string.IsNullOrEmpty(arenaScene) && string.Equals(role, "server", StringComparison.OrdinalIgnoreCase);
        }

        public static int OwnerPidArg()
        {
            string value = GetArg("-client-owner-pid");
            return int.TryParse(value, out int pid) ? pid : 0;
        }

        public static void Begin(MatchRequest request, bool autoDrive = false)
        {
            if (Application.isEditor)
                UnityEngine.Debug.LogWarning("[Client] Private matches launch a dedicated server process; run a build to use this flow.");

            EnsureQuitHook();

            // Start every match from a genuinely clean session: stop any previous manager, kill any
            // previous server, clear statics and remove leftover networked objects.
            Shutdown();
            ResetSessionState();

            // A fresh free port per match means an orphaned previous server can never make the new
            // client connect to an old match or fail to bind.
            if (!request.JoinExisting) request.Port = PickFreePort();

            Current = request;
            InMatch = true;

            MatchConfig.AutoStartClient = true;
            MatchConfig.UseMatchmaker = false;
            MatchConfig.PrivatePractice = MatchConfig.StrictSlots = true;
            MatchConfig.ServerAddress = request.JoinExisting ? request.Address : "127.0.0.1";
            MatchConfig.OneWayDelaySeconds = MatchConfig.LossPercent = 0f;
            MatchConfig.BuySeconds = 3f;
            MatchConfig.PortOverride = request.Port;
            MatchConfig.ClientToken = "local-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            MatchConfig.ClientTeam = request.Team;
            MatchConfig.ClientBody = request.Body;
            MatchConfig.ClientRole = request.Role;
            MatchConfig.BodiesPerTeam = request.BodiesPerTeam;
            MatchConfig.ExpectedPlayers = request.BodiesPerTeam * 4;
            MatchConfig.RequiredPlayers = request.RequiredHumans;
            MatchConfig.AutoDrive = autoDrive;
            MatchConfig.AutoBuy = autoDrive;
            MatchConfig.AutoFire = autoDrive;
            MatchConfig.AutoUtility = autoDrive;
            // Bot-filled private matches are a practice/playtest surface: default to Easy.
            MatchConfig.BotDifficulty = request.BotDifficulty;

            if (!request.JoinExisting && Allocator != null) Allocator.Allocate(request);
            SceneManager.LoadScene(request.ArenaScene);
        }

        public static void ReturnToMenu()
        {
            if (InMatch && Application.isPlaying && MatchDirector.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                InMatch = false;
                LocalInput.Reset();
                MatchDirector.Instance.LeavePracticeServerRpc();
                var flow = new GameObject("Client_GracefulLeave").AddComponent<GracefulLeave>();
                flow.StartCoroutine(flow.Leave());
                return;
            }
            CompleteReturnToMenu();
        }

        internal static void CompleteReturnToMenu()
        {
            InMatch = false;
            Shutdown();
            ResetSessionState();
            SceneManager.LoadScene(MenuScene);
        }

        public static void Shutdown()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening) manager.Shutdown();
            if (Allocator != null) Allocator.Shutdown();
        }

        static void EnsureQuitHook()
        {
            if (_quitHooked) return;
            _quitHooked = true;
            Application.quitting += OnApplicationQuitting;
        }

        public static string ConnectionLabel => InMatch ? $"{MatchConfig.ServerAddress}:{Current.Port}" : "";

        public static void LaunchLocalPartner()
        {
            if (!InMatch || Current.JoinExisting || Application.isEditor) return;
            int role = NetworkBodyClient.LocalSlotIndex >= 0 ? MatchSlots.RoleOf(NetworkBodyClient.LocalSlotIndex) : Current.Role;
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            Process.Start(new ProcessStartInfo(exe,
                $"-client-join 127.0.0.1 -client-port {Current.Port} -client-join-role {(role == 0 ? "p2" : "p1")}")
                { UseShellExecute = false });
        }

        static void OnApplicationQuitting()
        {
            // A normal quit (including Alt+F4 on Windows) must not leave an orphan private server.
            // A hard kill is covered by the server's owner-PID watchdog.
            try { if (Allocator != null) Allocator.Shutdown(); }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[Client] quit shutdown: " + e.Message); }
        }

        /// <summary>
        /// Clears per-session statics and destroys stray networked objects, so a match started after
        /// leaving a previous one cannot inherit the old local slot, director/roster or bodies.
        /// A client must never call NetworkObject.Despawn (that is a server-only operation); after the
        /// manager shutdown the remaining objects are simply destroyed locally.
        /// </summary>
        public static void ResetSessionState()
        {
            NetworkBodyClient.SetLocalSlot(-1);
            MatchDirector.ResetStatics();
            MatchRoleService.ResetStatics();
            LocalInput.Reset();
            MatchConfig.AutoStartClient = false;
            MatchConfig.PortOverride = 0;
            MatchConfig.ServerAddress = "127.0.0.1";
            MatchConfig.PrivatePractice = MatchConfig.StrictSlots = false;

            NetworkManager[] managers = UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
                if (managers[i] != null && managers[i].IsListening) managers[i].Shutdown();

            NetworkObject[] networkObjects = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < networkObjects.Length; i++)
                if (networkObjects[i] != null) DestroyObject(networkObjects[i].gameObject);

            for (int i = 0; i < managers.Length; i++)
                if (managers[i] != null) DestroyObject(managers[i].gameObject);
        }

        /// <summary>Picks an unused local UDP port for this match.</summary>
        public static ushort PickFreePort()
        {
            try
            {
                var udp = new System.Net.Sockets.UdpClient(0);
                int port = ((System.Net.IPEndPoint)udp.Client.LocalEndPoint).Port;
                udp.Close();
                return (ushort)port;
            }
            catch
            {
                return DefaultPort;
            }
        }

        static void DestroyObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        public static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }

    /// <summary>Give the reliable voluntary-leave RPC time to flush before shutting down transport.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7GracefulLeave")]
    public sealed class GracefulLeave : MonoBehaviour
    {
        public System.Collections.IEnumerator Leave()
        {
            yield return new WaitForSecondsRealtime(0.2f);
            PrivateMatch.CompleteReturnToMenu();
            Destroy(gameObject);
        }
    }
}
