using System.Collections.Generic;
using System.IO;
using BeMyArms.M5;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.M6.EditorTools
{
    /// <summary>
    /// Builds a production showcase scene: the shared-body rig wearing one P1 skin and one P2 skin,
    /// the rifle at the weapon anchor, and the environment kit placed around a greybox floor. This
    /// is the "assets in the project" evidence for M6 — the same prefabs the tests validate.
    /// </summary>
    public static class M6ShowcaseSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/M6ProductionShowcase.unity";

        [MenuItem("Be My Arms/M6/Build Production Showcase Scene")]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static void Build()
        {
            // Make sure the prefabs exist and are current.
            M6PipelineCommands.Regenerate();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            BuildGround();

            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(M6AssetConventions.RigPrefabPath);
            if (rigPrefab == null) throw new FileNotFoundException("rig prefab missing: " + M6AssetConventions.RigPrefabPath);

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.name = M6RigLayout.RootName;

            MountFirst(rig, "Cosmetic_P1", M6AssetConventions.P1Dir);
            MountFirst(rig, "Cosmetic_P2", M6AssetConventions.P2Dir);
            MountFirst(rig, "WeaponAnchor", M6AssetConventions.WeaponDir);

            M5RigValidationReport report = M5RigValidator.Validate(rig, M5RigContract.Default());
            if (!report.IsValid) throw new System.InvalidOperationException("showcase rig invalid: " + report);

            PlaceEnvironment();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[M6] built {ScenePath}");
        }

        static void MountFirst(GameObject rig, string socketName, string prefabDir)
        {
            Transform socket = Find(rig.transform, socketName);
            if (socket == null) throw new System.InvalidOperationException("missing socket " + socketName);

            string folder = $"{prefabDir}/{M6AssetConventions.PrefabSubdir}";
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            if (guids.Length == 0) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(socket, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        static void PlaceEnvironment()
        {
            Place(M6AssetConventions.EnvironmentDir, "BMA_Env_Wall", new Vector3(0f, 0f, 9f), new Vector3(0f, 180f, 0f));
            Place(M6AssetConventions.EnvironmentDir, "BMA_Env_Wall", new Vector3(-9f, 0f, 0f), new Vector3(0f, 90f, 0f));
            Place(M6AssetConventions.EnvironmentDir, "BMA_Env_Crate", new Vector3(2.5f, 0f, 2.5f), Vector3.zero);
            Place(M6AssetConventions.EnvironmentDir, "BMA_Env_Crate", new Vector3(3.4f, 0f, 1.6f), new Vector3(0f, 35f, 0f));
            Place(M6AssetConventions.EnvironmentDir, "BMA_Env_Platform", new Vector3(-4f, 0f, -2f), new Vector3(0f, 30f, 0f));
            Place(M6AssetConventions.EnvironmentDir, "BMA_Env_Pillar", new Vector3(5f, 0f, -4f), Vector3.zero);
        }

        static void Place(string dir, string name, Vector3 position, Vector3 rotation)
        {
            string path = $"{dir}/{M6AssetConventions.PrefabSubdir}/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(rotation);
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
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
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
