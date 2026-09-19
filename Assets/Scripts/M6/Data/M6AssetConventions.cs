using UnityEngine;

namespace BeMyArms.M6
{
    /// <summary>
    /// Production asset conventions: folder layout, naming prefixes and expected dimensions.
    /// The pipeline validates imported assets against these so a mismatched scale, orientation
    /// or name fails fast instead of silently breaking the rig.
    /// </summary>
    public static class M6AssetConventions
    {
        public const string ArtRoot = "Assets/Art";
        // The rig root GameObject must be named "SharedBody" (the M5 contract), so the prefab file
        // is named to match rather than using the cosmetic BMA_ prefix.
        public const string RigPrefabPath = "Assets/Art/Rigs/SharedBody.prefab";
        public const string MaterialsDir = "Assets/Art/Materials";

        public const string P1Dir = "Assets/Art/Characters/P1";
        public const string P2Dir = "Assets/Art/Characters/P2";
        public const string WeaponDir = "Assets/Art/Weapons";
        public const string EnvironmentDir = "Assets/Art/Environment";
        public const string PrefabSubdir = "Prefabs";

        public const string P1Prefix = "BMA_P1_";
        public const string P2Prefix = "BMA_P2_";
        public const string WeaponPrefix = "BMA_Weapon_";
        public const string UtilityPrefix = "BMA_Utility_";
        public const string EnvironmentPrefix = "BMA_Env_";
        public const string MaterialPrefix = "BMA_";

        // Expected world-space extents (metres). Tolerant: these are production *tests*, not final art.
        public static readonly Vector2 P1HeightRange = new Vector2(1.60f, 2.10f);
        public static readonly Vector2 P2SizeRange = new Vector2(0.40f, 1.25f);
        public static readonly Vector2 WeaponLengthRange = new Vector2(0.70f, 1.40f);
        public static readonly Vector2 UtilitySizeRange = new Vector2(0.05f, 0.40f);
        public static readonly Vector2 EnvironmentPieceMax = new Vector2(0.5f, 6.0f);
        public const float DimensionTolerance = 0.20f;

        // LOD0 triangle budgets. LOD1/LOD2 are authored in M7+ when content scale begins; for now the
        // pipeline enforces a sane LOD0 ceiling so assets cannot silently balloon.
        public const int P1TriangleBudget = 30000;
        public const int P2TriangleBudget = 20000;
        public const int WeaponTriangleBudget = 10000;
        public const int EnvironmentTriangleBudget = 8000;

        public static string PrefabPath(string modelDir, string assetName)
            => $"{modelDir}/{PrefabSubdir}/{assetName}.prefab";
    }
}
