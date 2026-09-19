using System.Collections;
using BeMyArms.M7;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BeMyArms.M7.Tests
{
    /// <summary>Runtime smoke proof that the player-facing menu builds (foundation for the M8 UI).</summary>
    public class M7MenuRuntimeTests
    {
        [UnityTest]
        public IEnumerator MenuController_BuildsMainMenu()
        {
            var go = new GameObject("m7_menu_test");
            go.AddComponent<M7MenuController>();
            yield return null;

            Button[] buttons = Object.FindObjectsByType<Button>();
            Assert.GreaterOrEqual(buttons.Length, 3, "expected PLAY / SETTINGS / QUIT");
            bool hasPlay = false, hasQuit = false;
            foreach (Button button in buttons)
            {
                if (button.name == "PLAY") hasPlay = true;
                if (button.name == "QUIT") hasQuit = true;
            }
            Assert.IsTrue(hasPlay, "main menu should expose PLAY");
            Assert.IsTrue(hasQuit, "main menu should expose QUIT");

            Object.Destroy(go);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
