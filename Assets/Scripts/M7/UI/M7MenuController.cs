using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BeMyArms.M7
{
    /// <summary>
    /// Player-facing main menu, private lobby (mode/role/slot selection + bot fill) and settings.
    /// This is the real entry flow, not debug UI; it is the foundation for the M8 UI.
    /// </summary>
    public class M7MenuController : MonoBehaviour
    {
        enum Screen { Main, Lobby, Settings }

        Canvas _canvas;
        Screen _screen = Screen.Main;
        M7MapFamily _mode = M7MapFamily.Duel;
        int _role; // 0 = P1, 1 = P2
        Text _status;

        void Start()
        {
            M7Ui.EnsureEventSystem();
            M7Settings.Apply();
            _canvas = M7Ui.CreateCanvas("M7Menu", 100);
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

            var root = M7Ui.Panel(_canvas.transform, "Root", new Color(0.05f, 0.06f, 0.08f, 0.98f));
            M7Ui.Fill(root.rectTransform, 0f, 0f, 0f, 0f);

            Text title = M7Ui.Label(root.transform, "Title", "BE MY ARMS", 88, TextAnchor.MiddleCenter);
            M7Ui.Place(title.rectTransform, new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(1200f, 140f));

            Text subtitle = M7Ui.Label(root.transform, "Sub", "two players, one body", 30, TextAnchor.MiddleCenter);
            M7Ui.Place(subtitle.rectTransform, new Vector2(0.5f, 0.70f), Vector2.zero, new Vector2(1000f, 60f));

            Stack(root.transform, new[]
            {
                ("PLAY", (System.Action)ShowLobby),
                ("SETTINGS", (System.Action)ShowSettings),
                ("QUIT", (System.Action)Quit),
            }, 0.5f, 0.42f, 78f, 18f);

            _status = M7Ui.Label(root.transform, "Status", EditorHint(), 22, TextAnchor.MiddleCenter);
            M7Ui.Place(_status.rectTransform, new Vector2(0.5f, 0.10f), Vector2.zero, new Vector2(1400f, 60f));
        }

        // ---- Private lobby ----

        void ShowLobby()
        {
            _screen = Screen.Lobby;
            Clear();

            var root = M7Ui.Panel(_canvas.transform, "Root", new Color(0.05f, 0.06f, 0.08f, 0.98f));
            M7Ui.Fill(root.rectTransform, 0f, 0f, 0f, 0f);

            Text title = M7Ui.Label(root.transform, "Title", "PRIVATE LOBBY", 56, TextAnchor.MiddleCenter);
            M7Ui.Place(title.rectTransform, new Vector2(0.5f, 0.90f), Vector2.zero, new Vector2(1200f, 80f));

            // Mode.
            Text modeLabel = M7Ui.Label(root.transform, "ModeLabel", "MODE", 24, TextAnchor.MiddleLeft);
            M7Ui.Place(modeLabel.rectTransform, new Vector2(0.16f, 0.78f), Vector2.zero, new Vector2(220f, 50f));
            Button duel = M7Ui.Button(root.transform, "Duel", "DUEL (1v1)", () => { _mode = M7MapFamily.Duel; ShowLobby(); });
            M7Ui.Place(duel.image.rectTransform, new Vector2(0.16f, 0.72f), Vector2.zero, new Vector2(300f, 70f));
            Button two = M7Ui.Button(root.transform, "TwoVsTwo", "2v2", () => { _mode = M7MapFamily.TwoVsTwo; ShowLobby(); });
            M7Ui.Place(two.image.rectTransform, new Vector2(0.16f, 0.72f), new Vector2(320f, 0f), new Vector2(300f, 70f));
            Highlight(duel, _mode == M7MapFamily.Duel);
            Highlight(two, _mode == M7MapFamily.TwoVsTwo);

            // Role.
            Text roleLabel = M7Ui.Label(root.transform, "RoleLabel", "YOUR ROLE", 24, TextAnchor.MiddleLeft);
            M7Ui.Place(roleLabel.rectTransform, new Vector2(0.16f, 0.64f), Vector2.zero, new Vector2(260f, 50f));
            Button p1 = M7Ui.Button(root.transform, "P1", "P1 (body)", () => { _role = 0; ShowLobby(); });
            M7Ui.Place(p1.image.rectTransform, new Vector2(0.16f, 0.58f), Vector2.zero, new Vector2(300f, 70f));
            Button p2 = M7Ui.Button(root.transform, "P2", "P2 (arms)", () => { _role = 1; ShowLobby(); });
            M7Ui.Place(p2.image.rectTransform, new Vector2(0.16f, 0.58f), new Vector2(320f, 0f), new Vector2(300f, 70f));
            Highlight(p1, _role == 0);
            Highlight(p2, _role == 1);

            // Slot list.
            Text slotsTitle = M7Ui.Label(root.transform, "SlotsTitle", "SLOTS  (empty slots are bots)", 24, TextAnchor.MiddleLeft);
            M7Ui.Place(slotsTitle.rectTransform, new Vector2(0.72f, 0.80f), Vector2.zero, new Vector2(620f, 40f));
            int bodies = _mode == M7MapFamily.Duel ? 1 : 2;
            int line = 0;
            for (int team = 0; team < 2; team++)
            {
                for (int body = 0; body < bodies; body++)
                {
                    for (int role = 0; role < 2; role++)
                    {
                        bool you = _role == role && body == 0 && team == 0; // human is team A, body 0
                        string label = $"Team {(team == 0 ? "A" : "B")}  Body {body}  P{role + 1}   {(you ? "YOU" : "BOT")}";
                        Text row = M7Ui.Label(root.transform, "Slot" + line, label, 26, TextAnchor.MiddleLeft);
                        M7Ui.Place(row.rectTransform, new Vector2(0.72f, 0.76f), new Vector2(0f, -line * 44f), new Vector2(620f, 40f));
                        row.color = you ? new Color(0.4f, 0.9f, 1f) : new Color(0.85f, 0.85f, 0.85f);
                        line++;
                    }
                }
            }

            // Start / back.
            Button start = M7Ui.Button(root.transform, "Start", "START MATCH", StartMatch, 30);
            M7Ui.Place(start.image.rectTransform, new Vector2(0.5f, 0.14f), Vector2.zero, new Vector2(420f, 84f));
            Button back = M7Ui.Button(root.transform, "Back", "BACK", ShowMain, 24);
            M7Ui.Place(back.image.rectTransform, new Vector2(0.5f, 0.05f), Vector2.zero, new Vector2(260f, 60f));

            _status = M7Ui.Label(root.transform, "Status", EditorHint(), 20, TextAnchor.MiddleCenter);
            M7Ui.Place(_status.rectTransform, new Vector2(0.5f, 0.005f), Vector2.zero, new Vector2(1400f, 40f));
        }

        void StartMatch()
        {
            if (Application.isEditor)
            {
                if (_status != null) _status.text = "Run a build to start a private match (local dedicated server).";
                return;
            }

            var request = new M7MatchRequest
            {
                Mode = _mode,
                ArenaScene = _mode == M7MapFamily.Duel ? "M7DuelArena" : "M7TwoVsTwoArena",
                Team = 0,
                Body = 0,
                Role = _role,
                Port = M7PrivateMatch.DefaultPort
            };
            M7PrivateMatch.Begin(request);
        }

        // ---- Settings ----

        void ShowSettings()
        {
            _screen = Screen.Settings;
            Clear();

            var root = M7Ui.Panel(_canvas.transform, "Root", new Color(0.05f, 0.06f, 0.08f, 0.98f));
            M7Ui.Fill(root.rectTransform, 0f, 0f, 0f, 0f);

            Text title = M7Ui.Label(root.transform, "Title", "SETTINGS", 56, TextAnchor.MiddleCenter);
            M7Ui.Place(title.rectTransform, new Vector2(0.5f, 0.82f), Vector2.zero, new Vector2(900f, 80f));

            Text sens = M7Ui.Label(root.transform, "Sens", $"Mouse sensitivity: {M7Settings.MouseSensitivity:0.00}", 26, TextAnchor.MiddleCenter);
            M7Ui.Place(sens.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(800f, 50f));
            Button sensDown = M7Ui.Button(root.transform, "SensDown", "-", () => { M7Settings.MouseSensitivity = Mathf.Max(0.02f, M7Settings.MouseSensitivity - 0.02f); ShowSettings(); }, 30);
            M7Ui.Place(sensDown.image.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(-120f, 0f), new Vector2(90f, 60f));
            Button sensUp = M7Ui.Button(root.transform, "SensUp", "+", () => { M7Settings.MouseSensitivity = Mathf.Min(0.5f, M7Settings.MouseSensitivity + 0.02f); ShowSettings(); }, 30);
            M7Ui.Place(sensUp.image.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(120f, 0f), new Vector2(90f, 60f));

            Text vol = M7Ui.Label(root.transform, "Vol", $"Master volume: {M7Settings.MasterVolume * 100f:0}%", 26, TextAnchor.MiddleCenter);
            M7Ui.Place(vol.rectTransform, new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(800f, 50f));
            Button volDown = M7Ui.Button(root.transform, "VolDown", "-", () => { M7Settings.MasterVolume = Mathf.Max(0f, M7Settings.MasterVolume - 0.1f); ShowSettings(); }, 30);
            M7Ui.Place(volDown.image.rectTransform, new Vector2(0.5f, 0.35f), new Vector2(-120f, 0f), new Vector2(90f, 60f));
            Button volUp = M7Ui.Button(root.transform, "VolUp", "+", () => { M7Settings.MasterVolume = Mathf.Min(1f, M7Settings.MasterVolume + 0.1f); ShowSettings(); }, 30);
            M7Ui.Place(volUp.image.rectTransform, new Vector2(0.5f, 0.35f), new Vector2(120f, 0f), new Vector2(90f, 60f));

            Button back = M7Ui.Button(root.transform, "Back", "BACK", ShowMain, 24);
            M7Ui.Place(back.image.rectTransform, new Vector2(0.5f, 0.15f), Vector2.zero, new Vector2(260f, 64f));
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
            => M7Ui.SetColor(button, selected ? new Color(0.20f, 0.52f, 0.66f, 1f) : new Color(0.16f, 0.18f, 0.22f, 0.96f));

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
                Button button = M7Ui.Button(parent, label, label, action, 30);
                M7Ui.Place(button.image.rectTransform, new Vector2(anchorX, anchorY), new Vector2(0f, y), new Vector2(420f, height));
                y -= height + gap;
            }
        }
    }
}
