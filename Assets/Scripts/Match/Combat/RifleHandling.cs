using System;

namespace BeMyArms.Match
{
    /// <summary>Shared rifle burst timing and recoil pattern. Bloom is enforced by the server;
    /// local recoil moves real mouse aim, never just the camera/viewmodel.</summary>
    public sealed class RifleHandling
    {
        public const double BurstResetSeconds = .30;
        public int Burst { get; private set; }
        double _lastShot = double.NegativeInfinity;

        public void Reset() { Burst = 0; _lastShot = double.NegativeInfinity; }

        public int Shot(double now)
        {
            if (now - _lastShot > BurstResetSeconds) Burst = 0;
            _lastShot = now;
            return ++Burst;
        }

        public static float SpreadDegrees(int burst)
            => burst <= 3 ? .10f + Math.Max(0, burst - 1) * .06f : Math.Min(1.8f, .28f + (burst - 3) * .16f);

        public static void Recoil(int burst, out float up, out float right)
        {
            up = burst <= 3 ? .24f + (burst - 1) * .08f : .72f;
            right = burst <= 3 ? 0 : (float)Math.Sin((burst - 3) * .85) * .35f;
        }

        /// <summary>Uniform disk sample with independent radius/angle. Stable per accepted shot;
        /// rejected triggers cannot grow bloom or consume the pattern.</summary>
        public static void Spread(uint shot, uint seed, float degrees, out float yaw, out float pitch)
        {
            uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; return x ^ (x >> 16); }
            double radius = Math.Sqrt((Hash(shot ^ seed) + .5) / 4294967296d) * degrees;
            double angle = (Hash(shot ^ seed ^ 0x9e3779b9u) + .5) / 4294967296d * Math.PI * 2;
            yaw = (float)(Math.Cos(angle) * radius); pitch = (float)(Math.Sin(angle) * radius);
        }
    }
}
