using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Shadow play world: the room goes dark and a lamp low behind you lights the wall in front, and your hands cast
    /// their shadows on it: big and soft when close to you, small and sharp near the wall, as real shadows are. Your head
    /// casts one too, high up. On every beat your shadow leaves a coloured echo that drifts up and grows as it fades. Small notes
    /// of light float in the lamp's pool; cover one with a shadow and it rings, a higher note higher up the wall, so
    /// your shadow plays along with the song. The lamp breathes with the bass, and the sung words appear on the wall.
    /// </summary>
    public class ShadowPlay : World
    {
        public override string DisplayName => "Shadow play";

        public override Color Color => new Color(1f, 0.74f, 0.45f);

        public override int MenuOrder => 113;

        public override int MenuIcon => 30;

        public override string Hint => "Cast shadows on the wall; touch the lights with them";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.05f, 0.04f, 0.04f, 0.7f),
            Edge = new Color(1f, 0.74f, 0.45f),
            Glow = new Color(1f, 0.7f, 0.4f),
            Pattern = HandTheme.Effect.Heat,
            PatternColor = new Color(1f, 0.7f, 0.42f)
        };

        [Header("Materials")]
        [SerializeField] Material veilMaterial;
        [SerializeField] Material shadowMaskMaterial;
        [SerializeField] Material ringMaterial;

        [Header("Lamp")]
        [Tooltip("How dark the rest of the room gets (0 not at all, 1 black).")]
        [SerializeField, Range(0f, 1f)] float darkness = 0.86f;
        [Tooltip("Where the lamp is from your head when the world starts (m): to the right, up, and forward (negative is behind). Low behind you, like a floor lamp, so your hands' shadows land in the middle of the wall and your head's falls high above them.")]
        [SerializeField] Vector3 lampOffset = new Vector3(0.3f, -0.55f, -0.9f);
        [Tooltip("The lamp's radius (m): bigger makes softer shadow edges.")]
        [SerializeField] float lampRadius = 0.025f;
        [SerializeField] Color lampColor = new Color(1f, 0.78f, 0.52f);

        [Header("Music")]
        [Tooltip("Seconds a beat's coloured echo of your shadow lasts.")]
        [SerializeField] float echoLife = 1.8f;
        [SerializeField] int lightNotes = 22;

        static readonly Color[] EchoColors = { Effects.Pink, Effects.Blue, Effects.Mint, Effects.Gold, Effects.Violet, Effects.Orange };
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int DarkId = Shader.PropertyToID("_Dark");
        static readonly int WallOnId = Shader.PropertyToID("_WallOn");
        static readonly int WallCenterId = Shader.PropertyToID("_WallCenter");
        static readonly int WallRightId = Shader.PropertyToID("_WallRight");
        static readonly int WallUpId = Shader.PropertyToID("_WallUp");
        static readonly int WallNormalId = Shader.PropertyToID("_WallNormal");
        static readonly int WallSizeId = Shader.PropertyToID("_WallSize");
        static readonly int PoolId = Shader.PropertyToID("_Pool");
        static readonly int PoolColorId = Shader.PropertyToID("_PoolColor");
        static readonly int MaskId = Shader.PropertyToID("_Mask");
        static readonly int[] LightIds = { Shader.PropertyToID("_Light0"), Shader.PropertyToID("_Light1"), Shader.PropertyToID("_Light2") };

        const int CapsPerShadow = 20;
        const int Echoes = 3;

        /// <summary>A capsule on the wall, in metres along the wall's Right and Up.</summary>
        struct Cap
        {
            public Vector2 A, B;
            public float Radius, Soft;
        }

        sealed class Echo
        {
            public readonly Cap[] Caps = new Cap[CapsPerShadow];
            public int Count;
            public float Age = 99f;
            public Color Color;
        }

        sealed class Note
        {
            public Renderer Light;
            public Vector2 Position, Velocity;
            public float Ring, Rest, Seed;
            public bool Covered;
        }

        sealed class Ripple
        {
            public Renderer Ring;
            public Vector2 Position;
            public float Age = 99f;
            public Color Color;
        }

        readonly Cap[] live = new Cap[CapsPerShadow];
        int liveCount;
        readonly Echo[] echoes = new Echo[Echoes];
        int nextEcho, colorIndex;
        readonly List<Note> notes = new List<Note>();
        readonly Ripple[] ripples = new Ripple[10];
        int nextRipple;
        readonly Vector3[] handAlong = { Vector3.up, Vector3.up };
        readonly Vector3[] tips = new Vector3[5];

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> spots = new List<Vector2>();
        readonly List<Vector4> ends = new List<Vector4>();
        readonly List<Vector4> shapes = new List<Vector4>();
        readonly List<int> triangles = new List<int>();

        Room.Surface wall;
        Vector3 lamp;
        Vector2 pool;
        float poolRadius;
        Renderer veil, lampGlow;
        Material veilInstance;
        RenderTexture mask;
        Mesh capsules;
        UnityEngine.Rendering.CommandBuffer commands;
        MaterialPropertyBlock block;
        AudioPool voices;

        protected override int SungWordCount => 5;

        protected override float SungWordHeight => 0.045f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            for (int i = 0; i < Echoes; i++) echoes[i] = new Echo();
            for (int i = 0; i < ripples.Length; i++)
            {
                ripples[i] = new Ripple { Ring = Effects.MakeQuad($"Ripple {i}", transform, ringMaterial) };
                ripples[i].Ring.gameObject.SetActive(false);
            }
            for (int i = 0; i < lightNotes; i++)
            {
                var light = Effects.MakeQuad($"Light Note {i}", transform, glowMaterial);
                light.gameObject.SetActive(false);
                notes.Add(new Note { Light = light, Seed = Random.value * 6.28f });
            }
            lampGlow = Effects.MakeQuad("Lamp", transform, glowMaterial);
            if (veilMaterial != null)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Dark Room";
                DebugShapes.RemoveCollider(sphere);
                sphere.transform.SetParent(transform, false);
                veil = sphere.GetComponent<Renderer>();
                veilInstance = new Material(veilMaterial) { name = "Dark Room Veil" };
                veil.sharedMaterial = veilInstance;
                veil.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                veil.receiveShadows = false;
            }
            capsules = new Mesh { name = "Shadow Capsules" };
            capsules.MarkDynamic();
            commands = new UnityEngine.Rendering.CommandBuffer { name = "Shadow Mask" };
            voices = new AudioPool(transform, 8);
        }

        protected override void OnEnter()
        {
            foreach (var e in echoes) e.Age = 99f;
        }

        protected override void OnExit()
        {
            HandSkin.FocusStrength = 0f;
            foreach (var n in notes) n.Light.gameObject.SetActive(false);
            foreach (var r in ripples) { r.Age = 99f; r.Ring.gameObject.SetActive(false); }
        }

        void OnDestroy()
        {
            if (mask != null) { mask.Release(); Discard(mask); }
            if (veilInstance != null) Discard(veilInstance);
            if (capsules != null) Discard(capsules);
            commands?.Release();
        }

        /// <summary>On the beat, the shadow as it is now becomes a coloured echo.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            if (liveCount > 0) Remember();
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Shadows(rig, view);
            foreach (var e in echoes) e.Age += dt;
            DrawMask();

            float strength = 0.82f + 0.2f * music.Band(1) + 0.12f * music.Kick;
            DrawVeil(view, strength);
            PlayNotes(music, strength, dt);
            DrawRipples(dt);
            DrawLamp(view, music);
            HandSkin.FocusPosition = lamp;
            HandSkin.FocusStrength = 0.8f;
            Sing(music, view, dt);
        }

        /// <summary>
        /// Finds the wall in front of you (the scanned one, or a stand-in 2.4 m ahead) and puts the lamp behind you. The
        /// light's pool is centred where your hands, held at chest height in front of you, cast their shadow.
        /// </summary>
        protected override void OnPlace(Camera view)
        {
            var head = view.transform.position;
            var forward = Effects.FlatForward(view);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            float floor = Effects.FloorY(view);
            if (!Room.WallAhead(head, forward, 6f, out wall, out _))
                wall = new Room.Surface(new Vector3(head.x, floor + 1.4f, head.z) + forward * 2.4f, -forward, -right, new Vector2(5f, 2.8f));
            lamp = head + right * lampOffset.x + Vector3.up * lampOffset.y + forward * lampOffset.z;

            poolRadius = Mathf.Min(1.15f, Mathf.Min(wall.Size.x, wall.Size.y) * 0.45f);
            pool = Project(head + forward * 0.45f + Vector3.down * 0.2f, out var local, out _) ? local : wall.Local(wall.Center);
            var room = wall.Size * 0.5f - Vector2.one * poolRadius * 0.6f;
            pool = new Vector2(Mathf.Clamp(pool.x, -Mathf.Max(0f, room.x), Mathf.Max(0f, room.x)), Mathf.Clamp(pool.y, -Mathf.Max(0f, room.y), Mathf.Max(0f, room.y)));

            // Roughly 100 pixels per metre is plenty: the shadows are soft and the texture is filtered.
            int width = 512, height = Mathf.Clamp(Mathf.RoundToInt(512f * wall.Size.y / Mathf.Max(wall.Size.x, 0.1f)), 128, 512);
            if (mask == null || mask.width != width || mask.height != height)
            {
                if (mask != null) { mask.Release(); Discard(mask); }
                mask = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "Shadow Mask", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                mask.Create();
            }

            foreach (var n in notes)
            {
                n.Position = pool + Random.insideUnitCircle * poolRadius * 0.85f;
                n.Velocity = Random.insideUnitCircle.normalized * Random.Range(0.03f, 0.07f);
                n.Ring = 0f;
                n.Covered = false;
            }
            foreach (var e in echoes) e.Age = 99f;
        }

        /// <summary>Where the lamp's light through <paramref name="p"/> lands on the wall, and how much it is magnified.</summary>
        bool Project(Vector3 p, out Vector2 local, out float magnify)
        {
            local = default;
            magnify = 1f;
            float lampHeight = Vector3.Dot(lamp - wall.Center, wall.Normal);
            float pointHeight = Vector3.Dot(p - wall.Center, wall.Normal);
            if (lampHeight < 0.05f || pointHeight < 0.003f || pointHeight > lampHeight - 0.03f) return false;
            magnify = lampHeight / (lampHeight - pointHeight);
            local = wall.Local(lamp + (p - lamp) * magnify);
            return true;
        }

        /// <summary>Adds the shadow of a capsule between two points: magnified, with an edge as soft as the lamp makes it.</summary>
        void AddShadow(Vector3 a, Vector3 b, float radius)
        {
            if (liveCount >= CapsPerShadow) return;
            if (!Project(a, out var la, out float ma) || !Project(b, out var lb, out float mb)) return;
            float magnify = (ma + mb) * 0.5f;
            live[liveCount++] = new Cap { A = la, B = lb, Radius = radius * magnify, Soft = lampRadius * (magnify - 1f) * 0.5f + 0.003f };
        }

        /// <summary>
        /// The shadow of this frame: each tracked hand as a forearm, a palm and five fingers, and your head, all as
        /// capsules seen from the lamp.
        /// </summary>
        void Shadows(HandRig rig, Camera view)
        {
            liveCount = 0;
            var head = view.transform.position;
            var forward = Effects.FlatForward(view);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            AddShadow(head - forward * 0.06f + Vector3.up * 0.04f, head - forward * 0.06f - Vector3.up * 0.04f, 0.085f);
            if (rig == null) return;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                if (!hand.IsTracked) continue;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                for (int f = 0; f < 5; f++) tips[f] = hand.TipJoints[f] != null ? hand.TipJoints[f].position : hand.Tips[f];
                // Which way the hand points: towards the fingers, or as last seen when they curl into a fist.
                var along = (tips[1] + tips[2] + tips[3] + tips[4]) * 0.25f - palm;
                if (along.magnitude > 0.035f) handAlong[side] = along.normalized;
                var up = handAlong[side];
                var across = Vector3.Cross(hand.PalmNormal, up);
                across = across.sqrMagnitude > 1e-4f ? across.normalized : right;

                AddShadow(palm - up * 0.05f, palm - up * 0.28f, 0.03f);
                // The palm, up to the knuckles: two wide capsules.
                AddShadow(palm - up * 0.02f + across * 0.024f, palm - up * 0.02f - across * 0.024f, 0.032f);
                AddShadow(palm + up * 0.03f + across * 0.026f, palm + up * 0.03f - across * 0.026f, 0.028f);
                for (int f = 0; f < 5; f++)
                {
                    var root = Vector3.Lerp(palm, tips[f], f == 0 ? 0.3f : 0.32f);
                    AddShadow(root, tips[f], f == 0 ? 0.011f : f == 4 ? 0.008f : 0.0095f);
                }
            }
        }

        void Remember()
        {
            var e = echoes[nextEcho];
            nextEcho = (nextEcho + 1) % Echoes;
            System.Array.Copy(live, e.Caps, liveCount);
            e.Count = liveCount;
            e.Age = 0f;
            e.Color = EchoColors[colorIndex++ % EchoColors.Length];
        }

        /// <summary>Draws the shadow and the echoes into the wall's mask texture.</summary>
        void DrawMask()
        {
            if (mask == null || shadowMaskMaterial == null) return;
            vertices.Clear();
            spots.Clear();
            ends.Clear();
            shapes.Clear();
            triangles.Clear();
            for (int i = 0; i < liveCount; i++) AddQuad(live[i], 0, 1f);

            // Echoes drift up the wall and grow from the middle of the pool as they fade.
            var upOnWall = new Vector2(Vector3.Dot(Vector3.up, wall.Right), Vector3.Dot(Vector3.up, wall.Up));
            for (int k = 0; k < Echoes; k++)
            {
                var e = echoes[k];
                if (e.Age >= echoLife) continue;
                float t = e.Age / echoLife;
                float grow = 1f + t * 0.45f;
                var lift = upOnWall * (e.Age * 0.22f);
                float strength = Mathf.Pow(1f - t, 1.5f) * Mathf.Clamp01(e.Age / 0.05f);
                for (int i = 0; i < e.Count; i++)
                {
                    var c = e.Caps[i];
                    c.A = pool + (c.A - pool) * grow + lift;
                    c.B = pool + (c.B - pool) * grow + lift;
                    c.Radius *= grow;
                    c.Soft += t * 0.05f;
                    AddQuad(c, 1 + k, strength);
                }
            }

            capsules.Clear();
            capsules.SetVertices(vertices);
            capsules.SetUVs(0, spots);
            capsules.SetUVs(1, ends);
            capsules.SetUVs(2, shapes);
            capsules.SetTriangles(triangles, 0);
            capsules.bounds = new Bounds(Vector3.zero, new Vector3(wall.Size.x, wall.Size.y, 2f) * 2f);

            var half = wall.Size * 0.5f;
            commands.Clear();
            commands.SetRenderTarget(mask);
            commands.ClearRenderTarget(false, true, Color.clear);
            commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(-half.x, half.x, -half.y, half.y, -1f, 1f));
            if (triangles.Count > 0) commands.DrawMesh(capsules, Matrix4x4.identity, shadowMaskMaterial, 0, 0);
            Graphics.ExecuteCommandBuffer(commands);
        }

        void AddQuad(Cap c, int channel, float strength)
        {
            float pad = c.Radius + c.Soft;
            var min = Vector2.Min(c.A, c.B) - Vector2.one * pad;
            var max = Vector2.Max(c.A, c.B) + Vector2.one * pad;
            int v = vertices.Count;
            var ab = new Vector4(c.A.x, c.A.y, c.B.x, c.B.y);
            var shape = new Vector4(c.Radius, c.Soft, channel, strength);
            for (int k = 0; k < 4; k++)
            {
                var corner = new Vector2(k == 1 || k == 2 ? max.x : min.x, k >= 2 ? max.y : min.y);
                vertices.Add(new Vector3(corner.x, corner.y, 0f));
                spots.Add(corner);
                ends.Add(ab);
                shapes.Add(shape);
            }
            triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
            triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
        }

        void DrawVeil(Camera view, float strength)
        {
            if (veil == null) return;
            veil.transform.position = view.transform.position;
            veil.transform.localScale = Vector3.one * 0.5f;
            veilInstance.SetFloat(DarkId, darkness);
            veilInstance.SetFloat(WallOnId, 1f);
            veilInstance.SetVector(WallCenterId, wall.Center);
            veilInstance.SetVector(WallRightId, wall.Right);
            veilInstance.SetVector(WallUpId, wall.Up);
            veilInstance.SetVector(WallNormalId, wall.Normal);
            veilInstance.SetVector(WallSizeId, wall.Size);
            veilInstance.SetVector(PoolId, new Vector4(pool.x, pool.y, poolRadius, strength));
            veilInstance.SetColor(PoolColorId, lampColor);
            veilInstance.SetTexture(MaskId, mask);
            for (int k = 0; k < Echoes; k++) veilInstance.SetColor(LightIds[k], echoes[k].Color);
        }

        /// <summary>How brightly the lamp lights a point on the wall, 0-1 (the same falloff as the shader's).</summary>
        float Lit(Vector2 p, float strength)
        {
            float d = (p - pool).magnitude / Mathf.Max(poolRadius, 1e-3f);
            return strength * Mathf.Exp(-d * d * 1.6f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.25f, 0.8f, d));
        }

        /// <summary>True when the shadow covers this point on the wall.</summary>
        bool InShadow(Vector2 p)
        {
            for (int i = 0; i < liveCount; i++)
            {
                var c = live[i];
                var ab = c.B - c.A;
                float h = Mathf.Clamp01(Vector2.Dot(p - c.A, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
                if ((p - c.A - ab * h).magnitude < c.Radius + c.Soft * 0.3f) return true;
            }
            return false;
        }

        Vector3 OnWall(Vector2 p, float lift) => wall.Center + wall.Right * p.x + wall.Up * p.y + wall.Normal * lift;

        /// <summary>
        /// The lights drift slowly about the pool. When the shadow covers one it rings: a note that fits the song,
        /// higher up the wall for higher notes, a ripple spreading from it, and a moment's rest before it can ring again.
        /// </summary>
        void PlayNotes(MusicFrame music, float strength, float dt)
        {
            var facing = Quaternion.LookRotation(-wall.Normal, Vector3.up);
            var upOnWall = new Vector2(Vector3.Dot(Vector3.up, wall.Right), Vector3.Dot(Vector3.up, wall.Up));
            for (int i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                n.Position += n.Velocity * dt;
                var offset = n.Position - pool;
                if (offset.magnitude > poolRadius * 0.9f && Vector2.Dot(offset, n.Velocity) > 0f)
                    n.Velocity = Vector2.Reflect(n.Velocity, offset.normalized);
                n.Ring = Mathf.Max(0f, n.Ring - dt * 2.5f);
                n.Rest -= dt;

                bool covered = InShadow(n.Position);
                if (covered && !n.Covered && n.Rest <= 0f)
                {
                    float height = Vector2.Dot(n.Position - pool, upOnWall) / poolRadius * 0.5f + 0.5f;
                    var at = OnWall(n.Position, 0.02f);
                    int midi = Musical.Play(voices, music, 60 + Mathf.RoundToInt(Mathf.Clamp01(height) * 19f), at, 0.45f);
                    n.Ring = 1f;
                    n.Rest = 0.35f;
                    Splash(n.Position, NoteColor(midi));
                }
                n.Covered = covered;

                var light = n.Light;
                if (!light.gameObject.activeSelf) light.gameObject.SetActive(true);
                light.transform.SetPositionAndRotation(OnWall(n.Position, 0.012f), facing);
                light.transform.localScale = Vector3.one * (0.05f + n.Ring * 0.06f + music.Kick * 0.01f);
                float lit = Lit(n.Position, strength);
                float twinkle = 0.8f + 0.2f * Mathf.Sin(Now * 3f + n.Seed);
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(lampColor, Color.white, 0.35f + n.Ring * 0.5f));
                block.SetFloat(IntensityId, (0.25f + lit * 1.1f) * twinkle * (covered ? 0.35f : 1f) + n.Ring * 1.6f);
                light.SetPropertyBlock(block);
            }
        }

        static Color NoteColor(int midi) => EchoColors[((midi % 12) + 12) % 12 % EchoColors.Length];

        void Splash(Vector2 at, Color color)
        {
            var r = ripples[nextRipple];
            nextRipple = (nextRipple + 1) % ripples.Length;
            r.Position = at;
            r.Age = 0f;
            r.Color = color;
            r.Ring.gameObject.SetActive(true);
        }

        void DrawRipples(float dt)
        {
            var facing = Quaternion.LookRotation(-wall.Normal, Vector3.up);
            foreach (var r in ripples)
            {
                if (r.Age >= 1f) continue;
                r.Age += dt;
                if (r.Age >= 1f) { r.Ring.gameObject.SetActive(false); continue; }
                r.Ring.transform.SetPositionAndRotation(OnWall(r.Position, 0.015f), facing);
                r.Ring.transform.localScale = Vector3.one * 0.35f;
                block.Clear();
                block.SetColor(ColorId, r.Color);
                block.SetFloat(RadiusId, Mathf.Lerp(0.1f, 0.95f, Mathf.Sqrt(r.Age)));
                block.SetFloat(ThicknessId, 0.04f);
                block.SetFloat(AlphaId, 1f - r.Age);
                r.Ring.SetPropertyBlock(block);
            }
        }

        /// <summary>The lamp itself, behind you, for when you turn round.</summary>
        void DrawLamp(Camera view, MusicFrame music)
        {
            lampGlow.transform.position = lamp;
            lampGlow.transform.localScale = Vector3.one * (0.22f + music.Band(1) * 0.05f);
            Effects.Face(lampGlow.transform, view);
            block.Clear();
            block.SetColor(ColorId, lampColor);
            block.SetFloat(IntensityId, 1.4f);
            lampGlow.SetPropertyBlock(block);
        }

        /// <summary>The sung words appear on the wall, in the light above your shadow.</summary>
        protected override void OnWord(string word, bool newLine)
        {
            var upOnWall = new Vector2(Vector3.Dot(Vector3.up, wall.Right), Vector3.Dot(Vector3.up, wall.Up));
            var w = SungWords.Add(word, new Color(1f, 0.9f, 0.75f), 2.4f, alone: true);
            w.Position = OnWall(pool + upOnWall * (poolRadius * 0.62f), 0.02f);
            w.Velocity = Vector3.up * 0.02f;
        }

        void Sing(MusicFrame music, Camera view, float dt)
        {
            var facing = Quaternion.LookRotation(-wall.Normal, Vector3.up);
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                w.Position += w.Velocity * dt;
                w.Rotation = facing;
                w.Halo = 0.3f + music.Kick * 0.3f;
            }
        }
    }
}
