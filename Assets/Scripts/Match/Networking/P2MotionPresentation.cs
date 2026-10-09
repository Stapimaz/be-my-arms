using UnityEngine;

namespace BeMyArms.Match
{
    /// <summary>Local-P2 presentation only: interpolate received positions/stance on a short
    /// arrival timeline. Never extrapolates, predicts movement, or modifies aim/shot authority.</summary>
    public sealed class P2MotionPresentation
    {
        public const double Delay = .075;
        const int Capacity = 12;
        struct Sample { public double Time; public Vector3 Position; public float EyeHeight; }
        readonly Sample[] _samples = new Sample[Capacity];
        int _count;
        uint _epoch, _tick;
        public void Clear() => _count = 0;

        public void Observe(uint epoch, uint tick, Vector3 position, float eyeHeight, double now)
        {
            if (_count != 0 && (epoch != _epoch || tick < _tick ||
                (position - _samples[_count - 1].Position).sqrMagnitude > 9f || now - _samples[_count - 1].Time > .25))
                Clear(); // Do not glide through round/role/teleport/stale-stream discontinuities.
            if (_count != 0 && tick == _tick) return;
            _epoch = epoch; _tick = tick;
            if (_count == Capacity)
            {
                for (int i = 1; i < _count; i++) _samples[i - 1] = _samples[i];
                _count--;
            }
            _samples[_count++] = new Sample { Time = now, Position = position, EyeHeight = eyeHeight };
        }

        public Vector3 Evaluate(double now, out float eyeHeight)
        {
            if (_count == 0) { eyeHeight = 1.45f; return Vector3.zero; }
            double time = now - Delay;
            Sample value = _samples[0];
            for (int i = 1; i < _count; i++)
            {
                Sample next = _samples[i];
                if (next.Time >= time)
                {
                    float t = next.Time > value.Time ? Mathf.Clamp01((float)((time - value.Time) / (next.Time - value.Time))) : 1;
                    eyeHeight = Mathf.Lerp(value.EyeHeight, next.EyeHeight, t);
                    return Vector3.Lerp(value.Position, next.Position, t);
                }
                value = next;
            }
            eyeHeight = value.EyeHeight;
            return value.Position; // Missing packets hold received geometry, not invented travel.
        }
    }
}
