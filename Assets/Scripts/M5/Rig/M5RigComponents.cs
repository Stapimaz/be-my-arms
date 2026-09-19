using UnityEngine;

namespace BeMyArms.M5
{
    /// <summary>Marker for components that can affect authoritative gameplay. Cosmetics must not carry one.</summary>
    public interface IM5GameplayStatSource
    {
    }

    /// <summary>
    /// The gameplay statistics of one shared body, held on the authoritative rig — never on a
    /// cosmetic skin. The contract validator treats the presence of this component inside a cosmetic
    /// layer as a contract violation, which is how "cosmetics never change stats" is enforced.
    /// </summary>
    public class M5GameplayRigStats : MonoBehaviour, IM5GameplayStatSource
    {
        public float MaxHealth = 100f;
        public float MoveSpeed = 5f;
        public float SectorHalfDegrees = 70f;
        public float BodyRadius = 0.35f;
    }

    /// <summary>
    /// The animation interface every gameplay rig must expose. A skin may drive presentation through
    /// it but may not replace it. The placeholder implementation only stores the values.
    /// </summary>
    public interface IM5RigAnimation
    {
        void SetAim(float worldYaw, float pitch);
        void SetMove(float normalizedSpeed);
        void SetPose(string poseId);
    }

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

    /// <summary>
    /// Describes a cosmetic skin: which half of the body it dresses, its stable id and the rig
    /// contract version it was authored against. Skins are pure presentation layers.
    /// </summary>
    public class M5SkinDescriptor : MonoBehaviour
    {
        public M5RigRole Role = M5RigRole.P1;
        public string SkinId = "";
        public string RigId = "default";
    }
}
