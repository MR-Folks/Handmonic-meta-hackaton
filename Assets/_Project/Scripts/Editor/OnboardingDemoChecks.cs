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
        /// The first-launch demo, driven frame by frame with stand-in hands and the world menu: it opens in its first
        /// world and plays every step in order for its seconds (three minutes in all); the cassette step raises the deck
        /// with its caption above it and a pinching ghost, waits its time when no song is picked, and the deck goes; the
        /// worlds show their own ghost demos and Skip shows throughout; lesson 1 follows the demo; a replayed demo ends
        /// when a world is chosen at the menu, and Skip ends it too.
        /// </summary>
        static bool CheckOnboardingDemo()
        {
            OnboardingSetup.EnsureAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, 0f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            foreach (bool left in new[] { true, false })
            {
                var palm = new Vector3(left ? -0.15f : 0.15f, 1.27f, 0.36f);
                var tips = new Vector3[5];
                for (int i = 1; i < 5; i++) tips[i] = palm + new Vector3((left ? 1f : -1f) * (0.03f - i * 0.018f), 0.075f, 0.03f);
                tips[0] = palm + new Vector3((left ? 1f : -1f) * 0.06f, 0.02f, 0.02f);
                rig.Push(left, true, new Pose(palm, Quaternion.identity), left ? Vector3.right : Vector3.left, tips);
            }

            var managerGo = new GameObject("Worlds");
            var worlds = WorldCatalog.MakePreviews(managerGo.transform);
            var manager = managerGo.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Length;
            for (int i = 0; i < worlds.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            manager.Switch(0);

            var menu = new GameObject("WorldMenu").AddComponent<WorldMenu>();
            var wso = new SerializedObject(menu);
            wso.FindProperty("bubbleMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble");
            wso.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            wso.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            wso.FindProperty("panelMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.PanelMaterialPath, "MusicInYourHands/Panel");
            wso.ApplyModifiedPropertiesWithoutUndo();

            var failures = new List<string>();
            void Expect(bool ok, string what)
            {
                if (!ok) failures.Add(what);
            }

            string savedKey = Onboarding.PrefsKey;
            Onboarding.PrefsKey = "Onboarding.DemoCheck";
            PlayerPrefs.DeleteKey(Onboarding.PrefsKey);
            bool savedDemo = Onboarding.DemoOnFirstLaunch;
            Onboarding.DemoOnFirstLaunch = true;
            var onboarding = new GameObject("Onboarding").AddComponent<Onboarding>();
            const float Frame = 1f / 30f;
            void Tick(float dt)
            {
                menu.Step(rig, manager, null, camera, dt);
                onboarding.Step(rig, manager, menu, menu.IsOpen, default, camera, dt);
            }
            void Run(float seconds)
            {
                for (float t = 0f; t < seconds; t += Frame) Tick(Frame);
            }
            string Playing() => manager.Current >= 0 ? manager.Worlds[manager.Current].GetType().Name : "none";
            try
            {
                menu.Step(rig, manager, null, camera, 0f);
                onboarding.Begin(manager);
                Tick(Frame);
                var steps = onboarding.DemoSteps;
                Expect(onboarding.DemoRunning && onboarding.DemoStepIndex == 0, "a first launch starts the demo");
                Expect(Playing() == steps[0].World, $"the demo opens in {steps[0].World} (playing {Playing()})");
                Expect(Mathf.Abs(onboarding.DemoLength - 180f) < 0.01f, $"the demo takes three minutes at most ({onboarding.DemoLength:0} s)");
                Expect(steps.Count(s => s.World == Onboarding.CassetteStep) == 1, "the demo has one cassette step");
                var types = new HashSet<string>(WorldCatalog.Types().Select(t => t.Name));
                foreach (var s in steps)
                    Expect(s.World == Onboarding.CassetteStep || types.Contains(s.World), $"the demo's {s.World} is a world");

                var played = new List<string> { Playing() };
                var began = new List<float> { 0f };
                float clock = Frame;
                int last = 0;
                bool deck = false, caption = false, pinch = false, skip = false, hints = false, quietCassette = true;
                while (onboarding.DemoRunning && clock < 200f)
                {
                    Tick(Frame);
                    clock += Frame;
                    int i = onboarding.DemoStepIndex;
                    if (i < 0) break;
                    bool cassette = steps[i].World == Onboarding.CassetteStep;
                    if (i != last)
                    {
                        last = i;
                        played.Add(cassette ? Onboarding.CassetteStep : Playing());
                        began.Add(clock);
                    }
                    if (cassette)
                    {
                        deck |= menu.Deck != null && menu.Deck.IsOpen;
                        caption |= onboarding.Caption == Onboarding.CassetteCaption;
                        pinch |= onboarding.ShowingMotion == GhostMotion.Pinch;
                        quietCassette &= !onboarding.SkipShowing || onboarding.DemoStepTime < 0.5f;
                    }
                    else
                    {
                        skip |= onboarding.SkipShowing;
                        hints |= onboarding.ShowingMotion != GhostMotion.None;
                    }
                }
                began.Add(clock);
                var order = steps.Select(s => s.World).ToList();
                Expect(played.SequenceEqual(order), $"the demo plays {string.Join(", ", order)} in order (played {string.Join(", ", played)})");
                for (int k = 0; k < steps.Length && k + 1 < began.Count; k++)
                {
                    float took = began[k + 1] - began[k];
                    Expect(Mathf.Abs(took - steps[k].Seconds) < 0.15f, $"{steps[k].World} plays {steps[k].Seconds:0} s (played {took:0.00} s)");
                }
                Expect(!onboarding.DemoRunning && Mathf.Abs(clock - onboarding.DemoLength) < 0.3f, $"the demo is over after {onboarding.DemoLength:0} s ({clock:0.0} s)");
                Expect(deck && caption && pinch, $"the cassette step raises the deck, with its caption and a pinching ghost (deck {deck}, caption {caption}, pinch {pinch})");
                Expect(quietCassette, "no Skip target over the cassette deck");
                Expect(menu.Deck == null || !menu.Deck.IsOpen, "the deck goes after the cassette step");
                Expect(skip, "the Skip target shows during the demo");
                Expect(hints, "the worlds show their own ghost demos during the demo");
                Expect(Playing() == steps[steps.Length - 1].World, $"the demo ends in {steps[steps.Length - 1].World} (playing {Playing()})");
                Run(1.5f);
                Expect(onboarding.CurrentLesson == Onboarding.Lesson.OpenMenu, $"lesson 1 follows the demo (lesson {onboarding.CurrentLesson})");

                // A replayed demo ends when a world is chosen at the menu.
                onboarding.Replay(withOpening: true);
                Tick(Frame);
                Expect(onboarding.DemoRunning && Playing() == steps[0].World, "Replay with the opening plays the demo again");
                Run(3f);
                menu.Open(camera);
                Run(0.5f);
                float before = onboarding.DemoStepTime;
                Run(1f);
                Expect(onboarding.DemoRunning && Mathf.Abs(onboarding.DemoStepTime - before) < 0.01f, "the demo waits while the menu is open");
                int other = System.Array.FindIndex(worlds, w => w != null && w.GetType().Name != steps[0].World);
                manager.Switch(other);
                onboarding.NotifyWorldChanged(other);
                Tick(Frame);
                Expect(!onboarding.DemoRunning, "choosing a world at the menu ends the demo");
                menu.Close();

                // Skip ends it too.
                onboarding.Replay(withOpening: true);
                Tick(Frame);
                Run(2f);
                onboarding.Skip();
                Tick(Frame);
                Expect(!onboarding.DemoRunning, "Skip ends the demo");

                if (failures.Count == 0)
                    Debug.Log($"[Check] Onboarding demo: a first launch plays {string.Join(", ", order)} in order, each for its seconds " +
                              $"({onboarding.DemoLength:0} s in all); the cassette step raises the deck with its caption and a pinching ghost and " +
                              "the deck goes after it; world demos and Skip show; lesson 1 follows; choosing a world or Skip ends it.");
                else Debug.LogError("[Check] Onboarding demo: " + string.Join("; ", failures) + ".");
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
    }
}
