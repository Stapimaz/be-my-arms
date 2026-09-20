using BeMyArms.M3;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BeMyArms.M7
{
    /// <summary>
    /// Player-facing match HUD: P1/P2 essentials, buy panel, round/match states and the post-match
    /// return path. This is the real HUD (not the debug one) and the foundation for M8.
    /// </summary>
    public class M7MatchHudController : MonoBehaviour
    {
        static readonly (string label, int index)[] BuyCatalog =
        {
            ("Rifle 700", 0), ("SMG 500", 1), ("Shotgun 450", 2), ("Pistol 150", 3),
            ("Smoke 150", 4), ("Flash 150", 5), ("Grenade 200", 6)
        };

        Canvas _canvas;
        Text _top;
        Text _vitals;
        Text _weapon;
        RectTransform _crosshair;
        GameObject _buyPanel;
        GameObject _postPanel;
        Text _postTitle;

        M3DuelDirector _director;

        void Awake()
        {
            // The client connection is established later, in M3DuelBootstrap.Start(), so we cannot
            // decide client-ness here — a previous check against IsClient disabled the HUD on every
            // private-match client before it ever connected. Only a dedicated server is excluded up
            // front; a client builds its HUD lazily in Update() once the NetworkManager reports one.
            if (Application.isBatchMode)
            {
                enabled = false;
                return;
            }

            // Camera/viewmodel/cursor/ESC-menu presentation for the local player.
            if (GetComponent<M7LocalPlayer>() == null) gameObject.AddComponent<M7LocalPlayer>();
        }

        static bool NetworkManagerIsClient()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            return manager != null && manager.IsClient;
        }

        void Build()
        {
            M7Ui.EnsureEventSystem();
            _canvas = M7Ui.CreateCanvas("M7MatchHud", 50);

            var top = M7Ui.Panel(_canvas.transform, "Top", new Color(0.02f, 0.03f, 0.05f, 0.55f));
            top.rectTransform.anchorMin = new Vector2(0f, 1f);
            top.rectTransform.anchorMax = new Vector2(1f, 1f);
            top.rectTransform.pivot = new Vector2(0.5f, 1f);
            top.rectTransform.anchoredPosition = Vector2.zero;
            top.rectTransform.sizeDelta = new Vector2(0f, 64f);
            _top = M7Ui.Label(top.transform, "TopText", "", 28);
            M7Ui.Fill(_top.rectTransform, 20f, 0f, 20f, 0f);

            _vitals = M7Ui.Label(_canvas.transform, "Vitals", "", 26, TextAnchor.LowerLeft);
            M7Ui.Place(_vitals.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(700f, 120f));

            _weapon = M7Ui.Label(_canvas.transform, "Weapon", "", 26, TextAnchor.LowerRight);
            M7Ui.Place(_weapon.rectTransform, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(700f, 120f));

            // Crosshair (P2): four arms with a gap plus a centre dot, readable against the arena.
            _crosshair = M7Ui.Rect(_canvas.transform, "Crosshair");
            M7Ui.Place(_crosshair, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            var cross = new Color(0.86f, 1f, 0.92f, 0.92f);
            M7Ui.Place(M7Ui.Panel(_crosshair, "Top", cross).rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(2f, 10f));
            M7Ui.Place(M7Ui.Panel(_crosshair, "Bottom", cross).rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(2f, 10f));
            M7Ui.Place(M7Ui.Panel(_crosshair, "Left", cross).rectTransform, new Vector2(0f, 0.5f), new Vector2(7f, 0f), new Vector2(10f, 2f));
            M7Ui.Place(M7Ui.Panel(_crosshair, "Right", cross).rectTransform, new Vector2(1f, 0.5f), new Vector2(-7f, 0f), new Vector2(10f, 2f));
            M7Ui.Place(M7Ui.Panel(_crosshair, "Dot", cross).rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 2f));

            BuildBuyPanel();
            BuildPostPanel();
        }

        void BuildBuyPanel()
        {
            var panel = M7Ui.Panel(_canvas.transform, "Buy", new Color(0.04f, 0.05f, 0.07f, 0.85f));
            M7Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 220f));
            _buyPanel = panel.gameObject;

            Text title = M7Ui.Label(panel.transform, "Title", "BUY PHASE  (P2 loadout)", 26, TextAnchor.UpperCenter);
            M7Ui.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(720f, 40f));

            for (int i = 0; i < BuyCatalog.Length; i++)
            {
                (string label, int index) = BuyCatalog[i];
                int column = i % 4;
                int row = i / 4;
                Button button = M7Ui.Button(panel.transform, "Buy" + index, label, () => Buy(index), 20);
                M7Ui.Place(button.image.rectTransform, new Vector2(0f, 1f),
                    new Vector2(30f + column * 180f, -70f - row * 70f), new Vector2(170f, 60f));
            }
            _buyPanel.SetActive(false);
        }

        void BuildPostPanel()
        {
            var panel = M7Ui.Panel(_canvas.transform, "Post", new Color(0.03f, 0.04f, 0.06f, 0.92f));
            M7Ui.Fill(panel.rectTransform, 0f, 0f, 0f, 0f);
            _postPanel = panel.gameObject;

            _postTitle = M7Ui.Label(panel.transform, "Winner", "", 72, TextAnchor.MiddleCenter);
            M7Ui.Place(_postTitle.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(1400f, 120f));

            Button lobby = M7Ui.Button(panel.transform, "Lobby", "RETURN TO LOBBY", M7PrivateMatch.ReturnToMenu, 28);
            M7Ui.Place(lobby.image.rectTransform, new Vector2(0.5f, 0.40f), Vector2.zero, new Vector2(460f, 80f));
            Button quit = M7Ui.Button(panel.transform, "Quit", "QUIT", Quit, 24);
            M7Ui.Place(quit.image.rectTransform, new Vector2(0.5f, 0.28f), Vector2.zero, new Vector2(300f, 64f));

            _postPanel.SetActive(false);
        }

        void Update()
        {
            if (_canvas == null)
            {
                if (!NetworkManagerIsClient()) return;
                Build();
            }

            if (_director == null) _director = M3DuelDirector.Instance;
            if (_director == null) return;

            M3DuelBody own = FindOwnBody();
            UpdateTop(own);
            UpdateVitals(own);
            UpdateWeapon(own);
            UpdateBuy(own);
            UpdatePost();
            UpdateCursorAndEscape();
        }

        void UpdateTop(M3DuelBody own)
        {
            M3Phase phase = _director.CurrentPhase;
            string score = $"A {_director.TeamAWins.Value} - {_director.TeamBWins.Value} B";
            string time = phase == M3Phase.Buy || phase == M3Phase.Live ? $"  {_director.TimeRemaining.Value:0}s" : "";
            string zone = phase == M3Phase.Live ? $"   zone {_director.ZoneRadius.Value:0}m" : "";
            string role = own != null ? (M3DuelSlots.RoleOf(M3DuelClient.LocalSlotIndex) == 0 ? "P1" : "P2") : "";
            _top.text = $"{_director.CurrentPhase.ToString().ToUpperInvariant()}   round {_director.RoundIndex.Value}   {score}{time}{zone}    you: {role}";
        }

        void UpdateVitals(M3DuelBody own)
        {
            if (own == null) { _vitals.text = ""; return; }
            string zone = own.OutsideZone.Value ? "   OUTSIDE ZONE" : "";
            string blind = own.BlindRemaining.Value > 0f ? "   FLASHED" : "";
            _vitals.text = $"HP {own.State.Value.Health}   {(own.Alive.Value ? "alive" : "DOWN")}   kills {own.Kills.Value}{zone}{blind}";
        }

        void UpdateWeapon(M3DuelBody own)
        {
            if (own == null) { _weapon.text = ""; return; }
            _weapon.text = $"{(M3WeaponId)own.WeaponId.Value}   {own.Ammo.Value}/{own.Magazine.Value}{(own.Reloading.Value ? "  RELOADING" : "")}";
        }

        void UpdateBuy(M3DuelBody own)
        {
            bool p2Own = own != null && M3DuelClient.LocalSlotIndex >= 0 && M3DuelSlots.RoleOf(M3DuelClient.LocalSlotIndex) == 1;
            _buyPanel.SetActive(_director.IsBuy && p2Own);
        }

        void UpdatePost()
        {
            bool ended = _director.CurrentPhase == M3Phase.MatchEnd || _director.MatchWinner.Value >= 0;
            if (!ended) { _postPanel.SetActive(false); return; }
            _postPanel.SetActive(true);
            int winner = _director.MatchWinner.Value;
            int myTeam = M3DuelClient.LocalSlotIndex >= 0 ? M3DuelSlots.TeamOf(M3DuelClient.LocalSlotIndex, M3Config.BodiesPerTeam) : M3Config.ClientTeam;
            _postTitle.text = winner < 0 ? "MATCH DRAW" : (winner == myTeam ? "VICTORY" : "DEFEAT");
        }

        void UpdateCursorAndEscape()
        {
            // Cursor capture and the ESC menu are owned by M7LocalPlayer; the HUD only shows the
            // crosshair when this body is the P2 role and the round is live.
            bool roleP2 = M3DuelClient.LocalSlotIndex >= 0 && M3DuelSlots.RoleOf(M3DuelClient.LocalSlotIndex) == 1;
            bool crosshair = roleP2 && _director.IsLive;
            if (_crosshair != null) _crosshair.gameObject.SetActive(crosshair);
        }

        void Buy(int index)
        {
            M3DuelBody own = FindOwnBody();
            if (own != null && _director.IsBuy) own.SubmitBuyServerRpc(index);
        }

        M3DuelBody FindOwnBody()
        {
            if (M3DuelClient.LocalSlotIndex < 0) return null;
            int team = M3DuelSlots.TeamOf(M3DuelClient.LocalSlotIndex, M3Config.BodiesPerTeam);
            int body = M3DuelSlots.BodyOf(M3DuelClient.LocalSlotIndex, M3Config.BodiesPerTeam);
            M3DuelBody[] bodies = FindObjectsByType<M3DuelBody>();
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i].IsSpawned && bodies[i].TeamIndex == team && bodies[i].BodyId == body) return bodies[i];
            return null;
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
