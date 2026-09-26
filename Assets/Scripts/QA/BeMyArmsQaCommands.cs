using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeMyArms.M2;
using BeMyArms.M3;
using BeMyArms.M7;
using Unity.Netcode;
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

        // ---- Input injection (development) ---------------------------------------------------

        [CliCommand("qa_inject_look",
            "Development only: inject a look/aim delta (degrees, positive yaw = right, positive pitch = up) into the local input path for the next frame. Exercises the real input -> prediction -> camera path without a physical mouse.",
            MainThreadRequired = true, RuntimeOnly = true, Tags = new[] { "qa", "input" })]
        public static QaSimpleResult InjectLook(
            [CliArg("yaw", "Yaw delta in degrees (positive = look right).")] float yaw = 0f,
            [CliArg("pitch", "Pitch delta in degrees (positive = look up).")] float pitch = 0f)
        {
            M3LocalInput.InjectedLookYaw += yaw;
            M3LocalInput.InjectedLookPitch += pitch;
            return new QaSimpleResult { Success = true, Detail = $"queued yaw={yaw} pitch={pitch}" };
        }

        [CliCommand("qa_inject_input",
            "Development only: set held local movement/fire injection (same path and same Buy/live gating as real input). Values persist until changed; set 0/false to release.",
            MainThreadRequired = true, RuntimeOnly = true, Tags = new[] { "qa", "input" })]
        public static QaSimpleResult InjectInput(
            [CliArg("movex", "Body-relative strafe [-1..1]; 0 releases.")] float moveX = 0f,
            [CliArg("movez", "Body-relative forward [-1..1]; 0 releases.")] float moveZ = 0f,
            [CliArg("fire", "Hold the trigger while true.")] bool fire = false)
        {
            M3LocalInput.InjectedMoveX = moveX;
            M3LocalInput.InjectedMoveZ = moveZ;
            M3LocalInput.InjectedFire = fire;
            return new QaSimpleResult { Success = true, Detail = $"move=({moveX},{moveZ}) fire={fire}" };
        }

        // ---- Structured gameplay state -------------------------------------------------------

        [CliCommand("qa_player_state",
            "Compact structured local-player state for gameplay debugging: local slot/body, match phase, input mode/cursor, camera, look/aim, combined-body parts and viewport visibility, weapon/viewmodel, and duplicate objects.",
            MainThreadRequired = true, RuntimeOnly = true, Tags = new[] { "qa", "player" })]
        public static QaPlayerStateResult PlayerState()
        {
            var result = new QaPlayerStateResult { Success = true };
            try
            {
                result.Scene = SceneManager.GetActiveScene().name;

                M3DuelDirector director = M3DuelDirector.Instance;
                result.MatchPhase = director != null ? director.CurrentPhase.ToString() : "none";
                result.MatchLive = director != null && director.IsLive;

                M3DuelClient[] clients = Object.FindObjectsByType<M3DuelClient>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                M3DuelBody[] bodies = Object.FindObjectsByType<M3DuelBody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                result.ClientCount = clients.Length;
                result.BodyCount = bodies.Length;

                M3DuelClient local = null;
                int localMatches = 0;
                for (int i = 0; i < clients.Length; i++)
                {
                    if (clients[i] == null || !clients[i].IsLocalOwnBody) continue;
                    localMatches++;
                    if (local == null) local = clients[i];
                }
                result.LocalClientMatches = localMatches;
                result.LocalSlot = M3DuelClient.LocalSlotIndex;

                if (local != null)
                {
                    result.OwnBodyResolved = true;
                    result.LocalTeam = local.LocalTeam;
                    result.LocalBodyIndex = local.LocalBody;
                    result.LocalRole = local.LocalRoleIndex;
                    result.LookYaw = Round(local.LocalLookYaw);
                    result.LookPitch = Round(local.LocalLookPitch);
                    result.AimYaw = Round(local.LocalAimYaw);
                    result.AimPitch = Round(local.LocalAimPitch);
                    result.LastMouseDelta = new[] { Round(local.LastMouseDelta.x), Round(local.LastMouseDelta.y) };
                    result.SimLookYaw = Round(local.SimLookYaw);
                    result.P1Sequence = local.P1Sequence;
                    result.LastAckedP1 = local.LastAckedP1;
                    result.PendingP1Inputs = local.PendingP1Inputs;

                    M2BodyState state = local.ViewState;
                    result.BodyPosition = V3(state.PosX, state.PosY, state.PosZ);
                    result.BodyYaw = Round(state.BodyYaw);

                    if (local.Body != null)
                    {
                        result.BodyAlive = local.Body.Alive.Value;
                        result.ActiveWeapon = ((M3WeaponId)local.Body.WeaponId.Value).ToString();
                        result.Ammo = local.Body.Ammo.Value;
                        result.LocalShots = local.TotalLocalShots;

                        M7CharacterAnimator animator = local.Body.GetComponent<M7CharacterAnimator>();
                        result.P1 = DescribeGroup(animator != null ? animator.P1Skin : null);
                        result.P2 = DescribeGroup(animator != null ? animator.P2Skin : null);
                        result.WeaponPart = DescribeGroup(animator != null ? animator.Weapon : null);

                        Renderer[] renderers = local.Body.GetComponentsInChildren<Renderer>(true);
                        result.BodyRenderedParts = renderers.Length;
                        if (renderers.Length > 0)
                        {
                            Bounds bounds = renderers[0].bounds;
                            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                            result.BodyBoundsCenter = V3(bounds.center.x, bounds.center.y, bounds.center.z);
                            result.BodyBoundsSize = V3(bounds.size.x, bounds.size.y, bounds.size.z);
                        }
                    }
                }

                M7LocalPlayer[] players = Object.FindObjectsByType<M7LocalPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                result.LocalPlayerCount = players.Length;
                M7LocalPlayer player = players.Length > 0 ? players[0] : null;
                Camera camera = player != null && player.LocalCamera != null ? player.LocalCamera : Camera.main;
                if (camera != null)
                {
                    result.CameraName = camera.name;
                    Vector3 p = camera.transform.position;
                    Vector3 e = camera.transform.eulerAngles;
                    result.CameraPosition = V3(p.x, p.y, p.z);
                    result.CameraEuler = V3(e.x, e.y, e.z);
                    result.CameraFov = Round(camera.fieldOfView);
                    result.CameraCullingMask = camera.cullingMask;
                    if (result.BodyBoundsSize != null)
                    {
                        var bounds = new Bounds(
                            new Vector3(result.BodyBoundsCenter[0], result.BodyBoundsCenter[1], result.BodyBoundsCenter[2]),
                            new Vector3(result.BodyBoundsSize[0], result.BodyBoundsSize[1], result.BodyBoundsSize[2]));
                        result.BodyInViewport = BoundsInViewport(camera, bounds);
                    }
                }

                result.InputGameplayActive = M3LocalInput.GameplayActive;
                result.InputCursorCaptured = M3LocalInput.CursorCaptured;
                result.CursorLock = Cursor.lockState.ToString();
                result.CursorVisible = Cursor.visible;
                result.ApplicationFocused = Application.isFocused;
                result.MouseSensitivity = Round(M3LocalInput.MouseSensitivity);

                result.ViewmodelCount = CountObjectsNamed("M7_P2Viewmodel");
                result.ViewmodelWeaponCount = CountObjectsNamed("ViewmodelWeapon");
                int cameraCount = 0;
                foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (cam.name != "M7_ViewModelCamera") cameraCount++; // the FP viewmodel overlay is expected
                result.CameraCount = cameraCount;

                var duplicates = new List<string>();
                var bodyCounts = new Dictionary<string, int>();
                for (int i = 0; i < bodies.Length; i++)
                {
                    string key = bodies[i].TeamIndex + ":" + bodies[i].BodyId;
                    bodyCounts[key] = bodyCounts.TryGetValue(key, out int count) ? count + 1 : 1;
                }
                foreach (KeyValuePair<string, int> kv in bodyCounts)
                    if (kv.Value > 1) duplicates.Add($"body {kv.Key} x{kv.Value}");
                if (localMatches > 1) duplicates.Add($"local clients x{localMatches}");
                if (players.Length > 1) duplicates.Add($"M7LocalPlayer x{players.Length}");
                if (result.CameraCount > 1) duplicates.Add($"cameras x{result.CameraCount}");
                if (result.ViewmodelCount > 1) duplicates.Add($"viewmodels x{result.ViewmodelCount}");
                result.Duplicates = duplicates;
                result.DuplicateSummary = duplicates.Count == 0 ? "none" : string.Join("; ", duplicates);

                result.Bodies = bodies.Select(b => new QaBodyInstance
                {
                    Team = b.TeamIndex,
                    Body = b.BodyId,
                    Alive = b.Alive.Value,
                    Scene = b.gameObject.scene.name,
                    Root = b.transform.parent != null ? b.transform.parent.name : "(scene root)",
                    Position = V3(b.transform.position.x, b.transform.position.y, b.transform.position.z)
                }).ToList();

                result.Managers = Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Select(m => new QaManagerInstance
                    {
                        Name = m.gameObject.name,
                        Scene = m.gameObject.scene.name,
                        IsListening = m.IsListening,
                        IsClient = m.IsClient,
                        IsServer = m.IsServer
                    }).ToList();

                int sceneCount = SceneManager.sceneCount;
                var scenes = new List<string>();
                for (int i = 0; i < sceneCount; i++) scenes.Add(SceneManager.GetSceneAt(i).name);
                result.Scenes = scenes;

                result.Summary = BuildSummary(result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.ToString();
            }
            return result;
        }

        static string BuildSummary(QaPlayerStateResult r)
        {
            if (!r.OwnBodyResolved)
                return $"scene={r.Scene} phase={r.MatchPhase} localBody=NONE cli={r.ClientCount} bodies={r.BodyCount} dupes={r.DuplicateSummary}";
            string p1 = r.P1 != null && r.P1.Present ? (r.P1.WorldHeight > 0.2f ? "ok" : "COLLAPSED") : "missing";
            string p2 = r.P2 != null && r.P2.Present ? (r.P2.WorldHeight > 0.2f ? "ok" : "COLLAPSED") : "missing";
            string w = r.WeaponPart != null && r.WeaponPart.Present ? "ok" : "missing";
            return $"scene={r.Scene} phase={r.MatchPhase} role=P{r.LocalRole + 1} slot={r.LocalSlot} " +
                   $"drawn={r.BodyRenderedParts} p1={p1} p2={p2} weapon={w} bboxH={r.BodyBoundsSize?[1]} inView={r.BodyInViewport} " +
                   $"cam={r.CameraName} input={r.InputGameplayActive} cursor={r.CursorLock}/{r.CursorVisible} focused={r.ApplicationFocused} " +
                   $"vm={r.ViewmodelCount} dupes={r.DuplicateSummary}";
        }

        static QaPartInfo DescribeGroup(Transform root)
        {
            var info = new QaPartInfo { Present = root != null };
            if (root == null) return info;

            info.Name = root.name;
            info.Active = root.gameObject.activeInHierarchy;
            info.Layer = root.gameObject.layer;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            info.Renderers = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].enabled) info.EnabledRenderers++;
                if (renderers[i].gameObject.layer != 0) info.NonDefaultLayerRenderers++;
            }
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                info.BoundsCenter = V3(bounds.center.x, bounds.center.y, bounds.center.z);
                info.BoundsSize = V3(bounds.size.x, bounds.size.y, bounds.size.z);
                info.WorldHeight = Round(bounds.size.y);
            }
            return info;
        }

        static int CountObjectsNamed(string name)
        {
            int count = 0;
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == name) count++;
            return count;
        }

        static bool BoundsInViewport(Camera camera, Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = center + new Vector3(
                    (i & 1) == 0 ? -extents.x : extents.x,
                    (i & 2) == 0 ? -extents.y : extents.y,
                    (i & 4) == 0 ? -extents.z : extents.z);
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
                    return true;
            }
            return false;
        }

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        static float[] V3(float x, float y, float z) => new[] { Round(x), Round(y), Round(z) };

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

    /// <summary>Generic success/detail result for small QA commands.</summary>
    [Serializable]
    public class QaSimpleResult
    {
        public bool Success { get; set; }
        public string Detail { get; set; }
        public string Error { get; set; }
    }

    /// <summary>Renderer summary for one part of the combined body (P1, P2, weapon).</summary>
    [Serializable]
    public class QaPartInfo    {
        public bool Present { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public int Layer { get; set; }
        public int Renderers { get; set; }
        public int EnabledRenderers { get; set; }
        public int NonDefaultLayerRenderers { get; set; }
        public float[] BoundsCenter { get; set; }
        public float[] BoundsSize { get; set; }
        public float WorldHeight { get; set; }
    }

    /// <summary>
    /// Compact structured local-player state. Intended for cheap textual/JSON debugging during
    /// development instead of routine screenshot inspection.
    /// </summary>
    [Serializable]
    public class QaPlayerStateResult
    {
        public bool Success { get; set; }
        public string Summary { get; set; }
        public string Error { get; set; }

        public string Scene { get; set; }
        public string MatchPhase { get; set; }
        public bool MatchLive { get; set; }

        public int LocalSlot { get; set; }
        public bool OwnBodyResolved { get; set; }
        public int LocalTeam { get; set; }
        public int LocalBodyIndex { get; set; }
        public int LocalRole { get; set; }
        public int LocalClientMatches { get; set; }
        public int ClientCount { get; set; }
        public int BodyCount { get; set; }
        public int LocalPlayerCount { get; set; }

        public bool BodyAlive { get; set; }
        public string ActiveWeapon { get; set; }
        public int Ammo { get; set; }
        public int LocalShots { get; set; }
        public float[] BodyPosition { get; set; }
        public float BodyYaw { get; set; }

        public float LookYaw { get; set; }
        public float LookPitch { get; set; }
        public float AimYaw { get; set; }
        public float AimPitch { get; set; }
        public float[] LastMouseDelta { get; set; }
        public float SimLookYaw { get; set; }
        public uint P1Sequence { get; set; }
        public uint LastAckedP1 { get; set; }
        public int PendingP1Inputs { get; set; }

        public string CameraName { get; set; }
        public float[] CameraPosition { get; set; }
        public float[] CameraEuler { get; set; }
        public float CameraFov { get; set; }
        public int CameraCullingMask { get; set; }
        public int CameraCount { get; set; }
        public bool BodyInViewport { get; set; }
        public float[] BodyBoundsCenter { get; set; }
        public float[] BodyBoundsSize { get; set; }
        public int BodyRenderedParts { get; set; }

        public QaPartInfo P1 { get; set; }
        public QaPartInfo P2 { get; set; }
        public QaPartInfo WeaponPart { get; set; }

        public int ViewmodelCount { get; set; }
        public int ViewmodelWeaponCount { get; set; }

        public bool InputGameplayActive { get; set; }
        public bool InputCursorCaptured { get; set; }
        public string CursorLock { get; set; }
        public bool CursorVisible { get; set; }
        public bool ApplicationFocused { get; set; }
        public float MouseSensitivity { get; set; }

        public List<string> Duplicates { get; set; }
        public string DuplicateSummary { get; set; }
        public List<QaBodyInstance> Bodies { get; set; }
        public List<QaManagerInstance> Managers { get; set; }
        public List<string> Scenes { get; set; }
    }

    /// <summary>One replicated body instance, with the scene/root it actually lives in.</summary>
    [Serializable]
    public class QaBodyInstance
    {
        public int Team { get; set; }
        public int Body { get; set; }
        public bool Alive { get; set; }
        public string Scene { get; set; }
        public string Root { get; set; }
        public float[] Position { get; set; }
    }

    /// <summary>One NetworkManager instance, with its scene and role.</summary>
    [Serializable]
    public class QaManagerInstance
    {
        public string Name { get; set; }
        public string Scene { get; set; }
        public bool IsListening { get; set; }
        public bool IsClient { get; set; }
        public bool IsServer { get; set; }
    }
}
