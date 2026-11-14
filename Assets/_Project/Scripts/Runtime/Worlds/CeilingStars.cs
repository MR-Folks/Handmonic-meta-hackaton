using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Ceiling stars world: your ceiling opens onto a deep night sky, and the lyrics become constellations in it.
    /// Each sung word lights up as a star overhead with its word beside it, and a line of light draws across to it from
    /// the word before, so the line of the song is traced in stars. The stars twinkle with the music and pulse on the beat. Point at a star and its
    /// word floats down in front of you to read up close. Each new line forms a new constellation while the last one
    /// fades. With a room scan the sky fills your ceiling; until then it opens a little above your head and fades out
    /// around you.
    /// </summary>
    public class CeilingStars : World
    {
        public override string DisplayName => "Ceiling stars";

        public override Color Color => new Color(0.78f, 0.7f, 1f);

        public override int MenuOrder => 180;

        public override int MenuIcon => 22;

        public override bool IsLyric => true;

        public override string Hint => "Look up, then point at a star";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.1f, 0.08f, 0.2f, 0.5f),
            Edge = new Color(0.72f, 0.65f, 1f),
            Glow = new Color(1f, 0.9f, 0.6f),
            Pattern = HandTheme.Effect.Specks,
            PatternColor = new Color(0.85f, 0.8f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material lineMaterial;
        [SerializeField] Material skyMaterial;

        [Header("Sky")]
        [Tooltip("How far across the ceiling a constellation spreads (m from where you stand), and its width in degrees.")]
        [SerializeField] Vector2 reach = new Vector2(0.9f, 2f);
        [SerializeField] float spread = 50f;
        [SerializeField] float labelHeight = 0.07f;
        [SerializeField] float readHeight = 0.13f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int SoftId = Shader.PropertyToID("_Soft");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        // The sky without a room scan: how wide it is and how softly it fades out around you (m).
        const float OpenSkySize = 10f;
        const float OpenSkySoft = 3f;
        const int StarsPerLine = 14;
        const int Sets = 2;

        sealed class Star
        {
            public Renderer Light;
            public MeshRenderer Label;
            public Mesh Mesh;
            public string Text;
            public Vector3 Position;
            public float Born = -1f;
            public float Twinkle;
        }

        sealed class Constellation
        {
            public readonly Star[] Stars = new Star[StarsPerLine];
            public LineStrip Lines;
            public int Count;
            public float Fading = -1f;
            public float Turn;
        }

        readonly Constellation[] skies = new Constellation[Sets];
        readonly Vector3[] linePath = new Vector3[StarsPerLine * 8];
        readonly Color[] lineColors = new Color[StarsPerLine * 8];
        Material material;
        MaterialPropertyBlock block;
        Renderer nightSky;
        Vector2 skySize;
        float skySoft;
        MeshRenderer reading;
        Mesh readingMesh;
        Star pointed;
        LineStrip pointer;
        readonly Vector3[] pointerPath = new Vector3[2];
        int current = -1;
        float readFade, readHold;
        Vector3 head, forward;
        float ceiling;

        protected override float FadeInSeconds => 1f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            material = new Material(letteringMaterial) { name = "Star Lettering", mainTexture = TypeFace.Bold.Atlas };
            for (int s = 0; s < Sets; s++)
            {
                var sky = new Constellation { Lines = new LineStrip($"Constellation {s}", transform, lineMaterial, linePath.Length) };
                sky.Lines.SetVisible(false);
                for (int i = 0; i < StarsPerLine; i++)
                {
                    var star = new Star { Light = Effects.MakeQuad($"Star {s}.{i}", transform, glowMaterial) };
                    star.Label = Glyphs.MakeLetter($"Star Word {s}.{i}", transform, material, out star.Mesh);
                    star.Light.gameObject.SetActive(false);
                    star.Label.gameObject.SetActive(false);
                    sky.Stars[i] = star;
                }
                skies[s] = sky;
            }
            if (skyMaterial != null)
            {
                nightSky = Effects.MakeQuad("Night Sky", transform, skyMaterial);
                nightSky.gameObject.SetActive(false);
            }
            reading = Glyphs.MakeLetter("Reading", transform, material, out readingMesh);
            reading.gameObject.SetActive(false);
            pointer = new LineStrip("Pointing", transform, lineMaterial, 2);
            pointer.SetVisible(false);
        }

        protected override void OnEnter()
        {
            current = -1;
            foreach (var sky in skies) Clear(sky);
        }

        protected override void OnExit()
        {
            foreach (var sky in skies) Clear(sky);
            reading.gameObject.SetActive(false);
            pointer.SetVisible(false);
            if (nightSky != null) nightSky.gameObject.SetActive(false);
        }

        static void Clear(Constellation sky)
        {
            sky.Count = 0;
            sky.Fading = -1f;
            sky.Lines.SetVisible(false);
            foreach (var star in sky.Stars)
            {
                star.Born = -1f;
                star.Light.gameObject.SetActive(false);
                star.Label.gameObject.SetActive(false);
            }
        }

        /// <summary>Opens the sky over where you stand: on the scanned ceiling, or a little above your head.</summary>
        protected override void OnPlace(Camera view)
        {
            head = view.transform.position;
            forward = Effects.FlatForward(view);
            ceiling = Room.Ceiling(Mathf.Max(head.y + 0.85f, Effects.FloorY(view) + 2.4f));
            PlaceSky();
        }

        /// <summary>Each sung word adds a star; a new line starts a new constellation while the last one fades.</summary>
        protected override void OnWord(string word, bool newLine)
        {
            if (newLine || current < 0)
            {
                if (current >= 0) skies[current].Fading = Clock;
                current = (current + 1) % Sets;
                Clear(skies[current]);
                skies[current].Turn = Random.Range(-spread * 0.4f, spread * 0.4f);
            }
            AddStar(skies[current], word);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Point(rig, view, dt);
            foreach (var sky in skies) Draw(sky, music, view);
            DrawNightSky(music);
            DrawReading(music, view, dt);
        }

        /// <summary>Lays the night sky over the ceiling: across the scanned walls, or centred above you without a scan.</summary>
        void PlaceSky()
        {
            if (nightSky == null) return;
            var center = new Vector3(head.x, ceiling, head.z);
            var along = forward;
            skySize = Vector2.one * OpenSkySize;
            skySoft = OpenSkySoft;
            if (Room.Walls.Count > 0)
            {
                along = Vector3.ProjectOnPlane(Room.Walls[0].Right, Vector3.up).normalized;
                var across = Vector3.Cross(Vector3.up, along);
                Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                foreach (var wall in Room.Walls)
                    for (int end = -1; end <= 1; end += 2)
                    {
                        var p = wall.Center + wall.Right * (end * wall.Size.x * 0.5f);
                        var local = new Vector2(Vector3.Dot(p, across), Vector3.Dot(p, along));
                        min = Vector2.Min(min, local);
                        max = Vector2.Max(max, local);
                    }
                var middle = (min + max) * 0.5f;
                center = across * middle.x + along * middle.y + Vector3.up * ceiling;
                skySize = max - min;
                skySoft = 0.2f;
            }
            // Just under the ceiling, so it never fights with anything at the ceiling's own height.
            nightSky.transform.SetPositionAndRotation(center + Vector3.down * 0.02f, Quaternion.LookRotation(Vector3.up, along));
            nightSky.transform.localScale = new Vector3(skySize.x, skySize.y, 1f);
            nightSky.gameObject.SetActive(true);
        }

        void DrawNightSky(MusicFrame music)
        {
            if (nightSky == null) return;
            block.Clear();
            block.SetVector(SizeId, skySize);
            block.SetFloat(SoftId, skySoft);
            block.SetFloat(AlphaId, FadeIn);
            block.SetFloat(PulseId, music.Kick * 0.5f + music.Band(5) * 0.5f);
            nightSky.SetPropertyBlock(block);
        }

        /// <summary>A new star for the sung word, on a wandering path across the ceiling.</summary>
        void AddStar(Constellation sky, string text)
        {
            if (sky.Count >= StarsPerLine) return;
            int i = sky.Count++;
            var star = sky.Stars[i];
            float u = StarsPerLine > 1 ? i / 7f : 0f;
            float angle = Mathf.Lerp(-spread, spread, Mathf.Repeat(u, 1f)) + sky.Turn + Random.Range(-8f, 8f);
            float distance = Mathf.Lerp(reach.x, reach.y, 0.5f + 0.5f * Mathf.Sin(i * 1.7f + sky.Turn)) + Random.Range(-0.15f, 0.15f);
            var dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            star.Position = new Vector3(head.x, ceiling, head.z) + dir * distance;
            star.Text = text;
            star.Born = Clock;
            star.Twinkle = Random.value * 6.28f;
            Glyphs.BuildWord(star.Mesh, TypeFace.Bold, text, 1f, 1, 0f, Color.white, Color.white);
            star.Light.gameObject.SetActive(true);
            star.Label.gameObject.SetActive(true);
        }

        /// <summary>
        /// Points with the index finger: a ray from the shoulder through the fingertip, the star nearest to it picked.
        /// </summary>
        void Point(HandRig rig, Camera view, float dt)
        {
            Star best = null;
            float bestAngle = 6f;
            Vector3 tipOfBest = Vector3.zero;
            if (rig != null)
            {
                var side = Vector3.Cross(Vector3.up, Effects.FlatForward(view)).normalized;
                for (int s = 0; s < 2; s++)
                {
                    var hand = rig.Get(s == 0);
                    // A finger pointing at the open menu isn't pointing at the sky.
                    if (!hand.IsTracked || hand.InSystemGesture || MenuOpen) continue;
                    var tip = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
                    var shoulder = view.transform.position + Vector3.down * 0.17f + side * (s == 0 ? -0.17f : 0.17f);
                    var dir = (tip - shoulder).normalized;
                    if (dir.y < 0.2f) continue;
                    foreach (var sky in skies)
                    {
                        if (sky.Fading >= 0f) continue;
                        for (int i = 0; i < sky.Count; i++)
                        {
                            float a = Vector3.Angle(dir, sky.Stars[i].Position - tip);
                            if (a < bestAngle) { bestAngle = a; best = sky.Stars[i]; tipOfBest = tip; }
                        }
                    }
                }
            }
            if (best != null)
            {
                Acted("star pick");
                if (best != pointed) Glyphs.BuildWord(readingMesh, TypeFace.Bold, best.Text, 1f, 1, 0f, Color.white, Color.white);
                pointed = best;
                readHold = 1.2f;
                pointerPath[0] = tipOfBest;
                pointerPath[1] = best.Position;
                pointer.SetVisible(true);
                pointer.Set(pointerPath, 2, 0.003f, new Color(0.85f, 0.8f, 1f, 0.35f), view.transform.position);
            }
            else
            {
                pointer.SetVisible(false);
                readHold -= dt;
                if (readHold <= 0f) pointed = null;
            }
        }

        void Draw(Constellation sky, MusicFrame music, Camera view)
        {
            float skyFade = sky.Fading >= 0f ? 1f - Mathf.Clamp01((Clock - sky.Fading) / 3f) : 1f;
            if (sky.Fading >= 0f && skyFade <= 0f) { Clear(sky); return; }
            var eye = view.transform.position;
            float hat = music.Band(5);
            int n = 0;
            for (int i = 0; i < sky.Count; i++)
            {
                var star = sky.Stars[i];
                float age = Clock - star.Born;
                float appear = Mathf.Clamp01(age / 0.4f);
                float twinkle = 0.7f + 0.3f * Mathf.Sin(Clock * (3f + i % 3) + star.Twinkle) + hat * 0.3f;
                bool picked = star == pointed;
                star.Light.transform.position = star.Position;
                star.Light.transform.localScale = Vector3.one * (0.07f + music.Kick * 0.03f + (picked ? 0.05f : 0f)) * appear;
                Effects.Face(star.Light.transform, view);
                block.Clear();
                block.SetColor(ColorId, picked ? new Color(1f, 0.9f, 0.6f) : new Color(0.85f, 0.82f, 1f));
                block.SetFloat(IntensityId, (1.1f * twinkle + (picked ? 0.8f : 0f)) * skyFade * FadeIn);
                star.Light.SetPropertyBlock(block);

                // The word beside its star, turned to you.
                var labelAt = star.Position + Vector3.down * 0.06f;
                var look = labelAt - eye;
                star.Label.transform.SetPositionAndRotation(labelAt, Quaternion.LookRotation(look, Vector3.up));
                star.Label.transform.localScale = Vector3.one * labelHeight;
                block.Clear();
                block.SetColor(ColorId, new Color(0.88f, 0.86f, 1f));
                block.SetFloat(AlphaId, appear * 0.85f * skyFade * FadeIn);
                star.Label.SetPropertyBlock(block);

                // The line from the previous star draws itself across as the word arrives.
                if (i == 0) continue;
                var from = sky.Stars[i - 1].Position;
                float grow = Mathf.Clamp01(age / 0.5f);
                for (int k = 0; k <= 6 && n < linePath.Length; k++)
                {
                    linePath[n] = Vector3.Lerp(from, star.Position, grow * k / 6f);
                    lineColors[n] = new Color(0.75f, 0.72f, 1f, (k == 0 || k == 6 ? 0.1f : 0.45f));
                    n++;
                }
            }
            bool showLines = n >= 2;
            sky.Lines.SetVisible(showLines);
            if (showLines) sky.Lines.Set(linePath, lineColors, n, 0.003f, new Color(1f, 1f, 1f, skyFade * FadeIn), view.transform.position, false);
        }

        /// <summary>The word of the star you point at, floating in front of you to read.</summary>
        void DrawReading(MusicFrame music, Camera view, float dt)
        {
            readFade = Mathf.MoveTowards(readFade, pointed != null ? 1f : 0f, dt * 3f);
            bool show = readFade > 0.01f;
            if (reading.gameObject.activeSelf != show) reading.gameObject.SetActive(show);
            if (!show) return;
            var eye = view.transform.position;
            var at = eye + Effects.FlatForward(view) * 0.7f + Vector3.up * 0.05f;
            if (pointed != null) at = Vector3.Lerp(pointed.Position, at, Mathf.SmoothStep(0f, 1f, readFade));
            reading.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - eye, Vector3.up));
            reading.transform.localScale = Vector3.one * readHeight * (1f + music.Kick * 0.05f);
            block.Clear();
            block.SetColor(ColorId, new Color(1f, 0.92f, 0.75f));
            block.SetFloat(AlphaId, readFade * FadeIn);
            reading.SetPropertyBlock(block);
        }
    }
}
