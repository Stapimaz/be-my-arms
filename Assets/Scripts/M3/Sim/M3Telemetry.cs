using System.Collections.Generic;

namespace BeMyArms.M3
{
    /// <summary>
    /// Playtest telemetry (concept §31 subset): round length and time to first contact, so tuning
    /// questions are answered with data rather than theory. Pure and testable.
    /// </summary>
    public class M3Telemetry
    {
        public readonly List<float> RoundLengths = new List<float>();
        public readonly List<float> TimeToFirstContact = new List<float>();

        public int RoundsPlayed => RoundLengths.Count;

        public void RecordRound(float seconds) => RoundLengths.Add(seconds);

        public void RecordFirstContact(float seconds) => TimeToFirstContact.Add(seconds);

        public float AverageRoundSeconds() => Average(RoundLengths);

        public float AverageTimeToFirstContact() => Average(TimeToFirstContact);

        static float Average(List<float> values)
        {
            if (values.Count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < values.Count; i++) sum += values[i];
            return sum / values.Count;
        }
    }
}
