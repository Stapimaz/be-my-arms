using UnityEngine;

namespace BeMyArms.M0
{
    /// <summary>Base damage receiver for M0. Shared HP pools and dummy targets derive from this.</summary>
    public class Damageable : MonoBehaviour
    {
        public float MaxHealth = 100f;

        public float Health { get; protected set; }
        public bool IsAlive => Health > 0f;

        public event System.Action<Damageable, float, HitboxRegion.Region> Damaged;
        public event System.Action<Damageable> Died;

        protected virtual void Awake()
        {
            Health = MaxHealth;
        }

        public virtual void ApplyDamage(float amount, HitboxRegion.Region region)
        {
            if (amount <= 0f || Health <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            Damaged?.Invoke(this, amount, region);
            if (Health <= 0f) Died?.Invoke(this);
        }

        public void ResetHealth()
        {
            Health = MaxHealth;
        }
    }
}
