#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// A test world for the editor checks (CheckWorldHooks): it writes down each hook World calls. It is compiled only
    /// in the editor and hidden from the menu; it lives outside the Editor folder because Unity won't attach a
    /// component from there, and in a file of its own name because Unity won't attach one otherwise.
    /// </summary>
    [HiddenWorld]
    public sealed class HookProbe : World
    {
        public bool Lyric;
        public int Built, Placed, Steps, BeatsSeen;
        public readonly List<int> Notes = new List<int>();
        public readonly List<string> Words = new List<string>();
        public readonly List<string> Hands = new List<string>();

        public override string DisplayName => "Hook probe";
        public override Color Color => Color.white;
        public override bool IsLyric => Lyric;
        protected override int SungWordCount => Lyric ? 0 : 3;

        public float Fade => FadeIn;
        public int ShownWords => SungWords == null ? -1 : SungWords.Words.Count(w => w.Active);
        public bool SeesMenu => MenuOpen;
        public bool Pinching(bool left) => IsPinching(left);
        public bool Free(bool left) => PinchFree(left);

        protected override void OnBuild() => Built++;
        protected override void OnPlace(Camera view) => Placed++;
        protected override void OnBeat(MusicFrame music) => BeatsSeen = Beats;
        protected override void OnNote(NoteTrack.Note note) => Notes.Add(note.Midi);

        protected override void OnWord(string word, bool newLine)
        {
            Words.Add(newLine ? "/" + word : word);
            SungWords?.Add(word, Color.white, 2f);
        }

        protected override void OnPinch(HandRig.Hand hand, bool left) => Hands.Add(left ? "pinch L" : "pinch R");
        protected override void OnRelease(HandRig.Hand hand, bool left) => Hands.Add(left ? "release L" : "release R");
        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt) => Steps++;
    }
}
#endif
