using System.Collections.Generic;
using System.Linq;
using BeMyArms.M3;
using NUnit.Framework;

namespace BeMyArms.M4.Tests
{
    /// <summary>Role-specific rating, derived body MMR, matchmaking and provider-neutral allocation.</summary>
    public class M4MatchmakingTests
    {
        static M4QueueEntry E(string id, M3RolePreference pref, float p1, float p2, string party = null, float wait = 0f)
            => new M4QueueEntry { PlayerId = id, Preference = pref, P1Mmr = p1, P2Mmr = p2, PartyId = party, WaitSeconds = wait };

        static M4MatchmakingConfig Config() => new M4MatchmakingConfig();

        // ---- Rating ----

        [Test]
        public void Rating_UpdatesOnlyThePlayedRole()
        {
            var profile = new M4PlayerProfile { PlayerId = "p", P1Mmr = 1000f, P2Mmr = 1000f };
            M4Rating.ApplyOutcome(profile, role: 0, opponentMmr: 1000f, score: 1f);

            Assert.Greater(profile.P1Mmr, 1000f, "Winner gains P1 rating.");
            Assert.AreEqual(1000f, profile.P2Mmr, 0.0001f, "P2 rating is untouched.");
        }

        [Test]
        public void Rating_ExpectedScore_IsFairAndMonotonic()
        {
            Assert.AreEqual(0.5f, M4Rating.ExpectedScore(1000f, 1000f), 0.0001f);
            Assert.Greater(M4Rating.ExpectedScore(1200f, 1000f), 0.5f, "Higher rating expects to win.");
            Assert.Less(M4Rating.ExpectedScore(800f, 1000f), 0.5f);

            // An upset gains more than a favourite's expected win.
            float upset = M4Rating.Update(800f, 1200f, 1f) - 800f;
            float expected = M4Rating.Update(1200f, 800f, 1f) - 1200f;
            Assert.Greater(upset, expected);
        }

        // ---- Body MMR ----

        [Test]
        public void BodyMmr_PenalizesRoleGap()
        {
            Assert.AreEqual(1000f, M4BodyMmr.Derive(1000f, 1000f), 0.0001f);
            Assert.AreEqual(940f, M4BodyMmr.Derive(1200f, 800f, 0.15f), 0.0001f, "Gap penalty applies.");
            Assert.AreEqual(1000f, M4BodyMmr.Derive(1200f, 800f, 0f), 0.0001f, "k = 0 collapses to the average.");
        }

        [Test]
        public void BodyMmr_TeamRatingIsTheBodyAverage()
        {
            Assert.AreEqual(1000f, M4BodyMmr.TeamRating(1100f, 900f), 0.0001f);
        }

        // ---- Duel matchmaking ----

        [Test]
        public void Matchmaker_Duel_FormsTwoBodies_AndHonorsRoles()
        {
            var entries = new List<M4QueueEntry>
            {
                E("a1", M3RolePreference.P1, 1000f, 1000f),
                E("b1", M3RolePreference.P1, 1000f, 1000f),
                E("a2", M3RolePreference.P2, 1000f, 1000f),
                E("b2", M3RolePreference.P2, 1000f, 1000f),
            };

            Assert.IsTrue(M4Matchmaker.TryMatch(entries, M4MatchMode.Duel, Config(), out M4MatchProposal proposal));
            Assert.AreEqual(1, proposal.BodiesPerTeam);
            Assert.AreEqual(4, proposal.Assignments.Count);

            foreach (M4SlotAssignment a in proposal.Assignments)
            {
                M4QueueEntry source = entries.First(e => e.PlayerId == a.PlayerId);
                int expectedRole = source.Preference == M3RolePreference.P1 ? 0 : 1;
                Assert.AreEqual(expectedRole, a.Slot.Role, "Role preference is honored.");
            }

            Assert.AreEqual(2, proposal.Assignments.Select(a => a.Slot.Team).Distinct().Count());
        }

        [Test]
        public void Matchmaker_Duel_RejectsUnfillableBodies()
        {
            var entries = new List<M4QueueEntry>
            {
                E("a", M3RolePreference.P1, 1000f, 1000f),
                E("b", M3RolePreference.P1, 1000f, 1000f),
                E("c", M3RolePreference.P1, 1000f, 1000f),
                E("d", M3RolePreference.P1, 1000f, 1000f),
            };

            Assert.IsFalse(M4Matchmaker.TryMatch(entries, M4MatchMode.Duel, Config(), out _),
                "Four P1-only players cannot form two P1+P2 bodies.");
        }

        [Test]
        public void Matchmaker_Duel_EitherFillsComplementaryRole()
        {
            var entries = new List<M4QueueEntry>
            {
                E("a", M3RolePreference.P1, 1000f, 1000f),
                E("b", M3RolePreference.Either, 1000f, 1000f),
                E("c", M3RolePreference.P1, 1000f, 1000f),
                E("d", M3RolePreference.Either, 1000f, 1000f),
            };

            Assert.IsTrue(M4Matchmaker.TryMatch(entries, M4MatchMode.Duel, Config(), out M4MatchProposal proposal));
            Assert.AreEqual(4, proposal.Assignments.Count);
            Assert.IsTrue(proposal.Assignments.Any(a => a.PlayerId == "b" && a.Slot.Role == 1));
            Assert.IsTrue(proposal.Assignments.Any(a => a.PlayerId == "d" && a.Slot.Role == 1));
        }

        // ---- Parties ----

        [Test]
        public void Matchmaker_KeepsPremadeDuoOnOneBody()
        {
            var entries = new List<M4QueueEntry>
            {
                E("duoP1", M3RolePreference.P1, 1000f, 1000f, party: "duo"),
                E("duoP2", M3RolePreference.P2, 1000f, 1000f, party: "duo"),
                E("soloP1", M3RolePreference.P1, 1000f, 1000f),
                E("soloP2", M3RolePreference.P2, 1000f, 1000f),
            };

            Assert.IsTrue(M4Matchmaker.TryMatch(entries, M4MatchMode.Duel, Config(), out M4MatchProposal proposal));
            M4SlotAssignment duoP1 = proposal.Assignments.First(a => a.PlayerId == "duoP1");
            M4SlotAssignment duoP2 = proposal.Assignments.First(a => a.PlayerId == "duoP2");
            Assert.AreEqual(duoP1.Slot.Team, duoP2.Slot.Team, "Party stays on the same team.");
            Assert.AreEqual(duoP1.Slot.Body, duoP2.Slot.Body, "Party stays on the same body.");
        }

        // ---- 2v2 ----

        [Test]
        public void Matchmaker_TwoVsTwo_FormsFourBodies_TwoPerTeam()
        {
            var entries = new List<M4QueueEntry>();
            for (int i = 0; i < 4; i++) entries.Add(E($"p1_{i}", M3RolePreference.P1, 1000f, 1000f));
            for (int i = 0; i < 4; i++) entries.Add(E($"p2_{i}", M3RolePreference.P2, 1000f, 1000f));

            Assert.IsTrue(M4Matchmaker.TryMatch(entries, M4MatchMode.TwoVsTwo, Config(), out M4MatchProposal proposal));
            Assert.AreEqual(2, proposal.BodiesPerTeam);
            Assert.AreEqual(8, proposal.Assignments.Count);

            for (int team = 0; team < 2; team++)
            {
                int bodies = proposal.Assignments.Where(a => a.Slot.Team == team).Select(a => a.Slot.Body).Distinct().Count();
                Assert.AreEqual(2, bodies, $"Team {team} has two bodies.");
            }
        }

        [Test]
        public void Matchmaker_TwoVsTwo_BalancesTeamsByRating()
        {
            var entries = new List<M4QueueEntry>
            {
                E("s1p1", M3RolePreference.P1, 1600f, 1600f),
                E("s1p2", M3RolePreference.P2, 1600f, 1600f),
                E("w1p1", M3RolePreference.P1, 400f, 400f),
                E("w1p2", M3RolePreference.P2, 400f, 400f),
                E("s2p1", M3RolePreference.P1, 1600f, 1600f),
                E("s2p2", M3RolePreference.P2, 1600f, 1600f),
                E("w2p1", M3RolePreference.P1, 400f, 400f),
                E("w2p2", M3RolePreference.P2, 400f, 400f),
            };

            Assert.IsTrue(M4Matchmaker.TryMatch(entries, M4MatchMode.TwoVsTwo, Config(), out M4MatchProposal proposal));
            Assert.AreEqual(proposal.TeamARating, proposal.TeamBRating, 0.0001f, "Strong and weak bodies are split across teams.");
            Assert.AreEqual(0f, proposal.Quality, 0.0001f);
        }

        // ---- Constraint relaxation ----

        [Test]
        public void Matchmaker_RelaxesTeamTolerance_WithQueueTime()
        {
            var tight = new M4MatchmakingConfig { MaxTeamRatingGap = 250f, RelaxPerSecond = 0f, MaxRelax = 0f };
            var entries = new List<M4QueueEntry>
            {
                E("strongP1", M3RolePreference.P1, 1500f, 1500f),
                E("strongP2", M3RolePreference.P2, 1500f, 1500f),
                E("weakP1", M3RolePreference.P1, 500f, 500f),
                E("weakP2", M3RolePreference.P2, 500f, 500f),
            };

            Assert.IsFalse(M4Matchmaker.TryMatch(entries, M4MatchMode.Duel, tight, out _),
                "A 1000-point team gap is rejected immediately.");

            for (int i = 0; i < entries.Count; i++)
            {
                M4QueueEntry e = entries[i];
                e.WaitSeconds = 100f;
                entries[i] = e;
            }

            Assert.IsTrue(M4Matchmaker.TryMatch(entries, M4MatchMode.Duel, Config(), out M4MatchProposal proposal),
                "Waiting long enough widens the acceptable team gap.");
            Assert.AreEqual(100f, proposal.LongestWaitSeconds, 0.0001f);
        }

        // ---- Provider-neutral allocation ----

        [Test]
        public void Allocation_LocalAllocator_HandsOutProviderNeutralTickets()
        {
            var entries = new List<M4QueueEntry>
            {
                E("a1", M3RolePreference.P1, 1000f, 1000f),
                E("a2", M3RolePreference.P2, 1000f, 1000f),
                E("b1", M3RolePreference.P1, 1000f, 1000f),
                E("b2", M3RolePreference.P2, 1000f, 1000f),
            };
            Assert.IsTrue(M4Matchmaker.TryMatch(entries, M4MatchMode.Duel, Config(), out M4MatchProposal proposal));

            IM4ServerAllocator allocator = new M4LocalAllocator();
            M4ServerTicket first = allocator.Allocate(proposal);
            M4ServerTicket second = allocator.Allocate(proposal);

            Assert.IsNotNull(first);
            Assert.IsNotNull(first.Endpoint);
            Assert.IsNotEmpty(first.JoinToken);
            Assert.AreNotEqual(first.MatchId, second.MatchId, "Each match gets a unique id.");

            var local = (M4LocalAllocator)allocator;
            Assert.AreEqual(2, local.ActiveMatches);
            local.Release(first.MatchId);
            Assert.AreEqual(1, local.ActiveMatches);
        }

        // ---- Bridge to the shipped Duel ----

        [Test]
        public void Bridge_DuelProposal_MapsToFourUniqueSlots()
        {
            var entries = new List<M4QueueEntry>
            {
                E("a1", M3RolePreference.P1, 1000f, 1000f),
                E("a2", M3RolePreference.P2, 1000f, 1000f),
                E("b1", M3RolePreference.P1, 1000f, 1000f),
                E("b2", M3RolePreference.P2, 1000f, 1000f),
            };
            Assert.IsTrue(M4Matchmaker.TryMatch(entries, M4MatchMode.Duel, Config(), out M4MatchProposal proposal));

            Assert.IsTrue(M4MatchBridge.TryBuildDuelSlots(proposal, out Dictionary<M3DuelSlot, string> slots));
            Assert.AreEqual(4, slots.Count);
            Assert.AreEqual(4, slots.Values.Distinct().Count(), "Every slot holds a distinct player.");
            Assert.IsTrue(slots.ContainsKey(M3DuelSlot.TeamAP1) && slots.ContainsKey(M3DuelSlot.TeamAP2));
            Assert.IsTrue(slots.ContainsKey(M3DuelSlot.TeamBP1) && slots.ContainsKey(M3DuelSlot.TeamBP2));
        }

        [Test]
        public void Bridge_ApplyResult_UpdatesOnlyPlayedRoles()
        {
            var entries = new List<M4QueueEntry>
            {
                E("a1", M3RolePreference.P1, 1000f, 1000f),
                E("a2", M3RolePreference.P2, 1000f, 1000f),
                E("b1", M3RolePreference.P1, 1000f, 1000f),
                E("b2", M3RolePreference.P2, 1000f, 1000f),
            };
            Assert.IsTrue(M4Matchmaker.TryMatch(entries, M4MatchMode.Duel, Config(), out M4MatchProposal proposal));

            var profiles = new Dictionary<string, M4PlayerProfile>();
            for (int i = 0; i < entries.Count; i++)
                profiles[entries[i].PlayerId] = new M4PlayerProfile { PlayerId = entries[i].PlayerId, P1Mmr = entries[i].P1Mmr, P2Mmr = entries[i].P2Mmr };

            M4MatchBridge.ApplyResult(proposal, winningTeam: 0, profiles);

            foreach (M4SlotAssignment a in proposal.Assignments)
            {
                M4PlayerProfile p = profiles[a.PlayerId];
                bool winner = a.Slot.Team == 0;
                if (a.Slot.Role == 0)
                {
                    Assert.AreEqual(winner, p.P1Mmr > 1000f, $"P1 rating moved for {a.PlayerId}.");
                    Assert.AreEqual(1000f, p.P2Mmr, 0.0001f, "Unplayed role is untouched.");
                }
                else
                {
                    Assert.AreEqual(winner, p.P2Mmr > 1000f, $"P2 rating moved for {a.PlayerId}.");
                    Assert.AreEqual(1000f, p.P1Mmr, 0.0001f, "Unplayed role is untouched.");
                }
            }
        }
    }
}
