using System;

namespace BeMyArms.M3
{
    /// <summary>
    /// Stateful damped-spring wall for the P2 aim-sector edge (per client). Most of the sector is
    /// normal mouse movement; in the last <see cref="Tuning.WallZoneDegrees"/> before a boundary,
    /// outward movement becomes strongly resistant. Outward input rejected by the boundary compresses
    /// the spring; when the push stops, the stored compression releases as a small inward rebound
    /// (~<see cref="Tuning.ReboundMaxDegrees"/>) that damps quickly. Inward movement is immediately
    /// responsive and clears the spring. The returned offset is always clamped inside the legal
    /// sector, so no mouse input is ever accumulated beyond the boundary.
    /// </summary>
    public static class M3SectorWall
    {
        public struct State
        {
            /// <summary>Stored compression in degrees (>= 0).</summary>
            public float Pressure;
            /// <summary>Rebound velocity in degrees/second.</summary>
            public float Velocity;
        }

        public struct Tuning
        {
            /// <summary>Width of the soft wall zone at each end of the sector (degrees).</summary>
            public float WallZoneDegrees;
            /// <summary>Rebound spring stiffness.</summary>
            public float Stiffness;
            /// <summary>Rebound damping.</summary>
            public float Damping;
            /// <summary>Maximum stored compression / rebound (degrees).</summary>
            public float ReboundMaxDegrees;
            /// <summary>How much rejected outward input becomes compression.</summary>
            public float PressureGain;

            public static Tuning Default => new Tuning
            {
                WallZoneDegrees = 12f,
                Stiffness = 120f,
                Damping = 16f,
                ReboundMaxDegrees = 2f,
                PressureGain = 0.6f
            };
        }

        /// <summary>
        /// Advances the wall by one frame.
        /// </summary>
        /// <param name="offset">Current legal target offset from the body yaw (degrees).</param>
        /// <param name="deltaYaw">This frame's mouse yaw delta (degrees).</param>
        /// <param name="innerHalf">Legal half-sector (degrees).</param>
        /// <param name="targetOffset">New legal target offset (clamped, no phantom).</param>
        /// <returns>The displayed offset (target plus any inward rebound), inside the sector.</returns>
        public static float Step(ref State state, in Tuning t, float offset, float deltaYaw, float innerHalf, float dt, out float targetOffset)
        {
            if (dt <= 0f) dt = 1f / 60f;

            bool hasDelta = Math.Abs(deltaYaw) > 0.0001f;
            bool outward = hasDelta && Math.Sign(deltaYaw) == Math.Sign(offset) && Math.Abs(offset) > 0.001f;

            float applied = deltaYaw;
            float rejected = 0f;
            if (outward)
            {
                float remaining = innerHalf - Math.Abs(offset);
                float zone = Math.Max(0.001f, t.WallZoneDegrees);
                if (remaining < zone)
                {
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
                // Inward movement is 1:1 and clears the wall.
                state.Pressure = 0f;
                state.Velocity = 0f;
            }
            else if (pushing)
            {
                // Pushing into the wall: compress (held), aim stays at the target.
                state.Pressure = Math.Min(t.ReboundMaxDegrees, state.Pressure + Math.Abs(rejected) * t.PressureGain);
                state.Velocity = 0f;
            }
            else
            {
                // Released: damped spring returns the compression to zero.
                float accel = -t.Stiffness * state.Pressure - t.Damping * state.Velocity;
                state.Velocity += accel * dt;
                state.Pressure += state.Velocity * dt;
                if (state.Pressure <= 0f)
                {
                    state.Pressure = 0f;
                    state.Velocity = 0f;
                }
            }

            float displayed = desired;
            if (!pushing && state.Pressure > 0f)
            {
                float side = Math.Sign(desired);
                if (side == 0f) side = Math.Sign(offset);
                displayed = desired - side * state.Pressure;
            }
            if (displayed > innerHalf) displayed = innerHalf;
            else if (displayed < -innerHalf) displayed = -innerHalf;
            return displayed;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
