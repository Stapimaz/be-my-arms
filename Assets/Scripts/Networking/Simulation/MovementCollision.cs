using System;
using System.Collections.Generic;

namespace BeMyArms.Networking
{
    /// <summary>
    /// Pure, deterministic 3D arena collision for the shared body. Both the authoritative server and
    /// client prediction build the same model from the same scene geometry, so movement and
    /// reconciliation agree exactly. Engine-free and unit-testable.
    ///
    /// The body is a vertical cylinder (radius + height). <see cref="Solids"/> are blocking boxes;
    /// <see cref="Surfaces"/> are walkable tops (flat or sloped, e.g. ramps). A solid whose top is
    /// within <see cref="StepHeight"/> of the feet is not a wall — the body steps onto it and the
    /// surface height takes over.
    /// </summary>
    public class MovementCollision
    {
        public struct Box
        {
            public float MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
        }

        /// <summary>A walkable top. SlopeAxis 0 = height rises along +X, 1 = along +Z, 2 = flat.</summary>
        public struct Surface
        {
            public float MinX, MinZ, MaxX, MaxZ;
            public float LowY, HighY;
            public byte SlopeAxis;
        }

        public bool HasBounds;
        public float MinX, MaxX, MinZ, MaxZ;

        public float BodyRadius = 0.4f;
        public float BodyHeight = 1.8f;
        public float StepHeight = 0.55f;
        public float BaseGroundY;

        public readonly List<Box> Solids = new List<Box>();
        public readonly List<Surface> Surfaces = new List<Surface>();

        public bool IsEmpty => !HasBounds && Solids.Count == 0 && Surfaces.Count == 0;

        public void AddBox(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            Solids.Add(new Box { MinX = minX, MinY = minY, MinZ = minZ, MaxX = maxX, MaxY = maxY, MaxZ = maxZ });
        }

        public void AddSurface(float minX, float minZ, float maxX, float maxZ, float lowY, float highY, byte slopeAxis)
        {
            Surfaces.Add(new Surface
            {
                MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ,
                LowY = lowY, HighY = highY, SlopeAxis = slopeAxis
            });
        }

        /// <summary>
        /// Highest walkable surface at (x,z) that is at or below <paramref name="maxY"/>, or the
        /// base ground height. maxY keeps the body from snapping up onto a catwalk it is under.
        /// </summary>
        public float SurfaceHeight(float x, float z, float maxY = float.PositiveInfinity)
        {
            float best = BaseGroundY;
            for (int i = 0; i < Surfaces.Count; i++)
            {
                Surface s = Surfaces[i];
                if (x < s.MinX || x > s.MaxX || z < s.MinZ || z > s.MaxZ) continue;

                float h = s.HighY;
                if (s.SlopeAxis == 0 && s.MaxX > s.MinX)
                    h = s.LowY + (s.HighY - s.LowY) * ((x - s.MinX) / (s.MaxX - s.MinX));
                else if (s.SlopeAxis == 1 && s.MaxZ > s.MinZ)
                    h = s.LowY + (s.HighY - s.LowY) * ((z - s.MinZ) / (s.MaxZ - s.MinZ));

                if (h > best && h <= maxY) best = h;
            }

            // Any solid's top is walkable when it is within the caller's reach (step band), so a
            // step/platform/crate needs only a box.
            for (int i = 0; i < Solids.Count; i++)
            {
                Box b = Solids[i];
                if (x < b.MinX || x > b.MaxX || z < b.MinZ || z > b.MaxZ) continue;
                if (b.MaxY > best && b.MaxY <= maxY) best = b.MaxY;
            }

            return best;
        }

        /// <summary>Arena bounds clamp for the body's feet.</summary>
        public void ClampBounds(ref float x, ref float z)
        {
            if (!HasBounds) return;
            x = MathfClamp(x, MinX + BodyRadius, MaxX - BodyRadius);
            z = MathfClamp(z, MinZ + BodyRadius, MaxZ - BodyRadius);
        }

        /// <summary>Push the body out of any solid it vertically overlaps and cannot step onto.</summary>
        public void ResolveHorizontal(ref BodyState s)
        {
            ClampBounds(ref s.PosX, ref s.PosZ);
            if (Solids.Count == 0) return;

            float feet = s.PosY;
            // Use the current stance height so a crouched body can pass under low cover.
            float bodyHeight = s.HitHeight > 0.01f ? s.HitHeight : BodyHeight;
            float head = s.PosY + bodyHeight;

            // Two passes so corners between two boxes settle cleanly.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < Solids.Count; i++)
                {
                    Box b = Solids[i];
                    if (b.MaxY <= feet + StepHeight) continue; // low enough to step onto
                    if (b.MinY >= head) continue;              // above the body

                    float minX = b.MinX - BodyRadius, maxX = b.MaxX + BodyRadius;
                    float minZ = b.MinZ - BodyRadius, maxZ = b.MaxZ + BodyRadius;
                    if (s.PosX <= minX || s.PosX >= maxX || s.PosZ <= minZ || s.PosZ >= maxZ) continue;

                    float left = s.PosX - minX, right = maxX - s.PosX;
                    float down = s.PosZ - minZ, up = maxZ - s.PosZ;

                    float least = left;
                    int axis = 0;
                    if (right < least) { least = right; axis = 1; }
                    if (down < least) { least = down; axis = 2; }
                    if (up < least) { axis = 3; }

                    switch (axis)
                    {
                        case 0: s.PosX = minX; break;
                        case 1: s.PosX = maxX; break;
                        case 2: s.PosZ = minZ; break;
                        default: s.PosZ = maxZ; break;
                    }
                }
            }
        }

        /// <summary>
        /// True when the body has vertical clearance to stand to <paramref name="standHeight"/> at
        /// (x, feetY, z): no solid overlaps the body cylinder in the band between the current
        /// crouched height and the standing height.
        /// </summary>
        public bool CanStand(float x, float feetY, float z, float standHeight)
        {
            float lowBand = feetY + StepHeight;
            float highBand = feetY + standHeight;
            for (int i = 0; i < Solids.Count; i++)
            {
                Box b = Solids[i];
                if (b.MaxY <= lowBand) continue;  // steppable / below the body
                if (b.MinY >= highBand) continue; // above the standing head
                if (x <= b.MinX - BodyRadius || x >= b.MaxX + BodyRadius) continue;
                if (z <= b.MinZ - BodyRadius || z >= b.MaxZ + BodyRadius) continue;
                return false; // a ceiling/ledge blocks standing up here
            }
            return true;
        }

        /// <summary>Nearest solid hit along a ray (bullet blocking). Returns false when nothing is hit.</summary>
        public bool RaycastSolids(float ox, float oy, float oz, float dx, float dy, float dz, float maxDistance, out float hitDistance)
        {
            hitDistance = maxDistance;
            bool any = false;
            for (int i = 0; i < Solids.Count; i++)
            {
                if (RayBox(Solids[i], ox, oy, oz, dx, dy, dz, out float t) && t > 0.001f && t < hitDistance)
                {
                    hitDistance = t;
                    any = true;
                }
            }
            return any;
        }

        /// <summary>Slab-method ray/AABB intersection; t is the entry distance.</summary>
        static bool RayBox(Box b, float ox, float oy, float oz, float dx, float dy, float dz, out float t)
        {
            t = 0f;
            const float epsilon = 1e-6f;
            float invX = Math.Abs(dx) < epsilon ? 0f : 1f / dx;
            float invY = Math.Abs(dy) < epsilon ? 0f : 1f / dy;
            float invZ = Math.Abs(dz) < epsilon ? 0f : 1f / dz;

            float tmin = float.NegativeInfinity;
            float tmax = float.PositiveInfinity;

            if (!Slab(ox, dx, invX, b.MinX, b.MaxX, ref tmin, ref tmax)) return false;
            if (!Slab(oy, dy, invY, b.MinY, b.MaxY, ref tmin, ref tmax)) return false;
            if (!Slab(oz, dz, invZ, b.MinZ, b.MaxZ, ref tmin, ref tmax)) return false;

            if (tmax < 0f || tmin > tmax) return false;
            t = tmin > 0f ? tmin : 0f;
            return true;
        }

        static bool Slab(float origin, float dir, float invDir, float lo, float hi, ref float tmin, ref float tmax)
        {
            if (Math.Abs(dir) < 1e-6f)
            {
                return origin >= lo && origin <= hi;
            }
            float t1 = (lo - origin) * invDir;
            float t2 = (hi - origin) * invDir;
            if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
            if (t1 > tmin) tmin = t1;
            if (t2 < tmax) tmax = t2;
            return tmin <= tmax;
        }

        static float MathfClamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
