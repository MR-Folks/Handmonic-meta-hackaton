using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Finger frame world (the team calls it Music frame): hold up both hands and a window opens between your thumbs
    /// and index fingers, its four corners on the four fingertips (index to index along the top, thumb to thumb along the
    /// bottom), so it takes the shape your fingers make and bends when you cross them. Through it you first see your own
    /// room through a camera filter, lined up with the room around the window, so the room itself looks changed between
    /// your fingers: comic ink, heat vision, neon edges, bass tiles, night vision and a kaleidoscope, each moving with the
    /// music (ink lines that thicken on the kick, heat rings and scan bars on the beat, tiles that grow with the bass).
    /// After the filters come four other worlds: a sunset valley, the deep sea, a neon city and an arctic night. They
    /// stay put in the room behind the window, so moving the frame or your head looks around inside them, and they move
    /// with the music too. Pinch and the window holds while the next filter or world swirls in, its name showing under
    /// the window; pinch with both hands to skip one. Sparks drift out of the window on the beat, and the sung words
    /// float inside it.
    /// With one hand up, spread its thumb and index in an "L" (the other fingers curled) and a small lens opens in the
    /// crook of the L onto the same view; pinch that hand to change it.
    /// The filters see the room through the headset's passthrough camera (<see cref="ICameraFeed"/>), asked for the
    /// first time the window opens on a filter, never before. Without it (no permission, or a headset or system without
    /// camera access) the frame says so once and shows its worlds.
    /// </summary>
    public class FingerFrame : World
    {
        public override string DisplayName => "Finger frame";

        public override Color Color => new Color(1f, 0.6f, 0.82f);

        public override int MenuOrder => 111;

        public override int MenuIcon => 27;

        public override string Hint => "Frame with both hands; pinch to change the filter";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.12f, 0.1f, 0.16f, 0.5f),
            Edge = new Color(1f, 0.88f, 0.7f),
            Glow = new Color(1f, 0.6f, 0.85f),
            Pattern = HandTheme.Effect.Film,
            PatternColor = new Color(1f, 0.85f, 0.95f)
        };

        [Header("Materials")]
        [SerializeField] Material portalMaterial;

        [Header("Frame")]
        [Tooltip("Seconds the window stays open after the frame breaks, so a pinch can change the view without closing it.")]
        [SerializeField] float holdOpen = 0.9f;
        [Tooltip("Seconds the next filter or world takes to swirl in.")]
        [SerializeField] float changeTime = 0.8f;
        [Tooltip("Seconds after the first pinch in which a pinch of the other hand skips one more.")]
        [SerializeField] float skipWindow = 0.35f;

        [Header("Camera filters")]
        [Tooltip("Without a room scan, how far away (m) the room is taken to be when lining the camera image up with it.")]
        [SerializeField] float roomDistance = 2f;
        [Tooltip("Seconds the window may stay closed before the camera rests; it starts again when you frame.")]
        [SerializeField] float cameraRest = 20f;

        /// <summary>
        /// One of the headset's cameras for the filters. FingerFrame asks for it the first time the window opens on a
        /// camera filter; the Meta build supplies one through <see cref="MakeCameraFeed"/> (FingerFrameCamera), and the
        /// editor stands in with a rendered view of its own room.
        /// </summary>
        public interface ICameraFeed
        {
            /// <summary>Whether the camera should run now. The first time it is wanted it asks for permission.</summary>
            bool Wanted { set; }

            CameraStatus Status { get; }

            /// <summary>
            /// The latest camera image for an eye (0 left, 1 right) and the matrix that takes a world point to it:
            /// (u w, v w, w), u and v from 0 to 1 across the image. False while there is no image.
            /// </summary>
            bool TryGetImage(int eye, out Texture image, out Matrix4x4 worldToImage);
        }

        public enum CameraStatus { Off, Asking, Starting, Running, Unavailable }

        /// <summary>Makes the headset's camera feed under the given parent. Set by the Meta code on the headset.</summary>
        public static System.Func<Transform, ICameraFeed> MakeCameraFeed;

        /// <summary>
        /// What the window shows, in the order pinches go through them: first the camera filters, then the other worlds.
        /// </summary>
        public static readonly string[] SceneNames =
        {
            "Comic ink", "Heat vision", "Neon edges", "Bass tiles", "Night vision", "Kaleidoscope",
            "Sunset valley", "Deep sea", "Neon city", "Arctic night"
        };

        // What the shader draws for each: 0-3 the other worlds, 10 and up the camera filters.
        static readonly int[] SceneShader = { 10, 11, 12, 13, 14, 15, 0, 1, 2, 3 };
        const int FirstWorld = 6;

        // Each view's colours, for its rim and the sparks that drift out of it.
        static readonly Color[][] ScenePalette =
        {
            new[] { new Color(1f, 0.88f, 0.3f), new Color(1f, 0.38f, 0.32f), new Color(0.35f, 0.8f, 1f) },
            new[] { new Color(1f, 0.6f, 0.2f), new Color(1f, 0.3f, 0.55f), new Color(1f, 0.92f, 0.45f) },
            new[] { new Color(0.3f, 0.95f, 1f), new Color(1f, 0.3f, 0.85f), new Color(0.65f, 0.45f, 1f) },
            new[] { new Color(0.55f, 1f, 0.7f), new Color(1f, 0.8f, 0.3f), new Color(0.45f, 0.62f, 1f) },
            new[] { new Color(0.5f, 1f, 0.55f), new Color(0.75f, 1f, 0.75f), new Color(0.3f, 0.85f, 0.45f) },
            new[] { new Color(1f, 0.62f, 0.92f), new Color(0.6f, 0.9f, 1f), new Color(1f, 0.9f, 0.5f) },
            new[] { new Color(1f, 0.85f, 0.6f), new Color(1f, 0.55f, 0.75f), new Color(0.6f, 0.5f, 1f) },
            new[] { new Color(0.55f, 0.95f, 1f), new Color(0.4f, 0.75f, 1f), new Color(1f, 0.55f, 0.85f) },
            new[] { new Color(1f, 0.3f, 0.75f), new Color(0.2f, 0.9f, 1f), new Color(1f, 0.85f, 0.3f) },
            new[] { new Color(0.4f, 1f, 0.6f), new Color(0.75f, 0.5f, 1f), new Color(0.85f, 0.92f, 1f) }
        };

        /// <summary>True for the views that show the room through the camera.</summary>
        public static bool IsCameraFilter(int view) => SceneShader[view] >= 10;

        static readonly int SceneAId = Shader.PropertyToID("_SceneA");
        static readonly int SceneBId = Shader.PropertyToID("_SceneB");
        static readonly int MixId = Shader.PropertyToID("_Mix");
        static readonly int OpenId = Shader.PropertyToID("_Open");
        static readonly int OriginId = Shader.PropertyToID("_Origin");
        static readonly int ForwardId = Shader.PropertyToID("_Forward");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int BandsId = Shader.PropertyToID("_Bands");
        static readonly int HighId = Shader.PropertyToID("_High");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        static readonly int CamTexLId = Shader.PropertyToID("_CamTexL");
        static readonly int CamTexRId = Shader.PropertyToID("_CamTexR");
        static readonly int CamMatrixLId = Shader.PropertyToID("_CamMatrixL");
        static readonly int CamMatrixRId = Shader.PropertyToID("_CamMatrixR");
        static readonly int CamTexelId = Shader.PropertyToID("_CamTexel");
        static readonly int CamFadeId = Shader.PropertyToID("_CamFade");
        static readonly int SurfacesId = Shader.PropertyToID("_Surfaces");
        static readonly int SurfaceCountId = Shader.PropertyToID("_SurfaceCount");
        static readonly int DepthCapId = Shader.PropertyToID("_DepthCap");
        static readonly int CenterId = Shader.PropertyToID("_Center");
        static readonly int BeatAgeId = Shader.PropertyToID("_BeatAge");
        static readonly int BassId = Shader.PropertyToID("_Bass");
        static readonly int BeatsId = Shader.PropertyToID("_Beats");
        static readonly int SpinId = Shader.PropertyToID("_Spin");

        // The window's mesh: a grid stretched between the four corners, so it can bend when the fingertips are not
        // on one plane. Each vertex also carries its distance to each of the four edges, for the rim.
        const int Grid = 10;

        // The room surfaces the camera image is lined up with: four rows each (see AddSurface and the shader).
        const int MaxSurfaces = 16;

        Renderer window;
        Mesh windowMesh;
        readonly Vector3[] gridVertices = new Vector3[(Grid + 1) * (Grid + 1)];
        readonly Vector2[] gridEdges = new Vector2[(Grid + 1) * (Grid + 1)];
        readonly Vector2[] gridEdgesFar = new Vector2[(Grid + 1) * (Grid + 1)];
        readonly Vector4[] surfaces = new Vector4[MaxSurfaces * 4];
        int surfaceCount;
        float depthCap;
        MaterialPropertyBlock block;
        ParticleSystem sparks;
        AudioPool voices;
        TypeLabel caption;
        float captionAge = 99f, captionSeconds;
        int scene, nextScene;
        float mix = 1f, open, seen = -99f, heldUntil = -99f, changedAt = -99f;
        bool sounded, skipped;
        Vector3 origin, forward;
        // The music as the filters use it: the bass smoothed, and the kaleidoscope's turn.
        float bass, spin;
        // The camera: made the first time the window opens on a filter.
        ICameraFeed cameraFeed;
        float cameraFade;
        static bool toldNoCamera;
#if UNITY_EDITOR
        StandInCamera standIn;
#endif
        // The window's corners, anticlockwise as you see them from the bottom left: on the fingertips, a little behind.
        readonly Vector3[] corners = new Vector3[4];
        readonly Vector3[] found = new Vector3[4];
        readonly Vector3[] drawn = new Vector3[4];
        readonly Vector3[] tips = new Vector3[4];
        readonly float[] lensAngles = new float[4];
        // The hand holding the one-handed lens (0 left, 1 right), or -1 for the two-handed frame or none.
        int lensSide = -1;
        // When both hands last framed the window, so a hand that blinks out for a moment doesn't shrink it to a lens.
        float framedTwo = -99f;

        Vector3 Center => (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;

        // About how tall the window is.
        float Height => ((corners[3] - corners[0]).magnitude + (corners[2] - corners[1]).magnitude) * 0.5f;

        /// <summary>The view showing in the window now (the one swirling in, during a change).</summary>
        public int Scene => mix < 1f ? nextScene : scene;

        /// <summary>The camera's state, or Off while it has not been asked for yet.</summary>
        public CameraStatus CameraState => cameraFeed != null ? cameraFeed.Status : CameraStatus.Off;

        bool CameraUnavailable => cameraFeed != null && cameraFeed.Status == CameraStatus.Unavailable;

        protected override int SungWordCount => 5;

        protected override float SungWordHeight => 0.028f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            window = Effects.MakeQuad("Window", transform, portalMaterial);
            windowMesh = new Mesh { name = "Finger Frame Window" };
            windowMesh.MarkDynamic();
            var uvs = new Vector2[gridVertices.Length];
            var triangles = new int[Grid * Grid * 6];
            for (int y = 0, t = 0; y <= Grid; y++)
            for (int x = 0; x <= Grid; x++)
            {
                uvs[y * (Grid + 1) + x] = new Vector2(x / (float)Grid, y / (float)Grid);
                if (x == Grid || y == Grid) continue;
                int i = y * (Grid + 1) + x;
                triangles[t++] = i; triangles[t++] = i + Grid + 1; triangles[t++] = i + 1;
                triangles[t++] = i + 1; triangles[t++] = i + Grid + 1; triangles[t++] = i + Grid + 2;
            }
            windowMesh.vertices = gridVertices;
            windowMesh.uv = uvs;
            windowMesh.SetUVs(1, gridEdges);
            windowMesh.SetUVs(2, gridEdgesFar);
            windowMesh.triangles = triangles;
            window.GetComponent<MeshFilter>().sharedMesh = windowMesh;
            window.gameObject.SetActive(false);
            sparks = Effects.MakeSparkSystem("Window Sparks", transform, glowMaterial);
            voices = new AudioPool(transform, 6);
            // The view's name, on a soft dark backing so it reads over whatever the window shows.
            caption = new TypeLabel("Caption", transform, 0.018f) { Backing = 1f, IsCaption = true };
            caption.GameObject.SetActive(false);
        }

        protected override void OnEnter()
        {
            open = 0f;
            mix = 1f;
            sounded = skipped = false;
            seen = heldUntil = changedAt = framedTwo = -99f;
            captionAge = 99f;
            cameraFade = 0f;
        }

        protected override void OnExit()
        {
            window.gameObject.SetActive(false);
            caption.GameObject.SetActive(false);
            if (cameraFeed != null) cameraFeed.Wanted = false;
        }

        void OnDestroy()
        {
            if (windowMesh != null) Discard(windowMesh);
#if UNITY_EDITOR
            standIn?.Dispose();
#endif
        }

        /// <summary>
        /// The other worlds are anchored where you stand, facing the way you face; the camera filters line up with the
        /// room's surfaces around you.
        /// </summary>
        protected override void OnPlace(Camera view)
        {
            origin = view.transform.position;
            forward = Effects.FlatForward(view);
            FindSurfaces(view);
        }

        /// <summary>
        /// A pinch while the window is open (or just was) brings the next view and holds the window open. A pinch of
        /// the other hand straight after (both hands pinching) skips one more.
        /// </summary>
        protected override void OnPinch(HandRig.Hand hand, bool left)
        {
            if (Clock - seen > 0.7f) return;
            // The pinching fingers light up with a burst; the bells are the sound.
            Feedback(GestureKind.Pinch, left, HandPinchPoint(hand), 1f, false);
            if (mix < 1f && !skipped && IsPinching(!left) && Clock - changedAt < skipWindow)
            {
                Skip(Music);
                Acted("skip a view");
                return;
            }
            Change(Music);
            Acted("next view");
        }

        /// <summary>Only a pinch at an open window changes the view, so OnPinch shows its own feedback.</summary>
        protected override bool AutoPinchFeedback => false;

        /// <summary>On the beat, a few sparks in the view's colours drift out of the window's edge into the room.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            if (open < 0.6f) return;
            var palette = ScenePalette[Scene];
            var center = Center;
            var toEye = (View.transform.position - center).normalized;
            for (int i = 0; i < 10; i++)
            {
                // A random point on the rim, between two fingertips.
                int edge = Random.Range(0, 4);
                var at = Vector3.Lerp(corners[edge], corners[(edge + 1) % 4], Random.value);
                var outward = Vector3.ProjectOnPlane(at - center, toEye).normalized;
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = outward * Random.Range(0.02f, 0.06f) + toEye * Random.Range(0.02f, 0.05f),
                    startColor = palette[Random.Range(0, palette.Length)],
                    startSize = Random.Range(0.006f, 0.014f),
                    startLifetime = Random.Range(0.9f, 1.6f)
                }, 1);
            }
        }

        /// <summary>The sung words float up inside the window, or in front of you while it is closed.</summary>
        protected override void OnWord(string word, bool newLine)
        {
            var eye = View.transform.position;
            var w = SungWords.Add(word, Color.Lerp(ScenePalette[Scene][0], Color.white, 0.5f), 2.2f, alone: true);
            var at = open > 0.5f ? Center + Vector3.down * (Height * 0.25f) : eye + Effects.FlatForward(View) * 0.6f + Vector3.down * 0.12f;
            // Just in front of the window, so the words sit over the view.
            if (open > 0.5f) at += (eye - at).normalized * 0.01f;
            w.Position = at;
            w.Velocity = Vector3.up * 0.025f;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            // Both hands frame; one hand alone may open its lens.
            bool framed = Frame(rig, view);
            if (framed)
            {
                lensSide = -1;
                framedTwo = Clock;
            }
            // Just after a two-hand frame the lens waits, and the window's grace holds it still instead.
            else if (Clock - framedTwo > 0.3f) framed = Lens(rig, view);
            if (!framed) lensSide = -1;
            if (framed)
            {
                // Snap to the hands as the window opens; afterwards follow them closely but smoothly.
                float k = open < 0.05f ? 1f : 1f - Mathf.Exp(-30f * dt);
                for (int i = 0; i < 4; i++) corners[i] = Vector3.Lerp(corners[i], found[i], k);
                seen = Clock;
                Acted("frame");
            }

            // A short grace keeps the window from flickering when tracking hesitates for a frame or two.
            bool show = framed || Clock - seen < 0.15f || Clock < heldUntil;
            open = Mathf.MoveTowards(open, show ? 1f : 0f, dt * (show ? 5f : 3.5f));
            if (open > 0.3f && !sounded)
            {
                Chord(music);
                sounded = true;
                // The thumbs and index fingers holding the frame (or the one hand's lens) light up as it opens.
                if (!MenuOpen)
                {
                    if (lensSide != 1) GestureFeedback.Light(true, Fingers.Thumb | Fingers.Index, 0.8f);
                    if (lensSide != 0) GestureFeedback.Light(false, Fingers.Thumb | Fingers.Index, 0.8f);
                }
            }
            if (open <= 0f) sounded = false;

            if (mix < 1f)
            {
                mix += dt / Mathf.Max(changeTime, 0.05f);
                if (mix >= 1f) { mix = 1f; scene = nextScene; }
            }

            // The filters' music: the bass rises quickly and settles slowly, and the kaleidoscope turns faster with the mids.
            float low = (music.Band(0) + music.Band(1)) * 0.5f;
            bass = Mathf.Lerp(bass, low, 1f - Mathf.Exp(-(low > bass ? 14f : 3f) * dt));
            spin += dt * (0.1f + music.Band(2) * 0.25f);

            UpdateCamera(view);
            Draw(music, dt);
            PlaceCaption(view, dt);
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                w.Position += w.Velocity * dt;
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.5f + music.Kick * 0.3f;
            }
        }

        /// <summary>
        /// Finds the frame whenever both hands are tracked: its corners are your two thumb tips and two index tips,
        /// wherever they are, and its edges always run index to index, thumb to thumb, and thumb to index on each hand.
        /// So the window takes whatever shape your fingers make, and if you cross your fingers over it twists and bends
        /// with them. Each corner sits 2 cm further away along your line of sight, so the fingers stay in front and the
        /// window still looks pinned to the tips. Puts the corners in <see cref="found"/>.
        /// </summary>
        bool Frame(HandRig rig, Camera view)
        {
            // While the menu is open the hands are choosing there, and no window opens between them.
            if (rig == null || MenuOpen) return false;
            var left = rig.Left;
            var right = rig.Right;
            if (!left.IsTracked || !right.IsTracked || left.InSystemGesture || right.InSystemGesture) return false;

            // Bottom left, bottom right, top right, top left when you hold an ordinary frame.
            tips[0] = Tip(left, 0);
            tips[1] = Tip(right, 0);
            tips[2] = Tip(right, 1);
            tips[3] = Tip(left, 1);
            var eye = view.transform.position;
            var look = view.transform.forward;
            const float behind = 0.02f;
            for (int i = 0; i < 4; i++)
            {
                var d = tips[i] - eye;
                // A fingertip behind or right at your eyes makes no window.
                if (Vector3.Dot(d, look) < 0.05f) return false;
                float distance = d.magnitude;
                found[i] = eye + d * ((distance + behind) / distance);
            }
            return true;
        }

        /// <summary>
        /// The one-handed lens, when only one hand is up: its thumb and index spread in an "L" (both out, at least 50
        /// degrees apart, not pinching) with the other three fingers curled in. The lens fills the crook of the L, from
        /// a little out of the palm to just past the two fingertips, pushed 2 cm back along your line of sight like the frame.
        /// Once open it holds through a little wobble. Puts the corners in <see cref="found"/>.
        /// </summary>
        bool Lens(HandRig rig, Camera view)
        {
            if (rig == null || MenuOpen) return false;
            var left = rig.Left;
            var right = rig.Right;
            var hand = left.IsTracked && !right.IsTracked ? left : right.IsTracked && !left.IsTracked ? right : null;
            if (hand == null || hand.InSystemGesture) return false;
            int side = hand == left ? 0 : 1;
            bool was = lensSide == side;
            var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
            var thumb = Tip(hand, 0);
            var index = Tip(hand, 1);
            var toIndex = index - palm;
            var toThumb = thumb - palm;
            float curled = (Vector3.Distance(Tip(hand, 2), palm) + Vector3.Distance(Tip(hand, 3), palm) + Vector3.Distance(Tip(hand, 4), palm)) / 3f;
            float spread = Vector3.Angle(toIndex, toThumb);
            bool shaped = toIndex.magnitude > (was ? 0.065f : 0.075f) && toThumb.magnitude > (was ? 0.05f : 0.06f)
                && curled < (was ? 0.085f : 0.075f) && spread > (was ? 40f : 50f) && hand.Pinch < (was ? 0.5f : 0.2f);
            if (!shaped) return false;

            // The crook of the L, a little out from the palm towards the two fingers, and its two arms.
            var crook = palm + toIndex * 0.22f + toThumb * 0.22f;
            var eye = view.transform.position;
            var right3 = view.transform.right;
            var up = view.transform.up;
            // The arms reach a little past the fingertips, so the lens shows clear of the fingers.
            index = crook + (index - crook) * 1.25f;
            thumb = crook + (thumb - crook) * 1.25f;
            tips[0] = crook;
            tips[1] = index;
            tips[2] = index + thumb - crook;
            tips[3] = thumb;
            // Anticlockwise from the bottom left as you see them, whichever hand and however it is turned.
            var middle = (tips[0] + tips[1] + tips[2] + tips[3]) * 0.25f;
            for (int i = 0; i < 4; i++)
            {
                var d = tips[i] - middle;
                lensAngles[i] = Mathf.Atan2(Vector3.Dot(d, up), Vector3.Dot(d, right3));
            }
            for (int i = 1; i < 4; i++)
                for (int j = i; j > 0 && lensAngles[j] < lensAngles[j - 1]; j--)
                {
                    (lensAngles[j], lensAngles[j - 1]) = (lensAngles[j - 1], lensAngles[j]);
                    (tips[j], tips[j - 1]) = (tips[j - 1], tips[j]);
                }
            int first = 0;
            float lowest = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var d = tips[i] - middle;
                float corner = Vector3.Dot(d, up) + Vector3.Dot(d, right3);
                if (corner < lowest) { lowest = corner; first = i; }
            }
            var look = view.transform.forward;
            const float behind = 0.02f;
            for (int i = 0; i < 4; i++)
            {
                var d = tips[(first + i) % 4] - eye;
                if (Vector3.Dot(d, look) < 0.05f) return false;
                float distance = d.magnitude;
                found[i] = eye + d * ((distance + behind) / distance);
            }
            lensSide = side;
            return true;
        }

        /// <summary>For the editor checks: the hand whose lens is open (0 left, 1 right), or -1; and how open the window is.</summary>
        public int LensSide => lensSide;
        public float Openness => open;

        static Vector3 Tip(HandRig.Hand hand, int finger) =>
            hand.TipJoints[finger] != null ? hand.TipJoints[finger].position : hand.Tips[finger];

        /// <summary>The view after <paramref name="view"/>, skipping the camera filters when there is no camera.</summary>
        int Following(int view)
        {
            for (int i = 0; i < SceneNames.Length; i++)
            {
                view = (view + 1) % SceneNames.Length;
                if (!IsCameraFilter(view) || !CameraUnavailable) break;
            }
            return view;
        }

        /// <summary>Starts the next view swirling into the window, and shows its name under it.</summary>
        void Change(MusicFrame music)
        {
            if (mix < 1f) scene = nextScene;
            nextScene = Following(scene);
            mix = 0f;
            skipped = false;
            heldUntil = Clock + holdOpen;
            seen = changedAt = Clock;
            Say(SceneNames[nextScene], 1.8f);
            // A rising pair of bells, fitted to the song.
            int a = Musical.Pitch(music, 76), b = Musical.Pitch(music, 81);
            voices.Play(Synth.Chime(Hz(a)), Center, 0.35f, 1f, Musical.Delay(music));
            voices.Play(Synth.Chime(Hz(b)), Center, 0.3f, 1f, Musical.Delay(music) + 0.11f);
        }

        /// <summary>Both hands pinched: the view swirling in becomes the one after it, with a third, higher bell.</summary>
        void Skip(MusicFrame music)
        {
            nextScene = Following(nextScene);
            skipped = true;
            heldUntil = Clock + holdOpen;
            seen = Clock;
            Say(SceneNames[nextScene], 1.8f);
            voices.Play(Synth.Chime(Hz(Musical.Pitch(music, 88))), Center, 0.3f, 1f, Musical.Delay(music));
        }

        /// <summary>Goes straight to a view: swirling in while the window is open, at once while it is closed.</summary>
        void GoTo(int view)
        {
            if (open > 0.05f)
            {
                if (mix < 1f) scene = nextScene;
                nextScene = view;
                mix = 0f;
            }
            else
            {
                scene = nextScene = view;
                mix = 1f;
            }
        }

        /// <summary>Three notes ringing out as the window opens.</summary>
        void Chord(MusicFrame music)
        {
            float delay = Musical.Delay(music);
            int[] wanted = { 64, 69, 72 };
            for (int i = 0; i < wanted.Length; i++)
                voices.Play(Synth.Soft(Hz(Musical.Pitch(music, wanted[i]))), Center, 0.3f, 1f, delay + i * 0.08f);
        }

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        /// <summary>
        /// The camera for the filters: made, and so asked for, the first time the window opens on one; running while a
        /// filter shows (or the next pinch brings one) and resting once the window has stayed closed a while. Without a
        /// camera the window moves on to the first world and says why, once.
        /// </summary>
        void UpdateCamera(Camera view)
        {
            bool onFilter = IsCameraFilter(scene) || mix < 1f && IsCameraFilter(nextScene);
            if (cameraFeed == null)
            {
                if (!onFilter || open <= 0f) return;
                cameraFeed = MakeFeed();
            }
            if (CameraUnavailable)
            {
                cameraFeed.Wanted = false;
                cameraFade = 0f;
                if (IsCameraFilter(Scene)) GoTo(FirstWorld);
                if (!toldNoCamera)
                {
                    toldNoCamera = true;
                    Say("Camera filters are off: no camera access", 4.5f);
                }
                return;
            }
            bool wanted = (onFilter || IsCameraFilter(Following(Scene))) && Clock - seen < cameraRest;
            cameraFeed.Wanted = wanted;
#if UNITY_EDITOR
            if (wanted) standIn?.Capture(view);
#endif
        }

        ICameraFeed MakeFeed()
        {
            if (MakeCameraFeed != null) return MakeCameraFeed(transform);
#if UNITY_EDITOR
            return standIn = new StandInCamera(this);
#else
            Debug.Log("[Frame] No camera feed in this build; the frame shows its worlds.");
            return new NoCamera();
#endif
        }

        /// <summary>A build without the headset's camera code.</summary>
        sealed class NoCamera : ICameraFeed
        {
            public bool Wanted { set { } }
            public CameraStatus Status => CameraStatus.Unavailable;

            public bool TryGetImage(int eye, out Texture image, out Matrix4x4 worldToImage)
            {
                image = null;
                worldToImage = Matrix4x4.identity;
                return false;
            }
        }

        /// <summary>
        /// The room surfaces behind the window, for lining the camera image up with the room: the scanned floor,
        /// ceiling and walls and the tops of the nearest furniture; without a scan, the floor and four walls
        /// <see cref="roomDistance"/> away around you.
        /// </summary>
        void FindSurfaces(Camera view)
        {
            surfaceCount = 0;
            var eye = view.transform.position;
            float floor = Room.Floor(view);
            const float wide = 40f;
            AddSurface(new Room.Surface(new Vector3(eye.x, floor, eye.z), Vector3.up, Vector3.right, new Vector2(wide, wide)), 0f);
            if (Room.Scanned && Room.Walls.Count > 0)
            {
                float ceiling = Room.Ceiling(-99f);
                if (ceiling > floor) AddSurface(new Room.Surface(new Vector3(eye.x, ceiling, eye.z), Vector3.down, Vector3.right, new Vector2(wide, wide)), 0f);
                var walls = Room.Walls;
                // Scanned corners rarely meet exactly, so each wall reaches 10 cm past its ends.
                for (int i = 0; i < walls.Count && surfaceCount < MaxSurfaces - 2; i++) AddSurface(walls[i], 0.1f);
                // The furniture tops nearest to you, as many as still fit.
                var tops = Room.Tops;
                long used = 0;
                while (surfaceCount < MaxSurfaces)
                {
                    int best = -1;
                    float nearest = float.MaxValue;
                    for (int i = 0; i < tops.Count && i < 64; i++)
                    {
                        if ((used & (1L << i)) != 0) continue;
                        float d = (tops[i].Clamp(eye) - eye).sqrMagnitude;
                        if (d < nearest) { nearest = d; best = i; }
                    }
                    if (best < 0) break;
                    used |= 1L << best;
                    AddSurface(tops[best], 0.02f);
                }
                depthCap = 8f;
            }
            else
            {
                var ahead = Effects.FlatForward(view);
                for (int i = 0; i < 4; i++)
                {
                    var outward = Quaternion.AngleAxis(90f * i, Vector3.up) * ahead;
                    var inward = -outward;
                    AddSurface(new Room.Surface(eye + outward * roomDistance, inward, Vector3.Cross(Vector3.up, inward),
                        new Vector2(roomDistance * 4f, wide)), 0f);
                }
                depthCap = roomDistance * 1.5f;
            }
        }

        /// <summary>
        /// One surface for the shader: its plane, and the planes along its two sides that bound it, each as a normal and
        /// an offset, then its half size.
        /// </summary>
        void AddSurface(Room.Surface s, float margin)
        {
            if (surfaceCount >= MaxSurfaces) return;
            int i = surfaceCount++ * 4;
            surfaces[i] = PlaneOf(s.Normal, s.Center);
            surfaces[i + 1] = PlaneOf(s.Right, s.Center);
            surfaces[i + 2] = PlaneOf(s.Up, s.Center);
            surfaces[i + 3] = new Vector4(s.Size.x * 0.5f + margin, s.Size.y * 0.5f + margin, 0f, 0f);
        }

        static Vector4 PlaneOf(Vector3 normal, Vector3 point) => new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));

        void Draw(MusicFrame music, float dt)
        {
            bool visible = open > 0.001f;
            if (window.gameObject.activeSelf != visible) window.gameObject.SetActive(visible);
            if (!visible) return;
            ShapeWindow(Mathf.SmoothStep(0f, 1f, open));
            block.Clear();
            block.SetFloat(SceneAId, SceneShader[scene]);
            block.SetFloat(SceneBId, SceneShader[nextScene]);
            block.SetFloat(MixId, mix < 1f ? mix : 0f);
            block.SetFloat(OpenId, Mathf.SmoothStep(0f, 1f, open));
            block.SetVector(OriginId, origin);
            block.SetVector(ForwardId, forward);
            block.SetFloat(PulseId, music.Kick);
            block.SetVector(BandsId, new Vector4(music.Band(0), music.Band(1), music.Band(2), music.Band(3)));
            block.SetVector(HighId, new Vector4(music.Band(4), music.Band(5), 0f, 0f));
            block.SetFloat(ClockId, Now);
            block.SetColor(EdgeColorId, ScenePalette[Scene][0]);
            DrawCamera(music, dt);
            window.SetPropertyBlock(block);
        }

        /// <summary>
        /// The camera images and the room for the filters. Each eye gets its camera's latest image with the matrix that
        /// projects room points onto it (from where the camera was when it took the image), and the room surfaces the
        /// shader follows each eye ray to; the image fades in once the camera has started.
        /// </summary>
        void DrawCamera(MusicFrame music, float dt)
        {
            Texture left = null, right = null;
            var toLeft = Matrix4x4.identity;
            var toRight = Matrix4x4.identity;
            bool onFilter = IsCameraFilter(scene) || mix < 1f && IsCameraFilter(nextScene);
            bool ready = onFilter && cameraFeed != null && cameraFeed.Status == CameraStatus.Running
                         && cameraFeed.TryGetImage(0, out left, out toLeft);
            if (ready && !cameraFeed.TryGetImage(1, out right, out toRight))
            {
                right = left;
                toRight = toLeft;
            }
            cameraFade = Mathf.MoveTowards(cameraFade, ready ? 1f : 0f, dt * 2.5f);
            if (!ready) left = right = Texture2D.blackTexture;
            block.SetTexture(CamTexLId, left);
            block.SetTexture(CamTexRId, right);
            block.SetMatrix(CamMatrixLId, toLeft);
            block.SetMatrix(CamMatrixRId, toRight);
            block.SetVector(CamTexelId, new Vector4(1f / left.width, 1f / left.height, left.width, left.height));
            block.SetFloat(CamFadeId, cameraFade);
            block.SetVectorArray(SurfacesId, surfaces);
            block.SetFloat(SurfaceCountId, surfaceCount);
            block.SetFloat(DepthCapId, depthCap);
            block.SetVector(CenterId, Center);
            block.SetFloat(BeatAgeId, Mathf.Min(music.BeatAge, 10f));
            block.SetFloat(BassId, Mathf.Clamp01(bass));
            block.SetFloat(BeatsId, Beats % 1000);
            block.SetFloat(SpinId, spin % (Mathf.PI * 200f));
        }

        /// <summary>
        /// Lays the window's grid between the corners, grown from the middle as it opens. Each vertex gets its distance
        /// (metres) to the line of each edge, bottom and right in one set of UVs and top and left in another: those
        /// blend exactly across the mesh, so the shader's rim keeps its corners sharp.
        /// </summary>
        void ShapeWindow(float grown)
        {
            var center = Center;
            for (int i = 0; i < 4; i++) drawn[i] = center + (corners[i] - center) * grown;
            var toLocal = window.transform.worldToLocalMatrix;
            for (int y = 0; y <= Grid; y++)
            {
                float v = y / (float)Grid;
                var from = Vector3.Lerp(drawn[0], drawn[3], v);
                var to = Vector3.Lerp(drawn[1], drawn[2], v);
                for (int x = 0; x <= Grid; x++)
                {
                    var p = Vector3.Lerp(from, to, x / (float)Grid);
                    int i = y * (Grid + 1) + x;
                    gridVertices[i] = toLocal.MultiplyPoint3x4(p);
                    gridEdges[i] = new Vector2(ToLine(p, drawn[0], drawn[1]), ToLine(p, drawn[1], drawn[2]));
                    gridEdgesFar[i] = new Vector2(ToLine(p, drawn[2], drawn[3]), ToLine(p, drawn[3], drawn[0]));
                }
            }
            windowMesh.vertices = gridVertices;
            windowMesh.SetUVs(1, gridEdges);
            windowMesh.SetUVs(2, gridEdgesFar);
            windowMesh.RecalculateBounds();
        }

        static float ToLine(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f);
            return (p - a - ab * t).magnitude;
        }

        /// <summary>A short line under the window (or in front of you while it is closed): a view's name, or why there are no filters.</summary>
        void Say(string text, float seconds)
        {
            caption.Text = text;
            captionAge = 0f;
            captionSeconds = seconds;
        }

        void PlaceCaption(Camera view, float dt)
        {
            captionAge += dt;
            bool show = captionAge < captionSeconds && !MenuOpen;
            if (caption.GameObject.activeSelf != show) caption.GameObject.SetActive(show);
            if (!show) return;
            var eye = view.transform.position;
            var at = open > 0.3f
                ? (corners[0] + corners[1]) * 0.5f + Vector3.down * 0.03f
                : eye + Effects.FlatForward(view) * 0.6f + Vector3.down * 0.2f;
            caption.Transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - eye, Vector3.up));
            float alpha = Mathf.Clamp01(Mathf.Min(captionAge / 0.2f, (captionSeconds - captionAge) / 0.5f));
            var tint = Color.Lerp(Color.white, ScenePalette[Scene][0], 0.35f);
            caption.Color = new Color(tint.r, tint.g, tint.b, alpha);
        }

        /// <summary>For previews and the caption checks: the name of the view showing comes up under the window.</summary>
        public void PreviewCaption() => Say(SceneNames[Scene], 1.8f);

        /// <summary>For previews: opens the window at a place, as if framed by hands, on a given view.</summary>
        public void PreviewWindow(Vector3 at, Quaternion rotation, Vector2 windowSize, int world)
        {
            var across = rotation * Vector3.right * (windowSize.x * 0.5f);
            var up = rotation * Vector3.up * (windowSize.y * 0.5f);
            PreviewWindow(new[] { at - across - up, at + across - up, at + across + up, at - across + up }, world);
        }

        /// <summary>For previews: opens the window between four corners (anticlockwise from the bottom left) on a given view.</summary>
        public void PreviewWindow(Vector3[] windowCorners, int world)
        {
            Build();
            for (int i = 0; i < 4; i++) corners[i] = windowCorners[i];
            scene = nextScene = world % SceneNames.Length;
            mix = 1f;
            open = 1f;
            seen = Clock;
            heldUntil = 1000f;
        }

#if UNITY_EDITOR
        /// <summary>
        /// The editor has no headset camera, so the filters look at a stand-in: the scene drawn each frame into a small
        /// image from a camera a few centimetres off your eyes, as the headset's own cameras are, without the frame's
        /// window, sparks and words. It goes through the same lining up as the real camera, so previews show its small
        /// misalignments too. Outside play mode it also sets a few things in the room ahead (a cabinet with books, a
        /// lamp, a plant and a picture), so the filters have a room to work on.
        /// </summary>
        sealed class StandInCamera : ICameraFeed
        {
            // About where a headset camera sits from the eye: a little to the side, below and in front.
            static readonly Vector3 LensOffset = new Vector3(-0.03f, -0.02f, 0.05f);
            const int Width = 640, Height = 480;
            // Shared by every stand-in, so previews made and dropped again don't leave textures behind.
            static RenderTexture image;
            static Material furnitureMaterial, lampMaterial, pictureMaterial;

            readonly FingerFrame frame;
            readonly List<Renderer> hidden = new List<Renderer>();
            readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
            Camera lens;
            Transform things;
            Matrix4x4 toImage = Matrix4x4.identity;
            bool captured;

            public StandInCamera(FingerFrame frame) => this.frame = frame;

            public bool Wanted { set { } }

            public CameraStatus Status => CameraStatus.Running;

            public bool TryGetImage(int eye, out Texture texture, out Matrix4x4 worldToImage)
            {
                texture = image;
                worldToImage = toImage;
                return captured && image != null;
            }

            public void Capture(Camera view)
            {
                if (image == null || !image.IsCreated())
                {
                    image = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                        { name = "Finger Frame Stand-in Camera", hideFlags = HideFlags.HideAndDontSave };
                    image.Create();
                }
                if (lens == null)
                {
                    var go = new GameObject("Stand-in Camera") { hideFlags = HideFlags.HideAndDontSave };
                    go.transform.SetParent(frame.transform, false);
                    lens = go.AddComponent<Camera>();
                    lens.enabled = false;
                }
                if (things == null && !Application.isPlaying) things = Furnish(view);

                lens.CopyFrom(view);
                lens.stereoTargetEye = StereoTargetEyeMask.None;
                lens.transform.SetPositionAndRotation(view.transform.TransformPoint(LensOffset), view.transform.rotation);
                lens.fieldOfView = 64f;
                lens.aspect = Width / (float)Height;
                lens.nearClipPlane = 0.05f;
                lens.targetTexture = image;

                // The room and the hands, without the frame's own window, sparks and words.
                hidden.Clear();
                foreach (var r in frame.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r.forceRenderingOff || things != null && r.transform.IsChildOf(things)) continue;
                    r.forceRenderingOff = true;
                    hidden.Add(r);
                }
                lens.Render();
                foreach (var r in hidden) r.forceRenderingOff = false;
                lens.targetTexture = null;

                // World point to image: the camera's clip space, its x and y taken from -1..1 to 0..1.
                var clip = lens.projectionMatrix * lens.worldToCameraMatrix;
                var w = clip.GetRow(3);
                toImage = Matrix4x4.identity;
                toImage.SetRow(0, (clip.GetRow(0) + w) * 0.5f);
                toImage.SetRow(1, (clip.GetRow(1) + w) * 0.5f);
                toImage.SetRow(2, w);
                toImage.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
                captured = true;
            }

            public void Dispose()
            {
                if (lens != null) Discard(lens.gameObject);
                if (things != null) Discard(things.gameObject);
            }

            /// <summary>
            /// A low cabinet against the wall ahead with books, a lamp and a plant on it, and a picture above: lit, with
            /// clear shapes and colours, for the filters' edges, tones and heat.
            /// </summary>
            Transform Furnish(Camera view)
            {
                var eye = view.transform.position;
                var ahead = Effects.FlatForward(view);
                float floor = Room.Floor(view);
                var foot = new Vector3(eye.x, floor, eye.z) + ahead * 2.38f;
                if (Room.WallAhead(eye, ahead, 6f, out var wall, out var onWall))
                {
                    ahead = -wall.Normal;
                    foot = new Vector3(onWall.x, floor, onWall.z) - ahead * 0.22f;
                }
                var side = Vector3.Cross(Vector3.up, ahead);
                var root = new GameObject("Stand-in Room Things") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(frame.transform, false);
                var facing = Quaternion.LookRotation(ahead, Vector3.up);

                if (furnitureMaterial == null)
                {
                    var lit = Shader.Find("Universal Render Pipeline/Lit");
                    furnitureMaterial = new Material(lit) { hideFlags = HideFlags.HideAndDontSave };
                    furnitureMaterial.SetFloat("_Smoothness", 0.25f);
                    lampMaterial = new Material(lit) { hideFlags = HideFlags.HideAndDontSave };
                    lampMaterial.SetColor("_BaseColor", new Color(1f, 0.92f, 0.75f));
                    lampMaterial.EnableKeyword("_EMISSION");
                    lampMaterial.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.5f) * 1.3f);
                    pictureMaterial = new Material(lit) { hideFlags = HideFlags.HideAndDontSave };
                    pictureMaterial.SetTexture("_BaseMap", Painting());
                    pictureMaterial.SetFloat("_Smoothness", 0.1f);
                }

                // x along the wall, y up from the floor, z towards the wall.
                Transform Piece(PrimitiveType shape, Vector3 at, Vector3 size, Color color, Material material = null, float roll = 0f)
                {
                    var go = GameObject.CreatePrimitive(shape);
                    go.hideFlags = HideFlags.DontSave;
                    Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.transform.SetParent(root, false);
                    go.transform.SetPositionAndRotation(foot + side * at.x + Vector3.up * at.y + ahead * at.z, facing * Quaternion.Euler(0f, 0f, roll));
                    go.transform.localScale = size;
                    var r = go.GetComponent<Renderer>();
                    r.sharedMaterial = material != null ? material : furnitureMaterial;
                    if (material == null)
                    {
                        tint.SetColor("_BaseColor", color);
                        r.SetPropertyBlock(tint);
                    }
                    return go.transform;
                }

                var wood = new Color(0.42f, 0.26f, 0.16f);
                Piece(PrimitiveType.Cube, new Vector3(0f, 0.37f, 0f), new Vector3(1.4f, 0.74f, 0.42f), wood);
                for (int i = -1; i <= 1; i++)
                {
                    Piece(PrimitiveType.Cube, new Vector3(i * 0.45f, 0.4f, -0.215f), new Vector3(0.41f, 0.52f, 0.02f), new Color(0.6f, 0.42f, 0.27f));
                    Piece(PrimitiveType.Sphere, new Vector3(i * 0.45f, 0.58f, -0.23f), Vector3.one * 0.035f, new Color(0.9f, 0.75f, 0.35f));
                }

                // Books standing on the left, the last one leaning.
                Color[] covers =
                {
                    new Color(0.62f, 0.1f, 0.12f), new Color(0.88f, 0.66f, 0.18f), new Color(0.08f, 0.45f, 0.46f), new Color(0.12f, 0.16f, 0.4f),
                    new Color(0.9f, 0.86f, 0.76f), new Color(0.22f, 0.48f, 0.2f), new Color(0.92f, 0.44f, 0.14f), new Color(0.42f, 0.16f, 0.36f)
                };
                float x = -0.64f;
                for (int i = 0; i < covers.Length; i++)
                {
                    float thick = 0.03f + 0.025f * Mathf.Repeat(i * 0.618f, 1f);
                    float tall = 0.19f + 0.1f * Mathf.Repeat(i * 0.37f + 0.2f, 1f);
                    bool lean = i == covers.Length - 1;
                    Piece(PrimitiveType.Cube, new Vector3(x + thick * 0.5f + (lean ? 0.035f : 0f), 0.74f + tall * 0.5f - (lean ? 0.01f : 0f), 0.04f),
                        new Vector3(thick, tall, 0.17f), covers[i], roll: lean ? -20f : 0f);
                    x += thick + 0.004f;
                }

                // A lamp with a glowing shade.
                Piece(PrimitiveType.Cylinder, new Vector3(0.08f, 0.75f, 0.05f), new Vector3(0.13f, 0.012f, 0.13f), new Color(0.15f, 0.13f, 0.12f));
                Piece(PrimitiveType.Cylinder, new Vector3(0.08f, 0.92f, 0.05f), new Vector3(0.016f, 0.17f, 0.016f), new Color(0.85f, 0.7f, 0.35f));
                Piece(PrimitiveType.Cylinder, new Vector3(0.08f, 1.15f, 0.05f), new Vector3(0.24f, 0.08f, 0.24f), Color.white, lampMaterial);

                // A plant in a clay pot.
                Piece(PrimitiveType.Cylinder, new Vector3(0.47f, 0.83f, 0.04f), new Vector3(0.18f, 0.09f, 0.18f), new Color(0.72f, 0.36f, 0.22f));
                Color[] greens = { new Color(0.16f, 0.48f, 0.2f), new Color(0.26f, 0.6f, 0.24f), new Color(0.1f, 0.38f, 0.17f) };
                for (int i = 0; i < 7; i++)
                {
                    float a = i * 2.4f;
                    float size = 0.11f + 0.05f * Mathf.Repeat(i * 0.43f, 1f);
                    Piece(PrimitiveType.Sphere, new Vector3(0.47f + Mathf.Cos(a) * 0.09f, 1.0f + 0.12f * Mathf.Repeat(i * 0.29f, 1f), 0.04f + Mathf.Sin(a) * 0.06f),
                        new Vector3(size, size * 1.3f, size), greens[i % greens.Length]);
                }

                // A framed picture on the wall above.
                Piece(PrimitiveType.Cube, new Vector3(-0.2f, 1.45f, 0.2f), new Vector3(0.72f, 0.5f, 0.03f), new Color(0.1f, 0.08f, 0.07f));
                var picture = Piece(PrimitiveType.Quad, new Vector3(-0.2f, 1.45f, 0.183f), new Vector3(0.62f, 0.4f, 1f), Color.white, pictureMaterial);
                picture.localScale = new Vector3(0.62f, 0.4f, 1f);

                // Warm light from above on the left, and a soft fill from the right.
                var key = new GameObject("Key Light") { hideFlags = HideFlags.DontSave }.AddComponent<Light>();
                key.transform.SetParent(root, false);
                key.type = LightType.Directional;
                key.color = new Color(1f, 0.93f, 0.84f);
                key.intensity = 1.2f;
                key.transform.rotation = Quaternion.LookRotation(ahead + Vector3.down * 0.9f + side * 0.6f, Vector3.up);
                var fill = new GameObject("Fill Light") { hideFlags = HideFlags.DontSave }.AddComponent<Light>();
                fill.transform.SetParent(root, false);
                fill.type = LightType.Directional;
                fill.color = new Color(0.75f, 0.82f, 1f);
                fill.intensity = 0.45f;
                fill.transform.rotation = Quaternion.LookRotation(ahead + Vector3.down * 0.3f - side * 0.8f, Vector3.up);
                return root;
            }

            /// <summary>A small painting: a sunset over hills, with a lake.</summary>
            static Texture2D Painting()
            {
                const int w = 160, h = 104;
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    var c = Color.Lerp(new Color(1f, 0.62f, 0.35f), new Color(0.25f, 0.35f, 0.75f), Mathf.SmoothStep(0.45f, 1f, v));
                    float sun = new Vector2(u - 0.62f, (v - 0.55f) * 0.65f).magnitude;
                    if (sun < 0.11f) c = new Color(1f, 0.92f, 0.6f);
                    float hills = 0.42f + 0.08f * Mathf.Sin(u * 9f) + 0.05f * Mathf.Sin(u * 23f + 1f);
                    if (v < hills) c = Color.Lerp(new Color(0.12f, 0.3f, 0.22f), new Color(0.25f, 0.45f, 0.3f), v / hills);
                    if (v < 0.22f) c = Color.Lerp(new Color(0.15f, 0.25f, 0.5f), new Color(0.45f, 0.5f, 0.75f), Mathf.Repeat(u * 7f + v * 31f, 1f) * 0.4f);
                    pixels[y * w + x] = c;
                }
                texture.SetPixels(pixels);
                texture.Apply();
                return texture;
            }
        }
#endif
    }
}
