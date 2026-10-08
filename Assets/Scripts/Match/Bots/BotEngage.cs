namespace BeMyArms.Match
{
    /// <summary>
    /// Pure engagement-distance state for an Easy P1 bot: returns +1 to approach, -1 to back away and
    /// 0 to hold. The bot closes until it enters the band around its preferred range and backs away
    /// until it re-enters it, with a latched state providing hysteresis so it does not flip direction
    /// near the thresholds. Two bots therefore settle into a firefight at a range inside their bands
    /// instead of walking through each other.
    /// </summary>
    public static class BotEngage
    {
        public const float DefaultBand = 2.5f;

        public static int Step(int state, float distance, float preferredRange, float band)
        {
            float b = band > 0f ? band : 0f;
            if (state > 0) return distance <= preferredRange + b ? 0 : 1;  // stop once inside the band
            if (state < 0) return distance >= preferredRange - b ? 0 : -1; // stop once back inside it
            if (distance > preferredRange + b) return 1;
            if (distance < preferredRange - b) return -1;
            return 0;
        }
    }
}
