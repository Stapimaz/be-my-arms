using BeMyArms.M0;
using UnityEngine;

namespace BeMyArms.M1
{
    /// <summary>
    /// P1 look/body model. Camera/head yaw is decoupled from BodyYaw:
    ///   - mouse drives LookYaw (the camera direction);
    ///   - LookYaw is limited to BodyYaw +/- NeckYawLimit;
    ///   - when the look offset passes BodyFollowThreshold, the body smoothly turns toward the
    ///     look at BodyFollowSpeed (natural follow, so looking around eventually turns the body);
    ///   - the explicit AlignBody action turns BodyYaw to LookYaw at AlignSpeed (fast but smooth).
    ///
    /// BodyYaw is what movement direction and P2's firing sector use; LookYaw is never used for
    /// either. Looking around inside the neck limit therefore does not move the aim sector.
    /// Simulation-only (no camera/HUD/input dependency).
    /// </summary>
    public class P1LookController : MonoBehaviour
    {
        public float NeckYawLimitDegrees = 80f;
        public float BodyFollowThresholdDegrees = 50f;
        public float BodyFollowSpeedDegreesPerSecond = 120f;
        public float BodyAlignSpeedDegreesPerSecond = 540f;

        public float BodyYaw { get; private set; }
        public float LookYaw { get; private set; }
        public bool IsAligning { get; private set; }
        public bool IsBodyFollowing { get; private set; }

        public float NeckOffsetDegrees => AimSector.NormalizeAngle(LookYaw - BodyYaw);

        public void Configure(M1Tuning tuning)
        {
            if (tuning == null) return;
            NeckYawLimitDegrees = tuning.neckYawLimitDegrees;
            BodyFollowThresholdDegrees = Mathf.Min(tuning.bodyFollowThresholdDegrees, tuning.neckYawLimitDegrees);
            BodyFollowSpeedDegreesPerSecond = tuning.bodyFollowSpeedDegreesPerSecond;
            BodyAlignSpeedDegreesPerSecond = tuning.bodyAlignSpeedDegreesPerSecond;
        }

        public void Initialize(float yaw)
        {
            BodyYaw = AimSector.NormalizeAngle(yaw);
            LookYaw = BodyYaw;
            IsAligning = false;
            IsBodyFollowing = false;
            transform.rotation = Quaternion.Euler(0f, BodyYaw, 0f);
        }

        public void Step(float lookYawDelta, bool alignRequested, float deltaTime)
        {
            // Mouse drives the look.
            LookYaw = AimSector.NormalizeAngle(LookYaw + lookYawDelta);

            // Neck limit: clamp look to BodyYaw +/- limit.
            float offset = AimSector.NormalizeAngle(LookYaw - BodyYaw);
            if (offset > NeckYawLimitDegrees) offset = NeckYawLimitDegrees;
            else if (offset < -NeckYawLimitDegrees) offset = -NeckYawLimitDegrees;
            LookYaw = AimSector.NormalizeAngle(BodyYaw + offset);

            // Body follows the look: explicit align is fast, natural follow is smooth and only
            // past the threshold.
            IsAligning = false;
            IsBodyFollowing = false;
            if (alignRequested)
            {
                if (Mathf.Abs(offset) > 0.01f)
                {
                    BodyYaw = Mathf.MoveTowardsAngle(BodyYaw, LookYaw, BodyAlignSpeedDegreesPerSecond * deltaTime);
                    IsAligning = true;
                }
            }
            else if (Mathf.Abs(offset) > BodyFollowThresholdDegrees)
            {
                BodyYaw = Mathf.MoveTowardsAngle(BodyYaw, LookYaw, BodyFollowSpeedDegreesPerSecond * deltaTime);
                IsBodyFollowing = true;
            }

            transform.rotation = Quaternion.Euler(0f, BodyYaw, 0f);
        }
    }
}
