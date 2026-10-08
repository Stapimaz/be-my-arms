using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.Match
{
    /// <summary>A slot the server should fill for a match (produced by the matchmaker host).</summary>
    public struct RoleSlotAssignment
    {
        public int Slot;
        public string PlayerId;
        public bool IsBot;
    }

    /// <summary>
    /// Server-authoritative match director for one or more shared bodies per team (Duel = 1, 2v2 = 2).
    /// Hosts the Match round-loop core (match/round state machine, closing zone, utility, telemetry),
    /// spawns and binds the bodies, drives direct or matchmaker-based slot assignment, and broadcasts
    /// the replicated match state. All match state is server-owned; clients only mirror it.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M3", "BeMyArms.M3", "M3DuelDirector")]
    public class MatchDirector : NetworkBehaviour
    {
        public static MatchDirector Instance { get; private set; }

        /// <summary>Clears the static instance so a later match cannot resolve a previous director.</summary>
        public static void ResetStatics() => Instance = null;

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

        public NetworkVariable<byte> Phase = new((byte)RoundPhase.Warmup, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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

        public RoundPhase CurrentPhase => (RoundPhase)Phase.Value;
        public bool IsBuy => CurrentPhase == RoundPhase.Buy;
        public bool IsLive => CurrentPhase == RoundPhase.Live;
        public bool InputsAccepted => CurrentPhase == RoundPhase.Live;
        public bool MatchStarted => _matchStarted;
        public int SlotCount => MatchSlots.SlotCount(BodiesPerTeam);
        public UtilitySystem Utility { get; } = new UtilitySystem();

        /// <summary>Raised on the server when the match ends: winner (-1 draw). Used by the rating host.</summary>
        public event Action<int> MatchCompleted;

        readonly MatchState _match = new MatchState();
        readonly ClosingZone _zone = new ClosingZone();
        readonly Telemetry _telemetry = new Telemetry();
        readonly List<NetworkBody>[] _bodies = { new List<NetworkBody>(), new List<NetworkBody>() };

        bool _matchStarted;
        bool _firstContact;
        double _liveStartTime;
        float _serverStartTime;
        string _ticket = "";
        double _nextPracticeAction;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _serverStartTime = Time.realtimeSinceStartup;

            BodiesPerTeam = Mathf.Clamp(MatchConfig.BodiesPerTeam, 1, 2);

            BuySeconds = MatchConfig.BuySeconds;
            LiveSeconds = MatchConfig.LiveSeconds;
            RoundEndSeconds = MatchConfig.RoundEndSeconds;
            ZoneStartRadius = MatchConfig.ZoneStartRadius;
            ZoneEndRadius = MatchConfig.ZoneEndRadius;
            ZoneCloseStart = MatchConfig.ZoneCloseStart;
            ZoneCloseDuration = MatchConfig.ZoneCloseDuration;
            ZoneDamagePerSecond = MatchConfig.ZoneDamagePerSecond;

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
                Debug.LogError("[Match] match director has no body prefab assigned.");
                return;
            }

            for (int team = 0; team < MatchSlots.Teams; team++)
                for (int body = 0; body < BodiesPerTeam; body++)
                    _bodies[team].Add(SpawnBody(team, body));

            for (int team = 0; team < MatchSlots.Teams; team++)
            {
                for (int i = 0; i < _bodies[team].Count; i++)
                {
                    NetworkBody self = _bodies[team][i];
                    if (self == null) continue;
                    self.ServerBind(this, BuildEnemyList(team));
                }
            }

            Log($"bodies spawned: {BodiesPerTeam} per team ({_bodies[0].Count + _bodies[1].Count} total)");
        }

        NetworkBody[] BuildEnemyList(int team)
        {
            int other = team == 0 ? 1 : 0;
            return _bodies[other].ToArray();
        }

        NetworkBody SpawnBody(int team, int body)
        {
            GameObject go = Instantiate(BodyPrefab);
            var component = go.GetComponent<NetworkBody>();
            NetworkObject networkObject = go.GetComponent<NetworkObject>();
            networkObject.Spawn(true);
            component.ServerConfigure(team, body, MatchSlots.Encode(team, body, 0, BodiesPerTeam));
            return component;
        }

        void OnClientConnected(ulong clientId)
        {
            if (!IsServer || MatchRoleService.Instance == null) return;
            int slot = MatchRoleService.Instance.Registry.SlotFor(clientId);
            if (!MatchSlots.IsValidSlot(slot, BodiesPerTeam)) return;
            SendSlot(clientId, slot);
        }

        void SendSlot(ulong clientId, int slot)
        {
            var target = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };
            AssignSlotClientRpc((byte)slot, target);
            Log($"client {clientId} assigned {MatchSlots.Name(slot, BodiesPerTeam)}");
        }

        [ClientRpc]
        void AssignSlotClientRpc(byte slot, ClientRpcParams rpcParams = default)
        {
            NetworkBodyClient.SetLocalSlot(slot);
        }

        // ---- Matchmaking hook ----

        /// <summary>
        /// Server-only: fill the match from a matchmaker proposal and start it. Slots not present in
        /// the proposal become bots. Called by the Matchmaking matchmaker host; direct/dev mode uses
        /// <see cref="TryStartMatch"/> instead.
        /// </summary>
        public void ServerBeginMatch(IReadOnlyList<RoleSlotAssignment> assignments, string ticket)
        {
            if (!IsServer || _matchStarted) return;
            _ticket = ticket ?? "";

            var roster = MatchRoleService.Instance != null ? MatchRoleService.Instance.Registry : null;
            if (roster == null) return;

            var filled = new bool[SlotCount];
            int humans = 0;
            for (int i = 0; i < assignments.Count; i++)
            {
                RoleSlotAssignment a = assignments[i];
                if (!MatchSlots.IsValidSlot(a.Slot, BodiesPerTeam) || filled[a.Slot]) continue;
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

                int assigned = MatchRoleService.Instance.AssignFromMatch(clientId, a.PlayerId, a.Slot, out ulong displaced);
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
            var roster = MatchRoleService.Instance != null ? MatchRoleService.Instance.Registry : null;
            if (roster == null) return;

            bool ready = roster.AssignedCount >= MatchConfig.RequiredPlayers;
            if (MatchConfig.PrivatePractice && MatchConfig.RequiredPlayers == 2)
                ready = roster.SlotTaken(0) && roster.SlotTaken(1);
            bool timedOut = MatchConfig.StartDelaySeconds > 0f && Time.realtimeSinceStartup - _serverStartTime >= MatchConfig.StartDelaySeconds;
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
                if (!MatchConfig.UseMatchmaker) TryStartMatch();
                return; // matchmaker mode is started by the Matchmaking host via ServerBeginMatch
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
            MatchWinner.Value = -1;
            ZoneRadius.Value = ZoneStartRadius;
            Utility.Clear();
            _firstContact = false;

            MapSpawns mapSpawns = FindAnyObjectByType<MapSpawns>();
            for (int team = 0; team < MatchSlots.Teams; team++)
            {
                for (int i = 0; i < _bodies[team].Count; i++)
                {
                    NetworkBody body = _bodies[team][i];
                    if (body == null) continue;

                    Vector3 position;
                    float yaw;
                    if (mapSpawns != null && mapSpawns.TryGetBodyPose(team, i, out position, out yaw))
                    {
                        body.ServerResetRound(position.x, position.y, position.z, yaw);
                    }
                    else
                    {
                        float offset = (i - (BodiesPerTeam - 1) * 0.5f) * 4f;
                        float z = team == 0 ? -10f : 10f;
                        body.ServerResetRound(offset, 0f, z, team == 0 ? 0f : 180f);
                    }
                }
            }
            if (mapSpawns != null) Log($"using map spawns ({mapSpawns.Spawns.Count})");

            // Vertical slice: every P2 (human or bot) is equipped with the single rifle at round
            // start, so there is always exactly one clear weapon per body.
            for (int team = 0; team < MatchSlots.Teams; team++)
            {
                for (int i = 0; i < _bodies[team].Count; i++)
                {
                    NetworkBody body = _bodies[team][i];
                    if (body != null) body.ServerAutoBuyBotLoadout();
                }
            }

            MirrorState();
            Log($"round {round} started: buy phase {BuySeconds:0}s");
        }

        void OnLiveStarted(int round)
        {
            _liveStartTime = Time.timeAsDouble;
            for (int team = 0; team < 2; team++)
                foreach (var body in _bodies[team]) body.ServerBeginLive();
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
            for (int team = 0; team < MatchSlots.Teams; team++)
                for (int i = 0; i < _bodies[team].Count; i++)
                    ApplyZoneTo(_bodies[team][i], radius, dt);
        }

        void ApplyZoneTo(NetworkBody body, float radius, float dt)
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
                    Log($"closing zone {radius:0.0}m: {MatchSlots.Name(body.SlotP1, BodiesPerTeam)} outside at {distance:0.0}m, damage {damage:0.00}");
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

        public int ServerApplyDamage(NetworkBody target, float damage, NetworkBody attacker,
            Vector3? hitPoint = null, BeMyArms.Core.HitboxRegion.Region region = BeMyArms.Core.HitboxRegion.Region.Body,
            DamageKind kind = DamageKind.World)
        {
            return target != null ? target.ServerTakeDamage(damage, attacker, hitPoint, region, kind) : 0;
        }

        public void OnBodyEliminated(int team)
        {
            int alive = CountAlive(team);
            Log($"team {(team == 0 ? "A" : "B")} body eliminated ({alive} left)");
            if (alive == 0 && _match.IsLive) _match.ReportTeamEliminated(team);
        }

        /// <summary>Server-only: apply a utility effect thrown by a body.</summary>
        public void ServerApplyUtility(int team, UtilityKind kind, float x, float z, float aimYaw)
        {
            if (!IsLive) return;
            Utility.Throw(kind, team, Time.timeAsDouble, x, z, aimYaw);
            Log($"team {(team == 0 ? "A" : "B")} threw {kind} from ({x:0.0},{z:0.0})");

            if (kind == UtilityKind.Flash)
            {
                float rad = aimYaw * Mathf.Deg2Rad;
                float tx = x + Mathf.Sin(rad) * Utility.ThrowDistance;
                float tz = z + Mathf.Cos(rad) * Utility.ThrowDistance;
                for (int t = 0; t < MatchSlots.Teams; t++)
                    for (int i = 0; i < _bodies[t].Count; i++)
                        BlindIfNear(_bodies[t][i], tx, tz);
            }
        }

        void BlindIfNear(NetworkBody body, float x, float z)
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
            for (int t = 0; t < MatchSlots.Teams; t++)
                for (int i = 0; i < _bodies[t].Count; i++)
                    DamageWithGrenade(_bodies[t][i], x, z);
        }

        void DamageWithGrenade(NetworkBody body, float x, float z)
        {
            if (body == null || !body.Alive.Value) return;
            float damage = Utility.GrenadeDamageAt(x, z, body.State.Value.PosX, body.State.Value.PosZ);
            if (damage > 0f) body.ServerTakeDamage(damage, null);
        }

        public bool TryGetSlotPlayer(int slot, out string playerId)
        {
            playerId = null;
            var roster = MatchRoleService.Instance != null ? MatchRoleService.Instance.Registry : null;
            if (roster == null || !MatchSlots.IsValidSlot(slot, BodiesPerTeam)) return false;
            playerId = roster.TokenForSlot(slot);
            return !string.IsNullOrEmpty(playerId);
        }

        /// <summary>Practice-only controls use normal server authority; either human can request them.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void LeavePracticeServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!MatchConfig.PrivatePractice || MatchRoleService.Instance == null) return;
            var roster = MatchRoleService.Instance.Registry;
            int slot = roster.ReleaseReservation(rpcParams.Receive.SenderClientId);
            if (slot >= 0) roster.SetBot(slot);
        }

        [ServerRpc(RequireOwnership = false)]
        public void PracticeActionServerRpc(byte action, ServerRpcParams rpcParams = default)
        {
            var roster = MatchRoleService.Instance != null ? MatchRoleService.Instance.Registry : null;
            if (action > 2 || !MatchConfig.PrivatePractice || roster == null || roster.SlotFor(rpcParams.Receive.SenderClientId) < 0 || !_matchStarted) return;
            if (Time.timeAsDouble < _nextPracticeAction) return;
            _nextPracticeAction = Time.timeAsDouble + 0.5;
            if (action == 2)
            {
                int slot = roster.SlotFor(rpcParams.Receive.SenderClientId);
                int first = slot - MatchSlots.RoleOf(slot);
                roster.SwapBodyRoles(first);
                for (int role = 0; role < 2; role++)
                {
                    ulong owner = roster.OwnerOf(first + role);
                    if (owner != ulong.MaxValue) SendSlot(owner, first + role);
                }
            }
            if (action == 1 || action == 2)
            {
                for (int team = 0; team < 2; team++) foreach (var body in _bodies[team]) body.Kills.Value = 0;
                _match.StartMatch();
            }
            else if (action == 0) _match.RestartRound();
            MirrorState();
        }

        static void Log(string message) => Debug.Log($"[Match] t={Time.realtimeSinceStartup:0.000} {message}");
    }
}
