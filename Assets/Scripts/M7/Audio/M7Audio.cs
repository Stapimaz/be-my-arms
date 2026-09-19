using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.M7
{
    /// <summary>Gameplay/UI audio events. The library maps these to clips.</summary>
    public enum M7AudioId
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
        MusicMatch
    }

    [Serializable]
    public class M7AudioClipEntry
    {
        public M7AudioId Id;
        public AudioClip Clip;
        [Range(0f, 2f)] public float Volume = 1f;
        [Range(0f, 1f)] public float PitchJitter;
        public bool Spatial;
        public bool Loop;
    }

    public static class M7AudioIds
    {
        public static readonly M7AudioId[] Sfx =
        {
            M7AudioId.RifleShot, M7AudioId.PistolShot, M7AudioId.ShotgunShot, M7AudioId.KnifeSwing,
            M7AudioId.Reload, M7AudioId.Footstep, M7AudioId.HitBody, M7AudioId.Headshot,
            M7AudioId.Elimination, M7AudioId.RoundStart, M7AudioId.RoundEnd, M7AudioId.MatchEnd,
            M7AudioId.UiClick, M7AudioId.UiHover, M7AudioId.GrenadeExplosion, M7AudioId.FlashBang,
            M7AudioId.SmokeDeploy, M7AudioId.ZoneWarning
        };

        public static readonly M7AudioId[] Music = { M7AudioId.AmbArena, M7AudioId.MusicMenu, M7AudioId.MusicMatch };

        public static IEnumerable<M7AudioId> All
        {
            get
            {
                foreach (M7AudioId id in Sfx) yield return id;
                foreach (M7AudioId id in Music) yield return id;
            }
        }
    }
}
