using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The sung words for a sound world: a few small 3D words (Glyphs, Lettering material) that the world places and
    /// moves in its own way each frame, by setting each word's Position, Rotation, Size and Color. Words fade in when
    /// added and fade out at the end of their life. Next tells the world when a new word starts; only real lyrics
    /// count, so songs without lyrics (and Listen mode) leave the sound worlds as they are.
    /// </summary>
    public sealed class WordLayer
    {
        public sealed class Word
        {
            public string Text;
            /// <summary>Width (m) at Size 1.</summary>
            public float Width;
            public float Age, Life;
            public Vector3 Position, Velocity;
            /// <summary>+Z points into the letters, away from a reader in front of them.</summary>
            public Quaternion Rotation = Quaternion.identity;
            public float Size = 1f;
            public Color Color = Color.white;
            public float Halo, Alpha = 1f;
            /// <summary>A random number 0-1 for the world's own use (a spot, a direction).</summary>
            public float Seed;
            /// <summary>Which of the world's own things the word belongs to (a string, a fish), for the world's use.</summary>
            public int Slot;
            /// <summary>A place on the world's own surface (a pool, a plate), for the world's use.</summary>
            public Vector2 Spot;
            /// <summary>The line the word was sung in, for worlds that clear a line at once.</summary>
            public int Line;
            public bool Active;
            internal MeshRenderer Renderer;
            internal Mesh Mesh;
        }

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int HaloId = Shader.PropertyToID("_Halo");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int RimId = Shader.PropertyToID("_Rim");

        readonly List<Word> words = new List<Word>();
        readonly LyricCursor cursor = new LyricCursor();
        readonly Material material;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly float letterHeight;

        public IReadOnlyList<Word> Words => words;

        /// <summary>Draw order of the words; raise it to draw them over bright, see-through things (the orb).</summary>
        public int RenderQueue
        {
            get => material.renderQueue;
            set => material.renderQueue = value;
        }

        /// <summary>How strong the dark edge around the letters is, 0-1. It only shows on words without a Halo.</summary>
        public float Rim
        {
            set => material.SetFloat(RimId, value);
        }

        /// <summary>The current word's position in its line (0 for the first word).</summary>
        public int Index => cursor.Index;

        public WordLayer(string name, Transform parent, Material lettering, int count, float letterHeight)
        {
            this.letterHeight = letterHeight;
            material = new Material(lettering) { name = $"{name} Lettering", mainTexture = TypeFace.Bold.Atlas };
            for (int i = 0; i < count; i++)
            {
                var w = new Word();
                w.Renderer = Glyphs.MakeLetter($"{name} Word {i}", parent, material, out w.Mesh);
                w.Renderer.gameObject.SetActive(false);
                words.Add(w);
            }
        }

        /// <summary>True on the frame a new sung word starts.</summary>
        public bool Next(MusicFrame music, out string text, out bool newLine)
        {
            text = null;
            newLine = false;
            if (string.IsNullOrEmpty(music.Word))
            {
                cursor.Reset();
                return false;
            }
            if (!cursor.Advance(music, out newLine)) return false;
            text = cursor.Word;
            return true;
        }

        /// <summary>
        /// Shows a word for <paramref name="life"/> seconds, reusing the oldest if all are in use. With
        /// <paramref name="alone"/>, the words already showing fade out quickly.
        /// </summary>
        public Word Add(string text, Color color, float life, bool alone = false)
        {
            if (alone)
                foreach (var o in words)
                    if (o.Active) o.Life = Mathf.Min(o.Life, o.Age + 0.35f);
            Word w = null;
            foreach (var o in words)
                if (!o.Active) { w = o; break; }
            if (w == null)
                foreach (var o in words)
                    if (w == null || o.Age / o.Life > w.Age / w.Life) w = o;
            w.Text = text;
            w.Width = Glyphs.BuildWord(w.Mesh, TypeFace.Bold, text, 1f, 1, 0f, Color.white, Color.white);
            w.Age = 0f;
            w.Life = life;
            w.Velocity = Vector3.zero;
            w.Size = 1f;
            w.Color = color;
            w.Halo = 0f;
            w.Alpha = 1f;
            w.Seed = Random.value;
            w.Line = cursor.Line;
            w.Active = true;
            w.Renderer.gameObject.SetActive(true);
            return w;
        }

        /// <summary>Ages the words and puts away the ones whose life is over.</summary>
        public void Tick(float dt)
        {
            foreach (var w in words)
            {
                if (!w.Active) continue;
                w.Age += dt;
                if (w.Age >= w.Life) Hide(w);
            }
        }

        /// <summary>Draws every word where the world put it. <paramref name="fade"/> fades the whole layer.</summary>
        public void Draw(float fade)
        {
            foreach (var w in words)
            {
                if (!w.Active) continue;
                w.Renderer.transform.SetPositionAndRotation(w.Position, w.Rotation);
                w.Renderer.transform.localScale = Vector3.one * (letterHeight * w.Size);
                float alpha = Mathf.Clamp01(w.Age / 0.25f) * Mathf.Clamp01((w.Life - w.Age) / 0.6f) * w.Alpha * fade;
                block.Clear();
                block.SetColor(ColorId, w.Color);
                block.SetFloat(AlphaId, alpha);
                block.SetFloat(HaloId, w.Halo);
                block.SetFloat(GlowId, 0.1f);
                w.Renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>The word's height in metres at its current size.</summary>
        public float Height(Word w) => letterHeight * w.Size;

        public void Clear()
        {
            cursor.Reset();
            foreach (var w in words) Hide(w);
        }

        static void Hide(Word w)
        {
            w.Active = false;
            w.Renderer.gameObject.SetActive(false);
        }

        /// <summary>Turned to be read from <paramref name="eye"/>, upright.</summary>
        public static Quaternion Facing(Vector3 position, Vector3 eye)
        {
            var away = position - eye;
            return away.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(away.normalized, Vector3.up) : Quaternion.identity;
        }

        /// <summary>Lying flat face up, its top edge pointing away from <paramref name="eye"/>, so it reads from there.</summary>
        public static Quaternion Lying(Vector3 position, Vector3 eye)
        {
            var away = Vector3.ProjectOnPlane(position - eye, Vector3.up);
            return Quaternion.LookRotation(Vector3.down, away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward);
        }
    }
}
