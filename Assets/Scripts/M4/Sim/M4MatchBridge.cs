using System.Collections.Generic;
using BeMyArms.M3;

namespace BeMyArms.M4
{
    /// <summary>
    /// Connects a pure <see cref="M4MatchProposal"/> to the shipped game: maps a Duel proposal onto
    /// the four M3 role slots used by the networked director, and applies a match result to each
    /// player's per-role rating. Pure and unit-testable; this is how matchmaking would hand a roster
    /// to the authoritative host.
    /// </summary>
    public static class M4MatchBridge
    {
        /// <summary>Maps a Duel proposal (1 body per team) to the four M3 role slots.</summary>
        public static bool TryBuildDuelSlots(M4MatchProposal proposal, out Dictionary<M3DuelSlot, string> slots)
        {
            slots = null;
            if (proposal == null || proposal.Mode != M4MatchMode.Duel || proposal.Assignments.Count != 4) return false;

            var map = new Dictionary<M3DuelSlot, string>();
            for (int i = 0; i < proposal.Assignments.Count; i++)
            {
                M4SlotAssignment a = proposal.Assignments[i];
                if (a.Slot.Team < 0 || a.Slot.Team > 1) return false;
                if (a.Slot.Role < 0 || a.Slot.Role > 1) return false;
                if (a.Slot.Body != 0) return false;

                M3DuelSlot slot = M3DuelSlots.FromTeamRole(a.Slot.Team, a.Slot.Role);
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
        public static void ApplyResult(M4MatchProposal proposal, int winningTeam, IDictionary<string, M4PlayerProfile> profiles, float k = M4Rating.KFactor)
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

        static void ApplyTeamRole(M4MatchProposal proposal, int team, int role, float opponentMmr, float score, IDictionary<string, M4PlayerProfile> profiles, float k)
        {
            for (int i = 0; i < proposal.Assignments.Count; i++)
            {
                M4SlotAssignment a = proposal.Assignments[i];
                if (a.Slot.Team != team || a.Slot.Role != role) continue;
                if (!profiles.TryGetValue(a.PlayerId, out M4PlayerProfile profile)) continue;
                M4Rating.ApplyOutcome(profile, role, opponentMmr, score, k);
            }
        }

        static float AverageRoleMmr(M4MatchProposal proposal, int team, int role)
        {
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < proposal.Assignments.Count; i++)
            {
                M4SlotAssignment a = proposal.Assignments[i];
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
