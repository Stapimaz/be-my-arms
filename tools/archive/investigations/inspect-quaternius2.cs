var path = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx";
var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
UnityEngine.Debug.Log("[q2] animType=" + importer.animationType + " importAnim=" + importer.importAnimation + " clips=" + importer.clipAnimations.Length + " defaultClips=" + importer.defaultClipAnimations.Length);
var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
var sb = new System.Text.StringBuilder();
foreach (var a in assets)
{
    sb.Append(a.GetType().Name).Append(" : ").Append(a.name).Append('\n');
}
UnityEngine.Debug.Log("[q2:assets]\n" + sb);
return "assets=" + assets.Length;
