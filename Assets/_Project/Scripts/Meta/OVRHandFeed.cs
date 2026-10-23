using UnityEngine;

namespace MusicInYourHands.Meta
{
    /// <summary>Feeds one Meta tracked hand (OVRHand + OVRSkeleton, OpenXR bones) into HandRig.</summary>
    [DefaultExecutionOrder(-50)]
    public class OVRHandFeed : MonoBehaviour
    {
        static readonly OVRSkeleton.BoneId[] TipIds =
        {
            OVRSkeleton.BoneId.XRHand_ThumbTip, OVRSkeleton.BoneId.XRHand_IndexTip, OVRSkeleton.BoneId.XRHand_MiddleTip,
            OVRSkeleton.BoneId.XRHand_RingTip, OVRSkeleton.BoneId.XRHand_LittleTip
        };

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

        void Update()
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
            if (!skeleton.IsInitialized || skeleton.Bones == null) return false;
            foreach (var bone in skeleton.Bones)
            {
                switch (bone.Id)
                {
                    case OVRSkeleton.BoneId.XRHand_Palm: palm = bone.Transform; break;
                    case OVRSkeleton.BoneId.XRHand_Wrist: wrist = bone.Transform; break;
                    case OVRSkeleton.BoneId.XRHand_IndexProximal: indexBase = bone.Transform; break;
                    case OVRSkeleton.BoneId.XRHand_LittleProximal: littleBase = bone.Transform; break;
                }
                int tip = System.Array.IndexOf(TipIds, bone.Id);
                if (tip >= 0) tips[tip] = bone.Transform;
            }

            bool found = palm != null && wrist != null && indexBase != null && littleBase != null;
            foreach (var t in tips) found &= t != null;
            if (!found) palm = null;
            return found;
        }
    }
}
