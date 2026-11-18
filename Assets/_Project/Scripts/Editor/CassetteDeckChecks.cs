using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        /// <summary>
        /// The cassette deck: its four models (exported or stand-in) have their parts at the right sizes and pivots,
        /// and the whole flow works with moved hands: the song bubble opens the deck within reach and view. The
        /// turntable: a pinch-drag on its edge follows the hand closely and smooths away jitter, and brings another
        /// cassette to the front; let go mid-turn it spins on; a hand put back into it catches it with a smooth brake;
        /// a slow slide of an open hand nudges it gently and a quick one flicks it into a glide, which a pinch on the
        /// edge catches too; and every turn ends settled on a cassette. Then a pinched cassette carried to the radio
        /// shows the snap outline and plays when let go, a second one pops the first back to the turntable, Eject sends
        /// it home, the song list button opens the song page and its Back button returns, the close button and Meta's
        /// menu gesture close it, and a frame of the deck allocates no memory (a hand resting, brushing or holding).
        /// </summary>
        static bool CheckCassetteDeck() => CheckDeckModels() & CheckDeckFlow();

        static bool CheckDeckModels()
        {
            DeckParts.Forget();
            var holder = new GameObject("Deck Models").transform;
            bool ok = true;
            var report = new StringBuilder("[Check] Cassette deck models:");
            void Expect(string model, Vector3 size, float tolerance, bool checkHeight, System.Func<Bounds, bool> pivot, string pivotText, params string[] parts)
            {
                var go = DeckParts.Make(model, holder);
                var b = DeckParts.ModelBounds(go.transform);
                bool exported = DeckParts.Model(model) != null;
                bool sized = Mathf.Abs(b.size.x - size.x) <= size.x * tolerance && Mathf.Abs(b.size.z - size.z) <= size.z * tolerance &&
                             (!checkHeight || Mathf.Abs(b.size.y - size.y) <= size.y * tolerance);
                var missing = parts.Where(p => DeckParts.Find(go.transform, p) == null).ToList();
                var foreign = new HashSet<string>();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m == null || m != DeckParts.Material(DeckParts.SlotName(m.name)) || !m.shader.isSupported) foreign.Add(m != null ? m.name : "none");
                bool pivoted = pivot(b);
                // Imported upright and to scale: no turned or scaled nodes, the root at scale 1.
                var turned = go.GetComponentsInChildren<Transform>(true)
                    .Where(t => t != go.transform && (Quaternion.Angle(t.localRotation, Quaternion.identity) > 0.5f || (t.localScale - Vector3.one).sqrMagnitude > 1e-4f))
                    .Select(t => $"{t.name} {t.localEulerAngles} x{t.localScale}").ToList();
                if ((go.transform.localScale - Vector3.one).sqrMagnitude > 1e-4f) turned.Add($"root x{go.transform.localScale}");
                // Facing the player: its front on -Z and its left on -X.
                string facing = model switch
                {
                    DeckParts.Cassette => Side(go.transform, "Label", z: -1) + Side(go.transform, "ReelLeft", x: -1),
                    DeckParts.Radio => Side(go.transform, "Door", z: -1) + Side(go.transform, "SpeakerLeft", x: -1) + Side(go.transform, "Slot", z: -1),
                    DeckParts.Turntable => Side(go.transform, "RimHeight", z: -1),
                    _ => ""
                };
                report.Append($" {model} ({(exported ? "export" : "stand-in")}) {b.size.x:0.000} x {b.size.y:0.000} x {b.size.z:0.000} m");
                if (turned.Count > 0 || facing.Length > 0)
                {
                    ok = false;
                    Debug.LogError($"[Check] Cassette deck {model}: {(turned.Count > 0 ? "turned or scaled parts [" + string.Join("; ", turned) + "] " : "")}{facing}");
                }
                if (!sized || missing.Count > 0 || foreign.Count > 0 || !pivoted)
                {
                    ok = false;
                    Debug.LogError($"[Check] Cassette deck {model} ({(exported ? "exported" : "stand-in")}): size {b.size} (want about {size}){(sized ? "" : " WRONG")}, " +
                                   $"pivot {(pivoted ? "fine" : "wrong, want " + pivotText + $"; bounds {b.min} to {b.max}")}, " +
                                   $"missing parts [{string.Join(", ", missing)}], materials without a deck slot [{string.Join(", ", foreign)}].");
                }
                report.Append(';');
            }
            Expect(DeckParts.Cassette, new Vector3(0.1f, 0.064f, 0.012f), 0.15f, true, b => b.center.magnitude < 0.004f, "the centre",
                "Shell", "Label", "Window", "ReelLeft", "ReelRight");
            Expect(DeckParts.Radio, new Vector3(0.42f, 0.24f, 0.12f), 0.15f, true,
                b => Mathf.Abs(b.min.y) < 0.006f && Mathf.Abs(b.center.x) < 0.01f && Mathf.Abs(b.center.z) < 0.02f, "the bottom centre",
                "Body", "SpeakerLeft", "SpeakerRight", "Door", "Slot", "PlayButton", "StopButton", "EjectButton", "Light");
            Expect(DeckParts.Turntable, new Vector3(0.52f, 0.05f, 0.52f), 0.08f, false,
                b => Mathf.Abs(b.min.y) < 0.006f && new Vector2(b.center.x, b.center.z).magnitude < 0.01f, "the base centre",
                "Platter", "Base", "RimHeight");
            Expect(DeckParts.Table, new Vector3(0.9f, 0.68f, 0.55f), 0.05f, true,
                b => Mathf.Abs(b.min.y) < 0.006f && new Vector2(b.center.x, b.center.z).magnitude < 0.01f, "the floor centre",
                "Top", "Legs", "TopSurface");
            var table = holder.Find(DeckParts.Table);
            var surface = DeckParts.Find(table, "TopSurface");
            if (surface == null || Mathf.Abs(table.InverseTransformPoint(surface.position).y - 0.68f) > 0.003f)
            {
                ok = false;
                Debug.LogError("[Check] Cassette deck table: TopSurface isn't at 0.68 m.");
            }
            var turntable = holder.Find(DeckParts.Turntable);
            var rim = DeckParts.Find(turntable, "RimHeight");
            float rimRadius = rim != null ? new Vector2(rim.localPosition.x, rim.localPosition.z).magnitude : 0f;
            if (Mathf.Abs(rimRadius - 0.23f) > 0.01f)
            {
                ok = false;
                Debug.LogError($"[Check] Cassette deck turntable: RimHeight is {rimRadius:0.000} m from the middle, not 0.23 m.");
            }
            Object.DestroyImmediate(holder.gameObject);
            if (ok) Debug.Log(report.ToString().TrimEnd(';') + "; all parts, sizes, pivots and materials as specified.");
            return ok;
        }

        static bool CheckDeckFlow()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Room.Clear();
            DeckParts.Forget();
            // A seated player: eyes 1.2 m above the floor, looking ahead and a little down.
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, 0f), Quaternion.Euler(20f, 0f, 0f));
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var managerGo = new GameObject("Worlds");
            var worlds = new World[] { MakePreviewWorld<HeartbeatOrb>(managerGo.transform), MakePreviewWorld<Lightning>(managerGo.transform) };
            var manager = managerGo.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Length;
            for (int i = 0; i < worlds.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            manager.Switch(0);

            // The music: the demo songs, nothing playing yet.
            var songs = AssetDatabase.FindAssets("t:SongAsset", new[] { "Assets/_Project/Songs" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SongAsset>(AssetDatabase.GUIDToAssetPath(guid))).Where(s => s != null && s.clip != null).ToList();
            var bus = new GameObject("Music").AddComponent<MusicBus>();
            var bso = new SerializedObject(bus);
            bso.FindProperty("startSong").objectReferenceValue = songs[0];
            var songList = bso.FindProperty("songs");
            songList.arraySize = songs.Count - 1;
            for (int i = 1; i < songs.Count; i++) songList.GetArrayElementAtIndex(i - 1).objectReferenceValue = songs[i];
            bso.ApplyModifiedPropertiesWithoutUndo();
            typeof(MusicBus).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bus, null);
            // The playlist as the menu's refresh makes it: the demo songs, then any added on this computer.
            typeof(MusicBus).GetMethod("ListSongs", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bus, null);

            var menu = new GameObject("WorldMenu").AddComponent<WorldMenu>();
            var wso = new SerializedObject(menu);
            wso.FindProperty("bubbleMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble");
            wso.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            wso.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            wso.FindProperty("panelMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.PanelMaterialPath, "MusicInYourHands/Panel");
            wso.ApplyModifiedPropertiesWithoutUndo();
            RunMessage(menu, "OnEnable");

            const float Frame = 1f / 72f;
            const float Open = 0.06f, Closed = 0.008f;
            var pinchAt = camera.transform.position + new Vector3(0.15f, -0.45f, 0.2f);
            float gap = Open;
            // The right hand by the point between its thumb and index fingertips, `gap` apart (pinched under 1.5 cm).
            void Hand(Vector3 at, float apart)
            {
                pinchAt = at;
                gap = apart;
                var index = at + Vector3.up * (apart * 0.5f);
                var tips = new[]
                {
                    at - Vector3.up * (apart * 0.5f), index, index + new Vector3(-0.02f, -0.05f, -0.06f),
                    index + new Vector3(-0.03f, -0.055f, -0.065f), index + new Vector3(-0.04f, -0.06f, -0.07f)
                };
                rig.Push(false, true, new Pose(index + new Vector3(-0.02f, -0.04f, -0.09f), Quaternion.identity), Vector3.left, tips);
            }
            // The right hand pointing its index fingertip at a spot, thumb tucked away.
            void Point(Vector3 tip)
            {
                pinchAt = tip;
                gap = -1f;
                var tips = new[]
                {
                    tip + new Vector3(-0.03f, -0.045f, -0.03f), tip, tip + new Vector3(-0.02f, -0.05f, -0.06f),
                    tip + new Vector3(-0.03f, -0.055f, -0.065f), tip + new Vector3(-0.04f, -0.06f, -0.07f)
                };
                rig.Push(false, true, new Pose(tip + new Vector3(-0.02f, -0.04f, -0.09f), Quaternion.identity), Vector3.left, tips);
            }
            // The right hand open and flat, palm down at `palm` with its fingers spread, moving at `velocity` (HandRig
            // works out a hand's speed between frames, which these checks don't have, so it is given). It allocates
            // nothing, so the frames it drives can be measured.
            var openTips = new Vector3[5];
            void OpenHand(Vector3 palm, Vector3 velocity)
            {
                openTips[0] = palm + new Vector3(-0.06f, 0.02f, 0.03f);
                openTips[1] = palm + new Vector3(-0.03f, 0.03f, 0.09f);
                openTips[2] = palm + new Vector3(0f, 0.03f, 0.1f);
                openTips[3] = palm + new Vector3(0.02f, 0.03f, 0.09f);
                openTips[4] = palm + new Vector3(0.04f, 0.02f, 0.07f);
                rig.Push(false, true, new Pose(palm, Quaternion.identity), Vector3.down, openTips);
                rig.Right.Velocity = velocity;
            }
            // Slides the open hand in a straight line, one frame at a time, calling `each` after every frame.
            void Slide(Vector3 from, Vector3 to, int frames, System.Action each)
            {
                var velocity = (to - from) / (frames * Frame);
                for (int i = 1; i <= frames; i++)
                {
                    OpenHand(Vector3.Lerp(from, to, i / (float)frames), velocity);
                    menu.Step(rig, manager, bus, camera, Frame);
                    each?.Invoke();
                }
            }
            void Step()
            {
                if (gap < 0f) Point(pinchAt); else Hand(pinchAt, gap);
                menu.Step(rig, manager, bus, camera, Frame);
            }
            void Run(float seconds)
            {
                for (float t = 0f; t < seconds; t += Frame) Step();
            }
            void Move(Vector3 to, int frames, float apart)
            {
                var from = pinchAt;
                for (int i = 1; i <= frames; i++)
                {
                    Hand(Vector3.Lerp(from, to, i / (float)frames), apart);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
            }
            void MoveTip(Vector3 to, int frames)
            {
                var from = pinchAt;
                for (int i = 1; i <= frames; i++)
                {
                    Point(Vector3.Lerp(from, to, i / (float)frames));
                    menu.Step(rig, manager, bus, camera, Frame);
                }
            }
            void Pinch(bool on)
            {
                for (int i = 0; i < 4; i++)
                {
                    Hand(pinchAt, on ? Closed : Open);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
            }
            // A fingertip press: down onto a key's face from 8 cm above and back up.
            void Press(string key)
            {
                var deckNow = menu.Deck;
                var face = deckNow.KeyFace(key);
                var inward = deckNow.KeyInward(key);
                MoveTip(face - inward * 0.08f, 12);
                MoveTip(face + inward * 0.004f, 10);
                MoveTip(face - inward * 0.08f, 10);
            }
            // Carries what the hand holds over to the radio's door, lifting it clear of the turntable on the way.
            void CarryToRadio()
            {
                var deckNow = menu.Deck;
                var lift = pinchAt + Vector3.up * 0.1f;
                Move(lift, 14, Closed);
                Move(deckNow.SnapPosition + Vector3.up * 0.08f, 24, Closed);
                Move(deckNow.SnapPosition, 14, Closed);
                Run(0.2f);
            }
            // Picks up a cassette: the open hand comes from in front and above, then pinches its middle.
            void PickUp(SongAsset song) => PickUpAt(menu.Deck.CassetteCenter(song));
            void PickUpAt(Vector3 center)
            {
                var toward = Vector3.ProjectOnPlane(camera.transform.position - center, Vector3.up).normalized;
                Move(center + toward * 0.12f + Vector3.up * 0.1f, 14, Open);
                Move(center, 14, Open);
                Pinch(true);
            }

            bool lyricsWere = LyricsOption.On;
            var failures = new List<string>();
            void Expect(bool condition, string what)
            {
                if (!condition) failures.Add(what);
            }
            try
            {
                // Open the menu and touch the song bubble: the deck rises and the menu steps aside.
                Step();
                menu.Open(camera);
                Run(1f);
                var root = menu.transform.Find("Menu");
                var toward = root.forward;
                var bubble = root.Find("Bubble Library").position;
                MoveTip(bubble - toward * 0.1f, 8);
                MoveTip(bubble + toward * 0.01f, 6);
                MoveTip(bubble - toward * 0.1f, 8);
                MoveTip(camera.transform.position + new Vector3(0.25f, -0.2f, 0.1f), 10);
                Run(1.4f);
                var deck = menu.Deck;
                Expect(deck != null && deck.IsOpen && menu.IsOpen && !root.gameObject.activeSelf, "the song bubble opens the deck and the menu steps aside");
                if (deck == null) return Report(failures);

                // Within reach and view: the front cassette 35-45 cm ahead along the floor, the table top at 68 cm, and
                // everything you touch within 25 degrees (or so) either side of the gaze.
                var eye = camera.transform.position;
                var gaze = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
                var front0 = deck.FrontSong;
                var frontCenter = deck.CassetteCenter(front0);
                float reach = Vector3.ProjectOnPlane(frontCenter - eye, Vector3.up).magnitude;
                float widest = 0f;
                string widestName = "";
                foreach (var (name, point) in new[]
                         {
                             ("front cassette", frontCenter), ("radio slot", deck.SlotPosition), ("play key", deck.KeyFace("PlayButton")),
                             ("stop key", deck.KeyFace("StopButton")), ("eject key", deck.KeyFace("EjectButton")),
                             ("song list button", deck.KeyFace("Song List Button")), ("close button", deck.KeyFace("Close Button"))
                         })
                {
                    float angle = Mathf.Abs(Vector3.SignedAngle(gaze, Vector3.ProjectOnPlane(point - eye, Vector3.up), Vector3.up));
                    if (angle > widest)
                    {
                        widest = angle;
                        widestName = name;
                    }
                }
                // The words over the turntable: inside the view too, and covering no label of the cassettes at the
                // front (as seen from the eyes), since those are what you choose by.
                var plate = deck.FrontPlate;
                float plateWidest = 0f;
                foreach (var side in new[] { -0.5f, 0.5f })
                {
                    float angle = Mathf.Abs(Vector3.SignedAngle(gaze, Vector3.ProjectOnPlane(plate.TransformPoint(side, 0f, 0f) - eye, Vector3.up), Vector3.up));
                    plateWidest = Mathf.Max(plateWidest, angle);
                    if (angle > widest)
                    {
                        widest = angle;
                        widestName = "words over the turntable";
                    }
                }
                Rect Seen(System.Func<int, Vector3> corner)
                {
                    var min = new Vector2(float.MaxValue, float.MaxValue);
                    var max = new Vector2(float.MinValue, float.MinValue);
                    for (int k = 0; k < 4; k++)
                    {
                        var v = (Vector2)camera.WorldToViewportPoint(corner(k));
                        min = Vector2.Min(min, v);
                        max = Vector2.Max(max, v);
                    }
                    return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                }
                var plateSeen = Seen(k => plate.TransformPoint((k & 1) == 0 ? -0.5f : 0.5f, (k & 2) == 0 ? -0.5f : 0.5f, 0f));
                var covered = new List<string>();
                for (int step = -2; step <= 2; step++)
                {
                    var neighbour = deck.SongBeside(step);
                    if (neighbour == null) continue;
                    // Only labels turned towards you can be read.
                    var centre = deck.PointOnCassette(neighbour, Vector3.zero);
                    var outward = (deck.PointOnCassette(neighbour, Vector3.back * 0.01f) - centre).normalized;
                    if (Vector3.Dot(outward, (eye - centre).normalized) < 0.3f) continue;
                    var labelSeen = Seen(k => deck.PointOnCassette(neighbour, new Vector3((k & 1) == 0 ? -0.044f : 0.044f, (k & 2) == 0 ? 0.0095f : 0.029f, -0.006f)));
                    if (labelSeen.Overlaps(plateSeen)) covered.Add(neighbour.title);
                }
                Expect(covered.Count == 0, $"the words over the turntable cover no front label (covered: {string.Join(", ", covered)})");
                float tableTop = deck.Root.Find("Rise/Table/Top Lift/TopSurface") is Transform top ? top.position.y : -1f;
                Expect(reach > 0.35f && reach < 0.45f, $"front cassette within reach ({reach:0.000} m ahead)");
                Expect(widest <= 26f, $"everything to touch within the narrow view (widest: {widestName} at {widest:0.0}°)");
                Expect(Mathf.Abs(tableTop - 0.68f) < 0.003f, $"table top at 0.68 m for a seated player (at {tableTop:0.000} m)");

                // ---- The turntable ----
                float pitch = deck.TurnPitch;
                Vector3 Over(float degrees) => deck.EdgePoint(degrees) + Vector3.up * 0.05f;
                bool OnCassette() => Mathf.Abs(deck.TurnPosition - Mathf.Round(deck.TurnPosition)) < 0.002f;
                // Lets it come to rest, the hand held still well above it; true if it settled with a cassette at the front.
                bool Settles(float seconds)
                {
                    Hand(deck.EdgePoint(0f) + Vector3.up * 0.4f, Open);
                    for (float t = 0f; t < seconds && deck.Turning; t += Frame) Step();
                    return !deck.Turning && OnCassette();
                }

                // The hard area: pinch the edge to the right of the front and drag it slowly round to the left. It
                // follows the hand closely; held still, hand tracking's jitter barely moves it; let go, it settles
                // with another cassette at the front.
                Move(deck.EdgePoint(25f) + Vector3.up * 0.08f, 12, Open);
                Move(deck.EdgePoint(25f), 10, Open);
                Pinch(true);
                float gripFrom = deck.TurnPosition;
                for (int i = 1; i <= 72; i++)
                {
                    Hand(deck.EdgePoint(25f - 70f * i / 72f), Closed);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
                float dragged = deck.TurnPosition - gripFrom, handTurned = 70f / pitch;
                Expect(Mathf.Abs(dragged - handTurned) < 0.2f, $"a pinched edge follows the hand closely (turned {dragged:0.00} cassettes for the hand's {handTurned:0.00})");
                for (int i = 0; i < 22; i++)
                {
                    Hand(deck.EdgePoint(-45f), Closed);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
                var noise = new System.Random(5);
                float lastNoise = 0f, rawStep = 0f, heldStep = 0f, heldAt = deck.TurnPosition;
                for (int i = 0; i < 36; i++)
                {
                    float jitter = ((float)noise.NextDouble() * 2f - 1f) * 0.8f;
                    if (i > 0) rawStep = Mathf.Max(rawStep, Mathf.Abs(jitter - lastNoise) / pitch);
                    lastNoise = jitter;
                    Hand(deck.EdgePoint(-45f + jitter), Closed);
                    menu.Step(rig, manager, bus, camera, Frame);
                    heldStep = Mathf.Max(heldStep, Mathf.Abs(deck.TurnPosition - heldAt));
                    heldAt = deck.TurnPosition;
                }
                Expect(heldStep < rawStep * 0.35f, $"held still, tracking jitter barely moves it (at most {heldStep:0.0000} cassettes a frame for the hand's {rawStep:0.0000})");
                Pinch(false);
                bool dragSettled = Settles(3f);
                var front1 = deck.FrontSong;
                Expect(front1 != front0 && dragSettled, $"a pinch-drag turns another cassette to the front and it settles there ({Title(front0)} -> {Title(front1)}, settled {dragSettled})");

                // A quick turn of the edge, let go mid-turn: it spins on at the hand's speed (no faster than the top
                // speed), and the open hand resting where it let go doesn't stop it.
                Move(deck.EdgePoint(30f) + Vector3.up * 0.08f, 12, Open);
                Move(deck.EdgePoint(30f), 10, Open);
                Pinch(true);
                for (int i = 1; i <= 12; i++)
                {
                    Hand(deck.EdgePoint(30f - 60f * i / 12f), Closed);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
                Hand(pinchAt, Open);
                menu.Step(rig, manager, bus, camera, Frame);
                float flung = deck.TurnSpeed, flungAt = deck.TurnPosition;
                for (int i = 0; i < 36; i++)
                {
                    Hand(pinchAt, Open);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
                float gliding = deck.TurnSpeed, glided = deck.TurnPosition - flungAt;
                Expect(flung > 3f && flung <= deck.TurnFeel.TopSpeed + 0.01f && gliding > 2.5f && glided > 1.5f,
                    $"let go mid-turn, it spins on (let go at {flung:0.0} cassettes/s, still {gliding:0.0} half a second later, {glided:0.0} cassettes on)");

                // A hand put back into it catches it: lifted out of the turntable and lowered, open and still, over
                // the front cassettes, it brakes the spin quickly but smoothly, and the turntable settles.
                for (int i = 0; i < 3; i++)
                {
                    OpenHand(Over(0f) + Vector3.up * 0.35f, Vector3.zero);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
                float beforeCatch = deck.TurnSpeed, catchFrom = deck.TurnPosition;
                OpenHand(Over(0f), Vector3.zero);
                menu.Step(rig, manager, bus, camera, Frame);
                float firstCatch = deck.TurnSpeed;
                for (int i = 0; i < 28; i++)
                {
                    OpenHand(Over(0f), Vector3.zero);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
                float caught = Mathf.Abs(deck.TurnSpeed);
                bool catchSettled = Settles(3f);
                // Left to glide from that speed it would go on for several cassettes; caught, it stops within one or so.
                float caughtIn = Mathf.Abs(deck.TurnPosition - catchFrom);
                Expect(beforeCatch > 2f && firstCatch > beforeCatch * 0.7f && caught < beforeCatch * 0.4f && caughtIn < 1.2f && catchSettled,
                    $"a hand put back into it catches it with a smooth brake and it settles on a cassette ({beforeCatch:0.0} cassettes/s, then {firstCatch:0.0} a frame later, " +
                    $"{caught:0.00} after 0.4 s, stopped {caughtIn:0.00} cassettes on, settled {catchSettled})");

                // The soft area: an open hand sliding slowly across the cassettes (15 cm/s) carries the turntable along
                // gently; when it stops, the turntable settles.
                var nudgeStart = Over(25f);
                var nudgeEnd = Over(-25f);
                OpenHand(nudgeStart + Vector3.up * 0.1f, Vector3.zero);
                menu.Step(rig, manager, bus, camera, Frame);
                for (int i = 0; i < 6; i++)
                {
                    OpenHand(nudgeStart, Vector3.zero);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
                float nudgeFrom = deck.TurnPosition, nudgeTop = 0f;
                int slowFrames = Mathf.RoundToInt(Vector3.Distance(nudgeStart, nudgeEnd) / 0.15f / Frame);
                Slide(nudgeStart, nudgeEnd, slowFrames, () => nudgeTop = Mathf.Max(nudgeTop, Mathf.Abs(deck.TurnSpeed)));
                float nudged = deck.TurnPosition - nudgeFrom;
                for (int i = 0; i < 14; i++)
                {
                    OpenHand(nudgeEnd, Vector3.zero);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
                bool nudgeSettled = Settles(3f);
                Expect(nudged > 0.4f && nudgeTop < 1.5f && nudgeSettled,
                    $"a slow slide of an open hand moves it gently ({nudged:0.00} cassettes at up to {nudgeTop:0.00} cassettes/s) and it settles (settled {nudgeSettled})");

                // A quick slide flicks it into a spin that glides on after the hand stops in the turntable; a pinch on
                // the edge of the spinning turntable catches it too, slipping at first and then holding.
                var flickFrom = Over(35f);
                var flickTo = Over(-35f);
                OpenHand(flickFrom + Vector3.up * 0.1f, Vector3.zero);
                menu.Step(rig, manager, bus, camera, Frame);
                Slide(flickFrom, flickTo, 12, null);
                float flicked = deck.TurnSpeed, flickedAt = deck.TurnPosition;
                for (int i = 0; i < 43; i++)
                {
                    OpenHand(flickTo, Vector3.zero);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
                float flickGliding = deck.TurnSpeed, flickGlided = deck.TurnPosition - flickedAt;
                Expect(flicked > 2.5f && flickGliding > 1.5f && flickGlided > 1f,
                    $"a quick slide flicks it into a glide ({flicked:0.0} cassettes/s as the hand stops, {flickGliding:0.0} 0.6 s later, {flickGlided:0.0} cassettes on)");
                rig.Right.Velocity = Vector3.zero;
                Hand(deck.EdgePoint(0f), Open);
                menu.Step(rig, manager, bus, camera, Frame);
                float beforeGrip = deck.TurnSpeed;
                Hand(deck.EdgePoint(0f), Closed);
                menu.Step(rig, manager, bus, camera, Frame);
                float firstGrip = deck.TurnSpeed;
                for (int i = 0; i < 29; i++)
                {
                    Hand(deck.EdgePoint(0f), Closed);
                    menu.Step(rig, manager, bus, camera, Frame);
                }
                float held = Mathf.Abs(deck.TurnSpeed);
                Pinch(false);
                bool gripSettled = Settles(3f);
                Expect(beforeGrip > 1f && firstGrip > beforeGrip * 0.6f && held < 0.3f && gripSettled,
                    $"a pinch on the spinning edge catches it smoothly ({beforeGrip:0.0} cassettes/s, then {firstGrip:0.0} a frame later, {held:0.00} after 0.4 s) and it settles (settled {gripSettled})");
                string turns = $"Turntable: a pinch-drag followed the hand to {Mathf.Abs(dragged - handTurned):0.00} cassettes, and held still its largest step was " +
                               $"{heldStep / Mathf.Max(rawStep, 1e-6f) * 100f:0}% of the hand's jitter; let go mid-turn at {flung:0.0} cassettes/s it glided on " +
                               $"{glided:0.0} cassettes in 0.5 s; a hand put back cut {beforeCatch:0.0} to {caught:0.00} cassettes/s in 0.4 s and it stopped {caughtIn:0.00} cassettes on; " +
                               $"a pinch on the spinning edge held it {held:0.00} cassettes/s after 0.4 s; a slow slide moved it {nudged:0.00} cassettes " +
                               $"at up to {nudgeTop:0.00} cassettes/s; a flick glided {flickGlided:0.0} cassettes in 0.6 s; every turn settled on a cassette.";

                // Pinch the front cassette, carry it to the radio's door: the outline shows; let go: it plays.
                var first = deck.FrontSong ?? deck.SongBeside(1);
                PickUp(first);
                Expect(deck.InHand(first), $"pinching the front cassette picks up {Title(first)}");
                CarryToRadio();
                Expect(deck.ShowsSnapPreview, "the snap outline shows at the radio's door");
                Pinch(false);
                Move(pinchAt + Vector3.up * 0.12f - gaze * 0.05f, 10, Open);
                Run(1.2f);
                Expect(deck.InsertedSong == first && bus.CurrentSong == first, $"letting go puts it in and plays {Title(first)} (radio: {Title(deck.InsertedSong)}, playing: {Title(bus.CurrentSong)})");

                // Put in another: the first pops out and goes home.
                var second = deck.SongBeside(1) ?? deck.SongBeside(-1);
                PickUp(second);
                Expect(deck.InHand(second), $"picking up a second cassette ({Title(second)})");
                CarryToRadio();
                Pinch(false);
                Move(pinchAt + Vector3.up * 0.12f - gaze * 0.05f, 10, Open);
                Run(1.6f);
                Expect(deck.InsertedSong == second && bus.CurrentSong == second, $"the second one plays ({Title(deck.InsertedSong)})");
                Expect(deck.OnTurntable(first), $"the first one is back on the turntable");

                // Eject: the cassette goes home and the song stops at its start.
                Press("EjectButton");
                Run(1.6f);
                Expect(deck.InsertedSong == null && deck.OnTurntable(second), "Eject pops the cassette out and home");

                // Play with the radio empty puts the front cassette in and plays it.
                Press("PlayButton");
                Run(1.2f);
                Expect(deck.InsertedSong != null && bus.CurrentSong == deck.InsertedSong, "Play with the radio empty plays the front cassette");

                // The song list button: the deck steps aside for the song page; the page's Back comes back to the deck.
                Press("Song List Button");
                Run(0.8f);
                var page = root.Find("Song Library");
                Expect(!deck.IsOpen && page != null && page.gameObject.activeSelf, "the song list button opens the song page");
                var back = page != null ? page.Find("Back") : null;
                if (back != null)
                {
                    MoveTip(back.position - root.forward * 0.08f, 10);
                    MoveTip(back.position + root.forward * 0.01f, 8);
                    MoveTip(back.position - root.forward * 0.1f, 8);
                    MoveTip(camera.transform.position + new Vector3(0.25f, -0.25f, 0.1f), 8);
                }
                Run(1.2f);
                Expect(deck.IsOpen && !root.gameObject.activeSelf, "the song page's Back returns to the deck");

                // Back at the deck, a slide of an open hand across the front cassettes still sends it round, and it
                // settles with another cassette at the front.
                var beforeSweep = deck.FrontSong;
                OpenHand(Over(30f) + Vector3.up * 0.1f, Vector3.zero);
                menu.Step(rig, manager, bus, camera, Frame);
                Slide(Over(30f), Over(-30f), 30, null);
                rig.Right.Velocity = Vector3.zero;
                bool sweepSettled = Settles(6f);
                Expect(deck.FrontSong != beforeSweep && sweepSettled, $"a slide of an open hand turns it ({Title(beforeSweep)} -> {Title(deck.FrontSong)}, settled {sweepSettled})");

                // A frame of the deck, hands near it and a song playing, allocates no memory: resting in the turntable,
                // brushing it along and holding its edge.
                Move(deck.FrontPlace + Vector3.up * 0.05f, 10, Open);
                for (int i = 0; i < 10; i++) deck.Step(rig, bus, camera, Frame);
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 120; i++) deck.Step(rig, bus, camera, Frame);
                long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
                // A short, slow slide (too short to bring another cassette to the front, which rewrites its title).
                var brushFrom = Over(4f);
                var brushTo = Over(-4f);
                var brushVelocity = (brushTo - brushFrom) / (16 * Frame);
                OpenHand(brushFrom, Vector3.zero);
                deck.Step(rig, bus, camera, Frame);
                before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = 1; i <= 16; i++)
                {
                    OpenHand(Vector3.Lerp(brushFrom, brushTo, i / 16f), brushVelocity);
                    deck.Step(rig, bus, camera, Frame);
                }
                long brushing = System.GC.GetAllocatedBytesForCurrentThread() - before;
                rig.Right.Velocity = Vector3.zero;
                Settles(3f);
                Move(deck.EdgePoint(-20f) + Vector3.up * 0.08f, 10, Open);
                Move(deck.EdgePoint(-20f), 8, Open);
                Pinch(true);
                before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 60; i++) deck.Step(rig, bus, camera, Frame);
                long holding = System.GC.GetAllocatedBytesForCurrentThread() - before;
                Pinch(false);
                Settles(3f);
                Expect(allocated == 0 && brushing == 0 && holding == 0,
                    $"no memory allocated per frame ({allocated} bytes over 120 frames with a hand resting, {brushing} over 16 brushing, {holding} over 60 holding the edge)");

                // The blank cassette in the radio opens the file picker on the headset; here it says so, comes
                // back out, and the song that is still playing goes back in.
                var playingNow = bus.CurrentSong;
                PickUpAt(deck.BlankCenter);
                CarryToRadio();
                Pinch(false);
                Move(pinchAt + Vector3.up * 0.12f - gaze * 0.05f, 10, Open);
                Run(0.3f);
                bool blankIn = deck.BlankInRadio;
                Run(2.6f);
                Expect(blankIn && deck.BlankOnTurntable && deck.InsertedSong == playingNow && bus.CurrentSong == playingNow,
                    $"the blank cassette goes in, says the picker opens on the headset and comes home, and {Title(playingNow)} goes back in (in: {blankIn}, home: {deck.BlankOnTurntable}, radio: {Title(deck.InsertedSong)})");

                // The close button closes it; Meta's menu gesture does too.
                Press("Close Button");
                Run(0.8f);
                Expect(!deck.IsOpen && !menu.IsOpen && !deck.Root.gameObject.activeSelf, "the close button closes the deck and leaves you in the world");
                menu.OpenDeck(camera);
                Run(1.2f);
                bool reopened = deck.IsOpen;
                menu.Toggle(camera);
                Run(0.8f);
                Expect(reopened && !deck.IsOpen && !menu.IsOpen, "Meta's menu gesture closes the deck");

                // Standing (eyes 1.6 m up), the table grows so the deck stays within easy reach.
                camera.transform.position = new Vector3(0f, 1.6f, 0f);
                menu.OpenDeck(camera);
                Run(1.2f);
                float standingTop = deck.TableHeight;
                float standingReach = Vector3.ProjectOnPlane(deck.FrontPlace - camera.transform.position, Vector3.up).magnitude;
                Expect(standingTop > 0.9f && standingTop < 1f && standingReach > 0.35f && standingReach < 0.45f,
                    $"standing, the table top rises to {standingTop:0.00} m with the front cassette {standingReach:0.00} m ahead");
                menu.Toggle(camera);
                Run(0.6f);
                return Report(failures, $"Seated: front cassette {reach:0.00} m ahead, everything to touch within {widest:0.0}° of the gaze ({widestName}), the words over the turntable within {plateWidest:0.0}° and clear of the front labels, table top {tableTop:0.00} m; standing: table top {standingTop:0.00} m. " + turns);
            }
            finally
            {
                LyricsOption.On = lyricsWere;
                RunMessage(menu, "OnDisable");
                Shader.SetGlobalVector("_CassetteDeckClip", Vector4.zero);
                Object.DestroyImmediate(bus.gameObject);
            }
        }

        static string Title(SongAsset song) => song == null ? "none" : song.title;

        /// <summary>Empty if a part sits on the expected side of its model (x or z sign), else what is wrong.</summary>
        static string Side(Transform model, string part, int x = 0, int z = 0)
        {
            var t = DeckParts.Find(model, part);
            if (t == null) return "";
            var b = DeckParts.LocalBounds(model, t);
            var at = t.GetComponent<MeshFilter>() != null ? b.center : model.InverseTransformPoint(t.position);
            bool fine = (x == 0 || Mathf.Sign(at.x) == x) && (z == 0 || Mathf.Sign(at.z) == z);
            return fine ? "" : $"{part} is at {at}, on the wrong side (want x {x}, z {z}: front on -Z, left on -X). ";
        }

        static bool Report(List<string> failures, string measures = "")
        {
            if (failures.Count == 0)
            {
                Debug.Log("[Check] The cassette deck works by hand: the song bubble opens it within reach and view; a pinched edge turns it, following " +
                          "the hand closely and smoothly, and let go mid-turn it spins on; a hand put back in it, or a pinch on its spinning edge, catches it " +
                          "smoothly; a slow slide of an open hand nudges it and a quick one flicks it into a glide; it always settles on a cassette; " +
                          "a pinched cassette shows the snap outline at the radio and plays when let go, the next one pops it home, Eject and Play work, " +
                          "the song list and its Back button come and go, the blank cassette asks for the file picker, the close button " +
                          "and the menu gesture close it, standing raises the table, and a frame allocates nothing. " + measures);
                return true;
            }
            Debug.LogError("[Check] Cassette deck: " + string.Join("; ", failures) + ".");
            return false;
        }
    }
}
