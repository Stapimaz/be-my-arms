namespace BeMyArms.M3
{
    public enum M3WeaponId : byte
    {
        Rifle = 0,
        Smg = 1,
        Shotgun = 2,
        Pistol = 3,
        Knife = 4
    }

    /// <summary>Server-authoritative weapon stats. Ids match the buy catalog. Values are TUNING.</summary>
    public struct M3WeaponStats
    {
        public M3WeaponId Id;
        public string DisplayName;
        public float Damage;
        public float RoundsPerMinute;
        public int Magazine;
        public float ReloadSeconds;
        public float RangeMeters;

        public float SecondsBetweenShots => 60f / (RoundsPerMinute <= 0f ? 1f : RoundsPerMinute);
    }

    /// <summary>
    /// Maps M3BuyPhase catalog ids to server weapon stats. The Duel economy is a DRAFT: a primary
    /// plus a secondary, no carry-over; utility is priced by <see cref="M3BuyPhase"/>.
    /// </summary>
    public static class M3Loadouts
    {
        public static M3WeaponStats Stats(M3WeaponId id)
        {
            switch (id)
            {
                case M3WeaponId.Rifle:
                    return new M3WeaponStats { Id = id, DisplayName = "Rifle", Damage = 18f, RoundsPerMinute = 480f, Magazine = 30, ReloadSeconds = 2.2f, RangeMeters = 150f };
                case M3WeaponId.Smg:
                    return new M3WeaponStats { Id = id, DisplayName = "SMG", Damage = 12f, RoundsPerMinute = 720f, Magazine = 25, ReloadSeconds = 1.9f, RangeMeters = 80f };
                case M3WeaponId.Shotgun:
                    return new M3WeaponStats { Id = id, DisplayName = "Shotgun", Damage = 55f, RoundsPerMinute = 90f, Magazine = 6, ReloadSeconds = 2.8f, RangeMeters = 20f };
                case M3WeaponId.Pistol:
                    return new M3WeaponStats { Id = id, DisplayName = "Pistol", Damage = 24f, RoundsPerMinute = 300f, Magazine = 12, ReloadSeconds = 1.6f, RangeMeters = 60f };
                default:
                    return new M3WeaponStats { Id = M3WeaponId.Knife, DisplayName = "Knife", Damage = 40f, RoundsPerMinute = 120f, Magazine = 1, ReloadSeconds = 0.1f, RangeMeters = 2.2f };
            }
        }

        public static M3WeaponId ToWeaponId(string catalogId)
        {
            switch (catalogId)
            {
                case "rifle": return M3WeaponId.Rifle;
                case "smg": return M3WeaponId.Smg;
                case "shotgun": return M3WeaponId.Shotgun;
                case "pistol": return M3WeaponId.Pistol;
                default: return M3WeaponId.Knife;
            }
        }

        /// <summary>
        /// Active weapon for a round loadout: the purchased primary, else the purchased secondary,
        /// else the default pistol. Utility is not a weapon.
        /// </summary>
        public static M3WeaponId ActiveWeapon(M3BuyPhase buy)
        {
            string primary = null;
            string secondary = null;
            for (int i = 0; i < buy.Purchased.Count; i++)
            {
                M3ShopItem item = buy.Purchased[i];
                if (item.Kind == M3ShopKind.Primary) primary = item.Id;
                else if (item.Kind == M3ShopKind.Secondary) secondary = item.Id;
            }
            if (primary != null) return ToWeaponId(primary);
            if (secondary != null) return ToWeaponId(secondary);
            return M3WeaponId.Pistol;
        }
    }
}
