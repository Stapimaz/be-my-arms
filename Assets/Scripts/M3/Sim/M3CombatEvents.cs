using UnityEngine;

namespace BeMyArms.M3
{
    /// <summary>One authoritative damage event, broadcast to clients after the server resolved it.</summary>
    public struct M3DamageEvent
    {
        public int AttackerTeam;
        public int AttackerBody;
        public int VictimTeam;
        public int VictimBody;
        public Vector3 Point;
        public bool Killed;

        public bool IsAttacker(int team, int body) => AttackerTeam == team && AttackerBody == body;
        public bool IsVictim(int team, int body) => VictimTeam == team && VictimBody == body;
    }

    /// <summary>
    /// Client-side combat feedback bus. The server broadcasts confirmed damage/hit events; the M7
    /// presentation (hitmarkers, damage feedback, impact particles) subscribes. Only authoritative
    /// events are raised — never speculative client hits.
    /// </summary>
    public static class M3CombatEvents
    {
        public static event System.Action<M3DamageEvent> Damage;
        public static event System.Action<Vector3> WorldImpact;

        public static void RaiseDamage(in M3DamageEvent e) => Damage?.Invoke(e);
        public static void RaiseWorldImpact(Vector3 point) => WorldImpact?.Invoke(point);
    }
}
