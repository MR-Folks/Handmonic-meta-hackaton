using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Builds 3D letters for the lyric worlds: one mesh per letter, made of a stack of glyph quads from the font's
    /// texture, front face bright and the layers behind it darker, so a letter reads as a solid block of type with
    /// depth. Draw it with the Lettering material (the font's texture as _MainTex). The mesh's origin is the middle of
    /// the letter's advance on the baseline; +Z points into the letter, away from a viewer it faces.
    /// </summary>
    public static class Glyphs
    {
        /// <summary>Fills <paramref name="mesh"/> with an extruded letter, or empties it for a space. Returns the advance (m).</summary>
        public static float Build(Mesh mesh, Font font, char ch, int fontSize, FontStyle style, float height, int layers, float depth,
            Color front, Color back)
        {
            mesh.Clear();
            font.RequestCharactersInTexture(ch.ToString(), fontSize, style);
            if (!font.GetCharacterInfo(ch, out var info, fontSize, style)) return height * 0.3f;
            float scale = height / fontSize;
            float advance = info.advance * scale;
            if (char.IsWhiteSpace(ch) || info.maxX <= info.minX) return advance;

            layers = Mathf.Max(1, layers);
            var vertices = new Vector3[layers * 4];
            var uvs = new Vector2[layers * 4];
            var colors = new Color[layers * 4];
            var triangles = new int[layers * 6];
            float x0 = info.minX * scale - advance * 0.5f, x1 = info.maxX * scale - advance * 0.5f;
            float y0 = info.minY * scale, y1 = info.maxY * scale;
            // Back layers first, so the front face draws last over them.
            for (int k = 0; k < layers; k++)
            {
                int layer = layers - 1 - k;
                float z = layers > 1 ? depth * layer / (layers - 1) : 0f;
                float shade = layers > 1 ? layer / (float)(layers - 1) : 0f;
                var c = layer == 0 ? front : Color.Lerp(Color.Lerp(front, back, 0.45f), back, shade);
                int v = k * 4;
                vertices[v] = new Vector3(x0, y0, z);
                vertices[v + 1] = new Vector3(x0, y1, z);
                vertices[v + 2] = new Vector3(x1, y1, z);
                vertices[v + 3] = new Vector3(x1, y0, z);
                uvs[v] = info.uvBottomLeft;
                uvs[v + 1] = info.uvTopLeft;
                uvs[v + 2] = info.uvTopRight;
                uvs[v + 3] = info.uvBottomRight;
                for (int q = 0; q < 4; q++) colors[v + q] = c;
                int t = k * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return advance;
        }

        /// <summary>
        /// Fills <paramref name="mesh"/> with a whole word of extruded letters on one line, centred on the origin
        /// horizontally with its baseline at y = 0. Returns the word's width (m).
        /// </summary>
        public static float BuildWord(Mesh mesh, Font font, string text, int fontSize, FontStyle style, float height, int layers, float depth,
            Color front, Color back)
        {
            mesh.Clear();
            if (string.IsNullOrEmpty(text)) return 0f;
            font.RequestCharactersInTexture(text, fontSize, style);
            float scale = height / fontSize;
            float width = 0f;
            foreach (char ch in text)
                width += font.GetCharacterInfo(ch, out var ci, fontSize, style) ? ci.advance * scale : height * 0.3f;

            layers = Mathf.Max(1, layers);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var colors = new System.Collections.Generic.List<Color>();
            var triangles = new System.Collections.Generic.List<int>();
            for (int k = 0; k < layers; k++)
            {
                int layer = layers - 1 - k;
                float z = layers > 1 ? depth * layer / (layers - 1) : 0f;
                float shade = layers > 1 ? layer / (float)(layers - 1) : 0f;
                var c = layer == 0 ? front : Color.Lerp(Color.Lerp(front, back, 0.45f), back, shade);
                float x = -width * 0.5f;
                foreach (char ch in text)
                {
                    if (!font.GetCharacterInfo(ch, out var info, fontSize, style)) { x += height * 0.3f; continue; }
                    if (!char.IsWhiteSpace(ch) && info.maxX > info.minX)
                    {
                        int v = vertices.Count;
                        float x0 = x + info.minX * scale, x1 = x + info.maxX * scale;
                        float y0 = info.minY * scale, y1 = info.maxY * scale;
                        vertices.Add(new Vector3(x0, y0, z)); vertices.Add(new Vector3(x0, y1, z));
                        vertices.Add(new Vector3(x1, y1, z)); vertices.Add(new Vector3(x1, y0, z));
                        uvs.Add(info.uvBottomLeft); uvs.Add(info.uvTopLeft); uvs.Add(info.uvTopRight); uvs.Add(info.uvBottomRight);
                        colors.Add(c); colors.Add(c); colors.Add(c); colors.Add(c);
                        triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
                        triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
                    }
                    x += info.advance * scale;
                }
            }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return width;
        }

        /// <summary>The advance of a letter (m) without building it.</summary>
        public static float Advance(Font font, char ch, int fontSize, FontStyle style, float height)
        {
            font.RequestCharactersInTexture(ch.ToString(), fontSize, style);
            return font.GetCharacterInfo(ch, out var info, fontSize, style) ? info.advance * height / fontSize : height * 0.3f;
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
