using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// What the onboarding draws: the ghost hands beside yours, the caption on its dark plate, the Skip target, the
    /// glow on your active fingertips, and the burst of sparks and chime when a lesson is done.
    /// </summary>
    public partial class Onboarding
    {
        /// <summary>What one frame wants to show.</summary>
        struct Display
        {
            public GhostMotion Motion;
            public bool Left;
            public Lesson Lesson;
            public string Caption;
            /// <summary>Which side the caption sits on: -1 left, 1 right, 0 in the middle (away from the ghost hand).</summary>
            public float CaptionSide;
            /// <summary>The caption floats a little above eye height instead of below it (over the cassette deck).</summary>
            public bool CaptionHigh;
            /// <summary>Words for the open menu's own caption line, in place of a caption of ours (lesson 2).</summary>
            public string Prompt;
            public bool Skip;
            /// <summary>Which of your own fingertips glow, on the ghost's side: bit 0 thumb, 1 index.</summary>
            public int Lights;
        }

        [Header("Look")]
        [Tooltip("Where a ghost hand sits when your hand isn't in view: to the side (m, mirrored), below your eyes and ahead.")]
        [SerializeField] Vector3 ghostHome = new Vector3(0.12f, -0.18f, 0.42f);
        [Tooltip("Where the ghost hand sits for 'Touch a world': nearer you than the menu, low on the right.")]
        [SerializeField] Vector3 pokeHome = new Vector3(0.1f, -0.17f, 0.27f);
        [Tooltip("How far ahead the caption floats (m).")]
        [SerializeField] float captionDistance = 0.62f;
        [Tooltip("Where the caption floats: degrees to the side, away from the ghost hand, and degrees below straight ahead. " +
                 "Low enough to keep clear of what a world floats at eye height (Flower dance's sung words), above the ghost.")]
        [SerializeField] Vector2 captionPlace = new Vector2(7f, 15f);
        [Tooltip("The caption's letter height (m): about 1.8 degrees at its distance.")]
        [SerializeField] float captionHeight = 0.019f;
        [Tooltip("Where the Skip target sits: degrees to the right, degrees down, and distance (m).")]
        [SerializeField] Vector3 skipPlace = new Vector3(20f, 22f, 0.5f);
        [Tooltip("The Skip target's radius (m).")]
        [SerializeField] float skipRadius = 0.026f;
        [SerializeField, Range(0f, 1f)] float volume = 0.35f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int InnerGlowId = Shader.PropertyToID("_InnerGlow");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly Color Warm = new Color(1f, 0.85f, 0.5f);
        static readonly Color Pale = new Color(0.86f, 0.92f, 1f);

        GhostHand ghostL, ghostR;
        Transform root, captionRoot, skipRoot;
        TypeLabel captionLabel, skipLabel;
        Renderer plate, skipRing, skipDot, burstRing;
        readonly Renderer[] lights = new Renderer[2];
        ParticleSystem sparks;
        AudioPool voices;
        MaterialPropertyBlock block;
        bool built;

        // The ghost on show, how visible, and how far into its loop.
        GhostMotion shownMotion;
        bool shownLeft;
        Lesson shownLesson;
        float ghostAlpha, motionClock;
        // Where the ghosts stand (left, right, and the middle for two hands), in your level frame.
        readonly Vector3[] anchors = new Vector3[3];
        bool anchored;
        Quaternion frame = Quaternion.identity;
        bool framed;
        // The caption.
        string shownCaption;
        bool captionPlaced, shownHigh;
        float captionAlpha, shownSide;
        // The Skip target.
        float skipAlpha, skipAway, skipHover, skipPop;
        bool skipPlaced, skipFollowing;
        readonly bool[] skipArmed = new bool[2];
        readonly float[] skipDepth = { 1f, 1f };
        // The menu, as lesson 2 aims at it: the world bubble chosen when it opened, and where it opened, for when there is
        // no menu to ask; and the menu whose caption and bubble lesson 2 has borrowed.
        WorldMenu menuNow, invited;
        int menuTarget = -1;
        Vector3 menuPlacedAt;
        // The last burst.
        Vector3 burstAt;
        float burstAge = 99f;
        Color burstColor;

        // ---- What the editor checks and screenshots read ----

        /// <summary>The ghost motion on show (more than half faded in), or None.</summary>
        public GhostMotion ShowingMotion => ghostAlpha > 0.5f ? shownMotion : GhostMotion.None;

        /// <summary>The lesson whose ghost is on show (more than half faded in), or None.</summary>
        public Lesson ShowingLesson => ghostAlpha > 0.5f ? shownLesson : Lesson.None;

        /// <summary>How far the ghost on show has faded in, 0-1.</summary>
        public float GhostAlpha => ghostAlpha;

        /// <summary>Whether the one-handed ghost on show is the left hand.</summary>
        public bool ShowingLeft => shownLeft;

        /// <summary>The caption on show (more than half faded in), or an empty string.</summary>
        public string Caption => captionAlpha > 0.5f && shownCaption != null ? shownCaption : "";

        public bool SkipShowing => skipAlpha > 0.5f;
        public Vector3 SkipPosition => skipRoot != null ? skipRoot.position : Vector3.zero;
        public GhostHand LeftGhost => ghostL;
        public GhostHand RightGhost => ghostR;

        /// <summary>Seconds into the ghost's loop (the screenshots set it to catch a gesture's key moment).</summary>
        public float MotionTime
        {
            get => motionClock;
            set => motionClock = value;
        }

        void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            var ghostMaterial = Load("OnboardingGhost", "MusicInYourHands/OnboardingGhost");
            var glowMaterial = Load("OnboardingGlow", "MusicInYourHands/Glow");
            var ringMaterial = Load("OnboardingRing", "MusicInYourHands/Ring");
            var plateMaterial = Load("OnboardingPlate", "MusicInYourHands/Panel");

            root = new GameObject("Onboarding Look").transform;
            root.SetParent(transform, false);
            ghostL = new GhostHand(root, true, ghostMaterial);
            ghostR = new GhostHand(root, false, ghostMaterial);
            ghostL.SetVisible(false);
            ghostR.SetVisible(false);

            captionRoot = new GameObject("Caption").transform;
            captionRoot.SetParent(root, false);
            plate = Effects.MakeQuad("Caption Plate", captionRoot, plateMaterial);
            captionLabel = new TypeLabel("Caption Text", captionRoot, captionHeight);
            captionLabel.Transform.localPosition = new Vector3(0f, 0f, -0.001f);
            captionRoot.gameObject.SetActive(false);

            skipRoot = new GameObject("Skip").transform;
            skipRoot.SetParent(root, false);
            skipRing = Effects.MakeQuad("Skip Ring", skipRoot, ringMaterial);
            skipLabel = new TypeLabel("Skip Text", skipRoot, 0.012f);
            skipLabel.Text = "Skip";
            skipLabel.Transform.localPosition = new Vector3(0f, 0f, -0.001f);
            skipRoot.gameObject.SetActive(false);
            skipDot = Effects.MakeQuad("Skip Fingertip", root, glowMaterial);
            skipDot.gameObject.SetActive(false);

            for (int i = 0; i < lights.Length; i++)
            {
                lights[i] = Effects.MakeQuad($"Fingertip Ring {i}", root, ringMaterial);
                lights[i].gameObject.SetActive(false);
            }
            sparks = Effects.MakeSparkSystem("Onboarding Sparks", root, glowMaterial);
            var main = sparks.main;
            main.maxParticles = 160;
            main.gravityModifier = 0.02f;
            burstRing = Effects.MakeQuad("Burst Ring", root, ringMaterial);
            burstRing.gameObject.SetActive(false);
            voices = new AudioPool(root, 3);
        }

        static Material Load(string name, string shader)
        {
            var material = Resources.Load<Material>("Onboarding/" + name);
            if (material != null) return material;
            var found = Shader.Find(shader);
            return found != null ? new Material(found) { name = name } : null;
        }

        void Animate(HandRig rig, Camera view, bool menuOpen, in Display want, float dt)
        {
            if (view == null) return;
            var head = view.transform.position;
            var level = Quaternion.LookRotation(Effects.FlatForward(view), Vector3.up);
            frame = framed ? Quaternion.Slerp(frame, level, 1f - Mathf.Exp(-2.5f * dt)) : level;
            framed = true;

            StepInvite(menuOpen, want);
            StepGhosts(rig, view, head, menuOpen, want, dt);
            StepCaption(view, head, want, dt);
            StepSkip(rig, view, head, want, dt);
            StepLights(rig, view, want);
            StepBurst(view, dt);
        }

        // ---- Ghost hands ----

        void StepGhosts(HandRig rig, Camera view, Vector3 head, bool menuOpen, in Display want, float dt)
        {
            bool two = GhostMotions.TwoHanded(want.Motion);
            bool same = want.Motion == shownMotion && (two || want.Left == shownLeft);
            if (!same)
            {
                // Fade out what is there, then change: never two gestures at once. Quicker when the menu opens.
                ghostAlpha = Mathf.MoveTowards(ghostAlpha, 0f, dt / (menuOpen ? 0.2f : 0.3f));
                if (ghostAlpha <= 0f)
                {
                    shownMotion = want.Motion;
                    shownLeft = want.Left;
                    shownLesson = want.Lesson;
                    motionClock = 0f;
                    anchored = false;
                }
            }
            else
            {
                shownLesson = want.Lesson;
                bool on = want.Motion != GhostMotion.None;
                ghostAlpha = Mathf.MoveTowards(ghostAlpha, on ? 1f : 0f, dt / (on ? 0.45f : menuOpen ? 0.2f : 0.6f));
            }

            bool visible = shownMotion != GhostMotion.None && ghostAlpha > 0.001f;
            if (!visible)
            {
                ghostL.SetVisible(false);
                ghostR.SetVisible(false);
                return;
            }
            motionClock += dt;
            two = GhostMotions.TwoHanded(shownMotion);
            PlaceAnchors(rig, view, head, two, dt);
            int active = GhostMotions.ActiveFingers(shownMotion);
            // Lessons light their fingers a little all the time; demos only at the key moment.
            float baseGlow = shownLesson != Lesson.None ? 0.45f : 0.2f;
            // Fade in like a breath rather than a switch.
            float alpha = ghostAlpha * ghostAlpha * (3f - 2f * ghostAlpha);
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                var ghost = left ? ghostL : ghostR;
                bool use = two || left == shownLeft;
                ghost.SetVisible(use);
                if (!use || !ghost.IsValid) continue;

                GhostMotions.Evaluate(shownMotion, left, motionClock, out var pose);
                var at = (two ? anchors[2] : anchors[side]) + frame * pose.Offset;
                var fingers = frame * pose.Fingers;
                var palm = frame * pose.Palm;
                if (pose.FaceView > 0f)
                {
                    var to = head - at;
                    if (to.sqrMagnitude > 1e-4f) palm = Vector3.Slerp(palm, to.normalized, pose.FaceView);
                }
                var toAim = pose.AimMenu > 0f ? MenuAim() - at : Vector3.zero;
                if (toAim.sqrMagnitude > 1e-4f)
                {
                    // Pointing at the menu: the hand turns so the index fingertip itself lines up with the bubble, and the
                    // poke ends with that fingertip just short of it, however far away the menu is.
                    fingers = Vector3.Slerp(fingers, toAim.normalized, pose.AimMenu);
                    ghost.Pose(at, fingers, palm, pose.Curl, pose.Spread, pose.ThumbFrom, pose.ThumbTo, pose.ThumbBlend);
                    var tip = ghost.Tip(1) - at;
                    var turn = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(tip, toAim), pose.AimMenu);
                    fingers = turn * fingers;
                    palm = turn * palm;
                    float room = Mathf.Clamp(toAim.magnitude - tip.magnitude - 0.012f, 0.03f, 0.2f);
                    at += toAim.normalized * (pose.Reach / GhostMotions.FullPoke * room);
                }
                else at += fingers * pose.Reach;
                ghost.Pose(at, fingers, palm, pose.Curl, pose.Spread, pose.ThumbFrom, pose.ThumbTo, pose.ThumbBlend);

                float glow = baseGlow + (1f - baseGlow) * pose.Glow;
                Vector4 focusA = default, focusB = default;
                int found = 0;
                for (int f = 0; f < 5 && found < 2; f++)
                {
                    if ((active >> f & 1) == 0) continue;
                    // The thumb gestures happen on the side of the index, not at its tip.
                    var tip = f == 1 && shownMotion == GhostMotion.ThumbTapSwipe ? ghost.IndexSide : ghost.Tip(f);
                    var focus = new Vector4(tip.x, tip.y, tip.z, glow);
                    if (found++ == 0) focusA = focus;
                    else focusB = focus;
                }
                ghost.SetLook(alpha, focusA, focusB);
            }
        }

        /// <summary>
        /// Where each ghost stands: beside your matching hand, a little inside, above and ahead of it so it never covers
        /// it, kept within comfortable view (Meta's 58 x 54 degree area); at a resting place in view when that hand isn't
        /// tracked. Two-handed demos stand together above the middle of your hands. They glide after your hands slowly,
        /// so they don't shake with them.
        /// </summary>
        void PlaceAnchors(HandRig rig, Camera view, Vector3 head, bool two, float dt)
        {
            bool poke = shownMotion == GhostMotion.TouchMenu;
            float follow = anchored ? 1f - Mathf.Exp(-2.2f * dt) : 1f;
            // Within Meta's comfortable area around where you look: up to 26 degrees below it (and never above the eyes).
            float looking = Mathf.Asin(Mathf.Clamp(view.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            float low = Mathf.Max(looking - 23f, -50f), high = Mathf.Max(Mathf.Min(looking + 6f, -6f), low + 4f);
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                var home = poke ? pokeHome : ghostHome;
                var target = head + frame * new Vector3(home.x * s, home.y, home.z);
                var hand = rig != null ? rig.Get(side == 0) : null;
                if (hand != null && hand.IsTracked)
                {
                    var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                    var beside = palm + frame * new Vector3(-0.09f * s, 0.07f, poke ? 0.02f : 0.08f);
                    target = poke ? Clamp(beside, head, s * 3f, s * 24f, low - 12f, high - 10f, 0.24f, 0.34f)
                        : Clamp(beside, head, s * 2f, s * 24f, low, high, 0.34f, 0.48f);
                }
                anchors[side] = Vector3.Lerp(anchors[side], target, follow);
            }
            if (two)
            {
                var target = head + frame * new Vector3(0f, ghostHome.y + 0.02f, ghostHome.z + 0.02f);
                var l = rig != null ? rig.Left : null;
                var r = rig != null ? rig.Right : null;
                if (l != null && r != null && l.IsTracked && r.IsTracked)
                {
                    var middle = (l.Palm.position + r.Palm.position) * 0.5f + frame * new Vector3(0f, 0.09f, 0.08f);
                    target = Clamp(middle, head, -10f, 10f, low, high, 0.36f, 0.5f);
                }
                anchors[2] = Vector3.Lerp(anchors[2], target, follow);
            }
            anchored = true;
        }

        /// <summary>A point brought within the given angles (degrees right and up) and distance of the head, in your level frame.</summary>
        Vector3 Clamp(Vector3 point, Vector3 head, float yawA, float yawB, float pitchMin, float pitchMax, float near, float far)
        {
            var local = Quaternion.Inverse(frame) * (point - head);
            float distance = Mathf.Clamp(local.magnitude, near, far);
            float yaw = Mathf.Atan2(local.x, Mathf.Max(local.z, 0.05f)) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            yaw = Mathf.Clamp(yaw, Mathf.Min(yawA, yawB), Mathf.Max(yawA, yawB));
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
            var direction = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
            return head + frame * (direction * distance);
        }

        // ---- The menu, for lesson 2 ----

        void RememberMenu(Camera view)
        {
            // Where the menu opens (half a metre along your gaze, a little lower, as WorldMenu places it), for when there
            // is no menu to ask; and a bubble to aim at is chosen afresh.
            var direction = Quaternion.AngleAxis(1f, view.transform.right) * view.transform.forward;
            menuPlacedAt = view.transform.position + direction * 0.5f;
            menuTarget = -1;
        }

        /// <summary>
        /// Where lesson 2's ghost finger points and pokes: a real world bubble on the open menu (WorldMenu.BubblePosition),
        /// chosen as the menu opens: of the worlds the player hasn't tried yet (or else any but the current one), the one
        /// nearest the middle of the honeycomb. Without a menu to ask, where the menu opened.
        /// </summary>
        Vector3 MenuAim()
        {
            if (menuNow != null)
            {
                if (menuTarget < 0) menuTarget = ChooseTarget(menuNow);
                if (menuTarget >= 0 && menuNow.BubblePosition(menuTarget, out var at)) return at;
            }
            return menuPlacedAt;
        }

        int ChooseTarget(WorldMenu menu)
        {
            if (manager == null) return -1;
            int count = manager.Worlds.Count;
            var middle = Vector3.zero;
            int shown = 0;
            for (int i = 0; i < count; i++)
            {
                if (!menu.BubblePosition(i, out var at)) continue;
                middle += at;
                shown++;
            }
            if (shown == 0) return -1;
            middle /= shown;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (i == manager.Current || !menu.BubblePosition(i, out var at)) continue;
                float score = Vector3.Distance(at, middle) + (Tried(i) ? 1f : 0f);
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            return best;
        }

        /// <summary>
        /// While lesson 2 shows, the open menu's own caption line says what to do (instead of a caption of ours over its
        /// bubbles) and the bubble the ghost points at gently swells, until the player points at a bubble themselves.
        /// </summary>
        void StepInvite(bool menuOpen, in Display want)
        {
            var menu = menuOpen && want.Prompt != null ? menuNow : null;
            if (invited != null && invited != menu) EndInvite();
            if (menu == null) return;
            if (menuTarget < 0) menuTarget = ChooseTarget(menu);
            menu.Prompt = want.Prompt;
            menu.Beckoned = menuTarget;
            invited = menu;
        }

        void EndInvite()
        {
            if (invited == null) return;
            invited.Prompt = null;
            invited.Beckoned = -1;
            invited = null;
        }

        // ---- Caption ----

        /// <summary>
        /// The one-line caption on a dark rounded plate for contrast over any room: a little below and to the side of the
        /// middle of your view, beside the ghost hand rather than over it, following your view loosely. It stands farther
        /// than half a metre away. (With the menu open, lesson 2 uses the menu's own caption line instead.)
        /// </summary>
        void StepCaption(Camera view, Vector3 head, in Display want, float dt)
        {
            var text = want.Caption;
            if (text != shownCaption || want.CaptionSide != shownSide || want.CaptionHigh != shownHigh)
            {
                captionAlpha = Mathf.MoveTowards(captionAlpha, 0f, dt / 0.25f);
                if (captionAlpha <= 0f)
                {
                    shownCaption = text;
                    shownSide = want.CaptionSide;
                    shownHigh = want.CaptionHigh;
                    captionPlaced = false;
                    if (text != null)
                    {
                        captionLabel.Text = text;
                        float width = TypeFace.Bold.Width(text) * captionHeight + captionHeight * 1.8f;
                        float height = captionHeight * 2.3f;
                        plate.transform.localScale = new Vector3(width, height, 1f);
                        block.Clear();
                        block.SetVector(SizeId, new Vector4(width, height, 0f, 0f));
                        plate.SetPropertyBlock(block);
                    }
                }
            }
            else captionAlpha = Mathf.MoveTowards(captionAlpha, text != null ? 1f : 0f, dt / (text != null ? 0.35f : 0.5f));

            bool show = shownCaption != null && captionAlpha > 0.001f;
            if (captionRoot.gameObject.activeSelf != show) captionRoot.gameObject.SetActive(show);
            if (!show) return;

            // Over the cassette deck the caption floats a little above eye height, clear of the deck below.
            float pitch = shownHigh ? -8f : captionPlace.y;
            var target = head + frame * (Quaternion.Euler(pitch, captionPlace.x * shownSide, 0f) * Vector3.forward * captionDistance);
            var position = captionPlaced ? Vector3.Lerp(captionRoot.position, target, 1f - Mathf.Exp(-3f * dt)) : target;
            captionRoot.SetPositionAndRotation(position, Quaternion.LookRotation(position - head, Vector3.up));
            captionPlaced = true;

            float a = captionAlpha * captionAlpha * (3f - 2f * captionAlpha);
            captionLabel.Color = new Color(1f, 1f, 1f, a);
            plate.GetPropertyBlock(block);
            block.SetFloat(AlphaId, a);
            plate.SetPropertyBlock(block);
        }

        // ---- Skip ----

        /// <summary>
        /// The Skip target: a ring with "Skip" in it, low on your right within reach, at half a metre. It stays put while
        /// you look around a little and comes back in front of you if you look well away (never away from a fingertip
        /// reaching for it). A fingertip coming near brightens it and shows a dot that shrinks as it nears; touching it
        /// skips the lessons. A finger already on it when it appears must come out first.
        /// </summary>
        void StepSkip(HandRig rig, Camera view, Vector3 head, in Display want, float dt)
        {
            bool wanted = want.Skip;
            if (wanted && skipAlpha <= 0f)
            {
                skipPlaced = skipFollowing = false;
                skipArmed[0] = skipArmed[1] = false;
                skipDepth[0] = skipDepth[1] = 1f;
            }
            skipAlpha = Mathf.MoveTowards(skipAlpha, wanted ? 1f : 0f, dt / (wanted ? 0.4f : 0.25f));
            skipPop = Mathf.MoveTowards(skipPop, 0f, dt * 3f);
            bool show = skipAlpha > 0.001f;
            if (skipRoot.gameObject.activeSelf != show) skipRoot.gameObject.SetActive(show);
            if (!show)
            {
                if (skipDot.gameObject.activeSelf) skipDot.gameObject.SetActive(false);
                return;
            }

            var target = head + frame * (Quaternion.Euler(skipPlace.y, skipPlace.x, 0f) * Vector3.forward * skipPlace.z);
            if (!skipPlaced) skipRoot.position = target;
            skipPlaced = true;

            // Fingertips: hover, and a press as one reaches the target from the front.
            var normal = (head - skipRoot.position).normalized;
            float hover = 0f, nearest = 99f;
            Vector3 dotAt = default;
            bool pressed = false;
            for (int s = 0; s < 2; s++)
            {
                var hand = rig != null ? rig.Get(s == 0) : null;
                if (hand == null || !hand.IsTracked || hand.InSystemGesture)
                {
                    skipArmed[s] = false;
                    skipDepth[s] = 1f;
                    continue;
                }
                var tip = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
                var offset = tip - skipRoot.position;
                float depth = Vector3.Dot(offset, normal);
                float lateral = (offset - normal * depth).magnitude;
                nearest = Mathf.Min(nearest, offset.magnitude);
                if (lateral < skipRadius * 2f && depth < 0.12f && depth > -0.04f)
                {
                    float h = 1f - Mathf.Clamp01(depth / 0.12f);
                    if (h > hover)
                    {
                        hover = h;
                        dotAt = tip - normal * depth;
                    }
                }
                if (skipAlpha > 0.6f && wanted && skipArmed[s] && lateral < skipRadius * 1.25f && depth <= 0.006f && skipDepth[s] > 0.006f)
                    pressed = true;
                if (depth > 0.05f || lateral > skipRadius * 3f) skipArmed[s] = true;
                skipDepth[s] = depth;
            }

            // Look well away for a moment and it glides back in front of you, unless a fingertip is reaching for it.
            float angle = Vector3.Angle(view.transform.forward, skipRoot.position - head);
            skipAway = angle > 40f ? skipAway + dt : 0f;
            if (skipAway > 0.6f) skipFollowing = true;
            if (nearest < 0.15f) skipFollowing = false;
            if (skipFollowing)
            {
                skipRoot.position = Vector3.Lerp(skipRoot.position, target, 1f - Mathf.Exp(-4f * dt));
                if ((skipRoot.position - target).sqrMagnitude < 0.0001f) skipFollowing = false;
            }
            skipRoot.rotation = Quaternion.LookRotation(skipRoot.position - head, Vector3.up);

            skipHover = Mathf.MoveTowards(skipHover, hover, dt * 6f);
            float a = skipAlpha * skipAlpha * (3f - 2f * skipAlpha);
            skipRing.transform.localScale = Vector3.one * (skipRadius * 2f * (1f + skipHover * 0.12f + skipPop * 0.3f));
            block.Clear();
            block.SetColor(ColorId, Color.Lerp(Pale, Warm, skipHover * 0.6f + skipPop));
            block.SetFloat(RadiusId, 0.86f);
            block.SetFloat(ThicknessId, 0.07f);
            block.SetFloat(InnerGlowId, 0.22f + skipHover * 0.35f);
            block.SetFloat(AlphaId, a * (0.75f + skipHover * 0.25f));
            skipRing.SetPropertyBlock(block);
            skipLabel.Color = new Color(1f, 1f, 1f, a * 0.95f);

            bool dot = hover > 0.01f && a > 0.1f;
            if (skipDot.gameObject.activeSelf != dot) skipDot.gameObject.SetActive(dot);
            if (dot)
            {
                skipDot.transform.position = dotAt + normal * 0.002f;
                skipDot.transform.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.012f, hover);
                Effects.Face(skipDot.transform, view);
                block.Clear();
                block.SetColor(ColorId, Color.white);
                block.SetFloat(IntensityId, a * Mathf.Lerp(0.5f, 1.4f, hover));
                skipDot.SetPropertyBlock(block);
            }
            if (pressed) Skip();
        }

        /// <summary>The Skip target was touched: it pops with a soft pop and a settling note.</summary>
        void Skipped()
        {
            skipPop = 1f;
            var at = skipRoot != null ? skipRoot.position : transform.position;
            Burst(at, Pale);
            if (voices == null) return;
            voices.Play(Synth.Pop(), at, volume);
            int midi = Musical.Pitch(music, 64);
            voices.Play(Synth.Chime(Hz(midi)), at, volume * 0.8f);
        }

        // ---- Your fingertips ----

        /// <summary>While a lesson shows, the fingertips it uses on your own hand get a warm ring that pulses gently.</summary>
        void StepLights(HandRig rig, Camera view, in Display want)
        {
            var hand = rig != null && want.Lesson != Lesson.None ? rig.Get(want.Left) : null;
            bool lit = hand != null && hand.IsTracked && shownLesson == want.Lesson && ghostAlpha > 0.01f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(clock * 4.2f);
            int finger = 0;
            for (int i = 0; i < lights.Length; i++)
            {
                while (finger < 5 && (want.Lights >> finger & 1) == 0) finger++;
                bool on = lit && finger < 5;
                var light = lights[i];
                if (light.gameObject.activeSelf != on) light.gameObject.SetActive(on);
                if (!on) continue;
                var tip = hand.TipJoints[finger] != null ? hand.TipJoints[finger].position : hand.Tips[finger];
                // Just in front of the fingertip, so the hand itself doesn't hide it.
                var toEyes = view.transform.position - tip;
                light.transform.position = tip + (toEyes.sqrMagnitude > 1e-6f ? toEyes.normalized * 0.012f : Vector3.zero);
                light.transform.localScale = Vector3.one * (0.034f * (1f + 0.15f * pulse));
                Effects.Face(light.transform, view);
                block.Clear();
                block.SetColor(ColorId, Warm);
                block.SetFloat(RadiusId, 0.78f);
                block.SetFloat(ThicknessId, 0.13f);
                block.SetFloat(InnerGlowId, 0.3f);
                block.SetFloat(AlphaId, ghostAlpha * (0.75f + 0.25f * pulse));
                light.SetPropertyBlock(block);
                finger++;
            }
        }

        // ---- Success ----

        /// <summary>
        /// A lesson was done: sparks burst from the fingers that did it, a ring of light spreads, and a soft two-note
        /// chime in the song's key rings out (higher for each lesson).
        /// </summary>
        void Celebrate(Lesson which, bool left)
        {
            Build();
            var at = WhereDone(which, left);
            Burst(at, Warm);
            Debug.Log($"[Onboarding] Learned: {which}");
            if (voices == null) return;
            int wanted = which == Lesson.OpenMenu ? 69 : which == Lesson.TouchWorld ? 72 : 76;
            int first = Musical.Pitch(music, wanted);
            int second = Musical.Pitch(music, first + 7);
            voices.Play(Synth.Chime(Hz(first)), at, volume);
            voices.Play(Synth.Chime(Hz(second)), at, volume * 0.75f, 1f, 0.16f);
        }

        Vector3 WhereDone(Lesson which, bool left)
        {
            var rig = lastRig;
            var hand = rig != null ? rig.Get(which == Lesson.OpenMenu || (which == Lesson.Thumb && left)) : null;
            bool tracked = hand != null && hand.IsTracked;
            Vector3 Tip(int f) => hand.TipJoints[f] != null ? hand.TipJoints[f].position : hand.Tips[f];
            switch (which)
            {
                case Lesson.OpenMenu:
                    if (tracked) return (Tip(0) + Tip(1)) * 0.5f;
                    return ghostL != null && ghostL.IsValid ? ghostL.Tip(1) : transform.position;
                case Lesson.TouchWorld:
                    return tracked ? Tip(1) : MenuAim();
                default:
                    if (tracked) return Tip(0);
                    var ghost = left ? ghostL : ghostR;
                    return ghost != null && ghost.IsValid ? ghost.Tip(0) : transform.position;
            }
        }

        void Burst(Vector3 at, Color color)
        {
            burstAt = at;
            burstAge = 0f;
            burstColor = color;
            if (sparks == null) return;
            for (int i = 0; i < 26; i++)
            {
                var direction = Random.onUnitSphere;
                var emit = new ParticleSystem.EmitParams
                {
                    position = at + direction * 0.008f,
                    velocity = direction * Random.Range(0.1f, 0.32f),
                    startColor = Color.Lerp(color, Color.white, Random.value * 0.5f),
                    startSize = Random.Range(0.006f, 0.014f),
                    startLifetime = Random.Range(0.5f, 0.95f)
                };
                sparks.Emit(emit, 1);
            }
        }

        void StepBurst(Camera view, float dt)
        {
            burstAge += dt;
            const float Life = 0.75f;
            bool show = burstAge < Life;
            if (burstRing.gameObject.activeSelf != show) burstRing.gameObject.SetActive(show);
            if (!show) return;
            float u = burstAge / Life;
            float grow = 1f - (1f - u) * (1f - u);
            burstRing.transform.position = burstAt;
            burstRing.transform.localScale = Vector3.one * Mathf.Lerp(0.02f, 0.17f, grow);
            Effects.Face(burstRing.transform, view);
            block.Clear();
            block.SetColor(ColorId, burstColor);
            block.SetFloat(RadiusId, 0.85f);
            block.SetFloat(ThicknessId, 0.09f * (1f - u) + 0.015f);
            block.SetFloat(InnerGlowId, 0.15f * (1f - u));
            block.SetFloat(AlphaId, (1f - u) * (1f - u));
            burstRing.SetPropertyBlock(block);
        }

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }
}
