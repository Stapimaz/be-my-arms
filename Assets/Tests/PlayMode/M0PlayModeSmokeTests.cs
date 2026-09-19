using System.Collections;
using BeMyArms.M0;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BeMyArms.M0.Tests
{
    /// <summary>
    /// Runtime smoke test: the generated scene boots, the orchestrator wires up, and the
    /// core coupling holds in a live frame loop.
    /// </summary>
    public class M0PlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator Scene_Boots_And_Runs_Without_Errors()
        {
            SceneManager.LoadScene("M0SharedBody");
            yield return null;
            yield return null;
            yield return null;

            GameObject rootGo = GameObject.Find("SharedBody");
            Assert.IsNotNull(rootGo, "SharedBody not found after load.");

            var root = rootGo.GetComponent<M0BodyRoot>();
            Assert.IsNotNull(root, "M0BodyRoot missing.");
            Assert.IsNotNull(root.body, "body");
            Assert.IsNotNull(root.aim, "aim");
            Assert.IsTrue(float.IsFinite(root.body.BodyYaw), "BodyYaw should be finite.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Coupling_Holds_When_Stepped()
        {
            SceneManager.LoadScene("M0SharedBody");
            yield return null;

            var root = GameObject.Find("SharedBody").GetComponent<M0BodyRoot>();
            SharedBodyController body = root.body;
            P2AimController aim = root.aim;

            aim.Initialize(body.BodyYaw);
            float startYaw = body.BodyYaw;

            // P2 input cannot move the body; the aim changes instead.
            aim.Step(new P2Command { YawDelta = 30f }, body.BodyYaw);
            Assert.AreEqual(startYaw, body.BodyYaw, 0.0001f, "P2 input moved the body.");
            Assert.AreEqual(startYaw + 30f, aim.DesiredWorldYaw, 0.0001f, "Aim did not move.");

            // P1 rotates the body: the aim stays world-stable while inside the sector.
            body.Step(new P1Command { LookYawDelta = 40f }, 0.016f);
            Assert.AreEqual(startYaw + 40f, body.BodyYaw, 0.0001f, "Body did not rotate.");
            Assert.AreEqual(startYaw + 30f, aim.DesiredWorldYaw, 0.0001f, "Aim was dragged by body rotation.");

            LogAssert.NoUnexpectedReceived();
        }
    }
}
