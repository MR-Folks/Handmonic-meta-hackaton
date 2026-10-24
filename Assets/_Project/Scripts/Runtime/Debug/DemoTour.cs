using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// A hands-free tour for screenshots and demos, started from the computer:
    ///   adb shell am start -S -n com.musicinyourhands.app/com.unity3d.player.UnityPlayerGameActivity --ez tour true
    /// It visits every world for a while, showing what to do with your hands, then opens the menu. Each step is
    /// logged as "[Tour] index name", so a script can take screenshots at the right moments. Without the extra, the
    /// app starts normally; "--ei world N" starts in world N instead.
    /// </summary>
    [DefaultExecutionOrder(180)]
    public class DemoTour : MonoBehaviour
    {
        [SerializeField] float secondsPerWorld = 16f;
        [SerializeField] float hintSeconds = 5f;

        static readonly string[] Hints =
        {
            "Hold both hands up\nand spread your fingers",
            "Hold your palms facing each other,\nthen twist them like a ball",
            "Pinch with both hands\nand pull them apart",
            "Push a fingertip into a string,\npluck when a bead reaches the ring",
            "Hold a palm out\nunder the falling notes",
            "Hold out an open hand\nand let the fireflies land",
            "Dip your hand into the pool\nand listen",
            "Hold your hand in the wind\nand spread your fingers",
            "Pop the bubbles\nwith a fingertip",
            "Hover a hand over the plate\nand slide it across",
            "Push the letters, flick a ring,\nmake a fist to pull the word in",
            "Point at a wall\nand pinch to spray",
            "The world menu"
        };

        TextMesh hint;
        bool touring;
        int step = -1;
        float stepTime;

        void Start()
        {
            if (ReadIntent("world", out int world) && WorldManager.Instance != null) WorldManager.Instance.Switch(world);
            touring = ReadFlag("tour");
            if (!touring) return;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Tour Hint");
            go.transform.SetParent(transform, false);
            hint = go.AddComponent<TextMesh>();
            hint.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            hint.fontSize = 56;
            hint.characterSize = 0.0028f;
            hint.anchor = TextAnchor.MiddleCenter;
            hint.alignment = TextAlignment.Center;
            Next();
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
                hint.gameObject.SetActive(false);
                Debug.Log("[Tour] done");
            }
        }

        void Update()
        {
            if (!touring) return;
            stepTime += Time.deltaTime;
            if (stepTime >= secondsPerWorld)
            {
                Next();
                if (!touring) return;
            }

            // The hint floats in front of the player for the first seconds of each step, then fades away.
            var view = Camera.main;
            if (view == null) return;
            var forward = Effects.FlatForward(view);
            hint.transform.position = view.transform.position + forward * 0.6f + Vector3.up * 0.12f;
            hint.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            hint.text = Hints[Mathf.Min(step, Hints.Length - 1)];
            float alpha = Mathf.Clamp01(Mathf.Min(stepTime / 0.4f, (hintSeconds - stepTime) / 0.6f));
            hint.color = new Color(1f, 1f, 1f, alpha);
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
