using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Pipeline.Commands;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BeMyArms.QA
{
    /// <summary>
    /// Development-time visual QA commands for the real BeMyArms player build.
    ///
    /// These are <c>RuntimeOnly</c> Pipeline commands: the running Editor hides them from its
    /// command listing, while a development Player exposes them and they are reached with
    /// <c>unity command &lt;name&gt; --runtime &lt;player&gt;</c> or <c>--runtime-path &lt;port file&gt;</c>.
    /// They act on the production UI and its real callbacks, so driving them exercises the same
    /// flow the player uses rather than a parallel test path.
    ///
    /// The whole assembly is compiled only when the Pipeline runtime is in the build
    /// (development build / ENABLE_RUNTIME_PIPELINE), matching the package's own constraint.
    /// </summary>
    public static class BeMyArmsQaCommands
    {
        /// <summary>Sub-folder (relative to the player root) that captures default to.</summary>
        const string QaFolderName = "QA";

        // ---- Capture -------------------------------------------------------------------------

        [CliCommand("qa_capture_frame",
            "Capture the current rendered player frame, including screen-space (overlay) UI, to a PNG and return its path.",
            MainThreadRequired = true, RuntimeOnly = true, Tags = new[] { "qa", "capture" })]
        public static QaCaptureResult CaptureFrame(
            [CliArg("output", "Output PNG path: absolute, or relative to the player root. Defaults to <player root>/QA/frame_<scene>_<timestamp>.png.")] string output = "",
            [CliArg("include_inline", "Also return the PNG inline as base64 (default false; path-only is cheaper).")] bool includeInline = false)
        {
            try
            {
                Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null)
                    return QaCaptureResult.Fail("ScreenCapture.CaptureScreenshotAsTexture() returned null.");

                int width = texture.width;
                int height = texture.height;
                byte[] png = texture.EncodeToPNG();
                Object.Destroy(texture);

                string path = ResolveOutputPath(output, "frame");
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, png);

                return new QaCaptureResult
                {
                    Success = true,
                    Path = path,
                    Scene = SceneManager.GetActiveScene().name,
                    Width = width,
                    Height = height,
                    Bytes = png.Length,
                    InlineBase64 = includeInline ? Convert.ToBase64String(png) : null
                };
            }
            catch (Exception ex)
            {
                return QaCaptureResult.Fail(ex.ToString());
            }
        }

        // ---- Interaction ---------------------------------------------------------------------

        [CliCommand("qa_click_button",
            "Invoke an active Unity UI Button by GameObject name through its real Button.onClick callback.",
            MainThreadRequired = true, RuntimeOnly = true, Tags = new[] { "qa", "ui" })]
        public static QaButtonResult ClickButton(
            [CliArg("name", "GameObject name of the active Button to activate (case-insensitive).", Required = true)] string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return QaButtonResult.Fail("A button name is required.", DescribeActiveButtons());

                List<Button> matches = FindActiveButtons()
                    .Where(b => string.Equals(b.gameObject.name, name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                    return QaButtonResult.Fail($"No active Button named '{name}'.", DescribeActiveButtons());

                Button button = matches[0];
                if (!button.interactable)
                    return QaButtonResult.Fail($"Button '{name}' is not interactable.", DescribeActiveButtons());

                QaButtonInfo info = Describe(button);
                button.onClick.Invoke();
                return new QaButtonResult { Success = true, Clicked = info, MatchCount = matches.Count };
            }
            catch (Exception ex)
            {
                return QaButtonResult.Fail(ex.ToString(), DescribeActiveButtons());
            }
        }

        // ---- Inspection ----------------------------------------------------------------------

        [CliCommand("qa_ui_state",
            "Inspect runtime UI state: active scene, resolution, canvases and active Unity UI buttons (with on-screen visibility).",
            MainThreadRequired = true, RuntimeOnly = true, Tags = new[] { "qa", "ui" })]
        public static QaUiStateResult UiState()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                List<QaButtonInfo> buttons = FindActiveButtons().Select(Describe).ToList();
                List<QaCanvasInfo> canvases = Object
                    .FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(c => c != null && c.isActiveAndEnabled)
                    .Select(c => new QaCanvasInfo
                    {
                        Name = c.gameObject.name,
                        RenderMode = c.renderMode.ToString(),
                        SortingOrder = c.sortingOrder
                    })
                    .ToList();

                return new QaUiStateResult
                {
                    Success = true,
                    Scene = scene.name,
                    ScenePath = scene.path,
                    Resolution = new[] { Screen.width, Screen.height },
                    FrameCount = Time.frameCount,
                    Buttons = buttons,
                    Canvases = canvases
                };
            }
            catch (Exception ex)
            {
                return QaUiStateResult.Fail(ex.ToString());
            }
        }

        // ---- Helpers -------------------------------------------------------------------------

        static IEnumerable<Button> FindActiveButtons()
            => Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(b => b != null && b.isActiveAndEnabled);

        static QaButtonInfo Describe(Button button)
        {
            Text label = button.GetComponentInChildren<Text>(true);
            RectTransform rect = button.transform as RectTransform;

            float centerX = 0f, centerY = 0f;
            bool onScreen = false;
            if (rect != null)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                centerX = (corners[0].x + corners[2].x) * 0.5f;
                centerY = (corners[0].y + corners[2].y) * 0.5f;
                onScreen = centerX >= 0f && centerX <= Screen.width && centerY >= 0f && centerY <= Screen.height;
            }

            return new QaButtonInfo
            {
                Name = button.gameObject.name,
                Text = label != null ? label.text : null,
                Interactable = button.interactable,
                ScreenCenter = new[] { Mathf.Round(centerX), Mathf.Round(centerY) },
                OnScreen = onScreen,
                Path = HierarchyPath(button.transform)
            };
        }

        static string DescribeActiveButtons()
        {
            string[] names = FindActiveButtons().Select(b => b.gameObject.name).Distinct().ToArray();
            return names.Length > 0
                ? "Active buttons: " + string.Join(", ", names)
                : "There are no active Unity UI buttons.";
        }

        static string HierarchyPath(Transform transform)
        {
            var parts = new List<string>();
            for (Transform t = transform; t != null; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>Player root: the folder containing the build (the parent of the *_Data folder).</summary>
        static string PlayerRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string ResolveOutputPath(string output, string prefix)
        {
            if (!string.IsNullOrWhiteSpace(output))
                return Path.IsPathRooted(output) ? output : Path.GetFullPath(Path.Combine(PlayerRoot, output));

            string scene = Sanitize(SceneManager.GetActiveScene().name);
            string file = $"{prefix}_{scene}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            return Path.Combine(PlayerRoot, QaFolderName, file);
        }

        static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";
            var chars = value.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray();
            return new string(chars);
        }
    }

    // ---- Response models (serialized by the Pipeline command envelope) -----------------------

    [Serializable]
    public class QaCaptureResult
    {
        public bool Success { get; set; }
        public string Path { get; set; }
        public string Scene { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Bytes { get; set; }
        public string InlineBase64 { get; set; }
        public string Error { get; set; }

        public static QaCaptureResult Fail(string error)
            => new QaCaptureResult { Success = false, Error = error };
    }

    [Serializable]
    public class QaButtonInfo
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public bool Interactable { get; set; }
        public float[] ScreenCenter { get; set; }
        public bool OnScreen { get; set; }
        public string Path { get; set; }
    }

    [Serializable]
    public class QaButtonResult
    {
        public bool Success { get; set; }
        public QaButtonInfo Clicked { get; set; }
        public int MatchCount { get; set; }
        public string Error { get; set; }
        public string ActiveButtons { get; set; }

        public static QaButtonResult Fail(string error, string activeButtons = null)
            => new QaButtonResult { Success = false, Error = error, ActiveButtons = activeButtons };
    }

    [Serializable]
    public class QaCanvasInfo
    {
        public string Name { get; set; }
        public string RenderMode { get; set; }
        public int SortingOrder { get; set; }
    }

    [Serializable]
    public class QaUiStateResult
    {
        public bool Success { get; set; }
        public string Scene { get; set; }
        public string ScenePath { get; set; }
        public int[] Resolution { get; set; }
        public int FrameCount { get; set; }
        public List<QaButtonInfo> Buttons { get; set; }
        public List<QaCanvasInfo> Canvases { get; set; }
        public string Error { get; set; }

        public static QaUiStateResult Fail(string error)
            => new QaUiStateResult { Success = false, Error = error };
    }
}
