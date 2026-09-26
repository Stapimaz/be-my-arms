using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using BeMyArms.M3;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.M7
{
    /// <summary>
    /// Provider-neutral server allocation seam. The local development implementation launches a
    /// dedicated server process of this same build; a production allocator (backend/hosting) can
    /// replace it without touching the lobby or match code.
    /// </summary>
    public interface IM7MatchServerAllocator
    {
        void Allocate(M7MatchRequest request);
        void Shutdown();
        bool IsAllocated { get; }
    }

    /// <summary>
    /// Launches a local dedicated server process of this build (transparent to the user). The server
    /// is given the owning client's PID so it can shut itself down if the client dies without a clean
    /// quit, and the client kills it explicitly on shutdown.
    /// </summary>
    public class M7LocalProcessAllocator : IM7MatchServerAllocator
    {
        Process _process;

        public bool IsAllocated => _process != null && !_process.HasExited;

        public void Allocate(M7MatchRequest request)
        {
            Shutdown();

            int ownerPid = 0;
            try { ownerPid = Process.GetCurrentProcess().Id; }
            catch { /* best effort */ }

            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string logPath = Path.Combine(Application.persistentDataPath, "m7_private_server.log");
            string mode = request.Mode == M7MapFamily.TwoVsTwo ? "2v2" : "duel";
            string args = new StringBuilder()
                .Append("-batchmode -nographics ")
                .Append($"-logFile \"{logPath}\" ")
                .Append($"-m7-owner-pid {ownerPid} ")
                .Append($"-m3-role server -m3-port {request.Port} ")
                .Append($"-m7-arena {request.ArenaScene} ")
                .Append($"-m4-mode {mode} -m4-matchmaker 0 ")
                .Append($"-m3-required-players {request.RequiredHumans} -m3-start-delay 30 ")
                .Append("-m3-exit-after 1800")
                .ToString();

            _process = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true });
            UnityEngine.Debug.Log($"[M7] local dedicated server launched (mode={mode} arena={request.ArenaScene} port={request.Port} ownerPid={ownerPid})");
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
            catch (Exception e) { UnityEngine.Debug.LogWarning("[M7] server shutdown: " + e.Message); }
            _process = null;
        }
    }

    /// <summary>
    /// The private/custom match flow: a normal client chooses mode + role, the allocator brings up a
    /// dedicated server, and the client connects and plays the real server-authoritative match.
    /// </summary>
    public static class M7PrivateMatch
    {
        public const ushort DefaultPort = 7780;
        public const string MenuScene = "M7MainMenu";

        /// <summary>Replace with the production allocator later; gameplay/lobby code does not change.</summary>
        public static IM7MatchServerAllocator Allocator = new M7LocalProcessAllocator();

        public static M7MatchRequest Current;
        public static bool InMatch { get; private set; }

        static bool _quitHooked;

        public static bool IsServerLaunch(out string arenaScene)
        {
            arenaScene = GetArg("-m7-arena");
            string role = GetArg("-m3-role");
            return !string.IsNullOrEmpty(arenaScene) && string.Equals(role, "server", StringComparison.OrdinalIgnoreCase);
        }

        public static int OwnerPidArg()
        {
            string value = GetArg("-m7-owner-pid");
            return int.TryParse(value, out int pid) ? pid : 0;
        }

        public static void Begin(M7MatchRequest request, bool autoDrive = false)
        {
            if (Application.isEditor)
                UnityEngine.Debug.LogWarning("[M7] Private matches launch a dedicated server process; run a build to use this flow.");

            EnsureQuitHook();

            // Start every match from a genuinely clean session: stop any previous manager, kill any
            // previous server, clear statics and remove leftover networked objects.
            Shutdown();
            ResetSessionState();

            // A fresh free port per match means an orphaned previous server can never make the new
            // client connect to an old match or fail to bind.
            request.Port = PickFreePort();

            Current = request;
            InMatch = true;

            M3Config.AutoStartClient = true;
            M3Config.PortOverride = request.Port;
            M3Config.ClientToken = "local-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            M3Config.ClientTeam = request.Team;
            M3Config.ClientBody = request.Body;
            M3Config.ClientRole = request.Role;
            M3Config.BodiesPerTeam = request.BodiesPerTeam;
            M3Config.ExpectedPlayers = request.BodiesPerTeam * 4;
            M3Config.RequiredPlayers = request.RequiredHumans;
            M3Config.AutoDrive = autoDrive;
            M3Config.AutoBuy = autoDrive;
            M3Config.AutoFire = autoDrive;
            M3Config.AutoUtility = autoDrive;

            if (Allocator != null) Allocator.Allocate(request);
            SceneManager.LoadScene(request.ArenaScene);
        }

        public static void ReturnToMenu()
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

        static void OnApplicationQuitting()
        {
            // A normal quit (including Alt+F4 on Windows) must not leave an orphan private server.
            // A hard kill is covered by the server's owner-PID watchdog.
            try { if (Allocator != null) Allocator.Shutdown(); }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[M7] quit shutdown: " + e.Message); }
        }

        /// <summary>
        /// Clears per-session statics and destroys stray networked objects, so a match started after
        /// leaving a previous one cannot inherit the old local slot, director/roster or bodies.
        /// A client must never call NetworkObject.Despawn (that is a server-only operation); after the
        /// manager shutdown the remaining objects are simply destroyed locally.
        /// </summary>
        public static void ResetSessionState()
        {
            M3DuelClient.SetLocalSlot(-1);
            M3DuelDirector.ResetStatics();
            M3DuelRoleService.ResetStatics();
            M3LocalInput.GameplayActive = false;
            M3LocalInput.CursorCaptured = false;

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
}
