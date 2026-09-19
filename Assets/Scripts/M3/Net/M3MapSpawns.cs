using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.M3
{
    [Serializable]
    public struct M3MapSpawn
    {
        public int Team;
        public int Body;
        public int Role;
        public Vector3 Position;
        public float Yaw;
    }

    /// <summary>
    /// Optional map-provided spawn data. When present in a match scene, the director spawns bodies
    /// at these poses instead of its built-in fallback, so production arenas drive match setup
    /// without coupling the networking layer to the map authoring layer.
    /// </summary>
    public class M3MapSpawns : MonoBehaviour
    {
        public Vector3 BoundsSize = new Vector3(24f, 8f, 24f);
        public List<M3MapSpawn> Spawns = new List<M3MapSpawn>();
        /// <summary>World-space XZ rectangles of cover/walls the body cannot enter.</summary>
        public List<Vector4> Obstacles = new List<Vector4>();

        public bool TryGet(int team, int body, int role, out M3MapSpawn spawn)
        {
            for (int i = 0; i < Spawns.Count; i++)
            {
                if (Spawns[i].Team == team && Spawns[i].Body == body && Spawns[i].Role == role)
                {
                    spawn = Spawns[i];
                    return true;
                }
            }
            spawn = default;
            return false;
        }

        public bool TryGetBodyPose(int team, int body, out Vector3 position, out float yaw)
        {
            M3MapSpawn p1, p2;
            if (TryGet(team, body, 0, out p1) && TryGet(team, body, 1, out p2))
            {
                position = (p1.Position + p2.Position) * 0.5f;
                yaw = p1.Yaw;
                return true;
            }
            if (TryGet(team, body, 0, out p1))
            {
                position = p1.Position;
                yaw = p1.Yaw;
                return true;
            }
            position = Vector3.zero;
            yaw = 0f;
            return false;
        }

        /// <summary>Builds the deterministic arena collision used by the server and client prediction.</summary>
        public M3MovementCollision BuildCollision()
        {
            var collision = new M3MovementCollision
            {
                HasBounds = BoundsSize.x > 0f && BoundsSize.z > 0f,
                MinX = -BoundsSize.x * 0.5f,
                MaxX = BoundsSize.x * 0.5f,
                MinZ = -BoundsSize.z * 0.5f,
                MaxZ = BoundsSize.z * 0.5f
            };
            for (int i = 0; i < Obstacles.Count; i++)
            {
                Vector4 o = Obstacles[i];
                collision.Boxes.Add(new M3MovementCollision.Box { MinX = o.x, MinZ = o.y, MaxX = o.z, MaxZ = o.w });
            }
            return collision;
        }
    }
}
