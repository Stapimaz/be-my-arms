using UnityEngine;
using UnityEngine.InputSystem;

namespace BeMyArms.M0
{
    public enum P2InputMode
    {
        Gamepad,
        ScriptedBot
    }

    /// <summary>
    /// M0 orchestrator. Reads both role input streams in a fixed order each frame:
    ///   1. P1 command  -> BodyYaw + movement
    ///   2. P2 command  -> aim (clamped against the NEW BodyYaw)
    ///   3. weapon      -> hitscan along the world aim
    /// Cameras update themselves in LateUpdate.
    ///
    /// Keeping this order explicit matters: the aim is always resolved against the body
    /// orientation produced by P1 that same step.
    /// </summary>
    public class M0BodyRoot : MonoBehaviour
    {
        [Header("Wiring")]
        public M0Tuning tuning;
        public SharedBodyController body;
        public P2AimController aim;
        public HitscanWeapon weapon;
        public SharedBodyHealth health;
        public Transform aimEye;
        public P1ThirdPersonCamera p1Camera;

        [Header("Input")]
        public P2InputMode p2InputMode = P2InputMode.Gamepad;
        public MonoBehaviour p1InputBehaviour;
        public MonoBehaviour p2DeviceInputBehaviour;
        public MonoBehaviour p2ScriptedInputBehaviour;

        IP1InputSource _p1;
        IP2InputSource _p2;

        void Start()
        {
            _p1 = p1InputBehaviour as IP1InputSource;
            _p2 = (p2InputMode == P2InputMode.ScriptedBot ? p2ScriptedInputBehaviour : p2DeviceInputBehaviour) as IP2InputSource;

            if (_p1 == null) Debug.LogError("[M0] No P1 input source assigned.", this);
            if (_p2 == null) Debug.LogError("[M0] No P2 input source assigned.", this);

            if (tuning != null)
            {
                body.MoveSpeed = tuning.moveSpeed;
                body.SprintMultiplier = tuning.sprintMultiplier;
                body.Gravity = tuning.gravity;
                aim.SectorHalfDegrees = tuning.sectorHalfDegrees;
                aim.MaxPitchDegrees = tuning.maxPitchDegrees;
                weapon.Configure(tuning);
                if (health != null) health.MaxHealth = tuning.maxHealth;
                if (p1Camera != null)
                {
                    p1Camera.distance = tuning.p1CameraDistance;
                    p1Camera.height = tuning.p1CameraHeight;
                }
            }

            aim.Initialize(body.BodyYaw);
        }

        void Update()
        {
            if (_p1 == null || _p2 == null) return;

            float deltaTime = Time.deltaTime;

            P1Command p1 = _p1.Read(deltaTime);
            if (p1Camera != null) p1Camera.AddPitch(p1.LookPitchDelta);
            body.Step(p1, deltaTime);

            P2Command p2 = _p2.Read(deltaTime);
            aim.Step(p2, body.BodyYaw);
            weapon.Step(p2.Fire, deltaTime, aim, aimEye, health);

            // M0 debug: demonstrates the single shared HP pool is reachable.
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame && health != null)
                health.ApplyDamage(10f, HitboxRegion.Region.Body);
        }
    }
}
