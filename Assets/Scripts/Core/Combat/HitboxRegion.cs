using UnityEngine;

namespace BeMyArms.Core
{
    /// <summary>
    /// Marks a collider as a damage region. P1's head is the only critical region;
    /// P2's arms/shoulders/upper chest and everything else take normal damage.
    /// Cosmetic meshes must never change this.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M0", "BeMyArms.M0", "HitboxRegion")]
    public class HitboxRegion : MonoBehaviour
    {
        public enum Region
        {
            Body,
            Head
        }

        public Region region = Region.Body;
        public Damageable owner;
    }
}
