namespace BeMyArms.M2
{
    /// <summary>
    /// Process-wide M2 configuration, populated from command-line args by <see cref="M2Bootstrap"/>
    /// before networking starts. One role client per process, so statics are safe here.
    /// </summary>
    public static class M2Config
    {
        /// <summary>Role this client plays (P1 or P2).</summary>
        public static byte ClientRole = M2NetworkBody.RoleP1;

        /// <summary>Stable identity used to restore a role on reconnect.</summary>
        public static string ClientToken = "";

        /// <summary>One-way message delay applied to inputs (server) and snapshots (client).</summary>
        public static float OneWayDelaySeconds = 0.05f;

        /// <summary>Percentage of conditioned messages dropped.</summary>
        public static float LossPercent = 2f;

        /// <summary>How far the server rewinds for lag-compensated fire validation.</summary>
        public static float LagRewindSeconds = 0.1f;

        public static bool AutoDrive = true;

        /// <summary>When true, the client deliberately also sends the other role's input for an auth test.</summary>
        public static bool WrongRoleTest;

        /// <summary>Use the transport-level simulator instead of the application conditioner.</summary>
        public static bool UseTransportSimulation;
        public static int TransportDelayMs = 50;
        public static int TransportLossPercent = 2;

        /// <summary>If > 0, the client disconnects gracefully and quits after this many seconds.</summary>
        public static float ExitAfterSeconds;

        /// <summary>Transport disconnect timeout (ms). Short enough to detect a force-kill, long enough to avoid false drops.</summary>
        public static int DisconnectTimeoutMs = 5000;
    }
}
