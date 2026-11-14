using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Drives the MusicInYourHands/Hand shader on one hand mesh: the beat pulse, the music energy and where the
    /// wrist is, so the forearm fades out. Values go through a property block, so both hands share one material.
    /// The active world sets the shared look (Theme) and its effect inputs (focus point, per-hand charge).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class HandSkin : MonoBehaviour
    {
        [SerializeField] Renderer target;
        [Tooltip("XRHand_Wrist in Meta's hand prefab.")]
        [SerializeField] Transform wrist;
        [Tooltip("A joint towards the fingers, such as XRHand_MiddleProximal, to know which way the hand points.")]
        [SerializeField] Transform knuckle;
        [Tooltip("How quickly the glow follows the music.")]
        [SerializeField, Range(1f, 30f)] float response = 12f;
        [SerializeField] bool isLeft;

        /// <summary>The hands' look, set by the active world.</summary>
        public static HandTheme Theme = HandTheme.Veins;
        /// <summary>A point in the world the hand effect reacts to (the Heartbeat orb's centre).</summary>
        public static Vector3 FocusPosition;
        public static float FocusStrength;
        /// <summary>Per-hand fill, 0-1 (left, right), such as caught rain.</summary>
        public static readonly float[] Charge = new float[2];

        static readonly int WristPos = Shader.PropertyToID("_WristPos");
        static readonly int HandForward = Shader.PropertyToID("_HandForward");
        static readonly int Kick = Shader.PropertyToID("_Kick");
        static readonly int Energy = Shader.PropertyToID("_Energy");
        static readonly int BeatAge = Shader.PropertyToID("_BeatAge");
        static readonly int FillColor = Shader.PropertyToID("_FillColor");
        static readonly int EdgeColor = Shader.PropertyToID("_EdgeColor");
        static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
        static readonly int Pattern = Shader.PropertyToID("_Pattern");
        static readonly int PatternColor = Shader.PropertyToID("_PatternColor");
        static readonly int FocusPos = Shader.PropertyToID("_FocusPos");
        static readonly int Focus = Shader.PropertyToID("_FocusStrength");
        static readonly int ChargeId = Shader.PropertyToID("_Charge");

        MaterialPropertyBlock block;
        float energy;

        public void Configure(Renderer handRenderer, Transform wristJoint, Transform knuckleJoint, bool left)
        {
            target = handRenderer;
            wrist = wristJoint;
            knuckle = knuckleJoint;
            isLeft = left;
        }

        void OnEnable() => Application.onBeforeRender += Apply;
        void OnDisable() => Application.onBeforeRender -= Apply;

        void Update()
        {
            var bus = MusicBus.Instance;
            float level = bus != null ? (bus.Bass + bus.Mids) * 0.5f : 0f;
            energy = Mathf.Lerp(energy, level, 1f - Mathf.Exp(-response * Time.deltaTime));
        }

        /// <summary>
        /// Pushes the current values to the shader. Runs right before rendering, after the late hand pose update,
        /// so the wrist fade lines up with the hand that is drawn. Also called by the editor preview.
        /// </summary>
        public void Apply()
        {
            if (target == null) return;
            block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(block);

            if (wrist != null && knuckle != null)
            {
                block.SetVector(WristPos, wrist.position);
                block.SetVector(HandForward, (knuckle.position - wrist.position).normalized);
            }
            var bus = MusicBus.Instance;
            block.SetFloat(Kick, bus != null ? bus.Kick : 0f);
            block.SetFloat(BeatAge, bus != null ? bus.TimeSinceBeat : 10f);
            block.SetFloat(Energy, energy);

            var theme = Theme;
            block.SetColor(FillColor, theme.Fill);
            block.SetColor(EdgeColor, theme.Edge);
            block.SetColor(GlowColor, theme.Glow);
            block.SetFloat(Pattern, (float)theme.Pattern);
            block.SetColor(PatternColor, theme.PatternColor);
            block.SetVector(FocusPos, FocusPosition);
            block.SetFloat(Focus, FocusStrength);
            block.SetFloat(ChargeId, Charge[isLeft ? 0 : 1]);
            target.SetPropertyBlock(block);
        }
    }
}
