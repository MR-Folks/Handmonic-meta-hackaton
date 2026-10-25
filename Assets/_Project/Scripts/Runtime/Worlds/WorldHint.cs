using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// When a world starts, its name and a short line saying what to do float in front of you for a few seconds,
    /// following your view loosely, then fade. Hidden while the menu is open.
    /// </summary>
    [DefaultExecutionOrder(175)]
    public class WorldHint : MonoBehaviour
    {
        [SerializeField] float showSeconds = 4.5f;
        [SerializeField] float distance = 0.65f;
        [SerializeField] float drop = 0.16f;

        TextMesh title, line, shadow;
        Transform root;
        float age = 99f;
        bool placed;

        void OnEnable()
        {
            if (WorldManager.Instance != null) WorldManager.Instance.WorldChanged += OnWorld;
        }

        void OnDisable()
        {
            if (WorldManager.Instance != null) WorldManager.Instance.WorldChanged -= OnWorld;
        }

        void Start()
        {
            // The first world may have started before this subscribed.
            if (WorldManager.Instance != null)
            {
                WorldManager.Instance.WorldChanged -= OnWorld;
                WorldManager.Instance.WorldChanged += OnWorld;
                if (WorldManager.Instance.Current >= 0) OnWorld(WorldManager.Instance.Current);
            }
        }

        void Build()
        {
            if (root != null) return;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root = new GameObject("Hint").transform;
            root.SetParent(transform, false);
            shadow = MakeText("Shadow", font, 56, 0.0011f, FontStyle.Normal);
            title = MakeText("Title", font, 72, 0.0016f, FontStyle.Bold);
            line = MakeText("Line", font, 56, 0.0011f, FontStyle.Normal);
            root.gameObject.SetActive(false);
        }

        TextMesh MakeText(string objectName, Font font, int size, float characterSize, FontStyle style)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(root, false);
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = size;
            text.characterSize = characterSize;
            text.fontStyle = style;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return text;
        }

        void OnWorld(int index)
        {
            var manager = WorldManager.Instance;
            if (manager == null || index < 0 || index >= manager.Worlds.Count) return;
            var world = manager.Worlds[index];
            if (world == null || string.IsNullOrEmpty(world.Hint)) return;
            Build();
            title.text = world.DisplayName;
            line.text = shadow.text = world.Hint;
            title.color = Color.Lerp(Color.white, world.Color, 0.35f);
            age = 0f;
            placed = false;
        }

        void LateUpdate()
        {
            if (root == null) return;
            age += Time.deltaTime;
            var view = Camera.main;
            var menu = WorldMenu.Instance;
            bool show = age < showSeconds && view != null && (menu == null || !menu.IsOpen);
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;

            // Follow the view loosely, so it stays readable while you look around.
            var forward = Effects.FlatForward(view);
            var target = view.transform.position + forward * distance + Vector3.down * drop;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            float follow = placed ? 1f - Mathf.Exp(-3f * Time.deltaTime) : 1f;
            root.SetPositionAndRotation(Vector3.Lerp(root.position, target, follow), Quaternion.Slerp(root.rotation, rotation, follow));
            placed = true;

            float alpha = Mathf.Clamp01(Mathf.Min(age / 0.35f, (showSeconds - age) / 0.8f));
            title.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            line.transform.localPosition = new Vector3(0f, -0.012f, 0f);
            shadow.transform.localPosition = new Vector3(0.0008f, -0.0128f, 0.001f);
            var c = title.color;
            title.color = new Color(c.r, c.g, c.b, alpha);
            line.color = new Color(1f, 1f, 1f, alpha * 0.9f);
            shadow.color = new Color(0f, 0f, 0f, alpha * 0.5f);
        }
    }
}
