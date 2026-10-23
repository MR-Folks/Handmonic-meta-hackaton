using System;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Plays the current song and exposes what every world reads each frame: six auto-levelled
    /// frequency bands, a beat pulse, the exact song time and the current lyric.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicBus : MonoBehaviour
    {
        public const int BandCount = 6;

        // Band edges in Hz: sub, bass, low mids, mids, presence, air.
        static readonly float[] BandEdges = { 20f, 120f, 300f, 800f, 2000f, 5000f, 12000f };

        public static MusicBus Instance { get; private set; }

        [SerializeField] SongAsset startSong;
        [SerializeField] bool playOnStart = true;
        [SerializeField] bool loop = true;

        [Header("Analysis")]
        [Tooltip("How quickly the bands follow the music. Higher is snappier.")]
        [SerializeField, Range(1f, 40f)] float bandResponse = 18f;
        [Tooltip("How fast each band's loudest-seen level fades, so quiet songs still fill the 0-1 range.")]
        [SerializeField, Range(0.5f, 0.99f)] float levelMemoryPerSecond = 0.85f;
        [Tooltip("How far bass energy must jump above its recent average to count as a beat.")]
        [SerializeField, Range(0.02f, 0.5f)] float beatThreshold = 0.15f;
        [SerializeField] float minBeatInterval = 0.28f;
        [SerializeField] float kickDecay = 7f;

        readonly float[] spectrum = new float[1024];
        readonly float[] bandPeak = new float[BandCount];
        AudioSource source;
        float bassAverage;

        /// <summary>Each band from 0 (silent) to 1 (as loud as it has been recently).</summary>
        public float[] Bands { get; } = new float[BandCount];
        public float Bass => Bands[1];
        public float Mids => Bands[3];
        public float Highs => Bands[5];

        /// <summary>1 on each detected beat, decaying towards 0.</summary>
        public float Kick { get; private set; }
        public float TimeSinceBeat { get; private set; } = 99f;
        public int BeatCount { get; private set; }

        public SongAsset CurrentSong { get; private set; }
        public LyricsTrack Lyrics { get; private set; } = LyricsTrack.Empty;
        public LyricsTrack.State Lyric { get; private set; } = new LyricsTrack.State(-1, -1);

        public bool IsPlaying => source != null && source.isPlaying;

        /// <summary>Song position in seconds, read from the audio clock rather than frame time.</summary>
        public float SongTime => source != null && source.clip != null ? (float)source.timeSamples / source.clip.frequency : 0f;

        public event Action Beat;
        public event Action<LyricsTrack.State> LyricChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            for (int i = 0; i < BandCount; i++) bandPeak[i] = 0.0001f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (playOnStart && startSong != null) Play(startSong);
        }

        public void Play(SongAsset song)
        {
            CurrentSong = song;
            Lyrics = song.lyrics != null ? LyricsTrack.Parse(song.lyrics.text) : LyricsTrack.Empty;
            Lyric = new LyricsTrack.State(-1, -1);
            source.clip = song.clip;
            source.loop = loop;
            source.Play();
        }

        public void TogglePause()
        {
            if (source.isPlaying) source.Pause();
            else source.UnPause();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateBands(dt);
            UpdateBeat(dt);
            UpdateLyric();
        }

        void UpdateBands(float dt)
        {
            if (!IsPlaying)
            {
                for (int i = 0; i < BandCount; i++) Bands[i] = Mathf.MoveTowards(Bands[i], 0f, dt * 2f);
                return;
            }

            source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            float binHz = AudioSettings.outputSampleRate * 0.5f / spectrum.Length;
            float memory = Mathf.Pow(levelMemoryPerSecond, dt);
            float follow = 1f - Mathf.Exp(-bandResponse * dt);

            for (int i = 0; i < BandCount; i++)
            {
                int lo = Mathf.Clamp((int)(BandEdges[i] / binHz), 0, spectrum.Length - 1);
                int hi = Mathf.Clamp((int)(BandEdges[i + 1] / binHz), lo + 1, spectrum.Length);
                float sum = 0f;
                for (int k = lo; k < hi; k++) sum += spectrum[k];
                float raw = sum / (hi - lo);

                bandPeak[i] = Mathf.Max(bandPeak[i] * memory, raw, 0.0001f);
                float level = raw / bandPeak[i];
                Bands[i] = Mathf.Lerp(Bands[i], level * level, follow);
            }
        }

        void UpdateBeat(float dt)
        {
            TimeSinceBeat += dt;
            float bass = Bands[0];
            float rise = bass - bassAverage;
            bassAverage = Mathf.Lerp(bassAverage, bass, 1f - Mathf.Exp(-6f * dt));

            if (IsPlaying && rise > beatThreshold && bass > 0.35f && TimeSinceBeat > minBeatInterval)
            {
                TimeSinceBeat = 0f;
                BeatCount++;
                Beat?.Invoke();
            }
            Kick = Mathf.Exp(-TimeSinceBeat * kickDecay);
        }

        void UpdateLyric()
        {
            float offset = CurrentSong != null ? CurrentSong.lyricsOffset : 0f;
            var state = Lyrics.Lookup(SongTime + offset);
            if (state.Equals(Lyric)) return;
            Lyric = state;
            LyricChanged?.Invoke(state);
        }

        /// <summary>The words of the current line, or an empty array before the first line.</summary>
        public LyricsTrack.Word[] CurrentLineWords => Lyric.HasLine ? Lyrics.Lines[Lyric.Line].Words : Array.Empty<LyricsTrack.Word>();
    }
}
