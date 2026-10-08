using System;

namespace BeMyArms.Match
{
    /// <summary>
    /// Pure bot-aiming math (no UnityEngine), shared by the server bot and the EditMode accuracy test.
    ///
    /// Easy bots do not rely on a smooth sine drift to miss: each shot is given an explicit offset in
    /// the target's plane. The offset is drawn so that a configurable fraction of shots land on the
    /// body ("hits") and the rest are near misses that stay close to it — the *observed* hit rate is
    /// therefore governed directly by the ordinary authoritative hitscan, not by an aim curve. The
    /// miss offset is horizontally dominant because the target is a tall segment: a purely vertical
    /// offset would often still intersect the body.
    /// </summary>
    public static class BotAim
    {
        /// <summary>Fraction of the target radius a "hit" shot is jittered within.</summary>
        public const float HitFraction = 0.6f;
        /// <summary>Nearest a "miss" shot is placed, as a multiple of the target radius (must be > 1).</summary>
        public const float MissInnerFraction = 1.3f;
        /// <summary>Farthest a "miss" shot is placed, as a multiple of the target radius.</summary>
        public const float MissOuterFraction = 2.8f;
        /// <summary>Vertical spread of a "miss" shot, as a fraction of the target radius.</summary>
        public const float MissVerticalFraction = 0.8f;

        /// <summary>
        /// Samples one shot's aim offset in the target plane: right/up metres from the target centre.
        /// <paramref name="roll"/> decides hit vs miss; <paramref name="u1"/>/<paramref name="u2"/> are
        /// independent uniform [0,1) values that shape the offset. Kept deterministic/testable.
        /// </summary>
        public static void SampleShotOffset(float hitProbability, float targetRadius, double roll, double u1, double u2,
            out float rightMeters, out float upMeters)
        {
            float radius = targetRadius > 0.01f ? targetRadius : 0.01f;
            double p = hitProbability;
            if (p < 0.0) p = 0.0;
            else if (p > 1.0) p = 1.0;

            if (roll < p)
            {
                // Hit: jitter within the body so the round lands on it.
                rightMeters = radius * HitFraction * (float)(2.0 * u1 - 1.0);
                upMeters = radius * HitFraction * (float)(2.0 * u2 - 1.0);
            }
            else
            {
                // Miss: pushed out sideways (plus a little vertical) so it reliably clears the body
                // while still reading as a near miss beside/over the target.
                double spread = MissInnerFraction + (MissOuterFraction - MissInnerFraction) * u2;
                float sign = u1 < 0.5 ? -1f : 1f;
                rightMeters = sign * radius * (float)spread;
                upMeters = radius * MissVerticalFraction * (float)(2.0 * u1 - 1.0);
            }
        }
    }
}
