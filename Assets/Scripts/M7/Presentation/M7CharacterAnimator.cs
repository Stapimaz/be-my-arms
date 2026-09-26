using BeMyArms.M2;
using BeMyArms.M3;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace BeMyArms.M7
{
    /// <summary>
    /// Presentation-only Mecanim driver for the Quaternius-derived shared body.
    ///
    /// Locomotion is a 2D directional blend driven by the sim's body-local movement direction
    /// (MoveRight/MoveForward, magnitude = speed). Forward/back/strafe/diagonal are real authored
    /// clips (the strafe/back clips are baked from the CC0 walk/jog/sprint cycles), so the fighter
    /// strafes while facing BodyYaw without any per-frame bone rotation. P2 plays the same locomotion
    /// for its torso and the two-bone arm IK aims the arms at the rifle grips on the aim pivot.
    ///
    /// It only reads simulation/replicated state and never writes simulation, hitboxes, aim or the
    /// contract anchors.
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

        [Header("Stance")]
        public float CrouchDrop = 0.33f;
        public float CrouchBlendSpeed = 10f;

        /// <summary>Diagnostic: the current crouch blend 0..1.</summary>
        public float CrouchWeight => _crouch01;

        static readonly int MoveXId = Animator.StringToHash("MoveX");
        static readonly int MoveYId = Animator.StringToHash("MoveY");
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int AliveId = Animator.StringToHash("Alive");
        static readonly int ShootId = Animator.StringToHash("Shoot");
        static readonly int ReloadId = Animator.StringToHash("Reload");

        Vector3 _aimPivotBase;
        float _crouch01;
        float _recoil;
        int _lastAmmo = -1;
        float _nextMuzzle;

        void Start()
        {
            if (Body == null) Body = GetComponent<M3DuelBody>();
            if (Client == null) Client = GetComponent<M3DuelClient>();
            _lastAmmo = Body != null ? Body.Ammo.Value : -1;
            if (AimPivot != null) _aimPivotBase = AimPivot.localPosition;
        }

        void LateUpdate()
        {
            if (Body == null || !Body.IsSpawned) return;

            M2BodyState state = Client != null ? Client.ViewState : Body.State.Value;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);

            bool alive = Body.Alive.Value && state.Health > 0;
            ApplyAnimator(P1Animator, state, alive);
            ApplyAnimator(P2Animator, state, alive);

            UpdateStance(state, alive, dt);
            UpdateAim(state, alive);
            UpdateCombat(dt);
        }

        void ApplyAnimator(Animator animator, M2BodyState state, bool alive)
        {
            if (animator == null) return;
            // Body-local movement direction scaled by speed magnitude (walk ~0.64, sprint 1.0); the
            // 2D blend picks forward/back/strafe and blends diagonals naturally.
            float sprint = Body != null && Body.SprintSpeed > 0.01f ? Body.SprintSpeed : 7f;
            float magnitude = Mathf.Clamp(state.PlanarSpeed / sprint, 0f, 1f);
            animator.SetFloat(MoveXId, state.MoveRight * magnitude);
            animator.SetFloat(MoveYId, state.MoveForward * magnitude);
            animator.SetFloat(SpeedId, state.PlanarSpeed);
            animator.SetBool(GroundedId, state.Grounded);
            animator.SetFloat(VerticalSpeedId, state.VerticalVelocity);
            animator.SetBool(AliveId, alive);
        }

        /// <summary>Blends the crouch override layer and drops the weapon mount with the stance.</summary>
        void UpdateStance(M2BodyState state, bool alive, float dt)
        {
            float target = alive && state.Crouching ? 1f : 0f;
            _crouch01 = Mathf.Lerp(_crouch01, target, 1f - Mathf.Exp(-CrouchBlendSpeed * dt));
            if (P1Animator != null) P1Animator.SetLayerWeight(1, _crouch01);
            if (P2Animator != null) P2Animator.SetLayerWeight(1, _crouch01);
            if (AimPivot != null) AimPivot.localPosition = _aimPivotBase + Vector3.down * (CrouchDrop * _crouch01);
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
