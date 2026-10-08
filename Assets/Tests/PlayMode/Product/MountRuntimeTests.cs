using System.Collections;
using System.Text.RegularExpressions;
using BeMyArms.Product;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BeMyArms.Product.Tests
{
    /// <summary>Runtime proof that the cosmetic contract validation runs in a live player loop.</summary>
    public class MountRuntimeTests
    {
        [UnityTest]
        public IEnumerator MountDemo_ValidatesEveryCombination_AtRuntime()
        {
            var go = new GameObject("MountDemo");
            var demo = go.AddComponent<MountDemo>();
            demo.VariantsPerRole = 3;
            demo.RunOnStart = false;

            LogAssert.Expect(LogType.Log, new Regex(@"\[Product\] mount matrix 9/9 combinations valid"));
            demo.Run();
            yield return null;

            Assert.IsTrue(demo.Completed, "demo did not complete");
            Assert.AreEqual(9, demo.CombinationsValidated, "3 P1 skins x 3 P2 skins");
            Assert.IsTrue(demo.AllValid, demo.LastReport);

            Object.Destroy(go);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
