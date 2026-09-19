using System;

namespace BeMyArms.M4
{
    /// <summary>
    /// Derived rating for one shared body and for a 2v2 team (concept §18.3/§18.4). A body is only
    /// as strong as the coordination of its two roles, so the draft formula penalises a large
    /// rating gap between P1 and P2. The penalty coefficient is TUNING and may go to zero.
    /// </summary>
    public static class M4BodyMmr
    {
        /// <summary>Draft gap penalty. 0 collapses to a plain average.</summary>
        public const float DefaultGapPenalty = 0.15f;

        public static float Derive(float p1Mmr, float p2Mmr, float gapPenalty = DefaultGapPenalty)
            => (p1Mmr + p2Mmr) * 0.5f - gapPenalty * Math.Abs(p1Mmr - p2Mmr);

        /// <summary>2v2 team rating from its two bodies' derived ratings.</summary>
        public static float TeamRating(float bodyOne, float bodyTwo)
            => (bodyOne + bodyTwo) * 0.5f;
    }
}
