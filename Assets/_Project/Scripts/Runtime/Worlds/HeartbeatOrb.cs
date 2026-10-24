using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Heartbeat orb world: hold the song between your hands. The orb forms between your palms and is sized by
    /// how far apart they are. Each kick swells it and nudges your visible hands apart (fake resistance: the hands
    /// you see move away from your real ones for a moment). Spreading your hands turns the music up.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class HeartbeatOrb : World
    {
        [Header("Materials")]
        [SerializeField] Material orbMaterial;
        [SerializeField] Material glowMaterial;

        [Header("Orb")]
        [SerializeField] float minRadius = 0.04f;
        [SerializeField] float maxRadius = 0.18f;
        [Tooltip("Orb radius as a share of the distance between the palms.")]
        [SerializeField] float radiusPerSpread = 0.36f;
        [SerializeField] float beatSwell = 0.2f;

        [Header("Feel")]
        [Tooltip("How far each kick pushes the visible hands apart, in metres.")]
        [SerializeField] float push = 0.012f;
        [Tooltip("Palm distance (m) mapped to volume: close together is quiet, wide apart is loud.")]
        [SerializeField] Vector2 spreadRange = new Vector2(0.12f, 0.6f);
        [SerializeField] Vector2 volumeRange = new Vector2(0.3f, 1f);
        [SerializeField] int motesPerBeat = 10;

        static readonly int KickId = Shader.PropertyToID("_Kick");
        static readonly int BassId = Shader.PropertyToID("_Bass");
        static readonly int HighsId = Shader.PropertyToID("_Highs");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        static readonly Color[] MoteColors =
        {
            new Color(1f, 0.37f, 0.68f), new Color(0.62f, 0.56f, 1f), new Color(0.5f, 0.72f, 1f), new Color(1f, 0.83f, 0.42f)
        };

        Transform orb;
        Renderer orbRenderer;
        ParticleSystem motes;
        MaterialPropertyBlock block;
        Vector3 center;
        float radius, visibility;
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
            Step(HandRig.Instance, bus != null ? bus.Bands : null, bus != null ? bus.Kick : 0f, Time.deltaTime, beatPending, bus);
            beatPending = false;
        }

        /// <summary>One frame of the world. Public so the editor preview can drive it without tracking or music.</summary>
        public void Step(HandRig rig, float[] bands, float kick, float dt, bool beat, MusicBus bus = null)
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
                orb.position = center;
                orb.localScale = Vector3.one * (radius * 2f * (1f + kick * beatSwell) * ease);
                orb.rotation = Quaternion.AngleAxis(Time.time * 12f, Vector3.up);

                block.Clear();
                block.SetFloat(KickId, kick);
                block.SetFloat(BassId, bass);
                block.SetFloat(HighsId, highs);
                block.SetFloat(AlphaId, ease);
                orbRenderer.SetPropertyBlock(block);

                if (beat && holding) EmitMotes();
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

        void EmitMotes()
        {
            for (int i = 0; i < motesPerBeat; i++)
            {
                var dir = Random.onUnitSphere;
                var emit = new ParticleSystem.EmitParams
                {
                    position = center + dir * radius * 0.9f,
                    velocity = dir * Random.Range(0.04f, 0.1f),
                    startColor = MoteColors[i % MoteColors.Length],
                    startSize = Random.Range(0.006f, 0.014f),
                    startLifetime = Random.Range(0.7f, 1.3f)
                };
                motes.Emit(emit, 1);
            }
        }

        public void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Orb";
            DebugShapes.RemoveCollider(sphere);
            sphere.transform.SetParent(transform, false);
            orbRenderer = sphere.GetComponent<Renderer>();
            orbRenderer.sharedMaterial = orbMaterial;
            orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            orb = sphere.transform;
            sphere.SetActive(false);

            motes = Effects.MakeSparkSystem("Motes", transform, glowMaterial);
        }
    }
}
