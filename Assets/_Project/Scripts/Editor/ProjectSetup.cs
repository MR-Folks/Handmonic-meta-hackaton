using System.Collections.Generic;
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
        // The demo songs, rendered by Tools/render_songs.py: (file name in Audio and Songs, title). The first one starts.
        static readonly (string file, string title)[] DemoSongs =
            { ("GlassRoom", "Glass Room"), ("NeonTide", "Neon Tide"), ("RainyWindow", "Rainy Window"), ("PaperBoat", "Paper Boat") };
        public const string MainScenePath = Root + "/Scenes/Main.unity";
        const string ApplicationId = "com.musicinyourhands.app";
        // The OpenXR features the app turns on for Android. HandInteractionProfile (XR_EXT_hand_interaction) carries Meta's
        // thumb microgestures: without it the runtime reports valid hands but never a tap or a swipe.
        static readonly string[] OpenXRFeaturesToEnable = { "MetaQuestFeature", "MetaXRFeature", "HandInteractionProfile" };

        [MenuItem("Music In Your Hands/Configure Project for Quest")]
        public static void Configure()
        {
            ConfigureAll();
            RunMetaFixAll(exitWhenDone: false);
        }

        [MenuItem("Music In Your Hands/Rebuild Main Scene")]
        public static void RebuildScene()
        {
            EnsureMainScene(EnsureDemoSongs(), rebuild: true);
        }

        public static void RunBatch()
        {
            ConfigureAll();
            RunMetaFixAll(exitWhenDone: true);
        }

        /// <summary>
        /// Adds any new World class to the main scene in menu order and refreshes every world's materials, without the
        /// rest of the project setup. Run it after adding or renaming a world.
        /// Batch mode: -executeMethod MusicInYourHands.EditorTools.ProjectSetup.UpdateWorldsBatch
        /// </summary>
        [MenuItem("Music In Your Hands/Update Worlds in Main Scene")]
        public static void UpdateWorlds()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            EnsureWorlds();
#if META_XR_CORE
            foreach (var feed in Object.FindObjectsByType<OVRHandFeed>(FindObjectsSortMode.None)) ApplyHandLook(feed.gameObject);
#endif
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] The main scene's worlds are up to date.");
        }

        public static void UpdateWorldsBatch()
        {
            UpdateWorlds();
            EditorApplication.Exit(0);
        }

        static void ConfigureAll()
        {
            ApplyPlayerSettings();
            EnsureUrp();
            ConfigureXR();
            ConfigureMetaProject();
            ConfigureTextures();
            EnsureMainScene(EnsureDemoSongs(), rebuild: false);
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

            EnableOpenXRFeatures();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
#else
            Debug.LogWarning("[Setup] OpenXR package not installed; skipped XR configuration.");
#endif
        }

#if UNITY_OPENXR
        /// <summary>Turns on, for Android, the OpenXR features in <see cref="OpenXRFeaturesToEnable"/> that are off.</summary>
        internal static void EnableOpenXRFeatures()
        {
            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXR == null) return;
            bool changed = false;
            foreach (var feature in openXR.GetFeatures<OpenXRFeature>())
            {
                if (!OpenXRFeaturesToEnable.Contains(feature.GetType().Name) || feature.enabled) continue;
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
                changed = true;
                Debug.Log($"[Setup] Turned on the OpenXR feature {feature.GetType().Name} for Android.");
            }
            if (!changed) return;
            EditorUtility.SetDirty(openXR);
            AssetDatabase.SaveAssets();
        }
#endif

        static void ConfigureMetaProject()
        {
#if META_XR_CORE
            var config = OVRProjectConfig.CachedProjectConfig;
            config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Supported;
            // The room scan (walls, tables, floor) the worlds place themselves on.
            config.sceneSupport = OVRProjectConfig.FeatureSupport.Supported;
            // Shadow play's arms follow the tracked body (see EnableBodyTracking).
            config.bodyTrackingSupport = OVRProjectConfig.FeatureSupport.Supported;
            OVRProjectConfig.CommitProjectConfig(config);
#endif
        }

#if META_XR_CORE
        /// <summary>
        /// Body tracking, for the worlds that follow your arms (Shadow play): Supported, not Required, in Meta's project
        /// config, so Meta's build step adds the body tracking feature to the manifest (required="false") and its
        /// permission; and never asked for when the app starts (the scene's OVRManager doesn't request it), since
        /// BodyFeed asks the first time a world wants the body.
        /// </summary>
        internal static void EnableBodyTracking()
        {
            var config = OVRProjectConfig.CachedProjectConfig;
            if (config.bodyTrackingSupport != OVRProjectConfig.FeatureSupport.Supported)
            {
                config.bodyTrackingSupport = OVRProjectConfig.FeatureSupport.Supported;
                OVRProjectConfig.CommitProjectConfig(config);
                Debug.Log("[Setup] Body tracking is Supported in Meta's project config.");
            }
            foreach (var manager in Object.FindObjectsByType<OVRManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(manager);
                var ask = so.FindProperty("requestBodyTrackingPermissionOnStartup");
                if (ask == null || !ask.boolValue) continue;
                ask.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Setup] OVRManager no longer asks for body tracking at startup; BodyFeed asks when a world needs it.");
            }
        }
#endif

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

        static SongAsset[] EnsureDemoSongs()
        {
            EnsureFolder(Root + "/Songs");
            var songs = new SongAsset[DemoSongs.Length];
            for (int i = 0; i < DemoSongs.Length; i++)
            {
                var (file, title) = DemoSongs[i];
                var path = $"{Root}/Songs/{file}.asset";
                var song = AssetDatabase.LoadAssetAtPath<SongAsset>(path);
                if (song == null)
                {
                    song = ScriptableObject.CreateInstance<SongAsset>();
                    AssetDatabase.CreateAsset(song, path);
                }
                song.title = title;
                song.artist = "Music In Your Hands demo";
                song.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/{file}.wav");
                song.lyrics = AssetDatabase.LoadAssetAtPath<TextAsset>($"{Root}/Audio/{file}.lrc");
                song.notes = AssetDatabase.LoadAssetAtPath<TextAsset>($"{Root}/Audio/{file}.notes");
                EditorUtility.SetDirty(song);
                if (song.clip == null) Debug.LogWarning($"[Setup] {file}.wav was not found, so {title} has no audio.");
                if (song.lyrics == null) Debug.LogWarning($"[Setup] {file}.lrc was not found, so {title} has no lyrics.");
                songs[i] = song;
            }
            return songs;
        }

        static void EnsureMainScene(SongAsset[] songs, bool rebuild)
        {
            EnsureFolder(Root + "/Scenes");
            var scene = File.Exists(MainScenePath) && !rebuild
                ? EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bus = Object.FindAnyObjectByType<MusicBus>();
            if (bus == null) bus = new GameObject("MusicBus", typeof(AudioSource), typeof(MusicBus)).GetComponent<MusicBus>();
            var so = new SerializedObject(bus);
            var start = so.FindProperty("startSong");
            if (start.objectReferenceValue == null) start.objectReferenceValue = songs[0];
            var list = so.FindProperty("songs");
            list.arraySize = songs.Length;
            for (int i = 0; i < songs.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = songs[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            if (Object.FindAnyObjectByType<HandRig>() == null) new GameObject("HandRig", typeof(HandRig));

            // The debug views (band bars, lyric line, joint dots) are switched off for the polished experience.
            // Turn the MusicDebugView or HandDebugView object on in the scene to see them again.
            var musicDebug = Object.FindAnyObjectByType<MusicDebugView>(FindObjectsInactive.Include);
            if (musicDebug == null)
            {
                musicDebug = new GameObject("MusicDebugView", typeof(MusicDebugView)).GetComponent<MusicDebugView>();
                musicDebug.transform.position = new Vector3(0f, 1.2f, 1f);
            }
            musicDebug.gameObject.SetActive(false);

            var handDebug = Object.FindAnyObjectByType<HandDebugView>(FindObjectsInactive.Include);
            if (handDebug == null) handDebug = new GameObject("HandDebugView", typeof(HandDebugView)).GetComponent<HandDebugView>();
            handDebug.gameObject.SetActive(false);

            EnsureWorlds();
            if (Object.FindAnyObjectByType<RuntimeDiagnostics>() == null) new GameObject("RuntimeDiagnostics", typeof(RuntimeDiagnostics));
            if (Object.FindAnyObjectByType<DemoTour>() == null) new GameObject("DemoTour", typeof(DemoTour));
            if (Object.FindAnyObjectByType<FrameMeter>() == null) new GameObject("FrameMeter", typeof(FrameMeter));
            if (Object.FindAnyObjectByType<WorldHint>() == null) new GameObject("WorldHint", typeof(WorldHint));

            var debugMaterial = EnsureDebugMaterial();
            foreach (var view in Object.FindObjectsByType<MusicDebugView>(FindObjectsInactive.Include, FindObjectsSortMode.None)) SetMaterial(view, debugMaterial);
            foreach (var view in Object.FindObjectsByType<HandDebugView>(FindObjectsInactive.Include, FindObjectsSortMode.None)) SetMaterial(view, debugMaterial);

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
            foreach (var feed in Object.FindObjectsByType<OVRHandFeed>(FindObjectsSortMode.None)) ApplyHandLook(feed.gameObject);
#endif

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
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

            handGo.AddComponent<OVRHandFeed>().Configure(hand, skeleton, left);
            ApplyHandLook(handGo);
        }

        const string HandMeshFolder = Root + "/Meshes";

        /// <summary>
        /// A copy of a Meta hand mesh that the game can read at runtime. Meta's hand FBX is imported without Read/Write,
        /// so on the headset its vertices can't be baked (Shadow play projects the posed hand mesh onto the wall). The
        /// package's import settings can't be changed, so the hands use this copy instead, kept next to the project's
        /// other assets. Returns the mesh unchanged when it is already readable.
        /// </summary>
        static Mesh EnsureReadableHandMesh(Mesh source)
        {
            if (source == null || source.isReadable) return source;
            EnsureFolder(HandMeshFolder);
            var path = $"{HandMeshFolder}/{source.name}.asset";
            var copy = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (copy == null)
            {
                copy = Object.Instantiate(source);
                copy.name = source.name;
                AssetDatabase.CreateAsset(copy, path);
                Debug.Log($"[Setup] Made a readable copy of the hand mesh {source.name} at {path}.");
            }
            // A copy keeps its source's Read/Write flag, so turn it on.
            var serialized = new SerializedObject(copy);
            var readable = serialized.FindProperty("m_IsReadable");
            if (!readable.boolValue)
            {
                readable.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssets();
            }
            return copy;
        }

        /// <summary>Gives a Meta hand the project's hand material and a HandSkin to drive it. Safe to repeat.</summary>
        public static void ApplyHandLook(GameObject handGo)
        {
            var material = EnsureHandMaterial();
            var skin = handGo.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin == null)
            {
                Debug.LogError($"[Setup] {handGo.name} has no hand mesh.");
                return;
            }
            skin.sharedMaterial = material;
            skin.sharedMesh = EnsureReadableHandMesh(skin.sharedMesh);
            if (PrefabUtility.IsPartOfPrefabInstance(skin)) PrefabUtility.RecordPrefabInstancePropertyModifications(skin);

            var bones = handGo.GetComponentsInChildren<Transform>(true);
            var wrist = bones.FirstOrDefault(t => t.name == "XRHand_Wrist");
            var knuckle = bones.FirstOrDefault(t => t.name == "XRHand_MiddleProximal");
            // Not "??": Unity's fake-null objects slip past it.
            var handSkin = handGo.GetComponent<HandSkin>();
            if (handSkin == null) handSkin = handGo.AddComponent<HandSkin>();
            bool left = handGo.name.Contains("Left") || handGo.name.EndsWith("_L");
            handSkin.Configure(skin, wrist, knuckle, left);
            EditorUtility.SetDirty(handSkin);
        }
#endif

        /// <summary>
        /// The worlds and the menu: a "Worlds" object with WorldManager and one child per World class in the code, in
        /// MenuOrder, each with its Material fields filled from Materials/ by name; then the bubble WorldMenu, the music
        /// gestures (thumb tap and swipes) and the menu button, microgesture and body feeds. Existing objects are kept and only
        /// their references are refreshed, so a new world needs nothing but its class: run Configure Project (or Rebuild
        /// Main Scene) and it joins the scene and the menu.
        /// </summary>
        internal static void EnsureWorlds()
        {
            var manager = Object.FindAnyObjectByType<WorldManager>(FindObjectsInactive.Include);
            if (manager == null) manager = new GameObject("Worlds", typeof(WorldManager)).GetComponent<WorldManager>();

            var worlds = new List<World>();
            foreach (var type in WorldCatalog.Types())
            {
                var world = (World)Object.FindAnyObjectByType(type, FindObjectsInactive.Include);
                if (world == null) world = (World)new GameObject(type.Name, type).GetComponent(type);
                world.transform.SetParent(manager.transform, false);
                world.gameObject.SetActive(true);
                AssignMaterials(world);
                worlds.Add(world);
            }
            worlds.Sort(WorldCatalog.Compare);
            // The hierarchy reads in menu order too.
            for (int i = 0; i < worlds.Count; i++) worlds[i].transform.SetSiblingIndex(i);
            var so = new SerializedObject(manager);
            var list = so.FindProperty("worlds");
            list.arraySize = worlds.Count;
            for (int i = 0; i < worlds.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var glow = EnsureMaterial(GlowMaterialPath, "MusicInYourHands/Glow");
            var line = EnsureMaterial(LineMaterialPath, "MusicInYourHands/Line");

            // The palm-up wheel was replaced by the bubble menu.
            var oldWheel = GameObject.Find("WorldWheel");
            if (oldWheel != null) Object.DestroyImmediate(oldWheel);

            var menu = Object.FindAnyObjectByType<WorldMenu>(FindObjectsInactive.Include);
            if (menu == null) menu = new GameObject("WorldMenu", typeof(WorldMenu)).GetComponent<WorldMenu>();
            SetReferences(menu, ("bubbleMaterial", EnsureMaterial(BubbleMaterialPath, "MusicInYourHands/Bubble")), ("glowMaterial", glow),
                ("lineMaterial", line), ("panelMaterial", EnsureMaterial(PanelMaterialPath, "MusicInYourHands/Panel")));

            // Thumb tap and thumb swipes pause, play and step the songs in every world.
            var gestures = Object.FindAnyObjectByType<MusicGestures>(FindObjectsInactive.Include);
            if (gestures == null) gestures = new GameObject("MusicGestures", typeof(MusicGestures)).GetComponent<MusicGestures>();
            SetReferences(gestures, ("bubbleMaterial", EnsureMaterial(BubbleMaterialPath, "MusicInYourHands/Bubble")));
#if META_XR_CORE
            if (Object.FindAnyObjectByType<MenuButtonFeed>(FindObjectsInactive.Include) == null)
                new GameObject("MenuButton", typeof(MenuButtonFeed));
            if (Object.FindAnyObjectByType<MicrogestureFeed>(FindObjectsInactive.Include) == null)
                new GameObject("Microgestures", typeof(MicrogestureFeed));
            // Body tracking, only while a world wants the body; its project config with it.
            if (Object.FindAnyObjectByType<BodyFeed>(FindObjectsInactive.Include) == null)
                new GameObject("Body", typeof(BodyFeed));
            EnableBodyTracking();
#if UNITY_OPENXR
            // The microgestures arrive through OpenXR's hand interaction profile, so make sure it is on.
            EnableOpenXRFeatures();
#endif
            if (Object.FindAnyObjectByType<RoomScanFeed>(FindObjectsInactive.Include) == null)
            {
                var scan = new GameObject("RoomScan");
                var mruk = scan.AddComponent<global::Meta.XR.MRUtilityKit.MRUK>();
                // RoomScanFeed loads the scan itself, without starting Space Setup, and the worlds do their own placing.
                mruk.EnableWorldLock = true;
                mruk.SceneSettings ??= new global::Meta.XR.MRUtilityKit.MRUK.MRUKSettings();
                mruk.SceneSettings.LoadSceneOnStartup = false;
                scan.AddComponent<RoomScanFeed>();
            }
#endif
        }

        /// <summary>Fills a world's Material fields from Materials/ by name (see WorldCatalog.MaterialName).</summary>
        public static void AssignMaterials(World world)
        {
            var so = new SerializedObject(world);
            foreach (var field in WorldCatalog.MaterialFields(world.GetType()))
            {
                var property = so.FindProperty(field.Name);
                if (property != null) property.objectReferenceValue = WorldMaterial(world.GetType(), field.Name);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The material asset a world's Material field gets, made from its shader if it doesn't exist yet.</summary>
        public static Material WorldMaterial(System.Type type, string field)
        {
            var name = WorldCatalog.MaterialName(type, field);
            return name == "Sky" ? EnsureSkyMaterial() : EnsureMaterial($"{Root}/Materials/{name}.mat", $"MusicInYourHands/{name}");
        }

        static void SetReferences(Object target, params (string property, Object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (property, value) in values) so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public const string HandMaterialPath = Root + "/Materials/Hand.mat";
        public const string OrbMaterialPath = Root + "/Materials/Orb.mat";
        public const string SkyMaterialPath = Root + "/Materials/Sky.mat";
        public const string PanelMaterialPath = Root + "/Materials/Panel.mat";
        public const string SkyTexturePath = Root + "/Materials/NightSky.png";
        public const string OrbDotsMaterialPath = Root + "/Materials/OrbDots.mat";
        public const string LineMaterialPath = Root + "/Materials/Line.mat";
        public const string BubbleMaterialPath = Root + "/Materials/Bubble.mat";
        public const string StreakMaterialPath = Root + "/Materials/Streak.mat";
        public const string GlowMaterialPath = Root + "/Materials/Glow.mat";
        public const string WaterMaterialPath = Root + "/Materials/Water.mat";
        public const string SilkMaterialPath = Root + "/Materials/Silk.mat";
        public const string CausticsMaterialPath = Root + "/Materials/Caustics.mat";
        public const string PaintMaterialPath = Root + "/Materials/PaintStroke.mat";
        public const string RoomGridMaterialPath = Root + "/Materials/RoomGrid.mat";
        public const string TrailMaterialPath = Root + "/Materials/Trail.mat";
        public const string PlateMaterialPath = Root + "/Materials/Plate.mat";
        public const string LetteringMaterialPath = Root + "/Materials/Lettering.mat";
        public const string RingMaterialPath = Root + "/Materials/Ring.mat";
        public const string ShellMaterialPath = Root + "/Materials/Shell.mat";
        public const string LivingWallsMaterialPath = Root + "/Materials/LivingWalls.mat";

        public static Material EnsureHandMaterial() => EnsureMaterial(HandMaterialPath, "MusicInYourHands/Hand");

        /// <summary>Loads the material, creating it from the shader the first time. Tuned values live in the asset.</summary>
        /// <summary>The atlases hold distances rather than colours, so they stay uncompressed, linear and single-channel.</summary>
        static void ConfigureTextures()
        {
            foreach (var path in new[] { Root + "/Resources/Type/Bold.png", Root + "/Resources/Type/Marker.png" })
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                importer.textureType = TextureImporterType.SingleChannel;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
                settings.sRGBTexture = false;
                settings.mipmapEnabled = true;
                settings.wrapMode = TextureWrapMode.Clamp;
                settings.filterMode = FilterMode.Trilinear;
                settings.npotScale = TextureImporterNPOTScale.None;
                importer.SetTextureSettings(settings);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android", overridden = true, format = TextureImporterFormat.R8, maxTextureSize = 2048
                });
                importer.SaveAndReimport();
            }
            if (AssetImporter.GetAtPath(SkyTexturePath) is TextureImporter sky)
            {
                sky.wrapMode = TextureWrapMode.Clamp;
                sky.mipmapEnabled = true;
                sky.SaveAndReimport();
            }
        }

        static Material EnsureSkyMaterial()
        {
            var material = EnsureMaterial(SkyMaterialPath, "MusicInYourHands/Sky");
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexturePath));
            return material;
        }

        public static Material EnsureMaterial(string path, string shaderName)
        {
            EnsureFolder(Root + "/Materials");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(shaderName)) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

#if META_XR_CORE

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
