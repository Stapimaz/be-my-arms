using BeMyArms.Networking;

namespace BeMyArms.Match
{
    /// <summary>Bounded, ordered P1 stream. One movement command per simulation tick; safe holds
    /// cover short gaps without repeating mouse deltas or pressed actions.</summary>
    public sealed class P1CommandStream
    {
        readonly DelayQueue<P1Input> _queue = new DelayQueue<P1Input>();
        uint _epoch, _accepted;
        P1Input _held;
        double _lastConsumed = double.NegativeInfinity;
        public uint Acknowledged { get; private set; }
        public int Count => _queue.Count;
        public float LossPercent { set => _queue.LossPercent = value; }

        public bool Submit(in P1Input input, double now, float delay)
        {
            if (input.ControlEpoch != _epoch || input.Sequence <= _accepted || !InputStream.Valid(input) || Count >= 120) return false;
            _accepted = input.Sequence;
            _queue.Enqueue(now, delay, input);
            return true;
        }

        public P1Input Consume(double now)
        {
            if (_queue.TryDequeue(now, out P1Input input))
            {
                Acknowledged = input.Sequence;
                _held = InputStream.Held(input);
                _lastConsumed = now;
                return input;
            }
            return now - _lastConsumed <= InputStream.SilenceTimeoutSeconds ? _held : default;
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
