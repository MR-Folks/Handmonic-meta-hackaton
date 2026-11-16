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
    /// Round the harp stands a ring of nine neon taiko drums, their skins tilted towards you: small high ones beside
    /// the harp, bigger and deeper ones further round, and the biggest behind you. They arrive with the music: first
    /// a single boom behind you, then one more drum on each beat, each from its own place round your head, until all
    /// nine are there. Strike a skin with your palm or fingers and it booms from where it stands, a ripple runs out
    /// from where you hit it and a ring of light spreads across the floor beneath it. Between strikes the big drums
    /// breathe with the bass and the small ones with the higher sounds.
    /// </summary>
    public class AirStrings : World
    {
        public override string DisplayName => "Air strings";

        public override Color Color => new Color(0.49f, 0.95f, 0.79f);

        public override int MenuOrder => 30;

        public override int MenuIcon => 3;

        public override string Hint => "Brush the strings, or drum on the drums around you";

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

        [Header("Drums")]
        [Tooltip("How fast (m/s) a palm or fingertip must come down onto a skin for the drum to sound.")]
        [SerializeField] float strikeSpeed = 0.35f;
        [Tooltip("How fast (m/s) a strike must be for the loudest boom.")]
        [SerializeField] float fullStrikeSpeed = 2f;
        [Tooltip("Seconds before the same hand can strike the same drum again.")]
        [SerializeField] float strikeRest = 0.12f;
        [Tooltip("Seconds a drum waits to arrive when no beat comes.")]
        [SerializeField] float wakeWithoutBeat = 1.2f;

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

        /// <summary>One drum of the ring: where it stands round you, its size, and its sound.</summary>
        readonly struct DrumSpec
        {
            /// <summary>Degrees round from straight ahead, positive to the right.</summary>
            public readonly float Angle;
            /// <summary>How far out from below your eyes its skin's centre is (m).</summary>
            public readonly float Distance;
            /// <summary>Height of its skin's centre relative to your eyes (m).</summary>
            public readonly float Height;
            public readonly float Radius, Depth;
            /// <summary>How far its skin leans towards you from level (degrees).</summary>
            public readonly float Tilt;
            /// <summary>Its boom's pitch (Hz) and how fast the boom dies away (per second).</summary>
            public readonly float Pitch, Decay;
            public readonly Color Color;

            public DrumSpec(float angle, float distance, float height, float radius, float depth, float tilt, float pitch, float decay, Color color)
            {
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
        }

        // In the order they arrive: the big drum behind you first, then round both sides towards the small ones beside
        // the harp. The front is left to the harp.
        static readonly DrumSpec[] Kit =
        {
            new DrumSpec(180f, 0.8f, -0.58f, 0.2f, 0.36f, 42f, 55f, 3.5f, new Color(1f, 0.3f, 0.68f)),
            new DrumSpec(150f, 0.66f, -0.6f, 0.16f, 0.3f, 30f, 69f, 4.5f, new Color(0.95f, 0.36f, 0.86f)),
            new DrumSpec(-150f, 0.66f, -0.6f, 0.16f, 0.3f, 30f, 65f, 4.5f, new Color(0.56f, 0.5f, 1f)),
            new DrumSpec(122f, 0.6f, -0.55f, 0.145f, 0.26f, 26f, 87f, 5.5f, new Color(1f, 0.28f, 0.52f)),
            new DrumSpec(-122f, 0.6f, -0.55f, 0.145f, 0.26f, 26f, 78f, 5.5f, new Color(0.62f, 0.42f, 1f)),
            new DrumSpec(94f, 0.53f, -0.45f, 0.11f, 0.19f, 30f, 131f, 7.5f, new Color(1f, 0.34f, 0.64f)),
            new DrumSpec(-94f, 0.53f, -0.45f, 0.11f, 0.19f, 30f, 117f, 7.5f, new Color(0.72f, 0.45f, 1f)),
            new DrumSpec(68f, 0.48f, -0.36f, 0.085f, 0.13f, 34f, 196f, 11f, new Color(1f, 0.44f, 0.8f)),
            new DrumSpec(-68f, 0.48f, -0.36f, 0.085f, 0.13f, 34f, 175f, 11f, new Color(0.86f, 0.46f, 1f))
        };

        sealed class Drum
        {
            public DrumSpec Spec;
            public Transform Root;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public AudioClip Sound;
            public Renderer Wave;
            public Vector3 Centre, Normal, Foot;
            /// <summary>False when a scanned wall leaves no room for it.</summary>
            public bool Placed;
            /// <summary>Seconds since it arrived, or below 0 while it hasn't yet.</summary>
            public float WakeAge = -1f;
            public float Wake, Glow, Pulse;
            /// <summary>The last strike: where on the skin (-1..1), seconds since, and how hard.</summary>
            public Vector4 Hit = new Vector4(0f, 0f, 9f, 0f);
            public float WaveAge = -1f, WaveStrength;
            public readonly float[] Rest = new float[2];
            public readonly bool[] Armed = { true, true };
        }

        // Each hand strikes with its palm and its five fingertips.
        const int HitPoints = 6;
        // Segments round a drum.
        const int Around = 28;

        HarpString[] strings;
        Vector3[] path;
        AudioPool voices, drumVoices;
        ParticleSystem sparks, notes;
        MaterialPropertyBlock block;
        readonly float[,] lastSide = new float[10, 8];
        readonly bool[,] hadSide = new bool[10, 8];
        readonly float[] shiver = new float[2];
        float beatTime = -10f;
        MusicFrame frame;

        Drum[] drums;
        int arrived;
        float sinceArrival;
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
            BuildDrums();
        }

        protected override void OnEnter()
        {
            System.Array.Clear(hadSide, 0, hadSide.Length);
            // The drums arrive again, one by one, each time the world starts.
            System.Array.Clear(hadHeight, 0, hadHeight.Length);
            arrived = 0;
            sinceArrival = wakeWithoutBeat * 0.6f;
            foreach (var d in drums)
            {
                d.WakeAge = -1f;
                d.Wake = d.Glow = d.Pulse = 0f;
                d.WaveAge = -1f;
                d.Armed[0] = d.Armed[1] = true;
                d.Root.gameObject.SetActive(false);
                d.Wave.gameObject.SetActive(false);
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
            PlaceDrums(view);
        }

        /// <summary>Each beat sends a pulse up the strings, one after the other, and brings the next drum.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            beatTime = Now;
            if (arrived < drums.Length) Arrive(true);
        }

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
            DrawDrums(music, dt);

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
            if (music.Notes != null)
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

        // ---- The drums ----

        void BuildDrums()
        {
            drums = new Drum[Kit.Length];
            for (int i = 0; i < Kit.Length; i++)
            {
                var spec = Kit[i];
                var go = new GameObject($"Drum {i}");
                go.transform.SetParent(transform, false);
                var mesh = new Mesh { name = $"Drum {i}" };
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = drumMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                drums[i] = new Drum
                {
                    Spec = spec,
                    Root = go.transform,
                    Renderer = r,
                    Mesh = mesh,
                    Sound = MakeDrumSound($"Drum {i}", spec.Pitch, spec.Decay, 31 + i),
                    Wave = Effects.MakeQuad($"Drum Wave {i}", transform, ringMaterial)
                };
                drums[i].Wave.gameObject.SetActive(false);
            }
            drumVoices = new AudioPool(transform, 10);
        }

        /// <summary>
        /// Stands the drums in a ring round you, at your height, their skins leaning towards you. With a room scan, a
        /// drum that would stand in a wall comes closer, or stays away when there is no room for it at all.
        /// </summary>
        void PlaceDrums(Camera view)
        {
            var forward = Effects.FlatForward(view);
            var side = Vector3.Cross(Vector3.up, forward).normalized;
            var eye = view.transform.position;
            float floorY = Effects.FloorY(view);
            foreach (var d in drums)
            {
                var s = d.Spec;
                float angle = s.Angle * Mathf.Deg2Rad;
                var outward = forward * Mathf.Cos(angle) + side * Mathf.Sin(angle);
                float distance = s.Distance;
                d.Placed = true;
                if (Room.Scanned && Room.Hit(Room.Walls, eye + Vector3.up * s.Height, outward, out _, out float wall))
                {
                    distance = Mathf.Min(distance, wall - s.Radius - 0.08f);
                    if (distance < 0.34f) d.Placed = false;
                }
                d.Centre = eye + outward * distance + Vector3.up * s.Height;
                float tilt = s.Tilt * Mathf.Deg2Rad;
                d.Normal = (Vector3.up * Mathf.Cos(tilt) - outward * Mathf.Sin(tilt)).normalized;
                var across = Vector3.ProjectOnPlane(-outward, d.Normal).normalized;
                d.Root.localScale = Vector3.one;
                d.Root.SetPositionAndRotation(d.Centre, Quaternion.LookRotation(across, d.Normal));
                d.Foot = new Vector3(d.Centre.x, floorY + 0.004f, d.Centre.z);
                BuildDrumMesh(d, floorY);
                if (!d.Placed) d.Root.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// A taiko drum in its own space, its skin's centre at the origin facing up: a barrel bulging a little at its
        /// middle, the skin, a bottom, a rim at each end, and three legs down to the floor.
        /// </summary>
        void BuildDrumMesh(Drum d, float floorY)
        {
            var s = d.Spec;
            meshVertices.Clear();
            meshNormals.Clear();
            meshUvs.Clear();
            meshParts.Clear();
            meshTriangles.Clear();

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
                var topWorld = d.Root.TransformPoint(top);
                if (topWorld.y - floorY < 0.06f) continue;
                var spread = Vector3.ProjectOnPlane(topWorld - d.Centre, Vector3.up);
                if (spread.sqrMagnitude > 1e-6f) spread = spread.normalized * (s.Radius * 0.55f);
                var footWorld = new Vector3(topWorld.x, floorY, topWorld.z) + spread;
                AddRod(top, d.Root.InverseTransformPoint(footWorld), 0.006f);
            }

            var mesh = d.Mesh;
            mesh.Clear();
            mesh.SetVertices(meshVertices);
            mesh.SetNormals(meshNormals);
            mesh.SetUVs(0, meshUvs);
            mesh.SetUVs(1, meshParts);
            mesh.SetTriangles(meshTriangles, 0);
            mesh.RecalculateBounds();
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
                var p = new Vector2(Mathf.Cos(u * Mathf.PI * 2f), Mathf.Sin(u * Mathf.PI * 2f));
                AddVertex(new Vector3(p.x * radius, y, p.y * radius), normal, skin ? p : new Vector2(u, 1f), part);
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

        /// <summary>A thin four-sided rod: a leg of the stand.</summary>
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

        /// <summary>The next drum arrives, with a soft boom from where it stands when it comes on a beat.</summary>
        void Arrive(bool boom)
        {
            sinceArrival = 0f;
            while (arrived < drums.Length)
            {
                var d = drums[arrived++];
                if (!d.Placed) continue;
                d.WakeAge = 0f;
                d.Glow = 0.8f;
                d.Hit = new Vector4(0f, 0f, 0f, 0.6f);
                d.WaveAge = 0f;
                d.WaveStrength = 0.5f;
                if (boom) drumVoices.Play(d.Sound, d.Centre, 0.25f);
                return;
            }
        }

        /// <summary>
        /// A palm or fingertip coming down through a drum's skin, inside its rim and fast enough, strikes it: louder
        /// the faster it came. The hand has to lift clear of the skin before it can strike that drum again.
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
                for (int i = 0; i < drums.Length; i++)
                {
                    var d = drums[i];
                    d.Rest[side] -= dt;
                    bool usable = live && d.Placed && d.Wake > 0.6f;
                    bool clear = true;
                    for (int k = 0; k < HitPoints; k++)
                    {
                        int slot = side * HitPoints + k;
                        if (!usable)
                        {
                            hadHeight[i, slot] = false;
                            continue;
                        }
                        var offset = handPoints[k] - d.Centre;
                        float height = Vector3.Dot(offset, d.Normal);
                        var across = offset - d.Normal * height;
                        bool over = across.sqrMagnitude < d.Spec.Radius * d.Spec.Radius * 1.17f;
                        if (over && height < 0.03f) clear = false;
                        if (hadHeight[i, slot] && over && lastHeight[i, slot] > 0f && height <= 0.004f && height > -d.Spec.Depth
                            && d.Armed[side] && d.Rest[side] <= 0f && dt > 0f)
                        {
                            float speed = (lastHeight[i, slot] - height) / dt;
                            if (speed > strikeSpeed)
                            {
                                Boom(d, across, Mathf.Clamp01((speed - strikeSpeed) / Mathf.Max(fullStrikeSpeed - strikeSpeed, 0.1f)));
                                d.Rest[side] = strikeRest;
                                d.Armed[side] = false;
                                shiver[side] = 1f;
                                Acted("drum");
                            }
                        }
                        lastHeight[i, slot] = height;
                        hadHeight[i, slot] = true;
                    }
                    if (clear) d.Armed[side] = true;
                }
            }
        }

        void Boom(Drum d, Vector3 across, float strength)
        {
            float loud = 0.3f + 0.7f * strength;
            d.Glow = 1f;
            var local = d.Root.InverseTransformDirection(across) / Mathf.Max(d.Spec.Radius, 1e-3f);
            d.Hit = new Vector4(local.x, local.z, 0f, loud);
            d.WaveAge = 0f;
            d.WaveStrength = loud;
            drumVoices.Play(d.Sound, d.Centre, 0.4f + 0.6f * strength, Random.Range(0.97f, 1.03f));

            var at = d.Centre + across + d.Normal * 0.01f;
            for (int k = 0; k < 8 + (int)(strength * 10f); k++)
            {
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Random.insideUnitSphere * 0.02f,
                    velocity = d.Normal * Random.Range(0.1f, 0.4f) + Random.insideUnitSphere * 0.15f,
                    startColor = Color.Lerp(d.Spec.Color, Color.white, Random.Range(0f, 0.4f)),
                    startSize = Random.Range(0.005f, 0.012f),
                    startLifetime = Random.Range(0.35f, 0.8f)
                }, 1);
            }
        }

        /// <summary>
        /// The drums arrive (popping up out of nothing), breathe with the music, and show their last strike: a glow,
        /// the ripple on the skin, and a ring of light spreading on the floor below.
        /// </summary>
        void DrawDrums(MusicFrame music, float dt)
        {
            sinceArrival += dt;
            if (arrived < drums.Length && sinceArrival > wakeWithoutBeat) Arrive(false);
            foreach (var d in drums)
            {
                bool shown = d.Placed && d.WakeAge >= 0f;
                if (d.Root.gameObject.activeSelf != shown) d.Root.gameObject.SetActive(shown);
                if (!shown)
                {
                    if (d.Wave.gameObject.activeSelf) d.Wave.gameObject.SetActive(false);
                    continue;
                }
                d.WakeAge += dt;
                d.Wake = Mathf.Clamp01(d.WakeAge / 0.5f);
                d.Root.localScale = Vector3.one * Mathf.Max(EaseOutBack(d.Wake), 0.001f);

                // The big drums breathe with the bass and the kick, the middle ones with the low sounds, the small
                // ones with the middle of the music.
                float target = d.Spec.Radius > 0.14f ? music.Band(0) * 0.55f + music.Kick * 0.5f
                    : d.Spec.Radius > 0.1f ? music.Band(1) * 0.7f
                    : music.Band(3) * 0.8f;
                d.Pulse = Mathf.Lerp(d.Pulse, Mathf.Clamp01(target), 1f - Mathf.Exp(-dt * 12f));
                d.Glow = Mathf.MoveTowards(d.Glow, 0f, dt * 2.2f);
                d.Hit.z += dt;

                block.Clear();
                block.SetColor(ColorId, d.Spec.Color);
                block.SetFloat(GlowId, d.Glow);
                block.SetFloat(PulseId, d.Pulse);
                block.SetFloat(WakeId, Mathf.Lerp(0.3f, 1f, d.Wake));
                block.SetVector(HitId, d.Hit);
                d.Renderer.SetPropertyBlock(block);
                DrawWave(d, dt);
            }
        }

        void DrawWave(Drum d, float dt)
        {
            bool show = d.WaveAge >= 0f;
            if (d.Wave.gameObject.activeSelf != show) d.Wave.gameObject.SetActive(show);
            if (!show) return;
            d.WaveAge += dt;
            float u = d.WaveAge / 1.1f;
            if (u >= 1f)
            {
                d.WaveAge = -1f;
                d.Wave.gameObject.SetActive(false);
                return;
            }
            d.Wave.transform.SetPositionAndRotation(d.Foot, LyingFlat);
            d.Wave.transform.localScale = Vector3.one * ((0.25f + u * 1.5f) * (0.6f + d.WaveStrength * 0.6f));
            block.Clear();
            block.SetColor(ColorId, d.Spec.Color);
            block.SetFloat(RadiusId, 0.85f);
            block.SetFloat(ThicknessId, 0.05f * (1f - u) + 0.01f);
            block.SetFloat(AlphaId, (1f - u) * (1f - u) * d.WaveStrength);
            d.Wave.SetPropertyBlock(block);
        }

        /// <summary>
        /// A taiko boom made in code: a low tone that drops in pitch as it sounds, two overtones so it carries on small
        /// speakers, and a short slap of noise for the hand on the skin.
        /// </summary>
        static AudioClip MakeDrumSound(string name, float pitch, float decay, int seed)
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int length = (int)(rate * Mathf.Clamp(4.5f / decay, 0.5f, 1.6f));
            var data = new float[length];
            var random = new System.Random(seed);
            double phase = 0;
            float noise = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float frequency = pitch * (1f + 0.9f * Mathf.Exp(-t * 30f));
                phase += 2.0 * System.Math.PI * frequency / rate;
                float body = Mathf.Exp(-t * decay);
                float tone = Mathf.Sin((float)phase) * 0.6f
                    + Mathf.Sin((float)(phase * 2.0)) * 0.3f * Mathf.Exp(-t * decay * 0.8f)
                    + Mathf.Sin((float)(phase * 3.01)) * 0.12f * Mathf.Exp(-t * decay * 1.6f);
                noise += ((float)random.NextDouble() * 2f - 1f - noise) * 0.35f;
                float slap = noise * Mathf.Exp(-t * 55f) * 0.6f;
                float attack = Mathf.Min(1f, t / 0.0015f);
                data[i] = (tone * body + slap) * attack * 0.7f;
            }
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        /// <summary>For previews: every drum has arrived, and one (or none, with -1) was just struck.</summary>
        public void PreviewDrums(int struck)
        {
            Build();
            arrived = drums.Length;
            for (int i = 0; i < drums.Length; i++)
            {
                var d = drums[i];
                d.WakeAge = 1f;
                d.Placed = true;
                if (i != struck) continue;
                d.Glow = 1f;
                d.Hit = new Vector4(0.25f, -0.2f, 0.12f, 1f);
                d.WaveAge = 0.25f;
                d.WaveStrength = 1f;
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
