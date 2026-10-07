using BeMyArms.M2;

namespace BeMyArms.M3
{
    /// <summary>Bounded, ordered P1 stream. One movement command per simulation tick; safe holds
    /// cover short gaps without repeating mouse deltas or pressed actions.</summary>
    public sealed class M3P1CommandStream
    {
        readonly M2DelayQueue<M2P1Input> _queue = new M2DelayQueue<M2P1Input>();
        uint _epoch, _accepted;
        M2P1Input _held;
        double _lastConsumed = double.NegativeInfinity;
        public uint Acknowledged { get; private set; }
        public int Count => _queue.Count;
        public float LossPercent { set => _queue.LossPercent = value; }

        public bool Submit(in M2P1Input input, double now, float delay)
        {
            if (input.ControlEpoch != _epoch || input.Sequence <= _accepted || !M3InputStream.Valid(input) || Count >= 120) return false;
            _accepted = input.Sequence;
            _queue.Enqueue(now, delay, input);
            return true;
        }

        public M2P1Input Consume(double now)
        {
            if (_queue.TryDequeue(now, out M2P1Input input))
            {
                Acknowledged = input.Sequence;
                _held = M3InputStream.Held(input);
                _lastConsumed = now;
                return input;
            }
            return now - _lastConsumed <= M3InputStream.SilenceTimeoutSeconds ? _held : default;
        }

        public void Reset(uint epoch)
        {
            _epoch = epoch;
            _accepted = Acknowledged = 0;
            _held = default;
            _lastConsumed = double.NegativeInfinity;
            _queue.Clear();
        }
    }
}
