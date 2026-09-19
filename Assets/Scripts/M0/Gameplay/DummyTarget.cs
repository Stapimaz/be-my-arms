using UnityEngine;

namespace BeMyArms.M0
{
    /// <summary>
    /// Simple respawning target. Used to verify hitscan, headshot regions and the shared
    /// damage pipeline. Not enemy AI; M0 dummies never shoot.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class DummyTarget : MonoBehaviour
    {
        public float RespawnDelay = 2f;

        Damageable _damageable;
        Collider[] _colliders;
        Renderer[] _renderers;
        float _respawnAt = -1f;

        void Awake()
        {
            _damageable = GetComponent<Damageable>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _damageable.Died += OnDied;
        }

        void OnDestroy()
        {
            if (_damageable != null) _damageable.Died -= OnDied;
        }

        void OnDied(Damageable damageable)
        {
            SetActiveState(false);
            _respawnAt = Time.time + RespawnDelay;
        }

        void Update()
        {
            if (_respawnAt < 0f || Time.time < _respawnAt) return;
            _respawnAt = -1f;
            _damageable.ResetHealth();
            SetActiveState(true);
        }

        void SetActiveState(bool active)
        {
            foreach (Collider collider in _colliders) collider.enabled = active;
            foreach (Renderer renderer in _renderers) renderer.enabled = active;
        }
    }
}
