using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M2
{
    struct M2Snapshot
    {
        public M2BodyState State;
        public uint Ack;
    }

    /// <summary>
    /// Role-aware client for M2.
    ///  - P1 client: predicts and reconciles the P1-owned portion, sends P1 input, presents the
    ///    predicted state with smoothing (no camera snap on correction).
    ///  - P2 client: keeps aim fully local, sends P2 input, presents the local aim.
    /// Snapshots are delay/loss conditioned before being applied, to exercise prediction under
    /// latency in separate processes.
    /// </summary>
    public class M2ClientPredictor : MonoBehaviour
    {
        public M2NetworkBody Body;
        public Transform Presentation;

        [Header("Role (set from bootstrap / args)")]
        public byte Role = M2NetworkBody.RoleP1;
        public string RoleToken = "";
        public byte DesiredRole = M2NetworkBody.RoleNone;

        [Header("Sim tuning (must match the server body)")]
        public float MoveSpeed = 5f;
        public float NeckYawLimitDegrees = 80f;
        public float BodyFollowThresholdDegrees = 50f;
        public float BodyFollowSpeedDegreesPerSecond = 120f;
        public float BodyAlignSpeedDegreesPerSecond = 540f;
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;

        [Header("Conditioned network")]
        public float SnapshotDelaySeconds = 0.05f; // ~100 ms RTT with the server input delay
        public float LossPercent = 2f;

        [Header("Input")]
        public bool AutoDrive = true;
        public float SendRateHz = 60f;
        public float PresentationSmoothing = 18f;

        readonly M2DelayQueue<M2Snapshot> _snapshotQueue = new M2DelayQueue<M2Snapshot>();

        M2BodySim _predictSim;
        M2Reconciler _reconciler;
        uint _p1Sequence;
        uint _p2Sequence;
        float _nextSendTime;
        float _autoClock;
        bool _registered;
        bool _hasServerState;
        M2BodyState _lastServerState;
        uint _lastAcked;

        Vector3 _visualPosition;
        float _visualYaw;

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
            _predictSim.Initialize(0f);
            _reconciler = new M2Reconciler();
            _reconciler.Reset(_predictSim.State);

            Role = M2Config.ClientRole;
            DesiredRole = M2Config.ClientRole;
            RoleToken = M2Config.ClientToken;
            SnapshotDelaySeconds = M2Config.OneWayDelaySeconds;
            LossPercent = M2Config.LossPercent;
            AutoDrive = M2Config.AutoDrive;
            _snapshotQueue.LossPercent = LossPercent;
        }

        void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsClient || Body == null) return;

            if (!_registered)
            {
                Body.RegisterServerRpc(RoleToken, DesiredRole);
                _registered = true;
                Debug.Log($"[M2] registered client with role {(Role == M2NetworkBody.RoleP1 ? "P1" : "P2")} token '{RoleToken}'");
            }

            _snapshotQueue.Enqueue(Time.timeAsDouble, SnapshotDelaySeconds, new M2Snapshot
            {
                State = Body.State.Value,
                Ack = Body.LastAckedP1Sequence.Value
            });

            // Apply delayed snapshots to prediction (P1) only.
            if (Role == M2NetworkBody.RoleP1)
                ApplyDelayedSnapshots();

            if (Time.time < _nextSendTime) { ApplyPresentation(); return; }
            _nextSendTime = Time.time + 1f / Mathf.Max(1f, SendRateHz);
            float deltaTime = 1f / Mathf.Max(1f, SendRateHz);

            if (Role == M2NetworkBody.RoleP1) SendP1(deltaTime);
            else SendP2();

            ApplyPresentation();
        }

        void ApplyDelayedSnapshots()
        {
            while (_snapshotQueue.TryDequeue(Time.timeAsDouble, out M2Snapshot snapshot))
            {
                _lastServerState = snapshot.State;
                _lastAcked = snapshot.Ack;
                _reconciler.Reconcile(snapshot.State, snapshot.Ack, _predictSim);
                _hasServerState = true;
            }
        }

        void SendP1(float deltaTime)
        {
            M2P1Input p1 = BuildP1(deltaTime);
            p1.Sequence = ++_p1Sequence;
            _reconciler.Predict(p1, deltaTime, _predictSim);
            Body.SubmitP1ServerRpc(p1);
        }

        void SendP2()
        {
            M2P2Input p2 = BuildP2();
            p2.Sequence = ++_p2Sequence;
            float bodyYaw = Body.State.Value.BodyYaw;
            p2.AimYaw = M2BodySim.ClampToSector(p2.AimYaw, bodyYaw, SectorHalfDegrees);
            LocalAimYaw = p2.AimYaw;
            LocalAimPitch = Mathf.Clamp(p2.AimPitch, -MaxPitchDegrees, MaxPitchDegrees);
            Body.SubmitP2ServerRpc(p2);
        }

        M2P1Input BuildP1(float deltaTime)
        {
            var input = new M2P1Input();
            if (!AutoDrive) return input;
            _autoClock += deltaTime;
            float cycle = _autoClock % 8f;
            input.MoveZ = 1f;
            input.MoveX = cycle > 3f ? 0.4f : 0f;
            input.LookYawDelta = 6f * deltaTime;
            input.AlignBody = cycle > 6f && cycle < 6.05f;
            return input;
        }

        M2P2Input BuildP2()
        {
            var input = new M2P2Input();
            if (!AutoDrive) return input;

            // Aim toward the replicated target; if it is outside the sector the clamped aim will
            // simply miss, which is the correct shared-body behaviour.
            Vector2 target = Body.TargetPosition.Value;
            M2BodyState state = Body.State.Value;
            float dx = target.x - state.PosX;
            float dz = target.y - state.PosZ;
            input.AimYaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            input.AimPitch = 0f;
            input.Fire = true;
            input.Reload = (Time.time % 6f) < 0.05f;
            return input;
        }

        void ApplyPresentation()
        {
            if (Presentation == null) return;

            M2BodyState s = Role == M2NetworkBody.RoleP1 ? _reconciler.Predicted : Body.State.Value;
            Vector3 targetPos = new Vector3(s.PosX, Presentation.position.y, s.PosZ);
            float targetYaw = Role == M2NetworkBody.RoleP2 ? LocalAimYaw : s.BodyYaw;

            // Smooth correction so reconciliation never snaps the presentation.
            float k = 1f - Mathf.Exp(-PresentationSmoothing * Time.deltaTime);
            _visualPosition = Vector3.Lerp(_visualPosition == Vector3.zero ? targetPos : _visualPosition, targetPos, k);
            _visualYaw = Mathf.LerpAngle(_visualYaw, targetYaw, k);

            Presentation.position = _visualPosition;
            Presentation.rotation = Quaternion.Euler(0f, _visualYaw, 0f);
        }
    }
}
