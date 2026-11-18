using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        /// <summary>Stands in for the MusicBus: a few songs, a paused flag, and a song that streams in when asked to.</summary>
        sealed class StandInMusic : IMusicRemote
        {
            public readonly List<SongAsset> Songs = new List<SongAsset>();
            public readonly float[] Bands = { 0.6f, 0.7f, 0.5f, 0.6f, 0.4f, 0.3f };
            public int Index;
            public bool Playing = true, Listening;
            /// <summary>A song that loads on demand: stepping to it starts loading it instead of playing it.</summary>
            public SongAsset OnDemand;
            SongAsset loading, failed;

            public bool IsListening => Listening;
            public bool IsPlaying => Playing;
            public SongAsset Current => Songs[Index];
            public SongAsset Loading => loading;
            public SongAsset Failed => failed;
            public MusicFrame Frame => new MusicFrame { Bands = Bands };

            public SongAsset SongAt(int step)
            {
                int from = loading != null ? Songs.IndexOf(loading) : Index;
                return Songs[((from + step) % Songs.Count + Songs.Count) % Songs.Count];
            }

            public void TogglePause() => Playing = !Playing;

            public void StepSong(int step)
            {
                var song = SongAt(step);
                loading = song == OnDemand ? song : null;
                if (loading == null) Index = Songs.IndexOf(song);
            }

            public void FinishLoading(bool ok)
            {
                if (ok) Index = Songs.IndexOf(loading);
                else failed = loading;
                loading = null;
            }
        }

        static object allocationProbe;

        /// <summary>
        /// The music gestures, driven with made-up microgestures: a thumb tap pauses and plays and swipes step to the
        /// previous and next song, with either hand; forward and backward swipes do nothing; a hand that pinches (or has
        /// just let go), makes Meta's menu gesture, makes a fist or isn't tracked is ignored while the other hand still
        /// works; nothing happens while the menu is open, nor a second command within the cooldown; while listening to
        /// the room the song is left alone; a song that streams in keeps its caption until it plays, or says it couldn't;
        /// and frames without a gesture allocate nothing.
        /// </summary>
        static bool CheckMusicGestures()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, 0f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var gestures = new GameObject("MusicGestures").AddComponent<MusicGestures>();
            SetSerialized(gestures, "bubbleMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble"));
            var music = new StandInMusic();
            foreach (var title in new[] { "Glass Room", "Neon Tide", "Rainy Window" })
            {
                var song = ScriptableObject.CreateInstance<SongAsset>();
                song.title = title;
                music.Songs.Add(song);
            }

            // Both hands loosely curled in front of the chest, thumbs resting on the side of the index finger.
            foreach (bool left in new[] { true, false })
            {
                var hand = rig.Get(left);
                var palm = new Vector3(left ? -0.15f : 0.15f, 1.2f, 0.35f);
                hand.IsTracked = true;
                hand.Palm = new Pose(palm, Quaternion.identity);
                hand.Pinch = 0.3f;
                hand.Fist = 0.4f;
                for (int i = 0; i < 5; i++) hand.Tips[i] = palm + new Vector3(0f, 0.05f, 0.04f + i * 0.01f);
            }
            var right = rig.Right;

            const float Frame = 1f / 72f;
            bool menuOpen = false;
            void Run(float seconds)
            {
                for (float t = 0f; t < seconds; t += Frame) gestures.Step(rig, music, menuOpen, camera, Frame);
            }
            // A hand finishes a gesture, and the frame it is reported in runs.
            void Do(bool left, Microgesture gesture)
            {
                gestures.Recognize(left, gesture);
                gestures.Step(rig, music, menuOpen, camera, Frame);
            }
            bool Tap(bool left)
            {
                bool was = music.Playing;
                Do(left, Microgesture.ThumbTap);
                return music.Playing != was;
            }
            // Past the cooldown.
            void Wait() => Run(0.6f);

            Run(0.5f);
            // A thumb tap pauses and the next one plays, from either hand; swipes go to the next and previous songs.
            Do(false, Microgesture.ThumbTap);
            bool paused = !music.Playing && gestures.Caption == "Paused";
            Wait();
            Do(true, Microgesture.ThumbTap);
            bool played = music.Playing && gestures.Caption == "Playing";
            Wait();
            Do(false, Microgesture.SwipeRight);
            bool next = music.Index == 1 && gestures.Caption == "Neon Tide";
            Wait();
            Do(true, Microgesture.SwipeLeft);
            bool previous = music.Index == 0 && gestures.Caption == "Glass Room";

            // A second command within the cooldown does nothing; after it, it works.
            Run(0.2f);
            Do(false, Microgesture.SwipeRight);
            bool cooled = music.Index == 0;
            Run(0.4f);
            Do(false, Microgesture.SwipeRight);
            cooled &= music.Index == 1;
            Wait();

            // Forward and backward swipes do nothing, not even start the cooldown.
            Do(false, Microgesture.SwipeForward);
            Do(false, Microgesture.SwipeBackward);
            bool unused = music.Index == 1 && music.Playing && Tap(false);
            Wait();

            // Nothing while the menu is open, nor just after it closes.
            menuOpen = true;
            Run(0.2f);
            bool menuBlocks = !Tap(false) && !Tap(true);
            menuOpen = false;
            menuBlocks &= !Tap(false);
            Run(0.4f);
            menuBlocks &= Tap(false);
            Wait();

            // A pinching hand is ignored, while the other still works, and so is a hand that has only just let go.
            right.Pinch = 0.95f;
            Run(0.2f);
            bool pinchBlocks = !Tap(false) && Tap(true);
            Wait();
            right.Pinch = 0.3f;
            Run(0.1f);
            pinchBlocks &= !Tap(false);
            Run(0.3f);
            pinchBlocks &= Tap(false);
            Wait();

            // Meta's menu gesture, a fist and a lost hand each block that hand until a moment after.
            right.InSystemGesture = true;
            Run(0.2f);
            bool systemBlocks = !Tap(false);
            right.InSystemGesture = false;
            Run(0.4f);
            systemBlocks &= Tap(false);
            Wait();
            right.Fist = 0.95f;
            Run(0.2f);
            bool fistBlocks = !Tap(false);
            right.Fist = 0.4f;
            Run(0.4f);
            fistBlocks &= Tap(false);
            Wait();
            right.IsTracked = false;
            Run(0.2f);
            bool lostBlocks = !Tap(false);
            right.IsTracked = true;
            Run(0.4f);
            lostBlocks &= Tap(false);
            Wait();

            // Listening to the room, the gestures only say so.
            music.Listening = true;
            Do(false, Microgesture.SwipeRight);
            bool listening = music.Index == 1 && gestures.Caption == "Listening";
            Wait();
            listening &= !Tap(false);
            music.Listening = false;
            Run(2f);

            // A song that streams in keeps its caption until it plays; one that can't load says so.
            music.OnDemand = music.Songs[2];
            Do(false, Microgesture.SwipeRight);
            Run(2.5f);
            bool streaming = music.Index == 1 && gestures.Caption == "Rainy Window…";
            music.FinishLoading(true);
            Run(0.1f);
            streaming &= music.Index == 2 && gestures.Caption == "Rainy Window";
            Run(1.2f);
            streaming &= gestures.Caption == "";
            music.OnDemand = music.Songs[0];
            Do(false, Microgesture.SwipeRight);
            Run(0.5f);
            music.FinishLoading(false);
            Run(0.1f);
            streaming &= music.Index == 2 && gestures.Caption == "Couldn't play Glass Room";

            // Opening the menu hides the caption, and it doesn't come back.
            menuOpen = true;
            Run(0.1f);
            bool hidden = gestures.Caption == "";
            menuOpen = false;
            Run(0.1f);
            hidden &= gestures.Caption == "";
            Run(1f);

            // Frames without a gesture allocate nothing, with or without a caption showing (here "Playing", whose
            // emblem follows the music).
            int probe = AllocationsIn(() => allocationProbe = new byte[2048]);
            int idle = AllocationsIn(() =>
            {
                for (int i = 0; i < 720; i++) gestures.Step(rig, music, false, camera, Frame);
            });
            music.Playing = false;
            Do(false, Microgesture.ThumbTap);
            gestures.Step(rig, music, false, camera, Frame);
            int showing = AllocationsIn(() =>
            {
                for (int i = 0; i < 90; i++) gestures.Step(rig, music, false, camera, Frame);
            });
            allocationProbe = null;
            // The probe makes sure allocations can be counted here at all.
            bool quiet = probe > 0 && idle == 0 && showing == 0;

            foreach (var song in music.Songs) Object.DestroyImmediate(song);
            bool ok = paused && played && next && previous && cooled && unused && menuBlocks && pinchBlocks && systemBlocks &&
                      fistBlocks && lostBlocks && listening && streaming && hidden && quiet;
            if (ok) Debug.Log("[Check] Music gestures: a thumb tap pauses and plays and swipes step songs with either hand; pinching, " +
                              "Meta's menu gesture, a fist, a lost hand, the open menu and the cooldown block them; listening " +
                              "leaves the song alone; a streaming song keeps its caption; idle frames allocate nothing.");
            else Debug.LogError($"[Check] Music gestures: tap pauses {paused}, tap plays {played}, swipe right next {next}, " +
                                $"swipe left previous {previous}, cooldown {cooled}, forward/backward unused {unused}, menu blocks {menuBlocks}, " +
                                $"pinch blocks {pinchBlocks}, system gesture blocks {systemBlocks}, fist blocks {fistBlocks}, " +
                                $"lost hand blocks {lostBlocks}, listening {listening}, streaming caption {streaming}, hidden by menu {hidden}, " +
                                $"allocations in idle frames {idle}, in frames with a caption {showing} (probe {probe}).");
            return ok;
        }

        /// <summary>
        /// How many managed allocations <paramref name="run"/> makes on this thread, from the profiler's GC.Alloc samples
        /// (as Unity's test framework counts them; Unity's Mono doesn't count allocated bytes per thread).
        /// </summary>
        static int AllocationsIn(System.Action run)
        {
            var recorder = Recorder.Get("GC.Alloc");
            // Turning the recorder off flushes what it caught before, such as its own creation.
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;
            try
            {
                run();
            }
            finally
            {
                recorder.enabled = false;
                recorder.CollectFromAllThreads();
            }
            return recorder.sampleBlockCount;
        }
    }
}
