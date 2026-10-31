using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// A typeface baked into a signed distance field atlas (Tools/bake_type.py). The atlas never changes while the app
    /// runs, so words built from it can't turn to gibberish the way words from a growing font texture do, and the
    /// Lettering material keeps them sharp at any size. Metrics are in ems, where 1 em is a word's letter height.
    /// </summary>
    public sealed class TypeFace
    {
        public readonly struct Letter
        {
            public readonly float Advance, Left, Bottom, Right, Top, U, V;

            public Letter(float advance, float left, float bottom, float right, float top, float u, float v)
            {
                Advance = advance;
                Left = left;
                Bottom = bottom;
                Right = right;
                Top = top;
                U = u;
                V = v;
            }

            public bool HasInk => Right > Left;
        }

        /// <summary>A sensible gap for a letter the face doesn't have.</summary>
        const float MissingAdvance = 0.3f;

        static TypeFace bold, marker;

        /// <summary>The face of everything people read in the app.</summary>
        public static TypeFace Bold => bold ??= new TypeFace("Type/Bold");

        /// <summary>The spray-can hand of the Graffiti world.</summary>
        public static TypeFace Marker => marker ??= new TypeFace("Type/Marker");

        readonly Dictionary<char, Letter> letters = new Dictionary<char, Letter>();
        readonly Dictionary<int, float> kerning = new Dictionary<int, float>();

        public Texture2D Atlas { get; }
        /// <summary>The atlas width of 1 em, as a share of the atlas.</summary>
        public float UvPerEm { get; private set; }

        TypeFace(string path)
        {
            Atlas = Resources.Load<Texture2D>(path);
            var metrics = Resources.Load<TextAsset>(path);
            if (Atlas == null || metrics == null)
            {
                Debug.LogError($"[Type] Missing {path}; run Tools/bake_type.py.");
                return;
            }
            foreach (var line in metrics.text.Split('\n'))
            {
                var parts = line.Split(' ');
                switch (parts[0])
                {
                    case "face":
                        UvPerEm = Number(parts[1]) / Number(parts[2]);
                        break;
                    case "g":
                        letters[(char)int.Parse(parts[1], CultureInfo.InvariantCulture)] = new Letter(Number(parts[2]), Number(parts[3]),
                            Number(parts[4]), Number(parts[5]), Number(parts[6]), Number(parts[7]), Number(parts[8]));
                        break;
                    case "k":
                        kerning[Pair((char)int.Parse(parts[1], CultureInfo.InvariantCulture), (char)int.Parse(parts[2], CultureInfo.InvariantCulture))] = Number(parts[3]);
                        break;
                }
            }
        }

        static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);

        static int Pair(char first, char second) => (first << 16) | second;

        public bool TryGet(char ch, out Letter letter) => letters.TryGetValue(ch, out letter);

        /// <summary>True when the face has every letter of <paramref name="text"/>.</summary>
        public bool Covers(string text)
        {
            foreach (char ch in text)
                if (!letters.ContainsKey(ch)) return false;
            return true;
        }

        /// <summary>How far the next letter sits from this one's origin, in ems, kerning included.</summary>
        public float Step(char ch, char next)
        {
            float advance = letters.TryGetValue(ch, out var letter) ? letter.Advance : MissingAdvance;
            return kerning.TryGetValue(Pair(ch, next), out float kern) ? advance + kern : advance;
        }

        /// <summary>The width of a line of text, in ems.</summary>
        public float Width(string text)
        {
            float width = 0f;
            for (int i = 0; i < text.Length; i++) width += Step(text[i], i + 1 < text.Length ? text[i + 1] : '\0');
            return width;
        }
    }
}
