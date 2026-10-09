// Read-only representative presentation check in smoke-duel's disposable rendered P2.
void Assert(bool ok, string message) { if (!ok) throw new System.Exception(message); }
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
Assert(scene.name == "Boatyard", "Expected revised Boatyard client");
var roots = scene.GetRootGameObjects();
var sample = roots.Single(g => g.name == "LookSample");
Assert(sample.GetComponentsInChildren<UnityEngine.Collider>().Length == 0, "Art detail must not add collision");
var volumeType = System.Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
var volume = sample.GetComponentInChildren(volumeType);
var profile = volumeType.GetField("sharedProfile").GetValue(volume);
var components = ((System.Collections.IEnumerable)profile.GetType().GetField("components").GetValue(profile)).Cast<object>();
Assert(components.Any(c => c.GetType().Name == "Tonemapping") && components.Any(c => c.GetType().Name == "ColorAdjustments"), "Authored grading components were not serialized into the player");
var cameraType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
var world = UnityEngine.GameObject.Find("Client_LocalCamera").GetComponent(cameraType);
var weapon = UnityEngine.GameObject.Find("Client_ViewModelCamera").GetComponent(cameraType);
Assert((bool)cameraType.GetProperty("renderPostProcessing").GetValue(world), "Map grading disabled on actual world camera");
Assert(!(bool)cameraType.GetProperty("renderPostProcessing").GetValue(weapon), "Accepted viewmodel should not be newly graded");
Assert(UnityEngine.LightmapSettings.lightmaps.Length > 0, "Baked lightmaps missing from player");
Assert(UnityEngine.LightmapSettings.lightProbes != null && UnityEngine.LightmapSettings.lightProbes.count > 20, "Baked dynamic-body probes missing");
Assert(sample.GetComponentsInChildren<UnityEngine.ReflectionProbe>().All(p => p.bakedTexture != null), "Reflection captures missing from player");
Assert(sample.GetComponentsInChildren<UnityEngine.Renderer>().All(r => r.sharedMaterial != null && r.sharedMaterial.shader.isSupported), "Sample material/shader missing or unsupported");
var arena = roots.Single(g => g.name == "Arena").transform;
foreach (var t in arena.Cast<UnityEngine.Transform>().Where(t => t.name.StartsWith("BMA_Map_Ramp"))) {
    var mesh = t.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
    Assert(mesh.vertexCount == mesh.uv.Length && mesh.vertexCount == mesh.tangents.Length, "Ramp surface coordinates missing");
}
return new {Success = true, Lightmaps = UnityEngine.LightmapSettings.lightmaps.Length,
    Probes = UnityEngine.LightmapSettings.lightProbes.count, WorldGrading = true, ViewmodelGrading = false};
