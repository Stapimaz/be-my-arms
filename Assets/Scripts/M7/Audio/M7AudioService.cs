using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.M7
{
    /// <summary>Pooled audio playback driven by the M7 audio library.</summary>
    public class M7AudioService : MonoBehaviour
    {
        public static M7AudioService Instance { get; private set; }

        public M7AudioLibrary Library;
        public int PoolSize = 16;
        public int MusicPoolSize = 2;

        AudioSource[] _pool;
        AudioSource[] _music;
        int _next;
        int _nextMusic;
        readonly Dictionary<M7AudioId, float> _lastPlay = new Dictionary<M7AudioId, float>();

        void Awake()
        {
            Instance = this;
            _pool = new AudioSource[Mathf.Max(1, PoolSize)];
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = gameObject.AddComponent<AudioSource>();
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

        public void Play(M7AudioId id) => PlayAt(id, null);

        public void PlayAt(M7AudioId id, Vector3 position) => PlayAt(id, (Vector3?)position);

        public void PlayAt(M7AudioId id, Vector3? position)
        {
            M7AudioClipEntry entry = Library != null ? Library.Get(id) : null;
            if (entry == null) return;

            AudioSource source = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            source.Stop();
            source.clip = entry.Clip;
            source.volume = Mathf.Clamp01(entry.Volume);
            source.pitch = 1f + Random.Range(-entry.PitchJitter, entry.PitchJitter);
            source.loop = entry.Loop;
            source.spatialBlend = entry.Spatial ? 1f : 0f;
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
        public void PlayMusic(M7AudioId id)
        {
            M7AudioClipEntry entry = Library != null ? Library.Get(id) : null;
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
        public bool TryPlayThrottled(M7AudioId id, float minInterval, Vector3? position = null)
        {
            float now = Time.unscaledTime;
            if (_lastPlay.TryGetValue(id, out float last) && now - last < minInterval) return false;
            _lastPlay[id] = now;
            PlayAt(id, position);
            return true;
        }
    }
}
