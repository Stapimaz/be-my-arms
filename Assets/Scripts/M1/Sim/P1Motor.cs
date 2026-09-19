using BeMyArms.M0;
using UnityEngine;

namespace BeMyArms.M1
{
    /// <summary>
    /// P1 locomotion and melee. Owns BodyYaw and the movement state that drives P2 accuracy.
    /// P1 actions never lock P2's weapon: they only change posture and therefore spread.
    ///
    /// Simulation-only: contains no camera/HUD/animation code, so it can later be driven by the
    /// NGO prediction layer (see TECHNICAL_PLAN.md).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class P1Motor : MonoBehaviour
    {
        public float WalkSpeed = 4.5f;
        public float SprintSpeed = 7f;
        public float Gravity = -20f;
        public float JumpSpeed = 6f;
        public float DodgeSpeed = 11f;
        public float DodgeDuration = 0.22f;
        public float DodgeCooldown = 0.9f;
        public float SlideEntrySpeed = 9f;
        public float SlideFriction = 6f;
        public float SlideMinDuration = 0.35f;
        public float SlideMaxDuration = 1.1f;
        public float VaultDuration = 0.45f;
        public float VaultReachMeters = 1.4f;
        public float VaultHeightMeters = 1.2f;
        public float LightKickDuration = 0.35f;
        public float LightKickCooldown = 0.5f;
        public float LightKickDamage = 12f;
        public float HeavyKickDuration = 0.7f;
        public float HeavyKickCooldown = 1.6f;
        public float HeavyKickDamage = 28f;
        public float KickRangeMeters = 2.4f;

        public float BodyYaw { get; private set; }
        public MovementState State { get; private set; } = MovementState.Idle;

        CharacterController _controller;
        float _verticalVelocity;
        float _dodgeEndTime, _dodgeReadyTime;
        float _slideEndTime;
        float _vaultEndTime;
        float _lightEndTime, _lightReadyTime;
        float _heavyEndTime, _heavyReadyTime;
        Vector3 _dodgeDirection;
        Vector3 _slideVelocity;
        Vector3 _vaultStart, _vaultTarget;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            BodyYaw = transform.eulerAngles.y;
        }

        public void Configure(M1Tuning tuning)
        {
            if (tuning == null) return;
            WalkSpeed = tuning.walkSpeed;
            SprintSpeed = tuning.sprintSpeed;
            Gravity = tuning.gravity;
            JumpSpeed = tuning.jumpSpeed;
            DodgeSpeed = tuning.dodgeSpeed;
            DodgeDuration = tuning.dodgeDuration;
            DodgeCooldown = tuning.dodgeCooldown;
            SlideEntrySpeed = tuning.slideEntrySpeed;
            SlideFriction = tuning.slideFriction;
            SlideMinDuration = tuning.slideMinDuration;
            SlideMaxDuration = tuning.slideMaxDuration;
            VaultDuration = tuning.vaultDuration;
            VaultReachMeters = tuning.vaultReachMeters;
            VaultHeightMeters = tuning.vaultHeightMeters;
            LightKickDuration = tuning.lightKickDuration;
            LightKickCooldown = tuning.lightKickCooldown;
            LightKickDamage = tuning.lightKickDamage;
            HeavyKickDuration = tuning.heavyKickDuration;
            HeavyKickCooldown = tuning.heavyKickCooldown;
            HeavyKickDamage = tuning.heavyKickDamage;
            KickRangeMeters = tuning.kickRangeMeters;
        }

        public void Step(in P1Command cmd, float deltaTime)
        {
            // BodyYaw is P1-only and always responsive, even mid-action.
            BodyYaw = AimSector.NormalizeAngle(BodyYaw + cmd.LookYawDelta);
            transform.rotation = Quaternion.Euler(0f, BodyYaw, 0f);

            float now = Time.time;

            if (now < _dodgeEndTime) { ContinueDodge(deltaTime); return; }
            if (now < _vaultEndTime) { ContinueVault(now); return; }
            if (now < _lightEndTime) { State = MovementState.KickLight; ApplyGravityOnly(deltaTime); return; }
            if (now < _heavyEndTime) { State = MovementState.KickHeavy; ApplyGravityOnly(deltaTime); return; }
            if (now < _slideEndTime) { ContinueSlide(cmd, deltaTime); return; }

            if (TryStartAction(cmd, now)) return;

            Locomotion(cmd, deltaTime);
        }

        bool TryStartAction(in P1Command cmd, float now)
        {
            if (cmd.HeavyKick && now >= _heavyReadyTime)
            {
                _heavyEndTime = now + HeavyKickDuration;
                _heavyReadyTime = now + HeavyKickDuration + HeavyKickCooldown;
                ApplyKickDamage(HeavyKickDamage);
                State = MovementState.KickHeavy;
                ApplyGravityOnly(Time.deltaTime);
                return true;
            }
            if (cmd.LightKick && now >= _lightReadyTime)
            {
                _lightEndTime = now + LightKickDuration;
                _lightReadyTime = now + LightKickDuration + LightKickCooldown;
                ApplyKickDamage(LightKickDamage);
                State = MovementState.KickLight;
                ApplyGravityOnly(Time.deltaTime);
                return true;
            }
            if (cmd.Vault && _controller.isGrounded && TryFindVaultTarget(out Vector3 target))
            {
                _vaultStart = transform.position;
                _vaultTarget = target;
                _vaultEndTime = now + VaultDuration;
                State = MovementState.Vault;
                return true;
            }
            if (cmd.Slide && _controller.isGrounded && _verticalVelocity <= 0f)
            {
                _slideVelocity = transform.forward * SlideEntrySpeed;
                _slideEndTime = now + SlideMinDuration;
                State = MovementState.Slide;
                return true;
            }
            if (cmd.Dodge && now >= _dodgeReadyTime)
            {
                Vector3 direction = new Vector3(cmd.DodgeDirection.x, 0f, cmd.DodgeDirection.y);
                direction = direction.sqrMagnitude > 0.01f ? transform.rotation * direction.normalized : transform.forward;
                _dodgeDirection = direction;
                _dodgeEndTime = now + DodgeDuration;
                _dodgeReadyTime = now + DodgeDuration + DodgeCooldown;
                State = MovementState.Dodge;
                return true;
            }
            if (cmd.Jump && _controller.isGrounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = JumpSpeed;
                State = MovementState.Jump;
            }
            return false;
        }

        void Locomotion(in P1Command cmd, float deltaTime)
        {
            Vector3 move = new Vector3(cmd.Move.x, 0f, cmd.Move.y);
            if (move.sqrMagnitude > 1f) move.Normalize();
            move = transform.rotation * move;

            bool grounded = _controller.isGrounded;
            if (grounded && _verticalVelocity <= 0f) _verticalVelocity = -2f;
            else _verticalVelocity += Gravity * deltaTime;

            float speed = cmd.Sprint && cmd.Move.sqrMagnitude > 0.01f ? SprintSpeed : WalkSpeed;
            Vector3 velocity = move * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * deltaTime);

            if (!grounded) State = MovementState.Jump;
            else if (cmd.Move.sqrMagnitude < 0.01f) State = MovementState.Idle;
            else State = cmd.Sprint ? MovementState.Sprint : MovementState.Walk;
        }

        void ContinueDodge(float deltaTime)
        {
            State = MovementState.Dodge;
            _verticalVelocity = _controller.isGrounded ? -2f : _verticalVelocity + Gravity * deltaTime;
            _controller.Move((_dodgeDirection * DodgeSpeed + Vector3.up * _verticalVelocity) * deltaTime);
        }

        void ContinueSlide(in P1Command cmd, float deltaTime)
        {
            State = MovementState.Slide;
            float speed = _slideVelocity.magnitude;
            speed = Mathf.Max(0f, speed - SlideFriction * deltaTime);
            _slideVelocity = transform.forward * speed;
            bool expired = Time.time >= _slideEndTime + (SlideMaxDuration - SlideMinDuration);
            if (speed < 0.5f || expired) { _slideEndTime = 0f; return; }
            _verticalVelocity = _controller.isGrounded ? -2f : _verticalVelocity + Gravity * deltaTime;
            _controller.Move((_slideVelocity + Vector3.up * _verticalVelocity) * deltaTime);
        }

        void ContinueVault(float now)
        {
            State = MovementState.Vault;
            float t = 1f - Mathf.Clamp01((_vaultEndTime - now) / Mathf.Max(0.01f, VaultDuration));
            Vector3 position = Vector3.Lerp(_vaultStart, _vaultTarget, t);
            _controller.Move(position - transform.position);
        }

        void ApplyGravityOnly(float deltaTime)
        {
            _verticalVelocity = _controller.isGrounded ? -2f : _verticalVelocity + Gravity * deltaTime;
            _controller.Move(Vector3.up * _verticalVelocity * deltaTime);
        }

        bool TryFindVaultTarget(out Vector3 target)
        {
            Vector3 origin = transform.position + Vector3.up * 0.6f;
            if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, VaultReachMeters, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
            {
                target = hit.point + transform.forward * 0.6f + Vector3.up * VaultHeightMeters;
                if (Physics.CheckCapsule(target + Vector3.up * 0.1f, target + Vector3.up * 1.7f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                    target = hit.point + transform.forward * 0.6f;
                return true;
            }
            target = default;
            return false;
        }

        void ApplyKickDamage(float damage)
        {
            Vector3 origin = transform.position + Vector3.up * 1f + transform.forward * 0.3f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.5f, transform.forward, KickRangeMeters, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                HitboxRegion region = hit.collider.GetComponentInParent<HitboxRegion>();
                if (region == null || region.owner == null) continue;
                region.owner.ApplyDamage(damage, region.region);
                return;
            }
        }
    }
}
