using System;
using System.Collections.Generic;
using BeMyArms.Networking;
using Unity.Netcode;

namespace BeMyArms.Match
{
    /// <summary>Read-only accepted-shot history for presentation, not client firing authority.</summary>
    public struct RifleAccuracyState : INetworkSerializable, IEquatable<RifleAccuracyState>
    {
        public uint ControlEpoch, ShotTick, Shots;
        public int Burst;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ControlEpoch);
            serializer.SerializeValue(ref ShotTick);
            serializer.SerializeValue(ref Shots);
            serializer.SerializeValue(ref Burst);
        }

        public bool Equals(RifleAccuracyState other) => ControlEpoch == other.ControlEpoch &&
            ShotTick == other.ShotTick && Shots == other.Shots && Burst == other.Burst;
    }

    /// <summary>Immediate next-round bloom preview reconciled with accepted server shots.
    /// Rejected optimistic shots expire; late confirmations cannot start a fresh burst.</summary>
    public sealed class RifleSpreadPreview
    {
        readonly Queue<double> _pending = new Queue<double>();
        RifleAccuracyState _confirmed;
        bool _initialized;
        uint _bodyTick;
        double _tickObservedAt;
        double _predictedAt = double.NegativeInfinity;

        public void Reset()
        {
            _pending.Clear(); _initialized = false;
            _confirmed = default; _bodyTick = 0; _tickObservedAt = 0;
            _predictedAt = double.NegativeInfinity;
        }

        public void Synchronize(in RifleAccuracyState state, uint bodyTick, double now)
        {
            if (!_initialized || state.ControlEpoch != _confirmed.ControlEpoch || state.Shots < _confirmed.Shots)
            {
                Reset(); _bodyTick = bodyTick; _tickObservedAt = now;
            }
            else if (!state.Equals(_confirmed))
            {
                uint accepted = state.Shots - _confirmed.Shots;
                while (accepted > 0 && _pending.Count > 0) { _pending.Dequeue(); accepted--; }
                if (state.Burst == 0) { _pending.Clear(); _predictedAt = double.NegativeInfinity; }
            }
            if (bodyTick != _bodyTick) { _bodyTick = bodyTick; _tickObservedAt = now; }
            _confirmed = state; _initialized = true;
            // Keep the whole predicted burst while firing continuously, even with RTT > reset time.
            // Individual old rounds do not recover bloom; only a pause after the newest shot does.
            if (now - _predictedAt > RifleHandling.BurstResetSeconds) _pending.Clear();
        }

        public void PredictShot(double now)
        {
            // No gameplay state is changed; cadence is checked by the existing local-shot path.
            if (now - _predictedAt > RifleHandling.BurstResetSeconds) _pending.Clear();
            _pending.Enqueue(now); _predictedAt = now;
        }

        public int NextBurst(double now)
        {
            int ticks = Math.Max(0, unchecked((int)(_bodyTick - _confirmed.ShotTick)));
            double age = ticks * (double)InputStream.TickSeconds + Math.Max(0, now - _tickObservedAt);
            int burst = _confirmed.Burst > 0 && age <= RifleHandling.BurstResetSeconds ? _confirmed.Burst : 0;
            if (now - _predictedAt <= RifleHandling.BurstResetSeconds)
                burst += _pending.Count;
            return burst + 1;
        }
    }
}
