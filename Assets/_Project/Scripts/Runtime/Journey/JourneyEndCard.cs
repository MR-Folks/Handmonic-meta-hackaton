using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The card at the end of a song's journey, floating within reach in front of you: the song's title, a line saying
    /// what comes next, and two glass bubbles like the menu's, "Another song" (the next song, journey on) and "Explore"
    /// (journey off, and the menu opens). Each bubble is 8 cm across, a little below eye height and about 42 cm away, so
    /// a seated player reaches it easily. Touch one with a fingertip, or point at it from farther away and pinch, the way
    /// the menu works: a press needs the fingertip to come from in front and back out before the next, a pinch must open
    /// before it can choose, and a hand making Meta's menu gesture is ignored. A ring round "Another song" fills as the
    /// seconds pass; when it is full the next song's journey starts by itself, so a demo never stalls. While the menu is
    /// open the card hides and waits.
    /// </summary>
    public sealed class JourneyEndCard
    {
        public const int None = 0, Another = 1, Explore = 2;

        [System.Serializable]
        public sealed class Tuning
        {
            [Tooltip("How far ahead of your eyes the card opens (m). Meta suggests 42 to 46 cm for UI you touch.")]
            public float distance = 0.42f;
            [Tooltip("How far below eye height the bubbles sit (m).")]
            public float drop = 0.13f;
            [Tooltip("Size of each bubble (m); Meta asks for touch targets of at least 64 mm.")]
            public float bubbleSize = 0.08f;
            [Tooltip("Distance between the two bubbles' centres (m).")]
            public float spacing = 0.17f;
            [Tooltip("Pinch strength (0-1) that chooses, and the strength it must drop below before it can choose again.")]
            public Vector2 pinchThresholds = new Vector2(0.85f, 0.5f);
        }

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IconId = Shader.PropertyToID("_Icon");
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        static readonly int SelectedId = Shader.PropertyToID("_Selected");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly Color AnotherColor = new Color(0.72f, 0.7f, 1f);
        static readonly Color ExploreColor = new Color(0.55f, 0.95f, 0.7f);
        // The menu's emblems: next song, and drifting lights for exploring.
        const float AnotherIcon = 14f, ExploreIcon = 5f;
        const float TouchContact = 0.005f, TouchRearm = 0.02f, TouchZone = 0.15f;
        const int RingPoints = 48;

        sealed class Pointer
        {
            public bool Touching, TouchArmed, PinchArmed;
            public float Depth;
            public int Target = -1, Aimed = -1;
            public float Visibility;
            public Vector3 Hit, Origin;
            public Renderer Cursor;
            public LineStrip Ray;
        }

        readonly Tuning tuning;
        readonly Transform root;
        readonly Renderer[] bubbles = new Renderer[2];
        readonly float[] hover = new float[2];
        readonly float[] pop = new float[2];
        readonly TypeLabel title, subtitle, anotherLabel, exploreLabel;
        readonly LineStrip ring;
        readonly Vector3[] ringPoints = new Vector3[RingPoints];
        readonly Vector3[] rayPoints = new Vector3[2];
        readonly Pointer[] pointers = { new Pointer(), new Pointer() };
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly string[] countdown = new string[32];
        float age, show;
        bool open;

        public bool IsOpen => open;

        /// <summary>Whether the card is showing now (open, and not hidden by the menu).</summary>
        public bool Showing => open && show > 0.01f;

        public JourneyEndCard(Transform parent, Material bubble, Material glow, Material line, Tuning tuning)
        {
            this.tuning = tuning;
            root = new GameObject("End Card").transform;
            root.SetParent(parent, false);
            bubbles[0] = Effects.MakeQuad("Another Song", root, bubble);
            bubbles[1] = Effects.MakeQuad("Explore", root, bubble);
            title = new TypeLabel("Title", root, 0.022f, 0f);
            subtitle = new TypeLabel("Next", root, 0.0135f, 1f);
            anotherLabel = new TypeLabel("Another Label", root, 0.0145f, 1f);
            exploreLabel = new TypeLabel("Explore Label", root, 0.0145f, 1f);
            anotherLabel.Text = "Another song";
            exploreLabel.Text = "Explore";
            ring = new LineStrip("Countdown", root, line, RingPoints);
            for (int s = 0; s < 2; s++)
            {
                pointers[s].Cursor = Effects.MakeQuad(s == 0 ? "Cursor L" : "Cursor R", root, glow);
                pointers[s].Cursor.gameObject.SetActive(false);
                pointers[s].Ray = new LineStrip(s == 0 ? "Pointer L" : "Pointer R", root, line, 2);
                pointers[s].Ray.SetVisible(false);
            }
            root.gameObject.SetActive(false);
        }

        /// <summary>Opens the card in front of the viewer, for <paramref name="song"/> with <paramref name="next"/> to follow.</summary>
        public void Open(Camera view, string song, string next, float seconds)
        {
            open = true;
            age = 0f;
            show = 0f;
            title.Text = string.IsNullOrEmpty(song) ? "The end" : song;
            // Made once here, so the countdown never makes text while it runs.
            int whole = Mathf.Clamp(Mathf.CeilToInt(seconds), 0, countdown.Length - 1);
            for (int i = 0; i <= whole; i++)
                countdown[i] = string.IsNullOrEmpty(next) ? $"Next song in {i}" : $"Next: {next} in {i}";
            subtitle.Text = countdown[whole];
            for (int b = 0; b < 2; b++) hover[b] = pop[b] = 0f;
            // A fingertip already at the card must come back out, and a held pinch must open, before either can choose.
            foreach (var p in pointers)
            {
                p.Touching = p.TouchArmed = p.PinchArmed = false;
                p.Aimed = p.Target = -1;
                p.Visibility = 0f;
            }
            if (view != null) Place(view);
        }

        public void Close()
        {
            open = false;
            show = 0f;
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// One frame of the card: <paramref name="remaining"/> seconds until it moves on by itself, hidden while the menu
        /// is open. Returns Another or Explore on the frame a bubble is chosen, else None.
        /// </summary>
        public int Step(HandRig rig, Camera view, float remaining, float total, bool hidden, float dt)
        {
            if (!open || view == null) return None;
            show = Mathf.MoveTowards(show, hidden ? 0f : 1f, dt / 0.3f);
            bool visible = show > 0.001f;
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            if (hidden)
            {
                // Coming back, the hands start afresh, as when the menu opens.
                foreach (var p in pointers) p.TouchArmed = p.PinchArmed = p.Touching = false;
                return None;
            }
            age += dt;
            if (!visible) return None;

            int chosen = Point(rig, view, dt);
            float appear = Mathf.Clamp01(age / 0.35f);
            for (int b = 0; b < 2; b++)
            {
                bool aimed = pointers[0].Target == b || pointers[1].Target == b;
                hover[b] = Mathf.MoveTowards(hover[b], aimed ? 1f : 0f, dt * 7f);
                pop[b] = Mathf.MoveTowards(pop[b], 0f, dt * 2.2f);
                var r = bubbles[b];
                r.transform.position = root.TransformPoint(Slot(b));
                r.transform.localScale = Vector3.one * Mathf.Max(tuning.bubbleSize * EaseOut(appear) * (1f + hover[b] * 0.15f + pop[b] * 0.3f), 1e-4f);
                Effects.Face(r.transform, view);
                block.Clear();
                block.SetColor(ColorId, b == 0 ? AnotherColor : ExploreColor);
                block.SetFloat(IconId, b == 0 ? AnotherIcon : ExploreIcon);
                block.SetFloat(HoverId, hover[b]);
                block.SetFloat(SelectedId, b == 0 ? 1f : 0f);
                block.SetFloat(AlphaId, show * appear);
                r.SetPropertyBlock(block);
            }

            var rotation = root.rotation;
            title.Transform.SetPositionAndRotation(root.TransformPoint(new Vector3(0f, tuning.bubbleSize * 1.15f, 0f)), rotation);
            subtitle.Transform.SetPositionAndRotation(root.TransformPoint(new Vector3(0f, tuning.bubbleSize * 0.95f, 0f)), rotation);
            subtitle.Text = countdown[Mathf.Clamp(Mathf.CeilToInt(remaining), 0, countdown.Length - 1)] ?? subtitle.Text;
            anotherLabel.Transform.SetPositionAndRotation(root.TransformPoint(Slot(0) + Vector3.down * (tuning.bubbleSize * 0.68f)), rotation);
            exploreLabel.Transform.SetPositionAndRotation(root.TransformPoint(Slot(1) + Vector3.down * (tuning.bubbleSize * 0.68f)), rotation);
            title.Color = new Color(1f, 0.95f, 0.85f, show * appear);
            subtitle.Color = new Color(1f, 1f, 1f, show * appear * 0.75f);
            anotherLabel.Color = exploreLabel.Color = new Color(1f, 1f, 1f, show * appear * 0.9f);

            // The countdown: a ring round "Another song" that fills from the top as the seconds pass.
            float filled = total > 0f ? Mathf.Clamp01(1f - remaining / total) : 0f;
            float radius = tuning.bubbleSize * 0.62f;
            var centre = Slot(0);
            for (int i = 0; i < RingPoints; i++)
            {
                float a = Mathf.PI * 0.5f - filled * Mathf.PI * 2f * i / (RingPoints - 1);
                ringPoints[i] = root.TransformPoint(centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius - Vector3.forward * 0.002f);
            }
            ring.SetVisible(filled > 0.01f);
            if (filled > 0.01f) ring.Set(ringPoints, RingPoints, 0.003f, new Color(AnotherColor.r, AnotherColor.g, AnotherColor.b, 0.85f * show), view.transform.position, false);

            DrawPointers(view);
            if (chosen >= 0) pop[chosen] = 1f;
            return chosen == 0 ? Another : chosen == 1 ? Explore : None;
        }

        Vector3 Slot(int b) => new Vector3((b == 0 ? -0.5f : 0.5f) * tuning.spacing, 0f, 0f);

        void Place(Camera view)
        {
            var head = view.transform.position;
            var forward = Effects.FlatForward(view);
            var position = head + forward * tuning.distance + Vector3.down * tuning.drop;
            root.SetPositionAndRotation(position, Quaternion.LookRotation(position - head, Vector3.up));
        }

        /// <summary>
        /// Where each hand touches or points, as the menu does: within reach the index fingertip presses a bubble as it
        /// reaches the card from in front; farther away a ray from the shoulder through the pinch aims, and closing the
        /// pinch chooses the bubble it was aimed at just before. Returns the bubble chosen this frame, or -1.
        /// </summary>
        int Point(HandRig rig, Camera view, float dt)
        {
            int chosen = -1;
            bool ready = age > 0.6f;
            var plane = new Plane(root.forward, root.position);
            var head = view.transform.position;
            var side = Vector3.Cross(Vector3.up, Effects.FlatForward(view)).normalized;
            for (int s = 0; s < 2; s++)
            {
                var p = pointers[s];
                var hand = rig != null ? rig.Get(s == 0) : null;
                p.Target = -1;
                if (hand == null || !hand.IsTracked || hand.InSystemGesture)
                {
                    p.Touching = p.TouchArmed = p.PinchArmed = false;
                    p.Visibility = 0f;
                    continue;
                }
                var tip = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
                var local = root.InverseTransformPoint(tip);
                float depth = -local.z;
                bool over = Mathf.Abs(local.x) < tuning.spacing + tuning.bubbleSize && Mathf.Abs(local.y) < tuning.bubbleSize * 1.4f;
                p.Touching = over && Mathf.Abs(depth) < TouchZone;
                if (p.Touching)
                {
                    p.Target = BubbleAt(local, tuning.bubbleSize * 0.6f);
                    if (p.TouchArmed && p.Depth > TouchContact && depth <= TouchContact)
                    {
                        p.TouchArmed = false;
                        if (ready && p.Target >= 0 && chosen < 0)
                        {
                            chosen = p.Target;
                            // The finger that pressed lights up, as at the menu.
                            GestureFeedback.Light(s == 0, Fingers.Index);
                        }
                    }
                    p.PinchArmed = false;
                }
                else
                {
                    var thumb = hand.TipJoints[0] != null ? hand.TipJoints[0].position : hand.Tips[0];
                    var pinchPoint = (thumb + tip) * 0.5f;
                    var shoulder = head + Vector3.down * 0.17f + side * (s == 0 ? -0.17f : 0.17f);
                    var ray = new Ray(shoulder, pinchPoint - shoulder);
                    bool hits = plane.Raycast(ray, out float enter);
                    p.Hit = ray.GetPoint(enter);
                    p.Origin = pinchPoint;
                    if (hits) p.Target = BubbleAt(root.InverseTransformPoint(p.Hit), tuning.bubbleSize * 0.65f);
                    if (hand.Pinch < 0.6f) p.Aimed = p.Target;
                    if (hand.Pinch < tuning.pinchThresholds.y) p.PinchArmed = true;
                    if (p.PinchArmed && hand.Pinch >= tuning.pinchThresholds.x)
                    {
                        p.PinchArmed = false;
                        if (ready && p.Aimed >= 0 && chosen < 0)
                        {
                            chosen = p.Aimed;
                            GestureFeedback.Light(s == 0, Fingers.Thumb | Fingers.Index);
                        }
                    }
                    if (!hits) p.Target = -1;
                }
                if (depth > TouchRearm) p.TouchArmed = true;
                p.Depth = depth;
                p.Visibility = Mathf.MoveTowards(p.Visibility, p.Touching || p.Target >= 0 ? 1f : 0.4f, dt * 6f);
            }
            return chosen;
        }

        void DrawPointers(Camera view)
        {
            for (int s = 0; s < 2; s++)
            {
                var p = pointers[s];
                bool pointing = !p.Touching && p.Visibility > 0.01f;
                if (p.Cursor.gameObject.activeSelf != pointing) p.Cursor.gameObject.SetActive(pointing);
                p.Ray.SetVisible(pointing);
                if (!pointing) continue;
                p.Cursor.transform.position = p.Hit - root.forward * 0.004f;
                p.Cursor.transform.localScale = Vector3.one * (p.Target >= 0 ? 0.024f : 0.018f);
                Effects.Face(p.Cursor.transform, view);
                block.Clear();
                block.SetColor(ColorId, p.Target >= 0 ? Color.white : new Color(0.85f, 0.9f, 1f));
                block.SetFloat(IntensityId, p.Visibility * show);
                p.Cursor.SetPropertyBlock(block);
                rayPoints[0] = p.Origin;
                rayPoints[1] = p.Hit;
                p.Ray.Set(rayPoints, 2, 0.0025f, new Color(0.85f, 0.9f, 1f, 0.2f * p.Visibility * show), view.transform.position, false);
            }
        }

        int BubbleAt(Vector3 local, float reach)
        {
            int found = -1;
            for (int b = 0; b < 2; b++)
            {
                float d = Vector2.Distance(local, Slot(b));
                if (d < reach)
                {
                    reach = d;
                    found = b;
                }
            }
            return found;
        }

        static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);
    }
}
