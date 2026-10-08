using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.Client
{
    /// <summary>Pooled audio playback driven by the Client audio library.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7AudioService")]
    public class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }

        public AudioLibrary Library;
        public int PoolSize = 16;
        public int MusicPoolSize = 2;

        AudioSource[] _pool;
        AudioSource[] _music;
        int _next;
        int _nextMusic;
        readonly Dictionary<AudioId, float> _lastPlay = new Dictionary<AudioId, float>();

        void Awake()
        {
            Instance = this;
            _pool = new AudioSource[Mathf.Max(1, PoolSize)];
            for (int i = 0; i < _pool.Length; i++)
            {
                var voice = new GameObject("SfxVoice_" + i);
                voice.transform.SetParent(transform, false);
                _pool[i] = voice.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
            }
            _music = new AudioSource[Mathf.Max(1, MusicPoolSize)];
            for (int i = 0; i < _music.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                _music[i] = source;
            }
        }

        public void Play(AudioId id) => PlayAt(id, null);

        public void PlayAt(AudioId id, Vector3 position) => PlayAt(id, (Vector3?)position);

        public void PlayAt(AudioId id, Vector3? position)
        {
            AudioClipEntry entry = Library != null ? Library.Get(id) : null;
            if (entry == null || entry.Clip == null || Application.isBatchMode) return;

            AudioSource source = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            source.Stop();
            source.clip = entry.Clip;
            source.volume = Mathf.Clamp01(entry.Volume);
            source.pitch = 1f + Random.Range(-entry.PitchJitter, entry.PitchJitter);
            source.loop = entry.Loop;
            source.spatialBlend = entry.Spatial && position.HasValue ? 1f : 0f;
            source.dopplerLevel = 0f;
            if (position.HasValue && entry.Spatial)
            {
                source.transform.position = position.Value;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 2f;
                source.maxDistance = 60f;
            }
            source.Play();
        }

        /// <summary>Plays a looping music/ambience bed, replacing the current one.</summary>
        public void PlayMusic(AudioId id)
        {
            if (Application.isBatchMode) return;
            AudioClipEntry entry = Library != null ? Library.Get(id) : null;
            if (entry == null) return;
            AudioSource source = _music[_nextMusic];
            if (source.clip == entry.Clip && source.isPlaying) return;
            _nextMusic = (_nextMusic + 1) % _music.Length;
            source.Stop();
            source.clip = entry.Clip;
            source.volume = Mathf.Clamp01(entry.Volume);
            source.loop = true;
            source.Play();
        }

        public void StopMusic()
        {
            for (int i = 0; i < _music.Length; i++) _music[i].Stop();
        }

        /// <summary>Rate-limits repeated events such as footsteps.</summary>
        public bool TryPlayThrottled(AudioId id, float minInterval, Vector3? position = null)
        {
            float now = Time.unscaledTime;
            if (_lastPlay.TryGetValue(id, out float last) && now - last < minInterval) return false;
            _lastPlay[id] = now;
            PlayAt(id, position);
            return true;
        }
    }
}
