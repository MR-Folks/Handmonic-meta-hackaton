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

        public string DisplayName => displayName;
        public Color Color => color;

        /// <summary>Creates the world's objects once. Safe to call again.</summary>
        public abstract void Build();

        /// <summary>One frame of the world.</summary>
        public abstract void Step(HandRig rig, MusicFrame music, Camera view, float dt);

        protected virtual void OnEnable()
        {
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
            var music = new MusicFrame
            {
                Bands = bus != null ? bus.Bands : null,
                Kick = bus != null ? bus.Kick : 0f,
                Beat = beatPending,
                Waveform = bus != null ? bus.Waveform : null,
                Bus = bus
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
