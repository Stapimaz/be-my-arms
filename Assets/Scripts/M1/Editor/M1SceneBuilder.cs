using System.Collections.Generic;
using System.IO;
using BeMyArms.M0;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.M1.EditorTools
{
    /// <summary>
    /// Builds the M1 local vertical-slice greybox scene and its tuning asset from code.
    /// Menu: Be My Arms > M1 > Build M1 Scene
    /// </summary>
    public static class M1SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/M1VerticalSlice.unity";
        const string TuningPath = "Assets/Scripts/M1/Data/M1Tuning.asset";

        [MenuItem("Be My Arms/M1/Build M1 Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            M1Tuning tuning = EnsureTuningAsset();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            BuildGround();
            BuildArenaCover();
            Transform dummy = BuildDummy(new Vector3(0f, 0f, 10f));

            BuildPlayerBody(tuning, dummy);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[M1] Built {ScenePath}");
        }

        static void BuildLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
        }

        static void BuildArenaCover()
        {
            var root = new GameObject("Arena").transform;

            CreateBox(root, "Cover_A", new Vector3(4f, 0.75f, 6f), new Vector3(1.5f, 1.5f, 4f));
            CreateBox(root, "Cover_B", new Vector3(-4f, 0.75f, 3f), new Vector3(4f, 1.5f, 1.5f));
            CreateBox(root, "Steps_Low", new Vector3(-7f, 0.4f, 8f), new Vector3(3f, 0.8f, 3f));
            CreateBox(root, "Platform", new Vector3(-7f, 1.4f, 11f), new Vector3(4f, 3f, 4f));
            CreateBox(root, "Cover_C", new Vector3(7f, 1f, 1f), new Vector3(1f, 2f, 5f));
        }

        static void CreateBox(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
        }

        static Transform BuildDummy(Vector3 position)
        {
            var go = new GameObject("Dummy");
            go.transform.position = position;

            var damageable = go.AddComponent<Damageable>();
            damageable.MaxHealth = 100f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            var bodyRegion = body.AddComponent<HitboxRegion>();
            bodyRegion.region = HitboxRegion.Region.Body;
            bodyRegion.owner = damageable;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
            head.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
            var headRegion = head.AddComponent<HitboxRegion>();
            headRegion.region = HitboxRegion.Region.Head;
            headRegion.owner = damageable;

            go.AddComponent<DummyTarget>();
            return go.transform;
        }

        static void BuildPlayerBody(M1Tuning tuning, Transform dummy)
        {
            var body = new GameObject("SharedBody");

            var controller = body.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            var health = body.AddComponent<SharedBodyHealth>();
            health.MaxHealth = tuning.maxHealth;

            var bodyRegion = body.AddComponent<HitboxRegion>();
            bodyRegion.region = HitboxRegion.Region.Body;
            bodyRegion.owner = health;

            var motor = body.AddComponent<P1Motor>();
            var look = body.AddComponent<P1LookController>();
            var aim = body.AddComponent<P2AimRig>();
            var weapon = body.AddComponent<WeaponController>();
            var p1Device = body.AddComponent<M1P1DeviceInputSource>();
            var p2Device = body.AddComponent<M1P2DeviceInputSource>();
            var scripted = body.AddComponent<M1ScriptedInputSource>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "P1_BodyVisual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(body.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "P1_Head";
            head.transform.SetParent(body.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.transform.localScale = new Vector3(0.34f, 0.34f, 0.34f);
            var headRegion = head.AddComponent<HitboxRegion>();
            headRegion.region = HitboxRegion.Region.Head;
            headRegion.owner = health;

            var anchor = new GameObject("P2_ShoulderAnchor");
            anchor.transform.SetParent(body.transform, false);
            anchor.transform.localPosition = tuning.p2ShoulderOffset;

            var p1CameraGo = new GameObject("P1_ThirdPersonCamera");
            var p1Cam = p1CameraGo.AddComponent<Camera>();
            p1Cam.rect = new Rect(0f, 0f, 0.5f, 1f);
            p1Cam.nearClipPlane = 0.1f;
            p1CameraGo.AddComponent<AudioListener>();
            var p1Camera = p1CameraGo.AddComponent<M1P1Camera>();
            p1Camera.target = body.transform;
            p1Camera.look = look;
            p1Camera.distance = tuning.p1CameraDistance;
            p1Camera.height = tuning.p1CameraHeight;

            var p2CameraGo = new GameObject("P2_FirstPersonCamera");
            var p2Cam = p2CameraGo.AddComponent<Camera>();
            p2Cam.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            p2Cam.nearClipPlane = 0.05f;
            var p2Camera = p2CameraGo.AddComponent<M1P2Camera>();
            p2Camera.eye = anchor.transform;
            p2Camera.aim = aim;

            p1Device.tuning = tuning;
            p2Device.tuning = tuning;
            p2Device.weapon = weapon;

            scripted.aimTarget = dummy;
            scripted.aim = aim;
            scripted.weapon = weapon;
            scripted.eye = anchor.transform;

            var root = body.AddComponent<M1BodyRoot>();
            root.tuning = tuning;
            root.look = look;
            root.motor = motor;
            root.aim = aim;
            root.weapon = weapon;
            root.health = health;
            root.aimEye = anchor.transform;
            root.p1Camera = p1Camera;
            root.inputMode = M1InputMode.Device;
            root.p1DeviceInputBehaviour = p1Device;
            root.p2DeviceInputBehaviour = p2Device;
            root.scriptedInputBehaviour = scripted;

            var hudGo = new GameObject("M1_Hud");
            var hud = hudGo.AddComponent<M1Hud>();
            hud.motor = motor;
            hud.look = look;
            hud.aim = aim;
            hud.weapon = weapon;
            hud.health = health;
            hud.SectorHalfDegrees = tuning.sectorHalfDegrees;
        }

        static M1Tuning EnsureTuningAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<M1Tuning>(TuningPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(TuningPath));
            var tuning = ScriptableObject.CreateInstance<M1Tuning>();

            tuning.weapons = new[]
            {
                new WeaponDefinition { displayName = "Rifle", kind = WeaponKind.Rifle, damage = 18f, headshotMultiplier = 2.5f,
                    roundsPerMinute = 480f, magazine = 30, reloadSeconds = 2.2f, baseSpreadDegrees = 1.2f, recoilPerShot = 0.35f, swapSeconds = 0.5f },
                new WeaponDefinition { displayName = "Pistol", kind = WeaponKind.Pistol, damage = 24f, headshotMultiplier = 2.5f,
                    roundsPerMinute = 300f, magazine = 12, reloadSeconds = 1.6f, baseSpreadDegrees = 1.0f, recoilPerShot = 0.5f, swapSeconds = 0.35f },
                new WeaponDefinition { displayName = "Knife", kind = WeaponKind.Knife, damage = 40f, headshotMultiplier = 1f,
                    roundsPerMinute = 120f, magazine = 1, reloadSeconds = 0.1f, baseSpreadDegrees = 0f, recoilPerShot = 0f, swapSeconds = 0.3f, meleeRangeMeters = 2.2f }
            };
            tuning.startingWeaponIndex = 0;
            tuning.accuracyByState = new[]
            {
                new AccuracyRow { state = MovementState.Idle, spreadMultiplier = 1f },
                new AccuracyRow { state = MovementState.Walk, spreadMultiplier = 1.6f },
                new AccuracyRow { state = MovementState.Sprint, spreadMultiplier = 3.0f },
                new AccuracyRow { state = MovementState.Jump, spreadMultiplier = 4.0f },
                new AccuracyRow { state = MovementState.Dodge, spreadMultiplier = 4.0f },
                new AccuracyRow { state = MovementState.Slide, spreadMultiplier = 4.0f },
                new AccuracyRow { state = MovementState.Vault, spreadMultiplier = 4.5f },
                new AccuracyRow { state = MovementState.KickLight, spreadMultiplier = 3.5f },
                new AccuracyRow { state = MovementState.KickHeavy, spreadMultiplier = 5.0f }
            };

            AssetDatabase.CreateAsset(tuning, TuningPath);
            AssetDatabase.SaveAssets();
            return tuning;
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
