using System.Collections.Generic;

namespace BeMyArms.Product
{
    public enum SocketKind
    {
        Gameplay,
        CosmeticMount,
        CameraAnchor,
        WeaponAnchor,
        UtilityAnchor
    }

    /// <summary>A standardized transform the contract requires, with its expected parent path.</summary>
    public class SocketSpec
    {
        public string Name;
        public string ParentPath;
        public SocketKind Kind;

        public SocketSpec(string name, string parentPath, SocketKind kind)
        {
            Name = name;
            ParentPath = parentPath;
            Kind = kind;
        }
    }

    /// <summary>An authoritative damage region. Cosmetic meshes must never add or change one.</summary>
    public class HitboxSpec
    {
        public string Name;
        public string Region;
        public bool Critical;

        public HitboxSpec(string name, string region, bool critical)
        {
            Name = name;
            Region = region;
            Critical = critical;
        }
    }

    /// <summary>
    /// The standardized P1/P2 rig contract (concept §5.1, TECHNICAL_PLAN §5): gameplay skeleton,
    /// attachment/camera/weapon/utility anchors, authoritative hitboxes and the animation interface.
    /// Every cosmetic skin is authored against this contract so any valid P1 skin combines with any
    /// valid P2 skin without pair-specific work, and cosmetics can never move gameplay geometry.
    /// </summary>
    public class RigContract
    {
        public string RootName = "SharedBody";
        public readonly List<SocketSpec> Sockets = new List<SocketSpec>();
        public readonly List<HitboxSpec> Hitboxes = new List<HitboxSpec>();
        /// <summary>Socket the P1 cosmetic layer is mounted under.</summary>
        public string P1CosmeticSocket = "Cosmetic_P1";
        /// <summary>Socket the P2 cosmetic layer is mounted under.</summary>
        public string P2CosmeticSocket = "Cosmetic_P2";

        public static RigContract Default()
        {
            var c = new RigContract();
            // Parent paths are relative to the pipeline root (RootName), excluded from the path.
            c.Sockets.Add(new SocketSpec("Hips", "", SocketKind.Gameplay));
            c.Sockets.Add(new SocketSpec("Chest", "Hips", SocketKind.Gameplay));
            c.Sockets.Add(new SocketSpec("Neck", "Hips/Chest", SocketKind.Gameplay));
            c.Sockets.Add(new SocketSpec("Head", "Hips/Chest/Neck", SocketKind.Gameplay));
            c.Sockets.Add(new SocketSpec("ShoulderAnchor", "Hips/Chest", SocketKind.Gameplay));
            c.Sockets.Add(new SocketSpec("WeaponAnchor", "Hips/Chest", SocketKind.WeaponAnchor));
            c.Sockets.Add(new SocketSpec("UtilityAnchor", "Hips/Chest", SocketKind.UtilityAnchor));
            c.Sockets.Add(new SocketSpec("P2CameraAnchor", "Hips/Chest", SocketKind.CameraAnchor));
            c.Sockets.Add(new SocketSpec("P1CameraAnchor", "Hips/Chest/Neck", SocketKind.CameraAnchor));
            c.Sockets.Add(new SocketSpec("Cosmetic_P1", "", SocketKind.CosmeticMount));
            c.Sockets.Add(new SocketSpec("Cosmetic_P2", "Hips/Chest", SocketKind.CosmeticMount));

            c.Hitboxes.Add(new HitboxSpec("Hitbox_Head", "Head", critical: true));
            c.Hitboxes.Add(new HitboxSpec("Hitbox_Body", "Body", critical: false));
            return c;
        }

        public SocketSpec FindSocket(string name)
        {
            for (int i = 0; i < Sockets.Count; i++)
                if (Sockets[i].Name == name) return Sockets[i];
            return null;
        }
    }
}
