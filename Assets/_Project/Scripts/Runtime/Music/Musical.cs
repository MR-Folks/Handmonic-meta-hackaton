using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Makes the sounds the worlds play when you touch something belong to the song. A touch asks for a note; it gets
    /// the nearest note that is being sung right now (or was just sung, or is about to be), so it always fits the
    /// melody's harmony. Without a melody nearby it falls back to the notes the song uses overall, and without a note
    /// track to A minor pentatonic. Notes stay in the melody's register, play with a soft tone below the song, and
    /// land on the next half-beat when that is only a moment away, so they also sit in time.
    /// </summary>
    public static class Musical
    {
        static readonly int[] Pentatonic = { 0, 2, 4, 7, 9 }; // A minor pentatonic as classes from C: C D E G A
        static NoteTrack cachedTrack;
        static readonly bool[] songScale = new bool[12];
        static readonly bool[] nearby = new bool[12];

        /// <summary>The note to play for a touch that wants <paramref name="wanted"/> (MIDI), fitted to the song.</summary>
        public static int Pitch(MusicFrame music, int wanted)
        {
            wanted = Fold(wanted);
            var track = music.Notes;
            bool any = false;
            System.Array.Clear(nearby, 0, 12);
            int sounding = -1;
            if (track != null && !track.IsEmpty)
            {
                // Pitch classes sung from a beat and a half ago to half a second ahead.
                float now = music.SongTime;
                foreach (var note in track.Notes)
                {
                    if (note.Time > now + 0.5f) break;
                    if (note.Time + note.Length < now - 1.5f) continue;
                    nearby[Mod12(note.Midi)] = true;
                    any = true;
                    if (note.Time <= now && now < note.Time + note.Length) sounding = note.Midi;
                }
            }
            // A touch close to the sung note simply joins it.
            if (sounding >= 0 && Mathf.Abs(Fold(sounding) - wanted) <= 2) return Fold(sounding);
            var allowed = any ? nearby : Scale(track);
            int best = wanted, bestDistance = 99;
            for (int d = 0; d <= 6; d++)
            {
                foreach (int candidate in new[] { wanted - d, wanted + d })
                {
                    if (!allowed[Mod12(candidate)] || d >= bestDistance) continue;
                    best = candidate;
                    bestDistance = d;
                }
                if (bestDistance < 99) break;
            }
            return Fold(best);
        }

        /// <summary>
        /// Seconds to wait so a sound lands on the next half-beat, or 0 when that is further away than
        /// <paramref name="maxDelay"/> (a longer wait would feel late) or the beat is unknown.
        /// </summary>
        public static float Delay(MusicFrame music, float maxDelay = 0.07f)
        {
            float step = music.BeatInterval * 0.5f;
            if (step < 0.1f || music.BeatAge > music.BeatInterval * 4f) return 0f;
            float wait = step - Mathf.Repeat(music.BeatAge, step);
            return wait <= maxDelay ? wait : 0f;
        }

        /// <summary>Plays a fitted note at a place in the room, in time with the song.</summary>
        public static int Play(AudioPool voices, MusicFrame music, int wanted, Vector3 at, float volume, bool pluck = false)
        {
            int midi = Pitch(music, wanted);
            float frequency = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
            voices.Play(pluck ? Synth.Pluck(frequency) : Synth.Soft(frequency), at, volume, 1f, Delay(music));
            return midi;
        }

        /// <summary>The pitch classes the whole song's melody uses (or A minor pentatonic without one).</summary>
        static bool[] Scale(NoteTrack track)
        {
            if (track == cachedTrack && track != null) return songScale;
            cachedTrack = track;
            System.Array.Clear(songScale, 0, 12);
            bool any = false;
            if (track != null)
                foreach (var note in track.Notes) { songScale[Mod12(note.Midi)] = true; any = true; }
            if (!any) foreach (int pc in Pentatonic) songScale[pc] = true;
            return songScale;
        }

        /// <summary>Keeps a note in the melody's comfortable register, around middle C to two octaves up.</summary>
        static int Fold(int midi)
        {
            while (midi < 57) midi += 12;
            while (midi > 81) midi -= 12;
            return midi;
        }

        static int Mod12(int midi) => ((midi % 12) + 12) % 12;
    }
}
