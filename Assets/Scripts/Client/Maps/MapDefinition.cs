using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.Client
{
    public enum MapFamily
    {
        Duel,
        TwoVsTwo
    }

    [Serializable]
    public struct MapSpawn
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
    [CreateAssetMenu(menuName = "Be My Arms/Client Map Definition", fileName = "MapDefinition")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7MapDefinition")]
    public class MapDefinition : ScriptableObject
    {
        public string MapId = "m7_arena";
        public MapFamily Family = MapFamily.Duel;
        public string ScenePath = "";
        public Vector3 BoundsSize = new Vector3(24f, 8f, 24f);
        public List<MapSpawn> Spawns = new List<MapSpawn>();
        public int CoverCount;
        public int LaneCount;
        public float MaxVerticality;
        /// <summary>Minimum horizontal distance from any spawn to non-floor geometry (metres).</summary>
        public float MinSpawnClearance;

        public int RequiredSpawns => Family == MapFamily.Duel ? 4 : 8;

        public bool TryGetSpawn(int team, int body, int role, out MapSpawn spawn)
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
