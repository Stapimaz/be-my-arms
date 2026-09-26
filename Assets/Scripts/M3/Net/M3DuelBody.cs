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
        public float WalkSpeed = 4.5f;
        public float SprintSpeed = 7f;
        public float JumpSpeed = 7f;
        public float Gravity = -20f;
        public float NeckYawLimitDegrees = 80f;
        public float BodyFollowThresholdDegrees = 50f;
        public float BodyFollowSpeedDegreesPerSecond = 120f;
        public float BodyAlignSpeedDegreesPerSecond = 540f;
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;

        [Header("Melee (server-authoritative)")]
        public float LightKickDamage = 12f;
        public float HeavyKickDamage = 28f;
        public float KickRangeMeters = 2.4f;

        [Header("Round pacing")]
        [Tooltip("Brief post-spawn damage grace so a body is not deleted the instant the live phase starts.")]
        public float SpawnGraceSeconds = 4f;

        [Header("Bot practice tuning (forgiving learning baseline)")]
        [Tooltip("Seconds into the live phase before a bot starts reacting at all.")]
        public float BotReactionSeconds = 1.6f;
        public float BotAimErrorBase = 3.5f;
        public float BotAimErrorPerMeter = 0.45f;
        public float BotBurstOnSeconds = 0.35f;
        public float BotBurstPeriodSeconds = 1.7f;
        public float BotMaxEngageDistance = 40f;

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
        M2LagCompensation[] _lag = Array.Empty<M2LagCompensation>();
        readonly M2DelayQueue<M2P1Input> _p1Queue = new M2DelayQueue<M2P1Input>();
        readonly M2DelayQueue<M2P2Input> _p2Queue = new M2DelayQueue<M2P2Input>();
        readonly M3BuyPhase _buy = new M3BuyPhase();
        readonly int[] _utilityCharges = new int[3];

        M3DuelDirector _director;
        M3DuelBody[] _enemies = Array.Empty<M3DuelBody>();

        M3WeaponId _activeWeapon = M3WeaponId.Pistol;
        M2MovementState _prevMovementState = M2MovementState.Idle;
        float _spawnGraceRemaining;
        float _blindRemaining;
        float _damageRemainder;
        float _botClock;
        float _botP2Clock;
        float _botStuckTime;
        float _botStrafeDir = 1f;
        float _lastBotX;
        float _lastBotZ;
        float _grenadeCooldown = 5f;
        float _botLiveTime;

        // Per-body randomised bot profile (seeded per spawn/round) so practice bots do not share one
        // deterministic firing clock.
        int _botSeed;
        float _botReaction;
        float _botBurstOn;
        float _botBurstPeriod;
        float _botFirePhase;
        float _botAimRate1;
        float _botAimRate2;
        float _botAimPhase1;
        float _botAimPhase2;
        float _botAimDrift;
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
                WalkSpeed = WalkSpeed,
                SprintSpeed = SprintSpeed,
                JumpSpeed = JumpSpeed,
                Gravity = Gravity,
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
            if (map != null) _sim.Collision = map.BuildCollision();

            _weapon = new M2WeaponState();
            _p1Queue.LossPercent = LossPercent;
            _p2Queue.LossPercent = LossPercent;

            if (IsServer)
            {
                State.Value = _sim.State;
                Alive.Value = true;
                ApplyActiveWeapon();
                InitializeBotProfile();
            }
        }

        /// <summary>
        /// Seeds this body's practice-bot timing. Each body/round gets different reaction, burst
        /// length, pause, firing phase and aim-error character so bots do not fire in lockstep.
        /// </summary>
        void InitializeBotProfile()
        {
            _botSeed = UnityEngine.Random.Range(0, 1 << 30);
            var rng = new System.Random(unchecked(_botSeed * 486187739 + SlotP1 * 73856093 + SlotP2 * 19349663));
            _botReaction = Mathf.Lerp(1.1f, 2.6f, (float)rng.NextDouble());
            _botBurstOn = Mathf.Lerp(0.22f, 0.5f, (float)rng.NextDouble());
            _botBurstPeriod = Mathf.Lerp(1.2f, 2.2f, (float)rng.NextDouble());
            _botFirePhase = (float)rng.NextDouble() * _botBurstPeriod;
            _botAimRate1 = Mathf.Lerp(0.7f, 1.6f, (float)rng.NextDouble());
            _botAimRate2 = Mathf.Lerp(0.2f, 0.7f, (float)rng.NextDouble());
            _botAimPhase1 = (float)rng.NextDouble() * 6.2831853f;
            _botAimPhase2 = (float)rng.NextDouble() * 6.2831853f;
            _botAimDrift = Mathf.Lerp(0.9f, 1.5f, (float)rng.NextDouble());
            _botClock = (float)rng.NextDouble() * 6.2831853f;
            _botP2Clock = (float)rng.NextDouble() * 6.2831853f;
            _grenadeCooldown = Mathf.Lerp(8f, 18f, (float)rng.NextDouble());
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
            // P1 input is accepted during Buy so look stays responsive; movement/actions are stripped
            // in ServerTick until the live phase (see LookOnly).
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

        [ClientRpc]
        void DamageFeedbackClientRpc(byte attackerTeam, byte attackerBody, byte victimTeam, byte victimBody, Vector3 point, bool killed)
        {
            M3CombatEvents.RaiseDamage(new M3DamageEvent
            {
                AttackerTeam = attackerTeam == 255 ? -1 : attackerTeam,
                AttackerBody = attackerBody == 255 ? -1 : attackerBody,
                VictimTeam = victimTeam == 255 ? -1 : victimTeam,
                VictimBody = victimBody == 255 ? -1 : victimBody,
                Point = point,
                Killed = killed
            });
        }

        [ClientRpc]
        void WorldImpactClientRpc(Vector3 point) => M3CombatEvents.RaiseWorldImpact(point);

        bool HasSlot(ulong clientId, int slot)
            => M3DuelRoleService.Instance != null && M3DuelRoleService.Instance.HasSlot(clientId, slot);

        public bool IsSlotBot(int slot)
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

            // Bots hold still for a moment at the start of the live phase so a learning player has
            // time to look around before they are engaged.
            if (_director != null && _director.IsLive) _botLiveTime += dt;
            else _botLiveTime = 0f;

            if (_spawnGraceRemaining > 0f) _spawnGraceRemaining = Mathf.Max(0f, _spawnGraceRemaining - dt);

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
                // Look is always applied; movement/actions only once the round is live, so the body
                // stays frozen through Buy even though look input is accepted.
                _sim.ApplyP1(_director != null && _director.IsLive ? p1 : LookOnly(p1), dt);
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

            // A kick that just started this tick resolves its authoritative hit once.
            M2MovementState movement = (M2MovementState)_sim.State.MovementState;
            if (movement != _prevMovementState &&
                (movement == M2MovementState.KickLight || movement == M2MovementState.KickHeavy))
                ResolveKick(movement);
            _prevMovementState = movement;

            State.Value = _sim.State;

            transform.position = new Vector3(_sim.State.PosX, _sim.State.PosY, _sim.State.PosZ);
            transform.rotation = Quaternion.Euler(0f, _sim.State.BodyYaw, 0f);
        }

        /// <summary>Server-only: a started kick hits the nearest enemy in front within range.</summary>
        void ResolveKick(M2MovementState kind)
        {
            if (_director == null) return;
            float damage = kind == M2MovementState.KickHeavy ? HeavyKickDamage : LightKickDamage;
            float range = KickRangeMeters + 0.4f;

            float rad = _sim.State.BodyYaw * Mathf.Deg2Rad;
            float fx = Mathf.Sin(rad), fz = Mathf.Cos(rad);
            float feet = _sim.State.PosY;

            M3DuelBody best = null;
            float bestForward = float.MaxValue;
            for (int i = 0; i < _enemies.Length; i++)
            {
                M3DuelBody enemy = _enemies[i];
                if (enemy == null || !enemy.Alive.Value) continue;
                M2BodyState es = enemy.State.Value;

                float toX = es.PosX - _sim.State.PosX;
                float toZ = es.PosZ - _sim.State.PosZ;
                float forward = toX * fx + toZ * fz;
                float lateral = Mathf.Abs(toX * fz - toZ * fx);
                if (forward <= 0f || forward > range || lateral > 0.9f) continue;

                // Arms reach the torso, not the legs or head.
                if (es.PosY + 2.0f < feet || es.PosY > feet + 1.9f) continue;

                if (forward < bestForward) { bestForward = forward; best = enemy; }
            }

            if (best != null) _director.ServerApplyDamage(best, damage, this);
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

            // Vertical aim must agree with the authoritative hit ray: build the direction from BOTH
            // the (sector-legal) yaw and the pitch, and test it against the enemy's vertical extent.
            float eye = _sim.State.PosY + 1.45f;
            Vector3 origin = new Vector3(_sim.State.PosX, eye, _sim.State.PosZ);
            Vector3 dir = Quaternion.Euler(input.AimPitch, input.AimYaw, 0f) * Vector3.forward;

            float wallDistance = float.MaxValue;
            bool wallBlocked = _sim.Collision != null &&
                _sim.Collision.RaycastSolids(origin.x, origin.y, origin.z, dir.x, dir.y, dir.z,
                    stats.RangeMeters, out wallDistance);

            M3DuelBody bestTarget = null;
            float bestForward = float.MaxValue;
            float bestX = 0f, bestZ = 0f;
            for (int i = 0; i < _enemies.Length; i++)
            {
                M3DuelBody enemy = _enemies[i];
                if (enemy == null || !enemy.Alive.Value) continue;
                if (!_lag[i].TryRewind(_serverTime - LagRewindSeconds, out _, out float ex, out float ez)) continue;

                float enemyFeet = enemy.State.Value.PosY;
                float forward = RaySegmentDistance(origin, dir, ex, enemyFeet, ez, 1.8f, stats.RangeMeters, out float lateral);
                if (lateral > TargetRadius + 0.15f || forward <= 0f) continue;
                if (wallBlocked && wallDistance < forward) continue;
                if (forward < bestForward)
                {
                    bestForward = forward;
                    bestTarget = enemy;
                    bestX = ex;
                    bestZ = ez;
                }
            }

            if (bestTarget != null && (_director == null || !_director.Utility.BlocksLine(_serverTime, _sim.State.PosX, _sim.State.PosZ, bestX, bestZ)))
            {
                _validatedHits++;
                _director.ServerApplyDamage(bestTarget, stats.Damage, this);
            }
            else if (bestTarget == null && wallBlocked && wallDistance < stats.RangeMeters)
            {
                // Authoritative miss into geometry: tell clients where to show the impact.
                WorldImpactClientRpc(origin + dir * wallDistance);
            }

            _sim.ApplyP2(in input);
        }

        /// <summary>
        /// Closest approach between a (normalised) ray and an enemy's vertical body segment, sampled
        /// along the segment. Returns the forward distance and the lateral miss distance.
        /// </summary>
        static float RaySegmentDistance(Vector3 origin, Vector3 dir, float cx, float feet, float cz,
            float height, float range, out float lateral)
        {
            const int samples = 8;
            float bestForward = -1f;
            lateral = float.MaxValue;
            for (int s = 0; s <= samples; s++)
            {
                float y = feet + height * (s / (float)samples);
                Vector3 p = new Vector3(cx, y, cz);
                float t = Vector3.Dot(p - origin, dir);
                if (t <= 0f || t > range) continue;
                float miss = Vector3.Distance(origin + dir * t, p);
                if (miss < lateral)
                {
                    lateral = miss;
                    bestForward = t;
                }
            }
            return bestForward;
        }

        /// <summary>Server-only: apply damage from a validated hit (or grenade/zone). Discrete hit
        /// damage lands immediately; continuous zone damage accumulates fractionally.</summary>
        public void ServerTakeDamage(float damage, M3DuelBody attacker)
        {
            if (!IsServer || !Alive.Value || damage <= 0f) return;
            if (_spawnGraceRemaining > 0f) return; // brief post-spawn protection
            _damageRemainder += damage;
            int amount = Mathf.FloorToInt(_damageRemainder);
            if (amount <= 0) return;
            _damageRemainder -= amount;

            _sim.State.Health -= amount;
            if (_sim.State.Health < 0) _sim.State.Health = 0;
            State.Value = _sim.State;

            if (_director != null) _director.NoteDamage(TeamIndex, attacker != null ? attacker.TeamIndex : -1);

            // Authoritative combat feedback for all clients (hitmarker / damage / impact particles).
            DamageFeedbackClientRpc(
                attacker != null ? (byte)attacker.TeamIndex : (byte)255,
                attacker != null ? (byte)attacker.BodyId : (byte)255,
                (byte)TeamIndex, (byte)BodyId,
                new Vector3(_sim.State.PosX, _sim.State.PosY + 1.15f, _sim.State.PosZ),
                _sim.State.Health <= 0);

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
        public void ServerResetRound(float x, float y, float z, float yaw)
        {
            if (!IsServer) return;
            _sim.Initialize(yaw, x, z, y);
            _sim.State.Health = MaxHealthDefault;
            _spawnGraceRemaining = SpawnGraceSeconds;
            _blindRemaining = 0f;
            _damageRemainder = 0f;
            BlindRemaining.Value = 0f;
            Alive.Value = true;
            OutsideZone.Value = false;
            _activeWeapon = M3WeaponId.Pistol;
            ApplyActiveWeapon();
            _buy.ResetForRound();
            _utilityCharges[0] = _utilityCharges[1] = _utilityCharges[2] = 0;
            InitializeBotProfile();
            for (int i = 0; i < _lag.Length; i++) _lag[i].Clear();
            _p1Queue.Clear();
            _p2Queue.Clear();
            State.Value = _sim.State;
            transform.position = new Vector3(x, y, z);
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
            // Idle through the reaction window so the player is not rushed at the start of live.
            if (_botLiveTime < _botReaction) return input;

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

            bool advance = distance > 10f && _botLiveTime > _botReaction;
            input.MoveZ = _botStuckTime > 0f ? 0f : (advance ? 1f : 0.35f);
            input.MoveX = _botStuckTime > 0f ? _botStrafeDir : Mathf.Sin(_botClock * 0.7f) * (distance < 12f ? 0.9f : 0.25f);
            input.Jump = _botStuckTime > 0.3f; // hop over low cover when progress stalls
            input.Sprint = distance > 16f && _botLiveTime > _botReaction + 1.5f;
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

            // Forgiving practice baseline with per-bot character: hold fire through a randomised
            // reaction window, drift the aim with two per-bot sine components, and fire in short
            // bursts whose length/pause/phase differ per bot so they never fire in lockstep.
            _botP2Clock += 1f / 60f;
            float error = (BotAimErrorBase + distance * BotAimErrorPerMeter) * _botAimDrift;
            input.AimYaw += Mathf.Sin(_botP2Clock * _botAimRate1 + _botAimPhase1) * error
                          + Mathf.Sin(_botP2Clock * _botAimRate2 + _botAimPhase2) * error * 0.6f;
            input.AimPitch += Mathf.Sin(_botP2Clock * _botAimRate2 * 1.7f + _botAimPhase1) * error * 0.4f;
            bool burst = ((_botP2Clock + _botFirePhase) % _botBurstPeriod) < _botBurstOn;
            input.Fire = burst && distance < BotMaxEngageDistance && _botLiveTime > _botReaction;
            input.Reload = _weapon.Ammo <= 0;

            // Occasional utility through the normal P2 authority path.
            _grenadeCooldown -= 1f / 60f;
            if (_director != null && _director.IsLive && _botLiveTime > _botReaction + 4f &&
                distance < 24f && _grenadeCooldown <= 0f && TryConsumeUtility(M3UtilityKind.Grenade))
            {
                _director.ServerApplyUtility(TeamIndex, M3UtilityKind.Grenade, _sim.State.PosX, _sim.State.PosZ, _sim.State.AimYaw);
                _grenadeCooldown = Mathf.Lerp(10f, 18f, UnityEngine.Random.value);
            }
            return input;
        }

        /// <summary>Server-only: give a bot-owned P2 a legal loadout at the start of a round. This
        /// vertical slice equips the single rifle only; it bypasses the buy-phase gate so the bot is
        /// armed even if the phase transition and the round-start callback race.</summary>
        public void ServerAutoBuyBotLoadout()
        {
            if (!IsServer) return;
            ServerApplyLoadout(M3WeaponId.Rifle);
        }

        /// <summary>Keeps only look deltas from a P1 input (movement/actions stripped).</summary>
        static M2P1Input LookOnly(in M2P1Input input)
        {
            return new M2P1Input
            {
                Sequence = input.Sequence,
                LookYawDelta = input.LookYawDelta,
                LookPitchDelta = input.LookPitchDelta
            };
        }

        void Log(string message)
            => Debug.Log($"[M3] t={Time.realtimeSinceStartup:0.000} {M3DuelSlots.Name(SlotP1, _director != null ? _director.BodiesPerTeam : 1)} {message}");
    }
}
