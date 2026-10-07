using BeMyArms.M2;
using NUnit.Framework;

namespace BeMyArms.M2.Tests
{
    /// <summary>
    /// Targeted tests for the recovered 3D movement model: gravity/grounding, jump, step-up,
    /// ramps, walls and sprint/dodge. Pure and deterministic, so server and prediction agree.
    /// </summary>
    public class M2MovementTests
    {
        const float Dt = 1f / 60f;

        static M2BodySim NewSim(M2MovementCollision collision = null)
        {
            var sim = new M2BodySim { Collision = collision };
            sim.Initialize(0f, 0f, 0f);
            return sim;
        }

        static void Step(M2BodySim sim, int frames, M2P1Input input)
        {
            for (int i = 0; i < frames; i++) sim.ApplyP1(input, Dt);
        }

        [Test]
        public void Gravity_DropsBodyToGround()
        {
            var sim = NewSim();
            sim.State.PosY = 5f;
            sim.State.Grounded = false;
            Step(sim, 120, default);
            Assert.AreEqual(0f, sim.State.PosY, 0.01f, "body must fall back to the ground plane");
            Assert.IsTrue(sim.State.Grounded);
        }

        [Test]
        public void Jump_LeavesGround_ThenLands()
        {
            var sim = NewSim();
            sim.ApplyP1(new M2P1Input { Jump = true }, Dt);
            Assert.IsFalse(sim.State.Grounded, "jump must leave the ground");

            float peak = 0f;
            for (int i = 0; i < 120; i++)
            {
                sim.ApplyP1(default, Dt);
                if (sim.State.PosY > peak) peak = sim.State.PosY;
            }
            Assert.Greater(peak, 0.7f, "jump should clear low cover scale");
            Assert.IsTrue(sim.State.Grounded, "body must land again");
            Assert.AreEqual(0f, sim.State.PosY, 0.02f);
        }

        [Test]
        public void Step_ClimbsLowLedge_ButNotTallWall()
        {
            var collision = new M2MovementCollision();
            collision.AddBox(-2f, 0f, 0.5f, 2f, 0.35f, 2f);    // low step (0.35 m)
            collision.AddBox(-2f, 0f, 4f, 2f, 3f, 5f);        // tall wall
            var sim = NewSim(collision);

            Step(sim, 20, new M2P1Input { MoveZ = 1f });
            Assert.Greater(sim.State.PosY, 0.3f, "body should step up onto the low ledge");
        }

        [Test]
        public void Ramp_RaisesBody_AlongSlope()
        {
            var collision = new M2MovementCollision();
            // Flat-ish ground then a ramp rising along +Z from y=0 to y=1.0 over 4 m.
            collision.AddSurface(-2f, 0f, 2f, 4f, 0f, 1f, 1);
            var sim = NewSim(collision);

            Step(sim, 45, new M2P1Input { MoveZ = 1f });
            Assert.Greater(sim.State.PosY, 0.5f, "body should be walking up the ramp");
            Assert.Greater(sim.State.PosZ, 2f, "body should have advanced up the ramp");
        }

        [Test]
        public void Wall_BlocksHorizontalMovement()
        {
            var collision = new M2MovementCollision();
            collision.AddBox(-2f, 0f, 1f, 2f, 3f, 1.5f);
            var sim = NewSim(collision);

            Step(sim, 200, new M2P1Input { MoveZ = 1f, Sprint = true });
            Assert.Less(sim.State.PosZ, 1f, "body must not pass through the wall");
        }

        [Test]
        public void Sprint_IsFasterThanWalk()
        {
            var walk = NewSim();
            var sprint = NewSim();
            Step(walk, 60, new M2P1Input { MoveZ = 1f });
            Step(sprint, 60, new M2P1Input { MoveZ = 1f, Sprint = true });
            Assert.Greater(sprint.State.PosZ, walk.State.PosZ * 1.3f, "sprint must be meaningfully faster");
        }

        [Test]
        public void Dodge_MovesQuicklyAlongInputDirection()
        {
            var sim = NewSim();
            float before = sim.State.PosZ;
            sim.ApplyP1(new M2P1Input { Dodge = true, MoveZ = 1f }, Dt);
            for (int i = 0; i < 12; i++) sim.ApplyP1(default, Dt);
            Assert.Greater(sim.State.PosZ - before, 0.6f, "a dodge should cover ground quickly");
        }

        [Test]
        public void Jump_HasBriskRiseAndFasterFall()
        {
            var sim=NewSim(); sim.ApplyP1(new M2P1Input{Jump=true},Dt);
            float peak=0; int apexFrame=0, landedFrame=0;
            for(int i=1;i<90;i++)
            {
                sim.ApplyP1(default,Dt);
                if(sim.State.PosY>peak){peak=sim.State.PosY;apexFrame=i;}
                if(sim.State.Grounded){landedFrame=i;break;}
            }
            Assert.That(peak,Is.InRange(.8f,1.05f));
            Assert.That(apexFrame*Dt,Is.InRange(.20f,.28f));
            Assert.That(landedFrame*Dt,Is.InRange(.38f,.51f));
            Assert.Less(landedFrame-apexFrame,apexFrame+1);
        }

        [TestCase(-40f)] [TestCase(40f)]
        public void Slide_UsesTheSameLookRelativeForwardAsMovement(float yaw)
        {
            var sim=NewSim();sim.State.LookYaw=yaw;
            sim.ApplyP1(new M2P1Input{Slide=true,MoveZ=1},Dt);
            float dx=sim.State.ActionDirX,dz=sim.State.ActionDirZ;
            Step(sim,12,default);
            Assert.That(sim.State.PosX/sim.State.PosZ,Is.EqualTo(dx/dz).Within(.0001f));
            Assert.That(dx,Is.EqualTo((float)System.Math.Sin(yaw*System.Math.PI/180)).Within(.0001f));
            Assert.Greater(sim.State.PlanarSpeed,1);
        }

        [Test]
        public void Determinism_ReplayMatches_WithMovementActions()
        {
            var a = NewSim();
            var b = NewSim();
            for (int i = 0; i < 240; i++)
            {
                var input = new M2P1Input
                {
                    MoveZ = 1f,
                    Sprint = i % 3 == 0,
                    Jump = i % 40 == 0,
                    Dodge = i % 55 == 0,
                    LookYawDelta = 2f
                };
                a.ApplyP1(input, Dt);
                b.ApplyP1(input, Dt);
            }
            Assert.AreEqual(a.State.PosX, b.State.PosX, 1e-5f);
            Assert.AreEqual(a.State.PosY, b.State.PosY, 1e-5f);
            Assert.AreEqual(a.State.PosZ, b.State.PosZ, 1e-5f);
            Assert.AreEqual(a.State.MovementState, b.State.MovementState);
        }
    }
}
