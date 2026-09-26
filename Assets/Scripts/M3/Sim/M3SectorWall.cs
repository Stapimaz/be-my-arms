using System;

namespace BeMyArms.M3
{
    /// <summary>
    /// Tactile rubber wall for the P2 aim-sector edge (per client). Aiming is normal 1:1 through most
    /// of the sector, then in the last <see cref="Tuning.WallZoneDegrees"/> before a boundary outward
    /// movement meets strong progressive resistance (a linear gain that is full at the zone edge and
    /// reaches zero at the limit). When a push actually drives the aim into the limit, the legal
    /// target is clamped there and a single small inward visual kick is fired; the kick then decays
    /// exponentially back to the target over <see cref="Tuning.KickDecaySeconds"/>. The kick is
    /// latched, so continuously holding/pushing into the same edge does not retrigger it frame after
    /// frame. Inward movement is exactly 1:1 and clears the latch. There is no spring, so the wall
    /// cannot oscillate or jitter, and the stored target is always clamped inside the sector, so no
    /// input is accumulated beyond the boundary (no phantom aim).
    /// </summary>
    public static class M3SectorWall
    {
        public struct State
        {
            /// <summary>Current inward visual kick (degrees, >= 0); decays to zero.</summary>
            public float Kick;
            /// <summary>True from the first kick on an edge until inward movement clears it.</summary>
            public bool EdgeLatched;
        }

        public struct Tuning
        {
            /// <summary>Width of the resistance zone at each end of the sector (degrees).</summary>
            public float WallZoneDegrees;
            /// <summary>Amplitude of the one-shot inward kick at the limit (degrees).</summary>
            public float KickDegrees;
            /// <summary>Exponential decay time constant of the kick (seconds).</summary>
            public float KickDecaySeconds;
            /// <summary>How close to the limit counts as "pushing into the wall" (degrees).</summary>
            public float EdgeEpsilonDegrees;

            public static Tuning Default => new Tuning
            {
                WallZoneDegrees = 10f,
                KickDegrees = 0.7f,
                KickDecaySeconds = 0.12f,
                EdgeEpsilonDegrees = 0.35f
            };
        }

        /// <summary>
        /// Advances the wall by one frame.
        /// </summary>
        /// <param name="offset">Current legal target offset from the body yaw (degrees).</param>
        /// <param name="deltaYaw">This frame's mouse yaw delta (degrees).</param>
        /// <param name="innerHalf">Legal half-sector (degrees).</param>
        /// <param name="targetOffset">New legal target offset (clamped, no phantom).</param>
        /// <returns>The displayed offset (target minus any inward kick), inside the sector.</returns>
        public static float Step(ref State state, in Tuning t, float offset, float deltaYaw, float innerHalf, float dt, out float targetOffset)
        {
            if (dt <= 0f) dt = 1f / 60f;
            if (innerHalf < 0.001f) innerHalf = 0.001f;

            bool hasDelta = Math.Abs(deltaYaw) > 0.0001f;
            bool outward = hasDelta && Math.Sign(deltaYaw) == Math.Sign(offset) && Math.Abs(offset) > 0.001f;

            // Progressive resistance: full sensitivity until the zone, then a linear gain that falls
            // to zero at the limit.
            float zone = Math.Max(0.001f, t.WallZoneDegrees);
            float applied = deltaYaw;
            if (outward)
            {
                float remaining = innerHalf - Math.Abs(offset);
                if (remaining < zone)
                    applied = deltaYaw * Clamp01(remaining / zone);
            }

            float desired = offset + applied;
            if (Math.Abs(desired) > innerHalf)
                desired = Math.Sign(desired) * innerHalf;
            targetOffset = desired; // always legal

            // Fire the one-shot kick only when a push actually reaches the limit (or would cross it),
            // and only once per edge contact.
            float remainingBeforePush = innerHalf - Math.Abs(offset);
            bool atLimit = outward && remainingBeforePush <= Math.Max(0.0001f, t.EdgeEpsilonDegrees);
            bool wouldCross = outward && Math.Abs(offset + deltaYaw) > innerHalf;
            if (hasDelta && !outward)
            {
                // Inward movement is 1:1 and clears the edge latch.
                state.EdgeLatched = false;
            }
            else if ((atLimit || wouldCross) && !state.EdgeLatched)
            {
                state.Kick = Math.Max(state.Kick, t.KickDegrees);
                state.EdgeLatched = true;
            }

            // Exponential decay of the kick back to the legal target.
            if (state.Kick > 0f)
            {
                state.Kick *= (float)Math.Exp(-dt / Math.Max(0.001f, t.KickDecaySeconds));
                if (state.Kick < 0.001f) state.Kick = 0f;
            }

            float displayed = desired;
            if (state.Kick > 0f)
            {
                float side = Math.Sign(desired);
                if (side == 0f) side = Math.Sign(offset);
                displayed = desired - side * state.Kick;
            }
            if (displayed > innerHalf) displayed = innerHalf;
            else if (displayed < -innerHalf) displayed = -innerHalf;
            return displayed;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
