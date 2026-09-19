using System.Collections.Generic;

namespace BeMyArms.M3
{
    public enum M3RolePreference
    {
        P1,
        P2,
        Either
    }

    public struct M3QueueEntry
    {
        public string PlayerId;
        public M3RolePreference Preference;
        public string PartyId;
    }

    public struct M3BodyPair
    {
        public string P1;
        public string P2;
        public bool IsComplete => !string.IsNullOrEmpty(P1) && !string.IsNullOrEmpty(P2);
    }

    /// <summary>
    /// Role queue: players may queue as P1, P2, or Either; solo queue finds the complementary role.
    /// Pure and testable. Party/MMR constraints are handled by the matchmaker above this.
    /// </summary>
    public static class M3RoleQueue
    {
        public static M3BodyPair FormBody(IReadOnlyList<M3QueueEntry> entries)
        {
            var pair = new M3BodyPair();

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Preference == M3RolePreference.P1 && pair.P1 == null) pair.P1 = entries[i].PlayerId;
                else if (entries[i].Preference == M3RolePreference.P2 && pair.P2 == null) pair.P2 = entries[i].PlayerId;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Preference != M3RolePreference.Either) continue;
                if (pair.P1 == null) pair.P1 = entries[i].PlayerId;
                else if (pair.P2 == null) pair.P2 = entries[i].PlayerId;
            }

            return pair;
        }
    }
}
