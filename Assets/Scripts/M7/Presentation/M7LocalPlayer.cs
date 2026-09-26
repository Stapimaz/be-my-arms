using BeMyArms.M2;
using BeMyArms.M3;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BeMyArms.M7
{
    /// <summary>
    /// Local-player presentation for a match.
    ///
    /// P1 third person is a Cinemachine rig orbiting a dedicated look/pivot target placed from the
    /// predicted shared-body state; BodyYaw stays simulation-owned.
    ///
    /// P2 first person follows the conventional shooter separation: a stable logical camera at a
    /// fixed eye height over the shared body's position, rotated directly from local P2 aim, with the
    /// whole local world-body hidden and a dedicated camera-local arms + rifle viewmodel drawn in a
    /// fixed screen-space composition. The viewmodel never feeds back into authoritative aim/hit
    /// detection.
    ///
    /// Firing feel is separated from hit confirmation: a locally valid trigger pull immediately plays
    /// the rifle sound, muzzle flash, viewmodel kick and a small camera recoil impulse, while
    /// hitmarkers/damage/kills remain server-authoritative.
    /// </summary>
    public class M7LocalPlayer : MonoBehaviour
    {
        const int PlayerBodyLayer = 8; // TagManager "PlayerBody"
        const int ViewModelLayer = 9;  // dedicated first-person viewmodel layer

        [Header("P1 third person (Cinemachine)")]
        public float P1Distance = 4.6f;
        public float P1PivotHeight = 1.55f;
        public float P1VerticalArmLength = 0.12f;
        public float P1CameraSide = 0.62f;
        public float P1MinPitch = -55f;
        public float P1MaxPitch = 70f;
        public float FieldOfView = 72f;

        [Header("P2 first person")]
        public float P2EyeHeight = 1.58f;
        public float ViewmodelFieldOfView = 68f;

        [Header("Shot feel")]
        public float CameraRecoilPitch = 1.15f;
        public float CameraRecoilYawJitter = 0.35f;
        public float CameraRecoilRecovery = 10f;

        Camera _camera;
        Camera _viewmodelCamera;
        CinemachineBrain _brain;
        CinemachineCamera _p1Cam;
        CinemachineCamera _p2Cam;
        Transform _pivot;
        Transform _aimTarget;
        M7PauseMenu _pauseMenu;

        GameObject _viewmodel;
        Transform _viewmodelWeapon;
        float _viewmodelKick;
        float _viewmodelKickVelocity;
        float _camRecoilPitch;
        float _camRecoilYaw;
        float _nextMuzzle;

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
        public GameObject Viewmodel => _viewmodel;

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
            if (_client.LocalRoleIndex == 0)
            {
                UpdateP1Camera(state);
            }
            else
            {
                UpdateShotFeedback(Time.deltaTime);
                UpdateP2Camera(state);
            }

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

            // Dedicated viewmodel camera: a lower FOV and its own layer make the arms/rifle read at a
            // conventional first-person scale and draw over the world without being clipped by it.
            var vmGo = new GameObject("M7_ViewModelCamera");
            vmGo.transform.SetParent(go.transform, false);
            _viewmodelCamera = vmGo.AddComponent<Camera>();
            _viewmodelCamera.cullingMask = 1 << ViewModelLayer;
            _viewmodelCamera.fieldOfView = ViewmodelFieldOfView;
            _viewmodelCamera.nearClipPlane = 0.01f;
            _viewmodelCamera.farClipPlane = 20f;
            _viewmodelCamera.enabled = false;

            // URP requires camera stacking: the viewmodel camera is an Overlay in the main camera's
            // stack, so it composites on top of the world instead of replacing it.
            var mainData = go.GetComponent<UniversalAdditionalCameraData>();
            if (mainData == null) mainData = go.AddComponent<UniversalAdditionalCameraData>();
            mainData.renderType = CameraRenderType.Base;
            var vmData = vmGo.AddComponent<UniversalAdditionalCameraData>();
            vmData.renderType = CameraRenderType.Overlay;
            mainData.cameraStack.Add(_viewmodelCamera);

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

            _camera.cullingMask = (role == 1 ? ~(1 << PlayerBodyLayer) : ~0) & ~(1 << ViewModelLayer);
            _camera.fieldOfView = FieldOfView;
            if (_viewmodelCamera != null) _viewmodelCamera.enabled = role == 1;

            _p1Cam.Priority.Value = role == 0 ? 20 : 0;
            _p2Cam.Priority.Value = role == 1 ? 20 : 0;

            if (role == 1) BuildViewmodel();
            else DestroyViewmodel();
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
            float pitch = Mathf.Clamp(_client.LocalAimPitch, -80f, 80f) - _camRecoilPitch;
            Quaternion rotation = Quaternion.Euler(pitch, _client.LocalAimYaw + _camRecoilYaw, 0f);

            // Stable logical eye: follows the smoothed shared-body position at a fixed height and is
            // completely independent of the animated chest/shoulder rig.
            Vector3 eye = _client.VisualPosition + Vector3.up * P2EyeHeight;
            _p2Cam.transform.SetPositionAndRotation(eye, rotation);
        }

        // ---- P2 first-person viewmodel + shot feel ----

        void BuildViewmodel()
        {
            DestroyViewmodel();
            if (_camera == null) return;

            var prefab = Resources.Load<GameObject>("M7_P2ArmsViewmodel");
            if (prefab == null)
            {
                Debug.LogWarning("[M7] P2 viewmodel prefab not found in Resources.");
                return;
            }

            _viewmodel = Instantiate(prefab, _viewmodelCamera != null ? _viewmodelCamera.transform : _camera.transform);
            _viewmodel.name = "M7_P2Viewmodel";
            _viewmodel.transform.localPosition = Vector3.zero;
            _viewmodel.transform.localRotation = Quaternion.identity;
            _viewmodel.transform.localScale = Vector3.one;
            _viewmodelWeapon = FindDeep(_viewmodel.transform, "ViewmodelWeapon");
            SetLayerRecursively(_viewmodel, ViewModelLayer);
        }

        void DestroyViewmodel()
        {
            if (_viewmodel != null) Destroy(_viewmodel);
            _viewmodel = null;
            _viewmodelWeapon = null;
        }

        void UpdateShotFeedback(float dt)
        {
            int shots = _client != null ? _client.ConsumePendingLocalShots() : 0;
            for (int i = 0; i < shots; i++)
            {
                _viewmodelKickVelocity += 1.0f;
                _camRecoilPitch += CameraRecoilPitch;
                _camRecoilYaw += Random.Range(-CameraRecoilYawJitter, CameraRecoilYawJitter);

                M7AudioService audio = M7AudioService.Instance;
                if (audio != null) audio.Play(M7AudioId.RifleShot);

                if (M7VfxService.Instance != null && _viewmodelWeapon != null && Time.time >= _nextMuzzle)
                {
                    _nextMuzzle = Time.time + 0.03f;
                    M7VfxService.Instance.Spawn(M7VfxId.MuzzleFlash,
                        _viewmodelWeapon.position + _viewmodelWeapon.forward * 0.3f, _viewmodelWeapon.rotation);
                }
            }

            // Quick camera recovery.
            _camRecoilPitch = Mathf.MoveTowards(_camRecoilPitch, 0f, dt * CameraRecoilRecovery);
            _camRecoilYaw = Mathf.MoveTowards(_camRecoilYaw, 0f, dt * CameraRecoilRecovery);

            if (_viewmodel == null) return;
            // Critically-damped viewmodel kick.
            _viewmodelKickVelocity -= _viewmodelKick * 150f * dt;
            _viewmodelKickVelocity *= Mathf.Exp(-16f * dt);
            _viewmodelKick += _viewmodelKickVelocity * dt;
            _viewmodelKick = Mathf.Clamp(_viewmodelKick, 0f, 0.08f);
            float k = _viewmodelKick / 0.08f;
            _viewmodel.transform.localPosition = new Vector3(0.006f * k, 0.008f * k, -0.05f * k);
            _viewmodel.transform.localRotation = Quaternion.Euler(-7f * k, 1.5f * k, 0f);
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
            _cameraRole = -1; // force the per-role camera setup to be re-applied
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
