using UnityEngine;
using UnityEngine.InputSystem;

namespace BeMyArms.Core
{
    /// <summary>
    /// P1 device input for Core: keyboard + mouse.
    /// Core temporary assumption: look input drives BodyYaw directly and the third-person
    /// camera follows that yaw. There is no free-look yet.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M0", "BeMyArms.M0", "P1DeviceInputSource")]
    public class P1DeviceInputSource : MonoBehaviour, IP1InputSource
    {
        public SharedControlTuning tuning;

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
