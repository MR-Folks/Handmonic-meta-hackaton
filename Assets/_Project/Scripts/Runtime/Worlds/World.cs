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
    /// <b>OnPinch</b> / <b>OnRelease</b> for each hand.</item>
    /// <item><b>OnStep</b>, the world's own update for the frame. The sung words are drawn after it.</item>
    /// </list>
    /// <b>OnEnter</b> runs each time the world is turned on and <b>OnExit</b> when it is turned off: undo there
    /// anything the world changed outside itself. WorldManager turns worlds on and off (their GameObjects with them),
    /// and the editor previews call Build and Step directly with hand-made MusicFrames.
    /// Several worlds can be on at once when they use different parts of the room and your body (<see cref="Spaces"/>).
    /// Every hook reaches every world that is on, except that only one of them gets the sung words (<see cref="OwnsWords"/>)
    /// and only one dresses the hands (<see cref="OwnsHands"/>): World undoes what the others do to the hands' look.
    /// To add a world, see "Adding a world" in the README: Update Worlds in Main Scene finds every World class, adds it
    /// to the scene in <see cref="MenuOrder"/> and fills its Material fields from Materials/ by name.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public abstract class World : MonoBehaviour
    {
        /// <summary>
        /// The parts of the room and of your body a world uses. Worlds that share none of them can be on together;
        /// choosing a world that shares one with a world that is on turns that one off.
        /// </summary>
        [System.Flags]
        public enum Space
        {
            None = 0,
            /// <summary>The floor and the tops of furniture: things that stand, grow, land or lie there.</summary>
            Ground = 1 << 0,
            /// <summary>The walls.</summary>
            Walls = 1 << 1,
            /// <summary>The ceiling, or a sky drawn over it.</summary>
            Ceiling = 1 << 2,
            /// <summary>The room's air all around you, out of reach, and things that circle you.</summary>
            AroundYou = 1 << 3,
            /// <summary>Within arm's reach in front of you: things that stand there for you to touch.</summary>
            InFront = 1 << 4,
            /// <summary>On and between your hands: what they carry or hold, and how they look beyond their colours.</summary>
            Hands = 1 << 5,
            /// <summary>What a pinch does (drawing, spraying, pouring, holding). Two worlds never share a pinch.</summary>
            Pinch = 1 << 6,
            /// <summary>What clenching your fists does (clearing, switching, pulling).</summary>
            Fists = 1 << 7,
            /// <summary>Everything: the world is on its own. A world that doesn't say which spaces it uses gets this.</summary>
            All = ~0
        }

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

        /// <summary>How the hands look in this world. Applied when the world starts to dress the hands (OwnsHands).</summary>
        public virtual HandTheme Theme => HandTheme.Veins;

        /// <summary>
        /// The parts of the room and of your body the world uses, so the menu knows which worlds can be on with it. A
        /// world that doesn't say is on its own: choosing it turns every other world off, and choosing another turns it
        /// off.
        /// </summary>
        public virtual Space Spaces => Space.All;

        /// <summary>
        /// Whether this world and <paramref name="other"/> can be on together: neither is on its own, they use none of
        /// the same spaces, and they aren't both lyric worlds (each is made of the sung words, which only one can show).
        /// </summary>
        public bool CombinesWith(World other) =>
            other != null && other != this && Spaces != Space.All && other.Spaces != Space.All
            && (Spaces & other.Spaces) == 0 && !(IsLyric && other.IsLyric);

        /// <summary>Whether the world shows the sung words: a lyric world, or one with a SungWordCount.</summary>
        public bool ShowsWords => IsLyric || SungWordCount > 0;

        // ---- Hooks, in the order they run ----

        /// <summary>Creates the world's objects, once.</summary>
        protected virtual void OnBuild() { }

        /// <summary>The world was turned on (after OnBuild), alone or beside others: start afresh.</summary>
        protected virtual void OnEnter() { }

        /// <summary>The world was turned off: hide its things and undo anything it changed outside itself.</summary>
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

        /// <summary>A hand closed a pinch (index and thumb together).</summary>
        protected virtual void OnPinch(HandRig.Hand hand, bool left) { }

        /// <summary>A hand let a pinch go, or was lost while pinching.</summary>
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

        // ---- What a world can read ----

        public bool IsBuilt { get; private set; }

        /// <summary>
        /// Whether the world gets the sung words (OnWord). A world on its own always does; with several on, WorldManager
        /// gives them to one: the lyric world, else the one chosen last that shows words.
        /// </summary>
        public bool OwnsWords { get; private set; } = true;

        /// <summary>
        /// Whether the world dresses the hands: its Theme, HandSkin's focus point and charge, and nudging the visible
        /// hands. A world on its own always does; with several on, WorldManager picks one (the one that uses the Hands
        /// space, else the one chosen last), and World undoes what the others' hooks do to the hands.
        /// </summary>
        public bool OwnsHands { get; private set; } = true;

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

        /// <summary>Whether a hand is pinching, by the same rule as OnPinch and OnRelease.</summary>
        protected bool IsPinching(bool left) => pinching[left ? 0 : 1];

        /// <summary>Whether the world is on: between Enter (OnEnter) and Exit (OnExit).</summary>
        public bool IsOn => entered;

        /// <summary>How many of its SungWords the world shows right now (for the editor checks).</summary>
        public int WordsShowing
        {
            get
            {
                if (SungWords == null) return 0;
                int count = 0;
                var words = SungWords.Words;
                for (int i = 0; i < words.Count; i++)
                    if (words[i].Active) count++;
                return count;
            }
        }

        // ---- The machinery ----

        bool beatPending, entered;
        readonly List<NoteTrack.Note> upcoming = new List<NoteTrack.Note>();
        NoteTrack noteTrack;
        int nextNote;
        float noteTime;
        readonly bool[] pinching = new bool[2];

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
        /// it directly. When another world dresses the hands, what this one's hooks do to them is undone afterwards.
        /// </summary>
        public void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            if (OwnsHands)
            {
                Run(rig, music, view, dt);
                return;
            }
            var look = HandSkin.Look.Save(rig);
            try
            {
                Run(rig, music, view, dt);
            }
            finally
            {
                look.Restore(rig);
            }
        }

        void Run(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
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

        void OnEnable() => Enter();

        void OnDisable() => Exit();

        /// <summary>
        /// Starts the world afresh (OnEnter), building it first if need be; a world that dresses the hands puts its Theme
        /// on them. WorldManager calls it when it turns the world on, as turning the GameObject on does while playing
        /// (the editor checks have no OnEnable). Does nothing if the world is already on.
        /// </summary>
        public void Enter()
        {
            if (entered) return;
            entered = true;
            var rig = HandRig.Instance;
            if (OwnsHands) DressHands(rig);
            var look = HandSkin.Look.Save(rig);
            try
            {
                Build();
                placed = false;
                FadeIn = 0f;
                Beats = 0;
                Sung.Reset();
                noteTrack = null;
                pinching[0] = pinching[1] = false;
                beatPending = false;
                if (MusicBus.Instance != null) MusicBus.Instance.Beat += OnBusBeat;
                OnEnter();
            }
            finally
            {
                if (!OwnsHands) look.Restore(rig);
            }
        }

        /// <summary>
        /// Stops the world (OnExit) and clears its sung words; a world that dressed the hands puts the visible hands back
        /// where they belong. WorldManager calls it when it turns the world off. Does nothing if the world is off.
        /// </summary>
        public void Exit()
        {
            if (!entered) return;
            entered = false;
            if (MusicBus.Instance != null) MusicBus.Instance.Beat -= OnBusBeat;
            beatPending = false;
            if (!IsBuilt) return;
            SungWords?.Clear();
            var rig = HandRig.Instance;
            var look = HandSkin.Look.Save(rig);
            try
            {
                OnExit();
            }
            finally
            {
                if (!OwnsHands) look.Restore(rig);
                // Worlds may nudge the visible hands (fake resistance); put them back.
                else if (rig != null)
                {
                    Effects.OffsetHand(rig.Left, Vector3.zero);
                    Effects.OffsetHand(rig.Right, Vector3.zero);
                }
            }
        }

        /// <summary>
        /// WorldManager says whether this world gets the sung words and dresses the hands while others are on too. A
        /// world that hands the words over clears its own (the next owner picks up at the word being sung); one that
        /// takes the hands over while it runs puts its Theme on them.
        /// </summary>
        internal void SetOwners(bool words, bool hands)
        {
            if (OwnsWords && !words) SungWords?.Clear();
            OwnsWords = words;
            bool dress = hands && !OwnsHands && entered;
            OwnsHands = hands;
            if (dress) DressHands(HandRig.Instance);
        }

        /// <summary>The world's look on the hands, with nothing nudged, focused or charged yet.</summary>
        void DressHands(HandRig rig)
        {
            HandSkin.Dress(Theme);
            if (rig == null) return;
            Effects.OffsetHand(rig.Left, Vector3.zero);
            Effects.OffsetHand(rig.Right, Vector3.zero);
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

        /// <summary>OnWord when a new sung word starts, for the world that shows the sung words.</summary>
        void DispatchWords(MusicFrame music)
        {
            // With several worlds on, another one shows them; this one's cursor waits, so it starts at the word being
            // sung if the words come to it.
            if (!OwnsWords) return;
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

        /// <summary>OnPinch and OnRelease, with hysteresis so a pinch held near the threshold doesn't flicker.</summary>
        void DispatchPinches(HandRig rig)
        {
            for (int s = 0; s < 2; s++)
            {
                var hand = rig != null ? rig.Get(s == 0) : null;
                bool was = pinching[s];
                bool now = hand != null && hand.IsTracked && !hand.InSystemGesture
                    && hand.Pinch >= (was ? PinchThresholds.y : PinchThresholds.x);
                pinching[s] = now;
                if (now && !was) OnPinch(hand, s == 0);
                else if (!now && was) OnRelease(hand, s == 0);
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
