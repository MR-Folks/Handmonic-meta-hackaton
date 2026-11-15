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
        [Tooltip("Log each gesture Meta reports, and every few seconds how many frames each hand had no valid gesture " +
                 "state, to see on the headset (adb logcat -s Unity) whether gestures arrive at all.")]
        [SerializeField] bool logGestures = true;
        [SerializeField] float reportSeconds = 5f;

        readonly OVRHand.MicrogestureType[] last = { OVRHand.MicrogestureType.NoGesture, OVRHand.MicrogestureType.NoGesture };
        readonly int[] invalidFrames = new int[2], quietFrames = new int[2], gestureCount = new int[2];
        float nextLookup, nextReport;

        void Awake() => MusicGestures.LogGestures = logGestures;

        void Update()
        {
            if ((leftHand == null || rightHand == null) && Time.unscaledTime >= nextLookup) FindHands();
            Read(leftHand, 0);
            Read(rightHand, 1);
            if (logGestures && Time.unscaledTime >= nextReport) Report();
        }

        void Read(OVRHand hand, int side)
        {
            if (hand == null) return;
            var type = hand.GetMicrogestureType();
            if (type == OVRHand.MicrogestureType.Invalid) invalidFrames[side]++;
            else if (type == OVRHand.MicrogestureType.NoGesture) quietFrames[side]++;
            bool repeat = type == last[side];
            last[side] = type;
            if (repeat || type == OVRHand.MicrogestureType.Invalid || type == OVRHand.MicrogestureType.NoGesture) return;
            gestureCount[side]++;
            if (logGestures) Debug.Log($"[Microgestures] {(side == 0 ? "left" : "right")} hand: Meta reports {type}");
            var gestures = MusicGestures.Instance;
            if (gestures == null) return;
            switch (type)
            {
                case OVRHand.MicrogestureType.ThumbTap: gestures.Recognize(side == 0, Microgesture.ThumbTap); break;
                case OVRHand.MicrogestureType.SwipeLeft: gestures.Recognize(side == 0, Microgesture.SwipeLeft); break;
                case OVRHand.MicrogestureType.SwipeRight: gestures.Recognize(side == 0, Microgesture.SwipeRight); break;
                case OVRHand.MicrogestureType.SwipeForward: gestures.Recognize(side == 0, Microgesture.SwipeForward); break;
                case OVRHand.MicrogestureType.SwipeBackward: gestures.Recognize(side == 0, Microgesture.SwipeBackward); break;
            }
        }

        /// <summary>Every few seconds: whether each hand was found, and how its gesture state looked frame by frame.</summary>
        void Report()
        {
            nextReport = Time.unscaledTime + reportSeconds;
            Debug.Log($"[Microgestures] last {reportSeconds:0} s: left {Describe(leftHand, 0)}; right {Describe(rightHand, 1)}; " +
                      $"MusicGestures {(MusicGestures.Instance != null ? "present" : "missing")}");
            for (int s = 0; s < 2; s++) invalidFrames[s] = quietFrames[s] = gestureCount[s] = 0;
        }

        string Describe(OVRHand hand, int side) => hand == null ? "hand not found"
            : $"tracked {hand.IsTracked}, frames invalid {invalidFrames[side]} / no gesture {quietFrames[side]}, gestures {gestureCount[side]}";

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
