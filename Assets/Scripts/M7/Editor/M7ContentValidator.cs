using System.Collections.Generic;
using System.IO;
using BeMyArms.M7;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>
    /// Gathers the real project contents into an <see cref="M7ContentSnapshot"/> and evaluates the
    /// shippable match-set requirements. Combines the M6 character/weapon/environment assets, the M7
    /// map kit, audio and VFX.
    /// </summary>
    public static class M7ContentValidator
    {
        public static M7ContentSnapshot GatherSnapshot()
        {
            var snapshot = new M7ContentSnapshot();

            foreach (string guid in AssetDatabase.FindAssets("t:M7MapDefinition"))
            {
                var map = AssetDatabase.LoadAssetAtPath<M7MapDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (map == null) continue;
                if (map.Family == M7MapFamily.Duel) snapshot.DuelMaps++;
                else snapshot.TwoVsTwoMaps++;
            }

            snapshot.P1Skins = CountPrefabs("Assets/Art/Characters/P1/Prefabs");
            snapshot.P2Skins = CountPrefabs("Assets/Art/Characters/P2/Prefabs");
            snapshot.Weapons = CountPrefabs("Assets/Art/Weapons/Prefabs", "BMA_Weapon_");
            snapshot.Utility = CountPrefabs("Assets/Art/Weapons/Prefabs", "BMA_Utility_");
            snapshot.EnvironmentPieces = CountPrefabs("Assets/Art/Environment/Prefabs");
            snapshot.MapKitPieces = CountPrefabs(M7MapPrefabBuilder.PrefabDir);
            snapshot.VfxEffects = CountPrefabs(M7VfxPrefabBuilder.PrefabDir);

            foreach (string clip in M7AudioImportSettings.ClipNames())
            {
                if (clip.StartsWith("sfx_")) snapshot.SfxClips++;
                else if (clip.StartsWith("amb_") || clip.StartsWith("music_")) snapshot.MusicClips++;
            }

            return snapshot;
        }

        public static M7ContentReport Evaluate()
            => M7ContentManifest.Evaluate(GatherSnapshot(), new M7ContentRequirements());

        static int CountPrefabs(string folder, string prefix = null)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return 0;
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                if (prefix == null || name.StartsWith(prefix)) count++;
            }
            return count;
        }
    }
}
