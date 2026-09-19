using BeMyArms.M2;
using NUnit.Framework;

namespace BeMyArms.M2.Tests
{
    /// <summary>
    /// Pure M2 core: deterministic shared-body sim (decoupled look, body follow, sector clamp) and
    /// client prediction/reconciliation.
    /// </summary>
    public class M2SimulationTests
    {
        static M2BodySim NewSim()
        {
            var sim = new M2BodySim();
            sim.Initialize(0f);
            return sim;
        }

        [Test]
        public void Sim_IsDeterministicForSameInputs()
        {
            var a = NewSim();
            var b = NewSim();
            for (int i = 0; i < 200; i++)
            {
                var p1 = new M2P1Input { Sequence = (uint)i, MoveX = 0.3f, MoveZ = 1f, LookYawDelta = 4f, AlignBody = (i % 37 == 0) };
                a.ApplyP1(p1, 1f / 60f);
                b.ApplyP1(p1, 1f / 60f);
            }
            Assert.AreEqual(a.State.PosX, b.State.PosX, 1e-5f);
            Assert.AreEqual(a.State.PosZ, b.State.PosZ, 1e-5f);
            Assert.AreEqual(a.State.BodyYaw, b.State.BodyYaw, 1e-5f);
        }

        [Test]
        public void LookInsideThreshold_DoesNotMoveBody_ButMovementUsesBodyYaw()
        {
            M2BodySim sim = NewSim();
            sim.ApplyP1(new M2P1Input { MoveX = 0f, MoveZ = 1f, LookYawDelta = 30f }, 0.1f);

            Assert.AreEqual(0f, sim.State.BodyYaw, 1e-4f, "Looking inside the threshold must not turn the body.");
            // Body faces +Z, so forward movement advances +Z and not +X.
            Assert.Greater(sim.State.PosZ, 0f);
            Assert.AreEqual(0f, sim.State.PosX, 1e-4f);
        }

        [Test]
        public void AlignBody_TurnsBodyToLook()
        {
            M2BodySim sim = NewSim();
            sim.ApplyP1(new M2P1Input { LookYawDelta = 60f }, 0.05f); // fast look, body follows only past threshold
            sim.ApplyP1(new M2P1Input { AlignBody = true }, 0.1f);
            Assert.AreEqual(sim.State.LookYaw, sim.State.BodyYaw, 1e-3f, "AlignBody should bring the body onto the look.");
        }

        [Test]
        public void P2Aim_IsClampedToSectorAroundBodyYaw()
        {
            M2BodySim sim = NewSim();
            sim.ApplyP2(new M2P2Input { AimYaw = 120f, AimPitch = 200f });

            Assert.AreEqual(70f, sim.State.AimYaw, 1e-4f, "Aim must clamp to the sector, not follow the raw input.");
            Assert.IsTrue(sim.SectorLegal(sim.State.AimYaw));
            Assert.AreEqual(80f, sim.State.AimPitch, 1e-4f);
        }

        // ---- Prediction / reconciliation ----

        [Test]
        public void Reconcile_ReplaysUnackedInputs_WithNoDrift()
        {
            var predictSim = new M2BodySim();
            predictSim.Initialize(0f);
            var reconciler = new M2Reconciler();
            reconciler.Reset(predictSim.State);

            var seq1 = new M2P1Input { Sequence = 1, MoveZ = 1f, LookYawDelta = 10f };
            var seq2 = new M2P1Input { Sequence = 2, MoveX = 1f, LookYawDelta = 5f };
            reconciler.Predict(seq1, 1f / 60f, predictSim);
            reconciler.Predict(seq2, 1f / 60f, predictSim);
            M2BodyState fullPrediction = reconciler.Predicted;

            // Authoritative state after the server processed seq1 only.
            var serverSim = new M2BodySim();
            serverSim.Initialize(0f);
            serverSim.ApplyP1(seq1, 1f / 60f);

            var replaySim = new M2BodySim();
            reconciler.Reconcile(serverSim.State, 1u, replaySim);

            Assert.AreEqual(fullPrediction.PosX, reconciler.Predicted.PosX, 1e-5f, "Replaying seq2 must match the full prediction.");
            Assert.AreEqual(fullPrediction.PosZ, reconciler.Predicted.PosZ, 1e-5f);
            Assert.AreEqual(fullPrediction.BodyYaw, reconciler.Predicted.BodyYaw, 1e-5f);
            Assert.AreEqual(1, reconciler.PendingInputCount, "Only the unacknowledged seq2 must remain in the buffer.");
        }

        [Test]
        public void Reconcile_CorrectsTowardServerState()
        {
            var predictSim = new M2BodySim();
            predictSim.Initialize(0f);
            var reconciler = new M2Reconciler();
            reconciler.Reset(predictSim.State);
            M2BodyState serverAuthoritative = predictSim.State; // origin, captured before predicting

            // Client believes it moved forward.
            reconciler.Predict(new M2P1Input { Sequence = 1, MoveZ = 1f }, 0.1f, predictSim);
            Assert.Greater(reconciler.Predicted.PosZ, 0f);

            // Server (authoritatively) says the body never moved (e.g. collision) and acks seq 1.
            var replaySim = new M2BodySim();

            reconciler.Reconcile(serverAuthoritative, 1u, replaySim);

            Assert.AreEqual(0f, reconciler.Predicted.PosZ, 1e-5f, "Prediction must be corrected to the authoritative state.");
        }
    }
}
