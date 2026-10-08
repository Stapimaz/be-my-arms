using System.IO;
using BeMyArms.Client;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.Client.EditorTools
{
    /// <summary>Builds the VfxLibrary asset from the generated effect prefabs.</summary>
    public static class VfxLibraryBuilder
    {
        public const string LibraryPath = "Assets/Art/Vfx/VfxLibrary.asset";

        public static VfxLibrary Build()
        {
            VfxLibrary library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            if (library == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                library = ScriptableObject.CreateInstance<VfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.Effects.Clear();
            foreach (VfxId id in VfxIds.All)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VfxPrefabBuilder.PrefabPath(id));
                float lifetime = 2f;
                if (prefab != null)
                {
                    var ps = prefab.GetComponent<ParticleSystem>();
                    if (ps != null) lifetime = ps.main.duration + ps.main.startLifetime.constantMax + 0.2f;
                }
                library.Effects.Add(new VfxEntry { Id = id, Prefab = prefab, Lifetime = lifetime, Scale = 1f });
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }
    }
}
