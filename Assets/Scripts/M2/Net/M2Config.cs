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
    }
}
