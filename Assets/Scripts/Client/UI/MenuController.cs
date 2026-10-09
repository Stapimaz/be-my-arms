using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BeMyArms.Client
{
    /// <summary>
    /// Player-facing main menu, private lobby (mode/role/slot selection + bot fill) and settings.
    /// This is the real entry flow, not debug UI; it is the foundation for the M8 UI.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7MenuController")]
    public class MenuController : MonoBehaviour
    {
        enum Screen { Main, Lobby, Settings }

        Canvas _canvas;
        Screen _screen = Screen.Main;
        MapFamily _mode = MapFamily.Duel;
        int _role; // 0 = P1, 1 = P2
        string _joinAddress = "127.0.0.1";
        string _joinPort = "7780";
        Text _status;

        void Start()
        {
            Ui.EnsureEventSystem();
            Settings.Apply();
            _canvas = Ui.CreateCanvas("Menu", 100);
            ShowMain();
        }

        void Clear()
        {
            for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
                Destroy(_canvas.transform.GetChild(i).gameObject);
        }

        // ---- Main menu ----

        void ShowMain()
        {
            _screen = Screen.Main;
            Clear();

            var root = Ui.Panel(_canvas.transform, "Root", new Color(0.05f, 0.06f, 0.08f, 0.98f));
            Ui.Fill(root.rectTransform, 0f, 0f, 0f, 0f);

            Text title = Ui.Label(root.transform, "Title", "BE MY ARMS", 88, TextAnchor.MiddleCenter);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(1200f, 140f));

            Text subtitle = Ui.Label(root.transform, "Sub", "two players, one body", 30, TextAnchor.MiddleCenter);
            Ui.Place(subtitle.rectTransform, new Vector2(0.5f, 0.70f), Vector2.zero, new Vector2(1000f, 60f));

            Stack(root.transform, new[]
            {
                ("PLAY", (System.Action)ShowLobby),
                ("DUO PRACTICE", (System.Action)ShowDuo),
                ("SETTINGS", (System.Action)ShowSettings),
                ("QUIT", (System.Action)Quit),
            }, 0.5f, 0.42f, 78f, 18f);

            _status = Ui.Label(root.transform, "Status", EditorHint(), 22, TextAnchor.MiddleCenter);
            Ui.Place(_status.rectTransform, new Vector2(0.5f, 0.10f), Vector2.zero, new Vector2(1400f, 60f));
        }

        // ---- Private lobby ----

        void ShowLobby()
        {
            _screen = Screen.Lobby;
            Clear();

            var root = Ui.Panel(_canvas.transform, "Root", new Color(0.05f, 0.06f, 0.08f, 0.98f));
            Ui.Fill(root.rectTransform, 0f, 0f, 0f, 0f);

            Text title = Ui.Label(root.transform, "Title", "PRIVATE LOBBY", 56, TextAnchor.MiddleCenter);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 0.90f), Vector2.zero, new Vector2(1200f, 80f));

            // Mode.
            Text modeLabel = Ui.Label(root.transform, "ModeLabel", "MODE", 24, TextAnchor.MiddleLeft);
            Ui.Place(modeLabel.rectTransform, new Vector2(0.16f, 0.78f), Vector2.zero, new Vector2(220f, 50f));
            Button duel = Ui.Button(root.transform, "Duel", "DUEL (1v1)", () => { _mode = MapFamily.Duel; ShowLobby(); });
            Ui.Place(duel.image.rectTransform, new Vector2(0.16f, 0.72f), Vector2.zero, new Vector2(300f, 70f));
            Button two = Ui.Button(root.transform, "TwoVsTwo", "2v2", () => { _mode = MapFamily.TwoVsTwo; ShowLobby(); });
            Ui.Place(two.image.rectTransform, new Vector2(0.16f, 0.72f), new Vector2(320f, 0f), new Vector2(300f, 70f));
            Highlight(duel, _mode == MapFamily.Duel);
            Highlight(two, _mode == MapFamily.TwoVsTwo);

            // Role.
            Text roleLabel = Ui.Label(root.transform, "RoleLabel", "YOUR ROLE", 24, TextAnchor.MiddleLeft);
            Ui.Place(roleLabel.rectTransform, new Vector2(0.16f, 0.64f), Vector2.zero, new Vector2(260f, 50f));
            Button p1 = Ui.Button(root.transform, "P1", "P1 (body)", () => { _role = 0; ShowLobby(); });
            Ui.Place(p1.image.rectTransform, new Vector2(0.16f, 0.58f), Vector2.zero, new Vector2(300f, 70f));
            Button p2 = Ui.Button(root.transform, "P2", "P2 (arms)", () => { _role = 1; ShowLobby(); });
            Ui.Place(p2.image.rectTransform, new Vector2(0.16f, 0.58f), new Vector2(320f, 0f), new Vector2(300f, 70f));
            Highlight(p1, _role == 0);
            Highlight(p2, _role == 1);

            // Slot list.
            Text slotsTitle = Ui.Label(root.transform, "SlotsTitle", "SLOTS  (empty slots are bots)", 24, TextAnchor.MiddleLeft);
            Ui.Place(slotsTitle.rectTransform, new Vector2(0.72f, 0.80f), Vector2.zero, new Vector2(620f, 40f));
            int bodies = _mode == MapFamily.Duel ? 1 : 2;
            int line = 0;
            for (int team = 0; team < 2; team++)
            {
                for (int body = 0; body < bodies; body++)
                {
                    for (int role = 0; role < 2; role++)
                    {
                        bool you = _role == role && body == 0 && team == 0; // human is team A, body 0
                        string label = $"Team {(team == 0 ? "A" : "B")}  Body {body}  P{role + 1}   {(you ? "YOU" : "BOT")}";
                        Text row = Ui.Label(root.transform, "Slot" + line, label, 26, TextAnchor.MiddleLeft);
                        Ui.Place(row.rectTransform, new Vector2(0.72f, 0.76f), new Vector2(0f, -line * 44f), new Vector2(620f, 40f));
                        row.color = you ? new Color(0.4f, 0.9f, 1f) : new Color(0.85f, 0.85f, 0.85f);
                        line++;
                    }
                }
            }

            // Start / back.
            Button start = Ui.Button(root.transform, "Start", "START MATCH", StartMatch, 30);
            Ui.Place(start.image.rectTransform, new Vector2(0.5f, 0.14f), Vector2.zero, new Vector2(420f, 84f));
            Button back = Ui.Button(root.transform, "Back", "BACK", ShowMain, 24);
            Ui.Place(back.image.rectTransform, new Vector2(0.5f, 0.05f), Vector2.zero, new Vector2(260f, 60f));

            _status = Ui.Label(root.transform, "Status", EditorHint(), 20, TextAnchor.MiddleCenter);
            Ui.Place(_status.rectTransform, new Vector2(0.5f, 0.005f), Vector2.zero, new Vector2(1400f, 40f));
        }

        void StartMatch()
        {
            if (Application.isEditor)
            {
                if (_status != null) _status.text = "Run a build to start a private match (local dedicated server).";
                return;
            }

            var request = new MatchRequest
            {
                Mode = _mode,
                ArenaScene = _mode == MapFamily.Duel ? PrivateMatch.DefaultDuelScene : "TwoVsTwoArena",
                Team = 0,
                Body = 0,
                Role = _role,
                Port = PrivateMatch.DefaultPort
            };
            PrivateMatch.Begin(request);
        }

        // ---- Settings ----

        void ShowDuo()
        {
            Clear();
            var root = Ui.Panel(_canvas.transform, "Duo", new Color(0.05f, 0.06f, 0.08f, 0.98f));
            Ui.Fill(root.rectTransform, 0f, 0f, 0f, 0f);
            var title = Ui.Label(root.transform, "Title", "DUO PRACTICE", 56, TextAnchor.MiddleCenter);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 0.87f), Vector2.zero, new Vector2(1200f, 80f));
            var hint = Ui.Label(root.transform, "Hint", "Two humans share Team A's body against Easy bots.\nHost waits for both roles. The host can open a second client from ESC.", 26, TextAnchor.MiddleCenter);
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0.76f), Vector2.zero, new Vector2(1400f, 90f));
            Button p1 = Ui.Button(root.transform, "P1", "P1 — BODY", () => { _role = 0; ShowDuo(); });
            Ui.Place(p1.image.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(-180f, 0f), new Vector2(340f, 70f));
            Button p2 = Ui.Button(root.transform, "P2", "P2 — ARMS", () => { _role = 1; ShowDuo(); });
            Ui.Place(p2.image.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(180f, 0f), new Vector2(340f, 70f));
            Highlight(p1, _role == 0); Highlight(p2, _role == 1);
            var host = Ui.Button(root.transform, "Host", "HOST DUO", () => StartDuo(false), 30);
            Ui.Place(host.image.rectTransform, new Vector2(0.5f, 0.50f), Vector2.zero, new Vector2(500f, 76f));
            var label = Ui.Label(root.transform, "JoinLabel", "JOIN: host IP address and port (shown on host's HUD)", 24, TextAnchor.MiddleCenter);
            Ui.Place(label.rectTransform, new Vector2(0.5f, 0.39f), Vector2.zero, new Vector2(1100f, 48f));
            var address = Ui.TextInput(root.transform, "Address", _joinAddress);
            Ui.Place(address.GetComponent<RectTransform>(), new Vector2(0.5f, 0.32f), new Vector2(-100f, 0f), new Vector2(470f, 60f));
            address.onValueChanged.AddListener(value => _joinAddress = value.Trim());
            var port = Ui.TextInput(root.transform, "Port", _joinPort);
            port.contentType = InputField.ContentType.IntegerNumber;
            Ui.Place(port.GetComponent<RectTransform>(), new Vector2(0.5f, 0.32f), new Vector2(240f, 0f), new Vector2(180f, 60f));
            port.onValueChanged.AddListener(value => _joinPort = value);
            var join = Ui.Button(root.transform, "Join", "JOIN DUO", () => StartDuo(true), 30);
            Ui.Place(join.image.rectTransform, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(500f, 76f));
            var back = Ui.Button(root.transform, "Back", "BACK", ShowMain, 24);
            Ui.Place(back.image.rectTransform, new Vector2(0.5f, 0.10f), Vector2.zero, new Vector2(260f, 60f));
            _status = Ui.Label(root.transform, "Status", EditorHint(), 22, TextAnchor.MiddleCenter);
            Ui.Place(_status.rectTransform, new Vector2(0.5f, 0.03f), Vector2.zero, new Vector2(1500f, 44f));
        }

        void StartDuo(bool join)
        {
            if (Application.isEditor) { _status.text = EditorHint(); return; }
            ushort port = PrivateMatch.DefaultPort;
            if (join && (string.IsNullOrWhiteSpace(_joinAddress) || !ushort.TryParse(_joinPort, out port) || port == 0))
            { _status.text = "Enter a host address and a port between 1 and 65535."; return; }
            PrivateMatch.Begin(new MatchRequest
            {
                Mode = MapFamily.Duel, ArenaScene = PrivateMatch.DefaultDuelScene, Duo = true,
                Role = _role, JoinExisting = join, Address = _joinAddress, Port = port
            });
        }

        void ShowSettings()
        {
            _screen = Screen.Settings;
            Clear();

            var root = Ui.Panel(_canvas.transform, "Root", new Color(0.05f, 0.06f, 0.08f, 0.98f));
            Ui.Fill(root.rectTransform, 0f, 0f, 0f, 0f);

            Text title = Ui.Label(root.transform, "Title", "SETTINGS", 56, TextAnchor.MiddleCenter);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 0.82f), Vector2.zero, new Vector2(900f, 80f));

            Text sens = Ui.Label(root.transform, "Sens", $"Mouse sensitivity: {Settings.MouseSensitivity:0.00}", 26, TextAnchor.MiddleCenter);
            Ui.Place(sens.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(800f, 50f));
            Button sensDown = Ui.Button(root.transform, "SensDown", "-", () => { Settings.MouseSensitivity = Mathf.Max(0.02f, Settings.MouseSensitivity - 0.02f); ShowSettings(); }, 30);
            Ui.Place(sensDown.image.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(-120f, 0f), new Vector2(90f, 60f));
            Button sensUp = Ui.Button(root.transform, "SensUp", "+", () => { Settings.MouseSensitivity = Mathf.Min(0.5f, Settings.MouseSensitivity + 0.02f); ShowSettings(); }, 30);
            Ui.Place(sensUp.image.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(120f, 0f), new Vector2(90f, 60f));

            Text vol = Ui.Label(root.transform, "Vol", $"Master volume: {Settings.MasterVolume * 100f:0}%", 26, TextAnchor.MiddleCenter);
            Ui.Place(vol.rectTransform, new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(800f, 50f));
            Button volDown = Ui.Button(root.transform, "VolDown", "-", () => { Settings.MasterVolume = Mathf.Max(0f, Settings.MasterVolume - 0.1f); ShowSettings(); }, 30);
            Ui.Place(volDown.image.rectTransform, new Vector2(0.5f, 0.35f), new Vector2(-120f, 0f), new Vector2(90f, 60f));
            Button volUp = Ui.Button(root.transform, "VolUp", "+", () => { Settings.MasterVolume = Mathf.Min(1f, Settings.MasterVolume + 0.1f); ShowSettings(); }, 30);
            Ui.Place(volUp.image.rectTransform, new Vector2(0.5f, 0.35f), new Vector2(120f, 0f), new Vector2(90f, 60f));

            Button back = Ui.Button(root.transform, "Back", "BACK", ShowMain, 24);
            Ui.Place(back.image.rectTransform, new Vector2(0.5f, 0.15f), Vector2.zero, new Vector2(260f, 64f));
        }

        // ---- Helpers ----

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static string EditorHint()
            => Application.isEditor ? "Editor: run a build to use the private-match flow." : "";

        static void Highlight(Button button, bool selected)
            => Ui.SetColor(button, selected ? new Color(0.20f, 0.52f, 0.66f, 1f) : new Color(0.16f, 0.18f, 0.22f, 0.96f));

        /// <summary>
        /// Lay out a vertical stack of buttons centered on the given normalized anchor. The anchor is
        /// in canvas space (0..1); the first item is the TOP button and later items go downward.
        /// </summary>
        static void Stack(Transform parent, IEnumerable<(string label, System.Action action)> items, float anchorX, float anchorY, float height, float gap)
        {
            var list = new List<(string label, System.Action action)>(items);
            float total = list.Count * height + Mathf.Max(0, list.Count - 1) * gap;
            float y = total * 0.5f - height * 0.5f; // first (top) button's center, relative to the stack center
            foreach ((string label, System.Action action) in list)
            {
                Button button = Ui.Button(parent, label, label, action, 30);
                Ui.Place(button.image.rectTransform, new Vector2(anchorX, anchorY), new Vector2(0f, y), new Vector2(420f, height));
                y -= height + gap;
            }
        }
    }
}
