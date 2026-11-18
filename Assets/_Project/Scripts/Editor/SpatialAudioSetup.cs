using UnityEditor;
using UnityEngine;
#if META_XR_AUDIO
using Meta.XR.Acoustics;
#endif

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// The project settings spatial audio needs, as Meta's Meta XR Audio setup page asks: Meta XR Audio as the
    /// spatializer and the ambisonic decoder, stereo output and the smallest DSP buffer (Best latency); and Meta's own
    /// settings assets in Assets/Resources, with the lightweight Shoebox Room as the acoustic model (RoomAcoustics
    /// sizes it from the room scan). Safe to run more than once. Project setup runs it; on its own:
    /// -executeMethod MusicInYourHands.EditorTools.SpatialAudioSetup.RunBatch
    /// </summary>
    public static class SpatialAudioSetup
    {
        public const string PluginName = "Meta XR Audio";
        /// <summary>The DSP buffer size Unity's "Best latency" stands for.</summary>
        public const int BestLatency = 256;
        public const string AudioManagerPath = "ProjectSettings/AudioManager.asset";
        public const string MixerAssetPath = "Assets/_Project/Resources/" + SpatialAudio.MixerPath + ".mixer";
        public const string AcousticSettingsPath = "Assets/Resources/MetaXRAcousticSettings.asset";
        public const string AudioSettingsPath = "Assets/Resources/MetaXRAudioSettings.asset";

        /// <summary>
        /// Meta XR Audio gets its voice limit as a player starts (MetaXRAudioSource, before the first scene loads), and
        /// until it has one it turns every spatialized source away: outside Play mode each one warned "Audio source
        /// failed to initialize audio spatializer". The editor gets the same limit as it loads, so the checks and
        /// previews build spatialized sources as the headset does.
        /// </summary>
        [InitializeOnLoadMethod]
        static void GiveEditorVoiceLimit()
        {
#if META_XR_AUDIO
            var settings = Resources.Load<MetaXRAudioSettings>("MetaXRAudioSettings");
            try
            {
                MetaXRAudio_Unity_SetGlobalVoiceLimit(settings != null ? settings.voiceLimit : 64);
            }
            catch (System.Exception e) when (e is System.DllNotFoundException || e is System.EntryPointNotFoundException)
            {
                Debug.LogWarning($"[Setup] Couldn't give Meta XR Audio its voice limit in the editor: {e.Message}");
            }
#endif
        }

#if META_XR_AUDIO
        [System.Runtime.InteropServices.DllImport(MetaXRAudioNativeInterface.UnityNativeInterface.binaryName)]
        static extern int MetaXRAudio_Unity_SetGlobalVoiceLimit(int voiceLimit);
#endif

        [MenuItem("Music In Your Hands/Set Up Spatial Audio")]
        public static void Configure()
        {
            var audioManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(AudioManagerPath)[0]);
            audioManager.FindProperty("m_SpatializerPlugin").stringValue = PluginName;
            audioManager.FindProperty("m_AmbisonicDecoderPlugin").stringValue = PluginName;
            audioManager.FindProperty("Default Speaker Mode").intValue = (int)AudioSpeakerMode.Stereo;
            audioManager.FindProperty("m_DSPBufferSize").intValue = BestLatency;
            var requested = audioManager.FindProperty("m_RequestedDSPBufferSize");
            if (requested != null) requested.intValue = BestLatency;
            audioManager.ApplyModifiedPropertiesWithoutUndo();
            if (System.Array.IndexOf(AudioSettings.GetSpatializerPluginNames(), PluginName) < 0)
                Debug.LogWarning($"[Setup] The {PluginName} plugin isn't loaded in this editor yet; the setting is saved and takes effect once it is.");

#if META_XR_AUDIO
            // Meta's settings assets, made here rather than the first time someone presses Play.
            var acoustics = AssetDatabase.LoadAssetAtPath<MetaXRAcousticSettings>(AcousticSettingsPath);
            if (acoustics == null)
            {
                EnsureResourcesFolder();
                acoustics = ScriptableObject.CreateInstance<MetaXRAcousticSettings>();
                AssetDatabase.CreateAsset(acoustics, AcousticSettingsPath);
            }
            var model = new SerializedObject(acoustics);
            model.FindProperty("acousticModel").intValue = (int)AcousticModel.ShoeboxRoom;
            model.ApplyModifiedPropertiesWithoutUndo();

            if (AssetDatabase.LoadAssetAtPath<MetaXRAudioSettings>(AudioSettingsPath) == null)
            {
                EnsureResourcesFolder();
                // Meta's default voice limit (64) is twice the 32 voices Unity plays at once, so no spatial voice is cut.
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MetaXRAudioSettings>(), AudioSettingsPath);
            }
#else
            Debug.LogWarning("[Setup] com.meta.xr.sdk.audio isn't installed; sounds will play without Meta XR Audio.");
#endif
            if (AssetDatabase.LoadMainAssetAtPath(MixerAssetPath) == null)
                Debug.LogError($"[Setup] {MixerAssetPath} is missing: spatial sounds need it for the room's reflections and reverb.");

            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] Spatial audio: {PluginName} spatializes and decodes ambisonics, stereo output, DSP buffer {BestLatency} (Best latency), Shoebox Room acoustics.");
        }

        public static void RunBatch()
        {
            Configure();
            EditorApplication.Exit(0);
        }

        static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }
}
