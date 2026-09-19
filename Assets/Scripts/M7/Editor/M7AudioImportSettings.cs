using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>
    /// Applies production audio import settings: short SFX are decompressed on load and mono; long
    /// ambience/music beds stream and loop.
    /// </summary>
    public static class M7AudioImportSettings
    {
        public const string AudioDir = "Assets/Art/Audio";

        public static int ApplyAll()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;

                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                bool loop = file.StartsWith("amb_") || file.StartsWith("music_");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                bool dirty = false;

                if (importer.forceToMono != true) { importer.forceToMono = true; dirty = true; }

                AudioClipLoadType loadType = loop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (settings.loadType != loadType) { settings.loadType = loadType; dirty = true; }
                if (settings.preloadAudioData != !loop) { settings.preloadAudioData = !loop; dirty = true; }
                if (settings.compressionFormat != AudioCompressionFormat.Vorbis) { settings.compressionFormat = AudioCompressionFormat.Vorbis; dirty = true; }
                float quality = loop ? 0.6f : 0.8f;
                if (!Mathf.Approximately(settings.quality, quality)) { settings.quality = quality; dirty = true; }

                if (dirty)
                {
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            return changed;
        }

        public static IEnumerable<string> ClipNames()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
                yield return System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
