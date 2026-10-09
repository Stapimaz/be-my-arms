// Live Editor only. Refuses Play mode/additive scenes to avoid baking another map's lighting.
if (!BeMyArms.Client.EditorTools.BoatyardLookSample.Bake())
    throw new System.Exception("Bake returned without usable lightmaps/probes.");
var sample = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "LookSample");
var reflections = sample.GetComponentsInChildren<UnityEngine.ReflectionProbe>();
if (reflections.Any(p => p.bakedTexture == null))
    throw new System.Exception("Bake completed without reflection captures.");
return new {Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
    Lightmaps = UnityEngine.LightmapSettings.lightmaps.Length,
    Probes = UnityEngine.LightmapSettings.lightProbes.count,
    Reflections = reflections.Select(p => new {p.name, Texture = UnityEditor.AssetDatabase.GetAssetPath(p.bakedTexture)}).ToArray(),
    LightingData = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Lightmapping.lightingDataAsset)};
