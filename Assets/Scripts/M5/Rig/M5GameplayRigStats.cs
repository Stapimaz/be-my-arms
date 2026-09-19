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
}
