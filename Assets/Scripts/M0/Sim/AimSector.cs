using System;

namespace BeMyArms.M0
{
    /// <summary>
    /// Aim Model C: P2 world-stabilized aim inside a P1-owned firing sector.
    ///
    /// Contract (see TECHNICAL_PLAN.md section 2):
    ///  - P1 fully owns BodyYaw. P2 input can never move the body.
    ///  - P2 controls a desired WORLD-space aim yaw.
    ///  - The stored desired yaw is clamped to the sector each step; overflow is discarded.
    ///  - While the aim is inside the sector it is world-stable: rotating the body does not
    ///    move it. Only when a boundary reaches it is the aim pushed with the body.
    ///  - Clamping the STORED state (not an unclamped accumulator) is what prevents a
    ///    phantom mouse offset from building up against the limit.
    ///
    /// Pure math only (System namespace, no UnityEngine) so it can be unit tested both in
    /// Unity EditMode tests and by an external harness.
    /// </summary>
    public static class AimSector
    {
        /// <summary>Normalizes an angle in degrees to the range (-180, 180].</summary>
        public static float NormalizeAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            else if (degrees <= -180f) degrees += 360f;
            return degrees;
        }

        /// <summary>Clamps a desired world yaw into [bodyYaw - half, bodyYaw + half].</summary>
        public static float ClampToSector(float desiredWorldYaw, float bodyYaw, float halfAngleDegrees)
        {
            float offset = NormalizeAngle(desiredWorldYaw - bodyYaw);
            if (offset > halfAngleDegrees) offset = halfAngleDegrees;
            else if (offset < -halfAngleDegrees) offset = -halfAngleDegrees;
            return NormalizeAngle(bodyYaw + offset);
        }

        /// <summary>
        /// Integrates a yaw delta and clamps the resulting stored state. Because the clamped
        /// value is stored back, pushing outward at the limit accumulates nothing.
        /// </summary>
        public static float ApplyInput(float currentDesiredWorldYaw, float yawDelta, float bodyYaw, float halfAngleDegrees)
        {
            float desired = currentDesiredWorldYaw + yawDelta;
            return ClampToSector(desired, bodyYaw, halfAngleDegrees);
        }

        public static float OffsetFromBody(float desiredWorldYaw, float bodyYaw)
            => NormalizeAngle(desiredWorldYaw - bodyYaw);

        public static bool IsPinned(float desiredWorldYaw, float bodyYaw, float halfAngleDegrees, float toleranceDegrees = 0.01f)
            => Math.Abs(OffsetFromBody(desiredWorldYaw, bodyYaw)) >= halfAngleDegrees - toleranceDegrees;
    }
}
