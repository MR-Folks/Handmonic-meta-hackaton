using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Finger frame world: hold up both hands and a window opens between your thumbs and index fingers, its four
    /// corners on the four fingertips (index to index along the top, thumb to thumb along the bottom), so it takes the
    /// shape your fingers make and bends when you cross them. It looks onto another world: a sunset valley, the deep
    /// sea, a neon city or an arctic night. That world stays put in the room behind the window, so moving the frame or
    /// your head looks around inside it, and it moves with the music (the sun and the jellyfish pulse on the beat, the
    /// city's towers rise with the bands). Pinch and the window holds while the next world swirls in. Sparks drift out
    /// of the window on the beat, and the sung words float inside it.
    /// </summary>
    public class FingerFrame : World
    {
        public override string DisplayName => "Finger frame";

        public override Color Color => new Color(1f, 0.6f, 0.82f);

        public override int MenuOrder => 111;

        public override int MenuIcon => 27;

        public override string Hint => "Frame with both hands; pinch for another world";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.12f, 0.1f, 0.16f, 0.5f),
            Edge = new Color(1f, 0.88f, 0.7f),
            Glow = new Color(1f, 0.6f, 0.85f),
            Pattern = HandTheme.Effect.Film,
            PatternColor = new Color(1f, 0.85f, 0.95f)
        };

        [Header("Materials")]
        [SerializeField] Material portalMaterial;

        [Header("Frame")]
        [Tooltip("Seconds the window stays open after the frame breaks, so a pinch can change the world without closing it.")]
        [SerializeField] float holdOpen = 0.9f;
        [Tooltip("Seconds the next world takes to swirl in.")]
        [SerializeField] float changeTime = 0.8f;

        /// <summary>The worlds behind the window, in the order pinches go through them.</summary>
        public static readonly string[] SceneNames = { "Sunset valley", "Deep sea", "Neon city", "Arctic night" };

        // Each world's colours, for its rim and the sparks that drift out of it.
        static readonly Color[][] ScenePalette =
        {
            new[] { new Color(1f, 0.85f, 0.6f), new Color(1f, 0.55f, 0.75f), new Color(0.6f, 0.5f, 1f) },
            new[] { new Color(0.55f, 0.95f, 1f), new Color(0.4f, 0.75f, 1f), new Color(1f, 0.55f, 0.85f) },
            new[] { new Color(1f, 0.3f, 0.75f), new Color(0.2f, 0.9f, 1f), new Color(1f, 0.85f, 0.3f) },
            new[] { new Color(0.4f, 1f, 0.6f), new Color(0.75f, 0.5f, 1f), new Color(0.85f, 0.92f, 1f) }
        };

        static readonly int SceneAId = Shader.PropertyToID("_SceneA");
        static readonly int SceneBId = Shader.PropertyToID("_SceneB");
        static readonly int MixId = Shader.PropertyToID("_Mix");
        static readonly int OpenId = Shader.PropertyToID("_Open");
        static readonly int OriginId = Shader.PropertyToID("_Origin");
        static readonly int ForwardId = Shader.PropertyToID("_Forward");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int BandsId = Shader.PropertyToID("_Bands");
        static readonly int HighId = Shader.PropertyToID("_High");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");

        // The window's mesh: a grid stretched between the four corners, so it can bend when the fingertips are not
        // on one plane. Each vertex also carries its distance to the nearest edge, for the rim.
        const int Grid = 10;

        Renderer window;
        Mesh windowMesh;
        readonly Vector3[] gridVertices = new Vector3[(Grid + 1) * (Grid + 1)];
        readonly Vector2[] gridEdges = new Vector2[(Grid + 1) * (Grid + 1)];
        MaterialPropertyBlock block;
        ParticleSystem sparks;
        AudioPool voices;
        int scene, nextScene;
        float mix = 1f, open, seen = -99f, heldUntil = -99f;
        bool sounded;
        Vector3 origin, forward;
        // The window's corners, anticlockwise as you see them from the bottom left: on the fingertips, a little behind.
        readonly Vector3[] corners = new Vector3[4];
        readonly Vector3[] found = new Vector3[4];
        readonly Vector3[] drawn = new Vector3[4];
        readonly Vector3[] tips = new Vector3[4];

        Vector3 Center => (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;

        // About how tall the window is.
        float Height => ((corners[3] - corners[0]).magnitude + (corners[2] - corners[1]).magnitude) * 0.5f;

        /// <summary>The world showing in the window now (the one swirling in, during a change).</summary>
        public int Scene => mix < 1f ? nextScene : scene;

        protected override int SungWordCount => 5;

        protected override float SungWordHeight => 0.028f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            window = Effects.MakeQuad("Window", transform, portalMaterial);
            windowMesh = new Mesh { name = "Finger Frame Window" };
            windowMesh.MarkDynamic();
            var uvs = new Vector2[gridVertices.Length];
            var triangles = new int[Grid * Grid * 6];
            for (int y = 0, t = 0; y <= Grid; y++)
            for (int x = 0; x <= Grid; x++)
            {
                uvs[y * (Grid + 1) + x] = new Vector2(x / (float)Grid, y / (float)Grid);
                if (x == Grid || y == Grid) continue;
                int i = y * (Grid + 1) + x;
                triangles[t++] = i; triangles[t++] = i + Grid + 1; triangles[t++] = i + 1;
                triangles[t++] = i + 1; triangles[t++] = i + Grid + 1; triangles[t++] = i + Grid + 2;
            }
            windowMesh.vertices = gridVertices;
            windowMesh.uv = uvs;
            windowMesh.SetUVs(1, gridEdges);
            windowMesh.triangles = triangles;
            window.GetComponent<MeshFilter>().sharedMesh = windowMesh;
            window.gameObject.SetActive(false);
            sparks = Effects.MakeSparkSystem("Window Sparks", transform, glowMaterial);
            voices = new AudioPool(transform, 6);
        }

        protected override void OnEnter()
        {
            open = 0f;
            mix = 1f;
            sounded = false;
            seen = heldUntil = -99f;
        }

        protected override void OnExit() => window.gameObject.SetActive(false);

        void OnDestroy()
        {
            if (windowMesh != null) Discard(windowMesh);
        }

        /// <summary>The world behind the window is anchored where you stand, facing the way you face.</summary>
        protected override void OnPlace(Camera view)
        {
            origin = view.transform.position;
            forward = Effects.FlatForward(view);
        }

        /// <summary>A pinch while the window is open (or just was) brings the next world and holds the window open.</summary>
        protected override void OnPinch(HandRig.Hand hand, bool left)
        {
            if (Clock - seen <= 0.7f) Change(Music);
        }

        /// <summary>On the beat, a few sparks in the world's colours drift out of the window's edge into the room.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            if (open < 0.6f) return;
            var palette = ScenePalette[Scene];
            var center = Center;
            var toEye = (View.transform.position - center).normalized;
            for (int i = 0; i < 10; i++)
            {
                // A random point on the rim, between two fingertips.
                int edge = Random.Range(0, 4);
                var at = Vector3.Lerp(corners[edge], corners[(edge + 1) % 4], Random.value);
                var outward = Vector3.ProjectOnPlane(at - center, toEye).normalized;
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = outward * Random.Range(0.02f, 0.06f) + toEye * Random.Range(0.02f, 0.05f),
                    startColor = palette[Random.Range(0, palette.Length)],
                    startSize = Random.Range(0.006f, 0.014f),
                    startLifetime = Random.Range(0.9f, 1.6f)
                }, 1);
            }
        }

        /// <summary>The sung words float up inside the window, or in front of you while it is closed.</summary>
        protected override void OnWord(string word, bool newLine)
        {
            var eye = View.transform.position;
            var w = SungWords.Add(word, Color.Lerp(ScenePalette[Scene][0], Color.white, 0.5f), 2.2f, alone: true);
            var at = open > 0.5f ? Center + Vector3.down * (Height * 0.25f) : eye + Effects.FlatForward(View) * 0.6f + Vector3.down * 0.12f;
            // Just in front of the window, so the words sit over the other world.
            if (open > 0.5f) at += (eye - at).normalized * 0.01f;
            w.Position = at;
            w.Velocity = Vector3.up * 0.025f;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            bool framed = Frame(rig, view);
            if (framed)
            {
                // Snap to the hands as the window opens; afterwards follow them closely but smoothly.
                float k = open < 0.05f ? 1f : 1f - Mathf.Exp(-30f * dt);
                for (int i = 0; i < 4; i++) corners[i] = Vector3.Lerp(corners[i], found[i], k);
                seen = Clock;
            }

            // A short grace keeps the window from flickering when tracking hesitates for a frame or two.
            bool show = framed || Clock - seen < 0.15f || Clock < heldUntil;
            open = Mathf.MoveTowards(open, show ? 1f : 0f, dt * (show ? 5f : 3.5f));
            if (open > 0.3f && !sounded) { Chord(music); sounded = true; }
            if (open <= 0f) sounded = false;

            if (mix < 1f)
            {
                mix += dt / Mathf.Max(changeTime, 0.05f);
                if (mix >= 1f) { mix = 1f; scene = nextScene; }
            }

            Draw(music);
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                w.Position += w.Velocity * dt;
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.5f + music.Kick * 0.3f;
            }
        }

        /// <summary>
        /// Finds the frame whenever both hands are tracked: its corners are your two thumb tips and two index tips,
        /// wherever they are, and its edges always run index to index, thumb to thumb, and thumb to index on each hand.
        /// So the window takes whatever shape your fingers make, and if you cross your fingers over it twists and bends
        /// with them. Each corner sits 2 cm further away along your line of sight, so the fingers stay in front and the
        /// window still looks pinned to the tips. Puts the corners in <see cref="found"/>.
        /// </summary>
        bool Frame(HandRig rig, Camera view)
        {
            if (rig == null) return false;
            var left = rig.Left;
            var right = rig.Right;
            if (!left.IsTracked || !right.IsTracked || left.InSystemGesture || right.InSystemGesture) return false;

            // Bottom left, bottom right, top right, top left when you hold an ordinary frame.
            tips[0] = Tip(left, 0);
            tips[1] = Tip(right, 0);
            tips[2] = Tip(right, 1);
            tips[3] = Tip(left, 1);
            var eye = view.transform.position;
            var look = view.transform.forward;
            const float behind = 0.02f;
            for (int i = 0; i < 4; i++)
            {
                var d = tips[i] - eye;
                // A fingertip behind or right at your eyes makes no window.
                if (Vector3.Dot(d, look) < 0.05f) return false;
                float distance = d.magnitude;
                found[i] = eye + d * ((distance + behind) / distance);
            }
            return true;
        }

        static Vector3 Tip(HandRig.Hand hand, int finger) =>
            hand.TipJoints[finger] != null ? hand.TipJoints[finger].position : hand.Tips[finger];

        /// <summary>Starts the next world swirling into the window.</summary>
        void Change(MusicFrame music)
        {
            if (mix < 1f) scene = nextScene;
            nextScene = (scene + 1) % SceneNames.Length;
            mix = 0f;
            heldUntil = Clock + holdOpen;
            seen = Clock;
            // A rising pair of bells, fitted to the song.
            int a = Musical.Pitch(music, 76), b = Musical.Pitch(music, 81);
            voices.Play(Synth.Chime(Hz(a)), Center, 0.35f, 1f, Musical.Delay(music));
            voices.Play(Synth.Chime(Hz(b)), Center, 0.3f, 1f, Musical.Delay(music) + 0.11f);
        }

        /// <summary>Three notes ringing out as the window opens.</summary>
        void Chord(MusicFrame music)
        {
            float delay = Musical.Delay(music);
            int[] wanted = { 64, 69, 72 };
            for (int i = 0; i < wanted.Length; i++)
                voices.Play(Synth.Soft(Hz(Musical.Pitch(music, wanted[i]))), Center, 0.3f, 1f, delay + i * 0.08f);
        }

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        void Draw(MusicFrame music)
        {
            bool visible = open > 0.001f;
            if (window.gameObject.activeSelf != visible) window.gameObject.SetActive(visible);
            if (!visible) return;
            ShapeWindow(Mathf.SmoothStep(0f, 1f, open));
            block.Clear();
            block.SetFloat(SceneAId, scene);
            block.SetFloat(SceneBId, nextScene);
            block.SetFloat(MixId, mix < 1f ? mix : 0f);
            block.SetFloat(OpenId, Mathf.SmoothStep(0f, 1f, open));
            block.SetVector(OriginId, origin);
            block.SetVector(ForwardId, forward);
            block.SetFloat(PulseId, music.Kick);
            block.SetVector(BandsId, new Vector4(music.Band(0), music.Band(1), music.Band(2), music.Band(3)));
            block.SetVector(HighId, new Vector4(music.Band(4), music.Band(5), 0f, 0f));
            block.SetFloat(ClockId, Now);
            block.SetColor(EdgeColorId, ScenePalette[Scene][0]);
            window.SetPropertyBlock(block);
        }

        /// <summary>
        /// Lays the window's grid between the corners, grown from the middle as it opens. Each vertex gets its distance
        /// to the nearest edge (metres), which the shader uses for the rim.
        /// </summary>
        void ShapeWindow(float grown)
        {
            var center = Center;
            for (int i = 0; i < 4; i++) drawn[i] = center + (corners[i] - center) * grown;
            var toLocal = window.transform.worldToLocalMatrix;
            for (int y = 0; y <= Grid; y++)
            {
                float v = y / (float)Grid;
                var from = Vector3.Lerp(drawn[0], drawn[3], v);
                var to = Vector3.Lerp(drawn[1], drawn[2], v);
                for (int x = 0; x <= Grid; x++)
                {
                    var p = Vector3.Lerp(from, to, x / (float)Grid);
                    float edge = float.MaxValue;
                    for (int e = 0; e < 4; e++) edge = Mathf.Min(edge, ToSegment(p, drawn[e], drawn[(e + 1) % 4]));
                    int i = y * (Grid + 1) + x;
                    gridVertices[i] = toLocal.MultiplyPoint3x4(p);
                    gridEdges[i] = new Vector2(edge, 0f);
                }
            }
            windowMesh.vertices = gridVertices;
            windowMesh.SetUVs(1, gridEdges);
            windowMesh.RecalculateBounds();
        }

        static float ToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            return (p - a - ab * t).magnitude;
        }

        /// <summary>For previews: opens the window at a place, as if framed by hands, on a given world.</summary>
        public void PreviewWindow(Vector3 at, Quaternion rotation, Vector2 windowSize, int world)
        {
            var across = rotation * Vector3.right * (windowSize.x * 0.5f);
            var up = rotation * Vector3.up * (windowSize.y * 0.5f);
            PreviewWindow(new[] { at - across - up, at + across - up, at + across + up, at - across + up }, world);
        }

        /// <summary>For previews: opens the window between four corners (anticlockwise from the bottom left) on a given world.</summary>
        public void PreviewWindow(Vector3[] windowCorners, int world)
        {
            Build();
            for (int i = 0; i < 4; i++) corners[i] = windowCorners[i];
            scene = nextScene = world % SceneNames.Length;
            mix = 1f;
            open = 1f;
            heldUntil = 1000f;
        }
    }
}
