using UnityEngine;
using UnityEngine.InputSystem;

namespace BeMyArms.M1
{
    /// <summary>P1 device input: keyboard + mouse. Look drives BodyYaw (M1 default).</summary>
    public class M1P1DeviceInputSource : MonoBehaviour, IP1CommandSource
    {
        public M1Tuning tuning;

        public P1Command Read(float deltaTime)
        {
            var cmd = default(P1Command);

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard != null)
            {
                float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                cmd.Move = new Vector2(x, y);
                cmd.Sprint = keyboard.leftShiftKey.isPressed;
                cmd.Jump = keyboard.spaceKey.wasPressedThisFrame;
                cmd.Dodge = keyboard.qKey.wasPressedThisFrame;
                cmd.DodgeDirection = cmd.Move;
                cmd.Slide = keyboard.cKey.wasPressedThisFrame;
                cmd.Vault = keyboard.eKey.wasPressedThisFrame;
                cmd.LightKick = keyboard.fKey.wasPressedThisFrame;
                cmd.HeavyKick = keyboard.vKey.wasPressedThisFrame;
            }

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
