namespace BeMyArms.M2
{
    /// <summary>
    /// Server-side weapon validation state: cadence, magazine, reload. Pure so it can be unit
    /// tested and stay independent from NGO.
    /// </summary>
    public class M2WeaponState
    {
        public enum FireResult
        {
            Ok,
            NotReady,
            Empty,
            Reloading
        }

        public int Magazine = 30;
        public int Ammo;
        public float SecondsBetweenShots = 60f / 480f;
        public float ReloadSeconds = 2.2f;
        public bool IsReloading;
        public double NextFireTime;
        public double ReloadEndTime;

        public M2WeaponState()
        {
            Reset();
        }

        public void Reset()
        {
            Ammo = Magazine;
            IsReloading = false;
            NextFireTime = 0;
            ReloadEndTime = 0;
        }

        public FireResult TryFire(double now)
        {
            if (IsReloading) return FireResult.Reloading;
            if (now < NextFireTime) return FireResult.NotReady;
            if (Ammo <= 0) return FireResult.Empty;
            Ammo--;
            NextFireTime = now + SecondsBetweenShots;
            return FireResult.Ok;
        }

        public void StartReload(double now)
        {
            if (IsReloading || Ammo >= Magazine) return;
            IsReloading = true;
            ReloadEndTime = now + ReloadSeconds;
        }

        public void Tick(double now)
        {
            if (IsReloading && now >= ReloadEndTime)
            {
                IsReloading = false;
                Ammo = Magazine;
            }
        }
    }
}
