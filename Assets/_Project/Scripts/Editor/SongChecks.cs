using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        const string AddedSongName = "Demo Band - Paper Boat";

        /// <summary>
        /// Every built-in song has audio, lyrics and one melody note per sung word, all inside the song; and a song
        /// added on the headset (a WAV and a line-timed LRC in a Songs folder) is found, named and loaded.
        /// </summary>
        static bool CheckSongs()
        {
            bool ok = true;
            var songs = AssetDatabase.FindAssets("t:SongAsset", new[] { "Assets/_Project/Songs" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SongAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
            foreach (var song in songs)
            {
                var lyrics = song.lyrics != null ? LyricsTrack.Parse(song.lyrics.text) : LyricsTrack.Empty;
                var notes = song.notes != null ? NoteTrack.Parse(song.notes.text) : NoteTrack.Empty;
                // The rendered demo songs have melody notes; the ElevenLabs songs have none, as with people's own songs.
                if (song.clip == null || lyrics.Lines.Count == 0 || (song.notes != null && notes.IsEmpty))
                {
                    Debug.LogError($"[Check] Song {song.name} is missing its audio or lyrics, or its notes are empty.");
                    ok = false;
                    continue;
                }
                int words = lyrics.Lines.Sum(line => line.Words.Length);
                var lastLine = lyrics.Lines[lyrics.Lines.Count - 1];
                if (lastLine.Words[lastLine.Words.Length - 1].Time > song.clip.length)
                {
                    Debug.LogError($"[Check] Song {song.name} has lyrics after its {song.clip.length:0.0} s of audio.");
                    ok = false;
                }
                if (song.notes == null)
                {
                    // Without a melody file MusicBus makes one note per sung word; those are checked like a melody.
                    notes = NoteTrack.FromLyrics(lyrics, song.lyricsOffset);
                    if (notes.IsEmpty || !notes.IsFromLyrics)
                    {
                        Debug.LogError($"[Check] Song {song.name} has no melody file and no notes could be made from its lyrics.");
                        ok = false;
                        continue;
                    }
                }
                var lastNote = notes.Notes[notes.Notes.Count - 1];
                if (words != notes.Notes.Count)
                {
                    Debug.LogError($"[Check] Song {song.name} sings {words} words but has {notes.Notes.Count} notes.");
                    ok = false;
                }
                if (lastNote.Time + lastNote.Length > song.clip.length)
                {
                    Debug.LogError($"[Check] Song {song.name} has notes after its {song.clip.length:0.0} s of audio.");
                    ok = false;
                }
            }
            if (songs.Count < 7)
            {
                Debug.LogError($"[Check] Expected the seven built-in songs in Assets/_Project/Songs, found {songs.Count}.");
                ok = false;
            }
            if (ok) Debug.Log($"[Check] {songs.Count} songs have audio, lyrics and a note for every sung word (from a melody file or made from the lyrics).");
            return CheckAddedSong() && ok;
        }

        static bool CheckAddedSong()
        {
            var folder = Path.Combine(Path.GetTempPath(), "MusicInYourHandsSongs");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            File.Copy("Assets/_Project/Audio/PaperBoat.wav", Path.Combine(folder, AddedSongName + ".wav"));
            File.WriteAllText(Path.Combine(folder, AddedSongName + ".lrc"), "[00:05.00]fold a paper boat for me\n[00:10.00]set it on the river slow\n");
            File.WriteAllText(Path.Combine(folder, "shopping list.txt"), "not a song");

            var found = AddedSongs.Scan(new List<SongAsset>(), folder);
            var song = found.Count == 1 ? found[0] : null;
            if (song == null || song.title != "Paper Boat" || song.artist != "Demo Band" || song.lyricsPath == null)
            {
                Debug.LogError($"[Check] Added songs: expected one song, Paper Boat by Demo Band with lyrics; found {found.Count}" +
                               (song != null ? $" ({song.title} by {song.artist}, lyrics {song.lyricsPath ?? "none"})" : "") + ".");
                return false;
            }

            // Load it the way the headset does, stepping the coroutine by hand.
            bool loaded = false;
            RunRoutine(AddedSongs.Load(song, success => loaded = success), 10);

            var lyrics = LyricsTrack.Parse(song.lyrics != null ? song.lyrics.text : null);
            bool ok = loaded && song.clip != null && song.clip.length > 50f && lyrics.Lines.Count == 2 && lyrics.Lines[1].Words.Length == 6;
            if (ok) Debug.Log($"[Check] Added song loaded: {song.title} by {song.artist}, {song.clip.length:0.0} s, {lyrics.Lines.Count} lyric lines.");
            else Debug.LogError($"[Check] Added song didn't load: audio {(song.clip != null ? $"{song.clip.length:0.0} s" : "missing")}, {lyrics.Lines.Count} lyric lines.");

            AddedSongs.Unload(song);
            Object.DestroyImmediate(song);
            Directory.Delete(folder, true);
            return ok;
        }

        /// <summary>Runs a coroutine to its end by hand, waiting on its web requests for at most <paramref name="seconds"/> in all.</summary>
        static void RunRoutine(IEnumerator routine, double seconds)
        {
            var timer = Stopwatch.StartNew();
            while (routine.MoveNext())
                if (routine.Current is AsyncOperation operation)
                    while (!operation.isDone && timer.Elapsed.TotalSeconds < seconds) System.Threading.Thread.Sleep(5);
        }
    }
}
