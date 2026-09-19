using System;

namespace BeMyArms.M3
{
    /// <summary>
    /// Anti-stall closing zone: the playable radius holds, then shrinks; bodies outside take damage.
    /// Pure and testable. Timings/radius/damage are TUNING.
    /// </summary>
    public class M3ClosingZone
    {
        public float StartRadius = 40f;
        public float EndRadius = 8f;
        public float CloseStartSeconds = 60f;
        public float CloseDurationSeconds = 60f;
        public float DamagePerSecond = 5f;

        public float RadiusAt(float liveElapsed)
        {
            if (liveElapsed <= CloseStartSeconds) return StartRadius;
            float duration = Math.Max(0.01f, CloseDurationSeconds);
            float t = (liveElapsed - CloseStartSeconds) / duration;
            if (t >= 1f) return EndRadius;
            return StartRadius + (EndRadius - StartRadius) * t;
        }

        public bool IsOutside(float distanceFromCenter, float liveElapsed)
            => distanceFromCenter > RadiusAt(liveElapsed);

        public float DamageFor(float distanceFromCenter, float liveElapsed, float deltaTime)
            => IsOutside(distanceFromCenter, liveElapsed) ? DamagePerSecond * deltaTime : 0f;
    }
}
