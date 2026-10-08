namespace BeMyArms.Match
{
    /// <summary>
    /// Server-side bot behaviour profile. Private bot-filled matches default to <see cref="Easy"/>.
    /// Both use geometry-aware positioning and smooth aim. Easy has slower acquisition, longer
    /// reaction and forgiving burst destinations; Hard tracks faster with smaller aim drift.
    /// </summary>
    public enum BotDifficulty
    {
        Easy = 0,
        Hard = 1
    }
}
