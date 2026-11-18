using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The world menu: glass bubbles in one honeycomb, like an Apple Watch home screen, opened with Meta's left-hand
    /// menu gesture. Every world has a bubble with a small animated emblem, in menu order, and the honeycomb works out its
    /// rows from the number of worlds so that it always fits the middle of your view (Meta's recommended design area,
    /// 58° across and 54° high), shrinking and staying centred as worlds come and go. Below it a row plays and pauses the
    /// song, steps to the previous and next song (including songs added on the headset), switches to
    /// listening to the room, turns the lyrics on and off and opens the cassette deck (CassetteDeck), where you choose a
    /// song by putting its cassette in a radio; the menu steps aside for it. The deck's song list button brings the menu
    /// back with the song page (SongLibrary) in the honeycomb's place, and the page's Back button returns to the deck.
    /// The honeycomb's last cell, just above the end of the song row, is a "?" bubble ("Show me how"): the menu closes
    /// and the onboarding's lessons start again. Bubbles near where you look are a little larger.
    /// The menu opens half a metre along your gaze, within reach, so you can touch a bubble or button with your index
    /// fingertip, or point at it from farther away and pinch. It stays put while you look round it, but once you have
    /// looked well away for a moment it glides back in front of you, up or down as well as sideways, and never away from
    /// a hand reaching for it. A pointing hand casts a soft ray onto the menu (from the shoulder through the pinch); once
    /// its fingertip comes within 15 cm of the menu, the ray gives way to a dot under the fingertip that shrinks as the
    /// finger nears (Meta's touch specs). The bubble you point at or reach for swells and its name shows large above the
    /// honeycomb. Under the menu two lines say how to choose, or that a song is loading or couldn't play.
    /// </summary>
    [DefaultExecutionOrder(170)]
    public class WorldMenu : MonoBehaviour
    {
        [SerializeField] Material bubbleMaterial;
        [SerializeField] Material glowMaterial;
        [SerializeField] Material lineMaterial;
        [SerializeField] Material panelMaterial;
        [Tooltip("How far along your gaze the middle of the menu opens (m). Things you touch should be at least 50 cm from the eyes, and 50 cm is still within reach when seated.")]
        [SerializeField] float viewDistance = 0.5f;
        [Tooltip("How far below the centre of your view the middle of the menu sits (degrees).")]
        [SerializeField] float dropAngle = 1f;
        [Tooltip("How wide a bubble is at rest (m), the smallest it ever shows; bubbles near where you look grow a little. Meta's smallest target for hands is 64 mm.")]
        [SerializeField] float bubbleDiameter = 0.066f;
        [Tooltip("The gap between neighbouring bubbles at rest (m).")]
        [SerializeField] float bubbleGap = 0.006f;
        [Tooltip("How far (degrees) the menu may be from the middle of your view, and for how long (s), before it glides back in front of you.")]
        [SerializeField] Vector2 lazyFollow = new Vector2(20f, 0.4f);
        [Tooltip("Roughly how long the glide back in front of you takes (s).")]
        [SerializeField] float glideTime = 0.3f;
        [Tooltip("Pinch strength (0-1) that selects, and the strength the pinch must drop below before it can select again.")]
        [SerializeField] Vector2 pinchThresholds = new Vector2(0.85f, 0.5f);
        [Tooltip("A fingertip nearer the menu than the first distance (m) touches instead of pointing, until it is farther than the second (Meta's hover zone).")]
        [SerializeField] Vector2 touchZone = new Vector2(0.15f, 0.2f);
        [Tooltip("How the cassette deck's turntable turns under the hands: how long it glides, how a brushing hand pulls it along, how a hand catches it, and its top speed.")]
        [SerializeField] DeckRing.Tuning turntable = new DeckRing.Tuning();

        /// <summary>Meta's recommended design area for essential content: degrees across and high, round the centre of the view.</summary>
        public static readonly Vector2 DesignArea = new Vector2(58f, 54f);
        /// <summary>The area (degrees across and high) the layout aims to stay within, a margin inside the design area.</summary>
        public static readonly Vector2 AimArea = new Vector2(50f, 48f);
        /// <summary>How much larger than at rest a bubble near where you look grows, and how much more one you point at swells.</summary>
        public const float NearGaze = 1.08f, Swell = 1.2f;
        /// <summary>Letter heights (m) of the caption over the honeycomb and the hint lines under the song row.</summary>
        public const float CaptionSize = 0.018f, HintSize = 0.013f;
        /// <summary>The song page's size in the menu, so that it fits the design area too.</summary>
        public const float PageScale = 0.88f;
        /// <summary>How far up and down (degrees) the menu follows your gaze: not so low or high that it is hard to reach.</summary>
        static readonly Vector2 PitchRange = new Vector2(-40f, 15f);
        // Lettering, in letter heights: above and below the baseline, and from one line's baseline to the next.
        const float Ascent = 0.78f, Descent = 0.22f, LineStep = 1.3f;

        /// <summary>Raised by the menu button feed (Meta's menu gesture); the menu opens or closes.</summary>
        public static event Action ToggleRequested;
        public static WorldMenu Instance { get; private set; }
        /// <summary>Meta's menu gesture, made with the left hand.</summary>
        public static void RequestToggle() => RequestToggle(true);

        /// <summary>Opens or closes the menu for a gesture of the given hand, which lights up as it does.</summary>
        public static void RequestToggle(bool left)
        {
            toggleLeft = left;
            ToggleRequested?.Invoke();
        }

        // The hand that asked for the last toggle.
        static bool toggleLeft = true;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IconId = Shader.PropertyToID("_Icon");
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        static readonly int SelectedId = Shader.PropertyToID("_Selected");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int EqId = Shader.PropertyToID("_Eq");
        static readonly int PausedId = Shader.PropertyToID("_Paused");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        // Bubble.World values for the bubbles that aren't worlds.
        const int NowPlaying = -1;
        const int ListenToggle = -2;
        const int PreviousSong = -3;
        const int NextSong = -4;
        const int LyricsToggle = -5;
        const int Library = -6;
        const int Help = -7;
        /// <summary>The bubbles of the song row under the honeycomb.</summary>
        const int SongRow = 6;
        /// <summary>The bubbles that aren't worlds: the song row, and the "?" at the end of the honeycomb.</summary>
        public const int ExtraBubbles = SongRow + 1;
        const string HelpName = "Show me how";
        static readonly Color ListenColor = new Color(0.98f, 0.62f, 0.5f);
        static readonly Color SongColor = new Color(0.72f, 0.7f, 1f);
        static readonly Color LyricsColor = new Color(0.55f, 0.85f, 1f);
        static readonly Color AddColor = new Color(0.55f, 0.95f, 0.7f);
        // The onboarding's warm colour, for the "?" and the words the onboarding asks the caption to show.
        static readonly Color HelpColor = new Color(1f, 0.85f, 0.55f);

        sealed class Bubble
        {
            public Renderer Renderer;
            public int World;
            public Vector2 Slot;
            public float Delay;
            public float Hover;
            public float Pop;
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
            /// <summary>Where the ray passes through the hand, held in the palm's frame so a closing pinch doesn't move it.</summary>
            public Vector3 AimOffset;
            /// <summary><see cref="AimOffset"/> belongs to this hand's current pointing (not yet reset by a touch or loss).</summary>
            public bool Aiming;
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
        /// <summary>How quickly (per second) the ray's point in the open hand follows the hand's shape (see Pointer.AimOffset).</summary>
        const float AimSettle = 1.5f;
        /// <summary>Thumb to index fingertip (m) beyond which the hand counts as open, so its shape may still settle the aim.</summary>
        const float OpenSpread = 0.06f;
        // The song page sits this far behind the bubbles.
        const float PageDepth = 0.003f;
        const string ChooseHint = "Touch or pinch a bubble to choose";
        const string GestureHint = "Thumb tap: play or pause  ·  thumb swipe: change song";

        Bubble[] bubbles = Array.Empty<Bubble>();
        readonly Pointer[] pointers = { new Pointer(), new Pointer() };
        readonly Vector3[] rayPath = new Vector3[2];
        TypeLabel caption, hint, gestures;
        string previewCaption;
        // What the caption and the hint were last worked out for, so their words are only made again when that changes.
        Bubble captioned;
        int captionedState = -1;
        SongAsset captionedSong, hintLoading, hintFailed;
        string captionedPrompt;
        bool hinted;
        // The glide back in front of you: degrees per second sideways and up, and metres per second nearer or farther.
        Vector2 glideSpeed;
        float distanceSpeed, awayTime;
        bool gliding;
        Transform root;
        MaterialPropertyBlock block;
        AudioPool voices;
        SongLibrary library;
        CassetteDeck deck;
        Camera lastView;
        MusicBus lastBus;
        bool pickerFromDeck;
        readonly Vector2?[] aims = new Vector2?[2];
        readonly Vector2?[] presses = new Vector2?[2];
        float honeycomb = 1f;
        bool open, toggleRequested;
        float openTime, closeAt = -1f, cooldown;
        // Where a fingertip can touch the menu (in its space), and how far behind the bubbles the surface it touches is.
        Rect touchArea;
        float surface;
        // The layout: how many worlds each row holds, how wide the text may be, and where the lines of text sit.
        int[] rowLengths = Array.Empty<int>();
        float textWidth;

        /// <summary>
        /// True while the menu or the cassette deck is open (the song page included): the hands are choosing, so the
        /// worlds and the music gestures leave them alone.
        /// </summary>
        public bool IsOpen => open || (deck != null && deck.IsOpen);

        /// <summary>The cassette deck, once the menu has been built (for the editor checks and screenshots).</summary>
        public CassetteDeck Deck => deck;

        /// <summary>How many world bubbles each row of the honeycomb holds, top to bottom (for the editor checks).</summary>
        public IReadOnlyList<int> RowLengths => rowLengths;

        /// <summary>True while the menu is gliding back in front of you (for the editor checks).</summary>
        public bool Gliding => gliding;

        /// <summary>
        /// Words for the caption over the honeycomb while no bubble is pointed at, in place of the current world's name
        /// (the onboarding's "Touch a world"); null for the usual caption.
        /// </summary>
        public string Prompt { get; set; }

        /// <summary>A world whose bubble gently swells and glows to invite a touch (the onboarding's lesson); -1 for none.</summary>
        public int Beckoned { get; set; } = -1;

        /// <summary>
        /// Where a world's bubble rests on the open menu, in world space (without its gentle bob). False while the menu is
        /// closed or showing the song page, or for a world without a bubble. The onboarding points its ghost finger at one.
        /// </summary>
        public bool BubblePosition(int world, out Vector3 position)
        {
            position = default;
            if (root == null || world < 0 || world >= bubbles.Length || bubbles[world].World != world) return false;
            position = root.TransformPoint(bubbles[world].Slot);
            return open && !library.IsOpen;
        }

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
                Toggle(Camera.main);
                ToggleFeedback(HandRig.Instance, Camera.main);
            }
            Step(HandRig.Instance, WorldManager.Instance, MusicBus.Instance, Camera.main, Time.deltaTime);
        }

        /// <summary>
        /// What Meta's menu gesture does: closes the cassette deck if it is open, else opens or closes the menu. Closing
        /// either leaves you in the world you were in.
        /// </summary>
        public void Toggle(Camera view)
        {
            if (deck != null && deck.IsOpen) deck.Close();
            else if (open) Close();
            else Open(view);
        }

        /// <summary>
        /// The hand that opened or closed the menu lights up, and a chime runs up as it opens or down as it closes,
        /// from that hand (or in front of you, if it can't be seen). Public for the editor checks.
        /// </summary>
        public void ToggleFeedback(HandRig rig, Camera view)
        {
            var hand = rig != null ? rig.Get(toggleLeft) : null;
            var at = hand != null && hand.IsTracked ? (hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position)
                : view != null ? view.transform.position + Effects.FlatForward(view) * 0.4f : Vector3.zero;
            GestureFeedback.Show(IsOpen ? GestureKind.MenuOpen : GestureKind.MenuClose, toggleLeft, at);
        }

        /// <summary>Opens the cassette deck straight away, the menu stepping aside (editor checks and screenshots).</summary>
        public void OpenDeck(Camera view)
        {
            if (view == null) return;
            lastView = view;
            Close();
            deck?.Open(view, lastBus != null ? lastBus : MusicBus.Instance);
        }

        /// <summary>The deck's song list button: the deck steps aside and the menu opens on the song page.</summary>
        public void ShowSongList(Camera view)
        {
            if (view == null) return;
            deck?.Close();
            library.Show();
            Open(view);
        }

        void OnLibraryBack()
        {
            Close();
            if (lastView != null) deck?.Open(lastView, lastBus != null ? lastBus : MusicBus.Instance);
        }

        void OnDeckPicker()
        {
            pickerFromDeck = true;
            deck?.Close();
            Close();
        }

        /// <summary>Opens the menu in front of the viewer.</summary>
        public void Open(Camera view)
        {
            if (view == null) return;
            // Songs copied onto the headset while the app runs show up the next time the menu opens.
            MusicBus.Instance?.RefreshSongs();
            open = true;
            openTime = 0f;
            closeAt = -1f;
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
            var direction = HomeDirection(view);
            root.SetPositionAndRotation(view.transform.position + direction * viewDistance, Quaternion.LookRotation(direction, Vector3.up));
            gliding = false;
            awayTime = 0f;
            glideSpeed = Vector2.zero;
            distanceSpeed = 0f;
        }

        /// <summary>
        /// Where the middle of the menu belongs, as a direction from the eyes: along your gaze, a little lower, and kept
        /// within PitchRange up and down so it never opens out of easy reach.
        /// </summary>
        Vector3 HomeDirection(Camera view)
        {
            var forward = view.transform.forward;
            var flat = Effects.FlatForward(view);
            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg, PitchRange.x, PitchRange.y) - dropAngle;
            return Direction(yaw, pitch);
        }

        static Vector3 Direction(float yaw, float pitch)
        {
            float y = yaw * Mathf.Deg2Rad, p = pitch * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(p) * Mathf.Sin(y), Mathf.Sin(p), Mathf.Cos(p) * Mathf.Cos(y));
        }

        static void Angles(Vector3 direction, out float yaw, out float pitch)
        {
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Lazy follow. The menu stays where it is in the room while you look round it. Once its middle has been more
        /// than lazyFollow.x degrees from where it belongs (the middle of your view) for lazyFollow.y seconds, it glides
        /// back there round your head at its own distance, easing in and out, sideways and up or down. A hand touching it
        /// or pointing at it holds it still, and one that reaches for it mid-glide brings it smoothly to a stop.
        /// </summary>
        void Follow(Camera view, float dt)
        {
            var head = view.transform.position;
            var home = HomeDirection(view);
            var offset = root.position - head;
            float distanceNow = offset.magnitude;
            var direction = distanceNow > 1e-4f ? offset / distanceNow : home;
            bool reaching = Reaching();
            awayTime = !reaching && Vector3.Angle(direction, home) > lazyFollow.x ? awayTime + dt : 0f;
            if (awayTime >= lazyFollow.y) gliding = true;
            if (!gliding || dt <= 0f) return;

            Angles(direction, out float yaw, out float pitch);
            Angles(home, out float homeYaw, out float homePitch);
            bool settled;
            if (reaching)
            {
                float brake = Mathf.Exp(-12f * dt);
                glideSpeed *= brake;
                distanceSpeed *= brake;
                yaw += glideSpeed.x * dt;
                pitch += glideSpeed.y * dt;
                distanceNow += distanceSpeed * dt;
                settled = glideSpeed.sqrMagnitude < 1f;
            }
            else
            {
                float time = Mathf.Max(glideTime, 0.01f);
                yaw = Mathf.SmoothDampAngle(yaw, homeYaw, ref glideSpeed.x, time, Mathf.Infinity, dt);
                pitch = Mathf.SmoothDampAngle(pitch, homePitch, ref glideSpeed.y, time, Mathf.Infinity, dt);
                distanceNow = Mathf.SmoothDamp(distanceNow, viewDistance, ref distanceSpeed, time, Mathf.Infinity, dt);
                settled = Mathf.Abs(Mathf.DeltaAngle(yaw, homeYaw)) < 0.3f && Mathf.Abs(pitch - homePitch) < 0.3f && glideSpeed.sqrMagnitude < 4f;
            }
            var to = Direction(yaw, pitch);
            var facing = Quaternion.LookRotation(to, Vector3.up);
            var rotation = Quaternion.Slerp(root.rotation, facing, 1f - Mathf.Exp(-12f * dt));
            root.SetPositionAndRotation(head + to * distanceNow, rotation);
            if (settled && Quaternion.Angle(rotation, facing) < 0.5f)
            {
                gliding = false;
                glideSpeed = Vector2.zero;
                distanceSpeed = 0f;
            }
        }

        /// <summary>Whether a hand is touching the menu or pointing at it, so it shouldn't move.</summary>
        bool Reaching()
        {
            foreach (var p in pointers)
                if (p.Touching || (p.Active && Over(root.InverseTransformPoint(p.Hit), 0f))) return true;
            return false;
        }

        /// <summary>One frame of the menu. Public so the editor preview can drive it.</summary>
        public void Step(HandRig rig, WorldManager manager, MusicBus bus, Camera view, float dt)
        {
            if (manager == null || view == null) return;
            Build(manager);
            lastView = view;
            lastBus = bus;
            // The file picker finished: the menu comes back to show what happened, on the song page. If it was
            // opened from the deck's blank cassette and nothing was picked, the deck comes back instead.
            if (library.Poll(bus, out var outcome))
            {
                if (pickerFromDeck && outcome == SongPicker.Outcome.Cancelled)
                {
                    library.Hide();
                    deck.Open(view, bus);
                }
                else Open(view);
                pickerFromDeck = false;
            }
            honeycomb = Mathf.MoveTowards(honeycomb, library.IsOpen ? 0f : 1f, dt * 6f);
            if (open && openTime == 0f) Place(view);
            else if (open) Follow(view, dt);
            openTime += dt;
            cooldown -= dt;
            if (closeAt >= 0f && openTime >= closeAt) Close();

            bool anyVisible = false;
            var gaze = GazePoint(view);
            UpdatePointers(rig, view, dt);

            float t = Application.isPlaying ? Time.time : 0f;
            float spacing = Spacing;
            int hovered = -1;
            for (int i = 0; i < bubbles.Length; i++)
            {
                var b = bubbles[i];
                float appear = open ? Mathf.Clamp01((openTime - b.Delay) / 0.32f) : 0f;
                b.Pop = Mathf.MoveTowards(b.Pop, 0f, dt * 2.2f);
                float show = open ? EaseOutBack(appear) * honeycomb : 0f;
                bool visible = show > 0.001f || b.Pop > 0.001f;
                if (b.Renderer.gameObject.activeSelf != visible) b.Renderer.gameObject.SetActive(visible);
                if (!visible) continue;
                anyVisible = true;

                // Apple Watch style: bubbles near where you look are a little larger (never smaller than at rest).
                var local = new Vector3(b.Slot.x, b.Slot.y, 0f) + new Vector3(0f, Mathf.Sin(t * 1.3f + i) * 0.003f, 0f);
                float fisheye = Mathf.Lerp(1f, NearGaze, Mathf.Exp(-((Vector2)local - gaze).sqrMagnitude / (spacing * spacing * 1.3f)));
                var world = root.TransformPoint(local);
                float size = bubbleDiameter * fisheye * show;

                bool aimed = open && !library.IsOpen && (pointers[0].Target == i || pointers[1].Target == i);
                b.Hover = Mathf.MoveTowards(b.Hover, aimed ? 1f : 0f, dt * 7f);
                if (aimed) hovered = i;
                // A bubble the onboarding invites you to touch swells and glows a little, gently pulsing.
                float lift = b.World >= 0 && b.World == Beckoned && !library.IsOpen ? Mathf.Max(b.Hover, 0.35f + 0.25f * Mathf.Sin(t * 4f)) : b.Hover;

                float scale = size * (1f + lift * (Swell - 1f) + b.Pop * 0.45f);
                b.Renderer.transform.position = world;
                b.Renderer.transform.localScale = Vector3.one * Mathf.Max(scale, 1e-4f);
                Effects.Face(b.Renderer.transform, view);

                bool selected = b.World == ListenToggle ? bus != null && bus.IsListening
                    : b.World == LyricsToggle ? LyricsOption.On : b.World == manager.Current;
                block.Clear();
                block.SetColor(ColorId, BubbleColor(b, manager));
                block.SetFloat(IconId, b.World >= 0 ? manager.Worlds[b.World].MenuIcon : Icon(b.World));
                block.SetFloat(HoverId, lift);
                block.SetFloat(SelectedId, selected ? 1f : 0f);
                block.SetFloat(AlphaId, Mathf.Clamp01(show) * (1f - b.Pop * 0.85f) + b.Pop * 0.15f);
                if (bus != null) block.SetVector(EqId, new Vector4(bus.Bands[1], bus.Bands[2], bus.Bands[3], bus.Bands[4]));
                block.SetFloat(PausedId, bus != null && !bus.IsPlaying ? 1f : 0f);
                b.Renderer.SetPropertyBlock(block);
            }

            // Touch or pinch to choose. A touch presses the bubble under the fingertip as it reaches the menu; a pinch,
            // the bubble the hand was aiming at just before the pinch closed, so the small jump of the hand while
            // pinching doesn't change the choice.
            if (open && cooldown <= 0f && !library.IsOpen)
            {
                for (int s = 0; s < 2; s++)
                {
                    var p = pointers[s];
                    bool touched = p.Tapped.HasValue;
                    int chosen = p.Tapped is Vector2 tap ? BubbleAt(tap, TouchReach)
                        : p.Active && p.Pinching && p.AimedAge <= 0.35f ? p.Aimed : -1;
                    if (chosen < 0) continue;
                    var b = bubbles[chosen];
                    if (openTime - b.Delay < 0.32f) continue;
                    Press(b, manager, bus);
                    // The finger that pressed lights up (the pop has already sounded): the index for a touch, thumb and
                    // index for a pinch.
                    GestureFeedback.Light(s == 0, touched ? Fingers.Index : Fingers.Thumb | Fingers.Index);
                    p.Aimed = -1;
                    break;
                }
            }

            StepLibrary(bus, dt);
            DrawCaption(manager, bus, hovered);
            DrawPointers(view);
            if (root.gameObject.activeSelf != (anyVisible || open)) root.gameObject.SetActive(anyVisible || open);
            deck.Step(rig, bus, view, dt);
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
        public void PreviewLibrary(IReadOnlyList<SongAsset> songs, SongAsset playing, int view) =>
            library.Preview(songs, playing, view);

        /// <summary>Shows these words as the caption (null for the usual one), for the editor checks.</summary>
        public void PreviewCaption(string text)
        {
            previewCaption = text;
            captioned = null;
            captionedState = -1;
        }

        static float Icon(int bubble) => bubble switch
        {
            ListenToggle => 7f, NextSong => 14f, PreviousSong => 15f, LyricsToggle => 23f, Library => 24f, Help => 31f, _ => 6f
        };

        static bool IsSongStep(Bubble b) => b.World == PreviousSong || b.World == NextSong;

        Color BubbleColor(Bubble b, WorldManager manager) =>
            b.World >= 0 ? manager.Worlds[b.World].Color : b.World == ListenToggle ? ListenColor : IsSongStep(b) ? SongColor
            : b.World == LyricsToggle ? LyricsColor : b.World == Library ? AddColor : b.World == Help ? HelpColor : new Color(0.85f, 0.85f, 1f);

        string BubbleName(Bubble b, WorldManager manager, MusicBus bus) =>
            b.World >= 0 ? manager.Worlds[b.World].DisplayName
            : b.World == ListenToggle ? bus != null && bus.IsListening ? "Play the song" : "Listen to the room"
            : IsSongStep(b) ? SongStepName(bus, b.World == NextSong ? 1 : -1)
            : b.World == LyricsToggle ? LyricsOption.On ? "Turn lyrics off" : "Turn lyrics on"
            : b.World == Library ? "Choose a song on the cassette deck"
            : b.World == Help ? HelpName
            : bus == null ? "Music" : bus.IsPlaying ? $"Pause {bus.Title}" : $"Play {bus.Title}";

        static string SongStepName(MusicBus bus, int step)
        {
            var song = bus != null ? bus.SongAt(step) : null;
            string where = step > 0 ? "Next" : "Previous";
            return song == null ? $"{where} song" : $"{where}: {song.title}";
        }

        /// <summary>The first line under the menu: news of a song that is loading or couldn't play, else how to choose.</summary>
        static string HintText(MusicBus bus)
        {
            if (bus == null) return ChooseHint;
            var song = bus.Loading;
            if (song != null) return $"Loading {song.title}…";
            song = bus.Failed;
            if (song == null) return ChooseHint;
            return $"Couldn't play {song.title}";
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
                    p.Touching = p.TouchArmed = p.Aiming = false;
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
                    p.Aiming = false;
                }
                else
                {
                    var thumb = hand.TipJoints[0] != null ? hand.TipJoints[0].position : hand.Tips[0];
                    var pinchPoint = (thumb + tip) * 0.5f;
                    var shoulder = head + Vector3.down * 0.17f + side * (s == 0 ? -0.17f : 0.17f);
                    float pinch = p.Pinch = hand.Pinch;
                    // The ray aims through the point between thumb and index, but that point drops as the index comes
                    // down to the thumb, which pulled the cursor off the bubble just as it was pinched. So the point is
                    // held in the palm's frame: it follows the hand's shape only slowly and only while the hand is
                    // clearly open, so the ray moves with the hand, not with the closing fingers.
                    var palm = hand.Palm;
                    var offset = Quaternion.Inverse(palm.rotation) * (pinchPoint - palm.position);
                    if (!p.Aiming) p.AimOffset = offset;
                    else if (Vector3.Distance(thumb, tip) > OpenSpread)
                        p.AimOffset = Vector3.Lerp(p.AimOffset, offset, 1f - Mathf.Exp(-AimSettle * dt));
                    p.Aiming = true;
                    var aimPoint = palm.position + palm.rotation * p.AimOffset;
                    var ray = new Ray(shoulder, aimPoint - shoulder);
                    p.Active = plane.Raycast(ray, out float enter);
                    var hit = ray.GetPoint(enter);
                    // Smooth the cursor a little, and a little more while pinching; hand tracking jitters more than a
                    // controller.
                    float settle = pinch > 0f ? 12f : 25f;
                    p.Hit = pointed ? Vector3.Lerp(p.Hit, hit, 1f - Mathf.Exp(-settle * dt)) : hit;
                    p.Origin = pinchPoint;

                    if (pinch < pinchThresholds.y) p.Armed = true;
                    if (p.Armed && pinch >= pinchThresholds.x)
                    {
                        p.Pinching = true;
                        p.Armed = false;
                    }

                    // The nearest bubble within reach of the cursor is the target.
                    var localHit = (Vector2)root.InverseTransformPoint(p.Hit);
                    if (p.Active) p.Target = BubbleAt(localHit, bubbleDiameter * 0.62f);
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

        /// <summary>Centre to centre between neighbouring bubbles (m).</summary>
        float Spacing => bubbleDiameter + bubbleGap;

        float TouchReach => bubbleDiameter * 0.55f;

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
        /// The name of the bubble you point at, large above the honeycomb (or the current world's when you point at
        /// none), with a soft shadow so it reads over any room. Two small lines under the song row say how to choose.
        /// Words too wide for the menu are shortened, so nothing reaches past the bubbles. Each is only worked out again
        /// when what it says changes.
        /// </summary>
        void DrawCaption(WorldManager manager, MusicBus bus, int hovered)
        {
            float show = open ? Mathf.Clamp01((openTime - 0.1f) / 0.3f) * honeycomb : 0f;
            bool visible = show > 0.001f;
            if (caption.GameObject.activeSelf != visible)
            {
                caption.GameObject.SetActive(visible);
                hint.GameObject.SetActive(visible);
                gestures.GameObject.SetActive(visible);
            }
            if (!visible) return;

            Bubble subject = hovered >= 0 ? bubbles[hovered] : null;
            if (subject == null)
                foreach (var b in bubbles) if (b.World == manager.Current) subject = b;
            int state = (bus != null ? 1 : 0) | (bus != null && bus.IsPlaying ? 2 : 0) | (bus != null && bus.IsListening ? 4 : 0) |
                        (LyricsOption.On ? 8 : 0) | ((bus != null ? bus.Songs.Count : 0) << 4);
            var song = bus != null ? bus.CurrentSong : null;
            // The onboarding's words, while no bubble is pointed at.
            string prompt = hovered < 0 ? Prompt : null;
            if (subject != captioned || state != captionedState || song != captionedSong || !ReferenceEquals(prompt, captionedPrompt))
            {
                captioned = subject;
                captionedState = state;
                captionedSong = song;
                captionedPrompt = prompt;
                string text = previewCaption ?? prompt ?? (subject != null ? BubbleName(subject, manager, bus) : "");
                caption.Text = SongLibrary.Fit(text, CaptionSize, textWidth);
            }
            var color = prompt != null ? Color.Lerp(Color.white, HelpColor, 0.45f)
                : subject != null ? Color.Lerp(Color.white, BubbleColor(subject, manager), hovered >= 0 ? 0.3f : 0.15f) : Color.white;
            color.a = show * (hovered >= 0 || prompt != null ? 1f : 0.8f);
            caption.Color = color;

            var loading = bus != null ? bus.Loading : null;
            var failed = bus != null ? bus.Failed : null;
            if (!hinted || loading != hintLoading || failed != hintFailed)
            {
                hinted = true;
                hintLoading = loading;
                hintFailed = failed;
                hint.Text = SongLibrary.Fit(HintText(bus), HintSize, textWidth);
            }
            hint.Color = new Color(1f, 1f, 1f, show * 0.8f);
            gestures.Color = new Color(1f, 1f, 1f, show * 0.6f);
        }

        /// <summary>
        /// A bubble pressed, by touch or by pinch: a soft pop sounds the moment it is pressed, its chime rings on under
        /// it, and it does what it is for.
        /// </summary>
        void Press(Bubble b, WorldManager manager, MusicBus bus)
        {
            cooldown = 0.6f;
            b.Pop = 1f;
            float note = b.World >= 0 ? Synth.Pentatonic[6 + b.World % 5]
                : Synth.Pentatonic[b.World == ListenToggle ? 4 : IsSongStep(b) ? 3 : b.World == LyricsToggle || b.World == Library ? 2 : 5];
            var at = b.Renderer.transform.position;
            voices.Play(Synth.Pop(), at, 0.45f);
            voices.Play(Synth.Chime(note), at, 0.5f);
            if (b.World >= 0)
            {
                manager.Switch(b.World);
                closeAt = openTime + 0.35f;
            }
            else if (b.World == LyricsToggle) LyricsOption.On = !LyricsOption.On;
            else if (b.World == Library)
            {
                // The menu steps aside as the cassette deck rises.
                closeAt = openTime + 0.3f;
                if (lastView != null) deck.Open(lastView, bus);
            }
            else if (b.World == Help)
            {
                // The menu closes, and once it has, the lessons start again with how to open it.
                closeAt = openTime + 0.35f;
                var onboarding = Onboarding.Instance;
                if (onboarding != null) onboarding.Replay();
            }
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

        /// <summary>
        /// How many bubbles each of <paramref name="rows"/> rows holds: as even as can be, a longer and a shorter row
        /// taking turns when that works out (they nest neatly), else the longer rows first.
        /// </summary>
        static void SplitRows(int count, int rows, int[] lengths)
        {
            int shortLength = count / rows, longRows = count % rows, shortRows = rows - longRows;
            bool alternate = longRows > 0 && Mathf.Abs(longRows - shortRows) <= 1;
            bool longFirst = longRows >= shortRows;
            for (int r = 0; r < rows; r++)
            {
                bool isLong = alternate ? (r % 2 == 0) == longFirst : r < longRows;
                lengths[r] = shortLength + (isLong ? 1 : 0);
            }
        }

        /// <summary>
        /// Where the middle of each row goes, in bubble steps: half a step along from the row above, so the rows nest like
        /// a honeycomb, as near the middle as it can, and then all of them centred together. Returns their width in steps.
        /// </summary>
        static float NestRows(int[] lengths, int rows, float[] centres)
        {
            if (rows == 0) return 0f;
            centres[0] = 0f;
            for (int r = 1; r < rows; r++)
            {
                // Rows whose lengths differ by an odd number nest on whole steps; the others on half steps.
                float half = (lengths[r] - lengths[r - 1]) % 2 == 0 ? 0.5f : 0f;
                centres[r] = centres[r - 1] + Mathf.Round(-centres[r - 1] - half) + half;
            }
            float min = float.MaxValue, max = float.MinValue;
            for (int r = 0; r < rows; r++)
            {
                min = Mathf.Min(min, centres[r] - (lengths[r] - 1) * 0.5f);
                max = Mathf.Max(max, centres[r] + (lengths[r] - 1) * 0.5f);
            }
            for (int r = 0; r < rows; r++) centres[r] -= (min + max) * 0.5f;
            return max - min;
        }

        /// <summary>
        /// Lays the worlds out as one honeycomb in menu order, row by row, the "?" in the cell after the last world, with the
        /// song row under it, the caption over it and the two hint lines under the song row. It tries every number of rows
        /// and keeps the one that fits AimArea best at the menu's distance (the narrower and lower, the better, both counted
        /// against the area), so the honeycomb shrinks and stays centred as worlds are removed. The middle of the menu is
        /// halfway between the top of the caption and the foot of the second hint line. Fills <paramref name="slots"/>
        /// (worlds, the song row, then the "?") and places the text.
        /// </summary>
        void LayOut(int count, Vector2[] slots)
        {
            float step = Spacing, rowStep = step * 0.866f;
            float big = bubbleDiameter * NearGaze, swollen = big * Swell * 0.5f;
            float songWidth = (SongRow - 1) * step + big;
            // The caption's baseline above the top row, clear of a swollen bubble; the song row under the bottom row; the
            // first hint line's baseline under the song row, its capitals a little below a bubble near your gaze; then the
            // second hint line.
            float captionAbove = swollen + 0.006f + CaptionSize * Descent;
            float songBelow = big + 0.008f;
            float hintBelow = big * 0.5f + 0.006f + HintSize * Ascent;
            float top = captionAbove + CaptionSize * Ascent;
            float underSongs = hintBelow + HintSize * LineStep + HintSize * Descent;
            float aimWidth = 2f * viewDistance * Mathf.Tan(AimArea.x * 0.5f * Mathf.Deg2Rad);
            float aimHeight = 2f * viewDistance * Mathf.Tan(AimArea.y * 0.5f * Mathf.Deg2Rad);

            // The honeycomb's cells: the worlds, then the "?".
            int cells = count + 1;
            var lengths = new int[cells];
            var centres = new float[lengths.Length];
            int bestRows = 0;
            float bestCost = float.MaxValue, bestSum = float.MaxValue, bestWidth = songWidth;
            for (int rows = 1; rows <= cells; rows++)
            {
                SplitRows(cells, rows, lengths);
                float span = NestRows(lengths, rows, centres);
                float width = Mathf.Max(rows > 0 ? span * step + big : 0f, songWidth);
                float height = top + (rows > 0 ? (rows - 1) * rowStep + songBelow : 0f) + underSongs;
                float w = width / aimWidth, h = height / aimHeight;
                float cost = Mathf.Max(w, h), sum = w + h;
                if (cost < bestCost - 0.005f || (cost < bestCost + 0.005f && sum < bestSum))
                {
                    bestRows = rows;
                    bestCost = cost;
                    bestSum = sum;
                    bestWidth = width;
                }
            }

            rowLengths = new int[bestRows];
            if (bestRows > 0) SplitRows(cells, bestRows, rowLengths);
            NestRows(rowLengths, bestRows, centres);
            float songY = bestRows > 0 ? -(bestRows - 1) * rowStep - songBelow : 0f;
            // Centre it all on the menu's middle, from the top of the caption to the foot of the second hint line.
            float shift = -(top + songY - underSongs) * 0.5f;
            // The worlds, then the "?" in the last cell (its slot follows the song row's).
            int next = 0;
            for (int r = 0; r < bestRows; r++)
                for (int i = 0; i < rowLengths[r]; i++, next++)
                    slots[next < count ? next : count + SongRow] = new Vector2((centres[r] + i - (rowLengths[r] - 1) * 0.5f) * step, -r * rowStep + shift);
            // The song row: previous, play or pause, next, listen, lyrics, the cassette deck.
            for (int i = 0; i < SongRow; i++)
            {
                int world = i == 0 ? NowPlaying : ListenToggle - (i - 1);
                int column = world == PreviousSong ? 0 : world == NowPlaying ? 1 : world == NextSong ? 2 : world == ListenToggle ? 3 : world == LyricsToggle ? 4 : 5;
                slots[count + i] = new Vector2((column - (SongRow - 1) * 0.5f) * step, songY + shift);
            }

            textWidth = bestWidth;
            gestures.Text = SongLibrary.Fit(GestureHint, HintSize, textWidth);
            float hintY = songY - hintBelow + shift;
            Put(caption, captionAbove + shift);
            Put(hint, hintY);
            Put(gestures, hintY - HintSize * LineStep);
        }

        static void Put(TypeLabel label, float y)
        {
            label.Transform.localPosition = new Vector3(0f, y, 0f);
            label.Transform.localRotation = Quaternion.identity;
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
            voices ??= new AudioPool(transform, 4);

            // The words: baselines on their lines, centred.
            caption = new TypeLabel("Caption", root, CaptionSize, 0f);
            hint = new TypeLabel("Hint", root, HintSize, 0f);
            gestures = new TypeLabel("Hint 2", root, HintSize, 0f);
            captioned = null;
            captionedState = -1;
            hinted = false;
            foreach (var label in new[] { caption, hint, gestures }) label.GameObject.SetActive(false);

            int count = manager.Worlds.Count;
            bubbles = new Bubble[count + ExtraBubbles];
            var slots = new Vector2[bubbles.Length];
            LayOut(count, slots);
            for (int i = 0; i < bubbles.Length; i++)
            {
                int world = i < count ? i : i == count ? NowPlaying : i < count + SongRow ? ListenToggle - (i - count - 1) : Help;
                var slot = slots[i];
                string objectName = world >= 0 ? $"Bubble {world}" : world == ListenToggle ? "Bubble Listen"
                    : world == PreviousSong ? "Bubble Previous Song" : world == NextSong ? "Bubble Next Song"
                    : world == LyricsToggle ? "Bubble Lyrics" : world == Library ? "Bubble Library" : world == Help ? "Bubble Help" : "Bubble Now Playing";
                var r = Effects.MakeQuad(objectName, root, bubbleMaterial);
                float delay = 0.04f + slot.magnitude * 0.6f;
                bubbles[i] = new Bubble { Renderer = r, World = world, Slot = slot, Delay = delay };
                r.gameObject.SetActive(false);
            }

            // Editor previews build a menu without the scene's materials.
            var panel = panelMaterial != null ? panelMaterial : new Material(Shader.Find("MusicInYourHands/Panel"));
            library = new SongLibrary(root, panel, Vector2.zero, PageScale);
            library.Pressed += OnLibraryPressed;
            library.PickerOpened += Close;
            library.BackPressed += OnLibraryBack;
            if (deck == null)
            {
                deck = new CassetteDeck(transform, glowMaterial, turntable);
                deck.SongListRequested += () => ShowSongList(lastView);
                deck.PickerOpened += OnDeckPicker;
            }

            // Where a fingertip can touch: the bubbles, with room for their size, and the song page.
            Vector2 min = bubbles[0].Slot, max = min;
            foreach (var b in bubbles)
            {
                min = Vector2.Min(min, b.Slot);
                max = Vector2.Max(max, b.Slot);
            }
            var page = library.Area;
            touchArea = Rect.MinMaxRect(Mathf.Min(min.x - bubbleDiameter, page.xMin), Mathf.Min(min.y - bubbleDiameter, page.yMin),
                Mathf.Max(max.x + bubbleDiameter, page.xMax), Mathf.Max(max.y + bubbleDiameter, page.yMax));

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
