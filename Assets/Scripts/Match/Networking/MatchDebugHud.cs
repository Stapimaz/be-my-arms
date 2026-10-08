using UnityEngine;

namespace BeMyArms.Match
{
    /// <summary>
    /// Minimal greybox Duel HUD (IMGUI): phase, round/score, timer, own health/ammo, buy list and
    /// the closing-zone state. Presentation only; it never changes authoritative state.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M3", "BeMyArms.M3", "M3DuelHud")]
    public class MatchDebugHud : MonoBehaviour
    {
        public float Width = 340f;
        public float Height = 220f;

        NetworkBody _own;

        void OnGUI()
        {
            MatchDirector director = MatchDirector.Instance;
            if (director == null || Application.isBatchMode) return;

            int bodiesPerTeam = director.BodiesPerTeam;
            int slot = NetworkBodyClient.LocalSlotIndex;
            int team = MatchSlots.IsValidSlot(slot, bodiesPerTeam) ? MatchSlots.TeamOf(slot, bodiesPerTeam) : MatchConfig.ClientTeam;
            int bodyIndex = MatchSlots.IsValidSlot(slot, bodiesPerTeam) ? MatchSlots.BodyOf(slot, bodiesPerTeam) : MatchConfig.ClientBody;

            if (_own == null || !_own.IsSpawned)
            {
                NetworkBody[] bodies = FindObjectsByType<NetworkBody>();
                for (int i = 0; i < bodies.Length; i++)
                    if (bodies[i].IsSpawned && bodies[i].TeamIndex == team && bodies[i].BodyId == bodyIndex) { _own = bodies[i]; break; }
            }

            GUILayout.BeginArea(new Rect(12f, 12f, Width, Height), GUI.skin.box);
            RoundPhase phase = director.CurrentPhase;
            string mode = bodiesPerTeam >= 2 ? "2v2" : "DUEL";
            GUILayout.Label($"{mode}  |  {phase}  |  round {director.RoundIndex.Value}  |  {MatchSlots.Name(slot, bodiesPerTeam)}");
            GUILayout.Label($"score   A {director.TeamAWins.Value}  -  B {director.TeamBWins.Value}   (first to 3, max 5)");
            GUILayout.Label($"bodies alive   A {director.BodiesAliveA.Value}  -  B {director.BodiesAliveB.Value}");

            if (phase == RoundPhase.Buy || phase == RoundPhase.Live)
                GUILayout.Label($"time remaining  {director.TimeRemaining.Value:0.0}s");

            if (phase == RoundPhase.Live)
                GUILayout.Label($"closing zone radius  {director.ZoneRadius.Value:0.0}m");

            if (director.MatchWinner.Value >= 0)
                GUILayout.Label($"MATCH WINNER: team {(director.MatchWinner.Value == 0 ? "A" : "B")}");
            else if (phase == RoundPhase.MatchEnd)
                GUILayout.Label("MATCH DRAW");

            if (_own != null)
            {
                GUILayout.Label($"you: team {(team == 0 ? "A" : "B")} body {bodyIndex} {(MatchSlots.IsValidSlot(slot, bodiesPerTeam) ? (MatchSlots.RoleOf(slot) == 0 ? "P1" : "P2") : "")}");
                GUILayout.Label($"health {_own.State.Value.Health}   alive {_own.Alive.Value}   kills {_own.Kills.Value}");
                GUILayout.Label($"weapon {(WeaponType)_own.WeaponId.Value}   ammo {_own.Ammo.Value}/{_own.Magazine.Value}");
                if (_own.BlindRemaining.Value > 0f) GUILayout.Label($"FLASHED {_own.BlindRemaining.Value:0.0}s");
                if (_own.OutsideZone.Value) GUILayout.Label("OUTSIDE CLOSING ZONE - taking damage");
            }

            if (phase == RoundPhase.Buy)
                GUILayout.Label("BUY (P2, auto in headless): rifle 700 / smg 500 / shotgun 450 / pistol 150 / smoke 150 / flash 150 / grenade 200");

            GUILayout.EndArea();
        }
    }
}
