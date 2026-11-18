using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>Where a ghost hand's thumb goes. Each is solved once per hand, so the tip lands where the gesture needs it.</summary>
    public enum ThumbShape { Rest, Pinch, Tap, Lift, SwipeTip, SwipeKnuckle, Tuck, Fist }

    /// <summary>
    /// One ghost hand: a copy of Meta's hand model (Resources/Onboarding/OnboardingGhostHand_L or _R: its bones and mesh
    /// only, nothing that tracks it) in the see-through OnboardingGhost look, posed each frame by turning its bones. A
    /// pose is a palm position, the way the fingers point and the palm faces, a curl for each finger, a spread and a thumb
    /// shape. The thumb shapes come from a small search over the thumb's three joints (and, for a pinch, the index curl)
    /// that puts its tip on the index tip for a pinch, on the side of the curled index for a tap or a swipe, and over the
    /// curled fingers for a fist; the editor setup runs it and stores the result with the hand (GhostHandShapes), so the
    /// headset only reads it. Posing allocates nothing.
    /// </summary>
    public sealed class GhostHand
    {
        public const string PrefabPath = "Onboarding/OnboardingGhostHand_";

        /// <summary>
        /// The index curl a motion's pinch keys give. Each hand finds the curl its thumb meets best and uses that while
        /// the thumb is in its pinch shape (<see cref="PinchIndex"/>), so the tips always meet.
        /// </summary>
        public const float PinchIndexCurl = 0.5f;
        /// <summary>The index curl the thumb tap and swipe shapes are solved for.</summary>
        public const float TapIndexCurl = 0.55f;
        /// <summary>The curls a pointing hand's tucked thumb is solved for: index straight, the others closed.</summary>
        public static readonly Vector4 TuckCurl = new Vector4(0f, 0.95f, 1f, 1f);
        public static readonly Vector4 FistCurl = new Vector4(1f, 1f, 1f, 1f);

        static readonly string[] Fingers = { "Index", "Middle", "Ring", "Little" };
        static readonly string[] Parts = { "Proximal", "Intermediate", "Distal" };
        static readonly string[] TipNames = { "XRHand_ThumbTip", "XRHand_IndexTip", "XRHand_MiddleTip", "XRHand_RingTip", "XRHand_LittleTip" };
        // Degrees each finger joint bends at curl 1, from the knuckle out.
        static readonly float[] BendAngles = { 75f, 100f, 65f };
        // Degrees each finger turns away from the middle finger at spread 1 (index, middle, ring, little).
        static readonly float[] SpreadAngles = { 11f, 0f, 9f, 18f };

        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int FocusAId = Shader.PropertyToID("_FocusA");
        static readonly int FocusBId = Shader.PropertyToID("_FocusB");
        static readonly int WristPosId = Shader.PropertyToID("_WristPos");
        static readonly int HandForwardId = Shader.PropertyToID("_HandForward");

        public readonly bool Left;
        public Transform Root { get; }
        public SkinnedMeshRenderer Renderer { get; }
        public bool IsValid => Root != null;

        readonly Transform wrist, palm, knuckle, indexBase, middleBase;
        readonly Transform[] tips = new Transform[5];
        // The four fingers' joints (proximal, intermediate, distal for index to little), at rest and the axis each bends
        // about, in its own frame; and the knuckles' spread axes.
        readonly Transform[] joints = new Transform[12];
        readonly Quaternion[] jointRest = new Quaternion[12];
        readonly Vector3[] jointAxis = new Vector3[12];
        readonly Vector3[] spreadAxis = new Vector3[4];
        readonly float[] spreadSign = new float[4];
        // The thumb: metacarpal, proximal and distal, at rest and their axes.
        readonly Transform thumbBase, thumbMid, thumbEnd;
        readonly Quaternion thumbBaseRest, thumbMidRest, thumbEndRest;
        readonly Vector3 swingAxis, flexAxis, thumbMidAxis, thumbEndAxis;
        readonly float swingSign = 1f;
        // Solved thumb shapes: swing, flex, curl (degrees); and the index curl that meets the thumb in a pinch.
        readonly Vector3[] thumbShapes = new Vector3[ShapeCount];
        float pinchIndexCurl = PinchIndexCurl;
        // The hand's own frame at rest (fingers forward, palm up), and the palm's place, in the root's space.
        readonly Quaternion restBasis;
        readonly Vector3 palmLocal;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        bool shown = true;

        /// <summary>Bumped whenever the thumb search changes, so shapes stored with the hands are found again.</summary>
        public const int SolverVersion = 1;

        /// <summary>The number of thumb shapes.</summary>
        public const int ShapeCount = 8;

        /// <summary>True when the thumb shapes came stored with the hand rather than being searched for here.</summary>
        public bool FromStore { get; }

        /// <summary>
        /// Makes a ghost hand under <paramref name="parent"/>. Its thumb shapes come stored with it (GhostHandShapes) when
        /// they are up to date, unless <paramref name="solve"/> asks for them to be found afresh (the editor setup).
        /// </summary>
        public GhostHand(Transform parent, bool left, Material material = null, bool solve = false)
        {
            Left = left;
            var prefab = Resources.Load<GameObject>(PrefabPath + (left ? "L" : "R"));
            if (prefab == null)
            {
                Debug.LogWarning($"[Onboarding] No ghost hand at Resources/{PrefabPath}{(left ? "L" : "R")}.");
                return;
            }
            var go = Object.Instantiate(prefab, parent, false);
            go.name = left ? "Ghost Hand L" : "Ghost Hand R";
            Root = go.transform;
            Root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Root.localScale = Vector3.one;
            Renderer = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (Renderer != null)
            {
                if (material != null) Renderer.sharedMaterial = material;
                Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Renderer.receiveShadows = false;
                Renderer.skinnedMotionVectors = false;
                Renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                // The bones move a long way from where the mesh was bound; keep it from being culled.
                Renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 0.6f);
            }

            wrist = Find("XRHand_Wrist");
            palm = Find("XRHand_Palm");
            knuckle = Find("XRHand_MiddleProximal");
            indexBase = Find("XRHand_IndexProximal");
            middleBase = knuckle;
            var littleBase = Find("XRHand_LittleProximal");
            for (int i = 0; i < 5; i++) tips[i] = Find(TipNames[i]);
            thumbBase = Find("XRHand_ThumbMetacarpal");
            thumbMid = Find("XRHand_ThumbProximal");
            thumbEnd = Find("XRHand_ThumbDistal");
            bool found = wrist != null && palm != null && knuckle != null && indexBase != null && littleBase != null &&
                         thumbBase != null && thumbMid != null && thumbEnd != null;
            for (int f = 0; f < 4 && found; f++)
                for (int j = 0; j < 3; j++)
                {
                    joints[f * 3 + j] = Find($"XRHand_{Fingers[f]}{Parts[j]}");
                    found &= joints[f * 3 + j] != null;
                }
            foreach (var t in tips) found &= t != null;
            if (!found)
            {
                Debug.LogWarning("[Onboarding] The ghost hand is missing some of Meta's XRHand_ bones.");
                if (Application.isPlaying) Object.Destroy(go);
                else Object.DestroyImmediate(go);
                Root = null;
                return;
            }

            // The rest pose (the model as bound: a flat hand), with the root at the origin.
            var normal = Vector3.Cross(indexBase.position - wrist.position, littleBase.position - wrist.position).normalized;
            if (!left) normal = -normal;
            var forward = (knuckle.position - wrist.position).normalized;
            normal = Vector3.ProjectOnPlane(normal, forward).normalized;
            restBasis = Quaternion.LookRotation(forward, normal);
            palmLocal = palm.position;

            for (int f = 0; f < 4; f++)
            {
                var proximal = joints[f * 3];
                var along = (tips[f + 1].position - proximal.position).normalized;
                var axis = Vector3.Cross(along, normal).normalized;
                for (int j = 0; j < 3; j++)
                {
                    var bone = joints[f * 3 + j];
                    jointRest[f * 3 + j] = bone.localRotation;
                    jointAxis[f * 3 + j] = Quaternion.Inverse(bone.rotation) * axis;
                }
                // Spreading turns a finger about the palm normal, away from the middle finger.
                spreadAxis[f] = Quaternion.Inverse(proximal.rotation) * normal;
                var away = proximal.position - middleBase.position;
                if (f == 1) away = middleBase.position - indexBase.position;
                spreadSign[f] = Vector3.Dot(Vector3.Cross(normal, along), away) >= 0f ? 1f : -1f;
            }

            thumbBaseRest = thumbBase.localRotation;
            thumbMidRest = thumbMid.localRotation;
            thumbEndRest = thumbEnd.localRotation;
            var thumbAlong = (thumbMid.position - thumbBase.position).normalized;
            swingAxis = Quaternion.Inverse(thumbBase.rotation) * normal;
            flexAxis = Quaternion.Inverse(thumbBase.rotation) * Vector3.Cross(thumbAlong, normal).normalized;
            var midAlong = (thumbEnd.position - thumbMid.position).normalized;
            thumbMidAxis = Quaternion.Inverse(thumbMid.rotation) * Vector3.Cross(midAlong, normal).normalized;
            var endAlong = (tips[0].position - thumbEnd.position).normalized;
            thumbEndAxis = Quaternion.Inverse(thumbEnd.rotation) * Vector3.Cross(endAlong, normal).normalized;

            // The swing that brings the thumb over the fingers rather than away from them.
            ApplyThumb(new Vector3(30f, 0f, 0f));
            float plus = Vector3.Distance(tips[0].position, knuckle.position);
            ApplyThumb(new Vector3(-30f, 0f, 0f));
            float minus = Vector3.Distance(tips[0].position, knuckle.position);
            swingSign = plus <= minus ? 1f : -1f;
            ApplyThumb(Vector3.zero);
            var stored = go.GetComponent<GhostHandShapes>();
            if (!solve && stored != null && stored.Has(SolverVersion, ShapeCount))
            {
                for (int i = 0; i < ShapeCount; i++) thumbShapes[i] = stored.Thumb(i);
                pinchIndexCurl = stored.PinchIndex;
                FromStore = true;
            }
            else SolveThumbs();
            Curl(Relax, 0f);
            ApplyThumb(Vector3.zero);
        }

        static readonly Vector4 Relax = new Vector4(0.15f, 0.2f, 0.25f, 0.3f);

        Transform Find(string boneName) => Find(Root, boneName);

        static Transform Find(Transform parent, string boneName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == boneName) return child;
                var match = Find(child, boneName);
                if (match != null) return match;
            }
            return null;
        }

        /// <summary>The thumb parameters (swing, flex, curl in degrees) for a shape.</summary>
        public Vector3 Thumb(ThumbShape shape) => thumbShapes[(int)shape];

        /// <summary>The index curl this hand pinches with.</summary>
        public float PinchIndex => pinchIndexCurl;

        /// <summary>Where a fingertip of the ghost is now (0 thumb, 1 index ... 4 little).</summary>
        public Vector3 Tip(int finger) => tips[Mathf.Clamp(finger, 0, 4)].position;

        /// <summary>Where the middle of the ghost's palm is now.</summary>
        public Vector3 PalmPosition => palm.position;

        /// <summary>Where the side of the curled index finger is, where a thumb tap lands (for the checks).</summary>
        public Vector3 IndexSide => SideOfIndex(0.3f);

        /// <summary>Shows or hides the hand.</summary>
        public void SetVisible(bool visible)
        {
            if (Root == null || shown == visible) return;
            shown = visible;
            Root.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Poses the hand: its palm at <paramref name="palmAt"/>, fingers pointing along <paramref name="fingers"/> and
        /// the palm facing <paramref name="palmFacing"/> (made square to the fingers); each finger curled, spread, and the
        /// thumb blended between two solved shapes.
        /// </summary>
        public void Pose(Vector3 palmAt, Vector3 fingers, Vector3 palmFacing, Vector4 curl, float spread,
            ThumbShape thumbFrom, ThumbShape thumbTo, float thumbBlend)
        {
            if (Root == null) return;
            if (fingers.sqrMagnitude < 1e-6f) fingers = Vector3.forward;
            var up = Vector3.ProjectOnPlane(palmFacing, fingers);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, fingers);
            var rotation = Quaternion.LookRotation(fingers, up) * Quaternion.Inverse(restBasis);
            Root.SetPositionAndRotation(palmAt - rotation * palmLocal, rotation);
            // Pinching, the index curls just as far as this hand's thumb needs to meet it.
            float pinch = (thumbFrom == ThumbShape.Pinch ? 1f - thumbBlend : 0f) + (thumbTo == ThumbShape.Pinch ? thumbBlend : 0f);
            if (pinch > 0f) curl.x = Mathf.Lerp(curl.x, pinchIndexCurl, pinch);
            Curl(curl, spread);
            ApplyThumb(Vector3.Lerp(thumbShapes[(int)thumbFrom], thumbShapes[(int)thumbTo], thumbBlend));
        }

        /// <summary>
        /// The look for this frame: how visible (0-1), and up to two points on the hand that glow as the active fingers
        /// (xyz, and w for how strongly).
        /// </summary>
        public void SetLook(float alpha, Vector4 focusA, Vector4 focusB)
        {
            if (Renderer == null) return;
            Renderer.GetPropertyBlock(block);
            block.SetFloat(AlphaId, alpha);
            block.SetVector(FocusAId, focusA);
            block.SetVector(FocusBId, focusB);
            block.SetVector(WristPosId, wrist.position);
            block.SetVector(HandForwardId, (knuckle.position - wrist.position).normalized);
            Renderer.SetPropertyBlock(block);
        }

        void Curl(Vector4 curl, float spread)
        {
            for (int f = 0; f < 4; f++)
            {
                float c = curl[f];
                var spin = Quaternion.AngleAxis(SpreadAngles[f] * spread * spreadSign[f], spreadAxis[f]);
                for (int j = 0; j < 3; j++)
                {
                    int i = f * 3 + j;
                    var bend = Quaternion.AngleAxis(BendAngles[j] * c, jointAxis[i]);
                    joints[i].localRotation = j == 0 ? jointRest[i] * spin * bend : jointRest[i] * bend;
                }
            }
        }

        void ApplyThumb(Vector3 p)
        {
            thumbBase.localRotation = thumbBaseRest * Quaternion.AngleAxis(p.x * swingSign, swingAxis) * Quaternion.AngleAxis(p.y, flexAxis);
            thumbMid.localRotation = thumbMidRest * Quaternion.AngleAxis(p.z * 0.7f, thumbMidAxis);
            thumbEnd.localRotation = thumbEndRest * Quaternion.AngleAxis(p.z, thumbEndAxis);
        }


        /// <summary>A point on the thumb's side of the curled index, <paramref name="along"/> from its middle joint (0) to the next (1).</summary>
        Vector3 SideOfIndex(float along, float lift = 0f)
        {
            var mid = joints[1].position;
            var end = joints[2].position;
            var point = along >= 0f ? Vector3.Lerp(mid, end, along) : Vector3.Lerp(mid, joints[0].position, -along);
            // Towards the thumb: from the middle finger's knuckle to the index's, square to the index's middle bone.
            var side = Vector3.ProjectOnPlane(indexBase.position - middleBase.position, end - mid).normalized;
            return point + side * (0.0105f + lift);
        }

        /// <summary>Where the thumb tip goes to pinch: against the pad of the index tip, a little to the thumb's side.</summary>
        Vector3 PinchTarget()
        {
            var normal = PalmNormalNow();
            var toThumbSide = Vector3.ProjectOnPlane(indexBase.position - middleBase.position, normal).normalized;
            return tips[1].position + (toThumbSide * 0.6f + normal * 0.5f).normalized * 0.011f;
        }

        /// <summary>
        /// Finds each thumb shape: for each, the fingers take the curl it is made for (for a pinch, the index curl that
        /// lets the thumb meet it best), then a coarse search over the thumb's swing, flex and curl and a finer one
        /// around the best finds the pose that puts the thumb tip nearest its target, with a small preference for smaller
        /// angles so the thumb stays natural.
        /// </summary>
        void SolveThumbs()
        {
            thumbShapes[(int)ThumbShape.Rest] = Vector3.zero;

            float bestCost = float.MaxValue;
            for (float curl = 0.3f; curl <= 0.861f; curl += 0.04f)
            {
                Curl(PinchCurl(curl), 0f);
                float cost = Solve(PinchTarget(), true).w + Mathf.Abs(curl - 0.5f) * 0.004f;
                if (cost >= bestCost) continue;
                bestCost = cost;
                pinchIndexCurl = curl;
            }
            Curl(PinchCurl(pinchIndexCurl), 0f);
            thumbShapes[(int)ThumbShape.Pinch] = Solve(PinchTarget(), false);

            Curl(new Vector4(TapIndexCurl, 0.62f, 0.7f, 0.76f), 0f);
            thumbShapes[(int)ThumbShape.Tap] = Solve(SideOfIndex(0.3f), false);
            thumbShapes[(int)ThumbShape.Lift] = Solve(SideOfIndex(0.3f, 0.028f), false);
            thumbShapes[(int)ThumbShape.SwipeTip] = Solve(SideOfIndex(0.85f), false);
            thumbShapes[(int)ThumbShape.SwipeKnuckle] = Solve(SideOfIndex(-0.55f), false);

            // A pointing hand: the thumb rests on the outside of the curled middle finger.
            Curl(TuckCurl, 0f);
            thumbShapes[(int)ThumbShape.Tuck] = Solve(Vector3.Lerp(joints[4].position, joints[5].position, 0.45f) + PalmNormalNow() * 0.012f, false);

            // A fist: the thumb lies across the curled index and middle fingers.
            Curl(FistCurl, 0f);
            var across = Vector3.Lerp(Vector3.Lerp(joints[1].position, joints[2].position, 0.5f), Vector3.Lerp(joints[4].position, joints[5].position, 0.5f), 0.4f);
            thumbShapes[(int)ThumbShape.Fist] = Solve(across + PalmNormalNow() * 0.013f, false);
        }

        static Vector4 PinchCurl(float index) => new Vector4(index, 0.3f, 0.34f, 0.38f);

        Vector3 PalmNormalNow()
        {
            var littleBase = joints[9];
            var n = Vector3.Cross(indexBase.position - wrist.position, littleBase.position - wrist.position).normalized;
            return Left ? n : -n;
        }

        /// <summary>The thumb's swing, flex and curl that bring its tip nearest <paramref name="target"/>, and in w how near (m).</summary>
        Vector4 Solve(Vector3 target, bool coarse)
        {
            float Cost(Vector3 p)
            {
                ApplyThumb(p);
                return Vector3.Distance(tips[0].position, target) + 0.00003f * (Mathf.Abs(p.x) + Mathf.Abs(p.y) + Mathf.Abs(p.z));
            }

            var best = Vector3.zero;
            float bestCost = float.MaxValue;
            float grid = coarse ? 15f : 10f;
            for (float swing = -20f; swing <= 80f; swing += grid)
                for (float flex = -30f; flex <= 80f; flex += grid)
                    for (float curl = 0f; curl <= 90f; curl += 15f)
                    {
                        var p = new Vector3(swing, flex, curl);
                        float c = Cost(p);
                        if (c >= bestCost) continue;
                        bestCost = c;
                        best = p;
                    }
            for (float step = coarse ? 4f : 5f; step > (coarse ? 1f : 0.3f); step *= 0.5f)
            {
                for (int round = 0; round < 8; round++)
                {
                    bool better = false;
                    for (int axis = 0; axis < 3; axis++)
                        for (int dir = -1; dir <= 1; dir += 2)
                        {
                            var p = best;
                            p[axis] += step * dir;
                            p.z = Mathf.Clamp(p.z, -5f, 100f);
                            float c = Cost(p);
                            if (c >= bestCost) continue;
                            bestCost = c;
                            best = p;
                            better = true;
                        }
                    if (!better) break;
                }
            }
            ApplyThumb(best);
            return new Vector4(best.x, best.y, best.z, Vector3.Distance(tips[0].position, target));
        }
    }
}
