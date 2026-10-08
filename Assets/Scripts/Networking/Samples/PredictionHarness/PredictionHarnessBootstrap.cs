using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace BeMyArms.Networking
{
    public enum PredictionHarnessRole
    {
        Host,
        Server,
        Client
    }

    /// <summary>
    /// Starts the Networking spike and configures role + network conditioning from command line, so a
    /// dedicated server and separate P1 / P2 clients can run as independent processes:
    ///   -prediction-role server|client|host
    ///   -prediction-position p1|p2
    ///   -prediction-token &lt;id&gt;          (stable identity for reconnect)
    ///   -prediction-delay &lt;ms&gt;           (one-way)
    ///   -prediction-loss &lt;percent&gt;
    ///   -prediction-auto 0|1
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M2", "BeMyArms.M2", "M2Bootstrap")]
    public class PredictionHarnessBootstrap : MonoBehaviour
    {
        public NetworkManager Manager;
        public GameObject BodyPrefab;
        public PredictionHarnessRole Role = PredictionHarnessRole.Host;
        public ushort Port = 7778;

        void Start()
        {
            ParseArgs();

            NetworkManager manager = Manager != null ? Manager : NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogError("[Networking] No NetworkManager found.");
                return;
            }

            var transport = manager.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
                transport.DisconnectTimeoutMS = PredictionHarnessConfig.DisconnectTimeoutMs;
            }

            if (PredictionHarnessConfig.UseTransportSimulation) ApplyTransportSimulation(manager);

            manager.OnClientConnectedCallback += id => Debug.Log($"[Networking-trace] t={Time.realtimeSinceStartup:0.000} CLIENT connected (id={id})");
            manager.OnClientDisconnectCallback += id => Debug.Log($"[Networking-trace] t={Time.realtimeSinceStartup:0.000} CLIENT disconnected (id={id}) reason='{manager.DisconnectReason}'");

            if (Role != PredictionHarnessRole.Client) manager.OnServerStarted += OnServerStarted;

            var roleService = manager.gameObject.GetComponent<PredictionHarnessRoleService>();
            if (roleService == null) roleService = manager.gameObject.AddComponent<PredictionHarnessRoleService>();
            roleService.InstallServerHooks();

            // We spawn the body ourselves; NGO scene/prefab synchronisation on connect adds
            // connection-establishment failure modes we do not need (and which correlated with one
            // client's disconnect upsetting another).
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ForceSamePrefabs = false;

            switch (Role)
            {
                case PredictionHarnessRole.Server:
                    manager.StartServer();
                    break;
                case PredictionHarnessRole.Client:
                    manager.NetworkConfig.ConnectionData = PredictionHarnessRoleService.Encode(PredictionHarnessConfig.ClientToken, PredictionHarnessConfig.ClientRole);
                    manager.StartClient();
                    break;
                default:
                    manager.StartHost();
                    roleService.Registry.Assign(manager.LocalClientId, PredictionHarnessConfig.ClientToken, PredictionHarnessConfig.ClientRole, out _);
                    break;
            }

            Debug.Log($"[Networking] bootstrap role={Role} position={(PredictionHarnessConfig.ClientRole == PredictionHarnessBody.RoleP1 ? "P1" : "P2")} token='{PredictionHarnessConfig.ClientToken}' port={Port} delay={PredictionHarnessConfig.OneWayDelaySeconds * 1000:0}ms loss={PredictionHarnessConfig.LossPercent:0}% auto={PredictionHarnessConfig.AutoDrive}");
        }

        void OnServerStarted()
        {
            if (BodyPrefab == null)
            {
                Debug.LogError("[Networking] BodyPrefab not assigned.");
                return;
            }

            GameObject go = Instantiate(BodyPrefab);
            go.GetComponent<NetworkObject>().Spawn(true);
            Debug.Log("[Networking] body spawned");
        }

        void ParseArgs()
        {
            string role = GetArg("-prediction-role");
            if (!string.IsNullOrEmpty(role))
            {
                switch (role.ToLowerInvariant())
                {
                    case "server": Role = PredictionHarnessRole.Server; break;
                    case "client": Role = PredictionHarnessRole.Client; break;
                    default: Role = PredictionHarnessRole.Host; break;
                }
            }

            string position = GetArg("-prediction-position");
            PredictionHarnessConfig.ClientRole = !string.IsNullOrEmpty(position) && position.ToLowerInvariant() == "p2"
                ? PredictionHarnessBody.RoleP2
                : PredictionHarnessBody.RoleP1;

            PredictionHarnessConfig.ClientToken = GetArg("-prediction-token") ?? "";

            if (float.TryParse(GetArg("-prediction-delay"), out float delayMs))
                PredictionHarnessConfig.OneWayDelaySeconds = Mathf.Max(0f, delayMs) / 1000f;

            if (float.TryParse(GetArg("-prediction-loss"), out float loss))
                PredictionHarnessConfig.LossPercent = Mathf.Clamp(loss, 0f, 100f);

            if (float.TryParse(GetArg("-prediction-rewind"), out float rewindMs))
                PredictionHarnessConfig.LagRewindSeconds = Mathf.Max(0f, rewindMs) / 1000f;

            string auto = GetArg("-prediction-auto");
            if (!string.IsNullOrEmpty(auto)) PredictionHarnessConfig.AutoDrive = auto != "0";

            string wrong = GetArg("-prediction-wrongrole");
            if (!string.IsNullOrEmpty(wrong)) PredictionHarnessConfig.WrongRoleTest = wrong != "0";

            string tsim = GetArg("-prediction-transport-sim");
            if (!string.IsNullOrEmpty(tsim) && tsim != "0")
            {
                PredictionHarnessConfig.UseTransportSimulation = true;
                // Avoid double-conditioning: the transport simulator owns latency/loss.
                PredictionHarnessConfig.OneWayDelaySeconds = 0f;
                PredictionHarnessConfig.LossPercent = 0f;
            }

            if (float.TryParse(GetArg("-prediction-exit-after"), out float exitAfter))
                PredictionHarnessConfig.ExitAfterSeconds = exitAfter;

            if (int.TryParse(GetArg("-prediction-disconnect-timeout"), out int disconnectMs))
                PredictionHarnessConfig.DisconnectTimeoutMs = Mathf.Max(200, disconnectMs);
        }

        void Update()
        {
            if (PredictionHarnessConfig.ExitAfterSeconds > 0f && Time.realtimeSinceStartup >= PredictionHarnessConfig.ExitAfterSeconds)
            {
                Debug.Log($"[Networking-trace] t={Time.realtimeSinceStartup:0.000} CLIENT graceful shutdown requested");
                NetworkManager m = Manager != null ? Manager : NetworkManager.Singleton;
                if (m != null && m.IsListening) m.Shutdown();
                Application.Quit();
                return;
            }

            // Safety net: if this client is (unexpectedly) disconnected, retry the connection so the
            // session survives; the token reclaims the same role from the bot.
            if (Role != PredictionHarnessRole.Client || PredictionHarnessConfig.ExitAfterSeconds > 0f) return;
            NetworkManager manager = Manager != null ? Manager : NetworkManager.Singleton;
            if (manager == null) return;
            if (!manager.IsConnectedClient)
            {
                _reconnectTimer += Time.deltaTime;
                if (_reconnectTimer >= 2f)
                {
                    _reconnectTimer = 0f;
                    if (manager.IsListening) manager.Shutdown();
                    Debug.Log($"[Networking-trace] t={Time.realtimeSinceStartup:0.000} CLIENT reconnect attempt (token '{PredictionHarnessConfig.ClientToken}')");
                    manager.StartClient();
                }
            }
            else
            {
                _reconnectTimer = 0f;
            }
        }

        float _reconnectTimer;

        void ApplyTransportSimulation(NetworkManager manager)
        {
            var simulator = manager.gameObject.AddComponent<Unity.Multiplayer.Tools.NetworkSimulator.Runtime.NetworkSimulator>();
            simulator.ConnectionPreset = new Unity.Multiplayer.Tools.NetworkSimulator.Runtime.NetworkSimulatorPreset
            {
                Name = "m2",
                PacketDelayMs = PredictionHarnessConfig.TransportDelayMs,
                PacketJitterMs = 0,
                PacketLossInterval = 0,
                PacketLossPercent = PredictionHarnessConfig.TransportLossPercent
            };
            Debug.Log($"[Networking] transport simulator enabled: delay {PredictionHarnessConfig.TransportDelayMs}ms loss {PredictionHarnessConfig.TransportLossPercent}%");
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
