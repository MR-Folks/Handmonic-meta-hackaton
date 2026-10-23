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

        static void SetSerialized(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
