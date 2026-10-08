using UnityEngine;

namespace BeMyArms.Match
{
    public enum DamageKind : byte { World, Weapon, Kick }

    /// <summary>One authoritative damage event, broadcast to clients after the server resolved it.</summary>
    public struct DamageEvent
    {
        public int AttackerTeam;
        public int AttackerBody;
        public int VictimTeam;
        public int VictimBody;
        public Vector3 Point;
        public bool Killed;
        public int Amount;
        public DamageKind Kind;
        public BeMyArms.Core.HitboxRegion.Region Region;
        public uint AttackerEpoch;
        public uint VictimEpoch;

        public bool IsAttacker(int team, int body) => AttackerTeam == team && AttackerBody == body;
        public bool IsVictim(int team, int body) => VictimTeam == team && VictimBody == body;
        public bool ConfirmsFor(int team, int body, uint epoch)
            => Amount > 0 && Kind != DamageKind.World && IsAttacker(team, body) && AttackerEpoch == epoch;
        public bool Hurts(int team, int body, uint epoch)
            => Amount > 0 && IsVictim(team, body) && VictimEpoch == epoch;
    }

    /// <summary>
    /// Client-side combat feedback bus. The server broadcasts confirmed damage/hit events; the Client
    /// presentation (hitmarkers, damage feedback, impact particles) subscribes. Only authoritative
    /// events are raised — never speculative client hits.
    /// </summary>
    public static class CombatEvents
    {
        public static event System.Action<DamageEvent> Damage;
        public static event System.Action<Vector3> WorldImpact;

        public static void RaiseDamage(in DamageEvent e) => Damage?.Invoke(e);
        public static void RaiseWorldImpact(Vector3 point) => WorldImpact?.Invoke(point);
    }
}
