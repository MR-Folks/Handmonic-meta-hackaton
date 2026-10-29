using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Measures the frame rate of each world on the headset and logs it with a "[Perf]" prefix: every few seconds
    /// while a world runs, and a summary when you leave it. Run the demo tour (see DemoTour) and read
    /// "adb logcat -s Unity | grep Perf" to get every world's numbers in one go. The first second and a half after a
    /// switch is skipped, while the world builds and settles.
    /// </summary>
    public class FrameMeter : MonoBehaviour
    {
        [SerializeField] float interval = 4f;
        [SerializeField] float settle = 1.5f;

        readonly List<float> frames = new List<float>(2048);
        int world = -1;
        float since, next;

        void Start()
        {
            Debug.Log($"[Perf] display {Screen.currentResolution.refreshRateRatio.value:F0} Hz, target {Application.targetFrameRate}");
        }

        void Update()
        {
            var manager = WorldManager.Instance;
            int current = manager != null ? manager.Current : -1;
            if (current != world)
            {
                Report("left", true);
                world = current;
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

        /// <summary>Average and 1%-low frame rate, and the slowest frame, since the world started.</summary>
        void Report(string when, bool reset)
        {
            if (frames.Count > 10)
            {
                var manager = WorldManager.Instance;
                string name = manager != null && world >= 0 && world < manager.Worlds.Count ? manager.Worlds[world].DisplayName : "none";
                float total = 0f, worst = 0f;
                foreach (float f in frames)
                {
                    total += f;
                    worst = Mathf.Max(worst, f);
                }
                var sorted = new List<float>(frames);
                sorted.Sort();
                float low = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.99f))];
                Debug.Log($"[Perf] {when} {name}: avg {frames.Count / total:F1} fps, 1% low {1f / low:F1} fps, worst {worst * 1000f:F0} ms, {frames.Count} frames");
            }
            if (reset) frames.Clear();
        }
    }
}
