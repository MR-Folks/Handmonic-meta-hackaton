using System;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The first-launch demo: a guided three-minute journey through the app before the lessons. Each world plays for a
    /// set time and comes in under Journey mode's wave of light (JourneyMode.Flourish), with its own ghost demo showing
    /// what to do. Halfway through, the cassette deck rises and a caption and a pinching ghost hand ask you to pick a new
    /// song by putting a cassette in the radio; once the new song plays the deck goes and the journey goes on with it (if
    /// no song is picked in time, or the deck is closed, the demo plays one itself). Then the lessons follow, and the
    /// Journey takes over from the last world. A Skip target stays low on the right the whole time. Choosing a world at
    /// the menu ends the demo (you're choosing); opening the menu and closing it again only pauses it.
    /// The default order runs 3:00: Living skin, Heartbeat orb, Shadow play, Solar system, Bubble notes, Lightning,
    /// the cassette, Air strings, Flower dance.
    /// </summary>
    public partial class Onboarding
    {
        /// <summary>One step of the first-launch demo: a world by class name (or <see cref="CassetteStep"/>), and for how long.</summary>
        [Serializable]
        public struct DemoStep
        {
            [Tooltip("A world's class name (LivingSkin, SolarSystem, ...), or \"Cassette\" for picking a new song on the cassette deck.")]
            public string World;
            [Tooltip("Seconds it plays (for the cassette: the longest it waits for a song to be picked).")]
            public float Seconds;

            public DemoStep(string world, float seconds)
            {
                World = world;
                Seconds = seconds;
            }
        }

        public const string CassetteStep = "Cassette";
        public const string CassetteCaption = "Pick a new song: put a cassette in the radio";

        [Header("First-launch demo")]
        [Tooltip("The guided journey a first launch plays before the lessons, in order. Leave empty for no demo.")]
        [SerializeField] DemoStep[] demoSteps = DefaultDemo();
        [Tooltip("The song the demo plays itself at the cassette step when none is picked, by title (else the next song).")]
        [SerializeField] string demoSecondSong = "Glass Room";
        [Tooltip("Seconds the new song plays with the deck still up before the demo goes on.")]
        [SerializeField] float cassetteSettle = 2.5f;

        /// <summary>Whether a first launch plays the demo. The editor checks of the lessons turn it off.</summary>
        public static bool DemoOnFirstLaunch = true;

        /// <summary>The team's three-minute demo order.</summary>
        public static DemoStep[] DefaultDemo() => new[]
        {
            new DemoStep("LivingSkin", 16f),
            new DemoStep("HeartbeatOrb", 20f),
            new DemoStep("ShadowPlay", 20f),
            new DemoStep("SolarSystem", 24f),
            new DemoStep("BubbleNotes", 20f),
            new DemoStep("Lightning", 20f),
            new DemoStep(CassetteStep, 20f),
            new DemoStep("AirStrings", 20f),
            new DemoStep("FlowerDance", 20f),
        };

        bool demo, demoSwitching;
        int demoIndex = -1;
        float demoClock, cassetteChangedAt = -1f;
        SongAsset cassetteSong;

        // ---- What the editor checks read ----

        /// <summary>True while the first-launch demo plays.</summary>
        public bool DemoRunning => demo;

        /// <summary>The demo step playing now (an index in <see cref="DemoSteps"/>), or -1.</summary>
        public int DemoStepIndex => demo ? demoIndex : -1;

        /// <summary>The demo's steps, in order.</summary>
        public DemoStep[] DemoSteps => demoSteps;

        /// <summary>Seconds into the step playing now.</summary>
        public float DemoStepTime => demoClock;

        /// <summary>The longest the demo takes (s), the cassette step at its longest.</summary>
        public float DemoLength
        {
            get
            {
                float total = 0f;
                if (demoSteps != null) foreach (var s in demoSteps) total += s.Seconds;
                return total;
            }
        }

        bool InCassette => demo && demoIndex >= 0 && demoIndex < demoSteps.Length && demoSteps[demoIndex].World == CassetteStep;

        /// <summary>The world the demo opens in, or null when there is no demo.</summary>
        string DemoOpeningWorld => demoSteps != null && demoSteps.Length > 0 && demoSteps[0].World != CassetteStep ? demoSteps[0].World : null;

        // ---- The flow ----

        /// <summary>The demo starts with its first world (already switched to by the opening).</summary>
        void StartDemo()
        {
            demo = true;
            demoIndex = 0;
            demoClock = 0f;
            cassetteChangedAt = -1f;
            Debug.Log($"[Onboarding] Demo: {demoSteps.Length} steps, up to {DemoLength:0} s, opening in {demoSteps[0].World}.");
        }

        /// <summary>One frame of the demo: the step's clock runs (not while you have the menu open), and moves it on.</summary>
        void StepDemo(WorldManager worlds, bool menuOpen, Camera view, float dt)
        {
            if (!demo || demoIndex < 0) return;
            if (demoIndex >= demoSteps.Length)
            {
                EndDemo(false, false);
                return;
            }
            if (InCassette)
            {
                demoClock += dt;
                StepCassette(worlds, menuOpen, view);
                return;
            }
            // You opened the menu: the demo waits for you to close it (choosing a world there ends it).
            if (menuOpen) return;
            demoClock += dt;
            if (demoClock >= demoSteps[demoIndex].Seconds) NextDemoStep(worlds, view);
        }

        void NextDemoStep(WorldManager worlds, Camera view)
        {
            for (;;)
            {
                demoIndex++;
                demoClock = 0f;
                if (demoIndex >= demoSteps.Length)
                {
                    EndDemo(false, false);
                    return;
                }
                var step = demoSteps[demoIndex];
                if (step.World == CassetteStep)
                {
                    OpenCassette(view);
                    Debug.Log($"[Onboarding] Demo {demoIndex + 1}/{demoSteps.Length}: the cassette deck, up to {step.Seconds:0} s.");
                    return;
                }
                int world = FindWorld(worlds, step.World);
                // A world this build doesn't have: on to the next step.
                if (world < 0) continue;
                if (worlds.Worlds[world] != null) JourneyMode.Instance?.Flourish(view, worlds.Worlds[world].Color);
                demoSwitching = true;
                worlds.Switch(world);
                demoSwitching = false;
                // On the headset WorldChanged starts the world's ghost demo; without that subscription (the editor
                // checks) it starts here.
                if (subscribed == null) StartHint(world);
                Debug.Log($"[Onboarding] Demo {demoIndex + 1}/{demoSteps.Length}: {step.World} for {step.Seconds:0} s.");
                return;
            }
        }

        void OpenCassette(Camera view)
        {
            var bus = Application.isPlaying ? MusicBus.Instance : null;
            cassetteSong = bus != null ? bus.CurrentSong : null;
            cassetteChangedAt = -1f;
            var menu = menuNow != null ? menuNow : WorldMenu.Instance;
            if (menu != null && (view != null || Camera.main != null)) menu.OpenDeck(view != null ? view : Camera.main);
        }

        /// <summary>
        /// The cassette step: a new song playing (a cassette went in, or an added song is loading) lets the deck stay a
        /// moment, then the demo goes on; with no new song in time, or the deck closed, the demo plays one itself. While
        /// you have gone on from the deck to its song list or the file picker you are still choosing, so it waits.
        /// </summary>
        void StepCassette(WorldManager worlds, bool menuOpen, Camera view)
        {
            var bus = Application.isPlaying ? MusicBus.Instance : null;
            var menu = menuNow != null ? menuNow : WorldMenu.Instance;
            var deck = menu != null ? menu.Deck : null;
            bool changed = bus != null && ((bus.CurrentSong != cassetteSong && bus.CurrentSong != null) ||
                                           (bus.Loading != null && bus.Loading != cassetteSong));
            if (changed && cassetteChangedAt < 0f) cassetteChangedAt = demoClock;
            bool deckOpen = deck != null && deck.IsOpen;
            bool settled = cassetteChangedAt >= 0f && demoClock - cassetteChangedAt >= cassetteSettle;
            // The song page or the file picker is up (the menu is open without the deck): still choosing.
            if (menuOpen && !deckOpen && !settled) return;
            bool timeUp = demoClock >= demoSteps[demoIndex].Seconds;
            if (!settled && !timeUp && !(!menuOpen && demoClock > 1f)) return;
            if (!changed) PlaySecondSong(bus);
            if (deckOpen) deck.Close();
            NextDemoStep(worlds, view);
        }

        void PlaySecondSong(MusicBus bus)
        {
            if (bus == null) return;
            foreach (var s in bus.Songs)
                if (s != null && s != bus.CurrentSong && string.Equals(s.title, demoSecondSong, StringComparison.OrdinalIgnoreCase))
                {
                    bus.Play(s);
                    return;
                }
            bus.StepSong(1);
        }

        /// <summary>
        /// The demo is over: finished, skipped, or a world chosen at the menu. The deck goes if it is up; unless a world
        /// was chosen, Journey mode takes over from the world playing now; then the lessons follow.
        /// </summary>
        void EndDemo(bool skipped, bool chosen)
        {
            if (!demo) return;
            demo = false;
            opening = false;
            var menu = menuNow != null ? menuNow : WorldMenu.Instance;
            if (InCassetteIndex() && menu != null && menu.Deck != null && menu.Deck.IsOpen) menu.Deck.Close();
            demoIndex = -1;
            var journey = JourneyMode.Instance;
            if (!chosen && journey != null && !journey.IsActive && manager != null)
            {
                var bus = Application.isPlaying ? MusicBus.Instance : null;
                journey.OpenWith(manager.Current, bus != null ? bus.CurrentSong : null);
                journey.Begin();
            }
            Debug.Log(skipped ? "[Onboarding] Demo skipped." : chosen ? "[Onboarding] Demo ended: a world was chosen." : "[Onboarding] Demo finished.");
        }

        bool InCassetteIndex() => demoIndex >= 0 && demoIndex < demoSteps.Length && demoSteps[demoIndex].World == CassetteStep;

        /// <summary>What the cassette step shows: the caption above the deck and a pinching ghost hand.</summary>
        void DemoWanted(ref Display want)
        {
            if (!demo) return;
            if (InCassette)
            {
                want.Motion = GhostMotion.Pinch;
                want.Left = false;
                want.Caption = CassetteCaption;
                want.CaptionSide = 0f;
                want.CaptionHigh = true;
                want.Lights = 1 | 1 << 1;
                return;
            }
            want.Skip = true;
        }

        static int FindWorld(WorldManager worlds, string name)
        {
            if (worlds == null || string.IsNullOrEmpty(name)) return -1;
            for (int i = 0; i < worlds.Worlds.Count; i++)
                if (worlds.Worlds[i] != null && worlds.Worlds[i].GetType().Name == name) return i;
            return -1;
        }
    }
}
