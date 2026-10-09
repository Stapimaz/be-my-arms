using System;
using System.Collections.Generic;
using System.Linq;
using BeMyArms.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.Client.EditorTools
{
    /// <summary>First playable blockout only, not an art-style generator. Run through the connected
    /// Editor. Copies the existing match wiring; refuses to overwrite a subsequently authored map.</summary>
    public static class BoatyardSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Boatyard.unity";
        public const string RecordPath = "Assets/Art/Maps/Boatyard.asset";
        const string AssetDir = "Assets/Art/Maps/BoatyardBlockout";

        [MenuItem("Be My Arms/Client/Create First Boatyard Blockout")]
        public static void CreateFirstBlockout()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before authoring.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null || AssetDatabase.LoadAssetAtPath<MapDefinition>(RecordPath) != null)
                throw new InvalidOperationException("Boatyard already exists. Edit the authored scene; do not regenerate over human work.");
            if (!AssetDatabase.IsValidFolder(AssetDir)) AssetDatabase.CreateFolder("Assets/Art/Maps", "BoatyardBlockout");
            var previous = SceneManager.GetActiveScene();
            if (!AssetDatabase.CopyAsset("Assets/Scenes/DuelArena.unity", ScenePath)) throw new InvalidOperationException("Cannot copy Duel scene wiring.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var oldArena = scene.GetRootGameObjects().Single(g => g.name == "Arena");
                UnityEngine.Object.DestroyImmediate(oldArena); // Only the newly copied scene's prototype geometry.
                var arena = new GameObject("Arena").transform;
                var ground = Material("Ground", new Color(.40f, .42f, .43f));
                var structure = Material("Structure", new Color(.64f, .65f, .65f));
                var equipment = Material("Equipment", new Color(.44f, .47f, .49f));
                var railing = Material("Railing", new Color(.54f, .56f, .57f));

                // Functional monochrome blockout: the venue comes from massing, not an art palette.
                Box(arena, "BMA_Map_Floor_Quay", new Vector3(0, -.15f, 0), new Vector3(40, .3f, 40), ground);
                Box(arena, "BMA_Map_Platform_YardFoundation", new Vector3(-1, .6f, 2), new Vector3(26, 1.2f, 14), ground);
                Box(arena, "BMA_Map_Platform_WorkshopFoundation", new Vector3(-3, 1.2f, 14), new Vector3(18, 2.4f, 10), ground);
                Box(arena, "BMA_Map_Platform_LoadingDock", new Vector3(-1, 1.2f, 7.5f), new Vector3(10, 2.4f, 3), ground);
                Box(arena, "BMA_Map_Platform_MaintenanceWalk", new Vector3(14.5f, .6f, 5.5f), new Vector3(5, 1.2f, 21), ground);
                Ramp(arena, "QuayToYard", new Vector3(-6, 0, -8), 6, 6, 1.2f, 180, ground);
                Ramp(arena, "YardToLoading", new Vector3(-4, 1.2f, 3.5f), 4, 5, 1.2f, 180, ground);
                Ramp(arena, "QuayToMaintenance", new Vector3(14.5f, 0, -7.5f), 5, 5, 1.2f, 180, ground);
                Ramp(arena, "WorkshopSideAccess", new Vector3(9, 1.2f, 13.5f), 3, 6, 1.2f, 90, ground);

                // Workshop shell. Wide yard door and offset side door, not a narrow corridor.
                Box(arena, "BMA_Map_Solid_WorkshopBack", new Vector3(-3, 4.4f, 19), new Vector3(18, 4, .5f), structure);
                Box(arena, "BMA_Map_Solid_WorkshopWest", new Vector3(-12, 4.4f, 14), new Vector3(.5f, 4, 10), structure);
                Box(arena, "BMA_Map_Solid_WorkshopFrontLeft", new Vector3(-10, 4.4f, 9), new Vector3(4, 4, .5f), structure);
                Box(arena, "BMA_Map_Solid_WorkshopFrontRight", new Vector3(1.5f, 4.4f, 9), new Vector3(9, 4, .5f), structure);
                Box(arena, "BMA_Map_Solid_WorkshopDoorHeader", new Vector3(-5.5f, 6.1f, 9), new Vector3(5, .6f, .5f), structure);
                Box(arena, "BMA_Map_Solid_WorkshopEastSouth", new Vector3(6, 4.4f, 10.5f), new Vector3(.5f, 4, 3), structure);
                Box(arena, "BMA_Map_Solid_WorkshopEastNorth", new Vector3(6, 4.4f, 17), new Vector3(.5f, 4, 4), structure);
                Box(arena, "BMA_Map_Solid_WorkshopRoof", new Vector3(-3, 6.55f, 14), new Vector3(18.5f, .3f, 10.5f), structure);
                Box(arena, "BMA_Map_Cover_EngineBench", new Vector3(-5, 3.1f, 13), new Vector3(4, 1.4f, 2.4f), equipment);
                Box(arena, "BMA_Map_Cover_WorkshopColumn", new Vector3(1.5f, 4, 15.5f), new Vector3(1, 3.2f, 1), structure);

                // Each obstacle has a venue function. No grid of interchangeable combat crates.
                Box(arena, "BMA_Map_Cover_CranePedestal", new Vector3(-1, 2.25f, 0), new Vector3(2.2f, 2.1f, 2.2f), equipment);
                Box(arena, "BMA_Map_Solid_CraneMast", new Vector3(-1, 4.4f, 0), new Vector3(.7f, 2.2f, .7f), equipment);
                Box(arena, "BMA_Map_Solid_CraneArm", new Vector3(-3, 5.4f, 0), new Vector3(4.5f, .35f, .7f), equipment);
                Box(arena, "BMA_Map_Cover_EngineCarriage", new Vector3(-9, 1.85f, -1), new Vector3(4, 1.3f, 1.6f), equipment);
                Box(arena, "BMA_Map_Cover_SpareMotorRack", new Vector3(7, 2.4f, 4), new Vector3(2.2f, 2.4f, 3), equipment);
                Box(arena, "BMA_Map_Cover_LoadingLip", new Vector3(2, 3.05f, 6.2f), new Vector3(4, 1.3f, .45f), railing);
                Box(arena, "BMA_Map_Cover_DryDockHull", new Vector3(3, 1.2f, -11.5f), new Vector3(5, 2.4f, 5), equipment);
                Box(arena, "BMA_Map_Cover_QuayWinch", new Vector3(-11.5f, .65f, -14), new Vector3(2, 1.3f, 2), equipment);
                Box(arena, "BMA_Map_Solid_MaintenanceDivider", new Vector3(11.5f, 2.7f, 4.5f), new Vector3(.6f, 3, 9), structure);
                // 1.30 m headroom over the 1.20 m walk: crouch/slide passes, standing does not.
                Box(arena, "BMA_Map_Solid_ServicePipe", new Vector3(14.5f, 2.675f, 1), new Vector3(5, .35f, 1.6f), equipment);

                // Physical boundary, no drowning/traversal rules or decorative collision impostors.
                Box(arena, "BMA_Map_Solid_CliffBoundary", new Vector3(-18, 2.5f, 0), new Vector3(4, 5, 40), structure);
                Box(arena, "BMA_Map_Solid_SeaBoundary", new Vector3(18.5f, 2.5f, 0), new Vector3(3, 5, 40), structure);
                Box(arena, "BMA_Map_Solid_ServiceBoundary", new Vector3(0, 2.5f, 19.75f), new Vector3(40, 5, .5f), structure);
                Box(arena, "BMA_Map_Solid_QuayBoundary", new Vector3(0, 1.25f, -19.75f), new Vector3(40, 2.5f, .5f), railing);

                var map = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapSpawns>()).Single();
                map.BoundsSize = new Vector3(40, 8, 40); map.AlternateDuelSides = true;
                map.Spawns.Clear(); map.Obstacles.Clear();
                AddSpawn(map, 0, new Vector3(-8, 2.4f, 16), 180);
                AddSpawn(map, 1, new Vector3(10, 0, -16), 0);
                var record = ScriptableObject.CreateInstance<MapDefinition>();
                record.MapId = "boatyard"; record.Family = MapFamily.Duel; record.ScenePath = ScenePath;
                record.BoundsSize = map.BoundsSize; record.LaneCount = 2; record.MaxVerticality = 2.4f;
                record.CoverCount = arena.Cast<Transform>().Count(t => t.name.StartsWith("BMA_Map_Cover"));
                foreach (var s in map.Spawns)
                    record.Spawns.Add(new MapSpawn { Team = s.Team, Body = s.Body, Role = s.Role, Position = s.Position, Yaw = s.Yaw });
                record.MinSpawnClearance = Clearance(arena, map.Spawns);
                AssetDatabase.CreateAsset(record, RecordPath);
                scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapSceneLink>()).Single().Map = record;
                EditorUtility.SetDirty(map); EditorUtility.SetDirty(record);
                EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                var scenes = EditorBuildSettings.scenes.ToList();
                if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Blockout_" + name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .15f);
            AssetDatabase.CreateAsset(material, $"{AssetDir}/{name}.mat"); return material;
        }

        static void Box(Transform root, string name, Vector3 center, Vector3 size, Material material)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube); piece.name = name;
            piece.transform.SetParent(root, false); piece.transform.localPosition = center; piece.transform.localScale = size;
            piece.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void Ramp(Transform root, string name, Vector3 origin, float width, float length, float height, float yaw, Material material)
        {
            float x = width / 2, z = length / 2;
            var mesh = new Mesh { name = name, vertices = new[] {
                new Vector3(-x, 0, z), new Vector3(x, 0, z), new Vector3(-x, 0, -z), new Vector3(x, 0, -z),
                new Vector3(-x, height, -z), new Vector3(x, height, -z) },
                triangles = new[] { 0,1,4, 1,5,4, 0,4,2, 1,3,5, 2,5,3, 2,4,5, 0,2,1, 1,2,3 } };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, $"{AssetDir}/{name}.asset");
            var piece = new GameObject("BMA_Map_Ramp_" + name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            piece.transform.SetParent(root, false); piece.transform.localPosition = origin; piece.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            piece.GetComponent<MeshFilter>().sharedMesh = mesh; piece.GetComponent<MeshRenderer>().sharedMaterial = material;
            piece.GetComponent<MeshCollider>().sharedMesh = mesh;
        }

        static void AddSpawn(MapSpawns map, int team, Vector3 point, float yaw)
        {
            for (int role = 0; role < 2; role++) map.Spawns.Add(new BodySpawn { Team = team, Role = role, Position = point, Yaw = yaw });
        }

        static float Clearance(Transform root, List<BodySpawn> spawns)
        {
            float result = float.MaxValue;
            foreach (var spawn in spawns)
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var b = renderer.bounds;
                    if (b.max.y <= spawn.Position.y + .55f || b.min.y >= spawn.Position.y + 1.8f) continue;
                    float dx = Mathf.Max(0, Mathf.Max(b.min.x - spawn.Position.x, spawn.Position.x - b.max.x));
                    float dz = Mathf.Max(0, Mathf.Max(b.min.z - spawn.Position.z, spawn.Position.z - b.max.z));
                    result = Mathf.Min(result, Mathf.Sqrt(dx * dx + dz * dz));
                }
            return result;
        }
    }
}
