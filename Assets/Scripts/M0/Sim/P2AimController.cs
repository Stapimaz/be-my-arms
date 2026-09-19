using UnityEngine;

namespace BeMyArms.M0
{
    /// <summary>
    /// Model C aim state: a desired WORLD-space yaw constrained to P1's firing sector,
    /// plus an unconstrained (clamped) pitch.
    ///
    /// The desired world yaw is stored and clamped every step. Because the clamped value is
    /// stored back, rotating the body cannot accumulate a hidden offset, and P2 input can
    /// never rotate the body.
    /// </summary>
    public class P2AimController : MonoBehaviour
    {
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;

        /// <summary>Desired world-space aim yaw in degrees.</summary>
        public float DesiredWorldYaw { get; private set; }

        /// <summary>Aim pitch in degrees (camera X euler).</summary>
        public float Pitch { get; private set; }

        /// <summary>True when the aim is resting on a sector boundary.</summary>
        public bool IsPinned { get; private set; }

        public void Initialize(float worldYaw)
        {
            DesiredWorldYaw = AimSector.NormalizeAngle(worldYaw);
            Pitch = 0f;
            IsPinned = false;
        }

        public void Step(in P2Command cmd, float bodyYaw)
        {
            DesiredWorldYaw = AimSector.ApplyInput(DesiredWorldYaw, cmd.YawDelta, bodyYaw, SectorHalfDegrees);
            Pitch = Mathf.Clamp(Pitch + cmd.PitchDelta, -MaxPitchDegrees, MaxPitchDegrees);
            IsPinned = AimSector.IsPinned(DesiredWorldYaw, bodyYaw, SectorHalfDegrees);
        }

        public Quaternion AimRotation => Quaternion.Euler(Pitch, DesiredWorldYaw, 0f);

        public Vector3 AimDirection => AimRotation * Vector3.forward;

        /// <summary>Signed horizontal offset from body-forward, in degrees.</summary>
        public float OffsetFromBody(float bodyYaw) => AimSector.OffsetFromBody(DesiredWorldYaw, bodyYaw);
    }
}
