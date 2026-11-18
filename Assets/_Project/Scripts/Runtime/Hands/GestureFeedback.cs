using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>What a hand just did, for <see cref="GestureFeedback"/>.</summary>
    public enum GestureKind { Pinch, Release, Fist, ThumbTap, ThumbSwipe, Touch, MenuOpen, MenuClose, Done }

    /// <summary>The fingers of one hand, to light together. None means "the fingers the gesture uses".</summary>
    [System.Flags]
    public enum Fingers { None = 0, Thumb = 1, Index = 2, Middle = 4, Ring = 8, Little = 16, All = 31 }

    /// <summary>
    /// Feedback you can feel without haptics, for every gesture that does something. One call shows all of it: the
    /// fingers that made the gesture light up and fade over about a third of a second (HandSkin draws it on the hand),
    /// a small burst of sparks in the hand's colour leaves the point where they met, and a short, quiet sound plays from
    /// there, in the song's key where that makes sense:
    /// <code>GestureFeedback.Show(GestureKind.Pinch, left, pinchPoint);</code>
    /// <list type="bullet">
    /// <item>Pinch lights the thumb and index with a tick, Release with a softer tock; a Fist lights all five with a low
    /// thump; a ThumbTap or ThumbSwipe lights the thumb and the side of the index it ran along, with a click or a swish;
    /// a Touch lights the touching fingertip (the index unless you say which) with a light tick; MenuOpen and MenuClose
    /// light the whole hand with a chime going up or down; Done (something held long enough) lights the whole hand
    /// with a soft bell.</item>
    /// <item>Pass <c>sound: false</c> where something already sounds for the same moment (the menu's pop, a bubble's
    /// pop, a world's own sound), and <c>fingers</c> to light other fingers than the gesture's own.</item>
    /// <item><see cref="Light"/> lights fingers without a burst or a sound (the cassette deck, which has its own), and
    /// <see cref="Hold"/> keeps fingers glowing steadily while you call it every frame, for a hold building up or a
    /// lesson pointing at the fingers to use.</item>
    /// </list>
    /// Nothing here allocates once each sound has been made the first time. The worlds call this through
    /// World.Feedback, which stays quiet while the menu is open.
    /// </summary>
    public static class GestureFeedback
    {
        /// <summary>Seconds a lit finger takes to fade back after a gesture.</summary>
        public const float FadeSeconds = 0.35f;

        /// <summary>The most sparks one burst throws (the fewest is eight), and the most alive at once.</summary>
        public const int MaxBurst = 12, MaxSparks = 48;

        // A finger stays fully lit this long before it fades, so a quick gesture still reads.
        const float HoldTop = 0.06f;
        // A held glow fades this fast (per second) once nobody keeps it up.
        const float HoldRelease = 6f;
        // Seconds between two bursts in the same spot; more would turn into a fountain (MaxSparks caps the rest).
        const float BurstGap = 0.12f;

        // Per finger: left hand 0-4, right hand 5-9 (thumb to little).
        static readonly float[] peak = new float[10];
        static readonly float[] age = { 99f, 99f, 99f, 99f, 99f, 99f, 99f, 99f, 99f, 99f };
        static readonly float[] held = new float[10];
        static readonly bool[] fresh = new bool[10];

        static GameObject host;
        static ParticleSystem sparks;
        static AudioPool voices;
        static Material sparkMaterial;
        static int tickedFrame = -1;
        static float clock, lastBurst = -99f;

        // Sounds, made once each (the pitched ones once per note).
        static readonly AudioClip[] ticks = new AudioClip[128], tocks = new AudioClip[128], thumps = new AudioClip[128], bells = new AudioClip[128];
        static AudioClip click, swish;

        /// <summary>
        /// For the editor checks: how many times Show ran, bursts were thrown, sounds started and fingers were lit or held
        /// (Light and Hold, Show included), and the last gesture and burst.
        /// </summary>
        public static int Shows { get; private set; }
        public static int Bursts { get; private set; }
        public static int Sounds { get; private set; }
        public static int Lit { get; private set; }
        public static GestureKind LastKind { get; private set; }
        public static Vector3 LastBurstAt { get; private set; }

        /// <summary>
        /// The feedback for one gesture: the fingers light up, a few sparks burst from <paramref name="at"/> and a sound
        /// plays there. <paramref name="strength"/> (0-1) scales the light and the burst; <paramref name="fingers"/>
        /// lights other fingers than the gesture's own.
        /// </summary>
        public static void Show(GestureKind kind, bool left, Vector3 at, float strength = 1f, bool sound = true, Fingers fingers = Fingers.None)
        {
            strength = Mathf.Clamp01(strength);
            Shows++;
            LastKind = kind;
            Light(left, fingers == Fingers.None ? FingersFor(kind) : fingers, 0.55f + 0.45f * strength);
            Burst(at, strength);
            if (sound) Play(kind, at, strength);
        }

        /// <summary>The fingers a gesture uses: thumb and index for a pinch, a tap or a swipe; the index for a touch; all five otherwise.</summary>
        public static Fingers FingersFor(GestureKind kind) => kind switch
        {
            GestureKind.Pinch or GestureKind.Release or GestureKind.ThumbTap or GestureKind.ThumbSwipe => Fingers.Thumb | Fingers.Index,
            GestureKind.Touch => Fingers.Index,
            _ => Fingers.All
        };

        /// <summary>The fingers for one finger of HandRig.</summary>
        public static Fingers Of(HandRig.Finger finger) => (Fingers)(1 << (int)finger);

        /// <summary>Lights fingers for a moment (they fade over <see cref="FadeSeconds"/>), with no burst and no sound.</summary>
        public static void Light(bool left, Fingers fingers, float strength = 1f)
        {
            Lit++;
            int start = left ? 0 : 5;
            for (int f = 0; f < 5; f++)
            {
                if (((int)fingers & (1 << f)) == 0) continue;
                int i = start + f;
                // A new flash starts from the top unless a brighter one is still showing.
                if (strength >= Flash(i))
                {
                    peak[i] = strength;
                    age[i] = 0f;
                }
            }
        }

        /// <summary>
        /// Keeps fingers glowing at <paramref name="level"/> (0-1) while you call this every frame, for a hold that is
        /// building up or a lesson showing which fingers to use. Once the calls stop the glow fades within a moment.
        /// </summary>
        public static void Hold(bool left, Fingers fingers, float level)
        {
            Lit++;
            int start = left ? 0 : 5;
            level = Mathf.Clamp01(level);
            for (int f = 0; f < 5; f++)
            {
                if (((int)fingers & (1 << f)) == 0) continue;
                held[start + f] = level;
                fresh[start + f] = true;
            }
        }

        /// <summary>How brightly a finger is lit now, 0-1.</summary>
        public static float Glow(bool left, HandRig.Finger finger)
        {
            int i = (left ? 0 : 5) + (int)finger;
            return Mathf.Max(Flash(i), held[i]);
        }

        /// <summary>
        /// Moves the fades on by <paramref name="dt"/> seconds. HandSkin calls it once a frame (TickFrame); the editor
        /// checks call it directly.
        /// </summary>
        public static void Tick(float dt)
        {
            clock += dt;
            for (int i = 0; i < 10; i++)
            {
                age[i] += dt;
                if (fresh[i]) fresh[i] = false;
                else held[i] = Mathf.MoveTowards(held[i], 0f, dt * HoldRelease);
            }
        }

        /// <summary>Tick, at most once per rendered frame, however many hands ask.</summary>
        public static void TickFrame()
        {
            if (Time.frameCount == tickedFrame) return;
            tickedFrame = Time.frameCount;
            Tick(Time.deltaTime);
        }

        /// <summary>Puts every finger out at once (a world or a lesson starting afresh).</summary>
        public static void Clear()
        {
            for (int i = 0; i < 10; i++)
            {
                peak[i] = held[i] = 0f;
                age[i] = 99f;
                fresh[i] = false;
            }
        }

        /// <summary>The material for the sparks: the worlds hand over their glow material; without one, the Glow shader is used.</summary>
        public static void UseMaterial(Material glow)
        {
            if (glow == null || sparkMaterial == glow) return;
            sparkMaterial = glow;
            if (sparks != null) sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = glow;
        }

        /// <summary>Moves the sparks on while the editor is not playing (previews and screenshots).</summary>
        public static void SimulateSparks(float seconds)
        {
            if (sparks != null) sparks.Simulate(seconds, true, false);
        }

        /// <summary>A flash's level now: full for a moment, then easing out (slow, faster, slow) over FadeSeconds.</summary>
        static float Flash(int i)
        {
            float a = age[i];
            if (a >= HoldTop + FadeSeconds) return 0f;
            if (a <= HoldTop) return peak[i];
            float u = (a - HoldTop) / FadeSeconds;
            return peak[i] * (1f - u * u * (3f - 2f * u));
        }

        /// <summary>The sparks and voices, made the first time they are needed (again after the editor changes scene).</summary>
        static bool EnsureHost()
        {
            if (host != null) return true;
            host = new GameObject("Gesture Feedback");
            // While editing (the editor checks and previews, each in a scene of its own) it stays an ordinary object of
            // that scene, so it goes with it and no sparks linger into the next.
            if (Application.isPlaying) Object.DontDestroyOnLoad(host);
            if (sparkMaterial == null)
            {
                var shader = Shader.Find("MusicInYourHands/Glow");
                if (shader != null) sparkMaterial = new Material(shader) { name = "Gesture Sparks" };
            }
            sparks = Effects.MakeSparkSystem("Gesture Sparks", host.transform, sparkMaterial);
            var main = sparks.main;
            main.maxParticles = MaxSparks;
            main.gravityModifier = 0.08f;
            var noise = sparks.noise;
            noise.strength = 0.015f;
            voices = new AudioPool(host.transform, 4);
            return true;
        }

        /// <summary>Eight to twelve small sparks in the hand's colour, flying a few centimetres out from the contact point.</summary>
        static void Burst(Vector3 at, float strength)
        {
            // Never two at once in the same spot (both hands may burst together).
            bool repeat = clock - lastBurst < BurstGap && (at - LastBurstAt).sqrMagnitude < 0.05f * 0.05f;
            if (strength <= 0.01f || repeat || !EnsureHost()) return;
            lastBurst = clock;
            Bursts++;
            LastBurstAt = at;
            var theme = HandSkin.Theme;
            var color = Color.Lerp(theme.Glow, Color.white, 0.3f);
            int count = 8 + Mathf.RoundToInt(4f * strength);
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at + dir * 0.006f,
                    velocity = dir * Random.Range(0.12f, 0.3f) * (0.6f + 0.4f * strength),
                    startColor = Color.Lerp(color, theme.Edge, Random.value * 0.35f),
                    startSize = Random.Range(0.005f, 0.01f),
                    startLifetime = Random.Range(0.22f, 0.42f)
                }, 1);
            }
        }

        /// <summary>The gesture's sound from where it happened, quiet, fitted to the song where it has a pitch.</summary>
        static void Play(GestureKind kind, Vector3 at, float strength)
        {
            if (!EnsureHost()) return;
            Sounds++;
            var music = Song();
            float loud = 0.6f + 0.4f * strength;
            switch (kind)
            {
                case GestureKind.Pinch:
                    voices.Play(Tick(Musical.Pitch(music, 76) + 12), at, 0.16f * loud);
                    break;
                case GestureKind.Touch:
                    voices.Play(Tick(Musical.Pitch(music, 79) + 12), at, 0.11f * loud);
                    break;
                case GestureKind.Release:
                    voices.Play(Tock(Musical.Pitch(music, 69)), at, 0.12f * loud);
                    break;
                case GestureKind.Fist:
                    voices.Play(Thump(Musical.Pitch(music, 69) - 24), at, 0.4f * loud);
                    break;
                case GestureKind.ThumbTap:
                    voices.Play(Click(), at, 0.14f * loud);
                    break;
                case GestureKind.ThumbSwipe:
                    voices.Play(Swish(), at, 0.16f * loud);
                    break;
                case GestureKind.MenuOpen:
                case GestureKind.MenuClose:
                {
                    int low = Musical.Pitch(music, 72), high = Musical.Pitch(music, 79);
                    if (high <= low) high = low + 7;
                    bool up = kind == GestureKind.MenuOpen;
                    voices.Play(Bell(up ? low : high), at, 0.16f);
                    voices.Play(Bell(up ? high : low), at, 0.14f, 1f, 0.075f);
                    break;
                }
                default:
                    voices.Play(Bell(Musical.Pitch(music, 81)), at, 0.15f * loud);
                    break;
            }
        }

        /// <summary>The song now, as far as fitting a note to it goes (no allocation: MusicFrame is a struct).</summary>
        static MusicFrame Song()
        {
            var bus = MusicBus.Instance;
            return bus == null ? default : new MusicFrame
            {
                Notes = bus.Notes, SongTime = bus.SongTime, BeatAge = bus.TimeSinceBeat, BeatInterval = bus.BeatInterval
            };
        }

        static int Rate => AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A tick: 45 ms of a high, pure note with a soft attack, gone almost at once.</summary>
        static AudioClip Tick(int midi)
        {
            midi = Mathf.Clamp(midi, 0, 127);
            if (ticks[midi] != null) return ticks[midi];
            int rate = Rate, length = (int)(rate * 0.045f);
            var data = new float[length];
            float f = Hz(midi);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, t / 0.0015f) * Mathf.Exp(-t / 0.009f) * Fade(i, length);
                data[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * f * t)) * env * 0.5f;
            }
            return ticks[midi] = Make("tick", data);
        }

        /// <summary>A tock: rounder and lower than the tick, a little longer, softer at the start.</summary>
        static AudioClip Tock(int midi)
        {
            midi = Mathf.Clamp(midi, 0, 127);
            if (tocks[midi] != null) return tocks[midi];
            int rate = Rate, length = (int)(rate * 0.08f);
            var data = new float[length];
            float f = Hz(midi);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                // The pitch sags a little, like knocking on wood.
                float wobble = 1f - 0.08f * (1f - Mathf.Exp(-t / 0.02f));
                float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t / 0.018f) * Fade(i, length);
                data[i] = Mathf.Sin(2f * Mathf.PI * f * wobble * t) * env * 0.5f;
            }
            return tocks[midi] = Make("tock", data);
        }

        /// <summary>A thump: a low note dropping in pitch, felt more than heard, with a breath of noise on top.</summary>
        static AudioClip Thump(int midi)
        {
            midi = Mathf.Clamp(midi, 0, 127);
            if (thumps[midi] != null) return thumps[midi];
            int rate = Rate, length = (int)(rate * 0.16f);
            var data = new float[length];
            float f = Hz(midi);
            var random = new System.Random(midi);
            float phase = 0f, low = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                phase += 2f * Mathf.PI * f * (1f + 0.6f * Mathf.Exp(-t / 0.015f)) / rate;
                low += ((float)(random.NextDouble() * 2 - 1) - low) * 0.08f;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t / 0.045f) * Fade(i, length);
                data[i] = (Mathf.Sin(phase) * 0.8f + low * 0.5f * Mathf.Exp(-t / 0.01f)) * env * 0.7f;
            }
            return thumps[midi] = Make("thump", data);
        }

        /// <summary>A click: ten milliseconds of a bright, damped knock, like a fingernail on glass.</summary>
        static AudioClip Click()
        {
            if (click != null) return click;
            int rate = Rate, length = (int)(rate * 0.012f);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, t / 0.0004f) * Mathf.Exp(-t / 0.0022f) * Fade(i, length);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 2600f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 4100f * t)) * env * 0.45f;
            }
            return click = Make("click", data);
        }

        /// <summary>A swish: a fifth of a second of soft noise that brightens and fades, like a hand brushing past.</summary>
        static AudioClip Swish()
        {
            if (swish != null) return swish;
            int rate = Rate, length = (int)(rate * 0.2f);
            var data = new float[length];
            var random = new System.Random(4);
            float low = 0f, lower = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float u = t / 0.2f;
                // The filter opens towards the middle of the sweep, then closes again.
                float k = 0.05f + 0.35f * Mathf.Sin(Mathf.PI * u);
                low += ((float)(random.NextDouble() * 2 - 1) - low) * k;
                lower += (low - lower) * 0.1f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.7f)) * Fade(i, length);
                data[i] = (low - lower) * env * 0.9f;
            }
            return swish = Make("swish", data);
        }

        /// <summary>A small soft bell: a sine with a gentle octave, ringing for half a second.</summary>
        static AudioClip Bell(int midi)
        {
            midi = Mathf.Clamp(midi, 0, 127);
            if (bells[midi] != null) return bells[midi];
            int rate = Rate, length = (int)(rate * 0.5f);
            var data = new float[length];
            float f = Hz(midi);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t / 0.12f) * Fade(i, length);
                data[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * f * t) * Mathf.Exp(-t / 0.05f)) * env * 0.4f;
            }
            return bells[midi] = Make("bell", data);
        }

        /// <summary>A short fade over the last 3 ms, so every sound ends on silence, without a click.</summary>
        static float Fade(int i, int length)
        {
            int tail = Mathf.Max(1, Rate * 3 / 1000);
            return Mathf.Clamp01((length - 1 - i) / (float)tail);
        }
    }
}
