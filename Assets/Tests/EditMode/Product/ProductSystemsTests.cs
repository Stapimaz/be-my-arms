using BeMyArms.Product;
using NUnit.Framework;

namespace BeMyArms.Product.Tests
{
    /// <summary>Account/profile foundations, role-specific ranked state, cosmetics, social and moderation.</summary>
    public class ProductSystemsTests
    {
        // ---- Account / profile ----

        [Test]
        public void AccountStore_RoundTripsAndProgressionAwardsLevels()
        {
            var store = new InMemoryAccountStore();
            var account = new Account { AccountId = "acct-1", DisplayName = "Tester" };
            store.Save(account);

            Assert.AreEqual("Tester", store.Load("acct-1").DisplayName);
            Assert.IsNull(store.Load("missing"));

            var progression = new Progression();
            int levelBefore = account.AccountLevel;
            int gained = progression.AwardExperience(account, progression.XpForLevel(1) + progression.XpForLevel(2) + 10);
            Assert.AreEqual(2, gained, "Two levels' worth of XP gains two levels.");
            Assert.AreEqual(levelBefore + 2, account.AccountLevel);
            Assert.Greater(progression.ProgressToNextLevel(account.Experience), 0f);
        }

        // ---- Ranked state and presentation ----

        [Test]
        public void RankTable_PresentsTierDivisionAndProgress()
        {
            RankTable table = RankTable.Default();

            RankInfo bronze = table.For(0f);
            Assert.AreEqual("Bronze", bronze.Tier);
            Assert.AreEqual(1, bronze.Division);

            RankInfo gold = table.For(1200f);
            Assert.AreEqual("Gold", gold.Tier);
            Assert.AreEqual(1, gold.Division);
            Assert.AreEqual("Gold I", gold.Label);

            RankInfo goldThree = table.For(1420f);
            Assert.AreEqual("Gold", goldThree.Tier);
            Assert.AreEqual(3, goldThree.Division);
            Assert.AreEqual(0.2f, goldThree.Progress, 0.0001f);
        }

        [Test]
        public void RankedProfile_UpdatesOnlyThePlayedRole()
        {
            var profile = new RankedProfile { P1Mmr = 1200f, P2Mmr = 1000f };
            profile.ApplyRoleResult(role: 0, opponentMmr: 1200f, score: 1f);

            Assert.Greater(profile.P1Mmr, 1200f);
            Assert.AreEqual(1000f, profile.P2Mmr, 0.0001f, "P2 rank untouched by a P1 match.");
            Assert.AreEqual(1, profile.RankedMatchesPlayed);
        }

        // ---- Cosmetics: ownership and equip ----

        static InMemoryCatalog Catalog()
        {
            return new InMemoryCatalog()
                .Add(new CosmeticItem { Id = "p1_knight", DisplayName = "Knight", Kind = CosmeticKind.P1Skin, RigId = "default" })
                .Add(new CosmeticItem { Id = "p1_toy", DisplayName = "Toy", Kind = CosmeticKind.P1Skin, RigId = "default" })
                .Add(new CosmeticItem { Id = "p2_robot", DisplayName = "Robot", Kind = CosmeticKind.P2Skin, RigId = "default" })
                .Add(new CosmeticItem { Id = "p2_undead", DisplayName = "Undead", Kind = CosmeticKind.P2Skin, RigId = "default" })
                .Add(new CosmeticItem { Id = "gun_chrome", DisplayName = "Chrome", Kind = CosmeticKind.WeaponSkin, RigId = "default" });
        }

        [Test]
        public void Cosmetics_RequireOwnership_AndKeepP1P2Independent()
        {
            InMemoryCatalog catalog = Catalog();
            var inventory = new CosmeticInventory();
            var loadout = new Loadout();

            Assert.IsFalse(loadout.TryEquip(catalog, inventory, "p1_knight", out string error), "Cannot equip an unowned skin.");
            Assert.IsNotNull(error);

            inventory.Grant("p1_knight");
            inventory.Grant("p2_robot");
            Assert.IsTrue(loadout.TryEquip(catalog, inventory, "p1_knight", out _));
            Assert.IsTrue(loadout.TryEquip(catalog, inventory, "p2_robot", out _));

            Assert.AreEqual("p1_knight", loadout.P1SkinId);
            Assert.AreEqual("p2_robot", loadout.P2SkinId, "P1 and P2 identities are independent.");

            // Equipping a P2 skin never changes the P1 slot.
            inventory.Grant("p2_undead");
            Assert.IsTrue(loadout.TryEquip(catalog, inventory, "p2_undead", out _));
            Assert.AreEqual("p1_knight", loadout.P1SkinId);
            Assert.AreEqual("p2_undead", loadout.P2SkinId);

            // Weapon skin goes to its own slot, not a body slot.
            inventory.Grant("gun_chrome");
            Assert.IsTrue(loadout.TryEquip(catalog, inventory, "gun_chrome", out _));
            Assert.AreEqual("gun_chrome", loadout.WeaponSkinId);
            Assert.AreEqual("p1_knight", loadout.P1SkinId);

            Assert.IsTrue(loadout.IsValid(catalog, inventory));
        }

        // ---- Social ----

        [Test]
        public void Social_RequestAcceptBlockUnblock()
        {
            var social = new InMemorySocialProvider();
            social.SendRequest("friend-1", "Friend One");
            Assert.AreEqual(FriendStatus.PendingOutgoing, social.Get("friend-1").Status);

            social.AcceptRequest("friend-1");
            Assert.AreEqual(FriendStatus.Accepted, social.Get("friend-1").Status);

            social.Block("friend-1");
            Assert.AreEqual(FriendStatus.Blocked, social.Get("friend-1").Status);

            social.Unblock("friend-1");
            Assert.IsNull(social.Get("friend-1"), "Unblock removes the block entry.");
        }

        // ---- Moderation ----

        [Test]
        public void Moderation_SubmitListResolve()
        {
            var moderation = new InMemoryModerationProvider();
            Report report = moderation.Submit("me", "them", ReportCategory.Cheating, "aimbot", 1.0);
            Assert.IsNotNull(report);
            Assert.AreEqual(ReportStatus.Open, report.Status);
            Assert.AreEqual(1, moderation.ReportsByStatus(ReportStatus.Open).Count);

            moderation.Resolve(report.Id, ReportStatus.Actioned);
            Assert.AreEqual(0, moderation.ReportsByStatus(ReportStatus.Open).Count);
            Assert.AreEqual(1, moderation.ReportsByStatus(ReportStatus.Actioned).Count);
        }
    }
}
