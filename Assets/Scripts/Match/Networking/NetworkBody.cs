using System;
using BeMyArms.Core;
using BeMyArms.Networking;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.Match
{
    /// <summary>
    /// One shared body of a match (Duel or 2v2). Server-authoritative: it reuses the Networking pure body
    /// simulation (decoupled look / neck limit / Model-C sector clamp), adds role-tagged P1/P2 input
    /// RPCs authorized by <see cref="MatchRoleService"/>, owns its buy/utility economy, and does
    /// lag-compensated hitscan against every enemy body. The round loop itself is owned by
    /// <see cref="MatchDirector"/>.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M3", "BeMyArms.M3", "M3DuelBody")]
    public class NetworkBody : NetworkBehaviour
    {
        public const int MaxHealthDefault = 100;

        [Header("Sim tuning (mirrors Networking)")]
        public float WalkSpeed = 4.5f;
        public float SprintSpeed = 7f;
        public float JumpSpeed = BodySim.DefaultJumpSpeed;
        public float Gravity = BodySim.DefaultGravity;
        public float NeckYawLimitDegrees = BodySim.DefaultNeckYawLimitDegrees;
        public float BodyFollowThresholdDegrees = BodySim.DefaultBodyFollowThresholdDegrees;
        public float BodyFollowSpeedDegreesPerSecond = BodySim.DefaultBodyFollowSpeedDegreesPerSecond;
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

        [Header("Bot difficulty (server)")]
        [Tooltip("Easy bots' chance to choose an accurate burst aim goal. Smooth tracking, movement " +
                 "and cover also affect the actual hit rate; this is not a damage dice roll.")]
        [Range(0.02f, 0.6f)] public float EasyBotAccuracy = 0.10f;

        [Header("Networked combat")]
        public float InputDelaySeconds = 0.05f;
        public float LossPercent = 2f;
        public float LagRewindSeconds = 0.1f;

        public NetworkVariable<byte> Team = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<byte> BodyIndex = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<BodyState> State = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> Alive = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<byte> WeaponId = new((byte)WeaponType.Pistol, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> Ammo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> Magazine = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> Reloading = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> BlindRemaining = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> Kills = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> OutsideZone = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<uint> LastAckedP1Sequence = new(0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<uint> LastAckedP2Sequence = new(0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<uint> ShotsFired = new(0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> P1Bot = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> P2Bot = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> Firing = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> ProtectionRemaining = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> TurnRequest = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        double _turnRequestUntil;

        [ServerRpc(RequireOwnership = false)]
        public void RequestTurnServerRpc(int side, uint epoch, ServerRpcParams rpcParams = default)
        {
            if (!Alive.Value || epoch != _controlEpoch || _director == null || !(_director.IsBuy || _director.IsLive) ||
                !HasSlot(rpcParams.Receive.SenderClientId, SlotP2)) return;
            TurnRequest.Value = Math.Sign(side);
            _turnRequestUntil = _serverTime + 1.5;
        }

        readonly AimHistory _aimHistory = new AimHistory();
        P2Input _heldP2;
        double _lastP2Time = -1;
        uint _acceptedP2, _controlEpoch, _simulationTick;
        ulong _ownerP1 = ulong.MaxValue, _ownerP2 = ulong.MaxValue;

        public int TeamIndex => Team.Value;
        public int BodyId => BodyIndex.Value;
        public int SlotP1 { get; private set; } = -1;
        public int SlotP2 { get; private set; } = -1;

        BodySim _sim;
        WeaponState _weapon;
        readonly RifleHandling _rifle = new RifleHandling();
        public Vector3 LastShotDirection { get; private set; }
        public float LastShotSpread { get; private set; }
        public int RifleBurst => _rifle.Burst;
        LagCompensation[] _lag = Array.Empty<LagCompensation>();
        readonly P1CommandStream _p1Stream = new P1CommandStream();
        readonly DelayQueue<P2Input> _p2Queue = new DelayQueue<P2Input>();
        readonly BuyPhase _buy = new BuyPhase();
        readonly int[] _utilityCharges = new int[3];

        MatchDirector _director;
        NetworkBody[] _enemies = Array.Empty<NetworkBody>();

        WeaponType _activeWeapon = WeaponType.Pistol;
        BodyMovementState _prevMovementState = BodyMovementState.Idle;
        float _spawnGraceRemaining;
        float _blindRemaining;
        float _damageRemainder;
        float _botP2Clock;
        float _grenadeCooldown = 5f;
        float _botLiveTime;

        // Per-body randomised bot profile (seeded per spawn/round) so practice bots do not share one
        // deterministic firing clock.
        int _botSeed;
        System.Random _botRng;
        BotDifficulty _difficulty = BotDifficulty.Easy;
        float _botReaction;
        float _botBurstOn;
        float _botBurstPeriod;
        float _botFirePhase;
        float _botAimRate1;
        float _botAimRate2;
        float _botAimPhase1;
        float _botAimPhase2;
        float _botAimDrift;
        // Easy: persistent burst aim goal (metres in the target plane), never per-shot pose jumps.
        float _botShotRight;
        float _botShotUp;
        float _botPreferredRange;
        readonly BotPositioning _botPositioning = new BotPositioning();
        readonly BotAimMotion _botAimMotion = new BotAimMotion();
        NetworkBody _botTarget;
        BodyState _botTargetPose;
        bool _botVisible, _botEnemyFiring, _botWasBurst;
        double _botSenseAt, _botSeenAt = -100;
        double _serverTime;
        float _accumulator;
        int _rejectedFires;
        int _validatedHits;

        const float FixedDeltaTime = 1f / 60f;

        public override void OnNetworkSpawn()
        {
            InputDelaySeconds = MatchConfig.OneWayDelaySeconds;
            LossPercent = MatchConfig.LossPercent;
            LagRewindSeconds = MatchConfig.LagRewindSeconds;

            _sim = new BodySim
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
                SectorOvertravelDegrees = SectorWall.OvertravelDegrees,
                MaxPitchDegrees = MaxPitchDegrees,
                MaxHealth = MaxHealthDefault
            };
            _sim.Initialize(0f, 0f, 0f);

            MapSpawns map = FindAnyObjectByType<MapSpawns>();
            if (map != null) _sim.Collision = map.BuildCollision();

            _weapon = new WeaponState();
            _p1Stream.LossPercent = LossPercent;
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
        /// Seeds this body's bot profile for the current server difficulty. Each body/round gets its
        /// own seeded RNG, so reaction, burst length, pause and (Easy) burst aim goals differ.
        /// Both difficulties use the same geometry-aware positioning and bounded aim controller.
        /// </summary>
        void InitializeBotProfile()
        {
            _difficulty = MatchConfig.BotDifficulty;
            _botSeed = UnityEngine.Random.Range(0, 1 << 30);
            _botRng = new System.Random(unchecked(_botSeed * 486187739 + SlotP1 * 73856093 + SlotP2 * 19349663));

            if (_difficulty == BotDifficulty.Easy)
            {
                _botReaction = Mathf.Lerp(2.2f, 3.8f, Rand());
                _botBurstOn = Mathf.Lerp(0.14f, 0.30f, Rand());
                _botBurstPeriod = Mathf.Lerp(1.9f, 3.3f, Rand());
            }
            else
            {
                _botReaction = Mathf.Lerp(1.1f, 2.6f, Rand());
                _botBurstOn = Mathf.Lerp(0.22f, 0.5f, Rand());
                _botBurstPeriod = Mathf.Lerp(1.2f, 2.2f, Rand());
            }

            _botFirePhase = Rand() * _botBurstPeriod;
            _botAimRate1 = Mathf.Lerp(0.7f, 1.6f, Rand());
            _botAimRate2 = Mathf.Lerp(0.2f, 0.7f, Rand());
            _botAimPhase1 = Rand() * 6.2831853f;
            _botAimPhase2 = Rand() * 6.2831853f;
            _botAimDrift = Mathf.Lerp(0.9f, 1.5f, Rand());
            _botP2Clock = Rand() * 6.2831853f;
            _grenadeCooldown = Mathf.Lerp(8f, 18f, Rand());
            _botPreferredRange = Mathf.Lerp(11f, 15f, Rand());
            ResetBotControls();
            SampleBotShot();
        }

        float Rand() => (float)_botRng.NextDouble();

        /// <summary>Choose a burst aim destination; the motion controller must turn toward it.</summary>
        void SampleBotShot()
        {
            BotAim.SampleShotOffset(EasyBotAccuracy, CombatHitGeometry.BodyRadius,
                _botRng.NextDouble(), _botRng.NextDouble(), _botRng.NextDouble(),
                out _botShotRight, out _botShotUp);
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
        public void ServerBind(MatchDirector director, NetworkBody[] enemies)
        {
            _director = director;
            _enemies = enemies ?? Array.Empty<NetworkBody>();
            _lag = new LagCompensation[_enemies.Length];
            for (int i = 0; i < _lag.Length; i++)
                _lag[i] = new LagCompensation { MaxRewindSeconds = Mathf.Max(0.2f, LagRewindSeconds * 2f) };
        }

        // ---- Input RPCs ----

        [ServerRpc(RequireOwnership = false)]
        public void SubmitP1ServerRpc(P1Input input, ServerRpcParams rpcParams = default)
        {
            if (_director == null || !Alive.Value || !(_director.IsBuy || _director.IsLive)) return;
            if (!HasSlot(rpcParams.Receive.SenderClientId, SlotP1)) { _director.NoteUnauthorized(); return; }
            // P1 input is accepted during Buy so look stays responsive; movement/actions are stripped
            // in ServerTick until the live phase (see LookOnly).
            _p1Stream.Submit(input, _serverTime, InputDelaySeconds);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitP2ServerRpc(P2Input input, ServerRpcParams rpcParams = default)
        {
            if (_director == null || _director.CurrentPhase == RoundPhase.Warmup || !Alive.Value) return;
            if (!HasSlot(rpcParams.Receive.SenderClientId, SlotP2)) { _director.NoteUnauthorized(); return; }
            if (!(_director.IsBuy || _director.IsLive)) return;
            if (input.ControlEpoch != _controlEpoch || input.Sequence <= _acceptedP2 || !InputStream.Valid(input) || _p2Queue.Count >= 120) return;
            _acceptedP2 = input.Sequence;
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
            UtilityKind kind = (UtilityKind)utilityKind;
            if (TryConsumeUtility(kind)) _director.ServerApplyUtility(TeamIndex, kind, _sim.State.PosX, _sim.State.PosZ, _sim.State.AimYaw);
        }

        [ClientRpc]
        void DamageFeedbackClientRpc(byte attackerTeam, byte attackerBody, byte victimTeam, byte victimBody,
            Vector3 point, bool killed, int amount, DamageKind kind, byte region, uint attackerEpoch, uint victimEpoch)
        {
            CombatEvents.RaiseDamage(new DamageEvent
            {
                AttackerTeam = attackerTeam == 255 ? -1 : attackerTeam,
                AttackerBody = attackerBody == 255 ? -1 : attackerBody,
                VictimTeam = victimTeam == 255 ? -1 : victimTeam,
                VictimBody = victimBody == 255 ? -1 : victimBody,
                Point = point,
                Killed = killed,
                Amount = amount,
                Kind = kind,
                Region = (HitboxRegion.Region)region,
                AttackerEpoch = attackerEpoch,
                VictimEpoch = victimEpoch
            });
        }

        [ClientRpc]
        void WorldImpactClientRpc(Vector3 point) => CombatEvents.RaiseWorldImpact(point);

        bool HasSlot(ulong clientId, int slot)
            => MatchRoleService.Instance != null && MatchRoleService.Instance.HasSlot(clientId, slot);

        public bool IsSlotBot(int slot)
            => MatchRoleService.Instance != null && MatchRoleService.Instance.IsBot(slot);

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
            var registry = MatchRoleService.Instance != null ? MatchRoleService.Instance.Registry : null;
            if (registry != null)
            {
                ulong p1Owner = registry.OwnerOf(SlotP1), p2Owner = registry.OwnerOf(SlotP2);
                bool bot1 = registry.IsBot(SlotP1), bot2 = registry.IsBot(SlotP2);
                if (p1Owner != _ownerP1 || p2Owner != _ownerP2 || bot1 != P1Bot.Value || bot2 != P2Bot.Value)
                {
                    _ownerP1 = p1Owner; _ownerP2 = p2Owner;
                    P1Bot.Value = bot1; P2Bot.Value = bot2;
                    ResetInputStreams();
                }
            }
            _sim.State.ControlEpoch = _controlEpoch;
            _sim.State.SimulationTick = ++_simulationTick;

            // Bots hold still for a moment at the start of the live phase so a learning player has
            // time to look around before they are engaged.
            if (_director != null && _director.IsLive) _botLiveTime += dt;
            else _botLiveTime = 0f;
            if ((IsSlotBot(SlotP1) || IsSlotBot(SlotP2)) && _director != null && _director.IsLive)
                ObserveBotEnemies();

            if (_spawnGraceRemaining > 0f) _spawnGraceRemaining = Mathf.Max(0f, _spawnGraceRemaining - dt);
            ProtectionRemaining.Value = _spawnGraceRemaining;

            if (_blindRemaining > 0f)
            {
                _blindRemaining = Mathf.Max(0f, _blindRemaining - dt);
                BlindRemaining.Value = _blindRemaining;
            }

            // Record each enemy's complete pose; rewind must retain its vertical position, stance
            // and life/control epoch, not borrow those fields from a newer snapshot.
            for (int i = 0; i < _enemies.Length; i++)
            {
                NetworkBody e = _enemies[i];
                if (e != null) _lag[i].Record(_serverTime, _sim.State.BodyYaw, e.State.Value, e.Alive.Value);
            }

            // Movement advances exactly once per server tick, never once per packet. Missing
            // packets briefly hold continuous controls, but cannot repeat look deltas/actions.
            P1Input p1 = _p1Stream.Consume(_serverTime);
            LastAckedP1Sequence.Value = _p1Stream.Acknowledged;
            if (IsSlotBot(SlotP1) && _director != null && _director.IsLive) p1 = BuildBotP1(dt);
            if (_director == null || !_director.IsLive) p1 = _director != null && _director.IsBuy ? InputStream.LookOnly(p1) : default;
            if (_director != null && (_director.IsBuy || _director.IsLive)) _sim.ApplyP1(p1, dt);
            _aimHistory.Record(_simulationTick, _sim.State.BodyYaw);

            _weapon.Tick(_serverTime);
            P2Input p2 = _serverTime - _lastP2Time <= InputStream.SilenceTimeoutSeconds ? _heldP2 : new P2Input { AimYaw = _sim.State.AimYaw, AimPitch = _sim.State.AimPitch };
            bool queuedFire = false, queuedReload = false;
            while (_p2Queue.TryDequeue(_serverTime, out P2Input nextP2))
            {
                // Retain a short trigger/reload tap even if several packets arrive together.
                queuedFire |= nextP2.Fire;
                queuedReload |= nextP2.Reload;
                p2 = nextP2;
                _heldP2 = nextP2;
                _heldP2.Reload = false;
                _lastP2Time = _serverTime;
                LastAckedP2Sequence.Value = p2.Sequence;
            }
            p2.Fire |= queuedFire;
            p2.Reload |= queuedReload;
            if (_director == null || !_director.IsLive) { p2.Fire = false; p2.Reload = false; }
            Firing.Value = Alive.Value && p2.Fire && !_weapon.IsReloading && _weapon.Ammo > 0;
            if (_serverTime >= _turnRequestUntil) TurnRequest.Value = 0;
            if (!IsSlotBot(SlotP2)) ProcessP2(p2);
            if (IsSlotBot(SlotP2) && _director != null && _director.IsLive)
            {
                P2Input bot = BuildBotP2(dt);
                Firing.Value = Alive.Value && bot.Fire && !_weapon.IsReloading && _weapon.Ammo > 0;
                ProcessP2(bot);
            }

            _weapon.Tick(_serverTime);
            Ammo.Value = _weapon.Ammo;
            Reloading.Value = _weapon.IsReloading;

            // A kick that just started this tick resolves its authoritative hit once.
            BodyMovementState movement = (BodyMovementState)_sim.State.MovementState;
            if (movement != _prevMovementState &&
                (movement == BodyMovementState.KickLight || movement == BodyMovementState.KickHeavy))
                ResolveKick(movement);
            _prevMovementState = movement;

            State.Value = _sim.State;

            transform.position = new Vector3(_sim.State.PosX, _sim.State.PosY, _sim.State.PosZ);
            transform.rotation = Quaternion.Euler(0f, _sim.State.BodyYaw, 0f);
        }

        /// <summary>Server-only: a started kick hits the nearest enemy in front within range.</summary>
        void ResolveKick(BodyMovementState kind)
        {
            if (_director == null) return;
            float damage = kind == BodyMovementState.KickHeavy ? HeavyKickDamage : LightKickDamage;
            float range = KickRangeMeters + 0.4f;

            float fx = _sim.State.ActionDirX, fz = _sim.State.ActionDirZ;
            float feet = _sim.State.PosY;

            NetworkBody best = null;
            float bestForward = float.MaxValue;
            for (int i = 0; i < _enemies.Length; i++)
            {
                NetworkBody enemy = _enemies[i];
                if (enemy == null || !enemy.Alive.Value) continue;
                BodyState es = enemy.State.Value;

                float toX = es.PosX - _sim.State.PosX;
                float toZ = es.PosZ - _sim.State.PosZ;
                float forward = toX * fx + toZ * fz;
                float lateral = Mathf.Abs(toX * fz - toZ * fx);
                if (forward <= 0f || forward > range || lateral > 0.9f) continue;

                // Arms reach the torso, not a body far above or below.
                float enemyTop = es.PosY + (es.HitHeight > 0.01f ? es.HitHeight : 1.8f);
                if (enemyTop < feet || es.PosY > feet + 1.9f) continue;

                if (forward < bestForward) { bestForward = forward; best = enemy; }
            }

            if (best != null) _director.ServerApplyDamage(best, damage, this, kind: DamageKind.Kick);
        }

        void ProcessP2(in P2Input input)
        {
            if (!Alive.Value) return;
            if (input.Reload) _weapon.StartReload(_serverTime);

            if (!input.Fire || _blindRemaining > 0f) { _sim.ApplyP2(in input); return; }

            // Humans validate against the exact body snapshot used by local aiming, not an unrelated
            // smoothed yaw / guessed fixed rewind. Bots aim against the current server body.
            float historicalBodyYaw = _sim.State.BodyYaw;
            if (!IsSlotBot(SlotP2) && !_aimHistory.TryGet(input.BodyTick, _simulationTick, out historicalBodyYaw))
            {
                _sim.ApplyP2(in input);
                return;
            }

            float offset = BodySim.Normalize(input.AimYaw - historicalBodyYaw);
            float aimLimit = SectorHalfDegrees + SectorWall.OvertravelDegrees;
            bool legalHistorically = Mathf.Abs(offset) <= aimLimit + 0.001f;

            if (!legalHistorically)
            {
                _rejectedFires++;
                _sim.ApplyP2(in input);
                return;
            }
            if (_weapon.TryFire(_serverTime) != WeaponState.FireResult.Ok) { _sim.ApplyP2(in input); return; }
            ShotsFired.Value++;

            WeaponStats stats = Loadouts.Stats(_activeWeapon);

            // Vertical aim must agree with the authoritative hit ray: build the direction from BOTH
            // the (sector-legal) yaw and the pitch, and test it against the enemy's vertical extent.
            float eye = _sim.State.PosY + (_sim.State.EyeHeight > 0.01f ? _sim.State.EyeHeight : 1.45f);
            Vector3 origin = new Vector3(_sim.State.PosX, eye, _sim.State.PosZ);
            float spreadYaw = 0, spreadPitch = 0;
            LastShotSpread = 0;
            if (_activeWeapon == WeaponType.Rifle)
            {
                LastShotSpread = RifleHandling.SpreadDegrees(_rifle.Shot(_serverTime));
                RifleHandling.Spread(ShotsFired.Value, _controlEpoch ^ (uint)_botSeed,
                    LastShotSpread, out spreadYaw, out spreadPitch);
            }
            // Validate the player's aim against the historical sector BEFORE ballistic spread.
            // Both damage and wall impacts use this exact spread ray, including for bot P2.
            Vector3 dir = Quaternion.Euler(input.AimPitch, input.AimYaw, 0f)
                * Quaternion.Euler(spreadPitch, spreadYaw, 0f) * Vector3.forward;
            LastShotDirection = dir;

            float wallDistance = float.MaxValue;
            bool wallBlocked = _sim.Collision != null &&
                _sim.Collision.RaycastSolids(origin.x, origin.y, origin.z, dir.x, dir.y, dir.z,
                    stats.RangeMeters, out wallDistance);

            NetworkBody bestTarget = null;
            float bestForward = float.MaxValue;
            CombatHit bestHit = default;
            float bestX = 0f, bestZ = 0f;
            for (int i = 0; i < _enemies.Length; i++)
            {
                NetworkBody enemy = _enemies[i];
                if (enemy == null || !enemy.Alive.Value) continue;
                if (!_lag[i].TryRewindPose(_serverTime - LagRewindSeconds, out _, out BodyState historical, out bool alive)) continue;
                if (!alive || historical.ControlEpoch != enemy.State.Value.ControlEpoch) continue;
                if (!CombatHitGeometry.Raycast(origin, dir, historical, stats.RangeMeters, out CombatHit hit)) continue;
                if (wallBlocked && wallDistance <= hit.Distance) continue;
                if (hit.Distance < bestForward)
                {
                    bestForward = hit.Distance;
                    bestTarget = enemy;
                    bestHit = hit;
                    bestX = historical.PosX;
                    bestZ = historical.PosZ;
                }
            }

            if (bestTarget != null && (_director == null || !_director.Utility.BlocksLine(_serverTime, _sim.State.PosX, _sim.State.PosZ, bestX, bestZ)))
            {
                int applied = _director.ServerApplyDamage(bestTarget, stats.DamageFor(bestHit.Region), this,
                    bestHit.Point, bestHit.Region, DamageKind.Weapon);
                if (applied > 0) _validatedHits++;
            }
            else if (bestTarget == null && wallBlocked && wallDistance < stats.RangeMeters)
            {
                // Authoritative miss into geometry: tell clients where to show the impact.
                WorldImpactClientRpc(origin + dir * wallDistance);
            }

            _sim.ApplyP2(in input);
        }

        /// <summary>Server-only: apply damage from a validated hit (or grenade/zone). Discrete hit
        /// damage lands immediately; continuous zone damage accumulates fractionally.</summary>
        public int ServerTakeDamage(float damage, NetworkBody attacker, Vector3? hitPoint = null,
            HitboxRegion.Region region = HitboxRegion.Region.Body, DamageKind kind = DamageKind.World)
        {
            if (!IsServer || !Alive.Value || damage <= 0f) return 0;
            if (_spawnGraceRemaining > 0f) return 0; // brief post-spawn protection
            _damageRemainder += damage;
            int amount = Mathf.FloorToInt(_damageRemainder);
            if (amount <= 0) return 0;
            _damageRemainder -= amount;

            int applied = Mathf.Min(amount, _sim.State.Health);
            if (IsSlotBot(SlotP1)) _botPositioning.Hurt((float)_serverTime, BotSight.Feet(_sim.State));
            _sim.State.Health -= amount;
            if (_sim.State.Health < 0) _sim.State.Health = 0;
            State.Value = _sim.State;

            if (_director != null) _director.NoteDamage(TeamIndex, attacker != null ? attacker.TeamIndex : -1);

            // Authoritative combat feedback for all clients (hitmarker / damage / impact particles).
            DamageFeedbackClientRpc(
                attacker != null ? (byte)attacker.TeamIndex : (byte)255,
                attacker != null ? (byte)attacker.BodyId : (byte)255,
                (byte)TeamIndex, (byte)BodyId,
                hitPoint ?? new Vector3(_sim.State.PosX, _sim.State.PosY + _sim.State.HitHeight * 0.55f, _sim.State.PosZ),
                _sim.State.Health <= 0, applied, kind, (byte)region,
                attacker != null ? attacker._controlEpoch : 0u, _controlEpoch);

            if (_sim.State.Health <= 0)
            {
                Alive.Value = false;
                State.Value = _sim.State;
                if (attacker != null) attacker.Kills.Value++;
                if (_director != null) _director.OnBodyEliminated(TeamIndex);
            }
            return applied;
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

            ShopItem item = _buy.Catalog[catalogIndex];
            if (!_buy.TryBuy(item.Id)) return;

            if (item.Kind == ShopKind.Utility)
            {
                UtilityKind kind = item.Id == "grenade" ? UtilityKind.Grenade : item.Id == "flash" ? UtilityKind.Flash : UtilityKind.Smoke;
                _utilityCharges[(int)kind]++;
            }

            ServerApplyLoadout(Loadouts.ActiveWeapon(_buy));
            Log($"bought {item.Id} (spent {_buy.Spent}/{_buy.Budget})");
        }

        public bool TryConsumeUtility(UtilityKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= _utilityCharges.Length || _utilityCharges[index] <= 0) return false;
            _utilityCharges[index]--;
            return true;
        }

        void ApplyActiveWeapon()
        {
            WeaponStats stats = Loadouts.Stats(_activeWeapon);
            _weapon.Magazine = stats.Magazine;
            _weapon.SecondsBetweenShots = stats.SecondsBetweenShots;
            _weapon.ReloadSeconds = stats.ReloadSeconds;
            _weapon.Reset();
            _rifle.Reset(); LastShotDirection = Vector3.zero; LastShotSpread = 0;
            WeaponId.Value = (byte)stats.Id;
            Magazine.Value = stats.Magazine;
            Ammo.Value = _weapon.Ammo;
            Reloading.Value = false;
        }

        public void ServerApplyLoadout(WeaponType weapon)
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
            ResetInputStreams();
            _sim.State.SimulationTick = _simulationTick;
            _sim.State.Health = MaxHealthDefault;
            _spawnGraceRemaining = SpawnGraceSeconds;
            _blindRemaining = 0f;
            _damageRemainder = 0f;
            BlindRemaining.Value = 0f;
            Alive.Value = true;
            OutsideZone.Value = false;
            _activeWeapon = WeaponType.Pistol;
            ApplyActiveWeapon();
            _buy.ResetForRound();
            _utilityCharges[0] = _utilityCharges[1] = _utilityCharges[2] = 0;
            InitializeBotProfile();
            _botLiveTime = 0f;
            _prevMovementState = BodyMovementState.Idle;
            for (int i = 0; i < _lag.Length; i++) _lag[i].Clear();
            _p2Queue.Clear();
            State.Value = _sim.State;
            transform.position = new Vector3(x, y, z);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void ServerBeginLive()
        {
            if (IsServer) { _spawnGraceRemaining = SpawnGraceSeconds; _botLiveTime = 0f; }
        }

        void ResetBotControls()
        {
            _botPositioning.Reset(); _botAimMotion.Reset();
            _botTarget = null; _botVisible = _botEnemyFiring = _botWasBurst = false;
            _botSenseAt = 0; _botSeenAt = -100;
        }

        bool BotKnowsEnemy => _botTarget != null && _serverTime - _botSeenAt <= 4f;

        void ObserveBotEnemies()
        {
            if (_serverTime < _botSenseAt) return;
            _botSenseAt = _serverTime + .12;
            if (_blindRemaining > 0f) { _botVisible = _botEnemyFiring = false; return; }
            NetworkBody best = null;
            float bestDistance = float.MaxValue;
            Vector3 eye = BotSight.Eye(_sim.State);
            for (int i = 0; i < _enemies.Length; i++)
            {
                NetworkBody e = _enemies[i];
                if (e == null || !e.Alive.Value) continue;
                BodyState pose = e.State.Value;
                float d = Vector3.Distance(eye, BotSight.Eye(pose));
                if (d > BotMaxEngageDistance ||
                    (!BotSight.Clear(_sim.Collision, eye, BotSight.Chest(pose)) &&
                     !BotSight.Clear(_sim.Collision, eye, CombatHitGeometry.HeadCenter(pose)))) continue;
                if (_director.Utility.BlocksLine(_serverTime, _sim.State.PosX, _sim.State.PosZ, pose.PosX, pose.PosZ)) continue;
                if (e == _botTarget) d -= 3f; // don't alternate targets every perception update
                if (d < bestDistance) { bestDistance = d; best = e; }
            }
            _botVisible = best != null;
            _botEnemyFiring = best != null && best.Firing.Value;
            if (best != null)
            {
                _botTarget = best; _botTargetPose = best.State.Value; _botSeenAt = _serverTime;
            }
            // If sight is lost, retain ONLY the observed pose, not the target's live hidden position.
        }

        P1Input BuildBotP1(float dt)
        {
            // Idle through the reaction window so the player is not rushed at the start of live.
            if (_botLiveTime < _botReaction) return BotSteering.Turn(_sim.State.LookYaw,_sim.State.BodyYaw,_sim.State.BodyYaw,dt);
            return _botPositioning.Step(_sim.State, _sim.Collision, BotKnowsEnemy ? _botTargetPose : (BodyState?)null,
                _botVisible, _botEnemyFiring, _weapon.IsReloading, _botPreferredRange,
                _director.ZoneRadius.Value, (float)_serverTime, dt);
        }

        P2Input BuildBotP2(float dt)
        {
            _botP2Clock += dt;
            bool burst = ((_botP2Clock + _botFirePhase) % _botBurstPeriod) < _botBurstOn;
            // Choose the NEXT burst's goal during the pause, allowing time to acquire it naturally.
            // Resampling on the first firing tick would recreate the old visible flick (or prevent
            // every short burst from firing while the new smooth controller is still settling).
            if (!burst && _botWasBurst) SampleBotShot();
            _botWasBurst = burst;
            var input = new P2Input { Reload = _weapon.Ammo <= 0 };
            float desiredYaw = _sim.State.BodyYaw, desiredPitch = 0f;
            float distance = float.MaxValue;
            bool clear = false, legal = false;
            if (BotKnowsEnemy && _botLiveTime >= _botReaction)
            {
                BodyState t = _botTargetPose;
                float dx = t.PosX - _sim.State.PosX;
                float dz = t.PosZ - _sim.State.PosZ;
                distance = Mathf.Sqrt(dx * dx + dz * dz);
                Vector3 chest = BotSight.Chest(t), eye = BotSight.Eye(_sim.State);
                Vector3 aimPoint = BotSight.Clear(_sim.Collision, eye, chest) ? chest : CombatHitGeometry.HeadCenter(t);
                if (_difficulty == BotDifficulty.Easy)
                {
                    Vector3 right = new Vector3(dz, 0f, -dx).normalized;
                    aimPoint += right * _botShotRight + Vector3.up * _botShotUp;
                }
                Vector3 to = aimPoint - eye;
                desiredYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                desiredPitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                if (_difficulty != BotDifficulty.Easy)
                {
                    float error = (BotAimErrorBase + distance * BotAimErrorPerMeter) * _botAimDrift * .25f;
                    desiredYaw += Mathf.Sin(_botP2Clock * _botAimRate1 + _botAimPhase1) * error
                        + Mathf.Sin(_botP2Clock * _botAimRate2 + _botAimPhase2) * error * .6f;
                    desiredPitch += Mathf.Sin(_botP2Clock * _botAimRate2 * 1.7f + _botAimPhase1) * error * .4f;
                }
                clear = _botVisible && BotSight.Clear(_sim.Collision, eye, aimPoint) &&
                    !_director.Utility.BlocksLine(_serverTime, _sim.State.PosX, _sim.State.PosZ, t.PosX, t.PosZ);
                legal = Mathf.Abs(BodySim.Normalize(desiredYaw - _sim.State.BodyYaw)) <= SectorHalfDegrees;
            }
            _botAimMotion.Step(_sim.State.AimYaw, _sim.State.AimPitch, desiredYaw, desiredPitch,
                _sim.State.BodyYaw, SectorHalfDegrees + SectorWall.OvertravelDegrees, MaxPitchDegrees, dt, _difficulty == BotDifficulty.Easy,
                out input.AimYaw, out input.AimPitch);
            bool settled = Mathf.Abs(BodySim.Normalize(input.AimYaw - desiredYaw)) < 1.2f &&
                Mathf.Abs(input.AimPitch - desiredPitch) < 1.2f;
            input.Fire = burst && legal && clear && settled && _blindRemaining <= 0f && distance < BotMaxEngageDistance;

            // Occasional utility through the normal P2 authority path.
            _grenadeCooldown -= dt;
            if (_director != null && _director.IsLive && _botLiveTime > _botReaction + 4f &&
                clear && settled && distance < 24f && _grenadeCooldown <= 0f && TryConsumeUtility(UtilityKind.Grenade))
            {
                _director.ServerApplyUtility(TeamIndex, UtilityKind.Grenade, _sim.State.PosX, _sim.State.PosZ, _sim.State.AimYaw);
                _grenadeCooldown = Mathf.Lerp(10f, 18f, Rand());
            }
            return input;
        }

        /// <summary>Server-only: give a bot-owned P2 a legal loadout at the start of a round. This
        /// vertical slice equips the single rifle only; it bypasses the buy-phase gate so the bot is
        /// armed even if the phase transition and the round-start callback race.</summary>
        public void ServerAutoBuyBotLoadout()
        {
            if (!IsServer) return;
            ServerApplyLoadout(WeaponType.Rifle);
        }

        void ResetInputStreams()
        {
            var roster = MatchRoleService.Instance != null ? MatchRoleService.Instance.Registry : null;
            if (roster != null)
            {
                _ownerP1 = roster.OwnerOf(SlotP1); _ownerP2 = roster.OwnerOf(SlotP2);
                P1Bot.Value = roster.IsBot(SlotP1); P2Bot.Value = roster.IsBot(SlotP2);
            }
            _controlEpoch++;
            _sim.State.ControlEpoch = _controlEpoch;
            _acceptedP2 = 0;
            _rifle.Reset();
            LastAckedP1Sequence.Value = LastAckedP2Sequence.Value = 0;
            _heldP2 = default;
            _lastP2Time = -1;
            Firing.Value = false; TurnRequest.Value = 0; _turnRequestUntil = 0;
            _p1Stream.Reset(_controlEpoch); _p2Queue.Clear();
            _aimHistory.Clear();
            _aimHistory.Record(_simulationTick, _sim.State.BodyYaw);
            ResetBotControls();
        }

        void Log(string message)
            => Debug.Log($"[Match] t={Time.realtimeSinceStartup:0.000} {MatchSlots.Name(SlotP1, _director != null ? _director.BodiesPerTeam : 1)} {message}");
    }
}
