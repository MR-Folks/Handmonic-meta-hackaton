using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        const string AddedSongName = "Demo Band - Paper Boat";

        // A reply from Audius in its own order: a track for sale, a label's track, an artist's own track, a deleted one.
        const string SampleAudiusReply = "{\"data\":[" +
            "{\"id\":\"b2\",\"title\":\"For Sale\",\"permalink\":\"/x/for-sale\",\"is_streamable\":true,\"is_available\":true,\"is_delete\":false,\"is_stream_gated\":true,\"access\":{\"stream\":false},\"user\":{\"name\":\"X\",\"handle\":\"x\"}}," +
            "{\"id\":\"a1\",\"title\":\"Tide Lines - Harbour Lights\",\"permalink\":\"/demolabel/harbour-lights\",\"is_streamable\":true,\"is_available\":true,\"is_delete\":false,\"is_stream_gated\":false,\"duration\":200,\"bpm\":null,\"access\":{\"stream\":true,\"download\":false},\"user\":{\"name\":\"Demo Label\",\"handle\":\"demolabel\",\"is_verified\":true}}," +
            "{\"id\":\"c3\",\"title\":\"Night Bus\",\"permalink\":\"/someone/night-bus\",\"is_streamable\":true,\"is_available\":true,\"is_delete\":false,\"is_stream_gated\":false,\"access\":{\"stream\":true},\"user\":{\"name\":\"Someone\",\"handle\":\"someone\"}}," +
            "{\"id\":\"d4\",\"title\":\"Gone\",\"permalink\":\"/x/gone\",\"is_streamable\":true,\"is_available\":false,\"is_delete\":true,\"is_stream_gated\":false,\"access\":{\"stream\":false},\"user\":{\"name\":\"X\",\"handle\":\"x\"}}]}";

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
                if (song.clip == null || lyrics.Lines.Count == 0 || notes.IsEmpty)
                {
                    Debug.LogError($"[Check] Song {song.name} is missing its audio, lyrics or notes.");
                    ok = false;
                    continue;
                }
                int words = lyrics.Lines.Sum(line => line.Words.Length);
                var lastLine = lyrics.Lines[lyrics.Lines.Count - 1];
                var lastNote = notes.Notes[notes.Notes.Count - 1];
                if (words != notes.Notes.Count)
                {
                    Debug.LogError($"[Check] Song {song.name} sings {words} words but has {notes.Notes.Count} notes.");
                    ok = false;
                }
                if (lastNote.Time + lastNote.Length > song.clip.length || lastLine.Words[lastLine.Words.Length - 1].Time > song.clip.length)
                {
                    Debug.LogError($"[Check] Song {song.name} has lyrics or notes after its {song.clip.length:0.0} s of audio.");
                    ok = false;
                }
            }
            if (songs.Count < 4)
            {
                Debug.LogError($"[Check] Expected the four demo songs in Assets/_Project/Songs, found {songs.Count}.");
                ok = false;
            }
            if (ok) Debug.Log($"[Check] {songs.Count} songs have audio, lyrics and a note for every sung word.");
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

        /// <summary>
        /// Songs from Audius: the picked list reads, a reply from Audius becomes songs in the list's order with their
        /// credits (leaving out tracks that are for sale or gone), and, when this computer is online, Audius sends the
        /// picked songs and one streams in and decodes.
        /// </summary>
        static bool CheckAudiusSongs()
        {
            var order = AudiusSongs.ReadPicks("# a comment\nc3  https://audius.co/someone/night-bus\n\n  a1\thttps://audius.co/demolabel/harbour-lights\r\nb2\nd4\nc3\nmissing\n");
            var found = AudiusSongs.Parse(SampleAudiusReply, order);
            var own = found.Count == 2 ? found[0] : null;
            var label = found.Count == 2 ? found[1] : null;
            bool ok = string.Join(",", order) == "c3,a1,b2,d4,missing" && own != null && label != null &&
                      own.title == "Night Bus" && own.artist == "Someone" && AudiusSongs.Byline(own) == "© Someone" &&
                      label.title == "Harbour Lights" && label.artist == "Tide Lines" && label.owner == "Demo Label" &&
                      AudiusSongs.Byline(label) == "Tide Lines  ·  © Demo Label" &&
                      AudiusSongs.Credit(label) == "Harbour Lights by Tide Lines  ·  © Demo Label  ·  Audius Open Music License" &&
                      AudiusSongs.ShortLink(label) == "audius.co/demolabel/harbour-lights" &&
                      label.IsFromAudius && !label.IsAdded && label.LoadsOnDemand &&
                      AudiusSongs.Parse("{}", order).Count == 0 && AudiusSongs.Parse("not json", order).Count == 0;
            if (ok) Debug.Log("[Check] A reply from Audius becomes songs in the picked order, with their credits.");
            else Debug.LogError($"[Check] Audius reply read wrongly: order {string.Join(",", order)}; songs " +
                                string.Join("; ", found.Select(s => $"{s.title} by {s.artist} (© {s.owner}, {s.link})")) + ".");
            foreach (var song in found) Object.DestroyImmediate(song);

            var picks = AudiusSongs.Picks;
            bool listed = picks.Count > 0 && picks.All(id => Regex.IsMatch(id, "^[A-Za-z0-9]+$"));
            if (listed) Debug.Log($"[Check] Resources/AudiusSongs.txt picks {picks.Count} songs.");
            else Debug.LogError("[Check] Resources/AudiusSongs.txt is missing, empty, or has a line that doesn't start with a track id.");
            return ok && listed && CheckAudiusStream();
        }

        /// <summary>When this computer is online, Audius sends the picked songs and one streams in, without counting a play.</summary>
        static bool CheckAudiusStream()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogWarning("[Check] Offline, so skipped asking Audius for the picked songs.");
                return true;
            }
            List<SongAsset> found = null;
            RunRoutine(AudiusSongs.Fetch(songs => found = songs), 30);
            if (found == null || found.Count == 0)
            {
                Debug.LogError("[Check] Audius didn't send any of the picked songs; see the [Audius] warning above.");
                return false;
            }
            if (found.Count < AudiusSongs.Picks.Count)
                Debug.LogWarning($"[Check] Only {found.Count} of the {AudiusSongs.Picks.Count} picked songs can play on Audius now; replace the others in Resources/AudiusSongs.txt.");
            foreach (var each in found)
                if (!TypeFace.Bold.Covers(each.title + each.artist + each.owner))
                    Debug.LogWarning($"[Check] The lettering lacks some letters of {each.title} by {each.artist}; they will show as gaps.");

            var song = found[found.Count - 1];
            bool loaded = false;
            RunRoutine(AudiusSongs.Load(song, success => loaded = success, countPlay: false), 120);
            bool ok = loaded && song.clip != null && song.clip.length > 60f;
            if (ok) Debug.Log($"[Check] Audius sent {found.Count} picked songs, and {song.title} by {song.artist} streamed in: {song.clip.length:0.0} s at {song.clip.frequency} Hz.");
            else Debug.LogError($"[Check] {song.title} by {song.artist} didn't stream in from Audius.");
            AudiusSongs.Unload(song);
            foreach (var each in found) Object.DestroyImmediate(each);
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
