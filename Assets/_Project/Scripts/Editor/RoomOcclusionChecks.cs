using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if META_XR_CORE
using Meta.XR.EnvironmentDepth;
using MusicInYourHands.Meta;
#endif
#if UNITY_OPENXR
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
#endif

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        const string HardOcclusion = "HARD_OCCLUSION", SoftOcclusion = "SOFT_OCCLUSION";

        // The menu's panel and the hands never hide behind the real room, so their shaders don't take part at all.
        static readonly string[] NeverHidden = { "MusicInYourHands/Hand", "MusicInYourHands/Panel" };

        /// <summary>
        /// Hidden by the real room (Meta's Depth API): every shader a world hidden by the real room draws with has Meta's
        /// occlusion keywords; the shared ones (with _RoomOcclusion: glow, ring, line, lettering, bubble) have it off in
        /// every material asset, so the menu's bubbles, the effects on the hands and the other worlds stay in front; the
        /// hands' and the menu panel's shaders have no keywords; outside play, InRoom gives back the shared material, so
        /// the editor's previews draw as before; and the project has what the Depth API needs: the depth manager in
        /// Main.unity, saved switched off, with the real hands kept in its depth map, the OpenXR session and occlusion
        /// features on for Android, and Scene support Required.
        /// </summary>
        static bool CheckRoomOcclusion()
        {
            bool ok = true;
            var holder = new GameObject("Room occlusion");
            var hidden = new List<string>();
            try
            {
                var inRoom = typeof(World).GetMethod("InRoom", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var world in WorldCatalog.MakePreviews(holder.transform))
                {
                    if (!world.HiddenByRealRoom) continue;
                    var type = world.GetType();
                    hidden.Add(world.DisplayName);
                    foreach (var field in WorldCatalog.MaterialFields(type))
                    {
                        var name = WorldCatalog.MaterialName(type, field.Name);
                        var shader = Shader.Find($"MusicInYourHands/{name}");
                        if (shader == null || TakesPart(shader)) continue;
                        Debug.LogError($"[Check] {world.DisplayName} is hidden by the real room, but its {field.Name} shader {shader.name} " +
                                       $"has no #pragma multi_compile _ {HardOcclusion} {SoftOcclusion} (see RoomOcclusion.hlsl).");
                        ok = false;
                    }
                    // In the editor there is no room depth: the shared material comes back unchanged.
                    var glow = ProjectSetup.WorldMaterial(type, "glowMaterial");
                    if (inRoom != null && !ReferenceEquals(inRoom.Invoke(world, new object[] { glow, -1f }), glow))
                    {
                        Debug.LogError($"[Check] {type.Name}.InRoom made a copy outside play; the editor's previews must keep the shared materials.");
                        ok = false;
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
            if (hidden.Count == 0)
            {
                Debug.LogError("[Check] No world is hidden by the real room (World.HiddenByRealRoom).");
                ok = false;
            }

            foreach (var name in NeverHidden)
            {
                var shader = Shader.Find(name);
                if (shader == null || !TakesPart(shader)) continue;
                Debug.LogError($"[Check] {name} has the real-room occlusion keywords; the hands and the menu must stay in front of the room.");
                ok = false;
            }

            // The shared shaders take part only through a world's own copies: every material asset keeps it off.
            int shared = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || material.shader == null || !material.HasProperty("_RoomOcclusion")) continue;
                shared++;
                if (material.GetFloat("_RoomOcclusion") == 0f) continue;
                Debug.LogError($"[Check] {AssetDatabase.GetAssetPath(material)} has _RoomOcclusion on; the menu and the other worlds share it, " +
                               "so it must stay 0 (worlds use World.InRoom for their own copies).");
                ok = false;
            }

            ok &= CheckRoomDepthSetup();
            if (ok)
                Debug.Log($"[Check] Hidden by the real room: {string.Join(", ", hidden)}; their shaders take part, {shared} shared " +
                          "materials keep it off, the hands and menu panel don't take part, and the depth manager is set up.");
            return ok;
        }

        /// <summary>True when the shader declares Meta's occlusion keywords.</summary>
        static bool TakesPart(Shader shader)
        {
            var names = shader.keywordSpace.keywordNames;
            return names.Contains(HardOcclusion) && names.Contains(SoftOcclusion);
        }

        /// <summary>The depth manager in Main.unity and the project settings the Depth API needs.</summary>
        static bool CheckRoomDepthSetup()
        {
            bool ok = true;
#if META_XR_CORE
            var scene = EditorSceneManager.OpenScene(ProjectSetup.MainScenePath, OpenSceneMode.Additive);
            try
            {
                var feeds = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoomDepthFeed>(true)).ToList();
                var managers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnvironmentDepthManager>(true)).ToList();
                if (feeds.Count != 1 || managers.Count != 1 || managers[0].gameObject != feeds[0].gameObject)
                {
                    Debug.LogError($"[Check] Main.unity should have one RoomDepth object with RoomDepthFeed and Meta's EnvironmentDepthManager " +
                                   $"(found {feeds.Count} feeds, {managers.Count} managers). Run Music In Your Hands > Update Worlds in Main Scene.");
                    ok = false;
                }
                else
                {
                    if (managers[0].enabled)
                    {
                        Debug.LogError("[Check] The EnvironmentDepthManager is saved switched on; it must start off, so only the worlds hidden by the real room pay for depth.");
                        ok = false;
                    }
                    if (managers[0].RemoveHands)
                    {
                        Debug.LogError("[Check] The EnvironmentDepthManager removes the hands from the depth map; real hands must pass in front of the worlds.");
                        ok = false;
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            if (OVRProjectConfig.CachedProjectConfig.sceneSupport != OVRProjectConfig.FeatureSupport.Required)
            {
                Debug.LogError("[Check] Scene support isn't Required in Meta's project config, as the Depth API asks. Run Update Worlds in Main Scene.");
                ok = false;
            }
#endif
#if UNITY_OPENXR
            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            foreach (var name in new[] { "ARSessionFeature", "AROcclusionFeature" })
            {
                var feature = openXR != null ? openXR.GetFeatures<OpenXRFeature>().FirstOrDefault(f => f.GetType().Name == name) : null;
                if (feature != null && feature.enabled) continue;
                Debug.LogError($"[Check] The OpenXR feature {name} is {(feature == null ? "missing (is Unity OpenXR: Meta installed?)" : "off")} for Android; " +
                               "the depth maps reach Meta's depth manager through it. Run Update Worlds in Main Scene.");
                ok = false;
            }
#endif
            return ok;
        }
    }
}
