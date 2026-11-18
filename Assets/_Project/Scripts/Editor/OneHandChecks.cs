using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        /// <summary>
        /// The core actions work with one hand, beside their two-handed ways: a forward thumb swipe opens and closes the
        /// menu with either hand (gated like the other microgestures, and also while the menu is open); Earth and water
        /// switches for one fist held about 0.6 s (and still for both fists); Light trails clears for one fist held 0.8 s
        /// (not for a pinch, nor sooner); the Heartbeat orb rests over one open palm and turns as the hand turns; the
        /// Waveform ribbon pins an end where one hand pinches and pulls out as the hand moves away; Finger frame opens a
        /// small lens in one hand's "L" (and not for an open hand). The holds glow on the fist as they build.
        /// </summary>
        static bool CheckOneHanded()
        {
            var failures = new List<string>();
            const float Frame = 1f / 72f;

            // The menu by a forward swipe, with either hand.
            {
                var eyes = StageEyes(out var rig);
                var gestures = new GameObject("MusicGestures").AddComponent<MusicGestures>();
                SetSerialized(gestures, "bubbleMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble"));
                int asked = 0;
                bool askedLeft = false;
                void Asked() => asked++;
                WorldMenu.ToggleRequested += Asked;
                try
                {
                    foreach (bool left in new[] { true, false })
                    {
                        var hand = rig.Get(left);
                        var palm = new Vector3(left ? -0.15f : 0.15f, 1.2f, 0.35f);
                        hand.IsTracked = true;
                        hand.Palm = new Pose(palm, Quaternion.identity);
                        hand.Pinch = 0.3f;
                        hand.Fist = 0.4f;
                        for (int i = 0; i < 5; i++) hand.Tips[i] = palm + new Vector3(0f, 0.05f, 0.04f + i * 0.01f);
                    }
                    bool menuOpen = false;
                    void Run(float seconds) { for (float t = 0f; t < seconds; t += Frame) gestures.Step(rig, null, menuOpen, eyes, Frame); }
                    bool Swipe(bool left)
                    {
                        int before = asked;
                        gestures.Recognize(left, Microgesture.SwipeForward);
                        gestures.Step(rig, null, menuOpen, eyes, Frame);
                        return asked == before + 1;
                    }
                    Run(0.5f);
                    bool opens = Swipe(true);
                    Run(0.6f);
                    // With the menu open it closes it (no music needed either way), with the other hand too.
                    menuOpen = true;
                    Run(0.3f);
                    bool closes = Swipe(false);
                    menuOpen = false;
                    Run(0.6f);
                    bool cooled = Swipe(true) && !Swipe(false);
                    Run(0.6f);
                    rig.Right.Pinch = 0.95f;
                    Run(0.2f);
                    bool pinchHeld = !Swipe(false);
                    rig.Right.Pinch = 0.3f;
                    Run(0.5f);
                    // Both fists: Meta reports a thumb gesture on each hand as fists clench; neither counts.
                    rig.Left.Fist = rig.Right.Fist = 0.95f;
                    Run(0.2f);
                    bool fistsHeld = !Swipe(true) && !Swipe(false);
                    rig.Left.Fist = rig.Right.Fist = 0.4f;
                    Run(0.5f);
                    bool again = Swipe(false);
                    askedLeft = opens && closes && cooled && pinchHeld && fistsHeld && again;
                    if (!askedLeft)
                        failures.Add($"forward swipe: opens {opens}, closes while open {closes}, cooldown {cooled}, pinching hand ignored {pinchHeld}, " +
                                     $"clenched fists ignored {fistsHeld}, works again after {again}");
                }
                finally
                {
                    WorldMenu.ToggleRequested -= Asked;
                }
            }

            // Earth and water: one fist held switches the element, and a held fist only once.
            {
                var (world, rig, eyes) = StageWorld<EarthAndWater>();
                world.PreviewElement(0, false);
                PutHand(rig, true, Vector3.zero, Vector3.up, Vector3.forward, HandShape.Open, Vector3.zero);
                rig.Push(true, false, default, default, null);
                var fist = new Vector3(0.15f, 1.25f, 0.38f);
                float glowing = 0f;
                int start = world.Element;
                for (int i = 0; i < 25; i++)
                {
                    PutHand(rig, false, fist, Vector3.left, Vector3.up, HandShape.Fist, Vector3.zero);
                    world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
                }
                glowing = GestureFeedback.Glow(false, HandRig.Finger.Index);
                bool notYet = world.Element == start;
                for (int i = 0; i < 20; i++)
                {
                    PutHand(rig, false, fist, Vector3.left, Vector3.up, HandShape.Fist, Vector3.zero);
                    world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
                }
                bool switched = world.Element != start;
                for (int i = 0; i < 60; i++)
                {
                    PutHand(rig, false, fist, Vector3.left, Vector3.up, HandShape.Fist, Vector3.zero);
                    world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
                }
                bool once = world.Element != start;
                // Both fists still switch, faster.
                for (int i = 0; i < 10; i++)
                {
                    PutHand(rig, true, fist + Vector3.left * 0.3f, Vector3.right, Vector3.up, HandShape.Open, Vector3.zero);
                    PutHand(rig, false, fist, Vector3.left, Vector3.up, HandShape.Open, Vector3.zero);
                    world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
                }
                int before = world.Element;
                for (int i = 0; i < 30; i++)
                {
                    PutHand(rig, true, fist + Vector3.left * 0.3f, Vector3.right, Vector3.up, HandShape.Fist, Vector3.zero);
                    PutHand(rig, false, fist, Vector3.left, Vector3.up, HandShape.Fist, Vector3.zero);
                    world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
                }
                bool both = world.Element != before;
                if (!(notYet && switched && once && both && glowing > 0.3f))
                    failures.Add($"Earth and water: one fist waits {notYet}, switches by 0.75 s {switched}, only once while held {once}, glows as it builds ({glowing:0.00}), both fists still switch {both}");
            }

            // Light trails: one fist held clears; a quick one, or a pinch with curled fingers, doesn't.
            {
                var (world, rig, eyes) = StageWorld<LightTrails>();
                var points = new List<Vector3>();
                for (int i = 0; i < 30; i++) points.Add(new Vector3(-0.2f + i * 0.01f, 1.4f, 0.7f));
                world.PreviewStroke(points, Vector3.back, Color.white);
                rig.Push(true, false, default, default, null);
                var fist = new Vector3(0.15f, 1.25f, 0.38f);
                void Hold(HandShape shape, float seconds, float pinch = -1f)
                {
                    for (float t = 0f; t < seconds; t += 1f / 60f)
                    {
                        PutHand(rig, false, fist, Vector3.left, Vector3.up, shape, Vector3.zero);
                        if (pinch >= 0f) rig.Right.Pinch = pinch;
                        world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
                    }
                }
                Hold(HandShape.Fist, 0.5f);
                Hold(HandShape.Open, 0.2f);
                bool quick = !world.Clearing && world.StrokeCount > 0;
                Hold(HandShape.Fist, 0.6f, 0.9f);
                bool pinch = !world.Clearing;
                Hold(HandShape.Open, 0.1f);
                Hold(HandShape.Fist, 0.9f);
                bool cleared = world.Clearing;
                if (!(quick && pinch && cleared)) failures.Add($"Light trails: a quick fist keeps the strokes {quick}, a fist that pinches keeps them {pinch}, one fist held clears {cleared}");
            }

            // The Heartbeat orb on one open palm, turning with it.
            {
                var (world, rig, eyes) = StageWorld<HeartbeatOrb>();
                rig.Push(true, false, default, default, null);
                var palm = new Vector3(0.12f, 1.15f, 0.4f);
                for (int i = 0; i < 60; i++)
                {
                    PutHand(rig, false, palm, Vector3.up, Vector3.forward, HandShape.Open, Vector3.zero);
                    world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
                }
                float above = Vector3.Dot(world.Centre - palm, Vector3.up);
                bool rests = world.OnPalm && above > 0.07f && above < 0.16f && Vector3.ProjectOnPlane(world.Centre - palm, Vector3.up).magnitude < 0.03f;
                // Turn the hand about its palm's normal, about 180 degrees a second.
                for (int i = 0; i < 30; i++)
                {
                    var along = Quaternion.AngleAxis(i * 3f, Vector3.up) * Vector3.forward;
                    PutHand(rig, false, palm, Vector3.up, along, HandShape.Open, Vector3.zero);
                    world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
                }
                float spin = Vector3.Dot(world.Spin, Vector3.up);
                bool turns = spin > 1.5f;
                if (!(rests && turns)) failures.Add($"Heartbeat orb: rests on one palm {rests} ({above * 100f:0.0} cm above it), turns with the hand {turns} ({spin:0.00} rad/s)");
            }

            // The Waveform ribbon pinned by one hand and pulled out.
            {
                var (world, rig, eyes) = StageWorld<WaveformRibbon>();
                rig.Push(true, false, default, default, null);
                var from = new Vector3(0.05f, 1.3f, 0.4f);
                for (int i = 0; i < 20; i++)
                {
                    PutHand(rig, false, from, Vector3.left, Vector3.forward, HandShape.Pinch, Vector3.zero);
                    world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
                }
                bool pinned = world.Held && world.Pinned;
                for (int i = 1; i <= 30; i++)
                {
                    PutHand(rig, false, from + Vector3.right * (0.3f * i / 30f), Vector3.left, Vector3.forward, HandShape.Pinch, new Vector3(0.6f, 0f, 0f));
                    world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
                }
                float length = world.Length;
                for (int i = 0; i < 10; i++)
                {
                    PutHand(rig, false, from + Vector3.right * 0.3f, Vector3.left, Vector3.forward, HandShape.Open, Vector3.zero);
                    world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
                }
                bool letGo = !world.Held;
                if (!(pinned && length > 0.25f && letGo)) failures.Add($"Waveform ribbon: one pinch pins it {pinned}, pulled out to {length * 100f:0} cm (want over 25), let go {letGo}");
            }

            // Finger frame: one hand's "L" opens a lens; an open hand doesn't.
            {
                var (world, rig, eyes) = StageWorld<FingerFrame>();
                rig.Push(true, false, default, default, null);
                var palm = new Vector3(0.1f, 1.45f, 0.42f);
                var tips = new Vector3[5];
                void L(bool shaped)
                {
                    // Palm to you, index up, thumb out to the side; the other fingers curled in (or, open, up beside the index).
                    tips[1] = palm + new Vector3(0f, 0.1f, 0.01f);
                    tips[0] = palm + new Vector3(-0.075f, 0.01f, 0.01f);
                    for (int f = 2; f < 5; f++)
                        tips[f] = shaped ? palm + new Vector3(0.01f * f - 0.01f, 0.03f, -0.035f) : palm + new Vector3(0.02f * (f - 1), 0.09f, 0.01f);
                    if (!shaped) tips[0] = palm + new Vector3(-0.03f, 0.07f, 0.01f);
                    rig.Push(false, true, new Pose(palm, Quaternion.identity), Vector3.back, tips);
                }
                for (int i = 0; i < 40; i++) { L(false); world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f); }
                bool shut = world.LensSide < 0 && world.Openness < 0.05f;
                for (int i = 0; i < 40; i++) { L(true); world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f); }
                bool lens = world.LensSide == 1 && world.Openness > 0.9f;
                if (!(shut && lens)) failures.Add($"Finger frame: an open hand opens nothing {shut}, an L opens the lens {lens} (hand {world.LensSide}, open {world.Openness:0.00})");
            }

            GestureFeedback.Clear();
            foreach (var failure in failures) Debug.LogError($"[Check] One hand: {failure}");
            if (failures.Count == 0)
                Debug.Log("[Check] One hand: a forward thumb swipe opens and closes the menu with either hand (not while pinching, not for clenched fists, " +
                          "not twice within the cooldown); one fist held switches Earth and water (glowing as it builds, once per hold, both fists " +
                          "still faster) and clears Light trails (not for a quick fist or a pinch); the Heartbeat orb rests on one open palm and turns " +
                          "with it; the Waveform ribbon pins where one hand pinches and pulls out; Finger frame opens a lens in one hand's L.");
            return failures.Count == 0;
        }

        /// <summary>A world in a fresh scene, with its materials, looked at from standing eyes, and run for a moment with no hands.</summary>
        static (T world, HandRig rig, Camera eyes) StageWorld<T>() where T : World
        {
            var eyes = StageEyes(out var rig);
            var world = new GameObject(typeof(T).Name).AddComponent<T>();
            ProjectSetup.AssignMaterials(world);
            world.Build();
            rig.Push(true, false, default, default, null);
            rig.Push(false, false, default, default, null);
            for (int i = 0; i < 5; i++) world.Step(rig, Music(PreviewBands, 0.1f, false), eyes, 1f / 60f);
            GestureFeedback.Clear();
            return (world, rig, eyes);
        }
    }
}
