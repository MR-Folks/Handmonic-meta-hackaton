using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace MusicInYourHands
{
    /// <summary>
    /// Songs the person added themselves: audio files (.mp3, .ogg, .wav) in the Songs folder on the headset, each with
    /// an optional .lrc lyrics file of the same name. The app only plays them and shows their lyrics; nothing is
    /// uploaded or shared. On Quest the folder is Android/data/com.musicinyourhands.app/files/Songs.
    /// </summary>
    public static class AddedSongs
    {
        const string HowToName = "How to add songs.txt";
        const string HowTo =
            "Put songs in this folder to play them in Handmonic.\n\n" +
            "- The song: an .mp3, .ogg or .wav file.\n" +
            "- Its lyrics (optional): an .lrc file with the same name, like \"Artist - Title.mp3\" and \"Artist - Title.lrc\".\n" +
            "  The lines show at the times in the .lrc, and the words too when it has a time for each word.\n" +
            "- Name the files \"Artist - Title\" to show the artist, or put [ti:] and [ar:] tags in the .lrc.\n\n" +
            "Then open the menu and step through the songs with the arrow bubbles.\n";

        static readonly Regex TitleTag = new Regex(@"^\s*\[ti:\s*(.*?)\s*\]", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        static readonly Regex ArtistTag = new Regex(@"^\s*\[ar:\s*(.*?)\s*\]", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public static string Folder => Path.Combine(Application.persistentDataPath, "Songs");

        /// <summary>
        /// Lists the added songs by file name, reusing the entries in <paramref name="previous"/> for files it already
        /// knows so a playing song keeps its audio. Creates the folder, with a short how-to, if it's missing.
        /// </summary>
        public static List<SongAsset> Scan(IReadOnlyList<SongAsset> previous) => Scan(previous, Folder);

        /// <summary>The same, in another folder (the editor checks use a temporary one).</summary>
        public static List<SongAsset> Scan(IReadOnlyList<SongAsset> previous, string folder)
        {
            var found = new List<SongAsset>();
            try
            {
                Directory.CreateDirectory(folder);
                var howTo = Path.Combine(folder, HowToName);
                if (!File.Exists(howTo)) File.WriteAllText(howTo, HowTo);

                var files = Directory.GetFiles(folder);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (var path in files)
                {
                    if (TypeOf(path) == AudioType.UNKNOWN) continue;
                    SongAsset song = null;
                    foreach (var known in previous) if (known != null && known.audioPath == path) song = known;
                    if (song == null)
                    {
                        song = ScriptableObject.CreateInstance<SongAsset>();
                        song.name = Path.GetFileNameWithoutExtension(path);
                        song.audioPath = path;
                    }
                    song.lyricsPath = LyricsFor(path);
                    Name(song);
                    found.Add(song);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AddedSongs] Couldn't read {folder}: {e.Message}");
            }
            return found;
        }

        /// <summary>Loads the song's audio and reads its lyrics, then calls <paramref name="done"/> (false if the audio couldn't be read).</summary>
        public static IEnumerator Load(SongAsset song, Action<bool> done)
        {
            var url = new UriBuilder { Scheme = Uri.UriSchemeFile, Host = "", Path = song.audioPath.Replace('\\', '/') }.Uri.AbsoluteUri;
            using var request = UnityWebRequestMultimedia.GetAudioClip(url, TypeOf(song.audioPath));
            // Keep MP3 and OGG audio compressed and decode it as it plays: decoding a whole song as it loads stalls
            // the headset for most of a second.
            ((DownloadHandlerAudioClip)request.downloadHandler).compressed = true;
            yield return request.SendWebRequest();
            var clip = request.result == UnityWebRequest.Result.Success ? DownloadHandlerAudioClip.GetContent(request) : null;
            if (song == null)
            {
                // The file went away while it loaded.
                Free(clip);
                yield break;
            }
            if (clip == null)
            {
                Debug.LogWarning($"[AddedSongs] Couldn't play {song.audioPath}: {request.error}");
                done(false);
                yield break;
            }
            // Stepping away and back while it loads starts a second load; keep whichever audio arrived first.
            if (song.clip != null) Free(clip);
            else
            {
                clip.name = song.name;
                song.clip = clip;
            }

            Free(song.lyrics);
            song.lyrics = null;
            try
            {
                if (song.lyricsPath != null) song.lyrics = new TextAsset(File.ReadAllText(song.lyricsPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AddedSongs] Couldn't read the lyrics of {song.name}: {e.Message}");
            }
            done(true);
        }

        /// <summary>Frees an added song's audio and lyrics; they are loaded again the next time it plays.</summary>
        public static void Unload(SongAsset song)
        {
            if (song == null || !song.IsAdded) return;
            Free(song.clip);
            Free(song.lyrics);
            song.clip = null;
            song.lyrics = null;
        }

        static void Free(UnityEngine.Object loaded)
        {
            if (loaded == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(loaded); else UnityEngine.Object.DestroyImmediate(loaded);
        }

        static AudioType TypeOf(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".mp3": return AudioType.MPEG;
                case ".ogg": return AudioType.OGGVORBIS;
                case ".wav": return AudioType.WAV;
                default: return AudioType.UNKNOWN;
            }
        }

        static string LyricsFor(string audioPath)
        {
            foreach (var extension in new[] { ".lrc", ".LRC" })
            {
                var path = Path.ChangeExtension(audioPath, extension);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        /// <summary>The title and artist from the .lrc's [ti:] and [ar:] tags, else from an "Artist - Title" file name.</summary>
        static void Name(SongAsset song)
        {
            var file = Path.GetFileNameWithoutExtension(song.audioPath);
            int dash = file.IndexOf(" - ", StringComparison.Ordinal);
            song.title = dash > 0 ? file.Substring(dash + 3).Trim() : file;
            song.artist = dash > 0 ? file.Substring(0, dash).Trim() : "";
            if (song.lyricsPath == null) return;
            try
            {
                var lrc = File.ReadAllText(song.lyricsPath);
                var title = TitleTag.Match(lrc);
                var artist = ArtistTag.Match(lrc);
                if (title.Success && title.Groups[1].Value.Length > 0) song.title = title.Groups[1].Value;
                if (artist.Success && artist.Groups[1].Value.Length > 0) song.artist = artist.Groups[1].Value;
            }
            catch (IOException) { }
        }
    }
}
