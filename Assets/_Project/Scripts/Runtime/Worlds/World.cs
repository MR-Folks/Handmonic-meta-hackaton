using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// One world the player can pick from the menu. A world is one class: subclass World, give it a name, a colour and
    /// a place in the menu, and override the hooks it needs; the base class does what every world shares. Each frame,
    /// Step runs:
    /// <list type="number">
    /// <item><b>OnBuild</b>, once, to create the world's objects. Worlds that show the sung words get them ready made
    /// (<see cref="SungWordCount"/>).</item>
    /// <item><b>OnPlace</b> when the world starts, and again when you move somewhere else (sit down, stand up, walk
    /// away, recenter) or the room scan changes, so the world can set itself up around you.</item>
    /// <item>The frame's events: <b>OnBeat</b> on each beat, <b>OnNote</b> when a melody note starts, <b>OnWord</b>
    /// when a sung word starts (lyric worlds also get the song title's words when a song has no lyrics), and
    /// <b>OnPinch</b> / <b>OnRelease</b> for each hand (no pinches while the menu is open: see <see cref="MenuOpen"/>).</item>
    /// <item><b>OnStep</b>, the world's own update for the frame. The sung words are drawn after it.</item>
    /// </list>
    /// <b>OnEnter</b> runs each time the world is switched to and <b>OnExit</b> when it is switched away: undo there
    /// anything the world changed outside itself. WorldManager switches worlds by turning their GameObjects on and off,
    /// and the editor previews call Build and Step directly with hand-made MusicFrames.
    /// To add a world, see "Adding a world" in the README: Update Worlds in Main Scene finds every World class, adds it
    /// to the scene in <see cref="MenuOrder"/> and fills its Material fields from Materials/ by name.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public abstract class World : MonoBehaviour
    {
        [Tooltip("The lettering of the sung words (Materials/Lettering).")]
        [SerializeField] protected Material letteringMaterial;
        [Tooltip("The soft glow of sparks and motes (Materials/Glow).")]
        [SerializeField] protected Material glowMaterial;

        // ---- Who the world is ----

        /// <summary>The world's name: its caption in the menu and the title of its hint.</summary>
        public abstract string DisplayName { get; }

        /// <summary>The world's colour: its menu bubble, caption and hint title.</summary>
        public abstract Color Color { get; }

        /// <summary>Where the world sits in the menu, lowest first within its group (sound worlds, lyric worlds).</summary>
        public virtual int MenuOrder => 1000;

        /// <summary>Which emblem the world's menu bubble shows (see the Bubble shader).</summary>
        public virtual int MenuIcon => 0;

        /// <summary>A short line saying what to do here, shown for a few seconds when the world starts.</summary>
        public virtual string Hint => "";

        /// <summary>True for the worlds built around the song's lyrics; the menu groups them apart.</summary>
        public virtual bool IsLyric => false;

        /// <summary>How the hands look in this world. Applied when the world becomes active.</summary>
        public virtual HandTheme Theme => HandTheme.Veins;

        // ---- Hooks, in the order they run ----

        /// <summary>Creates the world's objects, once.</summary>
        protected virtual void OnBuild() { }

        /// <summary>The world was switched to (after OnBuild): start afresh.</summary>
        protected virtual void OnEnter() { }

        /// <summary>The world was switched away: hide its things and undo anything it changed outside itself.</summary>
        protected virtual void OnExit() { }

        /// <summary>Sets the world up around the viewer: when it starts, and again once they have moved elsewhere.</summary>
        protected virtual void OnPlace(Camera view) { }

        /// <summary>A beat was detected this frame. <see cref="Beats"/> counts them since the world started.</summary>
        protected virtual void OnBeat(MusicFrame music) { }

        /// <summary>A note of the song's melody starts (songs with a .notes file; never while listening to the room).</summary>
        protected virtual void OnNote(NoteTrack.Note note) { }

        /// <summary>
        /// A sung word starts; <paramref name="newLine"/> when it begins a new line. <see cref="Sung"/> says where it is
        /// in its line. Lyric worlds also get the song title's words, one every four beats, when a song has no lyrics.
        /// </summary>
        protected virtual void OnWord(string word, bool newLine) { }

        /// <summary>A hand closed a pinch (index and thumb together). Not while the menu is open.</summary>
        protected virtual void OnPinch(HandRig.Hand hand, bool left) { }

        /// <summary>A hand let a pinch go, was lost while pinching, or the menu opened while it pinched.</summary>
        protected virtual void OnRelease(HandRig.Hand hand, bool left) { }

        /// <summary>The world's own update for this frame, after placing and the events.</summary>
        protected virtual void OnStep(HandRig rig, MusicFrame music, Camera view, float dt) { }

        // ---- Settings a world may override ----

        /// <summary>Seconds <see cref="FadeIn"/> takes to go from 0 to 1 when the world starts.</summary>
        protected virtual float FadeInSeconds => 1f / 1.5f;

        /// <summary>How many sung words the world shows at once (<see cref="SungWords"/>); 0 for none.</summary>
        protected virtual int SungWordCount => 0;

        /// <summary>The letter height (m) of the sung words at Size 1.</summary>
        protected virtual float SungWordHeight => 0.04f;

        /// <summary>How visible the sung words are as a whole this frame, 0-1.</summary>
        protected virtual float SungWordsAlpha => 1f;

        /// <summary>
        /// Whether OnWord also gets the song title's words when a song has no lyrics (and while listening to the
        /// room). Lyric worlds do; sound worlds only show real lyrics.
        /// </summary>
        protected virtual bool WordsWithoutLyrics => IsLyric;

        /// <summary>Pinch strength (0-1) that starts a pinch, and the strength it must drop below to let go.</summary>
        protected virtual Vector2 PinchThresholds => new Vector2(0.8f, 0.6f);

        /// <summary>
        /// Whether World shows the gesture feedback (<see cref="Feedback"/>) for each pinch it sends to OnPinch, and each
        /// let-go it sends to OnRelease, in a world that overrides them. A world whose pinch only sometimes does
        /// something turns this off and calls Feedback itself where it acts.
        /// </summary>
        protected virtual bool AutoPinchFeedback => true;

        /// <summary>Whether that automatic pinch feedback plays its tick and tock: off for a world with its own sound for a pinch.</summary>
        protected virtual bool PinchFeedbackSound => true;

        // ---- What a world can read ----

        public bool IsBuilt { get; private set; }

        /// <summary>0 when the world starts, rising to 1 over FadeInSeconds: fade the world in with it.</summary>
        protected float FadeIn { get; private set; }

        /// <summary>
        /// Seconds the world has run (frame time since it was built). It moves on after OnPlace and before the events,
        /// so a time an event hook stamps on something matches the clock OnStep reads in the same frame.
        /// </summary>
        protected float Clock { get; private set; }

        /// <summary>Beats since the world started; 1 during the first OnBeat.</summary>
        protected int Beats { get; private set; }

        /// <summary>Follows the sung words: the current one, its index and line, the words of its line.</summary>
        protected LyricCursor Sung { get; } = new LyricCursor();

        /// <summary>The sung words the world shows (SungWordCount of them), or null for worlds that show none.</summary>
        protected WordLayer SungWords { get; private set; }

        /// <summary>The hands, the music and the camera of the current frame, for the hooks.</summary>
        protected HandRig Rig { get; private set; }
        protected MusicFrame Music { get; private set; }
        protected Camera View { get; private set; }

        /// <summary>Whether a hand is pinching, by the same rule as OnPinch and OnRelease (so never while the menu is open).</summary>
        protected bool IsPinching(bool left) => pinching[left ? 0 : 1];

        /// <summary>
        /// True while the world menu, the cassette deck or the song page is open (the deck opens from the menu, and the
        /// page from the deck). The hands are choosing there, so a world leaves their pinches, touches, fists and pushes
        /// alone.
        /// OnPinch, OnRelease and IsPinching already do: no pinch starts while the menu is open, a pinch held as it
        /// opens gets its OnRelease, and one still held as it closes counts only once the hand has opened and pinched
        /// again. A world that reads the hands itself (hand.Pinch, Fist, a fingertip or palm on something) checks this
        /// too, and <see cref="PinchFree"/> for its own pinches. Following the hands just to look nice (lights on the
        /// fingertips, a swarm drifting to a palm) may go on.
        /// </summary>
        protected bool MenuOpen
        {
            get
            {
                var menu = WorldMenu.Instance;
                return menu != null && menu.IsOpen;
            }
        }

        /// <summary>
        /// Whether a hand's pinch is the world's: the menu is closed, and the hand has opened since it last pinched with
        /// the menu open. A world that reads hand.Pinch itself checks this beside its own strength, so a pinch made at
        /// the menu neither acts while the menu is open nor carries on into the world once it closes.
        /// </summary>
        protected bool PinchFree(bool left) => !MenuOpen && !waitOpen[left ? 0 : 1];

#if UNITY_EDITOR
        /// <summary>For the editor checks: how many times the world has acted for the hands (see Acted), and the last thing it did.</summary>
        public int HandActions { get; private set; }
        public string LastHandAction { get; private set; }
#endif

        /// <summary>
        /// Notes that the world just did something for the hands: plucked a string, popped a bubble, poured, struck.
        /// The editor checks count these to make sure nothing happens while the menu is open; outside the editor the
        /// call compiles away.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        protected void Acted(string what)
        {
#if UNITY_EDITOR
            HandActions++;
            LastHandAction = what;
#endif
        }

        // ---- Feedback for the hands ----

        /// <summary>
        /// The feedback for a gesture that did something here (see <see cref="GestureFeedback"/>): the fingers that made
        /// it light up, a few sparks burst from <paramref name="at"/> and a short sound plays there. Pass
        /// <paramref name="sound"/> false where the world already sounds for the same moment. Nothing while the menu is open.
        /// </summary>
        protected void Feedback(GestureKind kind, bool left, Vector3 at, float strength = 1f, bool sound = true, Fingers fingers = Fingers.None)
        {
            if (MenuOpen) return;
            GestureFeedback.Show(kind, left, at, strength, sound, fingers);
        }

        /// <summary>
        /// Feedback for something that goes on for a while (a fingertip in a word, a palm over a flower): call it every
        /// frame with whether it is happening, and it shows once as it starts, at most once every
        /// <paramref name="rest"/> seconds. <paramref name="key"/> (0-15) keeps each thing apart (say, one per hand).
        /// </summary>
        protected void FeedbackOnStart(int key, bool on, GestureKind kind, bool left, Vector3 at, float strength = 1f, bool sound = true,
            Fingers fingers = Fingers.None, float rest = 0.4f)
        {
            if (key < 0 || key >= feedbackOn.Length) return;
            bool was = feedbackOn[key];
            feedbackOn[key] = on;
            if (!on || was || Clock - feedbackAt[key] < rest || MenuOpen) return;
            feedbackAt[key] = Clock;
            Feedback(kind, left, at, strength, sound, fingers);
        }

        /// <summary>
        /// A hand's fingers glowing steadily while something builds under them (a fist held to switch), brighter as
        /// <paramref name="progress"/> (0-1) fills. Call it each frame while it builds; it fades once the calls stop.
        /// </summary>
        protected void FeedbackHold(bool left, float progress, Fingers fingers = Fingers.All)
        {
            if (MenuOpen || progress <= 0f) return;
            GestureFeedback.Hold(left, fingers, 0.15f + 0.75f * Mathf.Clamp01(progress));
        }

        // ---- Flashes ----

        /// <summary>The most flashes a world may make in any one second, Less flashing or not.</summary>
        public const int MostFlashesPerSecond = 3;

        /// <summary>
        /// Starts a flash: anything that suddenly brightens a lot of what you see, such as the room lighting up, a flare
        /// or a burst of light. Returns the strength to flash with: <paramref name="strength"/>, a quarter of it with
        /// Less flashing on (ComfortOptions), and 0 when this world has already flashed three times in the last second,
        /// so no world ever flashes faster than that. Call it once as each flash starts and draw the flash at what it
        /// returns (skip it at 0).
        /// </summary>
        protected float Flash(float strength = 1f)
        {
            // The oldest of the last three flashes this world made: a fourth must come more than a second after it.
            if (Clock - flashTimes[nextFlash] <= 1f)
            {
#if UNITY_EDITOR
                FlashesHeld++;
#endif
                return 0f;
            }
            flashTimes[nextFlash] = Clock;
            nextFlash = (nextFlash + 1) % MostFlashesPerSecond;
            float shown = strength * ComfortOptions.FlashScale;
#if UNITY_EDITOR
            FlashLog.Add(new Vector2(Clock, shown));
            if (FlashLog.Count > 512) FlashLog.RemoveAt(0);
#endif
            return shown;
        }

        /// <summary>
        /// Flash for a light that is already showing at <paramref name="showing"/>: only a flash brighter than that
        /// counts towards the three a second (and is returned); one no brighter can't be seen, so it returns 0 and
        /// leaves the allowance for the next flash.
        /// </summary>
        protected float Flash(float strength, float showing) =>
            strength * ComfortOptions.FlashScale > showing ? Flash(strength) : 0f;

#if UNITY_EDITOR
        /// <summary>For the editor checks: each flash the world made (its time on Clock and the strength it showed), and how many it held back.</summary>
        public readonly List<Vector2> FlashLog = new List<Vector2>();
        public int FlashesHeld { get; private set; }
#endif

        /// <summary>A fingertip as drawn (the joint the visible hand uses, when there is one).</summary>
        protected static Vector3 HandTip(HandRig.Hand hand, int finger) =>
            hand.TipJoints[finger] != null ? hand.TipJoints[finger].position : hand.Tips[finger];

        /// <summary>Where a hand's thumb and index meet.</summary>
        protected static Vector3 HandPinchPoint(HandRig.Hand hand) => (HandTip(hand, 0) + HandTip(hand, 1)) * 0.5f;

        /// <summary>The middle of a closed fist: between the palm and the curled fingertips.</summary>
        protected static Vector3 HandFistPoint(HandRig.Hand hand)
        {
            var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
            return (palm * 2f + HandTip(hand, 1) + HandTip(hand, 2) + HandTip(hand, 3) + HandTip(hand, 4)) / 6f;
        }

        // ---- The machinery ----

        bool beatPending;
        readonly List<NoteTrack.Note> upcoming = new List<NoteTrack.Note>();
        NoteTrack noteTrack;
        int nextNote;
        float noteTime;
        readonly bool[] pinching = new bool[2];
        // Set while the menu is open: the hand must open its pinch (or be lost) before its pinches are the world's again.
        readonly bool[] waitOpen = new bool[2];
        // Whether this world's own class overrides OnPinch and OnRelease (worked out once, in Build).
        bool hooksPinch, hooksRelease;
        // FeedbackOnStart: whether each thing was happening last frame, and when it last showed.
        readonly bool[] feedbackOn = new bool[16];
        // When this world's last three flashes started (on Clock), and which of them is the oldest.
        readonly float[] flashTimes = { -9f, -9f, -9f };
        int nextFlash;
        readonly float[] feedbackAt = { -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f };

        // Re-anchoring: worlds placed in the room move to you when you sit down, stand up, walk elsewhere or
        // recenter, instead of staying where you were when the world started.
        static int recenterCount;
        Vector3 anchorHead;
        int anchorRecenter;
        int anchorRoom;
        float driftTime;
        bool placed;

        /// <summary>Called when the headset recenters (Meta's recenter gesture or button).</summary>
        public static void NotifyRecentered() => recenterCount++;

        /// <summary>Creates the world's objects once (OnBuild). Safe to call again; Step calls it too.</summary>
        public void Build()
        {
            if (IsBuilt) return;
            IsBuilt = true;
            const System.Reflection.BindingFlags Hook = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            hooksPinch = GetType().GetMethod(nameof(OnPinch), Hook)?.DeclaringType != typeof(World);
            hooksRelease = GetType().GetMethod(nameof(OnRelease), Hook)?.DeclaringType != typeof(World);
            if (SungWordCount > 0)
            {
                var lettering = letteringMaterial != null ? letteringMaterial : FallbackLettering();
                if (lettering != null) SungWords = new WordLayer("Lyric", transform, lettering, SungWordCount, SungWordHeight, Sung);
                else Debug.LogError($"[World] {GetType().Name} has no lettering material for its sung words.");
            }
            OnBuild();
        }

        /// <summary>
        /// One frame of the world: placing, the frame's events, then OnStep and the sung words. The editor previews call
        /// it directly.
        /// </summary>
        public void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            // With Less flashing the beat's kick reaches the world at a quarter, so everything that brightens (and
            // swells) on it does so far less.
            music.Kick *= ComfortOptions.FlashScale;
            Rig = rig;
            Music = music;
            View = view;
            if (!placed || ShouldReanchor(view, dt))
            {
                OnPlace(view);
                MarkAnchored(view);
                placed = true;
            }
            FadeIn = Mathf.MoveTowards(FadeIn, 1f, FadeInSeconds > 0f ? dt / FadeInSeconds : 1f);
            Clock += dt;

            if (music.Beat)
            {
                Beats++;
                OnBeat(music);
            }
            DispatchNotes(music);
            DispatchWords(music);
            DispatchPinches(rig);

            SungWords?.Tick(dt);
            OnStep(rig, music, view, dt);
            SungWords?.Draw(SungWordsAlpha);
        }

        void OnEnable()
        {
            HandSkin.Theme = Theme;
            HandSkin.FocusStrength = 0f;
            HandSkin.Charge[0] = HandSkin.Charge[1] = 0f;
            GestureFeedback.UseMaterial(glowMaterial);
            for (int i = 0; i < feedbackOn.Length; i++) feedbackOn[i] = false;
            Build();
            placed = false;
            FadeIn = 0f;
            Beats = 0;
            Sung.Reset();
            noteTrack = null;
            pinching[0] = pinching[1] = false;
            waitOpen[0] = waitOpen[1] = false;
            beatPending = false;
            if (MusicBus.Instance != null) MusicBus.Instance.Beat += OnBusBeat;
            OnEnter();
        }

        void OnDisable()
        {
            if (MusicBus.Instance != null) MusicBus.Instance.Beat -= OnBusBeat;
            beatPending = false;
            if (!IsBuilt) return;
            SungWords?.Clear();
            OnExit();
            // Worlds may nudge the visible hands (fake resistance); put them back.
            var rig = HandRig.Instance;
            if (rig != null)
            {
                Effects.OffsetHand(rig.Left, Vector3.zero);
                Effects.OffsetHand(rig.Right, Vector3.zero);
            }
        }

        void OnBusBeat() => beatPending = true;

        void LateUpdate()
        {
            var rig = HandRig.Instance;
            if (rig == null) return;
            var bus = MusicBus.Instance;
            if (bus != null) bus.UpcomingNotes(3f, upcoming);
            else upcoming.Clear();
            var music = new MusicFrame
            {
                Bands = bus != null ? bus.Bands : null,
                Kick = bus != null ? bus.Kick : 0f,
                Beat = beatPending,
                Waveform = bus != null ? bus.Waveform : null,
                Bus = bus,
                SongTime = bus != null ? bus.SongTime : 0f,
                Upcoming = upcoming,
                Lyrics = bus != null ? bus.Lyrics : null,
                Notes = bus != null ? bus.Notes : null,
                BeatAge = bus != null ? bus.TimeSinceBeat : 99f,
                BeatInterval = bus != null ? bus.BeatInterval : 0f,
                Lyric = bus != null ? bus.Lyric : new LyricsTrack.State(-1, -1)
            };
            beatPending = false;
            Step(rig, music, Camera.main, Time.deltaTime);
        }

        /// <summary>OnNote for each melody note that started since the last frame.</summary>
        void DispatchNotes(MusicFrame music)
        {
            var track = music.Notes;
            if (track == null || track.IsEmpty)
            {
                noteTrack = null;
                return;
            }
            var notes = track.Notes;
            float now = music.SongTime;
            // A new song, or the song jumped (it looped, restarted or skipped ahead): carry on from where it is now.
            if (track != noteTrack || now < noteTime - 0.05f || now > noteTime + 1f)
            {
                noteTrack = track;
                nextNote = 0;
                while (nextNote < notes.Count && notes[nextNote].Time < now) nextNote++;
            }
            else
                while (nextNote < notes.Count && notes[nextNote].Time <= now) OnNote(notes[nextNote++]);
            noteTime = now;
        }

        /// <summary>OnWord when a new sung word starts.</summary>
        void DispatchWords(MusicFrame music)
        {
            // Without real lyrics, only lyric worlds carry on with the title's words.
            if (!WordsWithoutLyrics && string.IsNullOrEmpty(music.Word))
            {
                Sung.Reset();
                return;
            }
            if (!Sung.Advance(music, out bool newLine)) return;
            // A world whose sung words couldn't be made (no lettering) has nowhere to put them.
            if (SungWordCount > 0 && SungWords == null) return;
            OnWord(Sung.Word, newLine);
        }

        /// <summary>
        /// OnPinch and OnRelease, with hysteresis so a pinch held near the threshold doesn't flicker, and none for a hand
        /// in Meta's menu gesture. While the world menu is open the pinches are the menu's: a pinch held as it opens is
        /// let go (OnRelease), and one still held as it closes counts only once the hand has opened and pinched again.
        /// In a world that overrides OnPinch (or OnRelease) each one also gets its gesture feedback, unless the world
        /// shows its own (AutoPinchFeedback).
        /// </summary>
        void DispatchPinches(HandRig rig)
        {
            bool menu = MenuOpen;
            for (int s = 0; s < 2; s++)
            {
                var hand = rig != null ? rig.Get(s == 0) : null;
                bool was = pinching[s];
                bool tracked = hand != null && hand.IsTracked;
                // Waiting lasts until the hand opens or is lost, also through Meta's menu gesture, which closes the menu
                // with a pinch of its own.
                if (menu) waitOpen[s] = true;
                else if (!tracked || hand.Pinch < PinchThresholds.y) waitOpen[s] = false;
                bool now = tracked && !hand.InSystemGesture && !waitOpen[s]
                    && hand.Pinch >= (was ? PinchThresholds.y : PinchThresholds.x);
                pinching[s] = now;
                if (now && !was)
                {
                    OnPinch(hand, s == 0);
                    if (hooksPinch && AutoPinchFeedback) Feedback(GestureKind.Pinch, s == 0, HandPinchPoint(hand), 1f, PinchFeedbackSound);
                }
                else if (!now && was)
                {
                    OnRelease(hand, s == 0);
                    // A pinch let go by the hand itself; not one lost from view, nor one the opening menu took over.
                    if ((hooksRelease || hooksPinch) && AutoPinchFeedback && tracked && !menu)
                        Feedback(GestureKind.Release, s == 0, HandPinchPoint(hand), 0.7f, PinchFeedbackSound);
                }
            }
        }

        /// <summary>Remember where the head was when the world placed its things in the room.</summary>
        void MarkAnchored(Camera view)
        {
            anchorHead = view.transform.position;
            anchorRecenter = recenterCount;
            anchorRoom = Room.Version;
            driftTime = 0f;
        }

        /// <summary>
        /// True when the world should place itself again: after a recenter or a new room scan, or once the head has
        /// stayed more than 25 cm higher or lower (sitting down, standing up) or 90 cm away for a moment and a half.
        /// </summary>
        bool ShouldReanchor(Camera view, float dt)
        {
            // Also when the room scan loads or changes, so things move onto the real table, walls and floor.
            if (anchorRecenter != recenterCount || anchorRoom != Room.Version) return true;
            var head = view.transform.position;
            var flat = new Vector2(head.x - anchorHead.x, head.z - anchorHead.z);
            bool moved = Mathf.Abs(head.y - anchorHead.y) > 0.25f || flat.magnitude > 0.9f;
            driftTime = moved ? driftTime + dt : 0f;
            return driftTime > 1.5f;
        }

        static Material FallbackLettering()
        {
            var shader = Shader.Find("MusicInYourHands/Lettering");
            return shader != null ? new Material(shader) : null;
        }

        /// <summary>Seconds since start, also while the editor is not playing (for previews).</summary>
        protected static float Now => Application.isPlaying ? Time.time : (float)UnityEditorTime();

        static double UnityEditorTime()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.time;
#endif
        }

        /// <summary>Advances the world's particle systems while the editor is not playing (previews).</summary>
        public void SimulateParticles(float seconds)
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) ps.Simulate(seconds, true, false);
        }

        /// <summary>Destroys immediately in the editor (previews) and normally while playing.</summary>
        protected static void Discard(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
