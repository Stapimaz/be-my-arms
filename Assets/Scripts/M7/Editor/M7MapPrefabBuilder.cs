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

                RemapMaterials(root);

                string path = $"{PrefabDir}/{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                built.Add(path);
            }

            AssetDatabase.SaveAssets();
            return built;
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
    }
}
