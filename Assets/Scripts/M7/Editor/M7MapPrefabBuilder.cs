using System.Collections.Generic;
using System.IO;
using BeMyArms.M6.EditorTools;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>Wraps the Blender map-kit FBX in prefabs with the shared URP material palette.</summary>
    public static class M7MapPrefabBuilder
    {
        public const string ModelDir = "Assets/Art/MapKit";
        public const string PrefabDir = "Assets/Art/MapKit/Prefabs";

        public static List<string> BuildAll()
        {
            var built = new List<string>();
            if (!AssetDatabase.IsValidFolder(ModelDir)) return built;
            Directory.CreateDirectory(PrefabDir);

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                string modelPath = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(modelPath);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null) continue;

                var root = new GameObject(name);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = name;
                instance.transform.SetParent(root.transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                ApplyPieceMetrics(name, root);
                RemapMaterials(root);
                AddCameraColliders(root);

                string path = $"{PrefabDir}/{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                built.Add(path);
            }

            AssetDatabase.SaveAssets();
            return built;
        }

        /// <summary>
        /// Piece-specific metrics. Low cover is normalised to a height that sits above the crouched
        /// body top (M2BodySim CrouchHeight 1.15 m) yet below the standing eye (StandEyeHeight
        /// 1.45 m): crouching then genuinely hides the body while standing can still shoot over it.
        /// Applied to the shared prefab, so every arena placement stays symmetrical.
        /// </summary>
        static void ApplyPieceMetrics(string name, GameObject root)
        {
            if (!string.Equals(name, "BMA_Map_Cover_Low", System.StringComparison.Ordinal)) return;
            const float TargetHeight = 1.30f;
            float height = RendererBounds(root).size.y;
            if (height < 0.01f) return;
            root.transform.localScale = new Vector3(1f, TargetHeight / height, 1f);
        }

        static Bounds RendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        static void RemapMaterials(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool dirty = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    Material palette = M6MaterialPalette.GetOrCreate(materials[i].name);
                    if (palette != null && palette != materials[i]) { materials[i] = palette; dirty = true; }
                }
                if (dirty) renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// Gives every map piece a box collider matching its renderer bounds, on the Default layer.
        /// The M7 camera's Cinemachine deoccluder raycasts against Default only, so these become the
        /// camera's collision geometry without touching the shared-body hitboxes (Ignore Raycast) or
        /// the deterministic movement collision (which reads renderer/obstacle data separately).
        /// </summary>
        static void AddCameraColliders(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<Collider>() != null) continue;
                var box = renderer.gameObject.AddComponent<BoxCollider>();
                Bounds local = renderer.localBounds;
                box.center = local.center;
                box.size = local.size;
                box.isTrigger = false;
            }
        }
    }
}
