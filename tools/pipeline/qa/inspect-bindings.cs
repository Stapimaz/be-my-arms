var path = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx";
var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
importer.animationType = UnityEditor.ModelImporterAnimationType.Generic;
importer.SaveAndReimport();
var sb = new System.Text.StringBuilder();
foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
{
    if (!(a is UnityEngine.AnimationClip c) || c.name.StartsWith("__preview__")) continue;
    var bindings = UnityEditor.AnimationUtility.GetCurveBindings(c);
    sb.Append(c.name).Append(" legacy=").Append(c.legacy).Append(" curves=").Append(bindings.Length).Append('\n');
    for (int i = 0; i < bindings.Length && i < 4; i++)
        sb.Append("   path='").Append(bindings[i].path).Append("' type=").Append(bindings[i].propertyName).Append(" root=").Append(bindings[i].type != null ? bindings[i].type.Name : "null").Append('\n');
}
UnityEngine.Debug.Log("[bindings]\n" + sb);
return sb.ToString();
