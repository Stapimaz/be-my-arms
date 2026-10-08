using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.Client
{
    public enum VfxId
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
    public class VfxEntry
    {
        public VfxId Id;
        public GameObject Prefab;
        public float Lifetime = 2f;
        [Range(0.1f, 3f)] public float Scale = 1f;
    }

    public static class VfxIds
    {
        public static readonly VfxId[] All =
        {
            VfxId.MuzzleFlash, VfxId.Tracer, VfxId.ImpactFlesh, VfxId.ImpactWorld,
            VfxId.FootstepDust, VfxId.ReloadSpark, VfxId.GrenadeExplosion, VfxId.SmokeCloud,
            VfxId.FlashBurst, VfxId.EliminationBurst, VfxId.ZoneEdge
        };
    }
}
