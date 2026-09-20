using BeMyArms.M2;
using BeMyArms.M3;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BeMyArms.M7
{
    /// <summary>
    /// Local-player presentation for a match: the P1 third-person and P2 first-person cameras, the
    /// P2 viewmodel, cursor capture and the in-match ESC menu. It only reads simulation/replicated
    /// state (via <see cref="M3DuelClient"/>) and never writes authoritative state, hitboxes or aim.
    /// </summary>
    public class M7LocalPlayer : MonoBehaviour
    {
        const int PlayerBodyLayer = 8; // TagManager "PlayerBody"

        [Header("P1 third person")]
        public float P1Distance = 4.6f;
        public float P1PivotHeight = 1.65f;
        public float P1LookAtHeight = 1.35f;
        public float P1MinPitch = -55f;
        public float P1MaxPitch = 70f;
        public float FieldOfView = 72f;

        [Header("P2 first person")]
        public Vector3 ViewmodelWeaponPosition = new Vector3(0.13f, -0.13f, 0.30f);
        public Vector3 ViewmodelWeaponEuler = new Vector3(2f, -4f, 0f);
        public float ViewmodelWeaponScale = 0.35f;

        Camera _camera;
        int _cameraRole = -1;
        GameObject _viewmodel;
        Transform _viewmodelWeapon;
        M7PauseMenu _pauseMenu;
        M2MovementCollision _collision;
        bool _collisionLoaded;

        M3DuelClient _client;
        M3DuelDirector _director;
        int _layerAppliedForTeam = -1;

        int _lastAmmo = -1;
        float _recoil;
        float _recoilVelocity;

        void Start()
        {
            _pauseMenu = gameObject.AddComponent<M7PauseMenu>();
            CreateCamera(); // fallback so the map is visible before the local body spawns
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
            UpdateCursor();
        }

        void LateUpdate()
        {
            if (Application.isBatchMode || _client == null) return;
            EnsureCameraForRole();

            M2BodyState state = _client.ViewState;
            if (_client.LocalRoleIndex == 0) UpdateP1Camera(state);
            else UpdateP2Camera(state);

            UpdateViewmodel();
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

        void UpdateCursor()
        {
            bool paused = _pauseMenu != null && _pauseMenu.IsOpen;
            bool live = _director != null && _director.IsLive && _client != null && _client.IsLocalOwnBody;
            bool capture = live && !paused;

            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        // ---- Camera ----

        void CreateCamera()
        {
            if (_camera != null) return;
            var go = new GameObject("M7_LocalCamera");
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 600f;
            cam.fieldOfView = FieldOfView;
            cam.tag = "MainCamera";
            go.transform.SetPositionAndRotation(new Vector3(0f, 3f, -10f), Quaternion.Euler(10f, 0f, 0f));
            _camera = cam;
            _cameraRole = -1;
        }

        void EnsureCameraForRole()
        {
            int role = _client.LocalRoleIndex;
            if (_camera == null) CreateCamera();
            if (_cameraRole == role) return;

            _cameraRole = role;
            _camera.cullingMask = role == 1
                ? ~(1 << PlayerBodyLayer)   // FP: hide our own combined body
                : ~0;                        // TP: show everything
            _camera.fieldOfView = FieldOfView;

            if (role == 1) BuildViewmodel();
            else DestroyViewmodel();
        }

        void UpdateP1Camera(M2BodyState state)
        {
            if (_camera == null) return;
            float pitch = Mathf.Clamp(_client.LocalLookPitch, P1MinPitch, P1MaxPitch);
            Quaternion rotation = Quaternion.Euler(pitch, _client.LocalLookYaw, 0f);
            Vector3 pivot = new Vector3(state.PosX, state.PosY + P1PivotHeight, state.PosZ);
            Vector3 back = rotation * Vector3.back;

            // Spring arm: pull the camera in when a wall/cover is behind the body.
            float distance = P1Distance;
            M2MovementCollision collision = Collision();
            if (collision != null &&
                collision.RaycastSolids(pivot.x, pivot.y, pivot.z, back.x, back.y, back.z, P1Distance + 0.4f, out float hit))
                distance = Mathf.Max(1.0f, hit - 0.35f);

            Vector3 position = pivot + back * distance;
            if (collision != null && collision.HasBounds)
            {
                // Never let the third-person camera leave the arena (e.g. through a doorway gap).
                position.x = Mathf.Clamp(position.x, collision.MinX + 0.6f, collision.MaxX - 0.6f);
                position.z = Mathf.Clamp(position.z, collision.MinZ + 0.6f, collision.MaxZ - 0.6f);
                position.y = Mathf.Max(position.y, 0.6f);
            }

            _camera.transform.SetPositionAndRotation(position, rotation);
        }

        M2MovementCollision Collision()
        {
            if (_collisionLoaded) return _collision;
            _collisionLoaded = true;
            M3MapSpawns map = FindAnyObjectByType<M3MapSpawns>();
            if (map != null) _collision = map.BuildCollision();
            return _collision;
        }

        void UpdateP2Camera(M2BodyState state)
        {
            if (_camera == null) return;
            Quaternion rotation = Quaternion.Euler(_client.LocalAimPitch, _client.LocalAimYaw, 0f);

            Vector3 eye = new Vector3(state.PosX, state.PosY + 1.50f, state.PosZ);
            Transform anchor = FindDeep(_client.Presentation, "P2CameraAnchor");
            if (anchor != null) eye = anchor.position + Vector3.up * 0.02f;

            _camera.transform.SetPositionAndRotation(eye + rotation * Vector3.forward * 0.05f, rotation);
        }

        // ---- P2 viewmodel (presentation only) ----

        void BuildViewmodel()
        {
            DestroyViewmodel();
            if (_camera == null || _client == null || _client.Presentation == null) return;

            _viewmodel = new GameObject("M7_P2Viewmodel");
            _viewmodel.transform.SetParent(_camera.transform, false);
            _viewmodel.transform.localPosition = Vector3.zero;
            _viewmodel.transform.localRotation = Quaternion.identity;
            _viewmodel.transform.localScale = Vector3.one;

            Transform source = FindDeep(_client.Presentation, "Weapon");
            if (source != null)
            {
                GameObject weapon = Instantiate(source.gameObject, _viewmodel.transform);
                weapon.name = "ViewmodelWeapon";
                weapon.transform.localPosition = ViewmodelWeaponPosition;
                weapon.transform.localRotation = Quaternion.Euler(ViewmodelWeaponEuler);
                weapon.transform.localScale = Vector3.one * ViewmodelWeaponScale;
                _viewmodelWeapon = weapon.transform;
            }

            // Simple forearm/hand blocks so the grip reads from P2's point of view.
            AddHand(new Vector3(0.15f, -0.17f, 0.33f), new Vector3(0.055f, 0.07f, 0.055f), 18f);
            AddHand(new Vector3(0.07f, -0.12f, 0.45f), new Vector3(0.05f, 0.06f, 0.05f), -22f);

            // The viewmodel must stay visible to the first-person camera (which hides the local
            // body's PlayerBody layer), so put the whole viewmodel back on the default layer.
            SetLayerRecursively(_viewmodel, 0);
        }

        void AddHand(Vector3 localPosition, Vector3 scale, float pitch)
        {
            var hand = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hand.name = "ViewmodelHand";
            Collider collider = hand.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            hand.transform.SetParent(_viewmodel.transform, false);
            hand.transform.localPosition = localPosition;
            hand.transform.localRotation = Quaternion.Euler(90f + pitch, 0f, 90f);
            hand.transform.localScale = scale;
            var renderer = hand.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.30f, 0.31f, 0.34f) };
        }

        void DestroyViewmodel()
        {
            if (_viewmodel != null) Destroy(_viewmodel);
            _viewmodel = null;
            _viewmodelWeapon = null;
        }

        void UpdateViewmodel()
        {
            if (_viewmodel == null) return;

            M3DuelBody body = _client.Body;
            if (body != null)
            {
                int ammo = body.Ammo.Value;
                if (_lastAmmo >= 0 && ammo < _lastAmmo && !body.Reloading.Value)
                {
                    _recoilVelocity += 0.9f;
                    if (M7VfxService.Instance != null && _viewmodelWeapon != null)
                        M7VfxService.Instance.Spawn(M7VfxId.MuzzleFlash, _viewmodelWeapon.position + _viewmodelWeapon.forward * 0.35f, _viewmodelWeapon.rotation);
                }
                _lastAmmo = ammo;
            }

            // Critically-damped recoil spring.
            _recoilVelocity -= _recoil * 120f * Time.deltaTime;
            _recoilVelocity *= Mathf.Exp(-14f * Time.deltaTime);
            _recoil += _recoilVelocity * Time.deltaTime;
            _recoil = Mathf.Clamp(_recoil, 0f, 0.12f);
            float k = _recoil / 0.12f;
            _viewmodel.transform.localPosition = new Vector3(0f, 0.012f * k, -_recoil);
            _viewmodel.transform.localRotation = Quaternion.Euler(-9f * k, 0f, 0f);
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
            if (_layerAppliedForTeam == body.GetInstanceID()) return;
            _layerAppliedForTeam = body.GetInstanceID();
            SetLayerRecursively(body.gameObject, PlayerBodyLayer);
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
