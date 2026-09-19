using UnityEngine;

namespace BeMyArms.M1
{
    /// <summary>
    /// Deterministic synthetic input for both roles. Used to verify M1 without a human, and for the
    /// automated smoke run: P1 moves/sprints and periodically jumps, dodges, slides and kicks; P2
    /// tracks a target, fires and reloads.
    /// </summary>
    public class M1ScriptedInputSource : MonoBehaviour, IP1CommandSource, IP2CommandSource
    {
        public Transform aimTarget;
        public P2AimRig aim;
        public WeaponController weapon;
        public Transform eye;

        float _actionClock;

        P1Command IP1CommandSource.Read(float deltaTime)
        {
            var cmd = default(P1Command);
            cmd.Move = new Vector2(0f, 1f);
            cmd.Sprint = true;

            _actionClock += deltaTime;
            float cycle = _actionClock % 8f;
            if (cycle < 0.05f) cmd.Jump = true;
            else if (cycle > 1.0f && cycle < 1.05f) cmd.Dodge = true;
            else if (cycle > 2.0f && cycle < 2.05f) { cmd.Dodge = true; cmd.DodgeDirection = new Vector2(1f, 0f); }
            else if (cycle > 3.0f && cycle < 3.05f) cmd.Slide = true;
            else if (cycle > 4.0f && cycle < 4.05f) cmd.LightKick = true;
            else if (cycle > 5.0f && cycle < 5.05f) cmd.HeavyKick = true;

            cmd.LookYawDelta = 18f * deltaTime;
            return cmd;
        }

        P2Command IP2CommandSource.Read(float deltaTime)
        {
            var cmd = default(P2Command);
            if (aimTarget == null || aim == null) return cmd;

            Vector3 origin = eye != null ? eye.position : transform.position;
            Vector3 to = aimTarget.position - origin;
            var flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude < 0.0001f) return cmd;

            float targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float targetPitch = -Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg;
            float error = BeMyArms.M0.AimSector.NormalizeAngle(targetYaw - aim.DesiredWorldYaw);
            cmd.AimYawDelta = error;
            cmd.AimPitchDelta = targetPitch - aim.Pitch;
            cmd.Fire = Mathf.Abs(error) <= 2.5f;

            if (weapon != null && weapon.Current != null && !weapon.IsReloading && weapon.CurrentAmmo == 0)
                cmd.Reload = true;

            return cmd;
        }
    }
}
