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
    ///
    /// Directional locomotion: the sim publishes the body-local movement direction (MoveForward /
    /// MoveRight); the driver leads the legs toward it (hip-lead) and counter-rotates the torso for
    /// conventional third-person strafing without turning the fighter's facing. Crouch is a full-body
    /// override layer weighted from the replicated stance.
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

        /// <summary>Diagnostic: the current directional leg-lead yaw (degrees) and crouch blend 0..1.</summary>
        public float CurrentLegYaw => _legYaw;
        public float CrouchWeight => _crouch01;

        [Header("Directional locomotion")]
        public float MaxLegYaw = 55f;
        public float LegYawSmoothing = 12f;
        public float CrouchDrop = 0.33f;
        public float CrouchBlendSpeed = 10f;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int AliveId = Animator.StringToHash("Alive");
        static readonly int ShootId = Animator.StringToHash("Shoot");
        static readonly int ReloadId = Animator.StringToHash("Reload");

        Transform _p1Hips;
        Transform _p1Spine;
        Vector3 _aimPivotBase;
        float _legYaw;
        float _crouch01;
        float _recoil;
        int _lastAmmo = -1;
        float _nextMuzzle;

        void Start()
        {
            if (Body == null) Body = GetComponent<M3DuelBody>();
            if (Client == null) Client = GetComponent<M3DuelClient>();
            _lastAmmo = Body != null ? Body.Ammo.Value : -1;
            if (P1Skin != null)
            {
                _p1Hips = Find(P1Skin, "DEF-hips");
                _p1Spine = Find(P1Skin, "DEF-spine.001");
            }
            if (AimPivot != null) _aimPivotBase = AimPivot.localPosition;
        }

        void LateUpdate()
        {
            if (Body == null || !Body.IsSpawned) return;

            M2BodyState state = Client != null ? Client.ViewState : Body.State.Value;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);

            bool alive = Body.Alive.Value && state.Health > 0;
            // PlanarSpeed/MoveForward/MoveRight come straight from the sim (predicted or replicated),
            // so the locomotion blend is stable and never bursts from frame-to-frame position noise.
            ApplyAnimator(P1Animator, state, alive);
            ApplyAnimator(P2Animator, state, alive);

            UpdateStance(state, alive, dt);
            ApplyDirectionalLegs(state, dt);
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
            // Backing up plays the locomotion cycle in reverse so the feet read as stepping back.
            // Never reverse a dead body.
            animator.speed = alive && state.MoveForward < -0.15f ? -1f : 1f;
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

        /// <summary>
        /// Conventional third-person strafing: lead the legs toward the body-local movement direction
        /// and counter-rotate the spine so the fighter keeps facing BodyYaw/look. The library's free
        /// subset has no authored strafe clips, so this re-directs the existing authored leg swing
        /// rather than rotating the whole fighter.
        /// </summary>
        void ApplyDirectionalLegs(M2BodyState state, float dt)
        {
            if (_p1Hips == null) return;

            float target = 0f;
            float length = Mathf.Sqrt(state.MoveForward * state.MoveForward + state.MoveRight * state.MoveRight);
            if (state.Grounded && length > 0.05f && state.PlanarSpeed > 0.15f)
            {
                // Body-local movement angle (0 = forward, + = right). Backward is conveyed by reverse
                // playback, so the lateral component alone leads the legs.
                float denom = Mathf.Max(0.3f, state.MoveForward);
                target = Mathf.Clamp(Mathf.Atan2(state.MoveRight, denom) * Mathf.Rad2Deg, -MaxLegYaw, MaxLegYaw);
            }
            _legYaw = Mathf.Lerp(_legYaw, target, 1f - Mathf.Exp(-LegYawSmoothing * dt));
            if (Mathf.Abs(_legYaw) < 0.05f) return;

            Quaternion hipsLocal = _p1Hips.localRotation;
            Vector3 upInHipsParent = _p1Hips.parent != null
                ? _p1Hips.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
            _p1Hips.localRotation = Quaternion.AngleAxis(_legYaw, upInHipsParent) * hipsLocal;

            if (_p1Spine != null)
            {
                Quaternion spineLocal = _p1Spine.localRotation;
                Vector3 upInSpineParent = _p1Spine.parent != null
                    ? _p1Spine.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
                _p1Spine.localRotation = Quaternion.AngleAxis(-_legYaw, upInSpineParent) * spineLocal;
            }
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

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }
    }
}
