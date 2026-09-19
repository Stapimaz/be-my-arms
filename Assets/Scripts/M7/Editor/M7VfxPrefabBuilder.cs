using System.IO;
using BeMyArms.M7;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>
    /// Builds the M7 VFX prefabs from code (particle systems, no VFX Graph dependency). Prefabs are
    /// production-test effects: readable, short-lived and cheap, ready to be replaced by authored
    /// effects later without changing the runtime API.
    /// </summary>
    public static class M7VfxPrefabBuilder
    {
        public const string PrefabDir = "Assets/Art/Vfx/Prefabs";
        public const string MaterialDir = "Assets/Art/Vfx/Materials";

        public static void BuildAll()
        {
            Material particle = GetOrCreateMaterial();
            Directory.CreateDirectory(PrefabDir);

            Make(M7VfxId.MuzzleFlash, 0.12f, 0.08f, 1.5f, 3.5f, 0.10f, 0.26f, new Color(1f, 0.72f, 0.30f), 0f, 10, 22f, 0.05f, particle);
            Make(M7VfxId.Tracer, 0.2f, 0.14f, 55f, 70f, 0.02f, 0.05f, new Color(0.6f, 0.95f, 1f), 0f, 1, 0f, 0f, particle, stretched: true);
            Make(M7VfxId.ImpactFlesh, 0.3f, 0.35f, 1.5f, 3.5f, 0.06f, 0.16f, new Color(0.7f, 0.12f, 0.12f), 1.2f, 14, 40f, 0.05f, particle);
            Make(M7VfxId.ImpactWorld, 0.3f, 0.3f, 2f, 5f, 0.04f, 0.10f, new Color(0.8f, 0.8f, 0.75f), 1.6f, 16, 45f, 0.05f, particle);
            Make(M7VfxId.FootstepDust, 0.4f, 0.35f, 0.4f, 1.0f, 0.10f, 0.22f, new Color(0.6f, 0.6f, 0.58f), 0.1f, 7, 30f, 0.12f, particle);
            Make(M7VfxId.ReloadSpark, 0.35f, 0.25f, 0.8f, 1.8f, 0.03f, 0.06f, new Color(0.95f, 0.75f, 0.35f), 1.5f, 6, 50f, 0.04f, particle);
            Make(M7VfxId.GrenadeExplosion, 0.7f, 0.55f, 4f, 9f, 0.25f, 0.7f, new Color(1f, 0.55f, 0.20f), 1.0f, 46, 80f, 0.2f, particle);
            Make(M7VfxId.SmokeCloud, 1.4f, 1.2f, 0.3f, 0.7f, 0.8f, 1.8f, new Color(0.55f, 0.55f, 0.58f, 0.7f), -0.05f, 26, 60f, 0.3f, particle);
            Make(M7VfxId.FlashBurst, 0.35f, 0.18f, 0.05f, 0.2f, 1.2f, 2.4f, new Color(1f, 1f, 0.9f), 0f, 2, 10f, 0.05f, particle);
            Make(M7VfxId.EliminationBurst, 0.9f, 0.65f, 2.5f, 6f, 0.12f, 0.30f, new Color(0.2f, 0.8f, 0.95f), -0.4f, 32, 70f, 0.15f, particle);
            Make(M7VfxId.ZoneEdge, 1.2f, 0.9f, 0.8f, 1.8f, 0.20f, 0.45f, new Color(1f, 0.25f, 0.2f), 0f, 14, 360f, 0.4f, particle);

            AssetDatabase.SaveAssets();
        }

        public static string PrefabPath(M7VfxId id) => $"{PrefabDir}/{id}.prefab";

        static void Make(M7VfxId id, float duration, float lifetime, float speedMin, float speedMax,
            float sizeMin, float sizeMax, Color color, float gravity, int burst, float coneAngle, float radius,
            Material material, bool stretched = false)
        {
            var go = new GameObject(id.ToString());
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, Mathf.Max(speedMin, speedMax));
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, Mathf.Max(sizeMin, sizeMax));
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = Mathf.Max(8, burst * 2);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, burst)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = coneAngle >= 180f ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = radius;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.35f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretched)
            {
                renderer.lengthScale = 6f;
                renderer.velocityScale = 0.15f;
            }

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(id));
            Object.DestroyImmediate(go);
        }

        static Material GetOrCreateMaterial()
        {
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/VfxParticle.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Particles/Standard Unlit")
                    ?? Shader.Find("Sprites/Default");
                material = new Material(shader);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
    }
}
