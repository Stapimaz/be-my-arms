using System.Collections.Generic;
using BeMyArms.M3;

namespace BeMyArms.M4
{
    /// <summary>Shared combat bodies per team: Duel = 1, 2v2 = 2 (concept §14.1).</summary>
    public enum M4MatchMode
    {
        Duel,
        TwoVsTwo
    }

    /// <summary>One player waiting to be matched. MMRs are per-role (concept §18.2).</summary>
    public struct M4QueueEntry
    {
        public string PlayerId;
        /// <summary>Non-empty when queueing as a premade party; a duo forms one body.</summary>
        public string PartyId;
        public M3RolePreference Preference;
        public float P1Mmr;
        public float P2Mmr;
        public float WaitSeconds;
        public string Region;
        public string InputProfile;
    }

    /// <summary>A role slot inside a proposed match. <c>Body</c> indexes the body within its team.</summary>
    public struct M4MatchSlot
    {
        public int Team;
        public int Body;
        public int Role;

        public override string ToString() => $"T{Team}B{Body}{(Role == 0 ? "P1" : "P2")}";
    }

    public class M4SlotAssignment
    {
        public M4MatchSlot Slot;
        public string PlayerId;
        public float P1Mmr;
        public float P2Mmr;
    }

    /// <summary>Provider-neutral match proposal: who plays which slot, plus a quality score.</summary>
    public class M4MatchProposal
    {
        public M4MatchMode Mode;
        public int BodiesPerTeam;
        public readonly List<M4SlotAssignment> Assignments = new List<M4SlotAssignment>();
        public float TeamARating;
        public float TeamBRating;
        /// <summary>Lower is a better match (team rating gap).</summary>
        public float Quality;
        public float LongestWaitSeconds;
    }

    /// <summary>Matchmaking tolerances. All values are TUNING and relax with queue time.</summary>
    public class M4MatchmakingConfig
    {
        public float MaxBodyRoleGap = 400f;
        public float MaxTeamRatingGap = 250f;
        public float RelaxPerSecond = 15f;
        public float MaxRelax = 800f;
        public bool AllowEitherRole = true;
        public float GapPenalty = M4BodyMmr.DefaultGapPenalty;
    }

    /// <summary>
    /// Pure, provider-neutral matchmaker (concept §18). Forms complete shared bodies from solo and
    /// premade-duo players, respects role preferences and party constraints, then splits the bodies
    /// into balanced teams. Constraints relax as the longest wait grows. No networking, no services.
    /// </summary>
    public static class M4Matchmaker
    {
        struct Body
        {
            public M4QueueEntry P1;
            public M4QueueEntry P2;
        }

        public static bool TryMatch(IReadOnlyList<M4QueueEntry> entries, M4MatchMode mode, M4MatchmakingConfig config, out M4MatchProposal proposal)
        {
            proposal = null;
            if (entries == null || config == null) return false;

            int bodiesPerTeam = mode == M4MatchMode.Duel ? 1 : 2;
            int bodiesNeeded = bodiesPerTeam * 2;

            if (!FormBodies(entries, bodiesNeeded, config, out List<Body> bodies)) return false;

            float longestWait = 0f;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].WaitSeconds > longestWait) longestWait = entries[i].WaitSeconds;
            float relax = System.Math.Min(config.MaxRelax, config.RelaxPerSecond * longestWait);

            float[] bodyRatings = new float[bodies.Count];
            for (int i = 0; i < bodies.Count; i++)
            {
                float roleGap = System.Math.Abs(bodies[i].P1.P1Mmr - bodies[i].P2.P2Mmr);
                if (roleGap > config.MaxBodyRoleGap + relax) return false;
                bodyRatings[i] = M4BodyMmr.Derive(bodies[i].P1.P1Mmr, bodies[i].P2.P2Mmr, config.GapPenalty);
            }

            if (!SplitTeams(bodyRatings, bodiesPerTeam, out List<int> teamA, out float ratingA, out float ratingB)) return false;
            float teamGap = System.Math.Abs(ratingA - ratingB);
            if (teamGap > config.MaxTeamRatingGap + relax) return false;

            var proposalResult = new M4MatchProposal
            {
                Mode = mode,
                BodiesPerTeam = bodiesPerTeam,
                TeamARating = ratingA,
                TeamBRating = ratingB,
                Quality = teamGap,
                LongestWaitSeconds = longestWait
            };

            var teamASet = new HashSet<int>(teamA);
            int bodyIndexA = 0;
            int bodyIndexB = 0;
            for (int i = 0; i < bodies.Count; i++)
            {
                bool inA = teamASet.Contains(i);
                int team = inA ? 0 : 1;
                int bodyIndex = inA ? bodyIndexA++ : bodyIndexB++;
                AddAssignment(proposalResult, team, bodyIndex, 0, bodies[i].P1);
                AddAssignment(proposalResult, team, bodyIndex, 1, bodies[i].P2);
            }

            proposal = proposalResult;
            return true;
        }

        static void AddAssignment(M4MatchProposal proposal, int team, int body, int role, M4QueueEntry entry)
        {
            proposal.Assignments.Add(new M4SlotAssignment
            {
                Slot = new M4MatchSlot { Team = team, Body = body, Role = role },
                PlayerId = entry.PlayerId,
                P1Mmr = entry.P1Mmr,
                P2Mmr = entry.P2Mmr
            });
        }

        // ---- Body formation ----

        static bool FormBodies(IReadOnlyList<M4QueueEntry> entries, int bodiesNeeded, M4MatchmakingConfig config, out List<Body> bodies)
        {
            bodies = new List<Body>();
            bool[] used = new bool[entries.Count];

            // 1. Premade duos form one body together.
            for (int i = 0; i < entries.Count; i++)
            {
                if (used[i] || string.IsNullOrEmpty(entries[i].PartyId)) continue;
                int partner = -1;
                int count = 0;
                for (int j = 0; j < entries.Count; j++)
                {
                    if (used[j] || entries[j].PartyId != entries[i].PartyId) continue;
                    count++;
                    if (j != i) partner = j;
                }
                if (count > 2) return false; // parties larger than a duo are not yet supported here
                if (count < 2 || partner < 0) continue;

                if (!TryAssignRoles(entries[i], entries[partner], config.AllowEitherRole, out int roleA, out _)) return false;
                used[i] = true;
                used[partner] = true;
                bodies.Add(roleA == 0
                    ? new Body { P1 = entries[i], P2 = entries[partner] }
                    : new Body { P1 = entries[partner], P2 = entries[i] });
                if (bodies.Count > bodiesNeeded) return false;
            }

            // 2. Pair the remaining solo players by complementary role, closest skill first.
            while (bodies.Count < bodiesNeeded)
            {
                int anchor = -1;
                float anchorWait = -1f;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (used[i]) continue;
                    if (!config.AllowEitherRole && entries[i].Preference == M3RolePreference.Either) continue;
                    if (entries[i].WaitSeconds > anchorWait) { anchorWait = entries[i].WaitSeconds; anchor = i; }
                }
                if (anchor < 0) break;

                int bestPartner = -1;
                float bestGap = float.MaxValue;
                for (int j = 0; j < entries.Count; j++)
                {
                    if (used[j] || j == anchor) continue;
                    if (!config.AllowEitherRole && entries[j].Preference == M3RolePreference.Either) continue;
                    if (!TryAssignRoles(entries[anchor], entries[j], config.AllowEitherRole, out int roleA, out int roleB)) continue;
                    float gap = System.Math.Abs(RoleMmr(entries[anchor], roleA) - RoleMmr(entries[j], roleB));
                    if (gap < bestGap) { bestGap = gap; bestPartner = j; }
                }
                if (bestPartner < 0) break;

                TryAssignRoles(entries[anchor], entries[bestPartner], config.AllowEitherRole, out int pickedRoleA, out int pickedRoleB);
                used[anchor] = true;
                used[bestPartner] = true;
                bodies.Add(pickedRoleA == 0
                    ? new Body { P1 = entries[anchor], P2 = entries[bestPartner] }
                    : new Body { P1 = entries[bestPartner], P2 = entries[anchor] });
                _ = pickedRoleB;
            }

            return bodies.Count == bodiesNeeded;
        }

        /// <summary>True when the two entries can share a body; returns the role (0/1) of <paramref name="a"/>.</summary>
        static bool TryAssignRoles(M4QueueEntry a, M4QueueEntry b, bool allowEither, out int roleA, out int roleB)
        {
            roleA = 0;
            roleB = 1;

            M3RolePreference pa = allowEither ? a.Preference : (a.Preference == M3RolePreference.Either ? M3RolePreference.P1 : a.Preference);
            M3RolePreference pb = allowEither ? b.Preference : (b.Preference == M3RolePreference.Either ? M3RolePreference.P2 : b.Preference);

            if (pa == M3RolePreference.P1 && pb == M3RolePreference.P2) { roleA = 0; roleB = 1; return true; }
            if (pa == M3RolePreference.P2 && pb == M3RolePreference.P1) { roleA = 1; roleB = 0; return true; }
            if (pa == M3RolePreference.P1 && pb == M3RolePreference.Either) { roleA = 0; roleB = 1; return true; }
            if (pa == M3RolePreference.P2 && pb == M3RolePreference.Either) { roleA = 1; roleB = 0; return true; }
            if (pa == M3RolePreference.Either && pb == M3RolePreference.P1) { roleA = 1; roleB = 0; return true; }
            if (pa == M3RolePreference.Either && pb == M3RolePreference.P2) { roleA = 0; roleB = 1; return true; }
            if (pa == M3RolePreference.Either && pb == M3RolePreference.Either) { roleA = 0; roleB = 1; return true; }
            return false;
        }

        static float RoleMmr(M4QueueEntry entry, int role) => role == 0 ? entry.P1Mmr : entry.P2Mmr;

        // ---- Team split ----

        /// <summary>Chooses the team split (bodiesPerTeam per team) with the smallest rating gap.</summary>
        static bool SplitTeams(float[] bodyRatings, int bodiesPerTeam, out List<int> teamA, out float ratingA, out float ratingB)
        {
            teamA = null;
            ratingA = 0f;
            ratingB = 0f;
            int count = bodyRatings.Length;
            if (count != bodiesPerTeam * 2) return false;

            float bestGap = float.MaxValue;
            List<int> best = null;

            // Body 0 is pinned to team A so symmetric splits are not enumerated twice.
            int combinations = 1 << count;
            for (int mask = 0; mask < combinations; mask++)
            {
                if ((mask & 1) == 0) continue;
                if (CountBits(mask) != bodiesPerTeam) continue;

                float sumA = 0f;
                float sumB = 0f;
                var listA = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    if ((mask & (1 << i)) != 0) { sumA += bodyRatings[i]; listA.Add(i); }
                    else sumB += bodyRatings[i];
                }
                float gap = System.Math.Abs(sumA / bodiesPerTeam - sumB / bodiesPerTeam);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = listA;
                    ratingA = sumA / bodiesPerTeam;
                    ratingB = sumB / bodiesPerTeam;
                }
            }

            if (best == null) return false;
            teamA = best;
            return true;
        }

        static int CountBits(int value)
        {
            int count = 0;
            while (value != 0) { count += value & 1; value >>= 1; }
            return count;
        }
    }
}
