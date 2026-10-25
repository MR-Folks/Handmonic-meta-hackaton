using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Firefly choir world: a swarm of fireflies drifts around you on slow swirling currents, each a bright core
    /// in a soft halo with a fading trail. They sing in four voices (low to high frequency bands), each voice glowing
    /// with its part of the music, and on every beat a wave of flashes ripples out through the swarm. Hold out an open
    /// hand and they come to it, resting on your fingertips and circling your palm, while warm specks twinkle in your
    /// skin. Each kick startles some of them up and away. Close your hand and they swirl around your fist.
    /// </summary>
    public class FireflyChoir : World
    {
        public override int MenuIcon => 5;

        public override string Hint => "Hold out an open hand and let the fireflies land";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.1f, 0.08f, 0.06f, 0.55f),
            Edge = new Color(1f, 0.78f, 0.45f),
            Glow = new Color(1f, 0.9f, 0.55f),
            Pattern = HandTheme.Effect.Specks,
            PatternColor = new Color(1f, 0.85f, 0.45f)
        };

        [Header("Materials")]
        [SerializeField] Material glowMaterial;
        [SerializeField] Material lineMaterial;

        [Header("Swarm")]
        [SerializeField] int count = 44;
        [SerializeField] float cloudDistance = 0.6f;
        [SerializeField] float cloudRadius = 0.45f;
        [SerializeField] float flySize = 0.014f;
        [Tooltip("Share of the swarm that a kick startles.")]
        [SerializeField, Range(0f, 1f)] float startle = 0.35f;
        [Tooltip("How fast the beat's wave of flashes travels through the swarm (m/s).")]
        [SerializeField] float waveSpeed = 1.1f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        // Four voices, low to high: amber, gold, firefly green, cool white.
        static readonly Color[] VoiceColors =
        {
            new Color(1f, 0.55f, 0.25f), new Color(1f, 0.82f, 0.4f), new Color(0.75f, 1f, 0.45f), new Color(0.85f, 0.95f, 1f)
        };
        // The trail keeps where the firefly was over the last quarter second, sampled every TrailStep seconds, and is
        // drawn as a smooth curve through those samples, cut off at TrailLength so fast flights stay short.
        const int TrailPoints = 10;
        const float TrailStep = 0.03f;
        const float TrailLength = 0.06f;
        const int TrailSmooth = 3;
        const int DrawPoints = (TrailPoints - 1) * TrailSmooth + 1;

        sealed class Fly
        {
            public Renderer Core;
            public Renderer Halo;
            public LineStrip Trail;
            public Vector3[] History;
            public float SampleClock;
            public Vector3 Position;
            public Vector3 Velocity;
            public Vector3 Home;
            public float Phase;
            public float Speed;
            public float Startled;
            public float Landed;
            public int Voice;
        }

        Fly[] flies;
        Vector3[] trailPath;
        Color[] trailColors;
        Renderer[] palmGlows;
        MaterialPropertyBlock block;
        Vector3 center, waveOrigin;
        float waveTime = -10f;
        bool anchored, built;
        readonly bool[] open = new bool[2];
        readonly bool[] fist = new bool[2];
        readonly float[] landedOn = new float[2];

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            flies = new Fly[count];
            trailPath = new Vector3[DrawPoints];
            trailColors = new Color[DrawPoints];
            var random = new System.Random(11);
            for (int i = 0; i < count; i++)
            {
                var f = new Fly
                {
                    Halo = Effects.MakeQuad($"Firefly Halo {i}", transform, glowMaterial),
                    Core = Effects.MakeQuad($"Firefly {i}", transform, glowMaterial),
                    Trail = new LineStrip($"Firefly Trail {i}", transform, lineMaterial, DrawPoints),
                    History = new Vector3[TrailPoints],
                    Home = new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1),
                    Phase = (float)random.NextDouble() * 20f,
                    Speed = 0.6f + (float)random.NextDouble() * 0.8f,
                    Voice = i % VoiceColors.Length
                };
                f.Halo.sortingOrder = -1;
                SetVisible(f, false);
                flies[i] = f;
            }
            palmGlows = new[] { Effects.MakeQuad("Palm Glow L", transform, glowMaterial), Effects.MakeQuad("Palm Glow R", transform, glowMaterial) };
            foreach (var g in palmGlows) g.gameObject.SetActive(false);
        }

        static void SetVisible(Fly f, bool visible)
        {
            f.Core.gameObject.SetActive(visible);
            f.Halo.gameObject.SetActive(visible);
            f.Trail.SetVisible(visible);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (flies == null) return;
            foreach (var f in flies) SetVisible(f, false);
            foreach (var g in palmGlows) g.gameObject.SetActive(false);
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            float t = Now;
            if (anchored && ShouldReanchor(view, dt)) anchored = false;
            if (!anchored)
            {
                center = view.transform.position + Effects.FlatForward(view) * cloudDistance + Vector3.down * 0.1f;
                foreach (var f in flies)
                {
                    f.Position = center + f.Home * cloudRadius;
                    f.Velocity = Vector3.zero;
                    for (int k = 0; k < TrailPoints; k++) f.History[k] = f.Position;
                    SetVisible(f, true);
                }
                MarkAnchored(view);
                anchored = true;
            }

            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                open[side] = hand.IsTracked && hand.Fist < 0.45f;
                fist[side] = hand.IsTracked && hand.Fist >= 0.45f;
                landedOn[side] = 0f;
            }
            int calling = (open[0] || fist[0] ? 1 : 0) + (open[1] || fist[1] ? 1 : 0);

            if (music.Beat)
            {
                // The wave of flashes starts from a hand when the swarm is with you, otherwise from its centre.
                waveTime = t;
                waveOrigin = calling > 0 ? Palm(rig.Get(open[0] || fist[0])) : center;
            }

            for (int i = 0; i < flies.Length; i++)
            {
                var f = flies[i];
                if (music.Beat && Random.value < startle)
                {
                    // Startled fireflies dart off sideways around the swarm's middle rather than straight up.
                    var around = Vector3.Cross(Vector3.up, f.Position - center);
                    around = around.sqrMagnitude > 1e-6f ? around.normalized : Random.onUnitSphere;
                    f.Velocity += around * Random.Range(0.2f, 0.4f) + Random.insideUnitSphere * 0.15f;
                    f.Startled = 0.45f;
                }
                f.Startled -= dt;

                int side = calling == 0 ? -1 : calling == 2 ? i % 2 : (open[0] || fist[0] ? 0 : 1);
                var target = Target(rig, f, i, side, t, out float pull);
                if (side >= 0 && Vector3.Distance(f.Position, target) < 0.02f) landedOn[side] += 1f;

                if (f.Startled > 0f) pull *= 0.15f;
                f.Velocity += (target - f.Position) * (pull * dt);
                // Free fireflies ride slow swirling currents rather than heading straight home.
                if (side < 0) f.Velocity += Current(f.Position, t) * dt;
                f.Velocity *= Mathf.Exp(-dt * (side >= 0 ? 6f : 2f));
                f.Position += f.Velocity * dt;
                f.Landed = Mathf.MoveTowards(f.Landed, Vector3.Distance(f.Position, target) < 0.02f ? 1f : 0f, dt * 4f);

                Draw(f, music, view, t, dt);
            }

            DrawPalms(rig, view);
        }

        Vector3 Target(HandRig rig, Fly f, int i, int side, float t, out float pull)
        {
            if (side < 0)
            {
                pull = 1.2f;
                return center + f.Home * cloudRadius + Wobble(f, t, 0.1f);
            }
            var hand = rig.Get(side == 0);
            var palm = Palm(hand);
            if (fist[side])
            {
                pull = 10f;
                float a = t * (1.6f + f.Speed) + f.Phase;
                float radius = 0.08f + (i % 4) * 0.02f;
                var axis = Quaternion.AngleAxis(f.Phase * 40f, Vector3.up) * Vector3.right;
                return palm + Quaternion.AngleAxis(a * Mathf.Rad2Deg, axis) * (Vector3.up * radius);
            }
            pull = 9f;
            int slot = i / 2 % 8;
            if (slot < 5)
            {
                var tip = hand.TipJoints[slot] != null ? hand.TipJoints[slot].position : hand.Tips[slot];
                return tip + hand.PalmNormal * 0.012f + Wobble(f, t, 0.005f);
            }
            float b = t * 0.9f * f.Speed + f.Phase;
            var orbit = Vector3.ProjectOnPlane(new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)), hand.PalmNormal).normalized;
            return palm + hand.PalmNormal * (0.04f + (slot - 5) * 0.02f) + orbit * (0.05f + (slot - 5) * 0.015f);
        }

        void Draw(Fly f, MusicFrame music, Camera view, float t, float dt)
        {
            // The fly's voice glows with its band; its own blink keeps a slow rhythm; the beat's wave flashes it.
            float voice = music.Band(f.Voice + 2);
            float blink = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * (1.6f + f.Speed * 1.4f) + f.Phase), 3f);
            float age = t - waveTime;
            float front = Vector3.Distance(f.Position, waveOrigin) - age * waveSpeed;
            float flash = Mathf.Exp(-front * front / 0.006f) * Mathf.Exp(-age * 0.8f);
            float glow = 0.25f + blink * 0.6f + voice * 0.7f + flash * 1.6f + f.Landed * 0.4f;
            var color = Color.Lerp(VoiceColors[f.Voice], Color.white, flash * 0.4f);

            f.Core.transform.position = f.Position;
            f.Core.transform.localScale = Vector3.one * flySize * (0.9f + flash * 0.6f);
            Effects.Face(f.Core.transform, view);
            block.Clear();
            block.SetColor(ColorId, Color.Lerp(color, Color.white, 0.5f));
            block.SetFloat(IntensityId, 0.6f + glow);
            f.Core.SetPropertyBlock(block);

            f.Halo.transform.position = f.Position;
            f.Halo.transform.localScale = Vector3.one * flySize * (3.2f + glow * 1.6f);
            Effects.Face(f.Halo.transform, view);
            block.Clear();
            block.SetColor(ColorId, color);
            block.SetFloat(IntensityId, glow * 0.45f);
            f.Halo.SetPropertyBlock(block);

            // A soft comet tail along the path the firefly has just flown: a smooth curve that fades to nothing.
            f.SampleClock += dt;
            if (f.SampleClock >= TrailStep)
            {
                f.SampleClock -= TrailStep * Mathf.Floor(f.SampleClock / TrailStep);
                for (int k = TrailPoints - 1; k > 0; k--) f.History[k] = f.History[k - 1];
            }
            f.History[0] = f.Position;
            for (int k = 0; k < TrailPoints - 1; k++)
            {
                var p0 = f.History[Mathf.Max(k - 1, 0)];
                var p1 = f.History[k];
                var p2 = f.History[k + 1];
                var p3 = f.History[Mathf.Min(k + 2, TrailPoints - 1)];
                for (int j = 0; j < TrailSmooth; j++)
                    trailPath[k * TrailSmooth + j] = CatmullRom(p0, p1, p2, p3, j / (float)TrailSmooth);
            }
            trailPath[DrawPoints - 1] = f.History[TrailPoints - 1];
            // Fade along the length flown, and stop the tail at TrailLength.
            float strength = Mathf.Clamp01(0.2f + glow * 0.3f);
            float flown = 0f;
            for (int k = 0; k < DrawPoints; k++)
            {
                if (k > 0)
                {
                    float step = Vector3.Distance(trailPath[k], trailPath[k - 1]);
                    if (flown + step > TrailLength)
                    {
                        trailPath[k] = Vector3.Lerp(trailPath[k - 1], trailPath[k], (TrailLength - flown) / Mathf.Max(step, 1e-5f));
                        step = TrailLength - flown;
                    }
                    flown += step;
                }
                float u = flown / TrailLength;
                var c = color;
                c.a = strength * (1f - u) * (1f - u);
                trailColors[k] = c;
            }
            f.Trail.Set(trailPath, trailColors, DrawPoints, flySize * 0.22f, Color.white, view.transform.position, false);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 + p3 - 3f * p2) * t3);
        }

        /// <summary>A warm glow on the palm that grows with the fireflies resting on the hand.</summary>
        void DrawPalms(HandRig rig, Camera view)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                var g = palmGlows[side];
                float share = landedOn[side] / Mathf.Max(1f, flies.Length * 0.5f);
                bool show = hand.IsTracked && share > 0.05f;
                if (g.gameObject.activeSelf != show) g.gameObject.SetActive(show);
                if (!show) continue;
                g.transform.position = Palm(hand) + hand.PalmNormal * 0.015f;
                g.transform.localScale = Vector3.one * (0.09f + share * 0.08f);
                Effects.Face(g.transform, view);
                block.Clear();
                block.SetColor(ColorId, VoiceColors[1]);
                block.SetFloat(IntensityId, share * 0.7f);
                g.SetPropertyBlock(block);
            }
        }

        static Vector3 Palm(HandRig.Hand hand) => hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;

        /// <summary>A slow, swirling current: smooth in space and time, so the swarm drifts in lazy eddies.</summary>
        static Vector3 Current(Vector3 p, float t) => new Vector3(
            Mathf.Sin(p.y * 3.1f + t * 0.4f) + Mathf.Sin(p.z * 2.3f - t * 0.3f),
            Mathf.Sin(p.z * 2.7f + t * 0.35f) * 0.6f,
            Mathf.Sin(p.x * 2.9f - t * 0.45f) + Mathf.Sin(p.y * 1.7f + t * 0.25f)) * 0.08f;

        static Vector3 Wobble(Fly f, float t, float size) => new Vector3(
            Mathf.Sin(t * 0.9f * f.Speed + f.Phase),
            Mathf.Sin(t * 1.3f * f.Speed + f.Phase * 1.7f),
            Mathf.Sin(t * 0.7f * f.Speed + f.Phase * 2.3f)) * size;
    }
}
