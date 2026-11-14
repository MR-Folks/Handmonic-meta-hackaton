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

        TypeLabel title, line;
        Color titleColor;
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
            root = new GameObject("Hint").transform;
            root.SetParent(transform, false);
            title = new TypeLabel("Title", root, 0.03f);
            line = new TypeLabel("Line", root, 0.019f);
            root.gameObject.SetActive(false);
        }

        void OnWorld(int index)
        {
            var manager = WorldManager.Instance;
            if (manager == null || index < 0 || index >= manager.Worlds.Count) return;
            var world = manager.Worlds[index];
            if (world == null || string.IsNullOrEmpty(world.Hint)) return;
            Build();
            title.Text = world.DisplayName;
            line.Text = world.Hint;
            titleColor = Color.Lerp(Color.white, world.Color, 0.35f);
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
            title.Transform.localPosition = new Vector3(0f, 0.03f, 0f);
            line.Transform.localPosition = new Vector3(0f, -0.012f, 0f);
            title.Color = new Color(titleColor.r, titleColor.g, titleColor.b, alpha);
            line.Color = new Color(1f, 1f, 1f, alpha * 0.95f);
        }
    }
}
