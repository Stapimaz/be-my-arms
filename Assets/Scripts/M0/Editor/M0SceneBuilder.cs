using System.Collections.Generic;
using System.IO;
using BeMyArms.M0;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.M0.EditorTools
{
    /// <summary>
    /// Builds the disposable M0 greybox scene from code so the spike scene is reproducible
    /// and never hand-authored. Run from the menu or headless:
    ///   Unity -batchmode -quit -projectPath &lt;project&gt; -executeMethod BeMyArms.M0.EditorTools.M0SceneBuilder.Build
    /// </summary>
    public static class M0SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/M0SharedBody.unity";
        const string TuningPath = "Assets/Scripts/M0/Data/M0Tuning.asset";
        const string MaterialDir = "Assets/Scripts/M0/Data/Materials";
        const string BodyLayerName = "PlayerBody";

        [MenuItem("Be My Arms/M0/Build M0 Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            M0Tuning tuning = EnsureTuningAsset();
            int bodyLayer = EnsureLayer(BodyLayerName);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildEnvironment();
            BuildDummies(bodyLayer);

            BuildPlayerBody(tuning, bodyLayer, out M0BodyRoot root, out Transform shoulderAnchor,
                out P1ThirdPersonCamera p1Camera, out Camera p2Camera);

            BuildHud(root, tuning);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[M0] Built {ScenePath}");
        }

        static void BuildLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static void BuildEnvironment()
        {
            var root = new GameObject("Environment");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            ground.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial("M0_Ground", new Color(0.30f, 0.30f, 0.33f));

            CreateCover(root.transform, "Cover_Center", new Vector3(0f, 0.75f, 8f), new Vector3(4f, 1.5f, 1f));
            CreateCover(root.transform, "Cover_Left", new Vector3(-7f, 0.75f, 3f), new Vector3(1f, 1.5f, 4f));
            CreateCover(root.transform, "Cover_Right", new Vector3(7f, 1f, -2f), new Vector3(1f, 2f, 5f));
        }

        static void CreateCover(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial("M0_Cover", new Color(0.42f, 0.42f, 0.47f));
        }

        static void BuildDummies(int bodyLayer)
        {
            var root = new GameObject("Dummies");

            // Faces +Z at start (BodyYaw = 0). Sector is +/-70 degrees.
            CreateDummy(root.transform, "Dummy_Inside", Polar(25f, 12f), HitboxRegion.Region.Body);
            // 110 degrees out: 40 degrees beyond the sector, so P1 must rotate to expose it.
            CreateDummy(root.transform, "Dummy_NeedsRotation", Polar(110f, 12f), HitboxRegion.Region.Body);
            CreateDummy(root.transform, "Dummy_BehindCover", Polar(-40f, 15f), HitboxRegion.Region.Body);
        }

        static void CreateDummy(Transform parent, string name, Vector3 position, HitboxRegion.Region region)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            var damageable = go.AddComponent<Damageable>();
            damageable.MaxHealth = 100f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial("M0_DummyBody", new Color(0.72f, 0.52f, 0.22f));
            var bodyRegion = body.AddComponent<HitboxRegion>();
            bodyRegion.region = HitboxRegion.Region.Body;
            bodyRegion.owner = damageable;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
            head.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
            head.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial("M0_DummyHead", new Color(0.85f, 0.22f, 0.22f));
            var headRegion = head.AddComponent<HitboxRegion>();
            headRegion.region = HitboxRegion.Region.Head;
            headRegion.owner = damageable;

            go.AddComponent<DummyTarget>();
        }

        static void BuildPlayerBody(M0Tuning tuning, int bodyLayer, out M0BodyRoot root,
            out Transform shoulderAnchor, out P1ThirdPersonCamera p1Camera, out Camera p2Camera)
        {
            var body = new GameObject("SharedBody");
            body.layer = bodyLayer;

            var controller = body.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            var health = body.AddComponent<SharedBodyHealth>();
            health.MaxHealth = tuning.maxHealth;

            var bodyRegion = body.AddComponent<HitboxRegion>();
            bodyRegion.region = HitboxRegion.Region.Body;
            bodyRegion.owner = health;

            var bodyController = body.AddComponent<SharedBodyController>();
            var aimController = body.AddComponent<P2AimController>();
            var weapon = body.AddComponent<HitscanWeapon>();
            var p1Input = body.AddComponent<P1DeviceInputSource>();
            var p2Gamepad = body.AddComponent<P2GamepadInputSource>();
            var p2Scripted = body.AddComponent<P2ScriptedInputSource>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "P1_BodyVisual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(body.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            visual.layer = bodyLayer;
            visual.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial("M0_P1Body", new Color(0.25f, 0.48f, 0.85f));

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "P1_Head";
            head.transform.SetParent(body.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.transform.localScale = new Vector3(0.34f, 0.34f, 0.34f);
            head.layer = bodyLayer;
            head.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial("M0_P1Head", new Color(0.35f, 0.62f, 0.95f));
            var headRegion = head.AddComponent<HitboxRegion>();
            headRegion.region = HitboxRegion.Region.Head;
            headRegion.owner = health;

            var anchor = new GameObject("P2_ShoulderAnchor");
            anchor.transform.SetParent(body.transform, false);
            anchor.transform.localPosition = tuning.p2ShoulderOffset;
            shoulderAnchor = anchor.transform;

            var armProxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            armProxy.name = "P2_ArmProxy";
            Object.DestroyImmediate(armProxy.GetComponent<Collider>());
            armProxy.transform.SetParent(body.transform, false);
            armProxy.transform.localPosition = new Vector3(0.28f, 1.25f, 0.3f);
            armProxy.transform.localScale = new Vector3(0.85f, 0.16f, 0.16f);
            armProxy.layer = bodyLayer;
            armProxy.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial("M0_P2Arm", new Color(0.85f, 0.75f, 0.25f));

            // Cameras: left viewport = P1 third person, right viewport = P2 first person.
            var p1CameraGo = new GameObject("P1_ThirdPersonCamera");
            var p1Cam = p1CameraGo.AddComponent<Camera>();
            p1Cam.rect = new Rect(0f, 0f, 0.5f, 1f);
            p1Cam.nearClipPlane = 0.1f;
            p1CameraGo.AddComponent<AudioListener>();
            p1Camera = p1CameraGo.AddComponent<P1ThirdPersonCamera>();
            p1Camera.target = body.transform;
            p1Camera.distance = tuning.p1CameraDistance;
            p1Camera.height = tuning.p1CameraHeight;

            var p2CameraGo = new GameObject("P2_FirstPersonCamera");
            p2Camera = p2CameraGo.AddComponent<Camera>();
            p2Camera.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            p2Camera.nearClipPlane = 0.05f;
            // Hide the shared body from P2's own view. M0 has no real rig, so nothing to show.
            p2Camera.cullingMask = ~(1 << bodyLayer);
            var p2CameraComponent = p2CameraGo.AddComponent<P2FirstPersonCamera>();
            p2CameraComponent.eye = anchor.transform;
            p2CameraComponent.aim = aimController;

            p2Scripted.aimTarget = GameObject.Find("Dummy_NeedsRotation").transform;
            p2Scripted.eye = anchor.transform;
            p2Scripted.aim = aimController;

            root = body.AddComponent<M0BodyRoot>();
            root.tuning = tuning;
            root.body = bodyController;
            root.aim = aimController;
            root.weapon = weapon;
            root.health = health;
            root.aimEye = anchor.transform;
            root.p1Camera = p1Camera;
            root.p2InputMode = P2InputMode.Gamepad;
            root.p1InputBehaviour = p1Input;
            root.p2DeviceInputBehaviour = p2Gamepad;
            root.p2ScriptedInputBehaviour = p2Scripted;

            p1Input.tuning = tuning;
            p2Gamepad.tuning = tuning;
        }

        static void BuildHud(M0BodyRoot root, M0Tuning tuning)
        {
            var hudGo = new GameObject("M0_DebugHud");
            var hud = hudGo.AddComponent<M0DebugHud>();
            hud.body = root.body;
            hud.aim = root.aim;
            hud.health = root.health;
            hud.SectorHalfDegrees = tuning.sectorHalfDegrees;
        }

        static Vector3 Polar(float yawDegrees, float distance)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(yaw) * distance, 0f, Mathf.Cos(yaw) * distance);
        }

        static M0Tuning EnsureTuningAsset()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<M0Tuning>(TuningPath);
            if (tuning != null) return tuning;

            Directory.CreateDirectory(Path.GetDirectoryName(TuningPath));
            tuning = ScriptableObject.CreateInstance<M0Tuning>();
            AssetDatabase.CreateAsset(tuning, TuningPath);
            AssetDatabase.SaveAssets();
            return tuning;
        }

        static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Directory.CreateDirectory(MaterialDir);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static int EnsureLayer(string layerName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return i;
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }

            Debug.LogWarning($"[M0] No free layer for '{layerName}'; using Default.");
            return 0;
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
