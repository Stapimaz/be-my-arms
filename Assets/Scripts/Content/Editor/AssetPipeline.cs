using UnityEditor;
using UnityEngine;

namespace BeMyArms.Content.EditorTools
{
    /// <summary>
    /// Entry points for the production pipeline. Rebuilds prefabs/materials from the imported
    /// Blender FBX and validates the result. Safe to call from the menu or the Unity CLI.
    /// </summary>
    public static class AssetPipeline
    {
        [MenuItem("Be My Arms/Content/Regenerate Production Assets")]
        public static void Regenerate()
        {
            Debug.Log("[Content] applying import conventions...");
            AssetDatabase.Refresh();
            int reimported = AssetImportSettings.ApplyAll();
            MaterialPalette.RebuildAll();
            AssetPrefabBuilders.BuildSharedBodyRig();
            AssetPrefabBuilders.BuildAllCharacterSkins();
            AssetPrefabBuilders.BuildAllWeapons();
            AssetPrefabBuilders.BuildAllEnvironment();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Content] regenerated production assets (reimported {reimported} models)");
        }

        [MenuItem("Be My Arms/Content/Validate Production Assets")]
        public static void ValidateFromMenu()
        {
            ValidationReport report = AssetValidator.ValidateAll();
            if (report.IsValid) Debug.Log("[Content] production asset validation PASSED\n" + report);
            else Debug.LogError("[Content] production asset validation FAILED\n" + report);
        }

        /// <summary>String-returning entry point for CLI/eval and tests.</summary>
        public static string Validate()
        {
            ValidationReport report = AssetValidator.ValidateAll();
            return report.IsValid ? "Content VALID\n" + report : "Content INVALID\n" + report;
        }

        public static string RegenerateAndValidate()
        {
            Regenerate();
            return Validate();
        }
    }
}
