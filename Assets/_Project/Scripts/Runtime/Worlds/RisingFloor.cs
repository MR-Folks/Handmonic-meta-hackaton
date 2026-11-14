using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Rising floor world: the lyrics grow out of your floor. The words of a line stand in an arc on the floor in
    /// front of you, and each one rises out of it like liquid the moment it is sung, stretching up and settling with
    /// a ripple and a splash at its feet. When the line ends, its words sink back down into the floor. Sweep a hand
    /// across in front of you and the words lean away from it like grass in the wind, then spring back.
    /// </summary>
    public class RisingFloor : World
    {
        public override string DisplayName => "Rising floor";

        public override Color Color => new Color(0.5f, 0.88f, 1f);

        public override int MenuOrder => 160;

        public override int MenuIcon => 20;

        public override bool IsLyric => true;

        public override string Hint => "Sweep your hand across the words";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.06f, 0.12f, 0.2f, 0.5f),
            Edge = new Color(0.45f, 0.85f, 1f),
            Glow = new Color(0.75f, 0.95f, 1f),
            Pattern = HandTheme.Effect.Ripples,
            PatternColor = new Color(0.55f, 0.9f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material ringMaterial;

        [Header("Words")]
        [Tooltip("How far in front of you the arc of words stands (m).")]
        [SerializeField] float distance = 1.3f;
        [SerializeField] float letterHeight = 0.22f;
        [Tooltip("How wide the arc is, in degrees either side.")]
        [SerializeField] float spread = 38f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int FrameId = Shader.PropertyToID("_Frame");
        static readonly int FrameMatrixId = Shader.PropertyToID("_FrameMatrix");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly Color[] LineColors =
        {
            new Color(0.55f, 0.9f, 1f), new Color(0.6f, 1f, 0.85f), new Color(0.75f, 0.75f, 1f), new Color(1f, 0.8f, 0.9f)
        };
        const int MaxWords = 14;

        sealed class Stalk
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public Renderer Ripple;
            public Vector3 Foot;
            public float Width;
            public float Rise;
            public float RiseAt = -1f;
            public float SinkAt = -1f;
            public float RippleAge = -1f;
            public Vector3 Lean;
            public bool Used;
            public int Line;
            public int Index;
            public Color Color;
        }

        readonly List<Stalk> stalks = new List<Stalk>();
        readonly List<Stalk> sinking = new List<Stalk>();
        Material material;
        MaterialPropertyBlock block;
        ParticleSystem splashes;
        Vector3 head, forward, right;
        float floorY;
        int lineNumber;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            material = new Material(letteringMaterial) { name = "Rising Lettering", mainTexture = TypeFace.Bold.Atlas };
            // Two sets of words, so one line can sink while the next one rises.
            for (int i = 0; i < MaxWords * 2; i++)
            {
                var s = new Stalk();
                s.Renderer = Glyphs.MakeLetter($"Word {i}", transform, material, out s.Mesh);
                s.Renderer.gameObject.SetActive(false);
                s.Ripple = Effects.MakeQuad($"Ripple {i}", transform, ringMaterial);
                s.Ripple.gameObject.SetActive(false);
                stalks.Add(s);
            }
            splashes = Effects.MakeSparkSystem("Splashes", transform, glowMaterial);
            var main = splashes.main;
            main.gravityModifier = 0.4f;
        }

        protected override void OnEnter()
        {
            foreach (var s in stalks) Retire(s);
        }

        protected override void OnExit()
        {
            foreach (var s in stalks) Retire(s);
            splashes.Clear();
        }

        static void Retire(Stalk s)
        {
            s.Used = false;
            s.RiseAt = s.SinkAt = s.RippleAge = -1f;
            s.Renderer.gameObject.SetActive(false);
            s.Ripple.gameObject.SetActive(false);
        }

        /// <summary>The words grow out of the floor in front of where you stand.</summary>
        protected override void OnPlace(Camera view)
        {
            head = view.transform.position;
            forward = Effects.FlatForward(view);
            right = Vector3.Cross(Vector3.up, forward).normalized;
            floorY = Effects.FloorY(view);
        }

        protected override void OnWord(string word, bool newLine)
        {
            if (newLine) Plant();
            foreach (var s in stalks)
                if (s.Used && s.SinkAt < 0f && s.RiseAt < 0f && s.Line == lineNumber && s.Index == Sung.Index) Raise(s);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {

            // Anything below the floor is hidden, so words seem to come out of it.
            var toFloor = Matrix4x4.Translate(new Vector3(0f, -(floorY + 1.5f), 0f));
            material.SetMatrix(FrameMatrixId, toFloor);
            material.SetVector(FrameId, new Vector4(1000f, 1.5f, 0.01f, 0f));
            Sweep(rig, dt);
            Draw(music, view, dt);
        }

        /// <summary>
        /// A new line: the words of the last one start sinking, and this line's words are planted, still under the
        /// floor, in an arc in front of you.
        /// </summary>
        void Plant()
        {
            foreach (var s in stalks) if (s.Used && s.SinkAt < 0f) s.SinkAt = Clock;
            lineNumber++;
            var texts = Sung.LineTexts.Length > 0 ? Sung.LineTexts : new[] { Sung.Word };
            int count = Mathf.Min(texts.Length, MaxWords);
            var color = LineColors[lineNumber % LineColors.Length];
            for (int i = 0; i < count; i++)
            {
                Stalk s = null;
                foreach (var x in stalks) if (!x.Used) { s = x; break; }
                if (s == null) break;
                float a = count > 1 ? Mathf.Lerp(-spread, spread, i / (float)(count - 1)) : 0f;
                var dir = Quaternion.AngleAxis(a, Vector3.up) * forward;
                s.Foot = new Vector3(head.x, floorY, head.z) + dir * (distance + (i % 2) * 0.15f);
                s.Width = Glyphs.BuildWord(s.Mesh, TypeFace.Bold, texts[i], 1f, 1, 0f, Color.white, Color.white);
                s.Line = lineNumber;
                s.Index = i;
                s.Used = true;
                s.Rise = 0f;
                s.RiseAt = s.SinkAt = -1f;
                s.Lean = Vector3.zero;
                s.Color = color;
                s.Renderer.gameObject.SetActive(true);
            }
        }

        void Raise(Stalk s)
        {
            s.RiseAt = Clock;
            s.RippleAge = 0f;
            s.Ripple.gameObject.SetActive(true);
            for (int k = 0; k < 16; k++)
            {
                float a = k / 16f * Mathf.PI * 2f;
                splashes.Emit(new ParticleSystem.EmitParams
                {
                    position = s.Foot + Vector3.up * 0.005f,
                    velocity = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.15f + Vector3.up * Random.Range(0.3f, 0.55f),
                    startColor = Color.Lerp(s.Color, Color.white, 0.4f),
                    startSize = Random.Range(0.005f, 0.01f),
                    startLifetime = Random.Range(0.4f, 0.8f)
                }, 1);
            }
        }

        /// <summary>A hand sweeping across in front pushes the words near it to lean away, like grass.</summary>
        void Sweep(HandRig rig, float dt)
        {
            foreach (var s in stalks)
            {
                if (!s.Used) continue;
                var push = Vector3.zero;
                if (rig != null)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        var hand = rig.Get(side == 0);
                        if (!hand.IsTracked) continue;
                        var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                        var flatVelocity = Vector3.ProjectOnPlane(hand.Velocity, Vector3.up);
                        float speed = flatVelocity.magnitude;
                        if (speed < 0.3f) continue;
                        // Distance across the floor from the hand's line of sight down to the word.
                        var flat = Vector3.ProjectOnPlane(s.Foot - palm, Vector3.up);
                        var along = Vector3.ProjectOnPlane(palm - head, Vector3.up).normalized;
                        float off = Vector3.Cross(along, flat.normalized).magnitude * flat.magnitude;
                        if (off > 0.5f) continue;
                        push += flatVelocity / speed * Mathf.Min(speed, 2f) * (1f - off / 0.5f) * 0.6f;
                    }
                }
                s.Lean = Vector3.Lerp(s.Lean, push, 1f - Mathf.Exp(-(push.sqrMagnitude > 0f ? 8f : 2.5f) * dt));
            }
        }

        void Draw(MusicFrame music, Camera view, float dt)
        {
            var eye = view.transform.position;
            foreach (var s in stalks)
            {
                if (!s.Used) continue;
                // Rising: up out of the floor with a stretch that overshoots and settles, like a drop of liquid.
                float target = s.RiseAt < 0f ? 0f : 1f;
                if (s.SinkAt >= 0f) target = 0f;
                float speed = s.SinkAt >= 0f ? 1.6f : 5f;
                s.Rise = Mathf.MoveTowards(s.Rise, target, dt * speed);
                float age = s.RiseAt >= 0f ? Clock - s.RiseAt : 0f;
                float wobble = s.RiseAt >= 0f && s.SinkAt < 0f ? Mathf.Exp(-age * 4f) * Mathf.Sin(age * 18f) * 0.2f : 0f;
                float stretchY = 1f + wobble, stretchX = 1f - wobble * 0.5f;
                if (s.SinkAt >= 0f && s.Rise <= 0f) { Retire(s); continue; }

                float size = letterHeight * (1f + music.Kick * 0.05f);
                // Risen, the word stands clear of the floor, tails of its letters included.
                float sunk = (1f - Mathf.SmoothStep(0f, 1f, s.Rise)) * size * 1.4f - size * 0.25f;
                var look = Vector3.ProjectOnPlane(s.Foot - eye, Vector3.up);
                var facing = look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look, Vector3.up) : Quaternion.identity;
                // Lean from the foot, away from the sweeping hand.
                var leanAxis = Vector3.Cross(Vector3.up, s.Lean);
                var lean = leanAxis.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(Mathf.Min(s.Lean.magnitude * 35f, 40f), leanAxis.normalized) : Quaternion.identity;
                s.Renderer.transform.SetPositionAndRotation(s.Foot + Vector3.down * sunk, lean * facing);
                s.Renderer.transform.localScale = new Vector3(size * stretchX, size * stretchY, size);
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(s.Color, Color.white, 0.35f));
                block.SetFloat(AlphaId, FadeIn);
                s.Renderer.SetPropertyBlock(block);

                if (s.RippleAge >= 0f)
                {
                    s.RippleAge += dt;
                    float u = s.RippleAge / 1.1f;
                    if (u >= 1f) { s.RippleAge = -1f; s.Ripple.gameObject.SetActive(false); }
                    else
                    {
                        s.Ripple.transform.SetPositionAndRotation(s.Foot + Vector3.up * 0.003f, Quaternion.Euler(90f, 0f, 0f));
                        s.Ripple.transform.localScale = Vector3.one * (s.Width * size * 0.6f + u * 0.5f);
                        block.Clear();
                        block.SetColor(ColorId, s.Color);
                        block.SetFloat(RadiusId, 0.8f);
                        block.SetFloat(ThicknessId, 0.03f);
                        block.SetFloat(AlphaId, (1f - u) * (1f - u) * FadeIn);
                        s.Ripple.SetPropertyBlock(block);
                    }
                }
            }
        }
    }
}
