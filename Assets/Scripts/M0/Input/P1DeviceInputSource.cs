using UnityEngine;
using UnityEngine.InputSystem;

namespace BeMyArms.M0
{
    /// <summary>
    /// P1 device input for M0: keyboard + mouse.
    /// M0 temporary assumption: look input drives BodyYaw directly and the third-person
    /// camera follows that yaw. There is no free-look yet.
    /// </summary>
    public class P1DeviceInputSource : MonoBehaviour, IP1InputSource
    {
        public M0Tuning tuning;

        public P1Command Read(float deltaTime)
        {
            var cmd = default(P1Command);

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                cmd.Move = new Vector2(x, y);
                cmd.Sprint = keyboard.leftShiftKey.isPressed;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                float sensitivity = tuning != null ? tuning.p1MouseSensitivity : 0.12f;
                cmd.LookYawDelta = delta.x * sensitivity;
                cmd.LookPitchDelta = -delta.y * sensitivity;
            }

            return cmd;
        }
    }
}
