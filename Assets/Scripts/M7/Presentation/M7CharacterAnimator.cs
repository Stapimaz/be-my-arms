using BeMyArms.M2;
using BeMyArms.M3;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace BeMyArms.M7
{
    /// <summary>
    /// Presentation-only Mecanim driver for the Quaternius-derived shared body. P1 plays an authored
    /// locomotion blend tree (idle/walk/jog/sprint + jump/fall/land/death); P2 plays the same
    /// locomotion for its torso and the two-bone arm IK (Animation Rigging) aims the arms at the rifle
    /// grip targets on the authoritative aim pivot. It only reads simulation/replicated state and
    /// never writes simulation, hitboxes, aim or the contract anchors.
    /// </summary>
    public class M7CharacterAnimator : MonoBehaviour
    {
        public M3DuelBody Body;
        public M3DuelClient Client;
        public Transform P1Skin;
        public Transform P2Skin;
        public Animator P1Animator;
        public Animator P2Animator;
        public Transform AimPivot;
        public Transform Weapon;
        public Transform Muzzle;
        public TwoBoneIKConstraint ArmIkL;
        public TwoBoneIKConstraint ArmIkR;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int AliveId = Animator.StringToHash("Alive");
        static readonly int ShootId = Animator.StringToHash("Shoot");
        static readonly int ReloadId = Animator.StringToHash("Reload");

        float _recoil;
        int _lastAmmo = -1;
        float _nextMuzzle;

        void Start()
        {
            if (Body == null) Body = GetComponent<M3DuelBody>();
            if (Client == null) Client = GetComponent<M3DuelClient>();
            _lastAmmo = Body != null ? Body.Ammo.Value : -1;
        }

        void LateUpdate()
        {
            if (Body == null || !Body.IsSpawned) return;

            M2BodyState state = Client != null ? Client.ViewState : Body.State.Value;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);

            bool alive = Body.Alive.Value && state.Health > 0;
            // PlanarSpeed/MoveForward come straight from the sim (predicted or replicated), so the
            // locomotion blend is stable and never bursts from frame-to-frame position noise.
            ApplyAnimator(P1Animator, state, alive);
            ApplyAnimator(P2Animator, state, alive);

            UpdateAim(state, alive);
            UpdateCombat(dt);
        }

        static void ApplyAnimator(Animator animator, M2BodyState state, bool alive)
        {
            if (animator == null) return;
            animator.SetFloat(SpeedId, state.PlanarSpeed);
            animator.SetBool(GroundedId, state.Grounded);
            animator.SetFloat(VerticalSpeedId, state.VerticalVelocity);
            animator.SetBool(AliveId, alive);
            // Backing up plays the locomotion cycle in reverse so the feet read as stepping back
            // (the library ships no dedicated backward/strafe clips). Never reverse a dead body.
            animator.speed = alive && state.MoveForward < -0.15f ? -1f : 1f;
        }

        void UpdateAim(M2BodyState state, bool alive)
        {
            if (AimPivot == null) return;

            bool localP2 = Client != null && Client.IsLocalOwnBody && Client.LocalRoleIndex == 1;
            float yaw = localP2 ? Client.LocalAimYaw : state.AimYaw;
            float pitch = localP2 ? Client.LocalAimPitch : state.AimPitch;
            float bodyYaw = Client != null ? Client.VisualYaw : state.BodyYaw;

            float kick = _recoil * 4f;
            AimPivot.localRotation = Quaternion.Euler(pitch - kick, M2BodySim.Normalize(yaw - bodyYaw), 0f);

            // The arm IK is only meaningful while the body is alive and holding the rifle.
            float ikWeight = alive ? 1f : 0f;
            if (ArmIkL != null) ArmIkL.weight = ikWeight;
            if (ArmIkR != null) ArmIkR.weight = ikWeight;
        }

        void UpdateCombat(float dt)
        {
            _recoil = Mathf.Max(0f, _recoil - dt * 8f);

            int ammo = Body.Ammo.Value;
            if (_lastAmmo < 0) { _lastAmmo = ammo; return; }
            if (ammo < _lastAmmo && !Body.Reloading.Value)
            {
                _recoil = Mathf.Min(1f, _recoil + 0.7f);
                if (P2Animator != null) P2Animator.SetTrigger(ShootId);
                if (Time.time >= _nextMuzzle && M7VfxService.Instance != null && Weapon != null)
                {
                    _nextMuzzle = Time.time + 0.045f;
                    Vector3 point = Muzzle != null ? Muzzle.position : Weapon.position + Weapon.forward * 0.45f;
                    Quaternion rotation = Muzzle != null ? Muzzle.rotation : Weapon.rotation;
                    M7VfxService.Instance.Spawn(M7VfxId.MuzzleFlash, point, rotation);
                }
            }
            _lastAmmo = ammo;
        }
    }
}
