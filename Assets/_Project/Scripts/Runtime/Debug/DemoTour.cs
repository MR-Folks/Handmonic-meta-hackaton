using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// A hands-free tour for screenshots and demos, started from the computer:
    ///   adb shell am start -S -n com.musicinyourhands.app/com.unity3d.player.UnityPlayerGameActivity --ez tour true
    /// It visits every world for a while (each world shows its own hint on what to do), then opens the menu. Each step is
    /// logged as "[Tour] index name", so a script can take screenshots at the right moments. Without the extra, the
    /// app starts normally; "--ei world N" starts in world N instead.
    /// </summary>
    [DefaultExecutionOrder(180)]
    public class DemoTour : MonoBehaviour
    {
        [SerializeField] float secondsPerWorld = 16f;

        bool touring;
        int step = -1;
        float stepTime;

        void Start()
        {
            bool asked = ReadIntent("world", out int world);
            touring = ReadFlag("tour");
            // A world asked for, or the tour, holds its worlds: Journey mode would bring in worlds of its own.
            if ((asked || touring) && JourneyMode.Instance != null) JourneyMode.Instance.Stop();
            if (asked && WorldManager.Instance != null) WorldManager.Instance.Switch(world);
            if (touring) Next();
        }

        void Next()
        {
            var manager = WorldManager.Instance;
            int worlds = manager != null ? manager.Worlds.Count : 0;
            step++;
            stepTime = 0f;
            if (step < worlds)
            {
                manager.Switch(step);
                Debug.Log($"[Tour] {step} {manager.Worlds[step].DisplayName}");
            }
            else if (step == worlds)
            {
                var menu = FindAnyObjectByType<WorldMenu>();
                if (menu != null) menu.Open(Camera.main);
                Debug.Log($"[Tour] {step} Menu");
            }
            else
            {
                touring = false;
                Debug.Log("[Tour] done");
            }
        }

        void Update()
        {
            if (!touring) return;
            stepTime += Time.deltaTime;
            if (stepTime >= secondsPerWorld) Next();
        }

        static bool ReadFlag(string name)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var intent = Intent())
                return intent != null && intent.Call<bool>("getBooleanExtra", name, false);
#else
            return false;
#endif
        }

        static bool ReadIntent(string name, out int value)
        {
            value = -1;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var intent = Intent())
            {
                if (intent == null || !intent.Call<bool>("hasExtra", name)) return false;
                value = intent.Call<int>("getIntExtra", name, -1);
                return value >= 0;
            }
#else
            return false;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject Intent()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                return activity?.Call<AndroidJavaObject>("getIntent");
        }
#endif
    }
}
