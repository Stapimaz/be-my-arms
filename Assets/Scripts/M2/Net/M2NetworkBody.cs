using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M2
{
    /// <summary>
    /// Server-authoritative shared body for M2 with:
    ///  - connection-to-role authorization (a P1 connection cannot send P2 input and vice versa),
    ///  - fire validation (cadence, ammo, reload, sector),
    ///  - lag compensation (historical BodyYaw + target position rewind),
    ///  - an application-level latency/loss conditioner,
    ///  - basic server metrics.
    /// </summary>
    public class M2NetworkBody : NetworkBehaviour
    {
        public const byte RoleP1 = 0;
        public const byte RoleP2 = 1;
        public const byte RoleNone = 255;

        [Header("Sim tuning (mirrors M1)")]
        public float MoveSpeed = 5f;
        public float NeckYawLimitDegrees = 80f;
        public float BodyFollowThresholdDegrees = 50f;
        public float BodyFollowSpeedDegreesPerSecond = 120f;
        public float BodyAlignSpeedDegreesPerSecond = 540f;
        public float SectorHalfDegrees = 70f;
        public float MaxPitchDegrees = 80f;

        [Header("Conditioned network")]
        public float InputDelaySeconds = 0.05f;   // one-way; ~100 ms RTT with the client snapshot delay
        public float LossPercent = 2f;
        public float LagRewindSeconds = 0.1f;     // server rewinds this far for fire validation
        public float TargetRadius = 0.6f;
        public float FireRange = 150f;

        public NetworkVariable<M2BodyState> State = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<uint> LastAckedP1Sequence = new(0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<uint> LastAckedP2Sequence = new(0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<Vector2> TargetPosition = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> ValidatedHits = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> RejectedFires = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> UnauthorizedInputs = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly Dictionary<ulong, byte> _connectionRole = new Dictionary<ulong, byte>();
        readonly Dictionary<string, byte> _tokenRole = new Dictionary<string, byte>();
        readonly M2DelayQueue<M2P1Input> _p1Queue = new M2DelayQueue<M2P1Input>();
        readonly M2DelayQueue<M2P2Input> _p2Queue = new M2DelayQueue<M2P2Input>();

        M2BodySim _sim;
        M2WeaponState _weapon;
        M2LagCompensation _lag;
        System.Diagnostics.Stopwatch _tickWatch;
        float _metricAccum;
        int _metricTicks;
        double _serverTime;
        float _targetPhase;

        public override void OnNetworkSpawn()
        {
            InputDelaySeconds = M2Config.OneWayDelaySeconds;
            LossPercent = M2Config.LossPercent;
            LagRewindSeconds = M2Config.LagRewindSeconds;

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
            _weapon = new M2WeaponState();
            _lag = new M2LagCompensation { MaxRewindSeconds = Mathf.Max(0.2f, LagRewindSeconds * 2f) };
            _p1Queue.LossPercent = LossPercent;
            _p2Queue.LossPercent = LossPercent;
            _tickWatch = new System.Diagnostics.Stopwatch();

            if (IsServer)
            {
                State.Value = _sim.State;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        /// <summary>Client announces its identity; server binds the connection to a role.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void RegisterServerRpc(string token, byte desiredRole, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (!string.IsNullOrEmpty(token) && _tokenRole.TryGetValue(token, out byte remembered))
            {
                _connectionRole[clientId] = remembered;
                Debug.Log($"[M2] reconnect: client {clientId} restored {(remembered == RoleP1 ? "P1" : "P2")} (token '{token}')");
            }
            else
            {
                byte role = FindFreeRole();
                if (desiredRole == RoleP1 || desiredRole == RoleP2)
                {
                    if (!RoleTaken(desiredRole)) role = desiredRole;
                }
                _connectionRole[clientId] = role;
                if (!string.IsNullOrEmpty(token)) _tokenRole[token] = role;
                Debug.Log($"[M2] assigned client {clientId} -> {(role == RoleP1 ? "P1" : role == RoleP2 ? "P2" : "spectator")}");
            }
        }

        byte FindFreeRole()
        {
            if (!RoleTaken(RoleP1)) return RoleP1;
            if (!RoleTaken(RoleP2)) return RoleP2;
            return RoleNone;
        }

        bool RoleTaken(byte role)
        {
            foreach (var kvp in _connectionRole)
                if (kvp.Value == role) return true;
            return false;
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (_connectionRole.TryGetValue(clientId, out byte role))
            {
                _connectionRole.Remove(clientId);
                Debug.Log($"[M2] client {clientId} disconnected from role {(role == RoleP1 ? "P1" : "P2")} (role freed)");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitP1ServerRpc(M2P1Input input, ServerRpcParams rpcParams = default)
        {
            if (!HasRole(rpcParams.Receive.SenderClientId, RoleP1)) { UnauthorizedInputs.Value++; return; }
            _p1Queue.Enqueue(_serverTime, InputDelaySeconds, input);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitP2ServerRpc(M2P2Input input, ServerRpcParams rpcParams = default)
        {
            if (!HasRole(rpcParams.Receive.SenderClientId, RoleP2)) { UnauthorizedInputs.Value++; return; }
            _p2Queue.Enqueue(_serverTime, InputDelaySeconds, input);
        }

        bool HasRole(ulong clientId, byte role)
            => _connectionRole.TryGetValue(clientId, out byte r) && r == role;

        void Update()
        {
            if (!IsServer || _sim == null) return;

            _tickWatch.Restart();
            _serverTime += Time.deltaTime;

            // Move the target and record the lag-compensation history.
            _targetPhase += Time.deltaTime;
            float tx = Mathf.Sin(_targetPhase * 0.7f) * 6f;
            float tz = 10f;
            _lag.Record(_serverTime, _sim.State.BodyYaw, tx, tz);
            TargetPosition.Value = new Vector2(tx, tz);

            // Apply delay-conditioned inputs (oldest ready).
            bool appliedP1 = false;
            while (_p1Queue.TryDequeue(_serverTime, out M2P1Input p1))
            {
                _sim.ApplyP1(p1, Time.deltaTime);
                LastAckedP1Sequence.Value = p1.Sequence;
                appliedP1 = true;
            }
            while (_p2Queue.TryDequeue(_serverTime, out M2P2Input p2))
            {
                ProcessP2(in p2, tx, tz);
                LastAckedP2Sequence.Value = p2.Sequence;
            }

            _weapon.Tick(_serverTime);
            if (appliedP1) State.Value = _sim.State;

            float tickMs = (float)_tickWatch.Elapsed.TotalMilliseconds;
            _metricAccum += tickMs;
            _metricTicks++;
            if (_serverTime >= _nextMetricTime)
            {
                _nextMetricTime = _serverTime + 2.0;
                Debug.Log($"[M2-metrics] avg server tick {(_metricAccum / Mathf.Max(1, _metricTicks)):0.00} ms | hits {ValidatedHits.Value} rejected {RejectedFires.Value} unauthorized {UnauthorizedInputs.Value} | delay {InputDelaySeconds * 1000:0}ms loss {LossPercent:0}%");
                _metricAccum = 0f;
                _metricTicks = 0;
            }

            transform.position = new Vector3(_sim.State.PosX, transform.position.y, _sim.State.PosZ);
            transform.rotation = Quaternion.Euler(0f, _sim.State.BodyYaw, 0f);
        }

        double _nextMetricTime;

        void ProcessP2(in M2P2Input input, float targetX, float targetZ)
        {
            if (input.Reload) _weapon.StartReload(_serverTime);

            if (!input.Fire) { _sim.ApplyP2(in input); return; }

            double fireTime = _serverTime;
            if (!_lag.TryRewind(fireTime - LagRewindSeconds, out float historicalBodyYaw, out float histTargetX, out float histTargetZ))
            {
                _sim.ApplyP2(in input);
                return;
            }

            // Sector must be legal against the body orientation the shooter saw (historical).
            float offset = M2BodySim.Normalize(input.AimYaw - historicalBodyYaw);
            bool legalHistorically = Mathf.Abs(offset) <= SectorHalfDegrees + 0.001f;
            bool legalNow = _sim.SectorLegal(input.AimYaw);

            M2WeaponState.FireResult fire = _weapon.TryFire(fireTime);
            if (!legalHistorically || fire != M2WeaponState.FireResult.Ok)
            {
                RejectedFires.Value++;
                if (!legalHistorically)
                    Debug.Log($"[M2-validation] rejected fire: aim {input.AimYaw:0.0} outside historical sector around {historicalBodyYaw:0.0} (current {_sim.State.BodyYaw:0.0})");
                return;
            }

            // Rewound hitscan against the target position the shooter saw.
            Vector3 origin = new Vector3(_sim.State.PosX, 0f, _sim.State.PosZ);
            Vector3 dir = new Vector3(Mathf.Sin(input.AimYaw * Mathf.Deg2Rad), 0f, Mathf.Cos(input.AimYaw * Mathf.Deg2Rad));
            Vector3 toTarget = new Vector3(histTargetX - origin.x, 0f, histTargetZ - origin.z);
            float forward = Vector3.Dot(toTarget, dir);
            float lateral = Vector3.Cross(dir, toTarget).magnitude;
            bool hit = forward > 0f && forward <= FireRange && lateral <= TargetRadius;

            if (hit)
            {
                ValidatedHits.Value++;
                Debug.Log($"[M2-lagcomp] validated hit: aim {input.AimYaw:0.0} vs historical yaw {historicalBodyYaw:0.0} (current {_sim.State.BodyYaw:0.0}); target rewound to ({histTargetX:0.0},{histTargetZ:0.0})");
            }
            float bodyMoved = Mathf.Abs(M2BodySim.Normalize(_sim.State.BodyYaw - historicalBodyYaw));
            if (bodyMoved > 5f)
                Debug.Log($"[M2-lagcomp] rewind mattered: historical {historicalBodyYaw:0.0} vs current {_sim.State.BodyYaw:0.0} (moved {bodyMoved:0.0})");

            _sim.ApplyP2(in input);
        }
    }
}
