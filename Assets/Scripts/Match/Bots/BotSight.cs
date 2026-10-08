using BeMyArms.Networking;
using UnityEngine;

namespace BeMyArms.Match
{
    public static class BotSight
    {
        public static Vector3 Feet(in BodyState state) => new Vector3(state.PosX, state.PosY, state.PosZ);
        public static Vector3 Eye(in BodyState state) => Feet(state) + Vector3.up * (state.EyeHeight > .01f ? state.EyeHeight : 1.45f);
        public static Vector3 Chest(in BodyState state) => Feet(state) + Vector3.up * (state.HitHeight > .01f ? state.HitHeight * .55f : 1f);
        public static bool Clear(MovementCollision map, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from; float distance = d.magnitude;
            if (distance < .01f) return true;
            d /= distance;
            return map == null || !map.RaycastSolids(from.x, from.y, from.z, d.x, d.y, d.z, distance, out _);
        }

        /// <summary>Sample head and torso exposure, not merely a center ray through low cover.</summary>
        public static float Exposure(MovementCollision map, Vector3 feet, Vector3 threatEye, bool crouch)
        {
            float height = crouch ? 1.15f : 1.8f;
            return (Clear(map, threatEye, feet + Vector3.up * (height - .18f)) ? .6f : 0f)
                + (Clear(map, threatEye, feet + Vector3.up * (height * .55f)) ? .4f : 0f);
        }
    }
}
