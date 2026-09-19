using System.Collections.Generic;
using System.IO;
using BeMyArms.M4;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.M3.EditorTools
{
    /// <summary>
    /// Builds the M3 Duel greybox scene and its body/director prefabs from code.
    /// Menu: Be My Arms &gt; M3 &gt; Build M3 Scene
    /// </summary>
    public static class M3DuelSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/M3Duel.unity";
        const string BodyPrefabPath = "Assets/Scripts/M3/Net/M3DuelBody.prefab";
        const string DirectorPrefabPath = "Assets/Scripts/M3/Net/M3DuelDirector.prefab";

        [MenuItem("Be My Arms/M3/Build M3 Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            GameObject bodyPrefab = BuildBodyPrefab();
            GameObject directorPrefab = BuildDirectorPrefab(bodyPrefab);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            BuildGround();
            BuildArenaCover();
            BuildNetworkManager(bodyPrefab, directorPrefab);
            BuildPresentation();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[M3] Built {ScenePath}");
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
            CreateBox(root, "Cover_A", new Vector3(4f, 0.75f, 0f), new Vector3(2f, 1.5f, 4f));
            CreateBox(root, "Cover_B", new Vector3(-4f, 0.75f, 0f), new Vector3(2f, 1.5f, 4f));
            CreateBox(root, "Cover_Mid", new Vector3(0f, 1f, 0f), new Vector3(3f, 2f, 1.2f));
        }

        static void CreateBox(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
        }

        static void BuildNetworkManager(GameObject bodyPrefab, GameObject directorPrefab)
        {
            var go = new GameObject("NetworkManager");
            var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7779, "0.0.0.0");

            var manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.TickRate = 60;
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = bodyPrefab });
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = directorPrefab });

            var bootstrap = go.AddComponent<M3DuelBootstrap>();
            bootstrap.Manager = manager;
            bootstrap.DirectorPrefab = directorPrefab;
            bootstrap.Role = M3DuelRole.Host;
        }

        static void BuildPresentation()
        {
            var zoneGo = new GameObject("M3_ZoneVisual");
            zoneGo.AddComponent<M3ZoneVisual>();

            var hudGo = new GameObject("M3_DuelHud");
            hudGo.AddComponent<M3DuelHud>();

            // The server-side matchmaking/rating host. Inert unless matchmaker mode is enabled.
            var hostGo = new GameObject("M4_MatchHost");
            hostGo.AddComponent<M4MatchHost>();
        }

        static GameObject BuildBodyPrefab()
        {
            var go = new GameObject("M3DuelBody");
            go.AddComponent<NetworkObject>();
            var body = go.AddComponent<M3DuelBody>();
            var client = go.AddComponent<M3DuelClient>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

            // Simple muzzle/eye anchor for P2; presentation only.
            var anchor = new GameObject("P2_ShoulderAnchor");
            anchor.transform.SetParent(visual.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 0.55f, 0.25f);

            body.name = "M3DuelBody";
            client.Presentation = visual.transform;

            Directory.CreateDirectory(Path.GetDirectoryName(BodyPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, BodyPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject BuildDirectorPrefab(GameObject bodyPrefab)
        {
            var go = new GameObject("M3DuelDirector");
            go.AddComponent<NetworkObject>();
            var director = go.AddComponent<M3DuelDirector>();
            director.BodyPrefab = bodyPrefab;

            Directory.CreateDirectory(Path.GetDirectoryName(DirectorPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, DirectorPrefabPath);
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
