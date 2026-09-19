using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M2
{
    /// <summary>
    /// Server-authoritative shared body for M2. Both roles send their own input domain over separate
    /// role-tagged RPCs; the server merges them into one body. The authoritative state and the last
    /// acknowledged P1 input sequence are replicated so clients can predict and reconcile.
    /// </summary>
    public class M2NetworkBody : NetworkBehaviour
    {
        [Header("Sim tuning (mirrors M1)")]
        public float MoveSpeed = 5f;
        public float NeckYawLimitDegrees = 80f;
        public float BodyFollowThresholdDegrees = 50f;
        public float BodyFollowSpeedDegreesPerSecond = 120f;
        public float BodyAlignSpeedDegreesPerSecond = 540f;
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;

        public NetworkVariable<M2BodyState> State = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Last P1 input sequence the server has applied (for client reconciliation).</summary>
        public NetworkVariable<uint> LastAckedP1Sequence = new(
            0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<uint> LastAckedP2Sequence = new(
            0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        M2BodySim _sim;
        M2P1Input _pendingP1;
        M2P2Input _pendingP2;
        bool _hasP1;
        bool _hasP2;

        public override void OnNetworkSpawn()
        {
            _sim = new M2BodySim
            {
                MoveSpeed = MoveSpeed,
                NeckYawLimitDegrees = NeckYawLimitDegrees,
                BodyFollowThresholdDegrees = BodyFollowThresholdDegrees,
                BodyFollowSpeedDegreesPerSecond = BodyFollowSpeedDegreesPerSecond,
                BodyAlignSpeedDegreesPerSecond = BodyAlignSpeedDegreesPerSecond,
                SectorHalfDegrees = SectorHalfDegrees,
                MaxPitchDegrees = MaxPitchDegrees
            };
            _sim.Initialize(transform.eulerAngles.y);

            if (IsServer)
            {
                State.Value = _sim.State;
                ReconcilePresentation();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitP1ServerRpc(M2P1Input input)
        {
            _pendingP1 = input;
            _hasP1 = true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitP2ServerRpc(M2P2Input input)
        {
            _pendingP2 = input;
            _hasP2 = true;
        }

        void Update()
        {
            if (!IsServer || _sim == null) return;

            float deltaTime = Time.deltaTime;

            if (_hasP1)
            {
                _sim.ApplyP1(_pendingP1, deltaTime);
                LastAckedP1Sequence.Value = _pendingP1.Sequence;
                _hasP1 = false;
            }

            if (_hasP2)
            {
                // Only apply fire when it is legal for the current body orientation.
                if (!_pendingP2.Fire || _sim.SectorLegal(_pendingP2.AimYaw))
                    _sim.ApplyP2(_pendingP2);
                LastAckedP2Sequence.Value = _pendingP2.Sequence;
                _hasP2 = false;
            }

            State.Value = _sim.State;
            ReconcilePresentation();
        }

        /// <summary>Server-side presentation (and a hook for lag compensation later).</summary>
        void ReconcilePresentation()
        {
            transform.position = new Vector3(_sim.State.PosX, transform.position.y, _sim.State.PosZ);
            transform.rotation = Quaternion.Euler(0f, _sim.State.BodyYaw, 0f);
        }

        public bool IsAuthoritativeShooterLegal(float aimYaw) => _sim != null && _sim.SectorLegal(aimYaw);
    }
}
