using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The player's body in world space, for worlds that need more than the hands: each arm's shoulder, elbow and
    /// wrist, the hips, chest, neck and head, and each leg's hip, knee, ankle and toes (with full-body tracking; the legs
    /// stay invalid with the upper body only). Tracking the body costs the headset some work, so it runs only while a world
    /// asks for it: set <see cref="Wanted"/> in OnEnter and clear it in OnExit. A body feed (BodyFeed on Quest, with
    /// Meta's body tracking) fills the joints each frame while the body is wanted, so worlds never depend on a tracking
    /// SDK. Without a feed, without the player's permission, or while the body is lost, <see cref="IsTracked"/> is false
    /// and <see cref="Blend"/> fades to 0, and worlds fall back on what the hands alone tell them.
    /// </summary>
    public static class BodyRig
    {
        public sealed class Arm
        {
            /// <summary>The shoulder joint, where the upper arm starts.</summary>
            public Vector3 Shoulder;
            public Vector3 Elbow;
            /// <summary>
            /// The body tracker's wrist. It can sit a few centimetres from the hand tracker's wrist; to meet the visible
            /// hand exactly, start a forearm at the hand's own wrist joint instead.
            /// </summary>
            public Vector3 Wrist;
            /// <summary>True when all three joints came with a valid position this frame.</summary>
            public bool Valid;
        }

        public sealed class Leg
        {
            /// <summary>The hip joint, where the thigh starts.</summary>
            public Vector3 Hip;
            public Vector3 Knee;
            public Vector3 Ankle;
            /// <summary>The ball of the foot, where the toes start.</summary>
            public Vector3 Toes;
            /// <summary>True when all four joints came with a valid position this frame.</summary>
            public bool Valid;
        }

        /// <summary>Seconds <see cref="Blend"/> takes to rise from 0 to 1, or to fall back, as the body is found or lost.</summary>
        public const float FadeSeconds = 0.35f;

        /// <summary>Set by a world while it needs the body (OnEnter), cleared when it no longer does (OnExit).</summary>
        public static bool Wanted;

        /// <summary>True while the feed has fresh joints this frame.</summary>
        public static bool IsTracked { get; private set; }

        /// <summary>How sure the body tracker is of the pose, 0-1, as it reports it.</summary>
        public static float Confidence { get; private set; }

        /// <summary>
        /// 0-1, eased: how much to trust the body this frame. It rises over <see cref="FadeSeconds"/> while the body is
        /// tracked with good confidence and falls back as it is lost, so a world can fade between the body and its
        /// hands-only estimate without popping.
        /// </summary>
        public static float Blend { get; private set; }

        /// <summary>True when the chest, neck and head came with valid positions this frame.</summary>
        public static bool SpineValid { get; private set; }

        public static Vector3 Chest { get; private set; }
        public static Vector3 Neck { get; private set; }
        public static Vector3 Head { get; private set; }

        /// <summary>True when the hips (the root of the spine, between the legs) came with a valid position this frame.</summary>
        public static bool HipsValid { get; private set; }

        public static Vector3 Hips { get; private set; }

        public static Arm Left { get; } = new Arm();
        public static Arm Right { get; } = new Arm();

        public static Arm Get(bool left) => left ? Left : Right;

        public static Leg LeftLeg { get; } = new Leg();
        public static Leg RightLeg { get; } = new Leg();

        public static Leg GetLeg(bool left) => left ? LeftLeg : RightLeg;

        // Below this confidence the body counts for nothing, from the upper value fully.
        const float LowConfidence = 0.2f, GoodConfidence = 0.5f;

        /// <summary>Called by a body feed with one arm's joints, before <see cref="Push"/>.</summary>
        public static void SetArm(bool left, bool valid, Vector3 shoulder, Vector3 elbow, Vector3 wrist)
        {
            var arm = Get(left);
            arm.Valid = valid;
            arm.Shoulder = shoulder;
            arm.Elbow = elbow;
            arm.Wrist = wrist;
        }

        /// <summary>Called by a body feed with the chest, neck and head, before <see cref="Push"/>.</summary>
        public static void SetSpine(bool valid, Vector3 chest, Vector3 neck, Vector3 head)
        {
            SpineValid = valid;
            Chest = chest;
            Neck = neck;
            Head = head;
        }

        /// <summary>Called by a body feed with the hips, before <see cref="Push"/>.</summary>
        public static void SetHips(bool valid, Vector3 hips)
        {
            HipsValid = valid;
            Hips = hips;
        }

        /// <summary>Called by a body feed with one leg's joints (invalid without full-body tracking), before <see cref="Push"/>.</summary>
        public static void SetLeg(bool left, bool valid, Vector3 hip, Vector3 knee, Vector3 ankle, Vector3 toes)
        {
            var leg = GetLeg(left);
            leg.Valid = valid;
            leg.Hip = hip;
            leg.Knee = knee;
            leg.Ankle = ankle;
            leg.Toes = toes;
        }

        /// <summary>Called by a body feed once per frame after setting the joints: the body is tracked this frame.</summary>
        public static void Push(float confidence, float dt)
        {
            IsTracked = true;
            Confidence = confidence;
            float target = Mathf.InverseLerp(LowConfidence, GoodConfidence, confidence);
            Blend = Mathf.MoveTowards(Blend, target, dt / FadeSeconds);
        }

        /// <summary>
        /// Called by a body feed on a frame without a body (not found yet, no permission, lost). The joints keep their
        /// last values so a world fading out still has somewhere to draw.
        /// </summary>
        public static void Lose(float dt)
        {
            IsTracked = false;
            Confidence = 0f;
            Blend = Mathf.MoveTowards(Blend, 0f, dt / FadeSeconds);
        }

        /// <summary>Forgets the body at once: called when nothing wants it any more.</summary>
        public static void Clear()
        {
            IsTracked = false;
            Confidence = 0f;
            Blend = 0f;
            SpineValid = HipsValid = false;
            Left.Valid = Right.Valid = false;
            LeftLeg.Valid = RightLeg.Valid = false;
        }

        // Entering play mode without a domain reload keeps statics; start clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            Wanted = false;
            Clear();
        }
    }
}
