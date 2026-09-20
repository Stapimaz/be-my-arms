namespace BeMyArms.M3
{
    /// <summary>
    /// Explicit local input state for the local player, written by the M7 presentation layer from
    /// the authoritative gameplay/match state and read by <see cref="M3DuelClient"/>.
    ///
    /// Gameplay state decides whether input is live; cursor capture is only a consequence of that
    /// decision. This replaces the previous "is the cursor locked?" inference, which could leave the
    /// player unable to look/move (or leave the cursor visible) whenever the two disagreed.
    /// </summary>
    public static class M3LocalInput
    {
        /// <summary>True when mouse/keyboard should drive the local player's gameplay input.</summary>
        public static bool GameplayActive;

        /// <summary>Mouse sensitivity in degrees per mouse-count, mirrored from player settings.</summary>
        public static float MouseSensitivity = 0.12f;

        /// <summary>Whether the local player currently wants the cursor captured (for diagnostics).</summary>
        public static bool CursorCaptured;

        /// <summary>
        /// Pending yaw/pitch look injection in degrees, consumed by the local input path on the next
        /// frame. Development/QA only: lets tooling drive the exact input→sim→camera path without a
        /// physical mouse (the Input System's per-frame Mouse.delta cannot be driven by simulated
        /// pointer events across frames).
        /// </summary>
        public static float InjectedLookYaw;
        public static float InjectedLookPitch;

        /// <summary>Read and clear the pending look injection.</summary>
        public static void ConsumeInjectedLook(out float yaw, out float pitch)
        {
            yaw = InjectedLookYaw;
            pitch = InjectedLookPitch;
            InjectedLookYaw = 0f;
            InjectedLookPitch = 0f;
        }
    }
}
