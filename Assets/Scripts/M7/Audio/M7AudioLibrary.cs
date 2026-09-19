using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.M7
{
    /// <summary>Maps gameplay/UI audio events to clips. Owned by the project, not a vendor.</summary>
    [CreateAssetMenu(menuName = "Be My Arms/M7 Audio Library", fileName = "M7AudioLibrary")]
    public class M7AudioLibrary : ScriptableObject
    {
        public List<M7AudioClipEntry> Clips = new List<M7AudioClipEntry>();

        public M7AudioClipEntry Get(M7AudioId id)
        {
            for (int i = 0; i < Clips.Count; i++)
                if (Clips[i].Id == id && Clips[i].Clip != null) return Clips[i];
            return null;
        }
    }
}
