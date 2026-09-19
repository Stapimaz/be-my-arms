using System.Collections.Generic;
using BeMyArms.M3;
using NUnit.Framework;

namespace BeMyArms.M3.Tests
{
    /// <summary>Duel four-slot roster, utility effects, and loadout resolution.</summary>
    public class M3DuelTests
    {
        // ---- Roster ----

        const int SlotAP1 = (int)M3DuelSlot.TeamAP1;
        const int SlotAP2 = (int)M3DuelSlot.TeamAP2;
        const int SlotBP1 = (int)M3DuelSlot.TeamBP1;
        const int SlotBP2 = (int)M3DuelSlot.TeamBP2;

        [Test]
        public void Roster_AssignsRequestedSlots_ToFourConnections()
        {
            var roster = new M3DuelRoster();
            Assert.AreEqual(SlotAP1, roster.AssignPreferred(1, "a1", SlotAP1, out _));
            Assert.AreEqual(SlotAP2, roster.AssignPreferred(2, "a2", SlotAP2, out _));
            Assert.AreEqual(SlotBP1, roster.AssignPreferred(3, "b1", SlotBP1, out _));
            Assert.AreEqual(SlotBP2, roster.AssignPreferred(4, "b2", SlotBP2, out _));
            Assert.AreEqual(4, roster.AssignedCount);
            Assert.IsTrue(roster.HasSlot(3, SlotBP1));
        }

        [Test]
        public void Roster_AssignsFreeSlot_WhenRequestedIsTaken()
        {
            var roster = new M3DuelRoster();
            roster.AssignPreferred(1, "", SlotAP1, out _);
            int second = roster.AssignPreferred(2, "", SlotAP1, out _); // requested A P1 again
            Assert.AreNotEqual(SlotAP1, second);
            Assert.IsTrue(M3DuelSlots.IsValidSlot(second, roster.BodiesPerTeam));
        }

        [Test]
        public void Roster_TokenReclaimsSlot_AndDisplacesStaleConnection()
        {
            var roster = new M3DuelRoster();
            roster.AssignPreferred(1, "t", SlotAP1, out _);
            int reclaimed = roster.AssignPreferred(2, "t", SlotBP2, out ulong displaced); // same token
            Assert.AreEqual(SlotAP1, reclaimed, "Token keeps its original slot.");
            Assert.AreEqual(1ul, displaced);
            Assert.IsFalse(roster.HasSlot(1, SlotAP1));
            Assert.IsTrue(roster.HasSingleOwner(SlotAP1));
        }

        [Test]
        public void Roster_BotTakeover_IsSingleOwner_AndHumanClearsBot()
        {
            var roster = new M3DuelRoster();
            roster.AssignPreferred(1, "a1", SlotAP1, out _);
            Assert.IsTrue(roster.HasSingleOwner(SlotAP1));

            roster.Release(1);
            roster.SetBot(SlotAP1);
            Assert.IsTrue(roster.IsBot(SlotAP1));
            Assert.IsTrue(roster.HasSingleOwner(SlotAP1));
            Assert.IsFalse(roster.HasSingleOwner(SlotAP2), "Empty slot has no owner.");

            roster.AssignPreferred(9, "a1", SlotAP1, out _);
            Assert.IsFalse(roster.IsBot(SlotAP1));
            Assert.IsTrue(roster.HasSlot(9, SlotAP1));
        }

        [Test]
        public void Roster_Queue_AssignsFromMatchProposal()
        {
            var roster = new M3DuelRoster { BodiesPerTeam = 2 };
            Assert.AreEqual(8, roster.SlotCount);

            roster.Enqueue(5, "p5", 1, 0f, "local");
            Assert.AreEqual(1, roster.QueuedCount);
            Assert.IsTrue(roster.IsQueued(5));
            Assert.IsTrue(roster.TryGetQueuedClient("p5", out ulong queuedClient));
            Assert.AreEqual(5ul, queuedClient);

            int slot = M3DuelSlots.Encode(1, 1, 1, 2); // B body 1 P2
            roster.AssignSlot(5, "p5", slot, out _);
            Assert.AreEqual(0, roster.QueuedCount);
            Assert.IsFalse(roster.TryGetQueuedClient("p5", out _));
            Assert.IsTrue(roster.HasSlot(5, slot));
            Assert.AreEqual("p5", roster.TokenForSlot(slot));
            Assert.AreEqual(5ul, roster.ClientForToken("p5"));
        }

        [Test]
        public void FormTeams_BuildsTwoBodies_FromQueue()
        {
            var entries = new List<M3QueueEntry>
            {
                new M3QueueEntry { PlayerId = "a1", Preference = M3RolePreference.P1 },
                new M3QueueEntry { PlayerId = "a2", Preference = M3RolePreference.P2 },
                new M3QueueEntry { PlayerId = "b1", Preference = M3RolePreference.Either },
                new M3QueueEntry { PlayerId = "b2", Preference = M3RolePreference.Either },
            };

            M3DuelRoster.FormTeams(entries, out M3BodyPair teamA, out M3BodyPair teamB);
            Assert.IsTrue(teamA.IsComplete);
            Assert.IsTrue(teamB.IsComplete);
            Assert.AreEqual("a1", teamA.P1);
            Assert.AreEqual("a2", teamA.P2);
            Assert.AreEqual("b1", teamB.P1);
            Assert.AreEqual("b2", teamB.P2);
        }

        // ---- Utility ----

        [Test]
        public void Utility_Smoke_BlocksLineThroughIt()
        {
            var u = new M3UtilitySystem { SmokeRadius = 3f, SmokeDuration = 10f, ThrowDistance = 0f };
            u.Throw(M3UtilityKind.Smoke, 0, now: 0, x: 5f, z: 0f, aimYaw: 0f);

            Assert.IsTrue(u.BlocksLine(0, 0, 0, 10, 0), "Segment passes through the smoke.");
            Assert.IsFalse(u.BlocksLine(0, 0, 0, 10, 10), "Segment misses the smoke.");
            Assert.IsFalse(u.BlocksLine(11, 0, 0, 10, 0), "Smoke expired.");
        }

        [Test]
        public void Utility_Grenade_Detonates_OnFuse_AndDamagesByDistance()
        {
            var u = new M3UtilitySystem
            {
                GrenadeFuse = 1f,
                GrenadeRadius = 4f,
                GrenadeDamage = 60f,
                ThrowDistance = 10f
            };

            int detonations = 0;
            float dx = 0f, dz = 0f;
            u.GrenadeDetonated += (team, x, z) => { detonations++; dx = x; dz = z; };

            u.Throw(M3UtilityKind.Grenade, team: 1, now: 0, x: 0f, z: 0f, aimYaw: 0f); // lands at z = +10
            u.Tick(0.5);
            Assert.AreEqual(0, detonations, "Not yet.");
            u.Tick(1.2);
            Assert.AreEqual(1, detonations);
            Assert.AreEqual(0f, dx, 0.001f);
            Assert.AreEqual(10f, dz, 0.001f);

            Assert.AreEqual(60f, u.GrenadeDamageAt(10, 0, 10, 0), 0.001f, "Direct hit.");
            Assert.AreEqual(30f, u.GrenadeDamageAt(10, 0, 12, 0), 0.001f, "Half radius.");
            Assert.AreEqual(0f, u.GrenadeDamageAt(10, 0, 20, 0), 0.001f, "Out of radius.");
        }

        [Test]
        public void Utility_Flash_BlindsNearby_AndFadesWithDistance()
        {
            var u = new M3UtilitySystem { FlashRadius = 8f, FlashMaxDuration = 2f, ThrowDistance = 0f };
            u.Throw(M3UtilityKind.Flash, 0, now: 0, x: 0f, z: 0f, aimYaw: 0f);

            float near = u.FlashBlindSeconds(0, 1f, 0f);
            float far = u.FlashBlindSeconds(0, 6f, 0f);
            Assert.Greater(near, far);
            Assert.Greater(near, 0f);
            Assert.AreEqual(0f, u.FlashBlindSeconds(0, 20f, 0f), 0.001f);
            Assert.AreEqual(0f, u.FlashBlindSeconds(5, 1f, 0f), 0.001f, "Flash expired.");
        }

        // ---- Loadouts ----

        [Test]
        public void Loadouts_PrimaryWins_ElseSecondary_ElsePistol()
        {
            var buy = new M3BuyPhase();
            buy.ResetForRound();
            Assert.AreEqual(M3WeaponId.Pistol, M3Loadouts.ActiveWeapon(buy));

            buy.TryBuy("smg");
            Assert.AreEqual(M3WeaponId.Smg, M3Loadouts.ActiveWeapon(buy));

            buy.TryBuy("rifle"); // no-op: one primary already
            Assert.AreEqual(M3WeaponId.Smg, M3Loadouts.ActiveWeapon(buy));

            var secondaryOnly = new M3BuyPhase();
            secondaryOnly.TryBuy("pistol");
            Assert.AreEqual(M3WeaponId.Pistol, M3Loadouts.ActiveWeapon(secondaryOnly));
        }

        [Test]
        public void Loadouts_StatsAreServerAuthoritative()
        {
            M3WeaponStats rifle = M3Loadouts.Stats(M3WeaponId.Rifle);
            Assert.Greater(rifle.Damage, 0f);
            Assert.Greater(rifle.Magazine, 0);
            Assert.Greater(rifle.SecondsBetweenShots, 0f);
        }
    }
}
