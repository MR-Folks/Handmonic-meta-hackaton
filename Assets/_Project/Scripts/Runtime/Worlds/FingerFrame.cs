using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Finger frame world: make a frame with both hands, each thumb and index finger an L, and a window opens
    /// between them onto another world: a sunset valley, the deep sea, a neon city or an arctic night. That world stays
    /// put in the room behind the window, so moving the frame or your head looks around inside it, and it moves with
    /// the music (the sun and the jellyfish pulse on the beat, the city's towers rise with the bands). Pinch, which
    /// breaks the frame for a moment, and the window holds while the next world swirls in. Sparks drift out of the window
    /// on the beat, and the sung words float inside it.
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
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int OriginId = Shader.PropertyToID("_Origin");
        static readonly int ForwardId = Shader.PropertyToID("_Forward");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int BandsId = Shader.PropertyToID("_Bands");
        static readonly int HighId = Shader.PropertyToID("_High");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");

        Renderer window;
        MaterialPropertyBlock block;
        ParticleSystem sparks;
        AudioPool voices;
        int scene, nextScene;
        float mix = 1f, open, seen = -99f, heldUntil = -99f;
        bool sounded;
        Vector3 center, origin, forward;
        Quaternion facing = Quaternion.identity;
        Vector2 size = new Vector2(0.3f, 0.2f);

        /// <summary>The world showing in the window now (the one swirling in, during a change).</summary>
        public int Scene => mix < 1f ? nextScene : scene;

        protected override int SungWordCount => 5;

        protected override float SungWordHeight => 0.028f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            window = Effects.MakeQuad("Window", transform, portalMaterial);
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
            var toEye = (View.transform.position - center).normalized;
            var across = facing * Vector3.right;
            var up = facing * Vector3.up;
            for (int i = 0; i < 10; i++)
            {
                // A random point on the rim.
                float u = Random.value * 2f - 1f;
                bool sides = Random.value < size.y / (size.x + size.y);
                var local = sides ? new Vector2(Mathf.Sign(u) * 0.5f, Random.value - 0.5f) : new Vector2(u * 0.5f, Random.value < 0.5f ? -0.5f : 0.5f);
                var at = center + across * (local.x * size.x) + up * (local.y * size.y);
                var outward = (across * local.x + up * local.y).normalized;
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
            var at = open > 0.5f ? center - facing * Vector3.up * (size.y * 0.25f) : eye + Effects.FlatForward(View) * 0.6f + Vector3.down * 0.12f;
            // Just in front of the window, so the words sit over the other world.
            if (open > 0.5f) at += (eye - at).normalized * 0.01f;
            w.Position = at;
            w.Velocity = Vector3.up * 0.025f;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            bool framed = Frame(rig, view, out var c, out var q, out var s);
            if (framed)
            {
                // Snap to the hands as the window opens; afterwards follow them closely but smoothly.
                float k = open < 0.05f ? 1f : 1f - Mathf.Exp(-30f * dt);
                center = Vector3.Lerp(center, c, k);
                facing = Quaternion.Slerp(facing, q, k);
                size = Vector2.Lerp(size, s, k);
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
        /// Finds the frame: both hands tracked, each with thumb and index apart in an L and the middle finger folded
        /// (so two open hands don't count), the left hand on the left, both at about the same distance. The window is
        /// the rectangle around the four fingertips as you see them, a little behind them so the fingers stay in front.
        /// </summary>
        bool Frame(HandRig rig, Camera view, out Vector3 c, out Quaternion q, out Vector2 s)
        {
            c = default;
            q = Quaternion.identity;
            s = default;
            if (rig == null) return false;
            var left = rig.Left;
            var right = rig.Right;
            if (!left.IsTracked || !right.IsTracked || left.InSystemGesture || right.InSystemGesture) return false;
            if (left.Pinch > 0.35f || right.Pinch > 0.35f) return false;
            if (!IsL(left) || !IsL(right)) return false;

            var eye = view.transform.position;
            Vector3 lt = Tip(left, 0), li = Tip(left, 1), rt = Tip(right, 0), ri = Tip(right, 1);
            var mid = (lt + li + rt + ri) * 0.25f;
            var normal = (mid - eye).normalized;
            var across = Vector3.ProjectOnPlane(view.transform.right, normal).normalized;
            var up = Vector3.Cross(normal, across);
            float depth = Vector3.Dot(mid - eye, normal);
            if (depth < 0.15f) return false;
            float dl = Vector3.Dot(left.Palm.position - eye, normal), dr = Vector3.Dot(right.Palm.position - eye, normal);
            if (Mathf.Abs(dl - dr) > 0.25f) return false;

            // Every point as you see it, on the plane through the middle of the fingertips.
            Vector2 Seen(Vector3 p)
            {
                var d = p - eye;
                var onPlane = eye + d * (depth / Mathf.Max(Vector3.Dot(d, normal), 0.05f)) - mid;
                return new Vector2(Vector3.Dot(onPlane, across), Vector3.Dot(onPlane, up));
            }
            if (Seen(right.Palm.position).x - Seen(left.Palm.position).x < 0.08f) return false;

            Vector2 a = Seen(lt), b = Seen(li), c2 = Seen(rt), d = Seen(ri);
            var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c2, d));
            var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c2, d));
            var extent = max - min;
            if (extent.x < 0.08f || extent.y < 0.05f || extent.x > 0.9f || extent.y > 0.7f) return false;

            // Set the window 2 cm behind the fingertips and scale it up to match, so it looks the same size.
            const float behind = 0.02f;
            float grow = (depth + behind) / depth;
            var middle = (min + max) * 0.5f;
            c = eye + (mid + across * middle.x + up * middle.y - eye) * grow;
            q = Quaternion.LookRotation(normal, up);
            s = extent * grow;
            return true;
        }

        static bool IsL(HandRig.Hand hand)
        {
            var palm = hand.Palm.position;
            Vector3 thumb = Tip(hand, 0), index = Tip(hand, 1), middle = Tip(hand, 2);
            if (Vector3.Distance(thumb, index) < 0.05f) return false;
            if (Vector3.Angle(index - palm, thumb - palm) < 35f) return false;
            return Vector3.Distance(middle, palm) < Vector3.Distance(index, palm) - 0.015f;
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
            voices.Play(Synth.Chime(Hz(a)), center, 0.35f, 1f, Musical.Delay(music));
            voices.Play(Synth.Chime(Hz(b)), center, 0.3f, 1f, Musical.Delay(music) + 0.11f);
        }

        /// <summary>Three notes ringing out as the window opens.</summary>
        void Chord(MusicFrame music)
        {
            float delay = Musical.Delay(music);
            int[] wanted = { 64, 69, 72 };
            for (int i = 0; i < wanted.Length; i++)
                voices.Play(Synth.Soft(Hz(Musical.Pitch(music, wanted[i]))), center, 0.3f, 1f, delay + i * 0.08f);
        }

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        void Draw(MusicFrame music)
        {
            bool visible = open > 0.001f;
            if (window.gameObject.activeSelf != visible) window.gameObject.SetActive(visible);
            if (!visible) return;
            window.transform.SetPositionAndRotation(center, facing);
            window.transform.localScale = new Vector3(size.x, size.y, 1f);
            block.Clear();
            block.SetFloat(SceneAId, scene);
            block.SetFloat(SceneBId, nextScene);
            block.SetFloat(MixId, mix < 1f ? mix : 0f);
            block.SetFloat(OpenId, Mathf.SmoothStep(0f, 1f, open));
            block.SetVector(SizeId, size);
            block.SetVector(OriginId, origin);
            block.SetVector(ForwardId, forward);
            block.SetFloat(PulseId, music.Kick);
            block.SetVector(BandsId, new Vector4(music.Band(0), music.Band(1), music.Band(2), music.Band(3)));
            block.SetVector(HighId, new Vector4(music.Band(4), music.Band(5), 0f, 0f));
            block.SetFloat(ClockId, Now);
            block.SetColor(EdgeColorId, ScenePalette[Scene][0]);
            window.SetPropertyBlock(block);
        }

        /// <summary>For previews: opens the window at a place, as if framed by hands, on a given world.</summary>
        public void PreviewWindow(Vector3 at, Quaternion rotation, Vector2 windowSize, int world)
        {
            Build();
            center = at;
            facing = rotation;
            size = windowSize;
            scene = nextScene = world % SceneNames.Length;
            mix = 1f;
            open = 1f;
            heldUntil = 1000f;
        }
    }
}
