using UnityEngine;

namespace BeMyArms.M6
{
    public enum M6WeaponKind
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
    public class M6WeaponDescriptor : MonoBehaviour
    {
        public string WeaponId = "";
        public M6WeaponKind Kind = M6WeaponKind.Primary;
        public string MountSocket = "WeaponAnchor";
    }

    /// <summary>Marks a production environment-kit piece and its kit category.</summary>
    public class M6EnvironmentDescriptor : MonoBehaviour
    {
        public string AssetId = "";
        public string KitCategory = "blockout";
    }
}
