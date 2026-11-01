using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// A glowing line through a list of points, rebuilt each frame as a strip that always faces the viewer.
    /// Pair it with the MusicInYourHands/Line material. Ends fade out unless told otherwise.
    /// </summary>
    public sealed class LineStrip
    {
        public readonly GameObject GameObject;
        public readonly MeshRenderer Renderer;

        readonly Mesh mesh;
        readonly Vector3[] vertices;
        readonly Vector2[] uvs;
        readonly Color[] colors;
        readonly int maxPoints;

        public LineStrip(string name, Transform parent, Material material, int maxPoints)
        {
            this.maxPoints = maxPoints;
            GameObject = new GameObject(name);
            GameObject.transform.SetParent(parent, false);
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            GameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = GameObject.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer.receiveShadows = false;

            vertices = new Vector3[maxPoints * 2];
            uvs = new Vector2[maxPoints * 2];
            colors = new Color[maxPoints * 2];
            var triangles = new int[(maxPoints - 1) * 6];
            for (int i = 0; i < maxPoints - 1; i++)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
        }

        /// <summary>
        /// Rebuilds the line through the first <paramref name="count"/> points (world space), facing <paramref name="viewer"/>.
        /// Unused points collapse onto the last one.
        /// </summary>
        public void Set(Vector3[] points, int count, float width, Color color, Vector3 viewer, bool fadeEnds = true)
            => Set(points, null, count, width, color, viewer, fadeEnds);

        /// <summary>As above, with a colour per point (multiplied by <paramref name="color"/>).</summary>
        public void Set(Vector3[] points, Color[] pointColors, int count, float width, Color color, Vector3 viewer, bool fadeEnds = true)
            => Set(points, pointColors, null, count, width, color, viewer, fadeEnds);

        /// <summary>As above, with a width per point as well (multiplied by <paramref name="width"/>), for lines that taper.</summary>
        public void Set(Vector3[] points, Color[] pointColors, float[] pointWidths, int count, float width, Color color, Vector3 viewer, bool fadeEnds = true)
        {
            count = Mathf.Clamp(count, 2, maxPoints);
            var local = GameObject.transform;
            for (int i = 0; i < maxPoints; i++)
            {
                int k = Mathf.Min(i, count - 1);
                var p = points[k];
                var tangent = points[Mathf.Min(k + 1, count - 1)] - points[Mathf.Max(k - 1, 0)];
                var side = Vector3.Cross(tangent, viewer - p);
                side = side.sqrMagnitude > 1e-10f ? side.normalized : Vector3.up;

                float u = k / (float)(count - 1);
                float fade = fadeEnds ? Mathf.SmoothStep(0f, 1f, u / 0.08f) * Mathf.SmoothStep(0f, 1f, (1f - u) / 0.08f) : 1f;
                var half = side * (width * 0.5f * (pointWidths != null ? pointWidths[k] : 1f));
                vertices[i * 2] = local.InverseTransformPoint(p - half);
                vertices[i * 2 + 1] = local.InverseTransformPoint(p + half);
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
                var c = pointColors != null ? pointColors[k] * color : color;
                c.a *= fade;
                colors[i * 2] = c;
                colors[i * 2 + 1] = c;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        public void SetVisible(bool visible)
        {
            if (GameObject.activeSelf != visible) GameObject.SetActive(visible);
        }
    }
}
