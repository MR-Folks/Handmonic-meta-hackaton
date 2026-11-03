using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        /// <summary>
        /// World calls each hook when it should: OnBuild once; OnPlace when the world starts and again after you move;
        /// OnBeat on beats; OnNote as melody notes start, not again when the song jumps back until they come round;
        /// OnWord once per sung word (and in lyric worlds the title's words when there are no lyrics); OnPinch and
        /// OnRelease once per pinch, steady near the threshold; OnStep every frame that has a camera; and the sung
        /// words a world adds show.
        /// </summary>
        static bool CheckWorldHooks()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            bool lyricsWere = LyricsOption.On;
            LyricsOption.On = true;
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, 0f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var probe = new GameObject("Probe").AddComponent<HookProbe>();
            var so = new SerializedObject(probe);
            so.FindProperty("letteringMaterial").objectReferenceValue = ProjectSetup.WorldMaterial(typeof(HookProbe), "letteringMaterial");
            so.ApplyModifiedPropertiesWithoutUndo();

            var notes = NoteTrack.Parse("1.0 60 0.5\n1.5 62 0.5\n3.0 64 0.5");
            var lyrics = LyricsTrack.Parse("[00:01.00]hello world\n[00:04.00]again");
            const float dt = 0.1f;
            MusicFrame Frame(float time, bool beat = false, int line = -1, int word = -1) => new MusicFrame
            {
                SongTime = time,
                Beat = beat,
                Notes = notes,
                Lyrics = lyrics,
                Lyric = new LyricsTrack.State(line, word)
            };
            // The right hand, its thumb `gap` metres from the index fingertip (pinch 1 at 1.5 cm, 0 at 5 cm).
            void Hand(float gap)
            {
                var index = new Vector3(0.2f, 1.3f, 0.35f);
                var tips = new[]
                {
                    index + new Vector3(0f, -gap, 0f), index, index + new Vector3(-0.02f, -0.05f, -0.06f),
                    index + new Vector3(-0.03f, -0.055f, -0.065f), index + new Vector3(-0.04f, -0.06f, -0.07f)
                };
                rig.Push(false, true, new Pose(index + new Vector3(-0.02f, -0.04f, -0.09f), Quaternion.identity), Vector3.left, tips);
            }

            Hand(0.06f);
            probe.Build();
            probe.Build();
            probe.Step(rig, Frame(0.5f), camera, dt);
            bool built = probe.Built == 1 && probe.Placed == 1 && probe.Steps == 1 && probe.Fade > 0f && probe.Fade < 1f;

            probe.Step(rig, Frame(0.6f, beat: true), camera, dt);
            probe.Step(rig, Frame(0.7f), camera, dt);
            probe.Step(rig, Frame(0.8f, beat: true), camera, dt);
            bool beats = probe.BeatsSeen == 2;

            // Notes at 1.0 (60) and 1.5 (62); the song jumps back to 0.2 and comes round to 1.0 again.
            probe.Step(rig, Frame(1.2f), camera, dt);
            probe.Step(rig, Frame(1.6f), camera, dt);
            probe.Step(rig, Frame(0.2f), camera, dt);
            probe.Step(rig, Frame(1.1f), camera, dt);
            bool melody = probe.Notes.SequenceEqual(new[] { 60, 62, 60 });

            // "hello" starts the line, holds for a frame, then "world"; then a gap between lines.
            probe.Step(rig, Frame(1.2f, line: 0, word: 0), camera, dt);
            probe.Step(rig, Frame(1.3f, line: 0, word: 0), camera, dt);
            probe.Step(rig, Frame(1.6f, line: 0, word: 1), camera, dt);
            probe.Step(rig, Frame(2.5f), camera, dt);
            bool words = probe.Words.SequenceEqual(new[] { "/hello", "world" }) && probe.ShownWords == 2;

            // A pinch closes, holds, eases to just under the start strength (still held), then opens.
            foreach (var gap in new[] { 0.008f, 0.008f, 0.025f, 0.06f })
            {
                Hand(gap);
                probe.Step(rig, Frame(2.6f), camera, dt);
            }
            bool pinches = probe.Hands.SequenceEqual(new[] { "pinch R", "release R" });

            // Walking a metre away places the world again once you have stayed there a moment and a half.
            camera.transform.position += new Vector3(1f, 0f, 0f);
            for (int i = 0; i < 20; i++) probe.Step(rig, Frame(3f + i * dt), camera, dt);
            bool replaced = probe.Placed == 2;

            int steps = probe.Steps;
            probe.Step(rig, Frame(5f), null, dt);
            bool noView = probe.Steps == steps;

            // A lyric world without lyrics (and no MusicBus) steps through the title, "Music", a word every 2 s.
            var lyricProbe = new GameObject("Lyric Probe").AddComponent<HookProbe>();
            lyricProbe.Lyric = true;
            for (int i = 0; i < 30; i++) lyricProbe.Step(rig, new MusicFrame { SongTime = i * 0.2f }, camera, dt);
            bool title = lyricProbe.Words.Count == 3 && lyricProbe.Words.All(w => w == "/Music");

            LyricsOption.On = lyricsWere;
            bool ok = built && beats && melody && words && pinches && replaced && noView && title;
            if (ok) Debug.Log("[Check] World calls its hooks in turn: build once, place and place again after moving, beats, melody notes, sung words, pinches, and a fade-in.");
            else Debug.LogError($"[Check] World hooks: built and placed once {built}, beats {beats} ({probe.BeatsSeen}), melody notes {melody} ({string.Join(" ", probe.Notes)}), " +
                                $"sung words {words} ({string.Join(" ", probe.Words)}; {probe.ShownWords} shown), pinches {pinches} ({string.Join(", ", probe.Hands)}), " +
                                $"placed again after moving {replaced} ({probe.Placed}), no frame without a camera {noView}, title words {title} ({string.Join(" ", lyricProbe.Words)}).");
            return ok;
        }

        /// <summary>
        /// Every World class is ready for the menu: it has a name and its own MenuOrder, its Material fields name real
        /// materials or shaders, it declares none of the Unity messages World runs it with (OnEnable, OnDisable,
        /// LateUpdate...), and the main scene holds it, in menu order, with its materials set.
        /// </summary>
        static bool CheckWorldCatalog()
        {
            bool ok = true;
            var holder = new GameObject("Catalog");
            var worlds = WorldCatalog.MakePreviews(holder.transform);
            var expected = worlds.Select(w => w.GetType()).ToList();
            var orders = new Dictionary<int, string>();
            const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var world in worlds)
            {
                var type = world.GetType();
                if (string.IsNullOrWhiteSpace(world.DisplayName))
                {
                    Debug.LogError($"[Check] {type.Name} has no DisplayName.");
                    ok = false;
                }
                if (orders.TryGetValue(world.MenuOrder, out var other))
                {
                    Debug.LogError($"[Check] {type.Name} and {other} share MenuOrder {world.MenuOrder}; give each world its own place in the menu.");
                    ok = false;
                }
                else orders[world.MenuOrder] = type.Name;
                foreach (var message in new[] { "Awake", "Start", "OnEnable", "OnDisable", "Update", "LateUpdate" })
                {
                    if (type.GetMethod(message, Own, null, System.Type.EmptyTypes, null) == null) continue;
                    Debug.LogError($"[Check] {type.Name} declares {message}(); World runs worlds through its hooks (OnBuild, OnEnter, OnExit, OnStep), and a {message} of its own would skip them.");
                    ok = false;
                }
                foreach (var field in WorldCatalog.MaterialFields(type))
                {
                    var name = WorldCatalog.MaterialName(type, field.Name);
                    if (name == "Sky" || AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Materials/{name}.mat") != null
                        || Shader.Find($"MusicInYourHands/{name}") != null) continue;
                    Debug.LogError($"[Check] {type.Name}.{field.Name} would get Materials/{name}.mat, but there is no such material or shader MusicInYourHands/{name}. Name the field after its material or add [WorldMaterial(\"...\")].");
                    ok = false;
                }
            }
            Object.DestroyImmediate(holder);

            // A world outside the game's own code (say, made in an Editor folder) would never reach the menu.
            foreach (var stray in TypeCache.GetTypesDerivedFrom<World>())
                if (!stray.IsAbstract && stray.Assembly != typeof(World).Assembly)
                {
                    Debug.LogError($"[Check] {stray.Name} is in {stray.Assembly.GetName().Name}, outside the game's code, so it can't be in the menu. Move it to Assets/_Project/Scripts/Runtime/Worlds.");
                    ok = false;
                }

            // The main scene holds every world, in menu order, with its materials.
            var scene = EditorSceneManager.OpenScene(ProjectSetup.MainScenePath, OpenSceneMode.Additive);
            try
            {
                var manager = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<WorldManager>(true)).FirstOrDefault(m => m != null);
                var inScene = manager != null ? manager.Worlds.Select(w => w != null ? w.GetType() : null).ToList() : new List<System.Type>();
                if (!inScene.SequenceEqual(expected))
                {
                    Debug.LogError($"[Check] Main.unity's worlds ({string.Join(", ", inScene.Select(t => t != null ? t.Name : "missing"))}) don't match the code's, " +
                                   $"in menu order ({string.Join(", ", expected.Select(t => t.Name))}). Run Music In Your Hands > Update Worlds in Main Scene.");
                    ok = false;
                }
                else
                    foreach (var world in manager.Worlds)
                        foreach (var field in WorldCatalog.MaterialFields(world.GetType()))
                        {
                            if (field.GetValue(world) as Material != null) continue;
                            Debug.LogError($"[Check] {world.GetType().Name}.{field.Name} has no material in Main.unity. Run Music In Your Hands > Update Worlds in Main Scene.");
                            ok = false;
                        }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            if (ok) Debug.Log($"[Check] {expected.Count} worlds, each with a name, its own MenuOrder and real materials, all in Main.unity in menu order.");
            return ok;
        }
    }
}
