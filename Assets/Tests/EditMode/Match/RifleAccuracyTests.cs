using BeMyArms.Networking;
using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.Match.Tests
{
    public class RifleAccuracyTests
    {
        static BodyState Standing()
        {
            var sim = new BodySim(); sim.Initialize(0); return sim.State;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecoveredStationaryFirstRoundHasExactlyZeroBallisticError(bool crouch)
        {
            var state = Standing(); state.Crouching = crouch;
            float spread = RifleHandling.SpreadDegrees(1, state);
            Assert.AreEqual(0, spread);
            for (uint seed = 0; seed < 128; seed++)
            {
                RifleHandling.Spread(seed + 1, seed, spread, out float yaw, out float pitch);
                Assert.AreEqual(0, yaw); Assert.AreEqual(0, pitch);
            }
        }

        [Test]
        public void ActualSpeedOrdersCrouchWalkAndSprintWithoutUsingMovementLabels()
        {
            var state = Standing();
            state.PlanarSpeed = 4.5f;
            float walk = RifleHandling.SpreadDegrees(1, state);
            Assert.AreEqual(RifleHandling.WalkSpreadDegrees, walk, .0001f);
            state.PlanarSpeed = 7;
            float sprint = RifleHandling.SpreadDegrees(1, state);
            Assert.AreEqual(RifleHandling.SprintSpreadDegrees, sprint, .0001f);
            state.Crouching = true; state.PlanarSpeed = 2.5f;
            float crouch = RifleHandling.SpreadDegrees(1, state);
            Assert.AreEqual(RifleHandling.CrouchMoveSpreadDegrees, crouch, .0001f);
            Assert.Less(crouch, walk); Assert.Less(walk, sprint);
        }

        [TestCase(BodyMovementState.Jump, 3.5f)]
        [TestCase(BodyMovementState.Fall, 3.5f)]
        [TestCase(BodyMovementState.Dodge, 3.5f)]
        [TestCase(BodyMovementState.Slide, 3f)]
        [TestCase(BodyMovementState.KickLight, 1.5f)]
        [TestCase(BodyMovementState.KickHeavy, 4f)]
        public void ActionsHaveIndependentPenaltiesEvenAtZeroTranslation(BodyMovementState action, float expected)
        {
            var state = Standing(); state.MovementState = (byte)action;
            Assert.AreEqual(expected, RifleHandling.SpreadDegrees(1, state));
        }

        [Test]
        public void AirborneApexIsNotAnAccurateStandingShot()
        {
            var state = Standing(); state.Grounded = false; state.VerticalVelocity = 0;
            Assert.AreEqual(RifleHandling.AirSpreadDegrees, RifleHandling.SpreadDegrees(1, state));
        }

        [Test]
        public void StoppingDoesNotClearBurstBloomAndPauseDoesNotClearMovement()
        {
            var rifle = new RifleHandling(); var moving = Standing(); moving.PlanarSpeed = 7;
            for (int i = 0; i < 10; i++) rifle.Shot(i * .125);
            var stopped = Standing();
            Assert.Greater(RifleHandling.SpreadDegrees(rifle.Shot(1.25), stopped), 1f);
            Assert.AreEqual(1, rifle.Shot(2));
            Assert.AreEqual(RifleHandling.SprintSpreadDegrees, RifleHandling.SpreadDegrees(rifle.Burst, moving));
            rifle.Reset(); Assert.AreEqual(0, RifleHandling.SpreadDegrees(rifle.Shot(3), stopped));
        }

        [Test]
        public void SpreadRaysRemainDeterministicAndMovementCannotMultiplyZeroBloomAway()
        {
            var state = Standing(); state.PlanarSpeed = 7;
            float spread = RifleHandling.SpreadDegrees(1, state);
            float sum = 0;
            for (uint i = 0; i < 256; i++)
            {
                RifleHandling.Spread(i, 34, spread, out float yaw, out float pitch);
                RifleHandling.Spread(i, 34, spread, out float againYaw, out float againPitch);
                Assert.AreEqual(yaw, againYaw); Assert.AreEqual(pitch, againPitch);
                float error = new Vector2(yaw, pitch).magnitude;
                Assert.LessOrEqual(error, spread); sum += error;
            }
            Assert.Greater(sum / 256f, 1f);
        }

        [Test]
        public void MovementStartStopAndBlockedWallUseOrdinaryAuthoritativeSimulation()
        {
            var sim = new BodySim(); sim.Initialize(0);
            sim.ApplyP1(new P1Input { MoveZ = 1 }, 1f / 60);
            Assert.AreEqual(RifleHandling.WalkSpreadDegrees, RifleHandling.SpreadDegrees(1, sim.State), .001f);
            sim.ApplyP1(new P1Input { MoveZ = 1, Sprint = true }, 1f / 60);
            Assert.AreEqual(RifleHandling.SprintSpreadDegrees, RifleHandling.SpreadDegrees(1, sim.State), .001f);
            sim.ApplyP1(default, 1f / 60);
            Assert.AreEqual(0, RifleHandling.SpreadDegrees(1, sim.State));
            var map = new MovementCollision(); map.AddBox(-2, 0, .4f, 2, 3, 1);
            sim.Collision = map; sim.Initialize(0);
            sim.ApplyP1(new P1Input { MoveZ = 1, Sprint = true }, 1f / 60);
            Assert.AreEqual(0, sim.State.PlanarSpeed, .001f);
            Assert.AreEqual(0, RifleHandling.SpreadDegrees(1, sim.State), "Holding sprint into a wall is not actual body translation.");
        }
    }
}
