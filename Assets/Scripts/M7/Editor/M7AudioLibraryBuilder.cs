using System.IO;
using BeMyArms.M7;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>Builds the M7AudioLibrary asset from the generated WAV set.</summary>
    public static class M7AudioLibraryBuilder
    {
        public const string LibraryPath = "Assets/Art/Audio/M7AudioLibrary.asset";

        public static M7AudioLibrary Build()
        {
            M7AudioLibrary library = AssetDatabase.LoadAssetAtPath<M7AudioLibrary>(LibraryPath);
            if (library == null)
            {
                Directory.CreateDirectory(M7AudioImportSettings.AudioDir);
                library = ScriptableObject.CreateInstance<M7AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.Clips.Clear();
            foreach (M7AudioId id in M7AudioIds.All)
            {
                string clipName = ClipFileFor(id);
                string path = $"{M7AudioImportSettings.AudioDir}/{clipName}.wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                bool loop = clipName.StartsWith("amb_") || clipName.StartsWith("music_");
                library.Clips.Add(new M7AudioClipEntry
                {
                    Id = id,
                    Clip = clip,
                    Volume = 1f,
                    PitchJitter = loop ? 0f : 0.06f,
                    Spatial = !loop && id != M7AudioId.UiClick && id != M7AudioId.UiHover,
                    Loop = loop
                });
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        public static string ClipFileFor(M7AudioId id)
        {
            switch (id)
            {
                case M7AudioId.RifleShot: return "sfx_rifle_shot";
                case M7AudioId.PistolShot: return "sfx_pistol_shot";
                case M7AudioId.ShotgunShot: return "sfx_shotgun_shot";
                case M7AudioId.KnifeSwing: return "sfx_knife_swing";
                case M7AudioId.Reload: return "sfx_reload";
                case M7AudioId.Footstep: return "sfx_footstep";
                case M7AudioId.HitBody: return "sfx_hit_body";
                case M7AudioId.Headshot: return "sfx_headshot";
                case M7AudioId.Elimination: return "sfx_elimination";
                case M7AudioId.RoundStart: return "sfx_round_start";
                case M7AudioId.RoundEnd: return "sfx_round_end";
                case M7AudioId.MatchEnd: return "sfx_match_end";
                case M7AudioId.UiClick: return "sfx_ui_click";
                case M7AudioId.UiHover: return "sfx_ui_hover";
                case M7AudioId.GrenadeExplosion: return "sfx_grenade_explosion";
                case M7AudioId.FlashBang: return "sfx_flash_bang";
                case M7AudioId.SmokeDeploy: return "sfx_smoke_deploy";
                case M7AudioId.ZoneWarning: return "sfx_zone_warning";
                case M7AudioId.AmbArena: return "amb_arena_loop";
                case M7AudioId.MusicMenu: return "music_menu_loop";
                case M7AudioId.MusicMatch: return "music_match_loop";
                default: return id.ToString().ToLowerInvariant();
            }
        }
    }
}
