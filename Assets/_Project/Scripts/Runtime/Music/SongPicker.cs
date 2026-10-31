using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Adds songs from inside the headset: opens the system file picker (SongPickerActivity), where you choose an audio
    /// file and, if you like, its lyrics as an .lrc or plain .txt file. The picker copies them into the Songs folder,
    /// and back in the app the new song joins the list and starts playing. Nothing leaves the headset.
    /// </summary>
    public static class SongPicker
    {
        static readonly HashSet<string> known = new HashSet<string>();

        /// <summary>Opens the picker. False when there's no picker here (in the editor), so nothing will come back.</summary>
        public static bool Open(MusicBus bus)
        {
            known.Clear();
            foreach (var song in bus.Songs)
                if (song != null && song.IsAdded) known.Add(song.audioPath);
#if UNITY_ANDROID && !UNITY_EDITOR
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var picker = new AndroidJavaClass("com.musicinyourhands.app.SongPickerActivity");
            picker.CallStatic("open", activity, AddedSongs.Folder);
            return true;
#else
            Debug.Log("[Songs] The song picker only opens on the headset.");
            return false;
#endif
        }

        /// <summary>Picks up what was added, and plays the first new song.</summary>
        public static void Finish(MusicBus bus)
        {
            if (bus == null) return;
            bus.RefreshSongs();
            foreach (var song in bus.Songs)
            {
                if (song == null || !song.IsAdded || known.Contains(song.audioPath)) continue;
                Debug.Log($"[Songs] Added {song.title}{(song.lyricsPath != null ? " with lyrics" : "")}.");
                bus.Play(song);
                return;
            }
        }
    }
}
