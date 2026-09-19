using BeMyArms.M1;
using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.M1.Tests
{
    /// <summary>Deterministic M1 checks: accuracy mapping, aim coupling, weapon state machine.</summary>
    public class M1SimulationTests
    {
        // ---- Accuracy ----

        [Test]
        public void DefaultAccuracy_StandingIsBest_HeavyKickIsWorst()
        {
            float idle = AccuracyModel.DefaultSpreadMultiplier(MovementState.Idle);
            float walk = AccuracyModel.DefaultSpreadMultiplier(MovementState.Walk);
            float sprint = AccuracyModel.DefaultSpreadMultiplier(MovementState.Sprint);
            float jump = AccuracyModel.DefaultSpreadMultiplier(MovementState.Jump);
            float heavy = AccuracyModel.DefaultSpreadMultiplier(MovementState.KickHeavy);

            Assert.Less(idle, walk);
            Assert.Less(walk, sprint);
            Assert.Less(sprint, heavy);
            Assert.LessOrEqual(jump, heavy);
        }

        [Test]
        public void AccuracyRows_OverrideDefaults()
        {
            var rows = new[]
            {
                new AccuracyRow { state = MovementState.Idle, spreadMultiplier = 0.5f },
                new AccuracyRow { state = MovementState.Sprint, spreadMultiplier = 9f }
            };
            Assert.AreEqual(0.5f, AccuracyModel.SpreadMultiplier(MovementState.Idle, rows), 0.0001f);
            Assert.AreEqual(9f, AccuracyModel.SpreadMultiplier(MovementState.Sprint, rows), 0.0001f);
            // Falls back to default for unlisted states.
            Assert.AreEqual(AccuracyModel.DefaultSpreadMultiplier(MovementState.Vault),
                AccuracyModel.SpreadMultiplier(MovementState.Vault, rows), 0.0001f);
        }

        // ---- Aim rig (Model C coupling, reused from the M0 spike) ----

        [Test]
        public void AimRig_IsWorldStableInsideSector()
        {
            var go = new GameObject("aimRig");
            var rig = go.AddComponent<P2AimRig>();
            rig.SectorHalfDegrees = 70f;
            rig.Initialize(0f);

            rig.Step(new P2Command { AimYawDelta = 30f }, 0f);
            Assert.AreEqual(30f, rig.DesiredWorldYaw, 0.001f);

            // P1 rotates; the aim stays world-stable while inside the sector.
            rig.Step(default, 20f);
            Assert.AreEqual(30f, rig.DesiredWorldYaw, 0.001f);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AimRig_BodyRotationPushesAtBoundary_NoPhantomOffset()
        {
            var go = new GameObject("aimRig");
            var rig = go.AddComponent<P2AimRig>();
            rig.SectorHalfDegrees = 70f;
            rig.Initialize(0f);

            rig.Step(new P2Command { AimYawDelta = 120f }, 0f);
            Assert.AreEqual(70f, rig.DesiredWorldYaw, 0.001f);
            Assert.IsTrue(rig.IsPinned);

            // Push far outward while pinned, then a small inward move must respond immediately.
            rig.Step(new P2Command { AimYawDelta = 90f }, 0f);
            Assert.AreEqual(70f, rig.DesiredWorldYaw, 0.001f);
            rig.Step(new P2Command { AimYawDelta = -5f }, 0f);
            Assert.AreEqual(65f, rig.DesiredWorldYaw, 0.001f);

            Object.DestroyImmediate(go);
        }

        // ---- Weapons ----

        static WeaponController CreateWeapon(out M1Tuning tuning)
        {
            tuning = ScriptableObject.CreateInstance<M1Tuning>();
            tuning.weapons = new[]
            {
                new WeaponDefinition { displayName = "Rifle", kind = WeaponKind.Rifle, damage = 18f, magazine = 30, roundsPerMinute = 600f, baseSpreadDegrees = 1f },
                new WeaponDefinition { displayName = "Pistol", kind = WeaponKind.Pistol, damage = 24f, magazine = 12, roundsPerMinute = 300f, baseSpreadDegrees = 1f },
                new WeaponDefinition { displayName = "Knife", kind = WeaponKind.Knife, damage = 40f, magazine = 1, roundsPerMinute = 120f, baseSpreadDegrees = 0f }
            };
            tuning.startingWeaponIndex = 0;

            var go = new GameObject("weapon");
            var controller = go.AddComponent<WeaponController>();
            controller.Configure(tuning, null, null);
            return controller;
        }

        [Test]
        public void Weapon_StartsLoadedAndCanSwap()
        {
            WeaponController weapon = CreateWeapon(out M1Tuning tuning);
            Assert.AreEqual("Rifle", weapon.Current.displayName);
            Assert.AreEqual(30, weapon.CurrentAmmo);

            weapon.Step(new P2Command { SwitchRequested = true, SwitchWeapon = 1 }, 0f, MovementState.Idle, 0.016f);
            Assert.AreEqual("Pistol", weapon.Current.displayName);
            Assert.AreEqual(12, weapon.CurrentAmmo);

            Object.DestroyImmediate(weapon.gameObject);
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void Weapon_FireConsumesAmmo_AndReloadStarts()
        {
            WeaponController weapon = CreateWeapon(out M1Tuning tuning);

            weapon.Step(new P2Command { Fire = true }, 0f, MovementState.Idle, 0.016f);
            Assert.AreEqual(29, weapon.CurrentAmmo);

            weapon.Step(new P2Command { Reload = true }, 0f, MovementState.Idle, 0.016f);
            Assert.IsTrue(weapon.IsReloading);

            Object.DestroyImmediate(weapon.gameObject);
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void Weapon_PostureChangesSpread_ButNeverBlocksFiring()
        {
            WeaponController weapon = CreateWeapon(out M1Tuning tuning);

            weapon.Step(default, 0f, MovementState.Idle, 0.016f);
            float idleSpread = weapon.CurrentSpreadDegrees;

            weapon.Step(default, 0f, MovementState.Sprint, 0.016f);
            float sprintSpread = weapon.CurrentSpreadDegrees;

            Assert.Greater(sprintSpread, idleSpread, "Sprinting must widen spread.");

            // Firing during a heavy kick is allowed (accuracy penalty only, no lockout).
            int before = weapon.CurrentAmmo;
            weapon.Step(new P2Command { Fire = true }, 0f, MovementState.KickHeavy, 0.016f);
            Assert.AreEqual(before - 1, weapon.CurrentAmmo, "P2 must be able to fire during a P1 kick.");

            Object.DestroyImmediate(weapon.gameObject);
            Object.DestroyImmediate(tuning);
        }
    }
}
