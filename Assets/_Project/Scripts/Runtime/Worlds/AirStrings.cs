using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Air strings world: a harp of light. Eight glowing strings stand in a gentle arc in front of you, low notes
    /// on the left and high on the right. Brush a finger across a string and it rings; sweep your hand across them all
    /// and you strum a glissando. Whatever you play fits the song: each string retunes itself to the notes being sung
    /// right now, and its sound lands on the beat. The string that carries the sung note shimmers on its own, so you
    /// can follow the melody if you like, but nothing is scored and nothing is wrong. Plucks ring along the string,
    /// throw off sparks and a few notes of light that float up, and give your fingertip a little shiver.
    ///
    /// Round the harp stands a drum kit: nine neon taiko drums with their skins tilted towards you (small high ones
    /// beside the harp, bigger and deeper ones further round, the biggest behind you) and four brass cymbals on stands
    /// above them, as on a drum set: a hi-hat on the left, a ride on the right and a crash and a splash further round.
    /// Strike a skin or a cymbal with your palm or fingers and it sounds from where it stands, louder the harder you
    /// hit, and rings on for a while: the drums boom and the cymbals shimmer. A drum's skin ripples out from where you
    /// hit it and a ring of light spreads across the floor beneath it; a cymbal swings and its shine runs out across
    /// the metal. Between strikes the big drums breathe with the bass and the small ones and the cymbals with the
    /// higher sounds.
    /// </summary>
    public class AirStrings : World
    {
        public override string DisplayName => "Air strings";

        public override Color Color => new Color(0.49f, 0.95f, 0.79f);

        public override int MenuOrder => 30;

        public override int MenuIcon => 3;

        public override string Hint => "Brush the strings, or play the drums and cymbals around you";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.06f, 0.18f, 0.16f, 0.55f),
            Edge = new Color(0.49f, 0.95f, 0.79f),
            Glow = new Color(1f, 0.83f, 0.42f),
            Pattern = HandTheme.Effect.Electric,
            PatternColor = new Color(0.7f, 1f, 0.9f)
        };

        [Header("Materials")]
        [SerializeField] Material lineMaterial;
        [SerializeField] Material ringMaterial;
        [WorldMaterial("AirStringsDrum")]
        [SerializeField] Material drumMaterial;

        [Header("Harp")]
        [SerializeField] int stringCount = 8;
        [Tooltip("How far in front of the eyes the middle string stands (m).")]
        [SerializeField] float distance = 0.4f;
        [Tooltip("Width of the arc from the first string to the last (m).")]
        [SerializeField] float width = 0.62f;
        [Tooltip("Bottom and top of the strings relative to the eyes (m).")]
        [SerializeField] Vector2 heights = new Vector2(-0.68f, -0.12f);
        [Tooltip("How much the arc curves towards you at its ends (m).")]
        [SerializeField] float curve = 0.1f;
        [SerializeField] int points = 48;

        [Header("Touch")]
        [Tooltip("How close (m) to a string's line a fingertip must pass for it to pluck.")]
        [SerializeField] float reach = 0.035f;

        [Header("Drums and cymbals")]
        [Tooltip("How fast (m/s) a palm or fingertip must come down onto a skin or cymbal for it to sound.")]
        [SerializeField] float strikeSpeed = 0.35f;
        [Tooltip("How fast (m/s) a strike must be for the loudest sound.")]
        [SerializeField] float fullStrikeSpeed = 2f;
        [Tooltip("Seconds before the same hand can strike the same drum or cymbal again.")]
        [SerializeField] float strikeRest = 0.12f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int WakeId = Shader.PropertyToID("_Wake");
        static readonly int HitId = Shader.PropertyToID("_Hit");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly Color[] Colors =
        {
            Effects.Pink, new Color(1f, 0.52f, 0.5f), Effects.Orange, Effects.Gold, Effects.Mint, new Color(0.45f, 0.85f, 0.95f), Effects.Blue, Effects.Violet
        };

        sealed class HarpString
        {
            public LineStrip Line;
            public Renderer[] Ends;
            public Renderer Ripple;
            public Vector3 Bottom, Top, Normal;
            public float Ring;
            public float RingAge = 9f;
            public float PluckHeight = 0.5f;
            public float Glow;
            public float Hum;
            public float Cooldown;
            public float RippleAge = -1f;
            public int Midi;
        }

        enum Kind { Drum, HiHat, Ride, Crash, Splash }

        /// <summary>One piece of the kit, a drum or a cymbal: where it stands round you, its size, and its sound.</summary>
        readonly struct PieceSpec
        {
            public readonly Kind Kind;
            /// <summary>Degrees round from straight ahead, positive to the right.</summary>
            public readonly float Angle;
            /// <summary>How far out from below your eyes its centre is (m).</summary>
            public readonly float Distance;
            /// <summary>Height of its centre (a drum's skin, a cymbal's bell) relative to your eyes (m).</summary>
            public readonly float Height;
            /// <summary>Its radius, and for a drum the depth of its barrel (m).</summary>
            public readonly float Radius, Depth;
            /// <summary>How far it leans towards you from level (degrees).</summary>
            public readonly float Tilt;
            /// <summary>A drum's pitch (Hz), and how fast the sound dies away (per second).</summary>
            public readonly float Pitch, Decay;
            public readonly Color Color;

            public PieceSpec(Kind kind, float angle, float distance, float height, float radius, float depth, float tilt, float pitch, float decay, Color color)
            {
                Kind = kind;
                Angle = angle;
                Distance = distance;
                Height = height;
                Radius = radius;
                Depth = depth;
                Tilt = tilt;
                Pitch = pitch;
                Decay = decay;
                Color = color;
            }

            public bool IsCymbal => Kind != Kind.Drum;
        }

        // The front is left to the harp. The drums stand in a ring at hand height, the cymbals above the gaps between
        // them, higher up, as on a drum set.
        static readonly PieceSpec[] Kit =
        {
            new PieceSpec(Kind.Drum, 180f, 0.8f, -0.58f, 0.2f, 0.36f, 42f, 55f, 1.4f, new Color(1f, 0.3f, 0.68f)),
            new PieceSpec(Kind.Drum, 150f, 0.66f, -0.6f, 0.16f, 0.3f, 30f, 69f, 1.8f, new Color(0.95f, 0.36f, 0.86f)),
            new PieceSpec(Kind.Drum, -150f, 0.66f, -0.6f, 0.16f, 0.3f, 30f, 65f, 1.8f, new Color(0.56f, 0.5f, 1f)),
            new PieceSpec(Kind.Drum, 122f, 0.6f, -0.55f, 0.145f, 0.26f, 26f, 87f, 2.1f, new Color(1f, 0.28f, 0.52f)),
            new PieceSpec(Kind.Drum, -122f, 0.6f, -0.55f, 0.145f, 0.26f, 26f, 78f, 2.1f, new Color(0.62f, 0.42f, 1f)),
            new PieceSpec(Kind.Drum, 94f, 0.53f, -0.45f, 0.11f, 0.19f, 30f, 131f, 2.7f, new Color(1f, 0.34f, 0.64f)),
            new PieceSpec(Kind.Drum, -94f, 0.53f, -0.45f, 0.11f, 0.19f, 30f, 117f, 2.7f, new Color(0.72f, 0.45f, 1f)),
            new PieceSpec(Kind.Drum, 68f, 0.48f, -0.36f, 0.085f, 0.13f, 34f, 196f, 3.4f, new Color(1f, 0.44f, 0.8f)),
            new PieceSpec(Kind.Drum, -68f, 0.48f, -0.36f, 0.085f, 0.13f, 34f, 175f, 3.4f, new Color(0.86f, 0.46f, 1f)),
            new PieceSpec(Kind.HiHat, -81f, 0.47f, -0.19f, 0.12f, 0f, 8f, 0f, 7f, new Color(1f, 0.8f, 0.42f)),
            new PieceSpec(Kind.Ride, 81f, 0.5f, -0.12f, 0.17f, 0f, 14f, 0f, 0.9f, new Color(1f, 0.72f, 0.38f)),
            new PieceSpec(Kind.Crash, -108f, 0.6f, -0.02f, 0.17f, 0f, 24f, 0f, 1.1f, new Color(1f, 0.86f, 0.5f)),
            new PieceSpec(Kind.Splash, 108f, 0.6f, -0.03f, 0.13f, 0f, 24f, 0f, 2.4f, new Color(1f, 0.76f, 0.52f))
        };

        sealed class Piece
        {
            public PieceSpec Spec;
            public Transform Root;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public AudioSource Voice;
            public AudioClip Sound;
            public Renderer Wave;
            public Vector3 Centre, Normal, Foot;
            /// <summary>Level, from you out towards it.</summary>
            public Vector3 Outward;
            public Quaternion Rest;
            /// <summary>False when a scanned wall leaves no room for it.</summary>
            public bool Placed;
            public float Glow, Pulse;
            /// <summary>The last strike: where on it (-1..1), seconds since, and how hard.</summary>
            public Vector4 Hit = new Vector4(0f, 0f, 9f, 0f);
            public float WaveAge = -1f, WaveStrength;
            /// <summary>A struck cymbal's swing: seconds since, how far (degrees) and about which axis.</summary>
            public float SwingAge = -1f, Swing;
            public Vector3 SwingAxis = Vector3.right;
            public readonly float[] Wait = new float[2];
            public readonly bool[] Armed = { true, true };
        }

        // Each hand strikes with its palm and its five fingertips.
        const int HitPoints = 6;
        // Segments round a drum or a cymbal.
        const int Around = 28;

        HarpString[] strings;
        Vector3[] path;
        AudioPool voices;
        ParticleSystem sparks, notes;
        MaterialPropertyBlock block;
        readonly float[,] lastSide = new float[10, 8];
        readonly bool[,] hadSide = new bool[10, 8];
        readonly float[] shiver = new float[2];
        float beatTime = -10f;
        MusicFrame frame;

        Piece[] pieces;
        // The kit's sounds are made once per run, on a worker thread started as the app loads (see MakeSoundsEarly).
        static System.Threading.Tasks.Task<float[][]> soundWork;
        static int soundRate;
        float[][] soundData;
        int soundsMade;
        readonly Vector3[] handPoints = new Vector3[HitPoints];
        readonly float[,] lastHeight = new float[Kit.Length, HitPoints * 2];
        readonly bool[,] hadHeight = new bool[Kit.Length, HitPoints * 2];
        readonly List<Vector3> meshVertices = new List<Vector3>();
        readonly List<Vector3> meshNormals = new List<Vector3>();
        readonly List<Vector2> meshUvs = new List<Vector2>();
        readonly List<Vector2> meshParts = new List<Vector2>();
        readonly List<int> meshTriangles = new List<int>();
        static readonly Quaternion LyingFlat = Quaternion.Euler(90f, 0f, 0f);

        protected override int SungWordCount => 4;

        protected override float SungWordHeight => 0.032f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            path = new Vector3[points];
            strings = new HarpString[stringCount];
            for (int i = 0; i < stringCount; i++)
            {
                strings[i] = new HarpString
                {
                    Line = new LineStrip($"String {i}", transform, lineMaterial, points),
                    Ends = new[] { Effects.MakeQuad($"Foot {i}", transform, glowMaterial), Effects.MakeQuad($"Head {i}", transform, glowMaterial) },
                    Ripple = Effects.MakeQuad($"Pluck Ring {i}", transform, ringMaterial)
                };
                strings[i].Ripple.gameObject.SetActive(false);
            }
            voices = new AudioPool(transform, 8);
            sparks = Effects.MakeSparkSystem("Sparks", transform, glowMaterial);
            notes = Effects.MakeSparkSystem("Notes", transform, glowMaterial);
            var main = notes.main;
            main.gravityModifier = -0.02f;
            BuildKit();
        }

        protected override void OnEnter()
        {
            System.Array.Clear(hadSide, 0, hadSide.Length);
            System.Array.Clear(hadHeight, 0, hadHeight.Length);
            // The kit stands ready from the start; it is shown once it has been placed round you.
            foreach (var p in pieces)
            {
                p.Glow = p.Pulse = 0f;
                p.WaveAge = p.SwingAge = -1f;
                p.Armed[0] = p.Armed[1] = true;
                p.Root.gameObject.SetActive(false);
                p.Wave.gameObject.SetActive(false);
            }
        }

        /// <summary>Stands the strings in an arc in front of where you look, curving towards you at the ends.</summary>
        protected override void OnPlace(Camera view)
        {
            var forward = Effects.FlatForward(view);
            var side = Vector3.Cross(Vector3.up, forward).normalized;
            var eye = view.transform.position;
            for (int i = 0; i < stringCount; i++)
            {
                float x = stringCount > 1 ? i / (float)(stringCount - 1) * 2f - 1f : 0f;
                var foot = eye + forward * (distance - curve * x * x) + side * (x * width * 0.5f);
                var s = strings[i];
                s.Bottom = foot + Vector3.up * heights.x;
                s.Top = foot + Vector3.up * heights.y;
                // The plane a finger crosses to pluck: facing along the arc.
                var tangent = side + forward * (2f * curve * x / Mathf.Max(width * 0.5f, 0.01f));
                s.Normal = tangent.normalized;
            }
            PlaceKit(view);
        }

        /// <summary>Each beat sends a pulse up the strings, one after the other.</summary>
        protected override void OnBeat(MusicFrame music) => beatTime = Now;

        /// <summary>
        /// Each sung word appears beside the string that hums its note (or the next string along) and glides up it,
        /// glowing with the string's light.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            int pick = Sung.Index % stringCount;
            for (int i = 0; i < stringCount; i++)
                if (strings[i].Hum > 0.3f && strings[i].Hum >= strings[pick].Hum) pick = i;
            var w = SungWords.Add(word, Color.Lerp(Colors[pick % Colors.Length], Color.white, 0.3f), 2.4f);
            w.Slot = pick;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            frame = music;
            float t = Now;
            float pulseAge = t - beatTime;

            Retune(music);
            if (rig != null)
            {
                Strum(rig, dt);
                Strike(rig, dt);
            }
            FinishSounds();
            DrawKit(music, dt);

            for (int i = 0; i < stringCount; i++)
            {
                var s = strings[i];
                s.Cooldown -= dt;
                s.RingAge += dt;
                s.Ring *= Mathf.Exp(-dt * 2.2f);
                s.Glow = Mathf.MoveTowards(s.Glow, 0f, dt * 1.4f);
                float band = music.Band(1 + i * 5 / Mathf.Max(stringCount, 1));
                DrawString(s, i, band, t, pulseAge, view);
                DrawRipple(s, view, dt);
            }

            // A plucked fingertip shivers for a moment.
            for (int side = 0; side < 2; side++)
            {
                shiver[side] = Mathf.MoveTowards(shiver[side], 0f, dt * 6f);
                var hand = rig != null ? rig.Get(side == 0) : null;
                if (hand != null) Effects.OffsetHand(hand, Random.insideUnitSphere * (0.0025f * shiver[side]));
            }

            // The sung words glide up beside their strings.
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                var s = strings[w.Slot];
                float up = Mathf.Lerp(0.6f, 0.85f, w.Age / w.Life);
                var side = Vector3.Cross(Vector3.up, (s.Bottom - eye).normalized).normalized;
                var along = Vector3.Lerp(s.Bottom, s.Top, up);
                w.Position = along + side * (0.02f + SungWords.Height(w) * 0.2f + w.Width * SungWords.Height(w) * 0.5f) + (eye - along).normalized * 0.02f;
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.2f + s.Hum * 0.4f + s.Glow * 0.5f;
            }
        }

        /// <summary>
        /// Each string's note is fitted to the music right now (low strings low, high strings high), and the string
        /// whose note is being sung hums softly on its own.
        /// </summary>
        void Retune(MusicFrame music)
        {
            int sung = -1;
            // Notes made from the lyrics don't carry the sung pitch, so no string hums along with them.
            if (music.Notes != null && !music.Notes.IsFromLyrics)
            {
                foreach (var note in music.Notes.Notes)
                {
                    if (note.Time > music.SongTime) break;
                    if (music.SongTime < note.Time + note.Length) sung = note.Midi;
                }
            }
            for (int i = 0; i < stringCount; i++)
            {
                var s = strings[i];
                s.Midi = Musical.Pitch(music, 59 + i * 3);
                bool carries = sung >= 0 && ((s.Midi - sung) % 12 + 12) % 12 == 0;
                s.Hum = Mathf.MoveTowards(s.Hum, carries ? 1f : 0f, Time.deltaTime * 4f);
            }
        }

        /// <summary>A fingertip passing through a string's plane, close to the string, plucks it.</summary>
        void Strum(HandRig rig, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                for (int f = 0; f < 5; f++)
                {
                    int finger = side * 5 + f;
                    // While the menu is open the fingers are choosing there (its bubbles hang just behind the strings),
                    // not playing; once it closes, a finger starts afresh on whichever side of each string it is.
                    if (!hand.IsTracked || MenuOpen)
                    {
                        for (int i = 0; i < stringCount; i++) hadSide[finger, i] = false;
                        continue;
                    }
                    var tip = hand.TipJoints[f] != null ? hand.TipJoints[f].position : hand.Tips[f];
                    for (int i = 0; i < stringCount; i++)
                    {
                        var s = strings[i];
                        var axis = s.Top - s.Bottom;
                        float along = Vector3.Dot(tip - s.Bottom, axis) / axis.sqrMagnitude;
                        float sideOf = Vector3.Dot(tip - s.Bottom, s.Normal);
                        var closest = s.Bottom + axis * Mathf.Clamp01(along);
                        // Close to the string within its plane, and not far in front of or behind it.
                        var lateral = (tip - closest) - s.Normal * sideOf;
                        bool near = along > 0.02f && along < 0.98f && lateral.magnitude < reach && Mathf.Abs(sideOf) < 0.06f;
                        if (hadSide[finger, i] && near && Mathf.Sign(sideOf) != Mathf.Sign(lastSide[finger, i]) && s.Cooldown <= 0f)
                        {
                            float speed = dt > 0f ? Mathf.Abs(sideOf - lastSide[finger, i]) / dt : 0f;
                            Pluck(s, i, closest, along, Mathf.Clamp01(0.35f + speed * 0.6f));
                            shiver[side] = 1f;
                            Acted("pluck");
                        }
                        lastSide[finger, i] = sideOf;
                        hadSide[finger, i] = true;
                    }
                }
            }
        }

        void Pluck(HarpString s, int index, Vector3 at, float along, float strength)
        {
            s.Cooldown = 0.09f;
            s.Ring = 0.012f * (0.5f + strength);
            s.RingAge = 0f;
            s.PluckHeight = along;
            s.Glow = 1f;
            s.RippleAge = 0f;
            s.Ripple.transform.position = at;
            Musical.Play(voices, frame, s.Midi, at, 0.35f + strength * 0.4f, pluck: true);

            var color = Colors[index % Colors.Length];
            for (int k = 0; k < 10; k++)
            {
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = Random.insideUnitSphere * 0.12f + s.Normal * Random.Range(-0.08f, 0.08f),
                    startColor = color,
                    startSize = Random.Range(0.004f, 0.01f),
                    startLifetime = Random.Range(0.4f, 0.9f)
                }, 1);
            }
            // A few notes of light drift up from the string.
            for (int k = 0; k < 2; k++)
            {
                notes.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Random.insideUnitSphere * 0.01f,
                    velocity = Vector3.up * Random.Range(0.05f, 0.1f) + Random.insideUnitSphere * 0.02f,
                    startColor = Color.Lerp(color, Color.white, 0.3f),
                    startSize = Random.Range(0.012f, 0.02f),
                    startLifetime = Random.Range(1.4f, 2.2f)
                }, 1);
            }
        }

        void DrawString(HarpString s, int index, float band, float t, float pulseAge, Camera view)
        {
            var color = Colors[index % Colors.Length];
            for (int j = 0; j < points; j++)
            {
                float u = j / (float)(points - 1);
                float envelope = Mathf.Sin(Mathf.PI * u);
                var p = Vector3.Lerp(s.Bottom, s.Top, u);
                // Its own quiet sway with its band, a hum when it carries the sung note, and a pulse up the string on
                // each beat.
                float sway = Mathf.Sin(Mathf.PI * u * 2f) * Mathf.Sin(t * (3f + index * 0.4f)) * (0.002f + band * 0.004f);
                float hum = Mathf.Sin(t * 30f + u * 9f) * 0.003f * s.Hum;
                float front = (pulseAge - index * 0.03f) * 1.6f;
                float pulse = Mathf.Exp(-(u - front) * (u - front) / 0.004f) * 0.008f * Mathf.Exp(-pulseAge * 1.6f);
                // The pluck: a vibration strongest where it was plucked, with a bright wave running both ways.
                float shape = u < s.PluckHeight ? u / Mathf.Max(s.PluckHeight, 1e-3f) : (1f - u) / Mathf.Max(1f - s.PluckHeight, 1e-3f);
                float ring = s.Ring * shape * Mathf.Sin(t * (55f + index * 6f));
                p += s.Normal * (sway + hum + ring) + Vector3.Cross(s.Normal, Vector3.up) * pulse * envelope;
                path[j] = p;
            }
            var c = Color.Lerp(color, Color.white, s.Glow * 0.4f + s.Hum * 0.15f);
            c.a = 0.55f + band * 0.25f + s.Glow * 0.5f + s.Hum * 0.3f;
            s.Line.Set(path, points, 0.006f * (1f + s.Glow * 1.2f + s.Hum * 0.5f), c, view.transform.position, false);

            for (int k = 0; k < 2; k++)
            {
                var end = s.Ends[k];
                end.transform.position = k == 0 ? s.Bottom : s.Top;
                end.transform.localScale = Vector3.one * (0.02f + s.Glow * 0.015f);
                Effects.Face(end.transform, view);
                block.Clear();
                block.SetColor(ColorId, color);
                block.SetFloat(IntensityId, 0.45f + band * 0.3f + s.Glow * 0.6f + s.Hum * 0.4f);
                end.SetPropertyBlock(block);
            }
        }

        void DrawRipple(HarpString s, Camera view, float dt)
        {
            bool show = s.RippleAge >= 0f;
            if (s.Ripple.gameObject.activeSelf != show) s.Ripple.gameObject.SetActive(show);
            if (!show) return;
            s.RippleAge += dt;
            float u = s.RippleAge / 0.45f;
            if (u >= 1f)
            {
                s.RippleAge = -1f;
                s.Ripple.gameObject.SetActive(false);
                return;
            }
            s.Ripple.transform.localScale = Vector3.one * (0.03f + u * 0.09f);
            Effects.Face(s.Ripple.transform, view);
            block.Clear();
            block.SetColor(ColorId, Color.white);
            block.SetFloat(RadiusId, 0.8f);
            block.SetFloat(ThicknessId, 0.06f * (1f - u) + 0.01f);
            block.SetFloat(AlphaId, (1f - u) * (1f - u));
            s.Ripple.SetPropertyBlock(block);
        }

        // ---- The drum kit ----

        void BuildKit()
        {
            pieces = new Piece[Kit.Length];
            for (int i = 0; i < Kit.Length; i++)
            {
                var spec = Kit[i];
                var go = new GameObject(spec.IsCymbal ? $"Cymbal {i} ({spec.Kind})" : $"Drum {i}");
                go.transform.SetParent(transform, false);
                var mesh = new Mesh { name = go.name };
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = drumMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                // Each piece has its own voice, so its sound rings on from where it stands while others are struck.
                var voice = go.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                // Full level out to 1 m, so every piece of the ring (the furthest about 1 m from your eyes) plays as
                // loud as the nearest one.
                SpatialAudio.Spatialize(voice, near: 1f);
                go.SetActive(false);
                pieces[i] = new Piece
                {
                    Spec = spec,
                    Root = go.transform,
                    Renderer = r,
                    Mesh = mesh,
                    Voice = voice,
                    Wave = Effects.MakeQuad($"Drum Wave {i}", transform, ringMaterial)
                };
                pieces[i].Wave.gameObject.SetActive(false);
            }
            StartSounds();
        }

        /// <summary>
        /// Stands the kit round you at your height, leaning towards you. With a room scan, a piece that would stand in a
        /// wall comes closer, or stays away when there is no room for it at all.
        /// </summary>
        void PlaceKit(Camera view)
        {
            var forward = Effects.FlatForward(view);
            var side = Vector3.Cross(Vector3.up, forward).normalized;
            var eye = view.transform.position;
            float floorY = Effects.FloorY(view);
            foreach (var p in pieces)
            {
                var s = p.Spec;
                float angle = s.Angle * Mathf.Deg2Rad;
                var outward = forward * Mathf.Cos(angle) + side * Mathf.Sin(angle);
                float distance = s.Distance;
                p.Placed = true;
                if (Room.Scanned && Room.Hit(Room.Walls, eye + Vector3.up * s.Height, outward, out _, out float wall))
                {
                    distance = Mathf.Min(distance, wall - s.Radius - 0.08f);
                    if (distance < 0.34f) p.Placed = false;
                }
                p.Centre = eye + outward * distance + Vector3.up * s.Height;
                p.Outward = outward;
                float tilt = s.Tilt * Mathf.Deg2Rad;
                p.Normal = (Vector3.up * Mathf.Cos(tilt) - outward * Mathf.Sin(tilt)).normalized;
                var across = Vector3.ProjectOnPlane(-outward, p.Normal).normalized;
                p.Rest = Quaternion.LookRotation(across, p.Normal);
                p.Root.localScale = Vector3.one;
                p.Root.SetPositionAndRotation(p.Centre, p.Rest);
                p.Foot = new Vector3(p.Centre.x, floorY + 0.004f, p.Centre.z);
                if (s.IsCymbal) BuildCymbalMesh(p, floorY);
                else BuildDrumMesh(p, floorY);
                p.Root.gameObject.SetActive(p.Placed);
            }
        }

        void ClearMesh()
        {
            meshVertices.Clear();
            meshNormals.Clear();
            meshUvs.Clear();
            meshParts.Clear();
            meshTriangles.Clear();
        }

        void ApplyMesh(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(meshVertices);
            mesh.SetNormals(meshNormals);
            mesh.SetUVs(0, meshUvs);
            mesh.SetUVs(1, meshParts);
            mesh.SetTriangles(meshTriangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// A taiko drum in its own space, its skin's centre at the origin facing up: a barrel bulging a little at its
        /// middle, the skin, a bottom, a rim at each end, and three legs down to the floor.
        /// </summary>
        void BuildDrumMesh(Piece p, float floorY)
        {
            var s = p.Spec;
            ClearMesh();
            const int along = 6;
            for (int j = 0; j <= along; j++)
            {
                float v = j / (float)along;
                float radius = s.Radius * (1f + 0.12f * Mathf.Sin(Mathf.PI * v));
                float slope = 0.12f * Mathf.PI * s.Radius * Mathf.Cos(Mathf.PI * v) / s.Depth;
                for (int i = 0; i <= Around; i++)
                {
                    float u = i / (float)Around;
                    var radial = new Vector3(Mathf.Cos(u * Mathf.PI * 2f), 0f, Mathf.Sin(u * Mathf.PI * 2f));
                    AddVertex(radial * radius + Vector3.down * (s.Depth * v), (radial + Vector3.up * slope).normalized, new Vector2(u, v), 0f);
                }
            }
            for (int j = 0; j < along; j++)
            {
                for (int i = 0; i < Around; i++)
                {
                    int a = j * (Around + 1) + i;
                    int b = a + Around + 1;
                    AddQuad(a, a + 1, b + 1, b);
                }
            }
            AddDisc(s.Radius, 0.002f, Vector3.up, 1f, true);
            AddDisc(s.Radius, -s.Depth, Vector3.down, 0f, false);
            AddRing(s.Radius * 1.045f, 0.007f, -0.014f);
            AddRing(s.Radius * 1.045f, -s.Depth + 0.014f, -s.Depth - 0.007f);

            for (int k = 0; k < 3; k++)
            {
                float angle = (k / 3f + 0.25f) * Mathf.PI * 2f;
                var top = new Vector3(Mathf.Cos(angle) * s.Radius * 0.98f, -s.Depth * 0.72f, Mathf.Sin(angle) * s.Radius * 0.98f);
                var topWorld = p.Root.TransformPoint(top);
                if (topWorld.y - floorY < 0.06f) continue;
                var spread = Vector3.ProjectOnPlane(topWorld - p.Centre, Vector3.up);
                if (spread.sqrMagnitude > 1e-6f) spread = spread.normalized * (s.Radius * 0.55f);
                var footWorld = new Vector3(topWorld.x, floorY, topWorld.z) + spread;
                AddRod(top, p.Root.InverseTransformPoint(footWorld), 0.006f);
            }
            ApplyMesh(p.Mesh);
        }

        /// <summary>
        /// A cymbal in its own space, its bell at the origin facing up: a shallow bowed disc with a raised bell (two of
        /// them, closed together, for the hi-hat), on a stand with a pole and three feet.
        /// </summary>
        void BuildCymbalMesh(Piece p, float floorY)
        {
            var s = p.Spec;
            ClearMesh();
            int plates = s.Kind == Kind.HiHat ? 2 : 1;
            const int rings = 12;
            for (int plate = 0; plate < plates; plate++)
            {
                // The hi-hat's lower plate sits just below, facing up into the top one.
                float flip = plate == 0 ? 1f : -1f;
                float drop = plate == 0 ? 0f : -0.016f;
                int start = meshVertices.Count;
                for (int j = 0; j <= rings; j++)
                {
                    float t = j / (float)rings;
                    float height = CymbalProfile(t) * s.Radius;
                    float slope = CymbalSlope(t);
                    for (int i = 0; i <= Around; i++)
                    {
                        float u = i / (float)Around;
                        var radial = new Vector3(Mathf.Cos(u * Mathf.PI * 2f), 0f, Mathf.Sin(u * Mathf.PI * 2f));
                        var normal = (Vector3.up - radial * slope).normalized * flip;
                        AddVertex(radial * (s.Radius * t) + Vector3.up * (drop + height * flip), normal,
                            new Vector2(radial.x * t, radial.z * t), 4f);
                    }
                }
                for (int j = 0; j < rings; j++)
                {
                    for (int i = 0; i < Around; i++)
                    {
                        int a = start + j * (Around + 1) + i;
                        int b = a + Around + 1;
                        AddQuad(a, a + 1, b + 1, b);
                    }
                }
            }

            // The stand: a boom from under the bell leaning out and down, beyond the drums, to three feet on the floor.
            var under = Vector3.down * (plates == 2 ? 0.03f : 0.012f);
            var underWorld = p.Root.TransformPoint(under);
            if (underWorld.y - floorY > 0.1f)
            {
                var standWorld = new Vector3(underWorld.x, floorY, underWorld.z) + p.Outward * 0.35f;
                var hubWorld = standWorld + Vector3.up * 0.12f;
                AddRod(under, p.Root.InverseTransformPoint(hubWorld), 0.008f);
                for (int k = 0; k < 3; k++)
                {
                    float angle = k / 3f * Mathf.PI * 2f + 0.5f;
                    var footWorld = standWorld + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.16f;
                    AddRod(p.Root.InverseTransformPoint(hubWorld), p.Root.InverseTransformPoint(footWorld), 0.006f);
                }
            }
            ApplyMesh(p.Mesh);
        }

        /// <summary>A cymbal's height (share of its radius) at a share t of the way out: a raised bell and a shallow bow.</summary>
        static float CymbalProfile(float t)
        {
            float bell = t < 0.22f ? Mathf.Sqrt(1f - t * t / (0.22f * 0.22f)) * 0.1f : 0f;
            return bell + 0.05f * (1f - t);
        }

        /// <summary>The slope of <see cref="CymbalProfile"/> (height per distance out).</summary>
        static float CymbalSlope(float t)
        {
            float bell = t < 0.21f ? -0.1f * t / (0.22f * 0.22f) / Mathf.Sqrt(1f - t * t / (0.22f * 0.22f)) : 0f;
            return Mathf.Clamp(bell, -3f, 0f) - 0.05f;
        }

        void AddVertex(Vector3 position, Vector3 normal, Vector2 uv, float part)
        {
            meshVertices.Add(position);
            meshNormals.Add(normal);
            meshUvs.Add(uv);
            meshParts.Add(new Vector2(part, 0f));
        }

        void AddQuad(int a, int b, int c, int d)
        {
            meshTriangles.Add(a);
            meshTriangles.Add(b);
            meshTriangles.Add(c);
            meshTriangles.Add(a);
            meshTriangles.Add(c);
            meshTriangles.Add(d);
        }

        /// <summary>A flat disc at height y; on the skin its uv is the point across it, -1 to 1.</summary>
        void AddDisc(float radius, float y, Vector3 normal, float part, bool skin)
        {
            int centre = meshVertices.Count;
            AddVertex(Vector3.up * y, normal, skin ? Vector2.zero : new Vector2(0f, 1f), part);
            for (int i = 0; i <= Around; i++)
            {
                float u = i / (float)Around;
                var q = new Vector2(Mathf.Cos(u * Mathf.PI * 2f), Mathf.Sin(u * Mathf.PI * 2f));
                AddVertex(new Vector3(q.x * radius, y, q.y * radius), normal, skin ? q : new Vector2(u, 1f), part);
            }
            for (int i = 0; i < Around; i++)
            {
                meshTriangles.Add(centre);
                meshTriangles.Add(centre + 1 + i);
                meshTriangles.Add(centre + 2 + i);
            }
        }

        /// <summary>A short band round the drum from height top down to bottom: a rim.</summary>
        void AddRing(float radius, float top, float bottom)
        {
            int start = meshVertices.Count;
            for (int i = 0; i <= Around; i++)
            {
                float u = i / (float)Around;
                var radial = new Vector3(Mathf.Cos(u * Mathf.PI * 2f), 0f, Mathf.Sin(u * Mathf.PI * 2f));
                AddVertex(radial * radius + Vector3.up * top, radial, new Vector2(u, 0f), 2f);
                AddVertex(radial * radius + Vector3.up * bottom, radial, new Vector2(u, 1f), 2f);
            }
            for (int i = 0; i < Around; i++)
            {
                int a = start + i * 2;
                AddQuad(a, a + 2, a + 3, a + 1);
            }
        }

        /// <summary>A thin four-sided rod: a leg or a pole of a stand.</summary>
        void AddRod(Vector3 from, Vector3 to, float radius)
        {
            var along = (to - from).normalized;
            var a = Vector3.Cross(along, Mathf.Abs(along.y) < 0.9f ? Vector3.up : Vector3.right).normalized * radius;
            var b = Vector3.Cross(along, a).normalized * radius;
            int start = meshVertices.Count;
            for (int k = 0; k < 4; k++)
            {
                float angle = k * Mathf.PI * 0.5f;
                var offset = a * Mathf.Cos(angle) + b * Mathf.Sin(angle);
                AddVertex(from + offset, offset.normalized, new Vector2(k / 4f, 0f), 3f);
                AddVertex(to + offset, offset.normalized, new Vector2(k / 4f, 1f), 3f);
            }
            for (int k = 0; k < 4; k++)
            {
                int i0 = start + k * 2;
                int i1 = start + (k + 1) % 4 * 2;
                AddQuad(i0, i1, i1 + 1, i0 + 1);
            }
        }

        /// <summary>
        /// A palm or fingertip coming down through a skin or a cymbal, inside its edge and fast enough, strikes it:
        /// louder the faster it came. The hand has to lift clear before it can strike that piece again.
        /// </summary>
        void Strike(HandRig rig, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                // While the menu is open the hands are choosing there, not drumming.
                bool live = hand.IsTracked && !MenuOpen;
                handPoints[0] = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                for (int f = 0; f < 5; f++) handPoints[f + 1] = hand.TipJoints[f] != null ? hand.TipJoints[f].position : hand.Tips[f];
                for (int i = 0; i < pieces.Length; i++)
                {
                    var p = pieces[i];
                    p.Wait[side] -= dt;
                    bool usable = live && p.Placed;
                    // How far below its surface a hand still counts as striking it: through a drum's barrel, or just
                    // past a thin cymbal.
                    float below = p.Spec.IsCymbal ? 0.07f : p.Spec.Depth;
                    bool clear = true;
                    for (int k = 0; k < HitPoints; k++)
                    {
                        int slot = side * HitPoints + k;
                        if (!usable)
                        {
                            hadHeight[i, slot] = false;
                            continue;
                        }
                        var offset = handPoints[k] - p.Centre;
                        float height = Vector3.Dot(offset, p.Normal);
                        var across = offset - p.Normal * height;
                        bool over = across.sqrMagnitude < p.Spec.Radius * p.Spec.Radius * 1.17f;
                        if (over && height < 0.03f) clear = false;
                        if (hadHeight[i, slot] && over && lastHeight[i, slot] > 0f && height <= 0.004f && height > -below
                            && p.Armed[side] && p.Wait[side] <= 0f && dt > 0f)
                        {
                            float speed = (lastHeight[i, slot] - height) / dt;
                            if (speed > strikeSpeed)
                            {
                                Sound(p, across, Mathf.Clamp01((speed - strikeSpeed) / Mathf.Max(fullStrikeSpeed - strikeSpeed, 0.1f)));
                                p.Wait[side] = strikeRest;
                                p.Armed[side] = false;
                                shiver[side] = 1f;
                                Acted(p.Spec.IsCymbal ? "cymbal" : "drum");
                            }
                        }
                        lastHeight[i, slot] = height;
                        hadHeight[i, slot] = true;
                    }
                    if (clear) p.Armed[side] = true;
                }
            }
        }

        void Sound(Piece p, Vector3 across, float strength)
        {
            float loud = 0.3f + 0.7f * strength;
            p.Glow = 1f;
            var local = p.Root.InverseTransformDirection(across) / Mathf.Max(p.Spec.Radius, 1e-3f);
            p.Hit = new Vector4(local.x, local.z, 0f, loud);
            // A drum sounds strongly even from a light tap; a cymbal keeps its wider range from a touch to a crash.
            float level = p.Spec.IsCymbal ? 0.4f + 0.6f * strength : 0.75f + 0.25f * strength;
            if (p.Sound != null && p.Voice.isActiveAndEnabled) p.Voice.PlayOneShot(p.Sound, level);

            if (p.Spec.IsCymbal)
            {
                // The cymbal swings about the line across it, away from where it was struck.
                p.SwingAge = 0f;
                p.Swing = (p.Spec.Kind == Kind.HiHat ? 3f : 7f) * (0.4f + strength);
                var axis = Vector3.Cross(p.Normal, across);
                p.SwingAxis = axis.sqrMagnitude > 1e-8f ? axis.normalized : p.Root.right;
            }
            else
            {
                p.WaveAge = 0f;
                p.WaveStrength = loud;
            }

            var at = p.Centre + across + p.Normal * 0.01f;
            for (int k = 0; k < 8 + (int)(strength * 10f); k++)
            {
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Random.insideUnitSphere * 0.02f,
                    velocity = p.Normal * Random.Range(0.1f, 0.4f) + Random.insideUnitSphere * 0.15f,
                    startColor = Color.Lerp(p.Spec.Color, Color.white, Random.Range(0f, 0.4f)),
                    startSize = Random.Range(0.005f, 0.012f),
                    startLifetime = Random.Range(0.35f, 0.8f)
                }, 1);
            }
        }

        /// <summary>
        /// The kit breathes with the music and shows its last strikes: a glow, a drum's skin rippling and a ring of light
        /// spreading on the floor below it, a cymbal swinging with its shine running out across the metal.
        /// </summary>
        void DrawKit(MusicFrame music, float dt)
        {
            foreach (var p in pieces)
            {
                if (!p.Placed || !p.Root.gameObject.activeSelf)
                {
                    if (p.Wave.gameObject.activeSelf) p.Wave.gameObject.SetActive(false);
                    continue;
                }
                var s = p.Spec;
                // The big drums breathe with the bass and the kick, the middle ones with the low sounds, the small ones
                // with the middle of the music, and the cymbals with its top.
                float target = s.IsCymbal ? music.Band(s.Kind == Kind.HiHat || s.Kind == Kind.Ride ? 4 : 5) * 0.7f
                    : s.Radius > 0.14f ? music.Band(0) * 0.55f + music.Kick * 0.5f
                    : s.Radius > 0.1f ? music.Band(1) * 0.7f
                    : music.Band(3) * 0.8f;
                p.Pulse = Mathf.Lerp(p.Pulse, Mathf.Clamp01(target), 1f - Mathf.Exp(-dt * 12f));
                p.Glow = Mathf.MoveTowards(p.Glow, 0f, dt * (s.IsCymbal ? 1.2f : 2.2f));
                p.Hit.z += dt;

                if (p.SwingAge >= 0f)
                {
                    p.SwingAge += dt;
                    float fade = Mathf.Exp(-p.SwingAge * 2.4f);
                    float angle = p.Swing * Mathf.Sin(p.SwingAge * 12f) * fade;
                    p.Root.rotation = Quaternion.AngleAxis(angle, p.SwingAxis) * p.Rest;
                    if (fade < 0.01f)
                    {
                        p.SwingAge = -1f;
                        p.Root.rotation = p.Rest;
                    }
                }

                block.Clear();
                block.SetColor(ColorId, s.Color);
                block.SetFloat(GlowId, p.Glow);
                block.SetFloat(PulseId, p.Pulse);
                block.SetFloat(WakeId, Mathf.Lerp(0.3f, 1f, FadeIn));
                block.SetVector(HitId, p.Hit);
                p.Renderer.SetPropertyBlock(block);
                DrawWave(p, dt);
            }
        }

        void DrawWave(Piece p, float dt)
        {
            bool show = p.WaveAge >= 0f;
            if (p.Wave.gameObject.activeSelf != show) p.Wave.gameObject.SetActive(show);
            if (!show) return;
            p.WaveAge += dt;
            float u = p.WaveAge / 1.1f;
            if (u >= 1f)
            {
                p.WaveAge = -1f;
                p.Wave.gameObject.SetActive(false);
                return;
            }
            p.Wave.transform.SetPositionAndRotation(p.Foot, LyingFlat);
            p.Wave.transform.localScale = Vector3.one * ((0.25f + u * 1.5f) * (0.6f + p.WaveStrength * 0.6f));
            block.Clear();
            block.SetColor(ColorId, p.Spec.Color);
            block.SetFloat(RadiusId, 0.85f);
            block.SetFloat(ThicknessId, 0.05f * (1f - u) + 0.01f);
            block.SetFloat(AlphaId, (1f - u) * (1f - u) * p.WaveStrength);
            p.Wave.SetPropertyBlock(block);
        }

        // ---- The kit's sounds, made in code ----

        /// <summary>Starts making the kit's sounds as the app loads, so they are ready by the time the world is chosen.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void MakeSoundsEarly() => MakeSounds();

        /// <summary>
        /// Makes every piece's sound on a worker thread (once), so nothing stalls a frame; the world makes its clips from
        /// the results a few at a time once they are ready (see <see cref="FinishSounds"/>).
        /// </summary>
        static void MakeSounds()
        {
            if (soundWork != null && !soundWork.IsFaulted) return;
            soundRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int rate = soundRate;
            soundWork = System.Threading.Tasks.Task.Run(() =>
            {
                var all = new float[Kit.Length][];
                for (int i = 0; i < Kit.Length; i++)
                {
                    var s = Kit[i];
                    all[i] = s.IsCymbal ? MakeCymbal(rate, s.Kind, s.Decay, 57 + i) : MakeDrum(rate, s.Pitch, s.Decay, 31 + i);
                }
                return all;
            });
        }

        void StartSounds()
        {
            soundsMade = 0;
            soundData = null;
            MakeSounds();
        }

        void FinishSounds()
        {
            if (soundsMade >= pieces.Length) return;
            if (soundData == null)
            {
                if (soundWork == null || !soundWork.IsCompleted) return;
                if (soundWork.IsFaulted)
                {
                    // Once, rather than every frame: the kit stays silent.
                    Debug.LogException(soundWork.Exception);
                    soundsMade = pieces.Length;
                    return;
                }
                soundData = soundWork.Result;
                // The clips hold the sounds from here on; let the worker's copy go.
                soundWork = null;
            }
            // Two clips a frame, so no single frame does it all.
            for (int n = 0; n < 2 && soundsMade < pieces.Length; n++, soundsMade++)
            {
                var data = soundData[soundsMade];
                var clip = AudioClip.Create(pieces[soundsMade].Root.name, data.Length, 1, soundRate, false);
                clip.SetData(data, 0);
                pieces[soundsMade].Sound = clip;
            }
            if (soundsMade >= pieces.Length) soundData = null;
        }

        // Modes of a struck drum skin (the ratios of a circular membrane), each fainter than the last.
        static readonly float[] SkinModes = { 1f, 1.59f, 2.14f, 2.3f, 2.65f, 2.92f, 3.16f, 3.5f, 4.06f, 4.6f };
        static readonly float[] SkinLevels = { 1f, 0.55f, 0.38f, 0.3f, 0.22f, 0.16f, 0.12f, 0.1f, 0.08f, 0.06f };

        const int SineSize = 4096;
        static readonly float[] SineTable = MakeSineTable();

        static float[] MakeSineTable()
        {
            var table = new float[SineSize];
            for (int i = 0; i < SineSize; i++) table[i] = (float)System.Math.Sin(i * 2.0 * System.Math.PI / SineSize);
            return table;
        }

        /// <summary>A sine for a phase in turns (0 to 1), from the table: quick enough to make seconds of sound.</summary>
        static float Sine(float turns) => SineTable[(int)(turns * SineSize) & (SineSize - 1)];

        /// <summary>
        /// A taiko boom: the skin's modes, its pitch falling a little just after the strike as the skin relaxes, a low
        /// thump that gives the strike its weight, the slap of the hand, and a short room echo so it rings on.
        /// </summary>
        static float[] MakeDrum(int rate, float pitch, float decay, int seed)
        {
            float seconds = Mathf.Clamp(5.5f / decay, 1.4f, 3.2f) + 0.7f;
            int length = (int)(rate * seconds);
            var data = new float[length];
            var random = new System.Random(seed);
            int modes = SkinModes.Length;
            var phase = new float[modes];
            var level = new float[modes];
            var fade = new float[modes];
            for (int m = 0; m < modes; m++)
            {
                phase[m] = (float)random.NextDouble() * 0.05f;
                level[m] = SkinLevels[m];
                fade[m] = Mathf.Exp(-decay * (1f + m * 0.6f) / rate);
            }
            float thumpPitch = Mathf.Max(pitch * 0.75f, 60f), thumpPhase = 0f, thump = 0.45f;
            float thumpFade = Mathf.Exp(-16f / rate), slapFade = Mathf.Exp(-60f / rate), slap = 1.2f;
            float low = 0f, lower = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float bend = 1f + 0.55f * Mathf.Exp(-t * 24f) - 0.025f * (1f - Mathf.Exp(-t * 1.2f));
                float tone = 0f;
                for (int m = 0; m < modes; m++)
                {
                    phase[m] += pitch * SkinModes[m] * bend / rate;
                    if (phase[m] >= 1f) phase[m] -= 1f;
                    tone += Sine(phase[m]) * level[m];
                    level[m] *= fade[m];
                }
                thumpPhase += thumpPitch / rate;
                if (thumpPhase >= 1f) thumpPhase -= 1f;
                float white = (float)random.NextDouble() * 2f - 1f;
                low += (white - low) * 0.5f;
                lower += (low - lower) * 0.06f;
                float attack = Mathf.Min(1f, t / 0.0012f);
                data[i] = (tone * 0.6f + Sine(thumpPhase) * thump + (low - lower) * slap) * attack;
                thump *= thumpFade;
                slap *= slapFade;
            }
            // The headset's speakers give little of a big drum's low boom, so round the peaks off and lift the rest: the
            // drum carries more body and slap at the same peak level, and sounds clearly louder.
            Normalize(data, 1f);
            Saturate(data, 2.2f);
            Echo(data, rate, 0.26f);
            Normalize(data, 0.95f);
            FadeEnd(data, rate);
            return data;
        }

        /// <summary>A soft clip (tanh) with <paramref name="drive"/>: peaks rounded off, quieter parts lifted.</summary>
        static void Saturate(float[] data, float drive)
        {
            for (int i = 0; i < data.Length; i++) data[i] = (float)System.Math.Tanh(data[i] * drive);
        }

        /// <summary>
        /// A cymbal: a cloud of inharmonic partials (the shimmer of a metal plate) over hissing noise, with a bright tick
        /// as it is struck. The ride adds a clear bell, the crash swells for a moment, and the hi-hat is short and dry.
        /// </summary>
        static float[] MakeCymbal(int rate, Kind kind, float decay, int seed)
        {
            var random = new System.Random(seed);
            bool hat = kind == Kind.HiHat;
            float seconds = hat ? 0.9f : kind == Kind.Splash ? 2.4f : 4.5f;
            int length = (int)(rate * seconds);
            var data = new float[length];
            int count = hat ? 18 : 36;
            float lowest = kind == Kind.Ride ? 380f : hat ? 1800f : kind == Kind.Splash ? 900f : 520f;
            float highest = hat ? 11000f : 9500f;
            var phase = new float[count];
            var step = new float[count];
            var level = new float[count];
            var fade = new float[count];
            for (int k = 0; k < count; k++)
            {
                float frequency = lowest * Mathf.Pow(highest / lowest, Mathf.Pow((float)random.NextDouble(), 0.8f));
                step[k] = frequency / rate;
                phase[k] = (float)random.NextDouble();
                level[k] = (0.6f + 0.4f * (float)random.NextDouble()) / Mathf.Sqrt(1f + k);
                fade[k] = Mathf.Exp(-decay * (0.6f + 1.4f * (float)random.NextDouble()) * (1f + frequency / 9000f) / rate);
            }
            // The ride's bell: a few clear partials that ring through the wash.
            float[] bellStep = { 612f / rate, 1490f / rate, 2280f / rate };
            float[] bellLevel = { 0.32f, 0.2f, 0.12f };
            float[] bellFade = { Mathf.Exp(-1.6f / rate), Mathf.Exp(-2.4f / rate), Mathf.Exp(-3.2f / rate) };
            var bellPhase = new float[3];
            float hiss = 0f, previous = 0f, noise = hat ? 0.9f : 0.45f;
            float noiseFade = Mathf.Exp(-decay * (hat ? 1.5f : 1.2f) / rate), tick = 0.7f, tickFade = Mathf.Exp(-300f / rate);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float shimmer = 0f;
                for (int k = 0; k < count; k++)
                {
                    phase[k] += step[k];
                    if (phase[k] >= 1f) phase[k] -= 1f;
                    shimmer += Sine(phase[k]) * level[k];
                    level[k] *= fade[k];
                }
                float bell = 0f;
                if (kind == Kind.Ride)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        bellPhase[k] += bellStep[k];
                        if (bellPhase[k] >= 1f) bellPhase[k] -= 1f;
                        bell += Sine(bellPhase[k]) * bellLevel[k];
                        bellLevel[k] *= bellFade[k];
                    }
                }
                float white = (float)random.NextDouble() * 2f - 1f;
                hiss = 0.85f * (hiss + white - previous);
                previous = white;
                float swell = kind == Kind.Crash ? 1f - 0.6f * Mathf.Exp(-t * 30f) : 1f;
                data[i] = (shimmer * 0.35f + hiss * noise) * swell + bell + white * tick;
                noise *= noiseFade;
                tick *= tickFade;
            }
            if (!hat) Echo(data, rate, 0.2f);
            Normalize(data, hat ? 0.7f : 0.8f);
            FadeEnd(data, rate);
            return data;
        }

        /// <summary>A small room's echo (Schroeder's four combs and two all-passes), mixed in by <paramref name="wet"/>.</summary>
        static void Echo(float[] data, int rate, float wet)
        {
            float scale = rate / 44100f;
            int[] combLengths = { 1116, 1188, 1277, 1356 };
            int[] passLengths = { 556, 441 };
            var combs = new float[4][];
            var combAt = new int[4];
            var store = new float[4];
            for (int k = 0; k < 4; k++) combs[k] = new float[Mathf.Max(1, (int)(combLengths[k] * scale))];
            var passes = new float[2][];
            var passAt = new int[2];
            for (int k = 0; k < 2; k++) passes[k] = new float[Mathf.Max(1, (int)(passLengths[k] * scale))];
            const float feedback = 0.83f, damp = 0.25f;
            for (int i = 0; i < data.Length; i++)
            {
                float input = data[i] * 0.25f, sum = 0f;
                for (int k = 0; k < 4; k++)
                {
                    var buffer = combs[k];
                    float y = buffer[combAt[k]];
                    store[k] = y * (1f - damp) + store[k] * damp;
                    buffer[combAt[k]] = input + store[k] * feedback;
                    if (++combAt[k] >= buffer.Length) combAt[k] = 0;
                    sum += y;
                }
                for (int k = 0; k < 2; k++)
                {
                    var buffer = passes[k];
                    float held = buffer[passAt[k]];
                    float output = held - sum;
                    buffer[passAt[k]] = sum + held * 0.5f;
                    if (++passAt[k] >= buffer.Length) passAt[k] = 0;
                    sum = output;
                }
                data[i] = data[i] * (1f - wet) + sum * wet;
            }
        }

        /// <summary>Fades the last moments of a sound to silence, so a tail still ringing never stops with a click.</summary>
        static void FadeEnd(float[] data, int rate)
        {
            int span = Mathf.Min(data.Length, (int)(rate * 0.6f));
            for (int i = 0; i < span; i++)
            {
                float u = i / (float)span;
                data[data.Length - span + i] *= 0.5f + 0.5f * Mathf.Cos(u * Mathf.PI);
            }
        }

        static void Normalize(float[] data, float peak)
        {
            float most = 1e-6f;
            for (int i = 0; i < data.Length; i++) most = Mathf.Max(most, Mathf.Abs(data[i]));
            float gain = peak / most;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }

        /// <summary>For previews: one piece of the kit (or none, with -1) was just struck.</summary>
        public void PreviewDrums(int struck)
        {
            Build();
            if (struck < 0 || struck >= pieces.Length) return;
            var p = pieces[struck];
            p.Glow = 1f;
            p.Hit = new Vector4(0.25f, -0.2f, 0.12f, 1f);
            if (p.Spec.IsCymbal)
            {
                p.SwingAge = 0.08f;
                p.Swing = 7f;
                p.SwingAxis = Vector3.right;
            }
            else
            {
                p.WaveAge = 0.25f;
                p.WaveStrength = 1f;
            }
        }

        /// <summary>For previews: rings a string as if it had just been plucked.</summary>
        public void PreviewPluck(int index, float amount)
        {
            Build();
            var s = strings[Mathf.Clamp(index, 0, strings.Length - 1)];
            s.Ring = amount;
            s.Glow = 1f;
            s.PluckHeight = 0.5f;
        }
    }
}
