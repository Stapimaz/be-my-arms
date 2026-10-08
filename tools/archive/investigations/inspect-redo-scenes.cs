var result=new System.Collections.Generic.List<object>();
foreach(var path in new[]{"Assets/Scenes/M7MainMenu.unity","Assets/Scenes/M7DuelArena.unity","Assets/Scenes/M7TwoVsTwoArena.unity"}) {
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
try {
foreach(var root in scene.GetRootGameObjects()) {
var bodies=root.GetComponentsInChildren<BeMyArms.M3.M3DuelBody>(true);
var cameras=root.GetComponentsInChildren<UnityEngine.Camera>(true);
if(bodies.Length>0 || cameras.Length>0 || root.name.Contains("Preview"))result.Add(new {path,root.name,layer=root.layer,bodies=bodies.Length,cameras=System.Array.ConvertAll(cameras,c=>c.name)});
}
} finally {if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
}
return result;
