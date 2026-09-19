using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.M7
{
    public enum M7MapFamily
    {
        Duel,
        TwoVsTwo
    }

    [Serializable]
    public struct M7Spawn
    {
        public int Team;
        public int Body;
        public int Role;
        public Vector3 Position;
        public float Yaw;
    }

    /// <summary>
    /// Authoritative map record: family, play bounds, team spawns and measured layout facts
    /// (cover/lanes/verticality). Built by the map builder from the arena scene and validated
    /// independently of the scene so it can be unit-tested and consumed by match setup.
    /// </summary>
    [CreateAssetMenu(menuName = "Be My Arms/M7 Map Definition", fileName = "M7MapDefinition")]
    public class M7MapDefinition : ScriptableObject
    {
        public string MapId = "m7_arena";
        public M7MapFamily Family = M7MapFamily.Duel;
        public string ScenePath = "";
        public Vector3 BoundsSize = new Vector3(24f, 8f, 24f);
        public List<M7Spawn> Spawns = new List<M7Spawn>();
        public int CoverCount;
        public int LaneCount;
        public float MaxVerticality;

        public int RequiredSpawns => Family == M7MapFamily.Duel ? 4 : 8;

        public bool TryGetSpawn(int team, int body, int role, out M7Spawn spawn)
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
    }
}
