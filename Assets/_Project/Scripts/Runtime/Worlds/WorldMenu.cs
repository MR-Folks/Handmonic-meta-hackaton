using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The world menu: glass bubbles in two honeycombs, like an Apple Watch home screen, opened with Meta's left-hand
    /// menu gesture. The sound worlds cluster on the left and the lyric worlds on the right, each bubble showing a
    /// small animated emblem; below them a row plays and pauses the song, steps to the previous and next song
    /// (including songs added on the headset and songs from Audius), switches to listening to the room, turns the lyrics
    /// on and off and opens the song page (SongLibrary), which takes the honeycombs' place. Bubbles near where you look
    /// are a little larger, and if you look away the menu glides back in front of you, but never away from a fingertip
    /// reaching for it.
    /// The menu opens within arm's reach, so you can touch a bubble or button with your index fingertip, or point at it
    /// from farther away and pinch. A pointing hand casts a soft ray onto the menu (from the shoulder through the
    /// pinch); once its fingertip comes within 15 cm of the menu, the ray gives way to a dot under the fingertip that
    /// shrinks as the finger nears (Meta's touch specs). The bubble you point at or reach for swells and the caption
    /// above the honeycomb says what choosing it will do. Under the menu a line says how to choose, or that a song is
    /// loading or couldn't play, and while a song from Audius plays, a second line gives the credit its license asks for.
    /// Several worlds can be on at once (see WorldManager): each one that is on wears a ring, choosing another adds it
    /// ("Add Flower dance") or swaps it for the ones it clashes with ("Swap Cube field for Flower dance"), and choosing
    /// one that is on turns it off ("Turn off Solar system"). The menu stays open after each choice, so you can put
    /// several together; the menu gesture closes it. With no bubble under a finger, the caption names the worlds that
    /// are on.
    /// </summary>
    [DefaultExecutionOrder(170)]
    public class WorldMenu : MonoBehaviour
    {
        [SerializeField] Material bubbleMaterial;
        [SerializeField] Material glowMaterial;
        [SerializeField] Material lineMaterial;
        [SerializeField] Material panelMaterial;
        [Tooltip("How far ahead of your eyes the menu opens (m). Meta suggests 42 to 46 cm for UI you touch.")]
        [SerializeField] float distance = 0.44f;
        [SerializeField] float drop = 0.05f;
        [SerializeField] float spacing = 0.086f;
        [SerializeField] float bubbleSize = 0.078f;
        [Tooltip("How far (degrees) the menu may be from the centre of your view before it follows you.")]
        [SerializeField] float followAngle = 44f;
        [Tooltip("Pinch strength (0-1) that selects, and the strength the pinch must drop below before it can select again.")]
        [SerializeField] Vector2 pinchThresholds = new Vector2(0.85f, 0.5f);
        [Tooltip("A fingertip nearer the menu than the first distance (m) touches instead of pointing, until it is farther than the second (Meta's hover zone).")]
        [SerializeField] Vector2 touchZone = new Vector2(0.15f, 0.2f);

        /// <summary>Raised by the menu button feed (Meta's menu gesture); the menu opens or closes.</summary>
        public static event Action ToggleRequested;
        public static WorldMenu Instance { get; private set; }
        public static void RequestToggle() => ToggleRequested?.Invoke();

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IconId = Shader.PropertyToID("_Icon");
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        static readonly int SelectedId = Shader.PropertyToID("_Selected");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int EqId = Shader.PropertyToID("_Eq");
        static readonly int PausedId = Shader.PropertyToID("_Paused");
        static readonly int OnId = Shader.PropertyToID("_On");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        // Bubble.World values for the bubbles that aren't worlds.
        const int NowPlaying = -1;
        const int ListenToggle = -2;
        const int PreviousSong = -3;
        const int NextSong = -4;
        const int LyricsToggle = -5;
        const int Library = -6;
        const int ExtraBubbles = 6;
        static readonly Color ListenColor = new Color(0.98f, 0.62f, 0.5f);
        static readonly Color SongColor = new Color(0.72f, 0.7f, 1f);
        static readonly Color LyricsColor = new Color(0.55f, 0.85f, 1f);
        static readonly Color AddColor = new Color(0.55f, 0.95f, 0.7f);

        sealed class Bubble
        {
            public Renderer Renderer;
            public int World;
            public Vector2 Slot;
            public float Delay;
            public float Hover;
            public float Pop;
            /// <summary>The ring of a world that is on, 0-1: it eases in and out as the world turns on and off.</summary>
            public float On;
        }

        /// <summary>
        /// One hand's pointer: a ray from the shoulder through the pinch and where it meets the menu, or, within reach,
        /// the index fingertip touching it.
        /// </summary>
        sealed class Pointer
        {
            public bool Active;
            public bool Armed;
            public bool Pinching;
            public float Pinch;
            public Vector3 Hit;
            public Vector3 Origin;
            public int Target = -1;
            public int Aimed = -1;
            public Vector2 AimedAt;
            public float AimedAge;
            public float Visibility;
            public Renderer Cursor;
            public LineStrip Ray;
            /// <summary>The fingertip is within reach of the menu, so this hand touches instead of pointing.</summary>
            public bool Touching;
            /// <summary>The fingertip has been out in front since its last press, so reaching the menu presses.</summary>
            public bool TouchArmed;
            /// <summary>How far the fingertip is in front of the menu's surface (m); negative once through it.</summary>
            public float Depth;
            /// <summary>Where the fingertip is over the menu, in the menu's space.</summary>
            public Vector2 TouchAt;
            /// <summary>Where the fingertip reached the menu this frame, in the menu's space.</summary>
            public Vector2? Tapped;
        }

        // A touch presses when the index fingertip reaches the surface (Meta's touch specs model the fingertip as a 5 mm
        // sphere), and the next press needs the finger 2 cm back out first, so a resting or trembling finger presses once.
        const float TouchContact = 0.005f, TouchRearm = 0.02f;
        // The song page sits this far behind the bubbles.
        const float PageDepth = 0.003f;
        const string ChooseHint = "Touch or pinch to choose";

        Bubble[] bubbles = Array.Empty<Bubble>();
        readonly Pointer[] pointers = { new Pointer(), new Pointer() };
        readonly Vector3[] rayPath = new Vector3[2];
        TypeLabel caption, hint, soundLabel, lyricLabel, credit;
        SongAsset credited, previewCredit;
        Vector3 followPosition;
        Quaternion followRotation;
        float awayTime;
        bool following;
        Transform root;
        MaterialPropertyBlock block;
        AudioPool voices;
        SongLibrary library;
        readonly Vector2?[] aims = new Vector2?[2];
        readonly Vector2?[] presses = new Vector2?[2];
        float honeycomb = 1f;
        bool open, toggleRequested, snapRings;
        float openTime, cooldown;
        // The caption for a world bubble (or for none, -1), kept until the bubble or the worlds that are on change.
        int captionWorld = int.MinValue, captionVersion = -1;
        string captionText = "";
        // What the last world bubble pressed did ("Added Flower dance"), said while it stays under the finger for a moment.
        int doneWorld = -1;
        float doneUntil;
        string doneText = "";
        // The worlds choosing the world bubble under a finger would swap out, kept until that or the worlds on change.
        int goingFor = int.MinValue, goingVersion = -1;
        readonly List<int> going = new List<int>();
        readonly List<int> replaced = new List<int>();
        readonly StringBuilder names = new StringBuilder();
        // Where a fingertip can touch the menu (in its space), and how far behind the bubbles the surface it touches is.
        Rect touchArea;
        float surface;

        public bool IsOpen => open;

        void OnEnable()
        {
            Instance = this;
            ToggleRequested += OnToggle;
        }

        void OnDisable()
        {
            ToggleRequested -= OnToggle;
            if (Instance == this) Instance = null;
        }
        void OnToggle() => toggleRequested = true;

        void LateUpdate()
        {
            if (toggleRequested)
            {
                toggleRequested = false;
                if (open) Close(); else Open(Camera.main);
            }
            Step(HandRig.Instance, WorldManager.Instance, MusicBus.Instance, Camera.main, Time.deltaTime);
        }

        /// <summary>Opens the menu in front of the viewer.</summary>
        public void Open(Camera view)
        {
            if (view == null) return;
            // Songs copied onto the headset while the app runs show up the next time the menu opens.
            MusicBus.Instance?.RefreshSongs();
            open = true;
            openTime = 0f;
            snapRings = true;
            doneWorld = -1;
            Place(view);
            foreach (var b in bubbles) b.Pop = 0f;
            // The pinch that opened the menu (Meta's menu gesture) must be let go before a pinch can select, and a
            // fingertip already at the menu must come back out before it can press.
            foreach (var p in pointers) p.Armed = p.TouchArmed = p.Touching = false;
        }

        public void Close()
        {
            open = false;
            library?.Hide();
        }

        void Place(Camera view)
        {
            if (root == null) return;
            Placement(view, out var position, out var rotation);
            root.SetPositionAndRotation(position, rotation);
            following = false;
            awayTime = 0f;
        }

        void Placement(Camera view, out Vector3 position, out Quaternion rotation)
        {
            var head = view.transform.position;
            var forward = Effects.FlatForward(view);
            position = head + forward * distance + Vector3.down * drop;
            rotation = Quaternion.LookRotation(position - head, Vector3.up);
        }

        /// <summary>
        /// When the menu has been outside the middle of your view for half a second, it glides back in front of you
        /// (with a little lag, so it never chases small head movements).
        /// </summary>
        void Follow(Camera view, float dt)
        {
            // Never slide away from a fingertip reaching for a bubble, even an outer one far from the centre of view.
            if (pointers[0].Touching || pointers[1].Touching)
            {
                awayTime = 0f;
                following = false;
                return;
            }
            var toMenu = root.position - view.transform.position;
            float angle = Vector3.Angle(view.transform.forward, toMenu);
            awayTime = angle > followAngle ? awayTime + dt : 0f;
            if (awayTime > 0.5f && !following)
            {
                following = true;
                Placement(view, out followPosition, out followRotation);
            }
            if (!following) return;
            float ease = 1f - Mathf.Exp(-5f * dt);
            root.SetPositionAndRotation(Vector3.Lerp(root.position, followPosition, ease), Quaternion.Slerp(root.rotation, followRotation, ease));
            if ((root.position - followPosition).sqrMagnitude < 0.0001f) following = false;
        }

        /// <summary>One frame of the menu. Public so the editor preview can drive it.</summary>
        public void Step(HandRig rig, WorldManager manager, MusicBus bus, Camera view, float dt)
        {
            if (manager == null || view == null) return;
            Build(manager);
            // The file picker finished: the menu comes back to show what happened.
            if (library.Poll(bus)) Open(view);
            honeycomb = Mathf.MoveTowards(honeycomb, library.IsOpen ? 0f : 1f, dt * 6f);
            if (open && openTime == 0f) Place(view);
            else if (open) Follow(view, dt);
            openTime += dt;
            cooldown -= dt;

            bool anyVisible = false;
            var gaze = GazePoint(view);
            UpdatePointers(rig, view, dt);

            float t = Application.isPlaying ? Time.time : 0f;
            int hovered = -1;
            FindGoing(manager);
            for (int i = 0; i < bubbles.Length; i++)
            {
                var b = bubbles[i];
                float appear = open ? Mathf.Clamp01((openTime - b.Delay) / 0.32f) : 0f;
                b.Pop = Mathf.MoveTowards(b.Pop, 0f, dt * 2.2f);
                // A world that is on wears a ring, already there as the menu opens and easing in and out after that. While
                // a finger is on a world that would swap it out, its ring starts to unwind.
                float ring = b.World >= 0 && manager.IsOn(b.World) ? going.Contains(b.World) ? 0.35f : 1f : 0f;
                b.On = snapRings ? ring : Mathf.MoveTowards(b.On, ring, dt * 5f);
                float show = open ? EaseOutBack(appear) * honeycomb : 0f;
                bool visible = show > 0.001f || b.Pop > 0.001f;
                if (b.Renderer.gameObject.activeSelf != visible) b.Renderer.gameObject.SetActive(visible);
                if (!visible) continue;
                anyVisible = true;

                // Apple Watch style: bubbles near where you look are a little larger.
                var local = new Vector3(b.Slot.x, b.Slot.y, 0f) + new Vector3(0f, Mathf.Sin(t * 1.3f + i) * 0.003f, 0f);
                float fisheye = Mathf.Lerp(0.82f, 1.06f, Mathf.Exp(-((Vector2)local - gaze).sqrMagnitude / (spacing * spacing * 1.3f)));
                var world = root.TransformPoint(local);
                float size = bubbleSize * fisheye * show;

                bool aimed = open && !library.IsOpen && (pointers[0].Target == i || pointers[1].Target == i);
                b.Hover = Mathf.MoveTowards(b.Hover, aimed ? 1f : 0f, dt * 7f);
                if (aimed) hovered = i;

                float scale = size * (1f + b.Hover * 0.2f + b.Pop * 0.45f);
                b.Renderer.transform.position = world;
                b.Renderer.transform.localScale = Vector3.one * Mathf.Max(scale, 1e-4f);
                Effects.Face(b.Renderer.transform, view);

                bool selected = b.World == ListenToggle ? bus != null && bus.IsListening : b.World == LyricsToggle && LyricsOption.On;
                block.Clear();
                block.SetColor(ColorId, BubbleColor(b, manager));
                block.SetFloat(IconId, b.World >= 0 ? manager.Worlds[b.World].MenuIcon : Icon(b.World));
                block.SetFloat(HoverId, b.Hover);
                block.SetFloat(SelectedId, selected ? 1f : 0f);
                block.SetFloat(OnId, b.On);
                block.SetFloat(AlphaId, Mathf.Clamp01(show) * (1f - b.Pop * 0.85f) + b.Pop * 0.15f);
                if (bus != null) block.SetVector(EqId, new Vector4(bus.Bands[1], bus.Bands[2], bus.Bands[3], bus.Bands[4]));
                block.SetFloat(PausedId, bus != null && !bus.IsPlaying ? 1f : 0f);
                b.Renderer.SetPropertyBlock(block);
            }
            snapRings = false;

            // Touch or pinch to choose. A touch presses the bubble under the fingertip as it reaches the menu; a pinch,
            // the bubble the hand was aiming at just before the pinch closed, so the small jump of the hand while
            // pinching doesn't change the choice.
            if (open && cooldown <= 0f && !library.IsOpen)
            {
                foreach (var p in pointers)
                {
                    int chosen = p.Tapped is Vector2 tap ? BubbleAt(tap, TouchReach)
                        : p.Active && p.Pinching && p.AimedAge <= 0.35f ? p.Aimed : -1;
                    if (chosen < 0) continue;
                    var b = bubbles[chosen];
                    if (openTime - b.Delay < 0.32f) continue;
                    Press(b, manager, bus);
                    p.Aimed = -1;
                    break;
                }
            }

            StepLibrary(bus, dt);
            DrawCaption(manager, bus, hovered, view, dt);
            DrawPointers(view);
            if (root.gameObject.activeSelf != (anyVisible || open)) root.gameObject.SetActive(anyVisible || open);
        }

        void StepLibrary(MusicBus bus, float dt)
        {
            bool ready = open && library.IsOpen && cooldown <= 0f;
            for (int s = 0; s < 2; s++)
            {
                var p = pointers[s];
                // A touching hand aims with its fingertip and presses where it reaches the page. A pointing hand aims
                // with its ray and, as with the bubbles, presses where it was aiming just before the pinch closed.
                aims[s] = p.Touching ? p.TouchAt : p.Active ? (Vector2)root.InverseTransformPoint(p.Hit) : (Vector2?)null;
                presses[s] = !ready ? (Vector2?)null : p.Touching ? p.Tapped : p.Active && p.Pinching && p.AimedAge <= 0.35f ? p.AimedAt : (Vector2?)null;
            }
            library.Step(bus, open, dt, aims, presses);
        }

        void OnLibraryPressed()
        {
            cooldown = 0.4f;
            voices.Play(Synth.Chime(Synth.Pentatonic[4]), root.position, 0.5f);
        }

        /// <summary>Shows made-up songs on the library page, for the editor screenshots.</summary>
        public void PreviewLibrary(System.Collections.Generic.IReadOnlyList<SongAsset> songs, SongAsset playing, int view) =>
            library.Preview(songs, playing, view);

        /// <summary>Shows the credit line as if this song from Audius were playing, for the editor screenshots.</summary>
        public void PreviewCredit(SongAsset song) => previewCredit = song;

        static float Icon(int bubble) => bubble switch
        {
            ListenToggle => 7f, NextSong => 14f, PreviousSong => 15f, LyricsToggle => 23f, Library => 24f, _ => 6f
        };

        static bool IsSongStep(Bubble b) => b.World == PreviousSong || b.World == NextSong;

        Color BubbleColor(Bubble b, WorldManager manager) =>
            b.World >= 0 ? manager.Worlds[b.World].Color : b.World == ListenToggle ? ListenColor : IsSongStep(b) ? SongColor
            : b.World == LyricsToggle ? LyricsColor : b.World == Library ? AddColor : new Color(0.85f, 0.85f, 1f);

        string BubbleName(Bubble b, WorldManager manager, MusicBus bus) =>
            b.World >= 0 ? manager.Worlds[b.World].DisplayName
            : b.World == ListenToggle ? bus != null && bus.IsListening ? "Play the song" : "Listen to the room"
            : IsSongStep(b) ? SongStepName(bus, b.World == NextSong ? 1 : -1)
            : b.World == LyricsToggle ? LyricsOption.On ? "Turn lyrics off" : "Turn lyrics on"
            : b.World == Library ? "Your songs and songs from Audius"
            : bus == null ? "Music" : bus.IsPlaying ? $"Pause {bus.Title}" : $"Play {bus.Title}";

        static string SongStepName(MusicBus bus, int step)
        {
            var song = bus != null ? bus.SongAt(step) : null;
            string where = step > 0 ? "Next" : "Previous";
            return song == null ? $"{where} song" : $"{where}: {song.title}";
        }

        /// <summary>The line under the menu: news of a song that is loading or couldn't play, else how to choose.</summary>
        static string HintText(MusicBus bus)
        {
            if (bus == null) return ChooseHint;
            var song = bus.Loading;
            if (song != null) return song.IsFromAudius ? $"Streaming {song.title} from Audius…" : $"Loading {song.title}…";
            song = bus.Failed;
            if (song == null) return ChooseHint;
            return song.IsFromAudius ? $"Couldn't stream {song.title}. Is the headset online?" : $"Couldn't play {song.title}";
        }

        /// <summary>
        /// Finds where each hand touches or points. Within reach of the menu (Meta's hover zone, touchZone) the index
        /// fingertip touches: a press lands where it reaches the surface coming from the front. Farther away the hand
        /// points: a ray from an estimated shoulder through the point between thumb and index tips, like Meta's own hand
        /// pointers, hits the menu's plane, and the pinch is tracked with hysteresis.
        /// </summary>
        void UpdatePointers(HandRig rig, Camera view, float dt)
        {
            var plane = new Plane(root.forward, root.position);
            var head = view.transform.position;
            var side = Vector3.Cross(Vector3.up, Effects.FlatForward(view)).normalized;
            bool pageOpen = library.IsOpen;
            surface = pageOpen ? PageDepth : 0f;
            for (int s = 0; s < 2; s++)
            {
                var p = pointers[s];
                var hand = rig?.Get(s == 0);
                bool pointed = p.Active;
                p.Active = p.Pinching = false;
                p.Tapped = null;
                p.Target = -1;
                if (!open || hand == null || !hand.IsTracked || hand.InSystemGesture)
                {
                    // A lost hand's ray or dot goes at once rather than lingering where the hand was.
                    p.Touching = p.TouchArmed = false;
                    p.Visibility = 0f;
                    continue;
                }

                // The index fingertip in the menu's space; its depth is how far it is in front of the surface.
                var tip = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
                var local = root.InverseTransformPoint(tip);
                var at = (Vector2)local;
                float depth = surface - local.z;
                if (!p.Touching) p.Touching = Over(at, 0.03f) && Mathf.Abs(depth) < touchZone.x;
                else if (!Over(at, 0.07f) || Mathf.Abs(depth) > touchZone.y) p.Touching = false;

                if (p.Touching)
                {
                    // A press lands where the fingertip reached the surface, between last frame and this one.
                    if (p.TouchArmed && p.Depth > TouchContact && depth <= TouchContact)
                    {
                        float k = (p.Depth - TouchContact) / Mathf.Max(p.Depth - depth, 1e-5f);
                        p.Tapped = Vector2.Lerp(p.TouchAt, at, k);
                        p.TouchArmed = false;
                    }
                    if (!pageOpen) p.Target = BubbleAt(at, TouchReach);
                    // A pinch made while touching mustn't select once the hand pulls back and points.
                    p.Armed = false;
                    p.Pinch = 0f;
                }
                else
                {
                    var thumb = hand.TipJoints[0] != null ? hand.TipJoints[0].position : hand.Tips[0];
                    var pinchPoint = (thumb + tip) * 0.5f;
                    var shoulder = head + Vector3.down * 0.17f + side * (s == 0 ? -0.17f : 0.17f);
                    var ray = new Ray(shoulder, pinchPoint - shoulder);
                    p.Active = plane.Raycast(ray, out float enter);
                    var hit = ray.GetPoint(enter);
                    // Smooth the cursor a little; hand tracking jitters more than a controller.
                    p.Hit = pointed ? Vector3.Lerp(p.Hit, hit, 1f - Mathf.Exp(-25f * dt)) : hit;
                    p.Origin = pinchPoint;

                    float pinch = p.Pinch = hand.Pinch;
                    if (pinch < pinchThresholds.y) p.Armed = true;
                    if (p.Armed && pinch >= pinchThresholds.x)
                    {
                        p.Pinching = true;
                        p.Armed = false;
                    }

                    // The nearest bubble within reach of the cursor is the target.
                    var localHit = (Vector2)root.InverseTransformPoint(p.Hit);
                    if (p.Active) p.Target = BubbleAt(localHit, bubbleSize * 0.62f);
                    // Remember the aim while the pinch is still open.
                    if (pinch < 0.6f)
                    {
                        p.Aimed = p.Target;
                        p.AimedAt = localHit;
                        p.AimedAge = 0f;
                    }
                    else p.AimedAge += dt;
                }

                // The next touch needs the fingertip back out in front first.
                if (depth > TouchRearm) p.TouchArmed = true;
                p.Depth = depth;
                p.TouchAt = at;
                p.Visibility = Mathf.MoveTowards(p.Visibility, p.Active || p.Touching ? 1f : 0f, dt * 6f);
            }
        }

        /// <summary>
        /// A pointing hand shows a soft ray from the pinch to the menu and a glowing cursor where it lands, which tightens
        /// as you pinch. A touching hand shows only a dot under its fingertip that shrinks and brightens as the finger
        /// nears the menu, so you can see how close you are to pressing.
        /// </summary>
        void DrawPointers(Camera view)
        {
            for (int s = 0; s < 2; s++)
            {
                var p = pointers[s];
                bool show = p.Visibility > 0.01f;
                if (p.Cursor.gameObject.activeSelf != show) p.Cursor.gameObject.SetActive(show);
                p.Ray.SetVisible(show && !p.Touching);
                if (!show) continue;
                block.Clear();
                if (p.Touching)
                {
                    // 0 at the surface, 1 at the edge of the touch zone.
                    float near = Mathf.Clamp01(p.Depth / touchZone.x);
                    p.Cursor.transform.position = root.TransformPoint(new Vector3(p.TouchAt.x, p.TouchAt.y, surface - 0.004f));
                    p.Cursor.transform.localScale = Vector3.one * Mathf.Lerp(0.01f, 0.03f, near);
                    block.SetColor(ColorId, Color.white);
                    block.SetFloat(IntensityId, p.Visibility * Mathf.Lerp(1.4f, 0.5f, near));
                }
                else
                {
                    float pinch = p.Pinch;
                    bool onBubble = p.Target >= 0;
                    p.Cursor.transform.position = p.Hit - root.forward * 0.004f;
                    p.Cursor.transform.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.016f, pinch) * (onBubble ? 1.2f : 1f);
                    block.SetColor(ColorId, onBubble ? Color.white : new Color(0.85f, 0.9f, 1f));
                    block.SetFloat(IntensityId, p.Visibility * (onBubble ? 1.1f : 0.6f) * (1f + pinch * 0.6f));
                    rayPath[0] = p.Origin;
                    rayPath[1] = p.Hit;
                    p.Ray.Set(rayPath, 2, 0.0025f, new Color(0.85f, 0.9f, 1f, 0.22f * p.Visibility), view.transform.position, false);
                }
                Effects.Face(p.Cursor.transform, view);
                p.Cursor.SetPropertyBlock(block);
            }
        }

        float TouchReach => bubbleSize * 0.55f;

        /// <summary>The bubble nearest a point on the menu, if one is within <paramref name="reach"/> of it, else -1.</summary>
        int BubbleAt(Vector2 at, float reach)
        {
            int found = -1;
            for (int i = 0; i < bubbles.Length; i++)
            {
                float d = Vector2.Distance(at, bubbles[i].Slot);
                if (d < reach)
                {
                    reach = d;
                    found = i;
                }
            }
            return found;
        }

        /// <summary>Whether a point on the menu is over the part a fingertip can touch, give or take <paramref name="margin"/>.</summary>
        bool Over(Vector2 at, float margin) =>
            at.x > touchArea.xMin - margin && at.x < touchArea.xMax + margin && at.y > touchArea.yMin - margin && at.y < touchArea.yMax + margin;

        /// <summary>
        /// What choosing the bubble you point at or reach for will do, large above the honeycomb, with a soft shadow so
        /// it reads over any room: for a world, adding it, swapping it in or turning it off (<see cref="WorldCaption"/>);
        /// for the other bubbles, their name. With no bubble under a finger it names the worlds that are on, in the
        /// colour of the one chosen last. A small hint below says how to choose.
        /// </summary>
        void DrawCaption(WorldManager manager, MusicBus bus, int hovered, Camera view, float dt)
        {
            float show = open ? Mathf.Clamp01((openTime - 0.1f) / 0.3f) * honeycomb : 0f;
            bool visible = show > 0.001f;
            if (caption.GameObject.activeSelf != visible)
            {
                caption.GameObject.SetActive(visible);
                hint.GameObject.SetActive(visible);
                soundLabel.GameObject.SetActive(visible);
                lyricLabel.GameObject.SetActive(visible && lyricCount > 0);
            }
            var playing = previewCredit != null ? previewCredit : bus != null && !bus.IsListening ? bus.CurrentSong : null;
            bool credits = visible && playing != null && playing.IsFromAudius;
            if (credit.GameObject.activeSelf != credits) credit.GameObject.SetActive(credits);
            if (!visible) return;

            Bubble subject = hovered >= 0 ? bubbles[hovered] : null;
            int lead = manager.Current;
            // Right after a press the caption says what it did, rather than what pressing again would do.
            if (subject != null && subject.World == doneWorld && openTime < doneUntil) caption.Text = doneText;
            else if (subject == null || subject.World >= 0) caption.Text = WorldCaption(subject != null ? subject.World : -1, manager);
            else caption.Text = BubbleName(subject, manager, bus);
            var color = subject != null ? Color.Lerp(Color.white, BubbleColor(subject, manager), 0.3f)
                : lead >= 0 ? Color.Lerp(Color.white, manager.Worlds[lead].Color, 0.15f) : Color.white;
            color.a = show * (hovered >= 0 ? 1f : 0.8f);
            caption.Color = color;
            hint.Color = new Color(1f, 1f, 1f, show * 0.8f);
            soundLabel.Color = lyricLabel.Color = new Color(1f, 1f, 1f, show * 0.9f);

            float highest = spacing, lowest = 0f;
            foreach (var b in bubbles)
            {
                highest = Mathf.Max(highest, b.Slot.y);
                lowest = Mathf.Min(lowest, b.Slot.y);
            }
            var rotation = root.rotation;
            var top = root.TransformPoint(new Vector3(0f, highest + bubbleSize * 1.45f, 0f));
            caption.Transform.SetPositionAndRotation(top, rotation);
            hint.Text = HintText(bus);
            hint.Transform.SetPositionAndRotation(root.TransformPoint(new Vector3(0f, lowest - bubbleSize * 0.75f, 0f)), rotation);
            if (credits)
            {
                if (credited != playing)
                {
                    credited = playing;
                    credit.Text = AudiusSongs.Credit(playing);
                }
                credit.Color = new Color(1f, 1f, 1f, show * 0.65f);
                credit.Transform.SetPositionAndRotation(root.TransformPoint(new Vector3(0f, lowest - bubbleSize * 0.75f - 0.024f, 0f)), rotation);
            }
            soundLabel.Transform.SetPositionAndRotation(root.TransformPoint(new Vector3(SoundCenter.x, highest + bubbleSize * 0.62f, 0f)), rotation);
            lyricLabel.Transform.SetPositionAndRotation(root.TransformPoint(new Vector3(LyricCenter.x, highest + bubbleSize * 0.62f, 0f)), rotation);
        }

        /// <summary>
        /// The words of the caption for world bubble <paramref name="world"/>: what choosing it will do, as in "Add
        /// Flower dance", "Swap Cube field for Flower dance", "Turn off Solar system", or "Solar system is on" for the
        /// only world on. For -1 (no bubble under a finger), the worlds that are on: "Flower dance + Solar system".
        /// Made again only when the bubble or the worlds that are on change.
        /// </summary>
        string WorldCaption(int world, WorldManager manager)
        {
            if (world == captionWorld && manager.Version == captionVersion) return captionText;
            captionWorld = world;
            captionVersion = manager.Version;
            if (world < 0)
                return captionText = Names(manager, manager.Active, " + ", " + ");
            string name = manager.Worlds[world].DisplayName;
            captionText = manager.Plan(world, replaced) switch
            {
                WorldManager.Choice.Add => $"Add {name}",
                WorldManager.Choice.Swap => $"Swap {Names(manager, replaced, ", ", " and ")} for {name}",
                WorldManager.Choice.TurnOff => $"Turn off {name}",
                WorldManager.Choice.StayOn => $"{name} is on",
                _ => name
            };
            return captionText;
        }

        /// <summary>
        /// The worlds that choosing the world bubble a finger is on would swap out (none for other bubbles, or for a
        /// world that would only turn off), found again only when that bubble or the worlds that are on change.
        /// </summary>
        void FindGoing(WorldManager manager)
        {
            int target = -1;
            if (open && !library.IsOpen)
                foreach (var p in pointers)
                    if (p.Target >= 0 && bubbles[p.Target].World >= 0) target = bubbles[p.Target].World;
            if (target == goingFor && manager.Version == goingVersion) return;
            goingFor = target;
            goingVersion = manager.Version;
            if (target < 0 || manager.Plan(target, going) != WorldManager.Choice.Swap) going.Clear();
        }

        /// <summary>World names in a row: "Cube field", "Cube field and Solar system", "A, B and C".</summary>
        string Names(WorldManager manager, IReadOnlyList<int> indices, string between, string beforeLast)
        {
            names.Clear();
            for (int i = 0; i < indices.Count; i++)
            {
                if (i > 0) names.Append(i == indices.Count - 1 ? beforeLast : between);
                names.Append(manager.Worlds[indices[i]].DisplayName);
            }
            return names.ToString();
        }

        void Press(Bubble b, WorldManager manager, MusicBus bus)
        {
            cooldown = 0.6f;
            b.Pop = 1f;
            if (b.World >= 0)
            {
                // The menu stays open, so more worlds can be put together; the menu gesture closes it. A world turning
                // off answers an octave lower, and the one world that stays on only softly.
                string name = manager.Worlds[b.World].DisplayName;
                var choice = manager.Plan(b.World, replaced);
                doneText = choice switch
                {
                    WorldManager.Choice.Add => $"Added {name}",
                    WorldManager.Choice.Swap => $"Swapped {Names(manager, replaced, ", ", " and ")} for {name}",
                    WorldManager.Choice.TurnOff => $"Turned off {name}",
                    _ => $"{name} stays on"
                };
                doneWorld = b.World;
                doneUntil = openTime + 1.5f;
                manager.Choose(b.World);
                float tone = Synth.Pentatonic[(choice == WorldManager.Choice.TurnOff ? 1 : 6) + b.World % 5];
                voices.Play(Synth.Chime(tone), b.Renderer.transform.position, choice == WorldManager.Choice.StayOn ? 0.25f : 0.6f);
                return;
            }
            float note = Synth.Pentatonic[b.World == ListenToggle ? 4 : IsSongStep(b) ? 3 : b.World == LyricsToggle || b.World == Library ? 2 : 5];
            voices.Play(Synth.Chime(note), b.Renderer.transform.position, 0.6f);
            if (b.World == LyricsToggle) LyricsOption.On = !LyricsOption.On;
            else if (b.World == Library) library.Show();
            else if (bus == null) return;
            else if (b.World == ListenToggle) bus.ToggleListen();
            else if (IsSongStep(b)) bus.StepSong(b.World == NextSong ? 1 : -1);
            else bus.TogglePause();
        }

        /// <summary>Where the viewer is looking on the menu's plane, in the menu's local space.</summary>
        Vector2 GazePoint(Camera view)
        {
            var plane = new Plane(root.forward, root.position);
            var ray = new Ray(view.transform.position, view.transform.forward);
            if (!plane.Raycast(ray, out float enter)) return Vector2.zero;
            return root.InverseTransformPoint(ray.GetPoint(enter));
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        // Each group is a small honeycomb: a centre cell, a ring of six, then outer cells on its far side first, so
        // the two groups grow away from each other.
        Vector2 SoundCenter => new Vector2(-spacing * 2.25f, spacing * 0.35f);
        Vector2 LyricCenter => new Vector2(spacing * 2.25f, spacing * 0.35f);
        static readonly float[] InnerAngles = { 90f, 30f, -30f, -90f, -150f, 150f };
        static readonly (float angle, bool corner)[] OuterLeft =
        {
            (180f, false), (150f, true), (-150f, true), (120f, false), (-120f, false), (90f, true),
            (-90f, true), (60f, false), (-60f, false), (30f, true), (-30f, true), (0f, false)
        };
        int lyricCount;

        Vector2 HexSlot(Vector2 center, int index, bool growLeft)
        {
            if (index == 0) return center;
            if (index <= 6)
            {
                float a = InnerAngles[index - 1] * Mathf.Deg2Rad;
                return center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * spacing;
            }
            var (angle, corner) = OuterLeft[(index - 7) % OuterLeft.Length];
            if (!growLeft) angle = 180f - angle;
            float r = corner ? spacing * 2f : spacing * Mathf.Sqrt(3f);
            float rad = angle * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * r;
        }

        void Build(WorldManager manager)
        {
            if (root != null && bubbles.Length == manager.Worlds.Count + ExtraBubbles) return;
            if (root != null)
            {
                if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject);
            }
            block = new MaterialPropertyBlock();
            root = new GameObject("Menu").transform;
            root.SetParent(transform, false);
            voices ??= new AudioPool(transform, 3);

            int count = manager.Worlds.Count;
            bubbles = new Bubble[count + ExtraBubbles];
            int soundIndex = 0, lyricIndex = 0;
            lyricCount = 0;
            float lowest = 0f;
            var slots = new Vector2[count];
            for (int w = 0; w < count; w++)
            {
                bool lyric = manager.Worlds[w] != null && manager.Worlds[w].IsLyric;
                slots[w] = lyric ? HexSlot(LyricCenter, lyricIndex++, false) : HexSlot(SoundCenter, soundIndex++, true);
                lowest = Mathf.Min(lowest, slots[w].y);
            }
            lyricCount = lyricIndex;
            // The song row sits under both groups: previous, play or pause, next, listen, lyrics, add a song.
            float rowY = lowest - spacing * 1.15f;
            float rowX = (SoundCenter.x + LyricCenter.x) * 0.5f;
            for (int i = 0; i < bubbles.Length; i++)
            {
                int world = i < count ? i : i == count ? NowPlaying : ListenToggle - (i - count - 1);
                Vector2 slot = world >= 0 ? slots[world]
                    : world == PreviousSong ? new Vector2(rowX - spacing * 2.5f, rowY)
                    : world == NowPlaying ? new Vector2(rowX - spacing * 1.5f, rowY)
                    : world == NextSong ? new Vector2(rowX - spacing * 0.5f, rowY)
                    : world == ListenToggle ? new Vector2(rowX + spacing * 0.5f, rowY)
                    : world == LyricsToggle ? new Vector2(rowX + spacing * 1.5f, rowY)
                    : new Vector2(rowX + spacing * 2.5f, rowY);

                string objectName = world >= 0 ? $"Bubble {world}" : world == ListenToggle ? "Bubble Listen"
                    : world == PreviousSong ? "Bubble Previous Song" : world == NextSong ? "Bubble Next Song"
                    : world == LyricsToggle ? "Bubble Lyrics" : world == Library ? "Bubble Library" : "Bubble Now Playing";
                var r = Effects.MakeQuad(objectName, root, bubbleMaterial);
                float delay = 0.04f + slot.magnitude * 0.6f;
                bubbles[i] = new Bubble { Renderer = r, World = world, Slot = slot, Delay = delay };
                r.gameObject.SetActive(false);
            }

            soundLabel = new TypeLabel("Sound Worlds", root, 0.0135f, 0f);
            soundLabel.Text = "Sound worlds";
            lyricLabel = new TypeLabel("Lyric Worlds", root, 0.0135f, 0f);
            lyricLabel.Text = "Lyric worlds";
            caption = new TypeLabel("Caption", root, 0.019f, 0f);
            hint = new TypeLabel("Hint", root, 0.0135f, 1f);
            hint.Text = ChooseHint;
            credit = new TypeLabel("Credit", root, 0.0115f, 1f);
            credited = null;
            foreach (var label in new[] { soundLabel, lyricLabel, caption, hint, credit }) label.GameObject.SetActive(false);

            // Editor previews build a menu without the scene's materials.
            var panel = panelMaterial != null ? panelMaterial : new Material(Shader.Find("MusicInYourHands/Panel"));
            library = new SongLibrary(root, panel, new Vector2(0f, spacing * 0.2f));
            library.Pressed += OnLibraryPressed;
            library.PickerOpened += Close;

            // Where a fingertip can touch: the bubbles, with room for their size, and the song page.
            Vector2 min = bubbles[0].Slot, max = min;
            foreach (var b in bubbles)
            {
                min = Vector2.Min(min, b.Slot);
                max = Vector2.Max(max, b.Slot);
            }
            var page = library.Area;
            touchArea = Rect.MinMaxRect(Mathf.Min(min.x - bubbleSize, page.xMin), Mathf.Min(min.y - bubbleSize, page.yMin),
                Mathf.Max(max.x + bubbleSize, page.xMax), Mathf.Max(max.y + bubbleSize, page.yMax));

            for (int s = 0; s < 2; s++)
            {
                pointers[s].Cursor = Effects.MakeQuad(s == 0 ? "Cursor L" : "Cursor R", root, glowMaterial);
                pointers[s].Cursor.gameObject.SetActive(false);
                pointers[s].Ray = new LineStrip(s == 0 ? "Pointer L" : "Pointer R", root, lineMaterial, 2);
                pointers[s].Ray.SetVisible(false);
            }
            root.gameObject.SetActive(false);
        }
    }
}
