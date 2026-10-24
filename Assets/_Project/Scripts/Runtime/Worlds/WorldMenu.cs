using System;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The world menu: a honeycomb of glass bubbles, like an Apple Watch home screen, opened with Meta's left-hand
    /// menu gesture. The centre bubble plays and pauses the song; the six around it are the worlds, each showing a
    /// small animated emblem. Bubbles near where you look and near your finger swell; poke one to pop into it.
    /// </summary>
    [DefaultExecutionOrder(170)]
    public class WorldMenu : MonoBehaviour
    {
        [SerializeField] Material bubbleMaterial;
        [SerializeField] float distance = 0.42f;
        [SerializeField] float drop = 0.06f;
        [SerializeField] float spacing = 0.11f;
        [SerializeField] float bubbleSize = 0.095f;

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

        sealed class Bubble
        {
            public Renderer Renderer;
            public TextMesh Label;
            public int World;
            public Vector2 Slot;
            public float Delay;
            public float Hover;
            public float Pop;
        }

        Bubble[] bubbles = Array.Empty<Bubble>();
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
            Vector3? pointer = null;
            float best = float.MaxValue;
            if (rig != null)
            {
                foreach (var hand in new[] { rig.Left, rig.Right })
                {
                    if (!hand.IsTracked) continue;
                    var tip = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
                    float d = Vector3.Distance(tip, root.position);
                    if (d < best) { best = d; pointer = tip; }
                }
            }

            float t = Application.isPlaying ? Time.time : 0f;
            for (int i = 0; i < bubbles.Length; i++)
            {
                var b = bubbles[i];
                float appear = open ? Mathf.Clamp01((openTime - b.Delay) / 0.32f) : 0f;
                b.Pop = Mathf.MoveTowards(b.Pop, 0f, dt * 2.2f);
                float show = open ? EaseOutBack(appear) : 0f;
                if (!open) show = 0f;
                bool visible = show > 0.001f || b.Pop > 0.001f;
                if (b.Renderer.gameObject.activeSelf != visible)
                {
                    b.Renderer.gameObject.SetActive(visible);
                    b.Label.gameObject.SetActive(visible);
                }
                if (!visible) continue;
                anyVisible = true;

                // Apple Watch style: bubbles near where you look are a little larger.
                var local = new Vector3(b.Slot.x, b.Slot.y, 0f) + new Vector3(0f, Mathf.Sin(t * 1.3f + i) * 0.003f, 0f);
                float fisheye = Mathf.Lerp(0.82f, 1.06f, Mathf.Exp(-((Vector2)local - gaze).sqrMagnitude / (spacing * spacing * 1.3f)));
                var world = root.TransformPoint(local);

                float size = bubbleSize * fisheye * show;
                float distanceToTip = pointer.HasValue ? Vector3.Distance(pointer.Value, world) : 99f;
                b.Hover = Mathf.MoveTowards(b.Hover, distanceToTip < size * 0.9f ? 1f : 0f, dt * 6f);

                if (open && pointer.HasValue && distanceToTip < size * 0.42f && cooldown <= 0f && appear >= 1f) Press(b, manager, bus);

                float scale = size * (1f + b.Hover * 0.18f + b.Pop * 0.45f);
                b.Renderer.transform.position = world;
                b.Renderer.transform.localScale = Vector3.one * Mathf.Max(scale, 1e-4f);
                Effects.Face(b.Renderer.transform, view);

                var color = b.World >= 0 ? manager.Worlds[b.World].Color : new Color(0.85f, 0.85f, 1f);
                block.Clear();
                block.SetColor(ColorId, color);
                block.SetFloat(IconId, b.World >= 0 ? manager.Worlds[b.World].MenuIcon : 6f);
                block.SetFloat(HoverId, b.Hover);
                block.SetFloat(SelectedId, b.World == manager.Current ? 1f : 0f);
                block.SetFloat(AlphaId, Mathf.Clamp01(show) * (1f - b.Pop * 0.85f) + b.Pop * 0.15f);
                if (bus != null) block.SetVector(EqId, new Vector4(bus.Bands[1], bus.Bands[2], bus.Bands[3], bus.Bands[4]));
                block.SetFloat(PausedId, bus != null && !bus.IsPlaying ? 1f : 0f);
                b.Renderer.SetPropertyBlock(block);

                b.Label.text = b.World >= 0 ? manager.Worlds[b.World].DisplayName : bus != null && bus.CurrentSong != null ? bus.CurrentSong.title : "Music";
                b.Label.transform.position = world + root.up * -(size * 0.62f);
                b.Label.transform.rotation = b.Renderer.transform.rotation;
                var labelColor = Color.Lerp(Color.white, color, 0.35f);
                labelColor.a = Mathf.Clamp01(show) * (0.45f + b.Hover * 0.55f);
                b.Label.color = labelColor;
            }

            if (root.gameObject.activeSelf != (anyVisible || open)) root.gameObject.SetActive(anyVisible || open);
        }

        void Press(Bubble b, WorldManager manager, MusicBus bus)
        {
            cooldown = 0.6f;
            b.Pop = 1f;
            float note = b.World >= 0 ? Synth.Pentatonic[6 + b.World % 5] : Synth.Pentatonic[5];
            voices.Play(Synth.Chime(note), b.Renderer.transform.position, 0.6f);
            if (b.World >= 0)
            {
                manager.Switch(b.World);
                closeAt = openTime + 0.35f;
            }
            else if (bus != null) bus.TogglePause();
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

        void Build(WorldManager manager)
        {
            if (root != null && bubbles.Length == manager.Worlds.Count + 1) return;
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
            bubbles = new Bubble[count + 1];
            for (int i = 0; i <= count; i++)
            {
                // The centre bubble (now playing) first, then the worlds around it like a honeycomb.
                int world = i - 1;
                Vector2 slot = Vector2.zero;
                if (world >= 0)
                {
                    float angle = (90f - 60f * world) * Mathf.Deg2Rad;
                    float ring = world < 6 ? spacing : spacing * 2f;
                    slot = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                }

                var r = Effects.MakeQuad(world >= 0 ? $"Bubble {world}" : "Bubble Now Playing", root, bubbleMaterial);
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(root, false);
                var label = labelGo.AddComponent<TextMesh>();
                label.font = font;
                labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                label.fontSize = 40;
                label.characterSize = 0.0018f;
                label.anchor = TextAnchor.UpperCenter;
                label.alignment = TextAlignment.Center;

                bubbles[i] = new Bubble { Renderer = r, Label = label, World = world, Slot = slot, Delay = world < 0 ? 0f : 0.06f + world * 0.035f };
                r.gameObject.SetActive(false);
                labelGo.SetActive(false);
            }
            root.gameObject.SetActive(false);
        }
    }
}
