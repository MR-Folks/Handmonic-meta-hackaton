using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// A hands-free tour for screenshots and demos, started from the computer:
    ///   adb shell am start -S -n com.musicinyourhands.app/com.unity3d.player.UnityPlayerGameActivity --ez tour true
    /// It visits every world for a while (each world shows its own hint on what to do), then opens the menu, then, with
    /// "--ez combos true" as well, visits sets of worlds that can be on together (as many as the menu allows at once),
    /// so FrameMeter logs their frame rate too. Each step is logged as "[Tour] index name", so a script can take
    /// screenshots at the right moments. Without the extra, the app starts normally; "--ei world N" starts in world N
    /// instead.
    /// </summary>
    [DefaultExecutionOrder(180)]
    public class DemoTour : MonoBehaviour
    {
        [SerializeField] float secondsPerWorld = 16f;

        bool touring;
        int step = -1;
        float stepTime;
        readonly List<int[]> combos = new List<int[]>();

        void Start()
        {
            if (ReadIntent("world", out int world) && WorldManager.Instance != null) WorldManager.Instance.Switch(world);
            touring = ReadFlag("tour");
            if (touring && ReadFlag("combos")) FindCombos(WorldManager.Instance);
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
            else if (step - worlds - 1 < combos.Count)
            {
                var menu = FindAnyObjectByType<WorldMenu>();
                if (menu != null && menu.IsOpen) menu.Close();
                var set = combos[step - worlds - 1];
                manager.Switch(set[0]);
                var names = manager.Worlds[set[0]].DisplayName;
                for (int i = 1; i < set.Length; i++)
                {
                    manager.Choose(set[i]);
                    names += " + " + manager.Worlds[set[i]].DisplayName;
                }
                Debug.Log($"[Tour] {step} {names}");
            }
            else
            {
                touring = false;
                Debug.Log("[Tour] done");
            }
        }

        /// <summary>
        /// Sets of worlds that can be on together: from each world in menu order, the next worlds that fit beside it,
        /// up to the most the menu allows at once. Each set once.
        /// </summary>
        void FindCombos(WorldManager manager)
        {
            combos.Clear();
            if (manager == null) return;
            var seen = new HashSet<string>();
            var set = new List<int>();
            int count = manager.Worlds.Count;
            for (int seed = 0; seed < count; seed++)
            {
                set.Clear();
                set.Add(seed);
                for (int k = 1; k < count && set.Count < manager.MaxWorlds; k++)
                {
                    int next = (seed + k) % count;
                    if (set.TrueForAll(i => manager.Worlds[i].CombinesWith(manager.Worlds[next]))) set.Add(next);
                }
                if (set.Count < 2) continue;
                var sorted = new List<int>(set);
                sorted.Sort();
                if (seen.Add(string.Join(",", sorted))) combos.Add(set.ToArray());
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
