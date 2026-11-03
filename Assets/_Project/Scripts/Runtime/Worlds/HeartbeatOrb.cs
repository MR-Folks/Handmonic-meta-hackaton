using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Heartbeat orb world: hold the song between your hands. The orb forms just beyond your palms, sized by how
    /// far apart they are, and turns as you turn your hands, like holding a ball. Its shell is a cloud of glowing dots
    /// on a 3D supershape (the superformula) whose petals sharpen as the song gets louder and grow or lose a petal
    /// every eight beats; each kick sends a ripple across it. Inside sits a small glowing heart. Three torus-knot
    /// trails of light wind around it with comets racing along them, energy threads flow from your fingertips into
    /// it, and motes drift off its surface. Your hands glow with heat on the side facing it, each kick nudges your
    /// visible hands apart (fake resistance), and spreading your hands turns the music up.
    /// </summary>
    public class HeartbeatOrb : World
    {
        public override string DisplayName => "Heartbeat orb";

        public override Color Color => new Color(1f, 0.83f, 0.42f);

        public override int MenuOrder => 10;

        public override int MenuIcon => 1;

        public override string Hint => "Hold your palms facing each other, then turn them like a ball";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.22f, 0.1f, 0.2f, 0.55f),
            Edge = new Color(1f, 0.72f, 0.45f),
            Glow = new Color(1f, 0.37f, 0.68f),
            Pattern = HandTheme.Effect.Heat,
            PatternColor = new Color(1f, 0.6f, 0.35f)
        };

        [Header("Materials")]
        [WorldMaterial("OrbDots")]
        [SerializeField] Material dotsMaterial;
        [SerializeField] Material orbMaterial;
        [SerializeField] Material lineMaterial;

        [Header("Orb")]
        [SerializeField] float minRadius = 0.05f;
        [SerializeField] float maxRadius = 0.2f;
        [Tooltip("Orb radius as a share of the distance between the palms.")]
        [SerializeField] float radiusPerSpread = 0.5f;
        [Tooltip("How far beyond the palms, away from the player's face, the orb floats (m).")]
        [SerializeField] float forwardOffset = 0.1f;
        [SerializeField] float beatSwell = 0.12f;
        [Tooltip("Petal counts the shell steps through, one every eight beats.")]
        [SerializeField] int[] petals = { 3, 4, 5, 6, 4, 7 };

        [Header("Feel")]
        [Tooltip("How far each kick pushes the visible hands apart, in metres.")]
        [SerializeField] float push = 0.012f;
        [Tooltip("Palm distance (m) mapped to volume: close together is quiet, wide apart is loud.")]
        [SerializeField] Vector2 spreadRange = new Vector2(0.12f, 0.6f);
        [SerializeField] Vector2 volumeRange = new Vector2(0.3f, 1f);
        [SerializeField] int motesPerBeat = 16;
        [SerializeField] float motesPerSecond = 12f;
        [Tooltip("Fingertip distance from the orb's centre (m) up to which its thread is fully lit, and where it has faded away.")]
        [SerializeField] Vector2 threadReach = new Vector2(0.7f, 0.9f);

        static readonly int KickId = Shader.PropertyToID("_Kick");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int LobesId = Shader.PropertyToID("_Lobes");
        static readonly int SpinId = Shader.PropertyToID("_Spin");
        static readonly int BandId = Shader.PropertyToID("_Band");
        static readonly int BandsId = Shader.PropertyToID("_Bands");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int MorphId = Shader.PropertyToID("_Morph");
        static readonly int EnergyId = Shader.PropertyToID("_Energy");
        static readonly int WaveId = Shader.PropertyToID("_Wave");
        static readonly int ClockId = Shader.PropertyToID("_Clock");

        static readonly Color Pink = new Color(1f, 0.37f, 0.68f);
        static readonly Color Violet = new Color(0.62f, 0.56f, 1f);
        static readonly Color Blue = new Color(0.5f, 0.72f, 1f);
        static readonly Color Gold = new Color(1f, 0.83f, 0.42f);
        static readonly Color[] MoteColors = { Pink, Violet, Blue, Gold };

        // The heart inside the shell, from outside in: size, colour, petals, spin, band, base opacity.
        static readonly (float size, Color color, float lobes, float spin, int band, float opacity)[] Layers =
        {
            (0.34f, Pink, 3f, 1.6f, 1, 0.22f)
        };

        // Torus knots wound around the shell: (p, q) winding, colour, comet speed (loops per second at rest).
        static readonly (int p, int q, Color color, float speed)[] Knots =
        {
            (2, 3, new Color(1f, 0.72f, 0.2f), 0.22f),
            (3, 5, new Color(1f, 0.25f, 0.6f), -0.16f),
            (2, 5, new Color(0.35f, 0.6f, 1f), 0.19f)
        };

        const int DotCount = 1500;
        const int HeartGrid = 24;
        const int KnotPoints = 180;
        const int TendrilPoints = 28;

        Transform orb, halo, core, dots;
        Transform[] layers;
        Renderer[] layerRenderers;
        Renderer haloRenderer, coreRenderer, dotsRenderer;
        LineStrip[] knots, tendrils;
        Vector3[] path;
        Color[] pathColors;
        float[] knotPhase;
        readonly float[] tendrilFlow = new float[10];
        ParticleSystem motes;
        MaterialPropertyBlock block;
        Vector3 center, lastAxis;
        Quaternion spin = Quaternion.identity;
        Vector3 angularVelocity;
        float radius, visibility, moteDebt, morph = -1f, energy, waveAge = 9f;
        int beats;
        bool hasCenter, hasAxis;
        float savedVolume = -1f;

        protected override int SungWordCount => 3;

        protected override float SungWordHeight => 0.036f;

        protected override float SungWordsAlpha => visibility;

        protected override void OnEnter()
        {
            if (MusicBus.Instance != null) savedVolume = MusicBus.Instance.Volume;
        }

        protected override void OnExit()
        {
            if (MusicBus.Instance != null && savedVolume >= 0f) MusicBus.Instance.Volume = savedVolume;
            savedVolume = -1f;
            hasCenter = hasAxis = false;
            visibility = 0f;
            if (HandRig.Instance != null)
            {
                Effects.OffsetHand(HandRig.Instance.Left, Vector3.zero);
                Effects.OffsetHand(HandRig.Instance.Right, Vector3.zero);
            }
            if (orb != null) orb.gameObject.SetActive(false);
            if (tendrils != null) foreach (var t in tendrils) t.SetVisible(false);
            if (knots != null) foreach (var k in knots) k.SetVisible(false);
            if (motes != null) motes.Clear();
        }

        /// <summary>Each sung word takes the front of the orb on its own; the one before it fades out quickly.</summary>
        protected override void OnWord(string word, bool newLine) => SungWords.Add(word, new Color(0.55f, 0.08f, 0.22f), 1.6f, alone: true);

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            if (rig == null) return;
            float kick = music.Kick;
            var left = rig.Left;
            var right = rig.Right;
            bool holding = left.IsTracked && right.IsTracked;
            float spread = 0f;
            if (holding)
            {
                var a = PalmPosition(left);
                var b = PalmPosition(right);
                spread = Vector3.Distance(a, b);
                var target = (a + b) * 0.5f;
                if (view != null)
                {
                    // Float the orb a little beyond the hands, away from the face, along the level view direction.
                    var away = Vector3.ProjectOnPlane(target - view.transform.position, Vector3.up);
                    if (away.sqrMagnitude > 1e-6f) target += away.normalized * forwardOffset;
                }
                center = hasCenter ? Vector3.Lerp(center, target, 1f - Mathf.Exp(-18f * dt)) : target;
                hasCenter = true;
                float targetRadius = Mathf.Clamp(spread * radiusPerSpread, minRadius, maxRadius);
                radius = radius <= 0f ? targetRadius : Mathf.Lerp(radius, targetRadius, 1f - Mathf.Exp(-10f * dt));
                TurnWithHands(b - a, dt);
            }
            else
            {
                // Let go: the orb keeps spinning for a moment and slows down.
                angularVelocity *= Mathf.Exp(-dt * 1.5f);
                hasAxis = false;
            }
            spin = Quaternion.Euler(angularVelocity * (Mathf.Rad2Deg * dt)) * spin;

            // The shell's petal count moves on every eight beats; its sharpness follows how loud the song is.
            waveAge += dt;
            if (music.Beat)
            {
                beats++;
                waveAge = 0f;
            }
            float targetMorph = petals.Length > 0 ? petals[(beats / 8) % petals.Length] : 4f;
            morph = morph < 0f ? targetMorph : Mathf.MoveTowards(morph, targetMorph, dt * 1.6f);
            float loud = (music.Band(1) + music.Band(2) + music.Band(3) + music.Band(4)) * 0.25f;
            energy = Mathf.Lerp(energy, Mathf.Clamp01(loud * 0.9f + kick * 0.35f), 1f - Mathf.Exp(-6f * dt));

            visibility = Mathf.MoveTowards(visibility, holding ? 1f : 0f, dt * (holding ? 3f : 2f));
            bool show = visibility > 0.001f && hasCenter;
            if (orb.gameObject.activeSelf != show) orb.gameObject.SetActive(show);
            HandSkin.FocusPosition = center;
            HandSkin.FocusStrength = visibility;

            float size = radius * 2f * (1f + kick * beatSwell) * Mathf.SmoothStep(0f, 1f, visibility);
            if (show) DrawOrb(music, view, size);
            DrawKnots(music, view, show, size, dt);
            DrawTendrils(rig, music, view, show, size, dt);

            if (show && holding)
            {
                moteDebt += motesPerSecond * dt;
                int drift = (int)moteDebt;
                moteDebt -= drift;
                EmitMotes(drift, 0.015f, size * 0.5f);
                if (music.Beat) EmitMotes(motesPerBeat, 0.08f, size * 0.5f);
            }

            PushHand(left, kick * visibility);
            PushHand(right, kick * visibility);
            Sing(music, view, size);

            var bus = music.Bus;
            if (bus != null && holding)
            {
                float t = Mathf.InverseLerp(spreadRange.x, spreadRange.y, spread);
                bus.Volume = Mathf.Lerp(bus.Volume, Mathf.Lerp(volumeRange.x, volumeRange.y, t), 1f - Mathf.Exp(-5f * dt));
            }
        }

        /// <summary>The sung word beats on the front of the orb, over its heart, swelling with each kick.</summary>
        void Sing(MusicFrame music, Camera view, float size)
        {
            if (SungWords == null) return;
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                w.Size = Mathf.Clamp(size / 0.24f, 0.7f, 1.5f) * (1f + music.Kick * 0.2f);
                w.Position = center + (eye - center).normalized * (size * 0.55f) + Vector3.down * (SungWords.Height(w) * 0.4f);
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.1f;
            }
        }

        /// <summary>
        /// Turns the orb the way the line between the palms turns, like holding a ball, plus a slow idle spin.
        /// The turn rate is kept as angular velocity so the orb carries on spinning when let go.
        /// </summary>
        void TurnWithHands(Vector3 axis, float dt)
        {
            if (axis.sqrMagnitude < 1e-6f) return;
            axis.Normalize();
            if (hasAxis && dt > 0f)
            {
                var delta = Quaternion.FromToRotation(lastAxis, axis);
                delta.ToAngleAxis(out float degrees, out var rotationAxis);
                if (degrees > 180f) degrees -= 360f;
                var target = rotationAxis * (degrees * Mathf.Deg2Rad / dt);
                if (float.IsNaN(target.x)) target = Vector3.zero;
                angularVelocity = Vector3.Lerp(angularVelocity, target + Vector3.up * 0.25f, 1f - Mathf.Exp(-12f * dt));
            }
            lastAxis = axis;
            hasAxis = true;
        }

        void DrawOrb(MusicFrame music, Camera view, float size)
        {
            float kick = music.Kick;
            float ease = Mathf.SmoothStep(0f, 1f, visibility);
            orb.position = center;
            orb.localScale = Vector3.one * size;
            orb.rotation = spin;

            block.Clear();
            block.SetFloat(MorphId, morph);
            block.SetFloat(EnergyId, energy);
            block.SetVector(BandsId, new Vector4(music.Band(1), music.Band(2), music.Band(3), music.Band(4)));
            block.SetFloat(WaveId, waveAge);
            block.SetFloat(KickId, kick);
            block.SetFloat(ClockId, Now);
            block.SetFloat(AlphaId, ease);
            dotsRenderer.SetPropertyBlock(block);

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = Layers[i];
                layers[i].localScale = Vector3.one * layer.size * (1f + music.Band(layer.band) * 0.15f);
                block.Clear();
                block.SetColor(TintId, layer.color);
                block.SetFloat(LobesId, layer.lobes);
                block.SetFloat(SpinId, layer.spin);
                block.SetFloat(BandId, music.Band(layer.band));
                block.SetFloat(KickId, kick);
                block.SetFloat(OpacityId, layer.opacity);
                block.SetFloat(AlphaId, ease);
                layerRenderers[i].SetPropertyBlock(block);
            }

            if (view != null)
            {
                var toView = view.transform.position - center;
                if (toView.sqrMagnitude > 1e-6f) halo.rotation = Quaternion.LookRotation(-toView.normalized, Vector3.up);
            }
            halo.localScale = Vector3.one * (2.2f + kick * 0.5f);
            core.rotation = halo.rotation;
            core.localScale = Vector3.one * (0.5f + kick * 0.3f + music.Band(1) * 0.1f);
            block.Clear();
            block.SetColor(ColorId, Color.Lerp(Gold, Color.white, 0.3f + kick * 0.4f));
            block.SetFloat(IntensityId, (0.75f + kick * 0.6f) * ease);
            coreRenderer.SetPropertyBlock(block);
            block.Clear();
            block.SetColor(ColorId, Color.Lerp(Violet, Pink, 0.4f + kick * 0.6f));
            block.SetFloat(IntensityId, (0.28f + kick * 0.3f + energy * 0.15f) * ease);
            haloRenderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// Three torus knots of light wound around the shell, each slowly tumbling, with a bright comet racing along
        /// it that speeds up with the bass. The far side of each knot is dimmer, so they read as wrapping around.
        /// </summary>
        void DrawKnots(MusicFrame music, Camera view, bool show, float size, float dt)
        {
            bool visible = show && view != null;
            float time = Now;
            float bass = music.Band(1);
            var eye = visible ? view.transform.position : Vector3.zero;
            var toEye = visible ? (eye - center).normalized : Vector3.forward;
            for (int k = 0; k < knots.Length; k++)
            {
                var line = knots[k];
                line.SetVisible(visible);
                if (!visible) continue;
                var knot = Knots[k];
                knotPhase[k] += dt * knot.speed * (1f + bass * 1.5f + music.Kick * 2f);
                float head = Mathf.Repeat(knotPhase[k], 1f);
                var tumble = Quaternion.Euler(time * (9f + k * 4f), time * (k % 2 == 0 ? 14f : -11f), k * 50f + time * 5f);
                float bigRadius = 0.6f + k * 0.04f;
                float tube = 0.08f + music.Band(k + 2) * 0.06f + music.Kick * 0.03f;
                for (int i = 0; i < KnotPoints; i++)
                {
                    float u = i / (float)(KnotPoints - 1);
                    float phi = u * Mathf.PI * 2f;
                    float ring = bigRadius + tube * Mathf.Cos(knot.q * phi);
                    var local = new Vector3(ring * Mathf.Cos(knot.p * phi), tube * Mathf.Sin(knot.q * phi), ring * Mathf.Sin(knot.p * phi));
                    var p = orb.TransformPoint(tumble * local * 0.5f);
                    path[i] = p;

                    // The comet: bright at its head with a tail that trails behind it.
                    float behind = Mathf.Repeat(knot.speed >= 0f ? head - u : u - head, 1f);
                    float comet = Mathf.Exp(-behind * 14f);
                    float near = Vector3.Dot((p - center).normalized, toEye) * 0.5f + 0.5f;
                    // Keep the knot's own colour; only the comet's very head turns white.
                    var c = Color.Lerp(knot.color, Color.white, comet * comet * comet * comet * 0.5f);
                    c.a = (0.35f + comet * 0.8f) * Mathf.Lerp(0.3f, 1f, near);
                    pathColors[i] = c;
                }
                line.Set(path, pathColors, KnotPoints, 0.003f + music.Kick * 0.0015f, new Color(1f, 1f, 1f, visibility), eye, false);
            }
        }

        /// <summary>
        /// Threads of energy from each fingertip into the orb: smooth swaying curves, pink at the hand and gold at
        /// the orb, with bright drops flowing along them into the orb and a flare on each kick. The sway and the drops
        /// are measured in metres, so a thread looks the same however long it is: it never bunches up as the finger
        /// nears the orb or stretches as it moves away. It fades as the finger reaches the shell.
        /// </summary>
        void DrawTendrils(HandRig rig, MusicFrame music, Camera view, bool show, float size, float dt)
        {
            float time = Now;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                for (int f = 0; f < 5; f++)
                {
                    var line = tendrils[side * 5 + f];
                    bool visible = show && hand.IsTracked && view != null;
                    var tip = visible ? (hand.TipJoints[f] != null ? hand.TipJoints[f].position : hand.Tips[f]) : Vector3.zero;
                    var toTip = tip - center;
                    float reach = toTip.magnitude;
                    // Fingers stay linked to the orb out to about arm's length.
                    float surface = size * 0.42f;
                    float closeness = visible ? Mathf.InverseLerp(threadReach.y, threadReach.x, reach) * Mathf.InverseLerp(surface, surface + 0.03f, reach) : 0f;
                    visible &= closeness > 0.01f;
                    line.SetVisible(visible);
                    if (!visible) continue;

                    var end = center + toTip / reach * surface;
                    var along = end - tip;
                    float length = along.magnitude;
                    var side1 = Vector3.Cross(along, Vector3.up).normalized;
                    var side2 = Vector3.Cross(along, side1).normalized;
                    float seed = side * 5 + f;
                    // A short thread sways less, so it straightens instead of bunching up.
                    float sway = Mathf.Clamp01(length / 0.15f);
                    float flow = tendrilFlow[side * 5 + f] = Mathf.Repeat(tendrilFlow[side * 5 + f] + dt * 0.25f / length, 1f);
                    for (int i = 0; i < TendrilPoints; i++)
                    {
                        float u = i / (float)(TendrilPoints - 1);
                        float metres = u * length;
                        float env = Mathf.Sin(u * Mathf.PI) * sway;
                        var p = Vector3.Lerp(tip, end, u);
                        p += side2 * (env * 0.018f);
                        p += side1 * (Mathf.Sin(metres * 46f - time * 5f + seed) * 0.006f * env);
                        p += side2 * (Mathf.Sin(metres * 33f + time * 3.3f + seed * 2f) * 0.004f * env);
                        path[i] = p;

                        float drop = Mathf.Exp(-Mathf.Pow((u - flow) * length / 0.017f, 2f));
                        var c = Color.Lerp(Pink, Gold, u);
                        c = Color.Lerp(c, Color.white, drop * 0.35f);
                        c.a = 0.12f + drop * 0.6f + music.Kick * 0.35f + music.Band(f + 1) * 0.1f;
                        pathColors[i] = c;
                    }
                    line.Set(path, pathColors, TendrilPoints, 0.0026f, new Color(1f, 1f, 1f, visibility * closeness), view.transform.position);
                }
            }
        }

        static Vector3 PalmPosition(HandRig.Hand hand) => hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;

        void PushHand(HandRig.Hand hand, float amount)
        {
            if (!hand.IsTracked || !hasCenter)
            {
                Effects.OffsetHand(hand, Vector3.zero);
                return;
            }
            var away = PalmPosition(hand) - center;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.zero;
            Effects.OffsetHand(hand, away * (push * amount));
        }

        /// <summary>Motes leave the orb's surface; slow ones drift and swirl around it, fast ones burst out on the beat.</summary>
        void EmitMotes(int count, float speed, float surface)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                var swirl = Vector3.Cross(dir, Vector3.up).normalized;
                var emit = new ParticleSystem.EmitParams
                {
                    position = center + dir * surface * Random.Range(0.85f, 1.1f),
                    velocity = dir * speed * Random.Range(0.5f, 1.2f) + swirl * speed * 0.8f,
                    startColor = MoteColors[Random.Range(0, MoteColors.Length)],
                    startSize = Random.Range(0.005f, 0.012f),
                    startLifetime = Random.Range(0.8f, 1.6f)
                };
                motes.Emit(emit, 1);
            }
        }

        /// <summary>
        /// The dot shell's mesh: dots spread evenly over a unit sphere by the golden angle (a Fibonacci lattice), each a
        /// quad whose four corners all sit at the dot's direction. The shader pushes them onto the shape and turns them
        /// to face the viewer. The second UV holds a random seed per dot for its twinkle.
        /// </summary>
        static Mesh BuildDotMesh()
        {
            var vertices = new Vector3[DotCount * 4];
            var uvs = new Vector2[DotCount * 4];
            var seeds = new Vector2[DotCount * 4];
            var triangles = new int[DotCount * 6];
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            var random = new System.Random(7);
            for (int i = 0; i < DotCount; i++)
            {
                float y = 1f - (i + 0.5f) * 2f / DotCount;
                float r = Mathf.Sqrt(1f - y * y);
                float a = golden * i;
                var dir = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                var seed = new Vector2((float)random.NextDouble(), i / (float)DotCount);
                int v = i * 4;
                for (int c = 0; c < 4; c++)
                {
                    vertices[v + c] = dir;
                    seeds[v + c] = seed;
                }
                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(0f, 1f);
                uvs[v + 3] = new Vector2(1f, 1f);
                int t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Orb dots" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.uv2 = seeds;
            mesh.triangles = triangles;
            // The shader moves the dots out to up to about 0.8 of the orb's size, so keep the bounds generous.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }

        /// <summary>
        /// The heart's mesh: the six faces of a cube, each a fine grid, blown out into a ball. The Orb shader bends it
        /// into the heart's shape; Unity's Sphere has too few triangles for that, and they show as flat patches.
        /// </summary>
        static Mesh BuildHeartMesh()
        {
            int side = HeartGrid + 1;
            var vertices = new Vector3[6 * side * side];
            var triangles = new int[6 * HeartGrid * HeartGrid * 6];
            Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            // Even steps in angle, so the cells near the cube's corners are not squeezed.
            float Step(int i) => Mathf.Tan((i / (float)HeartGrid - 0.5f) * Mathf.PI * 0.5f);
            int t = 0;
            for (int f = 0; f < faces.Length; f++)
            {
                var normal = faces[f];
                var across = new Vector3(normal.y, normal.z, normal.x);
                var along = Vector3.Cross(normal, across);
                int start = f * side * side;
                for (int y = 0; y < side; y++)
                    for (int x = 0; x < side; x++)
                        vertices[start + y * side + x] = (normal + across * Step(x) + along * Step(y)).normalized;
                for (int y = 0; y < HeartGrid; y++)
                    for (int x = 0; x < HeartGrid; x++)
                    {
                        int v = start + y * side + x;
                        triangles[t++] = v; triangles[t++] = v + side; triangles[t++] = v + 1;
                        triangles[t++] = v + 1; triangles[t++] = v + side; triangles[t++] = v + side + 1;
                    }
            }
            var mesh = new Mesh { name = "Orb heart" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            // The shader moves the surface, so keep the bounds generous.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            if (SungWords != null)
                // Drawn after the orb, so its glow does not wash the word out.
                SungWords.RenderQueue = 3200;

            orb = new GameObject("Orb").transform;
            orb.SetParent(transform, false);

            layers = new Transform[Layers.Length];
            layerRenderers = new Renderer[Layers.Length];
            var heartMesh = BuildHeartMesh();
            for (int i = 0; i < Layers.Length; i++)
            {
                var heart = new GameObject($"Heart {i}");
                heart.transform.SetParent(orb, false);
                heart.AddComponent<MeshFilter>().sharedMesh = heartMesh;
                var r = heart.AddComponent<MeshRenderer>();
                r.sharedMaterial = orbMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                // Draw inner layers first, so the outer ones layer over them.
                r.sortingOrder = Layers.Length - i;
                layers[i] = heart.transform;
                layerRenderers[i] = r;
            }

            dots = new GameObject("Shell").transform;
            dots.SetParent(orb, false);
            dots.gameObject.AddComponent<MeshFilter>().sharedMesh = BuildDotMesh();
            dotsRenderer = dots.gameObject.AddComponent<MeshRenderer>();
            dotsRenderer.sharedMaterial = dotsMaterial;
            dotsRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dotsRenderer.receiveShadows = false;

            haloRenderer = Effects.MakeQuad("Halo", orb, glowMaterial);
            haloRenderer.sortingOrder = -1;
            halo = haloRenderer.transform;
            // The heart's own light: a hot glow in the middle of the shell.
            coreRenderer = Effects.MakeQuad("Core", orb, glowMaterial);
            core = coreRenderer.transform;

            path = new Vector3[KnotPoints];
            pathColors = new Color[KnotPoints];
            knotPhase = new float[Knots.Length];
            knots = new LineStrip[Knots.Length];
            for (int i = 0; i < knots.Length; i++)
            {
                knotPhase[i] = i * 0.33f;
                knots[i] = new LineStrip($"Knot {i}", transform, lineMaterial, KnotPoints);
                knots[i].SetVisible(false);
                // A thin coloured core instead of the line's usual white-hot one, so the knots read in colour.
                var lineBlock = new MaterialPropertyBlock();
                lineBlock.SetFloat("_Core", 0.08f);
                knots[i].Renderer.SetPropertyBlock(lineBlock);
            }

            tendrils = new LineStrip[10];
            for (int i = 0; i < tendrils.Length; i++)
            {
                tendrils[i] = new LineStrip($"Tendril {i}", transform, lineMaterial, TendrilPoints);
                tendrils[i].SetVisible(false);
                tendrilFlow[i] = Mathf.Repeat(i * 0.37f, 1f);
            }

            orb.gameObject.SetActive(false);
            motes = Effects.MakeSparkSystem("Motes", transform, glowMaterial);
        }
    }
}
