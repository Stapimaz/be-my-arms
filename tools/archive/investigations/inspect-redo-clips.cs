var result = new System.Collections.Generic.List<string>();
foreach (var path in System.IO.Directory.GetFiles("Assets/Art/Characters/Pass2Redo/Animations", "*.fbx"))
{
    result.Add(path);
    foreach (var obj in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\','/')))
        if (obj is UnityEngine.AnimationClip clip) result.Add(clip.name + " duration=" + clip.length + " human=" + clip.humanMotion);
}
return result;
