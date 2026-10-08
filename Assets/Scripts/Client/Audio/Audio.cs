using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.Client
{
    /// <summary>Gameplay/UI audio events. The library maps these to clips.</summary>
    public enum AudioId
    {
        RifleShot,
        PistolShot,
        ShotgunShot,
        KnifeSwing,
        Reload,
        Footstep,
        HitBody,
        Headshot,
        Elimination,
        RoundStart,
        RoundEnd,
        MatchEnd,
        UiClick,
        UiHover,
        GrenadeExplosion,
        FlashBang,
        SmokeDeploy,
        ZoneWarning,
        AmbArena,
        MusicMenu,
        MusicMatch,
        MagazineOut, MagazineIn, Bolt, Jump, Land, Slide, Vault, Kick, Gear
    }

    [Serializable]
    public class AudioClipEntry
    {
        public AudioId Id;
        public AudioClip Clip;
        [Range(0f, 2f)] public float Volume = 1f;
        [Range(0f, 1f)] public float PitchJitter;
        public bool Spatial;
        public bool Loop;
    }

    public static class AudioIds
    {
        public static readonly AudioId[] Sfx =
        {
            AudioId.RifleShot, AudioId.PistolShot, AudioId.ShotgunShot, AudioId.KnifeSwing,
            AudioId.Reload, AudioId.Footstep, AudioId.HitBody, AudioId.Headshot,
            AudioId.Elimination, AudioId.RoundStart, AudioId.RoundEnd, AudioId.MatchEnd,
            AudioId.UiClick, AudioId.UiHover, AudioId.GrenadeExplosion, AudioId.FlashBang,
            AudioId.SmokeDeploy, AudioId.ZoneWarning,
            AudioId.MagazineOut, AudioId.MagazineIn, AudioId.Bolt, AudioId.Jump,
            AudioId.Land, AudioId.Slide, AudioId.Vault, AudioId.Kick, AudioId.Gear
        };

        public static readonly AudioId[] Music = { AudioId.AmbArena, AudioId.MusicMenu, AudioId.MusicMatch };

        public static IEnumerable<AudioId> All
        {
            get
            {
                foreach (AudioId id in Sfx) yield return id;
                foreach (AudioId id in Music) yield return id;
            }
        }
    }
}
