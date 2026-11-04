using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Waveform ribbon world: pinch with both hands and the music stretches between your fingers as a band of
    /// see-through silk with a glowing thread along each edge, rippling with smooth waves layered from the frequency
    /// bands (bass as long slow waves, highs as short quick ones) and slowly twisting. Its colours keep moving through
    /// a palette, and the inside of the silk moves on to a new look every few beats (see the Silk shader). Pull apart and
    /// it tightens; bring your hands together and it billows. Move your hands and the silk leaves fading copies of itself in the air.
    /// Each beat tugs your visible hands a little towards each other, as if the ribbon pulled back. Let go and it fades away.
    /// </summary>
    public class WaveformRibbon : World
    {
        public override string DisplayName => "Waveform ribbon";

        public override Color Color => new Color(1f, 0.7f, 0.42f);

        public override int MenuOrder => 20;

        public override int MenuIcon => 2;

        public override string Hint => "Pinch with both hands and pull them apart";

        /// <summary>The silk stretches between your hands while they pinch.</summary>
        public override Space Spaces => Space.Hands | Space.Pinch;

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.08f, 0.12f, 0.22f, 0.55f),
            Edge = new Color(0.55f, 0.85f, 1f),
            Glow = new Color(1f, 0.83f, 0.42f),
            Pattern = HandTheme.Effect.Scanlines,
            PatternColor = new Color(0.55f, 0.9f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material lineMaterial;
        [SerializeField] Material silkMaterial;

        [Header("Ribbon")]
        [SerializeField] int points = 96;
        [Tooltip("How far the waveform swings from the straight line, in metres, before tension.")]
        [SerializeField] float amplitude = 0.07f;
        [SerializeField] float strandWidth = 0.004f;
        [Tooltip("Half the width of the silk band (m).")]
        [SerializeField] float silkHalfWidth = 0.024f;
        [Tooltip("How many beats the silk's inside keeps a look before moving on to the next.")]
        [SerializeField] int beatsPerLook = 8;
        [Tooltip("Seconds the ribbon takes to move from one colour of its palette to the next.")]
        [SerializeField] float secondsPerColor = 5f;
        [Tooltip("Pinch needed to grab, and the lower pinch at which a held ribbon is let go.")]
        [SerializeField] float grabPinch = 0.75f;
        [SerializeField] float releasePinch = 0.45f;

        [Header("Feel")]
        [Tooltip("How far each beat tugs the visible hands towards each other, in metres.")]
        [SerializeField] float tug = 0.007f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int KickId = Shader.PropertyToID("_Kick");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ColorAId = Shader.PropertyToID("_ColorA");
        static readonly int ColorBId = Shader.PropertyToID("_ColorB");
        static readonly int LengthId = Shader.PropertyToID("_Length");
        static readonly int LookId = Shader.PropertyToID("_Look");
        static readonly Color[] Palette = { Effects.Gold, Effects.Pink, Effects.Violet, Effects.Blue, Effects.Mint };
        // How far apart in the palette the two hands' colours are.
        const float ColorSpread = 1.2f;
        const int Looks = 3;
        const int Ghosts = 7;
        const float GhostLife = 0.7f;

        /// <summary>
        /// A band of silk as a strip mesh in world space. Several vertices across at each point keep the pattern even
        /// where the band twists and narrows: with only two, it shears across each cell in a visible zigzag.
        /// </summary>
        sealed class Silk
        {
            public const int Across = 6;

            public readonly MeshRenderer Renderer;
            public readonly Vector3[] Vertices;
            public readonly Color[] Colors;
            public float Age = -1f;
            readonly Vector3[] local;
            readonly Mesh mesh;
            readonly Transform transform;

            public Silk(string name, Transform parent, Material material, int points)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                transform = go.transform;
                mesh = new Mesh { name = name };
                mesh.MarkDynamic();
                Vertices = new Vector3[points * Across];
                Colors = new Color[points * Across];
                local = new Vector3[points * Across];
                var uv = new Vector2[points * Across];
                var tris = new int[(points - 1) * (Across - 1) * 6];
                int t = 0;
                for (int i = 0; i < points; i++)
                {
                    float u = i / (float)(points - 1);
                    for (int j = 0; j < Across; j++)
                    {
                        uv[i * Across + j] = new Vector2(u, j / (float)(Across - 1));
                        if (i == points - 1 || j == Across - 1) continue;
                        int v = i * Across + j;
                        tris[t++] = v; tris[t++] = v + Across; tris[t++] = v + 1;
                        tris[t++] = v + 1; tris[t++] = v + Across; tris[t++] = v + Across + 1;
                    }
                }
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.vertices = Vertices;
                mesh.uv = uv;
                mesh.colors = Colors;
                mesh.triangles = tris;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                Renderer = go.AddComponent<MeshRenderer>();
                Renderer.sharedMaterial = material;
                Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Renderer.receiveShadows = false;
            }

            public void Upload()
            {
                for (int i = 0; i < Vertices.Length; i++) local[i] = transform.InverseTransformPoint(Vertices[i]);
                mesh.vertices = local;
                mesh.colors = Colors;
                mesh.RecalculateBounds();
            }

            public void SetVisible(bool visible)
            {
                if (Renderer.gameObject.activeSelf != visible) Renderer.gameObject.SetActive(visible);
            }
        }

        LineStrip[] strands;
        Silk silk;
        Silk[] ghosts;
        Vector3 lastGhostMid;
        float ghostClock;
        int nextGhost;
        Renderer[] grips;
        Vector3[] path, pathB;
        MaterialPropertyBlock block;
        Color colorA = Effects.Gold, colorB = Effects.Pink;
        Vector3 lastA, lastB;
        float hold, ribbonLength, look;
        int beats, lookTarget;
        bool held;
        // Whether the ribbon is drawn this frame (set in OnStep), for the sung words' fade.
        bool showing;

        protected override int SungWordCount => 4;

        protected override float SungWordHeight => 0.028f;

        protected override float SungWordsAlpha => showing ? hold : 0f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            path = new Vector3[points];
            pathB = new Vector3[points];
            strands = new LineStrip[2];
            for (int i = 0; i < strands.Length; i++) strands[i] = new LineStrip($"Strand {i}", transform, lineMaterial, points);
            silk = new Silk("Silk", transform, silkMaterial, points);
            ghosts = new Silk[Ghosts];
            for (int i = 0; i < Ghosts; i++) ghosts[i] = new Silk($"Silk Trail {i}", transform, silkMaterial, points);
            grips = new Renderer[2];
            for (int i = 0; i < grips.Length; i++) grips[i] = Effects.MakeQuad($"Grip {i}", transform, glowMaterial);
            Hide();
        }

        protected override void OnExit()
        {
            held = false;
            hold = 0f;
            if (HandRig.Instance != null)
            {
                Effects.OffsetHand(HandRig.Instance.Left, Vector3.zero);
                Effects.OffsetHand(HandRig.Instance.Right, Vector3.zero);
            }
            Hide();
        }

        void Hide()
        {
            foreach (var s in strands) s.SetVisible(false);
            foreach (var g in grips) g.gameObject.SetActive(false);
            silk.SetVisible(false);
            foreach (var g in ghosts) { g.Age = -1f; g.SetVisible(false); }
        }

        static Vector3 PinchPoint(HandRig.Hand hand)
        {
            var thumb = hand.TipJoints[0] != null ? hand.TipJoints[0].position : hand.Tips[0];
            var index = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
            return (thumb + index) * 0.5f;
        }

        /// <summary>The silk's inside moves on to its next look every few beats.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            if (++beats % Mathf.Max(1, beatsPerLook) == 0) lookTarget++;
        }

        /// <summary>Each sung word rides along the ribbon from your left hand to your right, just above the silk.</summary>
        protected override void OnWord(string word, bool newLine)
        {
            // In the left and the right hand's colours by turns. OnStep moves those (colorA, colorB) along the palette
            // only after the words, so work out this frame's from the clock.
            float colorStep = Now / Mathf.Max(0.1f, secondsPerColor);
            SungWords.Add(word, PaletteColor(Sung.Index % 2 == 0 ? colorStep : colorStep + ColorSpread), 2.6f);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            var left = rig.Left;
            var right = rig.Right;
            bool tracked = left.IsTracked && right.IsTracked;
            float need = held ? releasePinch : grabPinch;
            held = tracked && left.Pinch > need && right.Pinch > need && !left.InSystemGesture && !right.InSystemGesture;
            hold = Mathf.MoveTowards(hold, held ? 1f : 0f, dt * (held ? 6f : 2.5f));

            // The silk's inside moves on every few beats (OnBeat), taking two seconds over each change.
            look = Mathf.MoveTowards(look, lookTarget, dt * 0.5f);

            float colorStep = Now / Mathf.Max(0.1f, secondsPerColor);
            colorA = PaletteColor(colorStep);
            colorB = PaletteColor(colorStep + ColorSpread);

            if (tracked)
            {
                var a = PinchPoint(left);
                var b = PinchPoint(right);
                if (held || hold <= 0f)
                {
                    lastA = a;
                    lastB = b;
                }
            }

            UpdateGrips(rig, music, view);

            showing = hold > 0.001f && view != null;
            foreach (var s in strands) s.SetVisible(showing);
            silk.SetVisible(showing);
            if (showing) DrawRibbon(music, view, dt);
            UpdateGhosts(music, dt);

            // Fake resistance: on each beat the ribbon tugs the visible hands towards each other.
            var pull = (lastB - lastA).sqrMagnitude > 1e-6f ? (lastB - lastA).normalized : Vector3.zero;
            float amount = held ? tug * music.Kick : 0f;
            Effects.OffsetHand(left, pull * amount);
            Effects.OffsetHand(right, -pull * amount);
            Sing(music, view);
        }

        /// <summary>The palette's colour at a step along it, easing from each colour into the next and round again.</summary>
        static Color PaletteColor(float step)
        {
            step = Mathf.Repeat(step, Palette.Length);
            int from = (int)step;
            return Color.Lerp(Palette[from], Palette[(from + 1) % Palette.Length], Mathf.SmoothStep(0f, 1f, step - from));
        }

        /// <summary>Carries each sung word along the ribbon over its life, from the left hand to the right, turned to you.</summary>
        void Sing(MusicFrame music, Camera view)
        {
            if (SungWords == null) return;
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                float t = Mathf.Clamp01(w.Age / w.Life) * (path.Length - 1);
                int i = Mathf.Min((int)t, path.Length - 2);
                w.Position = Vector3.Lerp(path[i], path[i + 1], t - i) + Vector3.up * 0.035f;
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.2f + music.Kick * 0.3f;
            }
        }

        void DrawRibbon(MusicFrame music, Camera view, float dt)
        {
            var a = lastA;
            var b = lastB;
            var mid = (a + b) * 0.5f;
            var along = b - a;
            float length = along.magnitude;
            if (length < 1e-4f) return;
            ribbonLength = length;
            var dir = along / length;
            var side = Vector3.Cross(dir, view.transform.position - mid);
            side = side.sqrMagnitude > 1e-8f ? side.normalized : Vector3.up;
            var depth = Vector3.Cross(dir, side);

            float tension = Mathf.Clamp01(length / 0.7f);
            float amp = amplitude * (1f - tension * 0.55f) * (1f + music.Kick * 0.8f) * hold;
            float t = Now;
            float half = silkHalfWidth * (1f + music.Kick * 0.35f) * hold;
            for (int i = 0; i < points; i++)
            {
                float u = i / (float)(points - 1);
                // Clamped: at the very end the sine dips a hair below zero, and its power below would not be a number.
                float envelope = Mathf.Max(Mathf.Sin(Mathf.PI * u), 0f);
                // Each band adds a smooth wave: low bands long and slow, high bands short and quick. Pulling the
                // ribbon tighter packs the waves closer together.
                float w = 0f;
                for (int q = 0; q < 4; q++)
                    w += music.Band(q + 1) * Mathf.Sin(u * Mathf.PI * (q + 2) * (1f + tension * 1.5f) - t * (4f + q * 2f) + 0.7f);
                var centre = a + along * u + side * (w * amp * envelope * 0.55f);
                // The band twists slowly along its length, narrowing to a point at each hand.
                float twist = Mathf.Sin(u * Mathf.PI * 2f + t * 1.3f) * 1.1f;
                var across = (side * Mathf.Cos(twist) + depth * Mathf.Sin(twist)) * (half * Mathf.Pow(envelope, 0.4f));
                var fade = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, envelope * 4f));
                for (int j = 0; j < Silk.Across; j++)
                {
                    silk.Vertices[i * Silk.Across + j] = centre + across * (j * 2f / (Silk.Across - 1) - 1f);
                    silk.Colors[i * Silk.Across + j] = fade;
                }
                path[i] = centre - across;
                pathB[i] = centre + across;
            }
            silk.Upload();
            SetSilk(silk.Renderer, music, hold);

            for (int s = 0; s < strands.Length; s++)
            {
                var color = s == 0 ? colorA : colorB;
                color.a = hold * 0.9f;
                strands[s].Set(s == 0 ? path : pathB, points, strandWidth * (1f + music.Kick * 0.6f), color, view.transform.position);
            }

            // Moving the ribbon leaves fading copies of the silk behind.
            ghostClock += dt;
            if (held && ghostClock > 0.06f && (mid - lastGhostMid).sqrMagnitude > 0.015f * 0.015f)
            {
                ghostClock = 0f;
                lastGhostMid = mid;
                var ghost = ghosts[nextGhost];
                nextGhost = (nextGhost + 1) % Ghosts;
                System.Array.Copy(silk.Vertices, ghost.Vertices, silk.Vertices.Length);
                System.Array.Copy(silk.Colors, ghost.Colors, silk.Colors.Length);
                ghost.Upload();
                ghost.Age = 0f;
            }
        }

        void UpdateGhosts(MusicFrame music, float dt)
        {
            foreach (var g in ghosts)
            {
                if (g.Age < 0f) continue;
                g.Age += dt;
                float fade = 1f - g.Age / GhostLife;
                if (fade <= 0f)
                {
                    g.Age = -1f;
                    g.SetVisible(false);
                    continue;
                }
                g.SetVisible(true);
                SetSilk(g.Renderer, music, fade * fade * 0.55f);
            }
        }

        void SetSilk(Renderer r, MusicFrame music, float alpha)
        {
            block.Clear();
            block.SetColor(ColorAId, colorA);
            block.SetColor(ColorBId, colorB);
            block.SetFloat(LengthId, ribbonLength);
            block.SetFloat(LookId, Mathf.Repeat(look, Looks));
            block.SetFloat(KickId, music.Kick);
            block.SetFloat(ClockId, Now);
            block.SetFloat(AlphaId, alpha);
            r.SetPropertyBlock(block);
        }

        void UpdateGrips(HandRig rig, MusicFrame music, Camera view)
        {
            for (int i = 0; i < 2; i++)
            {
                var hand = i == 0 ? rig.Left : rig.Right;
                var grip = grips[i];
                bool show = hand.IsTracked;
                if (grip.gameObject.activeSelf != show) grip.gameObject.SetActive(show);
                if (!show) continue;
                // A soft light between thumb and index: dim as a hint when open, bright while holding the ribbon.
                grip.transform.position = PinchPoint(hand);
                Effects.Face(grip.transform, view);
                float strength = Mathf.Lerp(0.25f + hand.Pinch * 0.35f, 1.2f + music.Kick * 0.8f, hold);
                grip.transform.localScale = Vector3.one * (0.03f + 0.02f * hold + music.Kick * 0.012f * hold);
                block.Clear();
                block.SetColor(ColorId, i == 0 ? colorA : colorB);
                block.SetFloat(IntensityId, strength);
                grip.SetPropertyBlock(block);
            }
        }
    }
}
