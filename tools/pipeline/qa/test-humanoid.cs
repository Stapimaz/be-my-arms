var path = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx";
var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
importer.animationType = UnityEditor.ModelImporterAnimationType.Human;
importer.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CreateFromThisModel;
importer.SaveAndReimport();
var sb = new System.Text.StringBuilder();
sb.Append("animType=").Append(importer.animationType).Append(" avatarSetup=").Append(importer.avatarSetup).Append('\n');
UnityEngine.Avatar avatar = null;
int clipCount = 0;
foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
{
    if (a is UnityEngine.Avatar av) avatar = av;
    if (a is UnityEngine.AnimationClip c && !c.name.StartsWith("__preview__")) clipCount++;
}
sb.Append("clips=").Append(clipCount).Append('\n');
if (avatar != null)
{
    sb.Append("avatar=").Append(avatar.name).Append(" isValid=").Append(avatar.isValid).Append(" isHuman=").Append(avatar.isHuman).Append('\n');
    var hd = avatar.humanDescription;
    int mapped = 0;
    foreach (var h in hd.human) if (h.boneName != "") mapped++;
    sb.Append("humanEntries=").Append(hd.human.Length).Append(" mapped=").Append(mapped).Append('\n');
    foreach (var h in hd.human)
        if (!string.IsNullOrEmpty(h.humanName) && string.IsNullOrEmpty(h.boneName))
            sb.Append("  UNMAPPED ").Append(h.humanName).Append('\n');
}
UnityEngine.Debug.Log("[humanoid]\n" + sb);
return sb.ToString();
