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
        enum HandPose { PalmsToFace, PalmsFacing, PalmsUp, BacksToFace }

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
            var glow = (ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            var ring = (ProjectSetup.RingMaterialPath, "MusicInYourHands/Ring");
            var line = (ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");

            Shot<LivingSkin>("1-living-skin", HandPose.PalmsToFace, 8f, new[] { ("glowMaterial", glow), ("ringMaterial", ring) });
            Shot<HeartbeatOrb>("2-heartbeat-orb", HandPose.PalmsFacing, 18f, new[] { ("glowMaterial", glow), ("lineMaterial", line),
                ("orbMaterial", (ProjectSetup.OrbMaterialPath, "MusicInYourHands/Orb")),
                ("dotsMaterial", (ProjectSetup.OrbDotsMaterialPath, "MusicInYourHands/OrbDots")) }, closeUp: true);
            Shot<WaveformRibbon>("3-waveform-ribbon", HandPose.BacksToFace, 8f, new[] { ("glowMaterial", glow), ("lineMaterial", line) },
                rig => { rig.Left.Pinch = 1f; rig.Right.Pinch = 1f; });
            Shot<AirStrings>("4-air-strings", HandPose.BacksToFace, 12f, new[] { ("glowMaterial", glow), ("lineMaterial", line), ("ringMaterial", ring) });
            Shot<NoteRain>("5-note-rain", HandPose.PalmsUp, 34f, new[] { ("glowMaterial", glow), ("ringMaterial", ring),
                ("streakMaterial", (ProjectSetup.StreakMaterialPath, "MusicInYourHands/Streak")) });
            Shot<FireflyChoir>("6-firefly-choir", HandPose.PalmsToFace, 8f, new[] { ("glowMaterial", glow), ("lineMaterial", line) });
            MenuShot();
            Debug.Log($"[Shots] Saved screenshots to {ShotFolder}.");
            return true;
#else
            return false;
#endif
        }

#if META_XR_CORE
        /// <summary>A room standing in for passthrough: floor and three walls in soft, warm grey tones.</summary>
        static Camera StageRoom(float pitch)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
            Panel("Back Wall", new Vector3(0f, 1.5f, 2.6f), Vector3.zero, new Vector2(6f, 3f), new Color(0.55f, 0.51f, 0.46f));
            Panel("Left Wall", new Vector3(-1.9f, 1.5f, 1f), new Vector3(0f, -90f, 0f), new Vector2(6f, 3f), new Color(0.5f, 0.46f, 0.42f));
            Panel("Right Wall", new Vector3(1.9f, 1.5f, 1f), new Vector3(0f, 90f, 0f), new Vector2(6f, 3f), new Color(0.5f, 0.46f, 0.42f));

            var camera = new GameObject("Head").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.42f, 0.38f);
            camera.fieldOfView = 78f;
            camera.nearClipPlane = 0.02f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.Euler(pitch, 0f, 0f));
            return camera;
        }

        /// <summary>Both hands about 35 cm in front, at chest height, held the way the world asks for.</summary>
        static List<PreviewHand> PlaceHands(Camera camera, HandPose pose, HandRig rig)
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
                var center = new Vector3(x, 1.3f, 0.36f);
                hand.transform.position += center - skin.bounds.center;
                Vector3 want = pose switch
                {
                    HandPose.PalmsToFace or HandPose.PalmsUp => Vector3.back,
                    // Turned a little towards the face, the way you hold a ball in front of you.
                    HandPose.PalmsFacing => ((left ? Vector3.right : Vector3.left) + Vector3.back * 0.6f).normalized,
                    HandPose.BacksToFace => Vector3.forward,
                    _ => Vector3.forward
                };
                var normal = Vector3.ProjectOnPlane(PalmNormal(Bone, left), Vector3.up).normalized;
                hand.transform.RotateAround(skin.bounds.center, Vector3.up, Vector3.SignedAngle(normal, want, Vector3.up));
                if (pose == HandPose.PalmsUp)
                    // Tip the hand forward: fingers point away, palm up, as if offering it.
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
                return new MusicFrame { Bands = bands, Kick = k, Beat = beat, SongTime = Time, Upcoming = upcoming };
            }
        }

        // Set SHOTS to part of a shot's name (for example "orb") to capture only the matching shots.
        static bool Wanted(string name)
        {
            var only = System.Environment.GetEnvironmentVariable("SHOTS");
            return string.IsNullOrEmpty(only) || only.Split(',').Any(name.Contains);
        }

        static void Shot<T>(string name, HandPose pose, float pitch, (string property, (string path, string shader) material)[] materials,
            System.Action<HandRig> holdPose = null, bool closeUp = false) where T : World
        {
            if (!Wanted(name)) return;
            var camera = StageRoom(pitch);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var hands = PlaceHands(camera, pose, rig);

            var world = new GameObject(typeof(T).Name).AddComponent<T>();
            var so = new SerializedObject(world);
            foreach (var (property, (path, shader)) in materials)
                so.FindProperty(property).objectReferenceValue = ProjectSetup.EnsureMaterial(path, shader);
            so.ApplyModifiedPropertiesWithoutUndo();
            world.Build();
            HandSkin.Theme = world.Theme;
            HandSkin.FocusStrength = 0f;
            HandSkin.Charge[0] = HandSkin.Charge[1] = 0f;

            // Play up to just after the chorus's first beat.
            var clock = new SongClock();
            const float dt = 1f / 30f;
            while (clock.Time < ChorusStart + 0.09f)
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
            File.WriteAllBytes($"{ShotFolder}/{name}.png", Render(camera, 1600, 900).EncodeToPNG());
            if (!closeUp) return;
            // A second, zoomed-in shot of whatever the world is focused on, to judge the detail.
            camera.transform.LookAt(HandSkin.FocusPosition);
            camera.fieldOfView = 30f;
            File.WriteAllBytes($"{ShotFolder}/{name}-close.png", Render(camera, 1200, 900).EncodeToPNG());
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
            var worlds = new World[]
            {
                MakePreviewWorld<LivingSkin>(managerGo.transform, "Living skin", new Color(1f, 0.37f, 0.68f)),
                MakePreviewWorld<HeartbeatOrb>(managerGo.transform, "Heartbeat orb", new Color(1f, 0.83f, 0.42f)),
                MakePreviewWorld<WaveformRibbon>(managerGo.transform, "Waveform ribbon", new Color(1f, 0.7f, 0.42f)),
                MakePreviewWorld<AirStrings>(managerGo.transform, "Air strings", new Color(0.49f, 0.95f, 0.79f)),
                MakePreviewWorld<NoteRain>(managerGo.transform, "Note rain", new Color(0.5f, 0.72f, 1f)),
                MakePreviewWorld<FireflyChoir>(managerGo.transform, "Firefly choir", new Color(0.79f, 0.61f, 1f))
            };
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
        }
#endif
    }
}
