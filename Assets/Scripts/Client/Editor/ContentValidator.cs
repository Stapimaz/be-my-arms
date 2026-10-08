using System.Collections.Generic;
using System.IO;
using BeMyArms.Client;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.Client.EditorTools
{
    /// <summary>
    /// Gathers the real project contents into an <see cref="ContentSnapshot"/> and evaluates the
    /// shippable match-set requirements. Combines the Content character/weapon/environment assets, the Client
    /// map kit, audio and VFX.
    /// </summary>
    public static class ContentValidator
    {
        public static ContentSnapshot GatherSnapshot()
        {
            var snapshot = new ContentSnapshot();

            foreach (string guid in AssetDatabase.FindAssets("t:MapDefinition"))
            {
                var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (map == null) continue;
                if (map.Family == MapFamily.Duel) snapshot.DuelMaps++;
                else snapshot.TwoVsTwoMaps++;
            }

            snapshot.P1Skins = CountPrefabs("Assets/Art/Characters/P1/Prefabs");
            snapshot.P2Skins = CountPrefabs("Assets/Art/Characters/P2/Prefabs");
            snapshot.Weapons = CountPrefabs("Assets/Art/Weapons/Prefabs", "BMA_Weapon_");
            snapshot.Utility = CountPrefabs("Assets/Art/Weapons/Prefabs", "BMA_Utility_");
            snapshot.EnvironmentPieces = CountPrefabs("Assets/Art/Environment/Prefabs");
            snapshot.MapKitPieces = CountPrefabs(MapPrefabBuilder.PrefabDir);
            snapshot.VfxEffects = CountPrefabs(VfxPrefabBuilder.PrefabDir);

            foreach (string clip in AudioImportSettings.ClipNames())
            {
                if (clip.StartsWith("sfx_")) snapshot.SfxClips++;
                else if (clip.StartsWith("amb_") || clip.StartsWith("music_")) snapshot.MusicClips++;
            }

            return snapshot;
        }

        public static ContentReport Evaluate()
            => ContentManifest.Evaluate(GatherSnapshot(), new ContentRequirements());

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
