using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// One-click project setup: URP tuned for Quest, Android player settings, the demo song asset
    /// and a Main scene with MusicBus and the debug view. Safe to run more than once.
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/_Project";
        const string UrpPath = Root + "/Settings/URP-Quest.asset";
        const string RendererPath = Root + "/Settings/URP-Quest_Renderer.asset";
        const string SongPath = Root + "/Songs/GlassRoom.asset";
        const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("Music In Your Hands/Run Project Setup")]
        public static void Run()
        {
            EnsureUrp();
            ConfigurePlayer();
            var song = EnsureDemoSong();
            EnsureMainScene(song);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Music In Your Hands project setup finished.");
        }

        /// <summary>Entry point for -executeMethod in batch mode.</summary>
        public static void RunBatch() => Run();

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path)) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
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

            // Quest-friendly defaults: 4x MSAA, no HDR, no post-processing cost.
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

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Music In Your Hands";
            PlayerSettings.productName = "Music In Your Hands";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.musicinyourhands.app");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
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

        static void EnsureMainScene(SongAsset song)
        {
            EnsureFolder(Root + "/Scenes");
            var scene = File.Exists(ScenePath)
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

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
