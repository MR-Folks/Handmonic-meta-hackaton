using UnityEngine;

namespace MusicInYourHands.Meta
{
    /// <summary>
    /// Passes Meta's thumb microgestures (a thumb tap on the side of the index finger, or a swipe along it) from both
    /// tracked hands to MusicGestures, which decides what they do. Meta reports a gesture once it is finished. Like Meta's
    /// OVRMicrogestureEventSource this reads every frame (which also tells the system the app wants microgestures), but
    /// it passes on only the first frame of a gesture, so one movement is never counted twice. Meta mirrors the swipes
    /// per hand, so a swipe left goes towards your left on either hand.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MicrogestureFeed : MonoBehaviour
    {
        [Tooltip("The tracked hands. Left empty, they are found under the camera rig's hand anchors.")]
        [SerializeField] OVRHand leftHand;
        [SerializeField] OVRHand rightHand;

        readonly OVRHand.MicrogestureType[] last = { OVRHand.MicrogestureType.NoGesture, OVRHand.MicrogestureType.NoGesture };
        float nextLookup;

        void Update()
        {
            if ((leftHand == null || rightHand == null) && Time.unscaledTime >= nextLookup) FindHands();
            Read(leftHand, 0);
            Read(rightHand, 1);
        }

        void Read(OVRHand hand, int side)
        {
            if (hand == null) return;
            var type = hand.GetMicrogestureType();
            bool repeat = type == last[side];
            last[side] = type;
            var gestures = MusicGestures.Instance;
            if (repeat || gestures == null) return;
            switch (type)
            {
                case OVRHand.MicrogestureType.ThumbTap: gestures.Recognize(side == 0, Microgesture.ThumbTap); break;
                case OVRHand.MicrogestureType.SwipeLeft: gestures.Recognize(side == 0, Microgesture.SwipeLeft); break;
                case OVRHand.MicrogestureType.SwipeRight: gestures.Recognize(side == 0, Microgesture.SwipeRight); break;
                case OVRHand.MicrogestureType.SwipeForward: gestures.Recognize(side == 0, Microgesture.SwipeForward); break;
                case OVRHand.MicrogestureType.SwipeBackward: gestures.Recognize(side == 0, Microgesture.SwipeBackward); break;
            }
        }

        void FindHands()
        {
            // Looked for again each second until both are there.
            nextLookup = Time.unscaledTime + 1f;
            var rig = FindAnyObjectByType<OVRCameraRig>();
            if (rig == null) return;
            if (leftHand == null && rig.leftHandAnchor != null) leftHand = rig.leftHandAnchor.GetComponentInChildren<OVRHand>(true);
            if (rightHand == null && rig.rightHandAnchor != null) rightHand = rig.rightHandAnchor.GetComponentInChildren<OVRHand>(true);
        }
    }
}
