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
        /// <summary>Song position in seconds.</summary>
        public float SongTime;
        /// <summary>Melody notes starting in the next few seconds, in song time. May be null or empty.</summary>
        public System.Collections.Generic.List<NoteTrack.Note> Upcoming;
        /// <summary>The song's timed lyrics, and the line and word being sung. May be null (no lyrics).</summary>
        public LyricsTrack Lyrics;
        /// <summary>The song's melody, for sounds that should fit it. May be null.</summary>
        public NoteTrack Notes;
        /// <summary>Seconds since the last beat, and the typical time between beats (0 when unknown).</summary>
        public float BeatAge;
        public float BeatInterval;
        public LyricsTrack.State Lyric;

        /// <summary>The words of the line being sung, or null.</summary>
        public LyricsTrack.Word[] LineWords => Lyrics != null && Lyric.HasLine && Lyric.Line < Lyrics.Lines.Count ? Lyrics.Lines[Lyric.Line].Words : null;

        /// <summary>The word being sung, or an empty string.</summary>
        public string Word
        {
            get
            {
                var words = LineWords;
                return words != null && Lyric.Word >= 0 && Lyric.Word < words.Length ? words[Lyric.Word].Text : "";
            }
        }

        public float Band(int index) => Bands != null && index >= 0 && index < Bands.Length ? Bands[index] : 0.3f;
    }
}
