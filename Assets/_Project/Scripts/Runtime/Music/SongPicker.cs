using System.IO;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Fetches one file from the headset through the system file picker (SongPickerActivity), which copies it into the
    /// Songs folder. On Quest the picker opens as a window beside the running app, which never loses focus, so the
    /// activity reports back by leaving a small result file for TryTake to find. Nothing leaves the headset.
    /// </summary>
    public static class SongPicker
    {
        public enum Kind { Song, Lyrics }
        public enum Outcome { Added, Cancelled, Unsupported, Failed }

        public readonly struct Result
        {
            public readonly Kind Kind;
            public readonly Outcome Outcome;
            /// <summary>The name of the file that was added to the Songs folder, or of the one that couldn't be.</summary>
            public readonly string File;
            /// <summary>For lyrics, the song they are for.</summary>
            public readonly SongAsset Song;

            public Result(Kind kind, Outcome outcome, string file, SongAsset song)
            {
                Kind = kind;
                Outcome = outcome;
                File = file;
                Song = song;
            }
        }

        const float CheckEvery = 0.25f;

        static Kind kind;
        static SongAsset target;
        static float nextCheck;

        /// <summary>True from opening the picker until its result has been taken.</summary>
        public static bool Waiting { get; private set; }

        static string ResultPath => Path.Combine(Application.persistentDataPath, "picker-result.txt");

        /// <summary>Opens the picker. False where there is no picker (in the editor), so nothing will come back.</summary>
        public static bool Open(Kind what, SongAsset forSong)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            File.Delete(ResultPath);
            kind = what;
            target = forSong;
            Waiting = true;
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var picker = new AndroidJavaClass("com.musicinyourhands.app.SongPickerActivity");
            picker.CallStatic("open", activity, what == Kind.Song ? "song" : "lyrics", AddedSongs.Folder,
                forSong != null ? forSong.audioPath : "", ResultPath);
            return true;
#else
            Debug.Log($"[Songs] The {what} picker only opens on the headset.");
            return false;
#endif
        }

        /// <summary>Hands over the picker's result, once, when it has one.</summary>
        public static bool TryTake(out Result result)
        {
            result = default;
            if (!Waiting || Time.unscaledTime < nextCheck) return false;
            nextCheck = Time.unscaledTime + CheckEvery;
            string text;
            try
            {
                if (!File.Exists(ResultPath)) return false;
                text = File.ReadAllText(ResultPath);
                File.Delete(ResultPath);
            }
            catch (IOException)
            {
                return false;
            }
            Waiting = false;
            Parse(text, out var outcome, out var file);
            result = new Result(kind, outcome, file, target);
            target = null;
            return true;
        }

        /// <summary>Reads a result file: the outcome on the first line, the file's name on the second.</summary>
        public static void Parse(string text, out Outcome outcome, out string file)
        {
            var lines = (text ?? "").Split('\n');
            file = lines.Length > 1 ? lines[1].Trim() : "";
            outcome = lines[0].Trim() switch
            {
                "added" => Outcome.Added,
                "cancelled" => Outcome.Cancelled,
                "unsupported" => Outcome.Unsupported,
                _ => Outcome.Failed
            };
        }
    }
}
