using System;
using BeMyArms.M2;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M3
{
    /// <summary>
    /// One shared body of a match (Duel or 2v2). Server-authoritative: it reuses the M2 pure body
    /// simulation (decoupled look / neck limit / Model-C sector clamp), adds role-tagged P1/P2 input
    /// RPCs authorized by <see cref="M3DuelRoleService"/>, owns its buy/utility economy, and does
    /// lag-compensated hitscan against every enemy body. The round loop itself is owned by
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
        public NetworkVariable<byte> BodyIndex = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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

        public int TeamIndex => Team.Value;
        public int BodyId => BodyIndex.Value;
        public int SlotP1 { get; private set; } = -1;
        public int SlotP2 { get; private set; } = -1;

        M2BodySim _sim;
        M2WeaponState _weapon;
        M3MovementCollision _collision;
        M2LagCompensation[] _lag = Array.Empty<M2LagCompensation>();
        readonly M2DelayQueue<M2P1Input> _p1Queue = new M2DelayQueue<M2P1Input>();
        readonly M2DelayQueue<M2P2Input> _p2Queue = new M2DelayQueue<M2P2Input>();
        readonly M3BuyPhase _buy = new M3BuyPhase();
        readonly int[] _utilityCharges = new int[3];

        M3DuelDirector _director;
        M3DuelBody[] _enemies = Array.Empty<M3DuelBody>();

        M3WeaponId _activeWeapon = M3WeaponId.Pistol;
        float _blindRemaining;
        float _damageRemainder;
        float _botClock;
        float _botStuckTime;
        float _botStrafeDir = 1f;
        float _lastBotX;
        float _lastBotZ;
        float _grenadeCooldown = 5f;
        double _serverTime;
        float _accumulator;
        int _rejectedFires;
        int _validatedHits;

        const float FixedDeltaTime = 1f / 60f;

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
            _sim.Initialize(0f, 0f, 0f);

            M3MapSpawns map = FindAnyObjectByType<M3MapSpawns>();
            if (map != null) _collision = map.BuildCollision();
            _sim.MovementConstraint = ClampToArena;

            _weapon = new M2WeaponState();
            _p1Queue.LossPercent = LossPercent;
            _p2Queue.LossPercent = LossPercent;

            if (IsServer)
            {
                State.Value = _sim.State;
                Alive.Value = true;
                ApplyActiveWeapon();
            }
        }

        /// <summary>Server-only: set team/body and the P1 slot base after spawning (a NetworkVariable
        /// cannot be written before the object is spawned).</summary>
        public void ServerConfigure(int team, int body, int slotP1)
        {
            if (!IsServer) return;
            Team.Value = (byte)team;
            BodyIndex.Value = (byte)body;
            SlotP1 = slotP1;
            SlotP2 = slotP1 + 1;
            _sim.Initialize(team == 0 ? 0f : 180f, team == 0 ? -10f : 10f, 0f);
            State.Value = _sim.State;
        }

        /// <summary>Server-only: bind the director and the opposing bodies after all bodies spawn.</summary>
        public void ServerBind(M3DuelDirector director, M3DuelBody[] enemies)
        {
            _director = director;
            _enemies = enemies ?? Array.Empty<M3DuelBody>();
            _lag = new M2LagCompensation[_enemies.Length];
            for (int i = 0; i < _lag.Length; i++)
                _lag[i] = new M2LagCompensation { MaxRewindSeconds = Mathf.Max(0.2f, LagRewindSeconds * 2f) };
        }

        // ---- Input RPCs ----

        [ServerRpc(RequireOwnership = false)]
        public void SubmitP1ServerRpc(M2P1Input input, ServerRpcParams rpcParams = default)
        {
            if (_director == null || _director.CurrentPhase == M3Phase.Warmup || !Alive.Value) return;
            if (!HasSlot(rpcParams.Receive.SenderClientId, SlotP1)) { _director.NoteUnauthorized(); return; }
            if (!_director.InputsAccepted) return; // buy/round-end freeze
            _p1Queue.Enqueue(_serverTime, InputDelaySeconds, input);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitP2ServerRpc(M2P2Input input, ServerRpcParams rpcParams = default)
        {
            if (_director == null || _director.CurrentPhase == M3Phase.Warmup || !Alive.Value) return;
            if (!HasSlot(rpcParams.Receive.SenderClientId, SlotP2)) { _director.NoteUnauthorized(); return; }
            if (!_director.InputsAccepted) return;
            _p2Queue.Enqueue(_serverTime, InputDelaySeconds, input);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitBuyServerRpc(int catalogIndex, ServerRpcParams rpcParams = default)
        {
            if (_director == null || !_director.IsBuy) return;
            if (!HasSlot(rpcParams.Receive.SenderClientId, SlotP2)) { _director.NoteUnauthorized(); return; }
            ServerBuy(catalogIndex);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitUtilityServerRpc(byte utilityKind, ServerRpcParams rpcParams = default)
        {
            if (_director == null || !_director.IsLive || !Alive.Value) return;
            if (!HasSlot(rpcParams.Receive.SenderClientId, SlotP2)) { _director.NoteUnauthorized(); return; }
            M3UtilityKind kind = (M3UtilityKind)utilityKind;
            if (TryConsumeUtility(kind)) _director.ServerApplyUtility(TeamIndex, kind, _sim.State.PosX, _sim.State.PosZ, _sim.State.AimYaw);
        }

        bool HasSlot(ulong clientId, int slot)
            => M3DuelRoleService.Instance != null && M3DuelRoleService.Instance.HasSlot(clientId, slot);

        public bool IsSlotBot(int slot)
            => M3DuelRoleService.Instance != null && M3DuelRoleService.Instance.IsBot(slot);

        /// <summary>Authoritative arena collision, applied after every P1 step (server and replay).</summary>
        void ClampToArena(M2BodySim sim)
        {
            if (_collision == null || _collision.IsEmpty) return;
            float x = sim.State.PosX;
            float z = sim.State.PosZ;
            _collision.Resolve(ref x, ref z);
            sim.State.PosX = x;
            sim.State.PosZ = z;
        }

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

            // Record the shooter's orientation and each enemy position so a fire can be validated
            // against what the shooter saw (historical sector + rewound targets).
            for (int i = 0; i < _enemies.Length; i++)
            {
                M3DuelBody e = _enemies[i];
                float ex = e != null ? e.State.Value.PosX : 0f;
                float ez = e != null ? e.State.Value.PosZ : 0f;
                if (e != null && e.Alive.Value) _lag[i].Record(_serverTime, _sim.State.BodyYaw, ex, ez);
                else _lag[i].Record(_serverTime, _sim.State.BodyYaw, ex, ez);
            }

            if (IsSlotBot(SlotP1) && _director != null && _director.IsLive)
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
            if (IsSlotBot(SlotP2) && _director != null && _director.IsLive) ProcessP2(BuildBotP2());

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

            // All lag samples share the shooter's own yaw; use the first for sector legality.
            if (_lag.Length == 0 || !_lag[0].TryRewind(_serverTime - LagRewindSeconds, out float historicalBodyYaw, out _, out _))
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
            Vector3 dir = new Vector3(Mathf.Sin(input.AimYaw * Mathf.Deg2Rad), 0f, Mathf.Cos(input.AimYaw * Mathf.Deg2Rad));

            M3DuelBody bestTarget = null;
            float bestForward = float.MaxValue;
            float bestX = 0f, bestZ = 0f;
            for (int i = 0; i < _enemies.Length; i++)
            {
                M3DuelBody enemy = _enemies[i];
                if (enemy == null || !enemy.Alive.Value) continue;
                if (!_lag[i].TryRewind(_serverTime - LagRewindSeconds, out _, out float ex, out float ez)) continue;

                float toX = ex - _sim.State.PosX;
                float toZ = ez - _sim.State.PosZ;
                float forward = toX * dir.x + toZ * dir.z;
                float lateral = Mathf.Abs(toX * dir.z - toZ * dir.x);
                bool hit = forward > 0f && forward <= stats.RangeMeters && lateral <= TargetRadius;
                if (hit && forward < bestForward)
                {
                    bestForward = forward;
                    bestTarget = enemy;
                    bestX = ex;
                    bestZ = ez;
                }
            }

            if (bestTarget != null)
            {
                bool blocked = _director != null && _director.Utility.BlocksLine(_serverTime, _sim.State.PosX, _sim.State.PosZ, bestX, bestZ);
                if (!blocked)
                {
                    _validatedHits++;
                    _director.ServerApplyDamage(bestTarget, stats.Damage, this);
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

            if (_director != null) _director.NoteDamage(TeamIndex, attacker != null ? attacker.TeamIndex : -1);

            if (_sim.State.Health <= 0)
            {
                Alive.Value = false;
                State.Value = _sim.State;
                if (attacker != null) attacker.Kills.Value++;
                if (_director != null) _director.OnBodyEliminated(TeamIndex);
            }
        }

        /// <summary>Server-only: blind the two roles of this body (flash).</summary>
        public void ServerApplyBlind(float seconds)
        {
            if (!IsServer || seconds <= _blindRemaining) return;
            _blindRemaining = seconds;
            BlindRemaining.Value = _blindRemaining;
        }

        /// <summary>Server-only: buy an item during the buy phase (owned per body/P2).</summary>
        public void ServerBuy(int catalogIndex)
        {
            if (!IsServer || !_director.IsBuy) return;
            if (catalogIndex < 0 || catalogIndex >= _buy.Catalog.Count) return;

            M3ShopItem item = _buy.Catalog[catalogIndex];
            if (!_buy.TryBuy(item.Id)) return;

            if (item.Kind == M3ShopKind.Utility)
            {
                M3UtilityKind kind = item.Id == "grenade" ? M3UtilityKind.Grenade : item.Id == "flash" ? M3UtilityKind.Flash : M3UtilityKind.Smoke;
                _utilityCharges[(int)kind]++;
            }

            ServerApplyLoadout(M3Loadouts.ActiveWeapon(_buy));
            Log($"bought {item.Id} (spent {_buy.Spent}/{_buy.Budget})");
        }

        public bool TryConsumeUtility(M3UtilityKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= _utilityCharges.Length || _utilityCharges[index] <= 0) return false;
            _utilityCharges[index]--;
            return true;
        }

        void ApplyActiveWeapon()
        {
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

        public void ServerApplyLoadout(M3WeaponId weapon)
        {
            if (!IsServer || _activeWeapon == weapon) return;
            _activeWeapon = weapon;
            ApplyActiveWeapon();
        }

        /// <summary>Server-only: full reset at the start of a round, including economy.</summary>
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
            ApplyActiveWeapon();
            _buy.ResetForRound();
            _utilityCharges[0] = _utilityCharges[1] = _utilityCharges[2] = 0;
            for (int i = 0; i < _lag.Length; i++) _lag[i].Clear();
            _p1Queue.Clear();
            _p2Queue.Clear();
            State.Value = _sim.State;
            transform.position = new Vector3(x, 0f, z);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        M3DuelBody NearestEnemy(out float distance)
        {
            M3DuelBody best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _enemies.Length; i++)
            {
                M3DuelBody e = _enemies[i];
                if (e == null || !e.Alive.Value) continue;
                float dx = e.State.Value.PosX - _sim.State.PosX;
                float dz = e.State.Value.PosZ - _sim.State.PosZ;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < bestDistance) { bestDistance = d; best = e; }
            }
            distance = bestDistance;
            return best;
        }

        M2P1Input BuildBotP1(float dt)
        {
            var input = new M2P1Input();
            M3DuelBody target = NearestEnemy(out float distance);
            if (target == null) { input.MoveZ = 0.5f; return input; }

            float dx = target.State.Value.PosX - _sim.State.PosX;
            float dz = target.State.Value.PosZ - _sim.State.PosZ;
            float desired = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            float delta = Mathf.Clamp(M2BodySim.Normalize(desired - _sim.State.LookYaw), -25f, 25f);
            input.LookYawDelta = delta;
            input.AlignBody = Mathf.Abs(delta) > 1.5f;

            // Simple wall avoidance: if advancing barely moves the body, strafe instead.
            float moved = Mathf.Sqrt((_sim.State.PosX - _lastBotX) * (_sim.State.PosX - _lastBotX) +
                                     (_sim.State.PosZ - _lastBotZ) * (_sim.State.PosZ - _lastBotZ));
            _lastBotX = _sim.State.PosX;
            _lastBotZ = _sim.State.PosZ;
            if (moved < 0.01f) _botStuckTime += dt;
            else _botStuckTime = Mathf.Max(0f, _botStuckTime - dt * 2f);
            if (_botStuckTime > 0.4f)
            {
                _botStrafeDir = _botStrafeDir >= 0f ? -1f : 1f;
                _botStuckTime = 0f;
            }

            bool advance = distance > 10f;
            input.MoveZ = _botStuckTime > 0f ? 0f : (advance ? 1f : 0.35f);
            input.MoveX = _botStuckTime > 0f ? _botStrafeDir : Mathf.Sin(_botClock * 0.7f) * (distance < 12f ? 0.9f : 0.25f);
            _botClock += dt;
            return input;
        }

        M2P2Input BuildBotP2()
        {
            var input = new M2P2Input { AimPitch = 0f, Fire = false };
            M3DuelBody target = NearestEnemy(out float distance);
            if (target == null) { input.AimYaw = _sim.State.BodyYaw; return input; }

            float dx = target.State.Value.PosX - _sim.State.PosX;
            float dz = target.State.Value.PosZ - _sim.State.PosZ;
            input.AimYaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            input.Fire = true;
            input.Reload = _weapon.Ammo <= 0;

            // Occasional utility through the normal P2 authority path.
            _grenadeCooldown -= 1f / 60f;
            if (_director != null && _director.IsLive && distance < 24f && _grenadeCooldown <= 0f && TryConsumeUtility(M3UtilityKind.Grenade))
            {
                _director.ServerApplyUtility(TeamIndex, M3UtilityKind.Grenade, _sim.State.PosX, _sim.State.PosZ, _sim.State.AimYaw);
                _grenadeCooldown = 9f;
            }
            return input;
        }

        /// <summary>Server-only: give a bot-owned P2 a legal loadout at the start of a round.</summary>
        public void ServerAutoBuyBotLoadout()
        {
            if (!IsServer) return;
            ServerBuy(0); // rifle (primary)
            ServerBuy(6); // grenade (utility)
        }

        void Log(string message)
            => Debug.Log($"[M3] t={Time.realtimeSinceStartup:0.000} {M3DuelSlots.Name(SlotP1, _director != null ? _director.BodiesPerTeam : 1)} {message}");
    }
}
