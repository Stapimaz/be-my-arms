using System;
using System.Collections.Generic;
using BeMyArms.M4;

namespace BeMyArms.M5
{
    public class M5RankTier
    {
        public string Name;
        public int MinMmr;

        public M5RankTier(string name, int minMmr)
        {
            Name = name;
            MinMmr = minMmr;
        }
    }

    /// <summary>Presentation of one role's competitive standing. Derived from MMR; not authoritative data.</summary>
    public struct M5RankInfo
    {
        public string Tier;
        public int Division;
        public float Mmr;
        /// <summary>Progress within the current division, 0..1.</summary>
        public float Progress;
        public string Label;
    }

    /// <summary>
    /// Maps role MMR to a presentable rank. Tier names and thresholds are TUNING/placeholder; the
    /// point of M5 is the structure (role-specific ranks presented separately), not the exact tiers.
    /// </summary>
    public class M5RankTable
    {
        public readonly List<M5RankTier> Tiers = new List<M5RankTier>();
        public int DivisionsPerTier = 3;
        public int MmrPerDivision = 100;

        public static M5RankTable Default()
        {
            var table = new M5RankTable();
            table.Tiers.Add(new M5RankTier("Bronze", 0));
            table.Tiers.Add(new M5RankTier("Silver", 900));
            table.Tiers.Add(new M5RankTier("Gold", 1200));
            table.Tiers.Add(new M5RankTier("Platinum", 1500));
            table.Tiers.Add(new M5RankTier("Diamond", 1800));
            table.Tiers.Add(new M5RankTier("Master", 2100));
            return table;
        }

        public M5RankInfo For(float mmr)
        {
            if (mmr < 0f) mmr = 0f;
            M5RankTier tier = Tiers.Count > 0 ? Tiers[0] : new M5RankTier("Unranked", 0);
            for (int i = 0; i < Tiers.Count; i++)
                if (mmr >= Tiers[i].MinMmr) tier = Tiers[i];

            int tierIndex = Tiers.IndexOf(tier);
            int nextTierMin = tierIndex + 1 < Tiers.Count ? Tiers[tierIndex + 1].MinMmr : tier.MinMmr + DivisionsPerTier * MmrPerDivision;

            int bandSize = Math.Max(1, DivisionsPerTier * MmrPerDivision);
            int withinTier = (int)mmr - tier.MinMmr;
            int divisionIndex = Math.Min(DivisionsPerTier - 1, withinTier / Math.Max(1, MmrPerDivision));
            int division = divisionIndex + 1; // 1 = lowest division inside the tier
            float divisionFloor = tier.MinMmr + divisionIndex * MmrPerDivision;
            float progress = MmrPerDivision <= 0 ? 0f : Clamp01((mmr - divisionFloor) / MmrPerDivision);

            return new M5RankInfo
            {
                Tier = tier.Name,
                Division = division,
                Mmr = mmr,
                Progress = progress,
                Label = $"{tier.Name} {RomanDivision(division)}"
            };
        }

        static string RomanDivision(int division)
        {
            switch (division)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                default: return division.ToString();
            }
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// Role-specific ranked state. P1 and P2 are rated and presented independently (concept §18.2),
    /// reusing the M4 role-rating update so matchmaking and progression agree.
    /// </summary>
    public class M5RankedProfile
    {
        public float P1Mmr = 1000f;
        public float P2Mmr = 1000f;
        public int RankedMatchesPlayed;

        public M5RankInfo P1Rank(M5RankTable table) => table.For(P1Mmr);
        public M5RankInfo P2Rank(M5RankTable table) => table.For(P2Mmr);

        /// <summary>Applies one match to the played role only. Score: 1 win, 0.5 draw, 0 loss.</summary>
        public void ApplyRoleResult(int role, float opponentMmr, float score, float k = M4Rating.KFactor)
        {
            if (role == 0) P1Mmr = M4Rating.Update(P1Mmr, opponentMmr, score, k);
            else P2Mmr = M4Rating.Update(P2Mmr, opponentMmr, score, k);
            RankedMatchesPlayed++;
        }
    }
}
