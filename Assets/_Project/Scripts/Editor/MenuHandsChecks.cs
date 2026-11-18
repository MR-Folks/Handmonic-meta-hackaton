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
        const float HandsDt = 1f / 30f;
        const float HandsSeconds = 37.6f;

        /// <summary>
        /// The worlds leave the hands to the open menu. World sends no OnPinch while it is open (with the song page
        /// showing too), lets go of a pinch held as it opens, and counts a pinch still held as it closes only once the
        /// hand has opened and pinched again, also when Meta's menu gesture closed it. And every world, played with by
        /// both hands through over half a minute of the demo song, acts for them (World.Acted) only while the menu is
        /// closed, and logs no error either way.
        /// </summary>
        static bool CheckMenuHands() => CheckMenuPinches() & CheckWorldsLeaveMenuHands();

        static bool CheckMenuPinches()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, 0f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var probe = new GameObject("Probe").AddComponent<HookProbe>();
            ProjectSetup.AssignMaterials(probe);
            var menu = MakeMenu(rig, camera, probe);
            try
            {
                const float dt = 0.1f;
                float time = 1f;
                // The right hand, its thumb `gap` metres from the index fingertip (pinch 1 at 1.5 cm, 0 at 5 cm), maybe in
                // Meta's menu gesture.
                void Step(float gap, int frames = 1, bool gesture = false)
                {
                    for (int i = 0; i < frames; i++)
                    {
                        var index = new Vector3(0.2f, 1.3f, 0.35f);
                        var tips = new[]
                        {
                            index + new Vector3(0f, -gap, 0f), index, index + new Vector3(-0.02f, -0.05f, -0.06f),
                            index + new Vector3(-0.03f, -0.055f, -0.065f), index + new Vector3(-0.04f, -0.06f, -0.07f)
                        };
                        rig.Push(false, true, new Pose(index + new Vector3(-0.02f, -0.04f, -0.09f), Quaternion.identity), Vector3.left, tips);
                        rig.Right.InSystemGesture = gesture;
                        probe.Step(rig, new MusicFrame { SongTime = time += dt }, camera, dt);
                    }
                }
                // Pinch 1, 0.71 (still held, not yet a new pinch) and 0.
                const float Closed = 0.008f, Held = 0.025f, Open = 0.06f;

                // With the menu closed a pinch counts as ever.
                Step(Open);
                Step(Closed, 2);
                bool before = probe.Hands.SequenceEqual(new[] { "pinch R" }) && probe.Pinching(false) && probe.Free(false) && !probe.SeesMenu;

                // The menu opens under the held pinch: it is let go once, and nothing counts while the menu is open, the
                // song page included, however the hand pinches.
                menu.Open(camera);
                Step(Closed, 3);
                bool letGo = probe.Hands.SequenceEqual(new[] { "pinch R", "release R" }) && probe.SeesMenu && !probe.Pinching(false) && !probe.Free(false);
                for (int i = 0; i < 3; i++) { Step(Open, 2); Step(Closed, 2); }
                menu.PreviewLibrary(new SongAsset[0], null, 0);
                for (int i = 0; i < 2; i++) { Step(Open, 2); Step(Closed, 2); }
                bool quiet = probe.Hands.Count == 2 && probe.SeesMenu;

                // The menu closes with the pinch still held: nothing, even as it eases and closes again, until the hand
                // has opened; then the next pinch counts.
                menu.Close();
                Step(Closed, 3);
                Step(Held, 2);
                Step(Closed, 2);
                bool held = probe.Hands.Count == 2 && !probe.SeesMenu && !probe.Pinching(false) && !probe.Free(false);
                Step(Open);
                bool freed = probe.Hands.Count == 2 && probe.Free(false);
                Step(Closed, 2);
                Step(Open);
                bool again = probe.Hands.Skip(2).SequenceEqual(new[] { "pinch R", "release R" });

                // Meta's menu gesture closes the menu with a pinch of its own: that pinch doesn't count once the palm
                // turns away either, until the hand opens and pinches again.
                menu.Open(camera);
                Step(Open, 2);
                Step(Closed, 2, gesture: true);
                menu.Close();
                Step(Closed, 2, gesture: true);
                Step(Closed, 3);
                bool gestured = probe.Hands.Count == 4 && !probe.Free(false);
                Step(Open);
                Step(Closed, 2);
                Step(Open);
                bool afterGesture = probe.Hands.Skip(4).SequenceEqual(new[] { "pinch R", "release R" });

                bool ok = before && letGo && quiet && held && freed && again && gestured && afterGesture;
                if (ok) Debug.Log("[Check] While the menu is open (song page too) World sends no OnPinch and lets go of a held pinch; a pinch held as it closes, also by Meta's menu gesture, counts only after the hand opens and pinches again.");
                else Debug.LogError($"[Check] Menu pinches: pinch with the menu closed {before}, let go as it opens {letGo}, none while open {quiet}, " +
                                    $"none while still held after closing {held}, free once opened {freed}, next pinch counts {again}, " +
                                    $"none after Meta's gesture closed it {gestured}, next pinch counts {afterGesture} ({string.Join(", ", probe.Hands)}).");
                return ok;
            }
            finally
            {
                RunMessage(menu, "OnDisable");
            }
        }

        /// <summary>
        /// Plays with every world twice, with the menu open and with it closed (HandsRoutine), and counts what each does
        /// for the hands: nothing at all while the menu is open, and something while it is closed for each world that can
        /// (one whose code calls Acted), so the check knows the hands really reached it.
        /// </summary>
        static bool CheckWorldsLeaveMenuHands()
        {
            // Each world's settings as saved in the main scene, so it plays as it does on the headset.
            var saved = new Dictionary<System.Type, string>();
            EditorSceneManager.OpenScene(ProjectSetup.MainScenePath, OpenSceneMode.Single);
            foreach (var w in Object.FindObjectsByType<World>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                saved[w.GetType()] = EditorJsonUtility.ToJson(w);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Room.Clear();
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var menu = MakeMenu(rig, camera, new GameObject("Probe").AddComponent<HookProbe>());
            bool lyricsWere = LyricsOption.On;
            LyricsOption.On = true;
            var errors = new List<string>();
            void Collect(string message, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
            }
            var failures = new List<string>();
            var acting = new List<string>();
            var lighting = new List<string>();
            int count = 0;
            Application.logMessageReceived += Collect;
            try
            {
                foreach (var type in WorldCatalog.Types().OrderBy(t => t.Name))
                {
                    count++;
                    int errorsBefore = errors.Count;
                    int litBefore = GestureFeedback.Lit;
                    var (open, openLast) = PlayWithHands(type, saved, rig, camera, menu, true);
                    int litOpen = GestureFeedback.Lit - litBefore;
                    litBefore = GestureFeedback.Lit;
                    var (closed, _) = PlayWithHands(type, saved, rig, camera, menu, false);
                    if (GestureFeedback.Lit > litBefore) lighting.Add(type.Name);
                    if (open > 0)
                        failures.Add($"{type.Name} acted for the hands {open} times while the menu was open (last: {openLast}). Gate that with MenuOpen, or PinchFree for its own pinches.");
                    if (litOpen > 0)
                        failures.Add($"{type.Name} lit the fingers {litOpen} times while the menu was open. Use World.Feedback (quiet while the menu is open), or gate GestureFeedback with MenuOpen.");
                    if (errors.Count > errorsBefore)
                        failures.Add($"{type.Name} logged errors while the hands played with it: {string.Join(" | ", errors.Skip(errorsBefore).Take(3))}");
                    if (!CallsActed(type)) continue;
                    if (closed > 0) acting.Add($"{type.Name} {closed}");
                    else failures.Add($"{type.Name} never acted for the hands with the menu closed either, so this check no longer reaches it and its menu gate goes untested. Make HandsRoutine reach it.");
                }
            }
            finally
            {
                Application.logMessageReceived -= Collect;
                LyricsOption.On = lyricsWere;
                RunMessage(menu, "OnDisable");
            }
            foreach (var failure in failures) Debug.LogError($"[Check] {failure}");
            if (failures.Count == 0)
                Debug.Log($"[Check] With the menu open none of the {count} worlds acted for hands holding, pinching, making fists, sweeping, pushing, " +
                          $"dipping, pointing up, catching and touching what it shows for {HandsSeconds:0.#} s, none lit a finger and none logged an error; " +
                          $"with it closed, {acting.Count} did ({string.Join(", ", acting)}), and {lighting.Count} lit the fingers ({string.Join(", ", lighting)}).");
            return failures.Count == 0;
        }

        /// <summary>
        /// One world, as saved in the main scene, switched to and played with by both hands (HandsRoutine) through the
        /// demo song from just before its first sung line, then switched away. Returns how often it acted for the hands.
        /// </summary>
        static (int actions, string last) PlayWithHands(System.Type type, Dictionary<System.Type, string> saved, HandRig rig, Camera camera,
            WorldMenu menu, bool menuOpen)
        {
            var go = new GameObject(type.Name);
            var world = (World)go.AddComponent(type);
            if (saved.TryGetValue(type, out var json)) EditorJsonUtility.FromJsonOverwrite(json, world);
            else ProjectSetup.AssignMaterials(world);
            if (menuOpen) menu.Open(camera);
            else menu.Close();
            var hands = new HandsRoutine(rig);
            var song = new SongFrames();
            try
            {
                RunMessage<World>(world, "OnEnable");
                for (int frame = 0; frame * HandsDt < HandsSeconds; frame++)
                {
                    float t = frame * HandsDt;
                    hands.Pose(t, world);
                    world.Step(rig, song.At(4f + t), camera, HandsDt);
                }
                RunMessage<World>(world, "OnDisable");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e is TargetInvocationException { InnerException: { } inner } ? inner : e);
            }
            var result = (world.HandActions, world.LastHandAction);
            Object.DestroyImmediate(go);
            return result;
        }

        /// <summary>
        /// A built menu in front of <paramref name="camera"/>, made WorldMenu.Instance as it is on the headset (Unity runs
        /// no OnEnable outside play mode), so worlds see it open. Undo with RunMessage(menu, "OnDisable").
        /// </summary>
        static WorldMenu MakeMenu(HandRig rig, Camera camera, World world)
        {
            var manager = new GameObject("Worlds").AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = world;
            mso.ApplyModifiedPropertiesWithoutUndo();
            var menu = new GameObject("WorldMenu").AddComponent<WorldMenu>();
            var wso = new SerializedObject(menu);
            wso.FindProperty("bubbleMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble");
            wso.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            wso.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            wso.ApplyModifiedPropertiesWithoutUndo();
            menu.Step(rig, manager, null, camera, 0f);
            RunMessage(menu, "OnEnable");
            return menu;
        }

        /// <summary>Runs one of the Unity messages (OnEnable, OnDisable) that <typeparamref name="T"/> declares, as play mode would.</summary>
        static void RunMessage<T>(T target, string message) where T : Component =>
            typeof(T).GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        /// <summary>Whether a world's own code calls World.Acted anywhere: its methods, and its lambdas and local functions.</summary>
        static bool CallsActed(System.Type type)
        {
            int token = typeof(World).GetMethod("Acted", BindingFlags.Instance | BindingFlags.NonPublic).MetadataToken;
            const BindingFlags Own = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var t in new[] { type }.Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)))
                foreach (var method in t.GetMethods(Own).Cast<MethodBase>().Concat(t.GetConstructors(Own)))
                {
                    var il = method.GetMethodBody()?.GetILAsByteArray();
                    if (il == null) continue;
                    // A call or callvirt instruction, then the called method's token.
                    for (int i = 0; i + 4 < il.Length; i++)
                        if ((il[i] == 0x28 || il[i] == 0x6F) && System.BitConverter.ToInt32(il, i + 1) == token) return true;
                }
            return false;
        }

        /// <summary>The demo song (Glass room) as MusicBus would give it: the beat at 96 bpm, the bands, the melody notes and the lyrics.</summary>
        sealed class SongFrames
        {
            const float BeatLength = 60f / 96f;
            static NoteTrack notes;
            static LyricsTrack lyrics;
            readonly List<NoteTrack.Note> upcoming = new List<NoteTrack.Note>();
            readonly float[] bands = new float[6];
            float lastBeat = -10f, lastTime = -1f;

            public MusicFrame At(float time)
            {
                notes ??= NoteTrack.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Audio/GlassRoom.notes").text);
                lyrics ??= LyricsTrack.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Audio/GlassRoom.lrc").text);
                bool beat = lastTime >= 0f && Mathf.Floor(time / BeatLength) > Mathf.Floor(lastTime / BeatLength);
                if (beat) lastBeat = Mathf.Floor(time / BeatLength) * BeatLength;
                lastTime = time;
                float age = time - lastBeat, kick = Mathf.Exp(-age * 7f);
                for (int b = 0; b < bands.Length; b++)
                    bands[b] = 0.3f + 0.12f * Mathf.Sin(time * (1.3f + b * 0.7f) + b) + kick * (b < 2 ? 0.5f : 0.15f);
                upcoming.Clear();
                notes.Between(time, time + 3f, upcoming);
                return new MusicFrame
                {
                    Bands = bands, Kick = kick, Beat = beat, SongTime = time, Upcoming = upcoming, Notes = notes, Lyrics = lyrics,
                    Lyric = lyrics.Lookup(time), BeatAge = age, BeatInterval = BeatLength
                };
            }
        }

        enum HandShape { Open, Pinch, Fist, Point }

        /// <summary>
        /// Both hands playing with a world, in turn: resting; holding something between the palms; pinching and drawing
        /// circles; fists; fingers sweeping from side to side through the space in front, where Air strings' strings and
        /// the menu are; palms pushed out at the walls; hovering over, dipping into and pinching above a table-height
        /// surface in front; pointing up at the ceiling; waiting palms up; a palm held down over the floor; and last,
        /// reaching for the things the world shows, one every half second: a fingertip through it, a pinch at it and the
        /// other palm under it (over it, low down), or a finger pointing at it overhead. The viewer stands at the origin,
        /// eyes 1.6 m up, facing +z.
        /// </summary>
        sealed class HandsRoutine
        {
            static readonly Vector3 Eye = new Vector3(0f, 1.6f, 0f);
            static readonly float[] SweepHeights = { -0.55f, -0.42f, -0.3f, -0.18f };
            readonly HandRig rig;
            readonly Vector3[] last = new Vector3[2];
            readonly bool[] seen = new bool[2];
            readonly List<Renderer> found = new List<Renderer>();
            Renderer target;
            int window = -1;

            public HandsRoutine(HandRig rig) => this.rig = rig;

            public void Pose(float t, World world)
            {
                Vector3 f = Vector3.forward, r = Vector3.right, u = Vector3.up;
                if (t < 2f) Rest();
                else if (t < 4.5f)
                {
                    // Palms facing each other at chest height, the gap between them breathing: holding a ball.
                    float s = t - 2f, gap = 0.13f + 0.07f * Mathf.Sin(s * 2.5f), y = 1.25f + 0.03f * Mathf.Sin(s * 1.7f);
                    Put(0, new Vector3(-gap, y, 0.38f), r, u, HandShape.Open);
                    Put(1, new Vector3(gap, y, 0.38f), -r, u, HandShape.Open);
                }
                else if (t < 7f)
                {
                    // Both hands pinch and draw circles, letting go for a moment halfway.
                    float s = t - 4.5f;
                    var shape = s > 1.2f && s < 1.35f ? HandShape.Open : HandShape.Pinch;
                    for (int side = 0; side < 2; side++)
                    {
                        float a = s * 5f + side * Mathf.PI;
                        var centre = new Vector3(side == 0 ? -0.16f : 0.16f, 1.3f, 0.42f);
                        Put(side, centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.09f, f, u, shape);
                    }
                }
                else if (t < 8.6f)
                {
                    Put(0, new Vector3(-0.15f, 1.25f, 0.36f), r, u, HandShape.Fist);
                    Put(1, new Vector3(0.15f, 1.25f, 0.36f), -r, u, HandShape.Fist);
                }
                else if (t < 13f)
                {
                    // Fingers forward, sweeping across 31 to 46 cm ahead, a little lower or higher on each pass.
                    float s = t - 8.6f;
                    int pass = (int)(s / 0.55f);
                    float p = s / 0.55f - pass;
                    for (int side = 0; side < 2; side++)
                    {
                        float x = Mathf.Lerp(-0.42f, 0.42f, (pass + side) % 2 == 0 ? p : 1f - p);
                        float y = Eye.y + SweepHeights[(pass + side * 2) % SweepHeights.Length];
                        float z = 0.4f + 0.06f * Mathf.Sin(s * 3.1f + side) - 0.09f;
                        Put(side, new Vector3(x, y, z), -u, f, HandShape.Open);
                    }
                }
                else if (t < 14.6f)
                {
                    // Palms pushed out quickly at the walls, again and again.
                    float s = (t - 13f) % 0.4f;
                    float z = s < 0.15f ? Mathf.Lerp(0.22f, 0.5f, s / 0.15f) : Mathf.Lerp(0.5f, 0.22f, (s - 0.15f) / 0.25f);
                    Put(0, new Vector3(-0.15f, 1.3f, z), f, u, HandShape.Open);
                    Put(1, new Vector3(0.15f, 1.3f, z), f, u, HandShape.Open);
                }
                else if (t < 18.2f)
                {
                    // Table height in front, 52 cm below the eyes (Sand table's plate, Sound pool's water): the palms hover
                    // over it, dip into it, then pinch above it.
                    float s = t - 14.6f;
                    const float Top = 1.08f;
                    for (int side = 0; side < 2; side++)
                    {
                        float x = (side == 0 ? -0.08f : 0.08f) + 0.12f * Mathf.Sin(s * 2.2f + side * 1.3f);
                        float z = 0.46f + 0.05f * Mathf.Sin(s * 1.6f + side);
                        if (s < 1.2f) Put(side, new Vector3(x, Top + 0.12f, z), -u, f, HandShape.Open);
                        else if (s < 2.4f) Put(side, new Vector3(x, Top - 0.02f, z), -u, f, HandShape.Open);
                        else Put(side, new Vector3(x, Top + 0.15f, z), -u, f, s < 2.5f ? HandShape.Open : HandShape.Pinch);
                    }
                }
                else if (t < 21.2f)
                {
                    // Pointing up at the ceiling, each finger working across its half of it row by row.
                    float s = t - 18.2f;
                    int row = Mathf.Min((int)(s / 0.5f), 5);
                    float p = Mathf.Clamp01(s / 0.5f - row);
                    for (int side = 0; side < 2; side++)
                    {
                        float across = side == 0 ? Mathf.Lerp(-75f, 10f, p) : Mathf.Lerp(-10f, 75f, p);
                        PointAt(side, Shoulder(side) + Quaternion.Euler(-30f - row * 10f, across, 0f) * f);
                    }
                }
                else if (t < 23.4f)
                {
                    // Palms up, waiting for whatever falls into them.
                    float s = t - 21.2f;
                    for (int side = 0; side < 2; side++)
                        Put(side, new Vector3((side == 0 ? -0.15f : 0.15f) + 0.12f * Mathf.Sin(s * 0.9f + side), 1.2f, 0.5f + 0.1f * Mathf.Sin(s * 1.3f + side)),
                            u, f, HandShape.Open);
                }
                else if (t < 25.6f)
                {
                    // A palm held down over the floor ahead, each hand working across its half of it row by row.
                    float s = t - 23.4f;
                    int pass = (int)(s / 0.55f);
                    float p = s / 0.55f - pass;
                    for (int side = 0; side < 2; side++)
                    {
                        float x = Mathf.Lerp(side == 0 ? -0.8f : 0f, side == 0 ? 0f : 0.8f, pass % 2 == 0 ? p : 1f - p);
                        Put(side, new Vector3(x, 0.6f, 0.55f + 0.27f * (pass % 4)), -u, f, HandShape.Open);
                    }
                }
                else Reach(t - 25.6f, world);
            }

            void Rest()
            {
                Put(0, new Vector3(-0.22f, 0.95f, 0.18f), Vector3.right, Vector3.down, HandShape.Open);
                Put(1, new Vector3(0.22f, 0.95f, 0.18f), Vector3.left, Vector3.down, HandShape.Open);
            }

            /// <summary>The next thing the world shows, every half second, and the hands on it.</summary>
            void Reach(float s, World world)
            {
                int w = (int)(s / 0.5f);
                float p = s - w * 0.5f;
                if (w != window || target == null || !target.gameObject.activeInHierarchy)
                {
                    window = w;
                    target = Pick(world, w);
                }
                if (target == null)
                {
                    Rest();
                    return;
                }
                var at = target.bounds.center;
                if (at.y > Eye.y + 0.3f)
                {
                    // Overhead: point at it.
                    PointAt(1, at);
                    Put(0, new Vector3(-0.22f, 0.95f, 0.18f), Vector3.right, Vector3.down, HandShape.Open);
                    return;
                }
                var along = Vector3.ProjectOnPlane(at - Eye, Vector3.up);
                along = along.sqrMagnitude > 1e-4f ? along.normalized : Vector3.forward;
                if (p < 0.3f)
                {
                    // The index fingertip goes to and fro through it, across and along.
                    var wiggle = Vector3.Cross(Vector3.up, along) * (0.035f * Mathf.Sin(p * 40f)) + along * (0.05f * Mathf.Sin(p * 25f));
                    Put(1, at + wiggle, Vector3.down, along, HandShape.Point);
                }
                else Put(1, at, Vector3.down, along, p < 0.36f ? HandShape.Open : HandShape.Pinch);
                // The other palm just under it, turned up to catch it, or over it, turned down, when it is low.
                if (at.y < 0.8f) Put(0, at + Vector3.up * 0.15f, Vector3.down, along, HandShape.Open);
                else Put(0, at - Vector3.up * 0.03f, Vector3.up, along, HandShape.Open);
            }

            /// <summary>One of the small things the world shows within a few metres (not its particles), varied by <paramref name="w"/>.</summary>
            Renderer Pick(World world, int w)
            {
                found.Clear();
                world.GetComponentsInChildren(false, found);
                found.RemoveAll(x => !x.enabled || x is ParticleSystemRenderer || x.bounds.size.sqrMagnitude < 1e-6f || x.bounds.size.magnitude > 0.8f
                    || (x.bounds.center - Eye).magnitude < 0.2f || (x.bounds.center - Eye).magnitude > 3.5f);
                return found.Count > 0 ? found[w * 7 % found.Count] : null;
            }

            static Vector3 Shoulder(int side) => Eye + Vector3.down * 0.17f + Vector3.right * (side == 0 ? -0.17f : 0.17f);

            /// <summary>The index finger pointing from the shoulder at <paramref name="at"/>, its tip 55 cm out.</summary>
            void PointAt(int side, Vector3 at)
            {
                var dir = (at - Shoulder(side)).normalized;
                Put(side, Shoulder(side) + dir * 0.55f, Vector3.ProjectOnPlane(Vector3.down, dir), dir, HandShape.Point);
            }

            /// <summary>
            /// A hand in <paramref name="shape"/> with its working point at <paramref name="point"/>: the palm (open, fist),
            /// the pinch, or the index fingertip (pointing). Its velocity is how far the palm moved since the last frame.
            /// </summary>
            void Put(int side, Vector3 point, Vector3 normal, Vector3 along, HandShape shape)
            {
                bool left = side == 0;
                normal = normal.normalized;
                along = Vector3.ProjectOnPlane(along, normal).normalized;
                var thumb = Vector3.Cross(normal, along) * (left ? -1f : 1f);
                var palm = point - shape switch
                {
                    HandShape.Pinch => along * 0.075f + thumb * 0.03f + normal * 0.029f,
                    HandShape.Point => along * 0.1f + thumb * 0.02f,
                    _ => Vector3.zero
                };
                var velocity = seen[side] ? (palm - last[side]) / HandsDt : Vector3.zero;
                last[side] = palm;
                seen[side] = true;
                PutHand(rig, left, palm, normal, along, shape, velocity);
            }
        }

        /// <summary>
        /// Feeds HandRig a made-up hand: the palm at <paramref name="palm"/>, its inside facing <paramref name="normal"/>,
        /// the fingers pointing <paramref name="along"/>, held open, pinching (thumb and index 8 mm apart), in a fist or
        /// pointing with the index finger. HandRig works its pinch and fist out from the fingertips, as on the headset.
        /// </summary>
        static void PutHand(HandRig rig, bool left, Vector3 palm, Vector3 normal, Vector3 along, HandShape shape, Vector3 velocity)
        {
            normal = normal.normalized;
            along = Vector3.ProjectOnPlane(along, normal).normalized;
            var thumbSide = Vector3.Cross(normal, along) * (left ? -1f : 1f);
            Vector3 At(float a, float t, float n) => palm + along * a + thumbSide * t + normal * n;
            var tips = new Vector3[5];
            switch (shape)
            {
                case HandShape.Pinch:
                    tips[0] = At(0.075f, 0.03f, 0.033f);
                    tips[1] = At(0.075f, 0.03f, 0.025f);
                    tips[2] = At(0.095f, 0.005f, 0f);
                    tips[3] = At(0.09f, -0.015f, 0f);
                    tips[4] = At(0.075f, -0.035f, 0f);
                    break;
                case HandShape.Fist:
                    tips[0] = At(0.025f, 0.06f, 0.03f);
                    tips[1] = At(0.035f, 0.02f, 0.025f);
                    tips[2] = At(0.035f, 0f, 0.028f);
                    tips[3] = At(0.033f, -0.018f, 0.026f);
                    tips[4] = At(0.03f, -0.033f, 0.022f);
                    break;
                case HandShape.Point:
                    tips[0] = At(0.04f, 0.045f, 0.03f);
                    tips[1] = At(0.1f, 0.02f, 0f);
                    tips[2] = At(0.05f, 0f, 0.03f);
                    tips[3] = At(0.048f, -0.017f, 0.03f);
                    tips[4] = At(0.045f, -0.032f, 0.026f);
                    break;
                default:
                    tips[0] = At(0.03f, 0.06f, 0.02f);
                    tips[1] = At(0.09f, 0.025f, 0f);
                    tips[2] = At(0.095f, 0.005f, 0f);
                    tips[3] = At(0.09f, -0.015f, 0f);
                    tips[4] = At(0.075f, -0.035f, 0f);
                    break;
            }
            rig.Push(left, true, new Pose(palm, Quaternion.LookRotation(along, -normal)), normal, tips);
            var hand = rig.Get(left);
            hand.Velocity = velocity;
            hand.InSystemGesture = false;
        }
    }
}
