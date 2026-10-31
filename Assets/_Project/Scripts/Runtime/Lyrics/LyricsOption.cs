using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Whether the song's words are shown, from the bubble in the menu; remembered between runs. With lyrics off the
    /// sound worlds show no words, and the lyric worlds step through the song's title, as they do for a song without
    /// lyrics.
    /// </summary>
    public static class LyricsOption
    {
        const string Key = "lyrics";
        static bool? on;

        public static bool On
        {
            get => on ??= PlayerPrefs.GetInt(Key, 1) == 1;
            set
            {
                on = value;
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
