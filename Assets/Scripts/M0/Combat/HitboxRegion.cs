using UnityEngine;

namespace BeMyArms.M0
{
    /// <summary>
    /// Marks a collider as a damage region. P1's head is the only critical region;
    /// P2's arms/shoulders/upper chest and everything else take normal damage.
    /// Cosmetic meshes must never change this.
    /// </summary>
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
