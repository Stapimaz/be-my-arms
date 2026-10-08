using BeMyArms.Content.EditorTools;
using BeMyArms.Client;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.Client.EditorTools
{
    /// <summary>Entry points for the Client content pipeline (maps, audio, VFX, match-set validation).</summary>
    public static class ContentPipeline
    {
        [MenuItem("Be My Arms/Client/Regenerate Content")]
        public static void Regenerate()
        {
            Debug.Log("[Client] regenerating content...");
            AssetDatabase.Refresh();
            AssetImportSettings.ApplyAll();
            AudioImportSettings.ApplyAll();
            AudioLibraryBuilder.Build();
            VfxPrefabBuilder.BuildAll();
            VfxLibraryBuilder.Build();
            ArenaSceneBuilder.BuildAll();
            MenuSceneBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("[Client] content regenerated");
        }

        [MenuItem("Be My Arms/Client/Validate Content")]
        public static void ValidateFromMenu()
        {
            MapValidationReport maps = MapValidator.ValidateAll();
            ContentReport content = ContentValidator.Evaluate();
            if (maps.IsValid && content.IsValid) Debug.Log("[Client] content validation PASSED\n" + maps + "\n" + content);
            else Debug.LogError("[Client] content validation FAILED\n" + maps + "\n" + content);
        }

        public static string Validate()
        {
            MapValidationReport maps = MapValidator.ValidateAll();
            ContentReport content = ContentValidator.Evaluate();
            bool valid = maps.IsValid && content.IsValid;
            return (valid ? "Client VALID\n" : "Client INVALID\n") + maps + "\n" + content;
        }

        public static string RegenerateAndValidate()
        {
            Regenerate();
            return Validate();
        }
    }
}
