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
    /// mouse and look/aim are sampled and applied every rendered frame, while the RPC stream runs at
    /// <see cref="SendRateHz"/> and consumes the input accumulated since the last send. This is what
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
        public float JumpSpeed = 7f;
        public float Gravity = -20f;
        public float NeckYawLimitDegrees = 80f;
        public float BodyFollowThresholdDegrees = 50f;
        public float BodyFollowSpeedDegreesPerSecond = 120f;
        public float BodyAlignSpeedDegreesPerSecond = 540f;
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;

        [Header("Send")]
        public float SendRateHz = 60f;
        public bool AutoDrive = true;
        public bool AutoBuy = true;
        public bool AutoFire = true;
        public bool AutoUtility = true;

        public float LocalAimYaw { get; private set; }
        public float LocalAimPitch { get; private set; }

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
                if (IsOwnBody && EffectiveRole == 0) return _reconciler.Predicted.LookYaw;
                if (_body != null && _body.IsSpawned) return _body.State.Value.LookYaw;
                return 0f;
            }
        }

        /// <summary>Decoupled look pitch for the local P1 camera (predicted, updated every frame).</summary>
        public float LocalLookPitch
        {
            get
            {
                if (IsOwnBody && EffectiveRole == 0) return _reconciler.Predicted.LookPitch;
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
        float _nextSendTime;
        float _autoClock;
        float _smoothedBodyYaw;
        bool _hasSmoothedYaw;
        int _boughtRound = -1;
        int _utilityRound = -1;
        float _utilityStartTime;
        bool _grenadeSent;
        bool _smokeSent;
        bool _flashSent;

        // Raw (unclamped) local P2 aim; the presentation and the sent aim are clamped to the sector.
        float _manualAimYaw;
        float _manualAimPitch;

        // Edge-triggered actions are latched every frame so a press between send ticks is not lost.
        M2P1Input _pendingP1;
        bool _pendingGrenade, _pendingSmoke, _pendingFlash;

        // Input accumulated over the current tick; sent at SendRateHz.
        M2P1Input _accumP1;

        Vector3 _visualPosition;
        float _visualYaw;
        bool _hasVisual;

        int BodiesPerTeam => Mathf.Clamp(M3Config.BodiesPerTeam, 1, 2);
        public int EffectiveRole => M3DuelSlots.IsValidSlot(LocalSlotIndex, BodiesPerTeam) ? M3DuelSlots.RoleOf(LocalSlotIndex) : M3Config.ClientRole;
        public int LocalTeam => M3DuelSlots.IsValidSlot(LocalSlotIndex, BodiesPerTeam) ? M3DuelSlots.TeamOf(LocalSlotIndex, BodiesPerTeam) : M3Config.ClientTeam;
        public int LocalBody => M3DuelSlots.IsValidSlot(LocalSlotIndex, BodiesPerTeam) ? M3DuelSlots.BodyOf(LocalSlotIndex, BodiesPerTeam) : M3Config.ClientBody;
        public bool IsOwnBody => _body != null && _body.IsSpawned && _body.TeamIndex == LocalTeam && _body.BodyId == LocalBody;

        void Start()
        {
            if (!M3DuelSlots.IsValidSlot(LocalSlotIndex, BodiesPerTeam))
                LocalSlotIndex = M3DuelSlots.Encode(M3Config.ClientTeam, M3Config.ClientBody, M3Config.ClientRole, BodiesPerTeam);

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
            PollInputEdges();

            if (!IsOwnBody)
            {
                ApplyPresentation(dt);
                return;
            }

            HandleBuy();

            bool sendTick = Time.time >= _nextSendTime;
            if (sendTick) _nextSendTime = Time.time + 1f / Mathf.Max(1f, SendRateHz);
            float sendDt = 1f / Mathf.Max(1f, SendRateHz);

            if (EffectiveRole == 0) UpdateP1Role(dt, sendDt, sendTick);
            else UpdateP2Role(dt, sendTick);

            ApplyPresentation(dt);
        }

        // ---- P1 ----

        void UpdateP1Role(float dt, float sendDt, bool sendTick)
        {
            if (AutoDrive)
            {
                DrainSnapshots();
                if (!sendTick) return;
                M2P1Input input = BuildAutoP1(sendDt);
                input.Sequence = _p1Sequence++;
                _reconciler.Predict(input, sendDt, _predictSim);
                _body.SubmitP1ServerRpc(input);
                return;
            }

            // Local player: sample and predict every rendered frame so look/body presentation is
            // smooth; accumulate the same input and send it once per network tick.
            M2P1Input frame = BuildManualP1();
            frame.Sequence = _p1Sequence;
            _reconciler.Predict(frame, dt, _predictSim);

            _accumP1.MoveX = frame.MoveX;
            _accumP1.MoveZ = frame.MoveZ;
            _accumP1.Sprint = frame.Sprint;
            _accumP1.AlignBody = frame.AlignBody;
            _accumP1.LookYawDelta += frame.LookYawDelta;
            _accumP1.LookPitchDelta += frame.LookPitchDelta;
            _accumP1.Jump |= frame.Jump;
            _accumP1.Dodge |= frame.Dodge;
            _accumP1.Slide |= frame.Slide;
            _accumP1.Vault |= frame.Vault;
            _accumP1.LightKick |= frame.LightKick;
            _accumP1.HeavyKick |= frame.HeavyKick;

            DrainSnapshots();

            if (!sendTick) return;
            _accumP1.Sequence = _p1Sequence++;
            _body.SubmitP1ServerRpc(_accumP1);
            _accumP1 = default;
        }

        // ---- P2 ----

        void UpdateP2Role(float dt, bool sendTick)
        {
            UpdateSmoothedBodyYaw(dt);

            if (AutoDrive)
            {
                if (!sendTick) return;
                M2P2Input auto = BuildAutoP2();
                auto.Sequence = ++_p2Sequence;
                SubmitP2(auto);
                return;
            }

            // Local player: mouse drives the raw aim every frame; presentation and the sent command
            // use the sector-clamped aim. Fire/reload are held state, sent each tick.
            M2P2Input input = BuildManualP2();
            if (!sendTick) return;
            input.Sequence = ++_p2Sequence;
            SubmitP2(input);
        }

        void SubmitP2(in M2P2Input input)
        {
            _body.SubmitP2ServerRpc(input);
            if (!AutoDrive) HandleManualUtility();
            else HandleUtility();
        }

        void UpdateSmoothedBodyYaw(float dt)
        {
            float serverBodyYaw = _body.State.Value.BodyYaw;
            if (!_hasSmoothedYaw)
            {
                _smoothedBodyYaw = serverBodyYaw;
                _hasSmoothedYaw = true;
                return;
            }
            float k = 1f - Mathf.Exp(-12f * dt);
            _smoothedBodyYaw = Mathf.LerpAngle(_smoothedBodyYaw, serverBodyYaw, k);
        }

        void DrainSnapshots()
        {
            _reconciler.Reconcile(_body.State.Value, _body.LastAckedP1Sequence.Value, _predictSim);
        }

        /// <summary>Latch edge-triggered actions every frame so the rate-limited send cannot miss one.</summary>
        void PollInputEdges()
        {
            if (AutoDrive || !M3LocalInput.GameplayActive) return;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame) _pendingP1.Jump = true;
            if (kb.qKey.wasPressedThisFrame) _pendingP1.Dodge = true;
            if (kb.cKey.wasPressedThisFrame) _pendingP1.Slide = true;
            if (kb.eKey.wasPressedThisFrame) _pendingP1.Vault = true;
            if (kb.fKey.wasPressedThisFrame) _pendingP1.LightKick = true;
            if (kb.vKey.wasPressedThisFrame) _pendingP1.HeavyKick = true;
            if (kb.gKey.wasPressedThisFrame) _pendingGrenade = true;
            if (kb.tKey.wasPressedThisFrame) _pendingSmoke = true;
            if (kb.yKey.wasPressedThisFrame) _pendingFlash = true;
#endif
        }

        void HandleBuy()
        {
            if (!AutoBuy || _director == null || EffectiveRole != 1) return;
            if (_director.CurrentPhase != M3Phase.Buy) return;
            if (_boughtRound == _director.RoundIndex.Value) return;
            _boughtRound = _director.RoundIndex.Value;

            // DRAFT budget 1200: rifle 700 + grenade 200 + smoke 150 + flash 150 = 1200.
            _body.SubmitBuyServerRpc(0); // rifle (primary)
            _body.SubmitBuyServerRpc(6); // grenade (utility)
            _body.SubmitBuyServerRpc(4); // smoke (utility)
            _body.SubmitBuyServerRpc(5); // flash (utility)
            Debug.Log($"[M3-client] {M3DuelSlots.Name(LocalSlotIndex, BodiesPerTeam)} auto-buy round {_boughtRound}");
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
            LocalAimYaw = M2BodySim.ClampToSector(input.AimYaw, _smoothedBodyYaw, Mathf.Max(1f, SectorHalfDegrees - 1f));
            LocalAimPitch = Mathf.Clamp(input.AimPitch, -MaxPitchDegrees, MaxPitchDegrees);
            input.AimYaw = LocalAimYaw;
            return input;
        }

        M2P1Input BuildManualP1()
        {
            var input = new M2P1Input();
            input.Jump = _pendingP1.Jump;
            input.Dodge = _pendingP1.Dodge;
            input.Slide = _pendingP1.Slide;
            input.Vault = _pendingP1.Vault;
            input.LightKick = _pendingP1.LightKick;
            input.HeavyKick = _pendingP1.HeavyKick;
            _pendingP1 = default;

            if (!M3LocalInput.GameplayActive) return input;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb != null)
            {
                input.MoveX = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                input.MoveZ = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                input.Sprint = kb.leftShiftKey.isPressed;
                input.AlignBody = kb.leftAltKey.isPressed;
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
#endif
            return input;
        }

        M2P2Input BuildManualP2()
        {
#if ENABLE_INPUT_SYSTEM
            if (M3LocalInput.GameplayActive)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 delta = mouse.delta.ReadValue() * M3LocalInput.MouseSensitivity;
                    LastMouseDelta = delta;
                    _manualAimYaw += delta.x;
                    _manualAimPitch = Mathf.Clamp(_manualAimPitch - delta.y, -MaxPitchDegrees, MaxPitchDegrees);
                }
                M3LocalInput.ConsumeInjectedLook(out float injectedYaw, out float injectedPitch);
                _manualAimYaw += injectedYaw;
                _manualAimPitch = Mathf.Clamp(_manualAimPitch - injectedPitch, -MaxPitchDegrees, MaxPitchDegrees); // injected pitch is "look up" positive
            }
#endif
            var input = new M2P2Input();
            float innerHalf = Mathf.Max(1f, SectorHalfDegrees - 1f);
            LocalAimYaw = M2BodySim.ClampToSector(_manualAimYaw, _smoothedBodyYaw, innerHalf);
            LocalAimPitch = Mathf.Clamp(_manualAimPitch, -MaxPitchDegrees, MaxPitchDegrees);
            input.AimYaw = LocalAimYaw;
            input.AimPitch = LocalAimPitch;
#if ENABLE_INPUT_SYSTEM
            if (M3LocalInput.GameplayActive)
            {
                Mouse mouse = Mouse.current;
                Keyboard kb = Keyboard.current;
                if (mouse != null) input.Fire = mouse.leftButton.isPressed;
                if (kb != null) input.Reload = kb.rKey.isPressed;
            }
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
