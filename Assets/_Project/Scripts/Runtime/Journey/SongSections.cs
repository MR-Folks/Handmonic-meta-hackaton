using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>What a part of a song does. Journey mode picks the kind of world for each part from it.</summary>
    public enum SectionRole { Intro, Verse, PreChorus, Chorus, Bridge, Break, FinalChorus, Outro }

    /// <summary>Where a song's sections came from: its own sections file, its lyrics, or the audio as it plays.</summary>
    public enum SectionSource { None, Authored, Lyrics, Audio }

    /// <summary>One part of a song: where it starts (song seconds), what it does, and the name the song map shows.</summary>
    public struct SongSection
    {
        public float Start;
        public SectionRole Role;
        public string Label;

        public SongSection(float start, SectionRole role, string label)
        {
            Start = start;
            Role = role;
            Label = label;
        }
    }

    /// <summary>
    /// A song cut into its sections (intro, verses, choruses, bridge, final chorus, outro), with its tempo, beats per
    /// bar and key when they are known. The built-in songs have a sections file next to their lyrics (see
    /// <see cref="Parse"/>); other songs get theirs from their lyrics (<see cref="SectionFinder"/>) or from the audio as
    /// it plays (<see cref="LiveSections"/>).
    /// </summary>
    public sealed class SongSections
    {
        public readonly List<SongSection> Sections = new List<SongSection>(24);
        public SectionSource Source;
        /// <summary>Beats per minute, 0 when unknown.</summary>
        public float Bpm;
        public int BeatsPerBar = 4;
        /// <summary>The key's tonic as a pitch class (0 = C, 9 = A), -1 when unknown.</summary>
        public int KeyTonic = -1;
        public bool KeyMinor;
        /// <summary>The audio's length (s), 0 when unknown (listening to the room).</summary>
        public float Length;
        /// <summary>Where the music itself ends (s), when the file says so; 0 means at the end of the audio.</summary>
        public float End;
        /// <summary>The last downbeat of the final chorus, where the finale blooms; -1 for none.</summary>
        public float FinaleTime = -1f;

        public int Count => Sections.Count;
        public SongSection this[int index] => Sections[index];

        /// <summary>When the music is over: the file's end mark, else the audio's length (0 when unknown).</summary>
        public float MusicEnd => End > 0f ? End : Length;

        /// <summary>When section <paramref name="index"/> ends: the next one's start, or the end of the music.</summary>
        public float EndOf(int index) =>
            index + 1 < Sections.Count ? Sections[index + 1].Start : MusicEnd > 0f ? MusicEnd : float.PositiveInfinity;

        /// <summary>Seconds in a bar: from the tempo when known, else from the beat the music bus hears.</summary>
        public float BarSeconds(float beatInterval) =>
            Bpm > 0f ? 60f / Bpm * Mathf.Max(1, BeatsPerBar) : beatInterval > 0.2f ? beatInterval * 4f : 2f;

        /// <summary>The section playing at <paramref name="time"/> (the first one before it starts), or -1 without sections.</summary>
        public int IndexAt(float time)
        {
            int lo = 0, hi = Sections.Count - 1, found = Sections.Count > 0 ? 0 : -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (Sections[mid].Start <= time) { found = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return found;
        }

        /// <summary>The final chorus (the last section with that role), or -1.</summary>
        public int FinalIndex
        {
            get
            {
                for (int i = Sections.Count - 1; i >= 0; i--)
                    if (Sections[i].Role == SectionRole.FinalChorus) return i;
                return -1;
            }
        }

        public void Clear()
        {
            Sections.Clear();
            Source = SectionSource.None;
            Bpm = 0f;
            BeatsPerBar = 4;
            KeyTonic = -1;
            KeyMinor = false;
            Length = 0f;
            End = 0f;
            FinaleTime = -1f;
        }

        public void CopyFrom(SongSections other)
        {
            Clear();
            Sections.AddRange(other.Sections);
            Source = other.Source;
            Bpm = other.Bpm;
            BeatsPerBar = other.BeatsPerBar;
            KeyTonic = other.KeyTonic;
            KeyMinor = other.KeyMinor;
            Length = other.Length;
            End = other.End;
            FinaleTime = other.FinaleTime;
        }

        // ---- The sections file ----

        static readonly char[] Space = { ' ', '\t' };

        /// <summary>
        /// Reads a sections file into <paramref name="into"/>: one line per section, its start (m:ss.xx, or seconds)
        /// and its label ("intro", "verse 1", "pre-chorus", "chorus", "bridge", "interlude", "final chorus", "outro"),
        /// and a line "m:ss.xx end" where the music is over. Lines "bpm 118", "beats 4" (per bar) and "key Ab major"
        /// give the tempo, metre and key; lines starting with # are notes. Lines it can't read are skipped. False when
        /// it finds no section.
        /// </summary>
        public static bool Parse(string text, SongSections into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int cut = line.IndexOfAny(Space);
                string head = cut < 0 ? line : line.Substring(0, cut);
                string rest = cut < 0 ? "" : line.Substring(cut + 1).Trim();
                switch (head.ToLowerInvariant())
                {
                    case "bpm":
                    case "tempo":
                        if (float.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out float bpm) && bpm > 20f && bpm < 400f) into.Bpm = bpm;
                        continue;
                    case "beats":
                    case "meter":
                        if (int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out int beats) && beats > 0 && beats < 16) into.BeatsPerBar = beats;
                        continue;
                    case "key":
                        ParseKey(rest, out into.KeyTonic, out into.KeyMinor);
                        continue;
                }
                if (!TryTime(head, out float time) || rest.Length == 0) continue;
                if (rest.Equals("end", System.StringComparison.OrdinalIgnoreCase)) into.End = time;
                else into.Sections.Add(new SongSection(time, RoleOf(rest), Capitalise(rest)));
            }
            into.Sections.Sort((a, b) => a.Start.CompareTo(b.Start));
            into.Source = into.Sections.Count > 0 ? SectionSource.Authored : SectionSource.None;
            return into.Sections.Count > 0;
        }

        /// <summary>The role a section's label names. Unknown labels count as verses.</summary>
        public static SectionRole RoleOf(string label)
        {
            var l = (label ?? "").ToLowerInvariant();
            bool chorus = l.Contains("chorus") || l.Contains("hook") || l.Contains("drop") || l.Contains("refrain");
            if (chorus && (l.Contains("final") || l.Contains("last"))) return SectionRole.FinalChorus;
            if (l.Contains("pre-chorus") || l.Contains("prechorus") || l.Contains("pre chorus") || l.Contains("build")) return SectionRole.PreChorus;
            if (chorus) return SectionRole.Chorus;
            if (l.Contains("bridge") || l.Contains("middle")) return SectionRole.Bridge;
            if (l.Contains("interlude") || l.Contains("break") || l.Contains("instrumental") || l.Contains("solo")) return SectionRole.Break;
            if (l.Contains("intro")) return SectionRole.Intro;
            if (l.Contains("outro") || l.Contains("ending") || l.Contains("coda")) return SectionRole.Outro;
            return SectionRole.Verse;
        }

        static readonly string[] NoteNames = { "c", "c#", "d", "d#", "e", "f", "f#", "g", "g#", "a", "a#", "b" };
        static readonly string[] FlatNames = { "c", "db", "d", "eb", "e", "f", "gb", "g", "ab", "a", "bb", "b" };

        /// <summary>Reads a key such as "Ab major", "F minor", "F#m" or "Am".</summary>
        public static bool ParseKey(string text, out int tonic, out bool minor)
        {
            tonic = -1;
            minor = false;
            var t = (text ?? "").Trim().ToLowerInvariant();
            if (t.Length == 0) return false;
            int length = t.Length > 1 && (t[1] == '#' || t[1] == 'b') ? 2 : 1;
            var name = t.Substring(0, length);
            for (int i = 0; i < 12; i++)
                if (NoteNames[i] == name || FlatNames[i] == name) tonic = i;
            if (tonic < 0) return false;
            var mode = t.Substring(length).Trim();
            minor = mode.StartsWith("min") || mode == "m";
            return true;
        }

        static bool TryTime(string text, out float seconds)
        {
            seconds = 0f;
            int colon = text.IndexOf(':');
            if (colon < 0) return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f;
            if (!int.TryParse(text.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes)) return false;
            if (!float.TryParse(text.Substring(colon + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float rest)) return false;
            seconds = minutes * 60f + rest;
            return minutes >= 0 && rest >= 0f;
        }

        static string Capitalise(string label) =>
            string.IsNullOrEmpty(label) ? "" : char.ToUpperInvariant(label[0]) + label.Substring(1);

        // ---- Tidying ----

        /// <summary>
        /// Fits the sections to a song <paramref name="length"/> seconds long: leaves out any that start after its music
        /// ends (a sections file longer than its song), starts the first with the song, makes sure there is a final
        /// chorus (the last chorus, or failing that the last big section) and notes its last downbeat for the finale, and
        /// folds sections shorter than <paramref name="minSeconds"/> into a neighbour (a short intro into what follows,
        /// anything else into what came before), since a world needs a little while to be seen.
        /// </summary>
        public void Tidy(float length, float minSeconds)
        {
            if (length > 0f) Length = length;
            if (Length > 0f && End > Length) End = 0f;
            Sections.Sort((a, b) => a.Start.CompareTo(b.Start));
            float end = MusicEnd;
            for (int i = Sections.Count - 1; i >= 0 && end > 0f; i--)
                if (Sections[i].Start >= end - 2f) Sections.RemoveAt(i);
            if (Sections.Count == 0) return;
            SetStart(0, 0f);

            int final = FinalIndex;
            if (final < 0)
            {
                for (int i = Sections.Count - 1; i >= 0 && final < 0; i--)
                    if (Sections[i].Role == SectionRole.Chorus) final = i;
                if (final >= 0) Promote(final, true);
            }
            if (final < 0)
            {
                // No chorus at all: the longest section in the second half that isn't an intro, outro or break.
                float best = -1f;
                for (int i = 0; i < Sections.Count; i++)
                {
                    var role = Sections[i].Role;
                    float span = Mathf.Min(EndOf(i), 1e5f) - Sections[i].Start;
                    if (role == SectionRole.Intro || role == SectionRole.Outro || role == SectionRole.Break) continue;
                    if (end > 0f && Sections[i].Start < end * 0.4f && Sections.Count > 2) continue;
                    if (span > best) { best = span; final = i; }
                }
                if (final < 0) final = Sections.Count - 1;
                Promote(final, false);
            }
            // The finale blooms on the final chorus's last downbeat: a bar before it ends (section starts are downbeats).
            float finalEnd = EndOf(final);
            FinaleTime = float.IsInfinity(finalEnd) ? -1f
                : Mathf.Max(Sections[final].Start + 1f, finalEnd - (Bpm > 0f ? BarSeconds(0f) : 2.5f));

            for (int guard = 0; guard < 64 && Sections.Count > 1; guard++)
            {
                int shortest = -1;
                for (int i = 0; i < Sections.Count && shortest < 0; i++)
                    if (Mathf.Min(EndOf(i), 1e5f) - Sections[i].Start < minSeconds) shortest = i;
                if (shortest < 0) break;
                var gone = Sections[shortest];
                Sections.RemoveAt(shortest);
                // The section that takes its time takes on a final chorus's role too.
                int taker = shortest == 0 ? 0 : shortest - 1;
                if (shortest == 0) SetStart(0, 0f);
                if (gone.Role == SectionRole.FinalChorus) Promote(taker, false);
            }
        }

        void SetStart(int index, float start)
        {
            var s = Sections[index];
            s.Start = start;
            Sections[index] = s;
        }

        void Promote(int index, bool rename)
        {
            var s = Sections[index];
            s.Role = SectionRole.FinalChorus;
            if (rename && !string.IsNullOrEmpty(s.Label)) s.Label = "Final " + char.ToLowerInvariant(s.Label[0]) + s.Label.Substring(1);
            Sections[index] = s;
        }
    }
}
