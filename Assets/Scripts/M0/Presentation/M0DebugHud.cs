using UnityEngine;

namespace BeMyArms.M0
{
    /// <summary>
    /// M0 on-screen readout. Central to the acceptance test: it makes the aim/body coupling
    /// and the boundary pin visible so the playtester can judge it.
    /// </summary>
    public class M0DebugHud : MonoBehaviour
    {
        public SharedBodyController body;
        public P2AimController aim;
        public SharedBodyHealth health;
        public float SectorHalfDegrees = 70f;

        GUIStyle _style;

        void OnGUI()
        {
            if (body == null || aim == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false };
                _style.normal.textColor = Color.white;
            }

            float offset = aim.OffsetFromBody(body.BodyYaw);
            float hp = health != null ? health.Health : 0f;

            string text =
                "M0 Shared-Body Spike\n" +
                $"Shared HP: {hp:0}\n" +
                $"BodyYaw {body.BodyYaw:0.0}   AimYaw {aim.DesiredWorldYaw:0.0}   " +
                $"Offset {offset:0.0} / +/-{SectorHalfDegrees:0}{(aim.IsPinned ? "   [PINNED]" : "")}\n" +
                "\n" +
                "P1 (left):  WASD move | mouse = BodyYaw | Shift sprint | B = test damage\n" +
                "P2 (right): gamepad right stick = aim | RT / RB = fire";

            GUI.Box(new Rect(8f, 8f, 520f, 118f), GUIContent.none);
            GUI.Label(new Rect(16f, 12f, 506f, 108f), text, _style);

            DrawSectorBar(offset);
            DrawCrosshair();
        }

        void DrawCrosshair()
        {
            // Center of the P2 (right) viewport.
            float cx = Screen.width * 0.75f;
            float cy = Screen.height * 0.5f;
            const float arm = 8f;
            const float thickness = 2f;

            GUI.Box(new Rect(cx - arm, cy - thickness * 0.5f, arm * 2f, thickness), GUIContent.none);
            GUI.Box(new Rect(cx - thickness * 0.5f, cy - arm, thickness, arm * 2f), GUIContent.none);
        }

        void DrawSectorBar(float offset)
        {
            const float width = 260f;
            const float height = 12f;
            float x = 16f;
            float y = 132f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none);

            // Boundary ticks.
            GUI.Box(new Rect(x, y - 4f, 2f, height + 8f), GUIContent.none);
            GUI.Box(new Rect(x + width - 2f, y - 4f, 2f, height + 8f), GUIContent.none);

            float center = x + width * 0.5f;
            GUI.Box(new Rect(center - 1f, y - 4f, 2f, height + 8f), GUIContent.none);

            float clamped = Mathf.Clamp(offset / Mathf.Max(1f, SectorHalfDegrees), -1f, 1f);
            float markerX = center + clamped * (width * 0.5f);
            GUI.Box(new Rect(markerX - 2f, y - 6f, 4f, height + 12f), GUIContent.none);
        }
    }
}
