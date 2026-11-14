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
    /// </summary>
    public class AirStrings : World
    {
        public override string DisplayName => "Air strings";

        public override Color Color => new Color(0.49f, 0.95f, 0.79f);

        public override int MenuOrder => 30;

        public override int MenuIcon => 3;

        public override string Hint => "Brush your fingers across the strings";

        /// <summary>The harp stands in front of you, within reach.</summary>
        public override Space Spaces => Space.InFront;

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

        static readonly int ColorId = Shader.PropertyToID("_Color");
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
        }

        protected override void OnEnter()
        {
            System.Array.Clear(hadSide, 0, hadSide.Length);
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
            if (rig != null) Strum(rig, dt);

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
                    // While the menu is open the fingers are choosing there, not playing.
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
