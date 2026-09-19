using System;
using System.Collections.Generic;

namespace BeMyArms.M5
{
    /// <summary>
    /// Platform-neutral game account identity (concept §22.3/§27). Steam (or any other identity
    /// provider) links to this account; it is never equivalent to a Steam id. In-memory foundation
    /// only — the persistence vendor is deliberately not chosen (see docs/M5_PRODUCT_SYSTEMS.md).
    /// </summary>
    public class M5Account
    {
        public string AccountId;
        public string DisplayName;
        public int AccountLevel = 1;
        public int Experience;
    }

    /// <summary>
    /// Foundational account-level progression curve. Explicitly a placeholder: exact pacing, rewards
    /// and any battle-pass-like structure are unresolved product decisions and are not fixed here.
    /// </summary>
    public class M5Progression
    {
        public int BaseLevelXp = 1000;
        public float GrowthPerLevel = 1.15f;

        public int XpForLevel(int level)
        {
            if (level < 1) level = 1;
            double xp = BaseLevelXp * Math.Pow(GrowthPerLevel, level - 1);
            return (int)Math.Round(xp);
        }

        public int TotalXpForLevel(int level)
        {
            int total = 0;
            for (int i = 1; i < level; i++) total += XpForLevel(i);
            return total;
        }

        public int LevelForTotalXp(int totalXp)
        {
            int level = 1;
            int remaining = totalXp;
            while (remaining >= XpForLevel(level))
            {
                remaining -= XpForLevel(level);
                level++;
            }
            return level;
        }

        /// <summary>Progress within the current level, 0..1.</summary>
        public float ProgressToNextLevel(int totalXp)
        {
            int level = LevelForTotalXp(totalXp);
            int spent = TotalXpForLevel(level);
            int needed = XpForLevel(level);
            if (needed <= 0) return 0f;
            return Clamp01((totalXp - spent) / (float)needed);
        }

        /// <summary>Applies XP and updates the account's level. Returns levels gained.</summary>
        public int AwardExperience(M5Account account, int xp)
        {
            if (account == null || xp <= 0) return 0;
            int before = account.AccountLevel;
            account.Experience += xp;
            account.AccountLevel = LevelForTotalXp(account.Experience);
            return account.AccountLevel - before;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    public interface IM5AccountStore
    {
        M5Account Load(string accountId);
        void Save(M5Account account);
    }

    /// <summary>Local/in-memory account store. A real backend implements the same interface.</summary>
    public class M5InMemoryAccountStore : IM5AccountStore
    {
        readonly Dictionary<string, M5Account> _accounts = new Dictionary<string, M5Account>();

        public M5Account Load(string accountId)
            => _accounts.TryGetValue(accountId ?? "", out M5Account account) ? account : null;

        public void Save(M5Account account)
        {
            if (account == null || string.IsNullOrEmpty(account.AccountId)) return;
            _accounts[account.AccountId] = account;
        }
    }

    /// <summary>How the running client learned its account id. Steam is one implementation, not the design.</summary>
    public interface IM5IdentityProvider
    {
        string CurrentAccountId { get; }
    }

    public class M5LocalIdentityProvider : IM5IdentityProvider
    {
        public string AccountId = "local-player";
        public string CurrentAccountId => AccountId;
    }
}
