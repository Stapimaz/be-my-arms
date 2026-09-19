using System.IO;
using BeMyArms.M0;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.M0.Tests
{
    /// <summary>
    /// Verifies the generated M0 scene exists and is wired, and that the layout actually
    /// satisfies acceptance criterion 2 (a target that starts outside the firing sector).
    /// </summary>
    public class M0SceneWiringTests
    {
        const string ScenePath = "Assets/Scenes/M0SharedBody.unity";

        [Test]
        public void SceneAssetExists()
        {
            Assert.IsTrue(File.Exists(ScenePath),
                $"Missing {ScenePath}. Run 'Be My Arms > M0 > Build M0 Scene'.");
        }

        [Test]
        public void SceneIsWiredCorrectly()
        {
            Assert.IsTrue(File.Exists(ScenePath), $"Missing {ScenePath}.");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject rootGo = GameObject.Find("SharedBody");
                Assert.IsNotNull(rootGo, "SharedBody root not found.");

                var root = rootGo.GetComponent<M0BodyRoot>();
                Assert.IsNotNull(root, "M0BodyRoot missing.");
                Assert.IsNotNull(root.tuning, "tuning");
                Assert.IsNotNull(root.body, "body");
                Assert.IsNotNull(root.aim, "aim");
                Assert.IsNotNull(root.weapon, "weapon");
                Assert.IsNotNull(root.health, "health");
                Assert.IsNotNull(root.aimEye, "aimEye");
                Assert.IsNotNull(root.p1Camera, "p1Camera");
                Assert.IsNotNull(root.p1InputBehaviour, "p1InputBehaviour");
                Assert.IsNotNull(root.p2DeviceInputBehaviour, "p2DeviceInputBehaviour");
                Assert.IsNotNull(root.p2ScriptedInputBehaviour, "p2ScriptedInputBehaviour");

                Assert.IsNotNull(rootGo.GetComponent<SharedBodyHealth>(), "SharedBodyHealth");
                Assert.IsNotNull(rootGo.GetComponent<CharacterController>(), "CharacterController");
                Assert.IsNotNull(GameObject.Find("P1_Head"), "P1_Head");
                Assert.IsNotNull(GameObject.Find("P2_ShoulderAnchor"), "P2_ShoulderAnchor");
                Assert.IsNotNull(GameObject.Find("P1_ThirdPersonCamera"), "P1 camera");
                Assert.IsNotNull(GameObject.Find("P2_FirstPersonCamera"), "P2 camera");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void NeedsRotationDummy_StartsOutsideTheSector()
        {
            Assert.IsTrue(File.Exists(ScenePath), $"Missing {ScenePath}.");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject inside = GameObject.Find("Dummy_Inside");
                Assert.IsNotNull(inside, "Dummy_Inside");
                float insideBearing = Bearing(inside.transform.position);
                Assert.Less(Mathf.Abs(insideBearing), 70f, "Dummy_Inside should start inside the sector.");

                GameObject needsRotation = GameObject.Find("Dummy_NeedsRotation");
                Assert.IsNotNull(needsRotation, "Dummy_NeedsRotation");
                float bearing = Bearing(needsRotation.transform.position);
                Assert.Greater(Mathf.Abs(bearing), 70f, "Dummy_NeedsRotation must start outside the sector.");
                Assert.AreEqual(110f, Mathf.Abs(bearing), 1f, "Dummy_NeedsRotation bearing.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static float Bearing(Vector3 position)
            => Mathf.Atan2(position.x, position.z) * Mathf.Rad2Deg;
    }
}
