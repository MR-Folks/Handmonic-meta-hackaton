using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace MusicInYourHands.Meta
{
    /// <summary>
    /// Loads the headset's room scan with Meta's MR Utility Kit and hands the room you are in to the worlds (Room):
    /// floor, ceiling, walls, tables, and the tops of sofas, beds and shelves. Asks for the spatial data permission
    /// first. Never starts Space Setup by itself: without a scan the worlds keep their stand-in room. Checks every
    /// few seconds which room you are in, for homes scanned room by room.
    /// </summary>
    [RequireComponent(typeof(MRUK))]
    public class RoomScanFeed : MonoBehaviour
    {
        const MRUKAnchor.SceneLabels TopLabels = MRUKAnchor.SceneLabels.COUCH | MRUKAnchor.SceneLabels.BED |
                                                 MRUKAnchor.SceneLabels.STORAGE | MRUKAnchor.SceneLabels.OTHER;

        MRUK mruk;
        MRUKRoom shown;
        bool loading;
        float checkTimer;

        void Awake()
        {
            mruk = GetComponent<MRUK>();
            // The worlds place themselves around you; MRUK must not move the tracking space under them.
            mruk.EnableWorldLock = false;
            if (mruk.SceneSettings != null) mruk.SceneSettings.LoadSceneOnStartup = false;
        }

        void OnEnable()
        {
            mruk.RoomUpdatedEvent.AddListener(OnRoomChanged);
            mruk.RoomRemovedEvent.AddListener(OnRoomChanged);
        }

        void OnDisable()
        {
            mruk.RoomUpdatedEvent.RemoveListener(OnRoomChanged);
            mruk.RoomRemovedEvent.RemoveListener(OnRoomChanged);
        }

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => Load();
                callbacks.PermissionDenied += _ => Debug.Log("[Room] Spatial data permission denied; using the stand-in room.");
                Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission, callbacks);
                return;
            }
#endif
            Load();
        }

        async void Load()
        {
            if (loading) return;
            loading = true;
            var result = await mruk.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound: false);
            loading = false;
            Debug.Log($"[Room] Scan load: {result}, {mruk.Rooms.Count} room(s).");
            Refresh();
        }

        void OnRoomChanged(MRUKRoom room)
        {
            shown = null;
            Refresh();
        }

        void Update()
        {
            checkTimer -= Time.unscaledDeltaTime;
            if (checkTimer > 0f || loading) return;
            checkTimer = 3f;
            if (mruk.IsInitialized && mruk.GetCurrentRoom() != shown) Refresh();
        }

        void Refresh()
        {
            var room = mruk.IsInitialized && mruk.Rooms.Count > 0 ? mruk.GetCurrentRoom() : null;
            shown = room;
            if (room == null || room.FloorAnchor == null)
            {
                Room.Clear();
                return;
            }

            float floor = room.FloorAnchor.transform.position.y;
            float ceiling = room.CeilingAnchor != null ? room.CeilingAnchor.transform.position.y : floor + 2.5f;
            var walls = new List<Room.Surface>();
            var tables = new List<Room.Surface>();
            var tops = new List<Room.Surface>();
            foreach (var anchor in room.Anchors)
            {
                if (anchor.HasAnyLabel(MRUKAnchor.SceneLabels.WALL_FACE) && anchor.PlaneRect.HasValue)
                {
                    var rect = anchor.PlaneRect.Value;
                    var t = anchor.transform;
                    walls.Add(new Room.Surface(t.TransformPoint(rect.center), t.forward, t.right, rect.size));
                }
                else if (anchor.HasAnyLabel(MRUKAnchor.SceneLabels.TABLE))
                {
                    if (TopOf(anchor, out var top)) tables.Add(top);
                }
                else if (anchor.HasAnyLabel(TopLabels))
                {
                    if (TopOf(anchor, out var top)) tops.Add(top);
                }
            }
            Room.Set(floor, ceiling, walls, tables, tops);
            Debug.Log($"[Room] Floor {floor:F2} m, ceiling {ceiling:F2} m, {walls.Count} walls, {tables.Count} tables, {tops.Count} other tops.");
        }

        /// <summary>The upward-facing top of a piece of furniture, from its volume or else its plane.</summary>
        static bool TopOf(MRUKAnchor anchor, out Room.Surface top)
        {
            top = default;
            var t = anchor.transform;
            if (anchor.VolumeBounds.HasValue)
            {
                var b = anchor.VolumeBounds.Value;
                // Whichever local axis points most nearly up is the height; the other two span the top.
                var axes = new[] { t.right, t.up, t.forward };
                int vertical = 0;
                for (int i = 1; i < 3; i++)
                    if (Mathf.Abs(axes[i].y) > Mathf.Abs(axes[vertical].y)) vertical = i;
                int a = (vertical + 1) % 3, c = (vertical + 2) % 3;
                var center = t.TransformPoint(b.center);
                float height = Mathf.Abs(Vector3.Scale(b.size, t.lossyScale)[vertical] * axes[vertical].y);
                center.y += height * 0.5f;
                var right = Vector3.ProjectOnPlane(axes[a], Vector3.up);
                if (right.sqrMagnitude < 1e-4f) return false;
                top = new Room.Surface(center, Vector3.up, right, new Vector2(b.size[a] * t.lossyScale[a], b.size[c] * t.lossyScale[c]));
                return true;
            }
            if (anchor.PlaneRect.HasValue && t.forward.y > 0.7f)
            {
                var rect = anchor.PlaneRect.Value;
                top = new Room.Surface(t.TransformPoint(rect.center), Vector3.up, t.right, rect.size);
                return true;
            }
            return false;
        }
    }
}
