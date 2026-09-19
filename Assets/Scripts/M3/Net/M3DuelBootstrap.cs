using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace BeMyArms.M3
{
    public enum M3DuelRole
    {
        Host,
        Server,
        Client
    }

    /// <summary>
    /// Starts a match and configures role + round timing from the command line, so a dedicated server
    /// and several role clients can run as independent processes:
    ///   -m3-role server|client|host
    ///   -m3-team a|b  -m3-body 0|1  -m3-position p1|p2|either
    ///   -m3-token &lt;id&gt;   -m3-delay &lt;ms&gt;   -m3-loss &lt;percent&gt;
    ///   -m3-buy &lt;s&gt; -m3-live &lt;s&gt; -m3-roundend &lt;s&gt;
    ///   -m3-zone-start/-end/-close/-duration/-dps
    ///   -m3-auto 0|1  -m3-auto-buy 0|1  -m3-auto-fire 0|1  -m3-auto-utility 0|1
    ///   -m3-start-delay &lt;s&gt; -m3-required-players &lt;n&gt; -m3-exit-after &lt;s&gt; -m3-port &lt;p&gt;
    ///   -m4-mode duel|2v2   -m4-matchmaker 0|1   -m4-mmr token=value,token=value
    /// </summary>
    public class M3DuelBootstrap : MonoBehaviour
    {
        public NetworkManager Manager;
        public GameObject DirectorPrefab;
        public M3DuelRole Role = M3DuelRole.Host;
        public ushort Port = 7779;

        float _reconnectTimer;

        void Start()
        {
            ParseArgs();

            // Private-match flow: a scene bootstrap with no CLI role connects as a client.
            if (Role == M3DuelRole.Host && M3Config.AutoStartClient) Role = M3DuelRole.Client;

            NetworkManager manager = Manager != null ? Manager : NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogError("[M3] No NetworkManager found.");
                return;
            }

            ushort effectivePort = M3Config.PortOverride != 0 ? M3Config.PortOverride : Port;
            var transport = manager.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("127.0.0.1", effectivePort, "0.0.0.0");
                transport.DisconnectTimeoutMS = M3Config.DisconnectTimeoutMs;
            }

            manager.OnClientConnectedCallback += id => Debug.Log($"[M3-trace] t={Time.realtimeSinceStartup:0.000} CLIENT connected (id={id})");
            manager.OnClientDisconnectCallback += id => Debug.Log($"[M3-trace] t={Time.realtimeSinceStartup:0.000} CLIENT disconnected (id={id}) reason='{manager.DisconnectReason}'");
            manager.OnServerStarted += OnServerStarted;

            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ForceSamePrefabs = false;

            var roleService = manager.gameObject.GetComponent<M3DuelRoleService>();
            if (roleService == null) roleService = manager.gameObject.AddComponent<M3DuelRoleService>();
            roleService.InstallServerHooks();

            switch (Role)
            {
                case M3DuelRole.Server:
                    manager.StartServer();
                    break;
                case M3DuelRole.Client:
                    manager.NetworkConfig.ConnectionData = M3DuelRoleService.Encode(M3Config.ClientToken, DesiredPayload());
                    manager.StartClient();
                    break;
                default:
                    manager.StartHost();
                    if (M3Config.UseMatchmaker)
                    {
                        roleService.Registry.Enqueue(manager.LocalClientId, M3Config.ClientToken, M3Config.ClientPreference, Time.realtimeSinceStartup, "local");
                    }
                    else
                    {
                        int hostSlot = M3DuelSlots.Encode(M3Config.ClientTeam, M3Config.ClientBody, M3Config.ClientRole, M3Config.BodiesPerTeam);
                        roleService.Registry.AssignPreferred(manager.LocalClientId, M3Config.ClientToken, hostSlot, out _);
                        M3DuelClient.SetLocalSlot(hostSlot);
                    }
                    break;
            }

            Debug.Log($"[M3] bootstrap role={Role} mode={(M3Config.BodiesPerTeam == 2 ? "2v2" : "duel")} matchmaker={M3Config.UseMatchmaker} " +
                      $"token='{M3Config.ClientToken}' port={Port} delay={M3Config.OneWayDelaySeconds * 1000:0}ms loss={M3Config.LossPercent:0}% " +
                      $"auto={M3Config.AutoDrive} buy={M3Config.BuySeconds:0}s live={M3Config.LiveSeconds:0}s");
        }

        void OnServerStarted()
        {
            if (Role == M3DuelRole.Client) return;
            if (DirectorPrefab == null)
            {
                Debug.LogError("[M3] DirectorPrefab not assigned.");
                return;
            }

            GameObject director = Instantiate(DirectorPrefab);
            director.GetComponent<NetworkObject>().Spawn(true);
            Debug.Log("[M3] match director spawned");
        }

        void Update()
        {
            NetworkManager manager = Manager != null ? Manager : NetworkManager.Singleton;
            if (manager == null) return;

            if (M3Config.ExitAfterSeconds > 0f && Time.realtimeSinceStartup >= M3Config.ExitAfterSeconds)
            {
                Debug.Log($"[M3-trace] t={Time.realtimeSinceStartup:0.000} graceful shutdown requested");
                if (manager.IsListening) manager.Shutdown();
                Application.Quit();
                return;
            }

            if (Role != M3DuelRole.Client || M3Config.ExitAfterSeconds > 0f) return;
            if (!manager.IsConnectedClient)
            {
                _reconnectTimer += Time.deltaTime;
                if (_reconnectTimer >= 2f)
                {
                    _reconnectTimer = 0f;
                    if (manager.IsListening) manager.Shutdown();
                    Debug.Log($"[M3-trace] t={Time.realtimeSinceStartup:0.000} CLIENT reconnect attempt (token '{M3Config.ClientToken}')");
                    manager.StartClient();
                }
            }
            else
            {
                _reconnectTimer = 0f;
            }
        }

        byte DesiredPayload()
        {
            if (M3Config.UseMatchmaker) return (byte)Mathf.Clamp(M3Config.ClientPreference, 0, 2);
            int slot = M3DuelSlots.Encode(M3Config.ClientTeam, M3Config.ClientBody, M3Config.ClientRole, M3Config.BodiesPerTeam);
            return (byte)slot;
        }

        void ParseArgs()
        {
            string role = GetArg("-m3-role");
            if (!string.IsNullOrEmpty(role))
            {
                switch (role.ToLowerInvariant())
                {
                    case "server": Role = M3DuelRole.Server; break;
                    case "client": Role = M3DuelRole.Client; break;
                    default: Role = M3DuelRole.Host; break;
                }
            }

            // Match shape first: Duel (1 body/team, 4 players) or 2v2 (2 bodies/team, 8 players).
            string mode = GetArg("-m4-mode");
            if (!string.IsNullOrEmpty(mode) && mode.ToLowerInvariant() == "2v2")
            {
                M3Config.BodiesPerTeam = 2;
                M3Config.ExpectedPlayers = 8;
            }
            else if (!string.IsNullOrEmpty(mode))
            {
                M3Config.BodiesPerTeam = 1;
                M3Config.ExpectedPlayers = 4;
            }
            if (int.TryParse(GetArg("-m4-bodies"), out int bodies)) M3Config.BodiesPerTeam = Mathf.Clamp(bodies, 1, 2);

            string matchmaker = GetArg("-m4-matchmaker");
            if (!string.IsNullOrEmpty(matchmaker)) M3Config.UseMatchmaker = matchmaker != "0";

            if (int.TryParse(GetArg("-m4-required"), out int requiredPlayers)) M3Config.RequiredPlayers = Mathf.Clamp(requiredPlayers, 1, 8);

            ParseMmr(GetArg("-m4-mmr"));
            ParseParties(GetArg("-m4-party"));

            string team = GetArg("-m3-team");
            if (!string.IsNullOrEmpty(team)) M3Config.ClientTeam = team.ToLowerInvariant() == "b" ? 1 : 0;

            string body = GetArg("-m3-body");
            if (!string.IsNullOrEmpty(body)) M3Config.ClientBody = Mathf.Clamp(int.TryParse(body, out int b) ? b : (body.ToLowerInvariant() == "b1" ? 1 : 0), 0, 1);

            string position = GetArg("-m3-position");
            if (!string.IsNullOrEmpty(position))
            {
                string p = position.ToLowerInvariant();
                if (p == "either") { M3Config.ClientPreference = 2; }
                else if (p == "p2") { M3Config.ClientRole = 1; M3Config.ClientPreference = 1; }
                else { M3Config.ClientRole = 0; M3Config.ClientPreference = 0; }
            }

            M3Config.ClientToken = GetArg("-m3-token") ?? "";

            if (float.TryParse(GetArg("-m3-delay"), out float delayMs)) M3Config.OneWayDelaySeconds = Mathf.Max(0f, delayMs) / 1000f;
            if (float.TryParse(GetArg("-m3-loss"), out float loss)) M3Config.LossPercent = Mathf.Clamp(loss, 0f, 100f);
            if (float.TryParse(GetArg("-m3-rewind"), out float rewindMs)) M3Config.LagRewindSeconds = Mathf.Max(0f, rewindMs) / 1000f;

            if (float.TryParse(GetArg("-m3-buy"), out float buy)) M3Config.BuySeconds = Mathf.Max(0.5f, buy);
            if (float.TryParse(GetArg("-m3-live"), out float live)) M3Config.LiveSeconds = Mathf.Max(1f, live);
            if (float.TryParse(GetArg("-m3-roundend"), out float roundEnd)) M3Config.RoundEndSeconds = Mathf.Max(0.5f, roundEnd);

            if (float.TryParse(GetArg("-m3-zone-start"), out float zStart)) M3Config.ZoneStartRadius = zStart;
            if (float.TryParse(GetArg("-m3-zone-end"), out float zEnd)) M3Config.ZoneEndRadius = zEnd;
            if (float.TryParse(GetArg("-m3-zone-close"), out float zClose)) M3Config.ZoneCloseStart = zClose;
            if (float.TryParse(GetArg("-m3-zone-duration"), out float zDuration)) M3Config.ZoneCloseDuration = zDuration;
            if (float.TryParse(GetArg("-m3-zone-dps"), out float zDps)) M3Config.ZoneDamagePerSecond = zDps;

            string auto = GetArg("-m3-auto");
            if (!string.IsNullOrEmpty(auto)) M3Config.AutoDrive = auto != "0";
            string autoBuy = GetArg("-m3-auto-buy");
            if (!string.IsNullOrEmpty(autoBuy)) M3Config.AutoBuy = autoBuy != "0";
            string autoFire = GetArg("-m3-auto-fire");
            if (!string.IsNullOrEmpty(autoFire)) M3Config.AutoFire = autoFire != "0";
            string autoUtility = GetArg("-m3-auto-utility");
            if (!string.IsNullOrEmpty(autoUtility)) M3Config.AutoUtility = autoUtility != "0";

            if (float.TryParse(GetArg("-m3-start-delay"), out float startDelay)) M3Config.StartDelaySeconds = Mathf.Max(0f, startDelay);
            if (int.TryParse(GetArg("-m3-required-players"), out int required)) M3Config.RequiredPlayers = Mathf.Clamp(required, 1, 8);
            if (float.TryParse(GetArg("-m3-exit-after"), out float exitAfter)) M3Config.ExitAfterSeconds = exitAfter;
            if (ushort.TryParse(GetArg("-m3-port"), out ushort port)) Port = port;
            if (int.TryParse(GetArg("-m3-disconnect-timeout"), out int disconnectMs)) M3Config.DisconnectTimeoutMs = Mathf.Max(200, disconnectMs);
        }

        static void ParseMmr(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return;
            string[] pairs = spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] kv = pairs[i].Split('=');
                if (kv.Length != 2) continue;
                if (float.TryParse(kv[1], out float mmr)) M3Config.PlayerMmr[kv[0].Trim()] = mmr;
            }
        }

        static void ParseParties(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return;
            string[] pairs = spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] kv = pairs[i].Split('=');
                if (kv.Length != 2) continue;
                M3Config.PlayerParty[kv[0].Trim()] = kv[1].Trim();
            }
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
