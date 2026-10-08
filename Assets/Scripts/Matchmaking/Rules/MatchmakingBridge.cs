using System.Collections.Generic;
using BeMyArms.Match;

namespace BeMyArms.Matchmaking
{
    /// <summary>
    /// Connects a pure <see cref="MatchProposal"/> to the shipped game: maps a Duel proposal onto
    /// the four Match role slots used by the networked director, and applies a match result to each
    /// player's per-role rating. Pure and unit-testable; this is how matchmaking would hand a roster
    /// to the authoritative host.
    /// </summary>
    public static class MatchmakingBridge
    {
        /// <summary>Maps a Duel proposal (1 body per team) to the four Match role slots.</summary>
        public static bool TryBuildDuelSlots(MatchProposal proposal, out Dictionary<MatchSlot, string> slots)
        {
            slots = null;
            if (proposal == null || proposal.Mode != MatchMode.Duel || proposal.Assignments.Count != 4) return false;

            var map = new Dictionary<MatchSlot, string>();
            for (int i = 0; i < proposal.Assignments.Count; i++)
            {
                MatchSlotAssignment a = proposal.Assignments[i];
                if (a.Slot.Team < 0 || a.Slot.Team > 1) return false;
                if (a.Slot.Role < 0 || a.Slot.Role > 1) return false;
                if (a.Slot.Body != 0) return false;

                MatchSlot slot = MatchSlots.FromTeamRole(a.Slot.Team, a.Slot.Role);
                if (map.ContainsKey(slot)) return false;
                map[slot] = a.PlayerId;
            }

            if (map.Count != 4) return false;
            slots = map;
            return true;
        }

        /// <summary>
        /// Applies a result to the role-specific ratings of every player in the proposal.
        /// <paramref name="winningTeam"/> is 0, 1, or -1 for a draw. Each played role is compared
        /// against the average of the opposing team's same role (2v2 aware).
        /// </summary>
        public static void ApplyResult(MatchProposal proposal, int winningTeam, IDictionary<string, MatchmakingPlayerProfile> profiles, float k = RoleRating.KFactor)
        {
            if (proposal == null || profiles == null) return;

            for (int role = 0; role < 2; role++)
            {
                float opponentForTeamA = AverageRoleMmr(proposal, team: 1, role);
                float opponentForTeamB = AverageRoleMmr(proposal, team: 0, role);

                ApplyTeamRole(proposal, team: 0, role, opponentForTeamB, ScoreFor(winningTeam, 0), profiles, k);
                ApplyTeamRole(proposal, team: 1, role, opponentForTeamA, ScoreFor(winningTeam, 1), profiles, k);
            }
        }

        static void ApplyTeamRole(MatchProposal proposal, int team, int role, float opponentMmr, float score, IDictionary<string, MatchmakingPlayerProfile> profiles, float k)
        {
            for (int i = 0; i < proposal.Assignments.Count; i++)
            {
                MatchSlotAssignment a = proposal.Assignments[i];
                if (a.Slot.Team != team || a.Slot.Role != role) continue;
                if (!profiles.TryGetValue(a.PlayerId, out MatchmakingPlayerProfile profile)) continue;
                RoleRating.ApplyOutcome(profile, role, opponentMmr, score, k);
            }
        }

        static float AverageRoleMmr(MatchProposal proposal, int team, int role)
        {
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < proposal.Assignments.Count; i++)
            {
                MatchSlotAssignment a = proposal.Assignments[i];
                if (a.Slot.Team != team || a.Slot.Role != role) continue;
                sum += role == 0 ? a.P1Mmr : a.P2Mmr;
                count++;
            }
            return count == 0 ? 0f : sum / count;
        }

        static float ScoreFor(int winningTeam, int team)
            => winningTeam < 0 ? 0.5f : (winningTeam == team ? 1f : 0f);
    }
}
