var path="Assets/Scenes/M7DuelArena.unity";
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
var removed=new System.Collections.Generic.List<string>();
try {
foreach(var root in scene.GetRootGameObjects()) {
bool fixture=root.name=="Pass2PreviewCamera" || root.name=="Pass2PreviewLight"
    || root.name=="M7PlayerBody(Clone)" && root.layer==31 && root.GetComponent<BeMyArms.M3.M3DuelBody>()!=null;
if(fixture){removed.Add(root.name);UnityEngine.Object.DestroyImmediate(root);}
}
if(removed.Count>0)UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
} finally {if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
return removed;
