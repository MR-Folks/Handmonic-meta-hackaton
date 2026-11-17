using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Whether the song's words are shown, from the bubble in the menu. Every launch starts with lyrics on, so a switch
    /// tapped off by mistake doesn't hide them for good. With lyrics off the sound worlds show no words, and the lyric
    /// worlds step through the song's title, as they do for a song without lyrics.
    /// </summary>
    public static class LyricsOption
    {
        // Earlier builds remembered the switch under this key, and a saved "off" left every world showing only the
        // song's title. It is cleared once, the first time the option is read.
        const string OldKey = "lyrics";
        static bool? on;

        public static bool On
        {
            get
            {
                if (on == null)
                {
                    on = true;
                    if (PlayerPrefs.HasKey(OldKey))
                    {
                        PlayerPrefs.DeleteKey(OldKey);
                        PlayerPrefs.Save();
                    }
                }
                return on.Value;
            }
            set => on = value;
        }
    }
}
