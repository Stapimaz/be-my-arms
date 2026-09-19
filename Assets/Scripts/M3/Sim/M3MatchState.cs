namespace BeMyArms.M3
{
    public enum M3Phase
    {
        Warmup,
        Buy,
        Live,
        RoundEnd,
        MatchEnd
    }

    /// <summary>
    /// Pure, authoritative Duel match/round state machine:
    ///   Buy -> Live -> RoundEnd -> (next round | MatchEnd), first to RoundsToWin with a MaxRounds cap.
    /// Elimination of a whole team ends the round immediately. Removing a body from a team is
    /// reported by the caller (the authoritative simulation). Engine-free and unit-testable.
    /// </summary>
    public class M3MatchState
    {
        public int RoundsToWin = 3;
        public int MaxRounds = 5;
        public float BuySeconds = 10f;
        public float LiveSeconds = 150f;
        public float RoundEndSeconds = 3f;

        public M3Phase Phase { get; private set; } = M3Phase.Warmup;
        public int RoundIndex { get; private set; }
        public int TeamAWins { get; private set; }
        public int TeamBWins { get; private set; }
        public float PhaseTimeRemaining { get; private set; }
        /// <summary>-1 = none/draw, 0 = team A, 1 = team B.</summary>
        public int LastRoundWinner { get; private set; } = -1;
        public int MatchWinner { get; private set; } = -1;

        public bool IsLive => Phase == M3Phase.Live;
        public bool IsBuy => Phase == M3Phase.Buy;
        public bool IsMatchOver => Phase == M3Phase.MatchEnd;

        /// <summary>Raised when a new round's buy phase begins (index is 1-based).</summary>
        public event System.Action<int> RoundStarted;
        /// <summary>Raised when the live phase begins.</summary>
        public event System.Action<int> LiveStarted;
        /// <summary>Raised when a round ends: (roundIndex, winningTeam).</summary>
        public event System.Action<int, int> RoundEnded;
        public event System.Action<int> MatchEnded;

        public void StartMatch()
        {
            RoundIndex = 0;
            TeamAWins = 0;
            TeamBWins = 0;
            LastRoundWinner = -1;
            MatchWinner = -1;
            BeginRound();
        }

        void BeginRound()
        {
            RoundIndex++;
            Phase = M3Phase.Buy;
            PhaseTimeRemaining = BuySeconds;
            LastRoundWinner = -1;
            RoundStarted?.Invoke(RoundIndex);
        }

        public void Tick(float deltaTime)
        {
            if (Phase == M3Phase.Warmup || Phase == M3Phase.MatchEnd) return;
            PhaseTimeRemaining -= deltaTime;

            switch (Phase)
            {
                case M3Phase.Buy:
                    if (PhaseTimeRemaining <= 0f) StartLive();
                    break;
                case M3Phase.Live:
                    if (PhaseTimeRemaining <= 0f) ResolveTimedOutRound();
                    break;
                case M3Phase.RoundEnd:
                    if (PhaseTimeRemaining <= 0f) AdvanceAfterRound();
                    break;
            }
        }

        void StartLive()
        {
            Phase = M3Phase.Live;
            PhaseTimeRemaining = LiveSeconds;
            LiveStarted?.Invoke(RoundIndex);
        }

        /// <summary>A whole team (0 or 1) was eliminated; the other team wins the round.</summary>
        public void ReportTeamEliminated(int eliminatedTeam)
        {
            if (Phase != M3Phase.Live) return;
            EndRound(eliminatedTeam == 0 ? 1 : 0);
        }

        /// <summary>Live timer expired with no elimination: called by the match to end in a draw.</summary>
        public void ResolveTimedOutRound()
        {
            if (Phase != M3Phase.Live) return;
            EndRound(-1);
        }

        public void EndRound(int winningTeam)
        {
            if (Phase != M3Phase.Live) return;
            LastRoundWinner = winningTeam;
            if (winningTeam == 0) TeamAWins++;
            else if (winningTeam == 1) TeamBWins++;

            Phase = M3Phase.RoundEnd;
            PhaseTimeRemaining = RoundEndSeconds;
            RoundEnded?.Invoke(RoundIndex, winningTeam);
        }

        void AdvanceAfterRound()
        {
            if (TeamAWins >= RoundsToWin || TeamBWins >= RoundsToWin || RoundIndex >= MaxRounds)
            {
                MatchWinner = TeamAWins == TeamBWins ? -1 : (TeamAWins > TeamBWins ? 0 : 1);
                Phase = M3Phase.MatchEnd;
                MatchEnded?.Invoke(MatchWinner);
                return;
            }
            BeginRound();
        }
    }
}
