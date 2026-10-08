using System.IO;
using BeMyArms.Client;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.Client.EditorTools
{
    /// <summary>Builds the AudioLibrary asset from the generated WAV set.</summary>
    public static class AudioLibraryBuilder
    {
        public const string LibraryPath = "Assets/Art/Audio/AudioLibrary.asset";

        public static AudioLibrary Build()
        {
            AudioLibrary library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null)
            {
                Directory.CreateDirectory(AudioImportSettings.AudioDir);
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.Clips.Clear();
            foreach (AudioId id in AudioIds.All)
            {
                string clipName = ClipFileFor(id);
                string path = $"{AudioImportSettings.AudioDir}/{clipName}.wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                bool loop = clipName.StartsWith("amb_") || clipName.StartsWith("music_");
                library.Clips.Add(new AudioClipEntry
                {
                    Id = id,
                    Clip = clip,
                    Volume = id == AudioId.Footstep ? .35f : id == AudioId.Gear ? .18f : id == AudioId.RifleShot ? .75f : .55f,
                    PitchJitter = loop ? 0f : 0.06f,
                    Spatial = !loop && id != AudioId.UiClick && id != AudioId.UiHover,
                    Loop = loop
                });
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        public static string ClipFileFor(AudioId id)
        {
            switch (id)
            {
                case AudioId.MagazineOut: return "sfx_magazine_out";
                case AudioId.MagazineIn: return "sfx_magazine_in";
                case AudioId.Bolt: return "sfx_bolt";
                case AudioId.Jump: return "sfx_jump";
                case AudioId.Land: return "sfx_land";
                case AudioId.Slide: return "sfx_slide";
                case AudioId.Vault: return "sfx_vault";
                case AudioId.Kick: return "sfx_kick";
                case AudioId.Gear: return "sfx_gear";
                case AudioId.RifleShot: return "sfx_rifle_shot";
                case AudioId.PistolShot: return "sfx_pistol_shot";
                case AudioId.ShotgunShot: return "sfx_shotgun_shot";
                case AudioId.KnifeSwing: return "sfx_knife_swing";
                case AudioId.Reload: return "sfx_reload";
                case AudioId.Footstep: return "sfx_footstep";
                case AudioId.HitBody: return "sfx_hit_body";
                case AudioId.Headshot: return "sfx_headshot";
                case AudioId.Elimination: return "sfx_elimination";
                case AudioId.RoundStart: return "sfx_round_start";
                case AudioId.RoundEnd: return "sfx_round_end";
                case AudioId.MatchEnd: return "sfx_match_end";
                case AudioId.UiClick: return "sfx_ui_click";
                case AudioId.UiHover: return "sfx_ui_hover";
                case AudioId.GrenadeExplosion: return "sfx_grenade_explosion";
                case AudioId.FlashBang: return "sfx_flash_bang";
                case AudioId.SmokeDeploy: return "sfx_smoke_deploy";
                case AudioId.ZoneWarning: return "sfx_zone_warning";
                case AudioId.AmbArena: return "amb_arena_loop";
                case AudioId.MusicMenu: return "music_menu_loop";
                case AudioId.MusicMatch: return "music_match_loop";
                default: return id.ToString().ToLowerInvariant();
            }
        }
    }
}
