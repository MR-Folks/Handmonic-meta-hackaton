using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Finds sections in music as it plays, for songs with neither a sections file nor lyrics (added songs
    /// without an .lrc) and for music heard in the room. It keeps its own beat clock, locked to the beats
    /// the music bus detects and carried on through quiet parts, counts phrases of eight beats, and at the end of each
    /// phrase compares how loud the music was and how often the kick came with the section so far. A clear change
    /// starts a new section right there, on the phrase's downbeat, once the current one has run at least
    /// <see cref="MinSeconds"/>; after <see cref="MaxSeconds"/> a new section starts at the next phrase anyway. A
    /// loud phrase with a steady kick makes a chorus, a quiet one a verse, and one without a kick an instrumental
    /// break; near the end of a song of known length the last loud part is the final chorus and a quiet one the outro.
    /// </summary>
    public sealed class LiveSections
    {
        /// <summary>The shortest a section may be (s) before a change can start the next.</summary>
        public float MinSeconds = 20f;
        /// <summary>The longest a section may run (s) before the next phrase starts a new one anyway.</summary>
        public float MaxSeconds = 60f;
        /// <summary>How big a change counts as clear: 1 is the loudness doubling or halving.</summary>
        public float ChangeThreshold = 0.5f;
        /// <summary>Beats in a phrase; sections only ever start on a phrase's downbeat.</summary>
        public int PhraseBeats = 8;

        const int History = 256;
        readonly float[] beatLoudness = new float[History];
        readonly float[] beatKick = new float[History];

        SongSections into;
        float clock, phase, lastTime, sectionStart, loudest;
        int beat, sectionBeat, verses, choruses, breaks;
        float sumLoudness, sumKick;
        int frames;
        bool kicked, active;

        /// <summary>Starts finding sections in <paramref name="into"/>, from <paramref name="time"/> (song seconds).</summary>
        public void Begin(SongSections into, float length, float time, bool listening)
        {
            this.into = into;
            into.Clear();
            into.Source = SectionSource.Audio;
            into.Length = length;
            into.Sections.Add(new SongSection(time, SectionRole.Intro, listening ? "Listening" : time < 1f ? "Intro" : "Start"));
            clock = 0f;
            phase = 0f;
            beat = 0;
            sectionBeat = 0;
            sectionStart = time;
            lastTime = time;
            loudest = 0f;
            verses = choruses = breaks = 0;
            sumLoudness = sumKick = 0f;
            frames = 0;
            kicked = false;
            active = true;
        }

        public void End() => active = false;

        /// <summary>
        /// One frame of music: the song time, the loudness of the latest samples (RMS), whether the bus heard a beat,
        /// and its beat interval. True when a new section started this frame (it is the last one in the list).
        /// </summary>
        public bool Step(float time, float dt, float loudness, bool heardBeat, float beatInterval)
        {
            if (!active || into == null) return false;
            float advance = time - lastTime;
            lastTime = time;
            // Paused, or the song jumped: the clock waits (the journey starts the sections afresh after a jump).
            if (advance <= 0f || advance > 1f) return false;
            float interval = beatInterval > 0.25f && beatInterval < 1.5f ? beatInterval : 0.5f;
            phase += advance / interval;
            clock += advance;
            if (heardBeat)
            {
                kicked = true;
                // Lock gently to the heard beat when it is near one of ours.
                float nearest = Mathf.Round(phase);
                if (Mathf.Abs(phase - nearest) < 0.3f) phase = Mathf.Lerp(phase, nearest, 0.5f);
            }
            sumLoudness += loudness;
            frames++;

            if (phase < beat + 1) return false;
            // A beat went by: keep its loudness and whether a kick came, then look at the phrase.
            int slot = beat % History;
            beatLoudness[slot] = frames > 0 ? sumLoudness / frames : 0f;
            beatKick[slot] = kicked ? 1f : 0f;
            beat++;
            sumLoudness = 0f;
            frames = 0;
            kicked = false;

            int inSection = beat - sectionBeat;
            if (inSection <= 0 || inSection % PhraseBeats != 0) return false;
            Mean(beat - PhraseBeats, beat, out float phraseLoudness, out float phraseKick);
            loudest = Mathf.Max(loudest, phraseLoudness);
            float length = time - sectionStart;
            bool change = false;
            if (inSection >= PhraseBeats * 2)
            {
                Mean(sectionBeat, beat - PhraseBeats, out float before, out float beforeKick);
                float ratio = Mathf.Log((phraseLoudness + 0.002f) / (before + 0.002f), 2f);
                change = Mathf.Abs(ratio) + Mathf.Abs(phraseKick - beforeKick) * 0.8f >= ChangeThreshold;
            }
            // Half a beat's grace, so a phrase that ends right on the limit counts.
            float grace = interval * 0.5f;
            if (!(length >= MinSeconds - grace && change) && length < MaxSeconds - grace) return false;

            Cut(time, phraseLoudness, phraseKick);
            return true;
        }

        /// <summary>Starts a new section at <paramref name="time"/>, its role from the phrase that just played.</summary>
        void Cut(float time, float phraseLoudness, float phraseKick)
        {
            sectionStart = time;
            sectionBeat = beat;
            float level = loudest > 0f ? phraseLoudness / loudest : 0f;
            float remaining = into.Length > 0f ? into.Length - time : float.PositiveInfinity;
            SectionRole role;
            string label;
            if (level >= 0.7f && phraseKick >= 0.5f)
            {
                bool last = remaining < 75f && into.FinalIndex < 0;
                role = last ? SectionRole.FinalChorus : SectionRole.Chorus;
                label = last ? "Final chorus" : ++choruses == 1 ? "Chorus" : $"Chorus {choruses}";
            }
            else if (remaining < 30f)
            {
                role = SectionRole.Outro;
                label = "Outro";
            }
            else if (phraseKick < 0.25f)
            {
                role = SectionRole.Break;
                label = ++breaks == 1 ? "Break" : $"Break {breaks}";
            }
            else
            {
                role = SectionRole.Verse;
                label = $"Verse {++verses}";
            }
            into.Sections.Add(new SongSection(time, role, label));
        }

        void Mean(int from, int to, out float loudness, out float kick)
        {
            loudness = kick = 0f;
            from = Mathf.Max(from, to - History);
            int count = to - from;
            if (count <= 0) return;
            for (int b = from; b < to; b++)
            {
                loudness += beatLoudness[b % History];
                kick += beatKick[b % History];
            }
            loudness /= count;
            kick /= count;
        }
    }
}
