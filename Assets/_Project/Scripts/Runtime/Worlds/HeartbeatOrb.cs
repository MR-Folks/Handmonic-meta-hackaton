using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Heartbeat orb world: hold the song between your hands. The orb forms just beyond your palms, sized by how
    /// far apart they are, and turns as you turn your hands, like holding a ball. It is four layers (pink, violet,
    /// blue, gold), each a 3D supershape (the superformula) that morphs with its own frequency band, plus a halo,
    /// two orbit rings with beads riding them, and drifting motes. Energy tendrils reach from your fingertips to it
    /// and your hands glow with heat on the side facing it. Each kick swells it and nudges your visible hands apart
    /// (fake resistance). Spreading your hands turns the music up.
    /// </summary>
    public class HeartbeatOrb : World
    {
        public override int MenuIcon => 1;

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.22f, 0.1f, 0.2f, 0.55f),
            Edge = new Color(1f, 0.72f, 0.45f),
            Glow = new Color(1f, 0.37f, 0.68f),
            Pattern = HandTheme.Effect.Heat,
            PatternColor = new Color(1f, 0.6f, 0.35f)
        };

        [Header("Materials")]
        [SerializeField] Material orbMaterial;
        [SerializeField] Material glowMaterial;
        [SerializeField] Material ringMaterial;
        [SerializeField] Material lineMaterial;

        [Header("Orb")]
        [SerializeField] float minRadius = 0.04f;
        [SerializeField] float maxRadius = 0.15f;
        [Tooltip("Orb radius as a share of the distance between the palms.")]
        [SerializeField] float radiusPerSpread = 0.3f;
        [Tooltip("How far beyond the palms, away from the player's face, the orb floats (m).")]
        [SerializeField] float forwardOffset = 0.12f;
        [SerializeField] float beatSwell = 0.2f;

        [Header("Feel")]
        [Tooltip("How far each kick pushes the visible hands apart, in metres.")]
        [SerializeField] float push = 0.012f;
        [Tooltip("Palm distance (m) mapped to volume: close together is quiet, wide apart is loud.")]
        [SerializeField] Vector2 spreadRange = new Vector2(0.12f, 0.6f);
        [SerializeField] Vector2 volumeRange = new Vector2(0.3f, 1f);
        [SerializeField] int motesPerBeat = 14;
        [SerializeField] float motesPerSecond = 10f;

        static readonly int KickId = Shader.PropertyToID("_Kick");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int LobesId = Shader.PropertyToID("_Lobes");
        static readonly int SpinId = Shader.PropertyToID("_Spin");
        static readonly int BandId = Shader.PropertyToID("_Band");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");

        static readonly Color Pink = new Color(1f, 0.37f, 0.68f);
        static readonly Color Violet = new Color(0.62f, 0.56f, 1f);
        static readonly Color Blue = new Color(0.5f, 0.72f, 1f);
        static readonly Color Gold = new Color(1f, 0.83f, 0.42f);
        static readonly Color[] MoteColors = { Pink, Violet, Blue, Gold };

        // Layers from outside in: size, colour, symmetry (petals), spin (sign is direction), band, base opacity.
        static readonly (float size, Color color, float lobes, float spin, int band, float opacity)[] Layers =
        {
            (1f, Pink, 3f, 0.9f, 1, 0.14f),
            (0.8f, Violet, 5f, -1.2f, 2, 0.18f),
            (0.6f, Blue, 4f, 1.5f, 3, 0.22f),
            (0.42f, Gold, 6f, -1.9f, 4, 0.3f)
        };

        const int BeadsPerRing = 3;
        const int TendrilPoints = 14;

        Transform orb, halo;
        Transform[] layers, rings, beads;
        Renderer[] layerRenderers, ringRenderers, beadRenderers;
        Renderer haloRenderer;
        LineStrip[] tendrils;
        Vector3[] tendrilPath;
        ParticleSystem motes;
        MaterialPropertyBlock block;
        Vector3 center, lastAxis;
        Quaternion spin = Quaternion.identity;
        Vector3 angularVelocity;
        float radius, visibility, moteDebt, beadPhase;
        bool built, hasCenter, hasAxis;
        float savedVolume = -1f;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (MusicBus.Instance != null) savedVolume = MusicBus.Instance.Volume;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
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
            if (motes != null) motes.Clear();
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            if (rig == null) return;
            Build();
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

            visibility = Mathf.MoveTowards(visibility, holding ? 1f : 0f, dt * (holding ? 3f : 2f));
            bool show = visibility > 0.001f && hasCenter;
            if (orb.gameObject.activeSelf != show) orb.gameObject.SetActive(show);
            HandSkin.FocusPosition = center;
            HandSkin.FocusStrength = visibility;

            float size = radius * 2f * (1f + kick * beatSwell) * Mathf.SmoothStep(0f, 1f, visibility);
            if (show) DrawOrb(music, view, size, dt);
            DrawTendrils(rig, music, view, show, size);

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

            var bus = music.Bus;
            if (bus != null && holding)
            {
                float t = Mathf.InverseLerp(spreadRange.x, spreadRange.y, spread);
                bus.Volume = Mathf.Lerp(bus.Volume, Mathf.Lerp(volumeRange.x, volumeRange.y, t), 1f - Mathf.Exp(-5f * dt));
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

        void DrawOrb(MusicFrame music, Camera view, float size, float dt)
        {
            float kick = music.Kick;
            float bass = music.Band(1);
            float ease = Mathf.SmoothStep(0f, 1f, visibility);
            orb.position = center;
            orb.localScale = Vector3.one * size;
            orb.rotation = spin;

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = Layers[i];
                layers[i].localScale = Vector3.one * layer.size;
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
            halo.localScale = Vector3.one * (2f + kick * 0.4f);
            block.Clear();
            block.SetColor(ColorId, Color.Lerp(Violet, Pink, kick));
            block.SetFloat(IntensityId, (0.35f + kick * 0.3f + bass * 0.15f) * ease);
            haloRenderer.SetPropertyBlock(block);

            // Two rings on tilted orbits that precess faster with the bass; beads ride them and race on the beat.
            float time = Now;
            beadPhase += dt * (1.2f + bass * 2.5f + kick * 4f);
            for (int i = 0; i < rings.Length; i++)
            {
                float tilt = i == 0 ? 68f : -58f;
                float precess = time * (i == 0 ? 25f : -18f) * (1f + bass * 0.8f);
                rings[i].localRotation = Quaternion.Euler(tilt + Mathf.Sin(time * 0.6f + i) * 12f, precess, time * (i == 0 ? 9f : -13f));
                rings[i].localScale = Vector3.one * (1.45f + i * 0.18f + kick * 0.12f);
                block.Clear();
                block.SetColor(ColorId, i == 0 ? Gold : Blue);
                block.SetFloat(RadiusId, 0.92f);
                block.SetFloat(ThicknessId, 0.012f);
                block.SetFloat(AlphaId, (0.45f + kick * 0.45f) * ease);
                ringRenderers[i].SetPropertyBlock(block);

                for (int k = 0; k < BeadsPerRing; k++)
                {
                    int index = i * BeadsPerRing + k;
                    float a = beadPhase * (i == 0 ? 1f : -1.3f) + k * Mathf.PI * 2f / BeadsPerRing;
                    var local = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.46f;
                    beads[index].position = rings[i].TransformPoint(local);
                    beads[index].localScale = Vector3.one * (0.16f + kick * 0.08f);
                    Effects.Face(beads[index], view);
                    block.Clear();
                    block.SetColor(ColorId, i == 0 ? Gold : Blue);
                    block.SetFloat(IntensityId, (1f + kick * 0.8f) * ease);
                    beadRenderers[index].SetPropertyBlock(block);
                }
            }
        }

        /// <summary>Flickering threads of energy from each fingertip to the orb's surface.</summary>
        void DrawTendrils(HandRig rig, MusicFrame music, Camera view, bool show, float size)
        {
            float time = Now;
            int flicker = Mathf.FloorToInt(time * 18f);
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                for (int f = 0; f < 5; f++)
                {
                    var line = tendrils[side * 5 + f];
                    bool visible = show && hand.IsTracked && view != null;
                    line.SetVisible(visible);
                    if (!visible) continue;

                    var tip = hand.TipJoints[f] != null ? hand.TipJoints[f].position : hand.Tips[f];
                    var toTip = tip - center;
                    var end = center + toTip.normalized * size * 0.42f;
                    var mid = (tip + end) * 0.5f;
                    var side1 = Vector3.Cross(end - tip, Vector3.up).normalized;
                    var side2 = Vector3.Cross(end - tip, side1).normalized;
                    var random = new System.Random(flicker * 31 + side * 7 + f);
                    float jag = 0.008f + music.Kick * 0.01f;
                    for (int i = 0; i < TendrilPoints; i++)
                    {
                        float u = i / (float)(TendrilPoints - 1);
                        // A gentle curve, with a jagged jitter that is zero at both ends.
                        var p = Vector3.Lerp(Vector3.Lerp(tip, mid + side2 * 0.02f, u), Vector3.Lerp(mid + side2 * 0.02f, end, u), u);
                        float env = Mathf.Sin(u * Mathf.PI);
                        p += (side1 * ((float)random.NextDouble() * 2 - 1) + side2 * ((float)random.NextDouble() * 2 - 1)) * jag * env;
                        tendrilPath[i] = p;
                    }
                    var color = Color.Lerp(Gold, Pink, (f / 4f) * 0.6f + music.Kick * 0.4f);
                    color.a = (0.25f + music.Band(f + 1) * 0.35f + music.Kick * 0.4f) * visibility;
                    line.Set(tendrilPath, TendrilPoints, 0.0035f, color, view.transform.position);
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
                    startSize = Random.Range(0.005f, 0.013f),
                    startLifetime = Random.Range(0.8f, 1.6f)
                };
                motes.Emit(emit, 1);
            }
        }

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();

            orb = new GameObject("Orb").transform;
            orb.SetParent(transform, false);

            layers = new Transform[Layers.Length];
            layerRenderers = new Renderer[Layers.Length];
            for (int i = 0; i < Layers.Length; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Layer {i}";
                DebugShapes.RemoveCollider(sphere);
                sphere.transform.SetParent(orb, false);
                var r = sphere.GetComponent<Renderer>();
                r.sharedMaterial = orbMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Draw inner layers first, so the outer ones layer over them.
                r.sortingOrder = Layers.Length - i;
                layers[i] = sphere.transform;
                layerRenderers[i] = r;
            }

            haloRenderer = Effects.MakeQuad("Halo", orb, glowMaterial);
            haloRenderer.sortingOrder = -1;
            halo = haloRenderer.transform;

            rings = new Transform[2];
            ringRenderers = new Renderer[2];
            for (int i = 0; i < rings.Length; i++)
            {
                ringRenderers[i] = Effects.MakeQuad($"Orbit {i}", orb, ringMaterial);
                rings[i] = ringRenderers[i].transform;
            }

            beads = new Transform[rings.Length * BeadsPerRing];
            beadRenderers = new Renderer[beads.Length];
            for (int i = 0; i < beads.Length; i++)
            {
                beadRenderers[i] = Effects.MakeQuad($"Bead {i}", orb, glowMaterial);
                beads[i] = beadRenderers[i].transform;
            }

            tendrils = new LineStrip[10];
            tendrilPath = new Vector3[TendrilPoints];
            for (int i = 0; i < tendrils.Length; i++)
            {
                tendrils[i] = new LineStrip($"Tendril {i}", transform, lineMaterial, TendrilPoints);
                tendrils[i].SetVisible(false);
            }

            orb.gameObject.SetActive(false);
            motes = Effects.MakeSparkSystem("Motes", transform, glowMaterial);
        }
    }
}
