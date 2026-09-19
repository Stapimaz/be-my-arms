using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M3
{
    /// <summary>
    /// Server-authoritative Duel director. Hosts the M3 round-loop core (match/round state machine,
    /// buy economy, closing zone, utility, telemetry), spawns the two shared bodies, drives the
    /// pre-match slot assignment and broadcasts the replicated match state to clients. One Duel =
    /// two bodies = four humans (team A P1/P2 vs team B P1/P2). All match state is server-owned; the
    /// clients only mirror it. M3 is not complete without this real networked run.
    /// </summary>
    public class M3DuelDirector : NetworkBehaviour
    {
        public static M3DuelDirector Instance { get; private set; }

        public GameObject BodyPrefab;

        [Header("Round loop (TUNING; overridden from command line)")]
        public float BuySeconds = 10f;
        public float LiveSeconds = 150f;
        public float RoundEndSeconds = 3f;
        public int RoundsToWin = 3;
        public int MaxRounds = 5;

        [Header("Closing zone (TUNING)")]
        public float ZoneStartRadius = 40f;
        public float ZoneEndRadius = 8f;
        public float ZoneCloseStart = 60f;
        public float ZoneCloseDuration = 60f;
        public float ZoneDamagePerSecond = 5f;

        public NetworkVariable<byte> Phase = new((byte)M3Phase.Warmup, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> RoundIndex = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> TeamAWins = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> TeamBWins = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> TimeRemaining = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> LastRoundWinner = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> MatchWinner = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> ZoneRadius = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> UnauthorizedInputs = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public M3Phase CurrentPhase => (M3Phase)Phase.Value;
        public bool IsBuy => CurrentPhase == M3Phase.Buy;
        public bool IsLive => CurrentPhase == M3Phase.Live;
        public bool InputsAccepted => CurrentPhase == M3Phase.Live;
        public M3UtilitySystem Utility { get; } = new M3UtilitySystem();

        readonly M3MatchState _match = new M3MatchState();
        readonly M3ClosingZone _zone = new M3ClosingZone();
        readonly M3BuyPhase[] _buy = { new M3BuyPhase(), new M3BuyPhase() };
        readonly M3Telemetry _telemetry = new M3Telemetry();
        readonly int[][] _utilityCharges = { new int[3], new int[3] };

        M3DuelBody _bodyA;
        M3DuelBody _bodyB;
        bool _matchStarted;
        bool _firstContact;
        double _liveStartTime;
        float _serverStartTime;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _serverStartTime = Time.realtimeSinceStartup;

            // Round-loop timing comes from M3Config (command line) so a dedicated run can be tuned
            // without a rebuild; the inspector values are the in-editor defaults.
            BuySeconds = M3Config.BuySeconds;
            LiveSeconds = M3Config.LiveSeconds;
            RoundEndSeconds = M3Config.RoundEndSeconds;
            ZoneStartRadius = M3Config.ZoneStartRadius;
            ZoneEndRadius = M3Config.ZoneEndRadius;
            ZoneCloseStart = M3Config.ZoneCloseStart;
            ZoneCloseDuration = M3Config.ZoneCloseDuration;
            ZoneDamagePerSecond = M3Config.ZoneDamagePerSecond;

            _match.BuySeconds = BuySeconds;
            _match.LiveSeconds = LiveSeconds;
            _match.RoundEndSeconds = RoundEndSeconds;
            _match.RoundsToWin = RoundsToWin;
            _match.MaxRounds = MaxRounds;

            _zone.StartRadius = ZoneStartRadius;
            _zone.EndRadius = ZoneEndRadius;
            _zone.CloseStartSeconds = ZoneCloseStart;
            _zone.CloseDurationSeconds = ZoneCloseDuration;
            _zone.DamagePerSecond = ZoneDamagePerSecond;

            Utility.GrenadeDetonated += OnGrenadeDetonated;

            if (IsServer)
            {
                _match.RoundStarted += OnRoundStarted;
                _match.LiveStarted += OnLiveStarted;
                _match.RoundEnded += OnRoundEnded;
                _match.MatchEnded += OnMatchEnded;
                SpawnBodies();
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                Phase.Value = (byte)_match.Phase;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            Utility.GrenadeDetonated -= OnGrenadeDetonated;
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                _match.RoundStarted -= OnRoundStarted;
                _match.LiveStarted -= OnLiveStarted;
                _match.RoundEnded -= OnRoundEnded;
                _match.MatchEnded -= OnMatchEnded;
            }
        }

        void SpawnBodies()
        {
            if (BodyPrefab == null)
            {
                Debug.LogError("[M3] Duel director has no body prefab assigned.");
                return;
            }

            _bodyA = SpawnBody(team: 0);
            _bodyB = SpawnBody(team: 1);
            if (_bodyA != null && _bodyB != null)
            {
                _bodyA.ServerBind(this, _bodyB);
                _bodyB.ServerBind(this, _bodyA);
            }
            Log($"bodies spawned: A={_bodyA != null} B={_bodyB != null}");
        }

        M3DuelBody SpawnBody(int team)
        {
            GameObject go = Instantiate(BodyPrefab);
            var body = go.GetComponent<M3DuelBody>();
            NetworkObject networkObject = go.GetComponent<NetworkObject>();
            networkObject.Spawn(true);
            body.ServerConfigure(team);
            return body;
        }

        void OnClientConnected(ulong clientId)
        {
            if (!IsServer || M3DuelRoleService.Instance == null) return;
            M3DuelSlot slot = M3DuelRoleService.Instance.Registry.SlotFor(clientId);
            if (!M3DuelSlots.IsValid(slot)) return;

            var target = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };
            AssignSlotClientRpc((byte)slot, target);
            Log($"client {clientId} assigned {M3DuelSlots.Name(slot)}");
        }

        [ClientRpc]
        void AssignSlotClientRpc(byte slot, ClientRpcParams rpcParams = default)
        {
            M3DuelClient.SetLocalSlot((M3DuelSlot)slot);
        }

        // ---- Round loop ----

        void Update()
        {
            if (!IsServer) return;

            Utility.Tick(Time.timeAsDouble);

            if (!_matchStarted)
            {
                TryStartMatch();
                return;
            }

            _match.Tick(Time.deltaTime);

            if (_match.IsLive)
            {
                float liveElapsed = LiveSeconds - _match.PhaseTimeRemaining;
                float radius = _zone.RadiusAt(liveElapsed);
                ZoneRadius.Value = radius;
                ApplyZone(radius, Time.deltaTime);
            }

            MirrorState();
        }

        void TryStartMatch()
        {
            var roster = M3DuelRoleService.Instance != null ? M3DuelRoleService.Instance.Registry : null;
            if (roster == null) return;

            bool ready = roster.AssignedCount >= M3Config.RequiredPlayers;
            bool timedOut = Time.realtimeSinceStartup - _serverStartTime >= M3Config.StartDelaySeconds;
            if (!ready && !timedOut) return;

            for (int i = 0; i < M3DuelSlots.Teams * M3DuelSlots.RolesPerTeam; i++)
            {
                var slot = (M3DuelSlot)i;
                if (!roster.SlotTaken(slot)) roster.SetBot(slot);
            }

            _matchStarted = true;
            Log($"match starting ({roster.AssignedCount} human slots; empty slots bot-filled)");
            _match.StartMatch();
        }

        void OnRoundStarted(int round)
        {
            for (int team = 0; team < 2; team++)
            {
                _buy[team].ResetForRound();
                _utilityCharges[team][0] = _utilityCharges[team][1] = _utilityCharges[team][2] = 0;
            }
            Utility.Clear();
            _firstContact = false;

            ResetBody(_bodyA, 0, new Vector3(0f, 0f, -10f), 0f);
            ResetBody(_bodyB, 1, new Vector3(0f, 0f, 10f), 180f);
            MirrorState();
            Log($"round {round} started: buy phase {BuySeconds:0}s");
        }

        void ResetBody(M3DuelBody body, int team, Vector3 position, float yaw)
        {
            if (body == null) return;
            body.ServerResetRound(position.x, position.z, yaw);
        }

        void OnLiveStarted(int round)
        {
            _liveStartTime = Time.timeAsDouble;
            Log($"round {round} live");
        }

        void OnRoundEnded(int round, int winner)
        {
            float seconds = (float)(Time.timeAsDouble - _liveStartTime);
            _telemetry.RecordRound(seconds);
            Log($"round {round} ended: winner={(winner < 0 ? "draw" : winner == 0 ? "A" : "B")} after {seconds:0.00}s");
        }

        void OnMatchEnded(int winner)
        {
            MatchWinner.Value = winner;
            Log($"MATCH END winner={(winner < 0 ? "draw" : winner == 0 ? "A" : "B")} score A={TeamAWins.Value} B={TeamBWins.Value} " +
                $"avgRound={_telemetry.AverageRoundSeconds():0.00}s avgFirstContact={_telemetry.AverageTimeToFirstContact():0.00}s rounds={_telemetry.RoundsPlayed}");
        }

        void ApplyZone(float radius, float dt)
        {
            ApplyZoneTo(_bodyA, radius, dt);
            ApplyZoneTo(_bodyB, radius, dt);
        }

        void ApplyZoneTo(M3DuelBody body, float radius, float dt)
        {
            if (body == null || !body.Alive.Value) return;
            float x = body.State.Value.PosX;
            float z = body.State.Value.PosZ;
            float distance = Mathf.Sqrt(x * x + z * z);
            bool outside = distance > radius;
            body.OutsideZone.Value = outside;
            if (outside)
            {
                float damage = _zone.DamagePerSecond * dt;
                body.ServerTakeDamage(damage, null);
                if (Time.timeAsDouble >= _nextZoneLog)
                {
                    _nextZoneLog = Time.timeAsDouble + 1.0;
                    Log($"closing zone {radius:0.0}m: team {(body.TeamIndex == 0 ? "A" : "B")} body outside at {distance:0.0}m, damage {damage:0.00}");
                }
            }
        }

        double _nextZoneLog;

        void MirrorState()
        {
            Phase.Value = (byte)_match.Phase;
            RoundIndex.Value = _match.RoundIndex;
            TeamAWins.Value = _match.TeamAWins;
            TeamBWins.Value = _match.TeamBWins;
            TimeRemaining.Value = Mathf.Max(0f, _match.PhaseTimeRemaining);
            LastRoundWinner.Value = _match.LastRoundWinner;
        }

        // ---- Called by bodies ----

        public void NoteUnauthorized() => UnauthorizedInputs.Value++;

        public void NoteDamage(int victimTeam, int attackerTeam)
        {
            if (_firstContact) return;
            if (attackerTeam < 0 || attackerTeam == victimTeam) return;
            _firstContact = true;
            _telemetry.RecordFirstContact((float)(Time.timeAsDouble - _liveStartTime));
            Log($"first contact at {(float)(Time.timeAsDouble - _liveStartTime):0.00}s (team {attackerTeam} -> team {victimTeam})");
        }

        public void ServerApplyDamage(M3DuelBody target, float damage, M3DuelBody attacker)
        {
            if (target == null) return;
            target.ServerTakeDamage(damage, attacker);
        }

        public void OnBodyEliminated(int team)
        {
            Log($"team {(team == 0 ? "A" : "B")} body eliminated");
            if (_match.IsLive) _match.ReportTeamEliminated(team);
        }

        public void ServerBuy(int team, int catalogIndex)
        {
            if (!IsBuy) return;
            if (catalogIndex < 0 || catalogIndex >= _buy[team].Catalog.Count) return;

            M3ShopItem item = _buy[team].Catalog[catalogIndex];
            if (!_buy[team].TryBuy(item.Id)) return;

            if (item.Kind == M3ShopKind.Utility)
            {
                M3UtilityKind kind = item.Id == "grenade" ? M3UtilityKind.Grenade : item.Id == "flash" ? M3UtilityKind.Flash : M3UtilityKind.Smoke;
                _utilityCharges[team][(int)kind]++;
            }

            M3DuelBody body = BodyFor(team);
            if (body != null) body.ServerApplyLoadout(M3Loadouts.ActiveWeapon(_buy[team]));
            Log($"team {(team == 0 ? "A" : "B")} bought {item.Id} (spent {_buy[team].Spent}/{_buy[team].Budget})");
        }

        public void ServerThrowUtility(int team, M3UtilityKind kind, float x, float z, float aimYaw)
        {
            if (!IsLive) return;
            if (_utilityCharges[team][(int)kind] <= 0)
            {
                Log($"throw {kind} rejected for team {(team == 0 ? "A" : "B")}: no charge");
                return;
            }
            _utilityCharges[team][(int)kind]--;

            Utility.Throw(kind, team, Time.timeAsDouble, x, z, aimYaw);
            Log($"team {(team == 0 ? "A" : "B")} threw {kind} (remaining {_utilityCharges[team][(int)kind]})");

            if (kind == M3UtilityKind.Flash)
            {
                float rad = aimYaw * Mathf.Deg2Rad;
                float tx = x + Mathf.Sin(rad) * Utility.ThrowDistance;
                float tz = z + Mathf.Cos(rad) * Utility.ThrowDistance;
                BlindIfNear(_bodyA, tx, tz);
                BlindIfNear(_bodyB, tx, tz);
            }
        }

        void BlindIfNear(M3DuelBody body, float x, float z)
        {
            if (body == null || !body.Alive.Value) return;
            float dx = body.State.Value.PosX - x;
            float dz = body.State.Value.PosZ - z;
            if (dx * dx + dz * dz > Utility.FlashRadius * Utility.FlashRadius) return;
            body.ServerApplyBlind(Utility.FlashBlindSeconds(Time.timeAsDouble, body.State.Value.PosX, body.State.Value.PosZ));
        }

        void OnGrenadeDetonated(int team, float x, float z)
        {
            if (!IsLive) return; // no post-round utility damage; the round result is already sealed
            Log($"team {(team == 0 ? "A" : "B")} grenade detonated at ({x:0.0},{z:0.0})");
            DamageWithGrenade(_bodyA, x, z);
            DamageWithGrenade(_bodyB, x, z);
        }

        void DamageWithGrenade(M3DuelBody body, float x, float z)
        {
            if (body == null || !body.Alive.Value) return;
            float damage = Utility.GrenadeDamageAt(x, z, body.State.Value.PosX, body.State.Value.PosZ);
            if (damage > 0f) body.ServerTakeDamage(damage, null);
        }

        M3DuelBody BodyFor(int team) => team == 0 ? _bodyA : _bodyB;

        static void Log(string message) => Debug.Log($"[M3] t={Time.realtimeSinceStartup:0.000} {message}");
    }
}
