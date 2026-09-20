using System.Collections;
using System.Linq;
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

        [UnityTest]
        public IEnumerator MenuController_PlacesMainMenuButtonsOnScreen()
        {
            var go = new GameObject("m7_menu_layout_test");
            go.AddComponent<M7MenuController>();
            yield return null;

            AssertButtonsVisible("PLAY", "SETTINGS", "QUIT");

            // The real callback navigates to the private lobby; the lobby's own controls must be
            // laid out on-screen too (the same bug class that hid the main-menu buttons).
            FindButton("PLAY").onClick.Invoke();
            yield return null; // menu clears on end-of-frame Destroy, lobby builds in the callback

            AssertButtonsVisible("Duel", "TwoVsTwo", "P1", "P2", "Start", "Back");

            Object.Destroy(go);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        static Button FindButton(string name)
        {
            Button button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name);
            Assert.NotNull(button, $"expected an active Button named '{name}'");
            return button;
        }

        static void AssertButtonsVisible(params string[] names)
        {
            foreach (string name in names)
            {
                Button button = FindButton(name);
                var rect = button.image.rectTransform;

                // Anchors/pivots must stay normalized; a non-normalized anchor is what pushed the
                // main-menu buttons hundreds of screens off the canvas before this regression guard.
                Assert.That(rect.anchorMin.x, Is.InRange(0f, 1f), $"{name} anchorMin.x must be normalized");
                Assert.That(rect.anchorMin.y, Is.InRange(0f, 1f), $"{name} anchorMin.y must be normalized");
                Assert.That(rect.anchorMax.x, Is.InRange(0f, 1f), $"{name} anchorMax.x must be normalized");
                Assert.That(rect.anchorMax.y, Is.InRange(0f, 1f), $"{name} anchorMax.y must be normalized");

                if (Screen.width <= 1 || Screen.height <= 1)
                    continue; // headless/no game view: the anchor guard above is the meaningful check

                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                float centerX = (corners[0].x + corners[2].x) * 0.5f;
                float centerY = (corners[0].y + corners[2].y) * 0.5f;
                Assert.That(centerX, Is.InRange(-1f, Screen.width + 1f), $"{name} must be horizontally on-screen");
                Assert.That(centerY, Is.InRange(-1f, Screen.height + 1f), $"{name} must be vertically on-screen");
            }
        }
    }
}
