using UnityEngine;

namespace BeMyArms.M0
{
    /// <summary>
    /// Scripted P2 input: a stand-in second player used when no gamepad is available and
    /// for deterministic verification. It drives the SAME Model C pipeline as a human by
    /// feeding a yaw delta toward a world target, so the sector clamp still applies and the
    /// bot never moves the body.
    ///
    /// Useful test: point the bot at a target outside the sector. It will pin at the
    /// boundary and refuse to fire until P1 rotates the body to expose it.
    /// </summary>
    public class P2ScriptedInputSource : MonoBehaviour, IP2InputSource
    {
        public Transform aimTarget;
        public Transform eye;
        public P2AimController aim;
        public bool autoFire = true;
        public float fireAlignmentDegrees = 2.5f;

        public P2Command Read(float deltaTime)
        {
            var cmd = default(P2Command);
            if (aimTarget == null || eye == null || aim == null) return cmd;

            Vector3 toTarget = aimTarget.position - eye.position;
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude < 0.0001f) return cmd;

            float targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float targetPitch = -Mathf.Atan2(toTarget.y, flat.magnitude) * Mathf.Rad2Deg;

            cmd.YawDelta = AimSector.NormalizeAngle(targetYaw - aim.DesiredWorldYaw);
            cmd.PitchDelta = targetPitch - aim.Pitch;

            float yawError = Mathf.Abs(AimSector.NormalizeAngle(targetYaw - aim.DesiredWorldYaw));
            cmd.Fire = autoFire && yawError <= fireAlignmentDegrees;

            return cmd;
        }
    }
}
