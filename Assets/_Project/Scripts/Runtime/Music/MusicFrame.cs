namespace MusicInYourHands
{
    /// <summary>What the music is doing this frame, handed to every world. Previews build one by hand.</summary>
    public struct MusicFrame
    {
        /// <summary>Six bands, 0-1: sub, bass, low mids, mids, presence, air. May be null.</summary>
        public float[] Bands;
        /// <summary>1 on a beat, decaying towards 0.</summary>
        public float Kick;
        /// <summary>True on the frame a beat was detected.</summary>
        public bool Beat;
        /// <summary>The latest audio samples, -1 to 1. May be null.</summary>
        public float[] Waveform;
        /// <summary>The bus, when there is one: worlds may change its volume.</summary>
        public MusicBus Bus;

        public float Band(int index) => Bands != null && index >= 0 && index < Bands.Length ? Bands[index] : 0.3f;
    }
}
