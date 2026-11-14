using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// When a world starts, its name and a short line saying what to do float in front of you for a few seconds,
    /// following your view loosely, then fade. Hidden while the menu is open: the worlds turned on there wait their
    /// turn and show one after another once it closes. A world's hint goes as soon as the world is turned off, and one
    /// turned off before its turn is skipped.
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
        // The world whose hint is showing (or waiting for the menu to close), or -1.
        int shown = -1;
        // The worlds whose hints wait to be shown, oldest first.
        readonly int[] waiting = new int[4];
        int waitingCount;

        void OnEnable()
        {
            if (WorldManager.Instance != null) WorldManager.Instance.WorldStarted += OnWorld;
        }

        void OnDisable()
        {
            if (WorldManager.Instance != null) WorldManager.Instance.WorldStarted -= OnWorld;
        }

        void Start()
        {
            // The first world may have started before this subscribed.
            if (WorldManager.Instance != null)
            {
                WorldManager.Instance.WorldStarted -= OnWorld;
                WorldManager.Instance.WorldStarted += OnWorld;
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

        /// <summary>A world started: its hint waits its turn (a world started again moves to the back).</summary>
        void OnWorld(int index)
        {
            var manager = WorldManager.Instance;
            if (manager == null || index < 0 || index >= manager.Worlds.Count) return;
            var world = manager.Worlds[index];
            if (world == null || string.IsNullOrEmpty(world.Hint)) return;
            int kept = 0;
            for (int i = 0; i < waitingCount; i++)
                if (waiting[i] != index) waiting[kept++] = waiting[i];
            waitingCount = kept;
            if (waitingCount == waiting.Length)
            {
                for (int i = 1; i < waiting.Length; i++) waiting[i - 1] = waiting[i];
                waitingCount--;
            }
            waiting[waitingCount++] = index;
            // The hint showing gives way. Turned on with the menu closed (the start, the demo tour), it shows now;
            // otherwise as soon as the menu closes.
            age = showSeconds;
            var menu = WorldMenu.Instance;
            if (menu == null || !menu.IsOpen) ShowNext();
        }

        /// <summary>Shows the next waiting hint of a world that is still on, if there is one.</summary>
        bool ShowNext()
        {
            var manager = WorldManager.Instance;
            while (waitingCount > 0)
            {
                int index = waiting[0];
                for (int i = 1; i < waitingCount; i++) waiting[i - 1] = waiting[i];
                waitingCount--;
                if (manager == null || !manager.IsOn(index)) continue;
                var world = manager.Worlds[index];
                shown = index;
                Build();
                title.Text = world.DisplayName;
                line.Text = world.Hint;
                titleColor = Color.Lerp(Color.white, world.Color, 0.35f);
                age = 0f;
                placed = false;
                return true;
            }
            return false;
        }

        void LateUpdate()
        {
            var menu = WorldMenu.Instance;
            bool menuOpen = menu != null && menu.IsOpen;
            // A world turned off takes its hint with it.
            var manager = WorldManager.Instance;
            if (age < showSeconds && shown >= 0 && manager != null && !manager.IsOn(shown)) age = showSeconds;
            // The hint's time only runs while the menu is closed; when it runs out, the next one waiting shows.
            if (!menuOpen && (age < showSeconds || !ShowNext())) age += Time.deltaTime;
            if (root == null) return;
            var view = Camera.main;
            bool show = age < showSeconds && view != null && !menuOpen;
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
