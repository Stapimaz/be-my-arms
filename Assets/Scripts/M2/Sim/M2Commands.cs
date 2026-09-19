using Unity.Netcode;

namespace BeMyArms.M2
{
    /// <summary>P1 role input for M2: locomotion, decoupled look, explicit align.</summary>
    public struct M2P1Input : INetworkSerializable
    {
        public uint Sequence;
        public float MoveX;
        public float MoveZ;
        public float LookYawDelta;
        public bool AlignBody;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref MoveX);
            serializer.SerializeValue(ref MoveZ);
            serializer.SerializeValue(ref LookYawDelta);
            serializer.SerializeValue(ref AlignBody);
        }
    }

    /// <summary>P2 role input for M2: desired world aim (absolute) and fire.</summary>
    public struct M2P2Input : INetworkSerializable
    {
        public uint Sequence;
        public float AimYaw;
        public float AimPitch;
        public bool Fire;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref AimYaw);
            serializer.SerializeValue(ref AimPitch);
            serializer.SerializeValue(ref Fire);
        }
    }

    /// <summary>Compact authoritative body state used for replication and reconciliation.</summary>
    public struct M2BodyState : INetworkSerializable
    {
        public float PosX;
        public float PosZ;
        public float BodyYaw;
        public float LookYaw;
        public float AimYaw;
        public float AimPitch;
        public int Health;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PosX);
            serializer.SerializeValue(ref PosZ);
            serializer.SerializeValue(ref BodyYaw);
            serializer.SerializeValue(ref LookYaw);
            serializer.SerializeValue(ref AimYaw);
            serializer.SerializeValue(ref AimPitch);
            serializer.SerializeValue(ref Health);
        }
    }
}
