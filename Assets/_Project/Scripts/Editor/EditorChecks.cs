using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Checks that catch device problems without a headset: hand joints match the visible hand mesh, debug shapes
    /// use a URP material, and a rendered preview of the debug view has no magenta (missing shader) pixels.
    /// Writes Logs/preview-debug.png. Batch mode (needs graphics, so no -nographics):
    /// -executeMethod MusicInYourHands.EditorTools.EditorChecks.RunBatch
    /// </summary>
    public static partial class EditorChecks
    {
        const string PreviewPath = "Logs/preview-debug.png";
        static readonly string[] JointNames =
        {
            "XRHand_Palm", "XRHand_Wrist", "XRHand_IndexProximal", "XRHand_LittleProximal",
            "XRHand_ThumbTip", "XRHand_IndexTip", "XRHand_MiddleTip", "XRHand_RingTip", "XRHand_LittleTip"
        };

        [MenuItem("Music In Your Hands/Run Editor Checks")]
        public static void Run() => RunAll();

        public static void RunBatch() => EditorApplication.Exit(RunAll() ? 0 : 1);

        static bool RunAll()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            bool ok = true;
            ok &= CheckWorldCatalog();
            ok &= CheckWorldHooks();
            ok &= CheckWorldSpaces();
            ok &= CheckCombining();
            ok &= CheckWorldPairs();
            ok &= CheckHandJoints();
            ok &= CheckDebugMaterial(out var material);
            ok &= CheckPreview(material);
            ok &= CheckHandPreview();
            ok &= CheckLivingSkinPreview();
            ok &= CheckOrbPreview();
            ok &= CheckMenuPreview();
            ok &= CheckMenuTouch();
            ok &= CheckRibbonPreview();
            ok &= CheckStringsPreview();
            ok &= CheckRainPreview();
            ok &= CheckFirefliesPreview();
            ok &= CheckSongs();
            ok &= CheckAudiusSongs();
            ok &= CheckLettering();
            ok &= CheckLyricsOption();
            ok &= CheckPickerResults();
            Debug.Log(ok ? "[Check] All checks passed." : "[Check] Some checks FAILED.");
            return ok;
        }

        static bool CheckHandJoints()
        {
#if META_XR_CORE
            bool ok = true;
            foreach (var side in new[] { "L", "R" })
            {
                var path = $"Packages/com.meta.xr.sdk.core/Prefabs/OpenXRCustomHandPrefab_{side}.prefab";
                var hand = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var skin = hand.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var meshBones = new HashSet<Transform>(skin != null ? skin.bones : new Transform[0]);
                var all = hand.GetComponentsInChildren<Transform>(true);

                foreach (var jointName in JointNames)
                {
                    var joint = all.FirstOrDefault(t => t.name == jointName);
                    if (joint == null)
                    {
                        Debug.LogError($"[Check] {side} hand: joint {jointName} not found by name.");
                        ok = false;
                    }
                    // Palm and tips are not skinned in every mesh; they still sit in the bone hierarchy.
                    else if (!meshBones.Contains(joint) && !meshBones.Contains(joint.parent))
                    {
                        Debug.LogError($"[Check] {side} hand: {jointName} is not part of the visible hand mesh's skeleton.");
                        ok = false;
                    }
                }

                ReportIdLookup(hand, side);
                Object.DestroyImmediate(hand);
            }
            if (ok) Debug.Log("[Check] Hand joints found by name and they drive the visible hand mesh.");
            return ok;
#else
            Debug.LogWarning("[Check] Meta XR Core SDK not installed; skipped hand joint check.");
            return true;
#endif
        }

#if META_XR_CORE
        /// <summary>Shows which transform the old id-based lookup would have used, for the record.</summary>
        /// <summary>The baked faces load, and words built from them are one quad per letter, inside the atlas.</summary>
        static bool CheckLettering()
        {
            bool ok = true;
            foreach (var (name, face) in new[] { ("Bold", TypeFace.Bold), ("Marker", TypeFace.Marker) })
            {
                var mesh = new Mesh();
                Glyphs.BuildWord(mesh, face, "Hi there", 1f, 1, 0f, Color.white, Color.white);
                bool inside = true;
                foreach (var uv in mesh.uv) inside &= uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
                bool good = face.Atlas != null && face.Covers("Hi there") && face.Width("Hi there") > 1f && mesh.vertexCount == 7 * 4 && inside;
                Object.DestroyImmediate(mesh);
                if (!good) Debug.LogError($"[Check] The {name} face didn't build a word properly.");
                ok &= good;
            }
            if (ok) Debug.Log("[Check] Lettering faces load and build words inside their atlases.");
            return ok;
        }

        /// <summary>With lyrics off no word is sung, and lyric worlds fall back on the song's title.</summary>
        static bool CheckLyricsOption()
        {
            bool was = LyricsOption.On;
            var lyrics = LyricsTrack.Parse("[00:01.00]hello world\n");
            var singing = new MusicFrame { Lyrics = lyrics, Lyric = new LyricsTrack.State(0, 1) };
            var betweenLines = new MusicFrame { Lyrics = lyrics, Lyric = new LyricsTrack.State(-1, -1) };
            LyricsOption.On = true;
            bool ok = singing.Word == "world" && !new LyricCursor().Advance(betweenLines, out _);
            LyricsOption.On = false;
            ok &= singing.Word == "" && singing.LineWords == null && new LyricCursor().Advance(singing, out _);
            LyricsOption.On = was;
            if (ok) Debug.Log("[Check] Turning lyrics off hides the words and leaves the title for lyric worlds.");
            else Debug.LogError("[Check] The lyrics option doesn't hide the words as it should.");
            return ok;
        }

        /// <summary>The file picker's result files are read correctly, including a damaged or empty one.</summary>
        static bool CheckPickerResults()
        {
            SongPicker.Parse("added\nMy Song.mp3\n", out var added, out var file);
            SongPicker.Parse("cancelled\n\n", out var cancelled, out _);
            SongPicker.Parse("unsupported\nmy song.m4a\n", out var unsupported, out _);
            SongPicker.Parse("", out var empty, out _);
            bool ok = added == SongPicker.Outcome.Added && file == "My Song.mp3" && cancelled == SongPicker.Outcome.Cancelled &&
                      unsupported == SongPicker.Outcome.Unsupported && empty == SongPicker.Outcome.Failed;
            if (ok) Debug.Log("[Check] The file picker's results are read correctly.");
            else Debug.LogError("[Check] The file picker's results aren't read correctly.");
            return ok;
        }

        static void ReportIdLookup(GameObject hand, string side)
        {
            var skeleton = hand.GetComponent<OVRCustomSkeleton>();
            if (skeleton == null) return;
            var so = new SerializedObject(skeleton);
            var list = so.FindProperty("_customBones_V2") ?? so.FindProperty("_customBones");
            if (list == null || !list.isArray) return;

            var sb = new StringBuilder($"[Check] {side} hand id lookup (old method): ");
            foreach (var id in new[] { OVRSkeleton.BoneId.XRHand_Palm, OVRSkeleton.BoneId.XRHand_IndexTip, OVRSkeleton.BoneId.XRHand_ThumbTip })
            {
                int index = (int)id;
                var t = index < list.arraySize ? list.GetArrayElementAtIndex(index).objectReferenceValue as Transform : null;
                sb.Append($"{id}(#{index}) -> {(t != null ? t.name : "none")}; ");
            }
            Debug.Log(sb.ToString());
        }
#endif

        static bool CheckDebugMaterial(out Material material)
        {
            material = ProjectSetup.EnsureDebugMaterial();
            bool ok = material != null && material.shader != null && material.shader.isSupported &&
                      material.shader.name == "Universal Render Pipeline/Unlit";
            if (ok) Debug.Log($"[Check] Debug material uses {material.shader.name}.");
            else Debug.LogError($"[Check] Debug material is not a supported URP Unlit material ({(material != null && material.shader != null ? material.shader.name : "missing")}).");
            return ok;
        }

        static bool CheckPreview(Material material)
        {
            var cameraGo = new GameObject("Preview Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.09f, 0.14f);
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.25f, 0.45f), Quaternion.identity);
            camera.fieldOfView = 50f;

            var view = new GameObject("MusicDebugView").AddComponent<MusicDebugView>();
            view.transform.position = new Vector3(0f, 1.2f, 1f);
            SetSerialized(view, "material", material);
            view.Build();

            // A few spheres like the fingertip markers, to check them too.
            for (int i = 0; i < 5; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = new Vector3(-0.12f + i * 0.06f, 1.05f, 0.95f);
                sphere.transform.localScale = Vector3.one * 0.03f;
                sphere.GetComponent<Renderer>().sharedMaterial = material;
            }

            const int width = 960, height = 540;
            var rt = new RenderTexture(width, height, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;

            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
            File.WriteAllBytes(PreviewPath, image.EncodeToPNG());

            int magenta = 0, drawn = 0;
            var background = camera.backgroundColor;
            foreach (var p in image.GetPixels())
            {
                if (Mathf.Abs(p.r - background.r) + Mathf.Abs(p.g - background.g) + Mathf.Abs(p.b - background.b) > 0.05f) drawn++;
                if (p.r > 0.85f && p.g < 0.15f && p.b > 0.85f) magenta++;
            }

            bool ok = drawn > 500 && magenta < 50;
            if (ok) Debug.Log($"[Check] Preview rendered {drawn} pixels of debug shapes with no magenta. Saved {PreviewPath}.");
            else Debug.LogError($"[Check] Preview problem: {drawn} pixels drawn, {magenta} magenta. See {PreviewPath}.");
            return ok;
        }

        /// <summary>
        /// Renders both Meta hands with the project's hand look over a room-coloured background (like passthrough):
        /// the left hand at rest, the right at a beat peak. Writes Logs/preview-hands.png.
        /// </summary>
        static bool CheckHandPreview()
        {
#if META_XR_CORE
            var material = ProjectSetup.EnsureHandMaterial();
            if (material.shader == null || !material.shader.isSupported || material.shader.name != "MusicInYourHands/Hand")
            {
                Debug.LogError("[Check] Hand material does not use a supported MusicInYourHands/Hand shader.");
                return false;
            }

            var hands = StageHands(out var camera);
            for (int h = 0; h < hands.Count; h++)
                SetBeat(hands[h].Skin, h == 0 ? 10f : 0.02f, h == 0 ? 0f : 1f);
            return CheckImage(Render(camera, 960, 540), camera.backgroundColor, "Logs/preview-hands.png", "Hand preview");
#else
            return true;
#endif
        }

        /// <summary>
        /// Renders the Living skin world at four moments after a beat (pulse through the hand, palm waves spreading,
        /// fingertip lights, sparks) into one 2x2 image: Logs/preview-living-skin.png.
        /// </summary>
        static bool CheckLivingSkinPreview()
        {
#if META_XR_CORE
            var hands = StageHands(out var camera);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            foreach (var hand in hands) PushHand(rig, hand);

            HandSkin.Theme = HandTheme.Veins;
            var skin = new GameObject("LivingSkin").AddComponent<LivingSkin>();
            var so = new SerializedObject(skin);
            so.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            so.FindProperty("shellMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.ShellMaterialPath, "MusicInYourHands/Shell");
            so.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            so.ApplyModifiedPropertiesWithoutUndo();
            skin.Build();

            float[] times = { 0.05f, 0.22f, 0.45f, 0.85f };
            float[] bands = { 0.9f, 0.7f, 0.55f, 0.6f, 0.4f, 0.5f };
            const int w = 640, h = 360;
            var sheet = new Texture2D(w * 2, h * 2, TextureFormat.RGB24, false);
            float clock = 0f;
            for (int i = 0; i < times.Length; i++)
            {
                float dt = times[i] - clock;
                clock = times[i];
                float kick = Mathf.Exp(-clock * 7f);
                // The beat happens at t = 0; step to each moment, wiggling the fingertips a little to make sparks.
                if (i == 0) skin.Step(rig, Music(bands, 1f, true), camera, 0f);
                foreach (var hand in hands) { hand.Root.transform.position += Vector3.up * 0.004f; PushHand(rig, hand); }
                skin.Step(rig, Music(bands, kick, false), camera, dt);
                skin.SimulateParticles(dt);
                foreach (var hand in hands) SetBeat(hand.Skin, clock, kick);

                var frame = Render(camera, w, h);
                sheet.SetPixels((i % 2) * w, (1 - i / 2) * h, w, h, frame.GetPixels());
            }
            sheet.Apply();
            return CheckImage(sheet, camera.backgroundColor, "Logs/preview-living-skin.png", "Living skin preview");
#else
            return true;
#endif
        }

        /// <summary>
        /// Renders the Heartbeat orb held between two hands, at rest and on a kick, side by side:
        /// Logs/preview-heartbeat-orb.png.
        /// </summary>
        static bool CheckOrbPreview()
        {
#if META_XR_CORE
            var hands = StageHands(out var camera);
            // Turn the palms towards each other, as if holding something between them.
            foreach (var hand in hands)
            {
                var bones = hand.Root.GetComponentsInChildren<Transform>();
                Transform Bone(string n) => bones.First(t => t.name == n);
                var normal = Vector3.ProjectOnPlane(PalmNormal(Bone, hand.Left), Vector3.up).normalized;
                var want = hand.Left ? Vector3.right : Vector3.left;
                var c = hand.Skin.bounds.center;
                hand.Root.transform.RotateAround(c, Vector3.up, Vector3.SignedAngle(normal, want, Vector3.up) * 0.8f);
                hand.Root.transform.position += (hand.Left ? Vector3.left : Vector3.right) * 0.03f;
            }
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            foreach (var hand in hands) PushHand(rig, hand);

            var orb = new GameObject("HeartbeatOrb").AddComponent<HeartbeatOrb>();
            HandSkin.Theme = orb.Theme;
            var so = new SerializedObject(orb);
            so.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            so.FindProperty("orbMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.OrbMaterialPath, "MusicInYourHands/Orb");
            so.FindProperty("dotsMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.OrbDotsMaterialPath, "MusicInYourHands/OrbDots");
            so.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            so.ApplyModifiedPropertiesWithoutUndo();
            orb.Build();

            float[] bands = { 0.8f, 0.6f, 0.5f, 0.5f, 0.4f, 0.5f };
            for (int i = 0; i < 8; i++) orb.Step(rig, Music(bands, 0f, false), camera, 0.1f);
            foreach (var hand in hands) hand.Root.GetComponent<HandSkin>().Apply();
            const int w = 640, h = 360;
            var sheet = new Texture2D(w * 2, h, TextureFormat.RGB24, false);
            sheet.SetPixels(0, 0, w, h, Render(camera, w, h).GetPixels());
            orb.Step(rig, Music(bands, 1f, true), camera, 0.016f);
            orb.SimulateParticles(0.18f);
            foreach (var hand in hands) { hand.Root.GetComponent<HandSkin>().Apply(); SetBeat(hand.Skin, 0.08f, 1f); }
            sheet.SetPixels(w, 0, w, h, Render(camera, w, h).GetPixels());
            sheet.Apply();
            return CheckImage(sheet, camera.backgroundColor, "Logs/preview-heartbeat-orb.png", "Heartbeat orb preview");
#else
            return true;
#endif
        }

        /// <summary>
        /// Renders the bubble menu open in front of the viewer, with three worlds on together wearing their rings:
        /// Logs/preview-menu.png.
        /// </summary>
        static bool CheckMenuPreview()
        {
#if META_XR_CORE
            var hands = StageHands(out var camera);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            foreach (var hand in hands) PushHand(rig, hand);

            var managerGo = new GameObject("Worlds");
            var manager = MakePreviewManager(managerGo);

            var menu = new GameObject("WorldMenu").AddComponent<WorldMenu>();
            var wso = new SerializedObject(menu);
            wso.FindProperty("bubbleMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble");
            wso.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            wso.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            wso.ApplyModifiedPropertiesWithoutUndo();
            // Lower the hands out of the way, as if the player had just opened the menu.
            foreach (var hand in hands) { hand.Root.transform.position += Vector3.down * 0.3f; PushHand(rig, hand); }
            menu.Step(rig, manager, null, camera, 0f);
            menu.Open(camera);
            for (int i = 0; i < 20; i++) menu.Step(rig, manager, null, camera, 0.05f);

            return CheckImage(Render(camera, 960, 540), camera.backgroundColor, "Logs/preview-menu.png", "Menu preview");
#else
            return true;
#endif
        }

        /// <summary>A world for the menu checks, off, with its materials: choosing it in the menu builds it.</summary>
        static T MakePreviewWorld<T>(Transform parent) where T : World
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            var world = go.AddComponent<T>();
            ProjectSetup.AssignMaterials(world);
            return world;
        }

        /// <summary>
        /// A WorldManager with every world, for the menu previews, and three of them on together as a player might
        /// choose them: Living skin on the hands and walls, Firefly choir around you and Ceiling stars overhead (so the
        /// menu shows their rings).
        /// </summary>
        static WorldManager MakePreviewManager(GameObject holder)
        {
            var worlds = WorldCatalog.MakePreviews(holder.transform);
            foreach (var world in worlds) ProjectSetup.AssignMaterials(world);
            var manager = holder.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Length;
            for (int i = 0; i < worlds.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            int Find<T>() where T : World => System.Array.FindIndex(worlds, w => w is T);
            manager.Switch(Find<LivingSkin>());
            manager.Choose(Find<FireflyChoir>());
            manager.Choose(Find<CeilingStars>());
            return manager;
        }

        static MusicFrame Music(float[] bands, float kick, bool beat) => new MusicFrame { Bands = bands, Kick = kick, Beat = beat };

        static readonly float[] PreviewBands = { 0.85f, 0.7f, 0.55f, 0.6f, 0.45f, 0.55f };

        /// <summary>
        /// Stages both hands, adds the world with its materials and lets <paramref name="pose"/> adjust the hands,
        /// then renders two moments side by side: before a beat and just after one.
        /// </summary>
        static bool CheckWorldPreview<T>(string label, string path, (string property, string materialPath, string shader)[] materials,
            System.Action<List<PreviewHand>, HandRig, T, Camera> pose, float warmup, bool withSong = false) where T : World
        {
#if META_XR_CORE
            var hands = StageHands(out var camera);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            foreach (var hand in hands) PushHand(rig, hand);

            var world = new GameObject(typeof(T).Name).AddComponent<T>();
            var so = new SerializedObject(world);
            foreach (var (property, materialPath, shader) in materials)
                so.FindProperty(property).objectReferenceValue = ProjectSetup.EnsureMaterial(materialPath, shader);
            so.ApplyModifiedPropertiesWithoutUndo();
            world.Build();
            HandSkin.Theme = world.Theme;
            HandSkin.FocusStrength = 0f;
            HandSkin.Charge[0] = HandSkin.Charge[1] = 0f;
            pose?.Invoke(hands, rig, world, camera);

            // With the demo song's note track, the preview plays from the first chorus like the real song would.
            var notes = withSong ? NoteTrack.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Audio/GlassRoom.notes")?.text) : null;
            var upcoming = new List<NoteTrack.Note>();
            float clock = 24.2f;
            MusicFrame Frame(float kick, bool beat)
            {
                var frame = Music(PreviewBands, kick, beat);
                if (notes == null) return frame;
                upcoming.Clear();
                notes.Between(clock, clock + 3f, upcoming);
                frame.SongTime = clock;
                frame.Upcoming = upcoming;
                return frame;
            }
            void Advance(float kick, bool beat, float dt)
            {
                world.Step(rig, Frame(kick, beat), camera, dt);
                world.SimulateParticles(dt);
                clock += dt;
            }

            for (float t = 0f; t < warmup; t += 0.05f) Advance(0.2f, false, 0.05f);
            const int w = 640, h = 360;
            var sheet = new Texture2D(w * 2, h, TextureFormat.RGB24, false);
            foreach (var hand in hands) { hand.Root.GetComponent<HandSkin>().Apply(); SetBeat(hand.Skin, 10f, 0.1f); }
            sheet.SetPixels(0, 0, w, h, Render(camera, w, h).GetPixels());
            Advance(1f, true, 0.016f);
            for (int i = 0; i < 3; i++) Advance(0.8f, false, 0.04f);
            foreach (var hand in hands) { hand.Root.GetComponent<HandSkin>().Apply(); SetBeat(hand.Skin, 0.12f, 0.8f); }
            sheet.SetPixels(w, 0, w, h, Render(camera, w, h).GetPixels());
            sheet.Apply();
            return CheckImage(sheet, camera.backgroundColor, path, label);
#else
            return true;
#endif
        }

        static bool CheckRibbonPreview() => CheckWorldPreview<WaveformRibbon>("Waveform ribbon preview", "Logs/preview-ribbon.png",
            new[] { ("lineMaterial", ProjectSetup.LineMaterialPath, "MusicInYourHands/Line"), ("glowMaterial", ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow"),
                ("silkMaterial", ProjectSetup.SilkMaterialPath, "MusicInYourHands/Silk") },
            (hands, rig, world, camera) => { rig.Left.Pinch = 1f; rig.Right.Pinch = 1f; }, 0.6f);

        static bool CheckStringsPreview() => CheckWorldPreview<AirStrings>("Air strings preview", "Logs/preview-strings.png",
            new[] { ("lineMaterial", ProjectSetup.LineMaterialPath, "MusicInYourHands/Line"), ("glowMaterial", ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow"),
                    ("ringMaterial", ProjectSetup.RingMaterialPath, "MusicInYourHands/Ring") },
            (hands, rig, world, camera) =>
            {
                // Look a little lower and further back to see all six strings.
                camera.transform.position += new Vector3(0f, -0.12f, -0.25f);
                camera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
                world.PreviewPluck(2, 0.02f);
            }, 0.6f, withSong: true);

        static bool CheckRainPreview() => CheckWorldPreview<NoteRain>("Note rain preview", "Logs/preview-rain.png",
            new[] { ("glowMaterial", ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow"), ("streakMaterial", ProjectSetup.StreakMaterialPath, "MusicInYourHands/Streak"),
                    ("ringMaterial", ProjectSetup.RingMaterialPath, "MusicInYourHands/Ring") },
            (hands, rig, world, camera) =>
            {
                // Step back and look down at the floor, where the notes land.
                camera.transform.position += new Vector3(0f, 0.1f, -0.6f);
                camera.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
                camera.fieldOfView = 70f;
            }, 2.6f, withSong: true);

        static bool CheckFirefliesPreview() => CheckWorldPreview<FireflyChoir>("Firefly choir preview", "Logs/preview-fireflies.png",
            new[] { ("glowMaterial", ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow"), ("lineMaterial", ProjectSetup.LineMaterialPath, "MusicInYourHands/Line") },
            (hands, rig, world, camera) => { }, 3f);

#if META_XR_CORE
        sealed class PreviewHand
        {
            public GameObject Root;
            public SkinnedMeshRenderer Skin;
            public bool Left;
        }

        /// <summary>Both Meta hands with the project look, held up palm-forward in front of a passthrough-coloured camera.</summary>
        static List<PreviewHand> StageHands(out Camera camera)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Preview Camera");
            camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.36f, 0.32f, 0.28f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.02f;
            camera.transform.position = new Vector3(0f, 1.2f, 0.45f) + new Vector3(0f, 0f, -0.5f);
            camera.transform.LookAt(new Vector3(0f, 1.2f, 0.45f));

            var hands = new List<PreviewHand>();
            foreach (var (side, x) in new[] { ("L", -0.11f), ("R", 0.11f) })
            {
                var path = $"Packages/com.meta.xr.sdk.core/Prefabs/OpenXRCustomHandPrefab_{side}.prefab";
                var hand = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                ProjectSetup.ApplyHandLook(hand);
                var skin = hand.GetComponentInChildren<SkinnedMeshRenderer>();
                var bones = hand.GetComponentsInChildren<Transform>();
                Transform Bone(string n) => bones.First(t => t.name == n);

                // Stand the hand up, then turn it so the palm faces the camera.
                var up = (Bone("XRHand_MiddleProximal").position - Bone("XRHand_Wrist").position).normalized;
                hand.transform.rotation = Quaternion.FromToRotation(up, Vector3.up) * hand.transform.rotation;
                hand.transform.position += new Vector3(x, 1.2f, 0.45f) - skin.bounds.center;
                bool left = side == "L";
                if (Vector3.Dot(PalmNormal(Bone, left), (camera.transform.position - skin.bounds.center).normalized) < 0f)
                    hand.transform.RotateAround(skin.bounds.center, Vector3.up, 180f);
                hand.GetComponent<HandSkin>().Apply();
                hands.Add(new PreviewHand { Root = hand, Skin = skin, Left = left });
            }
            return hands;
        }

        static Vector3 PalmNormal(System.Func<string, Transform> bone, bool left)
        {
            var wrist = bone("XRHand_Wrist").position;
            var normal = Vector3.Cross(bone("XRHand_IndexProximal").position - wrist, bone("XRHand_LittleProximal").position - wrist).normalized;
            return left ? normal : -normal;
        }

        /// <summary>Feeds a preview hand into HandRig the way OVRHandFeed does on the headset.</summary>
        static void PushHand(HandRig rig, PreviewHand hand)
        {
            var bones = hand.Root.GetComponentsInChildren<Transform>();
            Transform Bone(string n) => bones.First(t => t.name == n);
            var tipJoints = new[] { "XRHand_ThumbTip", "XRHand_IndexTip", "XRHand_MiddleTip", "XRHand_RingTip", "XRHand_LittleTip" }.Select(Bone).ToArray();
            var palm = Bone("XRHand_Palm");
            rig.SetJoints(hand.Left, palm, tipJoints);
            rig.Get(hand.Left).Mesh = hand.Skin;
            rig.Push(hand.Left, true, new Pose(palm.position, palm.rotation), PalmNormal(Bone, hand.Left), tipJoints.Select(t => t.position).ToArray());
            hand.Root.GetComponent<HandSkin>().Apply();
        }

        static void SetBeat(Renderer r, float beatAge, float kick)
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetFloat("_BeatAge", beatAge);
            block.SetFloat("_Kick", kick);
            block.SetFloat("_Energy", kick * 0.6f);
            r.SetPropertyBlock(block);
        }
#endif

        static bool CheckImage(Texture2D image, Color background, string path, string label)
        {
            File.WriteAllBytes(path, image.EncodeToPNG());
            int magenta = 0, drawn = 0;
            foreach (var p in image.GetPixels())
            {
                if (Mathf.Abs(p.r - background.r) + Mathf.Abs(p.g - background.g) + Mathf.Abs(p.b - background.b) > 0.06f) drawn++;
                if (p.r > 0.85f && p.g < 0.15f && p.b > 0.85f) magenta++;
            }
            bool ok = drawn > 2000 && magenta < 50;
            if (ok) Debug.Log($"[Check] {label} rendered {drawn} pixels with no magenta. Saved {path}.");
            else Debug.LogError($"[Check] {label} problem: {drawn} pixels drawn, {magenta} magenta. See {path}.");
            return ok;
        }

        static Texture2D Render(Camera camera, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            return image;
        }

        static void SetSerialized(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
