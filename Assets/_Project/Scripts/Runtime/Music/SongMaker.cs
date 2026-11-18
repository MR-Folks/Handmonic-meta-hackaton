using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace MusicInYourHands
{
    /// <summary>
    /// Makes a new song with the person's own ElevenLabs account: they pick a theme and a genre (an emoji each), and
    /// ElevenLabs writes it, composes it and times its lyrics. Three calls: a composition plan (the sections and the
    /// lyrics, free), the song from that plan, and forced alignment of the lyrics against the audio for word-timed
    /// lyrics. The song and its .lrc land in the Songs folder, where it plays like any song the person added.
    ///
    /// The API key is the person's own. It is kept in the app's private settings on the headset (never in the shared
    /// Songs folder) and only ever sent to api.elevenlabs.io. It needs the Music permission, and Speech to Text for
    /// the word timing (without it the lines are spread over each section instead).
    /// </summary>
    public static class SongMaker
    {
        /// <summary>A theme or a genre: its emoji, its name on the page, and how the prompt puts it.</summary>
        public readonly struct Choice
        {
            public readonly string Emoji, Name, Words;
            public readonly Color Color;

            public Choice(string emoji, string name, string words, Color color)
            {
                Emoji = emoji;
                Name = name;
                Words = words;
                Color = color;
            }
        }

        public static readonly Choice[] Themes =
        {
            new Choice("❤️", "Love", "love and feeling close to someone", new Color(1f, 0.4f, 0.55f)),
            new Choice("☀️", "Happy", "a bright, happy, sunny day", new Color(1f, 0.8f, 0.3f)),
            new Choice("\U0001F319", "Dreamy", "dreams, the night sky and drifting off to sleep", new Color(0.62f, 0.55f, 1f)),
            new Choice("\U0001F525", "Energy", "energy, fire and dancing all night", new Color(1f, 0.52f, 0.25f)),
            new Choice("\U0001F30A", "Calm", "calm water, gentle waves and slow breathing", new Color(0.35f, 0.75f, 1f)),
        };

        public static readonly Choice[] Genres =
        {
            new Choice("\U0001F3A4", "Pop", "catchy modern pop with a clear lead vocal", new Color(1f, 0.45f, 0.8f)),
            new Choice("\U0001F3B8", "Rock", "upbeat rock with electric guitars, bass and live drums", new Color(1f, 0.4f, 0.35f)),
            new Choice("\U0001F3A7", "Lo-fi", "mellow lo-fi hip-hop with dusty drums, warm keys and a relaxed sung vocal", new Color(0.5f, 0.8f, 0.7f)),
            new Choice("\U0001F483", "Dance", "energetic dance-pop with a four-on-the-floor beat and a big chorus", new Color(0.75f, 0.45f, 1f)),
            new Choice("\U0001FA95", "Folk", "warm acoustic folk with fingerpicked guitar and vocal harmonies", new Color(0.85f, 0.65f, 0.4f)),
        };

        public enum Status { Idle, Writing, Composing, Timing, Saving, Done, Failed }

        const string Api = "https://api.elevenlabs.io/v1";
        const string Model = "music_v2_5";
        const string KeyPref = "elevenlabs.api-key";
        public const string KeyFileName = "ElevenLabs key.txt";
        public const string Artist = "Made with ElevenLabs";
        /// <summary>How long a made song is.</summary>
        public const int SongSeconds = 120;
        /// <summary>
        /// About what a song costs: ElevenLabs lists music at 900 credits a minute, and timing the lyrics adds a few
        /// dozen.
        /// </summary>
        public const int ApproxCredits = SongSeconds * 15 + 50;

        static readonly Regex Header = new Regex(@"^\s*\[[^\]]*\]\s*$");
        static readonly Regex Direction = new Regex(@"\{[^}]*\}");
        static readonly Regex StatusField = new Regex("\"status\"\\s*:\\s*\"([^\"]*)\"");
        static readonly Regex MessageField = new Regex("\"message\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        static readonly Regex DetailText = new Regex("\"detail\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");

        // --- The plan and the alignment, as ElevenLabs sends them --------------------------------------------------

        [Serializable]
        public sealed class Plan
        {
            public Chunk[] chunks;
        }

        [Serializable]
        public sealed class Chunk
        {
            public string text;
            public int duration_ms;
            public string[] positive_styles;
            public string[] negative_styles;
        }

        [Serializable]
        sealed class ComposeRequest
        {
            public string model_id;
            public Plan composition_plan;
        }

        [Serializable]
        sealed class PlanRequest
        {
            public string prompt;
            public int music_length_ms;
            public string model_id;
        }

        [Serializable]
        public sealed class Alignment
        {
            public Word[] words;
            public float loss;
        }

        [Serializable]
        public sealed class Word
        {
            public string text;
            public float start;
            public float end;
        }

        /// <summary>One sung line, with the section it is in and its place there, for timing without an alignment.</summary>
        public readonly struct Line
        {
            public readonly string Text;
            public readonly int Chunk, Index, Count;

            public Line(string text, int chunk, int index, int count)
            {
                Text = text;
                Chunk = chunk;
                Index = index;
                Count = count;
            }
        }

        // --- The key ------------------------------------------------------------------------------------------------

        public static bool HasKey => !string.IsNullOrEmpty(Key);

        static string Key => PlayerPrefs.GetString(KeyPref, "");

        /// <summary>Keeps the key. False if it's empty.</summary>
        public static bool SetKey(string key)
        {
            key = Clean(key);
            if (string.IsNullOrEmpty(key)) return false;
            PlayerPrefs.SetString(KeyPref, key);
            PlayerPrefs.Save();
            return true;
        }

        public static void ForgetKey()
        {
            PlayerPrefs.DeleteKey(KeyPref);
            PlayerPrefs.Save();
        }

        /// <summary>A key file the person copied into the app's folder, for those who'd rather not type it.</summary>
        public static string KeyFile => Path.Combine(Application.persistentDataPath, KeyFileName);

        /// <summary>
        /// Takes the key from a key file if there is one, then deletes the file, as that folder can be read over USB.
        /// </summary>
        public static bool ImportKeyFile(string path = null)
        {
            path ??= KeyFile;
            try
            {
                if (!File.Exists(path)) return false;
                string key = null;
                foreach (var line in File.ReadAllLines(path))
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        key = line;
                        break;
                    }
                File.Delete(path);
                return SetKey(key);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[SongMaker] Couldn't read {KeyFileName}: {e.Message}");
                return false;
            }
        }

        static string Clean(string key)
        {
            if (key == null) return "";
            key = key.Trim().Trim('"', '\'');
            int equals = key.IndexOf('=');
            // "ELEVENLABS_API_KEY=sk_..." pasted from a .env file.
            if (equals >= 0 && key.IndexOf("sk_", StringComparison.Ordinal) > equals) key = key.Substring(equals + 1).Trim().Trim('"', '\'');
            return key;
        }

        // --- The job ------------------------------------------------------------------------------------------------

        public static Status State { get; private set; }
        /// <summary>Why the last song couldn't be made, said for the page.</summary>
        public static string Error { get; private set; }
        /// <summary>True when the last failure was about the key, so the page offers to change it.</summary>
        public static bool KeyProblem { get; private set; }
        public static int Theme { get; private set; }
        public static int Genre { get; private set; }
        public static bool Busy => State == Status.Writing || State == Status.Composing || State == Status.Timing || State == Status.Saving;

        static string madePath, madeTitle;

        /// <summary>Starts making a song. False if one is already being made, or there's no key.</summary>
        public static bool Start(MonoBehaviour host, int theme, int genre)
        {
            if (Busy || host == null || !HasKey) return false;
            Theme = Mathf.Clamp(theme, 0, Themes.Length - 1);
            Genre = Mathf.Clamp(genre, 0, Genres.Length - 1);
            Error = null;
            KeyProblem = false;
            madePath = madeTitle = null;
            State = Status.Writing;
            host.StartCoroutine(Run(Key, Theme, Genre));
            return true;
        }

        /// <summary>Hands over the finished song, once.</summary>
        public static bool TryTakeMade(out string audioPath, out string title)
        {
            audioPath = madePath;
            title = madeTitle;
            if (State != Status.Done || madePath == null) return false;
            madePath = madeTitle = null;
            State = Status.Idle;
            return true;
        }

        /// <summary>Clears a failure once the page has shown it.</summary>
        public static void Dismiss()
        {
            if (State == Status.Failed || State == Status.Done) State = Status.Idle;
        }

        static IEnumerator Run(string key, int theme, int genre)
        {
            Debug.Log($"[SongMaker] Making a {Genres[genre].Name} song about {Themes[theme].Name}.");
            Plan plan;
            using (var request = PostJson($"{Api}/music/plan", PlanJson(theme, genre), key, 120))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Fail(request);
                    yield break;
                }
                plan = ParsePlan(request.downloadHandler.text);
            }
            if (plan == null || plan.chunks == null || plan.chunks.Length == 0)
            {
                Fail("ElevenLabs didn't send a song plan. Please try again.");
                yield break;
            }

            State = Status.Composing;
            byte[] audio;
            using (var request = PostJson($"{Api}/music?output_format=mp3_44100_128", ComposeJson(plan), key, 600))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Fail(request);
                    yield break;
                }
                audio = request.downloadHandler.data;
            }
            if (audio == null || audio.Length < 1000)
            {
                Fail("ElevenLabs sent back an empty song. Please try again.");
                yield break;
            }

            State = Status.Timing;
            var lines = LyricLines(plan);
            Alignment alignment = null;
            if (lines.Count > 0)
            {
                var form = new List<IMultipartFormSection>
                {
                    new MultipartFormFileSection("file", audio, "song.mp3", "audio/mpeg"),
                    new MultipartFormDataSection("text", Sung(lines))
                };
                using var request = UnityWebRequest.Post($"{Api}/forced-alignment", form);
                request.SetRequestHeader("xi-api-key", key);
                request.timeout = 300;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) alignment = ParseAlignment(request.downloadHandler.text);
                else Debug.LogWarning($"[SongMaker] Couldn't time the lyrics ({request.responseCode}), so they follow the sections instead: {Explain(request.responseCode, request.downloadHandler?.text, request.error, out _)}");
            }

            State = Status.Saving;
            string title = Title(plan, theme, genre);
            try
            {
                Directory.CreateDirectory(AddedSongs.Folder);
                var path = FreePath(AddedSongs.Folder, FileName(title));
                File.WriteAllBytes(path, audio);
                File.WriteAllText(Path.ChangeExtension(path, ".lrc"), Lrc(title, plan, lines, alignment));
                madePath = path;
                madeTitle = title;
                State = Status.Done;
                Debug.Log($"[SongMaker] Made {title}: {audio.Length / 1024} KB, {lines.Count} lines, {(alignment != null ? "word-timed" : "timed by section")}.");
            }
            catch (Exception e)
            {
                Fail($"Couldn't save the song on the headset: {e.Message}");
            }
        }

        static UnityWebRequest PostJson(string url, string json, string key, int timeout)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeout
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", key);
            return request;
        }

        static void Fail(UnityWebRequest request)
        {
            Fail(Explain(request.responseCode, request.downloadHandler?.text, request.error, out bool keyProblem));
            KeyProblem = keyProblem;
            Debug.LogWarning($"[SongMaker] {request.url.Split('?')[0]} failed ({request.responseCode}): {request.downloadHandler?.text}");
        }

        static void Fail(string message)
        {
            Error = message;
            KeyProblem = false;
            State = Status.Failed;
        }

        // --- The parts, kept apart from the network so the editor checks can try them ------------------------------

        /// <summary>What ElevenLabs is asked for: a song in the genre about the theme, with lyrics.</summary>
        public static string Prompt(int theme, int genre) =>
            $"A {Genres[genre].Words} song about {Themes[theme].Words}. Original sung English lyrics with verses and a " +
            "catchy chorus that repeats and is easy to sing along to. Warm, family-friendly words. About two minutes long.";

        public static string PlanJson(int theme, int genre) =>
            JsonUtility.ToJson(new PlanRequest { prompt = Prompt(theme, genre), music_length_ms = SongSeconds * 1000, model_id = Model });

        public static Plan ParsePlan(string json)
        {
            try
            {
                return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Plan>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public static Alignment ParseAlignment(string json)
        {
            try
            {
                var a = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Alignment>(json);
                return a != null && a.words != null && a.words.Length > 0 ? a : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>The plan sent back to be composed, with only the fields the composer needs.</summary>
        public static string ComposeJson(Plan plan)
        {
            var clean = new Plan { chunks = new Chunk[plan.chunks.Length] };
            for (int i = 0; i < plan.chunks.Length; i++)
            {
                var c = plan.chunks[i];
                clean.chunks[i] = new Chunk
                {
                    text = c.text ?? "",
                    duration_ms = Mathf.Clamp(c.duration_ms, 3000, 120000),
                    positive_styles = c.positive_styles ?? new string[0],
                    negative_styles = c.negative_styles ?? new string[0]
                };
            }
            return JsonUtility.ToJson(new ComposeRequest { model_id = Model, composition_plan = clean });
        }

        /// <summary>The sung lines in order: section names ([Chorus]) and directions ({whispered}) left out.</summary>
        public static List<Line> LyricLines(Plan plan)
        {
            var lines = new List<Line>();
            if (plan?.chunks == null) return lines;
            for (int c = 0; c < plan.chunks.Length; c++)
            {
                var sung = new List<string>();
                foreach (var raw in (plan.chunks[c].text ?? "").Split('\n'))
                {
                    if (Header.IsMatch(raw)) continue;
                    var text = Regex.Replace(Direction.Replace(raw, ""), @"\s+", " ").Trim();
                    if (text.Length > 0 && Regex.IsMatch(text, @"[\p{L}\p{N}]")) sung.Add(text);
                }
                for (int i = 0; i < sung.Count; i++) lines.Add(new Line(sung[i], c, i, sung.Count));
            }
            return lines;
        }

        static string Sung(List<Line> lines)
        {
            var sb = new StringBuilder();
            foreach (var line in lines) sb.Append(line.Text).Append('\n');
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// A title from the song itself: the chorus's first line, cut to a few words. Falls back to the theme and genre.
        /// </summary>
        public static string Title(Plan plan, int theme, int genre)
        {
            string pick = null;
            if (plan?.chunks != null)
            {
                var lines = LyricLines(plan);
                for (int c = 0; c < plan.chunks.Length && pick == null; c++)
                {
                    var head = (plan.chunks[c].text ?? "").Split('\n')[0];
                    if (head.IndexOf("chorus", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    foreach (var line in lines)
                        if (line.Chunk == c)
                        {
                            pick = line.Text;
                            break;
                        }
                }
                if (pick == null && lines.Count > 0) pick = lines[0].Text;
            }
            if (pick != null)
            {
                // The first clause, at most five words, without its closing punctuation.
                var clause = Regex.Split(pick, @"[,;:.!?—–]")[0];
                var words = clause.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 5) Array.Resize(ref words, 5);
                pick = string.Join(" ", words).Trim(' ', '\'', '"', '-', '(', ')');
                if (pick.Length > 0) pick = char.ToUpperInvariant(pick[0]) + pick.Substring(1);
            }
            return string.IsNullOrWhiteSpace(pick) || pick.Length < 3 ? $"{Themes[theme].Name} {Genres[genre].Name}" : pick;
        }

        /// <summary>
        /// The lyrics as an .lrc. With an alignment every word has its own time, matched against what was heard;
        /// without one each line starts at its share of its section.
        /// </summary>
        public static string Lrc(string title, Plan plan, List<Line> lines, Alignment alignment)
        {
            var sb = new StringBuilder();
            sb.Append("[ti:").Append(title).Append("]\n[ar:").Append(Artist).Append("]\n");
            if (lines.Count == 0) return sb.ToString();

            var tokens = new List<(int line, string word)>();
            for (int i = 0; i < lines.Count; i++)
                foreach (var w in lines[i].Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) tokens.Add((i, w));
            var times = alignment != null ? WordTimes(tokens, alignment) : null;

            if (times == null)
            {
                var starts = new float[plan.chunks.Length];
                for (int c = 1; c < starts.Length; c++) starts[c] = starts[c - 1] + plan.chunks[c - 1].duration_ms / 1000f;
                foreach (var line in lines)
                {
                    float length = plan.chunks[line.Chunk].duration_ms / 1000f;
                    float at = starts[line.Chunk] + length * line.Index / Mathf.Max(1, line.Count);
                    sb.Append('[').Append(Stamp(at)).Append(']').Append(line.Text).Append('\n');
                }
                return sb.ToString();
            }

            for (int i = 0, k = 0; i < lines.Count; i++)
            {
                int first = k;
                while (k < tokens.Count && tokens[k].line == i) k++;
                if (k == first) continue;
                sb.Append('[').Append(Stamp(times[first])).Append(']');
                for (int t = first; t < k; t++)
                {
                    if (t > first) sb.Append(' ');
                    sb.Append('<').Append(Stamp(times[t])).Append('>').Append(tokens[t].word);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// A time for every lyric word: the longest run of words the alignment heard in order (by their letters), with
        /// the words it didn't match placed between their neighbours. Null if it matched nothing.
        /// </summary>
        static float[] WordTimes(List<(int line, string word)> tokens, Alignment alignment)
        {
            var heard = new List<Word>();
            foreach (var w in alignment.words)
                if (w != null && !string.IsNullOrWhiteSpace(w.text)) heard.Add(w);
            int n = tokens.Count, m = heard.Count;
            if (n == 0 || m == 0) return null;
            var a = new string[n];
            var b = new string[m];
            for (int i = 0; i < n; i++) a[i] = Norm(tokens[i].word);
            for (int j = 0; j < m; j++) b[j] = Norm(heard[j].text);

            var best = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = m - 1; j >= 0; j--)
                    best[i, j] = a[i].Length > 0 && a[i] == b[j] ? best[i + 1, j + 1] + 1 : Math.Max(best[i + 1, j], best[i, j + 1]);
            if (best[0, 0] == 0) return null;

            var times = new float[n];
            var known = new bool[n];
            for (int i = 0, j = 0; i < n && j < m;)
            {
                if (a[i].Length > 0 && a[i] == b[j])
                {
                    times[i] = heard[j].start;
                    known[i] = true;
                    i++;
                    j++;
                }
                else if (best[i + 1, j] >= best[i, j + 1]) i++;
                else j++;
            }
            for (int i = 0; i < n; i++)
            {
                if (known[i]) continue;
                int before = i - 1, after = i + 1;
                while (before >= 0 && !known[before]) before--;
                while (after < n && !known[after]) after++;
                if (before < 0) times[i] = times[after];
                else if (after >= n) times[i] = times[before] + 0.3f * (i - before);
                else times[i] = times[before] + (times[after] - times[before]) * (i - before) / (after - before);
            }
            for (int i = 1; i < n; i++) times[i] = Mathf.Max(times[i], times[i - 1]);
            return times;
        }

        static string Norm(string word)
        {
            var sb = new StringBuilder(word.Length);
            foreach (var ch in word.ToLowerInvariant())
                if (char.IsLetterOrDigit(ch) || ch == '\'') sb.Append(ch);
            return sb.ToString();
        }

        static string Stamp(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int minutes = (int)(seconds / 60f);
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds - minutes * 60f).ToString("00.00", CultureInfo.InvariantCulture);
        }

        /// <summary>A file name for the title, without characters a file name can't have.</summary>
        public static string FileName(string title)
        {
            var sb = new StringBuilder();
            foreach (var ch in title)
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), ch) < 0 && ch != '/' && ch != '\\' && ch != ':' && ch != '*' && ch != '?' && ch != '"' && ch != '<' && ch != '>' && ch != '|')
                    sb.Append(ch);
            var name = sb.ToString().Trim(' ', '.');
            return (name.Length > 0 ? name : "My song") + ".mp3";
        }

        static string FreePath(string folder, string file)
        {
            var path = Path.Combine(folder, file);
            var stem = Path.GetFileNameWithoutExtension(file);
            for (int n = 2; File.Exists(path) || File.Exists(Path.ChangeExtension(path, ".lrc")); n++)
                path = Path.Combine(folder, $"{stem} ({n}).mp3");
            return path;
        }

        /// <summary>
        /// Says why a request failed, for the page. <paramref name="keyProblem"/> is true when a different key would
        /// help (it was refused, or is missing a permission).
        /// </summary>
        public static string Explain(long code, string body, string error, out bool keyProblem)
        {
            keyProblem = false;
            body ??= "";
            string status = StatusField.Match(body) is { Success: true } s ? s.Groups[1].Value : "";
            string message = MessageField.Match(body) is { Success: true } msg ? Unescape(msg.Groups[1].Value)
                : DetailText.Match(body) is { Success: true } d ? Unescape(d.Groups[1].Value) : "";

            if (code == 0) return "Couldn't reach ElevenLabs. Is the headset online?";
            if (status == "missing_permissions")
            {
                keyProblem = true;
                if (message.Contains("music_generation")) return "Your key needs the Music permission. Turn it on in ElevenLabs, under Developers, API Keys.";
                if (message.Contains("speech_to_text") || message.Contains("forced_alignment")) return "Your key needs the Speech to Text permission for timed lyrics.";
                return "Your key is missing a permission this needs. Check it in ElevenLabs, under Developers, API Keys.";
            }
            if (status == "invalid_api_key" || status == "unauthorized" && message.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 || code == 401 && status != "quota_exceeded")
            {
                keyProblem = true;
                return "ElevenLabs didn't accept this key. Check it, or connect another one.";
            }
            if (status == "quota_exceeded" || message.IndexOf("credits", StringComparison.OrdinalIgnoreCase) >= 0 && message.IndexOf("enough", StringComparison.OrdinalIgnoreCase) >= 0)
                return "There aren't enough ElevenLabs credits left for a song.";
            if (code == 402 || message.IndexOf("paid", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("subscription", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Making songs needs a paid ElevenLabs plan.";
            if (code == 429) return "ElevenLabs is busy right now. Please try again in a minute.";
            if (code >= 500) return "ElevenLabs had a problem making the song. Please try again.";
            if (message.Length > 0) return $"ElevenLabs said: {message}";
            return string.IsNullOrEmpty(error) ? $"Something went wrong ({code}). Please try again." : $"Something went wrong: {error}";
        }

        static string Unescape(string s)
        {
            try
            {
                return Regex.Unescape(s);
            }
            catch (ArgumentException)
            {
                return s;
            }
        }
    }
}
