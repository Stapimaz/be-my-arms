using UnityEngine;

namespace BeMyArms.M6
{
    /// <summary>
    /// The standardized shared-body anchor layout, in Unity space (Y up, +Z forward, metres).
    /// Local values are relative to the named parent; this mirrors
    /// <c>art/blender/scripts/bma_common.py</c> (RIG) and is the single source of truth the
    /// production rig prefab and the Blender-authored skins are both built against.
    /// </summary>
    public static class M6RigLayout
    {
        public const string RootName = "SharedBody";
        public const string RigId = "default";

        // World-space anchor targets (metres).
        public static readonly Vector3 HipsWorld = new Vector3(0f, 0.95f, 0f);
        public static readonly Vector3 ChestWorld = new Vector3(0f, 1.35f, 0f);
        public static readonly Vector3 NeckWorld = new Vector3(0f, 1.55f, 0f);
        public static readonly Vector3 HeadWorld = new Vector3(0f, 1.68f, 0f);
        public static readonly Vector3 ShoulderAnchorWorld = new Vector3(0f, 1.48f, 0.06f);
        public static readonly Vector3 WeaponAnchorWorld = new Vector3(0f, 1.35f, 0.35f);
        public static readonly Vector3 UtilityAnchorWorld = new Vector3(0f, 1.15f, 0.22f);
        public static readonly Vector3 P2CameraAnchorWorld = new Vector3(0f, 1.50f, 0.16f);
        public static readonly Vector3 P1CameraAnchorWorld = new Vector3(0f, 1.68f, 0.06f);

        // Parent-relative local positions.
        public static readonly Vector3 Hips = HipsWorld;
        public static readonly Vector3 Chest = new Vector3(0f, 0.40f, 0f);
        public static readonly Vector3 Neck = new Vector3(0f, 0.20f, 0f);
        public static readonly Vector3 Head = new Vector3(0f, 0.13f, 0f);
        public static readonly Vector3 ShoulderAnchor = new Vector3(0f, 0.13f, 0.06f);
        public static readonly Vector3 WeaponAnchor = new Vector3(0f, 0f, 0.35f);
        public static readonly Vector3 UtilityAnchor = new Vector3(0f, -0.20f, 0.22f);
        public static readonly Vector3 P2CameraAnchor = new Vector3(0f, 0.15f, 0.16f);
        public static readonly Vector3 P1CameraAnchor = new Vector3(0f, 0.13f, 0.06f);
        public static readonly Vector3 CosmeticP1 = Vector3.zero;
        public static readonly Vector3 CosmeticP2 = new Vector3(0f, 0.13f, 0.06f);

        public static readonly Vector3 HitboxHeadScale = new Vector3(0.30f, 0.30f, 0.30f);
        public static readonly Vector3 HitboxBodyScale = new Vector3(0.60f, 0.55f, 0.34f);
    }
}
