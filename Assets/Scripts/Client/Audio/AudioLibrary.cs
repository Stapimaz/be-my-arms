using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.Client
{
    /// <summary>Maps gameplay/UI audio events to clips. Owned by the project, not a vendor.</summary>
    [CreateAssetMenu(menuName = "Be My Arms/Client Audio Library", fileName = "AudioLibrary")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        public List<AudioClipEntry> Clips = new List<AudioClipEntry>();

        public AudioClipEntry Get(AudioId id)
        {
            for (int i = 0; i < Clips.Count; i++)
                if (Clips[i].Id == id && Clips[i].Clip != null) return Clips[i];
            return null;
        }
    }
}
