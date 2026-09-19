using UnityEditor;
using UnityEngine;

namespace BeMyArms.M6.EditorTools
{
    /// <summary>
    /// Entry points for the production pipeline. Rebuilds prefabs/materials from the imported
    /// Blender FBX and validates the result. Safe to call from the menu or the Unity CLI.
    /// </summary>
    public static class M6PipelineCommands
    {
        [MenuItem("Be My Arms/M6/Regenerate Production Assets")]
        public static void Regenerate()
        {
            Debug.Log("[M6] applying import conventions...");
            AssetDatabase.Refresh();
            int reimported = M6AssetImportSettings.ApplyAll();
            M6MaterialPalette.RebuildAll();
            M6PrefabBuilders.BuildSharedBodyRig();
            M6PrefabBuilders.BuildAllCharacterSkins();
            M6PrefabBuilders.BuildAllWeapons();
            M6PrefabBuilders.BuildAllEnvironment();
            AssetDatabase.SaveAssets();
            Debug.Log($"[M6] regenerated production assets (reimported {reimported} models)");
        }

        [MenuItem("Be My Arms/M6/Validate Production Assets")]
        public static void ValidateFromMenu()
        {
            M6ValidationReport report = M6AssetValidator.ValidateAll();
            if (report.IsValid) Debug.Log("[M6] production asset validation PASSED\n" + report);
            else Debug.LogError("[M6] production asset validation FAILED\n" + report);
        }

        /// <summary>String-returning entry point for CLI/eval and tests.</summary>
        public static string Validate()
        {
            M6ValidationReport report = M6AssetValidator.ValidateAll();
            return report.IsValid ? "M6 VALID\n" + report : "M6 INVALID\n" + report;
        }

        public static string RegenerateAndValidate()
        {
            Regenerate();
            return Validate();
        }
    }
}
