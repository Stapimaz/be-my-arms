using BeMyArms.M2;

namespace BeMyArms.M3
{
    /// <summary>Bot look is angular velocity, not a large per-tick mouse flick. Continuous align
    /// keeps body and look together without alternating fast align and threshold follow.</summary>
    public static class M3BotSteering
    {
        public const float TurnDegreesPerSecond = 90f;

        public static M2P1Input Turn(float lookYaw, float bodyYaw, float desiredYaw, float dt)
            => new M2P1Input
            {
                LookYawDelta = M2BodySim.Normalize(M2BodySim.MoveTowardsAngle(bodyYaw, desiredYaw,
                    TurnDegreesPerSecond * dt) - lookYaw),
                AlignBody = true
            };
    }
}
