using System;
using UnityEngine;

namespace BeMyArms.M1
{
    public enum WeaponKind
    {
        Rifle,
        Pistol,
        Knife
    }

    /// <summary>Per-weapon tuning. Values are TUNING and live in the M1Tuning asset.</summary>
    [Serializable]
    public class WeaponDefinition
    {
        public string displayName = "Rifle";
        public WeaponKind kind = WeaponKind.Rifle;
        public float damage = 18f;
        public float headshotMultiplier = 2.5f;
        public float roundsPerMinute = 480f;
        public int magazine = 30;
        public float reloadSeconds = 2.2f;
        public float rangeMeters = 150f;
        public float baseSpreadDegrees = 1.2f;
        public float recoilPerShot = 0.35f;
        public float swapSeconds = 0.5f;
        public float meleeRangeMeters = 2.2f;

        public float SecondsBetweenShots => 60f / Mathf.Max(1f, roundsPerMinute);
    }

    /// <summary>Movement state to accuracy multiplier row.</summary>
    [Serializable]
    public class AccuracyRow
    {
        public MovementState state;
        public float spreadMultiplier = 1f;
    }

    /// <summary>
    /// M1 tuning. All gameplay numbers live here so they can be changed without recompiling.
    /// Greybox/proxy assets are temporary; this is data, not final balance.
    /// </summary>
    [CreateAssetMenu(menuName = "Be My Arms/M1 Tuning", fileName = "M1Tuning")]
    public class M1Tuning : ScriptableObject
    {
        [Header("Aim (Model C, prototyped direction)")]
        [Range(10f, 120f)] public float sectorHalfDegrees = 70f;
        [Range(30f, 89f)] public float maxPitchDegrees = 80f;

        [Header("Look")]
        public float p1MouseSensitivity = 0.12f;
        public float gamepadYawSpeed = 220f;
        public float gamepadPitchSpeed = 160f;

        [Header("P1 look/body model (all TUNING)")]
        [Tooltip("Max head/camera yaw offset from BodyYaw.")]
        public float neckYawLimitDegrees = 80f;
        [Tooltip("Look offset past which the body starts following the look.")]
        public float bodyFollowThresholdDegrees = 50f;
        [Tooltip("Degrees/second the body turns to follow the look when past the threshold.")]
        public float bodyFollowSpeedDegreesPerSecond = 120f;
        [Tooltip("Degrees/second the body turns for the explicit AlignBody action.")]
        public float bodyAlignSpeedDegreesPerSecond = 540f;
        [Tooltip("Temporary/configurable binding for AlignBody; not a design decision.")]
        public bool alignBodyOnLeftMouse = true;
        public UnityEngine.InputSystem.Key alignBodyKey = UnityEngine.InputSystem.Key.LeftAlt;

        [Header("P1 locomotion")]
        public float walkSpeed = 4.5f;
        public float sprintSpeed = 7f;
        public float gravity = -20f;
        public float jumpSpeed = 6f;

        [Header("P1 dodge (no i-frames)")]
        public float dodgeSpeed = 11f;
        public float dodgeDuration = 0.22f;
        public float dodgeCooldown = 0.9f;

        [Header("P1 slide")]
        public float slideEntrySpeed = 9f;
        public float slideFriction = 6f;
        public float slideMinDuration = 0.35f;
        public float slideMaxDuration = 1.1f;

        [Header("P1 vault")]
        public float vaultDuration = 0.45f;
        public float vaultReachMeters = 1.4f;
        public float vaultHeightMeters = 1.2f;

        [Header("P1 kicks")]
        public float lightKickDuration = 0.35f;
        public float lightKickCooldown = 0.5f;
        public float lightKickDamage = 12f;
        public float heavyKickDuration = 0.7f;
        public float heavyKickCooldown = 1.6f;
        public float heavyKickDamage = 28f;
        public float kickRangeMeters = 2.4f;

        [Header("P2 weapons")]
        public WeaponDefinition[] weapons;
        public int startingWeaponIndex = 0;

        [Header("Movement -> accuracy")]
        public AccuracyRow[] accuracyByState;

        [Header("Health")]
        public float maxHealth = 100f;

        [Header("Cameras / body")]
        public float p1CameraDistance = 4.5f;
        public float p1CameraHeight = 1.6f;
        public Vector3 p2ShoulderOffset = new Vector3(0f, 1.45f, 0.22f);
    }
}
