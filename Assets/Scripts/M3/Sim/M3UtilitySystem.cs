using System;
using System.Collections.Generic;

namespace BeMyArms.M3
{
    public enum M3UtilityKind : byte
    {
        Grenade = 0,
        Smoke = 1,
        Flash = 2
    }

    /// <summary>
    /// Server-authoritative placeholder utility model (concept M3 scope): a thrown grenade does
    /// radial damage on a fuse, smoke blocks the hitscan line, and a flash blinds nearby bodies.
    /// Pure and unit-testable; the visual/audio presentation is intentionally not modelled here.
    /// All values are TUNING.
    /// </summary>
    public class M3UtilitySystem
    {
        public float GrenadeFuse = 1.3f;
        public float GrenadeRadius = 4f;
        public float GrenadeDamage = 60f;
        public float ThrowDistance = 18f;

        public float SmokeRadius = 3f;
        public float SmokeDuration = 12f;

        public float FlashRadius = 8f;
        public float FlashMaxDuration = 2.5f;

        struct Grenade
        {
            public double DetonateTime;
            public int Team;
            public float X;
            public float Z;
        }

        struct Smoke
        {
            public double EndTime;
            public float X;
            public float Z;
        }

        struct Flash
        {
            public double EndTime;
            public float X;
            public float Z;
        }

        readonly List<Grenade> _grenades = new List<Grenade>();
        readonly List<Smoke> _smokes = new List<Smoke>();
        readonly List<Flash> _flashes = new List<Flash>();

        /// <summary>Raised when a grenade detonates: (team, x, z). The server applies the damage.</summary>
        public event Action<int, float, float> GrenadeDetonated;

        public int ActiveSmokeCount => _smokes.Count;
        public int ActiveGrenadeCount => _grenades.Count;

        public void Clear()
        {
            _grenades.Clear();
            _smokes.Clear();
            _flashes.Clear();
        }

        /// <summary>
        /// Resolve a throw immediately to its landing point along the aim direction and register the
        /// effect. Projectile travel is a placeholder; the authoritative effect is what matters here.
        /// </summary>
        public void Throw(M3UtilityKind kind, int team, double now, float x, float z, float aimYaw)
        {
            float rad = aimYaw * (float)Math.PI / 180f;
            float tx = x + (float)Math.Sin(rad) * ThrowDistance;
            float tz = z + (float)Math.Cos(rad) * ThrowDistance;

            switch (kind)
            {
                case M3UtilityKind.Grenade:
                    _grenades.Add(new Grenade { DetonateTime = now + GrenadeFuse, Team = team, X = tx, Z = tz });
                    break;
                case M3UtilityKind.Smoke:
                    _smokes.Add(new Smoke { EndTime = now + SmokeDuration, X = tx, Z = tz });
                    break;
                case M3UtilityKind.Flash:
                    _flashes.Add(new Flash { EndTime = now + FlashMaxDuration, X = tx, Z = tz });
                    break;
            }
        }

        public void Tick(double now)
        {
            for (int i = _grenades.Count - 1; i >= 0; i--)
            {
                if (_grenades[i].DetonateTime > now) continue;
                Grenade g = _grenades[i];
                _grenades.RemoveAt(i);
                GrenadeDetonated?.Invoke(g.Team, g.X, g.Z);
            }

            for (int i = _smokes.Count - 1; i >= 0; i--)
                if (_smokes[i].EndTime <= now) _smokes.RemoveAt(i);

            for (int i = _flashes.Count - 1; i >= 0; i--)
                if (_flashes[i].EndTime <= now) _flashes.RemoveAt(i);
        }

        /// <summary>Damage a grenade at (x,z) deals to a body at (bx,bz); falls off linearly to the edge.</summary>
        public float GrenadeDamageAt(float x, float z, float bx, float bz)
        {
            float distance = Distance(x, z, bx, bz);
            if (distance >= GrenadeRadius) return 0f;
            float t = 1f - distance / GrenadeRadius;
            return GrenadeDamage * t;
        }

        /// <summary>True when active smoke intersects the segment a-b, i.e. the shot is blocked.</summary>
        public bool BlocksLine(double now, float ax, float az, float bx, float bz)
        {
            for (int i = 0; i < _smokes.Count; i++)
            {
                if (_smokes[i].EndTime <= now) continue;
                if (SegmentCircle(ax, az, bx, bz, _smokes[i].X, _smokes[i].Z, SmokeRadius)) return true;
            }
            return false;
        }

        /// <summary>Remaining blind time for a body at (bx,bz); 0 when out of range or no flash.</summary>
        public float FlashBlindSeconds(double now, float bx, float bz)
        {
            float best = 0f;
            for (int i = 0; i < _flashes.Count; i++)
            {
                if (_flashes[i].EndTime <= now) continue;
                float distance = Distance(_flashes[i].X, _flashes[i].Z, bx, bz);
                if (distance >= FlashRadius) continue;
                float duration = FlashMaxDuration * (1f - distance / FlashRadius);
                if (duration > best) best = duration;
            }
            return best;
        }

        static float Distance(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx;
            float dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        static bool SegmentCircle(float ax, float az, float bx, float bz, float cx, float cz, float radius)
        {
            float dx = bx - ax;
            float dz = bz - az;
            float lengthSq = dx * dx + dz * dz;
            if (lengthSq <= 0.0001f)
                return Distance(ax, az, cx, cz) <= radius;

            float t = ((cx - ax) * dx + (cz - az) * dz) / lengthSq;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;

            float px = ax + dx * t;
            float pz = az + dz * t;
            return Distance(px, pz, cx, cz) <= radius;
        }
    }
}
