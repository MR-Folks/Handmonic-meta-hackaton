using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Drives the MusicInYourHands/Hand shader on one hand mesh: the beat pulse, the music energy and where the
    /// wrist is, so the forearm fades out. Values go through a property block, so both hands share one material.
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

        static readonly int WristPos = Shader.PropertyToID("_WristPos");
        static readonly int HandForward = Shader.PropertyToID("_HandForward");
        static readonly int Kick = Shader.PropertyToID("_Kick");
        static readonly int Energy = Shader.PropertyToID("_Energy");

        MaterialPropertyBlock block;
        float energy;

        public void Configure(Renderer handRenderer, Transform wristJoint, Transform knuckleJoint)
        {
            target = handRenderer;
            wrist = wristJoint;
            knuckle = knuckleJoint;
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
            block.SetFloat(Kick, MusicBus.Instance != null ? MusicBus.Instance.Kick : 0f);
            block.SetFloat(Energy, energy);
            target.SetPropertyBlock(block);
        }
    }
}
