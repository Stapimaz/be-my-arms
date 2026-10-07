namespace BeMyArms.M3
{
    /// <summary>Exact server body-yaw samples for the snapshot P2 actually aimed against.</summary>
    public sealed class M3AimHistory
    {
        readonly uint[] _ticks = new uint[120];
        readonly float[] _yaws = new float[120];
        int _count;
        public void Clear() => _count = 0;
        public void Record(uint tick, float yaw)
        {
            int index = (int)(tick % (uint)_ticks.Length);
            _ticks[index] = tick;
            _yaws[index] = yaw;
            _count = System.Math.Min(_ticks.Length, _count + 1);
        }
        public bool TryGet(uint tick, uint currentTick, out float yaw)
        {
            int index = (int)(tick % (uint)_ticks.Length);
            yaw = _yaws[index];
            return _count > 0 && tick <= currentTick && currentTick - tick < _ticks.Length && _ticks[index] == tick;
        }
    }
}
