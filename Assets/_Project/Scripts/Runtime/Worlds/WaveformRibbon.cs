using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Waveform ribbon world: pinch with both hands and the music stretches between your fingers as three
    /// glowing strands: smooth waves layered from the frequency bands (bass as long slow waves, highs as short quick
    /// ones), as on the concept page. Pull apart and it tightens; bring your hands together and it billows. Each beat
    /// tugs your visible hands a little towards each other, as if the ribbon pulled back. Let go and it fades away.
    /// </summary>
    public class WaveformRibbon : World
    {
        public override int MenuIcon => 2;

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
        [SerializeField] Material glowMaterial;

        [Header("Ribbon")]
        [SerializeField] int points = 96;
        [Tooltip("How far the waveform swings from the straight line, in metres, before tension.")]
        [SerializeField] float amplitude = 0.07f;
        [SerializeField] float strandWidth = 0.007f;
        [Tooltip("Pinch needed to grab, and the lower pinch at which a held ribbon is let go.")]
        [SerializeField] float grabPinch = 0.75f;
        [SerializeField] float releasePinch = 0.45f;

        [Header("Feel")]
        [Tooltip("How far each beat tugs the visible hands towards each other, in metres.")]
        [SerializeField] float tug = 0.007f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly Color[] StrandColors = { Effects.Gold, Effects.Pink, Effects.Blue };

        LineStrip[] strands;
        Renderer[] grips;
        Vector3[] path;
        MaterialPropertyBlock block;
        Vector3 lastA, lastB;
        float hold;
        bool held, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            path = new Vector3[points];
            strands = new LineStrip[StrandColors.Length];
            for (int i = 0; i < strands.Length; i++) strands[i] = new LineStrip($"Strand {i}", transform, lineMaterial, points);
            grips = new Renderer[2];
            for (int i = 0; i < grips.Length; i++) grips[i] = Effects.MakeQuad($"Grip {i}", transform, glowMaterial);
            Hide();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            held = false;
            hold = 0f;
            if (HandRig.Instance != null)
            {
                Effects.OffsetHand(HandRig.Instance.Left, Vector3.zero);
                Effects.OffsetHand(HandRig.Instance.Right, Vector3.zero);
            }
            if (built) Hide();
        }

        void Hide()
        {
            foreach (var s in strands) s.SetVisible(false);
            foreach (var g in grips) g.gameObject.SetActive(false);
        }

        static Vector3 PinchPoint(HandRig.Hand hand)
        {
            var thumb = hand.TipJoints[0] != null ? hand.TipJoints[0].position : hand.Tips[0];
            var index = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
            return (thumb + index) * 0.5f;
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            var left = rig.Left;
            var right = rig.Right;
            bool tracked = left.IsTracked && right.IsTracked;
            float need = held ? releasePinch : grabPinch;
            held = tracked && left.Pinch > need && right.Pinch > need && !left.InSystemGesture && !right.InSystemGesture;
            hold = Mathf.MoveTowards(hold, held ? 1f : 0f, dt * (held ? 6f : 2.5f));

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

            bool show = hold > 0.001f && view != null;
            foreach (var s in strands) s.SetVisible(show);
            if (show) DrawRibbon(music, view);

            // Fake resistance: on each beat the ribbon tugs the visible hands towards each other.
            var pull = (lastB - lastA).sqrMagnitude > 1e-6f ? (lastB - lastA).normalized : Vector3.zero;
            float amount = held ? tug * music.Kick : 0f;
            Effects.OffsetHand(left, pull * amount);
            Effects.OffsetHand(right, -pull * amount);
        }

        void DrawRibbon(MusicFrame music, Camera view)
        {
            var a = lastA;
            var b = lastB;
            var mid = (a + b) * 0.5f;
            var along = b - a;
            float length = along.magnitude;
            if (length < 1e-4f) return;
            var dir = along / length;
            var side = Vector3.Cross(dir, view.transform.position - mid);
            side = side.sqrMagnitude > 1e-8f ? side.normalized : Vector3.up;
            var depth = Vector3.Cross(dir, side);

            float tension = Mathf.Clamp01(length / 0.7f);
            float amp = amplitude * (1f - tension * 0.55f) * (1f + music.Kick * 0.8f) * hold;
            float t = Now;
            for (int s = 0; s < strands.Length; s++)
            {
                for (int i = 0; i < points; i++)
                {
                    float u = i / (float)(points - 1);
                    float envelope = Mathf.Sin(Mathf.PI * u);
                    // Each band adds a smooth wave: low bands long and slow, high bands short and quick. Pulling the
                    // ribbon tighter packs the waves closer together.
                    float w = 0f;
                    for (int q = 0; q < 4; q++)
                        w += music.Band(q + 1) * Mathf.Sin(u * Mathf.PI * (q + 2) * (1f + tension * 1.5f) - t * (4f + q * 2f) + s * 0.7f);
                    float twist = Mathf.Sin(u * Mathf.PI * 2f + t * (1.3f + s * 0.4f) + s * 2.1f);
                    var offset = side * (w * amp * envelope * 0.55f) + depth * (twist * amp * 0.25f * envelope);
                    path[i] = a + along * u + offset;
                }
                var color = StrandColors[s];
                color.a = hold * 0.9f;
                strands[s].Set(path, points, strandWidth * (1f + music.Kick * 0.6f), color, view.transform.position);
            }
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
                block.SetColor(ColorId, i == 0 ? Effects.Gold : Effects.Pink);
                block.SetFloat(IntensityId, strength);
                grip.SetPropertyBlock(block);
            }
        }
    }
}
