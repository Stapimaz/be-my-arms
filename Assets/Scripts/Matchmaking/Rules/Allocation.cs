using System.Collections.Generic;

namespace BeMyArms.Matchmaking
{
    /// <summary>
    /// Where a matched team is hosted. Deliberately **provider-neutral**: the game layer never
    /// references a hosting vendor directly. A real adapter (Unity Multiplayer Services, an external
    /// cloud, or a local fleet manager) implements <see cref="IServerAllocator"/>; the stub below
    /// is the local development implementation.
    /// </summary>
    public class ServerTicket
    {
        public string MatchId;
        public string Endpoint;
        public string Region;
        public string JoinToken;
    }

    public interface IServerAllocator
    {
        ServerTicket Allocate(MatchProposal proposal);
        void Release(string matchId);
    }

    /// <summary>
    /// Local provider-neutral allocator used by tests and development. It hands out loopback
    /// endpoints and bounded match ids; it is not a production host.
    /// </summary>
    public class LocalAllocator : IServerAllocator
    {
        public string Host = "127.0.0.1";
        public ushort BasePort = 7779;
        public string DefaultRegion = "local";
        public int MaxConcurrentMatches = 32;

        readonly HashSet<string> _active = new HashSet<string>();
        int _next;

        public int ActiveMatches => _active.Count;

        public ServerTicket Allocate(MatchProposal proposal)
        {
            if (proposal == null) return null;
            if (_active.Count >= MaxConcurrentMatches) return null;

            _next++;
            string matchId = $"local-{_next:0000}";
            _active.Add(matchId);

            return new ServerTicket
            {
                MatchId = matchId,
                Endpoint = $"{Host}:{BasePort + _next}",
                Region = DefaultRegion,
                JoinToken = $"m4-{matchId}-token"
            };
        }

        public void Release(string matchId)
        {
            if (!string.IsNullOrEmpty(matchId)) _active.Remove(matchId);
        }
    }
}
