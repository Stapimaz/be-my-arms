// Live-Editor follow-up to 641063b, not layout regeneration. Keep the accepted exterior light.
// Refuses dirty scenes or a rerun; source maps must be downloaded/imported first.
if (UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isCompiling)
    throw new System.Exception("Stop Play mode/finish compilation before authoring.");
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
        throw new System.Exception("Review/save open scene changes before authoring.");
string dir = "Assets/Art/Maps/BoatyardLookSample";
foreach (string id in new[] { "Concrete030", "Plaster001", "Metal027", "Metal032" })
    foreach (string map in new[] { "Color", "NormalGL", "Roughness" })
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "/Textures/" + id + "_2K-JPG_" + map + ".jpg") == null)
            throw new System.Exception("Missing imported source: " + id + " / " + map);
if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "/WorkshopLightPanel.mat") != null)
    throw new System.Exception("Follow-up already authored; edit normally instead of rerunning.");
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Boatyard.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
var roots = scene.GetRootGameObjects();
var sample = roots.Single(g => g.name == "LookSample").transform;
var sign = sample.Find("WorkshopServiceSign");
if (sign == null) throw new System.Exception("Expected the initial sample's known sign; do not overwrite other authoring.");
var arena = roots.Single(g => g.name == "Arena").transform;
var mapSpawns = roots.SelectMany(g => g.GetComponentsInChildren<BeMyArms.Match.MapSpawns>()).Single();
var before = mapSpawns.BuildCollision();
var sun = roots.SelectMany(g => g.GetComponentsInChildren<UnityEngine.Light>()).Single(l => l.type == UnityEngine.LightType.Directional);
var sunRotation = sun.transform.rotation; float sunIntensity = sun.intensity;

// Color texture retains its measured variation rather than flattening to target + tiny noise.
// Roughness is linear; normal uses GL convention; URP smoothness is packed into alpha.
UnityEngine.Texture2D WriteTexture(string name, UnityEngine.Color[] pixels, int width, int height, bool linear) {
    var texture = new UnityEngine.Texture2D(width, height, UnityEngine.TextureFormat.RGBA32, false, linear);
    texture.SetPixels(pixels); texture.Apply();
    string path = dir + "/Textures/" + name + ".png";
    System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
    UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate);
    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
    importer.sRGBTexture = !linear; importer.maxTextureSize = 2048;
    importer.textureCompression = UnityEditor.TextureImporterCompression.CompressedHQ;
    importer.compressionQuality = 100; importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
    importer.filterMode = UnityEngine.FilterMode.Trilinear; importer.anisoLevel = 8;
    importer.mipmapEnabled = true; importer.isReadable = false; importer.SaveAndReimport();
    return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(path);
}
void Pbr(string name, string id, UnityEngine.Color target, float contrast, float bump, float metal, float smooth, float repeat) {
    string prefix = dir + "/Textures/" + id + "_2K-JPG_";
    foreach (string kind in new[] { "Color", "NormalGL", "Roughness" }) {
        var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(prefix + kind + ".jpg");
        importer.textureType = kind == "NormalGL" ? UnityEditor.TextureImporterType.NormalMap : UnityEditor.TextureImporterType.Default;
        importer.sRGBTexture = kind == "Color"; importer.isReadable = kind != "NormalGL";
        importer.maxTextureSize = 2048; importer.mipmapEnabled = true;
        importer.textureCompression = UnityEditor.TextureImporterCompression.CompressedHQ; importer.compressionQuality = 100;
        importer.wrapMode = UnityEngine.TextureWrapMode.Repeat; importer.filterMode = UnityEngine.FilterMode.Trilinear;
        importer.anisoLevel = 8; importer.SaveAndReimport();
    }
    var source = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(prefix + "Color.jpg");
    var pixels = source.GetPixels(); float mean = pixels.Average(c => c.grayscale);
    for (int i = 0; i < pixels.Length; i++) {
        float ratio = UnityEngine.Mathf.Clamp(1 + (pixels[i].grayscale / UnityEngine.Mathf.Max(mean, .001f) - 1) * contrast, .55f, 1.35f);
        pixels[i] = new UnityEngine.Color(target.r * ratio, target.g * ratio, target.b * ratio, 1);
    }
    var albedo = WriteTexture(name + "_Albedo", pixels, source.width, source.height, false);
    var rough = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(prefix + "Roughness.jpg");
    var masks = rough.GetPixels();
    for (int i = 0; i < masks.Length; i++) masks[i] = new UnityEngine.Color(metal, 0, 0, 1 - masks[i].r);
    var packed = WriteTexture(name + "_MetalSmooth", masks, rough.width, rough.height, true);
    var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "/" + name + ".mat");
    material.SetColor("_BaseColor", UnityEngine.Color.white);
    material.SetTexture("_BaseMap", albedo);
    material.SetTexture("_BumpMap", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(prefix + "NormalGL.jpg"));
    material.SetTexture("_MetallicGlossMap", packed);
    material.SetFloat("_BumpScale", bump); material.SetFloat("_Metallic", metal); material.SetFloat("_Smoothness", smooth);
    material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
    foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
        material.SetTextureScale(property, UnityEngine.Vector2.one * repeat);
    UnityEditor.EditorUtility.SetDirty(material);
    foreach (string kind in new[] { "Color", "Roughness" }) {
        var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(prefix + kind + ".jpg");
        importer.isReadable = false; importer.SaveAndReimport();
    }
}
Pbr("Concrete", "Concrete030", new UnityEngine.Color(.72f, .70f, .65f), .85f, .8f, 0, .65f, 1);
Pbr("Plaster", "Plaster001", new UnityEngine.Color(.86f, .83f, .76f), .8f, .65f, 0, .55f, 1.5f);
Pbr("TealPaint", "Metal027", new UnityEngine.Color(.16f, .40f, .43f), .45f, .45f, 0, .85f, 4);
Pbr("SafetyOchre", "Metal027", new UnityEngine.Color(.88f, .58f, .16f), .45f, .45f, 0, .85f, 4);
Pbr("Steel", "Metal032", new UnityEngine.Color(.43f, .48f, .52f), .65f, .45f, 1, .9f, 2);

UnityEngine.Object.DestroyImmediate(sign.gameObject); // Only the rejected sign authored in 641063b.
// Thin painted strips/drawers originally used stretched built-in cube UVs. Metric surface
// coordinates keep coating/brushing at the same scale as the bevelled machinery.
foreach (var t in sample.Cast<UnityEngine.Transform>().Where(t => t.GetComponent<UnityEngine.MeshFilter>() != null)) {
    var filter = t.GetComponent<UnityEngine.MeshFilter>(); var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
    var vertices = mesh.vertices; var normals = mesh.normals; var uv = new UnityEngine.Vector2[vertices.Length];
    for (int i = 0; i < vertices.Length; i++) {
        var p = UnityEngine.Vector3.Scale(vertices[i], t.localScale); var n = normals[i];
        uv[i] = UnityEngine.Mathf.Abs(n.y) > .5f ? new UnityEngine.Vector2(p.x, p.z) * .5f :
            UnityEngine.Mathf.Abs(n.x) > .5f ? new UnityEngine.Vector2(p.z, p.y) * .5f : new UnityEngine.Vector2(p.x, p.y) * .5f;
    }
    mesh.uv = uv; mesh.RecalculateTangents(); mesh.name = t.name + "_MetricUV";
    UnityEditor.AssetDatabase.CreateAsset(mesh, dir + "/" + mesh.name + ".asset"); filter.sharedMesh = mesh;
}

// Local, plausible task lighting; no global ambient/exposure boost and no fake emissive bake.
// Area lights bake direct light + bounce into the static room and the dynamic-body probes.
var lightMaterial = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit")) { name = "WorkshopLightPanel" };
lightMaterial.SetColor("_BaseColor", new UnityEngine.Color(.65f, .68f, .69f));
lightMaterial.globalIlluminationFlags = UnityEngine.MaterialGlobalIlluminationFlags.BakedEmissive; // URP validation requires AnyEmissive; fixture is not a GI contributor.
lightMaterial.SetColor("_EmissionColor", new UnityEngine.Color(2.2f, 2.12f, 1.96f)); lightMaterial.EnableKeyword("_EMISSION");
UnityEditor.AssetDatabase.CreateAsset(lightMaterial, dir + "/WorkshopLightPanel.mat");
int index = 0;
foreach (var position in new[] { new UnityEngine.Vector3(-7, 6.2f, 12.7f), new UnityEngine.Vector3(1, 6.2f, 13), new UnityEngine.Vector3(-3, 6.2f, 17) }) {
    var panel = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube); panel.name = "WorkshopCeilingPanel_" + index;
    panel.transform.SetParent(sample, false); panel.transform.position = position;
    panel.transform.localScale = new UnityEngine.Vector3(2.4f, .08f, .65f);
    UnityEngine.Object.DestroyImmediate(panel.GetComponent<UnityEngine.Collider>());
    panel.GetComponent<UnityEngine.Renderer>().sharedMaterial = lightMaterial;
    UnityEditor.GameObjectUtility.SetStaticEditorFlags(panel, 0); // Area light supplies the bake, not the decorative emissive face.
    panel.GetComponent<UnityEngine.Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    var light = new UnityEngine.GameObject("WorkshopBakedArea_" + index++).AddComponent<UnityEngine.Light>();
    light.transform.SetParent(sample, false); light.transform.position = position - UnityEngine.Vector3.up * .045f;
    light.transform.rotation = UnityEngine.Quaternion.Euler(90, 0, 0);
    light.type = UnityEngine.LightType.Rectangle; light.areaSize = new UnityEngine.Vector2(2.4f, .65f);
    light.color = new UnityEngine.Color(1, .965f, .90f); light.intensity = 6;
    light.range = 12; light.lightmapBakeType = UnityEngine.LightmapBakeType.Baked;
    light.shadows = UnityEngine.LightShadows.Soft;
}
var settings = UnityEditor.Lightmapping.lightingSettings;
settings.maxBounces = 4; settings.indirectSampleCount = 128; settings.directSampleCount = 64;
UnityEditor.EditorUtility.SetDirty(settings);
foreach (UnityEngine.Transform t in arena)
    if (t.name.Contains("Workshop") || t.name.Contains("Loading")) t.GetComponent<UnityEngine.MeshRenderer>().scaleInLightmap = 1;

var after = mapSpawns.BuildCollision();
if (!before.Solids.SequenceEqual(after.Solids) || !before.Surfaces.SequenceEqual(after.Surfaces))
    throw new System.Exception("Surface follow-up changed shared numeric collision.");
if (sample.GetComponentsInChildren<UnityEngine.Collider>().Length != 0) throw new System.Exception("Sample must not add independent colliders.");
if (sun.transform.rotation != sunRotation || sun.intensity != sunIntensity) throw new System.Exception("Do not change accepted exterior sun.");
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene); UnityEditor.AssetDatabase.SaveAssets();
return new {Scene = scene.path, Materials = 5, SourceResolution = 2048, InteriorBakedAreaLights = index,
    RemovedOversizedSign = true, CollisionUnchanged = true, ExteriorSunUnchanged = true, NeedsBake = true};
