using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The music as Journey mode follows it: the song, where it is, what it sounds like, and stopping or changing it.
    /// On the headset it is the MusicBus; the editor checks and screenshots use a stand-in.
    /// </summary>
    public interface IJourneyMusic
    {
        SongAsset Song { get; }
        /// <summary>The song's place in the song list, so each song gets its own mix of worlds.</summary>
        int SongIndex { get; }
        bool IsListening { get; }
        bool IsPlaying { get; }
        /// <summary>Song time (s); while listening, the seconds spent listening.</summary>
        float Time { get; }
        /// <summary>The audio's length (s), 0 while listening.</summary>
        float Length { get; }
        LyricsTrack Lyrics { get; }
        /// <summary>How loud the latest samples are (RMS, 0-1), before any levelling.</summary>
        float Loudness { get; }
        /// <summary>Whether a beat was heard since the last frame.</summary>
        bool Beat { get; }
        float BeatInterval { get; }
        float Kick { get; }
        SongAsset Loading { get; }
        SongAsset Failed { get; }
        SongAsset SongAt(int step);
        /// <summary>Stops the song and goes back to its start (it has ended).</summary>
        void Stop();
        void StepSong(int step);
    }

    /// <summary>
    /// Journey mode: each song becomes a guided journey through several worlds that change at the song's own sections.
    /// The song is cut into its sections (intro, verses, choruses, bridge, final chorus, outro) from its sections file,
    /// else from its lyrics, else from the audio as it plays (also when listening to the room), and each section plays
    /// one world: calm worlds that show off the room for the intro and verses, big ones for the choruses, a contrast for
    /// the bridge and the biggest for the final chorus, with no world twice in a song and a different mix for each song.
    /// One world plays at a time; the change lands exactly on the section's downbeat, under a soft wave of light that
    /// spreads through the room from your feet with a gentle sound in the song's key. A song map of small dots low in
    /// your view shows where you are in the song. The final chorus ends with a bloom of light, and when the song is over
    /// an end card offers "Another song" or "Explore", and goes on to the next song's journey by itself after ten seconds.
    /// It is on by default: every song that starts begins its journey. Picking a world in the menu turns it off (a short
    /// caption says so) and the menu's Journey bubble turns it back on. While the menu or the cassette deck is open no
    /// world changes; a change that falls then waits for the next section. Pausing the song pauses the journey.
    /// The menu, the onboarding and others use <see cref="Instance"/>, <see cref="Begin"/>, <see cref="Stop"/>,
    /// <see cref="Toggle"/>, <see cref="IsActive"/> and the <see cref="ActiveChanged"/> and <see cref="SectionChanged"/> events.
    /// </summary>
    [DefaultExecutionOrder(140)]
    public class JourneyMode : MonoBehaviour
    {
        /// <summary>The worlds one kind of section may play, best first, by class name (for example "BubbleNotes").</summary>
        [Serializable]
        public struct RoleWorlds
        {
            [Tooltip("The kind of section.")]
            public SectionRole Role;
            [Tooltip("World class names for it, best first (BubbleNotes, SolarSystem, ...).")]
            public string[] Worlds;

            public RoleWorlds(SectionRole role, params string[] worlds)
            {
                Role = role;
                Worlds = worlds;
            }
        }

        [SerializeField] Material bubbleMaterial;
        [SerializeField] Material glowMaterial;
        [SerializeField] Material lineMaterial;
        [SerializeField] Material waveMaterial;

        [Header("Journey")]
        [Tooltip("Start the journey as the app starts, so every song plays one.")]
        [SerializeField] bool startOn = true;
        [Tooltip("The worlds each kind of section plays, by class name, best first. Calm worlds that show off the room " +
                 "for the intro, verses and outro; big ones for the choruses; a contrast for the bridge and breaks; the " +
                 "biggest for the final chorus. No world plays twice in a song: once a role's worlds are used, the next " +
                 "unused one in this table plays.")]
        [SerializeField] RoleWorlds[] journeySet = DefaultSet();
        [Tooltip("How many of a role's first worlds the songs take turns to start from, so each song gets its own mix.")]
        [SerializeField, Range(1, 4)] int variety = 2;
        [Tooltip("In a sections file, sections shorter than this (s) fold into a neighbour: a world needs a while to be seen.")]
        [SerializeField] float shortestSection = 7f;
        [Tooltip("A song started by hand (cassette deck, thumb swipe, menu arrows) after a world was picked turns the " +
                 "journey back on. Off: only the Journey bubble does.")]
        [SerializeField] bool newSongResumes;

        [Header("Changes of section")]
        [Tooltip("Seconds before a boundary the wave leaves your feet, so the world changes under it.")]
        [SerializeField] float waveLead = 0.3f;
        [Tooltip("A faint glow gathers at your feet during the bar before each change.")]
        [SerializeField] bool buildUp = true;
        [Tooltip("For songs whose sections come from their lyrics: how long (s) before and after a boundary a heard beat " +
                 "may take the change, so it lands on the beat.")]
        [SerializeField] Vector2 beatWindow = new Vector2(0.35f, 0.6f);
        [Tooltip("Seconds the menu must have been closed before a new song's (or a restarted journey's) first world comes in.")]
        [SerializeField] float settle = 0.5f;
        [Tooltip("Volume of the swell and chord at each change, 0-1.")]
        [SerializeField, Range(0f, 1f)] float volume = 0.35f;
        [SerializeField] JourneyWave.Tuning wave = new JourneyWave.Tuning();

        [Header("Song map")]
        [SerializeField] SongMap.Tuning map = new SongMap.Tuning();

        [Header("End card")]
        [Tooltip("Seconds the end card waits for a choice before the next song's journey starts by itself.")]
        [SerializeField] float choiceSeconds = 10f;
        [Tooltip("Seconds a chosen song may take to start (streaming) before the journey moves on to the one after.")]
        [SerializeField] float loadPatience = 20f;
        [SerializeField] JourneyEndCard.Tuning card = new JourneyEndCard.Tuning();

        public const string OffCaption = "Journey off: you're choosing";
        public const string OnCaption = "Journey on: the worlds follow the song";

        /// <summary>
        /// The worlds a journey plays for each kind of section, best first: the team's in-headset favourites that also
        /// look full with the hands resting (Solar system, Flower dance, Sound pool, Bubble notes, then Lightning, Air
        /// strings and Shadow play). Heartbeat orb and Finger frame need hands held up, so they come only in a bridge,
        /// where the song strips back and the world's hint invites the hands in.
        /// </summary>
        public static RoleWorlds[] DefaultSet() => new[]
        {
            new RoleWorlds(SectionRole.FinalChorus, "SolarSystem", "BubbleNotes", "Lightning"),
            new RoleWorlds(SectionRole.Chorus, "BubbleNotes", "FlowerDance", "SolarSystem", "Lightning"),
            new RoleWorlds(SectionRole.Bridge, "HeartbeatOrb", "FingerFrame", "Lightning", "ShadowPlay"),
            new RoleWorlds(SectionRole.Break, "ShadowPlay", "Lightning", "AirStrings", "FingerFrame"),
            new RoleWorlds(SectionRole.Intro, "SoundPool", "FlowerDance", "AirStrings"),
            new RoleWorlds(SectionRole.Verse, "SoundPool", "AirStrings", "FlowerDance", "Lightning"),
            new RoleWorlds(SectionRole.PreChorus, "AirStrings", "SoundPool", "Lightning"),
            new RoleWorlds(SectionRole.Outro, "AirStrings", "FlowerDance", "SoundPool", "Lightning", "ShadowPlay", "LivingSkin"),
        };

        // Sections get their worlds in this order, so the biggest moments get the best picks.
        static readonly SectionRole[] PickOrder =
        {
            SectionRole.FinalChorus, SectionRole.Chorus, SectionRole.Bridge, SectionRole.Break,
            SectionRole.Intro, SectionRole.Verse, SectionRole.PreChorus, SectionRole.Outro
        };

        public static JourneyMode Instance { get; private set; }

        /// <summary>True while the journey is on (playing a song's journey, or showing its end card).</summary>
        public bool IsActive => state != State.Off;

        /// <summary>True while the end card is up, waiting for a choice.</summary>
        public bool CardOpen => state == State.Card;

        /// <summary>The current song's sections (empty while off before any song).</summary>
        public SongSections Sections => plan;

        /// <summary>The section playing now, or -1.</summary>
        public int CurrentSection => state != State.Off ? lastIndex : -1;

        /// <summary>The world (index in WorldManager.Worlds) the journey gives section <paramref name="index"/>, or -1.</summary>
        public int WorldOf(int index) => index >= 0 && index < plan.Count && index < worldOf.Length ? worldOf[index] : -1;

        /// <summary>Raised when the journey turns on (true) or off (false).</summary>
        public event Action<bool> ActiveChanged;

        /// <summary>Raised when a section's world comes in, with the section's index in <see cref="Sections"/>.</summary>
        public event Action<int> SectionChanged;

        /// <summary>The caption showing under the song map now, or an empty string (for the editor checks).</summary>
        public string Caption => mapView != null ? mapView.Caption : "";

        /// <summary>For the editor checks and screenshots: the wave, the song map and the end card.</summary>
        public JourneyWave Wave => waveView;
        public SongMap Map => mapView;
        public JourneyEndCard EndCard => cardView;

        enum State { Off, Running, Card }

        sealed class BusMusic : IJourneyMusic
        {
            public MusicBus Bus;
            public bool Heard;
            public SongAsset Song => Bus.CurrentSong;
            public int SongIndex
            {
                get
                {
                    var songs = Bus.Songs;
                    for (int i = 0; i < songs.Count; i++) if (songs[i] == Bus.CurrentSong) return i;
                    return 0;
                }
            }
            public bool IsListening => Bus.IsListening;
            public bool IsPlaying => Bus.IsPlaying;
            public float Time => Bus.SongTime;
            public float Length => Bus.SongLength;
            public LyricsTrack Lyrics => Bus.Lyrics;
            public float Loudness
            {
                get
                {
                    var w = Bus.Waveform;
                    float sum = 0f;
                    for (int i = 0; i < w.Length; i++) sum += w[i] * w[i];
                    return w.Length > 0 ? Mathf.Sqrt(sum / w.Length) : 0f;
                }
            }
            public bool Beat => Heard;
            public float BeatInterval => Bus.BeatInterval;
            public float Kick => Bus.Kick;
            public SongAsset Loading => Bus.Loading;
            public SongAsset Failed => Bus.Failed;
            public SongAsset SongAt(int step) => Bus.SongAt(step);
            public void Stop() => Bus.Stop();
            public void StepSong(int step) => Bus.StepSong(step);
            public void OnBeat() => Heard = true;
        }

        readonly BusMusic busMusic = new BusMusic();
        MusicBus subscribedBus;
        readonly SongSections plan = new SongSections();
        readonly LiveSections live = new LiveSections();
        int[] worldOf = new int[32];
        readonly bool[] used = new bool[64];
        readonly Color[] colors = new Color[SongMap.MaxDots];
        readonly string[] labels = new string[SongMap.MaxDots];
        readonly List<string> names = new List<string>(8);

        State state;
        bool planned, planListening, pending, bloomed, ourSwitch, begun, musicOn;
        SongAsset planSong, offSong;
        int planSongIndex, lastIndex = -1, shown = -1, launchedFor = -1, armedFor = -1, livePicked;
        // Starts settled, so the first song's first world comes in at once.
        float lastTime, pendingFor, menuClosedFor = 99f, cardFor, awaitingFor, gather;
        SongAsset awaiting;
        int retries;
        WorldManager boundManager;
        Transform visuals;
        JourneyWave waveView;
        SongMap mapView;
        JourneyEndCard cardView;
        AudioPool voices;
        Camera lastView;

        void OnEnable()
        {
            Instance = this;
            if (boundManager != null) boundManager.WorldChanged += OnWorldChanged;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (boundManager != null) boundManager.WorldChanged -= OnWorldChanged;
            if (subscribedBus != null) subscribedBus.Beat -= busMusic.OnBeat;
            subscribedBus = null;
        }

        void Start()
        {
            if (startOn) Begin();
        }

        void LateUpdate()
        {
            var bus = MusicBus.Instance;
            if (bus != subscribedBus)
            {
                if (subscribedBus != null) subscribedBus.Beat -= busMusic.OnBeat;
                if (bus != null) bus.Beat += busMusic.OnBeat;
                subscribedBus = bus;
            }
            busMusic.Bus = bus;
            var menu = WorldMenu.Instance;
            bool menuOpen = (menu != null && menu.IsOpen) || SongPicker.Waiting;
            Step(HandRig.Instance, WorldManager.Instance, bus != null ? busMusic : null, menuOpen, Camera.main, Time.deltaTime);
            busMusic.Heard = false;
        }

        // ---- What others call ----

        /// <summary>Turns the journey on: the current song's journey starts where the song is now.</summary>
        public void Begin()
        {
            if (state != State.Off) return;
            state = State.Running;
            begun = true;
            planned = false;
            ActiveChanged?.Invoke(true);
        }

        /// <summary>Turns the journey off. The world playing stays, and the song loops as usual.</summary>
        public void Stop() => TurnOff(null);

        /// <summary>The menu's Journey bubble: on or off, with a short caption.</summary>
        public void Toggle()
        {
            if (state != State.Off)
            {
                TurnOff("Journey off");
                return;
            }
            Begin();
            mapView?.Say(OnCaption);
        }

        /// <summary>
        /// The player picked a world in the menu (even the one playing): they are choosing, so the journey turns off. The
        /// menu calls this; switches made by anything else than the journey turn it off too (WorldManager.WorldChanged).
        /// </summary>
        public void WorldPicked()
        {
            if (state != State.Off) TurnOff(OffCaption);
        }

        void TurnOff(string caption)
        {
            if (state == State.Off) return;
            // Turned off at the end card (the menu was opened over it): the music goes on with the next song.
            if (state == State.Card) musicOn = true;
            offSong = planSong;
            awaiting = null;
            state = State.Off;
            pending = false;
            armedFor = -1;
            gather = 0f;
            live.End();
            cardView?.Close();
            waveView?.Gather(null, 0f, Color.white);
            if (caption != null) mapView?.Say(caption);
            ActiveChanged?.Invoke(false);
        }

        void OnWorldChanged(int index)
        {
            if (ourSwitch || state == State.Off) return;
            TurnOff(OffCaption);
        }

        // ---- Each frame ----

        /// <summary>
        /// One frame of the journey. Public so the editor checks and screenshots can drive it with a stand-in for the
        /// music; on the headset LateUpdate calls it with the MusicBus.
        /// </summary>
        public void Step(HandRig rig, WorldManager manager, IJourneyMusic music, bool menuOpen, Camera view, float dt)
        {
            Build();
            Bind(manager);
            if (view != null) lastView = view;
            menuClosedFor = menuOpen ? 0f : menuClosedFor + dt;
            gather = 0f;
            if (musicOn && music != null)
            {
                musicOn = false;
                if (!music.IsPlaying)
                {
                    offSong = music.SongAt(1);
                    music.StepSong(1);
                }
            }
            if (state == State.Off && newSongResumes && begun && music != null && music.Song != offSong && music.IsPlaying && !music.IsListening)
                Begin();
            if (state != State.Off && music != null && manager != null) Follow(rig, manager, music, menuOpen, view, dt);

            waveView.Gather(view, gather, NextColor());
            waveView.Step(dt);
            bool mapHidden = menuOpen || state == State.Card;
            int count = state != State.Off ? Mathf.Min(plan.Count, SongMap.MaxDots) : 0;
            int future = state != State.Off && plan.Source == SectionSource.Audio && plan.Length > 0f && music != null
                ? Mathf.Clamp(Mathf.RoundToInt((plan.Length - music.Time) / 40f), 0, SongMap.MaxDots - count) : 0;
            mapView.Step(count, lastIndex, future, colors, labels, music != null ? music.Kick : 0f, view, mapHidden, dt);
        }

        void Follow(HandRig rig, WorldManager manager, IJourneyMusic music, bool menuOpen, Camera view, float dt)
        {
            float time = music.Time;
            bool playing = music.IsPlaying;

            // A new song, or the room instead of the song: its own journey, from where it is now.
            if (!planned || music.Song != planSong || music.IsListening != planListening)
            {
                bool wasCard = state == State.Card;
                MakePlan(manager, music, time);
                if (wasCard) cardView.Close();
                state = State.Running;
                StartAfresh();
                awaiting = null;
            }
            else if (state == State.Card)
            {
                StepCard(rig, music, menuOpen, view, dt);
                lastTime = time;
                return;
            }
            else if (awaiting != null)
            {
                // Waiting for the song chosen at the end card to start (one from Audius streams in first).
                WatchChosenSong(music, dt);
                lastTime = time;
                return;
            }
            else
            {
                // The song jumped: looped round, restarted, or skipped ahead or back.
                float expected = lastTime + (playing ? dt : 0f);
                bool wrapped = !music.IsListening && music.Length > 0f && lastTime > music.Length - 2f && time < 2f;
                if (wrapped && plan.Count > 0)
                {
                    lastTime = time;
                    EndSong(music, view);
                    return;
                }
                if (Mathf.Abs(time - expected) > 1f)
                {
                    if (plan.Source == SectionSource.Audio)
                    {
                        live.Begin(plan, music.IsListening ? 0f : music.Length, time, music.IsListening);
                        ResetPicks();
                        AssignLive(manager, music);
                    }
                    bloomed = plan.FinaleTime >= 0f && time > plan.FinaleTime + 1f;
                    StartAfresh();
                }
            }
            lastTime = time;

            // Sections heard as the music plays. After a long listen, once the song map is full, a fresh one starts here.
            if (plan.Source == SectionSource.Audio && playing && live.Step(time, dt, music.Loudness, music.Beat, music.BeatInterval))
            {
                if (plan.Count > SongMap.MaxDots)
                {
                    live.Begin(plan, music.IsListening ? 0f : music.Length, time, music.IsListening);
                    ResetPicks();
                }
                AssignLive(manager, music);
            }

            int index = plan.IndexAt(time);
            if (index < 0) return;

            // The music is over: the end card.
            if (!music.IsListening && playing && plan.Count > 0 && IsOver(time, music))
            {
                EndSong(music, view);
                return;
            }

            if (pending)
            {
                // A new song, a jump, or the journey just turned on: its world comes in on the first beat once the menu
                // has been closed a moment (or after a second without one, as when paused).
                lastIndex = index;
                if (menuOpen || menuClosedFor < settle) return;
                pendingFor += dt;
                // At a song's very start its first downbeat is now.
                if (music.Beat || pendingFor > 1f || !playing || time < 0.3f)
                {
                    pending = false;
                    SwitchTo(index, manager, view);
                }
                return;
            }

            // A boundary passed. (Only forward: a jump back starts afresh above, and a change taken on a beat just before
            // its boundary already moved lastIndex on.)
            if (index > lastIndex)
            {
                lastIndex = index;
                // A change that falls while the menu is open is let go: the world waits for the next section.
                if (menuOpen) armedFor = -1;
                else if (shown != index)
                {
                    if (plan.Source == SectionSource.Lyrics && !music.Beat) armedFor = index;
                    else SwitchTo(index, manager, view);
                }
            }

            if (plan.Source == SectionSource.Lyrics && !menuOpen)
            {
                // Sections found from the lyrics start near a sung line, not exactly on the downbeat: the change waits
                // for a beat heard just before or after the boundary.
                if (armedFor >= 0 && (music.Beat || time >= plan[armedFor].Start + beatWindow.y))
                {
                    SwitchTo(armedFor, manager, view);
                    armedFor = -1;
                }
                else if (armedFor < 0 && music.Beat && index + 1 < plan.Count && shown != index + 1 &&
                         plan[index + 1].Start - time <= beatWindow.x)
                    SwitchTo(index + 1, manager, view);
            }

            // Known boundaries: a faint glow gathers in the bar before, and the wave leaves a moment early so the world
            // changes under it.
            if (!menuOpen && playing && plan.Source != SectionSource.Audio && index + 1 < plan.Count && shown != index + 1)
            {
                float toGo = plan[index + 1].Start - time;
                float bar = plan.BarSeconds(music.BeatInterval);
                if (buildUp && toGo < bar) gather = 1f - toGo / bar;
                if (toGo <= waveLead && launchedFor != index + 1) Launch(index + 1, view, false);
            }

            // The finale: a bloom on the final chorus's last downbeat.
            if (!bloomed && playing && !menuOpen && plan.FinaleTime >= 0f && time >= plan.FinaleTime && time < plan.FinaleTime + 1.5f)
            {
                bloomed = true;
                Launch(-1, view, true);
            }
        }

        bool IsOver(float time, IJourneyMusic music)
        {
            float end = music.Length > 0f ? music.Length - 0.3f : float.PositiveInfinity;
            if (plan.End > 0f) end = Mathf.Min(end, plan.End);
            return time >= end;
        }

        void StartAfresh()
        {
            pending = true;
            pendingFor = 0f;
            armedFor = -1;
            launchedFor = -1;
            shown = -1;
        }

        /// <summary>Brings in section <paramref name="index"/>'s world, under a wave unless one already left for it.</summary>
        void SwitchTo(int index, WorldManager manager, Camera view)
        {
            shown = index;
            lastIndex = Mathf.Max(lastIndex, index);
            int world = WorldOf(index);
            if (world >= 0 && world < manager.Worlds.Count && world != manager.Current && manager.Worlds[world] != null)
            {
                ourSwitch = true;
                manager.Switch(world);
                ourSwitch = false;
            }
            if (launchedFor != index) Launch(index, view, false);
            mapView.Flash();
            SectionChanged?.Invoke(index);
        }

        void Launch(int index, Camera view, bool finale)
        {
            if (index >= 0) launchedFor = index;
            if (view == null) view = lastView;
            if (view == null) return;
            waveView.Launch(view, index >= 0 ? ColorOf(index) : Color.white, finale);
            var head = view.transform.position;
            JourneySounds.Play(voices, head + Effects.FlatForward(view) * 0.4f + Vector3.down * 0.9f, plan, finale, volume);
        }

        // ---- The end of a song ----

        void EndSong(IJourneyMusic music, Camera view)
        {
            music.Stop();
            state = State.Card;
            cardFor = 0f;
            pending = false;
            armedFor = -1;
            // Songs whose finale couldn't be placed in advance still end in bloom.
            if (!bloomed)
            {
                bloomed = true;
                Launch(-1, view, true);
            }
            var song = music.Song;
            var next = music.SongAt(1);
            cardView.Open(view != null ? view : lastView, song != null ? song.title : "", next != null && next != song ? next.title : "", choiceSeconds);
        }

        void StepCard(HandRig rig, IJourneyMusic music, bool menuOpen, Camera view, float dt)
        {
            // The song was started again (a thumb tap, the menu's play bubble, the deck): its journey starts over.
            if (music.IsPlaying)
            {
                cardView.Close();
                state = State.Running;
                planned = false;
                return;
            }
            if (!menuOpen) cardFor += dt;
            int choice = cardView.Step(rig, view, Mathf.Max(0f, choiceSeconds - cardFor), choiceSeconds, menuOpen, dt);
            if (choice == JourneyEndCard.Explore)
            {
                // The music goes on while you explore: the next song, as a song of its own.
                TurnOff(null);
                WorldMenu.Instance?.Open(view);
                return;
            }
            if (choice == JourneyEndCard.Another || cardFor >= choiceSeconds)
            {
                cardView.Close();
                state = State.Running;
                NextSong(music);
            }
        }

        void NextSong(IJourneyMusic music)
        {
            var next = music.SongAt(1);
            music.StepSong(1);
            awaiting = next;
            awaitingFor = 0f;
        }

        /// <summary>
        /// After moving on to the next song, if it can't play (no internet for a song from Audius) or takes too long, the
        /// journey moves on to the one after, so a demo never stalls in silence.
        /// </summary>
        void WatchChosenSong(IJourneyMusic music, float dt)
        {
            if (awaiting == null) return;
            if (music.Song == awaiting && music.IsPlaying)
            {
                awaiting = null;
                retries = 0;
                return;
            }
            awaitingFor += dt;
            bool failed = music.Failed == awaiting || (music.Loading != awaiting && music.Song != awaiting) || awaitingFor > loadPatience;
            if (!failed) return;
            if (++retries > 8)
            {
                awaiting = null;
                planned = false;
                return;
            }
            NextSong(music);
        }

        // ---- Plans: sections and their worlds ----

        void MakePlan(WorldManager manager, IJourneyMusic music, float time)
        {
            planned = true;
            planSong = music.Song;
            planListening = music.IsListening;
            planSongIndex = music.SongIndex;
            bloomed = false;
            var song = music.Song;
            float length = music.IsListening ? 0f : music.Length;
            live.End();
            // The best source there is: the song's sections file, else its lyrics, else the audio as it plays.
            bool known = !music.IsListening && song != null && song.sections != null && SongSections.Parse(song.sections.text, plan);
            if (known)
            {
                plan.Tidy(length, shortestSection);
                known = plan.Count > 0;
            }
            if (!known && !music.IsListening)
                known = SectionFinder.FromLyrics(music.Lyrics, song != null ? song.lyricsOffset : 0f, length, plan);
            if (!known) live.Begin(plan, length, time, music.IsListening);

            ResetPicks();
            if (plan.Source == SectionSource.Audio) AssignLive(manager, music);
            else AssignAll(manager);
            lastIndex = plan.IndexAt(time);
            bloomed = plan.FinaleTime >= 0f && time > plan.FinaleTime + 1f;
        }

        void ResetPicks()
        {
            Array.Clear(used, 0, used.Length);
            livePicked = 0;
            if (worldOf.Length < plan.Count + 8) worldOf = new int[plan.Count + 16];
            for (int i = 0; i < worldOf.Length; i++) worldOf[i] = -1;
        }

        void AssignAll(WorldManager manager)
        {
            if (worldOf.Length < plan.Count) worldOf = new int[plan.Count + 16];
            foreach (var role in PickOrder)
            {
                int k = 0;
                for (int i = 0; i < plan.Count; i++)
                    if (plan[i].Role == role) worldOf[i] = Pick(manager, role, k++, i);
            }
            for (int i = 0; i < plan.Count; i++) Remember(manager, i);
        }

        /// <summary>Gives the sections heard so far (music heard as it plays) their worlds.</summary>
        void AssignLive(WorldManager manager, IJourneyMusic music)
        {
            if (worldOf.Length < plan.Count + 1)
            {
                var grown = new int[plan.Count + 16];
                for (int i = 0; i < grown.Length; i++) grown[i] = i < worldOf.Length ? worldOf[i] : -1;
                worldOf = grown;
            }
            for (int i = livePicked; i < plan.Count; i++)
            {
                int k = 0;
                for (int j = 0; j < i; j++) if (plan[j].Role == plan[i].Role) k++;
                worldOf[i] = Pick(manager, plan[i].Role, k, i);
                Remember(manager, i);
            }
            livePicked = plan.Count;
        }

        void Remember(WorldManager manager, int i)
        {
            if (i >= SongMap.MaxDots) return;
            int w = worldOf[i];
            colors[i] = w >= 0 && w < manager.Worlds.Count && manager.Worlds[w] != null ? manager.Worlds[w].Color : Color.white;
            labels[i] = plan[i].Label;
        }

        /// <summary>
        /// The world for the <paramref name="k"/>-th section with this role: the first of the role's worlds not yet used
        /// in this song, each song starting from a different one of its first few; then any unused world in the journey
        /// set; then any unused world at all; and with every world used, one that differs from its neighbours.
        /// </summary>
        int Pick(WorldManager manager, SectionRole role, int k, int section)
        {
            names.Clear();
            foreach (var entry in journeySet)
                if (entry.Role == role && entry.Worlds != null) names.AddRange(entry.Worlds);
            if (names.Count > 0)
            {
                int turns = Mathf.Min(variety, names.Count);
                int start = (planSongIndex + k) % turns;
                for (int j = 0; j < names.Count; j++)
                {
                    int w = Find(manager, names[(start + j) % names.Count]);
                    if (Take(w)) return w;
                }
            }
            foreach (var entry in journeySet)
                if (entry.Worlds != null)
                    foreach (var name in entry.Worlds)
                    {
                        int w = Find(manager, name);
                        if (Take(w)) return w;
                    }
            for (int w = 0; w < manager.Worlds.Count && w < used.Length; w++)
                if (manager.Worlds[w] != null && Take(w)) return w;
            // More sections than worlds: repeat one, never the same as the section before or after.
            int before = section > 0 ? worldOf[section - 1] : -1, after = section + 1 < plan.Count ? worldOf[section + 1] : -1;
            for (int w = 0; w < manager.Worlds.Count; w++)
            {
                int candidate = (section * 7 + w) % manager.Worlds.Count;
                if (candidate != before && candidate != after && manager.Worlds[candidate] != null) return candidate;
            }
            return manager.Worlds.Count > 0 ? 0 : -1;
        }

        bool Take(int w)
        {
            if (w < 0 || w >= used.Length || used[w]) return false;
            used[w] = true;
            return true;
        }

        static int Find(WorldManager manager, string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            var worlds = manager.Worlds;
            for (int i = 0; i < worlds.Count; i++)
            {
                var world = worlds[i];
                if (world != null && (world.GetType().Name == name || world.DisplayName == name)) return i;
            }
            return -1;
        }

        Color ColorOf(int index) => index >= 0 && index < SongMap.MaxDots && index < plan.Count ? colors[index] : Color.white;

        Color NextColor() => lastIndex + 1 < plan.Count ? ColorOf(lastIndex + 1) : Color.white;

        // ---- Set-up ----

        void Bind(WorldManager manager)
        {
            if (manager == boundManager) return;
            if (boundManager != null) boundManager.WorldChanged -= OnWorldChanged;
            boundManager = manager;
            if (boundManager != null) boundManager.WorldChanged += OnWorldChanged;
        }

        void Build()
        {
            if (visuals != null) return;
            visuals = new GameObject("Journey").transform;
            visuals.SetParent(transform, false);
            // Editor previews may run without the scene's materials.
            var glow = glowMaterial != null ? glowMaterial : Make("MusicInYourHands/Glow");
            var line = lineMaterial != null ? lineMaterial : Make("MusicInYourHands/Line");
            var bubble = bubbleMaterial != null ? bubbleMaterial : Make("MusicInYourHands/Bubble");
            var wash = waveMaterial != null ? waveMaterial : Make("MusicInYourHands/JourneyWave");
            waveView = new JourneyWave(visuals, wash, glow, wave);
            mapView = new SongMap(visuals, glow, line, map);
            cardView = new JourneyEndCard(visuals, bubble, glow, line, card);
            voices = new AudioPool(visuals, 8);
        }

        static Material Make(string shader)
        {
            var found = Shader.Find(shader);
            return found != null ? new Material(found) : null;
        }
    }
}
