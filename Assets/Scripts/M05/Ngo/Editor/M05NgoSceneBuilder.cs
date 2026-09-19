using System.Collections.Generic;
using System.IO;
using BeMyArms.M0;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.M05.Ngo.EditorTools
{
    /// <summary>
    /// Builds the throwaway M0.5 NGO bake-off scene and its body prefab from code.
    /// Menu: Be My Arms > M0.5 > Build NGO Bake-off Scene
    /// </summary>
    public static class M05NgoSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/M05Ngo.unity";
        const string PrefabPath = "Assets/Scripts/M05/Ngo/NgoBakeoffBody.prefab";

        [MenuItem("Be My Arms/M0.5/Build NGO Bake-off Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            GameObject prefab = BuildBodyPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            BuildGround();
            Transform dummy = BuildDummy();
            BuildNetworkManager(prefab, dummy);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[M05-NGO] Built {ScenePath}");
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

        static Transform BuildDummy()
        {
            var go = new GameObject("Dummy");
            go.transform.localPosition = new Vector3(0f, 0f, 10f);

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

        static void BuildNetworkManager(GameObject prefab, Transform autoAimTarget)
        {
            var go = new GameObject("NetworkManager");
            var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");

            var manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.TickRate = 60;
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });

            var bootstrap = go.AddComponent<NgoBakeoffBootstrap>();
            bootstrap.Manager = manager;
            bootstrap.BodyPrefab = prefab;
            bootstrap.Role = BakeoffRole.Host;
            bootstrap.AutoDrive = true;
            bootstrap.AutoAimTarget = autoAimTarget;
        }

        static GameObject BuildBodyPrefab()
        {
            var go = new GameObject("NgoBakeoffBody");

            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            go.AddComponent<NetworkObject>();

            var eye = new GameObject("Eye");
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.45f, 0.22f);

            var body = go.AddComponent<NgoBakeoffBody>();
            body.Eye = eye.transform;

            go.AddComponent<NetworkTransform>();
            go.AddComponent<P1DeviceInputSource>();
            go.AddComponent<P2GamepadInputSource>();

            var driver = go.AddComponent<NgoBakeoffInputDriver>();
            driver.Body = body;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "P1Body";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
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
