using System.Collections.Generic;

namespace BeMyArms.M5
{
    public enum M5FriendStatus
    {
        None,
        PendingOutgoing,
        PendingIncoming,
        Accepted,
        Blocked
    }

    public class M5FriendEntry
    {
        public string PlayerId;
        public string DisplayName;
        public M5FriendStatus Status;
    }

    /// <summary>
    /// Platform-neutral social seams. Friends, invites and blocking are foundations only; the voice
    /// and social provider is deliberately not chosen (concept §19.2).
    /// </summary>
    public interface IM5SocialProvider
    {
        M5FriendEntry Get(string playerId);
        IReadOnlyList<M5FriendEntry> Entries();
        void SendRequest(string playerId, string displayName);
        void AcceptRequest(string playerId);
        void RemoveFriend(string playerId);
        void Block(string playerId);
        void Unblock(string playerId);
    }

    public class M5InMemorySocialProvider : IM5SocialProvider
    {
        readonly Dictionary<string, M5FriendEntry> _entries = new Dictionary<string, M5FriendEntry>();

        public IReadOnlyList<M5FriendEntry> Entries()
        {
            var list = new List<M5FriendEntry>(_entries.Values);
            return list;
        }

        public M5FriendEntry Get(string playerId)
            => !string.IsNullOrEmpty(playerId) && _entries.TryGetValue(playerId, out M5FriendEntry entry) ? entry : null;

        public void SendRequest(string playerId, string displayName)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (_entries.TryGetValue(playerId, out M5FriendEntry existing))
            {
                if (existing.Status == M5FriendStatus.PendingIncoming) { existing.Status = M5FriendStatus.Accepted; return; }
                if (existing.Status == M5FriendStatus.Blocked) return;
                existing.Status = M5FriendStatus.PendingOutgoing;
                if (!string.IsNullOrEmpty(displayName)) existing.DisplayName = displayName;
                return;
            }
            _entries[playerId] = new M5FriendEntry { PlayerId = playerId, DisplayName = displayName ?? playerId, Status = M5FriendStatus.PendingOutgoing };
        }

        public void AcceptRequest(string playerId)
        {
            if (_entries.TryGetValue(playerId, out M5FriendEntry entry) && entry.Status != M5FriendStatus.Blocked)
                entry.Status = M5FriendStatus.Accepted;
        }

        public void RemoveFriend(string playerId) => _entries.Remove(playerId);

        public void Block(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (_entries.TryGetValue(playerId, out M5FriendEntry entry)) entry.Status = M5FriendStatus.Blocked;
            else _entries[playerId] = new M5FriendEntry { PlayerId = playerId, DisplayName = playerId, Status = M5FriendStatus.Blocked };
        }

        public void Unblock(string playerId)
        {
            if (_entries.TryGetValue(playerId, out M5FriendEntry entry) && entry.Status == M5FriendStatus.Blocked)
                _entries.Remove(playerId);
        }
    }
}
