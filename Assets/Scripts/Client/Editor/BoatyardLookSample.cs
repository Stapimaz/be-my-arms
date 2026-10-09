using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeMyArms.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BeMyArms.Client.EditorTools
{
    /// <summary>One-time, live-Editor art slice. Preserves the accepted collision envelope and
    /// does not regenerate the layout. Further art edits are authored normally, not rerun here.</summary>
    public static class BoatyardLookSample
    {
        public const string AssetDir = "Assets/Art/Maps/BoatyardLookSample";
        public const string RootName = "LookSample";

        [MenuItem("Be My Arms/Client/Author Boatyard Look Sample")]
        public static void Author()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play mode and finish compilation before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Review/save open scene changes before authoring.");
            if (AssetDatabase.LoadAssetAtPath<Material>(AssetDir + "/Concrete.mat") != null)
                throw new InvalidOperationException("Look sample already exists; do not regenerate over authored work.");

            // Bake only this scene, never the unrelated additive Editor scenes.
            var scene = EditorSceneManager.OpenScene(BoatyardSceneBuilder.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            if (roots.Any(g => g.name == RootName)) throw new InvalidOperationException("Look sample already authored.");
            var arena = roots.Single(g => g.name == "Arena").transform;
            var map = roots.SelectMany(g => g.GetComponentsInChildren<MapSpawns>()).Single();
            var before = map.BuildCollision();
            var envelopes = arena.Cast<Transform>().ToDictionary(t => t.name, t => t.GetComponent<Renderer>().bounds);
            var sample = new GameObject(RootName).transform;

            ConfigureSourceTextures();
            var concrete = TexturedMaterial("Concrete", "Concrete030", new Color(.74f, .72f, .67f), .22f, .27f);
            var plaster = TexturedMaterial("Plaster", "Plaster001", new Color(.88f, .85f, .77f), .14f, .17f);
            var teal = PlainMaterial("TealPaint", new Color(.16f, .40f, .43f), .05f, .34f);
            var yellow = PlainMaterial("SafetyOchre", new Color(.88f, .58f, .16f), .05f, .30f);
            var steel = PlainMaterial("Steel", new Color(.36f, .42f, .45f), .65f, .38f);
            var rubber = PlainMaterial("DarkRubber", new Color(.12f, .15f, .16f), 0, .20f);

            foreach (Transform t in arena)
            {
                var filter = t.GetComponent<MeshFilter>();
                bool ramp = t.name.StartsWith("BMA_Map_Ramp");
                bool architecture = t.name.StartsWith("BMA_Map_Solid_Workshop") || t.name.Contains("WorkshopFoundation") ||
                    t.name.Contains("Loading") || t.name.Contains("YardFoundation");
                bool equipment = t.name.Contains("EngineBench") || t.name.Contains("EngineCarriage") ||
                    t.name.Contains("Crane") || t.name.Contains("WorkshopColumn");
                if (ramp)
                {
                    // Split geometric faces, not individual triangles. The sloped top stays planar;
                    // hard side/back normals stop a six-vertex wedge looking inflated or warped.
                    var mesh = RampMesh(filter.sharedMesh);
                    mesh.name = t.name + "_HardFaces";
                    AssetDatabase.CreateAsset(mesh, AssetDir + "/" + mesh.name + ".asset");
                    filter.sharedMesh = mesh;
                    t.GetComponent<MeshCollider>().sharedMesh = mesh;
                    if (t.name.Contains("YardToLoading") || t.name.Contains("QuayToYard"))
                        t.GetComponent<Renderer>().sharedMaterial = concrete;
                }
                else if (architecture || equipment)
                {
                    var mesh = BeveledBox(t.localScale, .035f);
                    mesh.name = t.name + "_Surface";
                    AssetDatabase.CreateAsset(mesh, AssetDir + "/" + mesh.name + ".asset");
                    filter.sharedMesh = mesh; // BoxCollider and the numeric collision stay untouched.
                    var material = t.name.Contains("Foundation") || t.name.Contains("LoadingDock") ? concrete : plaster;
                    if (equipment) material = t.name.Contains("Crane") ? yellow : teal;
                    if (t.name.Contains("Roof") || t.name.Contains("LoadingLip")) material = steel;
                    t.GetComponent<Renderer>().sharedMaterial = material;
                }
                else if (filter.sharedMesh.uv2.Length != filter.sharedMesh.vertexCount)
                {
                    // Untextured blockout needs valid UV2 too when used as GI context/occluders.
                    var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                    mesh.name = t.name + "_BakeUV";
                    Unwrapping.GenerateSecondaryUVSet(mesh);
                    AssetDatabase.CreateAsset(mesh, AssetDir + "/" + mesh.name + ".asset");
                    filter.sharedMesh = mesh;
                }
                var renderer = t.GetComponent<MeshRenderer>();
                renderer.receiveGI = ReceiveGI.Lightmaps;
                renderer.scaleInLightmap = t.name.Contains("_Post_") || t.name.Contains("_Rail_") ? .15f : .6f;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ContributeGI);
            }

            // Surface-mounted detail, no new cover, route obstruction or camera collider.
            Box(sample, "WorkshopFrontLowerPaint_Left", new Vector3(-10, 2.83f, 8.746f), new Vector3(4, .8f, .006f), teal);
            Box(sample, "WorkshopFrontLowerPaint_Right", new Vector3(1.5f, 2.83f, 8.746f), new Vector3(9, .8f, .006f), teal);
            Box(sample, "WorkshopRearLowerPaint", new Vector3(-3, 2.83f, 18.746f), new Vector3(18, .8f, .006f), teal);
            Box(sample, "WorkshopWestLowerPaint", new Vector3(-11.746f, 2.83f, 14), new Vector3(.006f, .8f, 10), teal);
            Box(sample, "DoorHeaderPaint", new Vector3(-5.5f, 6.1f, 8.746f), new Vector3(4.94f, .50f, .006f), teal);
            // Trim occupies the existing wall faces, not the open doorway.
            foreach (float x in new[] { -8.08f, -2.92f })
                Box(sample, "DoorEdge_" + x, new Vector3(x, 4.3f, 8.741f), new Vector3(.12f, 3.72f, .008f), steel);
            Box(sample, "LoadingEdgeStripe", new Vector3(-1, 2.404f, 6.12f), new Vector3(9.8f, .006f, .13f), yellow);
            Box(sample, "BenchFrontInset", new Vector3(-5, 3.1f, 11.796f), new Vector3(3.5f, .76f, .006f), rubber);
            for (int i = 0; i < 3; i++)
            {
                Box(sample, "BenchDrawer_" + i, new Vector3(-6.1f + i * 1.1f, 3.1f, 11.790f), new Vector3(.99f, .66f, .008f), teal);
                Box(sample, "BenchHandle_" + i, new Vector3(-6.1f + i * 1.1f, 3.23f, 11.780f), new Vector3(.34f, .045f, .01f), steel);
            }
            // Recessed service panel/vents on existing machinery, not decorative combat crates.
            for (int i = 0; i < 7; i++)
                Box(sample, "CarriageVent_" + i, new Vector3(-10.4f + i * .45f, 1.85f, -1.804f), new Vector3(.25f, .36f, .006f), rubber);
            var sign = new GameObject("WorkshopServiceSign").AddComponent<TextMesh>();
            sign.transform.SetParent(sample, false); sign.transform.position = new Vector3(1.25f, 5.08f, 8.736f);
            sign.transform.rotation = Quaternion.identity;
            sign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign.GetComponent<Renderer>().sharedMaterial = sign.font.material;
            sign.text = "BOAT & ENGINE\nSERVICE"; sign.fontSize = 72; sign.characterSize = .38f;
            sign.anchor = TextAnchor.MiddleCenter; sign.alignment = TextAlignment.Center;
            sign.color = new Color(.12f, .30f, .33f);

            var sun = roots.SelectMany(g => g.GetComponentsInChildren<Light>()).Single(l => l.type == LightType.Directional);
            sun.transform.rotation = Quaternion.Euler(48, -38, 0);
            sun.color = new Color(1, .95f, .86f); sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft; sun.shadowBias = .035f; sun.shadowNormalBias = .22f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "CoastalDaySky" };
            sky.SetFloat("_SunSize", .025f); sky.SetFloat("_AtmosphereThickness", .85f);
            sky.SetColor("_SkyTint", new Color(.50f, .60f, .73f));
            sky.SetColor("_GroundColor", new Color(.48f, .49f, .45f)); sky.SetFloat("_Exposure", 1.1f);
            AssetDatabase.CreateAsset(sky, AssetDir + "/CoastalDaySky.mat");
            RenderSettings.skybox = sky; RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = 1.15f;
            RenderSettings.reflectionIntensity = .65f; RenderSettings.fog = false;
            var sea = roots.Single(g => g.name == "Backdrop").transform.Find("SeaContext_NotPlayable");
            var water = PlainMaterial("SeaContext", new Color(.16f, .43f, .53f), 0, .40f);
            sea.GetComponent<Renderer>().sharedMaterial = water; // Context color only; no water system/art pass.

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, AssetDir + "/CoastalDayVolume.asset");
            profile.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.value = .12f; grade.contrast.value = 4; grade.saturation.value = 6;
            // Explicitly disable cinematic blur/glow rather than relying on global defaults.
            profile.Add<Bloom>(true).intensity.value = 0;
            profile.Add<MotionBlur>(true).intensity.value = 0;
            profile.Add<DepthOfField>(true).mode.value = DepthOfFieldMode.Off;
            profile.Add<Vignette>(true).intensity.value = 0;
            foreach (var component in profile.components)
            {
                AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("CoastalDayVolume").AddComponent<Volume>();
            volume.transform.SetParent(sample, false); volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;

            var probes = new GameObject("SampleLightProbes").AddComponent<LightProbeGroup>();
            probes.transform.SetParent(sample, false);
            var positions = new List<Vector3>();
            foreach (float x in new[] { -10f, -6f, -2f, 3f })
                foreach (float z in new[] { -2f, 4f, 7.5f, 11f, 16f })
                    foreach (float h in new[] { .7f, 2.2f, 3.5f })
                        positions.Add(new Vector3(x, (z >= 6 ? 2.4f : 1.2f) + h, z));
            // A few context probes keep dynamic bodies lit when leaving the sample.
            foreach (var p in new[] { new Vector3(10, 0, -16), new Vector3(-6, 0, -10),
                new Vector3(14.5f, 1.2f, -3), new Vector3(14.5f, 1.2f, 12) })
                foreach (float h in new[] { .7f, 2.2f, 3.5f }) positions.Add(p + Vector3.up * h);
            probes.probePositions = positions.ToArray();
            Reflection(sample, "WorkshopReflection", new Vector3(-3, 4.2f, 14), new Vector3(18, 7, 10));
            Reflection(sample, "YardReflection", new Vector3(-3, 3, 2), new Vector3(22, 8, 16));

            var settings = new LightingSettings { name = "BoatyardSampleLighting", bakedGI = true, realtimeGI = false,
                lightmapper = LightingSettings.Lightmapper.ProgressiveCPU, mixedBakeMode = MixedLightingMode.IndirectOnly,
                lightmapResolution = 12, lightmapMaxSize = 1024, lightmapPadding = 4,
                directSampleCount = 32, indirectSampleCount = 64, environmentSampleCount = 64,
                maxBounces = 2, ao = false };
            AssetDatabase.CreateAsset(settings, AssetDir + "/LightingSettings.asset");
            Lightmapping.lightingSettings = settings;
            Lightmapping.lightingDataAsset = null; // Never mutate inherited/shared lighting data.

            // Fail rather than silently changing accepted body/bullet/bot geometry.
            foreach (Transform t in arena)
            {
                var b = t.GetComponent<Renderer>().bounds; var old = envelopes[t.name];
                if ((b.min - old.min).sqrMagnitude > .000001f || (b.max - old.max).sqrMagnitude > .000001f)
                    throw new InvalidOperationException("Visual edit changed collision envelope: " + t.name);
            }
            var after = map.BuildCollision();
            if (!before.Solids.SequenceEqual(after.Solids) || !before.Surfaces.SequenceEqual(after.Surfaces))
                throw new InvalidOperationException("Art sample changed shared numeric collision.");
            if (sample.GetComponentsInChildren<Collider>().Length != 0)
                throw new InvalidOperationException("Art detail must not add independent collision.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Boatyard look sample authored. Collision unchanged. Bake this scene before building.");
        }

        public static bool Bake()
        {
            if (EditorApplication.isPlaying || SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != BoatyardSceneBuilder.ScenePath)
                throw new InvalidOperationException("Bake only the saved Boatyard scene, without additive scenes or Play mode.");
            if (!Lightmapping.Bake()) throw new InvalidOperationException("Boatyard sample light bake failed.");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            return LightmapSettings.lightmaps.Length > 0 && LightmapSettings.lightProbes != null;
        }

        static void ConfigureSourceTextures()
        {
            foreach (string file in Directory.GetFiles(AssetDir + "/Textures", "*.jpg"))
            {
                string path = file.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.Contains("NormalGL"), rough = path.Contains("Roughness");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal && !rough; importer.isReadable = !normal;
                importer.maxTextureSize = 1024; importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }
        }

        static Material TexturedMaterial(string name, string id, Color target, float bump, float contrast)
        {
            string prefix = AssetDir + "/Textures/" + id + "_1K-JPG_";
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "Color.jpg");
            var pixels = source.GetPixels(); float mean = pixels.Average(c => c.grayscale);
            for (int i = 0; i < pixels.Length; i++)
            {
                float delta = (pixels[i].grayscale - mean) * contrast;
                pixels[i] = new Color(target.r + delta, target.g + delta, target.b + delta, 1);
            }
            var albedo = WriteTexture(name + "_Albedo", pixels, source.width, source.height, false);
            var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "Roughness.jpg");
            var masks = rough.GetPixels();
            for (int i = 0; i < masks.Length; i++)
                masks[i] = new Color(0, 0, 0, Mathf.Clamp(1 - masks[i].r, .08f, .30f));
            var packed = WriteTexture(name + "_MetalSmooth", masks, rough.width, rough.height, true);
            var material = PlainMaterial(name, Color.white, 0, 1);
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "NormalGL.jpg"));
            material.SetFloat("_BumpScale", bump); material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", packed); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            foreach (string suffix in new[] { "Color.jpg", "Roughness.jpg" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(prefix + suffix);
                importer.isReadable = false; importer.SaveAndReimport();
            }
            return material;
        }

        static Texture2D WriteTexture(string name, Color[] pixels, int width, int height, bool linear)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
            texture.SetPixels(pixels); texture.Apply();
            string path = AssetDir + "/Textures/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = !linear; importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 8;
            importer.maxTextureSize = 1024; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material PlainMaterial(string name, Color color, float metal, float smooth)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metal); material.SetFloat("_Smoothness", smooth);
            AssetDatabase.CreateAsset(material, AssetDir + "/" + name + ".mat"); return material;
        }

        static void Box(Transform root, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(root, false); go.transform.position = position; go.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            // Thin paint/details need no separate bake chart or extra contact shadow.
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Reflection(Transform root, string name, Vector3 position, Vector3 size)
        {
            var probe = new GameObject(name).AddComponent<ReflectionProbe>(); probe.transform.SetParent(root, false);
            probe.transform.position = position; probe.size = size; probe.boxProjection = true;
            probe.mode = ReflectionProbeMode.Baked; probe.resolution = 128; probe.blendDistance = 2;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox; probe.cullingMask = 1;
        }

        static Mesh RampMesh(Mesh source)
        {
            var v = source.vertices;
            return Faces(new[] { new[] { v[0], v[1], v[5], v[4] }, new[] { v[0], v[4], v[2] },
                new[] { v[1], v[3], v[5] }, new[] { v[2], v[4], v[5], v[3] },
                new[] { v[0], v[2], v[3], v[1] } }, Vector3.one);
        }

        internal static Mesh BeveledBox(Vector3 size, float bevel)
        {
            var h = size * .5f; float b = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * .3f);
            var inset = h - Vector3.one * b;
            var faces = new List<Vector3[]>();
            // Six inset main faces; twelve bevel strips; eight corner triangles.
            for (int axis = 0; axis < 3; axis++) for (int sign = -1; sign <= 1; sign += 2)
            {
                int u = (axis + 1) % 3, w = (axis + 2) % 3;
                var face = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    var p = Vector3.zero; p[axis] = sign * h[axis];
                    p[u] = (i == 0 || i == 3 ? -1 : 1) * inset[u];
                    p[w] = (i < 2 ? -1 : 1) * inset[w]; face[i] = p;
                }
                faces.Add(Outward(face));
            }
            for (int axis = 0; axis < 3; axis++) for (int a = -1; a <= 1; a += 2) for (int c = -1; c <= 1; c += 2)
            {
                int u = (axis + 1) % 3, w = (axis + 2) % 3;
                var p = Vector3.zero; p[axis] = -inset[axis]; p[u] = a * h[u]; p[w] = c * inset[w];
                var q = p; q[u] = a * inset[u]; q[w] = c * h[w];
                var r = q; r[axis] = inset[axis]; var s = p; s[axis] = inset[axis];
                faces.Add(Outward(new[] { p, q, r, s }));
            }
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                faces.Add(Outward(new[] { new Vector3(x * h.x, y * inset.y, z * inset.z),
                    new Vector3(x * inset.x, y * h.y, z * inset.z), new Vector3(x * inset.x, y * inset.y, z * h.z) }));
            return Faces(faces, size);
        }

        static Vector3[] Outward(Vector3[] face)
        {
            if (Vector3.Dot(Vector3.Cross(face[1] - face[0], face[2] - face[0]), face.Aggregate(Vector3.zero, (a, p) => a + p)) < 0)
                Array.Reverse(face);
            return face;
        }

        static Mesh Faces(IEnumerable<Vector3[]> faces, Vector3 scale)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            foreach (var face in faces)
            {
                int start = vertices.Count;
                var normal = Vector3.Cross(face[1] - face[0], face[2] - face[0]).normalized;
                // Face-local metric UVs: 2 m repeat including actual ramp slope length.
                var u = (face[1] - face[0]).normalized; var w = Vector3.Cross(normal, u);
                foreach (var p in face)
                {
                    vertices.Add(new Vector3(p.x / scale.x, p.y / scale.y, p.z / scale.z));
                    uv.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, w)) * .5f);
                }
                for (int i = 1; i < face.Length - 1; i++) triangles.AddRange(new[] { start, start + i, start + i + 1 });
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh); return mesh;
        }
    }
}
