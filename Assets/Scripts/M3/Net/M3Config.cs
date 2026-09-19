namespace BeMyArms.M3
{
    using System.Collections.Generic;

    /// <summary>
    /// Process-wide match configuration, populated from command-line args by
    /// <see cref="M3DuelBootstrap"/> before networking starts. One role per process, so the
    /// statics are safe. The M4 layer reads the matchmaking/rating fields from here.
    /// </summary>
    public static class M3Config
    {
        /// <summary>Shared bodies per team: 1 = Duel, 2 = 2v2 (concept §14.1).</summary>
        public static int BodiesPerTeam = 1;

        /// <summary>When true the server enqueues connections and lets the matchmaker assign slots.</summary>
        public static bool UseMatchmaker;

        /// <summary>When true a scene bootstrap with no CLI role starts as a client (private-match flow).</summary>
        public static bool AutoStartClient;

        /// <summary>Port override for the private-match flow (0 = use the scene/CLI port).</summary>
        public static ushort PortOverride;

        /// <summary>Preferred number of human players (bodies per team * 2 players per body * 2 teams).</summary>
        public static int ExpectedPlayers = 4;

        /// <summary>Role-specific MMR seed per player token; used for post-match rating updates.</summary>
        public static readonly Dictionary<string, float> PlayerMmr = new Dictionary<string, float>();

        public static float MmrFor(string token)
            => !string.IsNullOrEmpty(token) && PlayerMmr.TryGetValue(token, out float mmr) ? mmr : 1000f;

        /// <summary>Premade party id per player token; a duo shares one body.</summary>
        public static readonly Dictionary<string, string> PlayerParty = new Dictionary<string, string>();

        public static string PartyFor(string token)
            => !string.IsNullOrEmpty(token) && PlayerParty.TryGetValue(token, out string party) ? party : null;

        /// <summary>Desired team (0 = A, 1 = B). Only used in direct/dev slot mode.</summary>
        public static int ClientTeam = 0;

        /// <summary>Desired body within the team (direct/dev slot mode).</summary>
        public static int ClientBody = 0;

        /// <summary>Desired role (0 = P1, 1 = P2).</summary>
        public static int ClientRole = 0;

        /// <summary>Matchmaker role preference: 0 = P1, 1 = P2, 2 = Either.</summary>
        public static int ClientPreference = 0;

        /// <summary>Stable identity used to restore a slot on reconnect.</summary>
        public static string ClientToken = "";

        /// <summary>One-way message delay applied to inputs (server) and snapshots (client).</summary>
        public static float OneWayDelaySeconds = 0.05f;

        /// <summary>Percentage of conditioned messages dropped.</summary>
        public static float LossPercent = 2f;

        /// <summary>How far the server rewinds for lag-compensated fire validation.</summary>
        public static float LagRewindSeconds = 0.1f;

        public static bool AutoDrive = true;

        /// <summary>Auto-driven clients automatically buy a primary/secondary/utility each round.</summary>
        public static bool AutoBuy = true;

        /// <summary>Auto-driven P2 clients fire at the enemy body.</summary>
        public static bool AutoFire = true;

        /// <summary>Auto-driven P2 clients throw purchased utility.</summary>
        public static bool AutoUtility = true;

        /// <summary>Server starts the match after this many real seconds even if slots are unfilled (bots fill in).</summary>
        public static float StartDelaySeconds = 6f;

        /// <summary>Preferred number of human slots before the match starts.</summary>
        public static int RequiredPlayers = 4;

        public static float BuySeconds = 10f;
        public static float LiveSeconds = 150f;
        public static float RoundEndSeconds = 3f;

        public static float ZoneStartRadius = 40f;
        public static float ZoneEndRadius = 8f;
        public static float ZoneCloseStart = 60f;
        public static float ZoneCloseDuration = 60f;
        public static float ZoneDamagePerSecond = 5f;

        /// <summary>If > 0, the process gracefully disconnects and quits after this many seconds.</summary>
        public static float ExitAfterSeconds;

        /// <summary>Transport disconnect timeout (ms).</summary>
        public static int DisconnectTimeoutMs = 5000;

        public static ushort Port = 7779;
    }
}
