using System.Collections.Generic;

namespace BeMyArms.M2
{
    /// <summary>
    /// Application-level network conditioner: delays messages and drops a percentage of them. Unity
    /// Transport's debug simulator is deprecated (no effect) and the Multiplayer Tools simulator is
    /// editor-only, so the M2 spike conditions its own role messages to exercise prediction and
    /// reconciliation under latency and loss in separate processes.
    /// </summary>
    public class M2DelayQueue<T>
    {
        struct Entry
        {
            public double ReadyTime;
            public T Value;
        }

        readonly List<Entry> _entries = new List<Entry>();
        System.Random _random = new System.Random(12345);

        public float LossPercent;
        public double LastDelaySeconds;

        public int Count => _entries.Count;

        public void Enqueue(double now, double delaySeconds, T value)
        {
            if (LossPercent > 0f && _random.NextDouble() * 100.0 < LossPercent) return; // dropped
            LastDelaySeconds = delaySeconds;
            _entries.Add(new Entry { ReadyTime = now + delaySeconds, Value = value });
        }

        /// <summary>Dequeue the oldest entry whose delay has elapsed.</summary>
        public bool TryDequeue(double now, out T value)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].ReadyTime <= now)
                {
                    value = _entries[i].Value;
                    _entries.RemoveAt(i);
                    return true;
                }
            }
            value = default;
            return false;
        }

        public void Clear() => _entries.Clear();
    }
}
