using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// One place for every world to read the player's hands in world space: palm, fingertips,
    /// pinch, fist and palm-up. A hand feed (OVRHandFeed on Quest) pushes fresh joints each frame,
    /// so worlds never depend on a particular tracking SDK.
    /// </summary>
    public class HandRig : MonoBehaviour
    {
        public enum Finger { Thumb, Index, Middle, Ring, Little }

        public sealed class Hand
        {
            public bool IsTracked;
            public Pose Palm;
            public readonly Vector3[] Tips = new Vector3[5];
            /// <summary>Points out of the palm, away from the back of the hand.</summary>
            public Vector3 PalmNormal;
            /// <summary>0 when thumb and index are apart, 1 when they touch.</summary>
            public float Pinch;
            /// <summary>0 for an open hand, 1 for a closed fist.</summary>
            public float Fist;
            public bool PalmUp;
            public Vector3 Velocity;
            /// <summary>True while the palm faces the headset and Meta's menu button shows. Ignore pinches then.</summary>
            public bool InSystemGesture;

            /// <summary>
            /// The joint transforms that move the visible hand, when the feed has them. Attach visuals to these to
            /// stay exactly on the hand: positions above are sampled earlier in the frame than the late pose update.
            /// </summary>
            public Transform PalmJoint;
            public readonly Transform[] TipJoints = new Transform[5];

            internal int LastFrame = -1;

            public Vector3 Tip(Finger f) => Tips[(int)f];
        }

        public static HandRig Instance { get; private set; }

        [SerializeField, Range(0f, 1f)] float palmUpThreshold = 0.7f;

        public Hand Left { get; } = new Hand();
        public Hand Right { get; } = new Hand();

        public Hand Get(bool left) => left ? Left : Right;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Called by a hand feed once it has found the joints of the visible hand.</summary>
        public void SetJoints(bool left, Transform palm, Transform[] tips)
        {
            var hand = Get(left);
            hand.PalmJoint = palm;
            for (int i = 0; i < hand.TipJoints.Length; i++) hand.TipJoints[i] = tips[i];
        }

        /// <summary>
        /// Called by a hand feed, once per frame and optionally again right before rendering with the late pose.
        /// Pass tracked = false when the hand is lost.
        /// </summary>
        public void Push(bool left, bool tracked, Pose palm, Vector3 palmNormal, Vector3[] tips)
        {
            var hand = Get(left);
            bool wasTracked = hand.IsTracked;
            hand.IsTracked = tracked;
            if (!tracked) return;

            // Velocity only from the first sample each frame; the late sample would divide a tiny move by a whole frame.
            if (hand.LastFrame != Time.frameCount)
            {
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                hand.Velocity = wasTracked ? (palm.position - hand.Palm.position) / dt : Vector3.zero;
                hand.LastFrame = Time.frameCount;
            }
            hand.Palm = palm;
            hand.PalmNormal = palmNormal;
            hand.PalmUp = Vector3.Dot(palmNormal, Vector3.up) > palmUpThreshold;
            for (int i = 0; i < hand.Tips.Length; i++) hand.Tips[i] = tips[i];

            hand.Pinch = Mathf.InverseLerp(0.05f, 0.015f, Vector3.Distance(hand.Tips[0], hand.Tips[1]));
            float curl = 0f;
            for (int i = 1; i < 5; i++) curl += Vector3.Distance(hand.Tips[i], palm.position);
            hand.Fist = Mathf.InverseLerp(0.09f, 0.045f, curl / 4f);
        }
    }
}
