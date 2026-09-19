using System.Collections.Generic;
using System.IO;
using BeMyArms.M7;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>
    /// Composes the production Duel and 2v2 arenas from the Blender map kit, writes a validated
    /// <see cref="M7MapDefinition"/> record for each, and links it into the scene. Both maps share
    /// the theme/kit (concept §17.2 map family): the Duel variant closes the east/west flanks,
    /// while the 2v2 variant opens them.
    /// </summary>
    public static class M7ArenaSceneBuilder
    {
        const string MapPrefabDir = M7MapPrefabBuilder.PrefabDir;
        const string RecordDir = "Assets/Art/Maps";

        public static void BuildAll()
        {
            M7MapPrefabBuilder.BuildAll();
            Build(M7MapFamily.Duel);
            Build(M7MapFamily.TwoVsTwo);
            AssetDatabase.SaveAssets();
        }

        public static void Build(M7MapFamily family)
        {
            bool duel = family == M7MapFamily.Duel;
            int grid = duel ? 6 : 8;
            int half = grid * 2;
            string mapId = duel ? "m7_duel_foundry" : "m7_2v2_foundry";
            string scenePath = duel ? "Assets/Scenes/M7DuelArena.unity" : "Assets/Scenes/M7TwoVsTwoArena.unity";
            string recordPath = $"{RecordDir}/{(duel ? "M7DuelArena" : "M7TwoVsTwoArena")}.asset";

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(0.92f, 0.94f, 1f);
            lightGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var root = new GameObject("Arena").transform;
            int cover = 0;
            int doorways = 0;

            // Floors.
            int tileStart = -(grid - 1) * 2;
            for (int ix = 0; ix < grid; ix++)
            {
                for (int iz = 0; iz < grid; iz++)
                    Place(root, "BMA_Map_Floor4", new Vector3(tileStart + ix * 4f, 0f, tileStart + iz * 4f), 0f);
            }

            // Perimeter walls with deliberate doorway gaps (lanes).
            int[] slots = new int[grid];
            for (int i = 0; i < grid; i++) slots[i] = tileStart + i * 4;
            // Perimeter walls with deliberate central doorways (the map family's lanes).
            foreach (int x in slots)
            {
                if (IsCentralSlot(x)) continue; // replaced by the central doorway below
                Place(root, "BMA_Map_Wall4x3", new Vector3(x, 0f, half), 180f);
                Place(root, "BMA_Map_Wall4x3", new Vector3(x, 0f, -half), 0f);
            }
            Place(root, "BMA_Map_Doorway4", new Vector3(0f, 0f, half), 180f);
            Place(root, "BMA_Map_Doorway4", new Vector3(0f, 0f, -half), 0f);
            doorways += 2;

            foreach (int z in slots)
            {
                if (!duel && IsCentralSlot(z)) continue;
                Place(root, "BMA_Map_Wall4x3", new Vector3(half, 0f, z), 90f);
                Place(root, "BMA_Map_Wall4x3", new Vector3(-half, 0f, z), 270f);
            }
            if (!duel)
            {
                Place(root, "BMA_Map_Doorway4", new Vector3(half, 0f, 0f), 90f);
                Place(root, "BMA_Map_Doorway4", new Vector3(-half, 0f, 0f), 270f);
                doorways += 2;
            }

            // Central landmark: platform + access ramps (verticality).
            cover += Place(root, "BMA_Map_Platform4", Vector3.zero, 0f);
            Place(root, "BMA_Map_Ramp4", new Vector3(0f, 0f, -4.2f), 180f);
            Place(root, "BMA_Map_Ramp4", new Vector3(0f, 0f, 4.2f), 0f);

            // Elevated catwalk along the west flank, reachable from a crate.
            Place(root, "BMA_Map_Catwalk4", new Vector3(-8f, 0f, 0f), 90f);
            cover += Place(root, "BMA_Map_Crate", new Vector3(-8f, 0f, -3.2f), 0f);

            // Symmetrical cover layout.
            int[] coverGap = { 5, 8 };
            foreach (int g in coverGap)
            {
                cover += Place(root, "BMA_Map_Cover_Low", new Vector3(g, 0f, -g), 0f);
                cover += Place(root, "BMA_Map_Cover_Low", new Vector3(-g, 0f, g), 0f);
                cover += Place(root, "BMA_Map_Cover_Low", new Vector3(g, 0f, g), 0f);
                cover += Place(root, "BMA_Map_Cover_Low", new Vector3(-g, 0f, -g), 0f);
            }
            cover += Place(root, "BMA_Map_Cover_High", new Vector3(0f, 0f, -half + 3f), 0f);
            cover += Place(root, "BMA_Map_Cover_High", new Vector3(0f, 0f, half - 3f), 0f);
            cover += Place(root, "BMA_Map_Pillar", new Vector3(half - 4f, 0f, 0f), 0f);
            cover += Place(root, "BMA_Map_Pillar", new Vector3(-half + 4f, 0f, 0f), 0f);

            if (!duel)
            {
                // Extra 2v2 lanes: mid-field cover and a second access route.
                cover += Place(root, "BMA_Map_Cover_Low", new Vector3(half - 6f, 0f, -6f), 0f);
                cover += Place(root, "BMA_Map_Cover_Low", new Vector3(-half + 6f, 0f, 6f), 0f);
                cover += Place(root, "BMA_Map_Cover_Low", new Vector3(half - 6f, 0f, 6f), 0f);
                cover += Place(root, "BMA_Map_Cover_Low", new Vector3(-half + 6f, 0f, -6f), 0f);
                cover += Place(root, "BMA_Map_Crate", new Vector3(3f, 0f, 0f), 0f);
                cover += Place(root, "BMA_Map_Crate", new Vector3(-3f, 0f, 0f), 0f);
            }

            // Spawn pads + spawn points.
            var record = LoadOrCreateRecord(recordPath);
            record.MapId = mapId;
            record.Family = family;
            record.ScenePath = scenePath;
            record.BoundsSize = new Vector3(half * 2, 8f, half * 2);
            record.CoverCount = cover;
            record.LaneCount = doorways + 2;
            record.MaxVerticality = MeasureVerticality(root);
            record.Spawns.Clear();

            int bodies = duel ? 1 : 2;
            for (int team = 0; team < 2; team++)
            {
                for (int body = 0; body < bodies; body++)
                {
                    float lateral = (body - (bodies - 1) * 0.5f) * 8f;
                    float z = team == 0 ? -(half - 3f) : (half - 3f);
                    float yaw = team == 0 ? 0f : 180f;
                    Place(root, team == 0 ? "BMA_Map_SpawnPad_A" : "BMA_Map_SpawnPad_B", new Vector3(lateral, 0f, z), yaw);
                    AddSpawn(record, team, body, 0, new Vector3(lateral - 0.9f, 0f, z), yaw);
                    AddSpawn(record, team, body, 1, new Vector3(lateral + 0.9f, 0f, z), yaw);
                }
            }

            EditorUtility.SetDirty(record);
            AssetDatabase.SaveAssets();

            var info = new GameObject("MapInfo");
            info.AddComponent<M7MapSceneLink>().Map = record;

            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuildSettings(scenePath);
            Debug.Log($"[M7] built {scenePath} (cover={record.CoverCount} lanes={record.LaneCount} verticality={record.MaxVerticality:0.00} spawns={record.Spawns.Count})");
        }

        static void AddSpawn(M7MapDefinition record, int team, int body, int role, Vector3 position, float yaw)
        {
            record.Spawns.Add(new M7Spawn { Team = team, Body = body, Role = role, Position = position, Yaw = yaw });
        }

        static bool IsCentralSlot(int value) => Mathf.Abs(value) == 2;

        static int Place(Transform parent, string piece, Vector3 position, float yaw)
        {
            string path = $"{MapPrefabDir}/{piece}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return 0;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return piece.Contains("Cover") || piece.Contains("Crate") || piece.Contains("Pillar") ? 1 : 0;
        }

        static float MeasureVerticality(Transform root)
        {
            float maxY = 0f;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                maxY = Mathf.Max(maxY, renderer.bounds.max.y);
            return maxY;
        }

        static M7MapDefinition LoadOrCreateRecord(string path)
        {
            var record = AssetDatabase.LoadAssetAtPath<M7MapDefinition>(path);
            if (record != null) return record;
            Directory.CreateDirectory(RecordDir);
            record = ScriptableObject.CreateInstance<M7MapDefinition>();
            AssetDatabase.CreateAsset(record, path);
            return record;
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
