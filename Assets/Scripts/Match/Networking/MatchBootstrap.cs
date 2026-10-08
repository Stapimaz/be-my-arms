using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace BeMyArms.Match
{
    public enum MatchProcessRole
    {
        Host,
        Server,
        Client
    }

    /// <summary>
    /// Starts a match and configures role + round timing from the command line, so a dedicated server
    /// and several role clients can run as independent processes:
    ///   -match-role server|client|host
    ///   -match-team a|b  -match-body 0|1  -match-position p1|p2|either
    ///   -match-token &lt;id&gt;   -match-delay &lt;ms&gt;   -match-loss &lt;percent&gt;
    ///   -match-buy &lt;s&gt; -match-live &lt;s&gt; -match-roundend &lt;s&gt;
    ///   -match-zone-start/-end/-close/-duration/-dps
    ///   -match-auto 0|1  -match-auto-buy 0|1  -match-auto-fire 0|1  -match-auto-utility 0|1
    ///   -match-bot-difficulty easy|hard
    ///   -match-start-delay &lt;s&gt; -match-required-players &lt;n&gt; -match-exit-after &lt;s&gt; -match-port &lt;p&gt;
    ///   -queue-mode duel|2v2   -queue-matchmaker 0|1   -queue-mmr token=value,token=value
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M3", "BeMyArms.M3", "M3DuelBootstrap")]
    public class MatchBootstrap : MonoBehaviour
    {
        public NetworkManager Manager;
        public GameObject DirectorPrefab;
        public MatchProcessRole Role = MatchProcessRole.Host;
        public ushort Port = 7779;

        float _reconnectTimer;

        void Start()
        {
            if (Application.isBatchMode)
            {
                Application.runInBackground = true;
                Application.targetFrameRate = 60;
            }
            ParseArgs();

            // Private-match flow: a scene bootstrap with no CLI role connects as a client.
            if (Role == MatchProcessRole.Host && MatchConfig.AutoStartClient) Role = MatchProcessRole.Client;

            NetworkManager manager = Manager != null ? Manager : NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogError("[Match] No NetworkManager found.");
                return;
            }

            ushort effectivePort = MatchConfig.PortOverride != 0 ? MatchConfig.PortOverride : Port;
            var transport = manager.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(MatchConfig.ServerAddress, effectivePort, "0.0.0.0");
                transport.DisconnectTimeoutMS = MatchConfig.DisconnectTimeoutMs;
            }

            manager.OnClientConnectedCallback += id => Debug.Log($"[Match-trace] t={Time.realtimeSinceStartup:0.000} CLIENT connected (id={id})");
            manager.OnClientDisconnectCallback += id =>
            {
                Debug.Log($"[Match-trace] t={Time.realtimeSinceStartup:0.000} CLIENT disconnected (id={id}) reason='{manager.DisconnectReason}'");
                if (Role == MatchProcessRole.Client) { NetworkBodyClient.SetLocalSlot(-1); LocalInput.Reset(); }
            };
            manager.OnServerStarted += OnServerStarted;

            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ForceSamePrefabs = false;

            var roleService = manager.gameObject.GetComponent<MatchRoleService>();
            if (roleService == null) roleService = manager.gameObject.AddComponent<MatchRoleService>();
            roleService.InstallServerHooks();

            switch (Role)
            {
                case MatchProcessRole.Server:
                    manager.StartServer();
                    break;
                case MatchProcessRole.Client:
                    manager.NetworkConfig.ConnectionData = MatchRoleService.Encode(MatchConfig.ClientToken, DesiredPayload());
                    manager.StartClient();
                    break;
                default:
                    manager.StartHost();
                    if (MatchConfig.UseMatchmaker)
                    {
                        roleService.Registry.Enqueue(manager.LocalClientId, MatchConfig.ClientToken, MatchConfig.ClientPreference, Time.realtimeSinceStartup, "local");
                    }
                    else
                    {
                        int hostSlot = MatchSlots.Encode(MatchConfig.ClientTeam, MatchConfig.ClientBody, MatchConfig.ClientRole, MatchConfig.BodiesPerTeam);
                        roleService.Registry.AssignPreferred(manager.LocalClientId, MatchConfig.ClientToken, hostSlot, out _);
                        NetworkBodyClient.SetLocalSlot(hostSlot);
                    }
                    break;
            }

            Debug.Log($"[Match] bootstrap role={Role} mode={(MatchConfig.BodiesPerTeam == 2 ? "2v2" : "duel")} matchmaker={MatchConfig.UseMatchmaker} " +
                      $"token='{MatchConfig.ClientToken}' port={Port} delay={MatchConfig.OneWayDelaySeconds * 1000:0}ms loss={MatchConfig.LossPercent:0}% " +
                      $"auto={MatchConfig.AutoDrive} bot={MatchConfig.BotDifficulty} buy={MatchConfig.BuySeconds:0}s live={MatchConfig.LiveSeconds:0}s");
        }

        void OnServerStarted()
        {
            if (Role == MatchProcessRole.Client) return;
            if (DirectorPrefab == null)
            {
                Debug.LogError("[Match] DirectorPrefab not assigned.");
                return;
            }

            GameObject director = Instantiate(DirectorPrefab);
            director.GetComponent<NetworkObject>().Spawn(true);
            Debug.Log("[Match] match director spawned");
        }

        void Update()
        {
            NetworkManager manager = Manager != null ? Manager : NetworkManager.Singleton;
            if (manager == null) return;

            if (MatchConfig.ExitAfterSeconds > 0f && Time.realtimeSinceStartup >= MatchConfig.ExitAfterSeconds)
            {
                Debug.Log($"[Match-trace] t={Time.realtimeSinceStartup:0.000} graceful shutdown requested");
                if (manager.IsListening) manager.Shutdown();
                Application.Quit();
                return;
            }

            if (Role != MatchProcessRole.Client || MatchConfig.ExitAfterSeconds > 0f) return;
            if (!manager.IsConnectedClient && !manager.IsListening && !manager.ShutdownInProgress)
            {
                _reconnectTimer += Time.deltaTime;
                if (_reconnectTimer >= 2f)
                {
                    _reconnectTimer = 0f;
                    Debug.Log($"[Match-trace] t={Time.realtimeSinceStartup:0.000} CLIENT reconnect attempt (token '{MatchConfig.ClientToken}')");
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
            if (MatchConfig.UseMatchmaker) return (byte)Mathf.Clamp(MatchConfig.ClientPreference, 0, 2);
            int slot = MatchSlots.Encode(MatchConfig.ClientTeam, MatchConfig.ClientBody, MatchConfig.ClientRole, MatchConfig.BodiesPerTeam);
            return (byte)slot;
        }

        void ParseArgs()
        {
            string role = GetArg("-match-role");
            if (!string.IsNullOrEmpty(role))
            {
                switch (role.ToLowerInvariant())
                {
                    case "server": Role = MatchProcessRole.Server; break;
                    case "client": Role = MatchProcessRole.Client; break;
                    default: Role = MatchProcessRole.Host; break;
                }
            }

            // Match shape first: Duel (1 body/team, 4 players) or 2v2 (2 bodies/team, 8 players).
            string mode = GetArg("-queue-mode");
            if (!string.IsNullOrEmpty(mode) && mode.ToLowerInvariant() == "2v2")
            {
                MatchConfig.BodiesPerTeam = 2;
                MatchConfig.ExpectedPlayers = 8;
            }
            else if (!string.IsNullOrEmpty(mode))
            {
                MatchConfig.BodiesPerTeam = 1;
                MatchConfig.ExpectedPlayers = 4;
            }
            if (int.TryParse(GetArg("-queue-bodies"), out int bodies)) MatchConfig.BodiesPerTeam = Mathf.Clamp(bodies, 1, 2);

            string matchmaker = GetArg("-queue-matchmaker");
            if (!string.IsNullOrEmpty(matchmaker)) MatchConfig.UseMatchmaker = matchmaker != "0";

            if (int.TryParse(GetArg("-queue-required"), out int requiredPlayers)) MatchConfig.RequiredPlayers = Mathf.Clamp(requiredPlayers, 1, 8);

            ParseMmr(GetArg("-queue-mmr"));
            ParseParties(GetArg("-queue-party"));

            string team = GetArg("-match-team");
            if (!string.IsNullOrEmpty(team)) MatchConfig.ClientTeam = team.ToLowerInvariant() == "b" ? 1 : 0;

            string body = GetArg("-match-body");
            if (!string.IsNullOrEmpty(body)) MatchConfig.ClientBody = Mathf.Clamp(int.TryParse(body, out int b) ? b : (body.ToLowerInvariant() == "b1" ? 1 : 0), 0, 1);

            string position = GetArg("-match-position");
            if (!string.IsNullOrEmpty(position))
            {
                string p = position.ToLowerInvariant();
                if (p == "either") { MatchConfig.ClientPreference = 2; }
                else if (p == "p2") { MatchConfig.ClientRole = 1; MatchConfig.ClientPreference = 1; }
                else { MatchConfig.ClientRole = 0; MatchConfig.ClientPreference = 0; }
            }

            string token = GetArg("-match-token");
            if (!string.IsNullOrEmpty(token)) MatchConfig.ClientToken = token;
            string address = GetArg("-match-address");
            if (!string.IsNullOrEmpty(address)) MatchConfig.ServerAddress = address;
            if (GetArg("-match-practice") == "1") MatchConfig.PrivatePractice = true;
            if (GetArg("-match-strict-slots") == "1") MatchConfig.StrictSlots = true;

            if (float.TryParse(GetArg("-match-delay"), out float delayMs)) MatchConfig.OneWayDelaySeconds = Mathf.Max(0f, delayMs) / 1000f;
            if (float.TryParse(GetArg("-match-loss"), out float loss)) MatchConfig.LossPercent = Mathf.Clamp(loss, 0f, 100f);
            if (float.TryParse(GetArg("-match-rewind"), out float rewindMs)) MatchConfig.LagRewindSeconds = Mathf.Max(0f, rewindMs) / 1000f;

            if (float.TryParse(GetArg("-match-buy"), out float buy)) MatchConfig.BuySeconds = Mathf.Max(0.5f, buy);
            if (float.TryParse(GetArg("-match-live"), out float live)) MatchConfig.LiveSeconds = Mathf.Max(1f, live);
            if (float.TryParse(GetArg("-match-roundend"), out float roundEnd)) MatchConfig.RoundEndSeconds = Mathf.Max(0.5f, roundEnd);

            if (float.TryParse(GetArg("-match-zone-start"), out float zStart)) MatchConfig.ZoneStartRadius = zStart;
            if (float.TryParse(GetArg("-match-zone-end"), out float zEnd)) MatchConfig.ZoneEndRadius = zEnd;
            if (float.TryParse(GetArg("-match-zone-close"), out float zClose)) MatchConfig.ZoneCloseStart = zClose;
            if (float.TryParse(GetArg("-match-zone-duration"), out float zDuration)) MatchConfig.ZoneCloseDuration = zDuration;
            if (float.TryParse(GetArg("-match-zone-dps"), out float zDps)) MatchConfig.ZoneDamagePerSecond = zDps;

            string auto = GetArg("-match-auto");
            if (!string.IsNullOrEmpty(auto)) MatchConfig.AutoDrive = auto != "0";
            string autoBuy = GetArg("-match-auto-buy");
            if (!string.IsNullOrEmpty(autoBuy)) MatchConfig.AutoBuy = autoBuy != "0";
            string autoFire = GetArg("-match-auto-fire");
            if (!string.IsNullOrEmpty(autoFire)) MatchConfig.AutoFire = autoFire != "0";
            string autoUtility = GetArg("-match-auto-utility");
            if (!string.IsNullOrEmpty(autoUtility)) MatchConfig.AutoUtility = autoUtility != "0";

            string botDifficulty = GetArg("-match-bot-difficulty");
            if (!string.IsNullOrEmpty(botDifficulty))
                MatchConfig.BotDifficulty = botDifficulty.ToLowerInvariant() == "hard" ? BotDifficulty.Hard : BotDifficulty.Easy;

            if (float.TryParse(GetArg("-match-start-delay"), out float startDelay)) MatchConfig.StartDelaySeconds = Mathf.Max(0f, startDelay);
            if (int.TryParse(GetArg("-match-required-players"), out int required)) MatchConfig.RequiredPlayers = Mathf.Clamp(required, 1, 8);
            if (float.TryParse(GetArg("-match-exit-after"), out float exitAfter)) MatchConfig.ExitAfterSeconds = exitAfter;
            if (ushort.TryParse(GetArg("-match-port"), out ushort port)) Port = port;
            if (int.TryParse(GetArg("-match-disconnect-timeout"), out int disconnectMs)) MatchConfig.DisconnectTimeoutMs = Mathf.Max(200, disconnectMs);
        }

        static void ParseMmr(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return;
            string[] pairs = spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] kv = pairs[i].Split('=');
                if (kv.Length != 2) continue;
                if (float.TryParse(kv[1], out float mmr)) MatchConfig.PlayerMmr[kv[0].Trim()] = mmr;
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
                MatchConfig.PlayerParty[kv[0].Trim()] = kv[1].Trim();
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
