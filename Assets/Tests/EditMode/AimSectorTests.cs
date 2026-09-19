using BeMyArms.M0;
using NUnit.Framework;

namespace BeMyArms.M0.Tests
{
    /// <summary>
    /// Verifies the deterministic half of M0 acceptance criteria 3, 4 and 5:
    /// world-stable aim inside the sector, boundary push, no phantom offset, and that the
    /// sector math can never move the body.
    /// </summary>
    public class AimSectorTests
    {
        const float Half = 70f;

        [Test]
        public void NormalizeAngle_WrapsIntoMinus180To180()
        {
            Assert.AreEqual(0f, AimSector.NormalizeAngle(360f), 0.0001f);
            Assert.AreEqual(10f, AimSector.NormalizeAngle(370f), 0.0001f);
            Assert.AreEqual(-10f, AimSector.NormalizeAngle(-370f), 0.0001f);
            Assert.AreEqual(170f, AimSector.NormalizeAngle(-190f), 0.0001f);
            Assert.AreEqual(180f, AimSector.NormalizeAngle(180f), 0.0001f);
        }

        [Test]
        public void Clamp_InsideSector_IsUnchanged()
        {
            Assert.AreEqual(30f, AimSector.ClampToSector(30f, 0f, Half), 0.0001f);
            Assert.AreEqual(-69f, AimSector.ClampToSector(-69f, 0f, Half), 0.0001f);
        }

        [Test]
        public void Clamp_OutsideSector_GoesToBoundary()
        {
            Assert.AreEqual(70f, AimSector.ClampToSector(90f, 0f, Half), 0.0001f);
            Assert.AreEqual(-70f, AimSector.ClampToSector(-90f, 0f, Half), 0.0001f);
        }

        [Test]
        public void BodyRotation_DoesNotMoveAimInsideSector()
        {
            // Criterion 3: P1 rotating must not drag P2's crosshair off target.
            float aim = AimSector.ApplyInput(50f, 0f, 0f, Half);
            Assert.AreEqual(50f, aim, 0.0001f);

            // Body rotates, no P2 input.
            aim = AimSector.ApplyInput(aim, 0f, 20f, Half);
            Assert.AreEqual(50f, aim, 0.0001f);

            aim = AimSector.ApplyInput(aim, 0f, -15f, Half);
            Assert.AreEqual(50f, aim, 0.0001f);
        }

        [Test]
        public void BodyRotationAwayFromAim_PushesAimWithBody()
        {
            float aim = AimSector.ApplyInput(70f, 0f, 0f, Half);
            Assert.AreEqual(70f, aim, 0.0001f);

            // Body rotates far left; the +70 boundary sweeps left past the aim.
            aim = AimSector.ApplyInput(aim, 0f, -100f, Half);
            Assert.AreEqual(-30f, aim, 0.0001f); // bodyYaw(-100) + half(70)
            Assert.IsTrue(AimSector.IsPinned(aim, -100f, Half));
        }

        [Test]
        public void BoundaryPush_RestabilizesInWorldSpace()
        {
            // Push the aim onto the boundary, then rotate the body back so the boundary
            // passes it again: the aim must stay where it is, not snap.
            float aim = AimSector.ApplyInput(70f, 0f, 0f, Half);
            aim = AimSector.ApplyInput(aim, 0f, -100f, Half); // pushed to -30
            aim = AimSector.ApplyInput(aim, 0f, 0f, Half);    // boundary sweeps back
            Assert.AreEqual(-30f, aim, 0.0001f);
        }

        [Test]
        public void NoPhantomOffset_WhenPushingOutwardThenInward()
        {
            // Criterion 4: overflow at the limit must be discarded, not accumulated.
            float aim = AimSector.ApplyInput(0f, 0f, 0f, Half);
            Assert.AreEqual(0f, aim, 0.0001f);

            // Slam outward past the limit: stored state clamps to +70.
            aim = AimSector.ApplyInput(aim, 90f, 0f, Half);
            Assert.AreEqual(70f, aim, 0.0001f);

            // Push outward again while already pinned: still +70, nothing stored.
            aim = AimSector.ApplyInput(aim, 90f, 0f, Half);
            Assert.AreEqual(70f, aim, 0.0001f);

            // Then move inward by 5 degrees: must respond immediately.
            aim = AimSector.ApplyInput(aim, -5f, 0f, Half);
            Assert.AreEqual(65f, aim, 0.0001f);

            // Push outward again, then inward by 20.
            aim = AimSector.ApplyInput(aim, 90f, 0f, Half);
            Assert.AreEqual(70f, aim, 0.0001f);
            aim = AimSector.ApplyInput(aim, -20f, 0f, Half);
            Assert.AreEqual(50f, aim, 0.0001f);
        }

        [Test]
        public void P2Input_CannotMoveTheBody()
        {
            // Criterion 5: the sector API only ever returns an aim yaw. This test documents
            // that bodyYaw is an input, never an output, so P2 input cannot rotate the body.
            const float bodyYaw = 33f;
            float aim = AimSector.ApplyInput(0f, 170f, bodyYaw, Half);
            Assert.AreEqual(bodyYaw + Half, aim, 0.0001f);
            Assert.AreEqual(bodyYaw, 33f, 0.0001f);
        }

        [Test]
        public void IsPinned_DetectsBoundary()
        {
            Assert.IsTrue(AimSector.IsPinned(70f, 0f, Half));
            Assert.IsTrue(AimSector.IsPinned(-70f, 0f, Half));
            Assert.IsFalse(AimSector.IsPinned(69f, 0f, Half));
            Assert.IsFalse(AimSector.IsPinned(0f, 0f, Half));
        }
    }
}
