using Unity.Netcode;

namespace BeMyArms.M2
{
    /// <summary>P1 role input: locomotion, decoupled look, and the full movement/melee action set.</summary>
    public struct M2P1Input : INetworkSerializable
    {
        public uint Sequence;
        public float MoveX;
        public float MoveZ;
        public float LookYawDelta;
        public float LookPitchDelta;
        public bool AlignBody;
        public bool Sprint;
        public bool Jump;
        public bool Dodge;
        public bool Slide;
        public bool Vault;
        public bool LightKick;
        public bool HeavyKick;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref MoveX);
            serializer.SerializeValue(ref MoveZ);
            serializer.SerializeValue(ref LookYawDelta);
            serializer.SerializeValue(ref LookPitchDelta);
            serializer.SerializeValue(ref AlignBody);
            serializer.SerializeValue(ref Sprint);
            serializer.SerializeValue(ref Jump);
            serializer.SerializeValue(ref Dodge);
            serializer.SerializeValue(ref Slide);
            serializer.SerializeValue(ref Vault);
            serializer.SerializeValue(ref LightKick);
            serializer.SerializeValue(ref HeavyKick);
        }
    }

    /// <summary>P2 role input for M2: desired world aim (absolute, yaw + pitch) and fire.</summary>
    public struct M2P2Input : INetworkSerializable
    {
        public uint Sequence;
        public float AimYaw;
        public float AimPitch;
        public bool Fire;
        public bool Reload;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref AimYaw);
            serializer.SerializeValue(ref AimPitch);
            serializer.SerializeValue(ref Fire);
            serializer.SerializeValue(ref Reload);
        }
    }

    /// <summary>
    /// Compact authoritative body state used for replication and reconciliation. Includes vertical
    /// position and the active action's timer so a predicted replay is exact (see M2Reconciler).
    /// </summary>
    public struct M2BodyState : INetworkSerializable
    {
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

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
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
        }
    }
}
