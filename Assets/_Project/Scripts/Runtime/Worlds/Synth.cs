using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Small sounds made in code, so worlds can answer the player's touch: a plucked string and a bell-like chime.
    /// Notes are in A minor pentatonic, the key of the demo song.
    /// </summary>
    public static class Synth
    {
        /// <summary>A minor pentatonic from A3 up two octaves, in Hz.</summary>
        public static readonly float[] Pentatonic =
        {
            220f, 261.63f, 293.66f, 329.63f, 392f, 440f, 523.25f, 587.33f, 659.25f, 783.99f, 880f
        };

        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        static int Rate => AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;

        /// <summary>A plucked string (Karplus-Strong): bright attack, warm decay.</summary>
        public static AudioClip Pluck(float frequency)
        {
            string key = $"pluck {frequency:F1}";
            if (Cache.TryGetValue(key, out var clip)) return clip;

            int rate = Rate, length = (int)(rate * 1.8f);
            int period = Mathf.Max(2, Mathf.RoundToInt(rate / frequency));
            var ring = new float[period];
            var random = new System.Random(period);
            for (int i = 0; i < period; i++) ring[i] = (float)(random.NextDouble() * 2 - 1);

            var data = new float[length];
            int index = 0;
            for (int i = 0; i < length; i++)
            {
                int next = (index + 1) % period;
                float sample = ring[index];
                ring[index] = (ring[index] + ring[next]) * 0.4985f;
                index = next;
                float attack = Mathf.Min(1f, i / (rate * 0.002f));
                data[i] = sample * attack * 0.55f;
            }
            return Store(key, data, rate);
        }

        /// <summary>A soft bell: a sine with a few gentle overtones and a long fade.</summary>
        public static AudioClip Chime(float frequency)
        {
            string key = $"chime {frequency:F1}";
            if (Cache.TryGetValue(key, out var clip)) return clip;

            int rate = Rate, length = (int)(rate * 1.4f);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 3.2f);
                float s = Mathf.Sin(2f * Mathf.PI * frequency * t)
                          + 0.35f * Mathf.Sin(2f * Mathf.PI * frequency * 2.01f * t) * Mathf.Exp(-t * 4f)
                          + 0.15f * Mathf.Sin(2f * Mathf.PI * frequency * 3.97f * t) * Mathf.Exp(-t * 7f);
                data[i] = s * env * 0.32f;
            }
            return Store(key, data, rate);
        }

        /// <summary>
        /// A soft, warm note (like a kalimba tine) for touches that should blend with the song: a sine with a
        /// short-lived overtone and a gentle tail, nothing bright enough to cut across the mix.
        /// </summary>
        public static AudioClip Soft(float frequency)
        {
            string key = $"soft {frequency:F1}";
            if (Cache.TryGetValue(key, out var clip)) return clip;

            int rate = Rate, length = (int)(rate * 1.6f);
            var data = new float[length];
            float lowpass = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, t / 0.006f) * Mathf.Exp(-t * 2.8f);
                float s = Mathf.Sin(2f * Mathf.PI * frequency * t)
                          + 0.22f * Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * Mathf.Exp(-t * 9f)
                          + 0.06f * Mathf.Sin(2f * Mathf.PI * frequency * 5.4f * t) * Mathf.Exp(-t * 30f);
                lowpass += (s - lowpass) * 0.35f;
                data[i] = lowpass * env * 0.3f;
            }
            return Store(key, data, rate);
        }

        /// <summary>A neon tube's buzz: a short mains hum with a crackle, for a sign switching on.</summary>
        public static AudioClip Buzz()
        {
            const string key = "buzz";
            if (Cache.TryGetValue(key, out var clip)) return clip;

            int rate = Rate, length = (int)(rate * 0.35f);
            var data = new float[length];
            var random = new System.Random(3);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 7f);
                float hum = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 100f * t)) * 0.5f + Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.3f;
                float crackle = random.NextDouble() < 0.02 ? (float)(random.NextDouble() * 2 - 1) : 0f;
                data[i] = (hum * 0.25f + crackle * 0.6f) * env * 0.35f;
            }
            return Store(key, data, rate);
        }

        /// <summary>A spray can's hiss: a second of bright noise that loops seamlessly.</summary>
        public static AudioClip Hiss()
        {
            const string key = "hiss";
            if (Cache.TryGetValue(key, out var clip)) return clip;

            int rate = Rate, length = rate;
            var data = new float[length];
            var random = new System.Random(9);
            float low = 0f, prev = 0f;
            for (int i = 0; i < length; i++)
            {
                float white = (float)(random.NextDouble() * 2 - 1);
                // Take off the lowest rumble and the harshest top, leaving the "shhh".
                low += (white - low) * 0.35f;
                float high = low - prev * 0.96f;
                prev = low;
                data[i] = high * 0.35f;
            }
            return Store(key, data, rate);
        }

        static AudioClip Store(string key, float[] data, int rate)
        {
            var clip = AudioClip.Create(key, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }
    }

    /// <summary>A few 3D audio sources reused round-robin, so sounds come from where they happen.</summary>
    public sealed class AudioPool
    {
        readonly AudioSource[] sources;
        int next;

        public AudioPool(Transform parent, int size)
        {
            sources = new AudioSource[size];
            for (int i = 0; i < size; i++)
            {
                var go = new GameObject($"Voice {i}");
                go.transform.SetParent(parent, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.minDistance = 0.4f;
                s.maxDistance = 6f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.dopplerLevel = 0f;
                sources[i] = s;
            }
        }

        /// <summary>Plays a clip at a place, now or after <paramref name="delay"/> seconds (to land on the beat).</summary>
        public void Play(AudioClip clip, Vector3 position, float volume, float pitch = 1f, float delay = 0f)
        {
            if (!Application.isPlaying || clip == null) return;
            var s = sources[next];
            next = (next + 1) % sources.Length;
            s.transform.position = position;
            s.pitch = pitch;
            if (delay <= 0.005f)
            {
                s.PlayOneShot(clip, volume);
                return;
            }
            s.clip = clip;
            s.volume = volume;
            s.PlayScheduled(AudioSettings.dspTime + delay);
        }
    }
}
