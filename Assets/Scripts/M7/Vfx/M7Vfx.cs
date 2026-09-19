using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.M7
{
    public enum M7VfxId
    {
        MuzzleFlash,
        Tracer,
        ImpactFlesh,
        ImpactWorld,
        FootstepDust,
        ReloadSpark,
        GrenadeExplosion,
        SmokeCloud,
        FlashBurst,
        EliminationBurst,
        ZoneEdge
    }

    [Serializable]
    public class M7VfxEntry
    {
        public M7VfxId Id;
        public GameObject Prefab;
        public float Lifetime = 2f;
        [Range(0.1f, 3f)] public float Scale = 1f;
    }

    public static class M7VfxIds
    {
        public static readonly M7VfxId[] All =
        {
            M7VfxId.MuzzleFlash, M7VfxId.Tracer, M7VfxId.ImpactFlesh, M7VfxId.ImpactWorld,
            M7VfxId.FootstepDust, M7VfxId.ReloadSpark, M7VfxId.GrenadeExplosion, M7VfxId.SmokeCloud,
            M7VfxId.FlashBurst, M7VfxId.EliminationBurst, M7VfxId.ZoneEdge
        };
    }
}
