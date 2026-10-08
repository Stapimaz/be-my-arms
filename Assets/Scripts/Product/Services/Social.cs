using System.Collections.Generic;

namespace BeMyArms.Product
{
    public enum FriendStatus
    {
        None,
        PendingOutgoing,
        PendingIncoming,
        Accepted,
        Blocked
    }

    public class FriendEntry
    {
        public string PlayerId;
        public string DisplayName;
        public FriendStatus Status;
    }

    /// <summary>
    /// Platform-neutral social seams. Friends, invites and blocking are foundations only; the voice
    /// and social provider is deliberately not chosen (concept §19.2).
    /// </summary>
    public interface ISocialProvider
    {
        FriendEntry Get(string playerId);
        IReadOnlyList<FriendEntry> Entries();
        void SendRequest(string playerId, string displayName);
        void AcceptRequest(string playerId);
        void RemoveFriend(string playerId);
        void Block(string playerId);
        void Unblock(string playerId);
    }

    public class InMemorySocialProvider : ISocialProvider
    {
        readonly Dictionary<string, FriendEntry> _entries = new Dictionary<string, FriendEntry>();

        public IReadOnlyList<FriendEntry> Entries()
        {
            var list = new List<FriendEntry>(_entries.Values);
            return list;
        }

        public FriendEntry Get(string playerId)
            => !string.IsNullOrEmpty(playerId) && _entries.TryGetValue(playerId, out FriendEntry entry) ? entry : null;

        public void SendRequest(string playerId, string displayName)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (_entries.TryGetValue(playerId, out FriendEntry existing))
            {
                if (existing.Status == FriendStatus.PendingIncoming) { existing.Status = FriendStatus.Accepted; return; }
                if (existing.Status == FriendStatus.Blocked) return;
                existing.Status = FriendStatus.PendingOutgoing;
                if (!string.IsNullOrEmpty(displayName)) existing.DisplayName = displayName;
                return;
            }
            _entries[playerId] = new FriendEntry { PlayerId = playerId, DisplayName = displayName ?? playerId, Status = FriendStatus.PendingOutgoing };
        }

        public void AcceptRequest(string playerId)
        {
            if (_entries.TryGetValue(playerId, out FriendEntry entry) && entry.Status != FriendStatus.Blocked)
                entry.Status = FriendStatus.Accepted;
        }

        public void RemoveFriend(string playerId) => _entries.Remove(playerId);

        public void Block(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (_entries.TryGetValue(playerId, out FriendEntry entry)) entry.Status = FriendStatus.Blocked;
            else _entries[playerId] = new FriendEntry { PlayerId = playerId, DisplayName = playerId, Status = FriendStatus.Blocked };
        }

        public void Unblock(string playerId)
        {
            if (_entries.TryGetValue(playerId, out FriendEntry entry) && entry.Status == FriendStatus.Blocked)
                _entries.Remove(playerId);
        }
    }
}
