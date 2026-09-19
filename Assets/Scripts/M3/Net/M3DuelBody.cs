using System;
using BeMyArms.M2;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M3
{
    /// <summary>
    /// One shared body of a Duel (team A or B). Server-authoritative: it reuses the M2 pure body
    /// simulation (decoupled look / neck limit / Model-C sector clamp), adds role-tagged P1/P2 input
    /// RPCs authorized by <see cref="M3DuelRoleService"/>, buy/utility RPCs, lag-compensated hitscan
    /// against the enemy body, HP/elimination and round resets. The round loop itself is owned by
    /// <see cref="M3DuelDirector"/>.
    /// </summary>
    public class M3DuelBody : NetworkBehaviour
    {
        public const int MaxHealthDefault = 100;

        [Header("Sim tuning (mirrors M2)")]
        public float MoveSpeed = 5f;
        public float NeckYawLimitDegrees = 80f;
        public float BodyFollowThresholdDegrees = 50f;
        public float BodyFollowSpeedDegreesPerSecond = 120f;
        public float BodyAlignSpeedDegreesPerSecond = 540f;
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;

        [Header("Networked combat")]
        public float InputDelaySeconds = 0.05f;
        public float LossPercent = 2f;
        public float LagRewindSeconds = 0.1f;
        public float TargetRadius = 0.6f;

        public NetworkVariable<byte> Team = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<M2BodyState> State = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> Alive = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<byte> WeaponId = new((byte)M3WeaponId.Pistol, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> Ammo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> Magazine = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> Reloading = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> BlindRemaining = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> Kills = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> OutsideZone = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<uint> LastAckedP1Sequence = new(0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<uint> LastAckedP2Sequence = new(0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int RejectedFires => _rejectedFires;
        public int ValidatedHits => _validatedHits;

        M2BodySim _sim;
        M2WeaponState _weapon;
        M2LagCompensation _lag;
        readonly M2DelayQueue<M2P1Input> _p1Queue = new M2DelayQueue<M2P1Input>();
        readonly M2DelayQueue<M2P2Input> _p2Queue = new M2DelayQueue<M2P2Input>();

        M3DuelDirector _director;
        M3DuelBody _enemy;
        M3DuelSlot _slotP1 = M3DuelSlot.None;
        M3DuelSlot _slotP2 = M3DuelSlot.None;

        M3WeaponId _activeWeapon = M3WeaponId.Pistol;
        float _blindRemaining;
        float _damageRemainder;
        double _serverTime;
        float _accumulator;
        int _rejectedFires;
        int _validatedHits;

        const float FixedDeltaTime = 1f / 60f;

        public int TeamIndex => Team.Value;

        public override void OnNetworkSpawn()
        {
            InputDelaySeconds = M3Config.OneWayDelaySeconds;
            LossPercent = M3Config.LossPercent;
            LagRewindSeconds = M3Config.LagRewindSeconds;

            _sim = new M2BodySim
            {
                MoveSpeed = MoveSpeed,
                NeckYawLimitDegrees = NeckYawLimitDegrees,
                BodyFollowThresholdDegrees = BodyFollowThresholdDegrees,
                BodyFollowSpeedDegreesPerSecond = BodyFollowSpeedDegreesPerSecond,
                BodyAlignSpeedDegreesPerSecond = BodyAlignSpeedDegreesPerSecond,
                SectorHalfDegrees = SectorHalfDegrees,
                MaxPitchDegrees = MaxPitchDegrees,
                MaxHealth = MaxHealthDefault
            };
            _sim.Initialize(Team.Value == 0 ? 0f : 180f, Team.Value == 0 ? -10f : 10f, 0f);

            _weapon = new M2WeaponState();
            _lag = new M2LagCompensation { MaxRewindSeconds = Mathf.Max(0.2f, LagRewindSeconds * 2f) };
            _p1Queue.LossPercent = LossPercent;
            _p2Queue.LossPercent = LossPercent;

            _slotP1 = M3DuelSlots.FromTeamRole(Team.Value, 0);
            _slotP2 = M3DuelSlots.FromTeamRole(Team.Value, 1);

            if (IsServer)
            {
                State.Value = _sim.State;
                Alive.Value = true;
                ApplyActiveWeapon(force: true);
            }
        }

        /// <summary>Server-only: set the team after spawning (a NetworkVariable cannot be written
        /// before the object is spawned) and re-initialize the team-dependent state.</summary>
        public void ServerConfigure(int team)
        {
            if (!IsServer) return;
            Team.Value = (byte)team;
            _slotP1 = M3DuelSlots.FromTeamRole(team, 0);
            _slotP2 = M3DuelSlots.FromTeamRole(team, 1);
            _sim.Initialize(team == 0 ? 0f : 180f, team == 0 ? -10f : 10f, 0f);
            State.Value = _sim.State;
        }

        /// <summary>Server-only: bind the director and opposing body after both bodies spawn.</summary>
        public void ServerBind(M3DuelDirector director, M3DuelBody enemy)
        {
            _director = director;
            _enemy = enemy;
        }

        // ---- Input RPCs ----

        [ServerRpc(RequireOwnership = false)]
        public void SubmitP1ServerRpc(M2P1Input input, ServerRpcParams rpcParams = default)
        {
            if (_director == null || _director.CurrentPhase == M3Phase.Warmup || !Alive.Value) return;
            if (!HasSlot(rpcParams.Receive.SenderClientId, _slotP1)) { _director.NoteUnauthorized(); return; }
            if (!_director.InputsAccepted) return; // buy/round-end freeze
            _p1Queue.Enqueue(_serverTime, InputDelaySeconds, input);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitP2ServerRpc(M2P2Input input, ServerRpcParams rpcParams = default)
        {
            if (_director == null || _director.CurrentPhase == M3Phase.Warmup || !Alive.Value) return;
            if (!HasSlot(rpcParams.Receive.SenderClientId, _slotP2)) { _director.NoteUnauthorized(); return; }
            if (!_director.InputsAccepted) return;
            _p2Queue.Enqueue(_serverTime, InputDelaySeconds, input);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitBuyServerRpc(int catalogIndex, ServerRpcParams rpcParams = default)
        {
            if (_director == null || !_director.IsBuy) return;
            if (!HasSlot(rpcParams.Receive.SenderClientId, _slotP2)) { _director.NoteUnauthorized(); return; }
            _director.ServerBuy(Team.Value, catalogIndex);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitUtilityServerRpc(byte utilityKind, ServerRpcParams rpcParams = default)
        {
            if (_director == null || !_director.IsLive || !Alive.Value) return;
            if (!HasSlot(rpcParams.Receive.SenderClientId, _slotP2)) { _director.NoteUnauthorized(); return; }
            _director.ServerThrowUtility(Team.Value, (M3UtilityKind)utilityKind, _sim.State.PosX, _sim.State.PosZ, _sim.State.AimYaw);
        }

        bool HasSlot(ulong clientId, M3DuelSlot slot)
            => M3DuelRoleService.Instance != null && M3DuelRoleService.Instance.HasSlot(clientId, slot);

        public bool IsSlotBot(M3DuelSlot slot)
            => M3DuelRoleService.Instance != null && M3DuelRoleService.Instance.IsBot(slot);

        // ---- Server tick ----

        void Update()
        {
            if (!IsServer || _sim == null) return;

            _accumulator += Time.deltaTime;
            int safety = 0;
            while (_accumulator >= FixedDeltaTime && safety++ < 8)
            {
                ServerTick(FixedDeltaTime);
                _accumulator -= FixedDeltaTime;
            }
        }

        void ServerTick(float dt)
        {
            _serverTime += dt;

            if (_blindRemaining > 0f)
            {
                _blindRemaining = Mathf.Max(0f, _blindRemaining - dt);
                BlindRemaining.Value = _blindRemaining;
            }

            // Record the shooter's orientation and the enemy position so a fire can be validated
            // against what the shooter saw (historical sector + rewound target).
            float enemyX = _enemy != null ? _enemy.State.Value.PosX : 0f;
            float enemyZ = _enemy != null ? _enemy.State.Value.PosZ : 0f;
            _lag.Record(_serverTime, _sim.State.BodyYaw, enemyX, enemyZ);

            if (IsSlotBot(_slotP1) && _director != null && _director.IsLive)
            {
                _sim.ApplyP1(BuildBotP1(dt), dt);
            }
            while (_p1Queue.TryDequeue(_serverTime, out M2P1Input p1))
            {
                _sim.ApplyP1(p1, dt);
                LastAckedP1Sequence.Value = p1.Sequence;
            }

            while (_p2Queue.TryDequeue(_serverTime, out M2P2Input p2))
            {
                ProcessP2(in p2);
                LastAckedP2Sequence.Value = p2.Sequence;
            }
            if (IsSlotBot(_slotP2) && _director != null && _director.IsLive) ProcessP2(BuildBotP2());

            _weapon.Tick(_serverTime);
            Ammo.Value = _weapon.Ammo;
            Reloading.Value = _weapon.IsReloading;

            State.Value = _sim.State;

            transform.position = new Vector3(_sim.State.PosX, 0f, _sim.State.PosZ);
            transform.rotation = Quaternion.Euler(0f, _sim.State.BodyYaw, 0f);
        }

        void ProcessP2(in M2P2Input input)
        {
            if (!Alive.Value) return;
            if (input.Reload) _weapon.StartReload(_serverTime);

            if (!input.Fire || _blindRemaining > 0f) { _sim.ApplyP2(in input); return; }

            if (!_lag.TryRewind(_serverTime - LagRewindSeconds, out float historicalBodyYaw, out float histTargetX, out float histTargetZ))
            {
                _sim.ApplyP2(in input);
                return;
            }

            float offset = M2BodySim.Normalize(input.AimYaw - historicalBodyYaw);
            bool legalHistorically = Mathf.Abs(offset) <= SectorHalfDegrees + 0.001f;

            M2WeaponState.FireResult fire = _weapon.TryFire(_serverTime);
            if (!legalHistorically || fire != M2WeaponState.FireResult.Ok)
            {
                _rejectedFires++;
                _sim.ApplyP2(in input);
                return;
            }

            M3WeaponStats stats = M3Loadouts.Stats(_activeWeapon);

            // Smoke blocks the hitscan line (last authoritative smoke state).
            bool blocked = _director != null && _director.Utility.BlocksLine(_serverTime, _sim.State.PosX, _sim.State.PosZ, histTargetX, histTargetZ);
            if (!blocked && _enemy != null && _enemy.Alive.Value)
            {
                Vector3 dir = new Vector3(Mathf.Sin(input.AimYaw * Mathf.Deg2Rad), 0f, Mathf.Cos(input.AimYaw * Mathf.Deg2Rad));
                float toX = histTargetX - _sim.State.PosX;
                float toZ = histTargetZ - _sim.State.PosZ;
                float forward = toX * dir.x + toZ * dir.z;
                float lateral = Mathf.Abs(toX * dir.z - toZ * dir.x);
                bool hit = forward > 0f && forward <= stats.RangeMeters && lateral <= TargetRadius;
                if (hit)
                {
                    _validatedHits++;
                    _director.ServerApplyDamage(_enemy, stats.Damage, this);
                }
            }

            _sim.ApplyP2(in input);
        }

        /// <summary>Server-only: apply damage from a validated hit (or grenade/zone). Discrete hit
        /// damage lands immediately; continuous zone damage accumulates fractionally.</summary>
        public void ServerTakeDamage(float damage, M3DuelBody attacker)
        {
            if (!IsServer || !Alive.Value || damage <= 0f) return;
            _damageRemainder += damage;
            int amount = Mathf.FloorToInt(_damageRemainder);
            if (amount <= 0) return;
            _damageRemainder -= amount;

            _sim.State.Health -= amount;
            if (_sim.State.Health < 0) _sim.State.Health = 0;
            State.Value = _sim.State;

            if (_director != null) _director.NoteDamage(Team.Value, attacker != null ? attacker.Team.Value : -1);

            if (_sim.State.Health <= 0)
            {
                Alive.Value = false;
                State.Value = _sim.State;
                if (attacker != null) attacker.Kills.Value++;
                if (_director != null) _director.OnBodyEliminated(Team.Value);
            }
        }

        /// <summary>Server-only: blind the two roles of this body (flash).</summary>
        public void ServerApplyBlind(float seconds)
        {
            if (!IsServer || seconds <= _blindRemaining) return;
            _blindRemaining = seconds;
            BlindRemaining.Value = _blindRemaining;
        }

        /// <summary>Server-only: regenerate the shot cadence state for the active weapon.</summary>
        public void ApplyActiveWeapon(bool force)
        {
            if (!IsServer) return;
            M3WeaponStats stats = M3Loadouts.Stats(_activeWeapon);
            _weapon.Magazine = stats.Magazine;
            _weapon.SecondsBetweenShots = stats.SecondsBetweenShots;
            _weapon.ReloadSeconds = stats.ReloadSeconds;
            _weapon.Reset();
            WeaponId.Value = (byte)stats.Id;
            Magazine.Value = stats.Magazine;
            Ammo.Value = _weapon.Ammo;
            Reloading.Value = false;
        }

        /// <summary>Server-only: set the loadout chosen during the buy phase.</summary>
        public void ServerApplyLoadout(M3WeaponId weapon)
        {
            if (!IsServer || _activeWeapon == weapon) return;
            _activeWeapon = weapon;
            ApplyActiveWeapon(force: true);
        }

        /// <summary>Server-only: full reset at the start of a round.</summary>
        public void ServerResetRound(float x, float z, float yaw)
        {
            if (!IsServer) return;
            _sim.Initialize(yaw, x, z);
            _sim.State.Health = MaxHealthDefault;
            _blindRemaining = 0f;
            _damageRemainder = 0f;
            BlindRemaining.Value = 0f;
            Alive.Value = true;
            OutsideZone.Value = false;
            _activeWeapon = M3WeaponId.Pistol;
            ApplyActiveWeapon(force: true);
            _lag.Clear();
            _p1Queue.Clear();
            _p2Queue.Clear();
            State.Value = _sim.State;
            transform.position = new Vector3(x, 0f, z);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        M2P1Input BuildBotP1(float dt)
        {
            if (_enemy == null || !_enemy.Alive.Value) return new M2P1Input { MoveZ = 0.5f };
            float dx = _enemy.State.Value.PosX - _sim.State.PosX;
            float dz = _enemy.State.Value.PosZ - _sim.State.PosZ;
            float desired = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            float delta = Mathf.Clamp(M2BodySim.Normalize(desired - _sim.State.LookYaw), -20f, 20f);
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            return new M2P1Input { MoveZ = distance > 8f ? 1f : 0f, LookYawDelta = delta, AlignBody = Mathf.Abs(delta) > 1f };
        }

        M2P2Input BuildBotP2()
        {
            var input = new M2P2Input { AimPitch = 0f, Fire = false };
            if (_enemy == null || !_enemy.Alive.Value) { input.AimYaw = _sim.State.BodyYaw; return input; }
            float dx = _enemy.State.Value.PosX - _sim.State.PosX;
            float dz = _enemy.State.Value.PosZ - _sim.State.PosZ;
            input.AimYaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            input.Fire = true;
            input.Reload = _weapon.Ammo <= 0;
            return input;
        }
    }
}
