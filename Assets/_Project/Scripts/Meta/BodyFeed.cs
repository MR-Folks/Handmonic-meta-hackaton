using UnityEngine;

namespace MusicInYourHands.Meta
{
    /// <summary>
    /// Feeds Meta's body tracking into BodyRig, but only while a world wants the body (BodyRig.Wanted): then it turns
    /// on an OVRBody for the upper body at high fidelity (on Quest 3 that is Inside-Out Body Tracking, which follows the
    /// real arms and elbows with the headset's cameras) and copies the joints into BodyRig each frame, in world space.
    /// When nothing wants the body it turns OVRBody off again, so body tracking costs the other worlds nothing.
    /// The first time the body is wanted it asks for Meta's body tracking permission (never at app start). If the player
    /// says no, or the headset can't track bodies, BodyRig simply stays untracked and the worlds use the hands alone.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class BodyFeed : MonoBehaviour
    {
        [Tooltip("Ask for high fidelity: on Quest 3, Inside-Out Body Tracking, which tracks the real arms and elbows " +
                 "with the headset's cameras. Off, the body is estimated from the head and hands.")]
        [SerializeField] bool highFidelity = true;
        [Tooltip("If body tracking won't start, how many times to try again while a world wants it, and how many " +
                 "seconds apart.")]
        [SerializeField] int startAttempts = 3;
        [SerializeField] float retrySeconds = 5f;
        [Tooltip("Log when body tracking starts and stops, why it can't, and every few seconds how well it tracks, to " +
                 "see on the headset (adb logcat -s Unity).")]
        [SerializeField] bool logBody = true;
        [SerializeField] float reportSeconds = 10f;

        const OVRPermissionsRequester.Permission Permission = OVRPermissionsRequester.Permission.BodyTracking;
        static readonly OVRPermissionsRequester.Permission[] Permissions = { Permission };

        OVRBody body;
        Transform trackingSpace;
        bool wasWanted, asked, granted, unsupported, toldUnsupported;
        int attempts;
        float nextAttempt, nextLookup, nextReport;
        int trackedFrames, lostFrames;
        float confidenceSum;

        void OnEnable() => OVRPermissionsRequester.PermissionGranted += OnPermissionGranted;

        void OnDisable()
        {
            OVRPermissionsRequester.PermissionGranted -= OnPermissionGranted;
            Stop();
        }

        void Update()
        {
            if (!BodyRig.Wanted)
            {
                if (wasWanted) Stop();
                return;
            }
            if (!wasWanted) Begin();

            float dt = Time.deltaTime;
            if (!granted || !Running() || !Read(dt))
            {
                BodyRig.Lose(dt);
                lostFrames++;
            }
            if (logBody && Time.unscaledTime >= nextReport) Report();
        }

        /// <summary>A world started wanting the body: check the permission, asking for it the first time.</summary>
        void Begin()
        {
            wasWanted = true;
            attempts = 0;
            nextAttempt = 0f;
            nextReport = Time.unscaledTime + reportSeconds;
            trackedFrames = lostFrames = 0;
            confidenceSum = 0f;
            unsupported = !OVRPlugin.bodyTrackingSupported;
            if (unsupported)
            {
                if (logBody && !toldUnsupported) Debug.Log("[Body] This headset can't track the body; the worlds use the hands alone.");
                toldUnsupported = true;
                return;
            }
            granted = OVRPermissionsRequester.IsPermissionGranted(Permission);
            if (granted || asked) return;
            asked = true;
            if (logBody) Debug.Log("[Body] Asking for the body tracking permission.");
            OVRPermissionsRequester.Request(Permissions);
        }

        void OnPermissionGranted(string permissionId)
        {
            if (permissionId != OVRPermissionsRequester.BodyTrackingPermission) return;
            granted = true;
            nextAttempt = 0f;
            if (logBody) Debug.Log("[Body] Body tracking permission granted.");
        }

        // The player may grant it in the system's settings and come back.
        void OnApplicationFocus(bool focused)
        {
            if (focused && wasWanted && !granted && !unsupported) granted = OVRPermissionsRequester.IsPermissionGranted(Permission);
        }

        /// <summary>
        /// True while OVRBody is running; otherwise starts it, a few times at most. OVRBody starts body tracking when it
        /// is enabled and turns itself off again (with a warning of its own) if it can't.
        /// </summary>
        bool Running()
        {
            if (body != null && body.enabled) return true;
            if (attempts >= startAttempts || Time.unscaledTime < nextAttempt) return false;
            attempts++;
            nextAttempt = Time.unscaledTime + retrySeconds;
            if (body == null) MakeBody();
            if (highFidelity) OVRBody.Fidelity = OVRPlugin.BodyTrackingFidelity2.High;
            body.enabled = true;
            if (logBody)
                Debug.Log(body.enabled ? $"[Body] Body tracking started ({(highFidelity ? "high" : "low")} fidelity, upper body)."
                    : $"[Body] Body tracking didn't start (try {attempts} of {startAttempts}).");
            return body.enabled;
        }

        /// <summary>The OVRBody, made off on a child of this object: it runs only while enabled.</summary>
        void MakeBody()
        {
            var go = new GameObject("Body Tracking");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            body = go.AddComponent<OVRBody>();
            body.ProvidedSkeletonType = OVRPlugin.BodyJointSet.UpperBody;
            body.enabled = false;
            go.SetActive(true);
        }

        /// <summary>Copies this frame's joints into BodyRig, in world space. False when there is no body this frame.</summary>
        bool Read(float dt)
        {
            var state = body.BodyState;
            if (!state.HasValue) return false;
            if (trackingSpace == null && !FindTrackingSpace()) return false;
            var s = state.GetValueOrDefault();
            var joints = s.JointLocations;
            if (joints == null || joints.Length < (int)OVRPlugin.BoneId.Body_End) return false;

            bool spine = Joint(joints, OVRPlugin.BoneId.Body_Chest, out var chest);
            spine &= Joint(joints, OVRPlugin.BoneId.Body_Neck, out var neck);
            spine &= Joint(joints, OVRPlugin.BoneId.Body_Head, out var head);
            BodyRig.SetSpine(spine, chest, neck, head);
            ReadArm(joints, true, OVRPlugin.BoneId.Body_LeftArmUpper, OVRPlugin.BoneId.Body_LeftArmLower, OVRPlugin.BoneId.Body_LeftHandWrist);
            ReadArm(joints, false, OVRPlugin.BoneId.Body_RightArmUpper, OVRPlugin.BoneId.Body_RightArmLower, OVRPlugin.BoneId.Body_RightHandWrist);
            BodyRig.Push(s.Confidence, dt);
            trackedFrames++;
            confidenceSum += s.Confidence;
            return true;
        }

        void ReadArm(OVRPlugin.BodyJointLocation[] joints, bool left, OVRPlugin.BoneId shoulder, OVRPlugin.BoneId elbow, OVRPlugin.BoneId wrist)
        {
            bool valid = Joint(joints, shoulder, out var s);
            valid &= Joint(joints, elbow, out var e);
            valid &= Joint(joints, wrist, out var w);
            BodyRig.SetArm(left, valid, s, e, w);
        }

        /// <summary>A joint's position in world space. Meta gives it in the camera rig's tracking space, in OpenXR's axes.</summary>
        bool Joint(OVRPlugin.BodyJointLocation[] joints, OVRPlugin.BoneId id, out Vector3 position)
        {
            var joint = joints[(int)id];
            position = trackingSpace.TransformPoint(joint.Pose.Position.FromFlippedZVector3f());
            return joint.PositionValid;
        }

        bool FindTrackingSpace()
        {
            // Looked for again each second until it is there.
            if (Time.unscaledTime < nextLookup) return false;
            nextLookup = Time.unscaledTime + 1f;
            var rig = FindAnyObjectByType<OVRCameraRig>();
            if (rig != null) trackingSpace = rig.trackingSpace;
            return trackingSpace != null;
        }

        /// <summary>Nothing wants the body any more: stop tracking it and forget it.</summary>
        void Stop()
        {
            bool was = wasWanted;
            wasWanted = false;
            BodyRig.Clear();
            if (body == null || !body.enabled) return;
            body.enabled = false;
            if (logBody && was) Debug.Log("[Body] Body tracking stopped.");
        }

        void Report()
        {
            nextReport = Time.unscaledTime + reportSeconds;
            string why = unsupported ? "not supported" : !granted ? "no permission" : body == null || !body.enabled ? "not running" : "running";
            float confidence = trackedFrames > 0 ? confidenceSum / trackedFrames : 0f;
            Debug.Log($"[Body] last {reportSeconds:0} s: {why}, body in {trackedFrames} frames, none in {lostFrames}, " +
                      $"mean confidence {confidence:0.00}, blend {BodyRig.Blend:0.00}" +
                      (body != null && body.enabled ? $", fidelity {body.GetBodyTrackingFidelityStatus()}" : ""));
            trackedFrames = lostFrames = 0;
            confidenceSum = 0f;
        }
    }
}
