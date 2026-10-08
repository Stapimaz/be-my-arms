using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.Networking.EditorTools
{
    /// <summary>
    /// Builds the Networking networking-spike scene and its body prefab from code.
    /// Menu: Be My Arms > Networking > Build Networking Scene
    /// </summary>
    public static class PredictionHarnessSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Samples/PredictionHarness.unity";
        const string PrefabPath = "Assets/Scripts/Networking/Samples/PredictionHarness/Body.prefab";

        [MenuItem("Be My Arms/Networking/Build Networking Scene")]
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
            BuildNetworkManager(prefab);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Networking] Built {ScenePath}");
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

        static void BuildNetworkManager(GameObject prefab)
        {
            var go = new GameObject("NetworkManager");
            var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7778, "0.0.0.0");

            var manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.TickRate = 60;
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });

            var bootstrap = go.AddComponent<PredictionHarnessBootstrap>();
            bootstrap.Manager = manager;
            bootstrap.BodyPrefab = prefab;
            bootstrap.Role = PredictionHarnessRole.Host;
        }

        static GameObject BuildBodyPrefab()
        {
            var go = new GameObject("Body");
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>();

            var body = go.AddComponent<PredictionHarnessBody>();
            var predictor = go.AddComponent<PredictionHarnessClient>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

            predictor.Body = body;
            predictor.Presentation = visual.transform;

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
