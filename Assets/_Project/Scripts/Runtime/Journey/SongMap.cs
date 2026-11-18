using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The song map: a gentle arc of small dots low in your view, one per section of the song, joined by a faint line.
    /// The section playing now is lit and a little larger, pulsing softly with the beat; the ones already played glow
    /// softly and the ones to come are faint, each in the colour of its world. The section's name shows over the lit
    /// dot. It appears when a journey starts and at each change of section, then fades after a few seconds. It keeps
    /// inside the middle of a narrow headset view (at most 13 degrees either side and about 24 degrees down, inside
    /// Meta's 58 by 54 degree design area), follows your view loosely and hides while the menu is open. It also carries
    /// Journey mode's short captions ("Journey off: you're choosing").
    /// </summary>
    public sealed class SongMap
    {
        public const int MaxDots = 20;

        [System.Serializable]
        public sealed class Tuning
        {
            [Tooltip("How far ahead the map floats (m).")]
            public float distance = 0.85f;
            [Tooltip("How far below eye level the middle of the arc sits (degrees). Meta's narrow design area reaches 27.")]
            public float down = 24f;
            [Tooltip("How much higher the arc's ends are than its middle (degrees).")]
            public float curve = 2f;
            [Tooltip("Degrees between neighbouring dots, and the widest the whole arc may be (it stays within ±13°).")]
            public Vector2 spread = new Vector2(3.6f, 26f);
            [Tooltip("Size of a dot, and of the dot playing now (m).")]
            public Vector2 dotSize = new Vector2(0.017f, 0.03f);
            [Tooltip("How long the map stays after a change of section before it fades (s).")]
            public float showSeconds = 4.5f;
            [Tooltip("How long a caption stays (s).")]
            public float captionSeconds = 3f;
        }

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        readonly Tuning tuning;
        readonly Transform root;
        readonly Renderer[] dots = new Renderer[MaxDots];
        readonly Vector3[] points = new Vector3[MaxDots];
        readonly Color[] pointColors = new Color[MaxDots];
        readonly LineStrip path;
        readonly TypeLabel label, caption;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        float alpha, showFor, captionFor, captionAlpha;
        bool placed;

        /// <summary>Whether any of the map is showing (for the editor checks and screenshots).</summary>
        public bool Visible => alpha > 0.01f || captionAlpha > 0.01f;

        /// <summary>The caption showing now, or an empty string.</summary>
        public string Caption => captionFor > 0f || captionAlpha > 0.01f ? caption.Text : "";

        public SongMap(Transform parent, Material glow, Material line, Tuning tuning)
        {
            this.tuning = tuning;
            root = new GameObject("Song Map").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < MaxDots; i++)
            {
                dots[i] = Effects.MakeQuad("Dot", root, glow);
                dots[i].gameObject.SetActive(false);
            }
            path = new LineStrip("Path", root, line, MaxDots);
            path.SetVisible(false);
            label = new TypeLabel("Section", root, 0.02f, 0f);
            caption = new TypeLabel("Caption", root, 0.022f, 0.5f);
            label.GameObject.SetActive(false);
            caption.GameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }

        /// <summary>Shows the map for a few seconds (a journey started, or a section changed).</summary>
        public void Flash() => showFor = tuning.showSeconds;

        /// <summary>Shows a short caption over the arc for a few seconds.</summary>
        public void Say(string text)
        {
            caption.Text = text;
            captionFor = tuning.captionSeconds;
        }

        /// <summary>Hides the map at once.</summary>
        public void Hide()
        {
            showFor = captionFor = 0f;
            alpha = captionAlpha = 0f;
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// One frame: <paramref name="count"/> sections with their world colours and labels, the one playing now
        /// (<paramref name="current"/>), and <paramref name="future"/> more expected but not yet known (music heard as it
        /// plays). The timers only run while it may show, so a caption made while the menu is open waits for it to close.
        /// </summary>
        public void Step(int count, int current, int future, Color[] colors, string[] labels, float pulse, Camera view, bool hidden, float dt)
        {
            if (!hidden)
            {
                showFor = Mathf.Max(0f, showFor - dt);
                captionFor = Mathf.Max(0f, captionFor - dt);
            }
            bool wantDots = !hidden && showFor > 0f && count > 0;
            alpha = Mathf.MoveTowards(alpha, wantDots ? 1f : 0f, dt / (wantDots ? 0.3f : 0.6f));
            captionAlpha = Mathf.MoveTowards(captionAlpha, !hidden && captionFor > 0f ? 1f : 0f, dt / 0.3f);
            bool show = view != null && (alpha > 0.005f || captionAlpha > 0.005f);
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show)
            {
                placed = false;
                return;
            }

            // Centred on the head, turned with it loosely, level with the horizon.
            var head = view.transform.position;
            var facing = Quaternion.LookRotation(Effects.FlatForward(view), Vector3.up);
            root.SetPositionAndRotation(head, placed ? Quaternion.Slerp(root.rotation, facing, 1f - Mathf.Exp(-2.5f * dt)) : facing);
            placed = true;

            int total = Mathf.Clamp(count + future, 0, MaxDots);
            count = Mathf.Min(count, total);
            float step = total > 1 ? Mathf.Min(tuning.spread.x, tuning.spread.y / (total - 1)) : 0f;
            for (int i = 0; i < MaxDots; i++)
            {
                var dot = dots[i];
                bool used = i < total && alpha > 0.005f;
                if (dot.gameObject.activeSelf != used) dot.gameObject.SetActive(used);
                if (!used) continue;
                float side = total > 1 ? i - (total - 1) * 0.5f : 0f;
                float edge = total > 1 ? side / ((total - 1) * 0.5f) : 0f;
                var direction = Quaternion.Euler(tuning.down - tuning.curve * edge * edge, side * step, 0f) * Vector3.forward;
                points[i] = root.TransformPoint(direction * tuning.distance);
                dot.transform.position = points[i];
                Effects.Face(dot.transform, view);

                bool known = i < count;
                bool now = i == current;
                var tint = known ? Color.Lerp(colors[i], Color.white, 0.2f) : new Color(0.85f, 0.88f, 1f);
                float size = now ? tuning.dotSize.y * (1f + pulse * 0.12f) : tuning.dotSize.x;
                float glow = now ? 1.8f + pulse * 0.4f : i < current ? 0.85f : known ? 0.4f : 0.22f;
                dot.transform.localScale = Vector3.one * size;
                block.Clear();
                block.SetColor(ColorId, tint);
                block.SetFloat(IntensityId, glow * alpha);
                dot.SetPropertyBlock(block);
                pointColors[i] = new Color(1f, 1f, 1f, i <= current ? 0.3f : 0.12f);
            }

            path.SetVisible(total > 1 && alpha > 0.005f);
            if (total > 1) path.Set(points, pointColors, total, 0.0022f, new Color(1f, 1f, 1f, alpha), head, false);

            bool named = alpha > 0.005f && current >= 0 && current < count && labels[current] != null;
            if (label.GameObject.activeSelf != named) label.GameObject.SetActive(named);
            if (named)
            {
                label.Text = labels[current];
                var at = points[current] + Vector3.up * (tuning.dotSize.y * 0.9f);
                label.Transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - head, Vector3.up));
                label.Color = new Color(1f, 1f, 1f, alpha * 0.9f);
            }

            bool saying = captionAlpha > 0.005f;
            if (caption.GameObject.activeSelf != saying) caption.GameObject.SetActive(saying);
            if (saying)
            {
                // Over the middle of the arc, a little above the section's name.
                var direction = Quaternion.Euler(tuning.down - tuning.curve - 4.5f, 0f, 0f) * Vector3.forward;
                var at = root.TransformPoint(direction * tuning.distance);
                caption.Transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - head, Vector3.up));
                caption.Color = new Color(1f, 0.92f, 0.8f, captionAlpha);
            }
        }
    }
}
