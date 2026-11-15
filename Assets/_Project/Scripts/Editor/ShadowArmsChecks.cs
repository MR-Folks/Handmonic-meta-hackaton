using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if META_XR_CORE
using MusicInYourHands.Meta;
#endif

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        // A made-up body for a player whose eyes are at (0, 1.6, 0), looking along +z, as the shots and checks stage it.
        const float PreviewShoulderWidth = 0.36f, PreviewUpperArm = 0.28f, PreviewForearm = 0.26f;
        static readonly Vector3 PreviewShoulder = new Vector3(PreviewShoulderWidth * 0.5f, 1.4f, -0.08f);
        static readonly Vector3 PreviewNeck = new Vector3(0f, 1.47f, -0.09f);
        static readonly Vector3 PreviewHeadJoint = new Vector3(0f, 1.55f, -0.07f);
        static readonly Vector3 PreviewChest = new Vector3(0f, 1.3f, -0.06f);

        /// <summary>
        /// Feeds BodyRig, as BodyFeed would, a body that fits the preview hands: shoulders below and behind the eyes, and
        /// each elbow where an upper arm and a forearm of the given lengths meet between the shoulder and the hand's wrist,
        /// bending out and down. The body's wrist sits a centimetre or two from the hand's, as Meta's does.
        /// </summary>
        static void PreviewBody(HandRig rig, float dt, float forearm = PreviewForearm, float upperArm = PreviewUpperArm, float confidence = 1f)
        {
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                var hand = rig.Get(left);
                var shoulder = PreviewShoulderAt(left);
                var wrist = hand.Mesh != null && hand.Mesh.rootBone != null ? hand.Mesh.rootBone.position : hand.Palm.position;
                var elbow = Elbow(shoulder, wrist, upperArm, forearm, new Vector3(left ? -0.6f : 0.6f, -1f, -0.3f));
                BodyRig.SetArm(left, true, shoulder, elbow, wrist + new Vector3(0.012f, -0.008f, -0.01f));
            }
            BodyRig.SetSpine(true, PreviewChest, PreviewNeck, PreviewHeadJoint);
            BodyRig.Push(confidence, dt);
        }

        static Vector3 PreviewShoulderAt(bool left) => new Vector3(left ? -PreviewShoulder.x : PreviewShoulder.x, PreviewShoulder.y, PreviewShoulder.z);

        /// <summary>
        /// Where the elbow is when an upper arm of length <paramref name="upper"/> from the shoulder and a forearm of length
        /// <paramref name="fore"/> from the wrist meet, bent towards <paramref name="pole"/>. A wrist out of reach
        /// straightens the arm.
        /// </summary>
        static Vector3 Elbow(Vector3 shoulder, Vector3 wrist, float upper, float fore, Vector3 pole)
        {
            var to = wrist - shoulder;
            float d = Mathf.Clamp(to.magnitude, Mathf.Abs(upper - fore) + 1e-4f, upper + fore - 1e-4f);
            var axis = to.normalized;
            float a = (upper * upper - fore * fore + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(upper * upper - a * a, 0f));
            return shoulder + axis * a + Vector3.ProjectOnPlane(pole, axis).normalized * h;
        }

        /// <summary>
        /// Shadow play's arms, driven with made-up body joints: with the elbows bent, each arm's shadow is a forearm from the
        /// hand's own wrist (inside the hand's shadow) to the elbow and an upper arm from that same elbow to the shoulder,
        /// tapered, with the shoulders and neck joining them; a straight arm casts a straight shadow; a fist moves neither
        /// the body's arm nor the hands-only estimate; without a body, or with one whose forearm is too long to believe,
        /// only the estimated forearm is left; the arms fade between the two with no jumps; entering the world wants the
        /// body and leaving it lets it go; and frames allocate nothing. Also: the main scene has the body feed, its
        /// OVRManager doesn't ask for body tracking at startup, and Meta's project config has body tracking Supported.
        /// </summary>
        static bool CheckShadowArms()
        {
#if META_XR_CORE
            var failures = new List<string>();
            // What was measured, for the log: elbow bends on the wall, the largest step of a fade, a fist's drift.
            var bends = new List<float>();
            float worstStep = 0f, fistDrift = 0f;
            void Expect(bool condition, string what)
            {
                if (!condition) failures.Add(what);
            }

            // The project: the feed in the main scene, no request at startup, body tracking Supported (not Required).
            EditorSceneManager.OpenScene(ProjectSetup.MainScenePath, OpenSceneMode.Single);
            Expect(Object.FindAnyObjectByType<BodyFeed>(FindObjectsInactive.Include) != null, "the main scene has no BodyFeed");
            foreach (var manager in Object.FindObjectsByType<OVRManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Expect(!new SerializedObject(manager).FindProperty("requestBodyTrackingPermissionOnStartup").boolValue,
                    "the main scene's OVRManager asks for body tracking at startup");
            Expect(OVRProjectConfig.CachedProjectConfig.bodyTrackingSupport == OVRProjectConfig.FeatureSupport.Supported,
                $"body tracking is {OVRProjectConfig.CachedProjectConfig.bodyTrackingSupport} in Meta's project config, not Supported");

            bool wantedBefore = BodyRig.Wanted;
            BodyRig.Wanted = false;
            BodyRig.Clear();
            var camera = StageRoom(6f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var hands = PlaceHands(camera, HandPose.BacksToFace, rig);
            var world = new GameObject("ShadowPlay").AddComponent<ShadowPlay>();
            ProjectSetup.AssignMaterials(world);
            world.Build();

            // Entering wants the body.
            RunMessage<World>(world, "OnEnable");
            Expect(BodyRig.Wanted, "entering Shadow play doesn't set BodyRig.Wanted");

            const float dt = 1f / 72f;
            var frame = Music(PreviewBands, 0.2f, false);
            // Per frame, the furthest any forearm's elbow end moved on the wall and the most any part's darkness changed.
            float maxStep = 0f, maxFade = 0f;
            var lastElbow = new Vector2[2];
            var lastStrength = new float[2];
            bool tracking = false;
            void Watch(bool start)
            {
                for (int s = 0; s < 2; s++)
                {
                    Capsule(world, ShadowPlay.ShadowPart.Forearm, s == 0, out _, out var b, out _, out _, out _);
                    float strength = Capsule(world, ShadowPlay.ShadowPart.UpperArm, s == 0, out _, out _, out _, out _, out var st) ? st : 0f;
                    if (!start)
                    {
                        maxStep = Mathf.Max(maxStep, (b - lastElbow[s]).magnitude);
                        maxFade = Mathf.Max(maxFade, Mathf.Abs(strength - lastStrength[s]));
                    }
                    lastElbow[s] = b;
                    lastStrength[s] = strength;
                }
            }
            void Run(float seconds, bool body, float forearm = PreviewForearm)
            {
                for (float t = 0f; t < seconds; t += dt)
                {
                    if (body) PreviewBody(rig, dt, forearm);
                    else BodyRig.Lose(dt);
                    world.Step(rig, frame, camera, dt);
                    if (tracking) Watch(false);
                }
            }
            // A fade watched from start to end: how far the elbow end went, and the biggest single step it took.
            float Fade(bool body, float forearm = PreviewForearm)
            {
                maxStep = maxFade = 0f;
                Watch(true);
                var from = (Vector2[])lastElbow.Clone();
                tracking = true;
                Run(1f, body, forearm);
                tracking = false;
                return Mathf.Max((lastElbow[0] - from[0]).magnitude, (lastElbow[1] - from[1]).magnitude);
            }

            // 1. Elbows bent: hands at chest height in front, elbows out and down.
            Run(1f, true);
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0;
                string side = left ? "left" : "right";
                var arm = BodyRig.Get(left);
                var wrist = rig.Get(left).Mesh.rootBone.position;
                world.PreviewProject(wrist, out var w);
                world.PreviewProject(arm.Elbow, out var e);
                world.PreviewProject(arm.Shoulder, out var sh);
                bool fore = Capsule(world, ShadowPlay.ShadowPart.Forearm, left, out var fa, out var fb, out var fra, out var frb, out var fs);
                bool upper = Capsule(world, ShadowPlay.ShadowPart.UpperArm, left, out var ua, out var ub, out var ura, out var urb, out var us);
                Expect(fore && upper, $"bent {side} arm: forearm {fore}, upper arm {upper}");
                if (!fore || !upper) continue;
                Expect((fa - w).magnitude < 0.002f, $"bent {side} arm: the forearm doesn't start at the hand's wrist ({(fa - w).magnitude:0.000} m off)");
                Expect(world.PreviewInHandShadow(s, fa), $"bent {side} arm: the forearm's wrist end isn't inside the hand's shadow");
                Expect((fb - e).magnitude < 0.002f && (ua - e).magnitude < 0.002f && (fb - ua).magnitude < 1e-4f,
                    $"bent {side} arm: forearm and upper arm don't meet at the elbow ({(fb - e).magnitude:0.000}, {(ua - e).magnitude:0.000} m off)");
                Expect((ub - sh).magnitude < 0.002f, $"bent {side} arm: the upper arm doesn't reach the shoulder");
                Expect(fs > 0.99f && us > 0.99f, $"bent {side} arm: not fully dark (forearm {fs:0.00}, upper arm {us:0.00})");
                Expect(fra < frb && frb < ura && ura < urb, $"bent {side} arm: not tapered wrist to shoulder ({fra:0.000} {frb:0.000} {ura:0.000} {urb:0.000} m)");
                float bend = Vector2.Angle(fa - fb, ub - ua);
                bends.Add(bend);
                Expect(bend > 15f, $"bent {side} arm: forearm and upper arm in line on the wall ({bend:0} degrees)");
                bool shoulder = Capsule(world, ShadowPlay.ShadowPart.Shoulder, left, out _, out var sb, out _, out _, out _);
                Expect(shoulder && (sb - sh).magnitude < 0.002f, $"bent {side} arm: no shoulder line out to the upper arm");
            }
            Expect(Capsule(world, ShadowPlay.ShadowPart.Neck, false, out _, out _, out _, out _, out _), "no neck with the body tracked");
            Expect(Capsule(world, ShadowPlay.ShadowPart.Head, false, out _, out _, out _, out _, out _), "no head");
            var bodyForearm = new Vector2[4];
            for (int s = 0; s < 2; s++) Capsule(world, ShadowPlay.ShadowPart.Forearm, s == 0, out bodyForearm[s * 2], out bodyForearm[s * 2 + 1], out _, out _, out _);

            // 2. The body lost: the arms fade to the hands-only forearms without a jump.
            float travelled = Fade(false);
            worstStep = Mathf.Max(worstStep, maxStep / Mathf.Max(travelled, 1e-4f));
            Expect(travelled > 0.05f && maxStep < travelled * 0.2f + 0.002f && maxFade < 0.12f,
                $"losing the body pops: elbow end moved {travelled:0.00} m, at most {maxStep:0.000} m in a frame; darkness changed at most {maxFade:0.00} a frame");
            var fallback = new Vector2[4];
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0;
                bool fore = Capsule(world, ShadowPlay.ShadowPart.Forearm, left, out fallback[s * 2], out fallback[s * 2 + 1], out _, out _, out var fs);
                world.PreviewProject(rig.Get(left).Mesh.rootBone.position, out var w);
                Expect(fore && fs > 0.99f && (fallback[s * 2] - w).magnitude < 0.002f && (fallback[s * 2 + 1] - fallback[s * 2]).magnitude > 0.1f,
                    $"without a body the {(left ? "left" : "right")} forearm isn't estimated from the hand");
                Expect(!Capsule(world, ShadowPlay.ShadowPart.UpperArm, left, out _, out _, out _, out _, out _) &&
                       !Capsule(world, ShadowPlay.ShadowPart.Shoulder, left, out _, out _, out _, out _, out _),
                    $"without a body the {(left ? "left" : "right")} upper arm or shoulder is still drawn");
            }
            Expect(!Capsule(world, ShadowPlay.ShadowPart.Neck, false, out _, out _, out _, out _, out _), "without a body the neck is still drawn");

            // 3. A fist moves neither the estimated forearm nor (4) the body's.
            foreach (var hand in hands)
            {
                CurlFingers(hand);
                PushHand(rig, hand);
            }
            Expect(rig.Left.Fist > 0.5f && rig.Right.Fist > 0.5f, $"the made-up fist isn't a fist (left {rig.Left.Fist:0.00}, right {rig.Right.Fist:0.00})");
            Run(0.2f, false);
            for (int s = 0; s < 2; s++)
            {
                Capsule(world, ShadowPlay.ShadowPart.Forearm, s == 0, out var fa, out var fb, out _, out _, out _);
                fistDrift = Mathf.Max(fistDrift, (fb - fallback[s * 2 + 1]).magnitude);
                Expect((fa - fallback[s * 2]).magnitude < 0.003f && (fb - fallback[s * 2 + 1]).magnitude < 0.003f,
                    $"a fist swings the estimated {(s == 0 ? "left" : "right")} forearm ({(fb - fallback[s * 2 + 1]).magnitude:0.000} m)");
            }
            travelled = Fade(true);
            worstStep = Mathf.Max(worstStep, maxStep / Mathf.Max(travelled, 1e-4f));
            Expect(travelled > 0.05f && maxStep < travelled * 0.2f + 0.002f && maxFade < 0.12f,
                $"finding the body pops: elbow end moved {travelled:0.00} m, at most {maxStep:0.000} m in a frame; darkness changed at most {maxFade:0.00} a frame");
            for (int s = 0; s < 2; s++)
            {
                Capsule(world, ShadowPlay.ShadowPart.Forearm, s == 0, out var fa, out var fb, out _, out _, out _);
                fistDrift = Mathf.Max(fistDrift, (fb - bodyForearm[s * 2 + 1]).magnitude);
                Expect((fa - bodyForearm[s * 2]).magnitude < 0.003f && (fb - bodyForearm[s * 2 + 1]).magnitude < 0.003f,
                    $"a fist swings the {(s == 0 ? "left" : "right")} forearm with the body tracked ({(fb - bodyForearm[s * 2 + 1]).magnitude:0.000} m)");
            }

            // 5. A body whose forearm is too long to believe (45 cm) isn't drawn: back to the estimate, smoothly.
            travelled = Fade(true, 0.45f);
            Expect(maxStep < travelled * 0.2f + 0.002f, $"an unbelievable body pops: at most {maxStep:0.000} m in a frame of {travelled:0.00} m");
            for (int s = 0; s < 2; s++)
            {
                Capsule(world, ShadowPlay.ShadowPart.Forearm, s == 0, out _, out var fb, out _, out _, out _);
                Expect((fb - fallback[s * 2 + 1]).magnitude < 0.003f && !Capsule(world, ShadowPlay.ShadowPart.UpperArm, s == 0, out _, out _, out _, out _, out _),
                    $"a 45 cm {(s == 0 ? "left" : "right")} forearm from the body is drawn instead of the estimate");
            }

            // 6. Arms straight out in front: one straight shadow from wrist to shoulder.
            foreach (var hand in hands)
            {
                var wrist = hand.Skin.rootBone.position;
                var shoulder = PreviewShoulderAt(hand.Left);
                var reach = shoulder + (new Vector3(hand.Left ? -0.05f : 0.05f, -0.15f, 1f).normalized * (PreviewUpperArm + PreviewForearm));
                hand.Root.transform.position += reach - wrist;
                PushHand(rig, hand);
            }
            Run(1f, true);
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0;
                bool fore = Capsule(world, ShadowPlay.ShadowPart.Forearm, left, out var fa, out var fb, out _, out _, out _);
                bool upper = Capsule(world, ShadowPlay.ShadowPart.UpperArm, left, out var ua, out var ub, out _, out _, out _);
                float bend = fore && upper ? Vector2.Angle(fa - fb, ub - ua) : 0f;
                Expect(fore && upper && (fb - ua).magnitude < 1e-4f && bend > 165f,
                    $"straight {(left ? "left" : "right")} arm: forearm {fore}, upper arm {upper}, {180f - bend:0} degrees from straight on the wall");
            }

            // 7. Frames allocate nothing, with the body and without. The menu is open so no light rings (its sound
            // is made once per pitch), but the shadow still covers the lights.
            var menu = MakeMenu(rig, camera, world);
            menu.Open(camera);
            Run(0.2f, true);
            int probe = AllocationsIn(() => allocationProbe = new byte[2048]);
            int withBody = AllocationsIn(() => Run(1.5f, true));
            Run(0.5f, false);
            int without = AllocationsIn(() => Run(1.5f, false));
            allocationProbe = null;
            RunMessage(menu, "OnDisable");
            Expect(probe > 0 && withBody == 0 && without == 0,
                $"frames allocate: {withBody} times in 1.5 s with the body, {without} without (probe {probe})");

            // Leaving lets the body go.
            RunMessage<World>(world, "OnDisable");
            Expect(!BodyRig.Wanted, "leaving Shadow play doesn't clear BodyRig.Wanted");

            BodyRig.Clear();
            BodyRig.Wanted = wantedBefore;
            if (failures.Count == 0)
                Debug.Log("[Check] Shadow play arms: with the body, a tapered forearm from the hand's wrist to the elbow and an upper arm on " +
                          "to the shoulder, joined by the shoulders and neck; a straight arm casts a straight shadow; a fist swings no " +
                          "arm; without a believable body only the estimated forearm stays; the fades don't pop; leaving lets the body " +
                          "go; no allocations. The main scene has the body feed and asks for nothing at startup. " +
                          $"(Elbows bent {string.Join(" and ", bends.Select(b => $"{b:0}"))} degrees on the wall; a fade's largest " +
                          $"frame step {worstStep:P0} of its way; a fist moved an elbow end {fistDrift * 1000f:0.0} mm.)");
            else Debug.LogError("[Check] Shadow play arms: " + string.Join("; ", failures) + ".");
            return failures.Count == 0;
#else
            return true;
#endif
        }

#if META_XR_CORE
        /// <summary>The first capsule of this part (and side, for arms and shoulders) in Shadow play's shadow this frame.</summary>
        static bool Capsule(ShadowPlay world, ShadowPlay.ShadowPart part, bool left, out Vector2 a, out Vector2 b, out float radiusA,
            out float radiusB, out float strength)
        {
            bool sided = part == ShadowPlay.ShadowPart.Forearm || part == ShadowPlay.ShadowPart.UpperArm || part == ShadowPlay.ShadowPart.Shoulder;
            for (int i = 0; i < world.PreviewCapsuleCount; i++)
            {
                world.PreviewCapsule(i, out var p, out bool l, out a, out b, out radiusA, out radiusB, out strength);
                if (p == part && (!sided || l == left)) return true;
            }
            a = b = default;
            radiusA = radiusB = strength = 0f;
            return false;
        }

        /// <summary>
        /// Curls a preview hand's fingers into a loose fist: each finger joint turns towards the palm, the thumb a little,
        /// while the wrist and palm stay where they are.
        /// </summary>
        static void CurlFingers(PreviewHand hand)
        {
            var bones = hand.Root.GetComponentsInChildren<Transform>();
            Transform Bone(string n) => bones.First(t => t.name == n);
            var palm = Bone("XRHand_Palm");
            var wrist = Bone("XRHand_Wrist").position;
            var normal = PalmNormal(Bone, hand.Left);
            var along = (Bone("XRHand_MiddleProximal").position - wrist).normalized;
            var across = Vector3.Cross(along, normal).normalized;
            var inside = palm.position + normal * 0.03f;
            void Turn(string joint, string tip, float angle)
            {
                var j = Bone(joint);
                var t = Bone(tip);
                // Whichever way brings the fingertip nearer the inside of the palm.
                float before = (t.position - inside).sqrMagnitude;
                j.RotateAround(j.position, across, angle);
                if ((t.position - inside).sqrMagnitude > before) j.RotateAround(j.position, across, -2f * angle);
            }
            foreach (var finger in new[] { "Index", "Middle", "Ring", "Little" })
            {
                Turn($"XRHand_{finger}Proximal", $"XRHand_{finger}Tip", 80f);
                Turn($"XRHand_{finger}Intermediate", $"XRHand_{finger}Tip", 95f);
                Turn($"XRHand_{finger}Distal", $"XRHand_{finger}Tip", 60f);
            }
            Turn("XRHand_ThumbProximal", "XRHand_ThumbTip", 30f);
            Turn("XRHand_ThumbDistal", "XRHand_ThumbTip", 40f);
        }
#endif
    }
}
