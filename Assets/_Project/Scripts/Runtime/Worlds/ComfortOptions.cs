namespace MusicInYourHands
{
    /// <summary>
    /// Comfort settings, from the bubbles in the menu's song row. Like the lyrics switch (LyricsOption), each lasts while
    /// the app runs and every launch starts from its default.
    /// </summary>
    public static class ComfortOptions
    {
        /// <summary>
        /// Less flashing: Lightning's flash becomes a gentle glow and its bolts dim (the thunder stays), and every beat
        /// flash and flare in the worlds and on the hands shows at about a quarter. Off by default. Whatever it says, no
        /// world flashes more than three times a second (World.Flash).
        /// </summary>
        public static bool LessFlashing { get; set; }

        /// <summary>How much of a beat flash or flare shows: a quarter with Less flashing on, all of it otherwise.</summary>
        public static float FlashScale => LessFlashing ? 0.25f : 1f;
    }
}
