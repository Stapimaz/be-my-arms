using System.Collections;
using BeMyArms.M7;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BeMyArms.M7.Tests
{
    /// <summary>Runtime smoke proof of the M7 audio and VFX services.</summary>
    public class M7RuntimeTests
    {
        [UnityTest]
        public IEnumerator AudioService_PlaysAndLoopsRuntimeClips()
        {
            var go = new GameObject("m7_audio");
            var service = go.AddComponent<M7AudioService>();
            var library = ScriptableObject.CreateInstance<M7AudioLibrary>();
            var clip = AudioClip.Create("m7_test", 4410, 1, 44100, false);

            library.Clips.Add(new M7AudioClipEntry { Id = M7AudioId.UiClick, Clip = clip });
            library.Clips.Add(new M7AudioClipEntry { Id = M7AudioId.AmbArena, Clip = clip, Loop = true });
            service.Library = library;

            service.Play(M7AudioId.UiClick);
            service.PlayAt(M7AudioId.UiClick, Vector3.one);
            service.PlayMusic(M7AudioId.AmbArena);
            yield return null;
            service.StopMusic();

            Object.Destroy(go);
            Object.Destroy(library);
            Object.Destroy(clip);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator VfxService_SpawnsAndDespawnsRuntimeEffects()
        {
            var go = new GameObject("m7_vfx");
            var service = go.AddComponent<M7VfxService>();
            var library = ScriptableObject.CreateInstance<M7VfxLibrary>();
            var prefab = new GameObject("fx_muzzle");
            prefab.AddComponent<ParticleSystem>();

            library.Effects.Add(new M7VfxEntry { Id = M7VfxId.MuzzleFlash, Prefab = prefab, Lifetime = 0.1f });
            service.Library = library;

            GameObject spawned = service.Spawn(M7VfxId.MuzzleFlash, Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(spawned, "effect did not spawn");
            Assert.IsTrue(spawned.activeSelf);
            yield return new WaitForSeconds(0.25f);
            Assert.IsFalse(spawned.activeSelf, "effect should despawn after its lifetime");

            Object.Destroy(go);
            Object.Destroy(library);
            Object.Destroy(prefab);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
