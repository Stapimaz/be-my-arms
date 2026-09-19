using System.Collections.Generic;

namespace BeMyArms.M2
{
    /// <summary>
    /// Server-side lag-compensation history. Records the body orientation and target position over
    /// time so a fire event can be validated at the moment the shooter saw it — including the
    /// historical BodyYaw that the aim sector depends on. Pure and unit-testable.
    /// </summary>
    public class M2LagCompensation
    {
        struct Sample
        {
            public double Time;
            public float BodyYaw;
            public float TargetX;
            public float TargetZ;
        }

        readonly List<Sample> _samples = new List<Sample>();

        /// <summary>Maximum rewind accepted, bounding the abuse window.</summary>
        public float MaxRewindSeconds = 1.0f;

        public int SampleCount => _samples.Count;

        public void Record(double time, float bodyYaw, float targetX, float targetZ)
        {
            _samples.Add(new Sample { Time = time, BodyYaw = bodyYaw, TargetX = targetX, TargetZ = targetZ });
            double cutoff = time - MaxRewindSeconds - 0.5;
            int removeCount = 0;
            while (removeCount < _samples.Count && _samples[removeCount].Time < cutoff) removeCount++;
            if (removeCount > 0) _samples.RemoveRange(0, removeCount);
        }

        public void Clear() => _samples.Clear();

        /// <summary>
        /// Rewind to (approximately) the given time, clamped to the rewind window. Returns false if
        /// no sample is available.
        /// </summary>
        public bool TryRewind(double time, out float bodyYaw, out float targetX, out float targetZ)
        {
            bodyYaw = 0f;
            targetX = 0f;
            targetZ = 0f;
            if (_samples.Count == 0) return false;

            double maxTime = _samples[_samples.Count - 1].Time;
            double minTime = maxTime - MaxRewindSeconds;
            double clamped = time;
            if (clamped > maxTime) clamped = maxTime;
            if (clamped < minTime) clamped = minTime;

            Sample best = _samples[0];
            for (int i = 0; i < _samples.Count; i++)
            {
                if (_samples[i].Time <= clamped) best = _samples[i];
                else break;
            }

            bodyYaw = best.BodyYaw;
            targetX = best.TargetX;
            targetZ = best.TargetZ;
            return true;
        }
    }
}
