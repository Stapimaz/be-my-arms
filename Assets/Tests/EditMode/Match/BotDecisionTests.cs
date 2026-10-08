using System.Collections.Generic;
using BeMyArms.Networking;
using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.Match.Tests
{
    public class BotDecisionTests
    {
        static BodyState Pose(float x = 0f, float z = 0f)
        {
            var sim = new BodySim(); sim.Initialize(0, x, z); return sim.State;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AimTargetsNeverTeleportOnAcquisitionBurstOrReversal(bool easy)
        {
            var motion = new BotAimMotion(); float yaw = 0, pitch = 0;
            float limit = easy ? 100f : 160f;
            foreach (float dt in new[] { 1f / 60f, 1f / 30f, 1f / 120f })
                for (int tick = 0; tick < 240; tick++)
                {
                    float goal = tick < 80 ? 65f : tick < 160 ? -65f : 30f;
                    motion.Step(yaw, pitch, goal, tick < 120 ? 25 : -25, 0, 85, 80, dt, easy, out float y, out float p);
                    Assert.LessOrEqual(new Vector2(BodySim.Normalize(y - yaw), p - pitch).magnitude, limit * dt + .0001f);
                    Assert.LessOrEqual(Mathf.Abs(y), 85f); Assert.LessOrEqual(Mathf.Abs(p), 80f);
                    yaw = y; pitch = p;
                }
        }

        [Test]
        public void AimBrakingAndNearbyGoalChangesRespectAccelerationToo()
        {
            var motion = new BotAimMotion(); float yaw = 0, pitch = 0; Vector2 velocity = Vector2.zero;
            float goal = 60;
            for (int i = 0; i < 180; i++)
            {
                if (i == 20) goal = yaw + .01f;
                float dt = 1f / 60;
                motion.Step(yaw, pitch, goal, 10, 0, 85, 80, dt, true, out float y, out float p);
                Vector2 next = new Vector2(BodySim.Normalize(y - yaw), p - pitch) / dt;
                Assert.LessOrEqual((next - velocity).magnitude, 420f * dt + .02f);
                yaw = y; pitch = p; velocity = next;
            }
            Assert.AreEqual(goal, yaw, .01f);
        }

        [Test]
        public void AimWrapsAndSettlesWithoutSpinningOrSnapping()
        {
            var motion = new BotAimMotion(); float yaw = 179, pitch = 0;
            for (int i = 0; i < 180; i++)
            {
                motion.Step(yaw, pitch, -178, 10, 180, 85, 80, 1f / 60f, true, out yaw, out pitch);
                Assert.Less(Mathf.Abs(BodySim.Normalize(yaw - 179)), 4f);
            }
            Assert.AreEqual(-178, yaw, .01f); Assert.AreEqual(10, pitch, .01f);
            motion.Reset(); motion.Step(yaw, pitch, 180, 0, 180, 85, 80, 1f / 60, true, out float next, out _);
            Assert.Less(Mathf.Abs(BodySim.Normalize(next - yaw)), .2f);
        }

        [TestCase(0f, 0f)]
        [TestCase(73f, -41f)]
        public void NavigationRoutesAroundWallsWithoutArenaCoordinates(float x, float z)
        {
            var map = new MovementCollision(); map.AddBox(x - 2, 0, z + 3, x + 2, 3, z + 3.3f);
            var nav = new BotNavigation(); Vector3 start = new Vector3(x, 0, z); nav.Search(map, start);
            int goal = -1;
            foreach (int i in nav.Reachable)
                if (Vector3.Distance(nav[i].Position, start + Vector3.forward * 6) < .1f) goal = i;
            Assert.GreaterOrEqual(goal, 0, "A reachable route must go around the wall, not stop at it.");
            var route = new List<Vector3>(); nav.Route(goal, route);
            Assert.Greater(nav[goal].Cost, 6);
            for (int i = 1; i < route.Count; i++) Assert.IsTrue(nav.Travel(route[i - 1], route[i], out _, out _));
            Assert.IsFalse(nav.Travel(start, start + Vector3.forward * 6, out _, out _));
        }

        [Test]
        public void WholeCorridorRejectsCornerCutsAndBounds()
        {
            var map = new MovementCollision { HasBounds = true, MinX = -4, MaxX = 4, MinZ = -4, MaxZ = 4 };
            map.AddBox(.5f, 0, .5f, 2, 3, 2);
            var nav = new BotNavigation(); nav.Search(map, Vector3.zero);
            Assert.IsFalse(nav.Travel(Vector3.zero, new Vector3(3, 0, 3), out _, out _));
            Assert.IsFalse(nav.Travel(Vector3.zero, new Vector3(-5, 0, 0), out _, out _));
            foreach (int i in nav.Reachable) Assert.IsTrue(nav.Clear(nav[i].Position, 1.15f));
        }

        [Test]
        public void LowPassageRequiresCrouchInsteadOfJumpingAtACeiling()
        {
            var map = new MovementCollision(); map.AddBox(-1, 1.3f, 1, 1, 2.5f, 4);
            var nav = new BotNavigation(); nav.Search(map, Vector3.zero);
            Assert.IsTrue(nav.Travel(Vector3.zero, new Vector3(0, 0, 5), out _, out bool crouch));
            Assert.IsTrue(crouch);
            Assert.IsFalse(nav.Clear(new Vector3(0, 0, 2), 1.8f));
            Assert.IsTrue(nav.Clear(new Vector3(0, 0, 2), 1.15f));
        }

        [Test]
        public void NavigationFollowsRampHeightAndRejectsUnsafeDrops()
        {
            var map = new MovementCollision(); map.AddSurface(-2, 0, 2, 6, 0, 2, 1);
            var nav = new BotNavigation(); nav.Search(map, Vector3.zero);
            Assert.IsTrue(nav.Travel(Vector3.zero, new Vector3(0, 0, 6), out Vector3 end, out _));
            Assert.AreEqual(2, end.y, .001f);
            Assert.IsFalse(nav.Travel(end, new Vector3(0, 2, 8), out _, out _));
        }

        [Test]
        public void ComfortableOpenRangeHoldsInsteadOfChargingOrRandomActions()
        {
            var brain = new BotPositioning(); var state = Pose(); var enemy = Pose(0, 12);
            P1Input input = brain.Step(state, new MovementCollision(), enemy, true, false, false, 12, 100, 5, 1f / 60);
            Assert.AreEqual(0, input.MoveX); Assert.AreEqual(0, input.MoveZ);
            Assert.IsFalse(input.Crouch || input.Slide || input.Jump || input.Dodge);
        }

        [TestCase(0f, 0f)]
        [TestCase(-30f, 45f)]
        public void LowCoverDucksUnderThreatThenGivesPartnerAPeek(float x, float z)
        {
            var map = new MovementCollision(); map.AddBox(x - 2, 0, z + 1, x + 2, 1.25f, z + 1.5f);
            var state = Pose(x, z); var enemy = Pose(x, z + 12); var brain = new BotPositioning();
            P1Input duck = brain.Step(state, map, enemy, true, true, false, 12, 1000, 5, 1f / 60);
            Assert.IsTrue(duck.Crouch); Assert.IsFalse(duck.Slide);
            Assert.IsTrue(brain.Hiding);
            P1Input peek = brain.Step(state, map, enemy, true, true, false, 12, 1000, 6.2f, 1f / 60);
            Assert.IsFalse(peek.Crouch, "Do not stay permanently crouched with the partner unable to fire.");
        }

        [Test]
        public void ReloadUsesUsefulCoverButOpenGroundDoesNotDuckAtRandom()
        {
            var state = Pose(); var enemy = Pose(0, 12); var map = new MovementCollision();
            var open = new BotPositioning();
            Assert.IsFalse(open.Step(state, map, enemy, true, false, true, 12, 100, 5, 1f / 60).Crouch);
            map.AddBox(-2, 0, 1, 2, 1.25f, 1.5f);
            var covered = new BotPositioning();
            Assert.IsTrue(covered.Step(state, map, enemy, true, false, true, 12, 100, 5, 1f / 60).Crouch);
        }

        [Test]
        public void DamageSelectsReachableCoverInsteadOfRunningIntoTheEnemy()
        {
            var map = new MovementCollision(); map.AddBox(3, 0, 1, 7, 1.25f, 1.5f);
            var state = Pose(); var enemy = Pose(0, 12); var brain = new BotPositioning();
            brain.Hurt(5, Vector3.zero);
            P1Input input = brain.Step(state, map, enemy, true, false, false, 12, 100, 5, 1f / 60);
            Assert.Greater(brain.Goal.x, 2);
            Assert.Greater(Vector3.Distance(brain.Goal, BotSight.Feet(enemy)), 6);
            Assert.Greater(input.MoveX, 0);
            Assert.Less(BotSight.Exposure(map, brain.Goal, BotSight.Eye(enemy), true), 1);
        }

        [Test]
        public void SlideRequiresAFullSafeStoppingCorridorAndActualExposureReduction()
        {
            var map = new MovementCollision(); map.AddBox(5.5f, 0, 1, 9, 1.25f, 1.5f);
            var state = Pose(); var enemy = Pose(0, 12); var brain = new BotPositioning();
            brain.Step(state, map, enemy, true, true, false, 12, 100, 5, 1f / 60);
            Assert.IsTrue(brain.CanSlide(state, enemy, Vector3.right * 7), "A straight covered escape can justify sliding.");
            Assert.IsFalse(brain.CanSlide(state, enemy, Vector3.right * 3), "Cannot brake before the end of a short path.");
            Assert.IsFalse(brain.CanSlide(state, enemy, Vector3.left * 7), "No timer-driven slide into open ground.");
            map.AddBox(3, 0, -1, 3.5f, 3, 1);
            Assert.IsFalse(brain.CanSlide(state, enemy, Vector3.right * 7), "Cannot slide through an intervening wall.");
        }

        [Test]
        public void BlockedRouteReplansInsteadOfRetainingAnUnreachableWaypoint()
        {
            var map = new MovementCollision(); var sim = new BodySim { Collision = map }; sim.Initialize(0);
            var enemy = Pose(0, 20); var brain = new BotPositioning();
            brain.Step(sim.State, map, enemy, true, false, false, 12, 100, 5, 1f / 60);
            map.AddBox(-2, 0, 2, 2, 3, 3);
            for (int i = 0; i < 180; i++)
            {
                var input = brain.Step(sim.State, map, enemy, true, false, false, 12, 100, 5 + i / 60f, 1f / 60);
                sim.ApplyP1(input, 1f / 60);
            }
            Assert.Greater(sim.State.PosZ, 3.5f);
        }

        [Test]
        public void ThreatenedBotCanActuallySlideToCoverThroughNormalBodySimulation()
        {
            var map = new MovementCollision(); map.AddBox(8, 0, 1, 13, 1.25f, 1.5f);
            var sim = new BodySim { Collision = map }; sim.Initialize(0);
            var enemy = Pose(0, 12); var brain = new BotPositioning(); brain.Hurt(5, Vector3.zero);
            int slides = 0, crouch = 0;
            for (int i = 0; i < 240; i++)
            {
                var input = brain.Step(sim.State, map, enemy, true, true, false, 12, 100, 5 + i / 60f, 1f / 60);
                if (input.Slide) slides++;
                if (input.Crouch) crouch++;
                sim.ApplyP1(input, 1f / 60);
            }
            Assert.AreEqual(1, slides, "One committed exposed-to-cover slide, not repeated animation spam.");
            Assert.Greater(crouch, 0, "Arrival at low cover creates useful stance choices.");
            Assert.Greater(sim.State.PosX, 6);
            Assert.IsFalse(brain.CanSlide(Pose(), enemy, Vector3.right * 7), "That cover is too far away for a slide from spawn.");
        }

        [Test]
        public void ClosingZoneOutranksComfortableRangeAndResetDropsOldPlan()
        {
            var state = Pose(14); var enemy = Pose(14, 12); var brain = new BotPositioning();
            brain.Step(state, new MovementCollision(), enemy, true, false, false, 12, 10, 5, 1f / 60);
            Assert.Less(new Vector2(brain.Goal.x, brain.Goal.z).magnitude, 10);
            brain.Reset(); Assert.IsFalse(brain.Hiding);
            var fresh = Pose();
            P1Input input = brain.Step(fresh, new MovementCollision(), Pose(0, 12), true, false, false, 12, 100, 6, 1f / 60);
            Assert.AreEqual(0, input.MoveX); Assert.AreEqual(0, input.MoveZ);
        }
    }
}
