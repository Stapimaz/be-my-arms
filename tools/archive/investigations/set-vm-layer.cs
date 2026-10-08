var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
var so = new UnityEditor.SerializedObject(assets[0]);
var layers = so.FindProperty("layers");
var element = layers.GetArrayElementAtIndex(9);
if (element.stringValue != "ViewModel")
{
    element.stringValue = "ViewModel";
    so.ApplyModifiedProperties();
    UnityEditor.AssetDatabase.SaveAssets();
}
UnityEngine.Debug.Log("[layer] 9=" + layers.GetArrayElementAtIndex(9).stringValue);
return layers.GetArrayElementAtIndex(9).stringValue;
