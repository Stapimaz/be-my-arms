using UnityEngine;

namespace BeMyArms.M3
{
    /// <summary>
    /// Minimal greybox Duel HUD (IMGUI): phase, round/score, timer, own health/ammo, buy list and
    /// the closing-zone state. Presentation only; it never changes authoritative state.
    /// </summary>
    public class M3DuelHud : MonoBehaviour
    {
        public float Width = 340f;
        public float Height = 220f;

        M3DuelBody _own;

        void OnGUI()
        {
            M3DuelDirector director = M3DuelDirector.Instance;
            if (director == null || Application.isBatchMode) return;

            if (_own == null || !_own.IsSpawned)
            {
                int team = M3DuelSlots.IsValid(M3DuelClient.LocalSlot) ? M3DuelSlots.Team(M3DuelClient.LocalSlot) : M3Config.ClientTeam;
                M3DuelBody[] bodies = FindObjectsByType<M3DuelBody>();
                for (int i = 0; i < bodies.Length; i++)
                    if (bodies[i].IsSpawned && bodies[i].TeamIndex == team) { _own = bodies[i]; break; }
            }

            GUILayout.BeginArea(new Rect(12f, 12f, Width, Height), GUI.skin.box);
            M3Phase phase = director.CurrentPhase;
            GUILayout.Label($"DUEL  |  {phase}  |  round {director.RoundIndex.Value}");
            GUILayout.Label($"score   A {director.TeamAWins.Value}  -  B {director.TeamBWins.Value}   (first to 3, max 5)");

            if (phase == M3Phase.Buy || phase == M3Phase.Live)
                GUILayout.Label($"time remaining  {director.TimeRemaining.Value:0.0}s");

            if (phase == M3Phase.Live)
                GUILayout.Label($"closing zone radius  {director.ZoneRadius.Value:0.0}m");

            if (director.MatchWinner.Value >= 0)
                GUILayout.Label($"MATCH WINNER: team {(director.MatchWinner.Value == 0 ? "A" : "B")}");
            else if (phase == M3Phase.MatchEnd)
                GUILayout.Label("MATCH DRAW");

            if (_own != null)
            {
                GUILayout.Label($"you: team {(_own.TeamIndex == 0 ? "A" : "B")} {(M3DuelSlots.IsValid(M3DuelClient.LocalSlot) ? (M3DuelSlots.Role(M3DuelClient.LocalSlot) == 0 ? "P1" : "P2") : "")}");
                GUILayout.Label($"health {_own.State.Value.Health}   alive {_own.Alive.Value}   kills {_own.Kills.Value}");
                GUILayout.Label($"weapon {(M3WeaponId)_own.WeaponId.Value}   ammo {_own.Ammo.Value}/{_own.Magazine.Value}");
                if (_own.BlindRemaining.Value > 0f) GUILayout.Label($"FLASHED {_own.BlindRemaining.Value:0.0}s");
                if (_own.OutsideZone.Value) GUILayout.Label("OUTSIDE CLOSING ZONE - taking damage");
            }

            if (phase == M3Phase.Buy)
                GUILayout.Label("BUY (P2, auto in headless): rifle+ammo 700 / smg 500 / shotgun 450 / pistol 150 / smoke 150 / flash 150 / grenade 200");

            GUILayout.EndArea();
        }
    }
}
