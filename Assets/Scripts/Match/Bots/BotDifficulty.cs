namespace BeMyArms.Match
{
    /// <summary>
    /// Server-side bot behaviour profile. Private bot-filled matches default to <see cref="Easy"/>.
    /// Hard preserves the original evasive/drifting bot; Easy is a calmer practice partner with a
    /// controlled real hit rate.
    /// </summary>
    public enum BotDifficulty
    {
        Easy = 0,
        Hard = 1
    }
}
