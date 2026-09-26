using System;

namespace BeMyArms.M3
{
    /// <summary>
    /// Non-oscillating rubber wall for the P2 aim-sector edge (per client). Most of the sector is
    /// normal 1:1 mouse movement. In the last <see cref="Tuning.WallZoneDegrees"/> before a boundary,
    /// outward movement becomes strongly resistant: a linear gain that stays full at the zone edge
    /// and falls to exactly zero at the boundary, so the center of the sector is untouched and the
    /// edge is very stiff. Outward input rejected by the wall banks a small compression; when the
    /// push stops, that compression is released once as an inward impulse that coasts a fraction of a
    /// degree inward. The coast speed only ever decays to zero and is never reflected, so there is
    /// deliberately no restoring force toward the boundary: the wall cannot oscillate or jitter.
    /// Inward movement is 1:1 and clears the wall immediately. Every returned offset is clamped
    /// inside the legal sector, so no mouse input is ever accumulated beyond the boundary (no phantom
    /// aim).
    /// </summary>
    public static class M3SectorWall
    {
        public struct State
        {
            /// <summary>Compression banked while pushing outward into the wall (degrees, >= 0).</summary>
            public float Compression;
            /// <summary>Current inward presentation offset of the released impulse (degrees, >= 0).</summary>
            public float Rebound;
            /// <summary>Inward coast speed of the released impulse (degrees/second, >= 0).</summary>
            public float Velocity;
        }

        public struct Tuning
        {
            /// <summary>Width of the rubber zone at each end of the sector (degrees).</summary>
            public float WallZoneDegrees;
            /// <summary>How much rejected outward input is banked as compression.</summary>
            public float PressureGain;
            /// <summary>Maximum banked compression / released rebound (degrees).</summary>
            public float ReboundMaxDegrees;
            /// <summary>Decay rate applied to the released impulse's coast speed (per second).</summary>
            public float ReleaseDamping;

            public static Tuning Default => new Tuning
            {
                WallZoneDegrees = 11f,
                PressureGain = 0.55f,
                ReboundMaxDegrees = 0.9f,
                ReleaseDamping = 14f
            };
        }

        /// <summary>
        /// Advances the wall by one frame.
        /// </summary>
        /// <param name="offset">Current legal target offset from the body yaw (degrees).</param>
        /// <param name="deltaYaw">This frame's mouse yaw delta (degrees).</param>
        /// <param name="innerHalf">Legal half-sector (degrees).</param>
        /// <param name="targetOffset">New legal target offset (clamped, no phantom).</param>
        /// <returns>The displayed offset (target plus any inward rubber rebound), inside the sector.</returns>
        public static float Step(ref State state, in Tuning t, float offset, float deltaYaw, float innerHalf, float dt, out float targetOffset)
        {
            if (dt <= 0f) dt = 1f / 60f;
            if (innerHalf < 0.001f) innerHalf = 0.001f;

            bool hasDelta = Math.Abs(deltaYaw) > 0.0001f;
            bool outward = hasDelta && Math.Sign(deltaYaw) == Math.Sign(offset) && Math.Abs(offset) > 0.001f;

            // Resolve this frame's mouse input against the rubber zone.
            float applied = deltaYaw;
            float rejected = 0f;
            float zone = Math.Max(0.001f, t.WallZoneDegrees);
            if (outward)
            {
                float remaining = innerHalf - Math.Abs(offset);
                if (remaining < zone)
                {
                    // Linear resistance: full gain at the zone edge, zero exactly at the boundary.
                    float gain = Clamp01(remaining / zone);
                    applied = deltaYaw * gain;
                    rejected = deltaYaw - applied;
                }
            }

            float desired = offset + applied;
            if (Math.Abs(desired) > innerHalf)
            {
                rejected += desired - Math.Sign(desired) * innerHalf;
                desired = Math.Sign(desired) * innerHalf;
            }
            targetOffset = desired; // always legal

            bool pushing = outward && Math.Abs(rejected) > 0.0001f;

            if (hasDelta && !outward)
            {
                // Inward movement is 1:1 and clears the wall immediately.
                state.Compression = 0f;
                state.Rebound = 0f;
                state.Velocity = 0f;
            }
            else if (pushing)
            {
                // Pushing into the wall: bank compression. Nothing coasts while the player holds.
                state.Compression = Math.Min(t.ReboundMaxDegrees, state.Compression + Math.Abs(rejected) * t.PressureGain);
            }
            else if (state.Compression > 0f)
            {
                // Released: convert the banked compression into a single inward impulse. The coast
                // speed is chosen so the total travel equals the compression; it then only decays.
                state.Velocity = state.Compression * Math.Max(0.001f, t.ReleaseDamping);
                state.Compression = 0f;
            }

            // Coast the released impulse inward. The speed decays monotonically toward zero and is
            // never reflected, so there is no restoring force and no oscillation.
            if (state.Velocity > 0f)
            {
                state.Rebound = Math.Min(t.ReboundMaxDegrees, state.Rebound + state.Velocity * dt);
                state.Velocity *= (float)Math.Exp(-Math.Max(0.001f, t.ReleaseDamping) * dt);
                if (state.Velocity < 0.001f) state.Velocity = 0f;
            }

            float displayed = desired;
            if (state.Rebound > 0f)
            {
                float side = Math.Sign(desired);
                if (side == 0f) side = Math.Sign(offset);
                displayed = desired - side * state.Rebound;
            }
            if (displayed > innerHalf) displayed = innerHalf;
            else if (displayed < -innerHalf) displayed = -innerHalf;
            return displayed;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
