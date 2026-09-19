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
        public P1LookController look;
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

            string lookLine = look != null
                ? $"BodyYaw {look.BodyYaw:0.0}  LookYaw {look.LookYaw:0.0}  Neck {look.NeckOffsetDegrees:0.0}" +
                  (look.IsAligning ? "  [ALIGNING]" : look.IsBodyFollowing ? "  [FOLLOW]" : "")
                : $"BodyYaw {motor.BodyYaw:0.0}";

            string text =
                "M1 Local Vertical Slice\n" +
                $"Shared HP: {hp:0}\n" +
                $"P1 state: {motor.State}   {lookLine}\n" +
                $"AimYaw {aim.DesiredWorldYaw:0.0}  Offset {offset:0.0} / +/-{SectorHalfDegrees:0}" +
                (aim.IsPinned ? "  [SECTOR PINNED]" : "") + "\n" +
                weaponLine + "\n" +
                "\nP1: WASD move (body-relative) | mouse=look | LMB/Alt=align body\n" +
                "     Shift sprint | Space jump | Q dodge | C slide | E vault | F/V kicks | B damage\n" +
                "P2: gamepad right stick aim | RT fire | X reload | Y swap | B knife";

            GUI.Box(new Rect(8f, 8f, 620f, 168f), GUIContent.none);
            GUI.Label(new Rect(16f, 12f, 606f, 158f), text, _style);

            DrawSectorBar(offset);
        }

        void DrawSectorBar(float offset)
        {
            const float width = 260f;
            const float height = 12f;
            float x = 16f;
            float y = 182f;

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
