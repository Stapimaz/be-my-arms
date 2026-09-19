using UnityEngine;

namespace BeMyArms.M7
{
    /// <summary>Small player settings store (foundation for the M8 settings screen).</summary>
    public static class M7Settings
    {
        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat("m7.sensitivity", 0.12f);
            set { PlayerPrefs.SetFloat("m7.sensitivity", value); PlayerPrefs.Save(); }
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat("m7.volume", 0.8f);
            set
            {
                PlayerPrefs.SetFloat("m7.volume", value);
                AudioListener.volume = value;
                PlayerPrefs.Save();
            }
        }

        public static void Apply() => AudioListener.volume = MasterVolume;
    }
}
