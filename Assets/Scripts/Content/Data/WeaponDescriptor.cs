using UnityEngine;

namespace BeMyArms.Content
{
    public enum AssetWeaponKind
    {
        Primary,
        Secondary,
        Knife,
        Utility
    }

    /// <summary>
    /// Describes a production weapon/utility asset and the contract socket it mounts at. Weapons
    /// are presentation meshes only; gameplay stats stay authoritative elsewhere.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M6", "BeMyArms.M6", "M6WeaponDescriptor")]
    public class WeaponDescriptor : MonoBehaviour
    {
        public string WeaponId = "";
        public AssetWeaponKind Kind = AssetWeaponKind.Primary;
        public string MountSocket = "WeaponAnchor";
    }
}
