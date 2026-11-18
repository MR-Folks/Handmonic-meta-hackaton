using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Store and demo media from the same staged scenes as the screenshots (see SceneShots), shot in a lit, furnished
    /// room (SceneMediaRoom) instead of the grey stand-in: for each shot, a 2560x1440 still (the Meta Horizon Store's
    /// screenshot size) and a clip of 960x540 frames at 15 fps, played on from the shot's moment with the hands
    /// moving, for a GIF. Writes Logs/media/&lt;shot&gt;.png and Logs/media/&lt;shot&gt;/f000.png...
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

        // Called by Shot before every simulation step while a clip plays, to move the hands (song time in seconds).
        static Action<float> MediaHands;

#if META_XR_CORE
        /// <summary>
        /// The still and the clip for one shot; <paramref name="play"/> runs the world on to a song time. During the
        /// clip the hands play: they drift apart and together and rise and fall a little, the way people move them to
        /// music, and are back where they started when the clip ends, so the GIF loops without a jump.
        /// </summary>
        static void CaptureMedia(string name, Camera camera, Action<float> play, Func<float> now,
            System.Collections.Generic.List<PreviewHand> hands, HandRig rig)
        {
            Directory.CreateDirectory(MediaFolder);
            SavePng(camera, 2560, 1440, $"{MediaFolder}/{name}.png");
            var frames = $"{MediaFolder}/{name}";
            if (Directory.Exists(frames)) Directory.Delete(frames, true);
            Directory.CreateDirectory(frames);

            float start = now(), length = MediaClipFrames / ClipFps;
            var basePositions = hands.ConvertAll(h => h.Root.transform.position);
            MediaHands = songTime =>
            {
                float phase = Mathf.Clamp01((songTime - start) / length) * Mathf.PI * 2f;
                for (int i = 0; i < hands.Count; i++)
                {
                    float outward = hands[i].Left ? -1f : 1f;
                    var offset = new Vector3(outward * 0.07f * Mathf.Sin(phase),
                        0.04f * Mathf.Sin(phase * 2f + (hands[i].Left ? 0f : Mathf.PI)),
                        0.03f * Mathf.Sin(phase));
                    hands[i].Root.transform.position = basePositions[i] + offset;
                    PushHand(rig, hands[i]);
                }
            };
            try
            {
                for (int i = 0; i < MediaClipFrames; i++)
                {
                    play(now() + 1f / ClipFps);
                    SavePng(camera, 960, 540, $"{frames}/f{i:000}.png");
                }
            }
            finally
            {
                MediaHands = null;
            }
            Debug.Log($"[Media] {name}: a still and {MediaClipFrames} frames");
        }
#endif

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
