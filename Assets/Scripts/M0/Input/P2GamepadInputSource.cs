using UnityEngine;
using UnityEngine.InputSystem;

namespace BeMyArms.M0
{
    /// <summary>
    /// P2 device input for M0: gamepad right stick aims, right trigger / shoulder fires.
    /// The right stick is a rate, so its contribution is scaled by deltaTime.
    /// </summary>
    public class P2GamepadInputSource : MonoBehaviour, IP2InputSource
    {
        public M0Tuning tuning;

        public P2Command Read(float deltaTime)
        {
            var cmd = default(P2Command);

            var gamepad = Gamepad.current;
            if (gamepad == null) return cmd;

            Vector2 stick = gamepad.rightStick.ReadValue();
            float yawSpeed = tuning != null ? tuning.gamepadYawSpeed : 220f;
            float pitchSpeed = tuning != null ? tuning.gamepadPitchSpeed : 160f;

            cmd.YawDelta = stick.x * yawSpeed * deltaTime;
            cmd.PitchDelta = -stick.y * pitchSpeed * deltaTime;
            cmd.Fire = gamepad.rightTrigger.ReadValue() > 0.5f || gamepad.rightShoulder.isPressed;

            return cmd;
        }
    }
}
