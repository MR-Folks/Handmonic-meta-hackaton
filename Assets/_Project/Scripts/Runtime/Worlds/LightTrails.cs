using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Light trails world: draw the song in the air. Pinch and move, and a ribbon of light follows your fingers,
    /// lying flat in the plane of your hand like a brush stroke, its colour following the song as it plays. Everything
    /// you have drawn stays hanging in the room and swells on every beat, so the sculpture breathes with the music.
    /// Old strokes fade after a while; hold both fists closed for a moment to clear it all.
    /// </summary>
    public class LightTrails : World
    {
        public override string DisplayName => "Light trails";

        public override Color Color => new Color(1f, 0.83f, 0.42f);

        public override int MenuOrder => 110;

        public override int MenuIcon => 17;

        public override string Hint => "Pinch and move to draw; two fists clear it";

        /// <summary>You draw in the air in front of you with a pinch, and clear it with both fists.</summary>
        public override Space Spaces => Space.InFront | Space.Pinch | Space.Fists;

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.14f, 0.12f, 0.1f, 0.5f),
            Edge = new Color(1f, 0.83f, 0.42f),
            Glow = new Color(1f, 0.6f, 0.85f),
            Pattern = HandTheme.Effect.Electric,
            PatternColor = new Color(1f, 0.9f, 0.6f)
        };

        [Header("Materials")]
        [SerializeField] Material trailMaterial;

        [Header("Strokes")]
        [SerializeField] int maxStrokes = 60;
        [SerializeField] int pointsPerStroke = 320;
        [Tooltip("Half width of a stroke (m), at rest and at a fast sweep.")]
        [SerializeField] Vector2 halfWidth = new Vector2(0.008f, 0.02f);
        [Tooltip("Seconds a stroke stays before it starts to fade.")]
        [SerializeField] float lifetime = 120f;

        static readonly int KickId = Shader.PropertyToID("_Kick");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly Color[] Palette = { Effects.Pink, Effects.Orange, Effects.Gold, Effects.Mint, Effects.Blue, Effects.Violet };

        sealed class Stroke
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public readonly List<Vector3> Centres = new List<Vector3>();
            public readonly List<Vector3> Halves = new List<Vector3>();
            public readonly List<Color> Colors = new List<Color>();
            public float Age = -1f;
            public float Length;
        }

        readonly List<Stroke> strokes = new List<Stroke>();
        readonly Stroke[] drawing = new Stroke[2];
        readonly Vector3[] lastPoint = new Vector3[2];
        Renderer[] nibs;
        MaterialPropertyBlock block;
        float clearHold, clearFade = 1f;

        protected override int SungWordCount => 6;

        protected override float SungWordHeight => 0.032f;

        protected override float SungWordsAlpha => clearFade;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            for (int i = 0; i < maxStrokes; i++)
            {
                var go = new GameObject($"Stroke {i}");
                go.transform.SetParent(transform, false);
                var mesh = new Mesh { name = go.name };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = trailMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                strokes.Add(new Stroke { Renderer = r, Mesh = mesh });
            }
            nibs = new[] { Effects.MakeQuad("Nib L", transform, glowMaterial), Effects.MakeQuad("Nib R", transform, glowMaterial) };
            foreach (var n in nibs) n.gameObject.SetActive(false);
        }

        protected override void OnExit()
        {
            drawing[0] = drawing[1] = null;
            foreach (var n in nibs) n.gameObject.SetActive(false);
        }

        /// <summary>
        /// Each sung word comes off the tip of the light you are drawing, in its colour, and floats slowly up and
        /// away from it. With no hand drawing, it appears in front of you.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            var eye = View.transform.position;
            var w = SungWords.Add(word, Color.Lerp(SongColor(Music), Color.white, 0.4f), 2.6f);
            int side = drawing[1] != null ? 1 : drawing[0] != null ? 0 : -1;
            var from = side >= 0 ? lastPoint[side] : eye + Effects.FlatForward(View) * 0.55f + Vector3.down * 0.15f;
            w.Position = from + Vector3.up * 0.03f;
            w.Velocity = Vector3.up * 0.04f + (from - eye).normalized * 0.03f;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            var color = SongColor(music);
            if (rig != null)
            {
                for (int side = 0; side < 2; side++) Draw(rig.Get(side == 0), side, color, music, view, dt);
                ClearWithFists(rig, dt);
            }

            foreach (var s in strokes)
            {
                if (s.Age < 0f) continue;
                if (s != drawing[0] && s != drawing[1]) s.Age += dt;
                float fade = 1f - Mathf.Clamp01((s.Age - lifetime) / 10f);
                if (fade <= 0f) { Retire(s); continue; }
                block.Clear();
                block.SetFloat(KickId, music.Kick);
                block.SetFloat(ClockId, Now);
                block.SetFloat(AlphaId, fade * clearFade);
                s.Renderer.SetPropertyBlock(block);
            }
            if (clearFade < 1f)
            {
                clearFade = Mathf.MoveTowards(clearFade, 0f, dt);
                if (clearFade <= 0f)
                {
                    foreach (var s in strokes) Retire(s);
                    clearFade = 1f;
                }
            }

            // The sung words float on from where they came off, turned to you.
            if (SungWords == null) return;
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                w.Position += w.Velocity * dt;
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.4f + music.Kick * 0.4f;
            }
        }

        /// <summary>The colour of the moment: it moves round the palette as the song plays, a step every few beats.</summary>
        static Color SongColor(MusicFrame music)
        {
            float t = music.SongTime * 0.12f;
            int a = Mathf.FloorToInt(t) % Palette.Length;
            return Color.Lerp(Palette[(a + Palette.Length) % Palette.Length], Palette[(a + 1) % Palette.Length], t - Mathf.Floor(t));
        }

        /// <summary>While a hand pinches, its stroke grows from the pinch point; a small light marks the nib.</summary>
        void Draw(HandRig.Hand hand, int side, Color color, MusicFrame music, Camera view, float dt)
        {
            // A pinch at the open menu chooses there; it doesn't draw.
            bool pinching = hand.IsTracked && hand.Pinch > 0.8f && !hand.InSystemGesture && !MenuOpen;
            var nib = nibs[side];
            if (nib.gameObject.activeSelf != hand.IsTracked) nib.gameObject.SetActive(hand.IsTracked);
            if (!hand.IsTracked) { drawing[side] = null; return; }

            var thumb = hand.TipJoints[0] != null ? hand.TipJoints[0].position : hand.Tips[0];
            var index = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
            var point = (thumb + index) * 0.5f;
            nib.transform.position = point;
            nib.transform.localScale = Vector3.one * (pinching ? 0.03f : 0.018f) * (1f + music.Kick * 0.3f);
            Effects.Face(nib.transform, view);
            block.Clear();
            block.SetColor(ColorId, color);
            block.SetFloat(IntensityId, pinching ? 1.2f : 0.35f + hand.Pinch * 0.4f);
            nib.SetPropertyBlock(block);

            if (!pinching)
            {
                drawing[side] = null;
                return;
            }
            var stroke = drawing[side];
            if (stroke == null || stroke.Centres.Count >= pointsPerStroke)
            {
                var previous = stroke;
                stroke = Begin();
                drawing[side] = stroke;
                // Continue seamlessly from a full stroke.
                if (previous != null) Append(stroke, previous.Centres[previous.Centres.Count - 1], previous.Halves[previous.Halves.Count - 1], previous.Colors[previous.Colors.Count - 1]);
                lastPoint[side] = point;
            }
            var move = point - lastPoint[side];
            if (stroke.Centres.Count > 0 && move.magnitude < 0.005f) return;
            float speed = dt > 0f ? move.magnitude / dt : 0f;
            // The ribbon lies in the plane of the hand, across the direction of travel.
            var normal = hand.PalmNormal.sqrMagnitude > 0.5f ? hand.PalmNormal : Vector3.up;
            var across = Vector3.Cross(move.sqrMagnitude > 1e-8f ? move.normalized : Vector3.right, normal);
            if (across.sqrMagnitude < 1e-6f) across = Vector3.Cross(Vector3.up, normal);
            float width = Mathf.Lerp(halfWidth.x, halfWidth.y, Mathf.Clamp01(speed / 1.2f));
            Append(stroke, point, across.normalized * width, color);
            lastPoint[side] = point;
            Upload(stroke);
        }

        Stroke Begin()
        {
            Stroke pick = null;
            foreach (var s in strokes) if (s.Age < 0f) { pick = s; break; }
            if (pick == null)
                foreach (var s in strokes)
                    if (s != drawing[0] && s != drawing[1] && (pick == null || s.Age > pick.Age)) pick = s;
            pick.Centres.Clear();
            pick.Halves.Clear();
            pick.Colors.Clear();
            pick.Age = 0f;
            pick.Length = 0f;
            pick.Renderer.gameObject.SetActive(true);
            return pick;
        }

        static void Append(Stroke s, Vector3 centre, Vector3 half, Color color)
        {
            if (s.Centres.Count > 0) s.Length += Vector3.Distance(s.Centres[s.Centres.Count - 1], centre);
            s.Centres.Add(centre);
            s.Halves.Add(half);
            s.Colors.Add(color);
        }

        /// <summary>Two vertices per point, both on the centre line; the shader spreads them by the half-width.</summary>
        void Upload(Stroke s)
        {
            int n = s.Centres.Count;
            if (n < 2) return;
            var vertices = new Vector3[n * 2];
            var normals = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var colors = new Color[n * 2];
            var triangles = new int[(n - 1) * 6];
            var local = s.Renderer.transform;
            float run = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) run += Vector3.Distance(s.Centres[i - 1], s.Centres[i]);
                var c = local.InverseTransformPoint(s.Centres[i]);
                var h = local.InverseTransformVector(s.Halves[i]);
                // Taper the ends so each stroke starts and finishes in a point.
                float taper = Mathf.Clamp01(run / 0.03f) * Mathf.Clamp01((s.Length - run) / 0.03f + (s == drawing[0] || s == drawing[1] ? 1f : 0f));
                vertices[i * 2] = vertices[i * 2 + 1] = c;
                normals[i * 2] = normals[i * 2 + 1] = h * Mathf.Max(taper, 0.05f);
                uvs[i * 2] = new Vector2(run, 0f);
                uvs[i * 2 + 1] = new Vector2(run, 1f);
                colors[i * 2] = colors[i * 2 + 1] = s.Colors[i];
                if (i == n - 1) continue;
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            s.Mesh.Clear();
            s.Mesh.vertices = vertices;
            s.Mesh.normals = normals;
            s.Mesh.uv = uvs;
            s.Mesh.colors = colors;
            s.Mesh.triangles = triangles;
            // The shader widens the ribbon, so leave room around it.
            var bounds = s.Mesh.bounds;
            bounds.Expand(0.1f);
            s.Mesh.bounds = bounds;
        }

        /// <summary>Both hands held as fists for a moment clears the sculpture, with a quick fade.</summary>
        void ClearWithFists(HandRig rig, float dt)
        {
            bool fists = !MenuOpen && rig.Left.IsTracked && rig.Right.IsTracked && rig.Left.Fist > 0.7f && rig.Right.Fist > 0.7f;
            clearHold = fists ? clearHold + dt : 0f;
            if (clearHold > 0.8f && clearFade >= 1f)
            {
                clearFade = 0.999f;
                drawing[0] = drawing[1] = null;
            }
        }

        static void Retire(Stroke s)
        {
            s.Age = -1f;
            s.Renderer.gameObject.SetActive(false);
        }

        /// <summary>For previews and screenshots: draws a stroke through the given points.</summary>
        public void PreviewStroke(IList<Vector3> points, Vector3 normal, Color color)
        {
            Build();
            var s = Begin();
            for (int i = 0; i < points.Count; i++)
            {
                var move = i > 0 ? points[i] - points[i - 1] : points[1] - points[0];
                var across = Vector3.Cross(move.normalized, normal).normalized * Mathf.Lerp(halfWidth.x, halfWidth.y, 0.5f + 0.5f * Mathf.Sin(i * 0.3f));
                Append(s, points[i], across, Color.Lerp(color, Palette[(i / 12) % Palette.Length], 0.5f));
            }
            Upload(s);
        }
    }
}
