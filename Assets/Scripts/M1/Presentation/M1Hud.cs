using BeMyArms.M0;
using UnityEngine;

namespace BeMyArms.M1
{
    /// <summary>M1 debug readout: posture, sector coupling and weapon state.</summary>
    public class M1Hud : MonoBehaviour
    {
        public P1Motor motor;
        public P2AimRig aim;
        public WeaponController weapon;
        public SharedBodyHealth health;
        public float SectorHalfDegrees = 70f;

        GUIStyle _style;

        void OnGUI()
        {
            if (motor == null || aim == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
                _style.normal.textColor = Color.white;
            }

            float offset = aim.OffsetFromBody(motor.BodyYaw);
            string weaponLine = weapon != null && weapon.Current != null
                ? $"{weapon.Current.displayName}  ammo {weapon.CurrentAmmo}/{weapon.Current.magazine}" +
                  (weapon.IsReloading ? "  [RELOADING]" : "") +
                  $"  spread {weapon.CurrentSpreadDegrees:0.0} deg"
                : "no weapon";
            float hp = health != null ? health.Health : 0f;

            string text =
                "M1 Local Vertical Slice\n" +
                $"Shared HP: {hp:0}\n" +
                $"P1 state: {motor.State}\n" +
                $"BodyYaw {motor.BodyYaw:0.0}  AimYaw {aim.DesiredWorldYaw:0.0}  " +
                $"Offset {offset:0.0} / +/-{SectorHalfDegrees:0}{(aim.IsPinned ? "  [PINNED]" : "")}\n" +
                weaponLine + "\n" +
                "\nP1: WASD move | mouse=BodyYaw | Shift sprint | Space jump | Q dodge | C slide\n" +
                "     E vault | F light kick | V heavy kick | B test damage\n" +
                "P2: gamepad right stick aim | RT fire | X reload | Y swap | B knife";

            GUI.Box(new Rect(8f, 8f, 560f, 150f), GUIContent.none);
            GUI.Label(new Rect(16f, 12f, 546f, 140f), text, _style);

            DrawSectorBar(offset);
        }

        void DrawSectorBar(float offset)
        {
            const float width = 260f;
            const float height = 12f;
            float x = 16f;
            float y = 164f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
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
