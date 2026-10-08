using BeMyArms.Core;
using BeMyArms.Networking;
using UnityEngine;

namespace BeMyArms.Match
{
    public struct CombatHit
    {
        public HitboxRegion.Region Region;
        public float Distance;
        public Vector3 Point;
    }

    /// <summary>
    /// Standard competitive hit profile, independent of skins, cosmetic sensors and client bones.
    /// A bounded torso/legs capsule and the exposed P1 head sphere move with historical stance.
    /// Returns actual surface entry, not closest approach to a sampled line.
    /// </summary>
    public static class CombatHitGeometry
    {
        public const float BodyRadius = 0.35f;
        public const float HeadRadius = 0.18f;
        public const float CrouchHeadForward = 0.20f;

        public static Vector3 HeadCenter(in BodyState state)
        {
            float height = Height(state);
            float yaw = state.BodyYaw * Mathf.Deg2Rad;
            float forward = state.Crouching ? CrouchHeadForward : 0f;
            return new Vector3(state.PosX + Mathf.Sin(yaw) * forward,
                state.PosY + height - HeadRadius, state.PosZ + Mathf.Cos(yaw) * forward);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, in BodyState target,
            float range, out CombatHit hit)
        {
            hit = default;
            if (range <= 0f || direction.sqrMagnitude < 1e-8f) return false;
            Vector3 dir = direction.normalized;
            float height = Height(target);
            float torsoTop = height - HeadRadius * 2f;
            float radius = Mathf.Min(BodyRadius, torsoTop * 0.5f);
            Vector3 bottom = new Vector3(target.PosX, target.PosY + radius, target.PosZ);
            Vector3 top = new Vector3(target.PosX, target.PosY + torsoTop - radius, target.PosZ);
            float body = CapsuleEntry(origin, dir, bottom, top, radius);
            float head = SphereEntry(origin, dir, HeadCenter(target), HeadRadius);
            float distance = Mathf.Min(body, head);
            if (float.IsPositiveInfinity(distance) || distance > range) return false;
            hit = new CombatHit
            {
                Region = head < body ? HitboxRegion.Region.Head : HitboxRegion.Region.Body,
                Distance = distance,
                Point = origin + dir * distance
            };
            return true;
        }

        static float Height(in BodyState state) => state.HitHeight > 0.01f ? state.HitHeight : 1.8f;

        static float SphereEntry(Vector3 origin, Vector3 dir, Vector3 center, float radius)
        {
            Vector3 offset = origin - center;
            float c = offset.sqrMagnitude - radius * radius;
            if (c <= 0f) return 0f;
            float b = Vector3.Dot(offset, dir);
            float discriminant = b * b - c;
            if (discriminant < 0f) return float.PositiveInfinity;
            float entry = -b - Mathf.Sqrt(discriminant);
            return entry >= 0f ? entry : float.PositiveInfinity;
        }

        static float CapsuleEntry(Vector3 origin, Vector3 dir, Vector3 bottom, Vector3 top, float radius)
        {
            Vector3 closest = new Vector3(bottom.x, Mathf.Clamp(origin.y, bottom.y, top.y), bottom.z);
            if ((origin - closest).sqrMagnitude <= radius * radius) return 0f;
            // Full end spheres are inside the union of this capsule; their nearest entries cannot
            // precede the capsule surface. This also handles vertical rays and zero-length capsules.
            float best = Mathf.Min(SphereEntry(origin, dir, bottom, radius), SphereEntry(origin, dir, top, radius));
            float dx = origin.x - bottom.x, dz = origin.z - bottom.z;
            float a = dir.x * dir.x + dir.z * dir.z;
            if (a < 1e-8f) return best;
            float b = dx * dir.x + dz * dir.z;
            float c = dx * dx + dz * dz - radius * radius;
            float discriminant = b * b - a * c;
            if (discriminant < 0f) return best;
            float root = Mathf.Sqrt(discriminant);
            for (int i = 0; i < 2; i++)
            {
                float t = (-b + (i == 0 ? -root : root)) / a;
                float y = origin.y + dir.y * t;
                if (t >= 0f && y >= bottom.y && y <= top.y) best = Mathf.Min(best, t);
            }
            return best;
        }
    }
}
