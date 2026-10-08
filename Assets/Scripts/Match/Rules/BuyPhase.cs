using System.Collections.Generic;

namespace BeMyArms.Match
{
    public enum ShopKind
    {
        Primary,
        Secondary,
        Utility
    }

    public class ShopItem
    {
        public string Id;
        public ShopKind Kind;
        public int Cost;

        public ShopItem(string id, ShopKind kind, int cost)
        {
            Id = id;
            Kind = kind;
            Cost = cost;
        }
    }

    /// <summary>
    /// DRAFT economy: a fixed per-round budget, no carry-over. P2 spends it during the buy phase on a
    /// loadout (one primary, one secondary, utility). Pure and testable; prices are TUNING.
    /// </summary>
    public class BuyPhase
    {
        public int Budget = 1200;
        public readonly List<ShopItem> Catalog = new List<ShopItem>();
        readonly List<ShopItem> _purchased = new List<ShopItem>();

        public int Spent { get; private set; }
        public int Remaining => Budget - Spent;
        public IReadOnlyList<ShopItem> Purchased => _purchased;

        public BuyPhase()
        {
            Catalog.Add(new ShopItem("rifle", ShopKind.Primary, 700));
            Catalog.Add(new ShopItem("smg", ShopKind.Primary, 500));
            Catalog.Add(new ShopItem("shotgun", ShopKind.Primary, 450));
            Catalog.Add(new ShopItem("pistol", ShopKind.Secondary, 150));
            Catalog.Add(new ShopItem("smoke", ShopKind.Utility, 150));
            Catalog.Add(new ShopItem("flash", ShopKind.Utility, 150));
            Catalog.Add(new ShopItem("grenade", ShopKind.Utility, 200));
        }

        public void ResetForRound()
        {
            Spent = 0;
            _purchased.Clear();
        }

        public bool CanAfford(string id)
        {
            ShopItem item = Find(id);
            return item != null && item.Cost <= Remaining;
        }

        /// <summary>
        /// Purchase rules enforced: one primary and one secondary max; utility is limited only by
        /// budget. Never overspends.
        /// </summary>
        public bool TryBuy(string id)
        {
            ShopItem item = Find(id);
            if (item == null || item.Cost > Remaining) return false;
            if (item.Kind == ShopKind.Primary && HasKind(ShopKind.Primary)) return false;
            if (item.Kind == ShopKind.Secondary && HasKind(ShopKind.Secondary)) return false;

            Spent += item.Cost;
            _purchased.Add(item);
            return true;
        }

        public ShopItem Find(string id)
        {
            for (int i = 0; i < Catalog.Count; i++)
                if (Catalog[i].Id == id) return Catalog[i];
            return null;
        }

        public bool HasKind(ShopKind kind)
        {
            for (int i = 0; i < _purchased.Count; i++)
                if (_purchased[i].Kind == kind) return true;
            return false;
        }
    }
}
