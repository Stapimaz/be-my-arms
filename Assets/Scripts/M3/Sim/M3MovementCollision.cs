using System.Collections.Generic;

namespace BeMyArms.M3
{
    /// <summary>
    /// Pure, deterministic arena collision for the shared body: keep it inside the map bounds and
    /// out of rectangular obstacles (cover/walls). Used identically on the server and in client
    /// prediction so reconciliation stays consistent. Engine-free and unit-testable.
    /// </summary>
    public class M3MovementCollision
    {
        public struct Box
        {
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
        }

        public bool HasBounds;
        public float MinX, MaxX, MinZ, MaxZ;
        public float BodyRadius = 0.4f;
        public readonly List<Box> Boxes = new List<Box>();

        public bool IsEmpty => !HasBounds && Boxes.Count == 0;

        /// <summary>Push a point out of bounds/obstacles along the least-penetrating axis.</summary>
        public void Resolve(ref float x, ref float z)
        {
            if (HasBounds)
            {
                x = Clamp(x, MinX + BodyRadius, MaxX - BodyRadius);
                z = Clamp(z, MinZ + BodyRadius, MaxZ - BodyRadius);
            }

            // Two passes so corners between two boxes settle cleanly.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < Boxes.Count; i++)
                {
                    Box b = Boxes[i];
                    float minX = b.MinX - BodyRadius, maxX = b.MaxX + BodyRadius;
                    float minZ = b.MinZ - BodyRadius, maxZ = b.MaxZ + BodyRadius;
                    if (x <= minX || x >= maxX || z <= minZ || z >= maxZ) continue;

                    float left = x - minX;
                    float right = maxX - x;
                    float down = z - minZ;
                    float up = maxZ - z;

                    float least = left;
                    int axis = 0;
                    if (right < least) { least = right; axis = 1; }
                    if (down < least) { least = down; axis = 2; }
                    if (up < least) { least = up; axis = 3; }

                    switch (axis)
                    {
                        case 0: x = minX; break;
                        case 1: x = maxX; break;
                        case 2: z = minZ; break;
                        default: z = maxZ; break;
                    }
                }
            }
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
