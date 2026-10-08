using System;
using BeMyArms.Match;
using UnityEngine;
using UnityEngine.UI;

namespace BeMyArms.Client
{
    /// <summary>
    /// Real in-match menu (ESC). Resume, Settings, Leave Match, Quit. It does NOT pause the match —
    /// a dedicated server keeps running — it only frees the cursor and stops gameplay input while
    /// open. Owned by <see cref="LocalPlayer"/>.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7PauseMenu")]
    public class PauseMenu : MonoBehaviour
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
            Ui.EnsureEventSystem();
            _canvas = Ui.CreateCanvas("PauseMenu", 200);

            var root = Ui.Panel(_canvas.transform, "Root", new Color(0.02f, 0.03f, 0.05f, 0.88f));
            Ui.Fill(root.rectTransform, 0f, 0f, 0f, 0f);
            _root = root.gameObject;

            Text title = Ui.Label(root.transform, "Title", "PAUSED", 64, TextAnchor.MiddleCenter);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(900f, 100f));

            Button resume = Ui.Button(root.transform, "Resume", "RESUME", () => SetOpen(false), 30);
            Ui.Place(resume.image.rectTransform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(420f, 72f));

            Button settings = Ui.Button(root.transform, "Settings", "SETTINGS", ShowSettings, 26);
            Ui.Place(settings.image.rectTransform, new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(420f, 66f));

            Button leave = Ui.Button(root.transform, "LeaveMatch", "LEAVE MATCH", LeaveMatch, 26);
            Ui.Place(leave.image.rectTransform, new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(420f, 66f));

            Button quit = Ui.Button(root.transform, "Quit", "QUIT", Quit, 26);
            Ui.Place(quit.image.rectTransform, new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(420f, 66f));

            if (MatchConfig.PrivatePractice)
            {
                var heading = Ui.Label(root.transform, "PracticeTitle", "PRACTICE TOOLS", 28, TextAnchor.MiddleCenter);
                Ui.Place(heading.rectTransform, new Vector2(0.80f, 0.68f), Vector2.zero, new Vector2(500f, 50f));
                PracticeButton(root.transform, "Restart encounter", 0.58f, () => PracticeAction(0));
                PracticeButton(root.transform, "Fresh match", 0.48f, () => PracticeAction(1));
                PracticeButton(root.transform, "Swap roles + reset", 0.38f, () => PracticeAction(2));
                if (!PrivateMatch.Current.JoinExisting)
                    PracticeButton(root.transform, "Open local partner", 0.18f, PrivateMatch.LaunchLocalPartner);
                var info = Ui.Label(root.transform, "Connection", $"Server {PrivateMatch.ConnectionLabel}\nLAN: use this PC's LAN IP and the same port.\nF6 restart encounter · F7 swap roles + reset · F8 fresh match", 22, TextAnchor.MiddleCenter);
                Ui.Place(info.rectTransform, new Vector2(0.5f, 0.08f), Vector2.zero, new Vector2(1500f, 110f));
            }

            BuildSettings(root.transform);
            _root.SetActive(false);
        }

        static void PracticeButton(Transform parent, string name, float y, Action action)
        {
            var button = Ui.Button(parent, name, name.ToUpperInvariant(), action, 24);
            Ui.Place(button.image.rectTransform, new Vector2(0.80f, y), Vector2.zero, new Vector2(440f, 70f));
        }

        void PracticeAction(byte action)
        {
            if (MatchDirector.Instance == null) return;
            MatchDirector.Instance.PracticeActionServerRpc(action);
            SetOpen(false);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (!Application.isFocused || !MatchConfig.PrivatePractice || kb == null) return;
            if (kb.f6Key.wasPressedThisFrame) PracticeAction(0);
            if (kb.f7Key.wasPressedThisFrame) PracticeAction(2);
            if (kb.f8Key.wasPressedThisFrame) PracticeAction(1);
#endif
        }

        void BuildSettings(Transform parent)
        {
            var panel = Ui.Panel(parent, "SettingsPanel", new Color(0.05f, 0.06f, 0.09f, 0.96f));
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 420f));
            _settingsRoot = panel.gameObject;

            Text title = Ui.Label(panel.transform, "Title", "SETTINGS", 40, TextAnchor.MiddleCenter);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(700f, 60f));

            _sensitivity = Ui.Label(panel.transform, "Sens", "", 26, TextAnchor.MiddleCenter);
            Ui.Place(_sensitivity.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(700f, 44f));
            Button sensDown = Ui.Button(panel.transform, "SensDown", "-", () => { Settings.MouseSensitivity = Mathf.Max(0.02f, Settings.MouseSensitivity - 0.02f); RefreshSettings(); }, 30);
            Ui.Place(sensDown.image.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(-110f, 0f), new Vector2(84f, 54f));
            Button sensUp = Ui.Button(panel.transform, "SensUp", "+", () => { Settings.MouseSensitivity = Mathf.Min(0.5f, Settings.MouseSensitivity + 0.02f); RefreshSettings(); }, 30);
            Ui.Place(sensUp.image.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(110f, 0f), new Vector2(84f, 54f));

            _volume = Ui.Label(panel.transform, "Vol", "", 26, TextAnchor.MiddleCenter);
            Ui.Place(_volume.rectTransform, new Vector2(0.5f, 0.34f), Vector2.zero, new Vector2(700f, 44f));
            Button volDown = Ui.Button(panel.transform, "VolDown", "-", () => { Settings.MasterVolume = Mathf.Max(0f, Settings.MasterVolume - 0.1f); RefreshSettings(); }, 30);
            Ui.Place(volDown.image.rectTransform, new Vector2(0.5f, 0.24f), new Vector2(-110f, 0f), new Vector2(84f, 54f));
            Button volUp = Ui.Button(panel.transform, "VolUp", "+", () => { Settings.MasterVolume = Mathf.Min(1f, Settings.MasterVolume + 0.1f); RefreshSettings(); }, 30);
            Ui.Place(volUp.image.rectTransform, new Vector2(0.5f, 0.24f), new Vector2(110f, 0f), new Vector2(84f, 54f));

            Button back = Ui.Button(panel.transform, "SettingsBack", "BACK", HideSettings, 24);
            Ui.Place(back.image.rectTransform, new Vector2(0.5f, 0.08f), Vector2.zero, new Vector2(240f, 54f));

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
                _sensitivity.text = $"Mouse sensitivity: {Settings.MouseSensitivity * 100f:0}%";
            if (_volume != null)
                _volume.text = $"Master volume: {Settings.MasterVolume * 100f:0}%";
            Settings.Apply();
        }

        static void LeaveMatch()
        {
            PrivateMatch.ReturnToMenu();
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
