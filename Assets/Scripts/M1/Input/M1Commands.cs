using UnityEngine;

namespace BeMyArms.M1
{
    /// <summary>P1 role command: locomotion, body orientation and melee.</summary>
    public struct P1Command
    {
        public Vector2 Move;
        public float LookYawDelta;
        public float LookPitchDelta;
        public bool Sprint;
        public bool Jump;
        public bool Dodge;
        public Vector2 DodgeDirection;
        public bool Slide;
        public bool Vault;
        public bool LightKick;
        public bool HeavyKick;
    }

    /// <summary>P2 role command: aim, weapons and utility/hands (hands are M3+).</summary>
    public struct P2Command
    {
        public float AimYawDelta;
        public float AimPitchDelta;
        public bool Fire;
        public bool Reload;
        public bool SwitchRequested;
        public int SwitchWeapon;
        public bool KnifeAttack;
    }

    public interface IP1CommandSource
    {
        P1Command Read(float deltaTime);
    }

    public interface IP2CommandSource
    {
        P2Command Read(float deltaTime);
    }

    public enum M1InputMode
    {
        Device,
        Scripted
    }
}
