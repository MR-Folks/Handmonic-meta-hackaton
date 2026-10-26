using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Follows the lyrics for a lyric world: tells it, once per change, which word has just started and whether it
    /// starts a new line. Songs without lyrics (or listening to the room) still give the world words to show: the
    /// song's title, a word every four beats, with a new "line" each time the title starts over.
    /// </summary>
    public sealed class LyricCursor
    {
        string key = "";
        int lastLine = int.MinValue, fallbackStep = -1;

        public string Word { get; private set; } = "";
        /// <summary>The line the word belongs to (lyrics) or a counter that changes when the title starts over.</summary>
        public int Line { get; private set; } = -1;
        /// <summary>The word's position in its line.</summary>
        public int Index { get; private set; }
        /// <summary>All the words of the current line, when the song has lyrics; otherwise null.</summary>
        public LyricsTrack.Word[] LineWords { get; private set; }
        /// <summary>The text of every word in the current line (the lyric line, or the title's words).</summary>
        public string[] LineTexts { get; private set; } = System.Array.Empty<string>();

        public void Reset()
        {
            key = "";
            lastLine = int.MinValue;
            fallbackStep = -1;
            Word = "";
        }

        /// <summary>True on the frame a new word starts; <paramref name="newLine"/> says whether it begins a new line.</summary>
        public bool Advance(MusicFrame music, out bool newLine)
        {
            newLine = false;
            string word = music.Word;
            string next;
            int line, index;
            if (!string.IsNullOrEmpty(word))
            {
                next = $"{music.Lyric.Line}:{music.Lyric.Word}";
                line = music.Lyric.Line;
                index = music.Lyric.Word;
                LineWords = music.LineWords;
                if (LineWords != null && (LineTexts.Length != LineWords.Length || line != Line))
                {
                    LineTexts = new string[LineWords.Length];
                    for (int i = 0; i < LineWords.Length; i++) LineTexts[i] = LineWords[i].Text;
                }
            }
            else
            {
                // Between lines of a song with lyrics, wait; without lyrics, step through the title on the beat.
                if (music.LineWords != null || (music.Lyrics != null && music.Lyrics.Lines.Count > 0)) return false;
                var title = music.Bus != null ? music.Bus.Title : "Music";
                var parts = title.Split(' ');
                int step = music.Bus != null ? music.Bus.BeatCount / 4 : Mathf.FloorToInt(music.SongTime / 2f);
                if (step == fallbackStep) return false;
                fallbackStep = step;
                index = step % parts.Length;
                line = step / parts.Length;
                word = parts[index];
                next = "title:" + step;
                LineWords = null;
                LineTexts = parts;
            }
            if (next == key) return false;
            key = next;
            newLine = line != lastLine;
            lastLine = line;
            Word = word;
            Line = line;
            Index = index;
            return true;
        }
    }
}
