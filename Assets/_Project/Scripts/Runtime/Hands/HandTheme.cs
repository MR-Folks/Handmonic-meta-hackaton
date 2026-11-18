using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>How the hands look in a world: colours and which effect the hand shader draws.</summary>
    public struct HandTheme
    {
        public enum Effect { Veins = 0, Heat = 1, Scanlines = 2, Electric = 3, Ripples = 4, Specks = 5, Submerged = 6, Flow = 7, Film = 8, Uplight = 9, Halftone = 10, Paint = 11 }

        public Color Fill;
        public Color Edge;
        public Color Glow;
        public Effect Pattern;
        public Color PatternColor;

        /// <summary>The default look, Breathing room's: violet with pulses of light running through the veins.</summary>
        public static HandTheme Veins => new HandTheme
        {
            Fill = new Color(0.19f, 0.11f, 0.36f, 0.62f),
            Edge = new Color(0.62f, 0.56f, 1f),
            Glow = new Color(1f, 0.37f, 0.68f),
            Pattern = Effect.Veins,
            PatternColor = new Color(1f, 0.37f, 0.68f)
        };
    }
}
