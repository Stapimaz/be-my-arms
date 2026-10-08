using System;
using BeMyArms.Networking;

namespace BeMyArms.Match
{
    /// <summary>Shared rifle burst timing, body-motion spread and recoil pattern. Spread is server-enforced;
    /// local recoil moves real mouse aim, never just the camera/viewmodel.</summary>
    public sealed class RifleHandling
    {
        public const double BurstResetSeconds = .30;
        public const float StationarySpeedThreshold = .10f;
        public const float WalkSpreadDegrees = .75f;
        public const float CrouchMoveSpreadDegrees = .35f;
        public const float SprintSpreadDegrees = 2.5f;
        public const float AirSpreadDegrees = 3.5f;
        public const float SlideSpreadDegrees = 3f;
        public const float SlideMinimumSpreadDegrees = .75f;
        public const float SlideReferenceSpeed = 9f;
        public const float LightKickSpreadDegrees = 1.5f;
        public const float HeavyKickSpreadDegrees = 4f;
        public int Burst { get; private set; }
        double _lastShot = double.NegativeInfinity;

        public void Reset() { Burst = 0; _lastShot = double.NegativeInfinity; }

        public int Shot(double now)
        {
            if (now - _lastShot > BurstResetSeconds) Burst = 0;
            _lastShot = now;
            return ++Burst;
        }

        public static float SpreadDegrees(int burst)
            => burst <= 1 ? 0f : burst <= 3 ? .10f + (burst - 1) * .06f : Math.Min(1.8f, .28f + (burst - 3) * .16f);

        /// <summary>Penalty uses actual collision-resolved body speed, not held movement buttons.
        /// Actions still disturb the arms when their translation is blocked or momentarily zero.</summary>
        public static float MovementSpreadDegrees(in BodyState state, float walkSpeed = 4.5f,
            float sprintSpeed = 7f, float crouchSpeed = 2.5f)
        {
            float speed = Math.Max(0f, state.PlanarSpeed);
            float moving = Math.Max(0f, speed - StationarySpeedThreshold);
            float spread;
            if (state.Crouching)
                spread = CrouchMoveSpreadDegrees * Math.Min(1f, moving / Math.Max(.01f, crouchSpeed - StationarySpeedThreshold));
            else if (speed <= walkSpeed)
                spread = WalkSpreadDegrees * Math.Min(1f, moving / Math.Max(.01f, walkSpeed - StationarySpeedThreshold));
            else
                spread = WalkSpreadDegrees + (SprintSpreadDegrees - WalkSpreadDegrees) *
                    Math.Min(1f, (speed - walkSpeed) / Math.Max(.01f, sprintSpeed - walkSpeed));

            if (!state.Grounded) spread = Math.Max(spread, AirSpreadDegrees);
            switch ((BodyMovementState)state.MovementState)
            {
                case BodyMovementState.Jump:
                case BodyMovementState.Fall:
                case BodyMovementState.Dodge: spread = Math.Max(spread, AirSpreadDegrees); break;
                case BodyMovementState.Slide:
                    spread = Math.Max(spread, SlideMinimumSpreadDegrees + (SlideSpreadDegrees - SlideMinimumSpreadDegrees) *
                        Math.Min(1f, speed / SlideReferenceSpeed));
                    break;
                case BodyMovementState.KickLight: spread = Math.Max(spread, LightKickSpreadDegrees); break;
                case BodyMovementState.KickHeavy: spread = Math.Max(spread, HeavyKickSpreadDegrees); break;
            }
            return spread;
        }

        public static float SpreadDegrees(int burst, in BodyState state, float walkSpeed = 4.5f,
            float sprintSpeed = 7f, float crouchSpeed = 2.5f)
            => SpreadDegrees(burst) + MovementSpreadDegrees(state, walkSpeed, sprintSpeed, crouchSpeed);

        public static void Recoil(int burst, out float up, out float right)
        {
            up = burst <= 3 ? .24f + (burst - 1) * .08f : .72f;
            right = burst <= 3 ? 0 : (float)Math.Sin((burst - 3) * .85) * .35f;
        }

        /// <summary>Uniform disk sample with independent radius/angle. Stable per accepted shot;
        /// rejected triggers cannot grow bloom or consume the pattern.</summary>
        public static void Spread(uint shot, uint seed, float degrees, out float yaw, out float pitch)
        {
            if (degrees <= 0f) { yaw = pitch = 0f; return; }
            uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; return x ^ (x >> 16); }
            double radius = Math.Sqrt((Hash(shot ^ seed) + .5) / 4294967296d) * degrees;
            double angle = (Hash(shot ^ seed ^ 0x9e3779b9u) + .5) / 4294967296d * Math.PI * 2;
            yaw = (float)(Math.Cos(angle) * radius); pitch = (float)(Math.Sin(angle) * radius);
        }
    }
}
