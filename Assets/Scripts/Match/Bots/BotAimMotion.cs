using BeMyArms.Networking;
using UnityEngine;

namespace BeMyArms.Match
{
    /// <summary>Mouse-like angular motion. Target/shot changes alter the destination, never the pose.</summary>
    public sealed class BotAimMotion
    {
        Vector2 _velocity;
        public void Reset() => _velocity = Vector2.zero;

        public void Step(float yaw, float pitch, float desiredYaw, float desiredPitch, float bodyYaw,
            float sector, float maxPitch, float dt, bool easy, out float nextYaw, out float nextPitch)
        {
            desiredYaw = BodySim.ClampToSector(desiredYaw, bodyYaw, sector);
            desiredPitch = Mathf.Clamp(desiredPitch, -maxPitch, maxPitch);
            Vector2 error = new Vector2(BodySim.Normalize(desiredYaw - yaw), desiredPitch - pitch);
            float speed = easy ? 100f : 160f;
            Vector2 desiredVelocity = Vector2.ClampMagnitude(error / .20f, speed);
            _velocity = Vector2.MoveTowards(_velocity, desiredVelocity, (easy ? 420f : 650f) * dt);
            Vector2 step = _velocity * dt;
            // A goal moving just ahead of a fast turn may be overshot slightly while braking.
            // Do not snap to it or zero velocity: acquisition and braking share the same bound.
            // Only physical sector/pitch walls may constrain the pose, as with human input.
            nextYaw = BodySim.ClampToSector(BodySim.Normalize(yaw + step.x), bodyYaw, sector);
            nextPitch = Mathf.Clamp(pitch + step.y, -maxPitch, maxPitch);
        }
    }
}
