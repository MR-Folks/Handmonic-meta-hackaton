using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Store and submission media from the same staged scenes as the screenshots (see SceneShots): for each shot, a
    /// 2560x1440 still (the Meta Horizon Store's screenshot size) and a clip of 960x540 frames at 15 fps, played on
    /// from the shot's moment, for a GIF. Writes Logs/media/&lt;shot&gt;.png and Logs/media/&lt;shot&gt;/f000.png...
    /// Batch mode (needs graphics, so no -nographics):
    /// -executeMethod MusicInYourHands.EditorTools.EditorChecks.CaptureMediaBatch
    /// with SHOTS to pick the shots (as for the screenshots) and CLIP_SECONDS for the clip length (default 4).
    /// </summary>
    public static partial class EditorChecks
    {
        const string MediaFolder = "Logs/media";
        const float ClipFps = 15f;

        // Frames per clip while CaptureMediaBatch runs; 0 the rest of the time, so plain screenshots stay as they were.
        static int MediaClipFrames;

        public static void CaptureMediaBatch()
        {
            var seconds = float.TryParse(Environment.GetEnvironmentVariable("CLIP_SECONDS"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var s) ? s : 4f;
            MediaClipFrames = Mathf.Max(1, Mathf.RoundToInt(seconds * ClipFps));
            bool ok;
            try { ok = CaptureAll(); }
            finally { MediaClipFrames = 0; }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>The still and the clip for one shot; <paramref name="play"/> runs the world on to a song time.</summary>
        static void CaptureMedia(string name, Camera camera, Action<float> play, Func<float> now)
        {
            Directory.CreateDirectory(MediaFolder);
            SavePng(camera, 2560, 1440, $"{MediaFolder}/{name}.png");
            var frames = $"{MediaFolder}/{name}";
            if (Directory.Exists(frames)) Directory.Delete(frames, true);
            Directory.CreateDirectory(frames);
            for (int i = 0; i < MediaClipFrames; i++)
            {
                play(now() + 1f / ClipFps);
                SavePng(camera, 960, 540, $"{frames}/f{i:000}.png");
            }
            Debug.Log($"[Media] {name}: a still and {MediaClipFrames} frames");
        }

        // Like Render, but anti-aliased, and it frees the textures: a clip renders hundreds of frames.
        static void SavePng(Camera camera, int width, int height, string path)
        {
            var rt = new RenderTexture(width, height, 24) { antiAliasing = 8 };
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(image);
            }
        }
    }
}
