using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The gestures a ghost hand can show. The first three are the onboarding's lessons; the rest are the small
    /// vocabulary the world hints use (see <see cref="GhostMotions.ForWorld"/>).
    /// </summary>
    public enum GhostMotion
    {
        None,
        /// <summary>Lesson 1, left hand: the palm turns to your face and thumb and index pinch (Meta's menu gesture).</summary>
        MenuGesture,
        /// <summary>Lesson 2, right hand: the index points at the menu and pokes it.</summary>
        TouchMenu,
        /// <summary>Lesson 3: a loosely curled hand taps its thumb on the side of the index twice, then swipes along it.</summary>
        ThumbTapSwipe,
        OpenPalm,
        PalmUp,
        PalmsFacing,
        Pinch,
        TwoHandPinch,
        PointPoke,
        Fist,
        BothFists,
        Sweep,
        SpreadFingers,
        Push,
        Dip,
        TwoHandFrame,
        PointUp
    }

    /// <summary>
    /// One moment of a ghost motion for one hand, in the viewer's level frame (x to the right, y up, z away from you),
    /// written for the right hand: the left hand mirrors it unless the motion gives the left hand keys of its own.
    /// </summary>
    public struct GhostKey
    {
        public float Time;
        /// <summary>Where the palm is, from the hand's place beside your hand (m).</summary>
        public Vector3 Offset;
        /// <summary>The way the fingers point, and the way the palm faces.</summary>
        public Vector3 Fingers, Palm;
        /// <summary>0-1: turns the palm to face your eyes instead (the menu gesture, spread fingers).</summary>
        public float FaceView;
        /// <summary>0-1: points the fingers at the menu instead (touch a world).</summary>
        public float AimMenu;
        /// <summary>How far the hand has moved along its fingers (a poke), m.</summary>
        public float Reach;
        /// <summary>Curl of the index, middle, ring and little fingers, 0 straight to 1 closed.</summary>
        public Vector4 Curl;
        /// <summary>0-1: fingers spread apart.</summary>
        public float Spread;
        public ThumbShape Thumb;
        /// <summary>0-1: how strongly the active fingers light up at this moment (on contact, on the poke).</summary>
        public float Glow;
    }

    /// <summary>A ghost hand's pose at one moment, eased between two keys.</summary>
    public struct GhostPose
    {
        public Vector3 Offset, Fingers, Palm;
        public float FaceView, AimMenu, Reach, Spread, Glow;
        public Vector4 Curl;
        public ThumbShape ThumbFrom, ThumbTo;
        public float ThumbBlend;
    }

    /// <summary>
    /// The ghost motions as looping keyframes, and which motion each world's hint shows. Keys ease in and out of each
    /// other and the last one eases back into the first, so every motion loops smoothly.
    /// </summary>
    public static class GhostMotions
    {
        /// <summary>
        /// A poke's full reach (m) in the keys. Aimed at the menu, a poke of this reach ends with the fingertip just short
        /// of the bubble it points at, however far the menu is.
        /// </summary>
        public const float FullPoke = 0.075f;

        // Finger curls: index, middle, ring, little.
        static readonly Vector4 Open = new Vector4(0.03f, 0.04f, 0.07f, 0.1f);
        static readonly Vector4 Relaxed = new Vector4(0.16f, 0.2f, 0.26f, 0.3f);
        static readonly Vector4 PinchCurl = new Vector4(GhostHand.PinchIndexCurl, 0.3f, 0.34f, 0.38f);
        static readonly Vector4 Loose = new Vector4(GhostHand.TapIndexCurl, 0.62f, 0.7f, 0.76f);
        static readonly Vector4 Point = GhostHand.TuckCurl;
        static readonly Vector4 Closed = GhostHand.FistCurl;
        static readonly Vector4 Cupped = new Vector4(0.34f, 0.4f, 0.46f, 0.52f);

        static GhostKey K(float time, Vector3 fingers, Vector3 palm, Vector4 curl, ThumbShape thumb = ThumbShape.Rest,
            Vector3 offset = default, float faceView = 0f, float aimMenu = 0f, float reach = 0f, float spread = 0f, float glow = 0f) =>
            new GhostKey
            {
                Time = time, Offset = offset, Fingers = fingers, Palm = palm, FaceView = faceView, AimMenu = aimMenu, Reach = reach,
                Curl = curl, Spread = spread, Thumb = thumb, Glow = glow
            };

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        sealed class Motion
        {
            public float Duration;
            public bool TwoHanded;
            /// <summary>Which fingertips light up: bit 0 thumb, 1 index, 2 middle, 3 ring, 4 little.</summary>
            public int Active;
            public GhostKey[] Right;
            /// <summary>The left hand's own keys, or null to mirror the right hand's.</summary>
            public GhostKey[] Left;
        }

        const int Thumb = 1, Index = 2, Middle = 4, Little = 16;

        // ---- The lessons ----

        // The left palm turns from facing in to facing your eyes, then thumb and index pinch, hold, and let go.
        static readonly Motion MenuGestureMotion = new Motion
        {
            Duration = 4.2f, Active = Thumb | Index, Right = new[]
            {
                K(0f, V(0.12f, 1f, 0.45f), V(-1f, 0f, 0.2f), Relaxed),
                K(0.5f, V(0.12f, 1f, 0.45f), V(-1f, 0f, 0.2f), Relaxed),
                K(1.3f, V(0.06f, 1f, 0.25f), V(-0.3f, 0f, -1f), Relaxed, offset: V(0f, 0.02f, 0f), faceView: 1f),
                K(1.8f, V(0.06f, 1f, 0.25f), V(-0.3f, 0f, -1f), Relaxed, offset: V(0f, 0.02f, 0f), faceView: 1f),
                K(2.35f, V(0.06f, 1f, 0.25f), V(-0.3f, 0f, -1f), PinchCurl, ThumbShape.Pinch, V(0f, 0.02f, 0f), faceView: 1f, glow: 1f),
                K(3f, V(0.06f, 1f, 0.25f), V(-0.3f, 0f, -1f), PinchCurl, ThumbShape.Pinch, V(0f, 0.02f, 0f), faceView: 1f, glow: 1f),
                K(3.45f, V(0.06f, 1f, 0.25f), V(-0.3f, 0f, -1f), Relaxed, offset: V(0f, 0.02f, 0f), faceView: 1f)
            }
        };

        // The right index points at the menu, pokes it and comes back.
        static readonly Motion TouchMenuMotion = new Motion
        {
            Duration = 2.6f, Active = Index, Right = new[]
            {
                K(0f, V(-0.2f, 0.3f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck, aimMenu: 1f),
                K(0.7f, V(-0.2f, 0.3f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck, aimMenu: 1f),
                K(1.2f, V(-0.2f, 0.3f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck, aimMenu: 1f, reach: FullPoke, glow: 1f),
                K(1.45f, V(-0.2f, 0.3f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck, aimMenu: 1f, reach: FullPoke, glow: 1f),
                K(2f, V(-0.2f, 0.3f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck, aimMenu: 1f)
            }
        };

        // A loosely curled hand held across in front of you, its thumb side towards you so the thumb shows clearly: two taps
        // on the side of the index, then a swipe along it.
        static readonly Vector3 MicroFingers = V(-0.8f, 0.25f, 0.55f), MicroPalm = V(-0.5f, -0.65f, -0.45f);
        static readonly Motion ThumbMotion = new Motion
        {
            Duration = 4.4f, Active = Thumb | Index, Right = new[]
            {
                K(0f, MicroFingers, MicroPalm, Loose, ThumbShape.Lift),
                K(0.35f, MicroFingers, MicroPalm, Loose, ThumbShape.Tap, glow: 1f),
                K(0.6f, MicroFingers, MicroPalm, Loose, ThumbShape.Lift),
                K(0.95f, MicroFingers, MicroPalm, Loose, ThumbShape.Tap, glow: 1f),
                K(1.25f, MicroFingers, MicroPalm, Loose, ThumbShape.Lift),
                K(1.75f, MicroFingers, MicroPalm, Loose, ThumbShape.SwipeTip, glow: 0.6f),
                K(2.45f, MicroFingers, MicroPalm, Loose, ThumbShape.SwipeKnuckle, glow: 1f),
                K(2.75f, MicroFingers, MicroPalm, Loose, ThumbShape.SwipeKnuckle, glow: 0.6f),
                K(3.2f, MicroFingers, MicroPalm, Loose, ThumbShape.Lift)
            }
        };

        // ---- The world hints' vocabulary ----

        // Held out in front, palm down and forward, opening and drifting slowly from side to side.
        static readonly Motion OpenPalmMotion = new Motion
        {
            Duration = 3.8f, Active = 0, Right = new[]
            {
                K(0f, V(0.05f, 0.35f, 1f), V(0f, -0.8f, 0.55f), Relaxed, offset: V(-0.03f, 0f, 0f)),
                K(1f, V(0.05f, 0.35f, 1f), V(0f, -0.8f, 0.55f), Open, offset: V(-0.02f, 0.01f, 0f), reach: 0.06f, spread: 0.5f, glow: 0.5f),
                K(2.1f, V(0.1f, 0.35f, 1f), V(0.1f, -0.8f, 0.55f), Open, offset: V(0.05f, 0.02f, 0f), reach: 0.06f, spread: 0.5f, glow: 0.5f),
                K(2.9f, V(0.08f, 0.35f, 1f), V(0.05f, -0.8f, 0.55f), Open, offset: V(0.03f, 0.01f, 0f), reach: 0.045f, spread: 0.3f)
            }
        };

        // Fingers forward, palm up, lifting as if raising something.
        static readonly Motion PalmUpMotion = new Motion
        {
            Duration = 3.2f, Active = 0, Right = new[]
            {
                K(0f, V(0f, 0.15f, 1f), V(0f, 1f, 0.1f), Relaxed, offset: V(0f, -0.03f, 0f)),
                K(1f, V(0f, 0.15f, 1f), V(0f, 1f, 0.1f), Open, offset: V(0f, 0.06f, 0f), spread: 0.25f, glow: 0.5f),
                K(1.8f, V(0f, 0.15f, 1f), V(0f, 1f, 0.1f), Open, offset: V(0f, 0.06f, 0f), spread: 0.25f),
                K(2.6f, V(0f, 0.15f, 1f), V(0f, 1f, 0.1f), Relaxed, offset: V(0f, -0.03f, 0f))
            }
        };

        // Both palms facing each other, closing in as if around a ball and opening again.
        static readonly Motion PalmsFacingMotion = new Motion
        {
            Duration = 3.6f, TwoHanded = true, Active = 0, Right = new[]
            {
                K(0f, V(0f, 0.65f, 0.75f), V(-1f, 0f, -0.1f), Cupped, offset: V(0.11f, 0f, 0f)),
                K(1.2f, V(0f, 0.65f, 0.75f), V(-1f, 0f, -0.1f), Relaxed, offset: V(0.065f, 0.01f, 0f), glow: 0.5f),
                K(2f, V(0f, 0.65f, 0.75f), V(-1f, 0f, -0.1f), Relaxed, offset: V(0.065f, 0.01f, 0f), glow: 0.5f),
                K(3f, V(0f, 0.65f, 0.75f), V(-1f, 0f, -0.1f), Cupped, offset: V(0.11f, 0f, 0f))
            }
        };

        // Thumb and index pinch, draw a short stroke while pinched, and let go.
        static readonly Vector3 PinchFingers = V(-0.3f, 0.5f, 1f), PinchPalm = V(-0.85f, -0.45f, 0.1f);
        static readonly Motion PinchMotion = new Motion
        {
            Duration = 3.4f, Active = Thumb | Index, Right = new[]
            {
                K(0f, PinchFingers, PinchPalm, Relaxed, offset: V(-0.03f, 0f, 0f)),
                K(0.6f, PinchFingers, PinchPalm, PinchCurl, ThumbShape.Pinch, V(-0.03f, 0f, 0f), glow: 1f),
                K(1f, PinchFingers, PinchPalm, PinchCurl, ThumbShape.Pinch, V(-0.03f, 0f, 0f), glow: 1f),
                K(2.1f, PinchFingers, PinchPalm, PinchCurl, ThumbShape.Pinch, V(0.05f, 0.04f, 0.02f), glow: 1f),
                K(2.6f, PinchFingers, PinchPalm, Relaxed, offset: V(0.05f, 0.04f, 0.02f))
            }
        };

        // Both hands pinch close together, pull apart while pinched, and let go.
        static readonly Motion TwoHandPinchMotion = new Motion
        {
            Duration = 3.8f, TwoHanded = true, Active = Thumb | Index, Right = new[]
            {
                K(0f, PinchFingers, PinchPalm, Relaxed, offset: V(0.07f, 0f, 0f)),
                K(0.6f, PinchFingers, PinchPalm, PinchCurl, ThumbShape.Pinch, V(0.07f, 0f, 0f), glow: 1f),
                K(1f, PinchFingers, PinchPalm, PinchCurl, ThumbShape.Pinch, V(0.07f, 0f, 0f), glow: 1f),
                K(2.3f, PinchFingers, PinchPalm, PinchCurl, ThumbShape.Pinch, V(0.15f, 0.01f, 0f), glow: 1f),
                K(2.8f, PinchFingers, PinchPalm, Relaxed, offset: V(0.15f, 0.01f, 0f))
            }
        };

        // The index points ahead and pokes.
        static readonly Motion PointPokeMotion = new Motion
        {
            Duration = 2.6f, Active = Index, Right = new[]
            {
                K(0f, V(-0.1f, 0.25f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck),
                K(0.7f, V(-0.1f, 0.25f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck),
                K(1.2f, V(-0.1f, 0.25f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck, reach: FullPoke, glow: 1f),
                K(1.45f, V(-0.1f, 0.25f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck, reach: FullPoke, glow: 1f),
                K(2f, V(-0.1f, 0.25f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck)
            }
        };

        // The index rises from pointing ahead to pointing up at the sky, a little ahead, holds there glowing, and comes down.
        static readonly Vector3 SkyFingers = V(-0.15f, 1.6f, 0.7f), SkyPalm = V(-1f, 0f, 0.2f), SkyLift = V(0f, 0.1f, 0.03f);
        static readonly Motion PointUpMotion = new Motion
        {
            Duration = 3.4f, Active = Index, Right = new[]
            {
                K(0f, V(-0.1f, 0.25f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck),
                K(0.8f, SkyFingers, SkyPalm, Point, ThumbShape.Tuck, SkyLift, glow: 1f),
                K(2.1f, SkyFingers, SkyPalm, Point, ThumbShape.Tuck, SkyLift, glow: 0.7f),
                K(2.8f, V(-0.1f, 0.25f, 1f), V(-0.4f, -1f, 0f), Point, ThumbShape.Tuck)
            }
        };

        // An open hand clenches into a fist with a firm little drop, holds, and opens.
        static readonly Vector3 FistFingers = V(-0.1f, 0.55f, 0.85f), FistPalm = V(-1f, -0.1f, -0.15f);
        static readonly Motion FistMotion = new Motion
        {
            Duration = 2.9f, Active = Index | Middle, Right = new[]
            {
                K(0f, FistFingers, FistPalm, Relaxed),
                K(0.5f, FistFingers, FistPalm, Closed, ThumbShape.Fist, V(0f, 0.02f, 0f), glow: 1f),
                K(0.75f, FistFingers, FistPalm, Closed, ThumbShape.Fist, V(0f, -0.03f, 0.02f), glow: 1f),
                K(1.7f, FistFingers, FistPalm, Closed, ThumbShape.Fist, V(0f, -0.03f, 0.02f), glow: 0.6f),
                K(2.2f, FistFingers, FistPalm, Relaxed)
            }
        };

        // Both hands clench and hold for a moment.
        static readonly Motion BothFistsMotion = new Motion
        {
            Duration = 3.2f, TwoHanded = true, Active = Index | Middle, Right = new[]
            {
                K(0f, FistFingers, FistPalm, Relaxed, offset: V(0.1f, 0f, 0f)),
                K(0.55f, FistFingers, FistPalm, Closed, ThumbShape.Fist, V(0.095f, 0.01f, 0f), glow: 1f),
                K(2f, FistFingers, FistPalm, Closed, ThumbShape.Fist, V(0.095f, 0.01f, 0f), glow: 1f),
                K(2.5f, FistFingers, FistPalm, Relaxed, offset: V(0.1f, 0f, 0f))
            }
        };

        // An open hand, palm leading, brushes across in front of you and drifts back.
        static readonly Motion SweepMotion = new Motion
        {
            Duration = 3.2f, Active = 0, Right = new[]
            {
                K(0f, V(-0.15f, 0.2f, 1f), V(-0.95f, -0.3f, 0f), Open, offset: V(0.08f, 0f, 0f), spread: 0.15f),
                K(1.1f, V(-0.2f, 0.2f, 1f), V(-0.95f, -0.3f, 0f), Open, offset: V(-0.1f, 0.01f, 0.03f), spread: 0.15f, glow: 0.6f),
                K(1.5f, V(-0.2f, 0.2f, 1f), V(-0.95f, -0.3f, 0f), Open, offset: V(-0.1f, 0.01f, 0.03f), spread: 0.15f),
                K(2.6f, V(-0.15f, 0.2f, 1f), V(-0.95f, -0.3f, 0f), Relaxed, offset: V(0.08f, 0f, 0f))
            }
        };

        // A hand held up, palm to your eyes, spreads its fingers wide and relaxes.
        static readonly Motion SpreadMotion = new Motion
        {
            Duration = 3f, Active = Index | Little, Right = new[]
            {
                K(0f, V(0.05f, 1f, 0.3f), V(0f, 0f, -1f), Cupped, faceView: 1f),
                K(0.9f, V(0.05f, 1f, 0.3f), V(0f, 0f, -1f), new Vector4(-0.04f, -0.04f, -0.02f, 0f), faceView: 1f, spread: 1f, glow: 1f),
                K(1.8f, V(0.05f, 1f, 0.3f), V(0f, 0f, -1f), new Vector4(-0.04f, -0.04f, -0.02f, 0f), faceView: 1f, spread: 1f, glow: 0.6f),
                K(2.5f, V(0.05f, 1f, 0.3f), V(0f, 0f, -1f), Cupped, faceView: 1f)
            }
        };

        // Fingers up, palm away from you, pushing forward and easing back.
        static readonly Motion PushMotion = new Motion
        {
            Duration = 3f, Active = 0, Right = new[]
            {
                K(0f, V(0f, 1f, 0.15f), V(0f, 0.1f, 1f), Relaxed, offset: V(0f, 0f, -0.03f)),
                K(1f, V(0f, 1f, 0.1f), V(0f, 0.05f, 1f), Open, offset: V(0f, 0.01f, 0.09f), spread: 0.25f, glow: 0.6f),
                K(1.4f, V(0f, 1f, 0.1f), V(0f, 0.05f, 1f), Open, offset: V(0f, 0.01f, 0.09f), spread: 0.25f),
                K(2.4f, V(0f, 1f, 0.15f), V(0f, 0.1f, 1f), Relaxed, offset: V(0f, 0f, -0.03f))
            }
        };

        // Palm down, the hand dips as if into water and rises again.
        static readonly Motion DipMotion = new Motion
        {
            Duration = 3.2f, Active = 0, Right = new[]
            {
                K(0f, V(0f, -0.15f, 1f), V(0f, -1f, 0.1f), Relaxed, offset: V(0f, 0.04f, 0f), spread: 0.2f),
                K(1.1f, V(0f, -0.25f, 1f), V(0f, -1f, 0.15f), Relaxed, offset: V(0f, -0.07f, 0.01f), spread: 0.2f, glow: 0.6f),
                K(1.7f, V(0f, -0.25f, 1f), V(0f, -1f, 0.15f), Relaxed, offset: V(0f, -0.07f, 0.01f), spread: 0.2f),
                K(2.6f, V(0f, -0.15f, 1f), V(0f, -1f, 0.1f), Relaxed, offset: V(0f, 0.04f, 0f), spread: 0.2f)
            }
        };

        // Two L shapes make a frame, both palms away from you: the left index up and its thumb pointing right make the
        // bottom left corner, the right index pointing left and its thumb down the top right one. The frame widens and
        // closes again.
        static readonly Motion FrameMotion = new Motion
        {
            Duration = 3.4f, TwoHanded = true, Active = Thumb | Index,
            Right = new[]
            {
                K(0f, V(-1f, 0.05f, 0.1f), V(0f, 0f, 1f), Point, offset: V(0.07f, 0.035f, 0f)),
                K(1.2f, V(-1f, 0.05f, 0.1f), V(0f, 0f, 1f), Point, offset: V(0.12f, 0.06f, 0f), glow: 1f),
                K(2.2f, V(-1f, 0.05f, 0.1f), V(0f, 0f, 1f), Point, offset: V(0.12f, 0.06f, 0f), glow: 0.6f),
                K(3f, V(-1f, 0.05f, 0.1f), V(0f, 0f, 1f), Point, offset: V(0.07f, 0.035f, 0f))
            },
            Left = new[]
            {
                K(0f, V(0f, 1f, 0.1f), V(0f, 0f, 1f), Point, offset: V(-0.07f, -0.035f, 0f)),
                K(1.2f, V(0f, 1f, 0.1f), V(0f, 0f, 1f), Point, offset: V(-0.12f, -0.06f, 0f), glow: 1f),
                K(2.2f, V(0f, 1f, 0.1f), V(0f, 0f, 1f), Point, offset: V(-0.12f, -0.06f, 0f), glow: 0.6f),
                K(3f, V(0f, 1f, 0.1f), V(0f, 0f, 1f), Point, offset: V(-0.07f, -0.035f, 0f))
            }
        };

        static readonly Motion[] Motions =
        {
            null, MenuGestureMotion, TouchMenuMotion, ThumbMotion, OpenPalmMotion, PalmUpMotion, PalmsFacingMotion, PinchMotion,
            TwoHandPinchMotion, PointPokeMotion, FistMotion, BothFistsMotion, SweepMotion, SpreadMotion, PushMotion, DipMotion,
            FrameMotion, PointUpMotion
        };

        /// <summary>
        /// Which ghost motion each world's hint shows, keyed by the world's class name (plain strings, so a world removed
        /// or renamed elsewhere never breaks the build; the editor checks make sure each name is a real world). Chosen
        /// from each world's Hint and from what its code reads of the hands. A world not listed keeps its text hint only.
        /// </summary>
        public static readonly (string World, GhostMotion Motion)[] WorldTable =
        {
            ("LivingSkin", GhostMotion.SpreadFingers),     // "Hold up your hands and spread your fingers"
            ("HeartbeatOrb", GhostMotion.PalmsFacing),     // "Hold your palms facing each other, then turn them like a ball"
            ("WaveformRibbon", GhostMotion.TwoHandPinch),  // "Pinch with both hands and pull them apart"
            ("AirStrings", GhostMotion.Sweep),             // "Brush the strings, or play the drums and cymbals around you"
            ("SoundPool", GhostMotion.Dip),                // "Dip your hand into the pool and listen"
            ("SoundWind", GhostMotion.SpreadFingers),      // "Put your hand in the school and spread your fingers"
            ("BubbleNotes", GhostMotion.OpenPalm),         // "...hold out a hand and the fish gather"
            ("BreathingRoom", GhostMotion.Push),           // "Push your palm towards a wall"
            ("LightTrails", GhostMotion.Pinch),            // "Pinch and move to draw; two fists clear it"
            ("FingerFrame", GhostMotion.TwoHandFrame),     // "Frame with both hands; pinch to change the filter"
            ("Lightning", GhostMotion.Fist),               // "Fist for thunder, hold it to switch snow and rain..."
            ("ShadowPlay", GhostMotion.OpenPalm),          // "Cast shadows on the wall; touch the lights with them"
            ("SolarSystem", GhostMotion.Pinch),            // "Pinch a planet to move it..."
            ("FlowerDance", GhostMotion.OpenPalm),         // "Your hand is the sun: move it and the flowers lean after it"
            ("EarthAndWater", GhostMotion.BothFists),      // "...clench both fists to switch"
            ("Graffiti", GhostMotion.Pinch),               // "Point at a wall and pinch to spray"
            ("OrbitType", GhostMotion.Push),               // "Push the letters, flick a ring, or make a fist"
            ("WordDrift", GhostMotion.PointPoke),          // "Touch a word to hold it"
            ("RisingFloor", GhostMotion.Sweep)             // "Sweep your hand across the words"
        };

        /// <summary>The motion a world's hint shows, or None to keep only its text.</summary>
        public static GhostMotion ForWorld(World world)
        {
            if (world == null) return GhostMotion.None;
            var name = world.GetType().Name;
            for (int i = 0; i < WorldTable.Length; i++)
                if (WorldTable[i].World == name) return WorldTable[i].Motion;
            return GhostMotion.None;
        }

        /// <summary>
        /// A second demo, with its caption, for a world whose first one can't show what is worth finding: it takes over
        /// from the first partway through the world's demo time. Keyed like <see cref="WorldTable"/>.
        /// </summary>
        public static readonly (string World, GhostMotion Motion, string Caption)[] FollowUps =
        {
            // The lyrics are stars overhead; a star pointed at sends its word down to read.
            ("SolarSystem", GhostMotion.PointUp, "Look up: point at a star to read its word")
        };

        /// <summary>The second demo a world shows and its caption, or None.</summary>
        public static GhostMotion FollowUpForWorld(World world, out string caption)
        {
            caption = null;
            if (world == null) return GhostMotion.None;
            var name = world.GetType().Name;
            for (int i = 0; i < FollowUps.Length; i++)
                if (FollowUps[i].World == name)
                {
                    caption = FollowUps[i].Caption;
                    return FollowUps[i].Motion;
                }
            return GhostMotion.None;
        }

        public static bool TwoHanded(GhostMotion motion) => Get(motion)?.TwoHanded ?? false;

        public static float Duration(GhostMotion motion) => Get(motion)?.Duration ?? 1f;

        /// <summary>Which fingertips light up: bit 0 thumb, 1 index, 2 middle, 3 ring, 4 little.</summary>
        public static int ActiveFingers(GhostMotion motion) => Get(motion)?.Active ?? 0;

        static Motion Get(GhostMotion motion)
        {
            int i = (int)motion;
            return i > 0 && i < Motions.Length ? Motions[i] : null;
        }

        /// <summary>
        /// One hand's pose <paramref name="time"/> seconds into a motion (looping). The left hand mirrors the right one
        /// across your middle, unless the motion has its own keys for it.
        /// </summary>
        public static void Evaluate(GhostMotion motion, bool left, float time, out GhostPose pose)
        {
            pose = default;
            var m = Get(motion);
            if (m == null) return;
            var keys = left && m.Left != null ? m.Left : m.Right;
            bool mirror = left && m.Left == null;
            float t = Mathf.Repeat(time, m.Duration);
            int a = keys.Length - 1;
            for (int i = 0; i < keys.Length; i++)
                if (keys[i].Time <= t) a = i;
            int b = (a + 1) % keys.Length;
            float start = keys[a].Time, end = b == 0 ? m.Duration : keys[b].Time;
            // Before the first key (only when it isn't at 0) the loop is still easing in from the last one.
            if (t < keys[0].Time)
            {
                a = keys.Length - 1;
                b = 0;
                start = keys[a].Time - m.Duration;
                end = keys[0].Time;
            }
            float s = end > start ? Mathf.Clamp01((t - start) / (end - start)) : 1f;
            s = s * s * (3f - 2f * s);
            ref readonly var ka = ref keys[a];
            ref readonly var kb = ref keys[b];
            pose.Offset = Vector3.Lerp(ka.Offset, kb.Offset, s);
            pose.Fingers = Vector3.Slerp(ka.Fingers.normalized, kb.Fingers.normalized, s);
            pose.Palm = Vector3.Slerp(ka.Palm.normalized, kb.Palm.normalized, s);
            pose.FaceView = Mathf.Lerp(ka.FaceView, kb.FaceView, s);
            pose.AimMenu = Mathf.Lerp(ka.AimMenu, kb.AimMenu, s);
            pose.Reach = Mathf.Lerp(ka.Reach, kb.Reach, s);
            pose.Spread = Mathf.Lerp(ka.Spread, kb.Spread, s);
            pose.Glow = Mathf.Lerp(ka.Glow, kb.Glow, s);
            pose.Curl = Vector4.Lerp(ka.Curl, kb.Curl, s);
            pose.ThumbFrom = ka.Thumb;
            pose.ThumbTo = kb.Thumb;
            pose.ThumbBlend = s;
            if (mirror)
            {
                pose.Offset.x = -pose.Offset.x;
                pose.Fingers.x = -pose.Fingers.x;
                pose.Palm.x = -pose.Palm.x;
            }
        }
    }
}
