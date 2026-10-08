namespace BeMyArms.Match
{
    public enum WeaponType : byte
    {
        Rifle = 0,
        Smg = 1,
        Shotgun = 2,
        Pistol = 3,
        Knife = 4
    }

    /// <summary>Server-authoritative weapon stats. Ids match the buy catalog. Values are TUNING.</summary>
    public struct WeaponStats
    {
        public WeaponType Id;
        public string DisplayName;
        public float Damage;
        public float RoundsPerMinute;
        public int Magazine;
        public float ReloadSeconds;
        public float RangeMeters;

        public float SecondsBetweenShots => 60f / (RoundsPerMinute <= 0f ? 1f : RoundsPerMinute);
    }

    /// <summary>
    /// Maps BuyPhase catalog ids to server weapon stats. The Duel economy is a DRAFT: a primary
    /// plus a secondary, no carry-over; utility is priced by <see cref="BuyPhase"/>.
    /// </summary>
    public static class Loadouts
    {
        public static WeaponStats Stats(WeaponType id)
        {
            switch (id)
            {
                case WeaponType.Rifle:
                    return new WeaponStats { Id = id, DisplayName = "Rifle", Damage = 18f, RoundsPerMinute = 480f, Magazine = 30, ReloadSeconds = 2.2f, RangeMeters = 150f };
                case WeaponType.Smg:
                    return new WeaponStats { Id = id, DisplayName = "SMG", Damage = 12f, RoundsPerMinute = 720f, Magazine = 25, ReloadSeconds = 1.9f, RangeMeters = 80f };
                case WeaponType.Shotgun:
                    return new WeaponStats { Id = id, DisplayName = "Shotgun", Damage = 55f, RoundsPerMinute = 90f, Magazine = 6, ReloadSeconds = 2.8f, RangeMeters = 20f };
                case WeaponType.Pistol:
                    return new WeaponStats { Id = id, DisplayName = "Pistol", Damage = 24f, RoundsPerMinute = 300f, Magazine = 12, ReloadSeconds = 1.6f, RangeMeters = 60f };
                default:
                    return new WeaponStats { Id = WeaponType.Knife, DisplayName = "Knife", Damage = 40f, RoundsPerMinute = 120f, Magazine = 1, ReloadSeconds = 0.1f, RangeMeters = 2.2f };
            }
        }

        public static WeaponType ToWeaponId(string catalogId)
        {
            switch (catalogId)
            {
                case "rifle": return WeaponType.Rifle;
                case "smg": return WeaponType.Smg;
                case "shotgun": return WeaponType.Shotgun;
                case "pistol": return WeaponType.Pistol;
                default: return WeaponType.Knife;
            }
        }

        /// <summary>
        /// Active weapon for a round loadout: the purchased primary, else the purchased secondary,
        /// else the default pistol. Utility is not a weapon.
        /// </summary>
        public static WeaponType ActiveWeapon(BuyPhase buy)
        {
            string primary = null;
            string secondary = null;
            for (int i = 0; i < buy.Purchased.Count; i++)
            {
                ShopItem item = buy.Purchased[i];
                if (item.Kind == ShopKind.Primary) primary = item.Id;
                else if (item.Kind == ShopKind.Secondary) secondary = item.Id;
            }
            if (primary != null) return ToWeaponId(primary);
            if (secondary != null) return ToWeaponId(secondary);
            return WeaponType.Pistol;
        }
    }
}
