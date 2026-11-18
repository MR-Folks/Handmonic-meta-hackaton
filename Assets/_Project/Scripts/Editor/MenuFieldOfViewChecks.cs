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
        /// <summary>How far across and up a set of points reaches from the centre of a view, in degrees.</summary>
        sealed class Extent
        {
            public float Left = float.MaxValue, Right = float.MinValue, Low = float.MaxValue, High = float.MinValue;
            public bool Any => Right >= Left;
            public float Wide => Mathf.Max(-Left, Right);
            public float Tall => Mathf.Max(-Low, High);

            /// <summary>Adds a point as seen from <paramref name="eye"/>: its angles across and up from the eye's view axis, as a camera frames them.</summary>
            public void Add(Transform eye, Vector3 point)
            {
                var local = eye.InverseTransformPoint(point);
                if (local.z <= 1e-4f)
                {
                    Left = Low = -90f;
                    Right = High = 90f;
                    return;
                }
                float across = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, up = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
                Left = Mathf.Min(Left, across);
                Right = Mathf.Max(Right, across);
                Low = Mathf.Min(Low, up);
                High = Mathf.Max(High, up);
            }

            public void Add(Extent other)
            {
                if (!other.Any) return;
                Left = Mathf.Min(Left, other.Left);
                Right = Mathf.Max(Right, other.Right);
                Low = Mathf.Min(Low, other.Low);
                High = Mathf.Max(High, other.High);
            }

            public bool Inside(Vector2 area) => Any && Wide <= area.x * 0.5f && Tall <= area.y * 0.5f;

            public override string ToString() => Any ? $"{Left:0.0}° to {Right:0.0}° across, {Low:0.0}° to {High:0.0}° up" : "nothing";
        }

        /// <summary>
        /// The menu keeps everything essential inside Meta's recommended design area, 58° across and 54° high round the
        /// centre of the view, where it opens in front of a standing player (eyes 1.6 m up, looking ahead): every bubble at
        /// its full size near your gaze, and swollen under a hand, the song row, the caption (with the longest words it
        /// can show), both hint lines and the song page; for today's worlds and for
        /// 10 and 6 of them. Its bubbles are never under 64 mm across, its letters never under 1° tall, and nothing is
        /// nearer the eyes than 50 cm. On the cassette deck that opens in its place, what you touch and read fits the area
        /// too once you look down at the table, seated or standing. Logs the rows and every extent (the deck's whole
        /// turntable and radio as well), and whether each is within AimArea.
        /// </summary>
        static bool CheckMenuFieldOfView()
        {
            var failures = new List<string>();
            var report = new StringBuilder();
            // Today's worlds, 23 (some repeated if there are fewer), and 10 and 6 of them.
            int all = WorldCatalog.Types().Count();
            foreach (int count in new[] { all, 23, 10, 6 }.Distinct()) MeasureMenu(count, all, failures, report);
            MeasureDeck("seated", 1.2f, 20f, failures, report);
            MeasureDeck("standing", 1.6f, 30f, failures, report);
            foreach (var line in report.ToString().TrimEnd().Split('\n')) Debug.Log($"[Check] Field of view: {line.Trim()}");
            foreach (var failure in failures) Debug.LogError($"[Check] Field of view: {failure}");
            if (failures.Count == 0)
                Debug.Log($"[Check] The menu, its song page and what you touch on the cassette deck keep everything essential inside Meta's {WorldMenu.DesignArea.x:0}° × {WorldMenu.DesignArea.y:0}° design area, with bubbles of 64 mm or more, letters of 1° or more and nothing nearer than 50 cm.");
            return failures.Count == 0;
        }

        static SongAsset PreviewSong(string title, string artist)
        {
            var song = ScriptableObject.CreateInstance<SongAsset>();
            song.title = title;
            song.artist = artist;
            return song;
        }

        /// <summary>
        /// A menu over the first <paramref name="count"/> worlds (in menu order; past the last world they repeat from the
        /// first), built in front of <paramref name="camera"/>.
        /// </summary>
        static WorldMenu MakeFieldOfViewMenu(int count, Camera camera, out WorldManager manager)
        {
            var managerGo = new GameObject("Worlds");
            var previews = WorldCatalog.MakePreviews(managerGo.transform);
            var worlds = previews.Take(count).ToList();
            for (int i = previews.Length; i < count; i++)
            {
                var go = new GameObject($"{previews[i % previews.Length].GetType().Name} {i}");
                go.transform.SetParent(managerGo.transform, false);
                worlds.Add((World)go.AddComponent(previews[i % previews.Length].GetType()));
                go.SetActive(false);
            }
            manager = managerGo.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Count;
            for (int i = 0; i < worlds.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            manager.Switch(0);
            var menu = new GameObject("WorldMenu").AddComponent<WorldMenu>();
            var wso = new SerializedObject(menu);
            wso.FindProperty("bubbleMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble");
            wso.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            wso.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            wso.FindProperty("panelMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.PanelMaterialPath, "MusicInYourHands/Panel");
            wso.ApplyModifiedPropertiesWithoutUndo();
            menu.Step(null, manager, null, camera, 0f);
            return menu;
        }

        static void MeasureMenu(int count, int existing, List<string> failures, StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            var menu = MakeFieldOfViewMenu(count, camera, out var manager);
            const float Frame = 1f / 72f;
            void Run(float seconds)
            {
                for (float t = 0f; t < seconds; t += Frame) menu.Step(null, manager, null, camera, Frame);
            }

            // The longest words the caption can be asked to show.
            menu.PreviewCaption("Next: A very long song title that needs to be shortened somehow, and then a little more");
            menu.Open(camera);
            Run(1.5f);

            var so = new SerializedObject(menu);
            float diameter = so.FindProperty("bubbleDiameter").floatValue;
            var root = menu.transform.Find("Menu");
            var eye = camera.transform;
            Extent full = new Extent(), swollen = new Extent(), text = new Extent(), page = new Extent(), pageContent = new Extent();
            float smallest = float.MaxValue, nearest = float.MaxValue, smallestLetters = float.MaxValue;
            int bubbleCount = 0;
            string smallestLabel = "";
            foreach (Transform child in root)
            {
                if (!child.name.StartsWith("Bubble ")) continue;
                bubbleCount++;
                if (!child.gameObject.activeSelf) failures.Add($"{count} worlds: {child.name} isn't showing a second and a half after the menu opened.");
                smallest = Mathf.Min(smallest, child.localScale.x);
                nearest = Mathf.Min(nearest, Vector3.Distance(eye.position, child.position));
                for (int k = 0; k < 32; k++)
                {
                    float a = k * Mathf.PI * 2f / 32f;
                    var round = root.right * Mathf.Cos(a) + root.up * Mathf.Sin(a);
                    full.Add(eye, child.position + round * (diameter * WorldMenu.NearGaze * 0.5f));
                    swollen.Add(eye, child.position + round * (diameter * WorldMenu.NearGaze * WorldMenu.Swell * 0.5f));
                }
            }
            foreach (var (name, size) in new[] { ("Caption", WorldMenu.CaptionSize), ("Hint", WorldMenu.HintSize), ("Hint 2", WorldMenu.HintSize) })
            {
                var label = root.Find(name);
                if (label == null || !label.gameObject.activeSelf)
                {
                    failures.Add($"{count} worlds: the {name} line isn't showing.");
                    continue;
                }
                var mesh = label.GetComponent<MeshFilter>().sharedMesh;
                float farthest = 0f;
                foreach (var v in mesh.vertices)
                {
                    var at = label.TransformPoint(v);
                    text.Add(eye, at);
                    farthest = Mathf.Max(farthest, Vector3.Distance(eye.position, at));
                }
                // Its letters' height, as seen at its far end.
                float letters = 2f * Mathf.Atan(size * 0.5f / farthest) * Mathf.Rad2Deg;
                if (letters < smallestLetters)
                {
                    smallestLetters = letters;
                    smallestLabel = name.ToLowerInvariant();
                }
            }
            // The song page in the honeycomb's place, showing its list of songs.
            menu.PreviewLibrary(new[] { PreviewSong("Crystals", "Fonts") }, null, 0);
            Run(0.7f);
            var panel = root.Find("Song Library/Panel");
            float pageLetters = 90f;
            if (panel == null || !panel.gameObject.activeInHierarchy) failures.Add($"{count} worlds: the song page didn't show.");
            else
            {
                float farthest = 0f;
                for (int k = 0; k < 4; k++)
                {
                    var corner = panel.TransformPoint((k & 1) == 0 ? -0.5f : 0.5f, (k & 2) == 0 ? -0.5f : 0.5f, 0f);
                    page.Add(eye, corner);
                    farthest = Mathf.Max(farthest, Vector3.Distance(eye.position, corner));
                }
                // The smallest letters on the page, at its farthest corner.
                pageLetters = 2f * Mathf.Atan(SongLibrary.SmallText * WorldMenu.PageScale * 0.5f / farthest) * Mathf.Rad2Deg;
                // Its buttons and words, inside its panel.
                foreach (var r in panel.parent.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.transform == panel) continue;
                    var filter = r.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    foreach (var v in filter.sharedMesh.vertices) pageContent.Add(eye, r.transform.TransformPoint(v));
                }
            }

            var everything = new Extent();
            everything.Add(full);
            everything.Add(text);
            everything.Add(page);
            string rows = string.Join(",", menu.RowLengths);
            int columns = menu.RowLengths.Count > 0 ? menu.RowLengths.Max() : 0;
            string Aim(Extent e) => e.Inside(WorldMenu.AimArea) ? "within" : "outside";
            string repeats = count > existing ? $" ({count - existing} of them repeats)" : "";
            report.Append($"{count} worlds{repeats} in {menu.RowLengths.Count} rows ({rows}, up to {columns} across), {bubbleCount} bubbles with the song row, " +
                          $"{diameter * 1000f:0} mm at rest ({smallest * 1000f:0.0} mm the smallest showing), the nearest {nearest:0.000} m from the eyes; " +
                          $"bubbles at full size {full}; swollen under a hand {swollen}; the words {text}; the song page {page} (its buttons and words {pageContent}); " +
                          $"all together {everything}, {Aim(everything)} the {WorldMenu.AimArea.x:0}° × {WorldMenu.AimArea.y:0}° aim; " +
                          $"smallest letters {Mathf.Min(smallestLetters, pageLetters):0.00}° ({(pageLetters < smallestLetters ? "song page" : smallestLabel)}).\n");

            foreach (var (what, e) in new[] { ("the bubbles", full), ("a swollen bubble", swollen), ("the words", text), ("the song page", page) })
                if (!e.Inside(WorldMenu.DesignArea))
                    failures.Add($"{count} worlds: {what} reach outside the {WorldMenu.DesignArea.x:0}° × {WorldMenu.DesignArea.y:0}° design area ({e}).");
            if (bubbleCount != count + WorldMenu.ExtraBubbles) failures.Add($"{count} worlds: {bubbleCount} bubbles instead of {count + WorldMenu.ExtraBubbles}.");
            if (smallest < 0.064f) failures.Add($"{count} worlds: a bubble shows only {smallest * 1000f:0.0} mm across, under the 64 mm hands need.");
            if (nearest < 0.499f) failures.Add($"{count} worlds: a bubble is only {nearest:0.000} m from the eyes, nearer than 50 cm.");
            if (smallestLetters < 1f || pageLetters < 1f)
                failures.Add($"{count} worlds: letters only {Mathf.Min(smallestLetters, pageLetters):0.00}° tall, under 1°.");
            Object.DestroyImmediate(menu.gameObject);
        }

        /// <summary>
        /// The cassette deck from a player's eyes <paramref name="eyeHeight"/> above the floor: what you touch and read on it
        /// (the front cassette and the turntable's edge by it, the radio's door and keys, the table's two buttons, the words
        /// over the turntable) must fit the design area, measured across from the way you face and up from the middle of
        /// it, as when you look down at the table. The whole turntable and radio are measured too, for the record.
        /// </summary>
        static void MeasureDeck(string who, float eyeHeight, float pitch, List<string> failures, StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Room.Clear();
            DeckParts.Forget();
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, eyeHeight, 0f), Quaternion.Euler(pitch, 0f, 0f));
            var menu = MakeFieldOfViewMenu(2, camera, out var manager);
            var songs = AssetDatabase.FindAssets("t:SongAsset", new[] { "Assets/_Project/Songs" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SongAsset>(AssetDatabase.GUIDToAssetPath(guid))).Where(s => s != null).OrderBy(s => s.name).ToList();
            songs.Add(PreviewSong("Crystals", "Fonts"));
            var deck = menu.Deck;
            deck.Preview(songs, null, PreviewBands, 0.2f);
            deck.PreviewFront(songs[1]);
            menu.OpenDeck(camera);
            for (float t = 0f; t < 1.2f; t += 1f / 72f) menu.Step(null, manager, null, camera, 1f / 72f);

            // What you touch and read: the front cassette, the turntable's edge by it where you pinch it, the radio's door
            // and keys, the table's two buttons and the words over the turntable.
            var points = new List<Vector3>();
            void Cassette(int step, List<Vector3> into)
            {
                var song = deck.SongBeside(step);
                if (song == null) return;
                for (int k = 0; k < 4; k++) into.Add(deck.PointOnCassette(song, new Vector3((k & 1) == 0 ? -0.05f : 0.05f, (k & 2) == 0 ? -0.032f : 0.032f, 0f)));
            }
            Cassette(0, points);
            for (float a = -20f; a <= 20f; a += 10f) points.Add(deck.EdgePoint(a, 0f));
            points.Add(deck.SlotPosition);
            foreach (var key in new[] { "PlayButton", "StopButton", "EjectButton", "Song List Button", "Close Button" }) points.Add(deck.KeyFace(key));
            var plate = deck.FrontPlate;
            if (plate.gameObject.activeInHierarchy)
                for (int k = 0; k < 4; k++) points.Add(plate.TransformPoint((k & 1) == 0 ? -0.5f : 0.5f, (k & 2) == 0 ? -0.5f : 0.5f, 0f));
            // And the whole of it: the turntable's near half and the cassettes two places either side, and the radio's body.
            var whole = new List<Vector3>(points);
            for (int step = -2; step <= 2; step++) Cassette(step, whole);
            for (float a = -90f; a <= 90f; a += 15f) whole.Add(deck.EdgePoint(a, 0f));
            foreach (var r in deck.Radio.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                for (int k = 0; k < 8; k++)
                    whole.Add(new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z));
            }

            // Look the way you face when it opened, down at the middle of the deck (from the eyes, up and down).
            var eye = camera.transform.position;
            var facing = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            float lowest = 90f, highest = -90f;
            foreach (var p in points)
            {
                var d = p - eye;
                float up = Mathf.Atan2(d.y, Vector3.ProjectOnPlane(d, Vector3.up).magnitude) * Mathf.Rad2Deg;
                lowest = Mathf.Min(lowest, up);
                highest = Mathf.Max(highest, up);
            }
            var look = new GameObject("Look").transform;
            look.SetPositionAndRotation(eye, Quaternion.LookRotation(facing) * Quaternion.Euler(-(lowest + highest) * 0.5f, 0f, 0f));
            var seen = new Extent();
            // Centre the view on it up and down, as you would by tipping your head.
            for (int pass = 0; pass < 3; pass++)
            {
                seen = new Extent();
                foreach (var p in points) seen.Add(look, p);
                look.rotation *= Quaternion.Euler(-(seen.Low + seen.High) * 0.5f, 0f, 0f);
            }
            seen = new Extent();
            foreach (var p in points) seen.Add(look, p);
            var all = new Extent();
            foreach (var p in whole) all.Add(look, p);
            float lookDown = -Mathf.Asin(look.forward.y) * Mathf.Rad2Deg;
            report.Append($"the cassette deck {who} (eyes {eyeHeight:0.0} m up, table {deck.TableHeight:0.00} m), looking {lookDown:0}° down at it: " +
                          $"what you touch and read {seen.Right - seen.Left:0.0}° across ({seen.Left:0.0}° to {seen.Right:0.0}° from the way you face) " +
                          $"by {seen.High - seen.Low:0.0}° high, {(seen.Inside(WorldMenu.AimArea) ? "within" : "outside")} the {WorldMenu.AimArea.x:0}° × {WorldMenu.AimArea.y:0}° aim; " +
                          $"the whole turntable and radio {all.Right - all.Left:0.0}° across ({all.Left:0.0}° to {all.Right:0.0}°) by {all.High - all.Low:0.0}° high " +
                          $"({all.Low:0.0}° to {all.High:0.0}°).\n");
            if (!seen.Inside(WorldMenu.DesignArea))
                failures.Add($"what you touch and read on the cassette deck {who} reaches outside the {WorldMenu.DesignArea.x:0}° × {WorldMenu.DesignArea.y:0}° design area ({seen}).");
            Object.DestroyImmediate(look.gameObject);
            Object.DestroyImmediate(menu.gameObject);
        }

        /// <summary>
        /// Lazy follow: the menu stays put while you look round it (15° to the side), and once you have looked well away
        /// (35° to the side, or 30° down) it waits a moment, then glides back in front of you, starting and ending gently
        /// and never jumping, with no overshoot. A fingertip at the menu holds it still however far you look away.
        /// </summary>
        static bool CheckMenuFollow()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            var head = camera.transform;
            head.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var menu = MakeFieldOfViewMenu(6, camera, out var manager);
            const float Frame = 1f / 72f;
            var fingertip = (Vector3?)null;
            void Step()
            {
                if (fingertip is Vector3 index)
                {
                    var tips = new[]
                    {
                        index + new Vector3(-0.03f, -0.045f, -0.03f), index, index + new Vector3(-0.02f, -0.05f, -0.06f),
                        index + new Vector3(-0.03f, -0.055f, -0.065f), index + new Vector3(-0.04f, -0.06f, -0.07f)
                    };
                    rig.Push(false, true, new Pose(index + new Vector3(-0.02f, -0.04f, -0.09f), Quaternion.identity), Vector3.left, tips);
                }
                else rig.Push(false, false, new Pose(head.position + new Vector3(0.2f, -0.6f, 0.1f), Quaternion.identity), Vector3.left, new Vector3[5]);
                menu.Step(rig, manager, null, camera, Frame);
            }
            var root = menu.transform.Find("Menu");
            // How far the menu's middle is from the middle of the view, how far it moved this frame, both in degrees.
            float Off() => Vector3.Angle(head.forward, root.position - head.position);
            var failures = new List<string>();
            void Expect(bool ok, string what)
            {
                if (!ok) failures.Add(what);
            }

            Step();
            menu.Open(camera);
            for (float t = 0f; t < 1f; t += Frame) Step();
            var start = root.position;

            // Looking 15° to the side for two seconds: it stays where it is.
            head.rotation = Quaternion.Euler(0f, 15f, 0f);
            for (float t = 0f; t < 2f; t += Frame) Step();
            Expect((root.position - start).magnitude < 0.0005f, $"stays put while you look 15° to the side (moved {(root.position - start).magnitude * 1000f:0.0} mm)");

            // Turning to 35°: nothing for a moment, then a glide that starts gently, never jumps and settles in front.
            float waited = 0f, firstStep = -1f, biggest = 0f, overshoot = 0f, took = -1f;
            var before = root.position;
            for (float t = 0f; t < 3f; t += Frame)
            {
                head.rotation = Quaternion.Euler(0f, Mathf.Lerp(15f, 35f, Mathf.Clamp01(t / 0.2f)), 0f);
                Step();
                float moved = Vector3.Angle(before - head.position, root.position - head.position);
                before = root.position;
                if (moved < 1e-3f && firstStep < 0f) waited = t;
                else if (moved >= 1e-3f && firstStep < 0f) firstStep = moved;
                biggest = Mathf.Max(biggest, moved);
                var flat = Vector3.ProjectOnPlane(root.position - head.position, Vector3.up);
                overshoot = Mathf.Max(overshoot, Vector3.SignedAngle(Vector3.ProjectOnPlane(head.forward, Vector3.up), flat, Vector3.up));
                if (took < 0f && firstStep >= 0f && !menu.Gliding) took = t - waited;
            }
            Expect(waited >= 0.3f && waited <= 0.75f, $"waits a moment before following (waited {waited:0.00} s)");
            Expect(firstStep >= 0f && firstStep < 0.5f, $"starts gently (first step {firstStep:0.00}°)");
            Expect(biggest < 3f, $"never jumps (biggest step {biggest:0.00}° in one frame at 72 fps)");
            Expect(overshoot < 0.5f, $"doesn't overshoot ({overshoot:0.00}° past the middle of the view)");
            Expect(Off() < 2.5f && took > 0f && took < 2f, $"ends up in front of you ({Off():0.0}° off, glide {took:0.00} s)");

            // Looking 30° down: it follows down too.
            head.rotation = Quaternion.Euler(30f, 35f, 0f);
            for (float t = 0f; t < 2.5f; t += Frame) Step();
            float down = -Mathf.Asin((root.position - head.position).normalized.y) * Mathf.Rad2Deg;
            Expect(Off() < 2.5f && down > 28f, $"follows when you look down ({Off():0.0}° off the middle of the view, {down:0.0}° down)");

            // A fingertip at a bubble holds it still, however far you look away.
            var bubble = root.Find("Bubble 0").position;
            fingertip = bubble - root.forward * 0.04f;
            for (float t = 0f; t < 0.3f; t += Frame) Step();
            var held = root.position;
            head.rotation = Quaternion.Euler(30f, 75f, 0f);
            for (float t = 0f; t < 2f; t += Frame) Step();
            Expect((root.position - held).magnitude < 0.0005f, $"a fingertip at it holds it still (moved {(root.position - held).magnitude * 1000f:0.0} mm)");
            // Taking the hand away, it follows again.
            fingertip = null;
            for (float t = 0f; t < 2.5f; t += Frame) Step();
            Expect(Off() < 2.5f, $"follows once the hand has gone ({Off():0.0}° off)");

            menu.Close();
            Object.DestroyImmediate(menu.gameObject);
            if (failures.Count == 0)
                Debug.Log($"[Check] The menu follows lazily: it stays put while you look 15° aside, waits {waited:0.00} s once you look 35° away, then glides " +
                          $"back in {took:0.00} s (first step {firstStep:0.00}°, largest {biggest:0.00}° a frame, no overshoot), follows you looking down, and holds still under a fingertip.");
            else Debug.LogError($"[Check] Menu follow: {string.Join("; ", failures)}.");
            return failures.Count == 0;
        }
    }
}
