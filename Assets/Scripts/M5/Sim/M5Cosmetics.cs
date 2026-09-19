using System.Collections.Generic;

namespace BeMyArms.M5
{
    /// <summary>Which half of the shared body a cosmetic belongs to (concept §5.1).</summary>
    public enum M5RigRole
    {
        P1,
        P2
    }

    /// <summary>
    /// Cosmetic categories from concept §21.2. M5 only needs ownership/equip foundations; which of
    /// these become purchasable products is an unresolved monetization decision.
    /// </summary>
    public enum M5CosmeticKind
    {
        P1Skin,
        P2Skin,
        WeaponSkin,
        KnifeSkin,
        LobbyAnimation,
        MountAnimation,
        Pose,
        Emote,
        Effect,
        ProfileBanner,
        DuoPresentation
    }

    public class M5CosmeticItem
    {
        public string Id;
        public string DisplayName;
        public M5CosmeticKind Kind;
        /// <summary>Rig/contract id this cosmetic is authored against (empty = not rig-bound).</summary>
        public string RigId = "";

        public M5RigRole? Role
            => Kind == M5CosmeticKind.P1Skin ? M5RigRole.P1
             : Kind == M5CosmeticKind.P2Skin ? M5RigRole.P2
             : (M5RigRole?)null;
    }

    public interface IM5CosmeticCatalog
    {
        M5CosmeticItem Get(string id);
        IReadOnlyList<M5CosmeticItem> All { get; }
    }

    /// <summary>In-memory catalog. A backend/store implements the same interface later.</summary>
    public class M5InMemoryCatalog : IM5CosmeticCatalog
    {
        readonly Dictionary<string, M5CosmeticItem> _items = new Dictionary<string, M5CosmeticItem>();
        readonly List<M5CosmeticItem> _order = new List<M5CosmeticItem>();

        public IReadOnlyList<M5CosmeticItem> All => _order;

        public M5InMemoryCatalog Add(M5CosmeticItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id)) return this;
            if (!_items.ContainsKey(item.Id))
            {
                _items[item.Id] = item;
                _order.Add(item);
            }
            return this;
        }

        public M5CosmeticItem Get(string id)
            => !string.IsNullOrEmpty(id) && _items.TryGetValue(id, out M5CosmeticItem item) ? item : null;
    }

    /// <summary>Which cosmetics an account owns. Authoritative ownership belongs on the backend (concept §27.3).</summary>
    public class M5CosmeticInventory
    {
        readonly HashSet<string> _owned = new HashSet<string>();

        public IReadOnlyCollection<string> Owned => _owned;
        public bool Owns(string id) => !string.IsNullOrEmpty(id) && _owned.Contains(id);

        public void Grant(string id)
        {
            if (!string.IsNullOrEmpty(id)) _owned.Add(id);
        }

        public bool Revoke(string id) => _owned.Remove(id);
    }

    /// <summary>
    /// The equipped cosmetic loadout. P1 and P2 identities are independent: the P1 slot only accepts
    /// a P1 skin and the P2 slot only accepts a P2 skin, so the two never collapse into one identity.
    /// </summary>
    public class M5Loadout
    {
        public string P1SkinId = "";
        public string P2SkinId = "";
        public string WeaponSkinId = "";
        public string KnifeSkinId = "";

        public bool TryEquip(IM5CosmeticCatalog catalog, M5CosmeticInventory inventory, string itemId, out string error)
        {
            error = null;
            if (catalog == null || inventory == null) { error = "catalog/inventory missing"; return false; }
            M5CosmeticItem item = catalog.Get(itemId);
            if (item == null) { error = $"unknown cosmetic '{itemId}'"; return false; }
            if (!inventory.Owns(itemId)) { error = $"not owned: '{itemId}'"; return false; }

            switch (item.Kind)
            {
                case M5CosmeticKind.P1Skin: P1SkinId = itemId; return true;
                case M5CosmeticKind.P2Skin: P2SkinId = itemId; return true;
                case M5CosmeticKind.WeaponSkin: WeaponSkinId = itemId; return true;
                case M5CosmeticKind.KnifeSkin: KnifeSkinId = itemId; return true;
                default:
                    error = $"kind {item.Kind} is not an equippable slot in this foundation";
                    return false;
            }
        }

        public void Unequip(M5CosmeticKind kind)
        {
            switch (kind)
            {
                case M5CosmeticKind.P1Skin: P1SkinId = ""; break;
                case M5CosmeticKind.P2Skin: P2SkinId = ""; break;
                case M5CosmeticKind.WeaponSkin: WeaponSkinId = ""; break;
                case M5CosmeticKind.KnifeSkin: KnifeSkinId = ""; break;
            }
        }

        /// <summary>True when every equipped skin is owned, exists and matches its slot's role.</summary>
        public bool IsValid(IM5CosmeticCatalog catalog, M5CosmeticInventory inventory)
        {
            if (catalog == null || inventory == null) return false;
            return SlotValid(catalog, inventory, P1SkinId, M5CosmeticKind.P1Skin)
                && SlotValid(catalog, inventory, P2SkinId, M5CosmeticKind.P2Skin)
                && SlotValid(catalog, inventory, WeaponSkinId, M5CosmeticKind.WeaponSkin)
                && SlotValid(catalog, inventory, KnifeSkinId, M5CosmeticKind.KnifeSkin);
        }

        static bool SlotValid(IM5CosmeticCatalog catalog, M5CosmeticInventory inventory, string id, M5CosmeticKind expected)
        {
            if (string.IsNullOrEmpty(id)) return true;
            M5CosmeticItem item = catalog.Get(id);
            return item != null && item.Kind == expected && inventory.Owns(id);
        }
    }
}
