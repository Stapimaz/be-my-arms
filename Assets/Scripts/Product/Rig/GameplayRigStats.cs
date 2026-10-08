using UnityEngine;

namespace BeMyArms.Product
{
    /// <summary>Marker for components that can affect authoritative gameplay. Cosmetics must not carry one.</summary>
    public interface IGameplayStatSource
    {
    }

    /// <summary>
    /// The gameplay statistics of one shared body, held on the authoritative rig — never on a
    /// cosmetic skin. The contract validator treats the presence of this component inside a cosmetic
    /// layer as a contract violation, which is how "cosmetics never change stats" is enforced.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M5", "BeMyArms.M5", "M5GameplayRigStats")]
    public class GameplayRigStats : MonoBehaviour, IGameplayStatSource
    {
        public float MaxHealth = 100f;
        public float MoveSpeed = 5f;
        public float SectorHalfDegrees = 70f;
        public float BodyRadius = 0.35f;
    }
}
