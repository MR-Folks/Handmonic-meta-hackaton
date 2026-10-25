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
            EnsureMainScene(EnsureDemoSongs(), rebuild: true);
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
        /// The worlds and the menu: a "Worlds" object with WorldManager, one child per world in menu order, the bubble
        /// WorldMenu and the menu button feed. Existing objects are kept and only their references and names are refreshed.
        /// </summary>
        static void EnsureWorlds()
        {
            var glow = EnsureMaterial(GlowMaterialPath, "MusicInYourHands/Glow");

            var manager = Object.FindAnyObjectByType<WorldManager>(FindObjectsInactive.Include);
            if (manager == null) manager = new GameObject("Worlds", typeof(WorldManager)).GetComponent<WorldManager>();

            var livingSkin = EnsureWorld<LivingSkin>(manager.transform, "LivingSkin", "Living skin", new Color(1f, 0.37f, 0.68f));
            SetReferences(livingSkin, ("glowMaterial", glow), ("ringMaterial", EnsureMaterial(RingMaterialPath, "MusicInYourHands/Ring")));

            var orb = EnsureWorld<HeartbeatOrb>(manager.transform, "HeartbeatOrb", "Heartbeat orb", new Color(1f, 0.83f, 0.42f));
            SetReferences(orb, ("glowMaterial", glow), ("orbMaterial", EnsureMaterial(OrbMaterialPath, "MusicInYourHands/Orb")),
                ("dotsMaterial", EnsureMaterial(OrbDotsMaterialPath, "MusicInYourHands/OrbDots")),
                ("lineMaterial", EnsureMaterial(LineMaterialPath, "MusicInYourHands/Line")));

            var line = EnsureMaterial(LineMaterialPath, "MusicInYourHands/Line");
            var ring = EnsureMaterial(RingMaterialPath, "MusicInYourHands/Ring");

            var ribbon = EnsureWorld<WaveformRibbon>(manager.transform, "WaveformRibbon", "Waveform ribbon", new Color(1f, 0.7f, 0.42f));
            SetReferences(ribbon, ("lineMaterial", line), ("glowMaterial", glow));

            var strings = EnsureWorld<AirStrings>(manager.transform, "AirStrings", "Air strings", new Color(0.49f, 0.95f, 0.79f));
            SetReferences(strings, ("lineMaterial", line), ("glowMaterial", glow), ("ringMaterial", ring));

            var rain = EnsureWorld<NoteRain>(manager.transform, "NoteRain", "Note rain", new Color(0.5f, 0.72f, 1f));
            SetReferences(rain, ("glowMaterial", glow), ("streakMaterial", EnsureMaterial(StreakMaterialPath, "MusicInYourHands/Streak")), ("ringMaterial", ring));

            var fireflies = EnsureWorld<FireflyChoir>(manager.transform, "FireflyChoir", "Firefly choir", new Color(0.79f, 0.61f, 1f));
            SetReferences(fireflies, ("glowMaterial", glow), ("lineMaterial", line));

            var pool = EnsureWorld<SoundPool>(manager.transform, "SoundPool", "Sound pool", new Color(0.37f, 0.83f, 1f));
            SetReferences(pool, ("waterMaterial", EnsureMaterial(WaterMaterialPath, "MusicInYourHands/Water")), ("glowMaterial", glow));

            var wind = EnsureWorld<SoundWind>(manager.transform, "SoundWind", "Sound wind", new Color(0.49f, 0.95f, 0.79f));
            SetReferences(wind, ("glowMaterial", glow), ("ringMaterial", ring));

            var bubbles = EnsureWorld<BubbleNotes>(manager.transform, "BubbleNotes", "Bubble notes", new Color(1f, 0.6f, 0.85f));
            SetReferences(bubbles, ("bubbleMaterial", EnsureMaterial(BubbleMaterialPath, "MusicInYourHands/Bubble")), ("ringMaterial", ring),
                ("glowMaterial", glow));

            var sand = EnsureWorld<SandTable>(manager.transform, "SandTable", "Sand table", new Color(1f, 0.86f, 0.68f));
            SetReferences(sand, ("plateMaterial", EnsureMaterial(PlateMaterialPath, "MusicInYourHands/Plate")), ("glowMaterial", glow));

            var so = new SerializedObject(manager);
            var list = so.FindProperty("worlds");
            var lettering = EnsureMaterial(LetteringMaterialPath, "MusicInYourHands/Lettering");
            var orbitType = EnsureWorld<OrbitType>(manager.transform, "OrbitType", "Orbit type", new Color(0.88f, 0.89f, 0.93f));
            SetReferences(orbitType, ("letteringMaterial", lettering), ("lineMaterial", line), ("glowMaterial", glow));

            var graffiti = EnsureWorld<Graffiti>(manager.transform, "Graffiti", "Graffiti", new Color(1f, 0.7f, 0.42f));
            SetReferences(graffiti, ("letteringMaterial", lettering), ("lineMaterial", line), ("glowMaterial", glow), ("ringMaterial", ring));

            World[] order = { livingSkin, orb, ribbon, strings, rain, fireflies, pool, wind, bubbles, sand, orbitType, graffiti };
            list.arraySize = order.Length;
            for (int i = 0; i < order.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // The palm-up wheel was replaced by the bubble menu.
            var oldWheel = GameObject.Find("WorldWheel");
            if (oldWheel != null) Object.DestroyImmediate(oldWheel);

            var menu = Object.FindAnyObjectByType<WorldMenu>(FindObjectsInactive.Include);
            if (menu == null) menu = new GameObject("WorldMenu", typeof(WorldMenu)).GetComponent<WorldMenu>();
            SetReferences(menu, ("bubbleMaterial", EnsureMaterial(BubbleMaterialPath, "MusicInYourHands/Bubble")), ("glowMaterial", glow),
                ("lineMaterial", line));
#if META_XR_CORE
            if (Object.FindAnyObjectByType<MenuButtonFeed>(FindObjectsInactive.Include) == null)
                new GameObject("MenuButton", typeof(MenuButtonFeed));
#endif
        }

        static T EnsureWorld<T>(Transform parent, string objectName, string displayName, Color color) where T : World
        {
            var world = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (world == null) world = new GameObject(objectName, typeof(T)).GetComponent<T>();
            world.transform.SetParent(parent, false);
            world.gameObject.SetActive(true);
            var so = new SerializedObject(world);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("color").colorValue = color;
            so.ApplyModifiedPropertiesWithoutUndo();
            return world;
        }

        static void SetReferences(Object target, params (string property, Object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (property, value) in values) so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public const string HandMaterialPath = Root + "/Materials/Hand.mat";
        public const string OrbMaterialPath = Root + "/Materials/Orb.mat";
        public const string OrbDotsMaterialPath = Root + "/Materials/OrbDots.mat";
        public const string LineMaterialPath = Root + "/Materials/Line.mat";
        public const string BubbleMaterialPath = Root + "/Materials/Bubble.mat";
        public const string StreakMaterialPath = Root + "/Materials/Streak.mat";
        public const string GlowMaterialPath = Root + "/Materials/Glow.mat";
        public const string WaterMaterialPath = Root + "/Materials/Water.mat";
        public const string PlateMaterialPath = Root + "/Materials/Plate.mat";
        public const string LetteringMaterialPath = Root + "/Materials/Lettering.mat";
        public const string RingMaterialPath = Root + "/Materials/Ring.mat";

        public static Material EnsureHandMaterial() => EnsureMaterial(HandMaterialPath, "MusicInYourHands/Hand");

        /// <summary>Loads the material, creating it from the shader the first time. Tuned values live in the asset.</summary>
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
