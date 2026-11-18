using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Screenshots of the onboarding as a player would see it, in the staged room with the preview hands at chest height:
    /// each lesson's ghost hand beside the real hands (over the opening world, the open menu, and a world), and a few
    /// worlds' ghost demos over the world itself. Each comes with a close-up strip of four moments of the ghost's loop.
    /// Writes Logs/onboarding/*.png. Batch mode (needs graphics):
    /// -executeMethod MusicInYourHands.EditorTools.EditorChecks.CaptureOnboardingShotsBatch
    /// ($env:SHOTS picks shots by name, as for the scene shots.)
    /// </summary>
    public static partial class EditorChecks
    {
        const string OnboardingShotFolder = "Logs/onboarding";

        [MenuItem("Music In Your Hands/Capture Onboarding Screenshots")]
        public static void CaptureOnboardingShots() => CaptureOnboardingAll();

        public static void CaptureOnboardingShotsBatch() => EditorApplication.Exit(CaptureOnboardingAll() ? 0 : 1);

        static bool CaptureOnboardingAll()
        {
#if META_XR_CORE
            Directory.CreateDirectory(OnboardingShotFolder);
            OnboardingSetup.EnsureAssets();
            LoadSceneSettings();
            string savedKey = Onboarding.PrefsKey;
            Onboarding.PrefsKey = "Onboarding.Shots";
            try
            {
                // Lesson 1 over the opening world; lesson 2 at the open menu; lesson 3 in a chosen world.
                OnboardingShot("1-open-the-menu", typeof(FlowerDance), HandPose.BacksToFace, Onboarding.Lesson.OpenMenu, 2.75f,
                    new[] { 0.3f, 1.45f, 2.75f, 3.6f });
                OnboardingShot("2-touch-a-world", null, HandPose.BacksToFace, Onboarding.Lesson.TouchWorld, 1.35f,
                    new[] { 0.3f, 0.9f, 1.35f, 2.2f });
                OnboardingShot("3-thumb", typeof(LightTrails), HandPose.BacksToFace, Onboarding.Lesson.Thumb, 0.95f,
                    new[] { 0.1f, 0.95f, 1.75f, 2.5f });
                // World demos over their worlds.
                OnboardingShot("h-light-trails", typeof(LightTrails), HandPose.BacksToFace, Onboarding.Lesson.None, 1.6f, new[] { 0.2f, 0.8f, 1.6f, 2.4f });
                OnboardingShot("h-bubble-notes", typeof(BubbleNotes), HandPose.BacksToFace, Onboarding.Lesson.None, 1.6f, new[] { 0.2f, 1f, 1.8f, 2.8f });
                OnboardingShot("h-lightning", typeof(Lightning), HandPose.BacksToFace, Onboarding.Lesson.None, 1.1f, new[] { 0.1f, 0.6f, 1.2f, 2.2f });
                OnboardingShot("h-heartbeat-orb", typeof(HeartbeatOrb), HandPose.PalmsFacing, Onboarding.Lesson.None, 1.6f, new[] { 0.1f, 1f, 1.8f, 2.8f });
                OnboardingShot("h-finger-frame", typeof(FingerFrame), HandPose.BacksToFace, Onboarding.Lesson.None, 1.6f, new[] { 0.1f, 1f, 1.8f, 2.8f });
                OnboardingShot("h-flower-dance", typeof(FlowerDance), HandPose.PalmsToFace, Onboarding.Lesson.None, 1.8f, new[] { 0.2f, 1f, 2f, 3f });
                OnboardingShot("h-waveform-ribbon", typeof(WaveformRibbon), HandPose.BacksToFace, Onboarding.Lesson.None, 2f, new[] { 0.2f, 0.8f, 1.6f, 2.5f });
                OnboardingShot("h-word-drift", typeof(WordDrift), HandPose.BacksToFace, Onboarding.Lesson.None, 1.3f, new[] { 0.2f, 0.8f, 1.3f, 2f });
            }
            finally
            {
                PlayerPrefs.DeleteKey(Onboarding.PrefsKey);
                Onboarding.PrefsKey = savedKey;
                Room.Clear();
            }
            Debug.Log($"[Shots] Saved onboarding screenshots to {OnboardingShotFolder}.");
            return true;
#else
            return false;
#endif
        }

#if META_XR_CORE
        /// <summary>
        /// One onboarding screenshot: the staged room, the preview hands, a world (or the open menu for lesson 2), and the
        /// onboarding run as on the headset until <paramref name="lesson"/> shows (or, for None, the world's ghost demo),
        /// caught <paramref name="moment"/> seconds into the ghost's loop. Then a close-up strip at <paramref name="strip"/>.
        /// </summary>
        static void OnboardingShot(string name, System.Type worldType, HandPose pose, Onboarding.Lesson lesson, float moment, float[] strip)
        {
            if (!Wanted(name)) return;
            BodyRig.Clear();
            var camera = StageRoom(8f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var hands = PlaceHands(camera, pose, rig);
            bool menuOpen = lesson == Onboarding.Lesson.TouchWorld;
            if (menuOpen)
            {
                // The left hand drops away after opening the menu; the right comes up a little to point.
                hands[0].Root.transform.position += new Vector3(-0.04f, -0.16f, 0f);
                hands[1].Root.transform.position += new Vector3(0f, -0.04f, -0.02f);
                foreach (var hand in hands) PushHand(rig, hand);
            }

            var managerGo = new GameObject("Worlds");
            World world = null;
            World[] worlds;
            if (worldType != null)
            {
                world = (World)new GameObject(worldType.Name).AddComponent(worldType);
                world.transform.SetParent(managerGo.transform, false);
                if (SceneSettings.TryGetValue(worldType, out var json)) EditorJsonUtility.FromJsonOverwrite(json, world);
                else ProjectSetup.AssignMaterials(world);
                worlds = new[] { world };
            }
            else worlds = WorldCatalog.MakePreviews(managerGo.transform);
            var manager = managerGo.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Length;
            for (int i = 0; i < worlds.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            manager.Switch(0);
            if (world != null)
            {
                world.Build();
                HandSkin.Theme = world.Theme;
            }
            else HandSkin.Theme = HandTheme.Veins;
            HandSkin.FocusStrength = 0f;
            HandSkin.Charge[0] = HandSkin.Charge[1] = 0f;

            WorldMenu menu = null;
            if (menuOpen)
            {
                menu = new GameObject("WorldMenu").AddComponent<WorldMenu>();
                var wso = new SerializedObject(menu);
                wso.FindProperty("bubbleMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble");
                wso.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
                wso.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
                wso.ApplyModifiedPropertiesWithoutUndo();
                menu.Step(rig, manager, null, camera, 0f);
            }

            // What was learned before this moment: nothing for lessons 1 and 2, the first two for the thumb lesson, all for
            // a world's demo.
            PlayerPrefs.SetInt(Onboarding.PrefsKey, lesson == Onboarding.Lesson.Thumb ? 3 : lesson == Onboarding.Lesson.None ? 15 : 0);
            var onboarding = new GameObject("Onboarding").AddComponent<Onboarding>();
            onboarding.Begin(manager);
            if (lesson == Onboarding.Lesson.None) onboarding.NotifyWorldChanged(0);
            if (lesson == Onboarding.Lesson.Thumb) onboarding.NotifySongEnded();

            var clock = new SongClock { Time = ChorusStart - 3f };
            const float dt = 1f / 30f;
            bool opened = false;
            void Frame(float step)
            {
                var music = clock.Advance(step);
                if (world != null) world.Step(rig, music, camera, step);
                if (world != null && step > 0f) world.SimulateParticles(step);
                if (menu != null)
                {
                    if (menuOpen && !opened && onboarding.CurrentLesson == Onboarding.Lesson.OpenMenu)
                    {
                        onboarding.NotifyMenuToggle();
                        menu.Open(camera);
                        opened = true;
                    }
                    menu.Step(rig, manager, null, camera, step);
                }
                onboarding.Step(rig, manager, opened, music, camera, step);
            }
            // Run until the wanted lesson (or the demo) has shown for a while.
            for (float t = 0f; t < 60f; t += dt)
            {
                Frame(dt);
                bool there = lesson == Onboarding.Lesson.None ? onboarding.ShowingMotion != GhostMotion.None : onboarding.ShowingLesson == lesson;
                if (there && t > 3f && onboarding.GhostAlpha >= 1f && (lesson != Onboarding.Lesson.TouchWorld || opened)) break;
            }
            for (int i = 0; i < 20; i++) Frame(dt);

            void Catch(float at)
            {
                onboarding.MotionTime = at;
                Frame(0f);
                foreach (var hand in hands)
                {
                    hand.Root.GetComponent<HandSkin>().Apply();
                    SetBeat(hand.Skin, clock.BeatAge, clock.Kick);
                }
            }
            Catch(moment);
            RenderWithGhosts(onboarding, () => File.WriteAllBytes($"{OnboardingShotFolder}/{name}.png", Render(camera, 1600, 900).EncodeToPNG()));

            // A close-up strip of the ghost's loop.
            var ghost = onboarding.ShowingLeft && !GhostMotions.TwoHanded(onboarding.ShowingMotion) ? onboarding.LeftGhost : onboarding.RightGhost;
            var focus = GhostMotions.TwoHanded(onboarding.ShowingMotion)
                ? (onboarding.LeftGhost.PalmPosition + onboarding.RightGhost.PalmPosition) * 0.5f
                : ghost.PalmPosition;
            camera.fieldOfView = GhostMotions.TwoHanded(onboarding.ShowingMotion) ? 44f : 36f;
            const int w = 800, h = 450;
            var sheet = new Texture2D(w * 2, h * 2, TextureFormat.RGB24, false);
            for (int frame = 0; frame < strip.Length && frame < 4; frame++)
            {
                Catch(strip[frame]);
                camera.transform.LookAt(focus + Vector3.up * 0.03f);
                Texture2D image = null;
                RenderWithGhosts(onboarding, () => image = Render(camera, w, h));
                sheet.SetPixels(frame % 2 * w, (1 - frame / 2) * h, w, h, image.GetPixels());
            }
            sheet.Apply();
            File.WriteAllBytes($"{OnboardingShotFolder}/{name}-loop.png", sheet.EncodeToPNG());
            Object.DestroyImmediate(onboarding.gameObject);
        }

        /// <summary>
        /// Renders with the ghost hands baked into plain meshes: batch mode doesn't skin a hand again after its bones
        /// move, so each pose is baked into a stand-in with the same material and look, as the hand version shots do.
        /// </summary>
        static void RenderWithGhosts(Onboarding onboarding, System.Action render)
        {
            var standIns = new List<(GameObject go, SkinnedMeshRenderer skin)>();
            var block = new MaterialPropertyBlock();
            foreach (var ghost in new[] { onboarding.LeftGhost, onboarding.RightGhost })
            {
                if (ghost == null || !ghost.IsValid || !ghost.Root.gameObject.activeInHierarchy || ghost.Renderer == null) continue;
                var skin = ghost.Renderer;
                var mesh = new Mesh();
                skin.BakeMesh(mesh, true);
                var go = new GameObject("Baked ghost");
                go.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = skin.sharedMaterial;
                skin.GetPropertyBlock(block);
                r.SetPropertyBlock(block);
                skin.enabled = false;
                standIns.Add((go, skin));
            }
            render();
            foreach (var (go, skin) in standIns)
            {
                skin.enabled = true;
                Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(go);
            }
        }
#endif
    }
}
