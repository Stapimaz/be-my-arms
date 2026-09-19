using UnityEngine;

namespace BeMyArms.M0
{
    /// <summary>
    /// M0 hitscan weapon. Fires along P2's world-space aim direction from the shoulder
    /// anchor. Headshot damage comes from the hit region, nothing else.
    /// No reload, no weapon switching, no spread: those are M1.
    /// </summary>
    public class HitscanWeapon : MonoBehaviour
    {
        public float FireRatePerSecond = 8f;
        public float Damage = 18f;
        public float HeadshotMultiplier = 2.5f;
        public float RangeMeters = 150f;

        float _nextAllowedFireTime;

        public void Configure(M0Tuning tuning)
        {
            if (tuning == null) return;
            FireRatePerSecond = tuning.fireRatePerSecond;
            Damage = tuning.damage;
            HeadshotMultiplier = tuning.headshotMultiplier;
            RangeMeters = tuning.rangeMeters;
        }

        public void Step(bool fire, float deltaTime, P2AimController aim, Transform eye, SharedBodyHealth self)
        {
            if (!fire || aim == null || eye == null) return;
            if (Time.time < _nextAllowedFireTime) return;

            _nextAllowedFireTime = Time.time + 1f / Mathf.Max(0.01f, FireRatePerSecond);

            Vector3 origin = eye.position;
            Vector3 direction = aim.AimDirection;
            if (direction.sqrMagnitude < 0.0001f) return;

            RaycastHit[] hits = Physics.RaycastAll(origin, direction, RangeMeters, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                HitboxRegion region = hit.collider.GetComponentInParent<HitboxRegion>();
                if (region == null || region.owner == null)
                {
                    // Hit geometry that is not a damageable body: the bullet stops here.
                    Debug.DrawLine(origin, hit.point, Color.grey, 0.1f);
                    break;
                }

                if (region.owner == self)
                {
                    // Ignore the shooter's own body (the muzzle starts inside it).
                    continue;
                }

                float amount = region.region == HitboxRegion.Region.Head
                    ? Damage * HeadshotMultiplier
                    : Damage;

                region.owner.ApplyDamage(amount, region.region);
                Debug.DrawLine(origin, hit.point,
                    region.region == HitboxRegion.Region.Head ? Color.red : Color.yellow, 0.15f);
                break;
            }
        }
    }
}
