using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MusicInYourHands
{
    /// <summary>
    /// Songs streamed from Audius (audius.co), a free music service whose artists let music players stream their
    /// tracks under the Audius Open Music License. The app offers a hand-picked list (Resources/AudiusSongs.txt): the
    /// first time the menu opens it asks Audius for those tracks in one request, and a song's audio streams in only
    /// while it plays. The license asks for a credit: who published the track, a copyright notice, a mention of the
    /// license and a link to the track. No account or key is needed, and nothing about the person is sent.
    /// </summary>
    public static class AudiusSongs
    {
        /// <summary>Where the songs from Audius stand: not asked for yet, on their way, here, or out of reach.</summary>
        public enum Status { NotAsked, Asking, Ready, Unreachable }

        public const string License = "Audius Open Music License";

        const string Api = "https://api.audius.co/v1";
        // Audius asks apps to give their name with each request.
        const string AppName = "MusicInYourHands";
        const string ListName = "AudiusSongs";

        // Field names match Audius's replies.
        [Serializable] sealed class Reply { public Track[] data; }

        [Serializable]
        sealed class Track
        {
            public string id, title, permalink;
            public bool is_streamable, is_available, is_delete, is_stream_gated;
            public Access access;
            public User user;
        }

        [Serializable] sealed class Access { public bool stream; }
        [Serializable] sealed class User { public string name, handle; }

        static List<string> picks;

        /// <summary>The ids of the hand-picked tracks, in the order the menu offers them.</summary>
        public static IReadOnlyList<string> Picks
        {
            get
            {
                if (picks != null) return picks;
                var list = Resources.Load<TextAsset>(ListName);
                picks = ReadPicks(list != null ? list.text : null);
                return picks;
            }
        }

        /// <summary>Reads the list: a track's id at the start of each line, skipping blank lines, # comments and repeats.</summary>
        public static List<string> ReadPicks(string text)
        {
            var ids = new List<string>();
            if (string.IsNullOrEmpty(text)) return ids;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int gap = line.IndexOfAny(new[] { ' ', '\t' });
                var id = gap < 0 ? line : line.Substring(0, gap);
                if (!ids.Contains(id)) ids.Add(id);
            }
            return ids;
        }

        /// <summary>
        /// Asks Audius for the picked tracks, then calls <paramref name="done"/> with the songs that can play, or with
        /// null when Audius couldn't be reached.
        /// </summary>
        public static IEnumerator Fetch(Action<List<SongAsset>> done)
        {
            var ids = Picks;
            if (ids.Count == 0)
            {
                done(new List<SongAsset>());
                yield break;
            }
            var url = new StringBuilder(Api).Append("/tracks?app_name=").Append(AppName);
            foreach (var id in ids) url.Append("&id=").Append(Uri.EscapeDataString(id));
            using var request = UnityWebRequest.Get(url.ToString());
            request.timeout = 20;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Audius] Couldn't get the song list: {request.error}");
                done(null);
                yield break;
            }
            var songs = Parse(request.downloadHandler.text, ids);
            Debug.Log($"[Audius] {songs.Count} of the {ids.Count} picked songs can play.");
            done(songs);
        }

        /// <summary>
        /// Turns Audius's reply into songs in the order of <paramref name="order"/>, leaving out tracks that are gone
        /// or that can't be streamed for free (artists can sell or lock their tracks).
        /// </summary>
        public static List<SongAsset> Parse(string json, IReadOnlyList<string> order)
        {
            var songs = new List<SongAsset>();
            Reply reply = null;
            try
            {
                reply = JsonUtility.FromJson<Reply>(json);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"[Audius] Couldn't read the song list: {e.Message}");
            }
            if (reply == null || reply.data == null) return songs;
            foreach (var id in order)
                foreach (var track in reply.data)
                    if (track != null && track.id == id)
                    {
                        if (CanStream(track)) songs.Add(Song(track));
                        break;
                    }
            return songs;
        }

        static bool CanStream(Track track) =>
            track.is_streamable && track.is_available && !track.is_delete && !track.is_stream_gated &&
            track.access != null && track.access.stream && !string.IsNullOrWhiteSpace(track.title);

        static SongAsset Song(Track track)
        {
            var song = ScriptableObject.CreateInstance<SongAsset>();
            string owner = track.user == null ? "" : !string.IsNullOrWhiteSpace(track.user.name) ? track.user.name.Trim() : track.user.handle ?? "";
            if (owner.Length == 0) owner = "Audius artist";
            // Labels publish their artists' tracks as "Artist - Title", like the file names of added songs.
            var title = track.title.Trim();
            int dash = title.IndexOf(" - ", StringComparison.Ordinal);
            bool split = dash > 0 && dash + 3 < title.Length;
            song.title = split ? title.Substring(dash + 3).Trim() : title;
            song.artist = split ? title.Substring(0, dash).Trim() : owner;
            song.name = song.title;
            song.audiusId = track.id;
            song.owner = owner;
            song.link = "https://audius.co" + track.permalink;
            return song;
        }

        /// <summary>
        /// Streams the song's audio in (a whole MP3), then calls <paramref name="done"/> (false if it couldn't). Each
        /// play counts towards the track's plays on Audius, unless <paramref name="countPlay"/> is false (the editor
        /// checks).
        /// </summary>
        public static IEnumerator Load(SongAsset song, Action<bool> done, bool countPlay = true)
        {
            var url = $"{Api}/tracks/{Uri.EscapeDataString(song.audiusId)}/stream?app_name={AppName}" + (countPlay ? "" : "&skip_play_count=true");
            using var request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
            // A song is 5 to 15 MB, so allow for slow Wi-Fi.
            request.timeout = 120;
            yield return request.SendWebRequest();
            var clip = request.result == UnityWebRequest.Result.Success ? DownloadHandlerAudioClip.GetContent(request) : null;
            if (song == null)
            {
                Free(clip);
                yield break;
            }
            if (clip == null || clip.length <= 0f)
            {
                Debug.LogWarning($"[Audius] Couldn't stream {song.title}: {request.error ?? "it isn't audio"}");
                Free(clip);
                done(false);
                yield break;
            }
            // Stepping away and back while it streams starts a second load; keep whichever audio arrived first.
            if (song.clip != null) Free(clip);
            else
            {
                clip.name = song.title;
                song.clip = clip;
            }
            done(true);
        }

        /// <summary>Frees a song's streamed audio; it streams in again the next time it plays.</summary>
        public static void Unload(SongAsset song)
        {
            if (song == null || !song.IsFromAudius) return;
            Free(song.clip);
            song.clip = null;
        }

        /// <summary>The credit in one line: the song, its artist, the copyright owner and the license.</summary>
        public static string Credit(SongAsset song) => $"{song.title} by {song.artist}  ·  © {song.owner}  ·  {License}";

        /// <summary>Who made the song and who owns it, for a list: "© Owner", or "Artist  ·  © Label".</summary>
        public static string Byline(SongAsset song) => song.artist == song.owner ? $"© {song.owner}" : $"{song.artist}  ·  © {song.owner}";

        /// <summary>The song's page without "https://", for showing.</summary>
        public static string ShortLink(SongAsset song) =>
            song.link != null && song.link.StartsWith("https://", StringComparison.Ordinal) ? song.link.Substring(8) : song.link ?? "";

        static void Free(UnityEngine.Object loaded)
        {
            if (loaded == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(loaded); else UnityEngine.Object.DestroyImmediate(loaded);
        }
    }
}
