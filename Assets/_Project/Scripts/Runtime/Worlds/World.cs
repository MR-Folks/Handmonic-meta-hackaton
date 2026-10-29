using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// One world the player can pick from the wheel. WorldManager switches worlds by turning their GameObjects on
    /// and off. Each frame the base class hands the world the hands, the music and the camera through Step, which
    /// the editor previews call directly. A world must undo anything it changed outside itself in OnDisable.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public abstract class World : MonoBehaviour
    {
        [SerializeField] string displayName = "World";
        [Tooltip("The world's orb colour on the wheel.")]
        [SerializeField] Color color = Color.white;

        bool beatPending;

        // Re-anchoring: worlds placed in the room move to you when you sit down, stand up, walk elsewhere or
        // recenter, instead of staying where you were when the world started.
        static int recenterCount;
        Vector3 anchorHead;
        int anchorRecenter;
        int anchorRoom;
        float driftTime;

        /// <summary>Called when the headset recenters (Meta's recenter gesture or button).</summary>
        public static void NotifyRecentered() => recenterCount++;

        /// <summary>Remember where the head was when the world placed its things in the room.</summary>
        protected void MarkAnchored(Camera view)
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
        protected bool ShouldReanchor(Camera view, float dt)
        {
            if (view == null) return false;
            // Also when the room scan loads or changes, so things move onto the real table, walls and floor.
            if (anchorRecenter != recenterCount || anchorRoom != Room.Version) return true;
            var head = view.transform.position;
            var flat = new Vector2(head.x - anchorHead.x, head.z - anchorHead.z);
            bool moved = Mathf.Abs(head.y - anchorHead.y) > 0.25f || flat.magnitude > 0.9f;
            driftTime = moved ? driftTime + dt : 0f;
            return driftTime > 1.5f;
        }
        readonly System.Collections.Generic.List<NoteTrack.Note> upcoming = new System.Collections.Generic.List<NoteTrack.Note>();

        public string DisplayName => displayName;
        public Color Color => color;

        /// <summary>Which emblem the world's menu bubble shows (see the Bubble shader).</summary>
        public virtual int MenuIcon => 0;

        /// <summary>A short line saying what to do here, shown for a few seconds when the world starts.</summary>
        public virtual string Hint => "";

        /// <summary>True for the worlds built around the song's lyrics; the menu groups them apart.</summary>
        public virtual bool IsLyric => false;

        /// <summary>How the hands look in this world. Applied when the world becomes active.</summary>
        public virtual HandTheme Theme => HandTheme.Veins;

        /// <summary>Creates the world's objects once. Safe to call again.</summary>
        public abstract void Build();

        /// <summary>One frame of the world.</summary>
        public abstract void Step(HandRig rig, MusicFrame music, Camera view, float dt);

        protected virtual void OnEnable()
        {
            HandSkin.Theme = Theme;
            HandSkin.FocusStrength = 0f;
            HandSkin.Charge[0] = HandSkin.Charge[1] = 0f;
            Build();
            if (MusicBus.Instance != null) MusicBus.Instance.Beat += OnBeat;
        }

        protected virtual void OnDisable()
        {
            if (MusicBus.Instance != null) MusicBus.Instance.Beat -= OnBeat;
            beatPending = false;
        }

        void OnBeat() => beatPending = true;

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
