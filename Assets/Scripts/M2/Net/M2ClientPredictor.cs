using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M2
{
    /// <summary>
    /// Client prediction and reconciliation for the P1-owned portion of the M2 body, plus local P2
    /// aim. Runs only on clients. Predicts locally from inputs every tick, and on each authoritative
    /// snapshot reconciles by replaying unacknowledged inputs.
    /// </summary>
    public class M2ClientPredictor : MonoBehaviour
    {
        public M2NetworkBody Body;
        public Transform Presentation;

        [Header("Sim tuning (must match the server body)")]
        public float MoveSpeed = 5f;
        public float NeckYawLimitDegrees = 80f;
        public float BodyFollowThresholdDegrees = 50f;
        public float BodyFollowSpeedDegreesPerSecond = 120f;
        public float BodyAlignSpeedDegreesPerSecond = 540f;
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;

        [Header("Input")]
        public bool AutoDrive = true;
        public float SendRateHz = 60f;

        M2BodySim _predictSim;
        M2Reconciler _reconciler;
        uint _p1Sequence;
        uint _p2Sequence;
        float _nextSendTime;
        float _autoClock;
        M2BodyState _lastServerState;
        uint _lastAcked;

        public M2BodyState Predicted => _reconciler != null ? _reconciler.Predicted : default;
        public float LocalAimYaw { get; private set; }
        public float LocalAimPitch { get; private set; }

        void Start()
        {
            _predictSim = new M2BodySim
            {
                MoveSpeed = MoveSpeed,
                NeckYawLimitDegrees = NeckYawLimitDegrees,
                BodyFollowThresholdDegrees = BodyFollowThresholdDegrees,
                BodyFollowSpeedDegreesPerSecond = BodyFollowSpeedDegreesPerSecond,
                BodyAlignSpeedDegreesPerSecond = BodyAlignSpeedDegreesPerSecond,
                SectorHalfDegrees = SectorHalfDegrees,
                MaxPitchDegrees = MaxPitchDegrees
            };
            _reconciler = new M2Reconciler();
            _predictSim.Initialize(0f);
            _reconciler.Reset(_predictSim.State);
        }

        void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsClient || Body == null || _reconciler == null) return;

            // Reconcile whenever the server reports a newer authoritative state / ack.
            M2BodyState serverState = Body.State.Value;
            uint acked = Body.LastAckedP1Sequence.Value;
            if (acked != _lastAcked || !SameState(serverState, _lastServerState))
            {
                _lastAcked = acked;
                _lastServerState = serverState;
                _reconciler.Reconcile(serverState, acked, _predictSim);
            }

            if (Time.time < _nextSendTime) { ApplyPresentation(); return; }
            _nextSendTime = Time.time + 1f / Mathf.Max(1f, SendRateHz);
            float deltaTime = 1f / Mathf.Max(1f, SendRateHz);

            // P1: predict locally and send.
            M2P1Input p1 = BuildP1(deltaTime);
            p1.Sequence = ++_p1Sequence;
            _reconciler.Predict(p1, deltaTime, _predictSim);
            Body.SubmitP1ServerRpc(p1);

            // P2: aim is local; clamp only for presentation. The server validates for real.
            M2P2Input p2 = BuildP2();
            p2.Sequence = ++_p2Sequence;
            LocalAimYaw = M2BodySim.ClampToSector(p2.AimYaw, _reconciler.Predicted.BodyYaw, SectorHalfDegrees);
            LocalAimPitch = Mathf.Clamp(p2.AimPitch, -MaxPitchDegrees, MaxPitchDegrees);
            Body.SubmitP2ServerRpc(p2);

            ApplyPresentation();
        }

        M2P1Input BuildP1(float deltaTime)
        {
            var input = new M2P1Input();
            if (AutoDrive)
            {
                _autoClock += deltaTime;
                float cycle = _autoClock % 8f;
                input.MoveZ = 1f;
                input.MoveX = cycle > 3f ? 0.4f : 0f;
                input.LookYawDelta = 20f * deltaTime;
                input.AlignBody = cycle > 6f && cycle < 6.05f;
            }
            return input;
        }

        M2P2Input BuildP2()
        {
            var input = new M2P2Input();
            if (AutoDrive)
            {
                // Aim straight ahead of the body (legal) and fire.
                input.AimYaw = _reconciler.Predicted.BodyYaw;
                input.AimPitch = 0f;
                input.Fire = true;
            }
            return input;
        }

        void ApplyPresentation()
        {
            if (Presentation == null) return;
            M2BodyState s = _reconciler.Predicted;
            Presentation.position = new Vector3(s.PosX, Presentation.position.y, s.PosZ);
            Presentation.rotation = Quaternion.Euler(0f, s.BodyYaw, 0f);
        }

        static bool SameState(in M2BodyState a, in M2BodyState b)
        {
            return Mathf.Abs(a.PosX - b.PosX) < 1e-5f
                && Mathf.Abs(a.PosZ - b.PosZ) < 1e-5f
                && Mathf.Abs(a.BodyYaw - b.BodyYaw) < 1e-4f;
        }
    }
}
