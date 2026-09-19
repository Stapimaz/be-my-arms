using UnityEngine;

namespace BeMyArms.M0
{
    /// <summary>
    /// M0 tuning. Deliberately a single throwaway asset: this spike is disposable and the
    /// roadmap only requires the values to be data-driven so they can be changed without
    /// recompiling. All values are TUNING, not locked decisions.
    /// </summary>
    [CreateAssetMenu(menuName = "Be My Arms/M0 Tuning", fileName = "M0Tuning")]
    public class M0Tuning : ScriptableObject
    {
        [Header("Aim sector (Model C prototype)")]
        [Range(10f, 120f)] public float sectorHalfDegrees = 70f;
        [Range(30f, 89f)] public float maxPitchDegrees = 80f;

        [Header("Look sensitivity")]
        public float p1MouseSensitivity = 0.12f;
        public float gamepadYawSpeed = 220f;
        public float gamepadPitchSpeed = 160f;

        [Header("Movement (P1)")]
        public float moveSpeed = 5f;
        public float sprintMultiplier = 1.5f;
        public float gravity = -20f;

        [Header("Weapon (P2)")]
        public float fireRatePerSecond = 8f;
        public float damage = 18f;
        public float headshotMultiplier = 2.5f;
        public float rangeMeters = 150f;

        [Header("Health")]
        public float maxHealth = 100f;

        [Header("Cameras")]
        public float p1CameraDistance = 4.5f;
        public float p1CameraHeight = 1.6f;
        public Vector3 p2ShoulderOffset = new Vector3(0f, 1.45f, 0.22f);
    }
}
