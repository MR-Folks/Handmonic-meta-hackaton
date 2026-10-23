using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace MusicInYourHands
{
    /// <summary>
    /// One place for every world to read the player's hands: palm, fingertips, pinch, fist and
    /// palm-up, in world space. Reads Unity's XR Hands subsystem, which Meta's SDK feeds on Quest.
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

            public Vector3 Tip(Finger f) => Tips[(int)f];
        }

        static readonly XRHandJointID[] TipJoints =
        {
            XRHandJointID.ThumbTip, XRHandJointID.IndexTip, XRHandJointID.MiddleTip, XRHandJointID.RingTip, XRHandJointID.LittleTip
        };

        public static HandRig Instance { get; private set; }

        [Tooltip("The XR rig's tracking space (OVRCameraRig > TrackingSpace). Found by name when left empty.")]
        [SerializeField] Transform trackingSpace;
        [SerializeField, Range(0f, 1f)] float palmUpThreshold = 0.7f;

        static readonly List<XRHandSubsystem> Subsystems = new List<XRHandSubsystem>();
        XRHandSubsystem subsystem;

        public Hand Left { get; } = new Hand();
        public Hand Right { get; } = new Hand();

        void Awake()
        {
            Instance = this;
            if (trackingSpace == null)
            {
                var found = GameObject.Find("TrackingSpace");
                if (found != null) trackingSpace = found.transform;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (subsystem == null || !subsystem.running)
            {
                subsystem = null;
                SubsystemManager.GetSubsystems(Subsystems);
                foreach (var s in Subsystems)
                {
                    if (s.running) { subsystem = s; break; }
                }
                if (subsystem == null)
                {
                    Left.IsTracked = Right.IsTracked = false;
                    return;
                }
            }

            Read(subsystem.leftHand, Left);
            Read(subsystem.rightHand, Right);
        }

        void Read(XRHand source, Hand hand)
        {
            hand.IsTracked = source.isTracked;
            if (!hand.IsTracked) return;

            if (source.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm))
            {
                var world = ToWorld(palm);
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                hand.Velocity = (world.position - hand.Palm.position) / dt;
                hand.Palm = world;
                // OpenXR joints use +Y for the back of the hand, so the palm faces -Y.
                hand.PalmNormal = world.rotation * Vector3.down;
                hand.PalmUp = Vector3.Dot(hand.PalmNormal, Vector3.up) > palmUpThreshold;
            }

            for (int i = 0; i < TipJoints.Length; i++)
            {
                if (source.GetJoint(TipJoints[i]).TryGetPose(out var tip)) hand.Tips[i] = ToWorld(tip).position;
            }

            hand.Pinch = Mathf.InverseLerp(0.05f, 0.015f, Vector3.Distance(hand.Tips[0], hand.Tips[1]));
            float curl = 0f;
            for (int i = 1; i < 5; i++) curl += Vector3.Distance(hand.Tips[i], hand.Palm.position);
            hand.Fist = Mathf.InverseLerp(0.09f, 0.045f, curl / 4f);
        }

        Pose ToWorld(Pose local) =>
            trackingSpace == null ? local : new Pose(trackingSpace.TransformPoint(local.position), trackingSpace.rotation * local.rotation);
    }
}
