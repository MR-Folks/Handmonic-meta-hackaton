using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Builds letters for the lyric worlds from a TypeFace: quads over the face's distance field atlas, drawn with the
    /// Lettering material (the face's atlas as _MainTex). With more than one layer the letter becomes a stack of quads,
    /// front face bright and the layers behind it darker, for type with real depth. A mesh's origin is the middle of
    /// the text on the baseline; +Z points into the letters, away from a viewer they face.
    /// </summary>
    public static class Glyphs
    {
        /// <summary>
        /// How far each quad reaches past its letter (ems), to leave room for the Lettering shader's edge, outline and
        /// glow. Must stay below the atlas's spread (Tools/bake_type.py).
        /// </summary>
        const float Pad = 0.16f;

        static readonly List<Vector3> vertices = new List<Vector3>();
        static readonly List<Vector2> uvs = new List<Vector2>();
        static readonly List<Color> colors = new List<Color>();
        static readonly List<int> triangles = new List<int>();

        /// <summary>Fills <paramref name="mesh"/> with one letter, or empties it for a space. Returns the advance (m).</summary>
        public static float Build(Mesh mesh, TypeFace face, char ch, float height, int layers, float depth, Color front, Color back)
        {
            float advance = face.Step(ch, '\0') * height;
            Begin(mesh);
            if (face.TryGet(ch, out var letter) && letter.HasInk)
                for (int k = 0; k < Mathf.Max(1, layers); k++)
                    AddLetter(face, letter, -advance * 0.5f, height, layers, k, depth, front, back, 0f, 0f);
            Finish(mesh);
            return advance;
        }

        /// <summary>
        /// Fills <paramref name="mesh"/> with a line of text, centred on x = <paramref name="offsetX"/> with its baseline
        /// at y = <paramref name="offsetY"/>. <paramref name="slant"/> leans it over like italics. Returns its width (m).
        /// </summary>
        public static float BuildWord(Mesh mesh, TypeFace face, string text, float height, int layers, float depth, Color front, Color back,
            float slant = 0f, float offsetY = 0f, float offsetX = 0f)
        {
            Begin(mesh);
            if (string.IsNullOrEmpty(text))
            {
                Finish(mesh);
                return 0f;
            }
            float width = face.Width(text) * height;
            // Back layers first, so the front face draws last over them.
            for (int k = 0; k < Mathf.Max(1, layers); k++)
            {
                float x = offsetX - width * 0.5f;
                for (int i = 0; i < text.Length; i++)
                {
                    if (face.TryGet(text[i], out var letter) && letter.HasInk)
                        AddLetter(face, letter, x, height, layers, k, depth, front, back, slant, offsetY);
                    x += face.Step(text[i], i + 1 < text.Length ? text[i + 1] : '\0') * height;
                }
            }
            Finish(mesh);
            return width;
        }

        static void Begin(Mesh mesh)
        {
            mesh.Clear();
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
        }

        static void Finish(Mesh mesh)
        {
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>Adds layer k (0 = the backmost) of a letter whose origin is at x.</summary>
        static void AddLetter(TypeFace face, in TypeFace.Letter letter, float x, float height, int layers, int k, float depth,
            Color front, Color back, float slant, float offsetY)
        {
            int count = Mathf.Max(1, layers);
            int layer = count - 1 - k;
            float z = count > 1 ? depth * layer / (count - 1) : 0f;
            float shade = count > 1 ? layer / (float)(count - 1) : 0f;
            var c = layer == 0 ? front : Color.Lerp(Color.Lerp(front, back, 0.45f), back, shade);

            float left = letter.Left - Pad, right = letter.Right + Pad, bottom = letter.Bottom - Pad, top = letter.Top + Pad;
            float u0 = letter.U - Pad * face.UvPerEm, u1 = letter.U + (letter.Right - letter.Left + Pad) * face.UvPerEm;
            float v0 = letter.V - Pad * face.UvPerEm, v1 = letter.V + (letter.Top - letter.Bottom + Pad) * face.UvPerEm;

            int v = vertices.Count;
            vertices.Add(new Vector3(x + (left + bottom * slant) * height, offsetY + bottom * height, z));
            vertices.Add(new Vector3(x + (left + top * slant) * height, offsetY + top * height, z));
            vertices.Add(new Vector3(x + (right + top * slant) * height, offsetY + top * height, z));
            vertices.Add(new Vector3(x + (right + bottom * slant) * height, offsetY + bottom * height, z));
            uvs.Add(new Vector2(u0, v0));
            uvs.Add(new Vector2(u0, v1));
            uvs.Add(new Vector2(u1, v1));
            uvs.Add(new Vector2(u1, v0));
            for (int q = 0; q < 4; q++) colors.Add(c);
            triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
            triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
        }

        /// <summary>A GameObject that draws one letter mesh with the given material.</summary>
        public static MeshRenderer MakeLetter(string name, Transform parent, Material material, out Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }
    }
}
