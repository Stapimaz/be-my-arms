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
        [TestCase(11.2f, 0f, -7.5f, 90f, 11.6f, true)]
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

        [Test]
        public void QuayIsBentAndBackdropIsVisualOnly()
        {
            var roots = _scene.GetRootGameObjects();
            var arena = roots.Single(g => g.name == "Arena").transform;
            Assert.IsNull(arena.Find("BMA_Map_Solid_SeaBoundary"));
            Assert.IsNull(arena.Find("BMA_Map_Solid_QuayBoundary"));
            var floors = arena.Cast<Transform>().Where(t => t.name.StartsWith("BMA_Map_Floor")).ToArray();
            Assert.AreEqual(3, floors.Length, "Connected quay pads, not a continuous square base");
            Assert.Less(floors.Sum(t => t.localScale.x * t.localScale.z), 300);
            var backdrop = roots.Single(g => g.name == "Backdrop");
            Assert.GreaterOrEqual(backdrop.transform.childCount, 8);
            Assert.IsEmpty(backdrop.GetComponentsInChildren<Collider>());
            Assert.IsFalse(_collision.RaycastSolids(25, .25f, -25, 0, 0, 1, 10, out _), "Remote moored boat is not pretend gameplay cover");
        }

        [Test]
        public void MainQuayApproachRoundsTheSpurAndAlternateApproachReachesWorkshop()
        {
            var nav = new BotNavigation(); nav.Search(_collision, new Vector3(10, 0, -16));
            var main = new[] { new Vector3(10, 0, -16), new Vector3(10, 0, -14.2f), new Vector3(-5, 0, -14.2f),
                new Vector3(-5, 0, -12), new Vector3(-5, 0, -11), new Vector3(-6, 0, -11.5f), new Vector3(-6, 1.2f, -4) };
            var side = new[] { new Vector3(10, 0, -16), new Vector3(14.5f, 0, -12.5f), new Vector3(14.5f, 0, -10.5f),
                new Vector3(14.5f, 1.2f, -4.5f), new Vector3(14.5f, 1.2f, -1), new Vector3(14.5f, 1.2f, 3),
                new Vector3(14.5f, 1.2f, 13.5f), new Vector3(12.5f, 1.2f, 13.5f), new Vector3(4.5f, 2.4f, 13.5f) };
            foreach (var route in new[] { main, side })
                for (int i = 1; i < route.Length; i++)
                {
                    Assert.IsTrue(nav.Travel(route[i - 1], route[i], out var end, out _), $"Route segment {route[i - 1]} → {route[i]}");
                    Assert.AreEqual(route[i].y, end.y, .025f);
                }
            Assert.IsFalse(nav.Travel(new Vector3(10, 0, -8), new Vector3(0, 1.2f, 0), out _, out _),
                "The land spur/pump house must interrupt the old diagonal arena crossing");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SeaGuardCannotBeJumpedFromTheRaisedRamp(bool crouch)
        {
            float y = _collision.SurfaceHeight(15.5f, -5.8f, 1.25f);
            var sim = new BodySim { Collision = _collision }; sim.Initialize(yaw: 90, posX: 15.5f, posZ: -5.8f, posY: y);
            for (int i = 0; i < 120; i++)
                sim.ApplyP1(new P1Input { MoveZ = 1, Jump = i == 0, Crouch = crouch }, 1f / 60);
            Assert.LessOrEqual(sim.State.PosX, 16.61f, "Guard height must follow the rising walkway, not let players hop into the backdrop");
        }

        [Test]
        public void RampVisualsHaveHardPlanarFacesAndUsableTextureAndBakeCoordinates()
        {
            var arena = _scene.GetRootGameObjects().Single(g => g.name == "Arena").transform;
            foreach (var t in arena.Cast<Transform>().Where(t => t.name.StartsWith("BMA_Map_Ramp")))
            {
                var mesh = t.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(mesh, t.GetComponent<MeshCollider>().sharedMesh);
                Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, t.name);
                Assert.AreEqual(mesh.vertexCount, mesh.uv2.Length, t.name);
                Assert.AreEqual(mesh.vertexCount, mesh.tangents.Length, t.name);
                var vertices = mesh.vertices; var normals = mesh.normals; var triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).normalized;
                    foreach (int index in new[] { a, b, c })
                        Assert.Greater(Vector3.Dot(normal, normals[index]), .999f, t.name + " must not smooth normals across wedge edges");
                }
                Assert.Greater(mesh.uv.Distinct().Count(), 4);
            }
        }

        [Test]
        public void LookSampleIsSurfaceDetailNotNewGameplayOrCameraCollision()
        {
            var sample = _scene.GetRootGameObjects().Single(g => g.name == BoatyardLookSample.RootName);
            Assert.IsEmpty(sample.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(sample.GetComponentsInChildren<MapSpawns>(true));
            Assert.IsTrue(sample.GetComponentsInChildren<Renderer>().All(r => r.transform.root == sample.transform));
            Assert.Greater(sample.GetComponentsInChildren<Renderer>().Length, 10);
            var arena = _scene.GetRootGameObjects().Single(g => g.name == "Arena").transform;
            var roof = arena.Find("BMA_Map_Solid_WorkshopRoof");
            Assert.AreEqual(new Vector3(-3, 6.55f, 14), roof.position);
            Assert.AreEqual(new Vector3(18.5f, .3f, 10.5f), roof.localScale);
            Assert.IsNotNull(roof.GetComponent<BoxCollider>(), "Existing camera collider is not replaced by the visual bevel");
        }

        [Test]
        public void LookSampleHasBakedLightAndRestrainedGradingWithoutCinematicBlur()
        {
            var sample = _scene.GetRootGameObjects().Single(g => g.name == BoatyardLookSample.RootName);
            var volume = sample.GetComponentInChildren<UnityEngine.Rendering.Volume>();
            Assert.IsTrue(volume.isGlobal); Assert.IsNotNull(volume.sharedProfile);
            var profile = volume.sharedProfile;
            Assert.IsTrue(profile.TryGet<UnityEngine.Rendering.Universal.Tonemapping>(out var tone));
            Assert.AreEqual(UnityEngine.Rendering.Universal.TonemappingMode.Neutral, tone.mode.value);
            profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom); Assert.AreEqual(0, bloom.intensity.value);
            profile.TryGet<UnityEngine.Rendering.Universal.MotionBlur>(out var blur); Assert.AreEqual(0, blur.intensity.value);
            profile.TryGet<UnityEngine.Rendering.Universal.DepthOfField>(out var dof);
            Assert.AreEqual(UnityEngine.Rendering.Universal.DepthOfFieldMode.Off, dof.mode.value);
            var sun = _scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>()).Single(l => l.type == LightType.Directional);
            Assert.AreEqual(LightShadows.Soft, sun.shadows);
            Assert.AreEqual(LightmapBakeType.Mixed, sun.lightmapBakeType);
            Assert.Greater(LightmapSettings.lightmaps.Length, 0);
            Assert.IsNotNull(LightmapSettings.lightProbes);
            Assert.Greater(sample.GetComponentInChildren<LightProbeGroup>().probePositions.Length, 20);
            var reflections = sample.GetComponentsInChildren<ReflectionProbe>();
            Assert.AreEqual(2, reflections.Length);
            Assert.IsTrue(reflections.All(p => p.bakedTexture != null), "Reflection captures must be baked before delivery");
        }

        [TestCase("Concrete")]
        [TestCase("Plaster")]
        [TestCase("TealPaint")]
        [TestCase("SafetyOchre")]
        [TestCase("Steel")]
        public void LookSampleMajorSurfacesUseMatchedPbrMapsNotOnlyFlatColor(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BoatyardLookSample.AssetDir + "/" + name + ".mat");
            Assert.IsNotNull(material);
            Assert.IsTrue(material.IsKeywordEnabled("_NORMALMAP"));
            Assert.IsTrue(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
            {
                var texture = material.GetTexture(property);
                Assert.IsNotNull(texture, name + " " + property);
                Assert.GreaterOrEqual(texture.width, 2000); Assert.GreaterOrEqual(texture.height, 2000);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                Assert.AreEqual(property == "_BaseMap", importer.sRGBTexture, "Only color data is sRGB");
                Assert.IsFalse(importer.isReadable, "No retained CPU copy of delivered surface maps");
                Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapMode);
                if (property == "_BumpMap") Assert.AreEqual(TextureImporterType.NormalMap, importer.textureType);
                Assert.AreEqual(material.GetTextureScale("_BaseMap"), material.GetTextureScale(property), "Matched surface-map scale");
            }
            Assert.AreEqual(name == "Steel" ? 1 : 0, material.GetFloat("_Metallic"), "Paint is a dielectric coating, not colored bare metal");
        }

        [Test]
        public void LookSampleLocalBakedWorkshopLightsReplaceTheRejectedOversizedSign()
        {
            var sample = _scene.GetRootGameObjects().Single(g => g.name == BoatyardLookSample.RootName);
            Assert.IsNull(sample.transform.Find("WorkshopServiceSign"));
            var lights = sample.GetComponentsInChildren<Light>(); Assert.AreEqual(3, lights.Length);
            foreach (var light in lights)
            {
                Assert.AreEqual(LightType.Rectangle, light.type); Assert.AreEqual(LightmapBakeType.Baked, light.lightmapBakeType);
                Assert.Less(light.transform.forward.y, -.99f, "Task lights illuminate the room, not the roof");
                Assert.Greater(light.intensity, 0); Assert.Greater(light.areaSize.x, 1);
            }
            var panel = AssetDatabase.LoadAssetAtPath<Material>(BoatyardLookSample.AssetDir + "/WorkshopLightPanel.mat");
            Assert.IsTrue(panel.IsKeywordEnabled("_EMISSION"), "The visible fixture must look switched on");
            Assert.IsEmpty(sample.GetComponentsInChildren<Collider>());
            var sun = _scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>()).Single(l => l.type == LightType.Directional);
            Assert.AreEqual(1.5f, sun.intensity); Assert.Less(Quaternion.Angle(Quaternion.Euler(48, -38, 0), sun.transform.rotation), .01f);
        }

        [Test]
        public void ContinuityPassUsesSharedWorldUvAcrossDeckAndRampContacts()
        {
            var arena = _scene.GetRootGameObjects().Single(g => g.name == "Arena").transform;
            foreach (var t in arena.Cast<Transform>().Where(t => t.name.StartsWith("BMA_Map_Platform") || t.name.StartsWith("BMA_Map_Floor") || t.name.StartsWith("BMA_Map_Ramp")))
            {
                var mesh = t.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual("Concrete", t.GetComponent<Renderer>().sharedMaterial.name, t.name);
                var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv;
                for (int i = 0; i < v.Length; i++)
                    if (Mathf.Abs(t.TransformDirection(n[i]).y) > .5f)
                    {
                        Vector3 p = t.TransformPoint(v[i]);
                        Assert.Less(Vector2.Distance(new Vector2(p.x, p.z) / 12, uv[i]), .00001f, "Surface coordinates must not restart/rotate at " + t.name);
                    }
            }
            var concrete = AssetDatabase.LoadAssetAtPath<Material>(BoatyardLookSample.AssetDir + "/Concrete.mat");
            Assert.AreEqual(4096, concrete.GetTexture("_BaseMap").width, "Large-repeat matched surface variation");
            var foundation = arena.Find("BMA_Map_Platform_LoadingDock").GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(24, foundation.vertexCount, "No per-block contact-edge bevel on joined construction");
        }

        [Test]
        public void ContinuityPassFinishesExistingObjectsWithoutIndependentCover()
        {
            var roots = _scene.GetRootGameObjects();
            var detail = roots.Single(g => g.name == BoatyardContinuityPass.RootName);
            Assert.IsEmpty(detail.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(detail.GetComponentsInChildren<MapSpawns>(true));
            Assert.IsNotNull(detail.transform.Find("WorkshopRoofFascia"));
            Assert.IsNotNull(detail.transform.Find("HullMouldRim"));
            foreach (var renderer in roots.Single(g => g.name == "Arena").GetComponentsInChildren<Renderer>())
                Assert.IsFalse(new[] { "Ground", "Structure", "Equipment", "Railing" }.Contains(renderer.sharedMaterial.name), renderer.name + " still uses the blockout material");
        }
    }
}
