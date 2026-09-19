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

    /// <summary>Launches a local dedicated server process of this build (transparent to the user).</summary>
    public class M7LocalProcessAllocator : IM7MatchServerAllocator
    {
        Process _process;

        public bool IsAllocated => _process != null && !_process.HasExited;

        public void Allocate(M7MatchRequest request)
        {
            Shutdown();
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string logPath = Path.Combine(Application.persistentDataPath, "m7_private_server.log");
            string mode = request.Mode == M7MapFamily.TwoVsTwo ? "2v2" : "duel";
            string args = new StringBuilder()
                .Append("-batchmode -nographics ")
                .Append($"-logFile \"{logPath}\" ")
                .Append($"-m3-role server -m3-port {request.Port} ")
                .Append($"-m7-arena {request.ArenaScene} ")
                .Append($"-m4-mode {mode} -m4-matchmaker 0 ")
                .Append($"-m3-required-players {request.RequiredHumans} -m3-start-delay 30 ")
                .Append("-m3-exit-after 900")
                .ToString();

            _process = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true });
            UnityEngine.Debug.Log($"[M7] local dedicated server launched (mode={mode} arena={request.ArenaScene} port={request.Port})");
        }

        public void Shutdown()
        {
            try { if (_process != null && !_process.HasExited) _process.Kill(); }
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

        public static bool IsServerLaunch(out string arenaScene)
        {
            arenaScene = GetArg("-m7-arena");
            string role = GetArg("-m3-role");
            return !string.IsNullOrEmpty(arenaScene) && string.Equals(role, "server", StringComparison.OrdinalIgnoreCase);
        }

        public static void Begin(M7MatchRequest request, bool autoDrive = false)
        {
            if (Application.isEditor)
                UnityEngine.Debug.LogWarning("[M7] Private matches launch a dedicated server process; run a build to use this flow.");

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
            SceneManager.LoadScene(MenuScene);
        }

        public static void Shutdown()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening) manager.Shutdown();
            if (Allocator != null) Allocator.Shutdown();
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
