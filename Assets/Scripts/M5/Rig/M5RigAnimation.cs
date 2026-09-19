using UnityEngine;

namespace BeMyArms.M5
{
    /// <summary>
    /// The animation interface every gameplay rig must expose. A skin may drive presentation through
    /// it but may not replace it.
    /// </summary>
    public interface IM5RigAnimation
    {
        void SetAim(float worldYaw, float pitch);
        void SetMove(float normalizedSpeed);
        void SetPose(string poseId);
    }

    /// <summary>Placeholder animation implementation that only stores the values.</summary>
    public class M5RigAnimation : MonoBehaviour, IM5RigAnimation
    {
        public float WorldYaw { get; private set; }
        public float Pitch { get; private set; }
        public float MoveSpeed { get; private set; }
        public string Pose { get; private set; } = "";

        public void SetAim(float worldYaw, float pitch)
        {
            WorldYaw = worldYaw;
            Pitch = pitch;
        }

        public void SetMove(float normalizedSpeed) => MoveSpeed = normalizedSpeed;
        public void SetPose(string poseId) => Pose = poseId ?? "";
    }
}
