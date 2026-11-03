using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Screenshots of every world as a player would see it, without a headset: a stand-in room (passthrough-like
    /// colours), the camera at head height with a wide headset-like view, hands posed for each world, and three
    /// seconds of the demo song's beat and melody played through before each shot (taken just after a chorus beat).
    /// Writes Logs/shots/*.png. Batch mode (needs graphics, so no -nographics):
    /// -executeMethod MusicInYourHands.EditorTools.EditorChecks.CaptureShotsBatch
    /// </summary>
    public static partial class EditorChecks
    {
        enum HandPose { PalmsToFace, PalmsFacing, PalmsUp, PalmsDown, BacksToFace }

        const string ShotFolder = "Logs/shots";
        const float Beat = 60f / 96f;
        const float ChorusStart = 25f;

        [MenuItem("Music In Your Hands/Capture Scene Screenshots")]
        public static void CaptureShots() => CaptureAll();

        public static void CaptureShotsBatch() => EditorApplication.Exit(CaptureAll() ? 0 : 1);

        static bool CaptureAll()
        {
#if META_XR_CORE
            Directory.CreateDirectory(ShotFolder);
            LoadSceneSettings();

            Shot<LivingSkin>("1-living-skin", HandPose.PalmsToFace, 8f);
            Shot<HeartbeatOrb>("2-heartbeat-orb", HandPose.PalmsFacing, 18f, closeUp: rig => HandSkin.FocusPosition);
            Shot<WaveformRibbon>("3-waveform-ribbon", HandPose.BacksToFace, 8f, rig => { rig.Left.Pinch = 1f; rig.Right.Pinch = 1f; });
            Shot<AirStrings>("4-air-strings", HandPose.BacksToFace, 12f);
            Shot<NoteRain>("5-note-rain", HandPose.PalmsUp, 34f, closeUp: rig => rig.Right.Palm.position + new Vector3(-0.1f, 0.1f, 0.05f));
            Shot<FireflyChoir>("6-firefly-choir", HandPose.PalmsToFace, 8f, closeUp: rig => rig.Right.Palm.position);
            // The right hand dips into the pool; the left rests above it.
            Shot<SoundPool>("8-sound-pool", HandPose.PalmsDown, 38f, closeUp: rig => rig.Right.Palm.position + Vector3.down * 0.04f,
                handOffset: side => side == "R" ? new Vector3(-0.03f, -0.27f, 0.12f) : new Vector3(0f, -0.12f, 0.1f));
            Shot<SoundWind>("9-sound-wind", HandPose.PalmsToFace, 22f, closeUp: rig => rig.Left.Palm.position,
                handOffset: side => side == "L" ? new Vector3(-0.05f, 0f, 0.15f) : new Vector3(0.05f, -0.1f, 0f));
            Shot<BubbleNotes>("10-bubble-notes", HandPose.BacksToFace, 4f, closeUp: rig => rig.Right.Tips[1] + new Vector3(-0.08f, 0.12f, 0.12f), lead: 8f);
            Shot<OrbitType>("12-orbit-type", HandPose.PalmsToFace, 2f, closeUp: rig => rig.Right.Palm.position + new Vector3(-0.2f, 0.05f, 0.7f),
                handOffset: side => side == "R" ? new Vector3(0.05f, 0.05f, 0.2f) : new Vector3(0f, -0.1f, 0f));
            Shot<Graffiti>("13-graffiti", HandPose.BacksToFace, 6f, rig => rig.Right.Pinch = 1f, closeUp: rig => new Vector3(0.3f, 1.45f, 1.55f),
                handOffset: side => side == "R" ? new Vector3(0.02f, 0.08f, 0.12f) : new Vector3(0f, -0.15f, 0f), lead: 6f);
            Shot<BreathingRoom>("14-breathing-room", HandPose.PalmsToFace, 6f);
            Shot<LightTrails>("15-light-trails", HandPose.BacksToFace, 4f, prepare: world => DrawPreviewSculpture((LightTrails)world));
            Shot<NeonSign>("16-neon-sign", HandPose.BacksToFace, 2f, lead: 4f, extra: 1.7f);
            Shot<WordDrift>("17-word-drift", HandPose.PalmsToFace, 2f, lead: 6f);
            WordDrift drift = null;
            int heldFrames = 0;
            Shot<WordDrift>("17h-word-drift-held", HandPose.PalmsToFace, 2f, lead: 7f, prepare: world => drift = (WordDrift)world,
                holdPose: _ => { if (++heldFrames % 24 == 0) drift.PreviewHold(); });
            Shot<RisingFloor>("18-rising-floor", HandPose.PalmsToFace, 20f, lead: 4f);
            Shot<WordSnow>("19-word-snow", HandPose.PalmsUp, 10f, lead: 9f);
            Shot<CeilingStars>("20-ceiling-stars", HandPose.PalmsToFace, -40f, lead: 4f);
            Shot<SandTable>("11-sand-table", HandPose.PalmsDown, 40f, closeUp: rig => rig.Right.Palm.position + Vector3.down * 0.08f,
                handOffset: side => side == "R" ? new Vector3(0.02f, -0.24f, 0.12f) : new Vector3(-0.05f, -0.12f, 0.06f));
            MenuShot();

            // The same worlds in a scanned, furnished room: on the real table, walls, ceiling and furniture.
            Shot<SoundPool>("8r-sound-pool-room", HandPose.PalmsDown, 42f, closeUp: rig => rig.Right.Palm.position + Vector3.down * 0.04f,
                handOffset: side => side == "R" ? new Vector3(-0.03f, -0.53f, 0.15f) : new Vector3(0f, -0.35f, 0.1f), scanned: true);
            Shot<SandTable>("11r-sand-table-room", HandPose.PalmsDown, 45f, handOffset: side => side == "R" ? new Vector3(0.02f, -0.45f, 0.15f) : new Vector3(-0.05f, -0.3f, 0.06f),
                scanned: true);
            Shot<Graffiti>("13r-graffiti-room", HandPose.BacksToFace, 4f, rig => rig.Right.Pinch = 1f,
                handOffset: side => side == "R" ? new Vector3(0.02f, 0.08f, 0.12f) : new Vector3(0f, -0.15f, 0f), lead: 6f, scanned: true);
            Shot<BreathingRoom>("14r-breathing-room-room", HandPose.PalmsToFace, 6f, scanned: true);
            Shot<NeonSign>("16r-neon-sign-room", HandPose.BacksToFace, 2f, lead: 4f, extra: 1.7f, scanned: true);
            Shot<WordSnow>("19r-word-snow-room", HandPose.PalmsUp, 22f, lead: 14f, scanned: true);
            Shot<CeilingStars>("20r-ceiling-stars-room", HandPose.PalmsToFace, -40f, lead: 4f, scanned: true);
            Shot<NoteRain>("5r-note-rain-room", HandPose.PalmsUp, 30f, scanned: true);
            Room.Clear();
            Debug.Log($"[Shots] Saved screenshots to {ShotFolder}.");
            return true;
#else
            return false;
#endif
        }

#if META_XR_CORE
        // Each world's settings as saved in the main scene, so the shots match what runs on the headset.
        static readonly Dictionary<System.Type, string> SceneSettings = new Dictionary<System.Type, string>();

        static void LoadSceneSettings()
        {
            SceneSettings.Clear();
            EditorSceneManager.OpenScene(ProjectSetup.MainScenePath, OpenSceneMode.Single);
            foreach (var world in Object.FindObjectsByType<World>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                SceneSettings[world.GetType()] = EditorJsonUtility.ToJson(world);
        }

        /// <summary>
        /// A room standing in for passthrough: floor and three walls in soft, warm grey tones. With
        /// <paramref name="scanned"/>, a smaller furnished room (a table in front, a sofa on the right, a shelf at the
        /// back, a ceiling) that the worlds also get as a room scan.
        /// </summary>
        static Camera StageRoom(float pitch, bool scanned = false)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Room.Clear();
            var material = ProjectSetup.EnsureDebugMaterial();
            var block = new MaterialPropertyBlock();
            void Panel(string name, Vector3 position, Vector3 euler, Vector2 size, Color color)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = name;
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
                quad.transform.localScale = new Vector3(size.x, size.y, 1f);
                var r = quad.GetComponent<Renderer>();
                r.sharedMaterial = material;
                block.SetColor("_BaseColor", color);
                r.SetPropertyBlock(block);
            }
            Panel("Floor", new Vector3(0f, 0f, 1f), new Vector3(90f, 0f, 0f), new Vector2(6f, 6f), new Color(0.36f, 0.32f, 0.28f));
            if (scanned)
                Furnish(Panel, material, block);
            else
            {
                Panel("Back Wall", new Vector3(0f, 1.5f, 2.6f), Vector3.zero, new Vector2(6f, 3f), new Color(0.55f, 0.51f, 0.46f));
                Panel("Left Wall", new Vector3(-1.9f, 1.5f, 1f), new Vector3(0f, -90f, 0f), new Vector2(6f, 3f), new Color(0.5f, 0.46f, 0.42f));
                Panel("Right Wall", new Vector3(1.9f, 1.5f, 1f), new Vector3(0f, 90f, 0f), new Vector2(6f, 3f), new Color(0.5f, 0.46f, 0.42f));
            }

            var camera = new GameObject("Head").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.42f, 0.38f);
            camera.fieldOfView = 78f;
            camera.nearClipPlane = 0.02f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.Euler(pitch, 0f, 0f));
            return camera;
        }

        /// <summary>The scanned stand-in room: walls, ceiling and furniture, drawn and handed to the worlds as Room.</summary>
        static void Furnish(System.Action<string, Vector3, Vector3, Vector2, Color> panel, Material material, MaterialPropertyBlock block)
        {
            const float height = 2.5f, back = 1.5f, behind = -1.5f, side = 1.7f;
            var wallColor = new Color(0.52f, 0.48f, 0.44f);
            panel("Back Wall", new Vector3(0f, height * 0.5f, back), Vector3.zero, new Vector2(side * 2f, height), new Color(0.55f, 0.51f, 0.46f));
            panel("Left Wall", new Vector3(-side, height * 0.5f, 0f), new Vector3(0f, -90f, 0f), new Vector2(back - behind, height), wallColor);
            panel("Right Wall", new Vector3(side, height * 0.5f, 0f), new Vector3(0f, 90f, 0f), new Vector2(back - behind, height), wallColor);
            panel("Ceiling", new Vector3(0f, height, 0f), new Vector3(-90f, 0f, 0f), new Vector2(side * 2f, back - behind), new Color(0.6f, 0.57f, 0.53f));
            void Box(string name, Vector3 top, Vector3 size, Color color)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = name;
                Object.DestroyImmediate(box.GetComponent<Collider>());
                box.transform.position = top + Vector3.down * (size.y * 0.5f);
                box.transform.localScale = size;
                var r = box.GetComponent<Renderer>();
                r.sharedMaterial = material;
                block.SetColor("_BaseColor", color);
                r.SetPropertyBlock(block);
            }
            var table = new Vector3(0f, 0.75f, 0.55f);
            var sofa = new Vector3(1.25f, 0.45f, 0.4f);
            var shelf = new Vector3(-1.35f, 0.95f, 1.3f);
            Box("Table", table, new Vector3(1.2f, 0.04f, 0.7f), new Color(0.42f, 0.33f, 0.26f));
            Box("Sofa", sofa, new Vector3(0.8f, 0.45f, 1.5f), new Color(0.3f, 0.36f, 0.42f));
            Box("Shelf", shelf, new Vector3(0.6f, 0.95f, 0.35f), new Color(0.46f, 0.4f, 0.34f));

            Room.Set(0f, height,
                new[]
                {
                    new Room.Surface(new Vector3(0f, height * 0.5f, back), Vector3.back, Vector3.right, new Vector2(side * 2f, height)),
                    new Room.Surface(new Vector3(-side, height * 0.5f, 0f), Vector3.right, Vector3.back, new Vector2(back - behind, height)),
                    new Room.Surface(new Vector3(side, height * 0.5f, 0f), Vector3.left, Vector3.forward, new Vector2(back - behind, height)),
                    new Room.Surface(new Vector3(0f, height * 0.5f, behind), Vector3.forward, Vector3.left, new Vector2(side * 2f, height))
                },
                new[] { new Room.Surface(table, Vector3.up, Vector3.right, new Vector2(1.2f, 0.7f)) },
                new[]
                {
                    new Room.Surface(sofa, Vector3.up, Vector3.right, new Vector2(0.8f, 1.5f)),
                    new Room.Surface(shelf, Vector3.up, Vector3.right, new Vector2(0.6f, 0.35f))
                });
        }

        /// <summary>Both hands about 35 cm in front, at chest height, held the way the world asks for.</summary>
        static List<PreviewHand> PlaceHands(Camera camera, HandPose pose, HandRig rig, System.Func<string, Vector3> offset = null)
        {
            var hands = new List<PreviewHand>();
            foreach (var (side, x) in new[] { ("L", -0.15f), ("R", 0.15f) })
            {
                var path = $"Packages/com.meta.xr.sdk.core/Prefabs/OpenXRCustomHandPrefab_{side}.prefab";
                var hand = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                ProjectSetup.ApplyHandLook(hand);
                var skin = hand.GetComponentInChildren<SkinnedMeshRenderer>();
                var bones = hand.GetComponentsInChildren<Transform>();
                Transform Bone(string n) => bones.First(t => t.name == n);
                bool left = side == "L";

                // Fingers up, then turn the palm the way the pose wants.
                var up = (Bone("XRHand_MiddleProximal").position - Bone("XRHand_Wrist").position).normalized;
                hand.transform.rotation = Quaternion.FromToRotation(up, Vector3.up) * hand.transform.rotation;
                var center = new Vector3(x, 1.3f, 0.36f) + (offset?.Invoke(side) ?? Vector3.zero);
                hand.transform.position += center - skin.bounds.center;
                Vector3 want = pose switch
                {
                    HandPose.PalmsToFace or HandPose.PalmsUp => Vector3.back,
                    // Turned a little towards the face, the way you hold a ball in front of you.
                    HandPose.PalmsFacing => ((left ? Vector3.right : Vector3.left) + Vector3.back * 0.6f).normalized,
                    HandPose.BacksToFace or HandPose.PalmsDown => Vector3.forward,
                    _ => Vector3.forward
                };
                var normal = Vector3.ProjectOnPlane(PalmNormal(Bone, left), Vector3.up).normalized;
                hand.transform.RotateAround(skin.bounds.center, Vector3.up, Vector3.SignedAngle(normal, want, Vector3.up));
                if (pose == HandPose.PalmsUp)
                    // Tip the hand forward: fingers point away, palm up, as if offering it.
                    hand.transform.RotateAround(skin.bounds.center, Vector3.right, 75f);
                else if (pose == HandPose.PalmsDown)
                    hand.transform.RotateAround(skin.bounds.center, Vector3.right, 75f);
                else if (pose == HandPose.BacksToFace)
                    hand.transform.RotateAround(skin.bounds.center, Vector3.right, 20f);

                var preview = new PreviewHand { Root = hand, Skin = skin, Left = left };
                PushHand(rig, preview);
                hands.Add(preview);
            }
            return hands;
        }

        /// <summary>The demo song's beat and melody around the start of the first chorus.</summary>
        sealed class SongClock
        {
            readonly NoteTrack notes = NoteTrack.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Audio/GlassRoom.notes")?.text);
            readonly LyricsTrack lyrics = LyricsTrack.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Audio/GlassRoom.lrc")?.text);
            readonly List<NoteTrack.Note> upcoming = new List<NoteTrack.Note>();
            readonly float[] bands = new float[6];
            public float Time = ChorusStart - 3f;
            float lastBeat = -10f;

            public float BeatAge => Time - lastBeat;
            public float Kick => Mathf.Exp(-BeatAge * 7f);

            public MusicFrame Advance(float dt)
            {
                float before = Time;
                Time += dt;
                bool beat = Mathf.Floor(Time / Beat) > Mathf.Floor(before / Beat);
                if (beat) lastBeat = Mathf.Floor(Time / Beat) * Beat;
                bool singing = notes.Notes.Any(n => Time >= n.Time && Time < n.Time + n.Length);
                float k = Kick;
                bands[0] = 0.15f + k * 0.85f;
                bands[1] = 0.35f + k * 0.55f;
                bands[2] = 0.4f + 0.15f * Mathf.Sin(Time * 2.1f) + (singing ? 0.25f : 0f);
                bands[3] = 0.35f + 0.15f * Mathf.Sin(Time * 3.3f + 1f) + (singing ? 0.3f : 0f);
                bands[4] = 0.3f + 0.2f * Mathf.Sin(Time * 4.1f + 2f);
                bands[5] = 0.25f + 0.25f * Mathf.Abs(Mathf.Sin(Time * Mathf.PI / (Beat * 0.5f)));
                upcoming.Clear();
                notes.Between(Time, Time + 3f, upcoming);
                return new MusicFrame { Bands = bands, Kick = k, Beat = beat, SongTime = Time, Upcoming = upcoming, Lyrics = lyrics, Lyric = lyrics.Lookup(Time),
                    Notes = notes, BeatAge = BeatAge, BeatInterval = Beat };
            }
        }

        // Set SHOTS to part of a shot's name (for example "orb") to capture only the matching shots.
        static bool Wanted(string name)
        {
            var only = System.Environment.GetEnvironmentVariable("SHOTS");
            return string.IsNullOrEmpty(only) || only.Split(',').Any(name.Contains);
        }

        /// <summary>
        /// One world's screenshot from head height. With <paramref name="closeUp"/>, also a zoomed-in filmstrip of
        /// four frames 0.1 s apart around that point (name-close.png), to judge detail and motion.
        /// </summary>
        static void Shot<T>(string name, HandPose pose, float pitch, System.Action<HandRig> holdPose = null,
            System.Func<HandRig, Vector3> closeUp = null, System.Func<string, Vector3> handOffset = null, float lead = 3f,
            System.Action<World> prepare = null, float extra = 0f, bool scanned = false) where T : World
        {
            if (!Wanted(name)) return;
            var camera = StageRoom(pitch, scanned);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var hands = PlaceHands(camera, pose, rig, handOffset);

            var world = new GameObject(typeof(T).Name).AddComponent<T>();
            if (SceneSettings.TryGetValue(typeof(T), out var json)) EditorJsonUtility.FromJsonOverwrite(json, world);
            else Debug.LogWarning($"[Shots] {typeof(T).Name} is not in the main scene; using its defaults.");
            world.Build();
            prepare?.Invoke(world);
            HandSkin.Theme = world.Theme;
            HandSkin.FocusStrength = 0f;
            HandSkin.Charge[0] = HandSkin.Charge[1] = 0f;

            // Play from <lead> seconds before the chorus to just after its first beat.
            var clock = new SongClock { Time = ChorusStart - lead };
            const float dt = 1f / 30f;
            void Play(float until)
            {
                while (clock.Time < until)
                {
                    holdPose?.Invoke(rig);
                    world.Step(rig, clock.Advance(dt), camera, dt);
                    world.SimulateParticles(dt);
                }
                foreach (var hand in hands)
                {
                    hand.Root.GetComponent<HandSkin>().Apply();
                    SetBeat(hand.Skin, clock.BeatAge, clock.Kick);
                }
            }
            Play(ChorusStart + 0.09f + extra);
            File.WriteAllBytes($"{ShotFolder}/{name}.png", Render(camera, 1600, 900).EncodeToPNG());
            if (closeUp == null) return;

            camera.fieldOfView = 34f;
            const int w = 800, h = 450;
            var sheet = new Texture2D(w * 2, h * 2, TextureFormat.RGB24, false);
            for (int frame = 0; frame < 4; frame++)
            {
                if (frame > 0) Play(clock.Time + 0.1f);
                camera.transform.LookAt(closeUp(rig));
                sheet.SetPixels(frame % 2 * w, (1 - frame / 2) * h, w, h, Render(camera, w, h).GetPixels());
            }
            sheet.Apply();
            File.WriteAllBytes($"{ShotFolder}/{name}-close.png", sheet.EncodeToPNG());
        }

        /// <summary>A few strokes of light in front of the viewer, as if drawn there, for the Light trails shot.</summary>
        static void DrawPreviewSculpture(LightTrails trails)
        {
            var points = new List<Vector3>();
            for (int i = 0; i < 90; i++)
            {
                float a = i / 90f * Mathf.PI * 2.4f;
                points.Add(new Vector3(Mathf.Sin(a) * 0.32f, 1.45f + Mathf.Sin(a * 2f) * 0.16f, 0.75f + Mathf.Cos(a) * 0.12f));
            }
            trails.PreviewStroke(points, Vector3.back, new Color(1f, 0.37f, 0.68f));
            points.Clear();
            for (int i = 0; i < 70; i++)
            {
                float a = i / 70f * Mathf.PI * 2f;
                points.Add(new Vector3(-0.15f + Mathf.Cos(a) * 0.18f, 1.35f + i * 0.004f, 0.6f + Mathf.Sin(a) * 0.18f));
            }
            trails.PreviewStroke(points, Vector3.up, new Color(0.49f, 0.95f, 0.79f));
        }

        static void MenuShot()
        {
            if (!Wanted("7-menu")) return;
            var camera = StageRoom(6f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var hands = PlaceHands(camera, HandPose.BacksToFace, rig);
            // The right index finger reaches towards a bubble; the left hand rests lower.
            hands[0].Root.transform.position += new Vector3(-0.05f, -0.22f, 0f);
            PushHand(rig, hands[0]);

            var managerGo = new GameObject("Worlds");
            var worlds = WorldCatalog.MakePreviews(managerGo.transform);
            var manager = managerGo.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Length;
            for (int i = 0; i < worlds.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            manager.Switch(0);
            HandSkin.Theme = HandTheme.Veins;

            var menu = new GameObject("WorldMenu").AddComponent<WorldMenu>();
            var wso = new SerializedObject(menu);
            wso.FindProperty("bubbleMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble");
            wso.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            wso.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            wso.ApplyModifiedPropertiesWithoutUndo();
            menu.Step(rig, manager, null, camera, 0f);
            menu.Open(camera);
            for (int i = 0; i < 24; i++) menu.Step(rig, manager, null, camera, 0.05f);
            foreach (var hand in hands) hand.Root.GetComponent<HandSkin>().Apply();
            File.WriteAllBytes($"{ShotFolder}/7-menu.png", Render(camera, 1600, 900).EncodeToPNG());

            // The song library's pages, with made-up songs.
            SongAsset Song(string title, string artist, bool lyrics)
            {
                var song = ScriptableObject.CreateInstance<SongAsset>();
                song.title = title;
                song.artist = artist;
                song.audioPath = title;
                song.lyricsPath = lyrics ? title : null;
                return song;
            }
            var songs = new[]
            {
                Song("Sunday Morning", "The Open Windows", true), Song("A very long song title that needs to be shortened somehow", "Someone", false),
                Song("Harbour Lights", "", true), Song("Slow River", "Anna and the Tide", false), Song("Last One", "", false)
            };
            void Page(string name, IReadOnlyList<SongAsset> list, int view)
            {
                menu.PreviewLibrary(list, list.Count > 0 ? list[0] : null, view);
                for (int i = 0; i < 24; i++) menu.Step(rig, manager, null, camera, 0.05f);
                File.WriteAllBytes($"{ShotFolder}/{name}.png", Render(camera, 1600, 900).EncodeToPNG());
            }
            Page("7a-library", songs, 0);
            Page("7b-library-empty", new SongAsset[0], 0);
            Page("7c-library-ask-lyrics", songs, 1);
            Page("7d-library-remove", songs, 2);

            // The Audius tab, a song's credits, and the menu's credit line while a song from Audius plays.
            SongAsset FromAudius(string title, string artist, string owner, string page)
            {
                var song = ScriptableObject.CreateInstance<SongAsset>();
                song.title = title;
                song.artist = artist;
                song.owner = owner;
                song.audiusId = page;
                song.link = "https://audius.co/" + page;
                return song;
            }
            var streamed = new[]
            {
                FromAudius("The Night Air Was Cool", "ATTLAS", "ATTLAS", "attlas/the-night-air-was-cool-90231"),
                FromAudius("It's Okay", "Yasper", "Chillhop Music", "chillhopmusic/yasper-its-okay"),
                FromAudius("Soul Trouble", "Kevin McKay", "Kevin McKay", "kevinmckay/soul-trouble"),
                FromAudius("fsh (AUDIUS EXCLUSIVE)", "harris cole", "harris cole", "harriscole/fsh-audius-exclusive-140992"),
                FromAudius("Crystals", "Fonts", "Critical Music", "CriticalMusic/fonts-crystals")
            };
            Page("7e-library-audius", streamed, 3);
            Page("7f-library-credits", new[] { streamed[1] }, 4);
            menu.Close();
            menu.PreviewCredit(streamed[1]);
            menu.Open(camera);
            for (int i = 0; i < 24; i++) menu.Step(rig, manager, null, camera, 0.05f);
            File.WriteAllBytes($"{ShotFolder}/7g-menu-audius-credit.png", Render(camera, 1600, 900).EncodeToPNG());
        }
#endif
    }
}
