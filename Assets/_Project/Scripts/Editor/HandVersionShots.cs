using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Close-ups of the right hand open, half closed and in a fist, drawn with every hand shader found in
    /// Assets/_HandVersions (old copies of Hand.shader, renamed) and with the current one. Writes
    /// Logs/hand-versions/*.png, one strip per shader. Batch mode (needs graphics):
    /// -executeMethod MusicInYourHands.EditorTools.EditorChecks.CaptureHandVersionsBatch
    /// </summary>
    public static partial class EditorChecks
    {
        const string HandVersionFolder = "Assets/_HandVersions";
        const string HandVersionOut = "Logs/hand-versions";

        [MenuItem("Music In Your Hands/Capture Hand Versions")]
        public static void CaptureHandVersions() => CaptureHandVersionsAll();

        public static void CaptureHandVersionsBatch() => EditorApplication.Exit(CaptureHandVersionsAll() ? 0 : 1);

        static bool CaptureHandVersionsAll()
        {
#if META_XR_CORE
            Directory.CreateDirectory(HandVersionOut);
            var shaders = AssetDatabase.IsValidFolder(HandVersionFolder)
                ? AssetDatabase.FindAssets("t:Shader", new[] { HandVersionFolder })
                    .Select(g => AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(s => s.name).ToList()
                : new System.Collections.Generic.List<Shader>();
            shaders.Add(Shader.Find("MusicInYourHands/Hand"));

            var hands = StageHands(out var camera);
            // Only the right hand, close up.
            Object.DestroyImmediate(hands.First(h => h.Left).Root);
            var hand = hands.First(h => !h.Left);
            var bones = hand.Root.GetComponentsInChildren<Transform>();
            Transform Bone(string n) => bones.First(t => t.name == n);
            var baseMaterial = hand.Skin.sharedMaterial;
            var center = hand.Skin.bounds.center;
            var normal = PalmNormal(Bone, false);

            // Each finger bends about (along x palm normal), joint by joint; the thumb also swings across the palm.
            string[] names = { "Thumb", "Index", "Middle", "Ring", "Little" };
            var joints = new System.Collections.Generic.List<(Transform bone, Quaternion rest, Vector3 axis, float angle)>();
            for (int f = 0; f < 5; f++)
            {
                var (parts, angles) = f == 0
                    ? (new[] { "Proximal", "Distal" }, new[] { 35f, 45f })
                    : (new[] { "Proximal", "Intermediate", "Distal" }, new[] { 75f, 100f, 65f });
                var along = (Bone($"XRHand_{names[f]}Tip").position - Bone($"XRHand_{names[f]}Proximal").position).normalized;
                var axis = Vector3.Cross(along, normal).normalized;
                foreach (var (part, angle) in parts.Zip(angles, (p, a) => (p, a)))
                {
                    var bone = Bone($"XRHand_{names[f]}{part}");
                    joints.Add((bone, bone.localRotation, Quaternion.Inverse(bone.rotation) * axis, angle));
                }
            }
            var thumbBase = Bone("XRHand_ThumbMetacarpal");
            var thumbRest = thumbBase.localRotation;
            var swingAxis = Quaternion.Inverse(thumbBase.rotation) * normal;

            void Bend(float curl, float swing)
            {
                thumbBase.localRotation = thumbRest * Quaternion.AngleAxis(swing * curl, swingAxis);
                foreach (var (bone, rest, axis, angle) in joints) bone.localRotation = rest * Quaternion.AngleAxis(angle * curl, axis);
            }

            // Pick the thumb swing direction that brings the thumb over the fingers rather than away from them.
            float SwingDistance(float swing)
            {
                Bend(1f, swing);
                return Vector3.Distance(Bone("XRHand_ThumbTip").position, Bone("XRHand_MiddleIntermediate").position);
            }
            float thumbSwing = SwingDistance(40f) < SwingDistance(-40f) ? 40f : -40f;

            // Views: palm side, from the thumb side, and the back of the hand.
            var shots = new (string name, float curl, float yaw, float pitch)[]
            {
                ("open-palm", 0f, 0f, 0f), ("half-palm", 0.55f, 0f, 0f), ("fist-palm", 1f, 0f, 0f),
                ("fist-below", 1f, 0f, -45f), ("fist-side", 1f, 70f, 0f), ("fist-back", 1f, 180f, 0f)
            };
            camera.fieldOfView = 34f;
            camera.backgroundColor = new Color(0.36f, 0.32f, 0.28f);
            var toCamera = (camera.transform.position - center).normalized;
            const int size = 420;

            // Batch mode does not re-skin the hand between renders, so each pose is baked into a plain mesh that
            // stands in for the skinned one.
            var baked = new Mesh();
            var standIn = new GameObject("Baked hand");
            standIn.AddComponent<MeshFilter>().sharedMesh = baked;
            var standInRenderer = standIn.AddComponent<MeshRenderer>();
            hand.Skin.enabled = false;
            var block = new MaterialPropertyBlock();

            foreach (var shader in shaders)
            {
                if (shader == null) continue;
                var material = new Material(baseMaterial) { shader = shader };
                standInRenderer.sharedMaterial = material;
                var strip = new Texture2D(size * shots.Length, size, TextureFormat.RGB24, false);
                for (int i = 0; i < shots.Length; i++)
                {
                    var (_, curl, yaw, pitch) = shots[i];
                    Bend(curl, thumbSwing);
                    hand.Skin.BakeMesh(baked, true);
                    standIn.transform.SetPositionAndRotation(hand.Skin.transform.position, hand.Skin.transform.rotation);
                    var dir = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.right) * toCamera;
                    camera.transform.position = center + dir * 0.42f;
                    camera.transform.LookAt(center);
                    hand.Root.GetComponent<HandSkin>().Apply();
                    // At rest: no beat pulse, a little music energy.
                    SetBeat(hand.Skin, 10f, 0f);
                    hand.Skin.GetPropertyBlock(block);
                    standInRenderer.SetPropertyBlock(block);
                    strip.SetPixels(i * size, 0, size, size, Render(camera, size, size).GetPixels());
                }
                strip.Apply();
                var label = shader.name.Replace("HandVersions/", "").Replace("MusicInYourHands/Hand", "current");
                File.WriteAllBytes($"{HandVersionOut}/{label}.png", strip.EncodeToPNG());
                Object.DestroyImmediate(material);
            }
            hand.Skin.sharedMaterial = baseMaterial;
            File.WriteAllText($"{HandVersionOut}/columns.txt", string.Join(",", shots.Select(s => s.name)));
            Debug.Log($"[HandVersions] Saved {shaders.Count} strips to {HandVersionOut}.");
            return true;
#else
            Debug.LogError("[HandVersions] Needs the Meta XR Core SDK for the hand prefab.");
            return false;
#endif
        }
    }
}
