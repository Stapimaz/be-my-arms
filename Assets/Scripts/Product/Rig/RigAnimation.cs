using UnityEngine;

namespace BeMyArms.Product
{
    /// <summary>
    /// The animation interface every gameplay rig must expose. A skin may drive presentation through
    /// it but may not replace it.
    /// </summary>
    public interface IRigAnimation
    {
        void SetAim(float worldYaw, float pitch);
        void SetMove(float normalizedSpeed);
        void SetPose(string poseId);
    }

    /// <summary>Placeholder animation implementation that only stores the values.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M5", "BeMyArms.M5", "M5RigAnimation")]
    public class RigAnimation : MonoBehaviour, IRigAnimation
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
