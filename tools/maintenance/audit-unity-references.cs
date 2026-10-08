// Run in the connected Editor. No asset generation, Play mode or visual judgement.
var errors=new System.Collections.Generic.List<string>();
var assets=new System.Collections.Generic.List<object>();
var guidPaths=new System.Collections.Generic.Dictionary<string,string>();
foreach(var path in UnityEditor.AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/"))) {
    string guid=UnityEditor.AssetDatabase.AssetPathToGUID(path);
    if(!string.IsNullOrEmpty(guid))guidPaths[guid]=path;
    string extension=System.IO.Path.GetExtension(path);
    if(!new[]{".unity",".prefab",".asset",".mat",".controller"}.Contains(extension))continue;
    if(!System.IO.File.Exists(path))continue;
    // GUID resolution catches missing references omitted by GetDependencies().
    string text=System.IO.File.ReadAllText(path);
    foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text,@"guid: ([a-f0-9]{32})")) {
        string reference=m.Groups[1].Value;
        if(reference.StartsWith("0000000000000000"))continue; // Unity built-ins
        if(string.IsNullOrEmpty(UnityEditor.AssetDatabase.GUIDToAssetPath(reference)))errors.Add(path+": unresolved GUID "+reference);
    }
    assets.Add(new {Path=path,Guid=guid,Dependencies=UnityEditor.AssetDatabase.GetDependencies(path,false).Select(UnityEditor.AssetDatabase.AssetPathToGUID).OrderBy(g=>g).ToArray()});
}
void CheckRoot(UnityEngine.GameObject root,string path) {
    foreach(var t in root.GetComponentsInChildren<UnityEngine.Transform>(true)) {
        int missing=UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        if(missing>0)errors.Add(path+": "+t.name+" has "+missing+" missing scripts");
        foreach(var component in t.GetComponents<UnityEngine.Component>())if(component!=null) {
            var serialized=new UnityEditor.SerializedObject(component);
            var property=serialized.GetIterator();
            while(property.Next(true))if(property.propertyType==UnityEditor.SerializedPropertyType.ObjectReference && property.objectReferenceValue==null && property.objectReferenceInstanceIDValue!=0)
                errors.Add(path+": "+t.name+"."+property.propertyPath+" has a broken object reference");
        }
    }
}
foreach(string path in guidPaths.Values.Where(p=>p.EndsWith(".prefab"))) {
    UnityEngine.GameObject root=null;
    try{root=UnityEditor.PrefabUtility.LoadPrefabContents(path);CheckRoot(root,path);}
    catch(System.Exception ex){errors.Add(path+": "+ex.Message);}
    finally{if(root!=null)UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
}
foreach(string path in guidPaths.Values.Where(p=>p.EndsWith(".unity"))) {
    var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(path);
    try{foreach(var root in scene.GetRootGameObjects())CheckRoot(root,path);}
    finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
}
var report=new {Errors=errors.Distinct().OrderBy(e=>e).ToArray(),GuidPaths=guidPaths,Assets=assets,BuildScenes=UnityEditor.EditorBuildSettings.scenes.Select(s=>new {s.path,s.enabled}).ToArray()};
System.IO.Directory.CreateDirectory("Builds/Normalization");
System.IO.File.WriteAllText("Builds/Normalization/reference-audit.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return new {AssetCount=assets.Count,GuidCount=guidPaths.Count,Errors=report.Errors};
