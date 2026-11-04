using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Choice = MusicInYourHands.WorldManager.Choice;
using Space = MusicInYourHands.World.Space;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        /// <summary>
        /// Every world says which parts of the room and body it uses (World.Spaces), or is on its own. The worlds still
        /// on their own are listed as a warning, not a failure; a world that says it uses nothing at all fails, since
        /// every world shows something somewhere.
        /// </summary>
        static bool CheckWorldSpaces()
        {
            var holder = new GameObject("Spaces");
            var worlds = WorldCatalog.MakePreviews(holder.transform);
            bool ok = true;
            var alone = new List<string>();
            foreach (var world in worlds)
            {
                if (world.Spaces == Space.All) alone.Add(world.DisplayName);
                else if (world.Spaces == Space.None)
                {
                    Debug.LogError($"[Check] {world.GetType().Name} says it uses no space at all. Give it the parts of the room and body it uses (World.Space), or leave Spaces out to keep it on its own.");
                    ok = false;
                }
            }
            int pairs = 0;
            for (int a = 0; a < worlds.Length; a++)
                for (int b = a + 1; b < worlds.Length; b++)
                    if (worlds[a].CombinesWith(worlds[b])) pairs++;
            UnityEngine.Object.DestroyImmediate(holder);
            if (alone.Count > 0)
                Debug.LogWarning($"[Check] {alone.Count} worlds don't say which spaces they use yet, so each is on its own: {string.Join(", ", alone)}.");
            if (ok) Debug.Log($"[Check] {worlds.Length - alone.Count} of {worlds.Length} worlds say which spaces they use; {pairs} pairs of worlds can be on together.");
            return ok;
        }

        /// <summary>
        /// WorldManager puts worlds together as the menu promises, tried with test worlds (HookProbe): a world that fits
        /// beside those that are on is added; one that clashes swaps out the ones it clashes with; two lyric worlds
        /// clash; past the most at once, the world chosen longest ago gives way; choosing a world that is on turns it
        /// off, but never the last one; a world on its own replaces them all. OnEnter and OnExit run once for each turn
        /// on and off. Only the world that shows the words gets OnWord (a lyric world first, else the one chosen last),
        /// and the words move on when it goes. Only the world that dresses the hands (the one using the Hands space,
        /// else the one chosen last) changes them: what the others' hooks do to them is undone.
        /// </summary>
        static bool CheckCombining()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            bool lyricsWere = LyricsOption.On;
            LyricsOption.On = true;
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, 0f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            // A visible left hand the test worlds nudge, as Meta's hands have.
            var anchor = new GameObject("Left Hand Anchor").transform;
            rig.Left.VisualRoot = new GameObject("Left Hand").transform;
            rig.Left.VisualRoot.SetParent(anchor, false);

            var holder = new GameObject("Worlds");
            var lettering = ProjectSetup.WorldMaterial(typeof(HookProbe), "letteringMaterial");
            HookProbe Probe(string name, Space uses, float mark, bool lyric = false)
            {
                var go = new GameObject(name);
                go.transform.SetParent(holder.transform, false);
                go.SetActive(false);
                var probe = go.AddComponent<HookProbe>();
                probe.Uses = uses;
                probe.Mark = mark;
                probe.Lyric = lyric;
                var so = new SerializedObject(probe);
                so.FindProperty("letteringMaterial").objectReferenceValue = lettering;
                so.ApplyModifiedPropertiesWithoutUndo();
                return probe;
            }
            var probes = new[]
            {
                Probe("Floor", Space.Ground, 0.1f),                        // 0
                Probe("Walls", Space.Walls, 0.2f),                         // 1
                Probe("Sky", Space.Ceiling, 0.3f, lyric: true),            // 2
                Probe("Hands", Space.Hands | Space.Pinch, 0.4f),           // 3
                Probe("Garden", Space.Ground, 0.5f),                       // 4: clashes with Floor
                Probe("Alone", Space.All, 0.6f),                           // 5: on its own
                Probe("Poem", Space.AroundYou, 0.7f, lyric: true)          // 6: clashes with Sky, both lyric
            };
            var manager = holder.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = probes.Length;
            for (int i = 0; i < probes.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = probes[i];
            mso.FindProperty("maxWorlds").intValue = 3;
            mso.ApplyModifiedPropertiesWithoutUndo();

            var problems = new List<string>();
            void Expect(bool ok, string what)
            {
                if (!ok) problems.Add(what);
            }
            string On() => string.Join(" ", manager.Active.Select(i => probes[i].name));
            var off = new List<int>();
            string Off() => string.Join(" ", off.Select(i => probes[i].name));
            // Each world's OnEnter and OnExit ran as often as it was turned on and off, and it is on if the manager says so.
            var turnedOn = new int[probes.Length];
            void Counted()
            {
                for (int i = 0; i < probes.Length; i++)
                {
                    bool on = manager.IsOn(i);
                    Expect(probes[i].Entered == turnedOn[i] && probes[i].Exited == turnedOn[i] - (on ? 1 : 0) && probes[i].IsOn == on
                           && probes[i].gameObject.activeSelf == on,
                        $"{probes[i].name} entered {probes[i].Entered} and exited {probes[i].Exited} times after being turned on {turnedOn[i]} times (on now: {on})");
                }
            }
            void Choose(int index, Choice expected, string after, string leaving = "")
            {
                var planned = manager.Plan(index, off);
                string plannedOff = Off();
                var done = manager.Choose(index);
                if (done == Choice.Add || done == Choice.Swap) turnedOn[index]++;
                Expect(planned == expected && done == expected && plannedOff == leaving && On() == after,
                    $"choosing {probes[index].name}: planned {planned} (turning off \"{plannedOff}\"), did {done}, now on \"{On()}\"; " +
                    $"wanted {expected} (turning off \"{leaving}\"), then \"{after}\"");
                Counted();
            }

            manager.Switch(0);
            turnedOn[0]++;
            Expect(On() == "Floor" && manager.Current == 0, $"starting with Floor alone left \"{On()}\" on");
            Counted();
            Choose(1, Choice.Add, "Floor Walls");
            Choose(4, Choice.Swap, "Walls Garden", "Floor");
            Choose(1, Choice.TurnOff, "Garden");
            Choose(4, Choice.StayOn, "Garden");
            Choose(1, Choice.Add, "Garden Walls");
            Choose(2, Choice.Add, "Garden Walls Sky");
            Choose(3, Choice.Swap, "Walls Sky Hands", "Garden");
            Choose(6, Choice.Swap, "Walls Hands Poem", "Sky");
            Choose(5, Choice.Swap, "Alone", "Walls Hands Poem");
            Choose(0, Choice.Swap, "Floor", "Alone");
            Expect(manager.Current == 0, "the world chosen last isn't Current");

            // The words: two sound worlds that show words, then a lyric world.
            var notes = NoteTrack.Parse("");
            var lyrics = LyricsTrack.Parse("[00:01.00]one two three four five six\n");
            const float dt = 0.1f;
            float time = 1f;
            void Frames(int word)
            {
                var music = new MusicFrame { SongTime = time, Notes = notes, Lyrics = lyrics, Lyric = new LyricsTrack.State(0, word) };
                time += dt;
                foreach (int i in manager.Active) probes[i].Step(rig, music, camera, dt);
            }
            Choose(1, Choice.Add, "Floor Walls");
            Expect(manager.WordsWorld == 1, $"the words went to {Name(manager.WordsWorld)}, not to Walls, the one chosen last");
            Frames(0);
            Frames(1);
            Expect(probes[1].Words.SequenceEqual(new[] { "/one", "two" }) && probes[0].Words.Count == 0 && probes[1].WordsShowing == 2 && probes[0].WordsShowing == 0,
                $"with Floor and Walls on, Walls got \"{string.Join(" ", probes[1].Words)}\" and Floor \"{string.Join(" ", probes[0].Words)}\"");
            Choose(2, Choice.Add, "Floor Walls Sky");
            Expect(manager.WordsWorld == 2 && probes[1].WordsShowing == 0, $"a lyric world on didn't take the words (they are with {Name(manager.WordsWorld)}, Walls shows {probes[1].WordsShowing})");
            Frames(2);
            Expect(probes[2].Words.SequenceEqual(new[] { "/three" }) && probes[1].Words.Count == 2, $"with Sky on, Sky got \"{string.Join(" ", probes[2].Words)}\" and Walls \"{string.Join(" ", probes[1].Words)}\"");
            Choose(2, Choice.TurnOff, "Floor Walls");
            Expect(manager.WordsWorld == 1, $"the words went to {Name(manager.WordsWorld)} when Sky went, not back to Walls");
            Frames(3);
            Frames(4);
            // Walls picks the line up where it is, as a new line of its own (so a world can lay the line out).
            Expect(probes[1].Words.Skip(2).SequenceEqual(new[] { "/four", "five" }) && probes[0].Words.Count == 0,
                $"after Sky went, Walls got \"{string.Join(" ", probes[1].Words.Skip(2))}\" and Floor \"{string.Join(" ", probes[0].Words)}\"");

            // The hands: only the world that dresses them changes them, whichever order the worlds run in.
            float Focus() => HandSkin.FocusStrength;
            float Nudge() => rig.Left.VisualRoot.localPosition.x;
            void Hands(float mark, string when)
            {
                Frames(5);
                bool first = Mathf.Approximately(Focus(), mark) && Mathf.Approximately(Nudge(), mark);
                // And the other way round.
                var music = new MusicFrame { SongTime = time, Notes = notes, Lyrics = lyrics, Lyric = new LyricsTrack.State(0, 5) };
                for (int k = manager.Active.Count - 1; k >= 0; k--) probes[manager.Active[k]].Step(rig, music, camera, dt);
                bool second = Mathf.Approximately(Focus(), mark) && Mathf.Approximately(Nudge(), mark);
                Expect(first && second, $"{when}: the hands show {Focus():F2} and are nudged {Nudge():F2}, not {mark:F2} (from {Name(manager.HandsWorld)})");
            }
            Expect(manager.HandsWorld == 1 && Mathf.Approximately(HandSkin.Theme.Fill.r, 0.2f), $"with Floor and Walls on, {Name(manager.HandsWorld)} dresses the hands, not Walls (chosen last)");
            Hands(0.2f, "Floor and Walls");
            Choose(3, Choice.Add, "Floor Walls Hands");
            Expect(manager.HandsWorld == 3 && Mathf.Approximately(HandSkin.Theme.Fill.r, 0.4f), $"with Hands on, {Name(manager.HandsWorld)} dresses the hands, not Hands");
            Hands(0.4f, "Floor, Walls and Hands");
            Choose(1, Choice.TurnOff, "Floor Hands");
            Hands(0.4f, "Floor and Hands, after Walls went");
            Choose(3, Choice.TurnOff, "Floor");
            Expect(manager.HandsWorld == 0 && Mathf.Approximately(HandSkin.Theme.Fill.r, 0.1f), $"after Hands went, {Name(manager.HandsWorld)} dresses the hands, not Floor");
            Hands(0.1f, "Floor alone");

            string Name(int index) => index >= 0 ? probes[index].name : "none";
            manager.Switch(0);
            LyricsOption.On = lyricsWere;
            UnityEngine.Object.DestroyImmediate(holder);
            if (problems.Count == 0)
                Debug.Log("[Check] Worlds combine as the menu says: added when they fit, swapped when they clash (two lyric worlds too), the oldest giving way past three, turned off but never the last, " +
                          "OnEnter and OnExit once each time; only one world gets the sung words (the lyric one first) and only one dresses the hands.");
            else
                foreach (var problem in problems) Debug.LogError($"[Check] Combining worlds: {problem}.");
            return problems.Count == 0;
        }

        /// <summary>
        /// Every pair of real worlds that can be on together runs together through WorldManager, for a second and a half
        /// of the demo song around its first chorus with both hands up, with no exceptions or errors. While they run,
        /// only the world that shows the words shows any (SungWords), and the hands wear the look of the one that
        /// dresses them.
        /// </summary>
        static bool CheckWorldPairs()
        {
#if META_XR_CORE
            LoadSceneSettings();
            bool lyricsWere = LyricsOption.On;
            LyricsOption.On = true;
            var camera = StageRoom(8f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            PlaceHands(camera, HandPose.PalmsToFace, rig);
            var holder = new GameObject("Worlds");
            var worlds = new List<World>();
            foreach (var type in WorldCatalog.Types())
            {
                var go = new GameObject(type.Name);
                go.transform.SetParent(holder.transform, false);
                go.SetActive(false);
                var world = (World)go.AddComponent(type);
                if (SceneSettings.TryGetValue(type, out var json)) EditorJsonUtility.FromJsonOverwrite(json, world);
                else ProjectSetup.AssignMaterials(world);
                worlds.Add(world);
            }
            worlds.Sort(WorldCatalog.Compare);
            var manager = holder.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Count;
            for (int i = 0; i < worlds.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();

            var problems = new List<string>();
            var logged = new List<string>();
            void Listen(string condition, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) logged.Add(condition);
            }
            Application.logMessageReceived += Listen;
            const float dt = 1f / 30f;
            int pairs = 0;
            try
            {
                for (int a = 0; a < worlds.Count; a++)
                    for (int b = a + 1; b < worlds.Count; b++)
                    {
                        if (!worlds[a].CombinesWith(worlds[b])) continue;
                        pairs++;
                        string pair = $"{worlds[a].DisplayName} + {worlds[b].DisplayName}";
                        logged.Clear();
                        try
                        {
                            manager.Switch(a);
                            var choice = manager.Choose(b);
                            if (choice != Choice.Add || manager.Active.Count != 2)
                            {
                                problems.Add($"{pair}: choosing the second {choice} left {manager.Active.Count} on");
                                continue;
                            }
                            var clock = new SongClock { Time = ChorusStart - 1.2f };
                            bool wordsElsewhere = false, lookElsewhere = false;
                            for (int frame = 0; frame < 45; frame++)
                            {
                                var music = clock.Advance(dt);
                                foreach (int i in manager.Active) worlds[i].Step(rig, music, camera, dt);
                                foreach (int i in manager.Active)
                                    if (i != manager.WordsWorld && worlds[i].WordsShowing > 0) wordsElsewhere = true;
                                var look = worlds[manager.HandsWorld].Theme;
                                if (HandSkin.Theme.Fill != look.Fill || HandSkin.Theme.Edge != look.Edge || HandSkin.Theme.Pattern != look.Pattern) lookElsewhere = true;
                            }
                            if (wordsElsewhere) problems.Add($"{pair}: a world other than {worlds[manager.WordsWorld].DisplayName} showed sung words");
                            if (lookElsewhere) problems.Add($"{pair}: the hands didn't keep the look of {worlds[manager.HandsWorld].DisplayName}");
                        }
                        catch (Exception e)
                        {
                            problems.Add($"{pair}: {e.GetType().Name}: {e.Message}");
                        }
                        foreach (var message in logged.Distinct()) problems.Add($"{pair}: logged \"{message}\"");
                    }
                if (worlds.Count > 0) manager.Switch(0);
            }
            finally
            {
                Application.logMessageReceived -= Listen;
                LyricsOption.On = lyricsWere;
                Room.Clear();
                // The next checks start from an empty scene, as they did before this one.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            if (problems.Count == 0)
                Debug.Log($"[Check] All {pairs} pairs of worlds that can be on together ran together for 1.5 s of the song without errors, one showing the sung words and one dressing the hands.");
            else
                foreach (var problem in problems) Debug.LogError($"[Check] Worlds together: {problem}.");
            return problems.Count == 0;
#else
            return true;
#endif
        }
    }
}
