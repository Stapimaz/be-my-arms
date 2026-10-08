using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.Client
{
    /// <summary>Simple pooled one-shot VFX spawner driven by the Client VFX library.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7VfxService")]
    public class VfxService : MonoBehaviour
    {
        public static VfxService Instance { get; private set; }

        public VfxLibrary Library;
        public int PoolPerEffect = 4;

        readonly Dictionary<VfxId, Stack<GameObject>> _pools = new Dictionary<VfxId, Stack<GameObject>>();
        readonly List<(GameObject go, float endTime, VfxId id)> _active = new();

        void Awake()
        {
            Instance = this;
        }

        public GameObject Spawn(VfxId id, Vector3 position, Quaternion rotation)
        {
            VfxEntry entry = Library != null ? Library.Get(id) : null;
            if (entry == null) return null;

            GameObject instance = Rent(id, entry);
            instance.transform.SetParent(transform, false);
            SetLayer(instance, entry.Prefab.layer);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * entry.Scale;
            instance.SetActive(true);
            foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true)) ps.Play(true);
            _active.Add((instance, Time.time + Mathf.Max(0.1f, entry.Lifetime), id));
            return instance;
        }

        public GameObject SpawnAttached(VfxId id, Transform parent)
        {
            if (parent == null) return Spawn(id, Vector3.zero, Quaternion.identity);
            GameObject instance = Spawn(id, parent.position, parent.rotation);
            if (instance != null) { instance.transform.SetParent(parent, true); SetLayer(instance, parent.gameObject.layer); }
            return instance;
        }

        void Update()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                (GameObject go, float endTime, VfxId id) = _active[i];
                if (Time.time < endTime) continue;
                if (go != null)
                {
                    foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true);
                    go.SetActive(false);
                    go.transform.SetParent(transform, false);
                    var pool=_pools[id];
                    if(pool.Count<PoolPerEffect) pool.Push(go); else Destroy(go);
                }
                _active.RemoveAt(i);
            }
        }

        GameObject Rent(VfxId id, VfxEntry entry)
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
        static void SetLayer(GameObject go,int layer)
        { foreach(var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=layer; }
    }
}
