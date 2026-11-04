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
        // The word shown last, by number so following it makes no garbage each frame: a lyric word (line, word), a
        // step through the title (TitleKey, step), or none.
        const int NoKey = int.MinValue, TitleKey = int.MinValue + 1;
        int keyLine = NoKey, keyWord;
        int lastLine = int.MinValue, fallbackStep = -1;
        // The title split into words, kept until the title changes.
        string splitTitle;
        string[] titleWords = System.Array.Empty<string>();

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
            keyLine = NoKey;
            lastLine = int.MinValue;
            fallbackStep = -1;
            Word = "";
        }

        /// <summary>True on the frame a new word starts; <paramref name="newLine"/> says whether it begins a new line.</summary>
        public bool Advance(MusicFrame music, out bool newLine)
        {
            newLine = false;
            string word = music.Word;
            int nextLine, nextWord;
            int line, index;
            if (!string.IsNullOrEmpty(word))
            {
                nextLine = music.Lyric.Line;
                nextWord = music.Lyric.Word;
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
                if (LyricsOption.On && (music.LineWords != null || (music.Lyrics != null && music.Lyrics.Lines.Count > 0))) return false;
                int step = music.Bus != null ? music.Bus.BeatCount / 4 : Mathf.FloorToInt(music.SongTime / 2f);
                if (step == fallbackStep) return false;
                fallbackStep = step;
                var parts = TitleWords(music.Bus != null ? music.Bus.Title : "Music");
                index = step % parts.Length;
                line = step / parts.Length;
                word = parts[index];
                nextLine = TitleKey;
                nextWord = step;
                LineWords = null;
                LineTexts = parts;
            }
            if (nextLine == keyLine && nextWord == keyWord) return false;
            keyLine = nextLine;
            keyWord = nextWord;
            newLine = line != lastLine;
            lastLine = line;
            Word = word;
            Line = line;
            Index = index;
            return true;
        }

        /// <summary>The title's words, split again only when the title changes.</summary>
        string[] TitleWords(string title)
        {
            title ??= "";
            if (title != splitTitle)
            {
                splitTitle = title;
                titleWords = title.Split(' ');
            }
            return titleWords;
        }
    }
}
