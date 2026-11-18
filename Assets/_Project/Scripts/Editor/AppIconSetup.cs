using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Makes Assets/_Project/Icons/AppIcon.png (drawn by Tools/make_logo.py) the app's icon: the default icon and
    /// every Android legacy and round launcher size. Adaptive icons stay empty, so launchers use those.
    /// Batch mode: -executeMethod MusicInYourHands.EditorTools.AppIconSetup.ApplyBatch
    /// </summary>
    public static class AppIconSetup
    {
        const string IconPath = "Assets/_Project/Icons/AppIcon.png";

        [MenuItem("Music In Your Hands/Apply App Icon")]
        public static void Apply()
        {
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(IconPath) is TextureImporter importer)
            {
                // Icons are scaled down by the build; keep the source sharp and uncompressed.
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.SaveAndReimport();
            }

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogError($"[Icon] {IconPath} is missing.");
                return;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            foreach (var kind in new[] { AndroidPlatformIconKind.Legacy, AndroidPlatformIconKind.Round })
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var slot in icons)
                    slot.SetTextures(icon);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Icon] {IconPath} is the app icon (default, Android legacy and round).");
        }

        public static void ApplyBatch()
        {
            Apply();
            EditorApplication.Exit(AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath) != null ? 0 : 1);
        }
    }
}
