using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        // A composition plan as ElevenLabs sends it (shortened), with an intro that has lyrics, a direction and a
        // chorus that repeats.
        const string SamplePlan = "{\"chunks\":[" +
            "{\"text\":\"[Intro]\\nMorning breaks on a wide blue street\",\"duration_ms\":10000,\"positive_styles\":[\"120 BPM\",\"rock\"],\"negative_styles\":[],\"context_adherence\":\"high\",\"conditioning_ref\":null,\"condition_strength\":null}," +
            "{\"text\":\"[Verse 1]\\nI roll the window down and grin\\n{softly} Let the fresh air rush right in\",\"duration_ms\":20000,\"positive_styles\":[\"rock verse\"],\"negative_styles\":[]}," +
            "{\"text\":\"[Chorus]\\nIt's a sunshine day, and I'm alive\\nLaughing out loud with the world outside\",\"duration_ms\":20000,\"positive_styles\":[\"anthemic chorus\"],\"negative_styles\":[]}," +
            "{\"text\":\"[Outro]\",\"duration_ms\":8000,\"positive_styles\":[\"fade out\"],\"negative_styles\":[]}]}";

        /// <summary>
        /// Making a song with ElevenLabs, without the network: the plan's lyrics and title, what is sent to be composed,
        /// the word-timed lyrics from an alignment (and the section timing without one), what a failure says, the key
        /// file, the emoji choices, and the page's picture support and the headset keyboard.
        /// </summary>
        static bool CheckSongMaker()
        {
            bool ok = true;
            void Fail(string what)
            {
                Debug.LogError($"[Check] Song maker: {what}");
                ok = false;
            }

            // Five themes and five genres, each an emoji with a name that fits its tile.
            foreach (var list in new[] { SongMaker.Themes, SongMaker.Genres })
            {
                if (list.Length != 5) Fail($"expected 5 choices, found {list.Length}.");
                if (list.Select(c => c.Emoji).Distinct().Count() != list.Length) Fail("two choices share an emoji.");
                foreach (var c in list)
                {
                    if (string.IsNullOrEmpty(c.Emoji) || c.Emoji.Any(ch => ch < 0x2000)) Fail($"{c.Name} has no emoji.");
                    if (TypeFace.Bold.Width(c.Name) * 0.0135f > 0.09f) Fail($"{c.Name} doesn't fit its tile.");
                }
            }
            if (SongMaker.Genres.Concat(SongMaker.Themes).Any(c => c.Emoji.Any(char.IsLetter))) Fail("an emoji has letters in it.");

            var plan = SongMaker.ParsePlan(SamplePlan);
            var lines = SongMaker.LyricLines(plan);
            string[] expected =
            {
                "Morning breaks on a wide blue street", "I roll the window down and grin", "Let the fresh air rush right in",
                "It's a sunshine day, and I'm alive", "Laughing out loud with the world outside"
            };
            if (!lines.Select(l => l.Text).SequenceEqual(expected)) Fail($"the lyrics read as: {string.Join(" / ", lines.Select(l => l.Text))}");
            string title = SongMaker.Title(plan, 1, 1);
            if (title != "It's a sunshine day") Fail($"the title is \"{title}\", not the chorus's first words.");
            if (SongMaker.Title(SongMaker.ParsePlan("{\"chunks\":[{\"text\":\"[Intro]\",\"duration_ms\":5000}]}"), 1, 1) != "Happy Rock")
                Fail("a song without lyrics isn't named after its theme and genre.");

            var compose = SongMaker.ComposeJson(plan);
            if (!compose.Contains("\"model_id\":\"music_v2_5\"") || !compose.Contains("\"chunks\"") || compose.Contains("conditioning_ref") ||
                !compose.Contains("{softly}") || !compose.Contains("\"duration_ms\":20000"))
                Fail($"the plan sent to be composed is wrong: {compose}");
            var planJson = SongMaker.PlanJson(3, 2);
            if (!planJson.Contains("lo-fi") || !planJson.Contains("dancing") || !planJson.Contains($"\"music_length_ms\":{SongMaker.SongSeconds * 1000}"))
                Fail($"the plan request is wrong: {planJson}");

            // An alignment that heard every word but one ("grin"), and one extra ("yeah").
            var heard = new List<SongMaker.Word>();
            float t = 10.5f;
            foreach (var line in lines)
            {
                foreach (var w in line.Text.Split(' '))
                {
                    if (w != "grin") heard.Add(new SongMaker.Word { text = w, start = t, end = t + 0.3f });
                    t += 0.4f;
                }
                heard.Add(new SongMaker.Word { text = " ", start = t, end = t });
                t += 2f;
            }
            heard.Insert(3, new SongMaker.Word { text = "yeah", start = 11.6f, end = 11.7f });
            var lrc = SongMaker.Lrc(title, plan, lines, new SongMaker.Alignment { words = heard.ToArray() });
            var track = LyricsTrack.Parse(lrc);
            int words = lines.Sum(l => l.Text.Split(' ').Length);
            var times = track.Lines.SelectMany(l => l.Words).Select(w => w.Time).ToList();
            if (track.Lines.Count != lines.Count || times.Count != words) Fail($"the timed lyrics have {track.Lines.Count} lines and {times.Count} words:\n{lrc}");
            else
            {
                for (int i = 1; i < times.Count; i++)
                    if (times[i] < times[i - 1]) Fail("a word comes before the one sung before it.");
                if (Mathf.Abs(times[0] - 10.5f) > 0.02f) Fail($"the first word is at {times[0]:0.00} s, not 10.50 s.");
                if (!lrc.StartsWith("[ti:It's a sunshine day]\n[ar:Made with ElevenLabs]\n")) Fail("the lyrics don't name the song.");
            }

            // Without an alignment the lines follow their sections: the verse starts at 10 s and its second line at 20 s.
            var bySection = LyricsTrack.Parse(SongMaker.Lrc(title, plan, lines, null));
            if (bySection.Lines.Count != lines.Count || Mathf.Abs(bySection.Lines[1].Time - 10f) > 0.02f || Mathf.Abs(bySection.Lines[2].Time - 20f) > 0.02f)
                Fail("the lyrics timed by section don't start where their sections do.");

            // What a failure says.
            var permission = SongMaker.Explain(401, "{\"detail\":{\"status\":\"missing_permissions\",\"message\":\"The API key you used is missing the permission music_generation to execute this operation.\"}}", null, out bool keyProblem);
            if (!permission.Contains("Music permission") || !keyProblem) Fail($"a missing permission reads \"{permission}\".");
            var invalid = SongMaker.Explain(401, "{\"detail\":{\"status\":\"invalid_api_key\",\"message\":\"Invalid API key\"}}", null, out keyProblem);
            if (!invalid.Contains("didn't accept") || !keyProblem) Fail($"a wrong key reads \"{invalid}\".");
            var credits = SongMaker.Explain(401, "{\"detail\":{\"status\":\"quota_exceeded\",\"message\":\"This request exceeds your quota.\"}}", null, out keyProblem);
            if (!credits.Contains("credits") || keyProblem) Fail($"running out of credits reads \"{credits}\".");
            if (!SongMaker.Explain(0, null, "Cannot resolve destination host", out _).Contains("online")) Fail("being offline isn't said.");
            if (!SongMaker.Explain(429, "", null, out _).Contains("busy")) Fail("a busy ElevenLabs isn't said.");
            foreach (var message in new[] { permission, invalid, credits })
            {
                SongLibrary.Wrap(message, 0.0145f, 0.52f, out var first, out var second);
                if ((first + " " + second).Trim() != message) Fail($"\"{message}\" doesn't fit on two lines of the page.");
            }

            // A key file is taken in and deleted, and a key pasted from a .env file is cleaned up. The key the editor
            // already has (if any) is put back afterwards.
            const string pref = "elevenlabs.api-key";
            bool had = PlayerPrefs.HasKey(pref);
            string old = PlayerPrefs.GetString(pref, "");
            var file = Path.Combine(Path.GetTempPath(), SongMaker.KeyFileName);
            File.WriteAllText(file, "\n  ELEVENLABS_API_KEY=\"sk_test_0123456789\"  \n");
            bool imported = SongMaker.ImportKeyFile(file);
            if (!imported || File.Exists(file) || PlayerPrefs.GetString(pref) != "sk_test_0123456789") Fail("the key file isn't taken in and deleted.");
            SongMaker.ForgetKey();
            if (SongMaker.HasKey) Fail("forgetting the key doesn't.");
            if (had) PlayerPrefs.SetString(pref, old);
            if (File.Exists(file)) File.Delete(file);

            if (SongMaker.FileName("What: a/song?") != "What asong.mp3") Fail($"a title becomes the file name {SongMaker.FileName("What: a/song?")}.");

            // The page can show emojis, and the headset's keyboard is asked for.
            var panel = Shader.Find("MusicInYourHands/Panel");
            if (panel == null || panel.FindPropertyIndex("_Picture") < 0 || panel.FindPropertyIndex("_MainTex") < 0) Fail("the Panel shader can't show a picture.");
            if (!File.ReadAllText("Assets/Plugins/Android/AndroidManifest.xml").Contains("oculus.software.overlay_keyboard")) Fail("the manifest doesn't ask for the system keyboard.");
            if (!File.Exists("Assets/Plugins/Android/EmojiPicture.java")) Fail("the emoji helper is missing.");

            if (ok)
                Debug.Log($"[Check] Song maker: 5 theme and 5 genre emojis; a plan gives {lines.Count} sung lines and the title \"{title}\"; " +
                          $"the alignment times all {words} words in order (one unheard, one extra); without it the lines follow their sections; " +
                          "failures say what to do; the key file is taken in and deleted; the page shows pictures and the keyboard is asked for.");
            return ok;
        }
    }
}
