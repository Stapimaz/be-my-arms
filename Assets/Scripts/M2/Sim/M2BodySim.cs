using System;

namespace BeMyArms.M2
{
    /// <summary>
    /// Pure, deterministic shared-body simulation. No UnityEngine types, so it can be stepped
    /// identically on the server, replayed by client prediction, and unit tested.
    ///
    /// Locomotion is 3D (feet position + vertical velocity + grounding) and supports the full M1
    /// action set: walk, unlimited sprint, jump, directional dodge, slide, vault and light/heavy
    /// kicks. Arena collision (<see cref="Collision"/>) resolves bounds, walls, steps and ramps.
    ///
    /// Orientation matches M1: decoupled look with neck limit, smooth body follow, explicit align;
    /// movement and P2's firing sector use BodyYaw only.
    ///
    /// Every bit of mutable state that affects motion lives in <see cref="State"/> so
    /// <see cref="M2Reconciler"/> can rewind and replay exactly.
    /// </summary>
    public class M2BodySim
    {
        public float WalkSpeed = 4.5f;
        public float SprintSpeed = 7f;
        public float Gravity = -20f;
        public float JumpSpeed = 7f;

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
        public float HeavyKickDuration = 0.7f;
        public float HeavyKickCooldown = 1.6f;
        public float KickRangeMeters = 2.4f;

        public float EyeHeight = 1.5f;

        public float NeckYawLimitDegrees = 80f;
        public float BodyFollowThresholdDegrees = 50f;
        public float BodyFollowSpeedDegreesPerSecond = 120f;
        public float BodyAlignSpeedDegreesPerSecond = 540f;
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;
        public int MaxHealth = 100;

        /// <summary>Optional deterministic arena collision; when null the world is a flat plane at y=0.</summary>
        public M2MovementCollision Collision;

        public M2BodyState State;

        // Vault endpoints are only needed while a vault is in flight and are recomputed at vault
        // start. The server is authoritative and never replays; a client's replay can resync from
        // the replicated state on the next snapshot.
        float _vaultStartX, _vaultStartY, _vaultStartZ;
        float _vaultTargetX, _vaultTargetY, _vaultTargetZ;

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
            State.MovementState = (byte)M2MovementState.Idle;
        }

        public void ApplyP1(in M2P1Input input, float deltaTime)
        {
            // Presentation signal defaults to stationary; Locomotion sets it while moving. Actions
            // (dodge/slide/vault/kick) and death therefore read as stationary to the animator.
            State.PlanarSpeed = 0f;
            State.MoveForward = 0f;

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

            // ---- Timers ----
            if (State.ActionTimeLeft > 0f) State.ActionTimeLeft = Math.Max(0f, State.ActionTimeLeft - deltaTime);
            if (State.ActionCooldown > 0f) State.ActionCooldown = Math.Max(0f, State.ActionCooldown - deltaTime);
            if (State.KickCooldown > 0f) State.KickCooldown = Math.Max(0f, State.KickCooldown - deltaTime);

            M2MovementState action = (M2MovementState)State.MovementState;

            // ---- Continue an action already in flight ----
            if (State.ActionTimeLeft > 0f)
            {
                switch (action)
                {
                    case M2MovementState.Dodge:
                        ContinueDodge(deltaTime);
                        return;
                    case M2MovementState.Vault:
                        ContinueVault();
                        return;
                    case M2MovementState.Slide:
                        if (ContinueSlide(deltaTime)) return;
                        State.MovementState = (byte)M2MovementState.Walk; // slide ran out; resume normal movement
                        break;
                    case M2MovementState.KickLight:
                    case M2MovementState.KickHeavy:
                        ContinueKick(action, deltaTime);
                        return;
                }
            }

            if (TryStartAction(input)) return;

            Locomotion(input, deltaTime);
        }

        /// <summary>Applies P2's desired world aim, clamped to the sector around BodyYaw.</summary>
        public void ApplyP2(in M2P2Input input)
        {
            State.AimYaw = ClampToSector(input.AimYaw, State.BodyYaw, SectorHalfDegrees);
            float pitch = input.AimPitch;
            if (pitch > MaxPitchDegrees) pitch = MaxPitchDegrees;
            else if (pitch < -MaxPitchDegrees) pitch = -MaxPitchDegrees;
            State.AimPitch = pitch;
        }

        public void AddRecoil(float pitchKickDegrees, float yawKickDegrees)
        {
            State.AimPitch = Clamp(State.AimPitch - pitchKickDegrees, -MaxPitchDegrees, MaxPitchDegrees);
            State.AimYaw = ClampToSector(Normalize(State.AimYaw + yawKickDegrees), State.BodyYaw, SectorHalfDegrees);
        }

        public bool SectorLegal(float aimYaw) => Math.Abs(Normalize(aimYaw - State.BodyYaw)) <= SectorHalfDegrees + 0.001f;

        // ---- Movement internals ----

        bool TryStartAction(in M2P1Input input)
        {
            if (input.HeavyKick && State.KickCooldown <= 0f)
            {
                State.ActionTimeLeft = HeavyKickDuration;
                State.KickCooldown = HeavyKickDuration + HeavyKickCooldown;
                State.MovementState = (byte)M2MovementState.KickHeavy;
                return true;
            }
            if (input.LightKick && State.KickCooldown <= 0f)
            {
                State.ActionTimeLeft = LightKickDuration;
                State.KickCooldown = LightKickDuration + LightKickCooldown;
                State.MovementState = (byte)M2MovementState.KickLight;
                return true;
            }
            if (input.Vault && State.Grounded && Collision != null)
            {
                BodyForward(out float fx, out float fz);
                if (Collision.TryFindVault(State.PosX, State.PosY, State.PosZ, fx, fz,
                        VaultReachMeters, VaultHeightMeters, out float tx, out float ty, out float tz))
                {
                    _vaultStartX = State.PosX; _vaultStartY = State.PosY; _vaultStartZ = State.PosZ;
                    _vaultTargetX = tx; _vaultTargetY = ty; _vaultTargetZ = tz;
                    State.ActionTimeLeft = VaultDuration;
                    State.MovementState = (byte)M2MovementState.Vault;
                    State.Grounded = false;
                    return true;
                }
            }
            if (input.Slide && State.Grounded && State.VerticalVelocity <= 0f)
            {
                BodyForward(out float fx, out float fz);
                State.ActionDirX = fx;
                State.ActionDirZ = fz;
                State.SlideSpeed = SlideEntrySpeed;
                State.ActionTimeLeft = SlideMaxDuration;
                State.MovementState = (byte)M2MovementState.Slide;
                return true;
            }
            if (input.Dodge && State.ActionCooldown <= 0f)
            {
                DirectionFromMove(in input, out float dx, out float dz);
                State.ActionDirX = dx;
                State.ActionDirZ = dz;
                State.ActionTimeLeft = DodgeDuration;
                State.ActionCooldown = DodgeDuration + DodgeCooldown;
                State.MovementState = (byte)M2MovementState.Dodge;
                return true;
            }
            if (input.Jump && State.Grounded && State.VerticalVelocity <= 0f)
            {
                State.VerticalVelocity = JumpSpeed;
                State.Grounded = false;
                State.MovementState = (byte)M2MovementState.Jump;
                return true;
            }
            return false;
        }

        void Locomotion(in M2P1Input input, float deltaTime)
        {
            float mx = input.MoveX, mz = input.MoveZ;
            float length = (float)Math.Sqrt(mx * mx + mz * mz);
            if (length > 1f) { mx /= length; mz /= length; }

            BodyForward(out float fx, out float fz);
            float rx = fz;
            float rz = -fx;
            float wx = rx * mx + fx * mz;
            float wz = rz * mx + fz * mz;

            float speed = input.Sprint && length > 0.01f ? SprintSpeed : WalkSpeed;
            State.PosX += wx * speed * deltaTime;
            State.PosZ += wz * speed * deltaTime;
            Collision?.ResolveHorizontal(ref State);
            ApplyVertical(deltaTime);

            // Stable locomotion presentation signal (never derived from frame-to-frame deltas). The
            // applied planar speed is input magnitude * move speed; MoveForward is the body-local
            // forward component so backward movement can play the cycle in reverse.
            State.PlanarSpeed = length > 0.01f ? length * speed : 0f;
            State.MoveForward = length > 0.01f ? mz : 0f;

            if (!State.Grounded) State.MovementState = (byte)M2MovementState.Fall;
            else if (length < 0.01f) State.MovementState = (byte)M2MovementState.Idle;
            else State.MovementState = (byte)(input.Sprint ? M2MovementState.Sprint : M2MovementState.Walk);
        }

        void ContinueDodge(float deltaTime)
        {
            State.MovementState = (byte)M2MovementState.Dodge;
            State.PosX += State.ActionDirX * DodgeSpeed * deltaTime;
            State.PosZ += State.ActionDirZ * DodgeSpeed * deltaTime;
            Collision?.ResolveHorizontal(ref State);
            ApplyVertical(deltaTime);
        }

        /// <summary>Returns false once the slide is spent (the caller resumes normal locomotion).</summary>
        bool ContinueSlide(float deltaTime)
        {
            State.MovementState = (byte)M2MovementState.Slide;

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

        void ContinueVault()
        {
            State.MovementState = (byte)M2MovementState.Vault;
            float t = 1f - Clamp(State.ActionTimeLeft / Math.Max(0.01f, VaultDuration), 0f, 1f);
            State.PosX = _vaultStartX + (_vaultTargetX - _vaultStartX) * t;
            State.PosY = _vaultStartY + (_vaultTargetY - _vaultStartY) * t;
            State.PosZ = _vaultStartZ + (_vaultTargetZ - _vaultStartZ) * t;
            State.VerticalVelocity = 0f;
            State.Grounded = false;
        }

        void ContinueKick(M2MovementState action, float deltaTime)
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

            State.VerticalVelocity += Gravity * deltaTime;
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

        void DirectionFromMove(in M2P1Input input, out float dx, out float dz)
        {
            float mx = input.MoveX, mz = input.MoveZ;
            float length = (float)Math.Sqrt(mx * mx + mz * mz);
            BodyForward(out float fx, out float fz);
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
