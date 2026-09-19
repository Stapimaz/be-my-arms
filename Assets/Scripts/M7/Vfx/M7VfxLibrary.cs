using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.M7
{
    /// <summary>Maps gameplay VFX events to effect prefabs.</summary>
    [CreateAssetMenu(menuName = "Be My Arms/M7 VFX Library", fileName = "M7VfxLibrary")]
    public class M7VfxLibrary : ScriptableObject
    {
        public List<M7VfxEntry> Effects = new List<M7VfxEntry>();

        public M7VfxEntry Get(M7VfxId id)
        {
            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i].Id == id && Effects[i].Prefab != null) return Effects[i];
            return null;
        }
    }
}
