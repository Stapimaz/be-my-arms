using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.M7
{
    /// <summary>Simple pooled one-shot VFX spawner driven by the M7 VFX library.</summary>
    public class M7VfxService : MonoBehaviour
    {
        public static M7VfxService Instance { get; private set; }

        public M7VfxLibrary Library;
        public int PoolPerEffect = 4;

        readonly Dictionary<M7VfxId, Stack<GameObject>> _pools = new Dictionary<M7VfxId, Stack<GameObject>>();
        readonly List<(GameObject go, float endTime)> _active = new List<(GameObject, float)>();

        void Awake()
        {
            Instance = this;
        }

        public GameObject Spawn(M7VfxId id, Vector3 position, Quaternion rotation)
        {
            M7VfxEntry entry = Library != null ? Library.Get(id) : null;
            if (entry == null) return null;

            GameObject instance = Rent(id, entry);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * entry.Scale;
            instance.SetActive(true);
            foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true)) ps.Play(true);
            _active.Add((instance, Time.time + Mathf.Max(0.1f, entry.Lifetime)));
            return instance;
        }

        public GameObject SpawnAttached(M7VfxId id, Transform parent)
        {
            if (parent == null) return Spawn(id, Vector3.zero, Quaternion.identity);
            GameObject instance = Spawn(id, parent.position, parent.rotation);
            if (instance != null) instance.transform.SetParent(parent, true);
            return instance;
        }

        void Update()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                (GameObject go, float endTime) = _active[i];
                if (Time.time < endTime) continue;
                if (go != null)
                {
                    foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true);
                    go.SetActive(false);
                    go.transform.SetParent(transform, false);
                }
                _active.RemoveAt(i);
            }
        }

        GameObject Rent(M7VfxId id, M7VfxEntry entry)
        {
            if (!_pools.TryGetValue(id, out Stack<GameObject> pool))
            {
                pool = new Stack<GameObject>();
                _pools[id] = pool;
            }
            while (pool.Count > 0)
            {
                GameObject pooled = pool.Pop();
                if (pooled != null) return pooled;
            }
            var instance = Instantiate(entry.Prefab, transform);
            instance.name = "vfx_" + id;
            return instance;
        }
    }
}
