#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// A test world for the editor checks (CheckWorldHooks, CheckCombining): it writes down each hook World calls, and
    /// can be given any spaces and a mark it paints on the hands each frame. It is compiled only in the editor and
    /// hidden from the menu; it lives outside the Editor folder because Unity won't attach a component from there, and
    /// in a file of its own name because Unity won't attach one otherwise.
    /// </summary>
    [HiddenWorld]
    public sealed class HookProbe : World
    {
        public bool Lyric;
        public Space Uses = Space.All;
        /// <summary>When above 0, OnStep sets HandSkin.FocusStrength to it and nudges the left hand by it (in metres).</summary>
        public float Mark;
        public int Built, Placed, Steps, BeatsSeen, Entered, Exited;
        public readonly List<int> Notes = new List<int>();
        public readonly List<string> Words = new List<string>();
        public readonly List<string> Hands = new List<string>();

        public override string DisplayName => "Hook probe";
        public override Color Color => Color.white;
        public override bool IsLyric => Lyric;
        public override Space Spaces => Uses;
        public override HandTheme Theme => new HandTheme { Fill = new Color(Mark, 0f, 0f, 1f) };
        protected override int SungWordCount => Lyric ? 0 : 3;

        public float Fade => FadeIn;
        public int ShownWords => SungWords == null ? -1 : SungWords.Words.Count(w => w.Active);

        protected override void OnBuild() => Built++;
        protected override void OnEnter() => Entered++;
        protected override void OnExit() => Exited++;
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

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Steps++;
            if (Mark <= 0f) return;
            HandSkin.FocusStrength = Mark;
            if (rig != null) Effects.OffsetHand(rig.Left, Vector3.right * Mark);
        }
    }
}
#endif
