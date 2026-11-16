using System.IO;
using UnityEditor;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// The cassette deck's materials: one per material slot of the Blender models (and a few for its effects), kept
    /// in Resources/CassetteDeck/Materials so they, and their shaders, go into the build without being in the scene.
    /// Their colours and styles are set here each time, so this file is where to tune them. Also imports the
    /// exported models the way the deck needs: real size, Y up, no animation, and each material slot using the deck's
    /// material of the same name.
    /// </summary>
    public static class CassetteDeckSetup
    {
        public const string Folder = "Assets/_Project/Resources/CassetteDeck";
        public const string MaterialFolder = Folder + "/Materials";

        const string Deck = "MusicInYourHands/CassetteDeck", Glass = "MusicInYourHands/CassetteDeckGlass";

        [MenuItem("Music In Your Hands/Update Cassette Deck Materials")]
        public static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
            }

            // The palette of the Blender models: cream plastic with teal trim and orange keys, an amber light, a walnut
            // turntable on a honey-wood table with teal legs, smoky glass, and cassettes tinted per song.
            Make("Shell", Deck, new Color(1f, 1f, 1f), style: 0, gloss: 70, shine: 0.55f);
            Make("Label", Deck, Color.white, style: 3, gloss: 12, shine: 0.12f);
            Make("Window", Glass, new Color(0.45f, 0.5f, 0.56f, 0.16f), reflection: 0.3f);
            Make("Reel", Deck, new Color(0.84f, 0.83f, 0.8f), style: 0, gloss: 40, shine: 0.4f);
            Make("Tape", Deck, new Color(0.24f, 0.16f, 0.12f), style: 0, gloss: 30, shine: 0.3f);
            Make("RadioBody", Deck, new Color(0.96f, 0.92f, 0.8f), style: 0, gloss: 36, shine: 0.32f);
            Make("RadioTrim", Deck, new Color(0.06f, 0.6f, 0.55f), style: 0, gloss: 60, shine: 0.5f);
            Make("Glass", Glass, new Color(0.25f, 0.42f, 0.42f, 0.32f), reflection: 0.45f);
            Make("Speaker", Deck, new Color(0.17f, 0.18f, 0.19f), style: 2, gloss: 40, shine: 0.3f, size: PartSize(DeckParts.Radio, "SpeakerLeft", new Vector3(0.124f, 0.124f, 0.006f)));
            Make("Button", Deck, new Color(0.96f, 0.46f, 0.16f), style: 7, gloss: 60, shine: 0.5f, size: new Vector3(0.034f, 0.014f, 0.026f));
            Make("Light", Deck, new Color(0.98f, 0.76f, 0.28f), style: 5, gloss: 120, shine: 1f);
            Make("Turntable", Deck, new Color(0.47f, 0.28f, 0.16f), style: 1, gloss: 30, shine: 0.3f);
            Make("TurntableBase", Deck, new Color(0.36f, 0.21f, 0.12f), style: 1, gloss: 24, shine: 0.22f);
            Make("TableTop", Deck, new Color(0.86f, 0.66f, 0.43f), style: 1, gloss: 28, shine: 0.26f);
            Make("TableLegs", Deck, new Color(0.06f, 0.6f, 0.55f), style: 0, gloss: 50, shine: 0.45f);
            // Effects.
            var ghost = Make("Ghost", "MusicInYourHands/CassetteDeckGhost", new Color(0.55f, 1f, 0.8f, 1f));
            ghost.SetFloat("_Alpha", 1f);
            Make("Shadow", "MusicInYourHands/CassetteDeckShadow", new Color(0f, 0f, 0f, 0.45f));
            var rim = Make("Rim", "MusicInYourHands/Ring", new Color(0.6f, 0.85f, 1f, 1f));
            rim.SetFloat("_Radius", 0.95f);
            rim.SetFloat("_Thickness", 0.02f);
            rim.SetFloat("_InnerGlow", 0f);
            var plate = Make("Plate", "MusicInYourHands/Panel", new Color(0.05f, 0.055f, 0.09f, 0.74f));
            plate.SetColor("_Edge", new Color(1f, 1f, 1f, 0.16f));
            var glow = Make("DeckGlow", "MusicInYourHands/Glow", Color.white);
            glow.SetFloat("_Intensity", 1f);
            glow.SetFloat("_Softness", 1.6f);
            glow.SetFloat("_Core", 0.6f);
            AssetDatabase.SaveAssets();
            DeckParts.Forget();

            // An export that arrived before these materials existed is imported again, so it uses them.
            foreach (var model in new[] { DeckParts.Cassette, DeckParts.Radio, DeckParts.Turntable, DeckParts.Table })
            {
                var path = $"{Folder}/{model}.fbx";
                if (!File.Exists(path)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                bool stale = false;
                if (prefab != null)
                    foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                        foreach (var m in r.sharedMaterials)
                            if (m != null && !AssetDatabase.GetAssetPath(m).StartsWith(MaterialFolder)) stale = true;
                if (stale) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        public static void EnsureMaterialsBatch()
        {
            EnsureMaterials();
            EditorApplication.Exit(0);
        }

        static Material Make(string slot, string shaderName, Color color, float style = -1f, float gloss = -1f, float shine = -1f,
            float reflection = -1f, Vector3? size = null)
        {
            var path = $"{MaterialFolder}/{slot}.mat";
            var shader = Shader.Find(shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = slot };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            material.SetColor("_Color", color);
            if (style >= 0f) material.SetFloat("_Style", style);
            if (gloss >= 0f) material.SetFloat("_Gloss", gloss);
            if (shine >= 0f) material.SetFloat("_Shine", shine);
            if (reflection >= 0f) material.SetFloat("_Reflection", reflection);
            if (size.HasValue) material.SetVector("_Size", size.Value);
            // No GPU instancing: the shells share a mesh and a material and differ only by their per-renderer
            // tint, which instanced batches would drop.
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The size of one part of an exported model, or <paramref name="fallback"/> while there is no export.</summary>
        static Vector3 PartSize(string model, string part, Vector3 fallback)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{model}.fbx");
            var node = prefab != null ? DeckParts.Find(prefab.transform, part) : null;
            var filter = node != null ? node.GetComponent<MeshFilter>() : null;
            return filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.size : fallback;
        }
    }

    /// <summary>
    /// Imports the deck's exported models (Resources/CassetteDeck/*.fbx) at real size with Y up (no turned root), without
    /// cameras, lights or animation, and gives each material slot the deck's material of the same name, so the build
    /// carries no extra materials. The deck maps the slots by name at run time too, so this is a tidy-up, not a need.
    /// </summary>
    sealed class CassetteDeckImport : AssetPostprocessor
    {
        bool Ours => assetPath.Replace('\\', '/').StartsWith(CassetteDeckSetup.Folder + "/") && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!Ours) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!Ours || material == null) return null;
            var slot = DeckParts.SlotName(material.name);
            return AssetDatabase.LoadAssetAtPath<Material>($"{CassetteDeckSetup.MaterialFolder}/{slot}.mat");
        }
    }
}
