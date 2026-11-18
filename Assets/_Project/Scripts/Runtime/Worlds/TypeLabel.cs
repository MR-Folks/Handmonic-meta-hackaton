using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// A line of small text for the menu, the world hints and the like, drawn with the same lettering as the lyrics.
    /// Its mesh is only rebuilt when the text changes. For text that must read over a bright real room it can have a
    /// soft dark backing behind it (<see cref="Backing"/>) and a dark halo round its letters (<see cref="Halo"/>).
    /// </summary>
    public sealed class TypeLabel
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int ShadeId = Shader.PropertyToID("_Shade");
        static readonly int EdgeId = Shader.PropertyToID("_Edge");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int SoftId = Shader.PropertyToID("_Soft");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        const int LabelQueue = 3120, BackingQueue = 3110;
        /// <summary>The height of the capitals, as a share of the letter height (the Bold face's H).</summary>
        public const float CapitalHeight = 0.7125f;
        static Material shared, backingMaterial;
        static readonly List<TypeLabel> all = new List<TypeLabel>();

        readonly MeshRenderer renderer;
        readonly Mesh mesh;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly float height, centreY;
        readonly bool left;
        string text = "";
        Renderer backing;
        MaterialPropertyBlock backingBlock;
        float backingStrength, alpha = 1f;

        public Transform Transform => renderer.transform;
        public GameObject GameObject => renderer.gameObject;

        public Color Color
        {
            set
            {
                alpha = value.a;
                block.SetColor(ColorId, value);
                renderer.SetPropertyBlock(block);
                if (backing != null) ColorBacking();
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
                if (backing != null) ShapeBacking();
            }
        }

        /// <summary>
        /// A soft, dark rounded backing behind the text, 0 (none) to 1, so light text reads over a bright room. It
        /// fades with the text's colour.
        /// </summary>
        public float Backing
        {
            get => backingStrength;
            set
            {
                backingStrength = Mathf.Clamp01(value);
                if (backingStrength > 0f && backing == null) MakeBacking();
                if (backing == null) return;
                backing.gameObject.SetActive(backingStrength > 0f);
                ColorBacking();
            }
        }

        /// <summary>A soft dark halo round the letters themselves, 0 (none) to 1: for text on things, where a backing would be too much.</summary>
        public float Halo
        {
            set
            {
                block.SetFloat(ShadeId, Mathf.Clamp01(value));
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>Whether this is a caption people need to read (checked to be at least a degree tall from the eyes), not small print.</summary>
        public bool IsCaption { get; set; }

        /// <summary>The letter height (m) at the label's own scale: the height of 1 em.</summary>
        public float LetterHeight => height;

        /// <summary>Whether the label shows now: in the scene, switched on, with text, and not faded out.</summary>
        public bool Visible => renderer != null && renderer.gameObject.activeInHierarchy && text.Length > 0 && alpha > 0.05f;

        /// <summary>
        /// How tall the capital letters look from <paramref name="eye"/>, in degrees: the angle they cover, at the
        /// label's scale and distance.
        /// </summary>
        public float CapitalAngle(Vector3 eye)
        {
            float capitals = height * CapitalHeight * Mathf.Abs(renderer.transform.lossyScale.y);
            float distance = Mathf.Max((renderer.bounds.center - eye).magnitude, 1e-4f);
            return 2f * Mathf.Atan(capitals * 0.5f / distance) * Mathf.Rad2Deg;
        }

        /// <summary>Every label made so far that still exists (for the editor checks).</summary>
        public static IReadOnlyList<TypeLabel> All
        {
            get
            {
                all.RemoveAll(l => l.renderer == null);
                return all;
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
            all.Add(this);
        }

        void MakeBacking()
        {
            if (backingMaterial == null)
            {
                var shader = Shader.Find("MusicInYourHands/Panel");
                if (shader == null) return;
                backingMaterial = new Material(shader) { name = "Label Backing", renderQueue = BackingQueue };
            }
            backing = Effects.MakeQuad(renderer.gameObject.name + " Backing", renderer.transform, backingMaterial);
            backingBlock = new MaterialPropertyBlock();
            ShapeBacking();
        }

        /// <summary>The backing's size and place: round-ended, a little wider and taller than the text, just behind it.</summary>
        void ShapeBacking()
        {
            float width = TypeFace.Bold.Width(text) * height;
            var size = new Vector2(width + height * 1.1f, height * 1.45f);
            // The text runs from just under its baseline (descenders) to the top of its capitals.
            float middle = centreY + height * 0.27f;
            backing.transform.localPosition = new Vector3(left ? width * 0.5f : 0f, middle, 0.002f);
            backing.transform.localRotation = Quaternion.identity;
            backing.transform.localScale = new Vector3(size.x, size.y, 1f);
            backingBlock.SetVector(SizeId, size);
            backingBlock.SetFloat(RadiusId, size.y * 0.5f);
            backingBlock.SetFloat(SoftId, size.y * 0.22f);
            ColorBacking();
        }

        void ColorBacking()
        {
            backingBlock.SetColor(ColorId, new Color(0.02f, 0.022f, 0.035f, 0.78f * backingStrength));
            backingBlock.SetColor(EdgeId, new Color(1f, 1f, 1f, 0f));
            backingBlock.SetFloat(AlphaId, Mathf.Clamp01(alpha));
            backing.SetPropertyBlock(backingBlock);
        }
    }
}
