using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// A line of small text for the menu, the world hints and the like, drawn with the same lettering as the lyrics.
    /// Its mesh is only rebuilt when the text changes.
    /// </summary>
    public sealed class TypeLabel
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        const int LabelQueue = 3120;
        static Material shared;

        readonly MeshRenderer renderer;
        readonly Mesh mesh;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly float height, centreY;
        readonly bool left;
        string text = "";

        public Transform Transform => renderer.transform;
        public GameObject GameObject => renderer.gameObject;

        public Color Color
        {
            set
            {
                block.SetColor(ColorId, value);
                renderer.SetPropertyBlock(block);
            }
        }

        public string Text
        {
            get => text;
            set
            {
                if (text == (value ?? "")) return;
                text = value ?? "";
                float offsetX = left ? TypeFace.Bold.Width(text) * height * 0.5f : 0f;
                Glyphs.BuildWord(mesh, TypeFace.Bold, text, height, 1, 0f, Color.white, Color.white, offsetY: centreY, offsetX: offsetX);
            }
        }

        /// <param name="height">Letter height (m).</param>
        /// <param name="anchorY">Where the object's origin sits on the text: 0 at its baseline, 1 at the top of its capitals.</param>
        /// <param name="left">Starts the text at the origin, instead of centring it there.</param>
        public TypeLabel(string name, Transform parent, float height, float anchorY = 0.5f, bool left = false)
        {
            this.height = height;
            this.left = left;
            centreY = -height * 0.71f * anchorY;
            if (shared == null)
                shared = new Material(Shader.Find("MusicInYourHands/Lettering")) { name = "Label Lettering", mainTexture = TypeFace.Bold.Atlas, renderQueue = LabelQueue };
            renderer = Glyphs.MakeLetter(name, parent, shared, out mesh);
        }
    }
}
