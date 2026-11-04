using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Graffiti world: the lyrics get sprayed onto the room wherever your hand points. Each sung word lands as a
    /// tag on the wall (or the floor, if you point down), sprayed on from left to right with a speckled overspray.
    /// Keep pointing at the same spot and new words pile up over the old ones, as on a real wall;
    /// on walls the paint drips for a moment, then the tag fades away. Pinch to spray paint yourself: a mist streams
    /// from your fingers with a hiss and leaves glowing strokes where you point. Your fingertip gets speckled with
    /// the paint colour. Tags land on your real walls once the room is scanned; until then the walls are an invisible
    /// ring around you.
    /// </summary>
    public class Graffiti : World
    {
        public override string DisplayName => "Graffiti";

        public override Color Color => new Color(1f, 0.7f, 0.42f);

        public override int MenuOrder => 130;

        public override int MenuIcon => 13;

        public override bool IsLyric => true;

        public override string Hint => "Point at a wall and pinch to spray";

        /// <summary>The words are sprayed onto the walls, and a pinch sprays paint.</summary>
        public override Space Spaces => Space.Walls | Space.Pinch;

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.1f, 0.09f, 0.12f, 0.55f),
            Edge = new Color(1f, 0.7f, 0.42f),
            Glow = new Color(1f, 0.37f, 0.68f),
            Pattern = HandTheme.Effect.Paint,
            PatternColor = Effects.Orange
        };

        [Header("Materials")]
        [SerializeField] Material lineMaterial;
        [SerializeField] Material ringMaterial;
        [WorldMaterial("PaintStroke")]
        [SerializeField] Material paintMaterial;

        [Header("Walls")]
        [Tooltip("Radius of the stand-in walls around you until the room is scanned (m).")]
        [SerializeField] float wallRadius = 1.6f;
        [Tooltip("Letter height of a tag on a wall at the stand-in distance (m).")]
        [SerializeField] float tagHeight = 0.2f;
        [SerializeField] float holdTime = 4f;
        [SerializeField] float fadeTime = 1.5f;
        [SerializeField] int maxTags = 10;
        [SerializeField] int maxStrokes = 8;

        static readonly Color[] Paints = { Effects.Pink, Effects.Orange, Effects.Gold, Effects.Mint, Effects.Blue, Effects.Violet };
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int ExtentId = Shader.PropertyToID("_Extent");
        static readonly int RimId = Shader.PropertyToID("_Rim");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int SprayId = Shader.PropertyToID("_Spray");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        const int StrokePoints = 160;

        sealed class Tag
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public TypeFace Face;
            public LineStrip[] Drips;
            public float[] DripX;
            public float[] DripLength;
            public float Width, Height;
            public float Age = -1f;
            public Color Color;
            public bool OnWall;
        }

        sealed class Stroke
        {
            public LineStrip Line;
            public readonly List<Vector3> Points = new List<Vector3>();
            public float Age = -1f;
            public Color Color;
        }

        readonly List<Tag> tags = new List<Tag>();
        readonly List<Stroke> strokes = new List<Stroke>();
        Material material;
        MaterialPropertyBlock block;
        Renderer reticle;
        ParticleSystem mist;
        AudioSource hiss;
        Vector3[] path, drip = new Vector3[2];
        Vector3 center;
        float floorY, sprayDebt;
        string lastKey = "";
        Stroke current;
        bool wasPinching;
        Color paint = Effects.Orange;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            material = new Material(letteringMaterial) { name = "Graffiti Lettering", mainTexture = TypeFace.Marker.Atlas };
            material.SetFloat(RimId, 0f);
            path = new Vector3[StrokePoints];

            for (int i = 0; i < maxTags; i++)
            {
                var tag = new Tag { Face = TypeFace.Marker, Drips = new LineStrip[3], DripX = new float[3], DripLength = new float[3] };
                tag.Renderer = Glyphs.MakeLetter($"Tag {i}", transform, material, out tag.Mesh);
                for (int d = 0; d < 3; d++) tag.Drips[d] = new LineStrip($"Drip {i}.{d}", transform, lineMaterial, 2);
                SetVisible(tag, false);
                tags.Add(tag);
            }
            for (int i = 0; i < maxStrokes; i++)
            {
                var stroke = new Stroke { Line = new LineStrip($"Stroke {i}", transform, paintMaterial, StrokePoints) };
                stroke.Line.SetVisible(false);
                strokes.Add(stroke);
            }

            reticle = Effects.MakeQuad("Aim", transform, ringMaterial);
            reticle.gameObject.SetActive(false);
            mist = Effects.MakeSparkSystem("Mist", transform, glowMaterial);
            var main = mist.main;
            main.gravityModifier = 0f;
            main.maxParticles = 1500;

            var hissGo = new GameObject("Hiss");
            hissGo.transform.SetParent(transform, false);
            hiss = hissGo.AddComponent<AudioSource>();
            hiss.clip = Synth.Hiss();
            hiss.loop = true;
            hiss.playOnAwake = false;
            hiss.spatialBlend = 1f;
            hiss.minDistance = 0.3f;
            hiss.volume = 0f;
        }

        static void SetVisible(Tag tag, bool visible)
        {
            tag.Renderer.gameObject.SetActive(visible);
            foreach (var d in tag.Drips) d.SetVisible(visible && tag.OnWall);
        }

        protected override void OnEnter()
        {
            lastKey = "";
        }

        protected override void OnExit()
        {
            foreach (var t in tags) { t.Age = -1f; SetVisible(t, false); }
            foreach (var s in strokes) { s.Age = -1f; s.Line.SetVisible(false); }
            current = null;
            wasPinching = false;
            if (reticle != null) reticle.gameObject.SetActive(false);
            if (mist != null) mist.Clear();
            if (hiss != null) hiss.Stop();
        }

        /// <summary>Finds the floor, and until the room is scanned rings the stand-in walls around where you stand.</summary>
        protected override void OnPlace(Camera view)
        {
            center = view.transform.position;
            floorY = Effects.FloorY(view);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            // Aim with the pinching hand, else the right, else the left, else the head.
            HandRig.Hand aimHand = null;
            if (rig != null)
            {
                if (rig.Right.IsTracked && rig.Right.Pinch > 0.8f) aimHand = rig.Right;
                else if (rig.Left.IsTracked && rig.Left.Pinch > 0.8f) aimHand = rig.Left;
                else if (rig.Right.IsTracked) aimHand = rig.Right;
                else if (rig.Left.IsTracked) aimHand = rig.Left;
            }
            Vector3 origin, nozzle;
            if (aimHand != null)
            {
                var thumb = aimHand.TipJoints[0] != null ? aimHand.TipJoints[0].position : aimHand.Tips[0];
                var index = aimHand.TipJoints[1] != null ? aimHand.TipJoints[1].position : aimHand.Tips[1];
                nozzle = (thumb + index) * 0.5f;
                var side = Vector3.Cross(Vector3.up, Effects.FlatForward(view)).normalized;
                origin = view.transform.position + Vector3.down * 0.17f + side * (aimHand == rig.Left ? -0.17f : 0.17f);
            }
            else
            {
                origin = view.transform.position;
                nozzle = origin + view.transform.forward * 0.4f;
            }
            var ray = new Ray(origin, (nozzle - origin).normalized);
            var hit = Hit(ray, out var normal, out bool onWall);

            int line = music.Lyric.Line, wordIndex = music.Lyric.Word;
            paint = Paints[Mathf.Abs(line * 3 + wordIndex + 1) % Paints.Length];
            var theme = Theme;
            theme.PatternColor = paint;
            HandSkin.Theme = theme;
            HandSkin.FocusPosition = aimHand != null ? (aimHand.TipJoints[1] != null ? aimHand.TipJoints[1].position : aimHand.Tips[1]) : nozzle;

            string key = $"{line}:{wordIndex}";
            string word = music.Word;
            if (key != lastKey)
            {
                lastKey = key;
                if (!string.IsNullOrEmpty(word)) SprayTag(word, line, hit, normal, onWall, view);
            }

            bool pinching = aimHand != null && aimHand.Pinch > 0.8f;
            HandSkin.FocusStrength = pinching ? 1f : 0.35f;
            Spray(pinching, nozzle, hit, normal, dt);
            DrawReticle(aimHand != null, hit, normal, pinching, music);
            UpdateTags(view, dt);
            UpdateStrokes(view, dt);
        }

        /// <summary>Where a ray meets the floor or a wall: the scanned walls, or until the room is scanned, stand-in walls
        /// (a ring around where you started).</summary>
        Vector3 Hit(Ray ray, out Vector3 normal, out bool onWall)
        {
            float best = float.MaxValue;
            normal = -ray.direction;
            onWall = true;
            if (ray.direction.y < -0.05f)
            {
                float t = (floorY + 0.002f - ray.origin.y) / ray.direction.y;
                if (t > 0f) { best = t; normal = Vector3.up; onWall = false; }
            }
            // The real walls once the room is scanned.
            if (Room.Scanned)
            {
                if (Room.RaycastWalls(ray, Mathf.Min(best, 8f), out float t, out var wall))
                {
                    best = t;
                    normal = wall.Normal;
                    onWall = true;
                }
                if (best == float.MaxValue) best = wallRadius;
                return ray.GetPoint(best) + normal * 0.004f;
            }
            // Else a ray against a vertical cylinder around the start point.
            var o = new Vector2(ray.origin.x - center.x, ray.origin.z - center.z);
            var d = new Vector2(ray.direction.x, ray.direction.z);
            float a = Vector2.Dot(d, d), b = 2f * Vector2.Dot(o, d), c = Vector2.Dot(o, o) - wallRadius * wallRadius;
            float disc = b * b - 4f * a * c;
            if (a > 1e-6f && disc >= 0f)
            {
                float t = (-b + Mathf.Sqrt(disc)) / (2f * a);
                if (t > 0f && t < best)
                {
                    best = t;
                    var p = ray.GetPoint(t);
                    normal = new Vector3(center.x - p.x, 0f, center.z - p.z).normalized;
                    onWall = true;
                }
            }
            if (best == float.MaxValue) best = wallRadius;
            return ray.GetPoint(best) + normal * 0.004f;
        }

        void SprayTag(string word, int line, Vector3 aim, Vector3 normal, bool onWall, Camera view)
        {
            Tag tag = null;
            float oldest = -1f;
            foreach (var t in tags)
            {
                if (t.Age < 0f) { tag = t; break; }
                if (t.Age > oldest) { oldest = t.Age; tag = t; }
            }
            float distance = Vector3.Distance(view.transform.position, aim);
            float height = tagHeight * Mathf.Clamp(distance / wallRadius, 0.6f, 1.6f) * Random.Range(0.85f, 1.2f);
            // Words the marker hand has no letters for are written in the plain face.
            tag.Face = TypeFace.Marker.Covers(word) ? TypeFace.Marker : TypeFace.Bold;
            tag.Width = Glyphs.BuildWord(tag.Mesh, tag.Face, word, height, 1, 0f, Color.white, Color.white, 0f, -height * 0.36f);
            tag.Height = height;
            tag.Age = 0f;
            tag.Color = Color.Lerp(paint, Color.white, 0.1f);
            tag.OnWall = onWall;

            // Face out of the surface; on the floor, read from where you stand.
            Vector3 up = onWall ? Vector3.up : Vector3.ProjectOnPlane(aim - view.transform.position, Vector3.up).normalized;
            if (up.sqrMagnitude < 1e-4f) up = Vector3.forward;
            var facing = Quaternion.LookRotation(-normal, up);
            var right = facing * Vector3.right;
            var down = facing * Vector3.down;

            // Each word lands where you point, right over the last one if you keep still, which is half the fun.
            var at = aim + right * Random.Range(-0.03f, 0.03f) + down * Random.Range(-0.02f, 0.02f);

            var rotation = facing * Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));
            tag.Renderer.transform.SetPositionAndRotation(at + down * Random.Range(-0.012f, 0.012f), rotation);
            for (int d = 0; d < 3; d++)
            {
                tag.DripX[d] = (Random.value - 0.5f) * tag.Width * 0.8f;
                tag.DripLength[d] = Random.Range(0.02f, 0.09f);
            }
            SetVisible(tag, true);
            MistBurst(at, normal, 18);
        }

        /// <summary>While pinching: a hissing mist from the fingers and a stroke of paint where the aim lands.</summary>
        void Spray(bool pinching, Vector3 nozzle, Vector3 hit, Vector3 normal, float dt)
        {
            if (pinching && !wasPinching)
            {
                current = NextStroke();
                current.Color = paint;
            }
            if (!pinching) current = null;
            wasPinching = pinching;

            if (current != null)
            {
                current.Age = 0f;
                if (current.Points.Count == 0 || (current.Points[current.Points.Count - 1] - hit).sqrMagnitude > 0.008f * 0.008f)
                {
                    if (current.Points.Count < StrokePoints) current.Points.Add(hit);
                    else current = null;
                }
                sprayDebt += 110f * dt;
                int count = (int)sprayDebt;
                sprayDebt -= count;
                float travel = Vector3.Distance(nozzle, hit);
                for (int i = 0; i < count; i++)
                {
                    float life = Random.Range(0.18f, 0.3f);
                    var target = hit + Random.insideUnitSphere * 0.03f;
                    mist.Emit(new ParticleSystem.EmitParams
                    {
                        position = nozzle,
                        velocity = (target - nozzle) / life + Random.insideUnitSphere * 0.1f,
                        startColor = new Color(paint.r, paint.g, paint.b, 0.45f),
                        startSize = Random.Range(0.002f, 0.005f) * (1f + travel * 0.3f),
                        startLifetime = life
                    }, 1);
                }
            }

            if (hiss != null && Application.isPlaying)
            {
                hiss.transform.position = nozzle;
                hiss.volume = Mathf.MoveTowards(hiss.volume, pinching ? 0.22f : 0f, dt * 3f);
                if (hiss.volume > 0f && !hiss.isPlaying) hiss.Play();
                else if (hiss.volume <= 0f && hiss.isPlaying) hiss.Stop();
            }
        }

        Stroke NextStroke()
        {
            Stroke pick = null;
            float oldest = -1f;
            foreach (var s in strokes)
            {
                if (s.Age < 0f) { pick = s; break; }
                if (s.Age > oldest) { oldest = s.Age; pick = s; }
            }
            pick.Points.Clear();
            pick.Age = 0f;
            return pick;
        }

        void MistBurst(Vector3 at, Vector3 normal, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var v = Vector3.ProjectOnPlane(Random.insideUnitSphere, normal) * 0.12f + normal * 0.03f;
                mist.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Vector3.ProjectOnPlane(Random.insideUnitSphere, normal) * 0.05f,
                    velocity = v,
                    startColor = Color.Lerp(paint, Color.white, 0.25f),
                    startSize = Random.Range(0.004f, 0.01f),
                    startLifetime = Random.Range(0.3f, 0.6f)
                }, 1);
            }
        }

        void DrawReticle(bool show, Vector3 hit, Vector3 normal, bool pinching, MusicFrame music)
        {
            if (reticle.gameObject.activeSelf != show) reticle.gameObject.SetActive(show);
            if (!show) return;
            reticle.transform.SetPositionAndRotation(hit + normal * 0.002f, Quaternion.LookRotation(-normal));
            reticle.transform.localScale = Vector3.one * (pinching ? 0.05f : 0.08f) * (1f + music.Kick * 0.1f);
            block.Clear();
            block.SetColor(ColorId, paint);
            block.SetFloat(RadiusId, 0.8f);
            block.SetFloat(ThicknessId, 0.06f);
            block.SetFloat(AlphaId, pinching ? 0.9f : 0.5f);
            reticle.SetPropertyBlock(block);
        }

        /// <summary>Tags spray on left to right, drip on walls, hold, then fade.</summary>
        void UpdateTags(Camera view, float dt)
        {
            foreach (var tag in tags)
            {
                if (tag.Age < 0f) continue;
                tag.Age += dt;
                float alpha = tag.Age < holdTime ? 1f : 1f - (tag.Age - holdTime) / fadeTime;
                if (alpha <= 0f)
                {
                    tag.Age = -1f;
                    SetVisible(tag, false);
                    continue;
                }
                float reveal = Mathf.Clamp01(tag.Age / 0.3f);
                block.Clear();
                block.SetColor(ColorId, tag.Color);
                block.SetTexture(MainTexId, tag.Face.Atlas);
                block.SetFloat(RevealId, reveal * 1.1f);
                block.SetVector(ExtentId, new Vector4(-tag.Width * 0.5f, tag.Width * 0.5f, 0f, 0f));
                block.SetFloat(SprayId, 1f);
                block.SetFloat("_Outline", 1f);
                block.SetColor("_OutlineColor", new Color(0.97f, 0.96f, 0.93f));
                block.SetFloat(AlphaId, alpha);
                tag.Renderer.SetPropertyBlock(block);

                if (!tag.OnWall) continue;
                var tr = tag.Renderer.transform;
                for (int d = 0; d < 3; d++)
                {
                    float grow = 1f - Mathf.Exp(-Mathf.Max(0f, tag.Age - 0.3f - d * 0.15f) * 1.6f);
                    var top = tr.TransformPoint(new Vector3(tag.DripX[d], -tag.Height * 0.3f, 0f));
                    drip[0] = top;
                    drip[1] = top + Vector3.down * (tag.DripLength[d] * grow + 0.001f);
                    var c = tag.Color;
                    c.a = alpha * 0.85f * (reveal >= 1f ? 1f : 0f);
                    tag.Drips[d].Set(drip, 2, 0.0035f, c, view.transform.position, false);
                }
            }
        }

        void UpdateStrokes(Camera view, float dt)
        {
            foreach (var s in strokes)
            {
                if (s.Age < 0f) continue;
                if (s != current) s.Age += dt;
                float alpha = s.Age < 6f ? 1f : 1f - (s.Age - 6f) / 1.5f;
                bool show = alpha > 0f && s.Points.Count >= 2;
                s.Line.SetVisible(show);
                if (alpha <= 0f) { s.Age = -1f; continue; }
                if (!show) continue;
                for (int i = 0; i < s.Points.Count; i++) path[i] = s.Points[i];
                var c = s.Color;
                c.a = alpha * 0.9f;
                s.Line.Set(path, s.Points.Count, 0.022f, c, view.transform.position, false);
            }
        }
    }
}
