namespace BeMyArms.M2
{
    /// <summary>
    /// P1 posture carried in the authoritative state. Drives animation intent and (conceptually) the
    /// weapon-accuracy model; the networked hit path validates cadence/ammo/sector independently.
    /// </summary>
    public enum M2MovementState : byte
    {
        Idle,
        Walk,
        Sprint,
        Jump,
        Fall,
        Dodge,
        Slide,
        Vault,
        KickLight,
        KickHeavy
    }
}
