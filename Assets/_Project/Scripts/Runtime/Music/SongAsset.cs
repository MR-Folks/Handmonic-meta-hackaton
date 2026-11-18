using System;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>A playable song: its audio plus optional timed lyrics (an .lrc file).</summary>
    [CreateAssetMenu(menuName = "Music In Your Hands/Song", fileName = "Song")]
    public class SongAsset : ScriptableObject
    {
        public string title;
        public string artist;
        public AudioClip clip;

        [Tooltip("Timed lyrics in LRC format. Word times (<mm:ss.xx>) are used when present.")]
        public TextAsset lyrics;

        [Tooltip("Optional melody notes (.notes: time, MIDI pitch, length per line). Lets worlds play in time with the tune.")]
        public TextAsset notes;

        [Tooltip("Seconds added to the song time before looking up lyrics. Use it to nudge an LRC that is slightly early or late.")]
        public float lyricsOffset;

        /// <summary>
        /// For a song the person added on the headset: its audio file, loaded only while it plays, and its lyrics
        /// file, read each time it starts. Empty for the songs built into the app.
        /// </summary>
        [NonSerialized] public string audioPath, lyricsPath;

        public bool IsAdded => !string.IsNullOrEmpty(audioPath);
    }
}
