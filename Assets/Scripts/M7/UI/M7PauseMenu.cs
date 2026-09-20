using System;
using UnityEngine;
using UnityEngine.UI;

namespace BeMyArms.M7
{
    /// <summary>
    /// Real in-match menu (ESC). Resume, Settings, Leave Match, Quit. It does NOT pause the match —
    /// a dedicated server keeps running — it only frees the cursor and stops gameplay input while
    /// open. Owned by <see cref="M7LocalPlayer"/>.
    /// </summary>
    public class M7PauseMenu : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        public event Action Opened;
        public event Action Closed;

        Canvas _canvas;
        GameObject _root;
        GameObject _settingsRoot;
        Text _sensitivity;
        Text _volume;

        void Awake()
        {
            Build();
            SetOpen(false);
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            if (_root != null) _root.SetActive(open);
            if (open) Opened?.Invoke();
            else Closed?.Invoke();
        }

        void Build()
        {
            M7Ui.EnsureEventSystem();
            _canvas = M7Ui.CreateCanvas("M7PauseMenu", 200);

            var root = M7Ui.Panel(_canvas.transform, "Root", new Color(0.02f, 0.03f, 0.05f, 0.88f));
            M7Ui.Fill(root.rectTransform, 0f, 0f, 0f, 0f);
            _root = root.gameObject;

            Text title = M7Ui.Label(root.transform, "Title", "PAUSED", 64, TextAnchor.MiddleCenter);
            M7Ui.Place(title.rectTransform, new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(900f, 100f));

            Button resume = M7Ui.Button(root.transform, "Resume", "RESUME", () => SetOpen(false), 30);
            M7Ui.Place(resume.image.rectTransform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(420f, 72f));

            Button settings = M7Ui.Button(root.transform, "Settings", "SETTINGS", ShowSettings, 26);
            M7Ui.Place(settings.image.rectTransform, new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(420f, 66f));

            Button leave = M7Ui.Button(root.transform, "LeaveMatch", "LEAVE MATCH", LeaveMatch, 26);
            M7Ui.Place(leave.image.rectTransform, new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(420f, 66f));

            Button quit = M7Ui.Button(root.transform, "Quit", "QUIT", Quit, 26);
            M7Ui.Place(quit.image.rectTransform, new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(420f, 66f));

            BuildSettings(root.transform);
            _root.SetActive(false);
        }

        void BuildSettings(Transform parent)
        {
            var panel = M7Ui.Panel(parent, "SettingsPanel", new Color(0.05f, 0.06f, 0.09f, 0.96f));
            M7Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 420f));
            _settingsRoot = panel.gameObject;

            Text title = M7Ui.Label(panel.transform, "Title", "SETTINGS", 40, TextAnchor.MiddleCenter);
            M7Ui.Place(title.rectTransform, new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(700f, 60f));

            _sensitivity = M7Ui.Label(panel.transform, "Sens", "", 26, TextAnchor.MiddleCenter);
            M7Ui.Place(_sensitivity.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(700f, 44f));
            Button sensDown = M7Ui.Button(panel.transform, "SensDown", "-", () => { M7Settings.MouseSensitivity = Mathf.Max(0.02f, M7Settings.MouseSensitivity - 0.02f); RefreshSettings(); }, 30);
            M7Ui.Place(sensDown.image.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(-110f, 0f), new Vector2(84f, 54f));
            Button sensUp = M7Ui.Button(panel.transform, "SensUp", "+", () => { M7Settings.MouseSensitivity = Mathf.Min(0.5f, M7Settings.MouseSensitivity + 0.02f); RefreshSettings(); }, 30);
            M7Ui.Place(sensUp.image.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(110f, 0f), new Vector2(84f, 54f));

            _volume = M7Ui.Label(panel.transform, "Vol", "", 26, TextAnchor.MiddleCenter);
            M7Ui.Place(_volume.rectTransform, new Vector2(0.5f, 0.34f), Vector2.zero, new Vector2(700f, 44f));
            Button volDown = M7Ui.Button(panel.transform, "VolDown", "-", () => { M7Settings.MasterVolume = Mathf.Max(0f, M7Settings.MasterVolume - 0.1f); RefreshSettings(); }, 30);
            M7Ui.Place(volDown.image.rectTransform, new Vector2(0.5f, 0.24f), new Vector2(-110f, 0f), new Vector2(84f, 54f));
            Button volUp = M7Ui.Button(panel.transform, "VolUp", "+", () => { M7Settings.MasterVolume = Mathf.Min(1f, M7Settings.MasterVolume + 0.1f); RefreshSettings(); }, 30);
            M7Ui.Place(volUp.image.rectTransform, new Vector2(0.5f, 0.24f), new Vector2(110f, 0f), new Vector2(84f, 54f));

            Button back = M7Ui.Button(panel.transform, "SettingsBack", "BACK", HideSettings, 24);
            M7Ui.Place(back.image.rectTransform, new Vector2(0.5f, 0.08f), Vector2.zero, new Vector2(240f, 54f));

            _settingsRoot.SetActive(false);
        }

        void ShowSettings()
        {
            RefreshSettings();
            _settingsRoot.SetActive(true);
        }

        void HideSettings() => _settingsRoot.SetActive(false);

        void RefreshSettings()
        {
            if (_sensitivity != null)
                _sensitivity.text = $"Mouse sensitivity: {M7Settings.MouseSensitivity * 100f:0}%";
            if (_volume != null)
                _volume.text = $"Master volume: {M7Settings.MasterVolume * 100f:0}%";
            M7Settings.Apply();
        }

        static void LeaveMatch()
        {
            M7PrivateMatch.ReturnToMenu();
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
