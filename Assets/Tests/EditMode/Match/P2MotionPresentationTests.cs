using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.Match.Tests
{
    public class P2MotionPresentationTests
    {
        [TestCase(60)] [TestCase(144)]
        public void SnapshotStepsBecomeContinuousFrameTravel(int frameRate)
        {
            var motion = new P2MotionPresentation();
            const double interval = 1.0 / 30;
            int snapshot = 0; float previous = 0;
            for (int frame = 0; frame < frameRate; frame++)
            {
                double now = frame / (double)frameRate;
                while (snapshot * interval <= now)
                {
                    double time = snapshot * interval;
                    motion.Observe(1, (uint)(snapshot * 2), Vector3.right * (float)(time * 4.5), 1.45f, time);
                    snapshot++;
                }
                float x = motion.Evaluate(now, out _).x;
                if (now > .12) Assert.AreEqual(4.5f / frameRate, x - previous, .00001f, "No snapshot-rate stop/start or exponential velocity pulse");
                previous = x;
            }
        }

        [Test]
        public void StanceSharesPositionTimelineAndDoesNotExtrapolate()
        {
            var motion = new P2MotionPresentation();
            motion.Observe(1, 1, Vector3.zero, 1.5f, 0);
            motion.Observe(1, 3, Vector3.right, 1f, .04);
            Assert.AreEqual(.5f, motion.Evaluate(P2MotionPresentation.Delay + .02, out float eye).x, .0001f);
            Assert.AreEqual(1.25f, eye, .0001f);
            Assert.AreEqual(Vector3.right, motion.Evaluate(1, out _));
        }

        [TestCase(true)] [TestCase(false)]
        public void EpochOrTeleportSnapsInsteadOfDraggingTheEye(bool epoch)
        {
            var motion = new P2MotionPresentation();
            motion.Observe(1, 10, Vector3.zero, 1.5f, 0);
            Vector3 destination = Vector3.right * (epoch ? 2 : 10);
            motion.Observe(epoch ? 2u : 1u, 12, destination, 1f, .03);
            Assert.AreEqual(destination, motion.Evaluate(.03, out float eye)); Assert.AreEqual(1, eye);
        }

        [Test]
        public void DuplicateFramesAreNotSnapshotsAndClearDropsTheOldRole()
        {
            var motion = new P2MotionPresentation();
            motion.Observe(1, 1, Vector3.zero, 1.5f, 0);
            motion.Observe(1, 3, Vector3.right, 1.5f, .04);
            motion.Observe(1, 3, Vector3.right, 1.5f, .05);
            Assert.AreEqual(.5f, motion.Evaluate(P2MotionPresentation.Delay + .02, out _).x, .0001f);
            motion.Clear(); motion.Observe(1, 3, Vector3.forward, 1.5f, .06);
            Assert.AreEqual(Vector3.forward, motion.Evaluate(.06, out _));
        }
    }
}
