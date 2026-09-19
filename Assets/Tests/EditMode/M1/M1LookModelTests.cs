using BeMyArms.M1;
using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.M1.Tests
{
    /// <summary>
    /// P1 look/body model: decoupled look, neck limit, smooth body follow, explicit align.
    /// BodyYaw (not LookYaw) is what movement and the P2 sector use.
    /// </summary>
    public class M1LookModelTests
    {
        static P1LookController CreateLook(float neckLimit = 80f, float followThreshold = 50f,
            float followSpeed = 120f, float alignSpeed = 540f)
        {
            var go = new GameObject("look");
            var look = go.AddComponent<P1LookController>();
            look.NeckYawLimitDegrees = neckLimit;
            look.BodyFollowThresholdDegrees = followThreshold;
            look.BodyFollowSpeedDegreesPerSecond = followSpeed;
            look.BodyAlignSpeedDegreesPerSecond = alignSpeed;
            look.Initialize(0f);
            return look;
        }

        [Test]
        public void LookInsideThreshold_DoesNotMoveBody()
        {
            P1LookController look = CreateLook();
            look.Step(30f, false, 0.1f);

            Assert.AreEqual(0f, look.BodyYaw, 0.001f, "Body must not move while looking inside the threshold.");
            Assert.AreEqual(30f, look.LookYaw, 0.001f);
            Assert.AreEqual(30f, look.NeckOffsetDegrees, 0.001f);

            Object.DestroyImmediate(look.gameObject);
        }

        [Test]
        public void LookIsClampedToNeckLimit()
        {
            P1LookController look = CreateLook(followThreshold: 1000f); // disable follow for this check
            look.Step(120f, false, 0.016f);

            Assert.AreEqual(80f, look.NeckOffsetDegrees, 0.001f, "Look must clamp at the neck limit.");

            Object.DestroyImmediate(look.gameObject);
        }

        [Test]
        public void BodyFollows_WhenLookExceedsThreshold()
        {
            P1LookController look = CreateLook(); // threshold 50, follow 120 deg/s
            look.Step(60f, false, 0.1f);

            Assert.AreEqual(12f, look.BodyYaw, 0.01f, "Body should follow smoothly at the follow speed.");
            Assert.IsTrue(look.IsBodyFollowing);
            Assert.Less(Mathf.Abs(look.NeckOffsetDegrees), 60f);

            Object.DestroyImmediate(look.gameObject);
        }

        [Test]
        public void AlignBody_TurnsBodyTowardLook()
        {
            P1LookController look = CreateLook();
            look.Step(60f, false, 0f); // set look to 60 without advancing time

            look.Step(0f, true, 0.05f); // align speed 540 -> 27 degrees this step

            Assert.AreEqual(27f, look.BodyYaw, 0.01f);
            Assert.IsTrue(look.IsAligning);

            Object.DestroyImmediate(look.gameObject);
        }

        [Test]
        public void LookingAround_DoesNotMoveTheAimSector()
        {
            var bodyGo = new GameObject("body");
            var look = bodyGo.AddComponent<P1LookController>();
            look.NeckYawLimitDegrees = 80f;
            look.BodyFollowThresholdDegrees = 50f;
            look.BodyFollowSpeedDegreesPerSecond = 120f;
            look.BodyAlignSpeedDegreesPerSecond = 540f;
            look.Initialize(0f);

            var aim = bodyGo.AddComponent<P2AimRig>();
            aim.SectorHalfDegrees = 70f;
            aim.Initialize(look.BodyYaw);

            // Look 30 degrees to the side (inside the threshold): body does not move.
            look.Step(30f, false, 0.016f);
            aim.Step(new P2Command { AimYawDelta = 30f }, look.BodyYaw);

            Assert.AreEqual(0f, look.BodyYaw, 0.001f, "Looking must not rotate the body.");
            Assert.AreEqual(30f, aim.DesiredWorldYaw, 0.001f, "Aim is relative to the unchanged body yaw.");

            Object.DestroyImmediate(bodyGo);
        }
    }
}
