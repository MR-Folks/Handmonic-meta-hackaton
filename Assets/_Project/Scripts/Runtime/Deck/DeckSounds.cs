using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The cassette deck's small mechanical sounds, made in code like Synth's chimes, so a hand with no haptics still
    /// hears every step: the click of picking a cassette up, the tick of the turntable passing a cassette, the soft
    /// swish of the door, the clunk of a cassette going in, the pop of an eject, a whoosh as one floats home and the
    /// whirr of the blank cassette waiting for a song. Each is made once and kept.
    /// </summary>
    public static class DeckSounds
    {
        static AudioClip click, tick, door, clunk, pop, whoosh, whirr;

        static int Rate => AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;

        /// <summary>A short plastic click: picking a cassette up, pressing a key.</summary>
        public static AudioClip Click => click ??= Make("deck click", 0.06f, 11, (t, noise) =>
            (noise * 0.6f * Mathf.Exp(-t * 260f) + Mathf.Sin(2f * Mathf.PI * 2600f * t) * 0.5f * Mathf.Exp(-t * 120f)
             + Mathf.Sin(2f * Mathf.PI * 900f * t) * 0.35f * Mathf.Exp(-t * 70f)) * 0.5f);

        /// <summary>A tiny ratchet tick as each cassette passes the front of the turntable.</summary>
        public static AudioClip Tick => tick ??= Make("deck tick", 0.03f, 5, (t, noise) =>
            (noise * 0.5f + Mathf.Sin(2f * Mathf.PI * 3800f * t) * 0.5f) * Mathf.Exp(-t * 320f) * 0.4f);

        /// <summary>The door swinging: a soft swish with a faint rising squeak.</summary>
        public static AudioClip Door => door ??= Make("deck door", 0.26f, 7, (t, noise) =>
        {
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.26f));
            float squeak = Mathf.Sin(2f * Mathf.PI * (700f + 900f * t) * t) * 0.08f;
            return (noise * 0.22f + squeak) * env * env * 0.6f;
        }, smooth: 0.25f);

        /// <summary>A cassette going in: a low thump, a knock, and the latch catching a moment later.</summary>
        public static AudioClip Clunk => clunk ??= Make("deck clunk", 0.25f, 3, (t, noise) =>
        {
            float thump = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 28f);
            float knock = Mathf.Sin(2f * Mathf.PI * 330f * t) * Mathf.Exp(-t * 48f) * 0.6f;
            float latch = t > 0.07f ? (noise * 0.5f + Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.4f) * Mathf.Exp(-(t - 0.07f) * 160f) : 0f;
            float hit = noise * Mathf.Exp(-t * 300f) * 0.6f;
            return (thump + knock + latch + hit) * 0.55f;
        });

        /// <summary>An eject: a springy pop sliding down in pitch.</summary>
        public static AudioClip Pop => pop ??= Make("deck pop", 0.2f, 13, (t, noise) =>
        {
            float f = Mathf.Lerp(1100f, 260f, Mathf.Clamp01(t / 0.12f));
            return (Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 22f) * 0.6f + noise * Mathf.Exp(-t * 200f) * 0.5f) * 0.5f;
        });

        /// <summary>A soft whoosh, for a cassette floating back to the turntable.</summary>
        public static AudioClip Whoosh => whoosh ??= Make("deck whoosh", 0.4f, 17, (t, noise) =>
        {
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.4f));
            return noise * env * env * env * 0.3f;
        }, smooth: 0.12f);

        /// <summary>The motor whirr of the blank cassette, while the radio waits for a new song.</summary>
        public static AudioClip Whirr => whirr ??= Make("deck whirr", 0.6f, 19, (t, noise) =>
        {
            float env = Mathf.Min(1f, t / 0.05f) * Mathf.Min(1f, (0.6f - t) / 0.15f);
            float motor = Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 280f * t) * 0.2f;
            return (motor * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 18f * t)) + noise * 0.08f) * env * 0.25f;
        });

        /// <summary>Renders a clip from a function of time and white noise, optionally smoothed (a gentle low-pass).</summary>
        static AudioClip Make(string name, float seconds, int seed, System.Func<float, float, float> sample, float smooth = 1f)
        {
            int rate = Rate, length = Mathf.Max(1, (int)(rate * seconds));
            var data = new float[length];
            var random = new System.Random(seed);
            float low = 0f;
            for (int i = 0; i < length; i++)
            {
                float white = (float)(random.NextDouble() * 2 - 1);
                low += (white - low) * smooth;
                data[i] = Mathf.Clamp(sample(i / (float)rate, low), -1f, 1f);
            }
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
