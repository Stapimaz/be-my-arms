using System.Collections.Generic;
using BeMyArms.M3;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M4
{
    /// <summary>
    /// Server-side host that runs the pure M4 matchmaking/allocation flow for a networked match and
    /// then applies post-match role ratings. It is deliberately networking-light: it observes the
    /// M3 connection queue, produces a <see cref="M4MatchProposal"/> with <see cref="M4Matchmaker"/>,
    /// allocates a server through the provider-neutral <see cref="IM4ServerAllocator"/>, hands the
    /// slot assignments to the M3 director, and subscribes to the director's match result.
    ///
    /// It runs only on the server and only when matchmaker mode is on (`-m4-matchmaker 1`).
    /// </summary>
    public class M4MatchHost : MonoBehaviour
    {
        readonly IM4ServerAllocator _allocator = new M4LocalAllocator();
        readonly M4MatchmakingConfig _config = new M4MatchmakingConfig();
        readonly Dictionary<string, M4PlayerProfile> _profiles = new Dictionary<string, M4PlayerProfile>();
        readonly Dictionary<string, (float p1, float p2)> _before = new Dictionary<string, (float, float)>();

        M3DuelDirector _director;
        M4MatchProposal _proposal;
        bool _subscribed;
        float _startTime;

        void Start()
        {
            _startTime = Time.realtimeSinceStartup;
        }

        void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;
            if (!M3Config.UseMatchmaker) return;

            if (_director == null) _director = M3DuelDirector.Instance;
            if (_director == null) return;

            if (_director.MatchStarted)
            {
                if (!_subscribed)
                {
                    _director.MatchCompleted += OnMatchCompleted;
                    _subscribed = true;
                }
                return;
            }

            var roster = M3DuelRoleService.Instance != null ? M3DuelRoleService.Instance.Registry : null;
            if (roster == null) return;

            bool ready = roster.QueuedCount >= M3Config.RequiredPlayers;
            bool timedOut = Time.realtimeSinceStartup - _startTime >= M3Config.StartDelaySeconds;
            if (!ready && !timedOut) return;

            TryStart(manager, roster, _director, timedOut);
        }

        void TryStart(NetworkManager manager, M3DuelRoster roster, M3DuelDirector director, bool timedOut)
        {
            int needed = director.SlotCount;
            M4MatchMode mode = director.BodiesPerTeam >= 2 ? M4MatchMode.TwoVsTwo : M4MatchMode.Duel;

            List<M4QueueEntry> entries = BuildEntries(roster, needed);
            if (entries.Count < needed) return;

            M4MatchmakingConfig config = _config;
            if (!M4Matchmaker.TryMatch(entries, mode, config, out M4MatchProposal proposal))
            {
                if (!timedOut) return;
                // Queue time relaxes the constraints strongly enough to always resolve once we are
                // willing to start with whoever is present.
                config = new M4MatchmakingConfig
                {
                    MaxBodyRoleGap = 100000f,
                    MaxTeamRatingGap = 100000f,
                    RelaxPerSecond = 100000f,
                    MaxRelax = 100000f,
                    AllowEitherRole = true
                };
                if (!M4Matchmaker.TryMatch(entries, mode, config, out proposal))
                {
                    Debug.Log("[M4] matchmaker could not form a match yet");
                    return;
                }
            }

            M4ServerTicket ticket = _allocator.Allocate(proposal);
            Debug.Log($"[M4] matchmaking ({mode}) quality={proposal.Quality:0.0} teamA={proposal.TeamARating:0} teamB={proposal.TeamBRating:0} " +
                      $"-> allocated match '{ticket?.MatchId}' at {ticket?.Endpoint} ({ticket?.Region}) region=local");

            var assignments = new List<M3SlotAssignment>(proposal.Assignments.Count);
            _profiles.Clear();
            _before.Clear();
            for (int i = 0; i < proposal.Assignments.Count; i++)
            {
                M4SlotAssignment a = proposal.Assignments[i];
                int slot = M3DuelSlots.Encode(a.Slot.Team, a.Slot.Body, a.Slot.Role, director.BodiesPerTeam);
                bool bot = string.IsNullOrEmpty(a.PlayerId) || a.PlayerId.StartsWith("bot:");
                assignments.Add(new M3SlotAssignment { Slot = slot, PlayerId = a.PlayerId, IsBot = bot });

                if (!bot)
                {
                    var profile = new M4PlayerProfile { PlayerId = a.PlayerId, P1Mmr = a.P1Mmr, P2Mmr = a.P2Mmr };
                    _profiles[a.PlayerId] = profile;
                    _before[a.PlayerId] = (a.P1Mmr, a.P2Mmr);
                    Debug.Log($"[M4] {a.PlayerId} -> {M3DuelSlots.Name(slot, director.BodiesPerTeam)} (P1 {a.P1Mmr:0}, P2 {a.P2Mmr:0})");
                }
            }

            _proposal = proposal;
            _director.ServerBeginMatch(assignments, ticket != null ? $"{ticket.MatchId}@{ticket.Endpoint}" : "local");
        }

        List<M4QueueEntry> BuildEntries(M3DuelRoster roster, int needed)
        {
            var entries = new List<M4QueueEntry>();
            List<M3QueuedPlayer> queued = roster.QueuedPlayers();
            float longestWait = 0f;

            for (int i = 0; i < queued.Count; i++)
            {
                M3QueuedPlayer q = queued[i];
                string playerId = string.IsNullOrEmpty(q.Token) ? $"client{q.ClientId}" : q.Token;
                float mmr = M3Config.MmrFor(q.Token);
                float wait = Time.realtimeSinceStartup - q.EnqueueTime;
                if (wait > longestWait) longestWait = wait;

                entries.Add(new M4QueueEntry
                {
                    PlayerId = playerId,
                    PartyId = M3Config.PartyFor(q.Token),
                    Preference = q.RolePreference == 0 ? M3RolePreference.P1 : q.RolePreference == 1 ? M3RolePreference.P2 : M3RolePreference.Either,
                    P1Mmr = mmr,
                    P2Mmr = mmr,
                    WaitSeconds = wait,
                    Region = q.Region
                });
            }

            // Fill the remaining slots with Either bots so a match can always start; the pure
            // matchmaker then decides the exact team/body/role composition.
            for (int i = entries.Count; i < needed; i++)
            {
                entries.Add(new M4QueueEntry
                {
                    PlayerId = $"bot:{i}",
                    Preference = M3RolePreference.Either,
                    P1Mmr = 1000f,
                    P2Mmr = 1000f,
                    WaitSeconds = longestWait,
                    Region = "local"
                });
            }

            return entries;
        }

        void OnMatchCompleted(int winner)
        {
            if (_proposal == null) return;
            M4MatchBridge.ApplyResult(_proposal, winner, _profiles);

            Debug.Log($"[M4] role-rating updates after {(winner < 0 ? "draw" : winner == 0 ? "team A win" : "team B win")}:");
            foreach (var kvp in _profiles)
            {
                (float p1, float p2) before = _before.TryGetValue(kvp.Key, out var b) ? b : (kvp.Value.P1Mmr, kvp.Value.P2Mmr);
                Debug.Log($"[M4]   {kvp.Key}: P1 {before.p1:0} -> {kvp.Value.P1Mmr:0}  P2 {before.p2:0} -> {kvp.Value.P2Mmr:0}");
            }
        }

        void OnDestroy()
        {
            if (_subscribed && _director != null) _director.MatchCompleted -= OnMatchCompleted;
        }
    }
}
