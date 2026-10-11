using System.Collections.Generic;
using UnityEngine;

namespace Corehold
{
    // Plays sound effects on a small round-robin pool of voices, plus one looping music track.
    // minInterval throttles very frequent sounds (machine guns, hits) so a big fight does not turn into noise.
    public sealed class Sfx : MonoBehaviour
    {
        const int Voices = 14;
        const float MusicFade = 1.5f;

        static Sfx instance;
        readonly AudioSource[] voices = new AudioSource[Voices];
        readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
        AudioSource music;
        float musicTarget;
        int next;

        public static bool Muted { get; private set; }

        public static void Create(GameObject host)
        {
            Sounds.Create();
            instance = host.AddComponent<Sfx>();
            for (int i = 0; i < Voices; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                instance.voices[i] = s;
            }
            instance.music = host.AddComponent<AudioSource>();
            instance.music.loop = true;
            instance.music.playOnAwake = false;
            SetMuted(Progress.Muted);
        }

        public static void Play(AudioClip clip, float volume = 1f, float minInterval = 0f, float pitchJitter = 0.06f)
        {
            if (instance == null || clip == null || Muted) return;
            float now = Time.unscaledTime;
            if (minInterval > 0f && instance.lastPlayed.TryGetValue(clip, out float last) && now - last < minInterval) return;
            instance.lastPlayed[clip] = now;
            var source = instance.voices[instance.next];
            instance.next = (instance.next + 1) % Voices;
            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.Play();
        }

        // Switches the music loop; null fades it out.
        public static void Music(AudioClip clip, float volume = 0.4f)
        {
            if (instance == null) return;
            var m = instance.music;
            if (clip != null && m.clip != clip)
            {
                m.clip = clip;
                m.volume = 0f;
                m.Play();
            }
            instance.musicTarget = clip != null ? volume : 0f;
        }

        public static void SetMuted(bool muted)
        {
            Muted = muted;
            AudioListener.volume = muted ? 0f : 1f;
            if (Progress.Muted != muted) Progress.Muted = muted;
        }

        void Update()
        {
            if (music.clip == null) return;
            music.volume = Mathf.MoveTowards(music.volume, musicTarget, Time.unscaledDeltaTime / MusicFade);
            if (musicTarget <= 0f && music.volume <= 0f && music.isPlaying)
            {
                music.Stop();
                music.clip = null;
            }
        }
    }
}
