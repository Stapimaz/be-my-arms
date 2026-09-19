using System.IO;
using BeMyArms.M7;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>Builds the M7VfxLibrary asset from the generated effect prefabs.</summary>
    public static class M7VfxLibraryBuilder
    {
        public const string LibraryPath = "Assets/Art/Vfx/M7VfxLibrary.asset";

        public static M7VfxLibrary Build()
        {
            M7VfxLibrary library = AssetDatabase.LoadAssetAtPath<M7VfxLibrary>(LibraryPath);
            if (library == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                library = ScriptableObject.CreateInstance<M7VfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.Effects.Clear();
            foreach (M7VfxId id in M7VfxIds.All)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(M7VfxPrefabBuilder.PrefabPath(id));
                float lifetime = 2f;
                if (prefab != null)
                {
                    var ps = prefab.GetComponent<ParticleSystem>();
                    if (ps != null) lifetime = ps.main.duration + ps.main.startLifetime.constantMax + 0.2f;
                }
                library.Effects.Add(new M7VfxEntry { Id = id, Prefab = prefab, Lifetime = lifetime, Scale = 1f });
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }
    }
}
