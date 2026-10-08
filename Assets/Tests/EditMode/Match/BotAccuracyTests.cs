using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.Match.Tests
{
    /// <summary>
    /// Verifies that the Easy bot's per-shot offset model actually produces the intended hit rate
    /// through the real authoritative ray/segment geometry (<see cref="NetworkBody.RaySegmentDistance"/>),
    /// and that misses stay in a natural cluster rather than spraying in unrelated directions.
    /// </summary>
    public class BotAccuracyTests
    {
        [Test]
        public void EasyAccuracy_ObservedHitRateIsAboutTenPercent()
        {
            const float targetRadius = 0.6f;
            const float hitRadius = targetRadius + 0.15f;
            const float accuracy = 0.10f;

            var rng = new System.Random(20260927);
            float[] distances = { 8f, 14f, 22f, 34f };
            int hits = 0, shots = 0;
            double missSum = 0;
            int misses = 0;

            foreach (float d in distances)
            {
                for (int i = 0; i < 4000; i++)
                {
                    Vector3 origin = new Vector3(0f, 1.45f, 0f);
                    const float feet = 0f;
                    const float height = 1.8f;
                    Vector3 chest = new Vector3(0f, feet + height * 0.55f, d);

                    BotAim.SampleShotOffset(accuracy, hitRadius,
                        rng.NextDouble(), rng.NextDouble(), rng.NextDouble(),
                        out float right, out float up);

                    Vector3 aimPoint = chest + new Vector3(1f, 0f, 0f) * right + Vector3.up * up;
                    Vector3 to = aimPoint - origin;
                    float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Atan2(to.y, Mathf.Sqrt(to.x * to.x + to.z * to.z)) * Mathf.Rad2Deg;
                    Vector3 dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

                    float forward = NetworkBody.RaySegmentDistance(origin, dir, 0f, feet, d, height, 60f, out float lateral);
                    bool hit = forward > 0f && lateral <= hitRadius;
                    shots++;
                    if (hit) hits++;
                    else { missSum += lateral; misses++; }
                }
            }

            float rate = hits / (float)shots;
            float avgMiss = misses > 0 ? (float)(missSum / misses) : 0f;
            TestContext.Progress.WriteLine($"[botacc] hits={hits}/{shots} rate={rate:F3} avgMiss={avgMiss:F2}m");
            Assert.That(rate, Is.InRange(0.06f, 0.15f), "Easy bot must average roughly 10% hits through the real hitscan geometry.");
            Assert.Less(avgMiss, 2.5f, "Misses must stay in a natural cluster around the target.");
        }
    }
}
