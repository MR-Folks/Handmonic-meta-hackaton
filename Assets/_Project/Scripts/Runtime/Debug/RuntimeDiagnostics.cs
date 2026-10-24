using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Logs the camera pose, hand palms and audio state every few seconds with a "[Diag]" prefix,
    /// so device problems can be read with "adb logcat -s Unity".
    /// </summary>
    public class RuntimeDiagnostics : MonoBehaviour
    {
        [SerializeField] float interval = 2f;

        float next;

        static string Fmt(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + interval;

            var cam = Camera.main;
            string camInfo = cam == null
                ? "none"
                : $"{Fmt(cam.transform.position)} local {Fmt(cam.transform.localPosition)} yaw {cam.transform.eulerAngles.y:F0} pitch {cam.transform.eulerAngles.x:F0}";

            var bus = MusicBus.Instance;
            var source = bus != null ? bus.GetComponent<AudioSource>() : null;
            string audio = source == null
                ? "no source"
                : $"{(source.clip != null ? $"{source.clip.name} ({source.clip.loadState})" : "no clip")} playing={source.isPlaying} t={source.time:F1} vol={source.volume:F2} mute={source.mute}";

            var rig = HandRig.Instance;
            string left = rig != null && rig.Left.IsTracked ? Fmt(rig.Left.Palm.position) : "-";
            string right = rig != null && rig.Right.IsTracked ? Fmt(rig.Right.Palm.position) : "-";

            int listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
            Debug.Log($"[Diag] cam={camInfo} | listeners={listeners} listenerVol={AudioListener.volume:F2} paused={AudioListener.pause} | audio={audio} | palmL={left} palmR={right} | source={(bus != null ? bus.Source.ToString() : "-")} mic={(bus != null ? bus.InputLevel : 0f):F4} | bass={(bus != null ? bus.Bass : 0f):F2}");
        }
    }
}
