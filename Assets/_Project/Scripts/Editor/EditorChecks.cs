using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Checks that catch device problems without a headset: hand joints match the visible hand mesh, debug shapes
    /// use a URP material, and a rendered preview of the debug view has no magenta (missing shader) pixels.
    /// Writes Logs/preview-debug.png. Batch mode (needs graphics, so no -nographics):
    /// -executeMethod MusicInYourHands.EditorTools.EditorChecks.RunBatch
    /// </summary>
    public static class EditorChecks
    {
        const string PreviewPath = "Logs/preview-debug.png";
        static readonly string[] JointNames =
        {
            "XRHand_Palm", "XRHand_Wrist", "XRHand_IndexProximal", "XRHand_LittleProximal",
            "XRHand_ThumbTip", "XRHand_IndexTip", "XRHand_MiddleTip", "XRHand_RingTip", "XRHand_LittleTip"
        };

        [MenuItem("Music In Your Hands/Run Editor Checks")]
        public static void Run() => RunAll();

        public static void RunBatch() => EditorApplication.Exit(RunAll() ? 0 : 1);

        static bool RunAll()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            bool ok = true;
            ok &= CheckHandJoints();
            ok &= CheckDebugMaterial(out var material);
            ok &= CheckPreview(material);
            ok &= CheckHandPreview();
            ok &= CheckLivingSkinPreview();
            ok &= CheckOrbPreview();
            ok &= CheckWheelPreview();
            Debug.Log(ok ? "[Check] All checks passed." : "[Check] Some checks FAILED.");
            return ok;
        }

        static bool CheckHandJoints()
        {
#if META_XR_CORE
            bool ok = true;
            foreach (var side in new[] { "L", "R" })
            {
                var path = $"Packages/com.meta.xr.sdk.core/Prefabs/OpenXRCustomHandPrefab_{side}.prefab";
                var hand = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var skin = hand.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var meshBones = new HashSet<Transform>(skin != null ? skin.bones : new Transform[0]);
                var all = hand.GetComponentsInChildren<Transform>(true);

                foreach (var jointName in JointNames)
                {
                    var joint = all.FirstOrDefault(t => t.name == jointName);
                    if (joint == null)
                    {
                        Debug.LogError($"[Check] {side} hand: joint {jointName} not found by name.");
                        ok = false;
                    }
                    // Palm and tips are not skinned in every mesh; they still sit in the bone hierarchy.
                    else if (!meshBones.Contains(joint) && !meshBones.Contains(joint.parent))
                    {
                        Debug.LogError($"[Check] {side} hand: {jointName} is not part of the visible hand mesh's skeleton.");
                        ok = false;
                    }
                }

                ReportIdLookup(hand, side);
                Object.DestroyImmediate(hand);
            }
            if (ok) Debug.Log("[Check] Hand joints found by name and they drive the visible hand mesh.");
            return ok;
#else
            Debug.LogWarning("[Check] Meta XR Core SDK not installed; skipped hand joint check.");
            return true;
#endif
        }

#if META_XR_CORE
        /// <summary>Shows which transform the old id-based lookup would have used, for the record.</summary>
        static void ReportIdLookup(GameObject hand, string side)
        {
            var skeleton = hand.GetComponent<OVRCustomSkeleton>();
            if (skeleton == null) return;
            var so = new SerializedObject(skeleton);
            var list = so.FindProperty("_customBones_V2") ?? so.FindProperty("_customBones");
            if (list == null || !list.isArray) return;

            var sb = new StringBuilder($"[Check] {side} hand id lookup (old method): ");
            foreach (var id in new[] { OVRSkeleton.BoneId.XRHand_Palm, OVRSkeleton.BoneId.XRHand_IndexTip, OVRSkeleton.BoneId.XRHand_ThumbTip })
            {
                int index = (int)id;
                var t = index < list.arraySize ? list.GetArrayElementAtIndex(index).objectReferenceValue as Transform : null;
                sb.Append($"{id}(#{index}) -> {(t != null ? t.name : "none")}; ");
            }
            Debug.Log(sb.ToString());
        }
#endif

        static bool CheckDebugMaterial(out Material material)
        {
            material = ProjectSetup.EnsureDebugMaterial();
            bool ok = material != null && material.shader != null && material.shader.isSupported &&
                      material.shader.name == "Universal Render Pipeline/Unlit";
            if (ok) Debug.Log($"[Check] Debug material uses {material.shader.name}.");
            else Debug.LogError($"[Check] Debug material is not a supported URP Unlit material ({(material != null && material.shader != null ? material.shader.name : "missing")}).");
            return ok;
        }

        static bool CheckPreview(Material material)
        {
            var cameraGo = new GameObject("Preview Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.09f, 0.14f);
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.25f, 0.45f), Quaternion.identity);
            camera.fieldOfView = 50f;

            var view = new GameObject("MusicDebugView").AddComponent<MusicDebugView>();
            view.transform.position = new Vector3(0f, 1.2f, 1f);
            SetSerialized(view, "material", material);
            view.Build();

            // A few spheres like the fingertip markers, to check them too.
            for (int i = 0; i < 5; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = new Vector3(-0.12f + i * 0.06f, 1.05f, 0.95f);
                sphere.transform.localScale = Vector3.one * 0.03f;
                sphere.GetComponent<Renderer>().sharedMaterial = material;
            }

            const int width = 960, height = 540;
            var rt = new RenderTexture(width, height, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;

            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
            File.WriteAllBytes(PreviewPath, image.EncodeToPNG());

            int magenta = 0, drawn = 0;
            var background = camera.backgroundColor;
            foreach (var p in image.GetPixels())
            {
                if (Mathf.Abs(p.r - background.r) + Mathf.Abs(p.g - background.g) + Mathf.Abs(p.b - background.b) > 0.05f) drawn++;
                if (p.r > 0.85f && p.g < 0.15f && p.b > 0.85f) magenta++;
            }

            bool ok = drawn > 500 && magenta < 50;
            if (ok) Debug.Log($"[Check] Preview rendered {drawn} pixels of debug shapes with no magenta. Saved {PreviewPath}.");
            else Debug.LogError($"[Check] Preview problem: {drawn} pixels drawn, {magenta} magenta. See {PreviewPath}.");
            return ok;
        }

        /// <summary>
        /// Renders both Meta hands with the project's hand look over a room-coloured background (like passthrough):
        /// the left hand at rest, the right at a beat peak. Writes Logs/preview-hands.png.
        /// </summary>
        static bool CheckHandPreview()
        {
#if META_XR_CORE
            var material = ProjectSetup.EnsureHandMaterial();
            if (material.shader == null || !material.shader.isSupported || material.shader.name != "MusicInYourHands/Hand")
            {
                Debug.LogError("[Check] Hand material does not use a supported MusicInYourHands/Hand shader.");
                return false;
            }

            var hands = StageHands(out var camera);
            for (int h = 0; h < hands.Count; h++)
                SetBeat(hands[h].Skin, h == 0 ? 10f : 0.02f, h == 0 ? 0f : 1f);
            return CheckImage(Render(camera, 960, 540), camera.backgroundColor, "Logs/preview-hands.png", "Hand preview");
#else
            return true;
#endif
        }

        /// <summary>
        /// Renders the Living skin world at four moments after a beat (pulse through the hand, palm waves spreading,
        /// fingertip lights, sparks) into one 2x2 image: Logs/preview-living-skin.png.
        /// </summary>
        static bool CheckLivingSkinPreview()
        {
#if META_XR_CORE
            var hands = StageHands(out var camera);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            foreach (var hand in hands) PushHand(rig, hand);

            var skin = new GameObject("LivingSkin").AddComponent<LivingSkin>();
            var so = new SerializedObject(skin);
            so.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            so.FindProperty("ringMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.RingMaterialPath, "MusicInYourHands/Ring");
            so.ApplyModifiedPropertiesWithoutUndo();
            skin.Build();

            float[] times = { 0.05f, 0.22f, 0.45f, 0.85f };
            float[] bands = { 0.9f, 0.7f, 0.55f, 0.6f, 0.4f, 0.5f };
            const int w = 640, h = 360;
            var sheet = new Texture2D(w * 2, h * 2, TextureFormat.RGB24, false);
            float clock = 0f;
            for (int i = 0; i < times.Length; i++)
            {
                float dt = times[i] - clock;
                clock = times[i];
                float kick = Mathf.Exp(-clock * 7f);
                // The beat happens at t = 0; step to each moment, wiggling the fingertips a little to make sparks.
                if (i == 0) skin.Step(rig, bands, 1f, 0f, camera, true);
                foreach (var hand in hands) { hand.Root.transform.position += Vector3.up * 0.004f; PushHand(rig, hand); }
                skin.Step(rig, bands, kick, dt, camera, false);
                skin.SimulateSparks(dt);
                foreach (var hand in hands) SetBeat(hand.Skin, clock, kick);

                var frame = Render(camera, w, h);
                sheet.SetPixels((i % 2) * w, (1 - i / 2) * h, w, h, frame.GetPixels());
            }
            sheet.Apply();
            return CheckImage(sheet, camera.backgroundColor, "Logs/preview-living-skin.png", "Living skin preview");
#else
            return true;
#endif
        }

        /// <summary>
        /// Renders the Heartbeat orb held between two hands, at rest and on a kick, side by side:
        /// Logs/preview-heartbeat-orb.png.
        /// </summary>
        static bool CheckOrbPreview()
        {
#if META_XR_CORE
            var hands = StageHands(out var camera);
            // Turn the palms towards each other, as if holding something between them.
            foreach (var hand in hands)
            {
                var bones = hand.Root.GetComponentsInChildren<Transform>();
                Transform Bone(string n) => bones.First(t => t.name == n);
                var normal = Vector3.ProjectOnPlane(PalmNormal(Bone, hand.Left), Vector3.up).normalized;
                var want = hand.Left ? Vector3.right : Vector3.left;
                var c = hand.Skin.bounds.center;
                hand.Root.transform.RotateAround(c, Vector3.up, Vector3.SignedAngle(normal, want, Vector3.up) * 0.8f);
                hand.Root.transform.position += (hand.Left ? Vector3.left : Vector3.right) * 0.03f;
            }
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            foreach (var hand in hands) PushHand(rig, hand);

            var orb = new GameObject("HeartbeatOrb").AddComponent<HeartbeatOrb>();
            var so = new SerializedObject(orb);
            so.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            so.FindProperty("orbMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.OrbMaterialPath, "MusicInYourHands/Orb");
            so.ApplyModifiedPropertiesWithoutUndo();
            orb.Build();

            float[] bands = { 0.8f, 0.6f, 0.5f, 0.5f, 0.4f, 0.5f };
            for (int i = 0; i < 8; i++) orb.Step(rig, bands, 0f, 0.1f, false);
            const int w = 640, h = 360;
            var sheet = new Texture2D(w * 2, h, TextureFormat.RGB24, false);
            sheet.SetPixels(0, 0, w, h, Render(camera, w, h).GetPixels());
            orb.Step(rig, bands, 1f, 0.016f, true);
            orb.SimulateMotes(0.18f);
            foreach (var hand in hands) SetBeat(hand.Skin, 0.08f, 1f);
            sheet.SetPixels(w, 0, w, h, Render(camera, w, h).GetPixels());
            sheet.Apply();
            return CheckImage(sheet, camera.backgroundColor, "Logs/preview-heartbeat-orb.png", "Heartbeat orb preview");
#else
            return true;
#endif
        }

        /// <summary>Renders the world wheel open above the left hand: Logs/preview-wheel.png.</summary>
        static bool CheckWheelPreview()
        {
#if META_XR_CORE
            var hands = StageHands(out var camera);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            foreach (var hand in hands) PushHand(rig, hand);

            var glow = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            var managerGo = new GameObject("Worlds");
            var worlds = new World[]
            {
                MakePreviewWorld<LivingSkin>(managerGo.transform, "Living skin", new Color(1f, 0.37f, 0.68f)),
                MakePreviewWorld<HeartbeatOrb>(managerGo.transform, "Heartbeat orb", new Color(1f, 0.83f, 0.42f))
            };
            var manager = managerGo.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Length;
            for (int i = 0; i < worlds.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            manager.Switch(0);

            var wheel = new GameObject("WorldWheel").AddComponent<WorldWheel>();
            var wso = new SerializedObject(wheel);
            wso.FindProperty("glowMaterial").objectReferenceValue = glow;
            wso.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < 10; i++) wheel.Step(rig, manager, camera, 0.05f, forceOpenLeft: true);

            return CheckImage(Render(camera, 960, 540), camera.backgroundColor, "Logs/preview-wheel.png", "Wheel preview");
#else
            return true;
#endif
        }

        static T MakePreviewWorld<T>(Transform parent, string displayName, Color color) where T : World
        {
            var go = new GameObject(displayName);
            go.transform.SetParent(parent, false);
            var world = go.AddComponent<T>();
            var so = new SerializedObject(world);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("color").colorValue = color;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(false);
            return world;
        }

#if META_XR_CORE
        sealed class PreviewHand
        {
            public GameObject Root;
            public SkinnedMeshRenderer Skin;
            public bool Left;
        }

        /// <summary>Both Meta hands with the project look, held up palm-forward in front of a passthrough-coloured camera.</summary>
        static List<PreviewHand> StageHands(out Camera camera)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Preview Camera");
            camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.5f, 0.44f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.02f;
            camera.transform.position = new Vector3(0f, 1.2f, 0.45f) + new Vector3(0f, 0f, -0.5f);
            camera.transform.LookAt(new Vector3(0f, 1.2f, 0.45f));

            var hands = new List<PreviewHand>();
            foreach (var (side, x) in new[] { ("L", -0.11f), ("R", 0.11f) })
            {
                var path = $"Packages/com.meta.xr.sdk.core/Prefabs/OpenXRCustomHandPrefab_{side}.prefab";
                var hand = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                ProjectSetup.ApplyHandLook(hand);
                var skin = hand.GetComponentInChildren<SkinnedMeshRenderer>();
                var bones = hand.GetComponentsInChildren<Transform>();
                Transform Bone(string n) => bones.First(t => t.name == n);

                // Stand the hand up, then turn it so the palm faces the camera.
                var up = (Bone("XRHand_MiddleProximal").position - Bone("XRHand_Wrist").position).normalized;
                hand.transform.rotation = Quaternion.FromToRotation(up, Vector3.up) * hand.transform.rotation;
                hand.transform.position += new Vector3(x, 1.2f, 0.45f) - skin.bounds.center;
                bool left = side == "L";
                if (Vector3.Dot(PalmNormal(Bone, left), (camera.transform.position - skin.bounds.center).normalized) < 0f)
                    hand.transform.RotateAround(skin.bounds.center, Vector3.up, 180f);
                hand.GetComponent<HandSkin>().Apply();
                hands.Add(new PreviewHand { Root = hand, Skin = skin, Left = left });
            }
            return hands;
        }

        static Vector3 PalmNormal(System.Func<string, Transform> bone, bool left)
        {
            var wrist = bone("XRHand_Wrist").position;
            var normal = Vector3.Cross(bone("XRHand_IndexProximal").position - wrist, bone("XRHand_LittleProximal").position - wrist).normalized;
            return left ? normal : -normal;
        }

        /// <summary>Feeds a preview hand into HandRig the way OVRHandFeed does on the headset.</summary>
        static void PushHand(HandRig rig, PreviewHand hand)
        {
            var bones = hand.Root.GetComponentsInChildren<Transform>();
            Transform Bone(string n) => bones.First(t => t.name == n);
            var tipJoints = new[] { "XRHand_ThumbTip", "XRHand_IndexTip", "XRHand_MiddleTip", "XRHand_RingTip", "XRHand_LittleTip" }.Select(Bone).ToArray();
            var palm = Bone("XRHand_Palm");
            rig.SetJoints(hand.Left, palm, tipJoints);
            rig.Push(hand.Left, true, new Pose(palm.position, palm.rotation), PalmNormal(Bone, hand.Left), tipJoints.Select(t => t.position).ToArray());
            hand.Root.GetComponent<HandSkin>().Apply();
        }

        static void SetBeat(Renderer r, float beatAge, float kick)
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetFloat("_BeatAge", beatAge);
            block.SetFloat("_Kick", kick);
            block.SetFloat("_Energy", kick * 0.6f);
            r.SetPropertyBlock(block);
        }
#endif

        static bool CheckImage(Texture2D image, Color background, string path, string label)
        {
            File.WriteAllBytes(path, image.EncodeToPNG());
            int magenta = 0, drawn = 0;
            foreach (var p in image.GetPixels())
            {
                if (Mathf.Abs(p.r - background.r) + Mathf.Abs(p.g - background.g) + Mathf.Abs(p.b - background.b) > 0.06f) drawn++;
                if (p.r > 0.85f && p.g < 0.15f && p.b > 0.85f) magenta++;
            }
            bool ok = drawn > 2000 && magenta < 50;
            if (ok) Debug.Log($"[Check] {label} rendered {drawn} pixels with no magenta. Saved {path}.");
            else Debug.LogError($"[Check] {label} problem: {drawn} pixels drawn, {magenta} magenta. See {path}.");
            return ok;
        }

        static Texture2D Render(Camera camera, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            return image;
        }

        static void SetSerialized(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
