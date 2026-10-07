using BeMyArms.M2;
using NUnit.Framework;

namespace BeMyArms.M3.Tests
{
    public class M3ControlBaselineTests
    {
        const float Dt = M3InputStream.TickSeconds;

        static float SectorStep(float yaw, float body, float delta, float half, out int side)
        {
            float velocity = 0f;
            return M3SectorWall.Step(yaw, body, delta, half, Dt, ref velocity, out side);
        }

        [Test]
        public void FrameEdgesSurviveUntilTick_CrouchIsHeld_ActionsAndLookNeverRepeat()
        {
            M2P1Input pending = default;
            M3InputStream.Accumulate(ref pending, new M2P1Input { Crouch = true, Jump = true, LookYawDelta = 4 });
            M3InputStream.Accumulate(ref pending, new M2P1Input { Crouch = true, MoveZ = 1, LookYawDelta = 2 });
            Assert.IsTrue(pending.Jump);
            Assert.IsTrue(pending.Crouch);
            Assert.AreEqual(6, pending.LookYawDelta);
            var held = M3InputStream.Held(pending);
            Assert.IsTrue(held.Crouch);
            Assert.AreEqual(1, held.MoveZ);
            Assert.IsFalse(held.Jump);
            Assert.AreEqual(0, held.LookYawDelta);
        }

        [Test]
        public void MenuFocusAndResetRequireTriggerRelease_BeforeFireCanResume()
        {
            bool armed = true;
            Assert.IsTrue(M3InputStream.FireGate(ref armed, true, true, true));
            Assert.IsFalse(M3InputStream.FireGate(ref armed, false, true, false));
            Assert.IsFalse(M3InputStream.FireGate(ref armed, true, true, false), "held menu/resume click must not fire");
            Assert.IsFalse(M3InputStream.FireGate(ref armed, true, false, false));
            Assert.IsTrue(M3InputStream.FireGate(ref armed, true, true, true));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void FrameSamplingAndFixedServerTicks_AdvanceAtSameSpeed(int fps)
        {
            var stream = new M3P1CommandStream();
            stream.Reset(1);
            var sim = new M2BodySim(); sim.Initialize(0);
            uint sequence = 0;
            int sentTicks = 0;
            // Rendered frames can enqueue multiple fixed commands. Server consumes one per tick,
            // independently of those arrival batches (as in a headless server's catch-up loop).
            for (int tick = 0; tick < 600; tick++)
            {
                double now = (tick + 1) / 60.0;
                int frame = (int)(now * fps + 1e-7);
                int renderedTicks = (int)(((frame + 1) / (double)fps) * 60 + 1e-7);
                while (sentTicks < renderedTicks)
                {
                    stream.Submit(new M2P1Input { ControlEpoch = 1, Sequence = ++sequence, MoveZ = 1 }, now, 0);
                    sentTicks++;
                }
                sim.ApplyP1(stream.Consume(now), Dt);
            }
            Assert.AreEqual(45f, sim.State.PosZ, 0.01f);
            Assert.LessOrEqual(stream.Count, 2);
        }

        [Test]
        public void BurstDoesNotFastForwardMovement_TimeoutReleasesHeldControls()
        {
            var stream = new M3P1CommandStream(); stream.Reset(4);
            for (uint seq = 1; seq <= 5; seq++)
                stream.Submit(new M2P1Input { ControlEpoch = 4, Sequence = seq, MoveZ = 1 }, 0, 0);
            var sim = new M2BodySim(); sim.Initialize(0);
            sim.ApplyP1(stream.Consume(0), Dt);
            Assert.AreEqual(0.075f, sim.State.PosZ, 0.0001f);
            Assert.AreEqual(1, stream.Acknowledged);
            for (int t = 1; t < 5; t++) stream.Consume(t * Dt);
            Assert.AreEqual(1, stream.Consume(0.1).MoveZ);
            Assert.AreEqual(0, stream.Consume(0.5).MoveZ);
        }

        [Test]
        public void NewEpochDropsDelayedCommandsAndOldSequences_RejectsNonfiniteControls()
        {
            var stream = new M3P1CommandStream(); stream.Reset(4);
            var input = new M2P1Input { ControlEpoch = 4, Sequence = 1, Crouch = true, HeavyKick = true };
            Assert.IsTrue(stream.Submit(input, 0, 1));
            Assert.IsFalse(stream.Submit(input, 0, 1));
            stream.Reset(5);
            Assert.IsFalse(stream.Submit(input, 0, 0));
            Assert.IsFalse(stream.Consume(2).HeavyKick);
            input.ControlEpoch = 5; input.MoveX = float.NaN;
            Assert.IsFalse(stream.Submit(input, 2, 0));
            Assert.IsFalse(M3InputStream.Valid(new M2P2Input { AimYaw = float.PositiveInfinity }));
            input.MoveX = 0;
            Assert.IsTrue(stream.Submit(input, 2, 0));
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void ElasticSectorHasNoOverflow_ImmediateInwardResponse(int side)
        {
            float yaw = SectorStep(0, 0, 10000 * side, 70, out int blocked);
            Assert.AreEqual(85 * side, yaw, 0.0001f);
            Assert.AreEqual(side, blocked);
            yaw = SectorStep(yaw, 0, -20 * side, 70, out blocked);
            Assert.AreEqual(65 * side, yaw, 0.0001f);
            Assert.AreEqual(0, blocked);
        }

        [Test]
        public void SectorPreservesWorldAimUntilSweptByBody_HandlesYawWrap()
        {
            Assert.AreEqual(179, SectorStep(179, -179, 0, 70, out _), 0.001);
            Assert.AreEqual(25, SectorStep(25, 40, 0, 70, out _), 0.001);
            Assert.Greater(SectorStep(0, 120, 2, 70, out _), 35, "body-swept elastic cap still allows immediate inward movement");
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void SoftEdgeIsIndependentOfMouseFrameSplitting_AndReversesImmediately(int side)
        {
            Assert.AreEqual(69 * side, SectorStep(0, 0, 69 * side, 70, out _), 0.001, "no resistance inside 70 degrees");
            float one = SectorStep(0, 0, 170 * side, 70, out _);
            float many = 0;
            for (int i = 0; i < 144; i++) many = SectorStep(many, 0, 170f * side / 144, 70, out _);
            Assert.AreEqual(one, many, 0.002);
            Assert.That(System.Math.Abs(one), Is.InRange(70.1f, 85f));
            Assert.LessOrEqual(System.Math.Abs(SectorStep(one, 0, -2 * side, 70, out _)), System.Math.Abs(one) - 2, "inward response cannot be resisted");
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void SoftPressureIsHeavyAndBounded_ReversalHasNoOverflowDebt(int side)
        {
            float yaw = SectorStep(70 * side, 0, side, 70, out _);
            Assert.That(System.Math.Abs(yaw) - 70, Is.InRange(0.02f, 0.081f), "initial outward sensitivity should be about eight percent");
            yaw = SectorStep(yaw, 0, 10000 * side, 70, out _);
            Assert.AreEqual(85 * side, yaw, 0.001);
            yaw = SectorStep(yaw, 0, -20 * side, 70, out _);
            Assert.AreEqual(65 * side, yaw, 0.001);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void ReleasedSoftStopReturnsPromptlyWithoutOvershoot_AtEveryFrameRate(int fps)
        {
            float yaw = 76, velocity = 0;
            for (int i = 0; i < fps; i++)
            {
                float before = yaw;
                yaw = M3SectorWall.Step(yaw, 0, 0, 70, 1f / fps, ref velocity, out _);
                Assert.That(yaw, Is.InRange(70, before));
                if (i == 0) Assert.Less(yaw, before, "return begins on the first release frame");
                if (i >= fps / 4) Assert.Less(yaw, 70.15f, "stiff stop should be almost settled in a quarter second");
            }
            Assert.AreEqual(70, yaw, 0.001);
            Assert.AreEqual(0, velocity);
        }

        [Test]
        public void SpringTraceIsFrameIndependent_RepressCancelsReturnVelocity()
        {
            float[] traces = new float[3]; int index = 0;
            foreach (int fps in new[] { 30, 60, 144 })
            {
                float yaw = 75, velocity = 0, time = 0;
                while (time < 0.2f)
                {
                    float dt = System.Math.Min(1f / fps, 0.2f - time);
                    yaw = M3SectorWall.Step(yaw, 0, 0, 70, dt, ref velocity, out _);
                    time += dt;
                }
                traces[index++] = yaw;
                float pushed = M3SectorWall.Step(yaw, 0, 1, 70, Dt, ref velocity, out _);
                Assert.Greater(pushed, yaw); Assert.AreEqual(0, velocity);
            }
            Assert.AreEqual(traces[0], traces[1], 0.001);
            Assert.AreEqual(traces[0], traces[2], 0.001);
        }

        [Test]
        public void AuthoritativeAimPreservesBoundedSoftOvertravel_AcrossP1Steps()
        {
            var sim = new M2BodySim { SectorOvertravelDegrees = M3SectorWall.OvertravelDegrees }; sim.Initialize(0);
            sim.ApplyP2(new M2P2Input { AimYaw = 84 });
            Assert.AreEqual(84, sim.State.AimYaw); Assert.IsTrue(sim.SectorLegal(84));
            sim.ApplyP1(default, Dt);
            Assert.AreEqual(84, sim.State.AimYaw, "P1 movement must not hard-clamp P2's displayed overtravel");
            Assert.IsFalse(sim.SectorLegal(85.1f));
            sim.ApplyP2(new M2P2Input { AimYaw = 100 });
            Assert.AreEqual(85, sim.State.AimYaw);
            sim.Initialize(0); Assert.AreEqual(0, sim.State.AimYaw);
        }

        [Test]
        public void AimHistoryValidatesTheDisplayedSnapshot_NotCurrentOrSmoothedYaw()
        {
            var history = new M3AimHistory(); history.Record(50, 170); history.Record(51, -179);
            Assert.IsTrue(history.TryGet(50, 51, out float yaw)); Assert.AreEqual(170, yaw);
            Assert.IsFalse(history.TryGet(52, 51, out _));
            Assert.IsFalse(history.TryGet(50, 170, out _));
            history.Clear(); Assert.IsFalse(history.TryGet(51, 51, out _));
        }

        [Test]
        public void SlideSnapshotReplaysWithFreshSim()
        {
            var collision = new M2MovementCollision();
            var original = new M2BodySim { Collision = collision }; original.Initialize(0);
            original.ApplyP1(new M2P1Input { Slide = true, MoveZ = 1 }, Dt);
            Assert.AreEqual((byte)M2MovementState.Slide, original.State.MovementState);
            for (int i = 0; i < 7; i++) original.ApplyP1(default, Dt);
            var replay = new M2BodySim { Collision = collision, State = original.State };
            for (int i = 0; i < 40; i++)
            {
                original.ApplyP1(default, Dt); replay.ApplyP1(default, Dt);
                Assert.AreEqual(original.State.PosZ, replay.State.PosZ, 1e-5);
                Assert.AreEqual(original.State.PosY, replay.State.PosY, 1e-5);
            }
            Assert.Greater(replay.State.PosZ,1f);
        }

        [Test]
        public void SlideUsesLowStance_CrouchSprintIsReportedAsWalk_DeathCannotMove()
        {
            var sim = new M2BodySim(); sim.Initialize(0);
            sim.ApplyP1(new M2P1Input { MoveZ = 1, Crouch = true, Sprint = true }, Dt);
            Assert.AreEqual((byte)M2MovementState.Walk, sim.State.MovementState);
            Assert.AreEqual(sim.CrouchSpeed, sim.State.PlanarSpeed);
            sim.ApplyP1(new M2P1Input { Slide = true }, Dt);
            Assert.AreEqual(sim.CrouchHeight, sim.State.HitHeight);
            sim.State.Health = 0;
            float before = sim.State.PosZ;
            sim.ApplyP1(new M2P1Input { MoveZ = 1 }, Dt);
            Assert.AreEqual(before, sim.State.PosZ);
        }

        [Test]
        public void PracticeRoleSwapMovesBothAuthorities_AndReconnectReservations()
        {
            var roster = new M3DuelRoster();
            roster.AssignExact(10, "body", 0, out _); roster.AssignExact(20, "arms", 1, out _);
            roster.SwapBodyRoles(0);
            Assert.IsTrue(roster.HasSlot(10, 1)); Assert.IsTrue(roster.HasSlot(20, 0));
            roster.Release(10); roster.SetBot(1);
            Assert.AreEqual(-1, roster.AssignExact(30, "stranger", 1, out _));
            Assert.AreEqual(1, roster.AssignExact(40, "body", 0, out _));
            Assert.IsFalse(roster.IsBot(1));
            Assert.IsTrue(roster.HasSingleOwner(0)); Assert.IsTrue(roster.HasSingleOwner(1));
        }

        [Test]
        public void SoloRoleSwapMovesBotToPartnerRole_RejectsOccupiedRoleInsteadOfFallback()
        {
            var roster = new M3DuelRoster(); roster.AssignExact(10, "human", 0, out _); roster.SetBot(1);
            Assert.AreEqual(-1, roster.AssignExact(20, "other", 0, out _));
            roster.SwapBodyRoles(0);
            Assert.IsTrue(roster.IsBot(0)); Assert.IsTrue(roster.HasSlot(10, 1));
            roster.SetBot(1); Assert.IsFalse(roster.IsBot(1));
        }

        [Test]
        public void ExplicitLeaveAllowsFreshPartner_UnexpectedDisconnectKeepsReservation()
        {
            var roster = new M3DuelRoster(); roster.AssignExact(10, "old", 0, out _);
            roster.Release(10); roster.SetBot(0);
            Assert.AreEqual(-1, roster.AssignExact(20, "new", 0, out _));
            roster.AssignExact(30, "old", 0, out _);
            roster.ReleaseReservation(30); roster.SetBot(0);
            Assert.AreEqual(0, roster.AssignExact(20, "new", 0, out _));
            Assert.IsTrue(roster.HasSingleOwner(0));
        }

        [Test]
        public void RestartEncounterKeepsScoreAndIndex_FreshMatchClearsBoth()
        {
            var match = new M3MatchState { BuySeconds = 0.1f, RoundEndSeconds = 0.1f };
            match.StartMatch(); match.Tick(1); match.ReportTeamEliminated(1); match.Tick(1);
            int index = match.RoundIndex, wins = match.TeamAWins, resets = 0;
            match.RoundStarted += _ => resets++;
            match.RestartRound();
            Assert.AreEqual(index, match.RoundIndex); Assert.AreEqual(wins, match.TeamAWins);
            Assert.AreEqual(M3Phase.Buy, match.Phase); Assert.AreEqual(1, resets);
            match.StartMatch(); Assert.AreEqual(1, match.RoundIndex); Assert.AreEqual(0, match.TeamAWins);
        }
    }
}
