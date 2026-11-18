using System.IO;
using UnityEditor;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// The onboarding's assets, kept in Resources/Onboarding so they (and their shaders) go into the build without being
    /// in the scene: the ghost look and the glow, ring and plate materials (their values are set here each time, so this
    /// is where to tune them), and the two ghost hands. A ghost hand is a copy of Meta's OpenXR hand prefab with only its
    /// bones and its mesh left (nothing that tracks it), wearing the ghost material. The hands are made once; use the
    /// menu item to make them again.
    /// </summary>
    public static class OnboardingSetup
    {
        public const string Folder = "Assets/_Project/Resources/Onboarding";
        const string MeshFolder = "Assets/_Project/Meshes";

        [MenuItem("Music In Your Hands/Update Onboarding Assets")]
        public static void Rebuild() => EnsureAssets(true);

        /// <summary>Makes or refreshes the materials, and makes the ghost hands if they are missing (or always, with <paramref name="remakeHands"/>).</summary>
        public static void EnsureAssets(bool remakeHands = false)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }
            var ghost = Make("OnboardingGhost", "MusicInYourHands/OnboardingGhost");
            ghost.SetColor("_Color", new Color(0.8f, 0.9f, 1f, 1f));
            ghost.SetColor("_Highlight", new Color(1f, 0.84f, 0.45f, 1f));
            ghost.SetFloat("_Fill", 0.045f);
            ghost.SetFloat("_RimPower", 3f);
            ghost.SetFloat("_RimStrength", 0.5f);
            ghost.SetFloat("_OutlineWidth", 0.002f);
            var glow = Make("OnboardingGlow", "MusicInYourHands/Glow");
            glow.SetColor("_Color", Color.white);
            glow.SetFloat("_Intensity", 1f);
            glow.SetFloat("_Softness", 1.8f);
            glow.SetFloat("_Core", 0.55f);
            var ring = Make("OnboardingRing", "MusicInYourHands/Ring");
            ring.SetColor("_Color", new Color(0.86f, 0.92f, 1f, 1f));
            ring.SetFloat("_Radius", 0.86f);
            ring.SetFloat("_Thickness", 0.07f);
            ring.SetFloat("_InnerGlow", 0.2f);
            var plate = Make("OnboardingPlate", "MusicInYourHands/Panel");
            // Opaque enough that a world's floating words passing behind it never show through the letters.
            plate.SetColor("_Color", new Color(0.04f, 0.045f, 0.075f, 0.88f));
            plate.SetColor("_Edge", new Color(1f, 1f, 1f, 0.14f));
            plate.SetFloat("_Radius", 0.016f);
            AssetDatabase.SaveAssets();

            foreach (var side in new[] { "L", "R" })
            {
                var path = $"{Folder}/OnboardingGhostHand_{side}.prefab";
                if (remakeHands || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) MakeGhostHand(side, path, ghost);
                StoreShapes(side, path);
            }
        }

        /// <summary>
        /// Finds the hand's thumb shapes (GhostHand's search) and stores them on the prefab, so the headset only reads them.
        /// Left as they are when they are already up to date, so the prefab only changes when the search does.
        /// </summary>
        static void StoreShapes(string side, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var parent = new GameObject("Ghost hand solve");
            var ghost = new GhostHand(parent.transform, side == "L", null, solve: true);
            var shapes = new Vector3[GhostHand.ShapeCount];
            for (int i = 0; i < shapes.Length; i++) shapes[i] = ghost.Thumb((ThumbShape)i);
            float pinch = ghost.PinchIndex;
            bool valid = ghost.IsValid;
            Object.DestroyImmediate(parent);
            if (!valid) return;

            var root = PrefabUtility.LoadPrefabContents(path);
            var stored = root.GetComponent<GhostHandShapes>();
            if (stored == null) stored = root.AddComponent<GhostHandShapes>();
            bool same = stored.Has(GhostHand.SolverVersion, shapes.Length) && Mathf.Abs(stored.PinchIndex - pinch) < 0.001f;
            for (int i = 0; same && i < shapes.Length; i++) same = (stored.Thumb(i) - shapes[i]).sqrMagnitude < 0.0001f;
            if (!same)
            {
                stored.Store(GhostHand.SolverVersion, shapes, pinch);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Setup] Stored the thumb shapes of {path} (pinch index curl {pinch:0.00}).");
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        static Material Make(string name, string shaderName)
        {
            var path = $"{Folder}/{name}.mat";
            var shader = Shader.Find(shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Meta's hand prefab with everything but its bones and mesh taken off, in the ghost material.</summary>
        static void MakeGhostHand(string side, string path, Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Packages/com.meta.xr.sdk.core/Prefabs/OpenXRCustomHandPrefab_{side}.prefab");
            if (source == null)
            {
                Debug.LogError($"[Setup] Meta's OpenXRCustomHandPrefab_{side} wasn't found; no ghost hand made.");
                return;
            }
            var hand = (GameObject)Object.Instantiate(source);
            hand.name = $"OnboardingGhostHand_{side}";
            // Scripts first (they may need the other components), then anything else that isn't a bone or the mesh.
            foreach (var script in hand.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
            foreach (var component in hand.GetComponentsInChildren<Component>(true))
                if (component != null && !(component is Transform) && !(component is SkinnedMeshRenderer)) Object.DestroyImmediate(component);

            var skin = hand.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin != null)
            {
                skin.sharedMaterial = material;
                // The game's readable copy of the hand mesh, which the player's own hands already use, when there is one.
                var copy = skin.sharedMesh != null ? AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshFolder}/{skin.sharedMesh.name}.asset") : null;
                if (copy != null) skin.sharedMesh = copy;
                skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                skin.receiveShadows = false;
                skin.skinnedMotionVectors = false;
                skin.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                skin.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            PrefabUtility.SaveAsPrefabAsset(hand, path);
            Object.DestroyImmediate(hand);
            Debug.Log($"[Setup] Made the ghost hand {path}.");
        }
    }
}
