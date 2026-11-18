using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>Where MusicBus hears the music: its own song, or the microphone (music playing in the room).</summary>
    public enum MusicSource { Song, Listen }

    /// <summary>
    /// Plays the current song, or listens to the room through the microphone, and exposes what every world reads
    /// each frame: six auto-levelled frequency bands, a beat pulse, the exact song time and the current lyric.
    /// While listening there are no lyrics or melody notes, so worlds fall back to following the bands.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicBus : MonoBehaviour
    {
        public const int BandCount = 6;

        // Band edges in Hz: sub, bass, low mids, mids, presence, air.
        static readonly float[] BandEdges = { 20f, 120f, 300f, 800f, 2000f, 5000f, 12000f };

        public static MusicBus Instance { get; private set; }

        [SerializeField] SongAsset startSong;
        [Tooltip("The songs the menu steps through, after the start song. Songs added in the Songs folder on the headset follow them.")]
        [SerializeField] List<SongAsset> songs = new List<SongAsset>();
        [SerializeField] bool playOnStart = true;
        [SerializeField] bool loop = true;
        [Tooltip("Listen starts on the microphone instead of the song.")]
        [SerializeField] MusicSource startSource = MusicSource.Song;

        [Header("Analysis")]
        [Tooltip("How quickly the bands follow the music. Higher is snappier.")]
        [SerializeField, Range(1f, 40f)] float bandResponse = 18f;
        [Tooltip("How fast each band's loudest-seen level fades, so quiet songs still fill the 0-1 range.")]
        [SerializeField, Range(0.5f, 0.99f)] float levelMemoryPerSecond = 0.85f;
        [Tooltip("How far bass energy must jump above its recent average to count as a beat.")]
        [SerializeField, Range(0.02f, 0.5f)] float beatThreshold = 0.15f;
        [SerializeField] float minBeatInterval = 0.28f;
        [SerializeField] float kickDecay = 7f;

        [Header("Listen mode")]
        [Tooltip("How many times louder than the room's own hum a band must be to count as music. Higher ignores more " +
                 "background noise; lower follows quieter music.")]
        [SerializeField, Range(1.5f, 10f)] float listenNoiseMargin = 4f;
        [Tooltip("How fast each band's hum estimate may rise, per second, so it settles on the room rather than the music.")]
        [SerializeField, Range(0.01f, 0.3f)] float listenNoiseRise = 0.05f;

        readonly float[] spectrum = new float[1024];
        readonly float[] bandPeak = new float[BandCount];
        readonly float[] bandNoise = new float[BandCount];
        readonly MicrophoneListener microphone = new MicrophoneListener();
        AudioSource source;
        readonly List<SongAsset> playlist = new List<SongAsset>();
        List<SongAsset> added = new List<SongAsset>();
        SongAsset loading, failed;
        float failedAt;
        float bassAverage;
        float listenTime;
        bool listenPaused, waitingForPermission;
        volatile bool permissionDenied;
        // The volume worlds set, and the opening fade-in on top of it.
        float volume = 1f, fade = 1f, fadeSpeed;

        public MusicSource Source { get; private set; }
        public bool IsListening => Source == MusicSource.Listen;

        /// <summary>Microphone loudness (RMS, 0-1) while listening, for tuning the gate.</summary>
        public float InputLevel => IsListening ? microphone.Level : 0f;

        /// <summary>What the menu shows: the song title, or "Listening".</summary>
        public string Title => IsListening ? "Listening" : CurrentSong != null ? CurrentSong.title : "Music";

        /// <summary>Each band from 0 (silent) to 1 (as loud as it has been recently).</summary>
        public float[] Bands { get; } = new float[BandCount];

        /// <summary>The latest 256 audio samples, -1 to 1, for drawing the waveform.</summary>
        public float[] Waveform { get; } = new float[256];
        public float Bass => Bands[1];
        public float Mids => Bands[3];
        public float Highs => Bands[5];

        /// <summary>1 on each detected beat, decaying towards 0.</summary>
        public float Kick { get; private set; }
        public float TimeSinceBeat { get; private set; } = 99f;
        /// <summary>The typical time between beats, from the last few (0 until there have been some).</summary>
        public float BeatInterval { get; private set; }
        public int BeatCount { get; private set; }

        public SongAsset CurrentSong { get; private set; }

        /// <summary>The song whose audio is on its way (read from the headset), or null.</summary>
        public SongAsset Loading => loading;

        /// <summary>A song whose audio couldn't be loaded in the last few seconds, so the menu can say so; else null.</summary>
        public SongAsset Failed => failed != null && Time.unscaledTime - failedAt < 5f ? failed : null;

        /// <summary>Every song the menu steps through: the built-in ones, then the ones added on the headset.</summary>
        public IReadOnlyList<SongAsset> Songs => playlist;
        public LyricsTrack Lyrics { get; private set; } = LyricsTrack.Empty;
        public NoteTrack Notes { get; private set; } = NoteTrack.Empty;

        /// <summary>Length of the current song in seconds, 0 without one or while listening.</summary>
        public float SongLength => !IsListening && source != null && source.clip != null ? source.clip.length : 0f;

        /// <summary>
        /// Adds the melody notes that start between now and <paramref name="ahead"/> seconds from now, with their
        /// times as song time. Looks across the loop point when the song repeats.
        /// </summary>
        public void UpcomingNotes(float ahead, List<NoteTrack.Note> into)
        {
            into.Clear();
            if (Notes.IsEmpty) return;
            float now = SongTime, end = now + ahead, length = SongLength;
            Notes.Between(now, end, into);
            if (loop && length > 0f && end > length) Notes.Between(0f, end - length, into, length);
        }
        public LyricsTrack.State Lyric { get; private set; } = new LyricsTrack.State(-1, -1);

        public bool IsPlaying => IsListening ? microphone.IsRecording : source != null && source.isPlaying;

        /// <summary>Playback volume, 0-1. Worlds may change it (Heartbeat orb does) and should restore it.</summary>
        public float Volume
        {
            get => volume;
            set
            {
                volume = Mathf.Clamp01(value);
                ApplyVolume();
            }
        }

        /// <summary>
        /// Fades the music in from silence over <paramref name="seconds"/>, on top of Volume (the first launch's opening
        /// song). Starting it before the song plays keeps the song silent until then.
        /// </summary>
        public void FadeIn(float seconds)
        {
            fadeSpeed = seconds > 0f ? 1f / seconds : 0f;
            fade = seconds > 0f ? 0f : 1f;
            ApplyVolume();
        }

        void ApplyVolume()
        {
            if (source != null) source.volume = volume * fade * fade * (3f - 2f * fade);
        }

        /// <summary>
        /// Song position in seconds, read from the audio clock rather than frame time. While listening, the seconds
        /// spent listening.
        /// </summary>
        public float SongTime => IsListening ? listenTime
            : source != null && source.clip != null ? (float)source.timeSamples / source.clip.frequency : 0f;

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
            volume = source.volume;
            // The song is a plain stereo bed: not spatialized and not in the room's reverb (that is for SpatialAudio's sounds).
            source.spatialBlend = 0f;
            source.spatialize = false;
            for (int i = 0; i < BandCount; i++) bandPeak[i] = 0.0001f;
        }

        void OnDestroy()
        {
            microphone.Stop();
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            RefreshSongs();
            if (startSource == MusicSource.Listen) Listen();
            else if (playOnStart && startSong != null) Play(startSong);
        }

        public void Play(SongAsset song)
        {
            if (song == null) return;
            // An added song's audio is read first; the current song keeps playing meanwhile.
            if (song.IsAdded && song.clip == null)
            {
                if (loading == song) return;
                loading = song;
                StartCoroutine(AddedSongs.Load(song, ok =>
                {
                    if (loading != song)
                    {
                        if (song != CurrentSong) AddedSongs.Unload(song);
                        return;
                    }
                    loading = null;
                    if (ok) Play(song);
                    else
                    {
                        failed = song;
                        failedAt = Time.unscaledTime;
                    }
                }));
                return;
            }
            loading = null;
            if (failed == song) failed = null;
            if (CurrentSong != null && CurrentSong != song && CurrentSong.IsAdded)
            {
                source.Stop();
                source.clip = null;
                AddedSongs.Unload(CurrentSong);
            }
            StopListening();
            CurrentSong = song;
            Lyrics = song.lyrics != null ? LyricsTrack.Parse(song.lyrics.text) : LyricsTrack.Empty;
            // A song without a melody file gets notes from its sung words, so the note-driven worlds follow the singing.
            Notes = song.notes != null ? NoteTrack.Parse(song.notes.text) : NoteTrack.FromLyrics(Lyrics, song.lyricsOffset);
            Lyric = new LyricsTrack.State(-1, -1);
            for (int i = 0; i < BandCount; i++) bandPeak[i] = 0.0001f;
            source.clip = song.clip;
            source.loop = loop;
            source.Play();
        }

        /// <summary>Plays the song <paramref name="step"/> places after the current one, or before it when negative.</summary>
        public void StepSong(int step) => Play(SongAt(step));

        /// <summary>The song <paramref name="step"/> places from the current one (or the one loading), wrapping around.</summary>
        public SongAsset SongAt(int step)
        {
            if (playlist.Count == 0) return null;
            int i = playlist.IndexOf(loading != null ? loading : CurrentSong);
            if (i < 0) return playlist[step > 0 ? 0 : playlist.Count - 1];
            return playlist[((i + step) % playlist.Count + playlist.Count) % playlist.Count];
        }

        /// <summary>Lists the built-in songs, then looks again for songs added in the Songs folder on the headset.</summary>
        public void RefreshSongs()
        {
            var found = AddedSongs.Scan(added);
            foreach (var old in added)
                if (!found.Contains(old) && old != CurrentSong && old != loading) Destroy(old);
            added = found;

            playlist.Clear();
            if (startSong != null) playlist.Add(startSong);
            foreach (var song in songs)
                if (song != null && !playlist.Contains(song)) playlist.Add(song);
            playlist.AddRange(added);
        }

        /// <summary>Stops the song and follows music playing in the room through the microphone.</summary>
        public void Listen()
        {
            source.Stop();
            Source = MusicSource.Listen;
            Lyrics = LyricsTrack.Empty;
            Notes = NoteTrack.Empty;
            Lyric = new LyricsTrack.State(-1, -1);
            listenTime = 0f;
            listenPaused = false;
            permissionDenied = false;
            // The microphone's spectrum is on its own scale, so forget the song's levels and learn the room's hum anew.
            for (int i = 0; i < BandCount; i++) bandPeak[i] = bandNoise[i] = 0f;
            StartMicrophone();
        }

        /// <summary>Switches between the song and listening to the room.</summary>
        public void ToggleListen()
        {
            if (IsListening) Play(CurrentSong != null ? CurrentSong : startSong);
            else Listen();
        }

        /// <summary>
        /// Stops the song and goes back to its start, as the cassette deck's Stop and Eject keys do; play or the
        /// thumb tap starts it again from the beginning. Listening to the room isn't stopped.
        /// </summary>
        public void Stop()
        {
            if (IsListening || source == null || source.clip == null) return;
            source.Pause();
            source.timeSamples = 0;
        }

        public void TogglePause()
        {
            if (IsListening)
            {
                listenPaused = !listenPaused;
                if (listenPaused) microphone.Stop();
                else StartMicrophone();
            }
            else if (source.isPlaying) source.Pause();
            else source.UnPause();
        }

        void StartMicrophone()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                if (waitingForPermission) return;
                waitingForPermission = true;
                // The answer may come on another thread; Update picks it up.
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionDenied += _ => permissionDenied = true;
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
                return;
            }
#endif
            waitingForPermission = false;
            // Without a microphone there is nothing to listen to, so go back to the song.
            if (!microphone.Start()) Play(CurrentSong != null ? CurrentSong : startSong);
        }

        void StopListening()
        {
            microphone.Stop();
            Source = MusicSource.Song;
            waitingForPermission = false;
        }

        void UpdatePermission()
        {
            if (!waitingForPermission) return;
            if (permissionDenied)
            {
                Debug.LogWarning("[MusicBus] Microphone permission refused, back to the song.");
                Play(CurrentSong != null ? CurrentSong : startSong);
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)) StartMicrophone();
#endif
        }

        void OnApplicationPause(bool paused)
        {
            if (!IsListening || listenPaused) return;
            if (paused) microphone.Stop();
            else StartMicrophone();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (fade < 1f)
            {
                fade = Mathf.MoveTowards(fade, 1f, dt * fadeSpeed);
                ApplyVolume();
            }
            UpdatePermission();
            bool heard;
            if (IsListening)
            {
                if (IsPlaying) listenTime += dt;
                heard = microphone.Read(spectrum, Waveform, dt);
            }
            else
            {
                heard = IsPlaying;
                if (heard)
                {
                    source.GetOutputData(Waveform, 0);
                    source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
                }
            }
            if (!heard) System.Array.Clear(Waveform, 0, Waveform.Length);
            UpdateBands(heard, IsListening ? microphone.SampleRate : AudioSettings.outputSampleRate, dt);
            UpdateBeat(dt);
            UpdateLyric();
        }

        void UpdateBands(bool heard, int sampleRate, float dt)
        {
            if (!heard)
            {
                for (int i = 0; i < BandCount; i++) Bands[i] = Mathf.MoveTowards(Bands[i], 0f, dt * 2f);
                return;
            }

            float binHz = sampleRate * 0.5f / spectrum.Length;
            float memory = Mathf.Pow(levelMemoryPerSecond, dt);
            float follow = 1f - Mathf.Exp(-bandResponse * dt);
            float noiseRise = Mathf.Exp(listenNoiseRise * dt);

            for (int i = 0; i < BandCount; i++)
            {
                int lo = Mathf.Clamp((int)(BandEdges[i] / binHz), 0, spectrum.Length - 1);
                int hi = Mathf.Clamp((int)(BandEdges[i + 1] / binHz), lo + 1, spectrum.Length);
                float sum = 0f;
                for (int k = lo; k < hi; k++) sum += spectrum[k];
                float raw = sum / (hi - lo);

                // The quietest a band may count as full. Songs use a fixed floor. The raw microphone has no gain, so a
                // fixed floor would hide normal-volume music; instead each band follows the room's own hum, dropping to
                // any quieter reading at once and creeping up slowly, and music counts once it rises well above it.
                float floor = 0.0001f;
                if (IsListening)
                {
                    bandNoise[i] = bandNoise[i] <= 0f || raw < bandNoise[i] ? raw : bandNoise[i] * noiseRise;
                    floor = Mathf.Max(bandNoise[i] * listenNoiseMargin, 1e-9f);
                }
                bandPeak[i] = Mathf.Max(bandPeak[i] * memory, raw, floor);
                float level = raw / bandPeak[i];
                Bands[i] = Mathf.Lerp(Bands[i], level * level, follow);
            }
        }

        void UpdateBeat(float dt)
        {
            TimeSinceBeat += dt;
            // Phone and laptop speakers barely reach the sub band, so while listening the bass band counts too.
            float bass = IsListening ? Mathf.Max(Bands[0], Bands[1]) : Bands[0];
            float rise = bass - bassAverage;
            bassAverage = Mathf.Lerp(bassAverage, bass, 1f - Mathf.Exp(-6f * dt));

            if (IsPlaying && rise > beatThreshold && bass > 0.35f && TimeSinceBeat > minBeatInterval)
            {
                if (TimeSinceBeat < 1.5f) BeatInterval = BeatInterval <= 0f ? TimeSinceBeat : Mathf.Lerp(BeatInterval, TimeSinceBeat, 0.25f);
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
