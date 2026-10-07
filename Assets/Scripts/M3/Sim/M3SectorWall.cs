using System;
using BeMyArms.M2;

namespace BeMyArms.M3
{
    /// <summary>World-stable aim with a stiff, releasable elastic stop.</summary>
    public static class M3SectorWall
    {
        public const float OvertravelDegrees = 15f;
        public const float OutwardGain = 0.08f;
        public const float ReturnRate = 24f;

        public static float Step(float worldYaw, float bodyYaw, float deltaYaw, float half,
            float deltaTime, ref float returnVelocity, out int blockedSide)
        {
            half = Math.Max(1f, half);
            float limit = half + OvertravelDegrees;
            float offset = M2BodySim.Normalize(M2BodySim.ClampToSector(worldYaw, bodyYaw, limit) - bodyYaw);
            {
                bool outwardPressure = deltaYaw != 0f && Math.Sign(deltaYaw) == Math.Sign(offset);
                if (deltaYaw != 0f)
                {
                    // Inward input and all travel inside the resting sector are exactly 1:1.
                    // Integrate diminishing outward gain by distance, independent of frame rate.
                    int direction = Math.Sign(deltaYaw);
                    float towardEdge = offset * direction;
                    float normalTravel = Math.Min(Math.Abs(deltaYaw), Math.Max(0f, half - towardEdge));
                    towardEdge += normalTravel;
                    float resistantTravel = Math.Abs(deltaYaw) - normalTravel;
                    if (resistantTravel > 0f)
                    {
                        float excess = Math.Max(0f, towardEdge - half);
                        excess = OvertravelDegrees - (OvertravelDegrees - excess) *
                            (float)Math.Exp(-OutwardGain * resistantTravel / OvertravelDegrees);
                        towardEdge = half + excess;
                    }
                    offset = direction * towardEdge;
                    // Reversal cancels stored spring velocity instead of fighting the player's hand.
                    returnVelocity = 0f;
                    outwardPressure |= resistantTravel > 0f;
                }

                float overtravel = Math.Abs(offset) - half;
                if (overtravel > 0f && !outwardPressure)
                {
                    // Exact critically damped solution: return starts on the first release frame,
                    // with no overshoot into the sector or frame-dependent Euler instability.
                    float dt = Math.Max(0f, deltaTime);
                    float term = (returnVelocity + ReturnRate * overtravel) * dt;
                    float decay = (float)Math.Exp(-ReturnRate * dt);
                    float remaining = (overtravel + term) * decay;
                    returnVelocity = (returnVelocity - ReturnRate * term) * decay;
                    if (remaining <= 0.005f) { remaining = 0f; returnVelocity = 0f; }
                    offset = Math.Sign(offset) * (half + remaining);
                }
                else if (overtravel <= 0f) returnVelocity = 0f;
            }
            blockedSide = Math.Abs(offset) >= half - 0.05f ? Math.Sign(offset) : 0;
            return M2BodySim.Normalize(bodyYaw + offset);
        }
    }
}
