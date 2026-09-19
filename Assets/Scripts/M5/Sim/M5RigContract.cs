using System.Collections.Generic;

namespace BeMyArms.M5
{
    public enum M5SocketKind
    {
        Gameplay,
        CosmeticMount,
        CameraAnchor,
        WeaponAnchor,
        UtilityAnchor
    }

    /// <summary>A standardized transform the contract requires, with its expected parent path.</summary>
    public class M5SocketSpec
    {
        public string Name;
        public string ParentPath;
        public M5SocketKind Kind;

        public M5SocketSpec(string name, string parentPath, M5SocketKind kind)
        {
            Name = name;
            ParentPath = parentPath;
            Kind = kind;
        }
    }

    /// <summary>An authoritative damage region. Cosmetic meshes must never add or change one.</summary>
    public class M5HitboxSpec
    {
        public string Name;
        public string Region;
        public bool Critical;

        public M5HitboxSpec(string name, string region, bool critical)
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
    public class M5RigContract
    {
        public string RootName = "SharedBody";
        public readonly List<M5SocketSpec> Sockets = new List<M5SocketSpec>();
        public readonly List<M5HitboxSpec> Hitboxes = new List<M5HitboxSpec>();
        /// <summary>Socket the P1 cosmetic layer is mounted under.</summary>
        public string P1CosmeticSocket = "Cosmetic_P1";
        /// <summary>Socket the P2 cosmetic layer is mounted under.</summary>
        public string P2CosmeticSocket = "Cosmetic_P2";

        public static M5RigContract Default()
        {
            var c = new M5RigContract();
            // Parent paths are relative to the pipeline root (RootName), excluded from the path.
            c.Sockets.Add(new M5SocketSpec("Hips", "", M5SocketKind.Gameplay));
            c.Sockets.Add(new M5SocketSpec("Chest", "Hips", M5SocketKind.Gameplay));
            c.Sockets.Add(new M5SocketSpec("Neck", "Hips/Chest", M5SocketKind.Gameplay));
            c.Sockets.Add(new M5SocketSpec("Head", "Hips/Chest/Neck", M5SocketKind.Gameplay));
            c.Sockets.Add(new M5SocketSpec("ShoulderAnchor", "Hips/Chest", M5SocketKind.Gameplay));
            c.Sockets.Add(new M5SocketSpec("WeaponAnchor", "Hips/Chest", M5SocketKind.WeaponAnchor));
            c.Sockets.Add(new M5SocketSpec("UtilityAnchor", "Hips/Chest", M5SocketKind.UtilityAnchor));
            c.Sockets.Add(new M5SocketSpec("P2CameraAnchor", "Hips/Chest", M5SocketKind.CameraAnchor));
            c.Sockets.Add(new M5SocketSpec("P1CameraAnchor", "Hips/Chest/Neck", M5SocketKind.CameraAnchor));
            c.Sockets.Add(new M5SocketSpec("Cosmetic_P1", "", M5SocketKind.CosmeticMount));
            c.Sockets.Add(new M5SocketSpec("Cosmetic_P2", "Hips/Chest", M5SocketKind.CosmeticMount));

            c.Hitboxes.Add(new M5HitboxSpec("Hitbox_Head", "Head", critical: true));
            c.Hitboxes.Add(new M5HitboxSpec("Hitbox_Body", "Body", critical: false));
            return c;
        }

        public M5SocketSpec FindSocket(string name)
        {
            for (int i = 0; i < Sockets.Count; i++)
                if (Sockets[i].Name == name) return Sockets[i];
            return null;
        }
    }
}
