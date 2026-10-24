using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Heartbeat orb world: hold the song between your hands. The orb forms just beyond your palms and is sized by
    /// how far apart they are. It is built in layers: three nested shells swirling different ways, a glow halo, two
    /// orbit rings and drifting motes. Each kick swells it, bursts motes and nudges your visible hands apart (fake
    /// resistance: the hands you see move away from your real ones for a moment). Spreading your hands turns the
    /// music up.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class HeartbeatOrb : World
    {
        [Header("Materials")]
        [SerializeField] Material orbMaterial;
        [SerializeField] Material glowMaterial;
        [SerializeField] Material ringMaterial;

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
        static readonly int BassId = Shader.PropertyToID("_Bass");
        static readonly int HighsId = Shader.PropertyToID("_Highs");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int LayerId = Shader.PropertyToID("_Layer");
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

        // Shells from outside in: relative size, base opacity.
        static readonly (float size, float opacity)[] Shells = { (1f, 0.12f), (0.72f, 0.2f), (0.42f, 0.35f) };

        Transform orb, halo;
        Transform[] shells, rings;
        Renderer[] shellRenderers, ringRenderers;
        Renderer haloRenderer;
        ParticleSystem motes;
        MaterialPropertyBlock block;
        Vector3 center;
        float radius, visibility, moteDebt;
        bool built, beatPending, hasCenter;
        float savedVolume = -1f;
        readonly Transform[] pushedRoots = new Transform[2];

        void OnEnable()
        {
            Build();
            var bus = MusicBus.Instance;
            if (bus != null)
            {
                bus.Beat += OnBeat;
                savedVolume = bus.Volume;
            }
        }

        void OnDisable()
        {
            var bus = MusicBus.Instance;
            if (bus != null)
            {
                bus.Beat -= OnBeat;
                if (savedVolume >= 0f) bus.Volume = savedVolume;
            }
            savedVolume = -1f;
            beatPending = false;
            hasCenter = false;
            visibility = 0f;
            // Put the visible hands back where the real ones are.
            for (int i = 0; i < pushedRoots.Length; i++)
            {
                if (pushedRoots[i] != null) pushedRoots[i].localPosition = Vector3.zero;
                pushedRoots[i] = null;
            }
            if (orb != null) orb.gameObject.SetActive(false);
            if (motes != null) motes.Clear();
        }

        void OnBeat() => beatPending = true;

        void LateUpdate()
        {
            var bus = MusicBus.Instance;
            Step(HandRig.Instance, bus != null ? bus.Bands : null, bus != null ? bus.Kick : 0f, Time.deltaTime, beatPending, Camera.main, bus);
            beatPending = false;
        }

        /// <summary>One frame of the world. Public so the editor preview can drive it without tracking or music.</summary>
        public void Step(HandRig rig, float[] bands, float kick, float dt, bool beat, Camera view, MusicBus bus = null)
        {
            if (rig == null) return;
            Build();

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
                float follow = 1f - Mathf.Exp(-18f * dt);
                center = hasCenter ? Vector3.Lerp(center, target, follow) : target;
                hasCenter = true;
                float targetRadius = Mathf.Clamp(spread * radiusPerSpread, minRadius, maxRadius);
                radius = radius <= 0f ? targetRadius : Mathf.Lerp(radius, targetRadius, 1f - Mathf.Exp(-10f * dt));
            }

            visibility = Mathf.MoveTowards(visibility, holding ? 1f : 0f, dt * (holding ? 3f : 2f));
            bool show = visibility > 0.001f && hasCenter;
            if (orb.gameObject.activeSelf != show) orb.gameObject.SetActive(show);

            if (show)
            {
                float bass = bands != null ? bands[1] : 0.4f;
                float highs = bands != null ? bands[5] : 0.3f;
                float ease = Mathf.SmoothStep(0f, 1f, visibility);
                float size = radius * 2f * (1f + kick * beatSwell) * ease;
                orb.position = center;
                orb.localScale = Vector3.one * size;
                float time = Application.isPlaying ? Time.time : 1.7f;
                orb.rotation = Quaternion.AngleAxis(time * 12f, Vector3.up);

                for (int i = 0; i < shells.Length; i++)
                {
                    // Inner shells breathe a little more with the bass.
                    shells[i].localScale = Vector3.one * Shells[i].size * (1f + bass * 0.05f * i);
                    block.Clear();
                    block.SetFloat(KickId, kick);
                    block.SetFloat(BassId, bass);
                    block.SetFloat(HighsId, highs);
                    block.SetFloat(AlphaId, ease);
                    block.SetFloat(LayerId, i);
                    block.SetFloat(OpacityId, Shells[i].opacity);
                    shellRenderers[i].SetPropertyBlock(block);
                }

                if (view != null)
                {
                    var toView = view.transform.position - center;
                    if (toView.sqrMagnitude > 1e-6f) halo.rotation = Quaternion.LookRotation(-toView.normalized, Vector3.up);
                }
                halo.localScale = Vector3.one * (2.4f + kick * 0.5f);
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(Violet, Pink, kick));
                block.SetFloat(IntensityId, (0.35f + kick * 0.3f + bass * 0.15f) * ease);
                haloRenderer.SetPropertyBlock(block);

                for (int i = 0; i < rings.Length; i++)
                {
                    // Two thin rings on tilted, slowly precessing orbits.
                    float tilt = i == 0 ? 68f : -58f;
                    rings[i].localRotation = Quaternion.Euler(tilt, time * (i == 0 ? 25f : -18f), time * (i == 0 ? 9f : -13f));
                    rings[i].localScale = Vector3.one * (1.45f + i * 0.18f + kick * 0.08f);
                    block.Clear();
                    block.SetColor(ColorId, i == 0 ? Gold : Blue);
                    block.SetFloat(RadiusId, 0.92f);
                    block.SetFloat(ThicknessId, 0.012f);
                    block.SetFloat(AlphaId, (0.55f + kick * 0.45f) * ease);
                    ringRenderers[i].SetPropertyBlock(block);
                }

                if (holding)
                {
                    moteDebt += motesPerSecond * dt;
                    int drift = (int)moteDebt;
                    moteDebt -= drift;
                    EmitMotes(drift, 0.015f, size * 0.5f);
                    if (beat) EmitMotes(motesPerBeat, 0.08f, size * 0.5f);
                }
            }

            PushHands(left, 0, kick * visibility);
            PushHands(right, 1, kick * visibility);

            if (bus != null && holding)
            {
                float t = Mathf.InverseLerp(spreadRange.x, spreadRange.y, spread);
                float volume = Mathf.Lerp(volumeRange.x, volumeRange.y, t);
                bus.Volume = Mathf.Lerp(bus.Volume, volume, 1f - Mathf.Exp(-5f * dt));
            }
        }

        /// <summary>Advances the motes while the editor is not playing.</summary>
        public void SimulateMotes(float seconds)
        {
            if (motes != null) motes.Simulate(seconds, true, false);
        }

        static Vector3 PalmPosition(HandRig.Hand hand) => hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;

        void PushHands(HandRig.Hand hand, int side, float amount)
        {
            var root = hand.VisualRoot;
            if (root == null || root.parent == null) return;
            pushedRoots[side] = root;
            if (!hand.IsTracked || !hasCenter)
            {
                root.localPosition = Vector3.zero;
                return;
            }
            var away = PalmPosition(hand) - center;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.zero;
            root.localPosition = root.parent.InverseTransformVector(away * (push * amount));
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

        public void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();

            orb = new GameObject("Orb").transform;
            orb.SetParent(transform, false);

            shells = new Transform[Shells.Length];
            shellRenderers = new Renderer[Shells.Length];
            for (int i = 0; i < Shells.Length; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Shell {i}";
                DebugShapes.RemoveCollider(sphere);
                sphere.transform.SetParent(orb, false);
                var r = sphere.GetComponent<Renderer>();
                r.sharedMaterial = orbMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Draw inner shells first, so the outer ones layer over them.
                r.sortingOrder = Shells.Length - i;
                shells[i] = sphere.transform;
                shellRenderers[i] = r;
            }

            halo = MakeQuad("Halo", glowMaterial, out haloRenderer).transform;
            haloRenderer.sortingOrder = -1;

            rings = new Transform[2];
            ringRenderers = new Renderer[2];
            for (int i = 0; i < rings.Length; i++) rings[i] = MakeQuad($"Orbit {i}", ringMaterial, out ringRenderers[i]).transform;

            orb.gameObject.SetActive(false);
            motes = Effects.MakeSparkSystem("Motes", transform, glowMaterial);
        }

        GameObject MakeQuad(string objectName, Material material, out Renderer r)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = objectName;
            DebugShapes.RemoveCollider(quad);
            quad.transform.SetParent(orb, false);
            r = quad.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return quad;
        }
    }
}
