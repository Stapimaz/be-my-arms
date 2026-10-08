using BeMyArms.Match;
using BeMyArms.Networking;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BeMyArms.Client
{
    /// <summary>
    /// Player-facing match HUD: P1/P2 essentials, buy panel, round/match states and the post-match
    /// return path. Both roles see shared combat/partner state; neither gains the other's authority.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7MatchHudController")]
    public class MatchHudController : MonoBehaviour
    {
        Canvas _canvas;
        Text _top;
        Text _vitals;
        Text _weapon;
        RectTransform _crosshair;
        DynamicCrosshair _dynamicCrosshair;
        NetworkBodyClient _crosshairClient;
        uint _crosshairEpoch = uint.MaxValue;
        GameObject _postPanel;
        Text _postTitle;
        Text _buyHint;
        Text _partner;
        Text _controls;
        Text _connection;

        MatchDirector _director;
        LocalPlayer _localPlayer;

        void Awake()
        {
            // The client connection is established later, in MatchBootstrap.Start(), so we cannot
            // decide client-ness here — a previous check against IsClient disabled the HUD on every
            // private-match client before it ever connected. Only a dedicated server is excluded up
            // front; a client builds its HUD lazily in Update() once the NetworkManager reports one.
            if (Application.isBatchMode)
            {
                enabled = false;
                return;
            }

            // Camera/viewmodel/cursor/ESC-menu presentation for the local player.
            _localPlayer = GetComponent<LocalPlayer>();
            if (_localPlayer == null) _localPlayer = gameObject.AddComponent<LocalPlayer>();
        }

        static bool NetworkManagerIsClient()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            return manager != null && manager.IsClient;
        }

        void Build()
        {
            Ui.EnsureEventSystem();
            _canvas = Ui.CreateCanvas("MatchHud", 50);

            var top = Ui.Panel(_canvas.transform, "Top", new Color(0.02f, 0.03f, 0.05f, 0.55f));
            top.rectTransform.anchorMin = new Vector2(0f, 1f);
            top.rectTransform.anchorMax = new Vector2(1f, 1f);
            top.rectTransform.pivot = new Vector2(0.5f, 1f);
            top.rectTransform.anchoredPosition = Vector2.zero;
            top.rectTransform.sizeDelta = new Vector2(0f, 64f);
            _top = Ui.Label(top.transform, "TopText", "", 28);
            Ui.Fill(_top.rectTransform, 20f, 0f, 20f, 0f);

            _vitals = Ui.Label(_canvas.transform, "Vitals", "", 26, TextAnchor.LowerLeft);
            Ui.Place(_vitals.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(700f, 120f));

            _weapon = Ui.Label(_canvas.transform, "Weapon", "", 26, TextAnchor.LowerRight);
            Ui.Place(_weapon.rectTransform, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(700f, 120f));

            // P2 spread envelope: retained center dot plus dynamic ticks and a thin distribution ring.
            _crosshair = Ui.Rect(_canvas.transform, "Crosshair");
            Ui.Place(_crosshair, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            var cross = new Color(0.86f, 1f, 0.92f, 0.92f);
            _dynamicCrosshair = _crosshair.gameObject.AddComponent<DynamicCrosshair>();
            _dynamicCrosshair.color = cross;
            _dynamicCrosshair.raycastTarget = false;
            _crosshair.gameObject.SetActive(false);

            // Vertical slice: the P2 loadout is a single auto-equipped rifle, so there is no weapon
            // selection UI. A short buy-phase hint keeps the phase readable without needing the mouse.
            _buyHint = Ui.Label(_canvas.transform, "BuyHint", "", 24, TextAnchor.UpperCenter);
            Ui.Place(_buyHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(900f, 40f));

            BuildPostPanel();
            _partner = Ui.Label(_canvas.transform, "Partner", "", 24, TextAnchor.MiddleCenter);
            Ui.Place(_partner.rectTransform, new Vector2(0.5f, 0.15f), Vector2.zero, new Vector2(1200f, 90f));
            _controls = Ui.Label(_canvas.transform, "Controls", "", 20, TextAnchor.MiddleCenter);
            Ui.Place(_controls.rectTransform, new Vector2(0.5f, 0.03f), Vector2.zero, new Vector2(1200f, 70f));
            _connection = Ui.Label(_canvas.transform, "Connection", "", 24, TextAnchor.MiddleCenter);
            Ui.Place(_connection.rectTransform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1500f, 140f));
        }

        void BuildPostPanel()
        {
            var panel = Ui.Panel(_canvas.transform, "Post", new Color(0.03f, 0.04f, 0.06f, 0.92f));
            Ui.Fill(panel.rectTransform, 0f, 0f, 0f, 0f);
            _postPanel = panel.gameObject;

            _postTitle = Ui.Label(panel.transform, "Winner", "", 72, TextAnchor.MiddleCenter);
            Ui.Place(_postTitle.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(1400f, 120f));

            Button lobby = Ui.Button(panel.transform, "Lobby", "RETURN TO LOBBY", PrivateMatch.ReturnToMenu, 28);
            Ui.Place(lobby.image.rectTransform, new Vector2(0.5f, 0.40f), Vector2.zero, new Vector2(460f, 80f));
            Button quit = Ui.Button(panel.transform, "Quit", "QUIT", Quit, 24);
            Ui.Place(quit.image.rectTransform, new Vector2(0.5f, 0.28f), Vector2.zero, new Vector2(300f, 64f));

            _postPanel.SetActive(false);
        }

        void Update()
        {
            if (_canvas == null)
            {
                if (!NetworkManagerIsClient() && !MatchConfig.AutoStartClient) return;
                Build();
            }

            if (_director == null) _director = MatchDirector.Instance;
            NetworkBody own = FindOwnBody();
            UpdateCoordination(own);
            if (_director == null) { _postPanel.SetActive(false); return; }
            UpdateTop(own);
            UpdateVitals(own);
            UpdateWeapon(own);
            UpdateBuy(own);
            UpdatePost();
        }

        void UpdateTop(NetworkBody own)
        {
            RoundPhase phase = _director.CurrentPhase;
            string score = $"A {_director.TeamAWins.Value} - {_director.TeamBWins.Value} B";
            string time = phase == RoundPhase.Buy || phase == RoundPhase.Live ? $"  {_director.TimeRemaining.Value:0}s" : "";
            string zone = phase == RoundPhase.Live ? $"   zone {_director.ZoneRadius.Value:0}m" : "";
            string role = own != null ? (MatchSlots.RoleOf(NetworkBodyClient.LocalSlotIndex) == 0 ? "P1" : "P2") : "";
            _top.text = $"{_director.CurrentPhase.ToString().ToUpperInvariant()}   round {_director.RoundIndex.Value}   {score}{time}{zone}    you: {role}";
        }

        void UpdateCoordination(NetworkBody own)
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager == null || !manager.IsConnectedClient)
            {
                _connection.text = $"Connecting to {PrivateMatch.ConnectionLabel}...\n{manager?.DisconnectReason}\nESC to leave or open the local partner.";
                _partner.text = _controls.text = "";
                return;
            }
            _connection.text = _director == null || _director.CurrentPhase == RoundPhase.Warmup
                ? $"WAITING FOR YOUR PARTNER\nServer port {PrivateMatch.Current.Port} — join the other role on Team A.\nESC → Open local partner, or join from another PC."
                : "";
            if (own == null) return;
            int role = MatchSlots.RoleOf(NetworkBodyClient.LocalSlotIndex);
            var client = own.GetComponent<NetworkBodyClient>();
            var state = own.State.Value;
            float offset = role == 1 ? client.LocalAimOffset : BodySim.Normalize(state.AimYaw - state.BodyYaw);
            string side = offset < 0f ? "LEFT" : "RIGHT";
            string edge = Mathf.Abs(offset) >= own.SectorHalfDegrees - 1f ? $"  AIM LIMIT {side}" : "";
            if (Mathf.Abs(offset) >= own.SectorHalfDegrees)
                edge = $"  ELASTIC {side} +{Mathf.Abs(offset) - own.SectorHalfDegrees:0.0}°";
            string weapon = own.Reloading.Value ? "RELOADING" : own.Ammo.Value == 0 ? "EMPTY — RELOAD" : own.Firing.Value ? "FIRING" : "READY";
            string body = ((BodyMovementState)state.MovementState).ToString().ToUpperInvariant();
            string request = own.TurnRequest.Value == 0 ? "" : $"  PARTNER REQUESTS TURN {(own.TurnRequest.Value < 0 ? "LEFT" : "RIGHT")}";
            _partner.text = role == 0
                ? $"ARMS: {(own.P2Bot.Value ? "BOT" : "HUMAN")}  {weapon}   aim {offset:+0;-0;0}°{edge}{request}"
                : $"BODY: {(own.P1Bot.Value ? "BOT" : "HUMAN")}  {body}   {state.PlanarSpeed:0.0} m/s\nELASTIC SECTOR  {offset:+0;-0;0}°{edge}";
            _partner.color = edge.Length > 0 || request.Length > 0 ? new Color(1f, 0.76f, 0.3f) : Color.white;
            _controls.text = role == 0
                ? "WASD move · Shift sprint · Ctrl crouch · Space jump · Alt align body\nQ dodge · C slide · F / V kicks · ESC practice tools"
                : "Mouse aim · LMB fire · R reload · Tab request body turn toward aim\nESC practice tools · Elastic stop ±70° + 15° overtravel";
        }

        void UpdateVitals(NetworkBody own)
        {
            if (own == null) { _vitals.text = ""; return; }
            string zone = own.OutsideZone.Value ? "   OUTSIDE ZONE" : "";
            string blind = own.BlindRemaining.Value > 0f ? "   FLASHED" : "";
            string protection = _director.IsLive && own.ProtectionRemaining.Value > 0f ? $"   PROTECTED {own.ProtectionRemaining.Value:0.0}s" : "";
            _vitals.text = $"HP {own.State.Value.Health}   {(own.Alive.Value ? "alive" : "DOWN")}   kills {own.Kills.Value}{zone}{blind}{protection}";
        }

        void UpdateWeapon(NetworkBody own)
        {
            if (own == null) { _weapon.text = ""; return; }
            _weapon.text = $"{(WeaponType)own.WeaponId.Value}   {own.Ammo.Value}/{own.Magazine.Value}{(own.Reloading.Value ? "  RELOADING" : "")}";
            if ((WeaponType)own.WeaponId.Value == WeaponType.Rifle)
            {
                float movement = RifleHandling.MovementSpreadDegrees(own.State.Value, own.WalkSpeed, own.SprintSpeed);
                // This is the current BODY penalty, not a claim that burst bloom/recoil has reset.
                _weapon.text += movement > 0f ? $"\nBODY MOTION: +{movement:0.00}° SPREAD" : "\nBODY STABLE";
            }
        }

        void UpdateBuy(NetworkBody own)
        {
            // The P2 loadout is one auto-equipped rifle in this slice: no interactive buy UI, so the
            // cursor stays captured through the buy phase. The property is still published so a
            // future buy/menu UI can release the cursor by setting it true.
            if (_localPlayer != null) _localPlayer.BuyMenuOpen = false;

            bool p2Own = own != null && NetworkBodyClient.LocalSlotIndex >= 0 && MatchSlots.RoleOf(NetworkBodyClient.LocalSlotIndex) == 1;
            if (_buyHint != null)
                _buyHint.text = (_director.IsBuy && p2Own) ? "BUY PHASE — rifle equipped" : "";
        }

        void UpdatePost()
        {
            bool ended = _director.CurrentPhase == RoundPhase.MatchEnd || _director.MatchWinner.Value >= 0;
            if (!ended) { _postPanel.SetActive(false); return; }
            _postPanel.SetActive(true);
            int winner = _director.MatchWinner.Value;
            int myTeam = NetworkBodyClient.LocalSlotIndex >= 0 ? MatchSlots.TeamOf(NetworkBodyClient.LocalSlotIndex, MatchConfig.BodiesPerTeam) : MatchConfig.ClientTeam;
            _postTitle.text = winner < 0 ? "MATCH DRAW" : (winner == myTeam ? "VICTORY" : "DEFEAT");
        }

        void LateUpdate()
        {
            // After local input/recoil and LocalPlayer's camera LateUpdate: use this frame's actual
            // world-camera projection, never the separate viewmodel FOV.
            if (_crosshair == null) return;
            var client = _localPlayer != null ? _localPlayer.LocalClient : null;
            var camera = _localPlayer != null ? _localPlayer.LocalCamera : null;
            bool visible = client != null && client.IsLocalOwnBody && client.LocalRoleIndex == 1 &&
                client.Body.Alive.Value && _director != null && _director.IsLive &&
                _localPlayer.IsGameplayActive && camera != null;
            _crosshair.gameObject.SetActive(visible);
            if (visible)
            {
                if (_crosshairClient != client || _crosshairEpoch != client.ControlEpoch)
                {
                    _crosshairClient = client; _crosshairEpoch = client.ControlEpoch;
                    _dynamicCrosshair.ResetSpread();
                }
                _dynamicCrosshair.SetSpread(client.NextRifleSpreadDegrees, camera, _canvas, Time.unscaledDeltaTime);
            }
        }

        NetworkBody FindOwnBody()
        {
            if (NetworkBodyClient.LocalSlotIndex < 0) return null;
            int team = MatchSlots.TeamOf(NetworkBodyClient.LocalSlotIndex, MatchConfig.BodiesPerTeam);
            int body = MatchSlots.BodyOf(NetworkBodyClient.LocalSlotIndex, MatchConfig.BodiesPerTeam);
            NetworkBody[] bodies = FindObjectsByType<NetworkBody>();
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
