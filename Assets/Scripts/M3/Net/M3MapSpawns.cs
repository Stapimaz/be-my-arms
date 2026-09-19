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

        /// <summary>The body pose is the midpoint of its two role spawns, facing the P1 yaw.</summary>
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
    }
}
