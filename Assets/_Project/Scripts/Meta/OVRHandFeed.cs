using UnityEngine;

namespace MusicInYourHands.Meta
{
    /// <summary>
    /// Feeds one Meta tracked hand into HandRig. Joints are looked up by name in Meta's OpenXR hand prefab
    /// (XRHand_IndexTip and so on): those are the transforms that bend the visible hand mesh, so the data
    /// always matches the hand the player sees. Looking them up by OVRSkeleton bone id did not, because the
    /// prefab's custom skeleton maps ids to its own bone list.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class OVRHandFeed : MonoBehaviour
    {
        static readonly string[] TipNames = { "XRHand_ThumbTip", "XRHand_IndexTip", "XRHand_MiddleTip", "XRHand_RingTip", "XRHand_LittleTip" };

        [SerializeField] OVRHand hand;
        [SerializeField] OVRSkeleton skeleton;
        [SerializeField] bool isLeft;

        readonly Transform[] tips = new Transform[5];
        readonly Vector3[] tipPositions = new Vector3[5];
        Transform palm, wrist, indexBase, littleBase;

        public void Configure(OVRHand trackedHand, OVRSkeleton trackedSkeleton, bool left)
        {
            hand = trackedHand;
            skeleton = trackedSkeleton;
            isLeft = left;
        }

        void OnEnable() => Application.onBeforeRender += Sample;
        void OnDisable() => Application.onBeforeRender -= Sample;

        // Runs after OVRCameraRig and OVRSkeleton update (execution order 100), and again right before rendering,
        // after OVRCameraRig's late pose update, so HandRig matches the hand that is actually drawn.
        void Update() => Sample();

        void Sample()
        {
            var rig = HandRig.Instance;
            if (rig == null || hand == null || skeleton == null) return;

            // The system gesture (palm up, pinch) opens the Meta menu, so treat the hand as busy.
            bool tracked = hand.IsTracked && skeleton.IsDataValid && !hand.IsSystemGestureInProgress && (palm != null || FindBones());
            if (!tracked)
            {
                rig.Push(isLeft, false, default, default, null);
                return;
            }

            // Palm normal from the knuckle plane, so it does not depend on bone axis conventions.
            var toIndex = indexBase.position - wrist.position;
            var toLittle = littleBase.position - wrist.position;
            var normal = Vector3.Cross(toIndex, toLittle).normalized;
            if (!isLeft) normal = -normal;

            for (int i = 0; i < tips.Length; i++) tipPositions[i] = tips[i].position;
            rig.Push(isLeft, true, new Pose(palm.position, palm.rotation), normal, tipPositions);
        }

        bool FindBones()
        {
            var root = skeleton.transform;
            palm = Find(root, "XRHand_Palm");
            wrist = Find(root, "XRHand_Wrist");
            indexBase = Find(root, "XRHand_IndexProximal");
            littleBase = Find(root, "XRHand_LittleProximal");
            for (int i = 0; i < TipNames.Length; i++) tips[i] = Find(root, TipNames[i]);

            bool found = palm != null && wrist != null && indexBase != null && littleBase != null;
            foreach (var t in tips) found &= t != null;
            if (!found)
            {
                palm = null;
                return false;
            }
            if (HandRig.Instance != null) HandRig.Instance.SetJoints(isLeft, palm, tips);
            return true;
        }

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
    }
}
