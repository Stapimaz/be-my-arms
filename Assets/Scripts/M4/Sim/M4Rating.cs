using System;

namespace BeMyArms.M4
{
    /// <summary>
    /// Role-specific competitive rating (concept §18.2/§18.3). P1 and P2 have separate MMR, so a
    /// match result updates only the rating of the role that was played. Pure and unit-testable.
    /// The Elo constants are TUNING.
    /// </summary>
    public class M4PlayerProfile
    {
        public string PlayerId;
        /// <summary>Competitive rating earned while playing P1.</summary>
        public float P1Mmr;
        /// <summary>Competitive rating earned while playing P2.</summary>
        public float P2Mmr;
    }

    public static class M4Rating
    {
        public const float KFactor = 32f;
        public const float Scale = 400f;

        /// <summary>Expected score for a player of rating <paramref name="own"/> against an opponent.</summary>
        public static float ExpectedScore(float own, float opponent)
            => 1f / (1f + (float)Math.Pow(10.0, (opponent - own) / Scale));

        /// <summary>Elo update. <paramref name="score"/> is 1 win, 0.5 draw, 0 loss.</summary>
        public static float Update(float own, float opponent, float score, float k = KFactor)
            => own + k * (score - ExpectedScore(own, opponent));

        /// <summary>
        /// Applies a body/team result to one role's rating. <paramref name="role"/> is 0 = P1,
        /// 1 = P2; <paramref name="opponentMmr"/> is the same role's opposing rating.
        /// </summary>
        public static void ApplyOutcome(M4PlayerProfile profile, int role, float opponentMmr, float score, float k = KFactor)
        {
            if (profile == null) return;
            if (role == 0) profile.P1Mmr = Update(profile.P1Mmr, opponentMmr, score, k);
            else profile.P2Mmr = Update(profile.P2Mmr, opponentMmr, score, k);
        }
    }
}
