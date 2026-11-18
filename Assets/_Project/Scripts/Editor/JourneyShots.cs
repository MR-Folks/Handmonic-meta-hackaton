using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        // The worlds a journey may play, shot hands-free (jf-<class>.png): the hands resting low, out of view, as a
        // judge sitting back would hold them. The journey's defaults favour the ones that look best like this.
        static readonly string[] JourneyWorlds =
        {
            "BubbleNotes", "SolarSystem", "Lightning", "FlowerDance", "SoundPool", "AirStrings", "LivingSkin",
            "HeartbeatOrb", "FingerFrame", "ShadowPlay", "WaveformRibbon", "LightTrails"
        };

        static Vector3 HandsInLap(string side) => new Vector3(side == "L" ? -0.04f : 0.04f, -0.46f, 0.06f);

        /// <summary>
        /// Journey mode's screenshots, through one sung song (Turn It Up to Light) in the furnished stand-in room, the hands
        /// resting low: a frame in each section showing its world and the song map (journey-N-section.png), the wave
        /// spreading across the floor and up the walls as the chorus comes in (journey-wave.png, journey-wave-walls.png),
        /// the finale's bloom (journey-bloom.png) and the end card (journey-end.png). Also each world a journey may play,
        /// hands-free (jf-class.png).
        /// </summary>
        static void JourneyShots()
        {
#if META_XR_CORE
            foreach (var name in JourneyWorlds)
            {
                var type = WorldCatalog.Types().FirstOrDefault(t => t.Name == name);
                if (type != null) Shot(type, $"jf-{name}", HandPose.PalmsDown, 8f, handOffset: HandsInLap);
            }

            var song = LoadSong("TurnItUpToLight");
            if (song == null || song.sections == null) return;
            var plan = new SongSections();
            SongSections.Parse(song.sections.text, plan);
            plan.Tidy(song.clip != null ? song.clip.length : 148f, 7f);
            var names = new List<string>();
            for (int i = 0; i < plan.Count; i++) names.Add($"journey-{i + 1}-{plan[i].Label.ToLowerInvariant().Replace(' ', '-')}");
            names.AddRange(new[] { "journey-wave", "journey-wave-walls", "journey-bloom", "journey-end" });
            if (!names.Any(Wanted)) return;

            BodyRig.Clear();
            var camera = StageRoom(10f, scanned: true);
            // A new scene unloads the assets nothing in it uses, so load the songs again now.
            song = LoadSong("TurnItUpToLight");
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var hands = PlaceHands(camera, HandPose.PalmsDown, rig, HandsInLap);

            // Every world with its settings from the main scene, as WorldManager holds them there.
            var managerGo = new GameObject("Worlds");
            var worlds = new List<World>();
            foreach (var type in WorldCatalog.Types())
            {
                var go = new GameObject(type.Name);
                go.transform.SetParent(managerGo.transform, false);
                var world = (World)go.AddComponent(type);
                if (SceneSettings.TryGetValue(type, out var json)) EditorJsonUtility.FromJsonOverwrite(json, world);
                else ProjectSetup.AssignMaterials(world);
                go.SetActive(false);
                worlds.Add(world);
            }
            worlds.Sort(WorldCatalog.Compare);
            var manager = managerGo.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var property = mso.FindProperty("worlds");
            property.arraySize = worlds.Count;
            for (int i = 0; i < worlds.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            // Outside play mode a world's OnEnable doesn't run, so set its hands the way it would.
            manager.WorldChanged += w =>
            {
                HandSkin.Theme = manager.Worlds[w].Theme;
                HandSkin.FocusStrength = 0f;
                HandSkin.Charge[0] = HandSkin.Charge[1] = 0f;
            };

            var journey = new GameObject("Journey").AddComponent<JourneyMode>();
            SetSerialized(journey, "bubbleMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble"));
            SetSerialized(journey, "glowMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow"));
            SetSerialized(journey, "lineMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line"));
            SetSerialized(journey, "waveMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.JourneyWaveMaterialPath, "MusicInYourHands/JourneyWave"));
            var music = new StandInJourney();
            music.List.Add(song);
            var next = LoadSong("TheWorldIsHumming");
            if (next != null) music.List.Add(next);
            music.Play(0, 118f, 0.02f);

            // The worlds hear the stand-in's beat, bands that swell on it, the song's lyrics and its sung words as notes.
            var lyrics = LyricsTrack.Parse(song.lyrics != null ? song.lyrics.text : null);
            var notes = NoteTrack.FromLyrics(lyrics, song.lyricsOffset);
            var upcoming = new List<NoteTrack.Note>();
            var bands = new float[6];
            MusicFrame Frame()
            {
                float time = music.Clock, k = music.Kick;
                bands[0] = 0.15f + k * 0.85f;
                bands[1] = 0.35f + k * 0.55f;
                bands[2] = 0.45f + 0.15f * Mathf.Sin(time * 2.1f);
                bands[3] = 0.45f + 0.15f * Mathf.Sin(time * 3.3f + 1f);
                bands[4] = 0.35f + 0.2f * Mathf.Sin(time * 4.1f + 2f);
                bands[5] = 0.3f + 0.25f * Mathf.Abs(Mathf.Sin(time * Mathf.PI / (music.Interval * 0.5f)));
                upcoming.Clear();
                notes.Between(time, time + 3f, upcoming);
                return new MusicFrame
                {
                    Bands = bands, Kick = k, Beat = music.Heard, SongTime = time, Upcoming = upcoming, Lyrics = lyrics,
                    Lyric = lyrics.Lookup(time + song.lyricsOffset), Notes = notes, BeatAge = music.BeatAge, BeatInterval = music.Interval
                };
            }
            const float dt = 1f / 30f;
            void Play(float seconds)
            {
                for (float t = 0f; t < seconds; t += dt)
                {
                    music.Advance(dt);
                    journey.Step(rig, manager, music, false, camera, dt);
                    var world = manager.Current >= 0 ? manager.Worlds[manager.Current] : null;
                    if (world == null) continue;
                    world.Step(rig, Frame(), camera, dt);
                    world.SimulateParticles(dt);
                    journey.Wave.Simulate(dt);
                }
                foreach (var hand in hands)
                {
                    hand.Root.GetComponent<HandSkin>().Apply();
                    SetBeat(hand.Skin, music.BeatAge, music.Kick);
                }
            }
            void Save(string name)
            {
                if (Wanted(name)) File.WriteAllBytes($"{ShotFolder}/{name}.png", Render(camera, 1600, 900).EncodeToPNG());
            }
            void Look(float pitch) => camera.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);

            journey.Begin();
            // A frame in each section, a few seconds after its world came in, while the song map still shows.
            for (int i = 0; i < plan.Count; i++)
            {
                if (!Wanted(names[i])) continue;
                if (i > 0) music.Seek(plan[i].Start + 1.5f);
                Look(10f);
                Play(3.4f);
                Save(names[i]);
            }

            // The wave as the first chorus comes in: leaving the feet across the floor, then climbing the walls.
            if (Wanted("journey-wave"))
            {
                float chorus = plan.Sections.First(s => s.Role == SectionRole.Chorus).Start;
                music.Seek(chorus - 4f);
                Look(24f);
                Play(4f + 0.15f);
                Save("journey-wave");
                Look(8f);
                Play(0.45f);
                Save("journey-wave-walls");
            }

            // The finale's bloom on the final chorus's last downbeat, then the end card as the music ends.
            if (Wanted("journey-bloom") || Wanted("journey-end"))
            {
                music.Seek(plan.FinaleTime - 3f);
                Look(10f);
                Play(3f + 0.8f);
                Save("journey-bloom");
                Play(plan.MusicEnd - music.Clock + 0.2f);
                Play(1.6f);
                Look(14f);
                Play(0.1f);
                Save("journey-end");
            }
            Debug.Log($"[Shots] Journey: {journey.Sections.Count} sections of {song.title} from its {journey.Sections.Source} sections, " +
                      $"{journey.Wave.Waves} waves, {journey.Wave.Blooms} bloom, end card {journey.CardOpen}.");
#endif
        }
    }
}
