using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeMyArms.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BeMyArms.Client.EditorTools
{
    /// <summary>Guarded continuation of the accepted sample, not a layout generator. Authored
    /// surfaces/details share the existing bounds; the accepted sun and gameplay geometry remain.</summary>
    public static class BoatyardContinuityPass
    {
        const string Dir = BoatyardLookSample.AssetDir;
        const string Output = Dir + "/Continuity";
        public const string RootName = "EnvironmentDetails";

        public static void Author()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || Lightmapping.isRunning)
                throw new InvalidOperationException("Finish play/compile/bake before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Review/save open scene edits first.");
            if (AssetDatabase.IsValidFolder(Output)) throw new InvalidOperationException("Already authored; edit normally, do not rerun.");
            foreach (string map in new[] { "Color", "NormalGL", "Roughness" })
                if (!File.Exists(Dir + "/Textures/ConcreteContinuous_" + map + ".png")) throw new InvalidOperationException("Generate matched concrete variation maps first.");
            var scene = EditorSceneManager.OpenScene(BoatyardSceneBuilder.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var arena = roots.Single(g => g.name == "Arena").transform;
            var backdrop = roots.Single(g => g.name == "Backdrop").transform;
            if (roots.Any(g => g.name == RootName)) throw new InvalidOperationException("Details already exist.");
            var mapSpawns = roots.SelectMany(g => g.GetComponentsInChildren<MapSpawns>()).Single();
            var before = mapSpawns.BuildCollision();
            var envelopes = arena.Cast<Transform>().ToDictionary(t => t.name, t => t.GetComponent<Renderer>().bounds);
            var root = new GameObject(RootName).transform;
            AssetDatabase.CreateFolder(Dir, "Continuity");
            ConfigureConcrete();
            var concrete = Material("Concrete"); var plaster = Material("Plaster");
            var steel = Material("Steel"); var teal = Material("TealPaint"); var ochre = Material("SafetyOchre");
            var rubber = Material("DarkRubber");
            var rock = Clone(concrete, "CoastalRock", new Color(.76f, .82f, .84f)); rock.SetFloat("_BumpScale", 1);
            var blue = Clone(plaster, "PumpHousePaint", new Color(.76f, .85f, .90f));
            var hull = Clone(teal, "FiberglassHousing", new Color(1.75f, 1.85f, 1.70f));
            hull.SetFloat("_Smoothness", .7f); hull.SetFloat("_BumpScale", .18f);
            var glass = Clone(steel, "ClosedWindow", new Color(.19f, .32f, .37f)); glass.SetFloat("_Smoothness", .95f);
            var grass = Clone(concrete, "CoastalScrub", new Color(.60f, .76f, .40f)); grass.SetFloat("_BumpScale", .35f);

            // Connected construction pieces have square contact edges, not individual inset bevels.
            // World-projected UVs use one origin across all decks/ramps, with no mesh-local resets.
            var temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cube = temporary.GetComponent<MeshFilter>().sharedMesh;
            foreach (Transform t in arena)
            {
                var renderer = t.GetComponent<MeshRenderer>();
                bool ramp = t.name.StartsWith("BMA_Map_Ramp");
                bool deck = t.name.StartsWith("BMA_Map_Floor") || t.name.StartsWith("BMA_Map_Platform");
                bool cliff = t.name.Contains("Cliff") || t.name.Contains("CoastSpur");
                bool building = t.name.Contains("Workshop") && t.name.StartsWith("BMA_Map_Solid") && !t.name.Contains("Roof") ||
                    t.name.Contains("PumpHouse") || t.name.Contains("ServiceStore") || t.name.Contains("ShorePumpTower");
                bool equipment = t.name.Contains("MotorRack") || t.name.Contains("DryDockHull") || t.name.Contains("QuayWinch") || t.name.Contains("ServicePipe");
                if (ramp || deck || building || cliff || equipment)
                {
                    var mesh = UnityEngine.Object.Instantiate(ramp ? t.GetComponent<MeshFilter>().sharedMesh :
                        equipment ? BoatyardLookSample.BeveledBox(t.localScale, .025f) : cube);
                    if (deck || ramp) renderer.sharedMaterial = concrete;
                    else if (cliff) renderer.sharedMaterial = rock;
                    else if (building) renderer.sharedMaterial = t.name.Contains("PumpHouse") ? blue : plaster;
                    else renderer.sharedMaterial = t.name.Contains("DryDockHull") ? hull : t.name.Contains("ServicePipe") ? steel : teal;
                    ProjectUv(mesh, t, deck || ramp || cliff ? 1f / 12 : .5f);
                    SaveMesh(mesh, t.name); t.GetComponent<MeshFilter>().sharedMesh = mesh;
                    if (ramp) t.GetComponent<MeshCollider>().sharedMesh = mesh;
                }
                else if (t.name.Contains("_Post_") || t.name.Contains("_Rail_"))
                {
                    renderer.sharedMaterial = steel;
                    var mesh = UnityEngine.Object.Instantiate(t.GetComponent<MeshFilter>().sharedMesh);
                    ProjectUv(mesh, t, .5f); SaveMesh(mesh, t.name); t.GetComponent<MeshFilter>().sharedMesh = mesh;
                }
            }
            UnityEngine.Object.DestroyImmediate(temporary);

            // Surface-mounted architectural language: continuous fascia/plinths, closed glazing,
            // roof seams and wall plumbing. These do not add openings, cover or route obstruction.
            Box(root, "WorkshopRoofFascia", new Vector3(-3, 6.53f, 8.746f), new Vector3(18.5f, .22f, .025f), teal);
            Box(root, "WorkshopRearFascia", new Vector3(-3, 6.53f, 19.254f), new Vector3(18.5f, .22f, .025f), teal);
            Box(root, "WorkshopWestFascia", new Vector3(-12.254f, 6.53f, 14), new Vector3(.025f, .22f, 10.5f), teal);
            for (int i = 0; i < 19; i++)
                Box(root, "WorkshopRoofStandingSeam_" + i, new Vector3(-12 + i, 6.704f, 14), new Vector3(.025f, .014f, 10.4f), steel);
            foreach (float x in new[] { -10f, -.4f, 3.5f }) Window(root, "WorkshopWindow_" + x, new Vector3(x, 4.65f, 8.739f), 2.1f, 1.3f, glass, steel);
            Building(root, arena.Find("BMA_Map_Solid_PumpHouse"), blue, teal, steel, glass);
            Building(root, arena.Find("BMA_Map_Solid_ServiceStore"), plaster, teal, steel, glass);
            Building(root, arena.Find("BMA_Map_Solid_ShorePumpTower"), blue, teal, steel, glass);
            // The PumpHouse return is the same continuous building, not a differently shaded block.
            for (int i = 0; i < 9; i++)
                Box(root, "PumpHouseCladdingJoint_" + i, new Vector3(12.003f, 3.3f, -6 + i * 1.8f), new Vector3(.012f, 6.55f, .025f), blue);
            foreach (float x in new[] { -11.5f, 5.4f })
                Cylinder(root, "WorkshopDownpipe_" + x, new Vector3(x, 4.45f, 8.72f), .085f, 3.7f, Quaternion.identity, steel);
            // Dock bumpers sit on vertical faces; leave the top of the loading ramp uninterrupted.
            foreach (float x in new[] { .6f, 2.9f }) Box(root, "LoadingDockRubberBumper_" + x, new Vector3(x, 1.88f, 5.981f), new Vector3(.26f, .65f, .04f), rubber);

            // Finish the existing cover as actual shop equipment while retaining the solid silhouettes.
            foreach (var name in new[] { "EngineBench", "EngineCarriage", "SpareMotorRack", "QuayWinch", "CranePedestal" })
            {
                var t = arena.Cast<Transform>().Single(p => p.name.EndsWith(name)); var b = t.GetComponent<Renderer>().bounds;
                Box(root, name + "TopPlate", new Vector3(b.center.x, b.max.y + .004f, b.center.z), new Vector3(b.size.x * .96f, .012f, b.size.z * .94f), steel);
                Box(root, name + "LowerSkirt", new Vector3(b.center.x, b.min.y + .10f, b.min.z - .005f), new Vector3(b.size.x * .95f, .18f, .018f), rubber);
                for (int i = 0; i < 3; i++)
                {
                    float x = b.min.x + b.size.x * (.2f + .3f * i);
                    Box(root, name + "ServiceDoor_" + i, new Vector3(x, b.center.y, b.min.z - .007f), new Vector3(b.size.x * .27f, b.size.y * .64f, .018f), teal);
                    Box(root, name + "Latch_" + i, new Vector3(x, b.center.y + .05f, b.min.z - .019f), new Vector3(.15f, .06f, .018f), steel);
                }
            }
            var rack = arena.Find("BMA_Map_Cover_SpareMotorRack").GetComponent<Renderer>().bounds;
            for (int i = 0; i < 7; i++) Box(root, "RackVent_" + i, new Vector3(rack.max.x + .006f, rack.center.y, rack.min.z + .3f + i * .37f), new Vector3(.012f, .48f, .06f), rubber);
            foreach (float z in new[] { .38f, 1, 1.62f })
                Cylinder(root, "PipeBundleSurface_" + z, new Vector3(14.5f, 2.82f, z), .14f, 4.9f, Quaternion.Euler(0, 0, 90), steel);
            foreach (float x in new[] { 12.2f, 16.8f }) Box(root, "PipeBundleStrap_" + x, new Vector3(x, 2.858f, 1), new Vector3(.12f, .025f, 1.6f), ochre);
            Box(root, "CraneMastCollar", new Vector3(-1, 3.4f, 0), new Vector3(.72f, .2f, .72f), steel);
            foreach (float x in new[] { -1.26f, -.74f }) foreach (float z in new[] { -.26f, .26f })
                Cylinder(root, "CraneBaseBolt_" + x + "_" + z, new Vector3(x, 3.32f, z), .09f, .05f, Quaternion.identity, steel);
            // Filled dry-dock mould/cradle remains solid gameplay cover; do not fake a hollow boat
            // that bullets should pass through but the numeric collision would still block.
            var dryDock = arena.Find("BMA_Map_Cover_DryDockHull").GetComponent<Renderer>().bounds;
            for (int i = 0; i < 6; i++)
                Box(root, "HullMouldRib_" + i, new Vector3(dryDock.center.x, dryDock.center.y, dryDock.min.z + .15f + i * .58f), new Vector3(dryDock.size.x + .014f, dryDock.size.y * .97f, .06f), steel);
            Box(root, "HullMouldRim", new Vector3(dryDock.center.x, dryDock.max.y + .004f, dryDock.center.z), new Vector3(dryDock.size.x, .018f, dryDock.size.z), hull);

            // Background can have real non-box silhouettes because it is not gameplay cover.
            foreach (Transform t in backdrop)
            {
                if (t.name.Contains("Land") || t.name.Contains("Headland") || t.name.Contains("Island"))
                {
                    var mesh = HillMesh(); SaveMesh(mesh, t.name);
                    t.GetComponent<MeshFilter>().sharedMesh = mesh; t.GetComponent<Renderer>().sharedMaterial = grass;
                }
                else if (t.name.Contains("Workshop") && !t.name.Contains("Roof") || t.name.Contains("Cabin")) t.GetComponent<Renderer>().sharedMaterial = plaster;
                else if (t.name.Contains("Roof") || t.name.Contains("Hull")) t.GetComponent<Renderer>().sharedMaterial = teal;
                else if (!t.name.Contains("SeaContext"))
                {
                    t.GetComponent<Renderer>().sharedMaterial = concrete;
                    var mesh = UnityEngine.Object.Instantiate(t.GetComponent<MeshFilter>().sharedMesh); ProjectUv(mesh, t, 1f / 12); SaveMesh(mesh, t.name); t.GetComponent<MeshFilter>().sharedMesh = mesh;
                }
            }
            var boat = backdrop.Find("MooredBoatHull"); var boatMesh = BoatMesh(); SaveMesh(boatMesh, "BackgroundBoatHull"); boat.GetComponent<MeshFilter>().sharedMesh = boatMesh;
            foreach (float x in new[] { 23.02f, 26.98f }) Box(root, "RemoteBoatGunwale_" + x, new Vector3(x, -.2f, -19.05f), new Vector3(.05f, .10f, 5.4f), steel);

            var after = mapSpawns.BuildCollision();
            if (!before.Solids.SequenceEqual(after.Solids) || !before.Surfaces.SequenceEqual(after.Surfaces)) throw new InvalidOperationException("Art changed accepted numeric collision.");
            foreach (Transform t in arena)
                if ((t.GetComponent<Renderer>().bounds.min - envelopes[t.name].min).sqrMagnitude > .000001f ||
                    (t.GetComponent<Renderer>().bounds.max - envelopes[t.name].max).sqrMagnitude > .000001f) throw new InvalidOperationException("Envelope changed: " + t.name);
            if (root.GetComponentsInChildren<Collider>().Length != 0) throw new InvalidOperationException("Details may not add camera/gameplay collision.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Boatyard continuity/equipment pass saved; collision unchanged. Rebake before delivery.");
        }

        static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(Dir + "/" + name + ".mat");
        static Material Clone(Material source, string name, Color tint)
        {
            var m = new Material(source) { name = name }; m.SetColor("_BaseColor", tint);
            AssetDatabase.CreateAsset(m, Output + "/" + name + ".mat"); return m;
        }
        static void ConfigureConcrete()
        {
            foreach (string kind in new[] { "Color", "NormalGL", "Roughness" })
            {
                string path = Dir + "/Textures/ConcreteContinuous_" + kind + ".png"; AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = kind == "NormalGL" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = kind == "Color"; importer.maxTextureSize = 4096; importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 8; importer.filterMode = FilterMode.Trilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.isReadable = kind == "Roughness"; importer.SaveAndReimport();
            }
            string roughPath = Dir + "/Textures/ConcreteContinuous_Roughness.png";
            var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath); var pixels = rough.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, (byte)(255 - pixels[i].r));
            var packed = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, false, true); packed.SetPixels32(pixels); packed.Apply();
            string pathPacked = Dir + "/Textures/ConcreteContinuous_MetalSmooth.png"; File.WriteAllBytes(pathPacked, packed.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(pathPacked); var pi = (TextureImporter)AssetImporter.GetAtPath(pathPacked);
            pi.sRGBTexture = false; pi.maxTextureSize = 4096; pi.isReadable = false; pi.anisoLevel = 8; pi.filterMode = FilterMode.Trilinear;
            pi.textureCompression = TextureImporterCompression.CompressedHQ; pi.SaveAndReimport();
            var ri = (TextureImporter)AssetImporter.GetAtPath(roughPath); ri.isReadable = false; ri.SaveAndReimport();
            var m = Material("Concrete"); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Textures/ConcreteContinuous_Color.png"));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Textures/ConcreteContinuous_NormalGL.png"));
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(pathPacked));
            foreach (string prop in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" }) m.SetTextureScale(prop, Vector2.one);
            EditorUtility.SetDirty(m);
        }
        static void ProjectUv(Mesh mesh, Transform t, float scale)
        {
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = t.TransformPoint(vertices[i]); var n = t.TransformDirection(normals[i]);
                uv[i] = (Mathf.Abs(n.y) > .5f ? new Vector2(p.x, p.z) : Mathf.Abs(n.x) > .5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y)) * scale;
            }
            mesh.uv = uv; mesh.RecalculateTangents(); Unwrapping.GenerateSecondaryUVSet(mesh);
        }
        static void SaveMesh(Mesh mesh, string name) { mesh.name = name + "_Continuous"; AssetDatabase.CreateAsset(mesh, Output + "/" + mesh.name + ".asset"); }
        static void Box(Transform root, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(root, false); go.transform.position = position; go.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var mesh = UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh); ProjectUv(mesh, go.transform, .5f); SaveMesh(mesh, name); go.GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        static void Cylinder(Transform root, string name, Vector3 position, float diameter, float length, Quaternion rotation, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(position, rotation); go.transform.localScale = new Vector3(diameter, length * .5f, diameter);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        static void Window(Transform root, string name, Vector3 position, float width, float height, Material glass, Material frame)
        {
            Box(root, name + "Frame", position, new Vector3(width + .12f, height + .12f, .022f), frame);
            Box(root, name + "ClosedPane", position - Vector3.forward * .014f, new Vector3(width, height, .012f), glass);
            Box(root, name + "Mullion", position - Vector3.forward * .023f, new Vector3(.045f, height, .01f), frame);
        }
        static void Building(Transform root, Transform t, Material wall, Material paint, Material steel, Material glass)
        {
            var b = t.GetComponent<Renderer>().bounds;
            Box(root, t.name + "RoofCap", new Vector3(b.center.x, b.max.y + .005f, b.center.z), new Vector3(b.size.x + .04f, .022f, b.size.z + .04f), steel);
            Box(root, t.name + "FrontPlinth", new Vector3(b.center.x, b.min.y + .6f, b.min.z - .006f), new Vector3(b.size.x, 1.2f, .012f), paint);
            Window(root, t.name + "FrontWindow", new Vector3(b.center.x, b.min.y + 3.8f, b.min.z - .015f), b.size.x * .57f, 1.1f, glass, steel);
            Box(root, t.name + "ClosedServiceDoor", new Vector3(b.center.x, b.min.y + 1.25f, b.min.z - .014f), new Vector3(1.3f, 2.5f, .025f), paint);
            for (int i = 0; i < 5; i++) Box(root, t.name + "DoorLouvre_" + i, new Vector3(b.center.x, b.min.y + .5f + i * .15f, b.min.z - .03f), new Vector3(.9f, .035f, .016f), steel);
        }
        static Mesh HillMesh()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            const int rings = 7, segments = 20;
            for (int y = 0; y <= rings; y++) for (int i = 0; i <= segments; i++)
            {
                float latitude = Mathf.PI * y / rings, angle = 2 * Mathf.PI * i / segments;
                float ridge = 1 + .10f * Mathf.Sin(angle * 3 + latitude) + .05f * Mathf.Cos(angle * 7 - latitude * 2);
                vertices.Add(new Vector3(Mathf.Cos(angle) * Mathf.Sin(latitude) * .47f * ridge, Mathf.Cos(latitude) * .5f, Mathf.Sin(angle) * Mathf.Sin(latitude) * .47f * ridge));
                if (y < rings && i < segments) { int a = y * (segments + 1) + i; triangles.AddRange(new[] { a, a + 1, a + segments + 1, a + 1, a + segments + 2, a + segments + 1 }); }
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            mesh.uv = vertices.Select(v => new Vector2(v.x, v.z) * 12).ToArray(); mesh.RecalculateTangents(); Unwrapping.GenerateSecondaryUVSet(mesh); return mesh;
        }
        static Mesh BoatMesh()
        {
            // Background-only workboat: closed chine hull with a tapered bow, not a stretched cube.
            var v = new List<Vector3>(); var tri = new List<int>();
            var sections = new[] { new Vector3(.36f, .12f, -.5f), new Vector3(.5f, .5f, -.28f), new Vector3(.5f, .5f, .27f), new Vector3(.23f, .35f, .5f) };
            foreach (var s in sections) v.AddRange(new[] { new Vector3(-s.x, .5f, s.z), new Vector3(s.x, .5f, s.z), new Vector3(s.x * .64f, -.38f, s.z), new Vector3(-s.x * .64f, -.38f, s.z) });
            for (int i = 0; i < 3; i++) for (int side = 0; side < 4; side++) { int a = i * 4 + side, b = i * 4 + (side + 1) % 4; tri.AddRange(new[] { a, b + 4, b, a, a + 4, b + 4 }); }
            tri.AddRange(new[] { 0, 1, 2, 0, 2, 3, 12, 14, 13, 12, 15, 14 });
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.uv = v.Select(p => new Vector2(p.x, p.z)).ToArray(); mesh.RecalculateTangents(); Unwrapping.GenerateSecondaryUVSet(mesh); return mesh;
        }
    }
}
