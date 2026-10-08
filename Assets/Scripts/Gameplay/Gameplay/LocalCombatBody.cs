using BeMyArms.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeMyArms.Gameplay
{
    /// <summary>
    /// Gameplay orchestrator. Step order:
    ///   P1 command -> look/body model (BodyYaw from decoupled look + follow + align),
    ///   P1 motor (movement/melee, relative to BodyYaw),
    ///   P2 command -> aim clamped to the sector around BodyYaw,
    ///   weapon.
    ///
    /// Simulation (look/motor/aim/weapon) is separate from presentation (cameras/HUD).
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M1", "BeMyArms.M1", "M1BodyRoot")]
    public class LocalCombatBody : MonoBehaviour
    {
        [Header("Simulation")]
        public LocalCombatTuning tuning;
        public P1LookController look;
        public P1Motor motor;
        public P2AimRig aim;
        public WeaponController weapon;
        public SharedBodyHealth health;

        [Header("Presentation")]
        public Transform aimEye;
        public LocalP1Camera p1Camera;

        [Header("Input")]
        public LocalInputMode inputMode = LocalInputMode.Device;
        public MonoBehaviour p1DeviceInputBehaviour;
        public MonoBehaviour p2DeviceInputBehaviour;
        public MonoBehaviour scriptedInputBehaviour;

        IP1CommandSource _p1;
        IP2CommandSource _p2;

        void Start()
        {
            bool scripted = inputMode == LocalInputMode.Scripted;
            var p1Device = (p1DeviceInputBehaviour as LocalP1DeviceInputSource) ?? GetComponent<LocalP1DeviceInputSource>();
            var p2Device = (p2DeviceInputBehaviour as LocalP2DeviceInputSource) ?? GetComponent<LocalP2DeviceInputSource>();
            var scriptedSource = (scriptedInputBehaviour as LocalScriptedInputSource) ?? GetComponent<LocalScriptedInputSource>();

            _p1 = scripted ? (IP1CommandSource)scriptedSource : p1Device;
            _p2 = scripted ? (IP2CommandSource)scriptedSource : p2Device;

            if (_p1 == null) Debug.LogError("[Gameplay] No P1 input source assigned.");
            if (_p2 == null) Debug.LogError("[Gameplay] No P2 input source assigned.");

            motor.look = look;
            look.Configure(tuning);
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
            look.Initialize(transform.eulerAngles.y);
            aim.Initialize(look.BodyYaw);
        }

        void Update()
        {
            if (_p1 == null || _p2 == null) return;

            float deltaTime = Time.deltaTime;

            P1Command p1 = _p1.Read(deltaTime);
            look.Step(p1.LookYawDelta, p1.AlignBody, deltaTime);
            if (p1Camera != null) p1Camera.AddPitch(p1.LookPitchDelta);
            motor.Step(p1, deltaTime);

            P2Command p2 = _p2.Read(deltaTime);
            aim.Step(p2, look.BodyYaw);
            weapon.Step(p2, look.BodyYaw, motor.State, deltaTime);

            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame && health != null)
                health.ApplyDamage(10f, HitboxRegion.Region.Body);
        }
    }
}
