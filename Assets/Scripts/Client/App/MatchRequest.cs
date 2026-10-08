using System;
using BeMyArms.Match;
using UnityEngine;

namespace BeMyArms.Client
{
    /// <summary>Private/custom match request. Mirrors the future online lobby's slot model.</summary>
    [Serializable]
    public struct MatchRequest
    {
        public MapFamily Mode;
        public string ArenaScene;
        public int Team;
        public int Body;
        public int Role;
        public ushort Port;
        public bool Duo;
        public bool JoinExisting;
        public string Address;

        /// <summary>Server bot profile for filled slots. Defaults to Easy (practice/playtest).</summary>
        public BotDifficulty BotDifficulty;

        public int BodiesPerTeam => Mode == MapFamily.Duel ? 1 : 2;
        public int RequiredHumans => Duo ? 2 : 1;
    }
}
