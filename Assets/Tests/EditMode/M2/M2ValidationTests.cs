using BeMyArms.M2;
using NUnit.Framework;

namespace BeMyArms.M2.Tests
{
    /// <summary>Server validation, lag compensation and the delay/loss conditioner.</summary>
    public class M2ValidationTests
    {
        [Test]
        public void Weapon_EnforcesCadenceAmmoAndReload()
        {
            var weapon = new M2WeaponState { SecondsBetweenShots = 0.5f };
            weapon.Magazine = 2;
            weapon.Reset();

            Assert.AreEqual(M2WeaponState.FireResult.Ok, weapon.TryFire(0.0));
            Assert.AreEqual(1, weapon.Ammo);
            Assert.AreEqual(M2WeaponState.FireResult.NotReady, weapon.TryFire(0.25), "Cadence must reject a too-fast second shot.");
            Assert.AreEqual(M2WeaponState.FireResult.Ok, weapon.TryFire(0.5));
            Assert.AreEqual(M2WeaponState.FireResult.Empty, weapon.TryFire(1.0), "Empty magazine must reject fire.");

            weapon.StartReload(1.0);
            Assert.AreEqual(M2WeaponState.FireResult.Reloading, weapon.TryFire(1.1));
            weapon.Tick(1.0 + weapon.ReloadSeconds);
            Assert.AreEqual(2, weapon.Ammo, "Reload must refill the magazine.");
        }

        [Test]
        public void LagComp_RewindsBodyYawAndTarget_AndClampsWindow()
        {
            var lag = new M2LagCompensation { MaxRewindSeconds = 1.0f };
            lag.Record(10.0, 0f, 0f, 10f);
            lag.Record(10.1, 30f, 1f, 10f);
            lag.Record(10.2, 60f, 2f, 10f);

            Assert.IsTrue(lag.TryRewind(10.1, out float yaw, out float tx, out float tz));
            Assert.AreEqual(30f, yaw, 1e-3f);
            Assert.AreEqual(1f, tx, 1e-3f);

            // Rewinding before the window start clamps to the oldest retained sample.
            Assert.IsTrue(lag.TryRewind(5.0, out float clampedYaw, out _, out _));
            Assert.AreEqual(0f, clampedYaw, 1e-3f);

            // Rewinding into the future clamps to the newest sample.
            Assert.IsTrue(lag.TryRewind(99.0, out float newestYaw, out _, out _));
            Assert.AreEqual(60f, newestYaw, 1e-3f);
        }

        [Test]
        public void DelayQueue_DelaysAndDrops()
        {
            var queue = new M2DelayQueue<int> { LossPercent = 0f };
            queue.Enqueue(0.0, 0.1, 42);

            Assert.IsFalse(queue.TryDequeue(0.05, out _), "Message must not be ready before the delay.");
            Assert.IsTrue(queue.TryDequeue(0.2, out int value));
            Assert.AreEqual(42, value);

            var lossy = new M2DelayQueue<int> { LossPercent = 100f };
            lossy.Enqueue(0.0, 0.0, 7);
            Assert.IsFalse(lossy.TryDequeue(1.0, out _), "100% loss must drop everything.");
        }
    }
}
