// Names only; preserve every object, component, serialized reference and tuning value.
var prefixes=new System.Collections.Generic.Dictionary<string,string> {
    {"M0_","Core_"},{"M1_","Gameplay_"},{"M2_","Networking_"},{"M3_","Match_"},
    {"M4_","Matchmaking_"},{"M5_","Product_"},{"M6_","Content_"},{"M7_","Client_"}
};
int renamed=0;
foreach(var path in UnityEditor.AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"}).Select(UnityEditor.AssetDatabase.GUIDToAssetPath)) {
    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid() || !scene.isLoaded;
    if(!opened && scene.isDirty)throw new System.Exception("Refusing to overwrite dirty scene: "+path);
    if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
    bool dirty=false;
    try {
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<UnityEngine.Transform>(true))
            foreach(var prefix in prefixes)if(t.name.StartsWith(prefix.Key)) {
                t.gameObject.name=prefix.Value+t.name.Substring(prefix.Key.Length);renamed++;dirty=true;break;
            }
        if(dirty){UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);}
    }finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
}
return new {Renamed=renamed};
