using BeMyArms.M2;
using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BeMyArms.M3
{
    /// <summary>
    /// Role-aware client for one Duel body: P1 predicts/reconciles the body and P2 keeps aim local
    /// (Model C), exactly like the M2 spike but driven by the M3 director's buy/live/round state.
    /// It also presents every body from replicated state, and (for headless/multi-process test runs)
    /// can auto-drive both roles. Only the local player's body sends inputs.
    /// </summary>
    public class M3DuelClient : NetworkBehaviour
    {
        public static M3DuelSlot LocalSlot = M3DuelSlot.None;

        public static void SetLocalSlot(M3DuelSlot slot) => LocalSlot = slot;

        public Transform Presentation;

        [Header("Tuning (must match the server body)")]
        public float MoveSpeed = 5f;
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
        public float MouseSensitivity = 0.12f;

        public float LocalAimYaw { get; private set; }
        public float LocalAimPitch { get; private set; }

        M3DuelBody _body;
        M3DuelBody _enemy;
        M3DuelDirector _director;
        M2BodySim _predictSim;
        M2Reconciler _reconciler;

        uint _p1Sequence;
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
        float _manualAimYaw;
        float _manualAimPitch;

        Vector3 _visualPosition;
        float _visualYaw;
        bool _hasVisual;

        Camera _camera;
        Transform _eye;

        public int EffectiveRole => M3DuelSlots.IsValid(LocalSlot) ? M3DuelSlots.Role(LocalSlot) : M3Config.ClientRole;
        public int LocalTeam => M3DuelSlots.IsValid(LocalSlot) ? M3DuelSlots.Team(LocalSlot) : M3Config.ClientTeam;
        public bool IsOwnBody => _body != null && _body.IsSpawned && _body.TeamIndex == LocalTeam;

        void Start()
        {
            if (!M3DuelSlots.IsValid(LocalSlot)) LocalSlot = M3DuelSlots.FromTeamRole(M3Config.ClientTeam, M3Config.ClientRole);
            AutoDrive = M3Config.AutoDrive;
            AutoBuy = M3Config.AutoBuy;
            AutoFire = M3Config.AutoFire;
            AutoUtility = M3Config.AutoUtility;

            _body = GetComponent<M3DuelBody>();
            _predictSim = new M2BodySim
            {
                MoveSpeed = MoveSpeed,
                NeckYawLimitDegrees = NeckYawLimitDegrees,
                BodyFollowThresholdDegrees = BodyFollowThresholdDegrees,
                BodyFollowSpeedDegreesPerSecond = BodyFollowSpeedDegreesPerSecond,
                BodyAlignSpeedDegreesPerSecond = BodyAlignSpeedDegreesPerSecond,
                SectorHalfDegrees = SectorHalfDegrees,
                MaxPitchDegrees = MaxPitchDegrees
            };
            _predictSim.Initialize(0f);
            _reconciler = new M2Reconciler();
            _reconciler.Reset(_predictSim.State);
            _manualAimYaw = 0f;
        }

        void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsClient || _body == null || !_body.IsSpawned) return;

            if (_director == null) _director = M3DuelDirector.Instance;

            if (!IsOwnBody)
            {
                ApplyPresentation(Time.deltaTime);
                return;
            }

            EnsureCamera();
            HandleBuy();

            if (Time.time < _nextSendTime)
            {
                ApplyPresentation(Time.deltaTime);
                return;
            }
            _nextSendTime = Time.time + 1f / Mathf.Max(1f, SendRateHz);
            float dt = 1f / Mathf.Max(1f, SendRateHz);

            if (EffectiveRole == 0)
            {
                DrainSnapshots();
                SendP1(dt);
            }
            else
            {
                SendP2();
            }

            ApplyPresentation(dt);
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
            Debug.Log($"[M3-client] team {(LocalTeam == 0 ? "A" : "B")} P2 auto-buy round {_boughtRound}");
        }

        void DrainSnapshots()
        {
            // Snapshots arrive on the NGO tick; the pure reconciler is fed from the replicated state.
            _reconciler.Reconcile(_body.State.Value, _body.LastAckedP1Sequence.Value, _predictSim);
        }

        void SendP1(float dt)
        {
            M2P1Input input = AutoDrive ? BuildAutoP1(dt) : BuildManualP1();
            input.Sequence = ++_p1Sequence;
            _reconciler.Predict(input, dt, _predictSim);
            _body.SubmitP1ServerRpc(input);
        }

        void SendP2()
        {
            M2P2Input input = AutoDrive ? BuildAutoP2() : BuildManualP2();
            input.Sequence = ++_p2Sequence;

            float serverBodyYaw = _body.State.Value.BodyYaw;
            if (!_hasSmoothedYaw)
            {
                _smoothedBodyYaw = serverBodyYaw;
                _hasSmoothedYaw = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-12f * Time.deltaTime);
                _smoothedBodyYaw = Mathf.LerpAngle(_smoothedBodyYaw, serverBodyYaw, k);
            }

            input.AimYaw = M2BodySim.ClampToSector(input.AimYaw, _smoothedBodyYaw, Mathf.Max(1f, SectorHalfDegrees - 1f));
            LocalAimYaw = input.AimYaw;
            LocalAimPitch = Mathf.Clamp(input.AimPitch, -MaxPitchDegrees, MaxPitchDegrees);
            _body.SubmitP2ServerRpc(input);

            HandleUtility();
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
                Debug.Log($"[M3-client] throw grenade team {LocalTeam}");
                _body.SubmitUtilityServerRpc((byte)M3UtilityKind.Grenade);
            }
            if (!_smokeSent && elapsed > 1.2f)
            {
                _smokeSent = true;
                Debug.Log($"[M3-client] throw smoke team {LocalTeam}");
                _body.SubmitUtilityServerRpc((byte)M3UtilityKind.Smoke);
            }
            if (!_flashSent && elapsed > 1.8f)
            {
                _flashSent = true;
                Debug.Log($"[M3-client] throw flash team {LocalTeam}");
                _body.SubmitUtilityServerRpc((byte)M3UtilityKind.Flash);
            }
        }

        M2P1Input BuildAutoP1(float dt)
        {
            var input = new M2P1Input();
            _autoClock += dt;
            if (FindEnemy() != null)
            {
                float dx = _enemy.State.Value.PosX - _predictSim.State.PosX;
                float dz = _enemy.State.Value.PosZ - _predictSim.State.PosZ;
                float desired = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                float delta = Mathf.Clamp(M2BodySim.Normalize(desired - _predictSim.State.LookYaw), -25f, 25f);
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
            if (FindEnemy() != null)
            {
                float dx = _enemy.State.Value.PosX - _body.State.Value.PosX;
                float dz = _enemy.State.Value.PosZ - _body.State.Value.PosZ;
                input.AimYaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                input.Fire = AutoFire;
                input.Reload = _body.Ammo.Value <= 1;
            }
            else
            {
                input.AimYaw = _body.State.Value.BodyYaw;
            }
            return input;
        }

        M2P1Input BuildManualP1()
        {
            var input = new M2P1Input();
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return input;
            input.MoveX = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            input.MoveZ = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            if (Mouse.current != null) input.LookYawDelta = Mouse.current.delta.x.ReadValue() * MouseSensitivity;
            input.AlignBody = kb.leftAltKey.isPressed;
#endif
            return input;
        }

        M2P2Input BuildManualP2()
        {
            var input = new M2P2Input();
#if ENABLE_INPUT_SYSTEM
            _manualAimYaw += Mouse.current != null ? Mouse.current.delta.x.ReadValue() * MouseSensitivity : 0f;
            _manualAimPitch = Mathf.Clamp(_manualAimPitch - (Mouse.current != null ? Mouse.current.delta.y.ReadValue() * MouseSensitivity : 0f), -MaxPitchDegrees, MaxPitchDegrees);
            input.AimYaw = _manualAimYaw;
            input.AimPitch = _manualAimPitch;
            input.Fire = Mouse.current != null && Mouse.current.leftButton.isPressed;
            input.Reload = Keyboard.current != null && Keyboard.current.rKey.isPressed;
#endif
            return input;
        }

        M3DuelBody FindEnemy()
        {
            if (_enemy != null && _enemy.IsSpawned) return _enemy;
            M3DuelBody[] bodies = FindObjectsByType<M3DuelBody>();
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].IsSpawned && bodies[i].TeamIndex != _body.TeamIndex)
                {
                    _enemy = bodies[i];
                    return _enemy;
                }
            }
            return null;
        }

        void ApplyPresentation(float dt)
        {
            if (Presentation == null || _body == null || !_body.IsSpawned) return;

            M2BodyState state = IsOwnBody && EffectiveRole == 0 ? _reconciler.Predicted : _body.State.Value;
            Vector3 targetPos = new Vector3(state.PosX, Presentation.position.y, state.PosZ);
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

            if (_camera != null) UpdateCamera();
        }

        void EnsureCamera()
        {
            if (Application.isBatchMode || _camera != null) return;
            bool own = IsOwnBody;
            if (!own) return;

            var go = new GameObject(EffectiveRole == 0 ? "M3_P1Camera" : "M3_P2Camera");
            _camera = go.AddComponent<Camera>();
            _camera.nearClipPlane = 0.1f;
            _camera.tag = "MainCamera";

            if (EffectiveRole == 1)
            {
                var eye = new GameObject("M3_P2Eye");
                eye.transform.SetParent(Presentation, false);
                eye.transform.localPosition = new Vector3(0f, 1.45f, 0.22f);
                _eye = eye.transform;
            }
        }

        void UpdateCamera()
        {
            if (EffectiveRole == 0)
            {
                Vector3 forward = Quaternion.Euler(0f, _visualYaw, 0f) * Vector3.forward;
                _camera.transform.position = _visualPosition - forward * 4.5f + Vector3.up * 1.6f;
                _camera.transform.rotation = Quaternion.LookRotation(_visualPosition + Vector3.up * 1.2f - _camera.transform.position);
            }
            else
            {
                if (_eye == null) return;
                _camera.transform.position = _eye.position;
                _camera.transform.rotation = Quaternion.Euler(LocalAimPitch, LocalAimYaw, 0f);
            }
        }
    }
}
