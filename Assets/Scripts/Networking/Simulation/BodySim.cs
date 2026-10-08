using System;

namespace BeMyArms.Networking
{
    /// <summary>
    /// Pure, deterministic shared-body simulation. No UnityEngine types, so it can be stepped
    /// identically on the server, replayed by client prediction, and unit tested.
    ///
    /// Locomotion is 3D (feet position + vertical velocity + grounding) and supports the full Gameplay
    /// action set: walk, unlimited sprint, jump, directional dodge, slide and light/heavy
    /// kicks. Arena collision (<see cref="Collision"/>) resolves bounds, walls, steps and ramps.
    ///
    /// Orientation matches Gameplay: decoupled look with neck limit, smooth body follow, explicit align;
    /// WASD is camera/look-relative (LookYaw) while BodyYaw is the fighter's facing, and P2's firing
    /// sector uses BodyYaw.
    ///
    /// Every bit of mutable state that affects motion lives in <see cref="State"/> so
    /// <see cref="Reconciler"/> can rewind and replay exactly.
    /// </summary>
    public class BodySim
    {
        public float WalkSpeed = 4.5f;
        public float SprintSpeed = 7f;
        public float CrouchSpeed = 2.5f;
        public const float DefaultGravity = -30f;
        public const float DefaultJumpSpeed = 7.5f;
        public float Gravity = DefaultGravity;
        public float JumpSpeed = DefaultJumpSpeed;
        public float FallingGravityMultiplier = 1.35f;

        // Stance dimensions (metres): standing vs crouched hit profile and eye/view height.
        public float StandHeight = 1.8f;
        public float CrouchHeight = 1.15f;
        public float StandEyeHeight = 1.45f;
        public float CrouchEyeHeight = 1.05f;

        public float DodgeSpeed = 11f;
        public float DodgeDuration = 0.22f;
        public float DodgeCooldown = 0.9f;

        public float SlideEntrySpeed = 9f;
        public float SlideFriction = 6f;
        public float SlideMinDuration = 0.35f;
        public float SlideMaxDuration = 1.1f;

        public float LightKickDuration = 0.35f;
        public float LightKickCooldown = 0.5f;
        public float HeavyKickDuration = 0.7f;
        public float HeavyKickCooldown = 1.6f;
        public float KickRangeMeters = 2.4f;

        public float EyeHeight = 1.5f;

        public const float DefaultNeckYawLimitDegrees = 45f;
        public const float DefaultBodyFollowThresholdDegrees = 25f;
        public const float DefaultBodyFollowSpeedDegreesPerSecond = 180f;
        public float NeckYawLimitDegrees = DefaultNeckYawLimitDegrees;
        public float BodyFollowThresholdDegrees = DefaultBodyFollowThresholdDegrees;
        public float BodyFollowSpeedDegreesPerSecond = DefaultBodyFollowSpeedDegreesPerSecond;
        public float BodyAlignSpeedDegreesPerSecond = 540f;
        public float SectorHalfDegrees = 70f;
        public float SectorOvertravelDegrees;
        public float AimSectorHalfDegrees => SectorHalfDegrees + SectorOvertravelDegrees;
        public float MaxPitchDegrees = 80f;
        public int MaxHealth = 100;

        /// <summary>Optional deterministic arena collision; when null the world is a flat plane at y=0.</summary>
        public MovementCollision Collision;

        public BodyState State;

        public void Initialize(float yaw, float posX = 0f, float posZ = 0f, float posY = 0f)
        {
            State = default;
            State.PosX = posX;
            State.PosY = posY;
            State.PosZ = posZ;
            State.BodyYaw = Normalize(yaw);
            State.LookYaw = State.BodyYaw;
            State.AimYaw = State.BodyYaw;
            State.AimPitch = 0f;
            State.Health = MaxHealth;
            State.Grounded = true;
            State.MovementState = (byte)BodyMovementState.Idle;
            State.HitHeight = StandHeight;
            State.EyeHeight = StandEyeHeight;
        }

        public void ApplyP1(in P1Input input, float deltaTime)
        {
            float x = State.PosX, z = State.PosZ;
            StepP1(in input, deltaTime);
            float dx = State.PosX - x, dz = State.PosZ - z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            State.PlanarSpeed = distance / Math.Max(.0001f, deltaTime);
            BodyForward(out float fx, out float fz);
            State.MoveForward = distance > .00001f ? (dx * fx + dz * fz) / distance : 0f;
            State.MoveRight = distance > .00001f ? (dx * fz - dz * fx) / distance : 0f;
        }

        void StepP1(in P1Input input, float deltaTime)
        {
            // ApplyP1 measures collision-resolved displacement after this step, including actions.
            State.PlanarSpeed = 0f;
            State.MoveForward = 0f;
            State.MoveRight = 0f;
            if (State.Health <= 0) return;

            // ---- Look (decoupled from BodyYaw) ----
            State.LookYaw = Normalize(State.LookYaw + input.LookYawDelta);
            State.LookPitch = Clamp(State.LookPitch + input.LookPitchDelta, -MaxPitchDegrees, MaxPitchDegrees);

            float offset = Normalize(State.LookYaw - State.BodyYaw);
            if (offset > NeckYawLimitDegrees) offset = NeckYawLimitDegrees;
            else if (offset < -NeckYawLimitDegrees) offset = -NeckYawLimitDegrees;
            State.LookYaw = Normalize(State.BodyYaw + offset);

            // ---- Body follow / explicit align ----
            if (input.AlignBody)
                State.BodyYaw = MoveTowardsAngle(State.BodyYaw, State.LookYaw, BodyAlignSpeedDegreesPerSecond * deltaTime);
            else if (Math.Abs(offset) > BodyFollowThresholdDegrees)
                State.BodyYaw = MoveTowardsAngle(State.BodyYaw, State.LookYaw, BodyFollowSpeedDegreesPerSecond * deltaTime);
            // Model C also applies when P1 turns without a new P2 packet.
            State.AimYaw = ClampToSector(State.AimYaw, State.BodyYaw, AimSectorHalfDegrees);

            // ---- Timers ----
            if (State.ActionTimeLeft > 0f) State.ActionTimeLeft = Math.Max(0f, State.ActionTimeLeft - deltaTime);
            if (State.ActionCooldown > 0f) State.ActionCooldown = Math.Max(0f, State.ActionCooldown - deltaTime);
            if (State.KickCooldown > 0f) State.KickCooldown = Math.Max(0f, State.KickCooldown - deltaTime);

            BodyMovementState action = (BodyMovementState)State.MovementState;

            UpdateStance(input.Crouch || (action == BodyMovementState.Slide && State.ActionTimeLeft > 0f));

            // ---- Continue an action already in flight ----
            if (State.ActionTimeLeft > 0f)
            {
                switch (action)
                {
                    case BodyMovementState.Dodge:
                        ContinueDodge(deltaTime);
                        return;
                    case BodyMovementState.Slide:
                        if (ContinueSlide(deltaTime)) return;
                        State.MovementState = (byte)BodyMovementState.Walk; // slide ran out; resume normal movement
                        break;
                    case BodyMovementState.KickLight:
                    case BodyMovementState.KickHeavy:
                        ContinueKick(action, deltaTime);
                        return;
                }
            }

            if (TryStartAction(input)) return;

            // ---- Stance (crouch) ----
            // Standing back up requires headroom; while blocked by a low ceiling the body stays
            // crouched so the transition is physical rather than cosmetic.
            UpdateStance(input.Crouch);

            Locomotion(input, deltaTime);
        }

        void UpdateStance(bool crouch)
        {
            if (crouch)
            {
                State.Crouching = true;
            }
            else if (State.Crouching && (Collision == null || Collision.CanStand(State.PosX, State.PosY, State.PosZ, StandHeight)))
            {
                State.Crouching = false;
            }
            State.HitHeight = State.Crouching ? CrouchHeight : StandHeight;
            State.EyeHeight = State.Crouching ? CrouchEyeHeight : StandEyeHeight;

        }

        /// <summary>Applies P2's desired world aim, clamped to the sector around BodyYaw.</summary>
        public void ApplyP2(in P2Input input)
        {
            State.AimYaw = ClampToSector(input.AimYaw, State.BodyYaw, AimSectorHalfDegrees);
            float pitch = input.AimPitch;
            if (pitch > MaxPitchDegrees) pitch = MaxPitchDegrees;
            else if (pitch < -MaxPitchDegrees) pitch = -MaxPitchDegrees;
            State.AimPitch = pitch;
        }

        public void AddRecoil(float pitchKickDegrees, float yawKickDegrees)
        {
            State.AimPitch = Clamp(State.AimPitch - pitchKickDegrees, -MaxPitchDegrees, MaxPitchDegrees);
            State.AimYaw = ClampToSector(Normalize(State.AimYaw + yawKickDegrees), State.BodyYaw, AimSectorHalfDegrees);
        }

        public bool SectorLegal(float aimYaw) => Math.Abs(Normalize(aimYaw - State.BodyYaw)) <= AimSectorHalfDegrees + 0.001f;

        // ---- Movement internals ----

        bool TryStartAction(in P1Input input)
        {
            if (input.HeavyKick && State.KickCooldown <= 0f)
            {
                LookForward(out State.ActionDirX, out State.ActionDirZ);
                State.ActionTimeLeft = HeavyKickDuration;
                State.KickCooldown = HeavyKickDuration + HeavyKickCooldown;
                State.MovementState = (byte)BodyMovementState.KickHeavy;
                return true;
            }
            if (input.LightKick && State.KickCooldown <= 0f)
            {
                LookForward(out State.ActionDirX, out State.ActionDirZ);
                State.ActionTimeLeft = LightKickDuration;
                State.KickCooldown = LightKickDuration + LightKickCooldown;
                State.MovementState = (byte)BodyMovementState.KickLight;
                return true;
            }
            if (input.Slide && State.Grounded && State.VerticalVelocity <= 0f)
            {
                UpdateStance(true);
                DirectionFromMove(in input, out float fx, out float fz);
                State.ActionDirX = fx;
                State.ActionDirZ = fz;
                State.SlideSpeed = SlideEntrySpeed;
                State.ActionTimeLeft = SlideMaxDuration;
                State.MovementState = (byte)BodyMovementState.Slide;
                return true;
            }
            if (input.Dodge && State.ActionCooldown <= 0f)
            {
                DirectionFromMove(in input, out float dx, out float dz);
                State.ActionDirX = dx;
                State.ActionDirZ = dz;
                State.ActionTimeLeft = DodgeDuration;
                State.ActionCooldown = DodgeDuration + DodgeCooldown;
                State.MovementState = (byte)BodyMovementState.Dodge;
                return true;
            }
            if (input.Jump && State.Grounded && State.VerticalVelocity <= 0f)
            {
                State.VerticalVelocity = JumpSpeed;
                State.Grounded = false;
                State.MovementState = (byte)BodyMovementState.Jump;
                return true;
            }
            return false;
        }

        void Locomotion(in P1Input input, float deltaTime)
        {
            float mx = input.MoveX, mz = input.MoveZ;
            float length = (float)Math.Sqrt(mx * mx + mz * mz);
            if (length > 1f) { mx /= length; mz /= length; }

            // WASD is camera/look-relative: forward is the LookYaw direction, right is LookYaw + 90.
            // BodyYaw is the fighter's facing only and keeps its follow/align behaviour.
            LookForward(out float fx, out float fz);
            float rx = fz;
            float rz = -fx;
            float wx = rx * mx + fx * mz;
            float wz = rz * mx + fz * mz;

            float speed = input.Sprint && !State.Crouching && length > 0.01f ? SprintSpeed : (State.Crouching ? CrouchSpeed : WalkSpeed);
            float oldX = State.PosX, oldZ = State.PosZ;
            State.PosX += wx * speed * deltaTime;
            State.PosZ += wz * speed * deltaTime;
            Collision?.ResolveHorizontal(ref State);
            ApplyVertical(deltaTime);

            // Locomotion presentation: project the ACTUAL world movement back into the BodyYaw basis
            // so the directional blend reflects motion relative to the fighter (LookYaw and BodyYaw
            // can differ). This is not the raw WASD vector.
            float actualX = State.PosX - oldX, actualZ = State.PosZ - oldZ;
            float actualDistance = (float)Math.Sqrt(actualX * actualX + actualZ * actualZ);
            State.PlanarSpeed = actualDistance / Math.Max(0.0001f, deltaTime);
            if (actualDistance > 0.00001f)
            {
                float inv = 1f / actualDistance;
                float nwx = actualX * inv, nwz = actualZ * inv;
                BodyForward(out float bfx, out float bfz);
                State.MoveForward = nwx * bfx + nwz * bfz;
                State.MoveRight = nwx * bfz - nwz * bfx; // dot(n, right=(bfz,-bfx))
            }
            else
            {
                State.MoveForward = 0f;
                State.MoveRight = 0f;
            }

            if (!State.Grounded) State.MovementState = (byte)BodyMovementState.Fall;
            else if (length < 0.01f) State.MovementState = (byte)BodyMovementState.Idle;
            else State.MovementState = (byte)(input.Sprint && !State.Crouching ? BodyMovementState.Sprint : BodyMovementState.Walk);
        }

        void ContinueDodge(float deltaTime)
        {
            State.MovementState = (byte)BodyMovementState.Dodge;
            State.PosX += State.ActionDirX * DodgeSpeed * deltaTime;
            State.PosZ += State.ActionDirZ * DodgeSpeed * deltaTime;
            Collision?.ResolveHorizontal(ref State);
            ApplyVertical(deltaTime);
        }

        /// <summary>Returns false once the slide is spent (the caller resumes normal locomotion).</summary>
        bool ContinueSlide(float deltaTime)
        {
            State.MovementState = (byte)BodyMovementState.Slide;

            float elapsed = SlideMaxDuration - State.ActionTimeLeft;
            State.SlideSpeed = Math.Max(0f, State.SlideSpeed - SlideFriction * deltaTime);
            if ((State.SlideSpeed < 0.5f && elapsed >= SlideMinDuration) || State.ActionTimeLeft <= 0f)
                return false;

            State.PosX += State.ActionDirX * State.SlideSpeed * deltaTime;
            State.PosZ += State.ActionDirZ * State.SlideSpeed * deltaTime;
            Collision?.ResolveHorizontal(ref State);
            ApplyVertical(deltaTime);
            return true;
        }

        void ContinueKick(BodyMovementState action, float deltaTime)
        {
            State.MovementState = (byte)action;
            ApplyVertical(deltaTime);
        }

        /// <summary>Gravity + grounding + step/slope following. Collision surfaces below the head.</summary>
        void ApplyVertical(float deltaTime)
        {
            float maxSurfaceY = State.PosY + (Collision != null ? Collision.StepHeight : 0f) + 0.05f;
            float ground = Collision != null ? Collision.SurfaceHeight(State.PosX, State.PosZ, maxSurfaceY) : 0f;

            if (State.Grounded && State.VerticalVelocity <= 0f)
            {
                float diff = ground - State.PosY;
                float step = Collision != null ? Collision.StepHeight + 0.05f : 0.05f;
                if (diff <= step && diff >= -0.6f)
                {
                    State.PosY = ground;
                    State.VerticalVelocity = 0f;
                    return;
                }
            }

            State.VerticalVelocity += Gravity * (State.VerticalVelocity < 0f ? FallingGravityMultiplier : 1f) * deltaTime;
            State.PosY += State.VerticalVelocity * deltaTime;

            if (State.PosY <= ground && State.VerticalVelocity <= 0f)
            {
                State.PosY = ground;
                State.VerticalVelocity = 0f;
                State.Grounded = true;
            }
            else
            {
                State.Grounded = false;
            }
        }

        void BodyForward(out float fx, out float fz)
        {
            float rad = State.BodyYaw * (float)Math.PI / 180f;
            fx = (float)Math.Sin(rad);
            fz = (float)Math.Cos(rad);
        }

        void LookForward(out float fx, out float fz)
        {
            float rad = State.LookYaw * (float)Math.PI / 180f;
            fx = (float)Math.Sin(rad);
            fz = (float)Math.Cos(rad);
        }

        /// <summary>World direction of a directional action, in the same camera/look-relative input
        /// space as locomotion (LookYaw), so dodges/actions agree with WASD.</summary>
        void DirectionFromMove(in P1Input input, out float dx, out float dz)
        {
            float mx = input.MoveX, mz = input.MoveZ;
            float length = (float)Math.Sqrt(mx * mx + mz * mz);
            LookForward(out float fx, out float fz);
            if (length < 0.01f)
            {
                dx = fx;
                dz = fz;
                return;
            }

            float rx = fz;
            float rz = -fx;
            dx = (rx * mx + fx * mz) / length;
            dz = (rz * mx + fz * mz) / length;
        }

        // ---- Static math ----

        public static float Normalize(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            else if (degrees <= -180f) degrees += 360f;
            return degrees;
        }

        public static float ClampToSector(float desiredYaw, float bodyYaw, float halfAngle)
        {
            float offset = Normalize(desiredYaw - bodyYaw);
            if (offset > halfAngle) offset = halfAngle;
            else if (offset < -halfAngle) offset = -halfAngle;
            return Normalize(bodyYaw + offset);
        }

        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = Normalize(target - current);
            if (Math.Abs(delta) <= maxDelta) return Normalize(target);
            return Normalize(current + Math.Sign(delta) * maxDelta);
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
