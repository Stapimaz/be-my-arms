using Unity.Netcode;

namespace BeMyArms.Networking
{
    /// <summary>P1 role input: locomotion, decoupled look, and the full movement/melee action set.</summary>
    public struct P1Input : INetworkSerializable
    {
        public uint Sequence;
        public uint ControlEpoch;
        public float MoveX;
        public float MoveZ;
        public float LookYawDelta;
        public float LookPitchDelta;
        public bool AlignBody;
        public bool Sprint;
        public bool Jump;
        public bool Dodge;
        public bool Slide;
        public bool LightKick;
        public bool HeavyKick;
        public bool Crouch;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref ControlEpoch);
            serializer.SerializeValue(ref MoveX);
            serializer.SerializeValue(ref MoveZ);
            serializer.SerializeValue(ref LookYawDelta);
            serializer.SerializeValue(ref LookPitchDelta);
            serializer.SerializeValue(ref AlignBody);
            serializer.SerializeValue(ref Sprint);
            serializer.SerializeValue(ref Jump);
            serializer.SerializeValue(ref Dodge);
            serializer.SerializeValue(ref Slide);
            serializer.SerializeValue(ref LightKick);
            serializer.SerializeValue(ref HeavyKick);
            serializer.SerializeValue(ref Crouch);
        }
    }

    /// <summary>P2 role input for Networking: desired world aim (absolute, yaw + pitch) and fire.</summary>
    public struct P2Input : INetworkSerializable
    {
        public uint Sequence;
        public uint ControlEpoch;
        public uint BodyTick;
        public float AimYaw;
        public float AimPitch;
        public bool Fire;
        public bool Reload;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref ControlEpoch);
            serializer.SerializeValue(ref BodyTick);
            serializer.SerializeValue(ref AimYaw);
            serializer.SerializeValue(ref AimPitch);
            serializer.SerializeValue(ref Fire);
            serializer.SerializeValue(ref Reload);
        }
    }

    /// <summary>
    /// Compact authoritative body state used for replication and reconciliation. Includes vertical
    /// position and the active action's timer so a predicted replay is exact (see Reconciler).
    /// </summary>
    public struct BodyState : INetworkSerializable
    {
        public uint ControlEpoch;
        public uint SimulationTick;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float BodyYaw;
        public float LookYaw;
        public float LookPitch;
        public float AimYaw;
        public float AimPitch;
        public float VerticalVelocity;
        public byte MovementState;
        public float ActionTimeLeft;
        public float ActionCooldown;
        public float KickCooldown;
        public float SlideSpeed;
        public float ActionDirX;
        public float ActionDirZ;
        public bool Grounded;
        public int Health;

        // Locomotion presentation signal: the authoritative planar speed this tick (m/s) and the
        // body-local movement direction. Replicated/predicted with the rest of the state so the
        // animation never has to infer speed from frame-to-frame position deltas.
        public float PlanarSpeed;
        public float MoveForward;
        public float MoveRight;

        // Stance (authoritative crouch): a lower hit profile + eye height and slower movement.
        public bool Crouching;
        public float HitHeight;
        public float EyeHeight;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ControlEpoch);
            serializer.SerializeValue(ref SimulationTick);
            serializer.SerializeValue(ref PosX);
            serializer.SerializeValue(ref PosY);
            serializer.SerializeValue(ref PosZ);
            serializer.SerializeValue(ref BodyYaw);
            serializer.SerializeValue(ref LookYaw);
            serializer.SerializeValue(ref LookPitch);
            serializer.SerializeValue(ref AimYaw);
            serializer.SerializeValue(ref AimPitch);
            serializer.SerializeValue(ref VerticalVelocity);
            serializer.SerializeValue(ref MovementState);
            serializer.SerializeValue(ref ActionTimeLeft);
            serializer.SerializeValue(ref ActionCooldown);
            serializer.SerializeValue(ref KickCooldown);
            serializer.SerializeValue(ref SlideSpeed);
            serializer.SerializeValue(ref ActionDirX);
            serializer.SerializeValue(ref ActionDirZ);
            serializer.SerializeValue(ref Grounded);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref PlanarSpeed);
            serializer.SerializeValue(ref MoveForward);
            serializer.SerializeValue(ref MoveRight);
            serializer.SerializeValue(ref Crouching);
            serializer.SerializeValue(ref HitHeight);
            serializer.SerializeValue(ref EyeHeight);
        }
    }
}
