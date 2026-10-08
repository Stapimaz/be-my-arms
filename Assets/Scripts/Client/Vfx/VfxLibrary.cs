using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.Client
{
    /// <summary>Maps gameplay VFX events to effect prefabs.</summary>
    [CreateAssetMenu(menuName = "Be My Arms/Client VFX Library", fileName = "VfxLibrary")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7VfxLibrary")]
    public class VfxLibrary : ScriptableObject
    {
        public List<VfxEntry> Effects = new List<VfxEntry>();

        public VfxEntry Get(VfxId id)
        {
            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i].Id == id && Effects[i].Prefab != null) return Effects[i];
            return null;
        }
    }
}
