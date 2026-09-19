using System.Collections.Generic;
using BeMyArms.M3;
using NUnit.Framework;

namespace BeMyArms.M3.Tests
{
    /// <summary>Duel round state machine, buy economy, closing zone, role queue, telemetry.</summary>
    public class M3MatchTests
    {
        static M3MatchState NewMatch()
        {
            var m = new M3MatchState
            {
                RoundsToWin = 3,
                MaxRounds = 5,
                BuySeconds = 10f,
                LiveSeconds = 60f,
                RoundEndSeconds = 3f
            };
            m.StartMatch();
            return m;
        }

        static void GoLive(M3MatchState m) => m.Tick(m.BuySeconds + 0.01f);
        static void GoNextRound(M3MatchState m) => m.Tick(m.RoundEndSeconds + 0.01f);

        [Test]
        public void Match_StartsInBuy_ThenLive()
        {
            M3MatchState m = NewMatch();
            Assert.AreEqual(M3Phase.Buy, m.Phase);
            Assert.AreEqual(1, m.RoundIndex);

            GoLive(m);
            Assert.AreEqual(M3Phase.Live, m.Phase);
        }

        [Test]
        public void Elimination_EndsRound_AndAwardsWin()
        {
            M3MatchState m = NewMatch();
            GoLive(m);
            m.ReportTeamEliminated(1); // B eliminated -> A wins
            Assert.AreEqual(M3Phase.RoundEnd, m.Phase);
            Assert.AreEqual(1, m.TeamAWins);
            Assert.AreEqual(0, m.LastRoundWinner);
        }

        [Test]
        public void FirstToThree_EndsMatch()
        {
            M3MatchState m = NewMatch();
            for (int round = 0; round < 3; round++)
            {
                GoLive(m);
                m.ReportTeamEliminated(1);
                GoNextRound(m);
            }
            Assert.AreEqual(M3Phase.MatchEnd, m.Phase);
            Assert.AreEqual(0, m.MatchWinner);
            Assert.AreEqual(3, m.TeamAWins);
        }

        [Test]
        public void MaxFiveRounds_CapsTheMatch()
        {
            var m = new M3MatchState
            {
                RoundsToWin = 4,   // unreachable within 5 rounds, so the cap decides
                MaxRounds = 5,
                BuySeconds = 10f,
                LiveSeconds = 60f,
                RoundEndSeconds = 3f
            };
            m.StartMatch();

            for (int round = 0; round < 5; round++)
            {
                GoLive(m);
                m.ReportTeamEliminated(round % 2 == 0 ? 1 : 0);
                GoNextRound(m);
            }

            Assert.AreEqual(M3Phase.MatchEnd, m.Phase);
            Assert.AreEqual(3, m.TeamAWins);
            Assert.AreEqual(2, m.TeamBWins);
            Assert.AreEqual(0, m.MatchWinner);
        }

        [Test]
        public void LiveTimeout_IsADraw()
        {
            M3MatchState m = NewMatch();
            GoLive(m);
            m.Tick(m.LiveSeconds + 0.01f);
            Assert.AreEqual(M3Phase.RoundEnd, m.Phase);
            Assert.AreEqual(-1, m.LastRoundWinner);
            Assert.AreEqual(0, m.TeamAWins);
        }

        // ---- Buy ----

        [Test]
        public void Buy_RespectsBudget_AndSlotLimits()
        {
            var buy = new M3BuyPhase { Budget = 1200 };
            buy.ResetForRound();

            Assert.IsTrue(buy.TryBuy("rifle"));      // 700
            Assert.AreEqual(500, buy.Remaining);
            Assert.IsFalse(buy.TryBuy("smg"), "Only one primary.");
            Assert.IsTrue(buy.TryBuy("pistol"));     // 150
            Assert.IsFalse(buy.TryBuy("pistol"), "Only one secondary.");
            Assert.IsTrue(buy.TryBuy("smoke"));      // 150
            Assert.IsTrue(buy.TryBuy("flash"));      // 150
            Assert.AreEqual(50, buy.Remaining);
            Assert.IsFalse(buy.TryBuy("grenade"), "Cannot overspend (200 > 50).");
        }

        [Test]
        public void Buy_ResetsEachRound_NoCarryOver()
        {
            var buy = new M3BuyPhase { Budget = 1200 };
            buy.TryBuy("rifle");
            buy.ResetForRound();
            Assert.AreEqual(1200, buy.Remaining);
            Assert.AreEqual(0, buy.Purchased.Count);
        }

        // ---- Closing zone ----

        [Test]
        public void Zone_HoldsThenShrinks_AndDamagesOutsideOnly()
        {
            var z = new M3ClosingZone
            {
                StartRadius = 40f,
                EndRadius = 8f,
                CloseStartSeconds = 60f,
                CloseDurationSeconds = 60f,
                DamagePerSecond = 5f
            };

            Assert.AreEqual(40f, z.RadiusAt(0f), 0.001f);
            Assert.AreEqual(40f, z.RadiusAt(60f), 0.001f);
            Assert.AreEqual(24f, z.RadiusAt(90f), 0.001f);
            Assert.AreEqual(8f, z.RadiusAt(120f), 0.001f);
            Assert.AreEqual(8f, z.RadiusAt(500f), 0.001f);

            Assert.IsTrue(z.IsOutside(30f, 90f));
            Assert.IsFalse(z.IsOutside(10f, 90f));
            Assert.AreEqual(5f, z.DamageFor(30f, 90f, 1f), 0.001f);
            Assert.AreEqual(0f, z.DamageFor(10f, 90f, 1f), 0.001f);
        }

        // ---- Role queue ----

        [Test]
        public void RoleQueue_FormsBody_FromExactPreferences()
        {
            var entries = new List<M3QueueEntry>
            {
                new M3QueueEntry { PlayerId = "A", Preference = M3RolePreference.P1 },
                new M3QueueEntry { PlayerId = "B", Preference = M3RolePreference.P2 }
            };
            M3BodyPair pair = M3RoleQueue.FormBody(entries);
            Assert.IsTrue(pair.IsComplete);
            Assert.AreEqual("A", pair.P1);
            Assert.AreEqual("B", pair.P2);
        }

        [Test]
        public void RoleQueue_EitherFillsTheComplementaryRole()
        {
            var entries = new List<M3QueueEntry>
            {
                new M3QueueEntry { PlayerId = "A", Preference = M3RolePreference.Either },
                new M3QueueEntry { PlayerId = "B", Preference = M3RolePreference.Either }
            };
            M3BodyPair pair = M3RoleQueue.FormBody(entries);
            Assert.IsTrue(pair.IsComplete);
            Assert.AreEqual("A", pair.P1);
            Assert.AreEqual("B", pair.P2);

            var solo = new List<M3QueueEntry> { new M3QueueEntry { PlayerId = "A", Preference = M3RolePreference.Either } };
            Assert.IsFalse(M3RoleQueue.FormBody(solo).IsComplete);
        }

        // ---- Telemetry ----

        [Test]
        public void Telemetry_AveragesRoundLengthAndFirstContact()
        {
            var t = new M3Telemetry();
            t.RecordRound(100f);
            t.RecordRound(120f);
            t.RecordFirstContact(12f);
            t.RecordFirstContact(18f);

            Assert.AreEqual(2, t.RoundsPlayed);
            Assert.AreEqual(110f, t.AverageRoundSeconds(), 0.001f);
            Assert.AreEqual(15f, t.AverageTimeToFirstContact(), 0.001f);
        }
    }
}
