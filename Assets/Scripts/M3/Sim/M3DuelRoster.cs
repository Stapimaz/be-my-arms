using System.Collections.Generic;

namespace BeMyArms.M3
{
    /// <summary>
    /// The four role slots of a Duel: each team has a P1 (body/legs) and a P2 (arms/aim).
    /// Slot value encodes team * 2 + role so it can be used directly as a network byte.
    /// </summary>
    public enum M3DuelSlot : byte
    {
        TeamAP1 = 0,
        TeamAP2 = 1,
        TeamBP1 = 2,
        TeamBP2 = 3,
        None = 255
    }

    public static class M3DuelSlots
    {
        public const int Teams = 2;
        public const int RolesPerTeam = 2;

        public static int Team(M3DuelSlot slot) => (byte)slot / RolesPerTeam;
        public static int Role(M3DuelSlot slot) => (byte)slot % RolesPerTeam;
        public static M3DuelSlot FromTeamRole(int team, int role) => (M3DuelSlot)(team * RolesPerTeam + role);
        public static bool IsValid(M3DuelSlot slot) => slot != M3DuelSlot.None && (byte)slot < Teams * RolesPerTeam;
        public static string Name(M3DuelSlot slot) => IsValid(slot)
            ? (Team(slot) == 0 ? "A" : "B") + (Role(slot) == 0 ? "P1" : "P2")
            : "spectator";
    }

    /// <summary>
    /// Pure connection-to-slot binding for the Duel, with token-based reconnect and temporary bot
    /// substitution (same product policy as M2, extended from two roles to four slots). Handles the
    /// double-role edge: when a token reconnects while its previous connection still holds the slot,
    /// the old connection is reported as displaced so the server can drop it and safely reclaim.
    /// </summary>
    public class M3DuelRoster
    {
        readonly Dictionary<ulong, M3DuelSlot> _connectionSlot = new Dictionary<ulong, M3DuelSlot>();
        readonly Dictionary<string, M3DuelSlot> _tokenSlot = new Dictionary<string, M3DuelSlot>();
        readonly HashSet<M3DuelSlot> _botSlots = new HashSet<M3DuelSlot>();

        /// <summary>
        /// Assign a slot to a connection. Returns the assigned slot and, when a slot is reclaimed
        /// from a still-connected previous owner, that displaced client id (else ulong.MaxValue).
        /// </summary>
        public M3DuelSlot Assign(ulong clientId, string token, int desiredTeam, int desiredRole, out ulong displacedClientId)
        {
            displacedClientId = ulong.MaxValue;

            M3DuelSlot target;
            if (!string.IsNullOrEmpty(token) && _tokenSlot.TryGetValue(token, out M3DuelSlot remembered))
            {
                target = remembered;
            }
            else
            {
                M3DuelSlot desired = M3DuelSlots.FromTeamRole(desiredTeam, desiredRole);
                target = M3DuelSlots.IsValid(desired) && !SlotTaken(desired) ? desired : FindFreeSlot();
            }

            if (target != M3DuelSlot.None)
            {
                ulong owner = Owner(target);
                if (owner != ulong.MaxValue && owner != clientId)
                {
                    displacedClientId = owner;
                    _connectionSlot.Remove(owner);
                }
            }

            _connectionSlot[clientId] = target;
            if (!string.IsNullOrEmpty(token) && target != M3DuelSlot.None) _tokenSlot[token] = target;
            if (target != M3DuelSlot.None) _botSlots.Remove(target); // a human owner always clears any bot
            return target;
        }

        /// <summary>Temporarily hand a slot to a bot (e.g. while its human is disconnected).</summary>
        public void SetBot(M3DuelSlot slot)
        {
            if (slot != M3DuelSlot.None) _botSlots.Add(slot);
        }

        public void ClearBot(M3DuelSlot slot) => _botSlots.Remove(slot);
        public bool IsBot(M3DuelSlot slot) => _botSlots.Contains(slot);

        /// <summary>True when exactly one owner (human or bot) holds the slot — never two.</summary>
        public bool HasSingleOwner(M3DuelSlot slot)
            => slot != M3DuelSlot.None && (IsBot(slot) ^ SlotTaken(slot));

        public M3DuelSlot Release(ulong clientId)
        {
            if (_connectionSlot.TryGetValue(clientId, out M3DuelSlot slot))
            {
                _connectionSlot.Remove(clientId);
                return slot;
            }
            return M3DuelSlot.None;
        }

        public bool HasSlot(ulong clientId, M3DuelSlot slot)
            => _connectionSlot.TryGetValue(clientId, out M3DuelSlot s) && s == slot;

        public M3DuelSlot SlotFor(ulong clientId)
            => _connectionSlot.TryGetValue(clientId, out M3DuelSlot s) ? s : M3DuelSlot.None;

        public bool SlotTaken(M3DuelSlot slot) => Owner(slot) != ulong.MaxValue;
        public int AssignedCount => _connectionSlot.Count;

        ulong Owner(M3DuelSlot slot)
        {
            foreach (var kvp in _connectionSlot)
                if (kvp.Value == slot) return kvp.Key;
            return ulong.MaxValue;
        }

        M3DuelSlot FindFreeSlot()
        {
            for (int i = 0; i < M3DuelSlots.Teams * M3DuelSlots.RolesPerTeam; i++)
            {
                var slot = (M3DuelSlot)i;
                if (!SlotTaken(slot)) return slot;
            }
            return M3DuelSlot.None;
        }

        /// <summary>
        /// Wires the role queue into the pre-match flow: forms two complete shared bodies (team A and
        /// team B) from four queued players. Exact P1/P2 preferences are filled first, then Either.
        /// </summary>
        public static void FormTeams(IReadOnlyList<M3QueueEntry> entries, out M3BodyPair teamA, out M3BodyPair teamB)
        {
            teamA = M3RoleQueue.FormBody(entries);

            var remaining = new List<M3QueueEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].PlayerId == teamA.P1 || entries[i].PlayerId == teamA.P2) continue;
                remaining.Add(entries[i]);
            }
            teamB = M3RoleQueue.FormBody(remaining);
        }
    }
}
