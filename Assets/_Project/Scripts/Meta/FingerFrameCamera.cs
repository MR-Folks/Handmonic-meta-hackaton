using Meta.XR;
using UnityEngine;
using UnityEngine.Android;

namespace MusicInYourHands.Meta
{
    /// <summary>
    /// The headset's passthrough cameras for the Finger frame's camera filters, through Meta's PassthroughCameraAccess
    /// (MR Utility Kit). The Finger frame makes it the first time its window opens on a filter, and only then does it
    /// ask for the headset camera permission (horizonos.permission.HEADSET_CAMERA, the passthrough cameras only), never
    /// at app start. Once allowed it runs the left camera, and the right one too so each eye sees the room from the
    /// camera on its side, at a small resolution and only while the frame wants it. For each image it gives the frame
    /// the matrix that projects room points onto it, from the camera's lens and where the camera was when it took the
    /// image. Without camera access (a Quest 2 or Pro, a system older than Horizon OS v74, or the player says no) it
    /// reports Unavailable and the frame shows its other worlds; if the player allows it later in the system settings,
    /// it starts when they come back. The images stay on the GPU for the filters alone: nothing here reads, saves,
    /// sends or logs them.
    /// </summary>
    public class FingerFrameCamera : MonoBehaviour, FingerFrame.ICameraFeed
    {
        [Tooltip("The camera image size asked for: small, since the filters only fill the window.")]
        [SerializeField] Vector2Int resolution = new Vector2Int(640, 480);
        [Tooltip("Camera images per second at most. The room stays lined up between images, since each is projected from where the camera was.")]
        [SerializeField] int framerate = 30;
        [Tooltip("Run both cameras, so each eye sees the room from the camera on its side (truer depth); off, both eyes share the left one.")]
        [SerializeField] bool bothCameras = true;
        [Tooltip("Seconds a camera may take to show its first image before the filters give up on it.")]
        [SerializeField] float startTimeout = 8f;

        const string CameraPermission = OVRPermissionsRequester.PassthroughCameraAccessPermission;

        readonly PassthroughCameraAccess[] cameras = new PassthroughCameraAccess[2];
        // Whether this asked each camera to run, so a camera that stops by itself (it couldn't start) shows as failed,
        // and when each last showed an image (or was started, or the app came back), for one that never does.
        readonly bool[] running = new bool[2];
        readonly float[] lastSeen = new float[2];
        // Each camera's lens as a projection: u = x/z * k.x + k.z and v = y/z * k.y + k.w, for the resolution it runs at.
        readonly Vector4[] lens = new Vector4[2];
        readonly Vector2Int[] lensFor = new Vector2Int[2];

        FingerFrame.CameraStatus status = FingerFrame.CameraStatus.Off;
        bool wanted, refusedBefore;
        volatile bool granted, refused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
#if !UNITY_EDITOR
            FingerFrame.MakeCameraFeed = parent =>
            {
                var go = new GameObject("Finger Frame Camera");
                go.transform.SetParent(parent, false);
                return go.AddComponent<FingerFrameCamera>();
            };
#endif
        }

        public bool Wanted { set => wanted = value; }

        public FingerFrame.CameraStatus Status => status;

        void Update()
        {
            switch (status)
            {
                case FingerFrame.CameraStatus.Off:
                    if (wanted) Ask();
                    return;
                case FingerFrame.CameraStatus.Asking:
                    if (granted) MakeCameras();
                    else if (refused) GiveUp(refusedBefore ? null : "the player didn't allow the headset camera");
                    return;
                case FingerFrame.CameraStatus.Unavailable:
                    return;
            }

            for (int i = 0; i < 2; i++)
            {
                var camera = cameras[i];
                if (camera == null) continue;
                // A camera that turned itself off while asked to run couldn't start: without the left one there are no
                // filters; without the right one both eyes share the left.
                if (camera.IsPlaying) lastSeen[i] = Time.unscaledTime;
                bool failed = running[i] && (!camera.enabled || Time.unscaledTime - lastSeen[i] > startTimeout);
                if (failed)
                {
                    Debug.Log($"[Frame] The {(i == 0 ? "left" : "right")} headset camera didn't start.");
                    Drop(i);
                    if (i == 0)
                    {
                        Drop(1);
                        GiveUp("the headset camera didn't start");
                        return;
                    }
                    continue;
                }
                if (running[i] == wanted) continue;
                running[i] = wanted;
                lastSeen[i] = Time.unscaledTime;
                camera.enabled = wanted;
            }
            status = cameras[0] != null && cameras[0].enabled && cameras[0].IsPlaying
                ? FingerFrame.CameraStatus.Running
                : FingerFrame.CameraStatus.Starting;
        }

        /// <summary>The first time the frame wants a camera: check the headset can, and ask for the permission.</summary>
        void Ask()
        {
            bool supported;
            try
            {
                supported = PassthroughCameraAccess.IsSupported;
            }
            catch (System.Exception)
            {
                supported = false;
            }
            if (!supported)
            {
                GiveUp("this headset has no passthrough camera access (it needs a Quest 3 or 3S with Horizon OS v74 or later)");
                return;
            }
            status = FingerFrame.CameraStatus.Asking;
            if (Permission.HasUserAuthorizedPermission(CameraPermission))
            {
                granted = true;
                return;
            }
            Debug.Log("[Frame] Asking for the headset camera permission, for the camera filters.");
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => granted = true;
            callbacks.PermissionDenied += _ => refused = true;
            callbacks.PermissionRequestDismissed += _ => refused = true;
            Permission.RequestUserPermission(CameraPermission, callbacks);
        }

        void GiveUp(string why)
        {
            if (why != null) Debug.Log($"[Frame] No camera filters: {why}. The frame shows its worlds.");
            status = FingerFrame.CameraStatus.Unavailable;
            refusedBefore |= refused;
        }

        // The cameras start again after the world or the app was away: give them their time to show an image.
        void OnEnable() => Restarted();

        void OnApplicationPause(bool paused)
        {
            if (!paused) Restarted();
        }

        void Restarted()
        {
            for (int i = 0; i < 2; i++) lastSeen[i] = Time.unscaledTime;
        }

        // The player may allow the camera in the system's settings and come back.
        void OnApplicationFocus(bool focused)
        {
            if (focused) Restarted();
            if (!focused || status != FingerFrame.CameraStatus.Unavailable || !refusedBefore) return;
            if (!Permission.HasUserAuthorizedPermission(CameraPermission)) return;
            Debug.Log("[Frame] The headset camera is allowed now; the camera filters are back.");
            refused = false;
            granted = true;
            status = FingerFrame.CameraStatus.Asking;
        }

        /// <summary>
        /// The cameras, each on a child of its own, set up while switched off (the resolution and frame rate can only be
        /// set then); Update switches them on while the frame wants them.
        /// </summary>
        void MakeCameras()
        {
            Debug.Log("[Frame] Headset camera allowed; starting it for the camera filters.");
            for (int i = 0; i < (bothCameras ? 2 : 1); i++)
            {
                var go = new GameObject(i == 0 ? "Left Camera" : "Right Camera");
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                var camera = go.AddComponent<PassthroughCameraAccess>();
                camera.enabled = false;
                camera.CameraPosition = i == 0 ? PassthroughCameraAccess.CameraPositionType.Left : PassthroughCameraAccess.CameraPositionType.Right;
                camera.RequestedResolution = resolution;
                camera.MaxFramerate = framerate;
                go.SetActive(true);
                cameras[i] = camera;
                running[i] = false;
                lensFor[i] = Vector2Int.zero;
            }
            status = FingerFrame.CameraStatus.Starting;
        }

        void Drop(int i)
        {
            if (cameras[i] != null) Destroy(cameras[i].gameObject);
            cameras[i] = null;
            running[i] = false;
        }

        public bool TryGetImage(int eye, out Texture image, out Matrix4x4 worldToImage)
        {
            image = null;
            worldToImage = Matrix4x4.identity;
            int i = eye == 1 && Playing(1) ? 1 : 0;
            if (status != FingerFrame.CameraStatus.Running || !Playing(i)) return false;
            var camera = cameras[i];
            if (lensFor[i] != camera.CurrentResolution)
            {
                lens[i] = Lens(camera);
                lensFor[i] = camera.CurrentResolution;
            }
            // Where the camera was when it took this image, so the room stays put however the head has moved since.
            var pose = camera.GetCameraPose();
            var k = lens[i];
            var project = Matrix4x4.identity;
            project.m00 = k.x;
            project.m02 = k.z;
            project.m11 = k.y;
            project.m12 = k.w;
            worldToImage = project * Matrix4x4.Rotate(Quaternion.Inverse(pose.rotation)) * Matrix4x4.Translate(-pose.position);
            image = camera.GetTexture();
            return image != null;
        }

        bool Playing(int i) => cameras[i] != null && cameras[i].enabled && cameras[i].IsPlaying;

        /// <summary>
        /// The camera's lens, from its own pinhole model: the rays through the image's bottom-left and top-right
        /// corners, in the camera's frame, give how x/z and y/z map onto the image (principal point and focal length,
        /// with the crop for the resolution it runs at).
        /// </summary>
        static Vector4 Lens(PassthroughCameraAccess camera)
        {
            var a = camera.ViewportPointToRay(Vector2.zero, Pose.identity).direction;
            var b = camera.ViewportPointToRay(Vector2.one, Pose.identity).direction;
            float x0 = a.x / a.z, y0 = a.y / a.z, x1 = b.x / b.z, y1 = b.y / b.z;
            float kx = 1f / (x1 - x0), ky = 1f / (y1 - y0);
            return new Vector4(kx, ky, -x0 * kx, -y0 * ky);
        }
    }
}
