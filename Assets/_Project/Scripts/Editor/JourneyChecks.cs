using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        const string SongsFolder = "Assets/_Project/Songs";

        /// <summary>
        /// Stands in for the MusicBus in Journey mode's checks and screenshots: songs that play by the clock, a beat on
        /// the song's tempo, a loudness to set, and a record of when the journey stopped or changed the song.
        /// </summary>
        sealed class StandInJourney : IJourneyMusic
        {
            public readonly List<SongAsset> List = new List<SongAsset>();
            readonly Dictionary<SongAsset, LyricsTrack> lyrics = new Dictionary<SongAsset, LyricsTrack>();
            public int Index;
            public bool Playing = true, Listening, Heard;
            public float Clock, Interval = 0.5f, Loud = 0.2f, Ends;
            public int Stops, Steps;
            float lastBeat = -10f;

            public SongAsset Song => List.Count > 0 ? List[Index] : null;
            public int SongIndex => Index;
            public bool IsListening => Listening;
            public bool IsPlaying => Playing;
            public float Time => Clock;
            public float Length => Listening || Song == null ? 0f : Ends > 0f ? Ends : Song.clip != null ? Song.clip.length : 120f;
            public LyricsTrack Lyrics
            {
                get
                {
                    var song = Song;
                    if (song == null || Listening) return LyricsTrack.Empty;
                    if (!lyrics.TryGetValue(song, out var track))
                        lyrics[song] = track = song.lyrics != null ? LyricsTrack.Parse(song.lyrics.text) : LyricsTrack.Empty;
                    return track;
                }
            }
            public float Loudness => Loud;
            public bool Beat => Heard;
            public float BeatInterval => Interval;
            public float Kick => Mathf.Exp(-BeatAge * 7f);
            public float BeatAge => Mathf.Max(0f, Clock - lastBeat);
            public SongAsset Loading => null;
            public SongAsset Failed => null;
            public SongAsset SongAt(int step) => List.Count == 0 ? null : List[((Index + step) % List.Count + List.Count) % List.Count];
            public void Stop() { Playing = false; Clock = 0f; lastBeat = -10f; Stops++; }

            public void StepSong(int step)
            {
                Steps++;
                int index = ((Index + step) % List.Count + List.Count) % List.Count;
                var tempo = Tempos.TryGetValue(List[index], out var t) ? t : new Vector2(60f / Interval, 0f);
                Play(index, tempo.x, tempo.y);
            }

            /// <summary>Each song's tempo (BPM) and first downbeat (s), for the beats it plays.</summary>
            public readonly Dictionary<SongAsset, Vector2> Tempos = new Dictionary<SongAsset, Vector2>();

            /// <summary>Plays on by <paramref name="dt"/>, hearing a beat when one goes by (on the song's own beat grid).</summary>
            public void Advance(float dt)
            {
                Heard = false;
                if (!Playing) return;
                float before = Clock;
                Clock += dt;
                if (Mathf.Floor((Clock - Offset) / Interval) > Mathf.Floor((before - Offset) / Interval))
                {
                    Heard = true;
                    lastBeat = Mathf.Floor((Clock - Offset) / Interval) * Interval + Offset;
                }
            }

            /// <summary>Where the beat grid starts (s).</summary>
            public float Offset;

            /// <summary>Jumps to <paramref name="time"/>, as the deck's keys or a seek would.</summary>
            public void Seek(float time)
            {
                Clock = time;
                lastBeat = time - 10f;
            }

            /// <summary>Plays the song from the list at <paramref name="index"/> from its start, at its tempo.</summary>
            public void Play(int index, float bpm, float firstBeat = 0f)
            {
                Index = index;
                Clock = 0f;
                lastBeat = -10f;
                Playing = true;
                Interval = 60f / bpm;
                Offset = firstBeat;
            }
        }

        static SongAsset LoadSong(string file) => AssetDatabase.LoadAssetAtPath<SongAsset>($"{SongsFolder}/{file}.asset");

        static string Stamp(float t) => $"{(int)(t / 60f)}:{t % 60f:00.00}";

        /// <summary>Journey mode: the built-in songs' sections, the lyric and audio fallbacks, and a run through songs.</summary>
        static bool CheckJourney()
        {
            bool ok = CheckJourneySections();
            ok &= CheckJourneyFallbacks();
            ok &= CheckJourneyRun();
            return ok;
        }

        /// <summary>
        /// Every built-in song has a sections file that reads into sensible sections: the first starting with the song,
        /// each starting after the one before and at least 7 s long, the last starting well before the music ends, one
        /// final chorus with its finale inside it, and a tempo and key. And the sections agree with the word-timed lyrics:
        /// no line is sung in an intro, outro or instrumental break, and every line sung more than once (a chorus line)
        /// falls in a chorus, allowing a pickup of a beat and a half before the chorus's downbeat.
        /// </summary>
        static bool CheckJourneySections()
        {
            bool ok = true;
            var report = new StringBuilder();
            var guids = AssetDatabase.FindAssets("t:SongAsset", new[] { SongsFolder });
            int checkedSongs = 0;
            foreach (var guid in guids)
            {
                var song = AssetDatabase.LoadAssetAtPath<SongAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (song == null) continue;
                checkedSongs++;
                var raw = new SongSections();
                if (song.sections == null || song.clip == null || !SongSections.Parse(song.sections.text, raw))
                {
                    Debug.LogError($"[Check] Journey: {song.name} has no sections file, or it has no sections.");
                    ok = false;
                    continue;
                }
                float length = song.clip.length;
                var problems = new List<string>();
                if (raw.Bpm <= 0f) problems.Add("no tempo");
                if (raw.KeyTonic < 0) problems.Add("no key");
                if (raw.End <= 0f || raw.End > length + 0.01f) problems.Add($"its end mark {Stamp(raw.End)} is missing or after the audio ({Stamp(length)})");
                if (raw[0].Start > 0.5f) problems.Add($"the first section starts at {Stamp(raw[0].Start)}");
                for (int i = 1; i < raw.Count; i++)
                    if (raw[i].Start <= raw[i - 1].Start + 1f) problems.Add($"{raw[i].Label} doesn't start after {raw[i - 1].Label}");
                if (raw[raw.Count - 1].Start > raw.MusicEnd - 5f) problems.Add($"{raw[raw.Count - 1].Label} starts too near the end");

                // The lyrics against the sections as written.
                var lyrics = song.lyrics != null ? LyricsTrack.Parse(song.lyrics.text) : LyricsTrack.Empty;
                var counts = new Dictionary<string, int>();
                foreach (var line in lyrics.Lines)
                {
                    var text = LineText(line);
                    counts[text] = counts.TryGetValue(text, out int n) ? n + 1 : 1;
                }
                float pickup = 1.5f * 60f / Mathf.Max(raw.Bpm, 40f);
                foreach (var line in lyrics.Lines)
                {
                    float t = line.Time - song.lyricsOffset;
                    var section = raw[Mathf.Max(0, raw.IndexAt(t + pickup))];
                    bool sung = section.Role != SectionRole.Intro && section.Role != SectionRole.Outro && section.Role != SectionRole.Break;
                    bool chorus = section.Role == SectionRole.Chorus || section.Role == SectionRole.FinalChorus;
                    if (!sung) problems.Add($"\"{LineText(line)}\" at {Stamp(t)} is sung in the {section.Label.ToLowerInvariant()}");
                    else if (counts[LineText(line)] > 1 && !chorus) problems.Add($"the chorus line \"{LineText(line)}\" at {Stamp(t)} falls in {section.Label}");
                }

                // As Journey mode plays them.
                var tidy = new SongSections();
                tidy.CopyFrom(raw);
                tidy.Tidy(length, 7f);
                int finals = tidy.Sections.Count(s => s.Role == SectionRole.FinalChorus);
                int final = tidy.FinalIndex;
                if (tidy.Count < 2) problems.Add($"only {tidy.Count} section after tidying");
                if (finals != 1) problems.Add($"{finals} final choruses");
                for (int i = 0; i < tidy.Count; i++)
                    if (tidy.EndOf(i) - tidy[i].Start < 7f) problems.Add($"{tidy[i].Label} lasts under 7 s");
                if (final >= 0 && (tidy.FinaleTime < tidy[final].Start || tidy.FinaleTime >= tidy.EndOf(final)))
                    problems.Add($"the finale at {Stamp(tidy.FinaleTime)} is outside the final chorus");

                if (problems.Count > 0)
                {
                    Debug.LogError($"[Check] Journey: {song.title}'s sections: {string.Join("; ", problems.Distinct())}.");
                    ok = false;
                }
                report.Append($"\n  {song.title} ({raw.Bpm:0} BPM): ");
                report.Append(string.Join(", ", raw.Sections.Select(s => $"{s.Label} {Stamp(s.Start)}")));
                report.Append($", end {Stamp(raw.End)}; plays as {tidy.Count} worlds, finale at {Stamp(tidy.FinaleTime)}");
            }
            if (checkedSongs < 7)
            {
                Debug.LogError($"[Check] Journey: expected the seven built-in songs, found {checkedSongs}.");
                ok = false;
            }
            if (ok) Debug.Log("[Check] Journey: the built-in songs' sections read, start with the song, follow each other, end before the music " +
                              "does, have one final chorus, and agree with the lyrics (no sung line in an intro, outro or break; " +
                              "chorus lines in choruses):" + report);
            return ok;
        }

        static string LineText(LyricsTrack.Line line)
        {
            var text = new StringBuilder();
            foreach (var word in line.Words)
            {
                foreach (char c in word.Text) if (char.IsLetterOrDigit(c)) text.Append(char.ToLowerInvariant(c));
                text.Append(' ');
            }
            return text.ToString().Trim();
        }

        /// <summary>
        /// Songs without a sections file: their sections come from their lyrics (repeated lines make the choruses, long
        /// gaps the instrumental breaks), close to the hand-made ones; without lyrics, from the audio as it plays, cut on
        /// phrase downbeats where the music clearly changes, each section 20 to 60 s. A sections file longer than its song
        /// is cut to fit it.
        /// </summary>
        static bool CheckJourneyFallbacks()
        {
            var problems = new List<string>();
            string lyricReport = "", audioReport = "";

            // From the lyrics alone, against the sections made by hand.
            foreach (var file in new[] { "TurnItUpToLight", "TheWorldIsHumming", "SlowMotionGlow" })
            {
                var song = LoadSong(file);
                if (song == null || song.clip == null || song.lyrics == null || song.sections == null)
                {
                    problems.Add($"{file} is missing");
                    continue;
                }
                var made = new SongSections();
                SongSections.Parse(song.sections.text, made);
                var found = new SongSections();
                bool any = SectionFinder.FromLyrics(LyricsTrack.Parse(song.lyrics.text), song.lyricsOffset, song.clip.length, found);
                if (!any)
                {
                    problems.Add($"no sections from {file}'s lyrics");
                    continue;
                }
                foreach (var s in made.Sections)
                {
                    if (s.Role != SectionRole.Chorus && s.Role != SectionRole.FinalChorus) continue;
                    bool near = found.Sections.Any(f => (f.Role == SectionRole.Chorus || f.Role == SectionRole.FinalChorus) && Mathf.Abs(f.Start - s.Start) < 1.5f);
                    if (!near) problems.Add($"{song.title}: no chorus found near {Stamp(s.Start)} from the lyrics");
                }
                if (found[0].Role != SectionRole.Intro || found[found.Count - 1].Role != SectionRole.Outro || found.FinalIndex < 0)
                    problems.Add($"{song.title}: the lyrics gave no intro, outro or final chorus (" +
                                 string.Join(", ", found.Sections.Select(f => $"{f.Label} {Stamp(f.Start)}")) + ")");
                if (file == "TheWorldIsHumming" && !found.Sections.Any(f => f.Role == SectionRole.Break && f.Start > 60f && f.Start < 66f))
                    problems.Add("The World Is Humming: the guitar interlude after the first chorus wasn't found from the gap in its lyrics");
                if (file == "TurnItUpToLight" && !found.Sections.Any(f => f.Role == SectionRole.Bridge))
                    problems.Add("Turn It Up to Light: no bridge found from its lyrics");
                if (file == "TurnItUpToLight")
                    lyricReport = string.Join(", ", found.Sections.Select(s => $"{s.Label} {Stamp(s.Start)}"));
            }

            // A song shorter than its sections file: the sections after its end go and the last chorus becomes the final.
            var shortSong = new SongSections();
            var turn = LoadSong("TurnItUpToLight");
            if (turn != null && SongSections.Parse(turn.sections.text, shortSong))
            {
                shortSong.Tidy(90f, 7f);
                if (shortSong[shortSong.Count - 1].Start >= 88f || shortSong.FinalIndex < 0 || shortSong[shortSong.FinalIndex].Start > 80f)
                    problems.Add("a 90 s cut of Turn It Up to Light kept sections past its end or lost its final chorus");
            }
            var broken = new SongSections();
            if (SongSections.Parse("# nothing but notes\nbpm fast\nnot a time\n", broken))
                problems.Add("a sections file with no sections still read as one");

            // From the audio: quiet, loud with a steady kick, quiet, loud again, at 120 BPM in a 170 s song.
            var live = new LiveSections();
            var heard = new SongSections();
            live.Begin(heard, 170f, 0f, false);
            const float dt = 1f / 60f;
            float time = 0f, lastBeat = -1f;
            while (time < 170f)
            {
                time += dt;
                bool loud = (time >= 34f && time < 90f) || time >= 112f;
                float beatTime = Mathf.Floor(time / 0.5f) * 0.5f;
                bool beat = loud && beatTime > lastBeat;
                if (beat) lastBeat = beatTime;
                live.Step(time, dt, loud ? 0.3f : 0.05f, beat, 0.5f);
            }
            var expected = new[] { SectionRole.Intro, SectionRole.Chorus, SectionRole.Verse, SectionRole.FinalChorus };
            for (int i = 0; i < heard.Count; i++)
            {
                float span = heard.EndOf(i) - heard[i].Start;
                // Phrases are eight beats (4 s here) from the start.
                float phrase = heard[i].Start / 4f;
                if (Mathf.Abs(phrase - Mathf.Round(phrase)) > 0.02f) problems.Add($"audio section {i} starts off a phrase, at {heard[i].Start:0.00} s");
                if (i < heard.Count - 1 && (span < 19.9f || span > 60.1f)) problems.Add($"audio section {i} lasts {span:0.0} s");
            }
            if (heard.Count != expected.Length || !expected.Select((r, i) => heard[i].Role == r).All(x => x))
                problems.Add("the audio gave " + string.Join(", ", heard.Sections.Select(s => $"{s.Role} at {s.Start:0.0} s")) +
                             " (wanted an intro, a chorus near 34 s, a verse near 90 s, a final chorus near 112 s)");
            else if (heard[1].Start - 34f > 4.1f || heard[2].Start - 90f > 4.1f || heard[3].Start - 112f > 4.1f)
                problems.Add("the audio's changes were found late: " + string.Join(", ", heard.Sections.Select(s => $"{s.Start:0.0} s")));
            audioReport = string.Join(", ", heard.Sections.Select(s => $"{s.Label} {s.Start:0} s"));

            // Steady music never changes, so a new section starts every minute.
            live.Begin(heard, 0f, 0f, true);
            time = 0f;
            lastBeat = -1f;
            while (time < 150f)
            {
                time += dt;
                float beatTime = Mathf.Floor(time / 0.5f) * 0.5f;
                bool beat = beatTime > lastBeat;
                if (beat) lastBeat = beatTime;
                live.Step(time, dt, 0.25f, beat, 0.5f);
            }
            if (heard.Count != 3 || Mathf.Abs(heard[1].Start - 60f) > 0.1f || Mathf.Abs(heard[2].Start - 120f) > 0.1f)
                problems.Add("steady music gave sections at " + string.Join(", ", heard.Sections.Select(s => $"{s.Start:0.0} s")) + " (wanted 0, 60 and 120 s)");

            if (problems.Count > 0)
            {
                Debug.LogError("[Check] Journey fallbacks: " + string.Join("; ", problems) + ".");
                return false;
            }
            Debug.Log("[Check] Journey fallbacks: without a sections file the lyrics give the sections (Turn It Up to Light: " + lyricReport +
                      "), within 1.5 s of the hand-made choruses, with The World Is Humming's interlude found from its gap; without lyrics the " +
                      "audio gives them on phrase downbeats, 20 to 60 s long (" + audioReport + "); a sections file longer than its song is cut to fit.");
            return true;
        }

        /// <summary>
        /// A run through songs with a stand-in for the music: the journey switches worlds exactly at Turn It Up to Light's
        /// section boundaries and never repeats one; nothing switches while the menu is open, and a boundary missed then
        /// waits for the next; the final chorus blooms; at the end the song stops and the end card shows, and after ten
        /// seconds the next song's journey starts by itself, with a different mix of worlds. A world picked by hand turns
        /// the journey off (with its caption) and no world changes after; the Journey bubble turns it back on. A song
        /// without a sections file plays a journey from its lyrics, and listening to the room one from the audio, with no
        /// end card. Frames between changes allocate nothing.
        /// </summary>
        static bool CheckJourneyRun()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, 0f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var managerGo = new GameObject("Worlds");
            var worlds = WorldCatalog.MakePreviews(managerGo.transform);
            var manager = managerGo.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Length;
            for (int i = 0; i < worlds.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            manager.Switch(0);

            var journey = new GameObject("Journey").AddComponent<JourneyMode>();
            SetSerialized(journey, "bubbleMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble"));
            SetSerialized(journey, "glowMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow"));
            SetSerialized(journey, "lineMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line"));
            SetSerialized(journey, "waveMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.JourneyWaveMaterialPath, "MusicInYourHands/JourneyWave"));

            var music = new StandInJourney();
            foreach (var file in new[] { "TurnItUpToLight", "TheWorldIsHumming", "SlowMotionGlow" }) music.List.Add(LoadSong(file));
            if (music.List.Any(s => s == null))
            {
                Debug.LogError("[Check] Journey run: the sung songs are missing.");
                return false;
            }
            music.Tempos[music.List[0]] = new Vector2(118f, 0.02f);
            music.Tempos[music.List[1]] = new Vector2(92f, 0.025f);
            music.Tempos[music.List[2]] = new Vector2(80f, 1.505f);
            music.Play(0, 118f, 0.02f);

            var switches = new List<(float time, int world)>();
            manager.WorldChanged += w => switches.Add((music.Clock, w));
            var problems = new List<string>();
            bool menuOpen = false;
            const float dt = 1f / 72f;
            void Frame()
            {
                music.Advance(dt);
                journey.Step(rig, manager, music, menuOpen, camera, dt);
            }
            void Run(float seconds)
            {
                for (float t = 0f; t < seconds; t += dt) Frame();
            }
            void RunUntil(float clock, float most = 400f)
            {
                for (float t = 0f; t < most && music.Clock < clock; t += dt) Frame();
            }

            journey.Begin();
            // Song one, with the menu open across the second chorus's downbeat (1:19.34).
            RunUntil(75f);
            var plan = new SongSections();
            plan.CopyFrom(journey.Sections);
            var picks = new int[plan.Count];
            for (int i = 0; i < plan.Count; i++) picks[i] = journey.WorldOf(i);
            menuOpen = true;
            RunUntil(82f);
            menuOpen = false;
            for (float t = 0f; t < 120f && !journey.CardOpen; t += dt) Frame();

            if (plan.Source != SectionSource.Authored || plan.Count != 8) problems.Add($"Turn It Up to Light played {plan.Count} sections from {plan.Source}");
            if (picks.Distinct().Count() != picks.Length || picks.Any(w => w < 0)) problems.Add("a world repeats in Turn It Up to Light: " + string.Join(", ", picks));
            int missed = plan.IndexAt(79.5f);
            for (int i = 0; i < plan.Count; i++)
            {
                float at = plan[i].Start;
                bool hit = switches.Any(s => Mathf.Abs(s.time - at) <= dt * 1.5f && s.world == picks[i]);
                if (i == missed)
                {
                    if (hit || switches.Any(s => s.time > 75f && s.time < plan[i + 1].Start - dt * 2f)) problems.Add("a world changed while the menu was open, or before the next section after it");
                }
                else if (!hit) problems.Add($"no switch to {worlds[Mathf.Max(0, picks[i])].DisplayName} at {plan[i].Label} ({Stamp(at)}); switches at " +
                                            string.Join(", ", switches.Select(s => $"{s.time:0.00}")));
            }
            if (journey.Wave.Blooms != 1) problems.Add($"{journey.Wave.Blooms} finale blooms in the song");
            if (!journey.CardOpen || music.Stops != 1) problems.Add($"the end card didn't show when the music ended (stops {music.Stops})");
            int songOneWaves = journey.Wave.Waves;

            // Nobody chooses: after ten seconds the next song's journey starts, with its own mix of worlds.
            switches.Clear();
            int stepsBefore = music.Steps;
            Run(10.5f);
            Run(0.5f);
            bool continued = music.Steps == stepsBefore + 1 && music.Index == 1 && !journey.CardOpen && journey.IsActive;
            if (!continued) problems.Add($"the end card didn't move on to the next song by itself (steps {music.Steps - stepsBefore}, song {music.Index}, card {journey.CardOpen})");
            var second = journey.Sections;
            if (second.Count < 5 || manager.Current != journey.WorldOf(0))
                problems.Add("the second song's first world didn't come in as it started");
            bool varied = false;
            for (int i = 0; i < Mathf.Min(plan.Count, second.Count); i++)
                if (plan[i].Role == second[i].Role && picks[i] != journey.WorldOf(i)) varied = true;
            if (!varied) problems.Add("the second song played the same worlds for the same sections");

            // Allocations: frames in the middle of a section, with the song map showing.
            RunUntil(25f);
            journey.Map.Flash();
            int probe = AllocationsIn(() => allocationProbe = new byte[2048]);
            int steady = AllocationsIn(() =>
            {
                for (int i = 0; i < 288; i++) Frame();
            });
            allocationProbe = null;
            if (probe <= 0 || steady != 0) problems.Add($"{steady} allocations in 4 s of frames between changes (probe {probe})");

            // A world picked in the menu: the journey turns off with its caption, and no world changes after.
            menuOpen = true;
            Run(0.3f);
            int other = Enumerable.Range(0, worlds.Length).First(w => w != manager.Current);
            journey.WorldPicked();
            manager.Switch(other);
            Run(0.3f);
            menuOpen = false;
            Run(0.5f);
            string caption = journey.Caption;
            switches.Clear();
            RunUntil(45f);
            if (journey.IsActive || caption != JourneyMode.OffCaption || switches.Count > 0)
                problems.Add($"picking a world left the journey on ({journey.IsActive}), said \"{caption}\", or a world changed after ({switches.Count})");

            // The Journey bubble turns it back on: the section's world comes in on a beat soon after the menu closes.
            menuOpen = true;
            journey.Toggle();
            Run(0.3f);
            menuOpen = false;
            Run(2f);
            int now = journey.Sections.IndexAt(music.Clock);
            if (!journey.IsActive || manager.Current != journey.WorldOf(now) || journey.Caption != JourneyMode.OnCaption)
                problems.Add($"the Journey bubble didn't bring the journey back (on {journey.IsActive}, world {manager.Current} not {journey.WorldOf(now)}, \"{journey.Caption}\")");

            // A switch made by anything else than the journey (a world switched by the tour, say) also turns it off.
            manager.Switch(Enumerable.Range(0, worlds.Length).First(w => w != manager.Current));
            if (journey.IsActive) problems.Add("a world switched from outside left the journey on");
            journey.Begin();
            Run(1.5f);

            // A jump in the song (the deck's stop key, a seek): the world for where it landed comes in.
            music.Seek(110f);
            Run(1.5f);
            now = journey.Sections.IndexAt(music.Clock);
            if (manager.Current != journey.WorldOf(now)) problems.Add($"after a jump to {Stamp(110f)} the world isn't {journey.Sections[now].Label}'s");

            // A song without a sections file plays a journey from its lyrics; one without lyrics either, from the audio.
            var bare = Object.Instantiate(music.List[2]);
            bare.name = "Bare";
            bare.sections = null;
            music.List.Add(bare);
            music.Play(3, 80f, 1.505f);
            Run(1f);
            bool fromLyrics = journey.Sections.Source == SectionSource.Lyrics && journey.Sections.Count >= 5;
            // Its worlds change on a beat near each boundary (sections from lyrics start near a sung line), and never back.
            switches.Clear();
            RunUntil(70f);
            var derived = new SongSections();
            derived.CopyFrom(journey.Sections);
            int crossed = 0;
            for (int i = 1; i < derived.Count && derived[i].Start < 69f; i++)
            {
                crossed++;
                bool onBeat = switches.Any(s => s.world == journey.WorldOf(i) && s.time >= derived[i].Start - 0.36f && s.time <= derived[i].Start + 0.62f);
                if (!onBeat) problems.Add($"from the lyrics, no switch on a beat near {derived[i].Label} ({Stamp(derived[i].Start)}); switches at " +
                                          string.Join(", ", switches.Select(s => $"{s.time:0.00}")));
            }
            if (switches.Count != crossed) problems.Add($"from the lyrics, {switches.Count} switches for {crossed} boundaries");
            var silent = Object.Instantiate(bare);
            silent.name = "Silent";
            silent.lyrics = null;
            music.List.Add(silent);
            music.Play(4, 120f);
            Run(1f);
            bool fromAudio = journey.Sections.Source == SectionSource.Audio;
            if (!fromLyrics || !fromAudio) problems.Add($"songs without a sections file: from the lyrics {fromLyrics}, from the audio {fromAudio}");

            // Listening to the room: sections from the audio, a new one at least every minute, and never an end card.
            music.Listening = true;
            music.Clock = 0f;
            Run(130f);
            if (journey.Sections.Source != SectionSource.Audio || journey.Sections.Count < 2 || journey.CardOpen)
                problems.Add($"listening gave {journey.Sections.Count} sections from {journey.Sections.Source}, end card {journey.CardOpen}");

            Object.DestroyImmediate(bare);
            Object.DestroyImmediate(silent);

            // Each built-in song's journey, at its place in the song list (the menu's order), for the log.
            var playlist = new StandInJourney();
            foreach (var file in new[] { "GlassRoom", "NeonTide", "RainyWindow", "PaperBoat", "TurnItUpToLight", "TheWorldIsHumming", "SlowMotionGlow" })
                if (LoadSong(file) is SongAsset song) playlist.List.Add(song);
            var journeys = new StringBuilder();
            for (int s = 0; s < playlist.List.Count; s++)
            {
                playlist.Play(s, 120f);
                journey.Stop();
                journey.Begin();
                journey.Step(rig, manager, playlist, false, camera, dt);
                var sections = journey.Sections;
                journeys.Append($"\n  {playlist.List[s].title}: ");
                journeys.Append(string.Join(", ", Enumerable.Range(0, sections.Count).Select(i =>
                    $"{sections[i].Label} {Stamp(sections[i].Start)} {(journey.WorldOf(i) >= 0 ? worlds[journey.WorldOf(i)].DisplayName : "none")}")));
                var picked = Enumerable.Range(0, sections.Count).Select(journey.WorldOf).ToList();
                if (picked.Distinct().Count() != picked.Count) problems.Add($"a world repeats in {playlist.List[s].title}'s journey");
            }

            if (problems.Count > 0)
            {
                Debug.LogError("[Check] Journey run: " + string.Join("; ", problems) + ".");
                return false;
            }
            Debug.Log($"[Check] Journey run: Turn It Up to Light switched worlds at all {plan.Count - 1} boundaries within a frame (" +
                      string.Join(", ", Enumerable.Range(0, plan.Count).Select(i => $"{plan[i].Label}: {worlds[picks[i]].DisplayName}")) +
                      $"), none twice; the menu held the {plan[missed].Label} change until the next section; {songOneWaves} waves and one bloom; " +
                      "the end card showed as the music ended and moved on to the next song by itself after 10 s, whose worlds differ; picking a " +
                      "world turned the journey off with its caption and nothing changed after; the Journey bubble and a jump brought the right " +
                      "world in; a song without a sections file followed its lyrics (changing on a beat near each boundary), one without lyrics its " +
                      "audio, and listening never ended in a " +
                      "card; frames between changes allocate nothing. The built-in songs' journeys:" + journeys);
            return true;
        }
    }
}
