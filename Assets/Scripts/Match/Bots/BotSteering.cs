using BeMyArms.Networking;

namespace BeMyArms.Match
{
    /// <summary>Bot look is angular velocity, not a large per-tick mouse flick. Continuous align
    /// keeps body and look together without alternating fast align and threshold follow.</summary>
    public static class BotSteering
    {
        public const float TurnDegreesPerSecond = 90f;

        public static P1Input Turn(float lookYaw, float bodyYaw, float desiredYaw, float dt)
            => new P1Input
            {
                LookYawDelta = BodySim.Normalize(BodySim.MoveTowardsAngle(bodyYaw, desiredYaw,
                    TurnDegreesPerSecond * dt) - lookYaw),
                AlignBody = true
            };
    }
}
