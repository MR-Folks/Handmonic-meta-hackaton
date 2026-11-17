using System;
using System.Collections.Generic;
using System.Globalization;

namespace MusicInYourHands
{
    /// <summary>
    /// The melody's notes from a .notes file: one note per line, "start-seconds midi length-seconds", lines starting
    /// with # are comments. Worlds use it to play in time with the song (rain lands on notes, strings guide plucks).
    /// </summary>
    public class NoteTrack
    {
        public readonly struct Note
        {
            public readonly float Time;
            public readonly int Midi;
            public readonly float Length;

            public Note(float time, int midi, float length)
            {
                Time = time;
                Midi = midi;
                Length = length;
            }

            public float Frequency => 440f * (float)Math.Pow(2, (Midi - 69) / 12.0);
        }

        public static readonly NoteTrack Empty = new NoteTrack(new List<Note>());

        readonly List<Note> notes;

        public IReadOnlyList<Note> Notes => notes;
        public bool IsEmpty => notes.Count == 0;

        /// <summary>
        /// True when the notes were made from the lyrics' word timings (<see cref="FromLyrics"/>): their times are the
        /// singing's, but their pitches are not the song's, so nothing should be tuned to them.
        /// </summary>
        public bool IsFromLyrics { get; private set; }

        NoteTrack(List<Note> notes) => this.notes = notes;

        // Pitches for notes made from lyrics: A minor pentatonic from middle C up, the scale worlds fall back to.
        static readonly int[] LyricPitches = { 60, 62, 64, 67, 69, 72, 74, 76, 79 };

        /// <summary>
        /// For a song with timed lyrics but no .notes melody (the ElevenLabs songs, people's own songs): one note per
        /// sung word, at the word's time and lasting until the next word (at most a second), so the worlds that play
        /// with the melody (bubbles, rain, droplets, twinkling planets) still move with the singing. The pitches only
        /// climb through each line and fall back at its end; they are not the sung melody (see <see cref="IsFromLyrics"/>).
        /// <paramref name="lyricsOffset"/> is the song's lyrics offset, so the notes land when the words are shown.
        /// </summary>
        public static NoteTrack FromLyrics(LyricsTrack lyrics, float lyricsOffset)
        {
            if (lyrics == null || lyrics.Lines.Count == 0) return Empty;
            var list = new List<Note>();
            var lines = lyrics.Lines;
            for (int l = 0; l < lines.Count; l++)
            {
                var words = lines[l].Words;
                for (int k = 0; k < words.Length; k++)
                {
                    float time = words[k].Time - lyricsOffset;
                    float next = k + 1 < words.Length ? words[k + 1].Time - lyricsOffset
                        : l + 1 < lines.Count ? lines[l + 1].Time - lyricsOffset : time + 0.6f;
                    float length = Math.Max(0.12f, Math.Min(1f, next - time));
                    // Up through the first half of the line and back down, each line starting a step higher.
                    int step = k < (words.Length + 1) / 2 ? k : words.Length - 1 - k;
                    int midi = LyricPitches[(step + l % 3) % LyricPitches.Length];
                    list.Add(new Note(Math.Max(0f, time), midi, length));
                }
            }
            list.Sort((a, b) => a.Time.CompareTo(b.Time));
            return new NoteTrack(list) { IsFromLyrics = true };
        }

        public static NoteTrack Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return Empty;
            var list = new List<Note>();
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var time)) continue;
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var midi)) continue;
                float length = parts.Length > 2 && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var l) ? l : 0.3f;
                list.Add(new Note(time, midi, length));
            }
            list.Sort((a, b) => a.Time.CompareTo(b.Time));
            return new NoteTrack(list);
        }

        /// <summary>Adds notes starting in [from, to) to <paramref name="into"/>, with their times shifted by <paramref name="shift"/>.</summary>
        public void Between(float from, float to, List<Note> into, float shift = 0f)
        {
            foreach (var n in notes)
            {
                if (n.Time < from) continue;
                if (n.Time >= to) break;
                into.Add(new Note(n.Time + shift, n.Midi, n.Length));
            }
        }
    }
}
