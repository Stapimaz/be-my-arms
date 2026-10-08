using System.Collections.Generic;

namespace BeMyArms.Match
{
    public enum RolePreference
    {
        P1,
        P2,
        Either
    }

    public struct RoleQueueEntry
    {
        public string PlayerId;
        public RolePreference Preference;
        public string PartyId;
    }

    public struct BodyPair
    {
        public string P1;
        public string P2;
        public bool IsComplete => !string.IsNullOrEmpty(P1) && !string.IsNullOrEmpty(P2);
    }

    /// <summary>
    /// Role queue: players may queue as P1, P2, or Either; solo queue finds the complementary role.
    /// Pure and testable. Party/MMR constraints are handled by the matchmaker above this.
    /// </summary>
    public static class RoleQueue
    {
        public static BodyPair FormBody(IReadOnlyList<RoleQueueEntry> entries)
        {
            var pair = new BodyPair();

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Preference == RolePreference.P1 && pair.P1 == null) pair.P1 = entries[i].PlayerId;
                else if (entries[i].Preference == RolePreference.P2 && pair.P2 == null) pair.P2 = entries[i].PlayerId;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Preference != RolePreference.Either) continue;
                if (pair.P1 == null) pair.P1 = entries[i].PlayerId;
                else if (pair.P2 == null) pair.P2 = entries[i].PlayerId;
            }

            return pair;
        }
    }
}
