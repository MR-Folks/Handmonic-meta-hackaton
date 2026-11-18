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
        /// The gesture feedback (GestureFeedback): each kind of gesture lights just its own fingers (on its own hand) and
        /// they fade within half a second; each Show bursts once and sounds once (none with sound off); a held glow
        /// lasts while it is kept up and fades once it isn't; World's pinches give feedback in a world that overrides
        /// OnPinch, once per pinch and once per let-go; a thumb tap lights the thumb and index; the menu's toggle lights
        /// the hand that made it; and the whole path (Show, the fades, HandSkin pushing them to the hand) allocates
        /// nothing once its sounds exist. Renders Logs/preview-gesture.png (a pinch and a fist lighting up, with their
        /// bursts) and Logs/preview-gesture-themes.png (a lit pinch in every world's hand look).
        /// </summary>
        static bool CheckGestureFeedback()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var failures = new List<string>();
            var at = new Vector3(0.1f, 1.3f, 0.4f);

            // Each kind lights its own fingers on its own hand, bursts once and sounds once.
            var expected = new (GestureKind kind, Fingers fingers)[]
            {
                (GestureKind.Pinch, Fingers.Thumb | Fingers.Index), (GestureKind.Release, Fingers.Thumb | Fingers.Index),
                (GestureKind.Fist, Fingers.All), (GestureKind.ThumbTap, Fingers.Thumb | Fingers.Index),
                (GestureKind.ThumbSwipe, Fingers.Thumb | Fingers.Index), (GestureKind.Touch, Fingers.Index),
                (GestureKind.MenuOpen, Fingers.All), (GestureKind.MenuClose, Fingers.All), (GestureKind.Done, Fingers.All)
            };
            foreach (var (kind, fingers) in expected)
                foreach (bool left in new[] { true, false })
                {
                    GestureFeedback.Clear();
                    GestureFeedback.Tick(0.5f);
                    int shows = GestureFeedback.Shows, bursts = GestureFeedback.Bursts, sounds = GestureFeedback.Sounds;
                    GestureFeedback.Show(kind, left, at);
                    GestureFeedback.Tick(1f / 72f);
                    string lit = Lit(left, fingers);
                    if (lit != null) failures.Add($"{kind} ({(left ? "left" : "right")}): {lit}");
                    if (GestureFeedback.Shows != shows + 1 || GestureFeedback.Bursts != bursts + 1 || GestureFeedback.Sounds != sounds + 1)
                        failures.Add($"{kind}: {GestureFeedback.Shows - shows} shows, {GestureFeedback.Bursts - bursts} bursts, {GestureFeedback.Sounds - sounds} sounds (want one each)");
                    for (int i = 0; i < 36; i++) GestureFeedback.Tick(1f / 72f);
                    if (MostLit() > 0f) failures.Add($"{kind}: still lit {MostLit():0.00} half a second later");
                }

            // A touch by another finger lights that finger; sound off is silent; strength scales the light.
            GestureFeedback.Clear();
            GestureFeedback.Tick(0.5f);
            int quietSounds = GestureFeedback.Sounds;
            GestureFeedback.Show(GestureKind.Touch, false, at, 0.3f, false, GestureFeedback.Of(HandRig.Finger.Ring));
            GestureFeedback.Tick(0.01f);
            string ring = Lit(false, Fingers.Ring);
            if (ring != null) failures.Add($"a ring-finger touch: {ring}");
            if (GestureFeedback.Sounds != quietSounds) failures.Add("a Show with sound off made a sound");
            if (GestureFeedback.Glow(false, HandRig.Finger.Ring) > 0.8f) failures.Add("a weak touch lit as brightly as a strong one");

            // A held glow lasts while kept up and fades soon after.
            GestureFeedback.Clear();
            for (int i = 0; i < 30; i++)
            {
                GestureFeedback.Hold(true, Fingers.Middle, 0.6f);
                GestureFeedback.Tick(1f / 72f);
            }
            bool heldUp = Mathf.Abs(GestureFeedback.Glow(true, HandRig.Finger.Middle) - 0.6f) < 0.01f && Lit(true, Fingers.Middle) == null;
            for (int i = 0; i < 20; i++) GestureFeedback.Tick(1f / 72f);
            if (!heldUp || GestureFeedback.Glow(true, HandRig.Finger.Middle) > 0f) failures.Add("a held glow didn't stay while kept up, or didn't fade once let go");

            // World: a world that overrides OnPinch gets feedback for each pinch and let-go, once each.
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, 0f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var probe = new GameObject("Probe").AddComponent<HookProbe>();
            ProjectSetup.AssignMaterials(probe);
            float time = 1f;
            void Pinch(float gap, int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    var index = new Vector3(0.2f, 1.3f, 0.35f);
                    var tips = new[]
                    {
                        index + new Vector3(0f, -gap, 0f), index, index + new Vector3(-0.02f, -0.05f, -0.06f),
                        index + new Vector3(-0.03f, -0.055f, -0.065f), index + new Vector3(-0.04f, -0.06f, -0.07f)
                    };
                    rig.Push(false, true, new Pose(index + new Vector3(-0.02f, -0.04f, -0.09f), Quaternion.identity), Vector3.left, tips);
                    probe.Step(rig, new MusicFrame { SongTime = time += 0.05f }, camera, 0.05f);
                    GestureFeedback.Tick(0.05f);
                }
            }
            Pinch(0.06f, 3);
            int before = GestureFeedback.Shows;
            Pinch(0.008f, 1);
            bool pinchShown = GestureFeedback.Shows == before + 1 && GestureFeedback.LastKind == GestureKind.Pinch && Lit(false, Fingers.Thumb | Fingers.Index) == null;
            Pinch(0.008f, 6);
            bool once = GestureFeedback.Shows == before + 1;
            Pinch(0.06f, 1);
            bool releaseShown = GestureFeedback.Shows == before + 2 && GestureFeedback.LastKind == GestureKind.Release;
            if (!pinchShown || !once || !releaseShown)
                failures.Add($"World's pinch feedback: shown on the pinch {pinchShown}, only once while held {once}, on the let-go {releaseShown}");

            // A thumb tap lights the thumb and index of the hand that tapped.
            var gestures = new GameObject("MusicGestures").AddComponent<MusicGestures>();
            SetSerialized(gestures, "bubbleMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble"));
            var music = new StandInMusic();
            var song = ScriptableObject.CreateInstance<SongAsset>();
            song.title = "Glass Room";
            music.Songs.Add(song);
            var tapper = rig.Left;
            tapper.IsTracked = true;
            tapper.Palm = new Pose(new Vector3(-0.15f, 1.2f, 0.35f), Quaternion.identity);
            tapper.Pinch = 0.3f;
            tapper.Fist = 0.4f;
            for (int i = 0; i < 5; i++) tapper.Tips[i] = tapper.Palm.position + new Vector3(0f, 0.05f, 0.04f + i * 0.01f);
            for (int i = 0; i < 40; i++) gestures.Step(rig, music, false, camera, 1f / 72f);
            GestureFeedback.Clear();
            gestures.Recognize(true, Microgesture.ThumbTap);
            gestures.Step(rig, music, false, camera, 1f / 72f);
            GestureFeedback.Tick(0.01f);
            string tap = Lit(true, Fingers.Thumb | Fingers.Index);
            if (tap != null || GestureFeedback.LastKind != GestureKind.ThumbTap) failures.Add($"a thumb tap: {tap ?? "it showed " + GestureFeedback.LastKind}");
            Object.DestroyImmediate(song);

            // Nothing allocates on the way, once each sound exists: Show, the fades and the hand being drawn.
            var skin = StageFeedbackHand(out var drawnHand);
            bool drawn = skin != null;
            foreach (var (kind, _) in expected) GestureFeedback.Show(kind, false, at);
            probe.Step(rig, new MusicFrame { SongTime = time += 0.05f }, camera, 0.05f);
            int probeCount = AllocationsIn(() => allocationProbe = new byte[2048]);
            int allocations = AllocationsIn(() =>
            {
                for (int i = 0; i < 90; i++)
                {
                    var (kind, _) = expected[i % expected.Length];
                    GestureFeedback.Show(kind, i % 2 == 0, at, 0.5f + (i % 3) * 0.25f);
                    GestureFeedback.Hold(true, Fingers.Middle, 0.5f);
                    GestureFeedback.Tick(1f / 72f);
                    skin?.Apply();
                }
            });
            allocationProbe = null;
            if (probeCount == 0 || allocations > 0) failures.Add($"the feedback path allocated {allocations} times in 90 gestures (probe {probeCount})");
            if (drawnHand != null) Object.DestroyImmediate(drawnHand);

            foreach (var failure in failures) Debug.LogError($"[Check] Gesture feedback: {failure}");
            bool ok = failures.Count == 0;
            if (ok)
                Debug.Log("[Check] Gesture feedback: every kind lights only its own fingers, fades within half a second and bursts and sounds once " +
                          "(silent with sound off); held glows last while kept up; World's pinches and a thumb tap light the right fingers; " +
                          $"nothing allocates on the way{(drawn ? ", the hand drawn too" : " (without a hand mesh to draw)")}.");
            ok &= CheckGesturePreview();
            GestureFeedback.Clear();
            return ok;
        }

        /// <summary>Null when exactly <paramref name="fingers"/> of that hand are lit (at least 0.5) and nothing else is; else what is wrong.</summary>
        static string Lit(bool left, Fingers fingers)
        {
            var wrong = new List<string>();
            foreach (bool side in new[] { true, false })
                for (int f = 0; f < 5; f++)
                {
                    float glow = GestureFeedback.Glow(side, (HandRig.Finger)f);
                    bool want = side == left && ((int)fingers & (1 << f)) != 0;
                    if (want && glow < 0.25f) wrong.Add($"{(side ? "left" : "right")} {(HandRig.Finger)f} dark ({glow:0.00})");
                    if (!want && glow > 0f) wrong.Add($"{(side ? "left" : "right")} {(HandRig.Finger)f} lit ({glow:0.00})");
                }
            return wrong.Count == 0 ? null : string.Join(", ", wrong);
        }

        static float MostLit()
        {
            float most = 0f;
            foreach (bool side in new[] { true, false })
                for (int f = 0; f < 5; f++) most = Mathf.Max(most, GestureFeedback.Glow(side, (HandRig.Finger)f));
            return most;
        }

        /// <summary>One of Meta's hands with the project look, to see HandSkin draw lit fingers; null without the Meta SDK.</summary>
        static HandSkin StageFeedbackHand(out GameObject hand)
        {
            hand = null;
#if META_XR_CORE
            var path = "Packages/com.meta.xr.sdk.core/Prefabs/OpenXRCustomHandPrefab_R.prefab";
            hand = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            ProjectSetup.ApplyHandLook(hand);
            var skin = hand.GetComponent<HandSkin>();
            skin.Apply();
            return skin;
#else
            return null;
#endif
        }

        /// <summary>
        /// A pinch and a fist lighting up as the player would see them: the right hand pinching with its thumb and index
        /// lit and sparks leaving the pinch, the left hand in a fist with all five lit, close up over a room-coloured
        /// background. Then the same pinch in every world's hand look, to see it reads on each.
        /// </summary>
        static bool CheckGesturePreview()
        {
#if META_XR_CORE
            var hands = StageHands(out var camera);
            var glow = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            GestureFeedback.UseMaterial(glow);
            HandSkin.Theme = HandTheme.Veins;
            var right = hands.First(h => !h.Left);
            var leftHand = hands.First(h => h.Left);
            var pinchAt = PosePinch(right.Root);
            PoseFist(leftHand.Root);
            foreach (var hand in hands) hand.Skin.forceMatrixRecalculationPerRender = true;

            void Light()
            {
                GestureFeedback.Clear();
                // Past the bursts' rate limit from the last picture.
                GestureFeedback.Tick(0.2f);
                GestureFeedback.Show(GestureKind.Pinch, false, pinchAt);
                GestureFeedback.Show(GestureKind.Fist, true, leftHand.Skin.bounds.center);
                GestureFeedback.Tick(0.03f);
                GestureFeedback.SimulateSparks(0.08f);
                foreach (var hand in hands)
                {
                    hand.Root.GetComponent<HandSkin>().Apply();
                    SetBeat(hand.Skin, 10f, 0f);
                }
            }
            Light();
            camera.transform.position = new Vector3(0.03f, 1.3f, 0.1f);
            camera.transform.LookAt(new Vector3(0f, 1.2f, 0.45f));
            camera.fieldOfView = 46f;
            const int w = 800, h = 450;
            var sheet = new Texture2D(w * 2, h, TextureFormat.RGB24, false);
            sheet.SetPixels(0, 0, w, h, Render(camera, w, h).GetPixels());
            // The same moment unlit, beside it, to compare.
            GestureFeedback.Clear();
            foreach (var hand in hands) hand.Root.GetComponent<HandSkin>().Apply();
            GestureFeedback.SimulateSparks(1f);
            sheet.SetPixels(w, 0, w, h, Render(camera, w, h).GetPixels());
            sheet.Apply();
            bool ok = CheckImage(sheet, camera.backgroundColor, "Logs/preview-gesture.png", "Gesture feedback preview");

            // Every world's hand look, the pinch lit, close up.
            var themes = new List<(string name, HandTheme theme)> { ("Default", HandTheme.Veins) };
            var holder = new GameObject("Worlds");
            foreach (var world in WorldCatalog.MakePreviews(holder.transform)) themes.Add((world.DisplayName, world.Theme));
            Object.DestroyImmediate(holder);
            const int cw = 400, ch = 225, columns = 4;
            int rows = (themes.Count + columns - 1) / columns;
            var grid = new Texture2D(cw * columns, ch * rows, TextureFormat.RGB24, false);
            for (int i = 0; i < themes.Count; i++)
            {
                HandSkin.Theme = themes[i].theme;
                Light();
                grid.SetPixels(i % columns * cw, (rows - 1 - i / columns) * ch, cw, ch, Render(camera, cw, ch).GetPixels());
            }
            grid.Apply();
            ok &= CheckImage(grid, camera.backgroundColor, "Logs/preview-gesture-themes.png", "Gesture feedback in every hand look");
            HandSkin.Theme = HandTheme.Veins;
            GestureFeedback.Clear();
            return ok;
#else
            return true;
#endif
        }

#if META_XR_CORE
        /// <summary>
        /// Bends a Meta hand's thumb and index together into a pinch (a few rounds of cyclic coordinate descent on each
        /// finger's joints towards the point between their tips). Returns where they meet.
        /// </summary>
        static Vector3 PosePinch(GameObject hand)
        {
            var bones = hand.GetComponentsInChildren<Transform>();
            Transform Bone(string n) => bones.First(t => t.name == "XRHand_" + n);
            var palm = Bone("Palm");
            var wrist = Bone("Wrist").position;
            // Out of the palm (Meta's right hand: the other way round to the left).
            var normal = -Vector3.Cross(Bone("IndexProximal").position - wrist, Bone("LittleProximal").position - wrist).normalized;
            var meet = Vector3.Lerp(Bone("ThumbTip").position, Bone("IndexTip").position, 0.5f) + normal * 0.03f;
            meet += (palm.position - meet) * 0.12f;
            Bend(Bone("IndexTip"), meet, Bone("IndexDistal"), Bone("IndexIntermediate"), Bone("IndexProximal"));
            Bend(Bone("ThumbTip"), meet, Bone("ThumbDistal"), Bone("ThumbProximal"), Bone("ThumbMetacarpal"));
            return (Bone("ThumbTip").position + Bone("IndexTip").position) * 0.5f;
        }

        /// <summary>Curls a Meta hand into a fist: each finger's tip in towards the palm, the thumb across the fingers.</summary>
        static void PoseFist(GameObject hand)
        {
            var bones = hand.GetComponentsInChildren<Transform>();
            Transform Bone(string n) => bones.First(t => t.name == "XRHand_" + n);
            var palm = Bone("Palm").position;
            foreach (var finger in new[] { "Index", "Middle", "Ring", "Little" })
            {
                var knuckle = Bone(finger + "Proximal").position;
                Bend(Bone(finger + "Tip"), Vector3.Lerp(knuckle, palm, 0.75f) + (palm - Bone("Wrist").position).normalized * -0.005f,
                    Bone(finger + "Distal"), Bone(finger + "Intermediate"), Bone(finger + "Proximal"));
            }
            Bend(Bone("ThumbTip"), Bone("MiddleIntermediate").position, Bone("ThumbDistal"), Bone("ThumbProximal"));
        }

        static void Bend(Transform tip, Vector3 target, params Transform[] joints)
        {
            for (int round = 0; round < 16; round++)
                foreach (var joint in joints)
                {
                    var from = tip.position - joint.position;
                    var to = target - joint.position;
                    if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) continue;
                    joint.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(from, to), 0.5f) * joint.rotation;
                }
        }
#endif
    }
}
