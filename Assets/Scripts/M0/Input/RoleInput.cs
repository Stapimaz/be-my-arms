using UnityEngine;

namespace BeMyArms.M0
{
    /// <summary>P1 input: body movement and BodyYaw. No melee, dodge or slide in M0.</summary>
    public struct P1Command
    {
        /// <summary>Raw move axes, x = strafe, y = forward.</summary>
        public Vector2 Move;

        /// <summary>Degrees added to BodyYaw this step (M0: from look input directly).</summary>
        public float LookYawDelta;

        /// <summary>Degrees added to the third-person camera pitch (camera only, not BodyYaw).</summary>
        public float LookPitchDelta;

        public bool Sprint;
    }

    /// <summary>P2 input: desired world aim delta and fire. No reload/swap/utility in M0.</summary>
    public struct P2Command
    {
        /// <summary>Degrees added to the desired world aim yaw this step.</summary>
        public float YawDelta;

        /// <summary>Degrees added to the aim pitch this step.</summary>
        public float PitchDelta;

        public bool Fire;
    }

    /// <summary>One input domain per role. The server will later own this binding; M0 is local.</summary>
    public interface IP1InputSource
    {
        P1Command Read(float deltaTime);
    }

    public interface IP2InputSource
    {
        P2Command Read(float deltaTime);
    }
}
