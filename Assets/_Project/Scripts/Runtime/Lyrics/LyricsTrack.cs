using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MusicInYourHands
{
    /// <summary>
    /// Timed lyrics parsed from an LRC file. Supports line times ([mm:ss.xx]), word times
    /// (&lt;mm:ss.xx&gt;, "enhanced LRC") and the [offset:+/-ms] tag. When a line has no word
    /// times, its words are spread across the line, with longer words taking longer.
    /// </summary>
    public class LyricsTrack
    {
        public readonly struct Word
        {
            public readonly string Text;
            public readonly float Time;

            public Word(string text, float time)
            {
                Text = text;
                Time = time;
            }
        }

        public sealed class Line
        {
            public readonly float Time;
            public readonly Word[] Words;

            public Line(float time, Word[] words)
            {
                Time = time;
                Words = words;
            }
        }

        /// <summary>Where the song is in the lyrics. Line is -1 before the first line starts.</summary>
        public readonly struct State : IEquatable<State>
        {
            public readonly int Line;
            public readonly int Word;

            public State(int line, int word)
            {
                Line = line;
                Word = word;
            }

            public bool HasLine => Line >= 0;
            public bool Equals(State other) => Line == other.Line && Word == other.Word;
            public override bool Equals(object obj) => obj is State s && Equals(s);
            public override int GetHashCode() => Line * 1000 + Word;
        }

        public static readonly LyricsTrack Empty = new LyricsTrack(new List<Line>());

        static readonly Regex LineTag = new Regex(@"\[(\d+):(\d+(?:\.\d+)?)\]");
        static readonly Regex WordTag = new Regex(@"<(\d+):(\d+(?:\.\d+)?)>\s*([^<]*)");
        static readonly Regex OffsetTag = new Regex(@"\[offset:\s*([+-]?\d+)\s*\]", RegexOptions.IgnoreCase);
        static readonly Regex AnyTag = new Regex(@"\[[^\]]*\]");
        static readonly char[] Space = { ' ', '\t' };

        readonly List<Line> lines;

        public IReadOnlyList<Line> Lines => lines;

        LyricsTrack(List<Line> lines) => this.lines = lines;

        public static LyricsTrack Parse(string lrc)
        {
            if (string.IsNullOrEmpty(lrc)) return Empty;

            var raw = new List<(float time, string text)>();
            float offset = 0f;
            foreach (var rawLine in lrc.Split('\n'))
            {
                var s = rawLine.TrimEnd('\r');
                var offsetMatch = OffsetTag.Match(s);
                if (offsetMatch.Success)
                {
                    // Positive offsets make lyrics appear earlier.
                    offset = int.Parse(offsetMatch.Groups[1].Value, CultureInfo.InvariantCulture) / 1000f;
                    continue;
                }

                var tags = LineTag.Matches(s);
                if (tags.Count == 0) continue;
                var text = AnyTag.Replace(s, "").Trim();
                foreach (Match m in tags) raw.Add((ToSeconds(m), text));
            }

            for (int i = 0; i < raw.Count; i++) raw[i] = (raw[i].time - offset, raw[i].text);
            raw.Sort((a, b) => a.time.CompareTo(b.time));

            var parsed = new List<Line>();
            for (int i = 0; i < raw.Count; i++)
            {
                var (time, text) = raw[i];
                if (text.Length == 0) continue;
                float next = i + 1 < raw.Count ? raw[i + 1].time : time + 5f;
                var words = text.Contains('<') ? ParseTimedWords(text, time, offset) : SpreadWords(text, time, next);
                if (words.Length > 0) parsed.Add(new Line(time, words));
            }
            return new LyricsTrack(parsed);
        }

        static Word[] ParseTimedWords(string text, float lineTime, float offset)
        {
            var words = new List<Word>();
            int firstTag = text.IndexOf('<');
            foreach (var w in text.Substring(0, firstTag).Split(Space, StringSplitOptions.RemoveEmptyEntries))
                words.Add(new Word(w, lineTime));

            foreach (Match m in WordTag.Matches(text))
            {
                float t = ToSeconds(m) - offset;
                var parts = m.Groups[3].Value.Split(Space, StringSplitOptions.RemoveEmptyEntries);
                for (int k = 0; k < parts.Length; k++) words.Add(new Word(parts[k], t + k * 0.05f));
            }
            return words.ToArray();
        }

        static Word[] SpreadWords(string text, float time, float next)
        {
            var parts = text.Split(Space, StringSplitOptions.RemoveEmptyEntries);
            float span = Math.Min(next - time, parts.Length * 0.9f + 1f) * 0.85f;
            float total = 0f;
            foreach (var p in parts) total += 1f + p.Length * 0.15f;

            var words = new Word[parts.Length];
            float acc = 0f;
            for (int k = 0; k < parts.Length; k++)
            {
                words[k] = new Word(parts[k], time + acc / total * span);
                acc += 1f + parts[k].Length * 0.15f;
            }
            return words;
        }

        static float ToSeconds(Match m) =>
            int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 60f +
            float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);

        /// <summary>The line and word being sung at <paramref name="time"/> seconds.</summary>
        public State Lookup(float time)
        {
            int lo = 0, hi = lines.Count - 1, line = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (lines[mid].Time <= time) { line = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            if (line < 0) return new State(-1, -1);

            var words = lines[line].Words;
            int word = 0;
            for (int k = 1; k < words.Length && words[k].Time <= time; k++) word = k;
            return new State(line, word);
        }
    }
}
