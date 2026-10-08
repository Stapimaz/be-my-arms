// Windows case-only rename goes through a temporary AssetDatabase path, preserving GUIDs.
if(UnityEditor.AssetDatabase.IsValidFolder("Assets/Scenes/SampleS")) {
    string error=UnityEditor.AssetDatabase.MoveAsset("Assets/Scenes/SampleS","Assets/Scenes/SceneSamplesTemp");
    if(!string.IsNullOrEmpty(error))throw new System.Exception(error);
    error=UnityEditor.AssetDatabase.MoveAsset("Assets/Scenes/SceneSamplesTemp","Assets/Scenes/Samples");
    if(!string.IsNullOrEmpty(error))throw new System.Exception(error);
}
UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);
return "Normalization imports refreshed";
