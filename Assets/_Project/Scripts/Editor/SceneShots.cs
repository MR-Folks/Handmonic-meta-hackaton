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
            ShotTypes.Clear();

            Shot<LivingSkin>("1-living-skin", HandPose.PalmsToFace, 8f);
            Shot<HeartbeatOrb>("2-heartbeat-orb", HandPose.PalmsFacing, 18f, closeUp: rig => HandSkin.FocusPosition);
            Shot<WaveformRibbon>("3-waveform-ribbon", HandPose.BacksToFace, 8f, rig => { rig.Left.Pinch = 1f; rig.Right.Pinch = 1f; });
            Shot<AirStrings>("4-air-strings", HandPose.BacksToFace, 12f);
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
            for (int scene = 0; scene < FingerFrame.SceneNames.Length; scene++)
            {
                int which = scene;
                Shot<FingerFrame>($"21{(char)('a' + scene)}-finger-frame", HandPose.BacksToFace, 8f, prepare: world => OpenPreviewWindow((FingerFrame)world, which),
                    handOffset: _ => new Vector3(0f, 0.12f, 0.06f));
            }
            // Lightning in its rain, a strike just landed; then in the snow, settled after half a minute, palms held up to it.
            Lightning storm = null;
            int stormFrames = 0;
            Shot<Lightning>("22-lightning", HandPose.BacksToFace, 14f, prepare: world => storm = (Lightning)world,
                holdPose: _ => { if (++stormFrames == 352) storm.PreviewStorm(); }, lead: 12f);
            Shot<Lightning>("22s-lightning-snow", HandPose.PalmsUp, 30f, prepare: world => ((Lightning)world).PreviewWeather(true), lead: 30f);
            // A strike in the settled snow: the bolt is ice blue, laid over the white ground rather than added to it.
            Lightning snowStorm = null;
            int snowFrames = 0;
            Shot<Lightning>("22st-lightning-snow-strike", HandPose.BacksToFace, 14f,
                prepare: world => { snowStorm = (Lightning)world; snowStorm.PreviewWeather(true); },
                holdPose: _ => { if (++snowFrames == 892) snowStorm.PreviewStorm(); }, lead: 30f);
            // Shadow play with a tracked body (arms bent, elbows out), and with the hands alone, as without body tracking.
            Shot<ShadowPlay>("23-shadow-play", HandPose.BacksToFace, 6f, holdPose: rig => PreviewBody(rig, 1f / 30f),
                closeUp: rig => new Vector3(0.12f, 1.95f, 2.6f), handOffset: ShadowPlayHands);
            Shot<ShadowPlay>("23h-shadow-play-hands-only", HandPose.BacksToFace, 6f, handOffset: ShadowPlayHands);
            // Flower dance: your hands are the sun, and the garden leans after them (here held out to your left).
            Shot<FlowerDance>("25-flower-dance", HandPose.PalmsToFace, 8f);
            Shot<FlowerDance>("25l-flower-dance-sun-left", HandPose.PalmsToFace, 8f, handOffset: _ => new Vector3(-0.45f, 0.05f, 0.1f));
            Shot<WordDrift>("17-word-drift", HandPose.PalmsToFace, 2f, lead: 6f);
            WordDrift drift = null;
            int heldFrames = 0;
            Shot<WordDrift>("17h-word-drift-held", HandPose.PalmsToFace, 2f, lead: 7f, prepare: world => drift = (WordDrift)world,
                holdPose: _ => { if (++heldFrames % 24 == 0) drift.PreviewHold(); });
            Shot<RisingFloor>("18-rising-floor", HandPose.PalmsToFace, 20f, lead: 4f);
            // Solar system: the planets round you, and looking up, the sung line as a constellation under the ceiling.
            Shot<SolarSystem>("24-solar-system", HandPose.PalmsToFace, 8f);
            Shot<SolarSystem>("24s-solar-system-stars", HandPose.PalmsToFace, -40f, lead: 4f);
            MenuShot();
            CassetteDeckSetup.EnsureMaterials();
            DeckShots();

            // The same worlds in a scanned, furnished room: on the real table, walls, ceiling and furniture.
            Shot<SoundPool>("8r-sound-pool-room", HandPose.PalmsDown, 42f, closeUp: rig => rig.Right.Palm.position + Vector3.down * 0.04f,
                handOffset: side => side == "R" ? new Vector3(-0.03f, -0.53f, 0.15f) : new Vector3(0f, -0.35f, 0.1f), scanned: true);
            Shot<Graffiti>("13r-graffiti-room", HandPose.BacksToFace, 4f, rig => rig.Right.Pinch = 1f,
                handOffset: side => side == "R" ? new Vector3(0.02f, 0.08f, 0.12f) : new Vector3(0f, -0.15f, 0f), lead: 6f, scanned: true);
            Shot<BreathingRoom>("14r-breathing-room-room", HandPose.PalmsToFace, 6f, scanned: true);
            Shot<SolarSystem>("24r-solar-system-stars-room", HandPose.PalmsToFace, -40f, lead: 4f, scanned: true);
            Shot<Lightning>("22r-lightning-snow-room", HandPose.PalmsUp, 26f, prepare: world => ((Lightning)world).PreviewWeather(true), lead: 30f, scanned: true);
            Shot<ShadowPlay>("23r-shadow-play-room", HandPose.BacksToFace, 6f, holdPose: rig => PreviewBody(rig, 1f / 30f),
                handOffset: ShadowPlayHands, scanned: true);

            // Every other world (a new one, say) gets a default shot, w-<class>.png: palms to the face, from head height.
            foreach (var type in WorldCatalog.Types())
                if (!ShotTypes.Contains(type)) Shot(type, $"w-{type.Name}", HandPose.PalmsToFace, 8f);
            JourneyShots();
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

        /// <summary>For Shadow play: the hands raised a little and apart, the way you hold them up to make shadows.</summary>
        static Vector3 ShadowPlayHands(string side) => new Vector3(side == "L" ? -0.06f : 0.06f, 0.12f, -0.02f);

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
            System.Action<World> prepare = null, float extra = 0f, bool scanned = false) where T : World =>
            Shot(typeof(T), name, pose, pitch, holdPose, closeUp, handOffset, lead, prepare, extra, scanned);

        // The worlds shot so far, so every other world gets a default shot.
        static readonly HashSet<System.Type> ShotTypes = new HashSet<System.Type>();

        static void Shot(System.Type type, string name, HandPose pose, float pitch, System.Action<HandRig> holdPose = null,
            System.Func<HandRig, Vector3> closeUp = null, System.Func<string, Vector3> handOffset = null, float lead = 3f,
            System.Action<World> prepare = null, float extra = 0f, bool scanned = false)
        {
            ShotTypes.Add(type);
            if (!Wanted(name)) return;
            // No body unless the shot makes one up (see PreviewBody).
            BodyRig.Clear();
            var camera = StageRoom(pitch, scanned);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var hands = PlaceHands(camera, pose, rig, handOffset);

            var world = (World)new GameObject(type.Name).AddComponent(type);
            if (SceneSettings.TryGetValue(type, out var json)) EditorJsonUtility.FromJsonOverwrite(json, world);
            else
            {
                // A world not in the main scene yet (a new one) gets its defaults and its materials by name, as Update
                // Worlds in Main Scene would give it.
                ProjectSetup.AssignMaterials(world);
                Debug.Log($"[Shots] {type.Name} is not in the main scene yet; using its defaults and materials by name.");
            }
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

        /// <summary>
        /// The Finger frame window open between the hands on one of its worlds, as if framed there: a slanted frame,
        /// the right hand a little higher and nearer than the left, so the window is neither flat nor rectangular.
        /// </summary>
        static void OpenPreviewWindow(FingerFrame frame, int scene)
        {
            var eye = new Vector3(0f, 1.6f, 0f);
            var at = new Vector3(0f, 1.46f, 0.46f);
            var facing = Quaternion.LookRotation(at - eye, Vector3.up);
            Vector3 Corner(float x, float y, float near) => at + facing * new Vector3(x, y, -near);
            frame.PreviewWindow(new[]
            {
                Corner(-0.14f, -0.1f, 0f), Corner(0.12f, -0.06f, 0.03f), Corner(0.15f, 0.11f, 0.05f), Corner(-0.12f, 0.06f, 0.01f)
            }, scene);
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
            // The right index fingertip 3 cm in front of the lyrics bubble, about to touch it: the dot under the
            // fingertip, the bubble swelling and its name over the menu.
            var root = menu.transform.Find("Menu");
            var rightBones = hands[1].Root.GetComponentsInChildren<Transform>();
            Vector3 Tip(string name) => rightBones.First(t => t.name == name).position;
            var reach = root.Find("Bubble Lyrics").position - root.forward * 0.03f - Tip("XRHand_IndexTip");
            hands[1].Root.transform.position += reach;
            PushHand(rig, hands[1]);
            for (int i = 0; i < 12; i++) menu.Step(rig, manager, null, camera, 0.05f);
            foreach (var hand in hands) hand.Root.GetComponent<HandSkin>().Apply();
            File.WriteAllBytes($"{ShotFolder}/7-menu.png", Render(camera, 1600, 900).EncodeToPNG());
            hands[1].Root.transform.position -= reach;
            PushHand(rig, hands[1]);

            // Pointing at a bubble from arm's length, the hand open: the ray lands on it, it swells and its name shows
            // over the menu. The point between thumb and index fingertips goes on the line from the shoulder to the bubble.
            var aimed = root.Find($"Bubble {Mathf.Min(8, worlds.Length - 1)}").position;
            var side = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized).normalized;
            var shoulder = camera.transform.position + Vector3.down * 0.17f + side * 0.17f;
            reach = shoulder + (aimed - shoulder) * 0.55f - (Tip("XRHand_ThumbTip") + Tip("XRHand_IndexTip")) * 0.5f;
            hands[1].Root.transform.position += reach;
            PushHand(rig, hands[1]);
            for (int i = 0; i < 12; i++) menu.Step(rig, manager, null, camera, 0.05f);
            foreach (var hand in hands) hand.Root.GetComponent<HandSkin>().Apply();
            File.WriteAllBytes($"{ShotFolder}/7h-menu-hover.png", Render(camera, 1600, 900).EncodeToPNG());
            hands[1].Root.transform.position -= reach;
            PushHand(rig, hands[1]);

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

            // For the Devpost: the menu where it opens in front of a standing player looking straight ahead (as
            // CheckMenuFieldOfView measures it), the hands lowered, and the outline of Meta's
            // recommended design area round the middle of the view.
            menu.Close();
            camera.transform.rotation = Quaternion.identity;
            foreach (var hand in hands)
            {
                hand.Root.transform.position += Vector3.down * 0.45f;
                PushHand(rig, hand);
            }
            menu.Open(camera);
            for (int i = 0; i < 24; i++) menu.Step(rig, manager, null, camera, 0.05f);
            var outline = DrawDesignArea(camera);
            File.WriteAllBytes($"{ShotFolder}/7v-menu-design-area.png", Render(camera, 1600, 900).EncodeToPNG());
            Object.DestroyImmediate(outline);
        }

        /// <summary>
        /// For the screenshots only: the outline of Meta's recommended design area as the camera's eye sees it, 58°
        /// across and 54° high round the middle of the view, drawn on a frame 0.9 m away (behind the menu) and labelled
        /// under it.
        /// </summary>
        static GameObject DrawDesignArea(Camera camera)
        {
            var overlay = new GameObject("Design Area Outline");
            var line = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            var eye = camera.transform;
            const float depth = 0.9f;
            float x = depth * Mathf.Tan(WorldMenu.DesignArea.x * 0.5f * Mathf.Deg2Rad), y = depth * Mathf.Tan(WorldMenu.DesignArea.y * 0.5f * Mathf.Deg2Rad);
            var corners = new[] { new Vector3(-x, -y, depth), new Vector3(x, -y, depth), new Vector3(x, y, depth), new Vector3(-x, y, depth) };
            var color = new Color(1f, 0.82f, 0.4f, 0.95f);
            var points = new Vector3[2];
            for (int i = 0; i < 4; i++)
            {
                var edge = new LineStrip($"Edge {i}", overlay.transform, line, 2);
                points[0] = eye.TransformPoint(corners[i]);
                points[1] = eye.TransformPoint(corners[(i + 1) % 4]);
                edge.Set(points, 2, 0.006f, color, eye.position, false);
            }
            var label = new TypeLabel("Design Area Label", overlay.transform, 0.026f, 1f);
            label.Text = $"Meta's recommended design area, {WorldMenu.DesignArea.x:0}° × {WorldMenu.DesignArea.y:0}°";
            label.Transform.SetPositionAndRotation(eye.TransformPoint(new Vector3(0f, -y - 0.016f, depth)), eye.rotation);
            label.Color = color;
            return overlay;
        }
#endif
    }
}
