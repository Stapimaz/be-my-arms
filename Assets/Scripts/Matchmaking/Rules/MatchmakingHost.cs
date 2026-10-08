using System.Collections.Generic;
using BeMyArms.Match;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.Matchmaking
{
    /// <summary>
    /// Server-side host that runs the pure Matchmaking matchmaking/allocation flow for a networked match and
    /// then applies post-match role ratings. It is deliberately networking-light: it observes the
    /// Match connection queue, produces a <see cref="MatchProposal"/> with <see cref="Matchmaker"/>,
    /// allocates a server through the provider-neutral <see cref="IServerAllocator"/>, hands the
    /// slot assignments to the Match director, and subscribes to the director's match result.
    ///
    /// It runs only on the server and only when matchmaker mode is on (`-queue-matchmaker 1`).
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M4", "BeMyArms.M4", "M4MatchHost")]
    public class MatchmakingHost : MonoBehaviour
    {
        readonly IServerAllocator _allocator = new LocalAllocator();
        readonly MatchmakingConfig _config = new MatchmakingConfig();
        readonly Dictionary<string, MatchmakingPlayerProfile> _profiles = new Dictionary<string, MatchmakingPlayerProfile>();
        readonly Dictionary<string, (float p1, float p2)> _before = new Dictionary<string, (float, float)>();

        MatchDirector _director;
        MatchProposal _proposal;
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
            if (!MatchConfig.UseMatchmaker) return;

            if (_director == null) _director = MatchDirector.Instance;
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

            var roster = MatchRoleService.Instance != null ? MatchRoleService.Instance.Registry : null;
            if (roster == null) return;

            bool ready = roster.QueuedCount >= MatchConfig.RequiredPlayers;
            bool timedOut = Time.realtimeSinceStartup - _startTime >= MatchConfig.StartDelaySeconds;
            if (!ready && !timedOut) return;

            TryStart(manager, roster, _director, timedOut);
        }

        void TryStart(NetworkManager manager, MatchRoster roster, MatchDirector director, bool timedOut)
        {
            int needed = director.SlotCount;
            MatchMode mode = director.BodiesPerTeam >= 2 ? MatchMode.TwoVsTwo : MatchMode.Duel;

            List<MatchmakingQueueEntry> entries = BuildEntries(roster, needed);
            if (entries.Count < needed) return;

            MatchmakingConfig config = _config;
            if (!Matchmaker.TryMatch(entries, mode, config, out MatchProposal proposal))
            {
                if (!timedOut) return;
                // Queue time relaxes the constraints strongly enough to always resolve once we are
                // willing to start with whoever is present.
                config = new MatchmakingConfig
                {
                    MaxBodyRoleGap = 100000f,
                    MaxTeamRatingGap = 100000f,
                    RelaxPerSecond = 100000f,
                    MaxRelax = 100000f,
                    AllowEitherRole = true
                };
                if (!Matchmaker.TryMatch(entries, mode, config, out proposal))
                {
                    Debug.Log("[Matchmaking] matchmaker could not form a match yet");
                    return;
                }
            }

            ServerTicket ticket = _allocator.Allocate(proposal);
            Debug.Log($"[Matchmaking] matchmaking ({mode}) quality={proposal.Quality:0.0} teamA={proposal.TeamARating:0} teamB={proposal.TeamBRating:0} " +
                      $"-> allocated match '{ticket?.MatchId}' at {ticket?.Endpoint} ({ticket?.Region}) region=local");

            var assignments = new List<RoleSlotAssignment>(proposal.Assignments.Count);
            _profiles.Clear();
            _before.Clear();
            for (int i = 0; i < proposal.Assignments.Count; i++)
            {
                MatchSlotAssignment a = proposal.Assignments[i];
                int slot = MatchSlots.Encode(a.Slot.Team, a.Slot.Body, a.Slot.Role, director.BodiesPerTeam);
                bool bot = string.IsNullOrEmpty(a.PlayerId) || a.PlayerId.StartsWith("bot:");
                assignments.Add(new RoleSlotAssignment { Slot = slot, PlayerId = a.PlayerId, IsBot = bot });

                if (!bot)
                {
                    var profile = new MatchmakingPlayerProfile { PlayerId = a.PlayerId, P1Mmr = a.P1Mmr, P2Mmr = a.P2Mmr };
                    _profiles[a.PlayerId] = profile;
                    _before[a.PlayerId] = (a.P1Mmr, a.P2Mmr);
                    Debug.Log($"[Matchmaking] {a.PlayerId} -> {MatchSlots.Name(slot, director.BodiesPerTeam)} (P1 {a.P1Mmr:0}, P2 {a.P2Mmr:0})");
                }
            }

            _proposal = proposal;
            _director.ServerBeginMatch(assignments, ticket != null ? $"{ticket.MatchId}@{ticket.Endpoint}" : "local");
        }

        List<MatchmakingQueueEntry> BuildEntries(MatchRoster roster, int needed)
        {
            var entries = new List<MatchmakingQueueEntry>();
            List<QueuedPlayer> queued = roster.QueuedPlayers();
            float longestWait = 0f;

            for (int i = 0; i < queued.Count; i++)
            {
                QueuedPlayer q = queued[i];
                string playerId = string.IsNullOrEmpty(q.Token) ? $"client{q.ClientId}" : q.Token;
                float mmr = MatchConfig.MmrFor(q.Token);
                float wait = Time.realtimeSinceStartup - q.EnqueueTime;
                if (wait > longestWait) longestWait = wait;

                entries.Add(new MatchmakingQueueEntry
                {
                    PlayerId = playerId,
                    PartyId = MatchConfig.PartyFor(q.Token),
                    Preference = q.RolePreference == 0 ? RolePreference.P1 : q.RolePreference == 1 ? RolePreference.P2 : RolePreference.Either,
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
                entries.Add(new MatchmakingQueueEntry
                {
                    PlayerId = $"bot:{i}",
                    Preference = RolePreference.Either,
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
            MatchmakingBridge.ApplyResult(_proposal, winner, _profiles);

            Debug.Log($"[Matchmaking] role-rating updates after {(winner < 0 ? "draw" : winner == 0 ? "team A win" : "team B win")}:");
            foreach (var kvp in _profiles)
            {
                (float p1, float p2) before = _before.TryGetValue(kvp.Key, out var b) ? b : (kvp.Value.P1Mmr, kvp.Value.P2Mmr);
                Debug.Log($"[Matchmaking]   {kvp.Key}: P1 {before.p1:0} -> {kvp.Value.P1Mmr:0}  P2 {before.p2:0} -> {kvp.Value.P2Mmr:0}");
            }
        }

        void OnDestroy()
        {
            if (_subscribed && _director != null) _director.MatchCompleted -= OnMatchCompleted;
        }
    }
}
