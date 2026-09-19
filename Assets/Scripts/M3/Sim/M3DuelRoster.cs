using System.Collections.Generic;

namespace BeMyArms.M3
{
    /// <summary>
    /// The four role slots of a Duel (one body per team): each team has a P1 and a P2. Slot value
    /// encodes team * 2 + role. For 2v2 (two bodies per team) use the generalized helpers that take
    /// a <c>bodiesPerTeam</c> argument; the encoding is <c>(team * bodiesPerTeam + body) * 2 + role</c>.
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

        // ---- Duel (one body per team) ----

        public static int Team(M3DuelSlot slot) => (byte)slot / RolesPerTeam;
        public static int Role(M3DuelSlot slot) => (byte)slot % RolesPerTeam;
        public static M3DuelSlot FromTeamRole(int team, int role) => (M3DuelSlot)(team * RolesPerTeam + role);
        public static bool IsValid(M3DuelSlot slot) => slot != M3DuelSlot.None && (byte)slot < Teams * RolesPerTeam;

        // ---- Generalized (one or more bodies per team) ----

        public static int SlotCount(int bodiesPerTeam) => Teams * bodiesPerTeam * RolesPerTeam;
        public static int Encode(int team, int body, int role, int bodiesPerTeam) => (team * bodiesPerTeam + body) * RolesPerTeam + role;
        public static int TeamOf(int slot, int bodiesPerTeam) => slot / (bodiesPerTeam * RolesPerTeam);
        public static int BodyOf(int slot, int bodiesPerTeam) => (slot / RolesPerTeam) % bodiesPerTeam;
        public static int RoleOf(int slot) => slot % RolesPerTeam;
        public static bool IsValidSlot(int slot, int bodiesPerTeam) => slot >= 0 && slot < SlotCount(bodiesPerTeam);

        public static string Name(int slot, int bodiesPerTeam)
        {
            if (!IsValidSlot(slot, bodiesPerTeam)) return "spectator";
            int team = TeamOf(slot, bodiesPerTeam);
            int body = BodyOf(slot, bodiesPerTeam);
            int role = RoleOf(slot);
            return (team == 0 ? "A" : "B") + body + (role == 0 ? "P1" : "P2");
        }

        public static string Name(M3DuelSlot slot) => IsValid(slot)
            ? (Team(slot) == 0 ? "A" : "B") + (Role(slot) == 0 ? "P1" : "P2")
            : "spectator";
    }

    /// <summary>A connection waiting in the pre-match queue (matchmaker mode, before slot assignment).</summary>
    public struct M3QueuedPlayer
    {
        public ulong ClientId;
        public string Token;
        /// <summary>0 = P1, 1 = P2, 2 = Either.</summary>
        public int RolePreference;
        public float EnqueueTime;
        public string Region;
    }

    /// <summary>
    /// Pure connection-to-slot binding for a match with one or more bodies per team, with
    /// token-based reconnect and temporary bot substitution. Also holds the pre-match queue used by
    /// the matchmaker-driven flow: connections are enqueued first and assigned slots once a match
    /// proposal exists.
    /// </summary>
    public class M3DuelRoster
    {
        public int BodiesPerTeam = 1;

        readonly Dictionary<ulong, int> _connectionSlot = new Dictionary<ulong, int>();
        readonly Dictionary<string, int> _tokenSlot = new Dictionary<string, int>();
        readonly HashSet<int> _botSlots = new HashSet<int>();
        readonly Dictionary<ulong, M3QueuedPlayer> _queue = new Dictionary<ulong, M3QueuedPlayer>();
        readonly Dictionary<string, M3QueuedPlayer> _tokenQueue = new Dictionary<string, M3QueuedPlayer>();

        public int SlotCount => M3DuelSlots.SlotCount(BodiesPerTeam);
        public int AssignedCount => _connectionSlot.Count;
        public int QueuedCount => _queue.Count;

        // ---- Queue (matchmaker mode) ----

        public void Enqueue(ulong clientId, string token, int rolePreference, float now, string region)
        {
            var entry = new M3QueuedPlayer
            {
                ClientId = clientId,
                Token = token ?? "",
                RolePreference = rolePreference,
                EnqueueTime = now,
                Region = region
            };
            _queue[clientId] = entry;
            if (!string.IsNullOrEmpty(entry.Token)) _tokenQueue[entry.Token] = entry;
            // If this token already had a slot (reconnect), reclaim it instead of queueing.
            if (!string.IsNullOrEmpty(entry.Token) && _tokenSlot.TryGetValue(entry.Token, out int remembered))
            {
                _queue.Remove(clientId);
                _tokenQueue.Remove(entry.Token);
                AssignSlot(clientId, entry.Token, remembered, out _);
            }
        }

        public bool IsQueued(ulong clientId) => _queue.ContainsKey(clientId);

        public bool Dequeue(ulong clientId)
        {
            if (!_queue.TryGetValue(clientId, out M3QueuedPlayer entry)) return false;
            _queue.Remove(clientId);
            if (!string.IsNullOrEmpty(entry.Token)) _tokenQueue.Remove(entry.Token);
            return true;
        }

        public List<M3QueuedPlayer> QueuedPlayers()
        {
            var list = new List<M3QueuedPlayer>(_queue.Count);
            foreach (var kvp in _queue) list.Add(kvp.Value);
            return list;
        }

        // ---- Slot assignment ----

        /// <summary>
        /// Assign a specific slot. Returns the slot and, when it is reclaimed from a still-connected
        /// previous owner, that displaced client id (else ulong.MaxValue).
        /// </summary>
        public int AssignSlot(ulong clientId, string token, int slot, out ulong displacedClientId)
        {
            displacedClientId = ulong.MaxValue;
            if (!M3DuelSlots.IsValidSlot(slot, BodiesPerTeam)) return -1;

            Dequeue(clientId);

            ulong owner = Owner(slot);
            if (owner != ulong.MaxValue && owner != clientId)
            {
                displacedClientId = owner;
                _connectionSlot.Remove(owner);
            }

            _connectionSlot[clientId] = slot;
            if (!string.IsNullOrEmpty(token)) _tokenSlot[token] = slot;
            _botSlots.Remove(slot);
            return slot;
        }

        /// <summary>Assign a requested slot when free, else any free slot (direct/dev mode).</summary>
        public int AssignPreferred(ulong clientId, string token, int desiredSlot, out ulong displacedClientId)
        {
            int target;
            if (!string.IsNullOrEmpty(token) && _tokenSlot.TryGetValue(token, out int remembered))
            {
                target = remembered;
            }
            else
            {
                target = M3DuelSlots.IsValidSlot(desiredSlot, BodiesPerTeam) && !SlotTaken(desiredSlot)
                    ? desiredSlot
                    : FindFreeSlot();
            }
            return AssignSlot(clientId, token, target, out displacedClientId);
        }

        public void SetBot(int slot)
        {
            if (M3DuelSlots.IsValidSlot(slot, BodiesPerTeam)) _botSlots.Add(slot);
        }

        public void ClearBot(int slot) => _botSlots.Remove(slot);
        public bool IsBot(int slot) => _botSlots.Contains(slot);

        public bool HasSingleOwner(int slot)
            => M3DuelSlots.IsValidSlot(slot, BodiesPerTeam) && (IsBot(slot) ^ SlotTaken(slot));

        public int Release(ulong clientId)
        {
            Dequeue(clientId);
            if (_connectionSlot.TryGetValue(clientId, out int slot))
            {
                _connectionSlot.Remove(clientId);
                return slot;
            }
            return -1;
        }

        public bool HasSlot(ulong clientId, int slot)
            => _connectionSlot.TryGetValue(clientId, out int s) && s == slot;

        public int SlotFor(ulong clientId)
            => _connectionSlot.TryGetValue(clientId, out int s) ? s : -1;

        public bool SlotTaken(int slot) => Owner(slot) != ulong.MaxValue;

        public ulong ClientForToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return ulong.MaxValue;
            for (int i = 0; i < SlotCount; i++)
            {
                if (SlotTaken(i) && TokenForSlot(i) == token)
                {
                    foreach (var kvp in _connectionSlot)
                        if (kvp.Value == i) return kvp.Key;
                }
            }
            return ulong.MaxValue;
        }

        public string TokenForSlot(int slot)
        {
            foreach (var kvp in _tokenSlot)
                if (kvp.Value == slot) return kvp.Key;
            return null;
        }

        public bool TryGetTokenSlot(string token, out int slot) => _tokenSlot.TryGetValue(token, out slot);

        /// <summary>Client id for a token that is still in the pre-match queue (before slot assignment).</summary>
        public bool TryGetQueuedClient(string token, out ulong clientId)
        {
            clientId = ulong.MaxValue;
            if (string.IsNullOrEmpty(token)) return false;
            if (_tokenQueue.TryGetValue(token, out M3QueuedPlayer entry))
            {
                clientId = entry.ClientId;
                return true;
            }
            return false;
        }

        ulong Owner(int slot)
        {
            foreach (var kvp in _connectionSlot)
                if (kvp.Value == slot) return kvp.Key;
            return ulong.MaxValue;
        }

        public int FindFreeSlot()
        {
            for (int i = 0; i < SlotCount; i++)
                if (!SlotTaken(i) && !IsBot(i)) return i;
            for (int i = 0; i < SlotCount; i++)
                if (!SlotTaken(i)) return i;
            return -1;
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
