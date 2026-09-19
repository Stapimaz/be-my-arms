using UnityEngine;
using UnityEngine.InputSystem;

namespace BeMyArms.M1
{
    /// <summary>P2 device input: gamepad. Aim is a rate, so it is scaled by deltaTime.</summary>
    public class M1P2DeviceInputSource : MonoBehaviour, IP2CommandSource
    {
        public M1Tuning tuning;
        public WeaponController weapon;

        public P2Command Read(float deltaTime)
        {
            var cmd = default(P2Command);
            var gamepad = Gamepad.current;
            if (gamepad == null) return cmd;

            Vector2 stick = gamepad.rightStick.ReadValue();
            float yawSpeed = tuning != null ? tuning.gamepadYawSpeed : 220f;
            float pitchSpeed = tuning != null ? tuning.gamepadPitchSpeed : 160f;
            cmd.AimYawDelta = stick.x * yawSpeed * deltaTime;
            cmd.AimPitchDelta = -stick.y * pitchSpeed * deltaTime;

            cmd.Fire = gamepad.rightTrigger.ReadValue() > 0.5f || gamepad.rightShoulder.isPressed;
            cmd.Reload = gamepad.buttonWest.wasPressedThisFrame;
            cmd.KnifeAttack = gamepad.buttonEast.wasPressedThisFrame;

            if (gamepad.buttonNorth.wasPressedThisFrame && weapon != null && weapon.tuning != null && weapon.tuning.weapons != null)
            {
                int count = weapon.tuning.weapons.Length;
                if (count > 0)
                {
                    cmd.SwitchRequested = true;
                    cmd.SwitchWeapon = (weapon.CurrentIndex + 1) % count;
                }
            }

            return cmd;
        }
    }
}
