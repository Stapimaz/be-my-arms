using BeMyArms.M0;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeMyArms.M1
{
    /// <summary>
    /// M1 orchestrator. Reads both role streams and steps the simulation in a fixed order:
    /// P1 command -> BodyYaw + locomotion/melee, P2 command -> aim, then the weapon.
    ///
    /// Simulation (motor/aim/weapon) is kept separate from presentation (cameras/HUD) so the NGO
    /// prediction layer can drive the simulation later.
    /// </summary>
    public class M1BodyRoot : MonoBehaviour
    {
        [Header("Simulation")]
        public M1Tuning tuning;
        public P1Motor motor;
        public P2AimRig aim;
        public WeaponController weapon;
        public SharedBodyHealth health;

        [Header("Presentation")]
        public Transform aimEye;
        public P1ThirdPersonCamera p1Camera;

        [Header("Input")]
        public M1InputMode inputMode = M1InputMode.Device;
        public MonoBehaviour p1DeviceInputBehaviour;
        public MonoBehaviour p2DeviceInputBehaviour;
        public MonoBehaviour scriptedInputBehaviour;

        IP1CommandSource _p1;
        IP2CommandSource _p2;

        void Start()
        {
            bool scripted = inputMode == M1InputMode.Scripted;
            var p1Device = (p1DeviceInputBehaviour as M1P1DeviceInputSource) ?? GetComponent<M1P1DeviceInputSource>();
            var p2Device = (p2DeviceInputBehaviour as M1P2DeviceInputSource) ?? GetComponent<M1P2DeviceInputSource>();
            var scriptedSource = (scriptedInputBehaviour as M1ScriptedInputSource) ?? GetComponent<M1ScriptedInputSource>();

            _p1 = scripted ? (IP1CommandSource)scriptedSource : p1Device;
            _p2 = scripted ? (IP2CommandSource)scriptedSource : p2Device;

            if (_p1 == null) Debug.LogError("[M1] No P1 input source assigned.");
            if (_p2 == null) Debug.LogError("[M1] No P2 input source assigned.");

            motor.Configure(tuning);
            if (tuning != null)
            {
                aim.SectorHalfDegrees = tuning.sectorHalfDegrees;
                aim.MaxPitchDegrees = tuning.maxPitchDegrees;
                if (health != null) health.MaxHealth = tuning.maxHealth;
                if (p1Camera != null)
                {
                    p1Camera.distance = tuning.p1CameraDistance;
                    p1Camera.height = tuning.p1CameraHeight;
                }
            }

            weapon.Configure(tuning, aim, aimEye);
            aim.Initialize(motor.BodyYaw);
        }

        void Update()
        {
            if (_p1 == null || _p2 == null) return;

            float deltaTime = Time.deltaTime;

            P1Command p1 = _p1.Read(deltaTime);
            if (p1Camera != null) p1Camera.AddPitch(p1.LookPitchDelta);
            motor.Step(p1, deltaTime);

            P2Command p2 = _p2.Read(deltaTime);
            aim.Step(p2, motor.BodyYaw);
            weapon.Step(p2, motor.BodyYaw, motor.State, deltaTime);

            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame && health != null)
                health.ApplyDamage(10f, HitboxRegion.Region.Body);
        }
    }
}
