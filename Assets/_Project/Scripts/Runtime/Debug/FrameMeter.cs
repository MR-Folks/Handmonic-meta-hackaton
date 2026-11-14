using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Measures the frame rate of each world on the headset, and of each set of worlds that are on together, and logs
    /// it with a "[Perf]" prefix: every few seconds while they run, and a summary when they change. Run the demo tour
    /// (see DemoTour) and read "adb logcat -s Unity | grep Perf" to get every world's numbers in one go. The first
    /// second and a half after a change is skipped, while the worlds build and settle.
    /// </summary>
    public class FrameMeter : MonoBehaviour
    {
        [SerializeField] float interval = 4f;
        [SerializeField] float settle = 1.5f;

        readonly List<float> frames = new List<float>(2048);
        int version = -1;
        string label = "none";
        float since, next;

        void Start()
        {
            Debug.Log($"[Perf] display {Screen.currentResolution.refreshRateRatio.value:F0} Hz, target {Application.targetFrameRate}");
        }

        void Update()
        {
            var manager = WorldManager.Instance;
            int now = manager != null ? manager.Version : -1;
            if (now != version)
            {
                Report("left", true);
                version = now;
                label = Names(manager);
                since = 0f;
                next = settle + interval;
            }
            since += Time.unscaledDeltaTime;
            if (since < settle) return;
            frames.Add(Time.unscaledDeltaTime);
            if (since >= next)
            {
                next += interval;
                Report("now", false);
            }
        }

        /// <summary>The worlds that are on, as "Flower dance + Solar system".</summary>
        static string Names(WorldManager manager)
        {
            if (manager == null || manager.Active.Count == 0) return "none";
            var names = new System.Text.StringBuilder();
            foreach (int index in manager.Active)
            {
                if (names.Length > 0) names.Append(" + ");
                names.Append(manager.Worlds[index].DisplayName);
            }
            return names.ToString();
        }

        /// <summary>Average and 1%-low frame rate, and the slowest frame, since the worlds that are on changed.</summary>
        void Report(string when, bool reset)
        {
            if (frames.Count > 10)
            {
                float total = 0f, worst = 0f;
                foreach (float f in frames)
                {
                    total += f;
                    worst = Mathf.Max(worst, f);
                }
                var sorted = new List<float>(frames);
                sorted.Sort();
                float low = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.99f))];
                Debug.Log($"[Perf] {when} {label}: avg {frames.Count / total:F1} fps, 1% low {1f / low:F1} fps, worst {worst * 1000f:F0} ms, {frames.Count} frames");
            }
            if (reset) frames.Clear();
        }
    }
}
