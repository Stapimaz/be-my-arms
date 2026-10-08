using BeMyArms.Core;
using BeMyArms.Networking;
using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.Match.Tests
{
    public class CombatHitTests
    {
        static BodyState Standing(float z = 10f)
            => new BodyState { PosZ = z, HitHeight = 1.8f, EyeHeight = 1.45f, Health = 100, ControlEpoch = 7 };

        [TestCase(0.5f)]
        [TestCase(0.9f)]
        [TestCase(1f)]
        public void BodyHitsReturnSurfaceEntryAndExactPoint(float y)
        {
            var target = Standing();
            Assert.IsTrue(CombatHitGeometry.Raycast(new Vector3(0, y, 0), Vector3.forward, target, 20f, out CombatHit hit));
            Assert.AreEqual(HitboxRegion.Region.Body, hit.Region);
            Assert.AreEqual(10f - CombatHitGeometry.BodyRadius, hit.Distance, 1e-4f);
            Assert.AreEqual(new Vector3(0, y, hit.Distance), hit.Point);
        }

        [Test]
        public void ExposedHeadIsCriticalButShoulderHeightIsNot()
        {
            var target = Standing();
            Vector3 head = CombatHitGeometry.HeadCenter(target);
            Assert.IsTrue(CombatHitGeometry.Raycast(new Vector3(0, head.y, 0), Vector3.forward, target, 20, out CombatHit critical));
            Assert.AreEqual(HitboxRegion.Region.Head, critical.Region);
            Assert.AreEqual(10f - CombatHitGeometry.HeadRadius, critical.Distance, 1e-4f);
            Assert.IsTrue(CombatHitGeometry.Raycast(new Vector3(.12f, 1.2f, 0), Vector3.forward, target, 20, out CombatHit ordinary));
            Assert.AreEqual(HitboxRegion.Region.Body, ordinary.Region);
        }

        [TestCase(0f, 1.82f)]
        [TestCase(0f, -.02f)]
        [TestCase(.4f, 1f)]
        [TestCase(.21f, 1.62f)]
        public void OutsideVisibleProfileDoesNotHit(float x, float y)
            => Assert.IsFalse(CombatHitGeometry.Raycast(new Vector3(x, y, 0), Vector3.forward, Standing(), 20, out _));

        [Test]
        public void VerticalAndDiagonalRaysUseTheSameHeadSurface()
        {
            var target = Standing();
            Assert.IsTrue(CombatHitGeometry.Raycast(new Vector3(0, 3, 10), Vector3.down, target, 20, out CombatHit above));
            Assert.AreEqual(HitboxRegion.Region.Head, above.Region);
            Assert.AreEqual(1.8f, above.Point.y, 1e-4f);
            Vector3 origin = new Vector3(0, 2f, 0);
            Assert.IsTrue(CombatHitGeometry.Raycast(origin, CombatHitGeometry.HeadCenter(target) - origin, target, 20, out CombatHit diagonal));
            Assert.AreEqual(HitboxRegion.Region.Head, diagonal.Region);
        }

        [Test]
        public void CrouchHasNoStandingGhostHeadAndFollowsBodyFacing()
        {
            var target = Standing();
            target.Crouching = true;
            target.HitHeight = 1.15f;
            Assert.IsFalse(CombatHitGeometry.Raycast(new Vector3(0, 1.62f, 0), Vector3.forward, target, 20, out _));
            Vector3 center = CombatHitGeometry.HeadCenter(target);
            Assert.AreEqual(10f + CombatHitGeometry.CrouchHeadForward, center.z, 1e-4f);
            Assert.IsTrue(CombatHitGeometry.Raycast(new Vector3(0, center.y, 0), Vector3.forward, target, 20, out CombatHit low));
            Assert.AreEqual(HitboxRegion.Region.Head, low.Region);
            target.BodyYaw = 90;
            Assert.AreEqual(CombatHitGeometry.CrouchHeadForward, CombatHitGeometry.HeadCenter(target).x, 1e-4f);
        }

        [Test]
        public void ElevatedTargetDoesNotLeaveAGroundLevelGhost()
        {
            var target = Standing(); target.PosY = 2;
            Assert.IsFalse(CombatHitGeometry.Raycast(new Vector3(0, 1f, 0), Vector3.forward, target, 20, out _));
            Assert.IsTrue(CombatHitGeometry.Raycast(new Vector3(0, 3f, 0), Vector3.forward, target, 20, out CombatHit hit));
            Assert.AreEqual(HitboxRegion.Region.Body, hit.Region);
        }

        [Test]
        public void RangeEndsAtTheSurfaceNotTheTargetsCenter()
        {
            Assert.IsTrue(CombatHitGeometry.Raycast(new Vector3(0, 1f, 0), Vector3.forward, Standing(), 9.66f, out _));
            Assert.IsFalse(CombatHitGeometry.Raycast(new Vector3(0, 1f, 0), Vector3.forward, Standing(), 9.64f, out _));
            Assert.IsFalse(CombatHitGeometry.Raycast(Vector3.zero, Vector3.zero, Standing(), 20, out _));
            Assert.IsFalse(CombatHitGeometry.Raycast(Vector3.zero, Vector3.back, Standing(), 20, out _));
        }

        [Test]
        public void InsideCapsuleIsAContactHitAndTangentIsStable()
        {
            var target = Standing();
            Assert.IsTrue(CombatHitGeometry.Raycast(new Vector3(0, 1f, 10), Vector3.up, target, 20, out CombatHit contact));
            Assert.AreEqual(0, contact.Distance);
            Assert.IsTrue(CombatHitGeometry.Raycast(new Vector3(CombatHitGeometry.BodyRadius, 1f, 0), Vector3.forward, target, 20, out CombatHit tangent));
            Assert.AreEqual(10, tangent.Distance, 1e-3f);
        }

        [Test]
        public void HistoryRewindsVerticalStanceFacingAndLifeTogether()
        {
            var history = new LagCompensation();
            var before = Standing(); before.PosY = 2f; before.BodyYaw = 30f;
            history.Record(1, 15, before, true);
            var after = before; after.PosY = 0; after.Crouching = true; after.HitHeight = 1.15f; after.ControlEpoch++;
            history.Record(1.2, 25, after, false);
            Assert.IsTrue(history.TryRewindPose(1.1, out float yaw, out BodyState pose, out bool alive));
            Assert.AreEqual(15, yaw); Assert.IsTrue(alive);
            Assert.AreEqual(2, pose.PosY); Assert.AreEqual(1.8f, pose.HitHeight); Assert.AreEqual(30, pose.BodyYaw);
            Assert.AreEqual(7, pose.ControlEpoch); Assert.IsFalse(pose.Crouching);
            Assert.IsTrue(history.TryRewindPose(1.2, out _, out pose, out alive));
            Assert.IsFalse(alive); Assert.IsTrue(pose.Crouching); Assert.AreEqual(8, pose.ControlEpoch);
            history.Clear(); Assert.IsFalse(history.TryRewindPose(1, out _, out pose, out alive));
        }

        [Test]
        public void RifleRewardsHeadAimWithoutIncreasingBodyDamage()
        {
            WeaponStats rifle = Loadouts.Stats(WeaponType.Rifle);
            Assert.AreEqual(18, rifle.DamageFor(HitboxRegion.Region.Body));
            Assert.AreEqual(45, rifle.DamageFor(HitboxRegion.Region.Head));
            Assert.AreEqual(6, Mathf.CeilToInt(100 / rifle.DamageFor(HitboxRegion.Region.Body)));
            Assert.AreEqual(3, Mathf.CeilToInt(100 / rifle.DamageFor(HitboxRegion.Region.Head)));
            Assert.AreEqual(24, Loadouts.Stats(WeaponType.Pistol).DamageFor(HitboxRegion.Region.Head), "Non-rifle balance is not part of this iteration.");
        }

        [Test]
        public void ConfirmationBelongsToBothRolesOfOnlyTheCurrentSharedBody()
        {
            var e = new DamageEvent { AttackerTeam = 0, AttackerBody = 1, AttackerEpoch = 4,
                VictimTeam = 1, VictimBody = 0, VictimEpoch = 8, Amount = 18, Kind = DamageKind.Weapon };
            Assert.IsTrue(e.ConfirmsFor(0, 1, 4));
            Assert.IsFalse(e.ConfirmsFor(0, 0, 4));
            Assert.IsFalse(e.ConfirmsFor(0, 1, 3));
            Assert.IsTrue(e.Hurts(1, 0, 8));
            Assert.IsFalse(e.Hurts(1, 0, 7));
            e.Kind = DamageKind.World; Assert.IsFalse(e.ConfirmsFor(0, 1, 4));
            e.Amount = 0; Assert.IsFalse(e.Hurts(1, 0, 8));
        }
    }
}
