using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Finds a song's sections from its timed lyrics, for songs without a sections file: lines that come back (sung
    /// twice or more) make the choruses, the other lines the verses, and a long gap between sung lines is an
    /// instrumental part. The music before the first line is the intro, after the last line the outro, and lines that
    /// come only after two choruses form the bridge. The last chorus is the final chorus.
    /// </summary>
    public static class SectionFinder
    {
        /// <summary>A gap between sung lines this long (s) or more is an instrumental part of its own.</summary>
        public const float BreakGap = 8f;
        /// <summary>Sections shorter than this (s) fold into a neighbour.</summary>
        public const float MinSeconds = 8f;
        /// <summary>Sections longer than this (s) are split at the sung line nearest their middle.</summary>
        public const float MaxSeconds = 60f;

        sealed class Block
        {
            public bool Chorus;
            public float Start, End;
            public int First, Last;
        }

        /// <summary>
        /// Fills <paramref name="into"/> from <paramref name="lyrics"/> (line times minus <paramref name="offset"/> are
        /// song times) for a song <paramref name="length"/> seconds long. False when the song has no sung lines.
        /// </summary>
        public static bool FromLyrics(LyricsTrack lyrics, float offset, float length, SongSections into)
        {
            into.Clear();
            var lines = lyrics?.Lines;
            if (lines == null || lines.Count == 0) return false;

            // How often each line is sung, by its words alone.
            var texts = new string[lines.Count];
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < lines.Count; i++)
            {
                texts[i] = Normalise(lines[i].Words);
                counts.TryGetValue(texts[i], out int n);
                counts[texts[i]] = n + 1;
            }

            // Blocks of lines of one kind, broken by a change of kind or a long gap.
            var blocks = new List<Block>();
            for (int i = 0; i < lines.Count; i++)
            {
                bool chorus = texts[i].Length > 0 && counts[texts[i]] > 1;
                float start = lines[i].Time - offset, end = LineEnd(lines, i, offset);
                var last = blocks.Count > 0 ? blocks[blocks.Count - 1] : null;
                if (last == null || last.Chorus != chorus || start - last.End >= BreakGap)
                    blocks.Add(new Block { Chorus = chorus, Start = start, End = end, First = i, Last = i });
                else
                {
                    last.End = Mathf.Max(last.End, end);
                    last.Last = i;
                }
            }

            int verses = 0, choruses = 0;
            if (blocks[0].Start > 3f) into.Sections.Add(new SongSection(0f, SectionRole.Intro, "Intro"));
            for (int b = 0; b < blocks.Count; b++)
            {
                var block = blocks[b];
                if (b > 0 && block.Start - blocks[b - 1].End >= BreakGap)
                    into.Sections.Add(new SongSection(blocks[b - 1].End + 0.5f, SectionRole.Break, "Instrumental"));
                if (block.Chorus)
                {
                    choruses++;
                    into.Sections.Add(new SongSection(block.Start, SectionRole.Chorus, "Chorus"));
                }
                else if (choruses >= 2)
                    into.Sections.Add(new SongSection(block.Start, SectionRole.Bridge, "Bridge"));
                else
                    into.Sections.Add(new SongSection(block.Start, SectionRole.Verse, $"Verse {++verses}"));
                SplitLong(into, lines, block, offset);
            }
            var lastBlock = blocks[blocks.Count - 1];
            if (length <= 0f || length - lastBlock.End > 6f)
                into.Sections.Add(new SongSection(lastBlock.End + 0.5f, SectionRole.Outro, "Outro"));
            if (into.Sections.Count > 0) into.Sections[0] = new SongSection(0f, into.Sections[0].Role, into.Sections[0].Label);

            into.Source = SectionSource.Lyrics;
            into.Tidy(length, MinSeconds);
            return into.Count > 0;
        }

        /// <summary>A block longer than MaxSeconds gets a second start at the line nearest its middle, with the same role.</summary>
        static void SplitLong(SongSections into, IReadOnlyList<LyricsTrack.Line> lines, Block block, float offset)
        {
            if (block.End - block.Start <= MaxSeconds) return;
            var head = into.Sections[into.Sections.Count - 1];
            float middle = (block.Start + block.End) * 0.5f;
            int best = -1;
            for (int i = block.First + 1; i <= block.Last; i++)
                if (best < 0 || Mathf.Abs(lines[i].Time - offset - middle) < Mathf.Abs(lines[best].Time - offset - middle)) best = i;
            if (best > 0) into.Sections.Add(new SongSection(lines[best].Time - offset, head.Role, head.Label));
        }

        /// <summary>
        /// When a line has been sung: its last word plus a moment, but never past the next line's start. A word timed long
        /// after the rest of its line (an echo in the outro, or a slip of the timing) doesn't stretch the line.
        /// </summary>
        static float LineEnd(IReadOnlyList<LyricsTrack.Line> lines, int i, float offset)
        {
            var words = lines[i].Words;
            float last = lines[i].Time;
            for (int k = 0; k < words.Length; k++)
            {
                if (words[k].Time - last > 3f) break;
                last = Mathf.Max(last, words[k].Time);
            }
            float end = last - offset + 1.2f;
            if (i + 1 < lines.Count) end = Mathf.Min(end, lines[i + 1].Time - offset);
            return end;
        }

        static readonly StringBuilder Text = new StringBuilder(128);

        /// <summary>A line's words in lower case without punctuation, so the same line sung again matches.</summary>
        static string Normalise(LyricsTrack.Word[] words)
        {
            Text.Clear();
            foreach (var word in words)
            {
                foreach (char c in word.Text)
                    if (char.IsLetterOrDigit(c)) Text.Append(char.ToLowerInvariant(c));
                Text.Append(' ');
            }
            return Text.ToString().Trim();
        }
    }
}
