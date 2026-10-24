using System;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The world menu: a honeycomb of glass bubbles, like an Apple Watch home screen, opened with Meta's left-hand
    /// menu gesture. The centre bubble plays and pauses the song; the six around it are the worlds, each showing a
    /// small animated emblem, and the one below them switches to listening to the room. Bubbles near where you look
    /// are a little larger. Each hand casts a soft pointer ray onto the menu (from the shoulder through the pinch);
    /// the bubble it points at swells and its name shows large above the honeycomb. Pinch to pop into it.
    /// </summary>
    [DefaultExecutionOrder(170)]
    public class WorldMenu : MonoBehaviour
    {
        [SerializeField] Material bubbleMaterial;
        [SerializeField] Material glowMaterial;
        [SerializeField] Material lineMaterial;
        [SerializeField] float distance = 0.42f;
        [SerializeField] float drop = 0.06f;
        [SerializeField] float spacing = 0.1f;
        [SerializeField] float bubbleSize = 0.088f;
        [Tooltip("Pinch strength (0-1) that selects, and the strength the pinch must drop below before it can select again.")]
        [SerializeField] Vector2 pinchThresholds = new Vector2(0.85f, 0.5f);

        /// <summary>Raised by the menu button feed (Meta's menu gesture); the menu opens or closes.</summary>
        public static event Action ToggleRequested;
        public static void RequestToggle() => ToggleRequested?.Invoke();

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IconId = Shader.PropertyToID("_Icon");
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        static readonly int SelectedId = Shader.PropertyToID("_Selected");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int EqId = Shader.PropertyToID("_Eq");
        static readonly int PausedId = Shader.PropertyToID("_Paused");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        // Bubble.World values for the two bubbles that aren't worlds.
        const int NowPlaying = -1;
        const int ListenToggle = -2;
        static readonly Color ListenColor = new Color(0.98f, 0.62f, 0.5f);

        sealed class Bubble
        {
            public Renderer Renderer;
            public int World;
            public Vector2 Slot;
            public float Delay;
            public float Hover;
            public float Pop;
        }

        /// <summary>One hand's pointer: a ray from the shoulder through the pinch, and where it meets the menu.</summary>
        sealed class Pointer
        {
            public bool Active;
            public bool Armed;
            public bool Pinching;
            public float Pinch;
            public Vector3 Hit;
            public Vector3 Origin;
            public int Target = -1;
            public int Aimed = -1;
            public float AimedAge;
            public float Visibility;
            public Renderer Cursor;
            public LineStrip Ray;
        }

        Bubble[] bubbles = Array.Empty<Bubble>();
        readonly Pointer[] pointers = { new Pointer(), new Pointer() };
        readonly Vector3[] rayPath = new Vector3[2];
        TextMesh caption, captionShadow, hint;
        Transform root;
        MaterialPropertyBlock block;
        AudioPool voices;
        bool open, toggleRequested;
        float openTime, closeAt = -1f, cooldown;

        public bool IsOpen => open;

        void OnEnable() => ToggleRequested += OnToggle;
        void OnDisable() => ToggleRequested -= OnToggle;
        void OnToggle() => toggleRequested = true;

        void LateUpdate()
        {
            if (toggleRequested)
            {
                toggleRequested = false;
                if (open) Close(); else Open(Camera.main);
            }
            Step(HandRig.Instance, WorldManager.Instance, MusicBus.Instance, Camera.main, Time.deltaTime);
        }

        /// <summary>Opens the menu in front of the viewer.</summary>
        public void Open(Camera view)
        {
            if (view == null) return;
            open = true;
            openTime = 0f;
            closeAt = -1f;
            Place(view);
            foreach (var b in bubbles) b.Pop = 0f;
            // The pinch that opened the menu (Meta's menu gesture) must be let go before a pinch can select.
            foreach (var p in pointers) p.Armed = false;
        }

        public void Close() => open = false;

        void Place(Camera view)
        {
            if (root == null) return;
            var head = view.transform.position;
            var forward = Effects.FlatForward(view);
            root.position = head + forward * distance + Vector3.down * drop;
            root.rotation = Quaternion.LookRotation(root.position - head, Vector3.up);
        }

        /// <summary>One frame of the menu. Public so the editor preview can drive it.</summary>
        public void Step(HandRig rig, WorldManager manager, MusicBus bus, Camera view, float dt)
        {
            if (manager == null || view == null) return;
            Build(manager);
            if (open && openTime == 0f) Place(view);
            openTime += dt;
            cooldown -= dt;
            if (closeAt >= 0f && openTime >= closeAt) Close();

            bool anyVisible = false;
            var gaze = GazePoint(view);
            UpdatePointers(rig, view, dt);

            float t = Application.isPlaying ? Time.time : 0f;
            int hovered = -1;
            for (int i = 0; i < bubbles.Length; i++)
            {
                var b = bubbles[i];
                float appear = open ? Mathf.Clamp01((openTime - b.Delay) / 0.32f) : 0f;
                b.Pop = Mathf.MoveTowards(b.Pop, 0f, dt * 2.2f);
                float show = open ? EaseOutBack(appear) : 0f;
                bool visible = show > 0.001f || b.Pop > 0.001f;
                if (b.Renderer.gameObject.activeSelf != visible) b.Renderer.gameObject.SetActive(visible);
                if (!visible) continue;
                anyVisible = true;

                // Apple Watch style: bubbles near where you look are a little larger.
                var local = new Vector3(b.Slot.x, b.Slot.y, 0f) + new Vector3(0f, Mathf.Sin(t * 1.3f + i) * 0.003f, 0f);
                float fisheye = Mathf.Lerp(0.82f, 1.06f, Mathf.Exp(-((Vector2)local - gaze).sqrMagnitude / (spacing * spacing * 1.3f)));
                var world = root.TransformPoint(local);
                float size = bubbleSize * fisheye * show;

                bool aimed = open && (pointers[0].Target == i || pointers[1].Target == i);
                b.Hover = Mathf.MoveTowards(b.Hover, aimed ? 1f : 0f, dt * 7f);
                if (aimed) hovered = i;

                float scale = size * (1f + b.Hover * 0.2f + b.Pop * 0.45f);
                b.Renderer.transform.position = world;
                b.Renderer.transform.localScale = Vector3.one * Mathf.Max(scale, 1e-4f);
                Effects.Face(b.Renderer.transform, view);

                bool selected = b.World == ListenToggle ? bus != null && bus.IsListening : b.World == manager.Current;
                block.Clear();
                block.SetColor(ColorId, BubbleColor(b, manager));
                block.SetFloat(IconId, b.World >= 0 ? manager.Worlds[b.World].MenuIcon : b.World == ListenToggle ? 7f : 6f);
                block.SetFloat(HoverId, b.Hover);
                block.SetFloat(SelectedId, selected ? 1f : 0f);
                block.SetFloat(AlphaId, Mathf.Clamp01(show) * (1f - b.Pop * 0.85f) + b.Pop * 0.15f);
                if (bus != null) block.SetVector(EqId, new Vector4(bus.Bands[1], bus.Bands[2], bus.Bands[3], bus.Bands[4]));
                block.SetFloat(PausedId, bus != null && !bus.IsPlaying ? 1f : 0f);
                b.Renderer.SetPropertyBlock(block);
            }

            // Pinch to select: the bubble the hand was aiming at just before the pinch closed, so the small jump of
            // the hand while pinching doesn't change the choice.
            if (open && cooldown <= 0f)
            {
                foreach (var p in pointers)
                {
                    if (!p.Active || !p.Pinching || p.Aimed < 0 || p.AimedAge > 0.35f) continue;
                    var b = bubbles[p.Aimed];
                    if (openTime - b.Delay < 0.32f) continue;
                    Press(b, manager, bus);
                    p.Aimed = -1;
                    break;
                }
            }

            DrawCaption(manager, bus, hovered, view, dt);
            DrawPointers(view);
            if (root.gameObject.activeSelf != (anyVisible || open)) root.gameObject.SetActive(anyVisible || open);
        }

        Color BubbleColor(Bubble b, WorldManager manager) =>
            b.World >= 0 ? manager.Worlds[b.World].Color : b.World == ListenToggle ? ListenColor : new Color(0.85f, 0.85f, 1f);

        string BubbleName(Bubble b, WorldManager manager, MusicBus bus) =>
            b.World >= 0 ? manager.Worlds[b.World].DisplayName
            : b.World == ListenToggle ? bus != null && bus.IsListening ? "Play the song" : "Listen to the room"
            : bus == null ? "Music" : bus.IsPlaying ? $"Pause {bus.Title}" : $"Play {bus.Title}";

        /// <summary>
        /// Finds where each hand points: a ray from an estimated shoulder through the point between thumb and index
        /// tips, like Meta's own hand pointers, hitting the menu's plane. Also tracks the pinch with hysteresis.
        /// </summary>
        void UpdatePointers(HandRig rig, Camera view, float dt)
        {
            var plane = new Plane(root.forward, root.position);
            var head = view.transform.position;
            var side = Vector3.Cross(Vector3.up, Effects.FlatForward(view)).normalized;
            for (int s = 0; s < 2; s++)
            {
                var p = pointers[s];
                var hand = rig?.Get(s == 0);
                p.Pinching = false;
                p.Active = open && hand != null && hand.IsTracked && !hand.InSystemGesture;
                if (p.Active)
                {
                    var thumb = hand.TipJoints[0] != null ? hand.TipJoints[0].position : hand.Tips[0];
                    var index = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
                    var pinchPoint = (thumb + index) * 0.5f;
                    var shoulder = head + Vector3.down * 0.17f + side * (s == 0 ? -0.17f : 0.17f);
                    var ray = new Ray(shoulder, pinchPoint - shoulder);
                    p.Active = plane.Raycast(ray, out float enter);
                    var hit = ray.GetPoint(enter);
                    // Smooth the cursor a little; hand tracking jitters more than a controller.
                    p.Hit = p.Visibility > 0.01f ? Vector3.Lerp(p.Hit, hit, 1f - Mathf.Exp(-25f * dt)) : hit;
                    p.Origin = pinchPoint;

                    float pinch = p.Pinch = hand.Pinch;
                    if (pinch < pinchThresholds.y) p.Armed = true;
                    if (p.Armed && pinch >= pinchThresholds.x)
                    {
                        p.Pinching = true;
                        p.Armed = false;
                    }

                    // The nearest bubble within reach of the cursor is the target.
                    var localHit = (Vector2)root.InverseTransformPoint(p.Hit);
                    p.Target = -1;
                    float best = bubbleSize * 0.62f;
                    for (int i = 0; i < bubbles.Length; i++)
                    {
                        float d = Vector2.Distance(localHit, bubbles[i].Slot);
                        if (d < best) { best = d; p.Target = i; }
                    }
                    // Remember the aim while the pinch is still open.
                    if (pinch < 0.6f)
                    {
                        p.Aimed = p.Target;
                        p.AimedAge = 0f;
                    }
                    else p.AimedAge += dt;
                }
                if (!p.Active) p.Target = -1;
                p.Visibility = Mathf.MoveTowards(p.Visibility, p.Active ? 1f : 0f, dt * 6f);
            }
        }

        /// <summary>A soft ray from the pinch to the menu and a glowing cursor where it lands; it tightens as you pinch.</summary>
        void DrawPointers(Camera view)
        {
            for (int s = 0; s < 2; s++)
            {
                var p = pointers[s];
                bool show = p.Visibility > 0.01f;
                if (p.Cursor.gameObject.activeSelf != show) p.Cursor.gameObject.SetActive(show);
                p.Ray.SetVisible(show);
                if (!show) continue;
                float pinch = p.Pinch;
                bool onBubble = p.Target >= 0;
                p.Cursor.transform.position = p.Hit - root.forward * 0.004f;
                p.Cursor.transform.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.016f, pinch) * (onBubble ? 1.2f : 1f);
                Effects.Face(p.Cursor.transform, view);
                block.Clear();
                block.SetColor(ColorId, onBubble ? Color.white : new Color(0.85f, 0.9f, 1f));
                block.SetFloat(IntensityId, p.Visibility * (onBubble ? 1.1f : 0.6f) * (1f + pinch * 0.6f));
                p.Cursor.SetPropertyBlock(block);

                rayPath[0] = p.Origin;
                rayPath[1] = p.Hit;
                p.Ray.Set(rayPath, 2, 0.0025f, new Color(0.85f, 0.9f, 1f, 0.22f * p.Visibility), view.transform.position, false);
            }
        }

        /// <summary>
        /// The name of the bubble you point at, large above the honeycomb (or the current world's when you point at
        /// none), with a soft shadow so it reads over any room. A small hint below says how to choose.
        /// </summary>
        void DrawCaption(WorldManager manager, MusicBus bus, int hovered, Camera view, float dt)
        {
            float show = open ? Mathf.Clamp01((openTime - 0.1f) / 0.3f) : 0f;
            bool visible = show > 0.001f;
            if (caption.gameObject.activeSelf != visible)
            {
                caption.gameObject.SetActive(visible);
                captionShadow.gameObject.SetActive(visible);
                hint.gameObject.SetActive(visible);
            }
            if (!visible) return;

            Bubble subject = hovered >= 0 ? bubbles[hovered] : null;
            if (subject == null)
                foreach (var b in bubbles) if (b.World == manager.Current) subject = b;
            string text = subject != null ? BubbleName(subject, manager, bus) : "";
            caption.text = captionShadow.text = text;
            var color = subject != null ? Color.Lerp(Color.white, BubbleColor(subject, manager), hovered >= 0 ? 0.3f : 0.15f) : Color.white;
            color.a = show * (hovered >= 0 ? 1f : 0.8f);
            caption.color = color;
            captionShadow.color = new Color(0f, 0f, 0f, show * 0.55f);
            hint.color = new Color(1f, 1f, 1f, show * 0.6f);

            var top = root.TransformPoint(new Vector3(0f, spacing + bubbleSize * 0.95f, 0f));
            var rotation = Quaternion.LookRotation(root.position - view.transform.position, Vector3.up);
            caption.transform.SetPositionAndRotation(top, rotation);
            captionShadow.transform.SetPositionAndRotation(top + rotation * new Vector3(0.0009f, -0.0009f, 0.001f), rotation);
            hint.transform.SetPositionAndRotation(root.TransformPoint(new Vector3(0f, -spacing * 2f - bubbleSize * 0.75f, 0f)), rotation);
        }

        void Press(Bubble b, WorldManager manager, MusicBus bus)
        {
            cooldown = 0.6f;
            b.Pop = 1f;
            float note = b.World >= 0 ? Synth.Pentatonic[6 + b.World % 5] : Synth.Pentatonic[b.World == ListenToggle ? 4 : 5];
            voices.Play(Synth.Chime(note), b.Renderer.transform.position, 0.6f);
            if (b.World >= 0)
            {
                manager.Switch(b.World);
                closeAt = openTime + 0.35f;
            }
            else if (bus == null) return;
            else if (b.World == ListenToggle) bus.ToggleListen();
            else bus.TogglePause();
        }

        /// <summary>Where the viewer is looking on the menu's plane, in the menu's local space.</summary>
        Vector2 GazePoint(Camera view)
        {
            var plane = new Plane(root.forward, root.position);
            var ray = new Ray(view.transform.position, view.transform.forward);
            if (!plane.Raycast(ray, out float enter)) return Vector2.zero;
            return root.InverseTransformPoint(ray.GetPoint(enter));
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        // Hexagonal honeycomb cells around the centre: the first six worlds ring it, the next ones sit in the outer
        // ring at its sides and lower corners (the bottom cell is the listen bubble).
        static readonly float[] OuterAngles = { 0f, 180f, 300f, 240f, 60f, 120f };

        Vector2 WorldSlot(int world)
        {
            if (world < 6)
            {
                float angle = (90f - 60f * world) * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spacing;
            }
            float outer = OuterAngles[(world - 6) % OuterAngles.Length] * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(outer), Mathf.Sin(outer)) * spacing * Mathf.Sqrt(3f);
        }

        TextMesh MakeText(string objectName, Font font, int fontSize, float characterSize, TextAnchor anchor, FontStyle style)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(root, false);
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            text.fontSize = fontSize;
            text.characterSize = characterSize;
            text.fontStyle = style;
            text.anchor = anchor;
            text.alignment = TextAlignment.Center;
            go.SetActive(false);
            return text;
        }

        void Build(WorldManager manager)
        {
            if (root != null && bubbles.Length == manager.Worlds.Count + 2) return;
            if (root != null)
            {
                if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject);
            }
            block = new MaterialPropertyBlock();
            root = new GameObject("Menu").transform;
            root.SetParent(transform, false);
            voices ??= new AudioPool(transform, 3);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int count = manager.Worlds.Count;
            bubbles = new Bubble[count + 2];
            for (int i = 0; i <= count + 1; i++)
            {
                // The centre bubble (now playing) first, then the worlds around it like a honeycomb, then the listen
                // bubble below them all.
                int world = i <= count ? i - 1 : ListenToggle;
                Vector2 slot = world >= 0 ? WorldSlot(world) : world == ListenToggle ? new Vector2(0f, -spacing * 2f) : Vector2.zero;

                string objectName = world >= 0 ? $"Bubble {world}" : world == ListenToggle ? "Bubble Listen" : "Bubble Now Playing";
                var r = Effects.MakeQuad(objectName, root, bubbleMaterial);
                float delay = world == NowPlaying ? 0f : world == ListenToggle ? 0.06f + count * 0.035f : 0.06f + world * 0.035f;
                bubbles[i] = new Bubble { Renderer = r, World = world, Slot = slot, Delay = delay };
                r.gameObject.SetActive(false);
            }

            captionShadow = MakeText("Caption Shadow", font, 64, 0.0026f, TextAnchor.LowerCenter, FontStyle.Bold);
            caption = MakeText("Caption", font, 64, 0.0026f, TextAnchor.LowerCenter, FontStyle.Bold);
            hint = MakeText("Hint", font, 48, 0.0019f, TextAnchor.UpperCenter, FontStyle.Normal);
            hint.text = "Point and pinch to choose";

            for (int s = 0; s < 2; s++)
            {
                pointers[s].Cursor = Effects.MakeQuad(s == 0 ? "Cursor L" : "Cursor R", root, glowMaterial);
                pointers[s].Cursor.gameObject.SetActive(false);
                pointers[s].Ray = new LineStrip(s == 0 ? "Pointer L" : "Pointer R", root, lineMaterial, 2);
                pointers[s].Ray.SetVisible(false);
            }
            root.gameObject.SetActive(false);
        }
    }
}
