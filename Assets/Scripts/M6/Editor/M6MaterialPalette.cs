using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M6.EditorTools
{
    /// <summary>
    /// The shared material palette. Blender exports material *names*; Unity owns the actual URP
    /// material assets so the look can be tuned without re-exporting geometry. Names are the
    /// contract between the two halves of the pipeline.
    /// </summary>
    public static class M6MaterialPalette
    {
        struct Entry
        {
            public Color BaseColor;
            public float Metallic;
            public float Smoothness;

            public Entry(Color baseColor, float metallic, float smoothness)
            {
                BaseColor = baseColor;
                Metallic = metallic;
                Smoothness = smoothness;
            }
        }

        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>
        {
            { "BMA_Armor", new Entry(new Color(0.11f, 0.12f, 0.14f), 0.25f, 0.45f) },
            { "BMA_Suit_Alpha", new Entry(new Color(0.18f, 0.42f, 0.52f), 0.00f, 0.35f) },
            { "BMA_Suit_Beta", new Entry(new Color(0.60f, 0.29f, 0.13f), 0.00f, 0.35f) },
            { "BMA_Accent_Cool", new Entry(new Color(0.15f, 0.75f, 0.85f), 0.30f, 0.60f) },
            { "BMA_Accent_Warm", new Entry(new Color(0.85f, 0.45f, 0.15f), 0.30f, 0.60f) },
            { "BMA_Visor", new Entry(new Color(0.05f, 0.25f, 0.40f), 0.60f, 0.85f) },
            { "BMA_Weapon_Metal", new Entry(new Color(0.14f, 0.15f, 0.17f), 0.75f, 0.65f) },
            { "BMA_Grip", new Entry(new Color(0.08f, 0.09f, 0.10f), 0.10f, 0.25f) },
            { "BMA_Concrete", new Entry(new Color(0.42f, 0.44f, 0.47f), 0.00f, 0.15f) },
            { "BMA_Env_Metal", new Entry(new Color(0.20f, 0.22f, 0.25f), 0.55f, 0.55f) },
            { "BMA_Env_Accent", new Entry(new Color(0.85f, 0.55f, 0.20f), 0.20f, 0.50f) },
        };

        public static IEnumerable<string> Names => Entries.Keys;

        public static Material GetOrCreate(string name)
        {
            if (string.IsNullOrEmpty(name) || !Entries.TryGetValue(name, out Entry entry)) return null;

            Directory.CreateDirectory(M6AssetConventions.MaterialsDir);
            string path = $"{M6AssetConventions.MaterialsDir}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", entry.BaseColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", entry.BaseColor);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", entry.Metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", entry.Smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void RebuildAll()
        {
            foreach (string name in Entries.Keys) GetOrCreate(name);
            AssetDatabase.SaveAssets();
        }
    }
}
