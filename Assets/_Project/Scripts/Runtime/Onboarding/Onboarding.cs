using System;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The first-launch onboarding, shown rather than told. The app opens straight into a world that answers your hands
    /// at once (Flower dance: your hand is the sun and the flowers lean after it) while a sung song fades in, and you
    /// get a few seconds just to enjoy it. Then, one at a time, a see-through ghost hand beside your own loops a gesture
    /// with a one-line caption above it, and stays until you do it:
    /// <list type="number">
    /// <item><b>Open the menu</b> (required): the left ghost hand turns its palm to your face and pinches.</item>
    /// <item><b>Touch a world</b>, while the menu is open: the right ghost hand points at the menu and pokes it. Choosing
    /// a world by touch or by pinching from afar both count.</item>
    /// <item><b>Thumb: tap to pause, swipe to skip</b>, later and only if you haven't found it yourself: when the first
    /// song ends or after about 40 s in the world you chose. It fades if unused and comes back once.</item>
    /// </list>
    /// Your own active fingertips glow while a lesson waits; doing it sets off a burst of sparks and a soft chime in the
    /// song's key. A Skip target low on the right skips the lessons. What you have learned is remembered, so later
    /// launches start as before. The menu's "?" bubble (Replay) shows the lessons again: the menu closes and the ghost
    /// shows how to open it; "--ez onboarding true" on the adb launch starts the whole first launch again.
    /// Every world also gets a ghost demo of its own gesture for its first ten seconds, beside its text hint (see
    /// <see cref="GhostMotions.WorldTable"/>), never while the menu is open. The onboarding is made at run time
    /// (nothing in the scene) and reads the menu only through WorldMenu.ToggleRequested, WorldMenu.IsOpen and
    /// WorldManager.WorldChanged, and the thumb gestures through MusicGestures.Used.
    /// </summary>
    [DefaultExecutionOrder(190)]
    public partial class Onboarding : MonoBehaviour
    {
        public enum Lesson { None, OpenMenu, TouchWorld, Thumb }

        // What the player has learned, kept between launches as bits (PlayerPrefs, under PrefsKey).
        const int LearnedMenu = 1, LearnedWorld = 2, LearnedThumb = 4, ThumbShownOnce = 8;
        const int AllLessons = LearnedMenu | LearnedWorld | LearnedThumb;

        public const string OpenMenuCaption = "Open the menu";
        public const string TouchWorldCaption = "Touch a world";
        public const string ThumbCaption = "Thumb: tap to pause, swipe to skip";
        public const string ControllersCaption = "Put your controllers down and use your hands";
        /// <summary>The same, short enough for the open menu's caption line.</summary>
        public const string ControllersPrompt = "Put your controllers down";

        [Header("Opening (first launch)")]
        [Tooltip("The world the app opens in on first launch, by its class name.")]
        [SerializeField] string openingWorld = "FlowerDance";
        [Tooltip("The song it opens with, by title.")]
        [SerializeField] string openingSong = "Turn It Up to Light";
        [Tooltip("Seconds the opening song takes to fade in.")]
        [SerializeField] float songFadeSeconds = 3f;
        [Tooltip("Seconds of simply enjoying the world before the first lesson.")]
        [SerializeField] float payoffSeconds = 9f;

        [Header("Thumb lesson")]
        [Tooltip("Seconds in the chosen world before the thumb lesson shows, unless the first song ends sooner.")]
        [SerializeField] float thumbAfterSeconds = 40f;
        [Tooltip("Seconds the thumb lesson stays when it isn't used, before it fades.")]
        [SerializeField] float thumbShowSeconds = 16f;
        [Tooltip("Seconds before an unused thumb lesson comes back, once.")]
        [SerializeField] float thumbReturnSeconds = 90f;

        [Header("World hints")]
        [Tooltip("Seconds a world's ghost demo shows after the world starts.")]
        [SerializeField] float hintSeconds = 10f;
        [Tooltip("Seconds after a world starts before its ghost demo fades in.")]
        [SerializeField] float hintDelay = 0.6f;

        public static Onboarding Instance { get; private set; }

        /// <summary>Where what the player has learned is kept (PlayerPrefs). The editor checks use a key of their own.</summary>
        public static string PrefsKey = "Onboarding.Learned";

        /// <summary>Set by the Meta feed: true while the player is holding controllers rather than using their hands.</summary>
        public static Func<bool> ControllersInUse;

        // What this session counts as learned (Replay forgets it), and what is kept between launches.
        int learned, remembered;
        bool lessonsOn = true, opening, openingPending, worldAsked;
        float clock, openingClock;
        Lesson lesson;
        float lessonClock, settledClock, thumbWaitUntil;
        bool thumbSecondTime, thumbLeft, songEnded;
        bool menuWasOpen, menuToggled;
        float menuClosedClock = 99f;
        WorldManager manager;
        HandRig lastRig;
        MusicFrame music;
        // The world hint: the motion of the world that started last, and how long ago.
        GhostMotion hintMotion;
        bool hintLeft, hintSidePending;
        float worldClock = 99f;
        // The song being watched for its end (on the headset).
        SongAsset watchedSong;
        float watchedTime;
        WorldManager subscribed;
        // The worlds the player has been in this session, so lesson 2 can point at one they haven't tried.
        bool[] tried = Array.Empty<bool>();

        // ---- What the editor checks read ----

        /// <summary>The lesson waiting to be done now (shown or not), or None.</summary>
        public Lesson CurrentLesson => lesson;

        /// <summary>True during the opening's first seconds, before the first lesson.</summary>
        public bool IsOpening => opening && lesson == Lesson.None && !Has(LearnedMenu);

        /// <summary>Whether a lesson counts as learned (or skipped) now: remembered between launches, forgotten by Replay.</summary>
        public bool HasLearned(Lesson which) => which switch
        {
            Lesson.OpenMenu => Has(LearnedMenu),
            Lesson.TouchWorld => Has(LearnedWorld),
            Lesson.Thumb => Has(LearnedThumb),
            _ => true
        };

        // ---- On the headset ----

        /// <summary>Makes the onboarding once the main scene has loaded (it isn't part of the scene).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            if (Instance != null || WorldManager.Instance == null) return;
            new GameObject("Onboarding").AddComponent<Onboarding>();
        }

        void Awake()
        {
            Instance = this;
            // The demo tour runs hands-free: no lessons, though each world still shows its ghost demo.
            bool tour = Intent.Flag("tour");
            worldAsked = Intent.Has("world");
            Begin(WorldManager.Instance, tour, Intent.Flag("onboarding"), now: false);
        }

        void OnDestroy()
        {
            EndInvite();
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            WorldMenu.ToggleRequested += NotifyMenuToggle;
            MusicGestures.Used += OnGesture;
            subscribed = WorldManager.Instance;
            if (subscribed != null) subscribed.WorldChanged += NotifyWorldChanged;
        }

        void OnDisable()
        {
            EndInvite();
            WorldMenu.ToggleRequested -= NotifyMenuToggle;
            MusicGestures.Used -= OnGesture;
            if (subscribed != null) subscribed.WorldChanged -= NotifyWorldChanged;
            subscribed = null;
        }

        void OnGesture(bool left, Microgesture gesture) => NotifyMicrogesture(left);

        void Update()
        {
            // After every Start: the world manager has started its first world and the music bus its first song.
            if (!openingPending) return;
            openingPending = false;
            StartOpening(WorldManager.Instance, MusicBus.Instance);
        }

        void LateUpdate()
        {
            var bus = MusicBus.Instance;
            float dt = Time.deltaTime;
            WatchSong(bus);
            var menu = WorldMenu.Instance;
            bool menuOpen = (menu != null && menu.IsOpen) || SongPicker.Waiting;
            var frame = bus == null ? default : new MusicFrame
            {
                Bands = bus.Bands, Kick = bus.Kick, Bus = bus, SongTime = bus.SongTime, Notes = bus.Notes,
                BeatAge = bus.TimeSinceBeat, BeatInterval = bus.BeatInterval
            };
            Step(HandRig.Instance, WorldManager.Instance, menu, menuOpen, frame, Camera.main, dt);
        }

        /// <summary>The first song looping round counts as it ending (songs loop), which brings on the thumb lesson.</summary>
        void WatchSong(MusicBus bus)
        {
            if (bus == null || bus.IsListening || !bus.IsPlaying) return;
            var song = bus.CurrentSong;
            float t = bus.SongTime;
            if (song != null && song == watchedSong && watchedTime > 10f && t + 2f < watchedTime) NotifySongEnded();
            watchedSong = song;
            watchedTime = t;
        }

        // ---- The flow ----

        /// <summary>
        /// Decides what this launch shows: on first launch (lessons 1 and 2 not yet learned) the opening world and song
        /// and then the lessons; otherwise only the world hints and, until it is learned, the thumb lesson.
        /// <paramref name="replay"/> forgets what was learned first. With <paramref name="now"/> the opening world and
        /// song start at once (editor checks); otherwise on the first frame, once the scene has started.
        /// </summary>
        public void Begin(WorldManager worlds, bool tour = false, bool replay = false, bool now = true)
        {
            Instance = this;
            manager = worlds;
            Load();
            lessonsOn = !tour;
            if (replay)
            {
                Debug.Log("[Onboarding] Replaying from the start, as asked.");
                Forget();
            }
            if (!lessonsOn || Has(LearnedMenu | LearnedWorld)) return;
            opening = true;
            openingClock = 0f;
            var bus = Application.isPlaying ? MusicBus.Instance : null;
            // Silent until the opening song starts, then it fades in.
            if (bus != null) bus.FadeIn(songFadeSeconds);
            if (now) StartOpening(worlds, bus);
            else openingPending = true;
        }

        /// <summary>Switches to the opening world (by type) and plays the opening song (by title).</summary>
        void StartOpening(WorldManager worlds, MusicBus bus)
        {
            manager = worlds;
            if (worlds != null && !worldAsked)
            {
                for (int i = 0; i < worlds.Worlds.Count; i++)
                {
                    var world = worlds.Worlds[i];
                    if (world == null || world.GetType().Name != openingWorld) continue;
                    worlds.Switch(i);
                    break;
                }
                StartHint(worlds.Current);
            }
            if (bus != null)
            {
                SongAsset song = null;
                foreach (var s in bus.Songs)
                    if (s != null && string.Equals(s.title, openingSong, StringComparison.OrdinalIgnoreCase)) song = s;
                if (song != null && song != bus.CurrentSong) bus.Play(song);
                else if (song == null) Debug.LogWarning($"[Onboarding] No song called \"{openingSong}\"; opening with the current one.");
            }
            Debug.Log($"[Onboarding] First launch: opening in {openingWorld} with \"{openingSong}\".");
        }

        /// <summary>
        /// Starts the lessons again from the first: the menu's "?" bubble. Pressed at the open menu, the first lesson waits
        /// until the menu has closed, then the ghost shows how to open it again. Only this session forgets the lessons, so a
        /// later launch still starts as before; with <paramref name="withOpening"/> the opening world and song come too.
        /// </summary>
        public void Replay(bool withOpening = false)
        {
            learned = 0;
            lessonsOn = true;
            SetLesson(Lesson.None);
            settledClock = 0f;
            thumbWaitUntil = 0f;
            songEnded = false;
            opening = withOpening;
            openingClock = 0f;
            if (withOpening)
            {
                var bus = Application.isPlaying ? MusicBus.Instance : null;
                if (bus != null) bus.FadeIn(songFadeSeconds);
                StartOpening(manager != null ? manager : WorldManager.Instance, bus);
            }
            Debug.Log("[Onboarding] Replay.");
        }

        /// <summary>Skips every lesson (the Skip target): they count as learned and don't come back.</summary>
        public void Skip()
        {
            bool showing = lesson != Lesson.None;
            Learn(AllLessons);
            SetLesson(Lesson.None);
            opening = false;
            if (showing) Skipped();
            Debug.Log("[Onboarding] Skipped the lessons.");
        }

        /// <summary>Meta's menu gesture asked the menu to open or close (WorldMenu.ToggleRequested).</summary>
        public void NotifyMenuToggle()
        {
            if (!menuWasOpen) menuToggled = true;
        }

        /// <summary>A world started (WorldManager.WorldChanged): its ghost demo starts, and if it was chosen at the menu, lesson 2 is done.</summary>
        public void NotifyWorldChanged(int index)
        {
            StartHint(index);
            if (!lessonsOn || !menuWasOpen || Has(LearnedWorld)) return;
            bool taught = lesson == Lesson.TouchWorld;
            Learn(LearnedMenu | LearnedWorld);
            if (taught) Celebrate(Lesson.TouchWorld, false);
            if (lesson == Lesson.TouchWorld || lesson == Lesson.OpenMenu) SetLesson(Lesson.None);
            settledClock = 0f;
        }

        /// <summary>A thumb tap or swipe was carried out (MusicGestures.Used).</summary>
        public void NotifyMicrogesture(bool left)
        {
            if (!lessonsOn || Has(LearnedThumb)) return;
            if (lesson == Lesson.Thumb)
            {
                Learn(LearnedThumb);
                Celebrate(Lesson.Thumb, left);
                SetLesson(Lesson.None);
            }
            // Found by themselves: nothing left to teach.
            else Learn(LearnedThumb);
        }

        /// <summary>The song came to its end (it loops round).</summary>
        public void NotifySongEnded() => songEnded = true;

        /// <summary>
        /// One frame: the lessons move on, then the ghost hands, caption, Skip target, fingertip lights and effects are
        /// drawn. Public so the editor checks can drive it; on the headset LateUpdate calls it.
        /// </summary>
        public void Step(HandRig rig, WorldManager worlds, WorldMenu menu, bool menuOpen, MusicFrame frame, Camera view, float dt)
        {
            manager = worlds;
            menuNow = menu;
            lastRig = rig;
            if (worlds != null && worlds.Current >= 0) MarkTried(worlds.Current);
            music = frame;
            Build();
            clock += dt;
            worldClock += dt;
            if (opening) openingClock += dt;
            bool opened = menuOpen && !menuWasOpen;
            menuClosedClock = menuOpen ? 0f : menuClosedClock + dt;
            if (opened && view != null) RememberMenu(view);
            if (lessonsOn) UpdateLessons(rig, menuOpen, opened || menuToggled, dt);
            menuToggled = false;
            menuWasOpen = menuOpen;
            Wanted(rig, menuOpen, out var want);
            Animate(rig, view, menuOpen, want, dt);
        }

        void UpdateLessons(HandRig rig, bool menuOpen, bool menuCame, float dt)
        {
            if (menuCame && !Has(LearnedMenu))
            {
                bool taught = lesson == Lesson.OpenMenu;
                Learn(LearnedMenu);
                if (taught) Celebrate(Lesson.OpenMenu, true);
                SetLesson(Has(LearnedWorld) ? Lesson.None : Lesson.TouchWorld);
            }
            switch (lesson)
            {
                case Lesson.None:
                    if (!Has(LearnedMenu))
                    {
                        if (!opening || openingClock >= payoffSeconds) SetLesson(Lesson.OpenMenu);
                    }
                    else if (!Has(LearnedWorld)) SetLesson(Lesson.TouchWorld);
                    else if (!Has(LearnedThumb))
                    {
                        settledClock += dt;
                        if (ThumbDue(menuOpen)) StartThumb(rig);
                    }
                    break;
                case Lesson.Thumb:
                    if (!menuOpen) lessonClock += dt;
                    if (lessonClock >= thumbShowSeconds) ThumbFaded();
                    break;
            }
        }

        /// <summary>
        /// The thumb lesson waits for a calm moment: menu closed for a while, the world's own demo over, and either the
        /// first song has ended or the player has spent thumbAfterSeconds in the world they chose.
        /// </summary>
        bool ThumbDue(bool menuOpen) =>
            !menuOpen && menuClosedClock > 2f && worldClock > hintSeconds + 1f && clock >= thumbWaitUntil &&
            settledClock >= 15f && (songEnded || settledClock >= thumbAfterSeconds);

        void StartThumb(HandRig rig)
        {
            bool right = rig != null && rig.Right.IsTracked, left = rig != null && rig.Left.IsTracked;
            thumbLeft = left && !right;
            thumbSecondTime = Has(ThumbShownOnce);
            songEnded = false;
            SetLesson(Lesson.Thumb);
        }

        /// <summary>The thumb lesson went unused: it comes back once more later, then leaves the player be.</summary>
        void ThumbFaded()
        {
            if (thumbSecondTime) Learn(LearnedThumb);
            else
            {
                Learn(ThumbShownOnce);
                thumbWaitUntil = clock + thumbReturnSeconds;
            }
            SetLesson(Lesson.None);
        }

        void SetLesson(Lesson next)
        {
            if (lesson == next) return;
            lesson = next;
            lessonClock = 0f;
            if (next != Lesson.None) Debug.Log($"[Onboarding] Lesson: {next}");
        }

        void MarkTried(int index)
        {
            if (index >= tried.Length) Array.Resize(ref tried, Mathf.Max(index + 1, manager != null ? manager.Worlds.Count : 0));
            tried[index] = true;
        }

        bool Tried(int index) => index >= 0 && index < tried.Length && tried[index];

        /// <summary>Starts a world's ghost demo.</summary>
        void StartHint(int index)
        {
            if (index >= 0) MarkTried(index);
            worldClock = 0f;
            hintMotion = GhostMotion.None;
            if (manager == null || index < 0 || index >= manager.Worlds.Count) return;
            hintMotion = GhostMotions.ForWorld(manager.Worlds[index]);
            hintSidePending = true;
        }

        bool HintShowing(bool menuOpen) =>
            hintMotion != GhostMotion.None && worldClock >= hintDelay && worldClock < hintSeconds && !menuOpen &&
            !(opening && openingClock < 1.5f);

        /// <summary>What should show this frame: a lesson's ghost, caption and Skip target, or else the world's demo.</summary>
        void Wanted(HandRig rig, bool menuOpen, out Display want)
        {
            want = default;
            switch (lesson)
            {
                case Lesson.OpenMenu:
                    // Just after the menu closes (the "?" bubble), a moment passes first, so the lesson follows the menu away.
                    if (!menuOpen && menuClosedClock > 0.3f) MenuLesson(ref want, Lesson.OpenMenu);
                    break;
                case Lesson.TouchWorld:
                    if (menuOpen)
                    {
                        // The menu's own caption line says it, so nothing of ours covers its bubbles.
                        want.Motion = GhostMotion.TouchMenu;
                        want.Lesson = Lesson.TouchWorld;
                        want.Prompt = TouchWorldCaption;
                        want.Lights = 1 << 1;
                    }
                    // The menu closed without a choice: show again how to open it.
                    else if (menuClosedClock > 0.8f) MenuLesson(ref want, Lesson.TouchWorld);
                    break;
                case Lesson.Thumb:
                    if (!menuOpen)
                    {
                        want.Motion = GhostMotion.ThumbTapSwipe;
                        want.Left = thumbLeft;
                        want.Lesson = Lesson.Thumb;
                        want.Caption = ThumbCaption;
                        want.CaptionSide = thumbLeft ? 1f : -1f;
                        want.Skip = true;
                        want.Lights = 1 | 1 << 1;
                    }
                    break;
            }
            bool controllers = ControllersInUse != null && ControllersInUse();
            if (controllers && lessonsOn && (want.Lesson != Lesson.None || IsOpening))
            {
                if (menuOpen) want.Prompt = want.Prompt != null ? ControllersPrompt : null;
                else want.Caption = ControllersCaption;
            }
            if (want.Motion != GhostMotion.None || !HintShowing(menuOpen)) return;
            if (hintSidePending)
            {
                // A one-handed demo shows beside the right hand, or the left when only it is in view.
                hintLeft = rig != null && rig.Left.IsTracked && !rig.Right.IsTracked;
                hintSidePending = false;
            }
            want.Motion = hintMotion;
            want.Left = hintLeft;
        }

        static void MenuLesson(ref Display want, Lesson lesson)
        {
            want.Motion = GhostMotion.MenuGesture;
            want.Left = true;
            want.Lesson = lesson;
            want.Caption = OpenMenuCaption;
            // Beside the left ghost hand, to its right.
            want.CaptionSide = 1f;
            want.Skip = true;
            want.Lights = 1 | 1 << 1;
        }

        // ---- Remembering ----

        bool Has(int bits) => (learned & bits) == bits;

        void Load() => learned = remembered = PlayerPrefs.GetInt(PrefsKey, 0);

        void Learn(int bits)
        {
            learned |= bits;
            if ((remembered | bits) == remembered) return;
            remembered |= bits;
            PlayerPrefs.SetInt(PrefsKey, remembered);
            PlayerPrefs.Save();
        }

        void Forget()
        {
            learned = remembered = 0;
            PlayerPrefs.SetInt(PrefsKey, 0);
            PlayerPrefs.Save();
        }

        /// <summary>The adb launch's extras (Android only): "--ez onboarding true", "--ez tour true", "--ei world N".</summary>
        static class Intent
        {
            public static bool Flag(string name)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var intent = Get())
                    return intent != null && intent.Call<bool>("getBooleanExtra", name, false);
#else
                return false;
#endif
            }

            public static bool Has(string name)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var intent = Get())
                    return intent != null && intent.Call<bool>("hasExtra", name);
#else
                return false;
#endif
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            static AndroidJavaObject Get()
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    return activity?.Call<AndroidJavaObject>("getIntent");
            }
#endif
        }
    }
}
