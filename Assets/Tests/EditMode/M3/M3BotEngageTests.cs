using NUnit.Framework;

namespace BeMyArms.M3.Tests
{
    /// <summary>
    /// Verifies the Easy bot engagement-distance hysteresis: it closes into its preferred band, holds
    /// without flipping, backs off when crowded, and — critically — two bots settle at a firefight
    /// range instead of walking through each other.
    /// </summary>
    public class M3BotEngageTests
    {
        const float Pref = 11f, Band = 2f; // band [9, 13]

        [Test]
        public void FarApproachesThenStopsInsideTheBand()
        {
            int s = M3BotEngage.Step(0, 25f, Pref, Band);
            Assert.AreEqual(1, s, "Far outside the band must approach.");
            s = M3BotEngage.Step(s, 13.5f, Pref, Band);
            Assert.AreEqual(1, s, "Still outside the band keeps closing.");
            s = M3BotEngage.Step(s, 12.8f, Pref, Band);
            Assert.AreEqual(0, s, "Stops once inside the band.");
            Assert.AreEqual(0, M3BotEngage.Step(0, 12.5f, Pref, Band), "Inside the band stays calm.");
            Assert.AreEqual(0, M3BotEngage.Step(0, 9.5f, Pref, Band), "Inside the band stays calm.");
        }

        [Test]
        public void TooCloseBacksOffThenStops()
        {
            int s = M3BotEngage.Step(0, 5f, Pref, Band);
            Assert.AreEqual(-1, s, "Clearly too close must back away.");
            s = M3BotEngage.Step(s, 8.8f, Pref, Band);
            Assert.AreEqual(-1, s, "Keeps backing off until it re-enters the band.");
            s = M3BotEngage.Step(s, 9.2f, Pref, Band);
            Assert.AreEqual(0, s, "Stops once back inside the band.");
        }

        [Test]
        public void ThresholdsHaveHysteresis()
        {
            Assert.AreEqual(0, M3BotEngage.Step(0, Pref + Band - 0.2f, Pref, Band), "Small drift does not command.");
            Assert.AreEqual(0, M3BotEngage.Step(0, Pref - Band + 0.2f, Pref, Band), "Small drift does not command.");
            Assert.AreEqual(1, M3BotEngage.Step(0, Pref + Band + 0.2f, Pref, Band), "Clear crossing commands approach.");
            Assert.AreEqual(-1, M3BotEngage.Step(0, Pref - Band - 0.2f, Pref, Band), "Clear crossing commands retreat.");
        }

        [Test]
        public void TwoBotsSettleInsteadOfChargingThrough()
        {
            const float speed = 4.5f, dt = 1f / 60f;
            var pairs = new[] { (9.5f, 13.5f), (9.5f, 11f), (11f, 13.5f), (13.5f, 9.5f) };
            foreach (var pair in pairs)
            {
                float d = 25f;
                int sA = 0, sB = 0;
                for (int i = 0; i < 60 * 30; i++)
                {
                    sA = M3BotEngage.Step(sA, d, pair.Item1, M3BotEngage.DefaultBand);
                    sB = M3BotEngage.Step(sB, d, pair.Item2, M3BotEngage.DefaultBand);
                    d -= (sA + sB) * speed * dt;
                    if (d < 0f) d = 0f;
                }
                Assert.AreEqual(0, sA, $"bot A must hold at {pair.Item1}/{pair.Item2} (d={d:F2})");
                Assert.AreEqual(0, sB, $"bot B must hold at {pair.Item1}/{pair.Item2} (d={d:F2})");
                Assert.Greater(d, 7f, $"bots must not overlap at {pair.Item1}/{pair.Item2} (d={d:F2})");
            }
        }
    }
}
