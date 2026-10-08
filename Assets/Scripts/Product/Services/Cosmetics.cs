using System.Collections.Generic;

namespace BeMyArms.Product
{
    /// <summary>Which half of the shared body a cosmetic belongs to (concept §5.1).</summary>
    public enum RigRole
    {
        P1,
        P2
    }

    /// <summary>
    /// Cosmetic categories from concept §21.2. Product only needs ownership/equip foundations; which of
    /// these become purchasable products is an unresolved monetization decision.
    /// </summary>
    public enum CosmeticKind
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

    public class CosmeticItem
    {
        public string Id;
        public string DisplayName;
        public CosmeticKind Kind;
        /// <summary>Rig/contract id this cosmetic is authored against (empty = not rig-bound).</summary>
        public string RigId = "";

        public RigRole? Role
            => Kind == CosmeticKind.P1Skin ? RigRole.P1
             : Kind == CosmeticKind.P2Skin ? RigRole.P2
             : (RigRole?)null;
    }

    public interface ICosmeticCatalog
    {
        CosmeticItem Get(string id);
        IReadOnlyList<CosmeticItem> All { get; }
    }

    /// <summary>In-memory catalog. A backend/store implements the same interface later.</summary>
    public class InMemoryCatalog : ICosmeticCatalog
    {
        readonly Dictionary<string, CosmeticItem> _items = new Dictionary<string, CosmeticItem>();
        readonly List<CosmeticItem> _order = new List<CosmeticItem>();

        public IReadOnlyList<CosmeticItem> All => _order;

        public InMemoryCatalog Add(CosmeticItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id)) return this;
            if (!_items.ContainsKey(item.Id))
            {
                _items[item.Id] = item;
                _order.Add(item);
            }
            return this;
        }

        public CosmeticItem Get(string id)
            => !string.IsNullOrEmpty(id) && _items.TryGetValue(id, out CosmeticItem item) ? item : null;
    }

    /// <summary>Which cosmetics an account owns. Authoritative ownership belongs on the backend (concept §27.3).</summary>
    public class CosmeticInventory
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
    public class Loadout
    {
        public string P1SkinId = "";
        public string P2SkinId = "";
        public string WeaponSkinId = "";
        public string KnifeSkinId = "";

        public bool TryEquip(ICosmeticCatalog catalog, CosmeticInventory inventory, string itemId, out string error)
        {
            error = null;
            if (catalog == null || inventory == null) { error = "catalog/inventory missing"; return false; }
            CosmeticItem item = catalog.Get(itemId);
            if (item == null) { error = $"unknown cosmetic '{itemId}'"; return false; }
            if (!inventory.Owns(itemId)) { error = $"not owned: '{itemId}'"; return false; }

            switch (item.Kind)
            {
                case CosmeticKind.P1Skin: P1SkinId = itemId; return true;
                case CosmeticKind.P2Skin: P2SkinId = itemId; return true;
                case CosmeticKind.WeaponSkin: WeaponSkinId = itemId; return true;
                case CosmeticKind.KnifeSkin: KnifeSkinId = itemId; return true;
                default:
                    error = $"kind {item.Kind} is not an equippable slot in this foundation";
                    return false;
            }
        }

        public void Unequip(CosmeticKind kind)
        {
            switch (kind)
            {
                case CosmeticKind.P1Skin: P1SkinId = ""; break;
                case CosmeticKind.P2Skin: P2SkinId = ""; break;
                case CosmeticKind.WeaponSkin: WeaponSkinId = ""; break;
                case CosmeticKind.KnifeSkin: KnifeSkinId = ""; break;
            }
        }

        /// <summary>True when every equipped skin is owned, exists and matches its slot's role.</summary>
        public bool IsValid(ICosmeticCatalog catalog, CosmeticInventory inventory)
        {
            if (catalog == null || inventory == null) return false;
            return SlotValid(catalog, inventory, P1SkinId, CosmeticKind.P1Skin)
                && SlotValid(catalog, inventory, P2SkinId, CosmeticKind.P2Skin)
                && SlotValid(catalog, inventory, WeaponSkinId, CosmeticKind.WeaponSkin)
                && SlotValid(catalog, inventory, KnifeSkinId, CosmeticKind.KnifeSkin);
        }

        static bool SlotValid(ICosmeticCatalog catalog, CosmeticInventory inventory, string id, CosmeticKind expected)
        {
            if (string.IsNullOrEmpty(id)) return true;
            CosmeticItem item = catalog.Get(id);
            return item != null && item.Kind == expected && inventory.Owns(id);
        }
    }
}
