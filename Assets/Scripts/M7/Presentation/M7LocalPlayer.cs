using BeMyArms.M2;
using BeMyArms.M3;
using Unity.Cinemachine;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BeMyArms.M7
{
    /// <summary>
    /// Local-player presentation for a match.
    ///
    /// P1 third person is a real Cinemachine rig: a dedicated look/pivot target is placed from the
    /// predicted shared-body state (LookYaw/LookPitch) and Cinemachine's third-person follow, framing
    /// and collision place the camera. The character transform is never forced to the camera
    /// direction, so BodyYaw stays independently driven by the shared-body simulation.
    ///
    /// P2 first person reuses the real P2 arms + rifle already mounted and IK-driven on the shared
    /// body: the local P1 skin is hidden and the camera sits at the P2 camera anchor, so the
    /// first-person view is the actual rig, not a separate primitive viewmodel.
    ///
    /// Cursor capture follows the explicit gameplay/UI state: gameplay (buy or live, focused, no
    /// interactive UI) captures the mouse; the ESC menu or an interactive buy panel releases it. It
    /// only reads simulation/replicated state.
    /// </summary>
    public class M7LocalPlayer : MonoBehaviour
    {
        const int PlayerBodyLayer = 8; // TagManager "PlayerBody"

        [Header("P1 third person (Cinemachine)")]
        public float P1Distance = 4.6f;
        public float P1PivotHeight = 1.55f;
        public float P1VerticalArmLength = 0.12f;
        public float P1CameraSide = 0.62f;
        public float P1MinPitch = -55f;
        public float P1MaxPitch = 70f;
        public float FieldOfView = 72f;

        [Header("P2 first person")]
        public float P2EyeHeight = 1.52f;
        public Vector3 P2EyeOffset = new Vector3(0f, 0.09f, 0.16f);

        Camera _camera;
        CinemachineBrain _brain;
        CinemachineCamera _p1Cam;
        CinemachineCamera _p2Cam;
        Transform _pivot;
        Transform _aimTarget;
        M7PauseMenu _pauseMenu;

        M3DuelClient _client;
        M3DuelDirector _director;
        M2MovementCollision _collision;
        bool _collisionLoaded;
        int _layerAppliedForBody = -1;
        int _cameraRole = -1;

        /// <summary>True while the local player owns gameplay input (in-match, focused, no UI).</summary>
        public bool IsGameplayActive { get; private set; }
        /// <summary>Set by the HUD while an interactive P2 buy/menu UI is genuinely open.</summary>
        public bool BuyMenuOpen { get; set; }
        public M3DuelClient LocalClient => _client;
        public Camera LocalCamera => _camera;

        void Start()
        {
            _pauseMenu = gameObject.AddComponent<M7PauseMenu>();
            CreateCamera();
        }

        void Update()
        {
            if (Application.isBatchMode)
            {
                if (_camera != null) { Destroy(_camera.gameObject); _camera = null; }
                return;
            }

            _client = FindLocalClient();
            if (_director == null) _director = M3DuelDirector.Instance;

            HandleEscape();
            UpdateInputMode();
        }

        void OnDisable()
        {
            M3LocalInput.GameplayActive = false;
            M3LocalInput.CursorCaptured = false;
        }

        void LateUpdate()
        {
            if (Application.isBatchMode || _client == null || _camera == null) return;
            EnsureCameraForRole();

            M2BodyState state = _client.ViewState;
            if (_client.LocalRoleIndex == 0) UpdateP1Camera(state);
            else UpdateP2Camera(state);

            // Frame-accurate: the look/aim above is sampled this frame, then Cinemachine applies it.
            if (_brain != null) _brain.ManualUpdate(Time.frameCount, Time.deltaTime);

            // Cinemachine's collision needs colliders; the arena doorways are gaps, so additionally
            // keep the third-person camera inside the authoritative map bounds.
            if (_client.LocalRoleIndex == 0) ClampCameraToArena();
        }

        void ClampCameraToArena()
        {
            M2MovementCollision collision = Collision();
            if (collision == null || !collision.HasBounds || _camera == null) return;
            Vector3 p = _camera.transform.position;
            p.x = Mathf.Clamp(p.x, collision.MinX + 0.6f, collision.MaxX - 0.6f);
            p.z = Mathf.Clamp(p.z, collision.MinZ + 0.6f, collision.MaxZ - 0.6f);
            p.y = Mathf.Max(p.y, 0.6f);
            _camera.transform.position = p;
        }

        M2MovementCollision Collision()
        {
            if (_collisionLoaded) return _collision;
            _collisionLoaded = true;
            M3MapSpawns map = FindAnyObjectByType<M3MapSpawns>();
            if (map != null) _collision = map.BuildCollision();
            return _collision;
        }

        // ---- Escape / cursor ----

        void HandleEscape()
        {
            if (_pauseMenu == null) return;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                _pauseMenu.Toggle();
#endif
        }

        void UpdateInputMode()
        {
            bool paused = _pauseMenu != null && _pauseMenu.IsOpen;
            bool focused = Application.isFocused;
            bool inMatch = _director != null && _director.CurrentPhase != M3Phase.Warmup && _client != null && _client.IsLocalOwnBody;
            bool ended = _director != null && (_director.CurrentPhase == M3Phase.MatchEnd || _director.MatchWinner.Value >= 0);
            bool roleP2 = _client != null && _client.LocalRoleIndex == 1;
            bool buyUi = roleP2 && BuyMenuOpen && _director != null && _director.IsBuy;

            // Gameplay state is the source of truth; cursor capture is a consequence of it.
            bool gameplay = inMatch && !ended && !paused && !buyUi && focused;
            IsGameplayActive = gameplay;
            M3LocalInput.GameplayActive = gameplay;
            M3LocalInput.CursorCaptured = gameplay;
            M3LocalInput.MouseSensitivity = M7Settings.MouseSensitivity;

            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }

        // ---- Camera ----

        void CreateCamera()
        {
            if (_camera != null) return;
            var go = new GameObject("M7_LocalCamera");
            _camera = go.AddComponent<Camera>();
            _camera.nearClipPlane = 0.03f;
            _camera.farClipPlane = 600f;
            _camera.fieldOfView = FieldOfView;
            _camera.tag = "MainCamera";
            go.transform.SetPositionAndRotation(new Vector3(0f, 3f, -10f), Quaternion.Euler(10f, 0f, 0f));

            _brain = go.AddComponent<CinemachineBrain>();
            _brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.25f);

            // Dedicated look/pivot target: the camera orbits this, not the character transform.
            var pivotGo = new GameObject("M7_P1LookPivot");
            _pivot = pivotGo.transform;
            var aimGo = new GameObject("M7_P1AimTarget");
            _aimTarget = aimGo.transform;
            _aimTarget.SetParent(_pivot, false);
            _aimTarget.localPosition = new Vector3(0f, 0f, 20f);

            var p1Go = new GameObject("M7_P1Camera");
            _p1Cam = p1Go.AddComponent<CinemachineCamera>();
            _p1Cam.Follow = _pivot;
            _p1Cam.LookAt = _aimTarget;
            _p1Cam.Lens.FieldOfView = FieldOfView;
            var body = p1Go.AddComponent<CinemachineThirdPersonFollow>();
            body.VerticalArmLength = P1VerticalArmLength;
            body.CameraSide = P1CameraSide;
            body.CameraDistance = P1Distance;
            body.ShoulderOffset = Vector3.zero;
            body.Damping = new Vector3(0.08f, 0.08f, 0.2f);
            body.AvoidObstacles.Enabled = true;
            body.AvoidObstacles.CollisionFilter = 1; // Default layer only (arena camera colliders)
            body.AvoidObstacles.CameraRadius = 0.3f;
            body.AvoidObstacles.DampingIntoCollision = 0.06f;
            body.AvoidObstacles.DampingFromCollision = 0.35f;
            var composer = p1Go.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.06f, 0.06f);

            var p2Go = new GameObject("M7_P2Camera");
            _p2Cam = p2Go.AddComponent<CinemachineCamera>();
            _p2Cam.Lens.FieldOfView = FieldOfView;

            _p1Cam.Priority.Value = 0;
            _p2Cam.Priority.Value = 0;
            _cameraRole = -1;
        }

        void EnsureCameraForRole()
        {
            int role = _client.LocalRoleIndex;
            if (_camera == null) CreateCamera();
            if (_cameraRole == role) return;
            _cameraRole = role;

            _camera.cullingMask = role == 1
                ? ~(1 << PlayerBodyLayer)   // FP: hide the P1 skin, keep the real P2 arms+rifle
                : ~0;                        // TP: show everything
            _camera.fieldOfView = FieldOfView;

            _p1Cam.Priority.Value = role == 0 ? 20 : 0;
            _p2Cam.Priority.Value = role == 1 ? 20 : 0;

            // The local body was put on PlayerBody; for P2 first person only the arms and rifle are
            // restored to the default layer so the first-person camera renders the actual rig.
            M3DuelBody body = _client.Body;
            var animator = body != null ? body.GetComponent<M7CharacterAnimator>() : null;
            if (animator != null)
            {
                if (animator.P1Skin != null) SetLayerRecursively(animator.P1Skin.gameObject, PlayerBodyLayer);
                if (animator.P2Skin != null) SetLayerRecursively(animator.P2Skin.gameObject, role == 1 ? 0 : PlayerBodyLayer);
                if (animator.Weapon != null) SetLayerRecursively(animator.Weapon.gameObject, role == 1 ? 0 : PlayerBodyLayer);
            }
        }

        void UpdateP1Camera(M2BodyState state)
        {
            if (_pivot == null || _camera == null) return;
            float pitch = Mathf.Clamp(_client.LocalLookPitch, P1MinPitch, P1MaxPitch);
            Vector3 pivot = _client.VisualPosition + Vector3.up * P1PivotHeight;
            _pivot.SetPositionAndRotation(pivot, Quaternion.Euler(pitch, _client.LocalLookYaw, 0f));
        }

        void UpdateP2Camera(M2BodyState state)
        {
            if (_p2Cam == null) return;
            float pitch = Mathf.Clamp(_client.LocalAimPitch, -80f, 80f);
            Quaternion rotation = Quaternion.Euler(pitch, _client.LocalAimYaw, 0f);

            Vector3 eye = new Vector3(state.PosX, state.PosY + P2EyeHeight, state.PosZ);
            Transform anchor = FindDeep(_client.Presentation, "P2CameraAnchor");
            if (anchor != null) eye = anchor.position + rotation * P2EyeOffset;

            _p2Cam.transform.SetPositionAndRotation(eye + rotation * Vector3.forward * 0.05f, rotation);
        }

        // ---- Helpers ----

        M3DuelClient FindLocalClient()
        {
            if (_client != null && _client.IsLocalOwnBody) return _client;
            M3DuelClient[] clients = FindObjectsByType<M3DuelClient>(FindObjectsSortMode.None);
            for (int i = 0; i < clients.Length; i++)
                if (clients[i].IsLocalOwnBody)
                {
                    ApplyLocalLayer(clients[i]);
                    return clients[i];
                }
            return _client;
        }

        void ApplyLocalLayer(M3DuelClient client)
        {
            M3DuelBody body = client.Body;
            if (body == null || !body.IsSpawned) return;
            if (_layerAppliedForBody == body.GetInstanceID()) return;
            _layerAppliedForBody = body.GetInstanceID();
            SetLayerRecursively(body.gameObject, PlayerBodyLayer);
            _cameraRole = -1; // force the per-role layer split to be re-applied
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }
    }
}
