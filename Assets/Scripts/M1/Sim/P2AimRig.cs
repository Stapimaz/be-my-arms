using BeMyArms.M0;
using UnityEngine;

namespace BeMyArms.M1
{
    /// <summary>
    /// P2 aim state: desired world-space yaw clamped to P1's firing sector, plus pitch and recoil.
    /// Uses the AimSector math from the M0 spike (Model C: world-stabilized aim inside the
    /// sector; the body never chases the aim). Simulation-only.
    /// </summary>
    public class P2AimRig : MonoBehaviour
    {
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;

        public float DesiredWorldYaw { get; private set; }
        public float Pitch { get; private set; }
        public bool IsPinned { get; private set; }

        public void Initialize(float worldYaw)
        {
            DesiredWorldYaw = AimSector.NormalizeAngle(worldYaw);
            Pitch = 0f;
            IsPinned = false;
        }

        public void Step(in P2Command cmd, float bodyYaw)
        {
            DesiredWorldYaw = AimSector.ApplyInput(DesiredWorldYaw, cmd.AimYawDelta, bodyYaw, SectorHalfDegrees);
            Pitch = Mathf.Clamp(Pitch + cmd.AimPitchDelta, -MaxPitchDegrees, MaxPitchDegrees);
            IsPinned = AimSector.IsPinned(DesiredWorldYaw, bodyYaw, SectorHalfDegrees);
        }

        /// <summary>Weapon recoil: raises the aim; the player recovers it manually.</summary>
        public void AddRecoil(float pitchKickDegrees, float yawKickDegrees, float bodyYaw)
        {
            Pitch = Mathf.Clamp(Pitch - pitchKickDegrees, -MaxPitchDegrees, MaxPitchDegrees);
            DesiredWorldYaw = AimSector.ClampToSector(
                AimSector.NormalizeAngle(DesiredWorldYaw + yawKickDegrees), bodyYaw, SectorHalfDegrees);
        }

        public Quaternion AimRotation => Quaternion.Euler(Pitch, DesiredWorldYaw, 0f);
        public Vector3 AimDirection => AimRotation * Vector3.forward;
        public float OffsetFromBody(float bodyYaw) => AimSector.OffsetFromBody(DesiredWorldYaw, bodyYaw);
    }
}
