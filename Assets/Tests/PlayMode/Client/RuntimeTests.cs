using System.Collections;
using BeMyArms.Client;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BeMyArms.Client.Tests
{
    /// <summary>Runtime smoke proof of the Client audio and VFX services.</summary>
    public class RuntimeTests
    {
        [UnityTest]
        public IEnumerator AudioService_PlaysAndLoopsRuntimeClips()
        {
            var go = new GameObject("m7_audio");
            var service = go.AddComponent<AudioService>();
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            var clip = AudioClip.Create("m7_test", 4410, 1, 44100, false);

            library.Clips.Add(new AudioClipEntry { Id = AudioId.UiClick, Clip = clip });
            library.Clips.Add(new AudioClipEntry { Id = AudioId.AmbArena, Clip = clip, Loop = true });
            service.Library = library;

            service.Play(AudioId.UiClick);
            service.PlayAt(AudioId.UiClick, Vector3.one);
            service.PlayMusic(AudioId.AmbArena);
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
            var service = go.AddComponent<VfxService>();
            var library = ScriptableObject.CreateInstance<VfxLibrary>();
            var prefab = new GameObject("fx_muzzle");
            prefab.AddComponent<ParticleSystem>();

            library.Effects.Add(new VfxEntry { Id = VfxId.MuzzleFlash, Prefab = prefab, Lifetime = 0.1f });
            service.Library = library;

            GameObject spawned = service.Spawn(VfxId.MuzzleFlash, Vector3.zero, Quaternion.identity);
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
