using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Orbit type world: poster-style motion graphics floating in your room, in white and soft grey. The word being
    /// sung fills the poster in giant letters that run off its edges and drift slowly sideways; the whole line sits
    /// small above it with the sung word lit. Thin orbit lines and rings of dots turn to the music, and soft discs
    /// breathe with the bass. Push the letters with your fingers, flick a dotted ring to spin it, or make a fist to pull
    /// the word towards your hand. Your hands are printed in halftone dots.
    /// </summary>
    public class OrbitType : World
    {
        public override int MenuIcon => 12;

        public override bool IsLyric => true;

        public override string Hint => "Push the letters, flick a ring, or make a fist";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.12f, 0.12f, 0.14f, 0.5f),
            Edge = new Color(0.88f, 0.89f, 0.93f),
            Glow = new Color(0.79f, 0.61f, 1f),
            Pattern = HandTheme.Effect.Halftone,
            PatternColor = new Color(0.95f, 0.95f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material letteringMaterial;
        [SerializeField] Material lineMaterial;
        [SerializeField] Material glowMaterial;

        [Header("Poster")]
        [SerializeField] float distance = 1.15f;
        [Tooltip("Height of the poster's centre relative to the eyes (m).")]
        [SerializeField] float height = -0.05f;
        [Tooltip("Half width and half height of the poster frame the letters are cropped to (m).")]
        [SerializeField] Vector2 frame = new Vector2(0.62f, 0.42f);
        [Tooltip("Height of the giant letters (m).")]
        [SerializeField] float letterHeight = 0.56f;
        [SerializeField] float drift = 0.035f;

        [Header("Touch")]
        [SerializeField] float pushRadius = 0.14f;
        [SerializeField] float pullRadius = 0.7f;

        static readonly Color Ink = new Color(0.95f, 0.95f, 0.97f);
        static readonly Color Soft = new Color(0.86f, 0.87f, 0.92f);
        static readonly Color Touch = new Color(0.79f, 0.61f, 1f);
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        static readonly int CoreId = Shader.PropertyToID("_Core");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int FrameId = Shader.PropertyToID("_Frame");
        static readonly int FrameMatrixId = Shader.PropertyToID("_FrameMatrix");

        const int MaxLetters = 14;
        const int FontSize = 160;
        const int DotsPerRing = 30;
        const int Scatter = 30;

        // Dotted rings in poster space: centre, radius (m), turning direction.
        static readonly (Vector2 center, float radius, float dir)[] Rings =
        {
            (new Vector2(0.14f, 0.08f), 0.11f, 1f),
            (new Vector2(0.3f, -0.2f), 0.1f, -1f),
            (new Vector2(-0.26f, -0.24f), 0.07f, 1f)
        };

        sealed class Letter
        {
            public TextMesh Text;
            public MeshRenderer Renderer;
            public float X;
            public float Width;
            public Vector2 Offset;
        }

        Transform poster;
        Font font;
        Material material;
        readonly List<Letter> letters = new List<Letter>();
        TextMesh line;
        MeshRenderer lineRenderer;
        LineStrip[] orbits;
        Vector3[] path;
        Renderer[] discs;
        ParticleSystem dots;
        ParticleSystem.Particle[] dotOut;
        Vector2[] scatter;
        int[] scatterBand;
        readonly float[] spin = new float[3];
        readonly float[] spinVelocity = new float[3];
        readonly float[] ringTouch = new float[3];
        readonly Vector3[,] lastTips = new Vector3[2, 5];
        MaterialPropertyBlock block;
        string word = "", lineKey = "";
        float wordTime, fade, clock;
        int fallbackIndex = -1;
        bool anchored, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            material = new Material(letteringMaterial) { name = "Orbit Lettering", mainTexture = font.material.mainTexture };
            Font.textureRebuilt += OnFontRebuilt;

            poster = new GameObject("Poster").transform;
            poster.SetParent(transform, false);

            for (int i = 0; i < MaxLetters; i++)
            {
                var text = MakeText($"Letter {i}", FontSize, letterHeight, TextAnchor.LowerCenter, FontStyle.Bold);
                letters.Add(new Letter { Text = text, Renderer = text.GetComponent<MeshRenderer>() });
            }
            line = MakeText("Line", 64, 0.032f, TextAnchor.MiddleLeft, FontStyle.Normal);
            line.richText = true;
            lineRenderer = line.GetComponent<MeshRenderer>();

            discs = new[] { Effects.MakeQuad("Disc Big", poster, glowMaterial), Effects.MakeQuad("Disc Small", poster, glowMaterial) };
            orbits = new LineStrip[3];
            path = new Vector3[97];
            for (int i = 0; i < orbits.Length; i++) orbits[i] = new LineStrip($"Orbit {i}", poster, lineMaterial, path.Length);

            var go = new GameObject("Dots");
            go.transform.SetParent(transform, false);
            dots = go.AddComponent<ParticleSystem>();
            dots.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = dots.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Rings.Length * DotsPerRing + Scatter;
            var emission = dots.emission;
            emission.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dotOut = new ParticleSystem.Particle[Rings.Length * DotsPerRing + Scatter];
            var random = new System.Random(3);
            scatter = new Vector2[Scatter];
            scatterBand = new int[Scatter];
            for (int i = 0; i < Scatter; i++)
            {
                scatter[i] = new Vector2(((float)random.NextDouble() * 2f - 1f) * 0.84f, ((float)random.NextDouble() * 2f - 1f) * 0.8f);
                scatterBand[i] = random.Next(1, 6);
            }
            poster.gameObject.SetActive(false);
        }

        TextMesh MakeText(string objectName, int fontSize, float height, TextAnchor anchor, FontStyle style)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(poster, false);
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            // TextMesh glyphs are fontSize * characterSize / 10 units tall.
            text.characterSize = height * 10f / fontSize;
            text.anchor = anchor;
            text.alignment = TextAlignment.Left;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return text;
        }

        void OnFontRebuilt(Font rebuilt)
        {
            if (rebuilt == font && material != null) material.mainTexture = font.material.mainTexture;
        }

        void OnDestroy() => Font.textureRebuilt -= OnFontRebuilt;

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
            fade = 0f;
            word = "";
            lineKey = "";
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (poster != null) poster.gameObject.SetActive(false);
            if (dots != null) dots.Clear();
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            if (anchored && ShouldReanchor(view, dt)) anchored = false;
            if (!anchored)
            {
                var forward = Effects.FlatForward(view);
                poster.SetPositionAndRotation(view.transform.position + forward * distance + Vector3.up * height,
                    Quaternion.LookRotation(forward, Vector3.up));
                poster.gameObject.SetActive(true);
                MarkAnchored(view);
                anchored = true;
            }
            fade = Mathf.MoveTowards(fade, 1f, dt * 1.5f);
            clock += dt;

            UpdateWord(music);
            float age = clock - wordTime;
            LayoutLetters(rig, age, dt);
            DrawLine(music);
            DrawOrbits(music);
            DrawDiscs(music);
            SpinRings(rig, dt);
            DrawDots(music);

            material.SetVector(FrameId, new Vector4(frame.x, frame.y, 0.08f, 0f));
            material.SetMatrix(FrameMatrixId, poster.worldToLocalMatrix);
            material.SetFloat(AlphaId, fade);
        }

        /// <summary>The sung word in capitals; without lyrics, the song's title words change every four beats.</summary>
        void UpdateWord(MusicFrame music)
        {
            string next = music.Word;
            if (string.IsNullOrEmpty(next))
            {
                var title = music.Bus != null ? music.Bus.Title : "Music";
                var parts = title.Split(' ');
                int beat = music.Bus != null ? music.Bus.BeatCount / 4 : 0;
                if (music.LineWords == null)
                {
                    if (beat != fallbackIndex)
                    {
                        fallbackIndex = beat;
                        next = parts[beat % parts.Length];
                    }
                    else next = word;
                }
                else next = word;
            }
            next = next.ToUpperInvariant();
            if (next == word) return;
            word = next;
            wordTime = clock;

            font.RequestCharactersInTexture(word, FontSize, FontStyle.Bold);
            float scale = letterHeight / FontSize;
            float x = 0f;
            for (int i = 0; i < letters.Count; i++)
            {
                var letter = letters[i];
                bool used = i < word.Length;
                letter.Text.gameObject.SetActive(used);
                if (!used) continue;
                char ch = word[i];
                letter.Text.text = ch.ToString();
                font.GetCharacterInfo(ch, out var info, FontSize, FontStyle.Bold);
                letter.Width = info.advance * scale;
                letter.X = x + letter.Width * 0.5f;
                letter.Offset = Vector2.zero;
                x += letter.Width;
            }
        }

        /// <summary>
        /// Giant letters along the poster's bottom, drifting left and cropped by the frame. Fingers push them aside;
        /// a fist pulls them towards the hand. Each letter springs back on its own.
        /// </summary>
        void LayoutLetters(HandRig rig, float age, float dt)
        {
            float enter = Mathf.Clamp01(age * 5f);
            float start = -frame.x * 0.92f - age * drift + (1f - enter) * 0.24f;
            // Low enough that the frame crops the bottom of the letters, like a poster.
            float baseline = -frame.y * 1.14f;
            bool touched = false;
            for (int i = 0; i < word.Length && i < letters.Count; i++)
            {
                var letter = letters[i];
                var home = new Vector2(start + letter.X, baseline + letterHeight * 0.35f);
                var target = Vector2.zero;
                if (rig != null)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        var hand = rig.Get(side == 0);
                        if (!hand.IsTracked) continue;
                        var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                        var palmLocal = poster.InverseTransformPoint(palm);
                        if (hand.Fist > 0.6f)
                        {
                            var toHand = (Vector2)palmLocal - home;
                            if (toHand.magnitude < pullRadius) target += toHand * 0.35f;
                            continue;
                        }
                        for (int k = 0; k < 6; k++)
                        {
                            var point = k < 5 ? (hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k]) : palm;
                            var local = poster.InverseTransformPoint(point);
                            if (Mathf.Abs(local.z) > 0.2f) continue;
                            var away = home - (Vector2)local;
                            float d = away.magnitude;
                            float reach = k < 5 ? pushRadius : pushRadius * 1.4f;
                            if (d >= reach) continue;
                            target += away / Mathf.Max(d, 0.01f) * (reach - d) * 0.8f;
                            touched = true;
                        }
                    }
                }
                letter.Offset = Vector2.Lerp(letter.Offset, target, 1f - Mathf.Exp(-12f * dt));
                var p = home + letter.Offset;
                letter.Text.transform.localPosition = new Vector3(p.x, p.y - letterHeight * 0.35f, 0f);
                letter.Text.color = new Color(Ink.r, Ink.g, Ink.b, enter);
            }
            HandSkin.FocusStrength = touched ? 1f : 0f;
        }

        /// <summary>The whole line, small, above the giant word, with the sung word lit.</summary>
        void DrawLine(MusicFrame music)
        {
            var words = music.LineWords;
            string key = words == null ? "" : $"{music.Lyric.Line}:{music.Lyric.Word}";
            if (key != lineKey)
            {
                lineKey = key;
                if (words == null) line.text = "";
                else
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < words.Length; i++)
                    {
                        if (i > 0) sb.Append(' ');
                        if (i == music.Lyric.Word) sb.Append("<color=#ffffff>").Append(words[i].Text).Append("</color>");
                        else sb.Append("<color=#ffffff66>").Append(words[i].Text).Append("</color>");
                    }
                    line.text = sb.ToString();
                }
            }
            line.transform.localPosition = new Vector3(-frame.x * 0.84f + 0.05f, frame.y * 0.12f, -0.01f);
            line.color = new Color(1f, 1f, 1f, fade);
        }

        /// <summary>Thin orbit lines: two circles high on the right and a wide arc low across the poster.</summary>
        void DrawOrbits(MusicFrame music)
        {
            var eye = Camera.main != null ? Camera.main.transform.position : poster.position - poster.forward;
            float t = clock;
            for (int o = 0; o < orbits.Length; o++)
            {
                Vector2 center;
                Vector2 radius;
                float from = 0f, to = Mathf.PI * 2f, tilt = 0f;
                if (o == 0) { center = new Vector2(0.16f, 0.36f); radius = Vector2.one * 0.24f * (1f + music.Kick * 0.04f); }
                else if (o == 1) { center = new Vector2(0.32f, 0.4f); radius = Vector2.one * 0.14f; }
                else { center = new Vector2(0.1f, -0.66f); radius = new Vector2(0.62f, 0.46f); from = 0.55f; to = 2.95f; tilt = 0.12f; }
                for (int i = 0; i < path.Length; i++)
                {
                    float a = Mathf.Lerp(from, to, i / (float)(path.Length - 1)) + (o == 2 ? 0f : t * 0.05f);
                    var local = new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y);
                    local = new Vector2(local.x * Mathf.Cos(tilt) - local.y * Mathf.Sin(tilt), local.x * Mathf.Sin(tilt) + local.y * Mathf.Cos(tilt));
                    path[i] = poster.TransformPoint(new Vector3(center.x + local.x, center.y + local.y, 0.03f));
                }
                var c = Soft;
                c.a = (o == 2 ? 0.55f : 0.4f + music.Kick * 0.2f) * fade;
                orbits[o].Set(path, path.Length, o == 2 ? 0.0028f : 0.0022f, c, eye, o == 2);
            }
        }

        /// <summary>Two soft grey discs behind everything, breathing with the bass and the kick.</summary>
        void DrawDiscs(MusicFrame music)
        {
            for (int i = 0; i < discs.Length; i++)
            {
                var d = discs[i];
                d.transform.localPosition = i == 0 ? new Vector3(-0.12f, -0.04f, 0.05f) : new Vector3(0.24f, 0.16f, 0.05f);
                d.transform.localRotation = Quaternion.identity;
                float r = i == 0 ? 0.2f * (1f + music.Band(1) * 0.08f) : 0.09f * (1f + music.Kick * 0.12f);
                d.transform.localScale = Vector3.one * r * 2.2f;
                block.Clear();
                block.SetColor(ColorId, new Color(0.78f, 0.8f, 0.84f));
                block.SetFloat(IntensityId, (i == 0 ? 0.16f : 0.12f) * fade);
                block.SetFloat(SoftnessId, 0.35f);
                block.SetFloat(CoreId, 0f);
                d.SetPropertyBlock(block);
            }
        }

        /// <summary>A fingertip moving along a dotted ring spins it that way; the spin eases back to a slow turn.</summary>
        void SpinRings(HandRig rig, float dt)
        {
            for (int r = 0; r < Rings.Length; r++)
            {
                ringTouch[r] = Mathf.MoveTowards(ringTouch[r], 0f, dt * 2f);
                spinVelocity[r] *= Mathf.Exp(-dt * 1.2f);
            }
            if (rig != null && dt > 0f)
            {
                for (int side = 0; side < 2; side++)
                {
                    var hand = rig.Get(side == 0);
                    for (int k = 0; k < 5; k++)
                    {
                        var tip = hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k];
                        var last = lastTips[side, k];
                        lastTips[side, k] = tip;
                        if (!hand.IsTracked) continue;
                        var local = poster.InverseTransformPoint(tip);
                        if (Mathf.Abs(local.z) > 0.08f) continue;
                        var move = (Vector2)(poster.InverseTransformPoint(tip) - poster.InverseTransformPoint(last)) / dt;
                        for (int r = 0; r < Rings.Length; r++)
                        {
                            var fromCenter = (Vector2)local - Rings[r].center;
                            if (Mathf.Abs(fromCenter.magnitude - Rings[r].radius) > 0.025f) continue;
                            var tangent = new Vector2(-fromCenter.y, fromCenter.x).normalized;
                            float push = Vector2.Dot(move, tangent) / Rings[r].radius;
                            spinVelocity[r] = Mathf.Lerp(spinVelocity[r], Mathf.Clamp(push, -12f, 12f), 0.5f);
                            ringTouch[r] = 1f;
                        }
                    }
                }
            }
            for (int r = 0; r < Rings.Length; r++) spin[r] += (0.3f * Rings[r].dir + spinVelocity[r]) * dt;
        }

        void DrawDots(MusicFrame music)
        {
            int n = 0;
            float hat = music.Band(5);
            for (int r = 0; r < Rings.Length; r++)
            {
                var ring = Rings[r];
                float radius = ring.radius * (1f + music.Band(r + 2) * 0.15f);
                var color = Color.Lerp(new Color(0.92f, 0.93f, 0.96f, 0.85f), Touch, ringTouch[r]);
                color.a *= fade;
                for (int i = 0; i < DotsPerRing; i++)
                {
                    float a = i / (float)DotsPerRing * Mathf.PI * 2f + spin[r];
                    var local = new Vector3(ring.center.x + Mathf.Cos(a) * radius, ring.center.y + Mathf.Sin(a) * radius, -0.005f);
                    dotOut[n++] = Dot(local, (0.006f + hat * 0.006f + ringTouch[r] * 0.003f), color);
                }
            }
            for (int i = 0; i < Scatter; i++)
            {
                var local = new Vector3(scatter[i].x * frame.x, scatter[i].y * frame.y, 0.02f);
                dotOut[n++] = Dot(local, 0.005f * (1f + music.Band(scatterBand[i]) * 0.9f), new Color(0.92f, 0.93f, 0.96f, 0.6f * fade));
            }
            dots.SetParticles(dotOut, n);
        }

        ParticleSystem.Particle Dot(Vector3 local, float size, Color color) => new ParticleSystem.Particle
        {
            position = poster.TransformPoint(local),
            startSize = size,
            startColor = color,
            startLifetime = 10f,
            remainingLifetime = 10f
        };
    }
}
