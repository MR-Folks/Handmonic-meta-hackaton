using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>Meta's thumb microgestures, as a hand feed reports them (MicrogestureFeed on Quest).</summary>
    public enum Microgesture { ThumbTap, SwipeLeft, SwipeRight, SwipeForward, SwipeBackward }

    /// <summary>
    /// The music as the hand controls see it: what they pause, play and step through. On the headset it is the
    /// MusicBus; the editor checks use a stand-in.
    /// </summary>
    public interface IMusicRemote
    {
        bool IsListening { get; }
        bool IsPlaying { get; }
        SongAsset Current { get; }
        /// <summary>The song whose audio is on its way, or null.</summary>
        SongAsset Loading { get; }
        /// <summary>A song whose audio just couldn't be loaded, or null.</summary>
        SongAsset Failed { get; }
        /// <summary>What the music is doing now, so the answering note fits the song.</summary>
        MusicFrame Frame { get; }
        SongAsset SongAt(int step);
        void TogglePause();
        void StepSong(int step);
    }

    /// <summary>
    /// Music controls in every world, on Meta's most reliable hand gestures: tap your thumb on the side of your index
    /// finger to pause or play, or swipe it along the finger to the left for the previous song and to the right for the
    /// next one, with either hand. A short caption low in your view says what happened ("Paused", "Next: Neon Tide"),
    /// beside a small bubble with the emblem of the menu's matching button, and a soft note in the song's key sounds from
    /// your thumb. A song that still has to load keeps its caption until it plays, or says it couldn't. While you listen to
    /// the room there is no song to pause or change, so the caption says "Listening to the room" and nothing changes.
    /// To avoid accidents a hand is ignored while it isn't tracked, makes Meta's menu gesture, pinches or makes a fist,
    /// and for a moment after; every gesture is ignored while the world menu (with its song page and file picker) is
    /// open; and two commands are at least half a second apart.
    /// </summary>
    [DefaultExecutionOrder(180)]
    public class MusicGestures : MonoBehaviour
    {
        [SerializeField] Material bubbleMaterial;

        [Header("Gates")]
        [Tooltip("Seconds between two commands, so one movement never counts twice.")]
        [SerializeField] float cooldown = 0.5f;
        [Tooltip("Seconds a hand stays ignored after it lets go of a pinch, opens a fist, ends Meta's menu gesture or is " +
                 "tracked again, and every hand after the world menu closes.")]
        [SerializeField] float settle = 0.3f;
        [Tooltip("Pinch strength (0-1) that counts as pinching, and the strength it must drop below to let go: the same " +
                 "as the worlds' pinches, so a pinch in a world never also pauses the song.")]
        [SerializeField] Vector2 pinchThresholds = new Vector2(0.8f, 0.6f);
        [Tooltip("How closed (0-1) a hand counts as a fist. Microgestures are made with a loosely curled hand, so keep it high.")]
        [SerializeField, Range(0.5f, 1f)] float fistThreshold = 0.8f;

        [Header("Feedback")]
        [Tooltip("How long a caption shows (s).")]
        [SerializeField] float showSeconds = 1.5f;
        [Tooltip("How far ahead of your eyes the caption shows (m).")]
        [SerializeField] float distance = 0.7f;
        [Tooltip("How far below eye height the caption sits (m): about 20 degrees down, under the world hints.")]
        [SerializeField] float drop = 0.25f;
        [Tooltip("Letter height (m).")]
        [SerializeField] float letterHeight = 0.026f;
        [Tooltip("Size of the emblem bubble beside the text (m).")]
        [SerializeField] float iconSize = 0.048f;
        [Tooltip("The widest a caption may be (m); longer song titles are shortened.")]
        [SerializeField] float maxWidth = 0.4f;
        [SerializeField, Range(0f, 1f)] float volume = 0.4f;

        public static MusicGestures Instance { get; private set; }

        /// <summary>
        /// Logs each reported gesture with what became of it (acted, or which gate held it) and the hand's pinch and fist
        /// then, for tuning on the headset. The Meta hand feed turns it on; editor checks leave it off.
        /// </summary>
        public static bool LogGestures;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IconId = Shader.PropertyToID("_Icon");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int EqId = Shader.PropertyToID("_Eq");
        static readonly int PausedId = Shader.PropertyToID("_Paused");
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        static readonly int SelectedId = Shader.PropertyToID("_Selected");
        // The emblems of the menu's song row: now playing (a pause sign while paused), listen, next and previous.
        const float PlayIcon = 6f, ListenIcon = 7f, NextIcon = 14f, PreviousIcon = 15f;
        // The menu's colours for its song and listen buttons, and an amber for a song that couldn't play.
        static readonly Color SongColor = new Color(0.72f, 0.7f, 1f);
        static readonly Color ListenColor = new Color(0.98f, 0.62f, 0.5f);
        static readonly Color FailColor = new Color(1f, 0.8f, 0.45f);
        const float Gap = 0.012f, FadeIn = 0.15f, FadeOut = 0.4f;
        // A song still loading holds its caption for at most this long (s).
        const float LongestWait = 15f;

        /// <summary>The MusicBus, as the hand controls see it.</summary>
        sealed class BusRemote : IMusicRemote
        {
            public MusicBus Bus;
            public bool IsListening => Bus.IsListening;
            public bool IsPlaying => Bus.IsPlaying;
            public SongAsset Current => Bus.CurrentSong;
            public SongAsset Loading => Bus.Loading;
            public SongAsset Failed => Bus.Failed;
            public MusicFrame Frame => new MusicFrame
            {
                Bands = Bus.Bands, Kick = Bus.Kick, Bus = Bus, SongTime = Bus.SongTime, Notes = Bus.Notes,
                BeatAge = Bus.TimeSinceBeat, BeatInterval = Bus.BeatInterval
            };
            public SongAsset SongAt(int step) => Bus.SongAt(step);
            public void TogglePause() => Bus.TogglePause();
            public void StepSong(int step) => Bus.StepSong(step);
        }

        readonly BusRemote bus = new BusRemote();
        // The gesture each hand (left, right) finished this frame, or -1.
        readonly int[] pending = { -1, -1 };
        readonly bool[] pinching = new bool[2];
        // Each hand is ignored until this time (on clock).
        readonly float[] quietUntil = new float[2];
        readonly string[] quietReason = { "", "" };
        float clock, lastCommand = -99f, menuQuietUntil;

        Transform root, content;
        TypeLabel label;
        Renderer icon;
        MaterialPropertyBlock block;
        AudioPool voices;
        Color tint;
        float age = 99f, iconKind;
        int slideDirection;
        bool iconPaused, placed;
        // The song a swipe stepped to while its audio is on its way, and which way the swipe went.
        SongAsset awaited;
        int awaitedStep;
        float awaitedSince;

        /// <summary>The caption showing now, or an empty string. For the editor checks.</summary>
        public string Caption => root != null && root.gameObject.activeSelf ? label.Text : "";

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            pending[0] = pending[1] = -1;
        }

        /// <summary>Called by the hand feed when a hand finishes a microgesture; it is acted on in this frame's LateUpdate.</summary>
        public void Recognize(bool left, Microgesture gesture) => pending[left ? 0 : 1] = (int)gesture;

        void LateUpdate()
        {
            var menu = WorldMenu.Instance;
            // The song page lives inside the menu; the file picker it opens closes the menu but is still part of it.
            bool menuOpen = (menu != null && menu.IsOpen) || SongPicker.Waiting;
            bus.Bus = MusicBus.Instance;
            Step(HandRig.Instance, bus.Bus != null ? bus : null, menuOpen, Camera.main, Time.deltaTime);
        }

        /// <summary>
        /// One frame: updates each hand's gates, carries out the gestures reported since the last frame that pass them,
        /// then draws the caption. Public so the editor checks can drive it.
        /// </summary>
        public void Step(HandRig rig, IMusicRemote music, bool menuOpen, Camera view, float dt)
        {
            Build();
            clock += dt;
            if (menuOpen) menuQuietUntil = clock + settle;
            for (int s = 0; s < 2; s++)
            {
                var hand = rig != null ? rig.Get(s == 0) : null;
                bool tracked = hand != null && hand.IsTracked;
                pinching[s] = tracked && hand.Pinch >= (pinching[s] ? pinchThresholds.y : pinchThresholds.x);
                string quiet = !tracked ? "hand not tracked" : hand.InSystemGesture ? "Meta's menu gesture"
                    : pinching[s] ? "pinching" : hand.Fist >= fistThreshold ? "fist" : null;
                if (quiet != null)
                {
                    quietUntil[s] = clock + settle;
                    quietReason[s] = quiet;
                }
            }
            for (int s = 0; s < 2; s++)
            {
                int gesture = pending[s];
                pending[s] = -1;
                if (gesture < 0) continue;
                string held = music == null ? "no music" : clock < quietUntil[s] ? quietReason[s] + " (or just before)"
                    : clock < menuQuietUntil ? "menu open (or just closed)" : clock - lastCommand < cooldown ? "cooldown" : null;
                if (LogGestures)
                {
                    var h = rig != null ? rig.Get(s == 0) : null;
                    Debug.Log($"[Gestures] {(s == 0 ? "left" : "right")} {(Microgesture)gesture}: {held ?? "acted"} (pinch {(h != null ? h.Pinch : -1f):0.00}, fist {(h != null ? h.Fist : -1f):0.00})");
                }
                if (held != null) continue;
                if (Act((Microgesture)gesture, music, rig.Get(s == 0))) lastCommand = clock;
            }
            Await(music);
            Draw(music, menuOpen, view, dt);
        }

        /// <summary>Carries out one gesture and shows what it did. False when the gesture does nothing.</summary>
        bool Act(Microgesture gesture, IMusicRemote music, HandRig.Hand hand)
        {
            int step = gesture == Microgesture.SwipeRight ? 1 : gesture == Microgesture.SwipeLeft ? -1 : 0;
            // Forward and backward swipes are left free.
            if (step == 0 && gesture != Microgesture.ThumbTap) return false;
            awaited = null;
            if (music.IsListening)
            {
                // The microphone follows music playing in the room, which these controls can't pause or change. No note
                // either, so the microphone doesn't pick it up.
                Show("Listening to the room", ListenIcon, false, ListenColor, 0);
                return true;
            }

            int wanted;
            if (step == 0)
            {
                bool wasPlaying = music.IsPlaying;
                music.TogglePause();
                Show(wasPlaying ? "Paused" : "Playing", PlayIcon, wasPlaying, SongColor, 0);
                // A settling note for pause, a brighter one to play.
                wanted = wasPlaying ? 64 : 69;
            }
            else
            {
                var song = music.SongAt(step);
                if (song == null) return false;
                music.StepSong(step);
                bool loading = music.Loading == song;
                Show(StepText(step, song, loading), step > 0 ? NextIcon : PreviousIcon, false, SongColor, step);
                if (loading)
                {
                    awaited = song;
                    awaitedStep = step;
                    awaitedSince = clock;
                }
                // Up for the next song, down for the previous one.
                wanted = step > 0 ? 74 : 62;
            }
            var thumb = hand.TipJoints[0] != null ? hand.TipJoints[0].position : hand.Tips[0];
            Musical.Play(voices, music.Frame, wanted, thumb, volume);
            return true;
        }

        /// <summary>
        /// While the song a swipe chose is still loading (read from the headset or streaming from Audius), its caption
        /// stays; once it plays the caption lingers a moment longer, and if it couldn't load the caption says so.
        /// </summary>
        void Await(IMusicRemote music)
        {
            if (awaited == null) return;
            var song = awaited;
            if (music != null && music.Loading == song && clock - awaitedSince < LongestWait)
            {
                age = Mathf.Min(age, showSeconds - FadeOut);
                return;
            }
            awaited = null;
            if (music == null) return;
            if (music.Failed == song)
                Show(Fit(song.IsFromAudius ? "Couldn't stream " : "Couldn't play ", Title(song), ""),
                    awaitedStep > 0 ? NextIcon : PreviousIcon, false, FailColor, 0);
            else if (music.Current == song)
            {
                SetText(StepText(awaitedStep, song, false));
                age = Mathf.Min(age, showSeconds - 1f);
            }
        }

        string StepText(int step, SongAsset song, bool loading) => Fit(step > 0 ? "Next: " : "Previous: ", Title(song), loading ? "…" : "");

        static string Title(SongAsset song) => string.IsNullOrEmpty(song.title) ? "song" : song.title;

        /// <summary>The prefix, title and suffix, with the title shortened if the caption would be wider than maxWidth.</summary>
        string Fit(string prefix, string title, string suffix)
        {
            var face = TypeFace.Bold;
            float room = (maxWidth - iconSize - Gap) / letterHeight;
            string text = prefix + title + suffix;
            for (int keep = title.Length - 1; keep > 0 && face.Width(text) > room; keep--)
                text = prefix + title.Substring(0, keep).TrimEnd() + "…";
            return text;
        }

        void Show(string text, float kind, bool paused, Color color, int direction)
        {
            // A caption that has gone starts again in front of you; one still showing stays where it is.
            if (!root.gameObject.activeSelf) placed = false;
            age = 0f;
            iconKind = kind;
            iconPaused = paused;
            slideDirection = direction;
            tint = Color.Lerp(Color.white, color, 0.3f);
            SetText(text);
            block.Clear();
            block.SetColor(ColorId, color);
            block.SetFloat(IconId, kind);
            block.SetFloat(PausedId, paused ? 1f : 0f);
            // Lit like a menu button just pressed: a bright rim, and an emblem that glows and settles.
            block.SetFloat(SelectedId, 1f);
        }

        /// <summary>Centres the emblem and the text together under the caption's origin.</summary>
        void SetText(string text)
        {
            label.Text = text;
            float width = TypeFace.Bold.Width(text) * letterHeight;
            float left = -(iconSize + Gap + width) * 0.5f;
            if (icon != null) icon.transform.localPosition = new Vector3(left + iconSize * 0.5f, 0f, 0f);
            label.Transform.localPosition = new Vector3(left + iconSize + Gap, 0f, 0f);
        }

        /// <summary>
        /// The caption floats about 20 degrees below eye height, under where the world hints show, and follows your view
        /// loosely. It fades in with the bubble popping up (sliding in the way a swipe went), holds, and fades out.
        /// Hidden while the menu is open, which shows the song itself.
        /// </summary>
        void Draw(IMusicRemote music, bool menuOpen, Camera view, float dt)
        {
            age += dt;
            if (menuOpen)
            {
                age = showSeconds;
                awaited = null;
            }
            bool show = age < showSeconds && view != null;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;

            var head = view.transform.position;
            var target = head + Effects.FlatForward(view) * distance + Vector3.down * drop;
            var position = placed ? Vector3.Lerp(root.position, target, 1f - Mathf.Exp(-3f * dt)) : target;
            root.SetPositionAndRotation(position, Quaternion.LookRotation(position - head, Vector3.up));
            placed = true;

            float alpha = Mathf.Clamp01(Mathf.Min(age / FadeIn, (showSeconds - age) / FadeOut));
            float arrive = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.35f), 3f);
            content.localPosition = new Vector3(-0.03f * slideDirection * (1f - arrive), -0.004f * (1f - arrive), 0f);
            label.Color = new Color(tint.r, tint.g, tint.b, alpha);
            if (icon == null) return;

            icon.transform.localScale = Vector3.one * Mathf.Max(iconSize * EaseOutBack(Mathf.Clamp01(age / 0.3f)), 1e-4f);
            block.SetFloat(AlphaId, alpha);
            block.SetFloat(HoverId, Mathf.Lerp(1f, 0.4f, arrive));
            // While playing, the emblem's equaliser follows the music.
            if (iconKind == PlayIcon && !iconPaused && music != null)
            {
                var bands = music.Frame.Bands;
                if (bands != null && bands.Length >= 5) block.SetVector(EqId, new Vector4(bands[1], bands[2], bands[3], bands[4]));
            }
            icon.SetPropertyBlock(block);
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        void Build()
        {
            if (root != null) return;
            root = new GameObject("Gesture Caption").transform;
            root.SetParent(transform, false);
            content = new GameObject("Content").transform;
            content.SetParent(root, false);
            label = new TypeLabel("Caption", content, letterHeight, 0.5f, left: true);
            var material = bubbleMaterial;
            if (material == null)
            {
                // Editor previews may run without the scene's material.
                var shader = Shader.Find("MusicInYourHands/Bubble");
                if (shader != null) material = new Material(shader);
            }
            if (material != null) icon = Effects.MakeQuad("Caption Emblem", content, material);
            block = new MaterialPropertyBlock();
            voices = new AudioPool(transform, 2);
            root.gameObject.SetActive(false);
        }
    }
}
