using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Emoji as pictures for the menu, drawn on the headset with Android's own colour emoji font (EmojiPicture.java),
    /// once each, then kept. The app's lettering has no emoji, and it ships no emoji images. In the editor there is no
    /// emoji font to draw with, so Get returns null and the page shows the choice's name alone.
    /// </summary>
    public static class EmojiPictures
    {
        const int Size = 128;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string emoji)
        {
            if (string.IsNullOrEmpty(emoji)) return null;
            if (cache.TryGetValue(emoji, out var texture)) return texture;
            texture = Draw(emoji);
            cache[emoji] = texture;
            return texture;
        }

        static Texture2D Draw(string emoji)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var helper = new AndroidJavaClass("com.musicinyourhands.app.EmojiPicture");
                var base64 = helper.CallStatic<string>("png", emoji, Size);
                if (string.IsNullOrEmpty(base64)) return null;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
                {
                    name = $"Emoji {emoji}",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (texture.LoadImage(Convert.FromBase64String(base64)))
                {
                    texture.Apply(true, true);
                    return texture;
                }
                UnityEngine.Object.Destroy(texture);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Emoji] Couldn't draw {emoji}: {e.Message}");
            }
#endif
            return null;
        }
    }
}
