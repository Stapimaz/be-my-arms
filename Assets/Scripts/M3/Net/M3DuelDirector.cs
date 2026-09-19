using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M3
{
    /// <summary>A slot the server should fill for a match (produced by the matchmaker host).</summary>
    public struct M3SlotAssignment
    {
        public int Slot;
        public string PlayerId;
        public bool IsBot;
    }

    /// <summary>
    /// Server-authoritative match director for one or more shared bodies per team (Duel = 1, 2v2 = 2).
    /// Hosts the M3 round-loop core (match/round state machine, closing zone, utility, telemetry),
    /// spawns and binds the bodies, drives direct or matchmaker-based slot assignment, and broadcasts
    /// the replicated match state. All match state is server-owned; clients only mirror it.
    /// </summary>
    public class M3DuelDirector : NetworkBehaviour
    {
        public static M3DuelDirector Instance { get; private set; }

        public GameObject BodyPrefab;

        [Header("Match shape (TUNING; overridden from command line)")]
        public int BodiesPerTeam = 1;

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
        public NetworkVariable<int> BodiesAliveA = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> BodiesAliveB = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public M3Phase CurrentPhase => (M3Phase)Phase.Value;
        public bool IsBuy => CurrentPhase == M3Phase.Buy;
        public bool IsLive => CurrentPhase == M3Phase.Live;
        public bool InputsAccepted => CurrentPhase == M3Phase.Live;
        public bool MatchStarted => _matchStarted;
        public int SlotCount => M3DuelSlots.SlotCount(BodiesPerTeam);
        public M3UtilitySystem Utility { get; } = new M3UtilitySystem();

        /// <summary>Raised on the server when the match ends: winner (-1 draw). Used by the rating host.</summary>
        public event Action<int> MatchCompleted;

        readonly M3MatchState _match = new M3MatchState();
        readonly M3ClosingZone _zone = new M3ClosingZone();
        readonly M3Telemetry _telemetry = new M3Telemetry();
        readonly List<M3DuelBody>[] _bodies = { new List<M3DuelBody>(), new List<M3DuelBody>() };

        bool _matchStarted;
        bool _firstContact;
        double _liveStartTime;
        float _serverStartTime;
        string _ticket = "";

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _serverStartTime = Time.realtimeSinceStartup;

            BodiesPerTeam = Mathf.Clamp(M3Config.BodiesPerTeam, 1, 2);

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

        // ---- Spawn ----

        void SpawnBodies()
        {
            if (BodyPrefab == null)
            {
                Debug.LogError("[M3] match director has no body prefab assigned.");
                return;
            }

            for (int team = 0; team < M3DuelSlots.Teams; team++)
                for (int body = 0; body < BodiesPerTeam; body++)
                    _bodies[team].Add(SpawnBody(team, body));

            for (int team = 0; team < M3DuelSlots.Teams; team++)
            {
                for (int i = 0; i < _bodies[team].Count; i++)
                {
                    M3DuelBody self = _bodies[team][i];
                    if (self == null) continue;
                    self.ServerBind(this, BuildEnemyList(team));
                }
            }

            Log($"bodies spawned: {BodiesPerTeam} per team ({_bodies[0].Count + _bodies[1].Count} total)");
        }

        M3DuelBody[] BuildEnemyList(int team)
        {
            int other = team == 0 ? 1 : 0;
            return _bodies[other].ToArray();
        }

        M3DuelBody SpawnBody(int team, int body)
        {
            GameObject go = Instantiate(BodyPrefab);
            var component = go.GetComponent<M3DuelBody>();
            NetworkObject networkObject = go.GetComponent<NetworkObject>();
            networkObject.Spawn(true);
            component.ServerConfigure(team, body, M3DuelSlots.Encode(team, body, 0, BodiesPerTeam));
            return component;
        }

        void OnClientConnected(ulong clientId)
        {
            if (!IsServer || M3DuelRoleService.Instance == null) return;
            int slot = M3DuelRoleService.Instance.Registry.SlotFor(clientId);
            if (!M3DuelSlots.IsValidSlot(slot, BodiesPerTeam)) return;
            SendSlot(clientId, slot);
        }

        void SendSlot(ulong clientId, int slot)
        {
            var target = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };
            AssignSlotClientRpc((byte)slot, target);
            Log($"client {clientId} assigned {M3DuelSlots.Name(slot, BodiesPerTeam)}");
        }

        [ClientRpc]
        void AssignSlotClientRpc(byte slot, ClientRpcParams rpcParams = default)
        {
            M3DuelClient.SetLocalSlot(slot);
        }

        // ---- Matchmaking hook ----

        /// <summary>
        /// Server-only: fill the match from a matchmaker proposal and start it. Slots not present in
        /// the proposal become bots. Called by the M4 matchmaker host; direct/dev mode uses
        /// <see cref="TryStartMatch"/> instead.
        /// </summary>
        public void ServerBeginMatch(IReadOnlyList<M3SlotAssignment> assignments, string ticket)
        {
            if (!IsServer || _matchStarted) return;
            _ticket = ticket ?? "";

            var roster = M3DuelRoleService.Instance != null ? M3DuelRoleService.Instance.Registry : null;
            if (roster == null) return;

            var filled = new bool[SlotCount];
            int humans = 0;
            for (int i = 0; i < assignments.Count; i++)
            {
                M3SlotAssignment a = assignments[i];
                if (!M3DuelSlots.IsValidSlot(a.Slot, BodiesPerTeam) || filled[a.Slot]) continue;
                filled[a.Slot] = true;

                if (a.IsBot || string.IsNullOrEmpty(a.PlayerId))
                {
                    roster.SetBot(a.Slot);
                    continue;
                }

                ulong clientId = roster.TryGetQueuedClient(a.PlayerId, out ulong queuedClient)
                    ? queuedClient
                    : roster.ClientForToken(a.PlayerId);
                if (clientId == ulong.MaxValue)
                {
                    roster.SetBot(a.Slot);
                    continue;
                }

                int assigned = M3DuelRoleService.Instance.AssignFromMatch(clientId, a.PlayerId, a.Slot, out ulong displaced);
                if (assigned >= 0)
                {
                    humans++;
                    SendSlot(clientId, assigned);
                }
            }

            for (int slot = 0; slot < SlotCount; slot++)
                if (!roster.SlotTaken(slot)) roster.SetBot(slot);

            _matchStarted = true;
            Log($"match starting ticket='{_ticket}' humans={humans} bodiesPerTeam={BodiesPerTeam}");
            _match.StartMatch();
        }

        void TryStartMatch()
        {
            var roster = M3DuelRoleService.Instance != null ? M3DuelRoleService.Instance.Registry : null;
            if (roster == null) return;

            bool ready = roster.AssignedCount >= M3Config.RequiredPlayers;
            bool timedOut = Time.realtimeSinceStartup - _serverStartTime >= M3Config.StartDelaySeconds;
            if (!ready && !timedOut) return;

            for (int slot = 0; slot < SlotCount; slot++)
                if (!roster.SlotTaken(slot)) roster.SetBot(slot);

            _matchStarted = true;
            Log($"match starting ({roster.AssignedCount} human slots; empty slots bot-filled)");
            _match.StartMatch();
        }

        // ---- Round loop ----

        void Update()
        {
            if (!IsServer) return;

            Utility.Tick(Time.timeAsDouble);

            if (!_matchStarted)
            {
                if (!M3Config.UseMatchmaker) TryStartMatch();
                return; // matchmaker mode is started by the M4 host via ServerBeginMatch
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

        void OnRoundStarted(int round)
        {
            Utility.Clear();
            _firstContact = false;

            M3MapSpawns mapSpawns = FindAnyObjectByType<M3MapSpawns>();
            for (int team = 0; team < M3DuelSlots.Teams; team++)
            {
                for (int i = 0; i < _bodies[team].Count; i++)
                {
                    M3DuelBody body = _bodies[team][i];
                    if (body == null) continue;

                    Vector3 position;
                    float yaw;
                    if (mapSpawns != null && mapSpawns.TryGetBodyPose(team, i, out position, out yaw))
                    {
                        body.ServerResetRound(position.x, position.z, yaw);
                    }
                    else
                    {
                        float offset = (i - (BodiesPerTeam - 1) * 0.5f) * 4f;
                        float z = team == 0 ? -10f : 10f;
                        body.ServerResetRound(offset, z, team == 0 ? 0f : 180f);
                    }
                }
            }
            if (mapSpawns != null) Log($"using map spawns ({mapSpawns.Spawns.Count})");

            MirrorState();
            Log($"round {round} started: buy phase {BuySeconds:0}s");
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
            MatchCompleted?.Invoke(winner);
        }

        void ApplyZone(float radius, float dt)
        {
            for (int team = 0; team < M3DuelSlots.Teams; team++)
                for (int i = 0; i < _bodies[team].Count; i++)
                    ApplyZoneTo(_bodies[team][i], radius, dt);
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
                    Log($"closing zone {radius:0.0}m: {M3DuelSlots.Name(body.SlotP1, BodiesPerTeam)} outside at {distance:0.0}m, damage {damage:0.00}");
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
            BodiesAliveA.Value = CountAlive(0);
            BodiesAliveB.Value = CountAlive(1);
        }

        int CountAlive(int team)
        {
            int count = 0;
            for (int i = 0; i < _bodies[team].Count; i++)
                if (_bodies[team][i] != null && _bodies[team][i].Alive.Value) count++;
            return count;
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
            int alive = CountAlive(team);
            Log($"team {(team == 0 ? "A" : "B")} body eliminated ({alive} left)");
            if (alive == 0 && _match.IsLive) _match.ReportTeamEliminated(team);
        }

        /// <summary>Server-only: apply a utility effect thrown by a body.</summary>
        public void ServerApplyUtility(int team, M3UtilityKind kind, float x, float z, float aimYaw)
        {
            if (!IsLive) return;
            Utility.Throw(kind, team, Time.timeAsDouble, x, z, aimYaw);
            Log($"team {(team == 0 ? "A" : "B")} threw {kind} from ({x:0.0},{z:0.0})");

            if (kind == M3UtilityKind.Flash)
            {
                float rad = aimYaw * Mathf.Deg2Rad;
                float tx = x + Mathf.Sin(rad) * Utility.ThrowDistance;
                float tz = z + Mathf.Cos(rad) * Utility.ThrowDistance;
                for (int t = 0; t < M3DuelSlots.Teams; t++)
                    for (int i = 0; i < _bodies[t].Count; i++)
                        BlindIfNear(_bodies[t][i], tx, tz);
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
            if (!IsLive) return;
            Log($"team {(team == 0 ? "A" : "B")} grenade detonated at ({x:0.0},{z:0.0})");
            for (int t = 0; t < M3DuelSlots.Teams; t++)
                for (int i = 0; i < _bodies[t].Count; i++)
                    DamageWithGrenade(_bodies[t][i], x, z);
        }

        void DamageWithGrenade(M3DuelBody body, float x, float z)
        {
            if (body == null || !body.Alive.Value) return;
            float damage = Utility.GrenadeDamageAt(x, z, body.State.Value.PosX, body.State.Value.PosZ);
            if (damage > 0f) body.ServerTakeDamage(damage, null);
        }

        public bool TryGetSlotPlayer(int slot, out string playerId)
        {
            playerId = null;
            var roster = M3DuelRoleService.Instance != null ? M3DuelRoleService.Instance.Registry : null;
            if (roster == null || !M3DuelSlots.IsValidSlot(slot, BodiesPerTeam)) return false;
            playerId = roster.TokenForSlot(slot);
            return !string.IsNullOrEmpty(playerId);
        }

        static void Log(string message) => Debug.Log($"[M3] t={Time.realtimeSinceStartup:0.000} {message}");
    }
}
