// One-time migration. Update serialized path/scene/resource strings using Unity APIs.
var manifest=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Builds/Normalization/manifest.json"));
var strings=((Newtonsoft.Json.Linq.JObject)manifest["Strings"]).Properties().ToDictionary(p=>p.Name,p=>(string)p.Value);
var assemblies=((Newtonsoft.Json.Linq.JObject)manifest["Assemblies"]).Properties().OrderByDescending(p=>p.Name.Length).ToArray();
var types=((Newtonsoft.Json.Linq.JObject)manifest["Types"]).Properties().ToDictionary(p=>p.Name,p=>(string)p.Value);
bool UpdateObject(UnityEngine.Object obj) {
    if(obj==null)return false;
    bool dirty=false;var serialized=new UnityEditor.SerializedObject(obj);var property=serialized.GetIterator();
    while(property.Next(true))if(property.propertyType==UnityEditor.SerializedPropertyType.String) {
        string value=property.stringValue,next=value;
        if(strings.TryGetValue(value,out string mapped))next=mapped;
        else if(property.name=="m_EditorClassIdentifier") {
            foreach(var a in assemblies)next=next.Replace(a.Name,(string)a.Value);
            next=System.Text.RegularExpressions.Regex.Replace(next,@"\bI?M(?:0[5]?|[1-7])[A-Z]\w*\b",m=>types.TryGetValue(m.Value,out string type)?type:m.Value);
        }
        if(next!=value){property.stringValue=next;dirty=true;}
    }
    if(dirty){serialized.ApplyModifiedPropertiesWithoutUndo();UnityEditor.EditorUtility.SetDirty(obj);}return dirty;
}
bool UpdateRoot(UnityEngine.GameObject root) {
    bool dirty=false;
    foreach(var t in root.GetComponentsInChildren<UnityEngine.Transform>(true)) {
        dirty|=UpdateObject(t.gameObject);
        foreach(var component in t.GetComponents<UnityEngine.Component>())dirty|=UpdateObject(component);
    }
    return dirty;
}
var changed=new System.Collections.Generic.List<string>();
var paths=UnityEditor.AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")).ToArray();
foreach(string path in paths.Where(p=>p.EndsWith(".prefab"))) {
    var root=UnityEditor.PrefabUtility.LoadPrefabContents(path);
    try {
        bool dirty=UpdateRoot(root);
        // Existing environment metadata was saved with a null script because two components
        // shared one source filename. Split files now; repair only those four metadata roots.
        if(path.StartsWith("Assets/Art/Environment/Prefabs/BMA_Env_") && UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root)==1) {
            UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            var descriptor=root.AddComponent<BeMyArms.Content.EnvironmentDescriptor>();descriptor.AssetId=root.name;descriptor.KitCategory="blockout";dirty=true;
        }
        if(dirty){UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,path);changed.Add(path);}
    }finally{UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
}
foreach(string path in paths.Where(p=>p.EndsWith(".asset") || p.EndsWith(".controller"))) {
    bool dirty=false;
    foreach(var obj in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))dirty|=UpdateObject(obj);
    if(dirty)changed.Add(path);
}
foreach(string path in paths.Where(p=>p.EndsWith(".unity"))) {
    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid() || !scene.isLoaded;
    if(!opened && scene.isDirty)throw new System.Exception("Refusing to overwrite an already dirty scene: "+path);
    if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
    try {
        bool dirty=false;foreach(var root in scene.GetRootGameObjects())dirty|=UpdateRoot(root);
        if(dirty){UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);changed.Add(path);}
    }finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
}
UnityEditor.EditorBuildSettings.scenes=new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/DuelArena.unity","Assets/Scenes/TwoVsTwoArena.unity"}.Select(p=>new UnityEditor.EditorBuildSettingsScene(p,true)).ToArray();
UnityEditor.AssetDatabase.SaveAssets();
// URP upgrade left removed legacy debug/effect fields; let Unity discard obsolete serialization.
UnityEditor.AssetDatabase.ForceReserializeAssets(new[]{"Assets/Settings/PC_Renderer.asset","Assets/Settings/DefaultVolumeProfile.asset"});
System.IO.File.WriteAllLines("Builds/Normalization/serialized-name-migration.txt",changed);
return new {Updated=changed.Count,Changed=changed};
