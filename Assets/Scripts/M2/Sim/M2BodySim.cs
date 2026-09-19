using System;

namespace BeMyArms.M2
{
    /// <summary>
    /// Pure, deterministic shared-body simulation for M2. No UnityEngine types, so it can be
    /// stepped identically on the server, replayed by client prediction, and unit tested.
    /// Orientation model matches M1: decoupled look with neck limit, smooth body follow, and an
    /// explicit align action; movement and P2's sector use BodyYaw only.
    /// </summary>
    public class M2BodySim
    {
        public float MoveSpeed = 5f;
        public float NeckYawLimitDegrees = 80f;
        public float BodyFollowThresholdDegrees = 50f;
        public float BodyFollowSpeedDegreesPerSecond = 120f;
        public float BodyAlignSpeedDegreesPerSecond = 540f;
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;
        public int MaxHealth = 100;

        /// <summary>
        /// Optional authoritative movement constraint (arena bounds/obstacles), invoked after each
        /// P1 step. Kept as a delegate so the pure sim stays engine-free and prediction replays the
        /// same constraint. Server and client set the same deterministic constraint.
        /// </summary>
        public System.Action<M2BodySim> MovementConstraint;

        public M2BodyState State;

        public void Initialize(float yaw, float posX = 0f, float posZ = 0f)
        {
            State.PosX = posX;
            State.PosZ = posZ;
            State.BodyYaw = Normalize(yaw);
            State.LookYaw = State.BodyYaw;
            State.AimYaw = State.BodyYaw;
            State.AimPitch = 0f;
            State.Health = MaxHealth;
        }

        public void ApplyP1(in M2P1Input input, float deltaTime)
        {
            State.LookYaw = Normalize(State.LookYaw + input.LookYawDelta);

            float offset = Normalize(State.LookYaw - State.BodyYaw);
            if (offset > NeckYawLimitDegrees) offset = NeckYawLimitDegrees;
            else if (offset < -NeckYawLimitDegrees) offset = -NeckYawLimitDegrees;
            State.LookYaw = Normalize(State.BodyYaw + offset);

            if (input.AlignBody)
            {
                State.BodyYaw = MoveTowardsAngle(State.BodyYaw, State.LookYaw, BodyAlignSpeedDegreesPerSecond * deltaTime);
            }
            else if (Math.Abs(offset) > BodyFollowThresholdDegrees)
            {
                State.BodyYaw = MoveTowardsAngle(State.BodyYaw, State.LookYaw, BodyFollowSpeedDegreesPerSecond * deltaTime);
            }

            float rad = State.BodyYaw * (float)Math.PI / 180f;
            float forwardX = (float)Math.Sin(rad);
            float forwardZ = (float)Math.Cos(rad);
            float rightX = (float)Math.Cos(rad);
            float rightZ = -(float)Math.Sin(rad);

            float moveX = rightX * input.MoveX + forwardX * input.MoveZ;
            float moveZ = rightZ * input.MoveX + forwardZ * input.MoveZ;
            float length = (float)Math.Sqrt(moveX * moveX + moveZ * moveZ);
            if (length > 1f) { moveX /= length; moveZ /= length; }

            State.PosX += moveX * MoveSpeed * deltaTime;
            State.PosZ += moveZ * MoveSpeed * deltaTime;

            MovementConstraint?.Invoke(this);
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

        public bool SectorLegal(float aimYaw) => Math.Abs(Normalize(aimYaw - State.BodyYaw)) <= SectorHalfDegrees + 0.001f;

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
    }
}
