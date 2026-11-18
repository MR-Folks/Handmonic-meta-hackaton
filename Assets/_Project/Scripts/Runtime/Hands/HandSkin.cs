using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Drives the MusicInYourHands/Hand shader on one hand mesh: the beat pulse, the music energy and where the
    /// wrist is, so the forearm fades out. Values go through a property block, so both hands share one material.
    /// The active world sets the shared look (Theme) and its effect inputs (focus point, per-hand charge).
    /// It also lights single fingers (GestureFeedback): the hand gets a copy of its mesh that knows, for each vertex,
    /// how much it belongs to each finger and how far along it, so a lit finger glows from its knuckle to its brightest
    /// tip and never bleeds into the finger beside it, whatever the pose.
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
        static readonly int FingerGlowId = Shader.PropertyToID("_FingerGlow");
        static readonly int FingerGlowLittleId = Shader.PropertyToID("_FingerGlowLittle");
        static readonly int FingerColorId = Shader.PropertyToID("_FingerColor");
        static readonly int PulseScale = Shader.PropertyToID("_PulseScale");

        MaterialPropertyBlock block;
        float energy;
        bool fingersReady;
        Mesh fingerMesh;

        public void Configure(Renderer handRenderer, Transform wristJoint, Transform knuckleJoint, bool left)
        {
            target = handRenderer;
            wrist = wristJoint;
            knuckle = knuckleJoint;
            isLeft = left;
        }

        void OnEnable() => Application.onBeforeRender += Apply;
        void OnDisable() => Application.onBeforeRender -= Apply;

        void OnDestroy()
        {
            if (fingerMesh == null) return;
            if (Application.isPlaying) Destroy(fingerMesh);
            else DestroyImmediate(fingerMesh);
        }

        void Update()
        {
            var bus = MusicBus.Instance;
            float level = bus != null ? (bus.Bass + bus.Mids) * 0.5f : 0f;
            energy = Mathf.Lerp(energy, level, 1f - Mathf.Exp(-response * Time.deltaTime));
            GestureFeedback.TickFrame();
        }

        /// <summary>
        /// Pushes the current values to the shader. Runs right before rendering, after the late hand pose update,
        /// so the wrist fade lines up with the hand that is drawn. Also called by the editor preview.
        /// </summary>
        public void Apply()
        {
            if (target == null) return;
            if (!fingersReady) PrepareFingers();
            block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(block);

            if (wrist != null && knuckle != null)
            {
                block.SetVector(WristPos, wrist.position);
                block.SetVector(HandForward, (knuckle.position - wrist.position).normalized);
            }
            var bus = MusicBus.Instance;
            // With Less flashing the beat brightens the hands, and runs its pulse along them, at a quarter.
            block.SetFloat(Kick, bus != null ? bus.Kick * ComfortOptions.FlashScale : 0f);
            block.SetFloat(PulseScale, ComfortOptions.FlashScale);
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

            // The lit fingers (GestureFeedback), in the theme's glow lifted towards white, so they read on every world's hands.
            block.SetVector(FingerGlowId, new Vector4(
                GestureFeedback.Glow(isLeft, HandRig.Finger.Thumb), GestureFeedback.Glow(isLeft, HandRig.Finger.Index),
                GestureFeedback.Glow(isLeft, HandRig.Finger.Middle), GestureFeedback.Glow(isLeft, HandRig.Finger.Ring)));
            block.SetFloat(FingerGlowLittleId, GestureFeedback.Glow(isLeft, HandRig.Finger.Little));
            block.SetColor(FingerColorId, FingerColor(theme));
            target.SetPropertyBlock(block);
        }

        /// <summary>The colour a lit finger glows in a theme: its glow colour, lifted a little towards white (the shader runs it white-hot at the edges).</summary>
        public static Color FingerColor(HandTheme theme) => Color.Lerp(theme.Glow, Color.white, 0.15f);

        /// <summary>
        /// Gives the hand a copy of its mesh that says, per vertex, how much of it belongs to each finger (UV2: thumb,
        /// index, middle, ring; UV3: little, then how far along its finger, 0 at the knuckle to 1 at the tip), worked out
        /// once from the bones that move it, the first time the hand is drawn. A hand saved in a scene keeps its own mesh
        /// while editing, so the scene never refers to the copy.
        /// </summary>
        void PrepareFingers()
        {
            fingersReady = true;
            if (!(target is SkinnedMeshRenderer skin) || skin.sharedMesh == null || !skin.sharedMesh.isReadable) return;
            if (!Application.isPlaying && !string.IsNullOrEmpty(gameObject.scene.path)) return;
            var source = skin.sharedMesh;
            var bones = skin.bones;
            if (bones == null || bones.Length == 0) return;

            // Which finger each bone belongs to (or -1), and how far along the finger it sits.
            var finger = new int[bones.Length];
            var along = new float[bones.Length];
            for (int b = 0; b < bones.Length; b++) Classify(bones[b] != null ? bones[b].name : "", out finger[b], out along[b]);

            var counts = source.GetBonesPerVertex();
            var weights = source.GetAllBoneWeights();
            int vertices = source.vertexCount;
            if (counts.Length != vertices) return;
            var fourFingers = new List<Vector4>(vertices);
            var littleAndAlong = new List<Vector2>(vertices);
            var share = new float[5];
            int w = 0;
            for (int v = 0; v < vertices; v++)
            {
                System.Array.Clear(share, 0, 5);
                float reach = 0f, total = 0f;
                for (int k = 0; k < counts[v]; k++, w++)
                {
                    var bw = weights[w];
                    int f = bw.boneIndex < finger.Length ? finger[bw.boneIndex] : -1;
                    if (f < 0) continue;
                    share[f] += bw.weight;
                    reach += bw.weight * along[bw.boneIndex];
                    total += bw.weight;
                }
                fourFingers.Add(new Vector4(share[0], share[1], share[2], share[3]));
                littleAndAlong.Add(new Vector2(share[4], total > 1e-4f ? reach / total : 0f));
            }
            fingerMesh = Instantiate(source);
            fingerMesh.name = source.name + " (fingers)";
            fingerMesh.hideFlags = HideFlags.DontSave;
            fingerMesh.SetUVs(2, fourFingers);
            fingerMesh.SetUVs(3, littleAndAlong);
            skin.sharedMesh = fingerMesh;
        }

        /// <summary>
        /// A bone of Meta's hand (XRHand_IndexProximal and so on): its finger (0 thumb to 4 little, or -1 for the palm and
        /// wrist), and how far along that finger it is. The metacarpals are inside the palm (the thumb's makes the ball of the
        /// thumb), so they stay dark and a lit finger glows from its first knuckle.
        /// </summary>
        static void Classify(string bone, out int finger, out float along)
        {
            finger = bone.Contains("Thumb") ? 0 : bone.Contains("Index") ? 1 : bone.Contains("Middle") ? 2
                : bone.Contains("Ring") ? 3 : bone.Contains("Little") ? 4 : -1;
            bool thumb = finger == 0;
            bool metacarpal = bone.Contains("Metacarpal");
            along = metacarpal ? 0f : bone.Contains("Proximal") ? (thumb ? 0.5f : 0.4f) : bone.Contains("Intermediate") ? 0.72f : 1f;
            if (metacarpal) finger = -1;
        }
    }
}
