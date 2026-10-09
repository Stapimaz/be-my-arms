using System.Linq;
using BeMyArms.Client.EditorTools;
using BeMyArms.Match;
using BeMyArms.Networking;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.Client.Tests
{
    public class BoatyardTests
    {
        Scene _previous, _scene;
        MapSpawns _map;
        MovementCollision _collision;

        [SetUp]
        public void OpenAuthoredScene()
        {
            _previous = SceneManager.GetActiveScene();
            _scene = EditorSceneManager.OpenScene(BoatyardSceneBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(_scene);
            _map = _scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapSpawns>()).Single();
            _collision = _map.BuildCollision();
        }

        [TearDown]
        public void RestoreEditor()
        {
            SceneManager.SetActiveScene(_previous);
            if (_scene.IsValid()) EditorSceneManager.CloseScene(_scene, true);
        }

        [Test]
        public void RecordIsValidAndBothSpawnsHaveActualStandingClearance()
        {
            var record = AssetDatabase.LoadAssetAtPath<MapDefinition>(BoatyardSceneBuilder.RecordPath);
            Assert.IsNotNull(record);
            var report = MapValidator.ValidateAll();
            Assert.IsTrue(report.IsValid, report.ToString());
            var navigation = new BotNavigation(); navigation.Search(_collision, _map.Spawns[0].Position);
            foreach (var s in _map.Spawns)
            {
                Assert.IsTrue(navigation.Clear(s.Position, 1.8f), $"Team {s.Team} starts clear");
                Assert.AreEqual(s.Position.y, _collision.SurfaceHeight(s.Position.x, s.Position.z, s.Position.y + .05f), .001f);
            }
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == BoatyardSceneBuilder.ScenePath));
            Assert.AreEqual("Boatyard", PrivateMatch.DefaultDuelScene);
        }

        [Test]
        public void SidesAlternateEveryRoundWithoutMutatingTeamRoleRecords()
        {
            for (int round = 1; round <= 5; round++)
                for (int team = 0; team < 2; team++)
                {
                    Assert.IsTrue(_map.TryGetBodyPose(team, 0, round, out var position, out var yaw));
                    Assert.IsTrue(_map.TryGet(round % 2 == 0 ? 1 - team : team, 0, 0, out var source));
                    Assert.AreEqual(source.Position, position); Assert.AreEqual(source.Yaw, yaw);
                    Assert.IsTrue(_map.TryGet(team, 0, 1, out var owned)); Assert.AreEqual(team, owned.Team);
                }
            _map.AlternateDuelSides = false; // In-memory disposable test scene only.
            _map.TryGetBodyPose(0, 0, 1, out var first, out _);
            _map.TryGetBodyPose(0, 0, 2, out var second, out _);
            Assert.AreEqual(first, second, "Legacy maps retain their fixed starts");
        }

        [Test]
        public void SpawnsCannotShootEachOtherImmediately()
        {
            _map.TryGetBodyPose(0, 0, out var a, out _); _map.TryGetBodyPose(1, 0, out var b, out _);
            a.y += 1.6f; b.y += 1.6f; var d = b - a;
            Assert.IsTrue(_collision.RaycastSolids(a.x, a.y, a.z, d.normalized.x, d.normalized.y, d.normalized.z, d.magnitude, out _));
        }

        [TestCase(-6f, 0f, -11.5f, 0f, 1.2f, -4f)]
        [TestCase(-4f, 1.2f, .5f, 0f, 2.4f, 6.5f)]
        [TestCase(14.5f, 0f, -10.5f, 0f, 1.2f, -4.5f)]
        [TestCase(12.5f, 1.2f, 13.5f, -90f, 2.4f, 5.5f)]
        public void OrdinaryMovementTraversesEachRamp(float x, float y, float z, float yaw, float endY, float endpoint)
        {
            var sim = new BodySim { Collision = _collision }; sim.Initialize(yaw: yaw, posX: x, posZ: z, posY: y);
            for (int i = 0; i < 110; i++) sim.ApplyP1(new P1Input { MoveZ = 1 }, 1f / 60);
            Assert.AreEqual(endY, sim.State.PosY, .025f);
            if (yaw == 0) Assert.Greater(sim.State.PosZ, endpoint);
            else Assert.Less(sim.State.PosX, endpoint);
        }

        [TestCase(-9.6f, 0f, -6f, 90f, -9.4f, true)]
        [TestCase(-.4f, 1.2f, 5f, -90f, -1.6f, false)]
        [TestCase(11.2f, 0f, -5.8f, 90f, 11.6f, true)]
        public void HighRampSideBlocksInsteadOfSwallowingTheBody(float x, float y, float z, float yaw, float edge, bool below)
        {
            foreach (bool crouch in new[] { false, true })
            {
                var sim = new BodySim { Collision = _collision }; sim.Initialize(yaw: yaw, posX: x, posZ: z, posY: y);
                for (int i = 0; i < 90; i++) sim.ApplyP1(new P1Input { MoveZ = 1, Crouch = crouch }, 1f / 60);
                float coordinate = yaw == 0 ? sim.State.PosZ : sim.State.PosX;
                if (below) Assert.LessOrEqual(coordinate, edge + .001f);
                else Assert.GreaterOrEqual(coordinate, edge - .001f);
                Assert.AreEqual(y, sim.State.PosY, .01f);
            }
        }

        [Test]
        public void LowRampSideRemainsSteppableAndBulletsRespectTheSlopedVolume()
        {
            var sim = new BodySim { Collision = _collision }; sim.Initialize(yaw: 90, posX: -10, posZ: -10, posY: 0);
            for (int i = 0; i < 45; i++) sim.ApplyP1(new P1Input { MoveZ = 1 }, 1f / 60);
            Assert.Greater(sim.State.PosX, -9); Assert.AreEqual(.2f, sim.State.PosY, .02f);
            Assert.IsTrue(_collision.RaycastSolids(-10, .5f, -6, 1, 0, 0, 4, out float hit));
            Assert.AreEqual(1, hit, .001f, "High ramp side is a real solid bullet entry");
            Assert.IsFalse(_collision.RaycastSolids(-10, .5f, -10, 1, 0, 0, 4, out _), "Do not block empty air above the low slope");
        }

        [Test]
        public void ServicePipeRequiresCrouchAndOpensAgainAfterCrossing()
        {
            var nav = new BotNavigation(); nav.Search(_collision, new Vector3(14.5f, 1.2f, -1));
            Assert.IsFalse(nav.Clear(new Vector3(14.5f, 1.2f, 1), 1.8f));
            Assert.IsTrue(nav.Clear(new Vector3(14.5f, 1.2f, 1), 1.15f));
            Assert.IsTrue(nav.Travel(new Vector3(14.5f, 1.2f, -1), new Vector3(14.5f, 1.2f, 3), out _, out bool crouch));
            Assert.IsTrue(crouch);
            var sim = new BodySim { Collision = _collision }; sim.Initialize(yaw: 0, posX: 14.5f, posZ: -1, posY: 1.2f);
            for (int i = 0; i < 160; i++) sim.ApplyP1(new P1Input { MoveZ = 1, Crouch = true }, 1f / 60);
            Assert.Greater(sim.State.PosZ, 2.2f);
            Assert.IsTrue(_collision.CanStand(sim.State.PosX, sim.State.PosY, sim.State.PosZ, 1.8f));
        }

        [Test]
        public void BothStartsHaveGeometryBasedRoutesOutToFightingSpace()
        {
            var nav = new BotNavigation();
            for (int team = 0; team < 2; team++)
            {
                _map.TryGetBodyPose(team, 0, out var start, out _); nav.Search(_collision, start);
                Assert.IsTrue(nav.Reachable.Any(i => team == 0 ? nav[i].Position.z < 8 : nav[i].Position.z > -5),
                    $"Team {team} must be able to leave its starting area using the existing bot navigator");
            }
            // The upper side also has a genuinely reachable side entrance, not only the yard door.
            nav.Search(_collision, new Vector3(4.5f, 2.4f, 13.5f));
            Assert.IsTrue(nav.Reachable.Any(i => nav[i].Position.x > 13 && nav[i].Position.y < 1.3f));
        }
    }
}
