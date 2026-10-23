using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_OPENXR
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
#endif
#if META_XR_CORE
using MusicInYourHands.Meta;
#endif

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Project setup, matching the autism-keyboard Quest setup: Android/IL2CPP/ARM64 with Vulkan, URP tuned for
    /// Quest, OpenXR with the Meta features, hand tracking and passthrough, Meta's "Fix All", the demo song and the
    /// Main scene (OVRCameraRig with passthrough and Meta's OpenXR hands). Safe to run more than once.
    /// Batch mode: -executeMethod MusicInYourHands.EditorTools.ProjectSetup.RunBatch
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/_Project";
        const string UrpPath = Root + "/Settings/URP-Quest.asset";
        const string RendererPath = Root + "/Settings/URP-Quest_Renderer.asset";
        const string SongPath = Root + "/Songs/GlassRoom.asset";
        const string ScenePath = Root + "/Scenes/Main.unity";
        const string ApplicationId = "com.musicinyourhands.app";
        static readonly string[] OpenXRFeaturesToEnable = { "MetaQuestFeature", "MetaXRFeature" };

        [MenuItem("Music In Your Hands/Configure Project for Quest")]
        public static void Configure()
        {
            ConfigureAll();
            RunMetaFixAll(exitWhenDone: false);
        }

        [MenuItem("Music In Your Hands/Rebuild Main Scene")]
        public static void RebuildScene()
        {
            EnsureMainScene(EnsureDemoSong(), rebuild: true);
        }

        public static void RunBatch()
        {
            ConfigureAll();
            RunMetaFixAll(exitWhenDone: true);
        }

        static void ConfigureAll()
        {
            ApplyPlayerSettings();
            EnsureUrp();
            ConfigureXR();
            ConfigureMetaProject();
            EnsureMainScene(EnsureDemoSong(), rebuild: false);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Music In Your Hands project setup finished.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        static void ApplyPlayerSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "MR Folks";
            PlayerSettings.productName = "Music In Your Hands";
            PlayerSettings.SetApplicationIdentifier(android, ApplicationId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            // A splash screen floating over passthrough looks broken in mixed reality.
            PlayerSettings.SplashScreen.show = false;
        }

        static void EnsureUrp()
        {
            EnsureFolder(Root + "/Settings");
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpPath);
            if (asset == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                asset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(asset, UrpPath);
            }

            // Quest-friendly defaults: 4x MSAA and no HDR (passthrough needs the alpha channel intact).
            asset.msaaSampleCount = 4;
            asset.supportsHDR = false;
            asset.renderScale = 1f;
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void ConfigureXR()
        {
#if UNITY_OPENXR
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                Directory.CreateDirectory("Assets/XR");
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            var settings = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (settings.Manager == null)
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            settings.InitManagerOnStart = true;

            XRPackageMetadataStore.AssignLoader(settings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);

            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            foreach (var feature in openXR.GetFeatures<OpenXRFeature>())
            {
                if (OpenXRFeaturesToEnable.Contains(feature.GetType().Name) && !feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                }
            }
            EditorUtility.SetDirty(openXR);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
#else
            Debug.LogWarning("[Setup] OpenXR package not installed; skipped XR configuration.");
#endif
        }

        static void ConfigureMetaProject()
        {
#if META_XR_CORE
            var config = OVRProjectConfig.CachedProjectConfig;
            config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Supported;
            OVRProjectConfig.CommitProjectConfig(config);
#endif
        }

        static void RunMetaFixAll(bool exitWhenDone)
        {
#if META_XR_CORE
            var task = OVRProjectSetup.FixAllAsync(BuildTargetGroup.Android);
            void Poll()
            {
                if (!task.IsCompleted) return;
                EditorApplication.update -= Poll;
                AssetDatabase.SaveAssets();
                if (task.IsFaulted) Debug.LogError($"[Setup] Meta Fix All failed: {task.Exception}");
                else Debug.Log("[Setup] Meta Project Setup Tool: Fix All completed.");
                if (exitWhenDone) EditorApplication.Exit(task.IsFaulted ? 1 : 0);
            }
            EditorApplication.update += Poll;
#else
            if (exitWhenDone) EditorApplication.Exit(0);
#endif
        }

        static SongAsset EnsureDemoSong()
        {
            EnsureFolder(Root + "/Songs");
            var song = AssetDatabase.LoadAssetAtPath<SongAsset>(SongPath);
            if (song == null)
            {
                song = ScriptableObject.CreateInstance<SongAsset>();
                AssetDatabase.CreateAsset(song, SongPath);
            }
            song.title = "Glass Room";
            song.artist = "Music In Your Hands demo";
            song.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/GlassRoom.wav");
            song.lyrics = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Audio/GlassRoom.lrc");
            EditorUtility.SetDirty(song);
            if (song.clip == null) Debug.LogWarning("[Setup] GlassRoom.wav was not found, so the demo song has no audio.");
            if (song.lyrics == null) Debug.LogWarning("[Setup] GlassRoom.lrc was not found, so the demo song has no lyrics.");
            return song;
        }

        static void EnsureMainScene(SongAsset song, bool rebuild)
        {
            EnsureFolder(Root + "/Scenes");
            var scene = File.Exists(ScenePath) && !rebuild
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (Object.FindAnyObjectByType<MusicBus>() == null)
            {
                var music = new GameObject("MusicBus", typeof(AudioSource), typeof(MusicBus));
                var so = new SerializedObject(music.GetComponent<MusicBus>());
                so.FindProperty("startSong").objectReferenceValue = song;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (Object.FindAnyObjectByType<HandRig>() == null) new GameObject("HandRig", typeof(HandRig));

            if (Object.FindAnyObjectByType<MusicDebugView>() == null)
            {
                var debugView = new GameObject("MusicDebugView", typeof(MusicDebugView));
                debugView.transform.position = new Vector3(0f, 1.2f, 1f);
            }

            if (Object.FindAnyObjectByType<HandDebugView>() == null) new GameObject("HandDebugView", typeof(HandDebugView));
            if (Object.FindAnyObjectByType<RuntimeDiagnostics>() == null) new GameObject("RuntimeDiagnostics", typeof(RuntimeDiagnostics));

            var debugMaterial = EnsureDebugMaterial();
            foreach (var view in Object.FindObjectsByType<MusicDebugView>(FindObjectsSortMode.None)) SetMaterial(view, debugMaterial);
            foreach (var view in Object.FindObjectsByType<HandDebugView>(FindObjectsSortMode.None)) SetMaterial(view, debugMaterial);

            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var light = new GameObject("Soft Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 0.9f;
                light.shadows = LightShadows.None;
                light.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
            }

#if META_XR_CORE
            if (Object.FindAnyObjectByType<OVRManager>() == null) CreateMetaRig();
#endif

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        public const string DebugMaterialPath = Root + "/Materials/DebugUnlit.mat";

        /// <summary>URP Unlit material for debug shapes; primitives' built-in default material renders magenta in URP.</summary>
        public static Material EnsureDebugMaterial()
        {
            EnsureFolder(Root + "/Materials");
            var material = AssetDatabase.LoadAssetAtPath<Material>(DebugMaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "DebugUnlit" };
            AssetDatabase.CreateAsset(material, DebugMaterialPath);
            return material;
        }

        static void SetMaterial(Object view, Material material)
        {
            var so = new SerializedObject(view);
            so.FindProperty("material").objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

#if META_XR_CORE
        const string MetaPrefabs = "Packages/com.meta.xr.sdk.core/Prefabs/";
        const string MetaHandPrefabLeft = MetaPrefabs + "OpenXRCustomHandPrefab_L.prefab";
        const string MetaHandPrefabRight = MetaPrefabs + "OpenXRCustomHandPrefab_R.prefab";
        const string MetaHandMaterial = "Packages/com.meta.xr.sdk.interaction/Runtime/Materials/OculusHand.mat";

        static void CreateMetaRig()
        {
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MetaPrefabs + "OVRCameraRig.prefab"));
            rig.name = "OVRCameraRig";
            var manager = rig.GetComponent<OVRManager>();
            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            // Mixed reality: the player keeps seeing their room.
            manager.isInsightPassthroughEnabled = true;
            rig.AddComponent<OVRPassthroughLayer>();

            var centerEye = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
            var camera = centerEye.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.nearClipPlane = 0.02f;
            centerEye.tag = "MainCamera";
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);

            AddHand(rig.transform.Find("TrackingSpace/LeftHandAnchor"), true);
            AddHand(rig.transform.Find("TrackingSpace/RightHandAnchor"), false);
        }

        static void AddHand(Transform anchor, bool left)
        {
            var handGo = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(left ? MetaHandPrefabLeft : MetaHandPrefabRight), anchor);
            handGo.name = left ? "LeftHand" : "RightHand";
            var hand = handGo.GetComponent<OVRHand>();
            var skeleton = handGo.GetComponent<OVRSkeleton>();
            // SerializedObject (not reflection) so the values are stored as prefab overrides for both hands.
            SetSerialized(hand, "HandType", (int)(left ? OVRHand.Hand.HandLeft : OVRHand.Hand.HandRight));
            SetSerialized(skeleton, "_skeletonType", (int)(left ? OVRSkeleton.SkeletonType.XRHandLeft : OVRSkeleton.SkeletonType.XRHandRight));
            if (handGo.TryGetComponent<OVRMesh>(out var mesh))
                SetSerialized(mesh, "_meshType", (int)(left ? OVRMesh.MeshType.XRHandLeft : OVRMesh.MeshType.XRHandRight));

            var material = AssetDatabase.LoadAssetAtPath<Material>(MetaHandMaterial);
            if (material == null) Debug.LogError($"[Setup] Hand material not found: {MetaHandMaterial}");
            foreach (var skin in handGo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.sharedMaterial = material;
                PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
            }

            handGo.AddComponent<OVRHandFeed>().Configure(hand, skeleton, left);
        }

        static void SetSerialized(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogError($"[Setup] {target.GetType().Name} has no serialized field '{property}'.");
                return;
            }
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
#endif
    }
}
