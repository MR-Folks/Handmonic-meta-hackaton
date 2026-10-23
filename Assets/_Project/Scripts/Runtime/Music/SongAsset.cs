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

        [Tooltip("Seconds added to the song time before looking up lyrics. Use it to nudge an LRC that is slightly early or late.")]
        public float lyricsOffset;
    }
}
