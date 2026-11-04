using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Firefly choir world: a swarm of fireflies drifts all around you on slow swirling currents, each a bright core
    /// in a soft halo with a small tail that narrows and fades away. They sing in four voices (low to high frequency bands), each voice glowing
    /// with its part of the music, and on every beat a wave of flashes ripples out through the swarm. Hold out a hand
    /// and some of them gather in a loose cloud in the air around it, following it as it moves without ever touching
    /// it; the rest keep filling the room. Each kick startles some of them away. Close your hand and the cloud draws in
    /// closer and swirls faster. Sweep a hand quickly and the fireflies near it are carried along in that direction.
    /// </summary>
    public class FireflyChoir : World
    {
        public override string DisplayName => "Firefly choir";

        public override Color Color => new Color(0.79f, 0.61f, 1f);

        public override int MenuOrder => 50;

        public override int MenuIcon => 5;

        public override string Hint => "Hold out a hand and the fireflies gather around it";

        /// <summary>The swarm fills the air all around you.</summary>
        public override Space Spaces => Space.AroundYou;

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.3f, 0.24f, 0.17f, 0.42f),
            Edge = new Color(1f, 0.78f, 0.45f),
            Glow = new Color(1f, 0.9f, 0.55f),
            Pattern = HandTheme.Effect.Specks,
            PatternColor = new Color(0.42f, 0.34f, 0.22f)
        };

        [Header("Materials")]
        [SerializeField] Material lineMaterial;

        [Header("Swarm")]
        [SerializeField] int count = 88;
        [SerializeField] float flySize = 0.014f;
        [Tooltip("How close to the palm (m) the cloud around a hand keeps, nearest and furthest. A closed hand draws it in further.")]
        [SerializeField] Vector2 cloudReach = new Vector2(0.08f, 0.16f);
        [Tooltip("How wide a tail is where it leaves the firefly, as a share of the firefly's size. The glow fills about half of this.")]
        [SerializeField] float tailWidth = 1.8f;
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
        // The tail keeps where the firefly was over the last quarter second, sampled every TrailStep seconds, and is
        // drawn as a smooth curve through those samples, stopping at TrailLength so fast flights stay small.
        const int TrailPoints = 10;
        const float TrailStep = 0.03f;
        const float TrailLength = 0.06f;
        const int TrailSmooth = 2;
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
            public Vector3 FreeHome;
            public float Phase;
            public float Speed;
            public float Startled;
            public float Landed;
            /// <summary>Its place in the cloud around a hand, as a direction from the palm.</summary>
            public Vector3 Around;
            public int Voice;
        }

        Fly[] flies;
        Vector3[] trailPath;
        Color[] trailColors;
        float[] trailWidths, trailFlown;
        Renderer[] palmGlows;
        MaterialPropertyBlock block;
        Vector3 center, waveOrigin;
        float waveTime = -10f;
        readonly bool[] open = new bool[2];
        readonly bool[] fist = new bool[2];
        readonly float[] landedOn = new float[2];

        protected override int SungWordCount => 5;

        protected override float SungWordHeight => 0.04f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            flies = new Fly[count];
            trailPath = new Vector3[DrawPoints];
            trailColors = new Color[DrawPoints];
            trailWidths = new float[DrawPoints];
            trailFlown = new float[DrawPoints];
            // No white-hot spine: the tail is all soft colour.
            var trailBlock = new MaterialPropertyBlock();
            trailBlock.SetFloat("_Core", 0f);
            var random = new System.Random(11);
            for (int i = 0; i < count; i++)
            {
                var f = new Fly
                {
                    Halo = Effects.MakeQuad($"Firefly Halo {i}", transform, glowMaterial),
                    Core = Effects.MakeQuad($"Firefly {i}", transform, glowMaterial),
                    Trail = new LineStrip($"Firefly Trail {i}", transform, lineMaterial, DrawPoints),
                    History = new Vector3[TrailPoints],
                    FreeHome = RoomSpot(random),
                    Phase = (float)random.NextDouble() * 20f,
                    Speed = 0.6f + (float)random.NextDouble() * 0.8f,
                    Around = CloudSpot(i),
                    Voice = i % VoiceColors.Length
                };
                f.Halo.sortingOrder = -1;
                f.Trail.Renderer.SetPropertyBlock(trailBlock);
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

        protected override void OnExit()
        {
            foreach (var f in flies) SetVisible(f, false);
            foreach (var g in palmGlows) g.gameObject.SetActive(false);
        }

        /// <summary>The swarm lives around you, centred a little below your eyes.</summary>
        protected override void OnPlace(Camera view)
        {
            center = view.transform.position + Vector3.down * 0.25f;
            var nearby = new List<Room.Surface>();
            foreach (var top in Room.Tops)
                if (Vector3.ProjectOnPlane(top.Center - center, Vector3.up).magnitude < 2.4f) nearby.Add(top);
            var perch = new System.Random(5);
            for (int i = 0; i < flies.Length; i++)
            {
                var f = flies[i];
                f.Home = f.FreeHome;
                // With a room scan, some fireflies settle just above your table, sofa and shelves.
                if (nearby.Count > 0 && i % 3 == 1)
                {
                    var top = nearby[i / 3 % nearby.Count];
                    var spot = top.Center + top.Right * (((float)perch.NextDouble() - 0.5f) * top.Size.x * 0.8f)
                        + top.Up * (((float)perch.NextDouble() - 0.5f) * top.Size.y * 0.8f) + Vector3.up * (0.06f + (float)perch.NextDouble() * 0.2f);
                    f.Home = spot - center;
                }
                f.Position = center + f.Home;
                f.Velocity = Vector3.zero;
                for (int k = 0; k < TrailPoints; k++) f.History[k] = f.Position;
                SetVisible(f, true);
            }
        }

        /// <summary>
        /// Each sung word lights up among the fireflies in front of you, glowing in their gold, and floats slowly up
        /// as it fades, the line spreading from left to right.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            var eye = View.transform.position;
            // A new line clears the last one, whose words would otherwise land on top of the new ones.
            var w = SungWords.Add(word, Color.Lerp(Effects.Gold, Color.white, 0.25f), 2.8f, newLine);
            float angle = ((Sung.Index % 5) - 2) * 14f + (w.Seed - 0.5f) * 8f;
            var dir = Quaternion.AngleAxis(angle, Vector3.up) * Effects.FlatForward(View);
            w.Position = eye + dir * 0.75f + Vector3.down * (0.12f - (w.Seed - 0.5f) * 0.1f);
            w.Velocity = Vector3.up * 0.025f;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            float t = Now;
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

                // Only every fourth firefly answers a hand; the rest stay out in the room.
                int caller = i / 4;
                int side = calling == 0 || i % 4 != 0 ? -1 : calling == 2 ? caller % 2 : (open[0] || fist[0] ? 0 : 1);
                var target = Target(rig, f, i, side, t, out float pull);
                if (side >= 0 && Vector3.Distance(f.Position, target) < 0.06f) landedOn[side] += 1f;

                if (f.Startled > 0f) pull *= 0.15f;
                f.Velocity += (target - f.Position) * (pull * dt);
                // Free fireflies ride slow swirling currents rather than heading straight home.
                if (side < 0)
                {
                    f.Velocity += Current(f.Position, t) * dt;
                    Gust(rig, f, dt);
                }
                f.Velocity *= Mathf.Exp(-dt * (side >= 0 ? 6f : 2f));
                f.Position += f.Velocity * dt;
                f.Landed = Mathf.MoveTowards(f.Landed, side >= 0 && Vector3.Distance(f.Position, target) < 0.06f ? 1f : 0f, dt * 4f);

                Draw(f, music, view, t, dt);
            }

            DrawPalms(rig, view);

            // The sung words float slowly up.
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                w.Position += w.Velocity * dt;
                w.Rotation = WordLayer.Facing(w.Position, eye);
                // A firefly's flicker.
                w.Halo = 0.5f + Mathf.Sin(Now * 7f + w.Seed * 20f) * 0.15f + music.Kick * 0.25f;
            }
        }

        Vector3 Target(HandRig rig, Fly f, int i, int side, float t, out float pull)
        {
            if (side < 0)
            {
                pull = 1.2f;
                return center + f.Home + Wobble(f, t, 0.1f);
            }
            // A cloud in the air around the hand: each firefly keeps its own spot in a shell around the palm, the whole
            // cloud turning slowly, so it follows the hand loosely and never sits on it.
            bool closed = fist[side];
            pull = closed ? 9f : 6f;
            float turn = t * (closed ? 70f : 22f);
            float reach = Mathf.Lerp(cloudReach.x, cloudReach.y, Mathf.Repeat(f.Phase * 0.37f, 1f)) * (closed ? 0.65f : 1f);
            return Palm(rig.Get(side == 0)) + Quaternion.AngleAxis(turn, Vector3.up) * f.Around * reach + Wobble(f, t, closed ? 0.008f : 0.012f);
        }

        /// <summary>The n-th of evenly spread directions around a point (a golden spiral over a sphere).</summary>
        static Vector3 CloudSpot(int i)
        {
            float y = 1f - (i / 4 % 24 + 0.5f) / 24f * 2f;
            float ring = Mathf.Sqrt(1f - y * y);
            float angle = i / 4 * 2.39996f;
            return new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring);
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
            f.Halo.transform.localScale = Vector3.one * flySize * (1.7f + glow * 0.8f);
            Effects.Face(f.Halo.transform, view);
            block.Clear();
            block.SetColor(ColorId, color);
            block.SetFloat(IntensityId, glow * 0.45f);
            f.Halo.SetPropertyBlock(block);

            // A small tail along the path just flown: a smooth curve from the firefly's own width down to a point.
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

            // Measure how far back the tail reaches, stopping at TrailLength.
            float flown = 0f;
            int last = 0;
            trailFlown[0] = 0f;
            while (last < DrawPoints - 1 && flown < TrailLength)
            {
                last++;
                float step = Vector3.Distance(trailPath[last], trailPath[last - 1]);
                if (flown + step > TrailLength)
                {
                    trailPath[last] = Vector3.Lerp(trailPath[last - 1], trailPath[last], (TrailLength - flown) / Mathf.Max(step, 1e-5f));
                    step = TrailLength - flown;
                }
                flown += step;
                trailFlown[last] = flown;
            }
            bool moving = flown > 0.004f;
            f.Trail.SetVisible(moving);
            if (!moving) return;

            // Narrowing and fading are measured against this tail's own length, so a short tail ends in a point too.
            float strength = Mathf.Clamp01(0.25f + glow * 0.3f);
            for (int k = 0; k <= last; k++)
            {
                float left = 1f - trailFlown[k] / flown;
                var c = color;
                c.a = strength * left * Mathf.Sqrt(left);
                trailColors[k] = c;
                // Holds its width near the firefly, then curves in to the point.
                trailWidths[k] = left * (2f - left);
            }
            f.Trail.Set(trailPath, trailColors, trailWidths, last + 1, flySize * tailWidth, Color.white, view.transform.position, false);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 + p3 - 3f * p2) * t3);
        }

        /// <summary>A warm glow at the palm that grows with the fireflies gathered around the hand.</summary>
        void DrawPalms(HandRig rig, Camera view)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                var g = palmGlows[side];
                float share = landedOn[side] / Mathf.Max(1f, flies.Length * 0.25f);
                bool show = hand.IsTracked && share > 0.05f;
                if (g.gameObject.activeSelf != show) g.gameObject.SetActive(show);
                if (!show) continue;
                g.transform.position = Palm(hand) + hand.PalmNormal * 0.015f;
                g.transform.localScale = Vector3.one * (0.09f + share * 0.08f);
                Effects.Face(g.transform, view);
                block.Clear();
                block.SetColor(ColorId, VoiceColors[1]);
                block.SetFloat(IntensityId, share * 0.35f);
                g.SetPropertyBlock(block);
            }
        }

        /// <summary>A home for a free firefly: somewhere in a shell around you, from arm's length to across the room.</summary>
        static Vector3 RoomSpot(System.Random random)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float reach = Mathf.Lerp(0.5f, 1.8f, Mathf.Sqrt((float)random.NextDouble()));
            float height = Mathf.Lerp(-0.7f, 0.5f, (float)random.NextDouble());
            return new Vector3(Mathf.Cos(angle) * reach, height, Mathf.Sin(angle) * reach);
        }

        /// <summary>A fast-moving hand sweeps nearby free fireflies along with it.</summary>
        void Gust(HandRig rig, Fly f, float dt)
        {
            if (rig == null) return;
            for (int s = 0; s < 2; s++)
            {
                var hand = rig.Get(s == 0);
                if (!hand.IsTracked) continue;
                float speed = hand.Velocity.magnitude;
                if (speed < 0.6f) continue;
                float d = Vector3.Distance(f.Position, Palm(hand));
                if (d > 0.5f) continue;
                f.Velocity += hand.Velocity * ((1f - d / 0.5f) * Mathf.Min(speed - 0.6f, 1.5f) * 3f * dt);
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
