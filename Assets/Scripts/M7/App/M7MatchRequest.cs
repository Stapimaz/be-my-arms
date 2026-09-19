using System;
using UnityEngine;

namespace BeMyArms.M7
{
    /// <summary>Private/custom match request. Mirrors the future online lobby's slot model.</summary>
    [Serializable]
    public struct M7MatchRequest
    {
        public M7MapFamily Mode;
        public string ArenaScene;
        public int Team;
        public int Body;
        public int Role;
        public ushort Port;

        public int BodiesPerTeam => Mode == M7MapFamily.Duel ? 1 : 2;
        public int RequiredHumans => 1;
    }
}
