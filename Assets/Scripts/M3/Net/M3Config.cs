namespace BeMyArms.M3
{
    /// <summary>
    /// Process-wide M3 Duel configuration, populated from command-line args by
    /// <see cref="M3DuelBootstrap"/> before networking starts. One role per process, so the
    /// statics are safe.
    /// </summary>
    public static class M3Config
    {
        /// <summary>Desired team (0 = A, 1 = B).</summary>
        public static int ClientTeam = 0;

        /// <summary>Desired role (0 = P1, 1 = P2).</summary>
        public static int ClientRole = 0;

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
