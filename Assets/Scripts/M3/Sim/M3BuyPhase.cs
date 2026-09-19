using System.Collections.Generic;

namespace BeMyArms.M3
{
    public enum M3ShopKind
    {
        Primary,
        Secondary,
        Utility
    }

    public class M3ShopItem
    {
        public string Id;
        public M3ShopKind Kind;
        public int Cost;

        public M3ShopItem(string id, M3ShopKind kind, int cost)
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
    public class M3BuyPhase
    {
        public int Budget = 1200;
        public readonly List<M3ShopItem> Catalog = new List<M3ShopItem>();
        readonly List<M3ShopItem> _purchased = new List<M3ShopItem>();

        public int Spent { get; private set; }
        public int Remaining => Budget - Spent;
        public IReadOnlyList<M3ShopItem> Purchased => _purchased;

        public M3BuyPhase()
        {
            Catalog.Add(new M3ShopItem("rifle", M3ShopKind.Primary, 700));
            Catalog.Add(new M3ShopItem("smg", M3ShopKind.Primary, 500));
            Catalog.Add(new M3ShopItem("shotgun", M3ShopKind.Primary, 450));
            Catalog.Add(new M3ShopItem("pistol", M3ShopKind.Secondary, 150));
            Catalog.Add(new M3ShopItem("smoke", M3ShopKind.Utility, 150));
            Catalog.Add(new M3ShopItem("flash", M3ShopKind.Utility, 150));
            Catalog.Add(new M3ShopItem("grenade", M3ShopKind.Utility, 200));
        }

        public void ResetForRound()
        {
            Spent = 0;
            _purchased.Clear();
        }

        public bool CanAfford(string id)
        {
            M3ShopItem item = Find(id);
            return item != null && item.Cost <= Remaining;
        }

        /// <summary>
        /// Purchase rules enforced: one primary and one secondary max; utility is limited only by
        /// budget. Never overspends.
        /// </summary>
        public bool TryBuy(string id)
        {
            M3ShopItem item = Find(id);
            if (item == null || item.Cost > Remaining) return false;
            if (item.Kind == M3ShopKind.Primary && HasKind(M3ShopKind.Primary)) return false;
            if (item.Kind == M3ShopKind.Secondary && HasKind(M3ShopKind.Secondary)) return false;

            Spent += item.Cost;
            _purchased.Add(item);
            return true;
        }

        public M3ShopItem Find(string id)
        {
            for (int i = 0; i < Catalog.Count; i++)
                if (Catalog[i].Id == id) return Catalog[i];
            return null;
        }

        public bool HasKind(M3ShopKind kind)
        {
            for (int i = 0; i < _purchased.Count; i++)
                if (_purchased[i].Kind == kind) return true;
            return false;
        }
    }
}
