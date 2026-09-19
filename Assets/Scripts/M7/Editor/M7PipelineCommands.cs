using BeMyArms.M6.EditorTools;
using BeMyArms.M7;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>Entry points for the M7 content pipeline (maps, audio, VFX, match-set validation).</summary>
    public static class M7PipelineCommands
    {
        [MenuItem("Be My Arms/M7/Regenerate Content")]
        public static void Regenerate()
        {
            Debug.Log("[M7] regenerating content...");
            AssetDatabase.Refresh();
            M6AssetImportSettings.ApplyAll();
            M7AudioImportSettings.ApplyAll();
            M7AudioLibraryBuilder.Build();
            M7VfxPrefabBuilder.BuildAll();
            M7VfxLibraryBuilder.Build();
            M7ArenaSceneBuilder.BuildAll();
            AssetDatabase.SaveAssets();
            Debug.Log("[M7] content regenerated");
        }

        [MenuItem("Be My Arms/M7/Validate Content")]
        public static void ValidateFromMenu()
        {
            M7MapValidationReport maps = M7MapValidator.ValidateAll();
            M7ContentReport content = M7ContentValidator.Evaluate();
            if (maps.IsValid && content.IsValid) Debug.Log("[M7] content validation PASSED\n" + maps + "\n" + content);
            else Debug.LogError("[M7] content validation FAILED\n" + maps + "\n" + content);
        }

        public static string Validate()
        {
            M7MapValidationReport maps = M7MapValidator.ValidateAll();
            M7ContentReport content = M7ContentValidator.Evaluate();
            bool valid = maps.IsValid && content.IsValid;
            return (valid ? "M7 VALID\n" : "M7 INVALID\n") + maps + "\n" + content;
        }

        public static string RegenerateAndValidate()
        {
            Regenerate();
            return Validate();
        }
    }
}
