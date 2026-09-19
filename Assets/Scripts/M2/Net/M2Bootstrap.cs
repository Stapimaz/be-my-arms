using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace BeMyArms.M2
{
    public enum M2Role
    {
        Host,
        Server,
        Client
    }

    /// <summary>
    /// Starts the M2 spike and configures role + network conditioning from command line, so a
    /// dedicated server and separate P1 / P2 clients can run as independent processes:
    ///   -m2-role server|client|host
    ///   -m2-position p1|p2
    ///   -m2-token &lt;id&gt;          (stable identity for reconnect)
    ///   -m2-delay &lt;ms&gt;           (one-way)
    ///   -m2-loss &lt;percent&gt;
    ///   -m2-auto 0|1
    /// </summary>
    public class M2Bootstrap : MonoBehaviour
    {
        public NetworkManager Manager;
        public GameObject BodyPrefab;
        public M2Role Role = M2Role.Host;
        public ushort Port = 7778;

        void Start()
        {
            ParseArgs();

            NetworkManager manager = Manager != null ? Manager : NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogError("[M2] No NetworkManager found.");
                return;
            }

            var transport = manager.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
                transport.DisconnectTimeoutMS = M2Config.DisconnectTimeoutMs;
            }

            if (M2Config.UseTransportSimulation) ApplyTransportSimulation(manager);

            manager.OnClientConnectedCallback += id => Debug.Log($"[M2-trace] t={Time.realtimeSinceStartup:0.000} CLIENT connected (id={id})");
            manager.OnClientDisconnectCallback += id => Debug.Log($"[M2-trace] t={Time.realtimeSinceStartup:0.000} CLIENT disconnected (id={id}) reason='{manager.DisconnectReason}'");

            if (Role != M2Role.Client) manager.OnServerStarted += OnServerStarted;

            var roleService = manager.gameObject.GetComponent<M2RoleService>();
            if (roleService == null) roleService = manager.gameObject.AddComponent<M2RoleService>();
            roleService.InstallServerHooks();

            // We spawn the body ourselves; NGO scene/prefab synchronisation on connect adds
            // connection-establishment failure modes we do not need (and which correlated with one
            // client's disconnect upsetting another).
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ForceSamePrefabs = false;

            switch (Role)
            {
                case M2Role.Server:
                    manager.StartServer();
                    break;
                case M2Role.Client:
                    manager.NetworkConfig.ConnectionData = M2RoleService.Encode(M2Config.ClientToken, M2Config.ClientRole);
                    manager.StartClient();
                    break;
                default:
                    manager.StartHost();
                    roleService.Registry.Assign(manager.LocalClientId, M2Config.ClientToken, M2Config.ClientRole, out _);
                    break;
            }

            Debug.Log($"[M2] bootstrap role={Role} position={(M2Config.ClientRole == M2NetworkBody.RoleP1 ? "P1" : "P2")} token='{M2Config.ClientToken}' port={Port} delay={M2Config.OneWayDelaySeconds * 1000:0}ms loss={M2Config.LossPercent:0}% auto={M2Config.AutoDrive}");
        }

        void OnServerStarted()
        {
            if (BodyPrefab == null)
            {
                Debug.LogError("[M2] BodyPrefab not assigned.");
                return;
            }

            GameObject go = Instantiate(BodyPrefab);
            go.GetComponent<NetworkObject>().Spawn(true);
            Debug.Log("[M2] body spawned");
        }

        void ParseArgs()
        {
            string role = GetArg("-m2-role");
            if (!string.IsNullOrEmpty(role))
            {
                switch (role.ToLowerInvariant())
                {
                    case "server": Role = M2Role.Server; break;
                    case "client": Role = M2Role.Client; break;
                    default: Role = M2Role.Host; break;
                }
            }

            string position = GetArg("-m2-position");
            M2Config.ClientRole = !string.IsNullOrEmpty(position) && position.ToLowerInvariant() == "p2"
                ? M2NetworkBody.RoleP2
                : M2NetworkBody.RoleP1;

            M2Config.ClientToken = GetArg("-m2-token") ?? "";

            if (float.TryParse(GetArg("-m2-delay"), out float delayMs))
                M2Config.OneWayDelaySeconds = Mathf.Max(0f, delayMs) / 1000f;

            if (float.TryParse(GetArg("-m2-loss"), out float loss))
                M2Config.LossPercent = Mathf.Clamp(loss, 0f, 100f);

            if (float.TryParse(GetArg("-m2-rewind"), out float rewindMs))
                M2Config.LagRewindSeconds = Mathf.Max(0f, rewindMs) / 1000f;

            string auto = GetArg("-m2-auto");
            if (!string.IsNullOrEmpty(auto)) M2Config.AutoDrive = auto != "0";

            string wrong = GetArg("-m2-wrongrole");
            if (!string.IsNullOrEmpty(wrong)) M2Config.WrongRoleTest = wrong != "0";

            string tsim = GetArg("-m2-transport-sim");
            if (!string.IsNullOrEmpty(tsim) && tsim != "0")
            {
                M2Config.UseTransportSimulation = true;
                // Avoid double-conditioning: the transport simulator owns latency/loss.
                M2Config.OneWayDelaySeconds = 0f;
                M2Config.LossPercent = 0f;
            }

            if (float.TryParse(GetArg("-m2-exit-after"), out float exitAfter))
                M2Config.ExitAfterSeconds = exitAfter;

            if (int.TryParse(GetArg("-m2-disconnect-timeout"), out int disconnectMs))
                M2Config.DisconnectTimeoutMs = Mathf.Max(200, disconnectMs);
        }

        void Update()
        {
            if (M2Config.ExitAfterSeconds > 0f && Time.realtimeSinceStartup >= M2Config.ExitAfterSeconds)
            {
                Debug.Log($"[M2-trace] t={Time.realtimeSinceStartup:0.000} CLIENT graceful shutdown requested");
                NetworkManager m = Manager != null ? Manager : NetworkManager.Singleton;
                if (m != null && m.IsListening) m.Shutdown();
                Application.Quit();
                return;
            }

            // Safety net: if this client is (unexpectedly) disconnected, retry the connection so the
            // session survives; the token reclaims the same role from the bot.
            if (Role != M2Role.Client || M2Config.ExitAfterSeconds > 0f) return;
            NetworkManager manager = Manager != null ? Manager : NetworkManager.Singleton;
            if (manager == null) return;
            if (!manager.IsConnectedClient)
            {
                _reconnectTimer += Time.deltaTime;
                if (_reconnectTimer >= 2f)
                {
                    _reconnectTimer = 0f;
                    if (manager.IsListening) manager.Shutdown();
                    Debug.Log($"[M2-trace] t={Time.realtimeSinceStartup:0.000} CLIENT reconnect attempt (token '{M2Config.ClientToken}')");
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
                PacketDelayMs = M2Config.TransportDelayMs,
                PacketJitterMs = 0,
                PacketLossInterval = 0,
                PacketLossPercent = M2Config.TransportLossPercent
            };
            Debug.Log($"[M2] transport simulator enabled: delay {M2Config.TransportDelayMs}ms loss {M2Config.TransportLossPercent}%");
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
