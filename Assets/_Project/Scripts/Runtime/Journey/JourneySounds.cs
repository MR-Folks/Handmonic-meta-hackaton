using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The sounds of Journey mode's changes of section: a soft, airy swell, like a breath of wind, that has no pitch so
    /// it fits any song, and when the song's key is known a gentle open chord in it (the key's root, its fifth and the
    /// octave, rolled softly upward), quiet under the music. The finale adds the third and a high chime.
    /// </summary>
    public static class JourneySounds
    {
        static AudioClip swell;

        /// <summary>The swell: two seconds of soft noise whose tone opens and closes, fading in and out without a click.</summary>
        public static AudioClip Swell()
        {
            if (swell != null) return swell;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int length = (int)(rate * 2f);
            var data = new float[length];
            var random = new System.Random(11);
            float low = 0f, lower = 0f, peak = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float white = (float)(random.NextDouble() * 2 - 1);
                // The tone brightens as the swell rises and darkens as it fades.
                float open = 0.015f + 0.07f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.6f));
                low += (white - low) * open;
                lower += (low - lower) * open;
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.55f));
                float fall = Mathf.Clamp01((2f - t) / 1.45f);
                data[i] = lower * rise * fall * fall;
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float scale = peak > 0f ? 0.3f / peak : 0f;
            for (int i = 0; i < length; i++) data[i] *= scale;
            swell = AudioClip.Create("journey swell", length, 1, rate, false);
            swell.SetData(data, 0);
            return swell;
        }

        /// <summary>
        /// Plays the change's sounds at <paramref name="at"/>: the swell, and the chord when <paramref name="sections"/>
        /// knows the key. <paramref name="finale"/> for the final chorus's bloom.
        /// </summary>
        public static void Play(AudioPool voices, Vector3 at, SongSections sections, bool finale, float volume)
        {
            if (voices == null || volume <= 0f) return;
            voices.Play(Swell(), at, volume * (finale ? 0.9f : 0.7f));
            if (sections == null || sections.KeyTonic < 0) return;
            // The key's root between A3 and G#4, where it sits softly under most songs.
            int root = 57 + ((sections.KeyTonic - 9) % 12 + 12) % 12;
            voices.Play(Synth.Soft(Hz(root)), at, volume * 0.55f, 1f, 0.05f);
            voices.Play(Synth.Soft(Hz(root + 7)), at, volume * 0.42f, 1f, 0.13f);
            voices.Play(Synth.Soft(Hz(root + 12)), at, volume * 0.32f, 1f, 0.21f);
            if (!finale) return;
            voices.Play(Synth.Soft(Hz(root + (sections.KeyMinor ? 15 : 16))), at, volume * 0.3f, 1f, 0.29f);
            voices.Play(Synth.Chime(Hz(root + 24)), at, volume * 0.22f, 1f, 0.4f);
        }

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }
}
