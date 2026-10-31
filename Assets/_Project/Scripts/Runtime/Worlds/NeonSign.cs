using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Neon sign world: the lyrics as a neon sign in front of you. Each line appears as unlit glass tubes, and
    /// each word switches on with a flicker and a buzz the moment it is sung, in the line's colour, until the whole
    /// line glows; then it switches off for the next one. The sign throws its light on the wall behind it and on
    /// the floor below, brighter on the beat. Touch a word and it buzzes and sparks. With a room scan the sign hangs
    /// on a wall within reach; otherwise it stands about a metre in front of you.
    /// </summary>
    public class NeonSign : World
    {
        public override int MenuIcon => 18;

        public override bool IsLyric => true;

        public override string Hint => "Touch a word to make it buzz";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.16f, 0.07f, 0.14f, 0.55f),
            Edge = new Color(1f, 0.35f, 0.7f),
            Glow = new Color(0.4f, 0.9f, 1f),
            Pattern = HandTheme.Effect.Electric,
            PatternColor = new Color(1f, 0.5f, 0.8f)
        };

        [Header("Materials")]
        [SerializeField] Material letteringMaterial;
        [SerializeField] Material glowMaterial;
        [SerializeField] Material lineMaterial;

        [Header("Sign")]
        [SerializeField] float distance = 1.0f;
        [Tooltip("Height of the sign's middle relative to the eyes (m).")]
        [SerializeField] float height = -0.05f;
        [SerializeField] float letterHeight = 0.1f;
        [SerializeField] float maxWidth = 1.1f;

        static readonly Color[] LineColors =
        {
            new Color(1f, 0.25f, 0.6f), new Color(0.3f, 0.9f, 1f), new Color(1f, 0.68f, 0.22f), new Color(0.72f, 0.45f, 1f)
        };
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int HaloId = Shader.PropertyToID("_Halo");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        const int MaxWords = 16;

        sealed class Word
        {
            public string Text;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public Vector2 Size;
            public Vector3 Centre;
            public float LitAt = -1f;
            public float Buzz;
            public bool Used;
        }

        readonly List<Word> words = new List<Word>();
        readonly LyricCursor cursor = new LyricCursor();
        readonly Vector3[] framePath = new Vector3[5];
        Material material;
        MaterialPropertyBlock block;
        Renderer wallGlow, floorGlow;
        LineStrip frame;
        AudioPool voices;
        ParticleSystem sparks;
        Vector3 anchor, forward, right;
        Vector2 frameSize;
        Color lineColor = LineColors[0];
        int lineNumber;
        float floorY, clock, fade, scale = 1f;
        bool anchored, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            material = new Material(letteringMaterial) { name = "Neon Lettering", mainTexture = TypeFace.Bold.Atlas };
            for (int i = 0; i < MaxWords; i++)
            {
                var word = new Word();
                word.Renderer = Glyphs.MakeLetter($"Word {i}", transform, material, out word.Mesh);
                word.Renderer.gameObject.SetActive(false);
                words.Add(word);
            }
            wallGlow = Effects.MakeQuad("Wall Glow", transform, glowMaterial);
            floorGlow = Effects.MakeQuad("Floor Glow", transform, glowMaterial);
            frame = new LineStrip("Tube Frame", transform, lineMaterial, framePath.Length);
            voices = new AudioPool(transform, 4);
            sparks = Effects.MakeSparkSystem("Sparks", transform, glowMaterial);
            SetSignVisible(false);
        }

        void SetSignVisible(bool visible)
        {
            wallGlow.gameObject.SetActive(visible);
            floorGlow.gameObject.SetActive(visible);
            frame.SetVisible(visible);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
            fade = 0f;
            cursor.Reset();
            if (built) foreach (var w in words) { w.Used = false; w.Renderer.gameObject.SetActive(false); }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (!built) return;
            foreach (var w in words) w.Renderer.gameObject.SetActive(false);
            SetSignVisible(false);
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            if (anchored && ShouldReanchor(view, dt)) anchored = false;
            if (!anchored)
            {
                forward = Effects.FlatForward(view);
                right = Vector3.Cross(Vector3.up, forward).normalized;
                anchor = view.transform.position + forward * distance + Vector3.up * height;
                scale = 1f;
                // Once the room is scanned, a wall within reach takes the sign, a few centimetres off it, and the
                // letters grow with the distance. Farther walls are out of reach for touching words, so the sign
                // stays in front of you.
                if (Room.WallAhead(view.transform.position, forward, distance * 1.6f, out var wall, out var onWall))
                {
                    forward = -wall.Normal;
                    right = Vector3.Cross(Vector3.up, forward).normalized;
                    anchor = onWall + wall.Normal * 0.05f + Vector3.up * height;
                    scale = Mathf.Max(1f, Vector3.Distance(view.transform.position, anchor) / distance);
                }
                floorY = Effects.FloorY(view);
                SetSignVisible(true);
                MarkAnchored(view);
                anchored = true;
            }
            clock += dt;
            fade = Mathf.MoveTowards(fade, 1f, dt * 1.5f);

            if (cursor.Advance(music, out bool newLine))
            {
                if (newLine) LayOut(music);
                LightUp(cursor.Index);
            }
            if (rig != null) Touch(rig);
            Draw(music, view);
        }

        /// <summary>Lays a new line out as unlit tubes, wrapping onto a second row when it is too wide.</summary>
        void LayOut(MusicFrame music)
        {
            lineColor = LineColors[lineNumber++ % LineColors.Length];
            var texts = new List<string>(cursor.LineTexts);
            if (texts.Count == 0) texts.Add(cursor.Word);

            float lh = letterHeight * scale;
            float space = lh * 0.45f;
            var rows = new List<List<int>> { new List<int>() };
            var widths = new List<float> { 0f };
            for (int i = 0; i < words.Count; i++)
            {
                var w = words[i];
                w.Used = i < texts.Count;
                w.LitAt = -1f;
                w.Buzz = 0f;
                w.Renderer.gameObject.SetActive(w.Used);
                if (!w.Used) continue;
                w.Text = texts[i];
                // Slanted like italics, and centred on the middle of the letters.
                float width = Glyphs.BuildWord(w.Mesh, TypeFace.Bold, w.Text, lh, 1, 0f, Color.white, Color.white, 0.2f, -lh * 0.36f);
                w.Size = new Vector2(width, lh * 1.1f);
                int row = rows.Count - 1;
                if (widths[row] > 0f && widths[row] + space + width > maxWidth * scale)
                {
                    rows.Add(new List<int>());
                    widths.Add(0f);
                    row++;
                }
                widths[row] += (widths[row] > 0f ? space : 0f) + width;
                rows[row].Add(i);
            }
            float rowHeight = lh * 1.5f;
            float top = (rows.Count - 1) * rowHeight * 0.5f;
            float widest = 0f;
            for (int r = 0; r < rows.Count; r++)
            {
                float x = -widths[r] * 0.5f;
                widest = Mathf.Max(widest, widths[r]);
                foreach (int i in rows[r])
                {
                    var w = words[i];
                    w.Centre = anchor + right * (x + w.Size.x * 0.5f) + Vector3.up * (top - r * rowHeight);
                    x += w.Size.x + space;
                    w.Renderer.transform.SetPositionAndRotation(w.Centre, Quaternion.LookRotation(forward, Vector3.up));
                }
            }
            frameSize = new Vector2(widest * 0.5f + lh * 0.6f, top + lh * 1.0f);
        }

        void LightUp(int index)
        {
            if (index < 0 || index >= words.Count || !words[index].Used) return;
            var w = words[index];
            w.LitAt = clock;
            if (Application.isPlaying) voices.Play(Synth.Buzz(), w.Centre, 0.18f);
        }

        /// <summary>A fingertip on a word makes it buzz, flicker and spark.</summary>
        void Touch(HandRig rig)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                if (!hand.IsTracked) continue;
                for (int k = 1; k < 3; k++)
                {
                    var tip = hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k];
                    foreach (var w in words)
                    {
                        if (!w.Used || w.Buzz > 0.5f) continue;
                        var d = tip - w.Centre;
                        if (Mathf.Abs(Vector3.Dot(d, forward)) > 0.08f) continue;
                        if (Mathf.Abs(Vector3.Dot(d, right)) > w.Size.x * 0.5f + 0.02f || Mathf.Abs(d.y) > w.Size.y * 0.5f + 0.02f) continue;
                        w.Buzz = 1f;
                        if (w.LitAt < 0f) w.LitAt = clock;
                        voices.Play(Synth.Buzz(), tip, 0.3f);
                        for (int s = 0; s < 14; s++)
                            sparks.Emit(new ParticleSystem.EmitParams
                            {
                                position = tip,
                                velocity = Random.insideUnitSphere * 0.25f + Vector3.down * 0.1f,
                                startColor = Color.Lerp(lineColor, Color.white, 0.5f),
                                startSize = Random.Range(0.003f, 0.007f),
                                startLifetime = Random.Range(0.3f, 0.7f)
                            }, 1);
                    }
                }
            }
        }

        void Draw(MusicFrame music, Camera view)
        {
            int lit = 0, used = 0;
            foreach (var w in words)
            {
                if (!w.Used) continue;
                used++;
                w.Buzz = Mathf.MoveTowards(w.Buzz, 0f, Time.deltaTime * 2f);
                float on = 0f;
                if (w.LitAt >= 0f)
                {
                    float age = clock - w.LitAt;
                    // Switching on: a few stutters, then steady, with a faint hum flicker after.
                    on = age < 0.3f ? (Mathf.Sin(age * 90f) > 0.2f ? 1f : 0.15f) : 0.93f + 0.07f * Mathf.Sin(clock * 50f + w.Centre.x * 30f);
                    lit++;
                }
                if (w.Buzz > 0f) on *= Mathf.Sin(clock * 70f) > -0.3f ? 1f : 0.2f;
                var tube = Color.Lerp(new Color(0.42f, 0.4f, 0.46f), lineColor, on);
                block.Clear();
                block.SetColor(ColorId, new Color(tube.r * (0.5f + on), tube.g * (0.5f + on), tube.b * (0.5f + on), 0.35f + on * 0.65f));
                block.SetFloat(HaloId, on * (0.85f + music.Kick * 0.3f));
                block.SetFloat(AlphaId, fade);
                w.Renderer.SetPropertyBlock(block);
            }

            float glow = used > 0 ? lit / (float)used : 0f;
            wallGlow.transform.SetPositionAndRotation(anchor + forward * 0.04f, Quaternion.LookRotation(forward, Vector3.up));
            wallGlow.transform.localScale = new Vector3(frameSize.x * 3.2f, frameSize.y * 3.6f, 1f);
            block.Clear();
            block.SetColor(ColorId, lineColor);
            block.SetFloat(IntensityId, (0.12f + glow * 0.3f + music.Kick * 0.08f) * fade);
            wallGlow.SetPropertyBlock(block);

            var below = new Vector3(anchor.x, floorY + 0.004f, anchor.z) - forward * 0.3f;
            floorGlow.transform.SetPositionAndRotation(below, Quaternion.LookRotation(Vector3.down, forward));
            floorGlow.transform.localScale = new Vector3(frameSize.x * 3.5f, 1.4f, 1f);
            block.Clear();
            block.SetColor(ColorId, lineColor);
            block.SetFloat(IntensityId, (0.1f + glow * 0.35f + music.Kick * 0.12f) * fade);
            floorGlow.SetPropertyBlock(block);

            // A thin tube frame around the sign, lit with the line.
            var c = anchor - forward * 0.005f;
            framePath[0] = c + right * -frameSize.x + Vector3.up * frameSize.y;
            framePath[1] = c + right * frameSize.x + Vector3.up * frameSize.y;
            framePath[2] = c + right * frameSize.x - Vector3.up * frameSize.y;
            framePath[3] = c + right * -frameSize.x - Vector3.up * frameSize.y;
            framePath[4] = framePath[0];
            var fc = lineColor;
            fc.a = (0.25f + glow * 0.5f) * fade;
            frame.Set(framePath, framePath.Length, 0.006f, fc, view.transform.position, false);
        }
    }
}
