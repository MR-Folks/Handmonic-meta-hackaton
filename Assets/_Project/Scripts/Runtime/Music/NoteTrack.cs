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

        NoteTrack(List<Note> notes) => this.notes = notes;

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
