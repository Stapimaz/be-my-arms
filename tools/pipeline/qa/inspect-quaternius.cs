var path = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx";
var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
UnityEngine.Debug.Log("[q] importer=" + (importer != null));
if (importer != null)
{
    importer.animationType = UnityEditor.ModelImporterAnimationType.Generic;
    importer.importAnimation = true;
    importer.importCameras = false;
    importer.importLights = false;
    importer.SaveAndReimport();
}
var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
var sb = new System.Text.StringBuilder();
System.Action<UnityEngine.Transform, int> dump = null;
dump = (t, d) =>
{
    sb.Append(' ', d * 2).Append(t.name).Append("  local=").Append(t.localPosition.ToString("0.###"))
      .Append(" rot=").Append(t.localRotation.eulerAngles.ToString("0.#")).Append(" scale=").Append(t.localScale.ToString("0.###"));
    foreach (var c in t.GetComponents<UnityEngine.Component>()) sb.Append(" [").Append(c.GetType().Name).Append(']');
    sb.Append('\n');
    for (int i = 0; i < t.childCount; i++) dump(t.GetChild(i), d + 1);
};
dump(go.transform, 0);
UnityEngine.Debug.Log("[q:tree]\n" + sb);
var clips = UnityEditor.AnimationUtility.GetAnimationClips(go);
sb.Clear();
foreach (var c in clips)
{
    var bindings = UnityEditor.AnimationUtility.GetCurveBindings(c);
    sb.Append(c.name).Append("  dur=").Append(c.length.ToString("0.00")).Append(" curves=").Append(bindings.Length).Append("  rootPath='").Append(bindings.Length > 0 ? bindings[0].path : "").Append("'\n");
}
UnityEngine.Debug.Log("[q:clips]\n" + sb);
return "ok clips=" + clips.Length;
