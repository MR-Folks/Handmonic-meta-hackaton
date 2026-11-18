using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        /// <summary>
        /// The onboarding, driven frame by frame with stand-in hands and the world menu: a first launch opens in Flower
        /// dance and shows lesson 1 after its payoff; opening the menu completes it and shows lesson 2, the ghost finger
        /// poking a real world bubble near the middle that the player hasn't tried, which swells, while the menu's own
        /// caption says "Touch a world" (no Skip target or caption of ours over the menu); closed without a choice, it
        /// shows how to open the menu again; choosing a world completes lesson 2; the thumb lesson comes later, fades when
        /// unused, comes back once, and a thumb gesture completes it; what was learned is remembered (a new launch shows
        /// nothing), touching Skip skips everything, and Replay starts again. The menu's "?" bubble, in the honeycomb's last
        /// cell, replays the lessons: the menu closes and lesson 1 follows, and the menu still fits the aim area. Each world's ghost demo appears for its first seconds (never while the menu is open)
        /// and goes; every world in the demo table exists; the ghost thumb really meets the index for a pinch and a tap;
        /// nothing it draws is closer than half a metre to read or can catch a touch; and steady frames allocate nothing.
        /// </summary>
        static bool CheckOnboarding()
        {
            OnboardingSetup.EnsureAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, 0f);
            var head = camera.transform.position;
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            // Both hands loosely in front of the chest.
            void Hands(Vector3? rightIndex = null)
            {
                foreach (bool left in new[] { true, false })
                {
                    var palm = new Vector3(left ? -0.15f : 0.15f, 1.27f, 0.36f);
                    var tips = new Vector3[5];
                    // Fingers up and a little apart, the thumb out to the side: not a pinch, not a fist.
                    for (int i = 1; i < 5; i++) tips[i] = palm + new Vector3((left ? 1f : -1f) * (0.03f - i * 0.018f), 0.075f, 0.03f);
                    tips[0] = palm + new Vector3((left ? 1f : -1f) * 0.06f, 0.02f, 0.02f);
                    if (!left && rightIndex.HasValue)
                    {
                        palm = rightIndex.Value + new Vector3(0f, -0.04f, -0.09f);
                        tips[1] = rightIndex.Value;
                    }
                    rig.Push(left, true, new Pose(palm, Quaternion.identity), left ? Vector3.right : Vector3.left, tips);
                }
            }
            Hands();

            var managerGo = new GameObject("Worlds");
            var worlds = WorldCatalog.MakePreviews(managerGo.transform);
            var manager = managerGo.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Length;
            for (int i = 0; i < worlds.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            manager.Switch(0);
            int IndexOf<T>() where T : World => System.Array.FindIndex(worlds, w => w is T);

            var menu = new GameObject("WorldMenu").AddComponent<WorldMenu>();
            var wso = new SerializedObject(menu);
            wso.FindProperty("bubbleMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble");
            wso.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            wso.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            wso.FindProperty("panelMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.PanelMaterialPath, "MusicInYourHands/Panel");
            wso.ApplyModifiedPropertiesWithoutUndo();
            float bubbleDiameter = wso.FindProperty("bubbleDiameter").floatValue;

            var failures = new List<string>();
            void Expect(bool ok, string what)
            {
                if (!ok) failures.Add(what);
            }

            string savedKey = Onboarding.PrefsKey;
            Onboarding.PrefsKey = "Onboarding.Check";
            // The lessons on their own; CheckOnboardingDemo plays the first-launch demo.
            bool savedDemo = Onboarding.DemoOnFirstLaunch;
            Onboarding.DemoOnFirstLaunch = false;
            PlayerPrefs.DeleteKey(Onboarding.PrefsKey);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var onboarding = new GameObject("Onboarding").AddComponent<Onboarding>();
            const float Frame = 1f / 72f;
            // The menu and the onboarding, a frame at a time, as on the headset (the menu first).
            void Tick(float dt)
            {
                menu.Step(rig, manager, null, camera, dt);
                onboarding.Step(rig, manager, menu, menu.IsOpen, default, camera, dt);
            }
            void Run(float seconds)
            {
                for (float t = 0f; t < seconds; t += Frame) Tick(Frame);
            }
            // The onboarding alone (the menu closed), to count its own allocations.
            void RunOnly(float seconds)
            {
                for (float t = 0f; t < seconds; t += Frame) onboarding.Step(rig, manager, menu, false, default, camera, Frame);
            }
            // Runs until something happens (or the limit), and says how long it took.
            float RunUntil(System.Func<bool> done, float limit)
            {
                float t = 0f;
                for (; t < limit && !done(); t += Frame) Tick(Frame);
                return t;
            }
            void SetMenu(bool open)
            {
                if (!open)
                {
                    menu.Close();
                    return;
                }
                menu.Step(rig, manager, null, camera, 0f);
                menu.Open(camera);
            }
            try
            {
                // First launch: the opening world, a payoff with Flower dance's own demo, then lesson 1.
                onboarding.Begin(manager);
                Tick(Frame);
                float buildMs = (float)stopwatch.Elapsed.TotalMilliseconds;
                Expect(manager.Worlds[manager.Current] is FlowerDance, "the first launch opens in Flower dance");
                Expect(onboarding.LeftGhost.IsValid && onboarding.RightGhost.IsValid, "both ghost hands are made from Resources/Onboarding");
                Expect(onboarding.LeftGhost.FromStore && onboarding.RightGhost.FromStore, "the ghost hands' thumb shapes come stored with them (no search on the headset)");
                Run(4f);
                Expect(onboarding.IsOpening && onboarding.CurrentLesson == Onboarding.Lesson.None, "no lesson during the opening's payoff");
                Expect(onboarding.ShowingMotion == GhostMotion.OpenPalm, $"Flower dance's open-palm demo shows during the payoff (showing {onboarding.ShowingMotion})");
                Run(6.5f);
                Expect(onboarding.CurrentLesson == Onboarding.Lesson.OpenMenu && onboarding.ShowingLesson == Onboarding.Lesson.OpenMenu,
                    $"lesson 1 shows after the payoff (lesson {onboarding.CurrentLesson}, showing {onboarding.ShowingLesson})");
                Expect(onboarding.ShowingMotion == GhostMotion.MenuGesture && onboarding.ShowingLeft, "lesson 1's ghost is the left hand's menu gesture");
                Expect(onboarding.Caption == Onboarding.OpenMenuCaption, $"lesson 1's caption (\"{onboarding.Caption}\")");
                Expect(onboarding.SkipShowing, "the Skip target shows with lesson 1");
                Expect(Vector3.Distance(head, onboarding.SkipPosition) >= 0.49f, "the Skip target is at least half a metre away");
                Expect(InComfortableView(camera, onboarding.LeftGhost.PalmPosition), "lesson 1's ghost is within Meta's comfortable view area");
                Expect(CaptionDistance(onboarding, head) >= 0.49f, "the caption is at least half a metre away");
                Expect(onboarding.GetComponentsInChildren<Collider>(true).Length == 0, "nothing the onboarding draws can catch a touch (no colliders)");

                // Allocation-free steady frames, during a lesson with the ghost, caption, Skip target and fingertip lights.
                int probe = AllocationsIn(() => allocationProbe = new byte[2048]);
                int lessonFrames = AllocationsIn(() => RunOnly(4f));
                allocationProbe = null;

                // Opening the menu completes lesson 1 and brings lesson 2, at the menu, without the Skip target.
                onboarding.NotifyMenuToggle();
                SetMenu(true);
                Run(1.2f);
                Expect(onboarding.HasLearned(Onboarding.Lesson.OpenMenu), "opening the menu completes lesson 1");
                Expect(PlayerPrefs.GetInt(Onboarding.PrefsKey, 0) == 1, "lesson 1 is remembered");
                Expect(onboarding.CurrentLesson == Onboarding.Lesson.TouchWorld && onboarding.ShowingMotion == GhostMotion.TouchMenu && !onboarding.ShowingLeft,
                    $"lesson 2 shows the right hand poking the menu (showing {onboarding.ShowingMotion})");
                Expect(onboarding.Caption == "" && menu.Prompt == Onboarding.TouchWorldCaption,
                    $"lesson 2's words are the menu's own caption line (ours \"{onboarding.Caption}\", the menu's \"{menu.Prompt}\")");
                Expect(!onboarding.SkipShowing, "no Skip target over the open menu");
                // The ghost finger aims at a real world bubble near the middle that the player hasn't been in yet, and that
                // bubble swells to invite the touch.
                int target = menu.Beckoned;
                var middle = Vector3.zero;
                for (int i = 0; i < worlds.Length; i++)
                    if (menu.BubblePosition(i, out var p)) middle += p / worlds.Length;
                bool found = menu.BubblePosition(target, out var bubbleAt);
                Expect(found && target != manager.Current,
                    $"lesson 2 aims at a world bubble other than the current one (world {target})");
                Expect(found && Vector3.Distance(bubbleAt, middle) < bubbleDiameter * 1.6f,
                    $"lesson 2's bubble is near the middle of the honeycomb ({Vector3.Distance(bubbleAt, middle) * 100f:0.0} cm from it)");
                onboarding.MotionTime = 0.4f;
                onboarding.Step(rig, manager, menu, true, default, camera, 0f);
                float before = Vector3.Distance(onboarding.RightGhost.Tip(1), bubbleAt);
                float pointing = Vector3.Angle(onboarding.RightGhost.Tip(1) - onboarding.RightGhost.PalmPosition, bubbleAt - onboarding.RightGhost.PalmPosition);
                onboarding.MotionTime = 1.3f;
                onboarding.Step(rig, manager, menu, true, default, camera, 0f);
                float poked = Vector3.Distance(onboarding.RightGhost.Tip(1), bubbleAt);
                Expect(pointing < 15f && poked < before - 0.05f && poked < 0.035f,
                    $"lesson 2's finger points at the bubble ({pointing:0}° off) and pokes up to it ({before * 100f:0.0} cm, then {poked * 100f:0.0} cm)");

                // The menu closes without a choice: lesson 2 shows how to open it again; reopened, the poke returns.
                SetMenu(false);
                Run(2f);
                Expect(menu.Prompt == null && menu.Beckoned < 0, "with the menu closed, lesson 2 gives the menu back its caption and bubble");
                Expect(onboarding.CurrentLesson == Onboarding.Lesson.TouchWorld && onboarding.ShowingMotion == GhostMotion.MenuGesture &&
                       onboarding.Caption == Onboarding.OpenMenuCaption, "with the menu closed, lesson 2 shows how to open it again");
                SetMenu(true);
                Run(1.2f);
                Expect(onboarding.ShowingMotion == GhostMotion.TouchMenu, "reopened, lesson 2's poke shows again");

                // Choosing a world completes lesson 2; its ghost demo waits for the menu to close, shows, and goes on time.
                int trails = IndexOf<LightTrails>();
                manager.Switch(trails);
                onboarding.NotifyWorldChanged(trails);
                Run(0.3f);
                Expect(onboarding.HasLearned(Onboarding.Lesson.TouchWorld) && onboarding.CurrentLesson == Onboarding.Lesson.None,
                    "choosing a world at the menu completes lesson 2");
                Expect(onboarding.ShowingMotion == GhostMotion.None, "no ghost demo while the menu is open");
                Expect(menu.Prompt == null && menu.Beckoned < 0, "lesson 2 done, the menu has its own caption back");
                SetMenu(false);
                Run(1.2f);
                Expect(onboarding.ShowingMotion == GhostMotion.Pinch, $"Light trails' pinch demo shows once the menu closes (showing {onboarding.ShowingMotion})");
                Expect(onboarding.Caption == "", "a world's demo has no caption of its own (the world's hint has the words)");
                Run(9f);
                Expect(onboarding.ShowingMotion == GhostMotion.None, "the demo has gone after its ten seconds");

                // The thumb lesson: about 40 s into the chosen world; unused it fades, comes back once, and a thumb gesture
                // completes it.
                float waited = 10.5f + RunUntil(() => onboarding.CurrentLesson == Onboarding.Lesson.Thumb, 60f);
                Run(1f);
                Expect(onboarding.CurrentLesson == Onboarding.Lesson.Thumb && waited > 38f && waited < 46f,
                    $"the thumb lesson comes about 40 s into the chosen world (after {waited:0} s)");
                Expect(onboarding.ShowingMotion == GhostMotion.ThumbTapSwipe && !onboarding.ShowingLeft, $"the thumb lesson's ghost (showing {onboarding.ShowingMotion})");
                Expect(onboarding.Caption == Onboarding.ThumbCaption, $"the thumb lesson's caption (\"{onboarding.Caption}\")");
                Expect(onboarding.SkipShowing, "the Skip target shows with the thumb lesson");
                float shown = 1f + RunUntil(() => onboarding.CurrentLesson != Onboarding.Lesson.Thumb, 30f);
                Expect(!onboarding.HasLearned(Onboarding.Lesson.Thumb) && shown > 14f && shown < 18f, $"unused, the thumb lesson fades (after {shown:0} s)");
                float away = RunUntil(() => onboarding.CurrentLesson == Onboarding.Lesson.Thumb, 150f);
                Expect(onboarding.CurrentLesson == Onboarding.Lesson.Thumb && away > 85f && away < 100f, $"it comes back once, later (after {away:0} s)");
                Run(1f);
                onboarding.NotifyMicrogesture(false);
                Run(0.5f);
                Expect(onboarding.HasLearned(Onboarding.Lesson.Thumb) && onboarding.CurrentLesson == Onboarding.Lesson.None,
                    "a thumb tap or swipe completes the thumb lesson");
                Expect(PlayerPrefs.GetInt(Onboarding.PrefsKey, 0) == (1 | 2 | 4 | 8), "every lesson is remembered");

                // A later launch: no opening, no lessons.
                Object.DestroyImmediate(onboarding.gameObject);
                int bubbles = IndexOf<BubbleNotes>();
                manager.Switch(bubbles);
                onboarding = new GameObject("Onboarding").AddComponent<Onboarding>();
                onboarding.Begin(manager);
                Run(15f);
                Expect(manager.Current == bubbles && !onboarding.IsOpening && onboarding.CurrentLesson == Onboarding.Lesson.None &&
                       onboarding.Caption == "", "a later launch starts as before, with no opening and no lessons");

                // Each world's demo for its first seconds.
                foreach (var (index, motion, label) in new[]
                         {
                             (IndexOf<BubbleNotes>(), GhostMotion.OpenPalm, "Bubble notes"), (IndexOf<Lightning>(), GhostMotion.Fist, "Lightning"),
                             (IndexOf<HeartbeatOrb>(), GhostMotion.PalmsFacing, "Heartbeat orb"), (IndexOf<FingerFrame>(), GhostMotion.TwoHandFrame, "Finger frame")
                         })
                {
                    manager.Switch(index);
                    onboarding.NotifyWorldChanged(index);
                    Run(0.3f);
                    Expect(onboarding.ShowingMotion == GhostMotion.None, $"{label}'s demo waits a moment");
                    Run(1.2f);
                    Expect(onboarding.ShowingMotion == motion, $"{label}'s demo is {motion} (showing {onboarding.ShowingMotion})");
                    if (motion == GhostMotion.PalmsFacing)
                    {
                        int demoFrames = AllocationsIn(() => RunOnly(3f));
                        Expect(demoFrames == 0, $"two-handed demo frames allocate nothing ({demoFrames})");
                    }
                    else Run(3f);
                    Expect(InComfortableView(camera, onboarding.RightGhost.PalmPosition), $"{label}'s demo is within Meta's comfortable view area");
                    Run(6.5f);
                    Expect(onboarding.ShowingMotion == GhostMotion.None, $"{label}'s demo goes after its ten seconds");
                }

                // Solar system's pinch hands over to a ghost pointing up at the stars, with its caption, and both go.
                int solar = IndexOf<SolarSystem>();
                manager.Switch(solar);
                onboarding.NotifyWorldChanged(solar);
                Run(1.5f);
                Expect(onboarding.ShowingMotion == GhostMotion.Pinch && onboarding.Caption == "",
                    $"Solar system's demo starts with the pinch (showing {onboarding.ShowingMotion})");
                Run(5.5f);
                var skyward = onboarding.RightGhost.Tip(1) - onboarding.RightGhost.PalmPosition;
                string skyCaption = GhostMotions.FollowUps[0].Caption;
                Expect(onboarding.ShowingMotion == GhostMotion.PointUp && onboarding.Caption == skyCaption && skyward.y > skyward.magnitude * 0.6f,
                    $"then its ghost points up at the stars with \"{skyCaption}\" (showing {onboarding.ShowingMotion}, \"{onboarding.Caption}\", " +
                    $"index {Vector3.Angle(skyward, Vector3.up):0}° from straight up)");
                Expect(InComfortableView(camera, onboarding.RightGhost.PalmPosition), "Solar system's point-up demo is within Meta's comfortable view area");
                Run(6f);
                Expect(onboarding.ShowingMotion == GhostMotion.None && onboarding.Caption == "", "Solar system's demos go after twelve seconds");

                var probeWorld = new GameObject("Hook probe").AddComponent<HookProbe>();
                Expect(GhostMotions.ForWorld(probeWorld) == GhostMotion.None, "a world without a demo keeps its text only");
                Object.DestroyImmediate(probeWorld.gameObject);

                // Replay starts the lessons again; touching Skip skips them all.
                onboarding.Replay();
                Run(1.2f);
                Expect(onboarding.CurrentLesson == Onboarding.Lesson.OpenMenu && onboarding.ShowingMotion == GhostMotion.MenuGesture &&
                       !onboarding.HasLearned(Onboarding.Lesson.OpenMenu), "Replay starts again from lesson 1");
                var skip = onboarding.SkipPosition;
                var towardsEyes = (head - skip).normalized;
                for (float d = 0.1f; d > -0.01f; d -= 0.01f)
                {
                    Hands(skip + towardsEyes * d);
                    Tick(Frame);
                }
                Hands();
                Run(1f);
                Expect(onboarding.HasLearned(Onboarding.Lesson.OpenMenu) && onboarding.HasLearned(Onboarding.Lesson.TouchWorld) &&
                       onboarding.HasLearned(Onboarding.Lesson.Thumb) && onboarding.CurrentLesson == Onboarding.Lesson.None,
                    "touching Skip skips every lesson");
                Expect(onboarding.ShowingLesson == Onboarding.Lesson.None && onboarding.Caption == "" && !onboarding.SkipShowing,
                    "after Skip the ghost, caption and target fade away");
                onboarding.Replay();
                Run(1.2f);
                Expect(onboarding.CurrentLesson == Onboarding.Lesson.OpenMenu, "Replay restarts after a Skip too");

                // The menu's "?" bubble: in the honeycomb's last cell, the whole menu still within the aim area; touched,
                // it replays the lessons, the menu closes and lesson 1 follows it. A later launch still starts as before.
                onboarding.Skip();
                Run(1f);
                SetMenu(true);
                Run(1.2f);
                var menuRoot = menu.transform.Find("Menu");
                var help = menuRoot.Find("Bubble Help");
                var lastWorld = menuRoot.Find($"Bubble {worlds.Length - 1}");
                Expect(help != null && help.gameObject.activeSelf, "the menu shows a \"?\" bubble");
                if (help != null && lastWorld != null)
                {
                    var helpLocal = menuRoot.InverseTransformPoint(help.position);
                    var lastLocal = menuRoot.InverseTransformPoint(lastWorld.position);
                    Expect(Mathf.Abs(helpLocal.y - lastLocal.y) < 0.008f && helpLocal.x > lastLocal.x,
                        "the \"?\" sits in the honeycomb's last cell, after the last world");
                }
                var reach = new Extent();
                foreach (Transform child in menuRoot)
                {
                    if (!child.name.StartsWith("Bubble ")) continue;
                    for (int k = 0; k < 32; k++)
                    {
                        float a = k * Mathf.PI * 2f / 32f;
                        reach.Add(camera.transform, child.position + (menuRoot.right * Mathf.Cos(a) + menuRoot.up * Mathf.Sin(a)) * (bubbleDiameter * WorldMenu.NearGaze * 0.5f));
                    }
                }
                Expect(reach.Inside(WorldMenu.AimArea), $"with the \"?\" the bubbles still fit the {WorldMenu.AimArea.x:0}° × {WorldMenu.AimArea.y:0}° aim area ({reach})");
                if (help != null)
                {
                    var helpAt = help.position;
                    for (float d = 0.08f; d > -0.015f; d -= 0.01f)
                    {
                        Hands(helpAt - menuRoot.forward * d);
                        Tick(Frame);
                    }
                }
                Hands();
                Expect(onboarding.CurrentLesson == Onboarding.Lesson.OpenMenu && !onboarding.HasLearned(Onboarding.Lesson.OpenMenu),
                    $"touching the \"?\" replays the lessons (lesson {onboarding.CurrentLesson})");
                Expect(onboarding.ShowingLesson == Onboarding.Lesson.None, "while the menu is still open, lesson 1 waits");
                float closed = RunUntil(() => !menu.IsOpen, 2f);
                Expect(!menu.IsOpen && closed < 0.6f, $"the menu closes after the \"?\" ({closed:0.00} s)");
                Run(0.15f);
                Expect(onboarding.ShowingLesson == Onboarding.Lesson.None, "lesson 1 follows the menu away a moment later, not at once");
                Run(1.2f);
                Expect(onboarding.ShowingLesson == Onboarding.Lesson.OpenMenu && onboarding.Caption == Onboarding.OpenMenuCaption,
                    "then lesson 1 shows how to open the menu again");
                Expect(PlayerPrefs.GetInt(Onboarding.PrefsKey, 0) == (1 | 2 | 4 | 8), "replaying keeps what is remembered, so a later launch starts as before");

                // The ghost thumb really meets the index: pinch and thumb tap, on both hands.
                foreach (var ghost in new[] { onboarding.LeftGhost, onboarding.RightGhost })
                {
                    var at = head + new Vector3(0f, -0.2f, 0.4f);
                    ghost.Pose(at, Vector3.up, Vector3.back, new Vector4(GhostHand.PinchIndexCurl, 0.3f, 0.34f, 0.38f), 0f, ThumbShape.Pinch, ThumbShape.Pinch, 1f);
                    float pinch = Vector3.Distance(ghost.Tip(0), ghost.Tip(1));
                    ghost.Pose(at, Vector3.forward, Vector3.left, new Vector4(GhostHand.TapIndexCurl, 0.62f, 0.7f, 0.76f), 0f, ThumbShape.Tap, ThumbShape.Tap, 1f);
                    float tap = Vector3.Distance(ghost.Tip(0), ghost.IndexSide);
                    ghost.Pose(at, Vector3.forward, Vector3.left, new Vector4(GhostHand.TapIndexCurl, 0.62f, 0.7f, 0.76f), 0f, ThumbShape.Lift, ThumbShape.Lift, 1f);
                    float lift = Vector3.Distance(ghost.Tip(0), ghost.IndexSide);
                    string side = ghost.Left ? "left" : "right";
                    Expect(pinch < 0.02f, $"the {side} ghost's thumb and index tips meet in a pinch ({pinch * 1000f:0} mm apart)");
                    Expect(tap < 0.012f && lift > tap + 0.012f, $"the {side} ghost's thumb lands on the side of its index for a tap and lifts off ({tap * 1000f:0} mm, lifted {lift * 1000f:0} mm)");
                }

                // MusicGestures reports the thumb gestures it carries out (the thumb lesson listens), not those it holds back.
                var gestures = new GameObject("MusicGestures").AddComponent<MusicGestures>();
                SetSerialized(gestures, "bubbleMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble"));
                var remote = new StandInMusic();
                foreach (var title in new[] { "One", "Two" })
                {
                    var song = ScriptableObject.CreateInstance<SongAsset>();
                    song.title = title;
                    remote.Songs.Add(song);
                }
                int used = 0;
                bool usedLeft = true;
                System.Action<bool, Microgesture> onUsed = (left, gesture) =>
                {
                    used++;
                    usedLeft = left;
                };
                MusicGestures.Used += onUsed;
                try
                {
                    for (int i = 0; i < 60; i++) gestures.Step(rig, remote, false, camera, Frame);
                    gestures.Recognize(false, Microgesture.ThumbTap);
                    gestures.Step(rig, remote, false, camera, Frame);
                    bool reported = used == 1 && !usedLeft;
                    for (int i = 0; i < 60; i++) gestures.Step(rig, remote, true, camera, Frame);
                    gestures.Recognize(true, Microgesture.SwipeRight);
                    gestures.Step(rig, remote, true, camera, Frame);
                    Expect(reported && used == 1, $"MusicGestures reports a thumb tap it carries out, and not one held back by the open menu ({used} reported)");
                }
                finally
                {
                    MusicGestures.Used -= onUsed;
                    foreach (var song in remote.Songs) Object.DestroyImmediate(song);
                }

                // Every world named in the demo table exists, with a motion.
                var types = new HashSet<string>(WorldCatalog.Types().Select(t => t.Name));
                foreach (var (world, motion) in GhostMotions.WorldTable)
                    Expect(types.Contains(world) && motion != GhostMotion.None, $"the demo table's {world} is a world with a motion");
                foreach (var (world, motion, caption) in GhostMotions.FollowUps)
                    Expect(types.Contains(world) && motion != GhostMotion.None && !string.IsNullOrEmpty(caption) &&
                           GhostMotions.WorldTable.Any(e => e.World == world), $"the second demos' {world} is a world with a first demo, a motion and a caption");
                var unmapped = types.Where(t => GhostMotions.WorldTable.All(e => e.World != t)).ToList();

                Expect(probe > 0 && lessonFrames == 0, $"lesson frames allocate nothing ({lessonFrames}, probe {probe})");
                if (failures.Count == 0)
                    Debug.Log("[Check] Onboarding: a first launch opens in Flower dance, and after its payoff the ghost left hand shows the menu " +
                              "gesture; opening the menu brings the poke at the menu, choosing a world completes it; the thumb lesson comes " +
                              "later, fades, comes back once and a thumb gesture completes it; it is all remembered, Skip skips it and Replay " +
                              "restarts, and so does the menu's \"?\"; lesson 2 points at a real bubble; world demos show for their first seconds, never over the menu, " +
                              "and Solar system's then points up at the stars with its caption; " +
                              "MusicGestures reports the thumb gestures it carries out; " +
                              "ghost thumbs meet the index; " +
                              $"nothing nearer than half a metre to read; steady frames allocate nothing. Ghost hands made in {buildMs:0} ms. " +
                              $"{GhostMotions.WorldTable.Length} worlds have a demo{(unmapped.Count > 0 ? "; without one: " + string.Join(", ", unmapped) : "")}.");
                else Debug.LogError("[Check] Onboarding: " + string.Join("; ", failures) + ".");
                return failures.Count == 0;
            }
            finally
            {
                PlayerPrefs.DeleteKey(Onboarding.PrefsKey);
                Onboarding.PrefsKey = savedKey;
                Onboarding.DemoOnFirstLaunch = savedDemo;
                if (onboarding != null) Object.DestroyImmediate(onboarding.gameObject);
            }
        }

        /// <summary>Whether a point is inside Meta's recommended design area: 29 degrees either side and 27 up or down of where you look.</summary>
        static bool InComfortableView(Camera view, Vector3 point)
        {
            var local = view.transform.InverseTransformPoint(point);
            if (local.z <= 0f) return false;
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
            return Mathf.Abs(yaw) <= 29f && Mathf.Abs(pitch) <= 27f;
        }

        static float CaptionDistance(Onboarding onboarding, Vector3 head)
        {
            var caption = onboarding.transform.Find("Onboarding Look/Caption");
            return caption != null ? Vector3.Distance(head, caption.position) : 0f;
        }
    }
}
