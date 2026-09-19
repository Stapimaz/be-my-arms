using BeMyArms.M0;
using UnityEngine;

namespace BeMyArms.M1
{
    /// <summary>
    /// Server-authoritative-ready weapon logic (simulation only): fire cadence, magazine, reload,
    /// weapon swap, movement-based spread and recoil. P2 keeps firing in every P1 posture; posture
    /// only changes spread, never locks the weapon.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        public M1Tuning tuning;
        public P2AimRig aim;
        public Transform eye;

        WeaponDefinition[] _weapons;
        int[] _ammo;
        int _index;
        bool _reloading;
        float _reloadEndTime;
        float _swapEndTime;
        float _nextFireTime;

        public WeaponDefinition Current => _weapons != null && _weapons.Length > 0 ? _weapons[_index] : null;
        public int CurrentIndex => _index;
        public int CurrentAmmo => _ammo != null && _ammo.Length > 0 ? _ammo[_index] : 0;
        public bool IsReloading => _reloading;
        public float CurrentSpreadDegrees { get; private set; }

        public void Configure(M1Tuning t, P2AimRig aimRig, Transform eyeTransform)
        {
            tuning = t;
            aim = aimRig;
            eye = eyeTransform;
            if (t == null || t.weapons == null || t.weapons.Length == 0) return;

            _weapons = t.weapons;
            _index = Mathf.Clamp(t.startingWeaponIndex, 0, _weapons.Length - 1);
            _ammo = new int[_weapons.Length];
            for (int i = 0; i < _weapons.Length; i++) _ammo[i] = _weapons[i].magazine;
        }

        public void Step(in P2Command cmd, float bodyYaw, MovementState posture, float deltaTime)
        {
            if (_weapons == null || _weapons.Length == 0) return;

            float now = Time.time;

            if (_reloading && now >= _reloadEndTime)
            {
                _reloading = false;
                _ammo[_index] = Current.magazine;
            }

            if (cmd.SwitchRequested && now >= _swapEndTime && cmd.SwitchWeapon >= 0 && cmd.SwitchWeapon < _weapons.Length)
            {
                _index = cmd.SwitchWeapon;
                _reloading = false;
                _swapEndTime = now + Current.swapSeconds;
            }

            float multiplier = tuning != null && tuning.accuracyByState != null && tuning.accuracyByState.Length > 0
                ? AccuracyModel.SpreadMultiplier(posture, tuning.accuracyByState)
                : AccuracyModel.DefaultSpreadMultiplier(posture);
            CurrentSpreadDegrees = Current.baseSpreadDegrees * multiplier;

            if (Current.kind == WeaponKind.Knife)
            {
                if (cmd.KnifeAttack && now >= _nextFireTime) Melee(now);
                return;
            }

            if (cmd.Reload && !_reloading && CurrentAmmo < Current.magazine && now >= _swapEndTime)
            {
                _reloading = true;
                _reloadEndTime = now + Current.reloadSeconds;
                return;
            }

            if (cmd.Fire && !_reloading && now >= _nextFireTime && now >= _swapEndTime && CurrentAmmo > 0)
            {
                Fire(bodyYaw, now);
            }
        }

        void Fire(float bodyYaw, float now)
        {
            _nextFireTime = now + Current.SecondsBetweenShots;
            _ammo[_index]--;

            Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up * 1.45f;
            Vector3 direction = ApplySpread(aim != null ? aim.AimDirection : transform.forward);

            RaycastHit[] hits = Physics.RaycastAll(origin, direction, Current.rangeMeters, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool hitSomething = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform.root)) continue;
                HitboxRegion region = hit.collider.GetComponentInParent<HitboxRegion>();
                if (region == null || region.owner == null) break;
                float amount = region.region == HitboxRegion.Region.Head ? Current.damage * Current.headshotMultiplier : Current.damage;
                region.owner.ApplyDamage(amount, region.region);
                Debug.Log($"[M1] {Current.displayName} {(region.region == HitboxRegion.Region.Head ? "HEADSHOT" : "hit")} {amount:0} -> {region.owner.Health:0}");
                hitSomething = true;
                break;
            }
            if (!hitSomething) Debug.Log($"[M1] {Current.displayName} miss (spread {CurrentSpreadDegrees:0.0} deg)");

            if (aim != null && tuning != null)
            {
                aim.AddRecoil(Current.recoilPerShot, Random.Range(-Current.recoilPerShot, Current.recoilPerShot) * 0.35f, bodyYaw);
            }
        }

        void Melee(float now)
        {
            _nextFireTime = now + 0.4f;
            Vector3 origin = transform.position + Vector3.up * 1.2f + transform.forward * 0.3f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.5f, transform.forward, Current.meleeRangeMeters, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform.root)) continue;
                HitboxRegion region = hit.collider.GetComponentInParent<HitboxRegion>();
                if (region == null || region.owner == null) continue;
                region.owner.ApplyDamage(Current.damage, region.region);
                Debug.Log($"[M1] {Current.displayName} melee {Current.damage:0} -> {region.owner.Health:0}");
                return;
            }
        }

        Vector3 ApplySpread(Vector3 direction)
        {
            if (CurrentSpreadDegrees <= 0f) return direction;
            Vector2 offset = Random.insideUnitCircle * CurrentSpreadDegrees;
            Quaternion rotation = Quaternion.Euler(offset.y, offset.x, 0f);
            return rotation * direction.normalized;
        }
    }
}
