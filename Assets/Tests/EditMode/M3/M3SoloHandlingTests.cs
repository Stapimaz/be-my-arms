using BeMyArms.M2;
using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.M3.Tests
{
    public class M3SoloHandlingTests
    {
        [Test]
        public void RifleBurstWidensActualRaysAndHasControllableRecoil_ThenRecoversAfterAPause()
        {
            var rifle=new M3RifleHandling();float early=0,late=0,pitch=0;
            for(int i=0;i<24;i++)
            {
                int burst=rifle.Shot(i*.125);
                M3RifleHandling.Spread((uint)i+1,123,M3RifleHandling.SpreadDegrees(burst),out float yaw,out float p);
                float offset=Mathf.Sqrt(yaw*yaw+p*p);
                if(i<3)early+=offset;else if(i>=16)late+=offset;
                M3RifleHandling.Recoil(burst,out float up,out _);pitch-=up;
            }
            Assert.Less(early/3,.25f,"first burst stays tight");
            Assert.Greater(late/8,.65f,"sustained ballistic bloom matters even if camera recoil is compensated");
            Assert.Less(pitch,-12f,"uncontrolled aim climbs visibly");
            Assert.AreEqual(1,rifle.Shot(4),"pause restores first-shot handling");
            rifle.Reset();Assert.AreEqual(1,rifle.Shot(4.1));
        }

        [TestCase(179f,-170f)] [TestCase(0f,175f)] [TestCase(-40f,80f)]
        public void BotP1TurnsAtABoundedRateAcrossWrapAndRetargeting(float initial,float desired)
        {
            var sim=new M2BodySim();sim.Initialize(initial);
            sim.State.LookYaw=M2BodySim.Normalize(initial+40); // takeover from independent human look
            for(int i=0;i<240;i++)
            {
                float target=i<120 ? desired : initial;
                float before=sim.State.BodyYaw;
                sim.ApplyP1(M3BotSteering.Turn(sim.State.LookYaw,sim.State.BodyYaw,target,1f/60),1f/60);
                Assert.LessOrEqual(Mathf.Abs(M2BodySim.Normalize(sim.State.BodyYaw-before)),1.501f);
                Assert.Less(Mathf.Abs(M2BodySim.Normalize(sim.State.BodyYaw-sim.State.LookYaw)),.001f);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void KickDirectionCapturesP1LookAndSurvivesBodyFollowAndSnapshotReplay(bool heavy)
        {
            var sim=new M2BodySim();sim.Initialize(120);
            sim.ApplyP1(new M2P1Input{LookYawDelta=-20,LightKick=!heavy,HeavyKick=heavy},1f/60);
            var direction=new Vector2(sim.State.ActionDirX,sim.State.ActionDirZ);
            Assert.AreEqual(100f,Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg,.001f);
            var replay=new M2BodySim{State=sim.State};
            for(int i=0;i<12;i++)
            {
                var input=new M2P1Input{LookYawDelta=5};
                sim.ApplyP1(input,1f/60);replay.ApplyP1(input,1f/60);
                Assert.AreEqual(direction.x,sim.State.ActionDirX);Assert.AreEqual(direction.y,sim.State.ActionDirZ);
                Assert.AreEqual(sim.State.ActionDirX,replay.State.ActionDirX);
                Assert.AreEqual(sim.State.BodyYaw,replay.State.BodyYaw);
            }
        }
    }
}
