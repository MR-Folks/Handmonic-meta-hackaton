using Meta.XR.EnvironmentDepth;
using UnityEngine;

namespace MusicInYourHands.Meta
{
    /// <summary>
    /// Hides the worlds' things behind the real room, with Meta's Depth API. While a world that is hidden by the real
    /// room is on (RoomDepth.Wanted), it turns on the EnvironmentDepthManager beside it: the headset then senses the
    /// room's depth every frame, and once the first depth map arrives the manager switches on the occlusion shader
    /// keyword, so those worlds' shaders hide whatever is behind real furniture, people and your real hands. For every
    /// other world it turns the depth sensing off again, and the keyword with it, which saves the GPU and the battery.
    /// Your real hands stay in the depth map, so they pass in front of the bubbles, planets and strings.
    /// Nothing changes where there is no depth sensing (Quest 2, the editor) or until the spatial data permission is
    /// given (RoomScanFeed asks for it at startup; the manager waits for it).
    /// </summary>
    [RequireComponent(typeof(EnvironmentDepthManager))]
    public class RoomDepthFeed : MonoBehaviour
    {
        [Tooltip("How the edges of real things cut into the worlds. Soft Occlusion: smooth, feathered edges that hide the " +
                 "depth map's coarseness and shimmer, for a little more GPU (a pass over the small depth map whenever it " +
                 "updates). Hard Occlusion: a little cheaper, but jagged edges that flicker, most of all round the hands. " +
                 "None: nothing hides behind real things, and the depth sensing stays off.")]
        [SerializeField] OcclusionShadersMode edges = OcclusionShadersMode.SoftOcclusion;
        [Tooltip("Log when the depth sensing starts and stops and when the first depth map arrives (adb logcat -s Unity).")]
        [SerializeField] bool logDepth = true;

        EnvironmentDepthManager depth;
        bool supported, toldAvailable;

        void Awake()
        {
            depth = GetComponent<EnvironmentDepthManager>();
            // Real hands stay in the depth map: they hide what is behind them, as the rest of the room does.
            depth.RemoveHands = false;
            depth.OcclusionShadersMode = edges;
            depth.enabled = false;
            supported = EnvironmentDepthManager.IsSupported;
            if (logDepth && !supported && Application.platform == RuntimePlatform.Android)
                Debug.Log("[Depth] This headset can't sense the room's depth, so nothing hides behind real things.");
        }

        void Update()
        {
            bool want = RoomDepth.Wanted && supported && edges != OcclusionShadersMode.None;
            if (want != depth.enabled)
            {
                depth.OcclusionShadersMode = edges;
                depth.enabled = want;
                toldAvailable = false;
                if (logDepth) Debug.Log(want ? $"[Depth] Room depth on ({edges})." : "[Depth] Room depth off.");
            }
            if (want && logDepth && !toldAvailable && depth.IsDepthAvailable)
            {
                toldAvailable = true;
                Debug.Log("[Depth] First depth map in: real things now hide the world behind them.");
            }
        }
    }
}
