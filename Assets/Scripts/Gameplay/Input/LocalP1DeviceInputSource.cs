using UnityEngine;
using UnityEngine.InputSystem;

namespace BeMyArms.Gameplay
{
    /// <summary>P1 device input: keyboard + mouse. Look drives BodyYaw (Gameplay default).</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M1", "BeMyArms.M1", "M1P1DeviceInputSource")]
    public class LocalP1DeviceInputSource : MonoBehaviour, IP1CommandSource
    {
        public LocalCombatTuning tuning;

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

            // Explicit align-body action. Temporary/configurable binding (not a design decision).
            bool alignOnMouse = tuning != null && tuning.alignBodyOnLeftMouse && mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool alignOnKey = tuning != null && keyboard != null && keyboard[tuning.alignBodyKey].wasPressedThisFrame;
            cmd.AlignBody = alignOnMouse || alignOnKey;

            return cmd;
        }
    }
}
