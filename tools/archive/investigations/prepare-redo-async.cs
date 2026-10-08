UnityEditor.EditorApplication.delayCall += () => {
    try {
        BeMyArms.M7.EditorTools.M7PlayerBodyBuilder.EnsurePrefabs(out var body, out var director);
        UnityEditor.AssetDatabase.SaveAssets();
        System.IO.File.WriteAllText("Builds/pass2-redo-prepare.txt", "Completed " + System.DateTime.UtcNow.ToString("O"));
    } catch(System.Exception e) {
        System.IO.File.WriteAllText("Builds/pass2-redo-prepare.txt",e.ToString());
        UnityEngine.Debug.LogException(e);
    }
};
return "Queued smooth-fighter preparation";
