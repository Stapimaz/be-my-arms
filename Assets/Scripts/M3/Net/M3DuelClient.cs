using BeMyArms.M2;
using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BeMyArms.M3
{
    /// <summary>
    /// Role-aware client for one shared body (Duel or 2v2): P1 predicts/reconciles the body and P2
    /// keeps aim local (Model C). It presents every body from replicated state and can auto-drive
    /// both roles for headless multi-process runs. Only the local player's body sends inputs; the
    /// local slot is assigned by the server (direct mode or matchmaker).
    ///
    /// Input sampling is deliberately decoupled from the network send rate: for the local player the
    /// mouse and look/aim are sampled and applied every rendered frame, while movement and its RPC
    /// stream use the same fixed 60 Hz step on both sides and consume latched edges once. This is what
    /// makes the camera smooth at high frame rates without smoothing over a low-frequency sample.
    ///
    /// Camera/viewmodel/cursor presentation is owned by the M7 layer (M7LocalPlayer); this component
    /// exposes the predicted state and local aim/look so presentation stays decoupled from netcode.
    /// </summary>
    public class M3DuelClient : NetworkBehaviour
    {
        /// <summary>Local slot index (team/body/role) assigned by the server; -1 until known.</summary>
        public static int LocalSlotIndex = -1;

        public static void SetLocalSlot(int slot) => LocalSlotIndex = slot;

        public Transform Presentation;

        [Header("Tuning (must match the server body)")]
        public float WalkSpeed = 4.5f;
        public float SprintSpeed = 7f;
        public float JumpSpeed = M2BodySim.DefaultJumpSpeed;
        public float Gravity = M2BodySim.DefaultGravity;
        public float NeckYawLimitDegrees = M2BodySim.DefaultNeckYawLimitDegrees;
        public float BodyFollowThresholdDegrees = M2BodySim.DefaultBodyFollowThresholdDegrees;
        public float BodyFollowSpeedDegreesPerSecond = M2BodySim.DefaultBodyFollowSpeedDegreesPerSecond;
        public float BodyAlignSpeedDegreesPerSecond = 540f;
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;

        [Header("Send")]
        public bool AutoDrive = true;
        public bool AutoBuy = true;
        public bool AutoFire = true;
        public bool AutoUtility = true;

        public float LocalAimYaw { get; private set; }
        public float LocalAimPitch { get; private set; }

        /// <summary>Diagnostic: the local P2 aim offset from the body-centred sector (degrees).</summary>
        public float LocalAimOffset => M2BodySim.Normalize(LocalAimYaw - (_body != null ? _body.State.Value.BodyYaw : 0f));
        public int SectorBlockedSide { get; private set; }
        public uint ControlEpoch => _epoch;

        /// <summary>Last raw mouse delta the local input path read (diagnostics).</summary>
        public Vector2 LastMouseDelta { get; private set; }
        /// <summary>Look yaw currently held by the prediction sim itself (diagnostics).</summary>
        public float SimLookYaw => _predictSim != null ? _predictSim.State.LookYaw : 0f;
        public uint P1Sequence => _p1Sequence;
        public uint LastAckedP1 => _body != null ? _body.LastAckedP1Sequence.Value : 0u;
        public int PendingP1Inputs => _reconciler != null ? _reconciler.PendingInputCount : 0;

        public M3DuelBody Body => _body;
        public bool IsLocalOwnBody => IsOwnBody;
        public int LocalRoleIndex => EffectiveRole;

        /// <summary>Smoothed presentation position of this body (local body root, feet).</summary>
        public Vector3 VisualPosition => _visualPosition;
        public float VisualYaw => _visualYaw;

        /// <summary>State the local camera/presentation should follow (predicted for local P1).</summary>
        public M2BodyState ViewState
        {
            get
            {
                if (_body == null || !_body.IsSpawned) return default;
                return IsOwnBody && EffectiveRole == 0 ? _reconciler.Predicted : _body.State.Value;
            }
        }

        /// <summary>Decoupled look yaw for the local P1 camera (predicted, updated every frame).</summary>
        public float LocalLookYaw
        {
            get
            {
                if (IsOwnBody && EffectiveRole == 0 && _reconciler != null)
                {
                    var state = _reconciler.Predicted;
                    float offset = M2BodySim.Normalize(state.LookYaw + _accumP1.LookYawDelta - state.BodyYaw);
                    return M2BodySim.Normalize(state.BodyYaw + Mathf.Clamp(offset, -NeckYawLimitDegrees, NeckYawLimitDegrees));
                }
                if (_body != null && _body.IsSpawned) return _body.State.Value.LookYaw;
                return 0f;
            }
        }

        /// <summary>Decoupled look pitch for the local P1 camera (predicted, updated every frame).</summary>
        public float LocalLookPitch
        {
            get
            {
                if (IsOwnBody && EffectiveRole == 0 && _reconciler != null) return Mathf.Clamp(_reconciler.Predicted.LookPitch + _accumP1.LookPitchDelta, -MaxPitchDegrees, MaxPitchDegrees);
                if (_body != null && _body.IsSpawned) return _body.State.Value.LookPitch;
                return 0f;
            }
        }

        M3DuelBody _body;
        M3DuelDirector _director;
        M2BodySim _predictSim;
        M2Reconciler _reconciler;

        uint _p1Sequence = 1; // sequence of the in-progress tick (0 is reserved for "none acked")
        uint _p2Sequence;
        float _autoClock;
        float _tickAccumulator;
        uint _epoch = uint.MaxValue, _snapshotTick;
        int _resetRole = -1;
        int _boughtRound = -1;
        int _utilityRound = -1;
        float _utilityStartTime;
        bool _grenadeSent;
        bool _smokeSent;
        bool _flashSent;

        // Legal world-space local P2 aim; presentation and the submitted aim use this exact value.
        float _manualAimYaw;
        float _manualAimPitch;
        bool _pendingFire, _pendingReload;
        bool _fireArmed;
        uint _lastServerShots;

        // Edge-triggered actions are latched every frame so a press between send ticks is not lost.
        M2P1Input _pendingP1;
        bool _pendingGrenade, _pendingSmoke, _pendingFlash;

        // Input accumulated over the current fixed simulation tick.
        M2P1Input _accumP1;

        // Local P2 trigger feedback (presentation only; never authoritative).
        float _localShotCooldown;
        int _lastServerAmmo = -1;
        int _optimisticSpent;
        int _pendingLocalShots;

        Vector3 _visualPosition;
        float _visualYaw;
        bool _hasVisual;
        uint _visualEpoch = uint.MaxValue;

        int BodiesPerTeam => Mathf.Clamp(M3Config.BodiesPerTeam, 1, 2);
        public int EffectiveRole => M3DuelSlots.IsValidSlot(LocalSlotIndex, BodiesPerTeam) ? M3DuelSlots.RoleOf(LocalSlotIndex) : M3Config.ClientRole;
        public int LocalTeam => M3DuelSlots.IsValidSlot(LocalSlotIndex, BodiesPerTeam) ? M3DuelSlots.TeamOf(LocalSlotIndex, BodiesPerTeam) : M3Config.ClientTeam;
        public int LocalBody => M3DuelSlots.IsValidSlot(LocalSlotIndex, BodiesPerTeam) ? M3DuelSlots.BodyOf(LocalSlotIndex, BodiesPerTeam) : M3Config.ClientBody;
        public bool IsOwnBody => M3DuelSlots.IsValidSlot(LocalSlotIndex, BodiesPerTeam) && _body != null && _body.IsSpawned && _body.TeamIndex == LocalTeam && _body.BodyId == LocalBody;

        void Start()
        {
            AutoDrive = M3Config.AutoDrive;
            AutoBuy = M3Config.AutoBuy;
            AutoFire = M3Config.AutoFire;
            AutoUtility = M3Config.AutoUtility;

            _body = GetComponent<M3DuelBody>();
            _predictSim = new M2BodySim
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
                SectorOvertravelDegrees = M3SectorWall.OvertravelDegrees,
                MaxPitchDegrees = MaxPitchDegrees
            };
            _predictSim.Initialize(0f);
            M3MapSpawns map = FindAnyObjectByType<M3MapSpawns>();
            if (map != null) _predictSim.Collision = map.BuildCollision();
            _reconciler = new M2Reconciler();
            _reconciler.Reset(_predictSim.State);
            _manualAimYaw = 0f;
        }

        void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsClient || _body == null || !_body.IsSpawned) return;

            if (_director == null) _director = M3DuelDirector.Instance;

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            if (!IsOwnBody)
            {
                ApplyPresentation(dt);
                return;
            }

            if (_epoch != _body.State.Value.ControlEpoch || _resetRole != EffectiveRole) ResetControls();
            PollInputEdges();

            HandleBuy();

            _tickAccumulator = Mathf.Min(0.1f, _tickAccumulator + dt);
            if (EffectiveRole == 0) UpdateP1Role(dt);
            else UpdateP2Role(dt);

            ApplyPresentation(dt);
        }

        // ---- P1 ----

        void UpdateP1Role(float dt)
        {
            // Look is accepted during Buy; translation/actions are not. Sanitise on both the
            // prediction and the sent command so the client never visually simulates movement the
            // server is rejecting.
            DrainSnapshots();
            bool canLook = _director != null && (_director.IsBuy || _director.IsLive) && _body.Alive.Value;
            bool canAct = canLook && _director.IsLive;
            if (!canLook) { _accumP1 = default; _pendingP1 = default; _tickAccumulator = 0f; return; }
            if (!AutoDrive)
            {
                if (!M3LocalInput.GameplayActive) { _accumP1 = default; _pendingP1 = default; }
                M2P1Input frame = BuildManualP1(canAct);
                M3InputStream.Accumulate(ref _accumP1, frame);
            }
            while (_tickAccumulator + 1e-6f >= M3InputStream.TickSeconds)
            {
                _tickAccumulator = Mathf.Max(0f, _tickAccumulator - M3InputStream.TickSeconds);
                M2P1Input input = AutoDrive ? (canAct ? BuildAutoP1(M3InputStream.TickSeconds) : default) : _accumP1;
                if (!canAct) input = M3InputStream.LookOnly(input);
                input.Sequence = _p1Sequence++;
                input.ControlEpoch = _epoch;
                input.LookYawDelta = Mathf.Clamp(input.LookYawDelta, -180f, 180f);
                input.LookPitchDelta = Mathf.Clamp(input.LookPitchDelta, -180f, 180f);
                _reconciler.Predict(input, M3InputStream.TickSeconds, _predictSim);
                _body.SubmitP1ServerRpc(input);
                _accumP1 = M3InputStream.Held(_accumP1);
            }
        }

        // ---- P2 ----

        void UpdateP2Role(float dt)
        {
            // Local player: mouse drives the raw aim every frame; presentation and the sent command
            // use the sector-clamped aim. Fire/reload are held state, sent each tick. Local trigger
            // feedback is detected every frame (independent of the authoritative ammo replication)
            // so the shot feels immediate.
            M2P2Input input = AutoDrive ? BuildAutoP2() : BuildManualP2(dt);
            bool live = _director != null && _director.IsLive && _body.Alive.Value;
            if (!live || (!AutoDrive && !M3LocalInput.GameplayActive))
            { input.Fire = input.Reload = false; _pendingFire = _pendingReload = false; }
            if (!AutoDrive) DetectLocalShot(input, dt);
            _pendingFire |= input.Fire;
            _pendingReload |= input.Reload;
            if (_tickAccumulator < M3InputStream.TickSeconds) return;
            _tickAccumulator %= M3InputStream.TickSeconds;
            input.Sequence = ++_p2Sequence;
            input.ControlEpoch = _epoch;
            input.BodyTick = _body.State.Value.SimulationTick;
            input.Fire |= _pendingFire;
            input.Reload |= _pendingReload;
            _pendingFire = _pendingReload = false;
            SubmitP2(input);
        }

        /// <summary>
        /// Locally detects a valid rifle trigger pull for immediate presentation feedback. The server
        /// still validates cadence/sector and owns all hit/damage/kill confirmation.
        /// </summary>
        void DetectLocalShot(in M2P2Input input, float dt)
        {
            int serverAmmo = _body.Ammo.Value;
            uint shots = _body.ShotsFired.Value;
            uint confirmed = shots >= _lastServerShots ? shots - _lastServerShots : 0;
            _optimisticSpent = Mathf.Max(0, _optimisticSpent - (int)confirmed);
            _lastServerShots = shots;
            if (serverAmmo > _lastServerAmmo) _optimisticSpent = 0;
            _lastServerAmmo = serverAmmo;
            _localShotCooldown = Mathf.Max(0f, _localShotCooldown - dt);
            if (!input.Fire && _localShotCooldown <= 0f) _optimisticSpent = 0;

            if (!M3LocalInput.GameplayActive || !_body.Alive.Value || _director == null || !_director.IsLive || _body.Reloading.Value || _body.BlindRemaining.Value > 0f || input.Reload) return;
            if (!input.Fire || _localShotCooldown > 0f) return;
            if (serverAmmo - _optimisticSpent <= 0) return;

            M3WeaponStats stats = M3Loadouts.Stats((M3WeaponId)_body.WeaponId.Value);
            _localShotCooldown = Mathf.Max(0.02f, stats.SecondsBetweenShots);
            _optimisticSpent++;
            _pendingLocalShots++;
        }

        /// <summary>Number of locally detected rifle shots since the last call (presentation only).</summary>
        public int ConsumePendingLocalShots()
        {
            int pending = _pendingLocalShots;
            _pendingLocalShots = 0;
            TotalLocalShots += pending;
            return pending;
        }

        /// <summary>Diagnostic: total locally detected shots this session.</summary>
        public int TotalLocalShots { get; private set; }

        void SubmitP2(in M2P2Input input)
        {
            _body.SubmitP2ServerRpc(input);
            if (!AutoDrive) HandleManualUtility();
            else HandleUtility();
        }

        void DrainSnapshots()
        {
            if (_snapshotTick == _body.State.Value.SimulationTick) return;
            _snapshotTick = _body.State.Value.SimulationTick;
            _reconciler.Reconcile(_body.State.Value, _body.LastAckedP1Sequence.Value, _predictSim);
        }

        void ResetControls()
        {
            M2BodyState state = _body.State.Value;
            _epoch = state.ControlEpoch;
            _resetRole = EffectiveRole;
            _snapshotTick = state.SimulationTick;
            _reconciler.Reset(state);
            _predictSim.State = state;
            _p1Sequence = 1; _p2Sequence = 0;
            _pendingP1 = _accumP1 = default;
            _pendingFire = _pendingReload = _pendingGrenade = _pendingSmoke = _pendingFlash = false;
            _fireArmed = false;
            _tickAccumulator = 0f;
            _manualAimYaw = LocalAimYaw = state.AimYaw;
            _manualAimPitch = LocalAimPitch = state.AimPitch;
            SectorBlockedSide = 0;
            _sectorReturnVelocity = 0f;
            _localShotCooldown = 0f; _optimisticSpent = _pendingLocalShots = 0;
            _lastServerAmmo = _body.Ammo.Value;
            _lastServerShots = _body.ShotsFired.Value;
            _hasVisual = false;
            LastMouseDelta = Vector2.zero;
        }

        /// <summary>Latch edge-triggered actions every frame so the rate-limited send cannot miss one.</summary>
        void PollInputEdges()
        {
            if (AutoDrive || !M3LocalInput.GameplayActive || _director == null || !_director.IsLive || !_body.Alive.Value)
            { _pendingP1 = default; _pendingGrenade = _pendingSmoke = _pendingFlash = false; return; }
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (EffectiveRole == 0)
            {
                if (kb.spaceKey.wasPressedThisFrame) _pendingP1.Jump = true;
                if (kb.qKey.wasPressedThisFrame) _pendingP1.Dodge = true;
                if (kb.cKey.wasPressedThisFrame) _pendingP1.Slide = true;
                if (kb.fKey.wasPressedThisFrame) _pendingP1.LightKick = true;
                if (kb.vKey.wasPressedThisFrame) _pendingP1.HeavyKick = true;
            }
            else
            {
                if (kb.gKey.wasPressedThisFrame) _pendingGrenade = true;
                if (kb.tKey.wasPressedThisFrame) _pendingSmoke = true;
                if (kb.yKey.wasPressedThisFrame) _pendingFlash = true;
            }
#endif
        }

        void HandleBuy()
        {
            if (!AutoBuy || _director == null || EffectiveRole != 1) return;
            if (_director.CurrentPhase != M3Phase.Buy) return;
            if (_boughtRound == _director.RoundIndex.Value) return;
            _boughtRound = _director.RoundIndex.Value;

            // Vertical slice: the playable P2 loadout is a single rifle, auto-equipped each round.
            _body.SubmitBuyServerRpc(0); // rifle (primary)
            Debug.Log($"[M3-client] {M3DuelSlots.Name(LocalSlotIndex, BodiesPerTeam)} auto-equipped rifle round {_boughtRound}");
        }

        void HandleManualUtility()
        {
            if (_director == null || !_director.IsLive) return;
            if (_pendingGrenade) { _pendingGrenade = false; _body.SubmitUtilityServerRpc((byte)M3UtilityKind.Grenade); }
            if (_pendingSmoke) { _pendingSmoke = false; _body.SubmitUtilityServerRpc((byte)M3UtilityKind.Smoke); }
            if (_pendingFlash) { _pendingFlash = false; _body.SubmitUtilityServerRpc((byte)M3UtilityKind.Flash); }
        }

        void HandleUtility()
        {
            if (!AutoDrive || !AutoUtility || _director == null || !_director.IsLive) return;
            if (_utilityRound != _director.RoundIndex.Value)
            {
                _utilityRound = _director.RoundIndex.Value;
                _utilityStartTime = Time.time;
                _grenadeSent = false;
                _smokeSent = false;
                _flashSent = false;
            }

            // Wall-clock, not accumulated frame delta: this runs on a rate-limited send, so summing
            // Time.deltaTime would advance far slower than real time on a high-FPS headless player.
            float elapsed = Time.time - _utilityStartTime;
            if (!_grenadeSent && elapsed > 0.6f)
            {
                _grenadeSent = true;
                _body.SubmitUtilityServerRpc((byte)M3UtilityKind.Grenade);
            }
            if (!_smokeSent && elapsed > 1.2f)
            {
                _smokeSent = true;
                _body.SubmitUtilityServerRpc((byte)M3UtilityKind.Smoke);
            }
            if (!_flashSent && elapsed > 1.8f)
            {
                _flashSent = true;
                _body.SubmitUtilityServerRpc((byte)M3UtilityKind.Flash);
            }
        }

        M2P1Input BuildAutoP1(float dt)
        {
            var input = new M2P1Input();
            _autoClock += dt;
            M3DuelBody target = NearestEnemy();
            if (target != null)
            {
                M2BodyState predicted = _reconciler.Predicted;
                float dx = target.State.Value.PosX - predicted.PosX;
                float dz = target.State.Value.PosZ - predicted.PosZ;
                float desired = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                float delta = Mathf.Clamp(M2BodySim.Normalize(desired - predicted.LookYaw), -25f, 25f);
                input.LookYawDelta = delta;
                input.AlignBody = Mathf.Abs(delta) > 1f;
                input.MoveZ = 0.4f;
                input.MoveX = Mathf.Sin(_autoClock * 1.3f) * 0.5f;
            }
            return input;
        }

        M2P2Input BuildAutoP2()
        {
            var input = new M2P2Input();
            M3DuelBody target = NearestEnemy();
            if (target != null)
            {
                float dx = target.State.Value.PosX - _body.State.Value.PosX;
                float dz = target.State.Value.PosZ - _body.State.Value.PosZ;
                input.AimYaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                input.Fire = AutoFire;
                input.Reload = _body.Ammo.Value <= 1;
            }
            else
            {
                input.AimYaw = _body.State.Value.BodyYaw;
            }
            LocalAimYaw = M2BodySim.ClampToSector(input.AimYaw, _body.State.Value.BodyYaw, SectorHalfDegrees);
            LocalAimPitch = Mathf.Clamp(input.AimPitch, -MaxPitchDegrees, MaxPitchDegrees);
            input.AimYaw = LocalAimYaw;
            return input;
        }

        M2P1Input BuildManualP1(bool canAct)
        {
            var input = new M2P1Input();
            if (canAct && M3LocalInput.GameplayActive)
            {
                input.Jump = _pendingP1.Jump;
                input.Dodge = _pendingP1.Dodge;
                input.Slide = _pendingP1.Slide;
                input.LightKick = _pendingP1.LightKick;
                input.HeavyKick = _pendingP1.HeavyKick;
            }
            _pendingP1 = default;

            if (!M3LocalInput.GameplayActive) return input;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb != null)
            {
                input.AlignBody = kb.leftAltKey.isPressed;
                if (canAct)
                {
                    input.MoveX = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                    input.MoveZ = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                    input.Sprint = kb.leftShiftKey.isPressed;
                    input.Crouch = kb.leftCtrlKey.isPressed;
                }
            }
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * M3LocalInput.MouseSensitivity;
                LastMouseDelta = delta;
                input.LookYawDelta = delta.x;
                input.LookPitchDelta = -delta.y;
            }
            M3LocalInput.ConsumeInjectedLook(out float injectedYaw, out float injectedPitch);
            input.LookYawDelta += injectedYaw;
            input.LookPitchDelta -= injectedPitch; // injected pitch is "look up" positive
            if (canAct)
            {
                input.MoveX = Mathf.Clamp(input.MoveX + M3LocalInput.InjectedMoveX, -1f, 1f);
                input.MoveZ = Mathf.Clamp(input.MoveZ + M3LocalInput.InjectedMoveZ, -1f, 1f);
                input.Crouch |= M3LocalInput.InjectedCrouch;
            }
#endif
            return input;
        }

        float _sectorReturnVelocity;

        M2P2Input BuildManualP2(float dt)
        {
            float deltaYaw = 0f;
#if ENABLE_INPUT_SYSTEM
            if (M3LocalInput.GameplayActive)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 delta = mouse.delta.ReadValue() * M3LocalInput.MouseSensitivity;
                    LastMouseDelta = delta;
                    deltaYaw += delta.x;
                    _manualAimPitch = Mathf.Clamp(_manualAimPitch - delta.y, -MaxPitchDegrees, MaxPitchDegrees);
                }
                M3LocalInput.ConsumeInjectedLook(out float injectedYaw, out float injectedPitch);
                deltaYaw += injectedYaw + M3LocalInput.InjectedLookYawRate * dt;
                _manualAimPitch = Mathf.Clamp(_manualAimPitch - injectedPitch, -MaxPitchDegrees, MaxPitchDegrees); // injected pitch is "look up" positive
            }
#endif
            float bodyYaw = _body.State.Value.BodyYaw;
            _manualAimYaw = M3SectorWall.Step(_manualAimYaw, bodyYaw, deltaYaw, SectorHalfDegrees,
                dt, ref _sectorReturnVelocity, out int blockedSide);
            SectorBlockedSide = blockedSide;
#if ENABLE_INPUT_SYSTEM
            if (M3LocalInput.GameplayActive && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                float requestOffset = M2BodySim.Normalize(_manualAimYaw - bodyYaw);
                _body.RequestTurnServerRpc(Mathf.Abs(requestOffset) < 1f ? 0 : (requestOffset < 0f ? -1 : 1), _epoch);
            }
#endif

            var input = new M2P2Input();
            LocalAimYaw = _manualAimYaw;
            LocalAimPitch = Mathf.Clamp(_manualAimPitch, -MaxPitchDegrees, MaxPitchDegrees);
            input.AimYaw = LocalAimYaw;
            input.AimPitch = LocalAimPitch;
#if ENABLE_INPUT_SYSTEM
            if (M3LocalInput.GameplayActive)
            {
                Mouse mouse = Mouse.current;
                Keyboard kb = Keyboard.current;
                bool pressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
                bool canFire = _director != null && _director.IsLive && _body.Alive.Value;
                input.Fire = M3InputStream.FireGate(ref _fireArmed, canFire, mouse != null && mouse.leftButton.isPressed, pressed);
                if (input.Fire && pressed) _pendingFire = true;
                input.Fire |= _pendingFire;
                if (kb != null && kb.rKey.wasPressedThisFrame) _pendingReload = true;
                input.Reload = _pendingReload;
                input.Fire |= M3LocalInput.InjectedFire;
            }
            else M3InputStream.FireGate(ref _fireArmed, false, false, false);
#endif
            return input;
        }

        /// <summary>Closest living enemy body (2v2 has two).</summary>
        M3DuelBody NearestEnemy()
        {
            M3DuelBody[] bodies = FindObjectsByType<M3DuelBody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            M3DuelBody best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < bodies.Length; i++)
            {
                M3DuelBody candidate = bodies[i];
                if (!candidate.IsSpawned || !candidate.Alive.Value || candidate.TeamIndex == _body.TeamIndex) continue;
                float dx = candidate.State.Value.PosX - _body.State.Value.PosX;
                float dz = candidate.State.Value.PosZ - _body.State.Value.PosZ;
                float d = dx * dx + dz * dz;
                if (d < bestDistance) { bestDistance = d; best = candidate; }
            }
            return best;
        }

        void ApplyPresentation(float dt)
        {
            if (Presentation == null || _body == null || !_body.IsSpawned) return;

            M2BodyState state = IsOwnBody && EffectiveRole == 0 ? _reconciler.Predicted : _body.State.Value;
            if (_visualEpoch != state.ControlEpoch) { _visualEpoch = state.ControlEpoch; _hasVisual = false; }
            Vector3 targetPos = new Vector3(state.PosX, state.PosY, state.PosZ);
            float targetYaw = state.BodyYaw;

            if (!_hasVisual)
            {
                _visualPosition = targetPos;
                _visualYaw = targetYaw;
                _hasVisual = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-18f * Mathf.Max(dt, 0.0001f));
                _visualPosition = Vector3.Lerp(_visualPosition, targetPos, k);
                _visualYaw = Mathf.LerpAngle(_visualYaw, targetYaw, k);
            }

            Presentation.position = _visualPosition;
            Presentation.rotation = Quaternion.Euler(0f, _visualYaw, 0f);
        }
    }
}
