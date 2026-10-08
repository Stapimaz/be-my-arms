using System.Collections.Generic;

namespace BeMyArms.Networking
{
    /// <summary>
    /// Server-side lag-compensation history. Records the body orientation and target position over
    /// time so a fire event can be validated at the moment the shooter saw it — including the
    /// historical BodyYaw that the aim sector depends on. Pure and unit-testable.
    /// </summary>
    public class LagCompensation
    {
        struct Sample
        {
            public double Time;
            public float BodyYaw;
            public BodyState Target;
            public bool Alive;
        }

        readonly List<Sample> _samples = new List<Sample>();

        /// <summary>Maximum rewind accepted, bounding the abuse window.</summary>
        public float MaxRewindSeconds = 1.0f;

        public int SampleCount => _samples.Count;

        public void Record(double time, float bodyYaw, float targetX, float targetZ)
            => Record(time, bodyYaw, new BodyState { PosX = targetX, PosZ = targetZ }, true);

        /// <summary>Capture the whole combat pose together: no mixing historical X/Z with live Y/stance.</summary>
        public void Record(double time, float bodyYaw, in BodyState target, bool alive)
        {
            _samples.Add(new Sample { Time = time, BodyYaw = bodyYaw, Target = target, Alive = alive });
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
            bool found = TryRewindPose(time, out bodyYaw, out BodyState target, out _);
            targetX = target.PosX;
            targetZ = target.PosZ;
            return found;
        }

        public bool TryRewindPose(double time, out float bodyYaw, out BodyState target, out bool alive)
        {
            bodyYaw = 0f;
            target = default;
            alive = false;
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
            target = best.Target;
            alive = best.Alive;
            return true;
        }
    }
}
