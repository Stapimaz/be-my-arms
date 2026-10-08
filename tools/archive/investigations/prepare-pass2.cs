BeMyArms.M7.EditorTools.M7PlayerBodyBuilder.EnsurePrefabs(out var body, out var director);
BeMyArms.M7.EditorTools.M7AudioImportSettings.ApplyAll();
BeMyArms.M7.EditorTools.M7AudioLibraryBuilder.Build();
BeMyArms.M7.EditorTools.M7VfxPrefabBuilder.BuildAll();
BeMyArms.M7.EditorTools.M7VfxLibraryBuilder.Build();
UnityEditor.AssetDatabase.SaveAssets();
return new { Body = UnityEditor.AssetDatabase.GetAssetPath(body), Director = UnityEditor.AssetDatabase.GetAssetPath(director) };
