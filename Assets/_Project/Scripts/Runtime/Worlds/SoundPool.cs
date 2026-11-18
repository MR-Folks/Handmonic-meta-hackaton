using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Sound pool world: a liquid surface at table height in front of you. The bass rolls slow swells across it,
    /// each kick drops a ring in the middle, and every melody note falls in as a glowing droplet exactly when it is
    /// sung, placed by pitch from left (low) to right (high). The surface is dotted with an even grid of low-poly beads
    /// that ride its waves, swelling and glowing on the crests. The stirred water throws shifting light on the floor
    /// below, and the same light carries on across the whole room, dimming with distance (RoomWaves): each kick, and
    /// each hand that breaks the surface, sends a brighter swell of it out over the walls, floor and ceiling.
    /// Dip your hand in and the liquid parts around your fingers and ripples out as you move, and the music bends
    /// around it: the deeper your hand, the more muffled the song sounds, as if you were hearing it under water. Your
    /// hand glistens below the waterline. Pinch above the pool and a clear stream pours from your fingers, curving as
    /// your hand moves, thinning as it falls and breaking into beads where it meets the water; let go and its tail
    /// falls as drops.
    /// </summary>
    public class SoundPool : World
    {
        public override string DisplayName => "Sound pool";

        public override Color Color => new Color(0.37f, 0.83f, 1f);

        public override int MenuOrder => 60;

        public override int MenuIcon => 8;

        public override string Hint => "Dip your hand into the pool and listen";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.05f, 0.14f, 0.2f, 0.5f),
            Edge = new Color(0.37f, 0.83f, 1f),
            Glow = new Color(0.75f, 0.95f, 1f),
            Pattern = HandTheme.Effect.Submerged,
            PatternColor = new Color(0.45f, 0.85f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material waterMaterial;
        [SerializeField] Material causticsMaterial;
        [Tooltip("The low-poly beads dotting the surface (Materials/Facet). Needs GPU instancing.")]
        [SerializeField] Material facetMaterial;
        [Tooltip("The stream poured from a pinch (Materials/Stream).")]
        [SerializeField] Material streamMaterial;
        [Tooltip("The water light spreading over the room (Materials/RoomRipples).")]
        [SerializeField] Material roomRipplesMaterial;

        [Header("Room")]
        [Tooltip("Half the width and depth of the stand-in room (m), and its height.")]
        [SerializeField] Vector2 roomHalfSize = new Vector2(1.8f, 1.8f);
        [SerializeField] float roomHeight = 2.6f;

        [Header("Pool")]
        [SerializeField] Vector2 size = new Vector2(0.7f, 0.48f);
        [Tooltip("How far in front of the eyes the pool's centre is (m).")]
        [SerializeField] float distance = 0.5f;
        [Tooltip("How far below the eyes the surface is (m), like a table top.")]
        [SerializeField] float belowEyes = 0.52f;
        [SerializeField] int columns = 72;
        [SerializeField] int rows = 50;
        [Tooltip("How quickly ripples die away (per simulation step).")]
        [SerializeField, Range(0.9f, 1f)] float damping = 0.988f;

        [Header("Feel")]
        [Tooltip("Low-pass cutoff (Hz) with the hand fully dipped.")]
        [SerializeField] float muffledCutoff = 650f;
        [Tooltip("How deep (m) the hand must go for the music to be fully muffled.")]
        [SerializeField] float fullDip = 0.1f;
        [Tooltip("Seconds a melody droplet takes to fall into the pool.")]
        [SerializeField] float dropTime = 0.5f;

        static readonly int KickId = Shader.PropertyToID("_Kick");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int LengthId = Shader.PropertyToID("_Length");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly Color[] DropColors = { Effects.Blue, Effects.Mint, new Color(0.6f, 0.92f, 1f), Effects.Violet };
        static readonly Color BeadDeep = new Color(0.25f, 0.6f, 1f);
        static readonly Color BeadCrest = new Color(0.72f, 0.96f, 1f);
        static readonly Color RippleColor = new Color(0.45f, 0.85f, 1f);

        /// <summary>A drop of poured water. Attached drops are still part of the stream; let go, they fall as drops.</summary>
        struct Drip
        {
            public Vector3 Position, Velocity;
            public Color Color;
            public int Side;
            public bool Attached;
        }

        Transform surface;
        Mesh mesh;
        MeshRenderer surfaceRenderer;
        Vector3[] vertices, normals;
        float[] height, previous, next, sponge;
        ParticleSystem splashes, loose;
        Renderer caustics;
        ParticleSystem.Particle[] looseOut;
        Quaternion[] beadTurn;
        Mesh beadMesh;
        // How tall the ocean swell is this frame (m), from the bass and the kick.
        float swellHeight;
        Matrix4x4[] beadMatrices;
        Vector4[] beadTints;
        MaterialPropertyBlock beadBlock;
        LineStrip[] streams;
        Vector3[] streamPath;
        float[] streamWidths;
        RoomWaves room;
        readonly List<Drip> drips = new List<Drip>();
        readonly float[] streamDebt = new float[2];
        readonly bool[] pouring = new bool[2];
        readonly Vector3[] pourFrom = new Vector3[2];
        readonly Vector3[] lastPourFrom = new Vector3[2];
        // The hand's speed with tracking jitter smoothed out, so neighbouring drops leave it alike.
        readonly Vector3[] pourVelocity = new Vector3[2];
        Vector3[] streamSmooth;
        Vector3 litPoint;
        float stir;
        // The beads over the surface, about two centimetres apart, in lines across the pool.
        const int BeadColumns = 35, BeadRows = 24, BeadCount = BeadColumns * BeadRows;
        const float BeadSize = 0.0045f;
        const int MaxDrips = 260;
        const int StreamPoints = 48;
        MaterialPropertyBlock block;
        AudioLowPassFilter lowPass;
        readonly HashSet<float> spawned = new HashSet<float>();
        readonly List<(float time, Vector2 spot)> pending = new List<(float, Vector2)>();
        readonly bool[,] wasUnder = new bool[2, 6];
        readonly Vector3[,] lastPoint = new Vector3[2, 6];
        readonly float[] lastBands = new float[MusicBus.BandCount];
        float cutoff = 22000f, songTime, onsetCooldown;

        protected override float FadeInSeconds => 1f / 2f;

        protected override int SungWordCount => 6;

        protected override float SungWordHeight => 0.034f;

        protected override float SungWordsAlpha => FadeIn;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            int count = columns * rows;
            height = new float[count];
            previous = new float[count];
            next = new float[count];
            sponge = new float[count];
            vertices = new Vector3[count];
            normals = new Vector3[count];
            var uvs = new Vector2[count];
            var triangles = new int[(columns - 1) * (rows - 1) * 6];
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int i = z * columns + x;
                    float u = x / (float)(columns - 1), v = z / (float)(rows - 1);
                    uvs[i] = new Vector2(u, v);
                    vertices[i] = new Vector3((u - 0.5f) * size.x, 0f, (v - 0.5f) * size.y);
                    normals[i] = Vector3.up;
                    // A soft absorbing border, so ripples fade out at the edges instead of bouncing back hard.
                    int border = Mathf.Min(Mathf.Min(x, columns - 1 - x), Mathf.Min(z, rows - 1 - z));
                    sponge[i] = border >= 5 ? 1f : Mathf.Lerp(0.82f, 1f, border / 5f);
                }
            }
            int t = 0;
            for (int z = 0; z < rows - 1; z++)
            {
                for (int x = 0; x < columns - 1; x++)
                {
                    int i = z * columns + x;
                    triangles[t++] = i; triangles[t++] = i + columns; triangles[t++] = i + 1;
                    triangles[t++] = i + 1; triangles[t++] = i + columns; triangles[t++] = i + columns + 1;
                }
            }
            mesh = new Mesh { name = "Pool surface" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size.x, 0.2f, size.y));

            surface = new GameObject("Pool").transform;
            surface.SetParent(transform, false);
            surface.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            surfaceRenderer = surface.gameObject.AddComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterial = waterMaterial;
            surfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            surfaceRenderer.receiveShadows = false;
            surface.gameObject.SetActive(false);

            splashes = Effects.MakeSparkSystem("Splashes", transform, glowMaterial);
            var main = splashes.main;
            main.gravityModifier = 0.5f;

            caustics = Effects.MakeQuad("Water Light", transform, causticsMaterial);
            caustics.gameObject.SetActive(false);

            // Each bead is turned its own way, so neighbours show different facets.
            var random = new System.Random(4);
            float Next() => (float)random.NextDouble();
            beadTurn = new Quaternion[BeadCount];
            for (int i = 0; i < BeadCount; i++)
                beadTurn[i] = Quaternion.Euler(Next() * 360f, Next() * 360f, Next() * 360f);
            beadMesh = LowPolySphere();
            beadMatrices = new Matrix4x4[BeadCount];
            beadTints = new Vector4[BeadCount];
            beadBlock = new MaterialPropertyBlock();
            // All the beads are one instanced draw, which the material must allow.
            if (facetMaterial != null && !facetMaterial.enableInstancing) facetMaterial.enableInstancing = true;

            looseOut = new ParticleSystem.Particle[MaxDrips];
            loose = MakeManualSystem("Drops", MaxDrips, true);
            if (streamMaterial != null)
            {
                streams = new LineStrip[2];
                for (int s = 0; s < 2; s++)
                {
                    streams[s] = new LineStrip(s == 0 ? "Pour L" : "Pour R", transform, streamMaterial, StreamPoints);
                    streams[s].SetVisible(false);
                }
            }
            streamPath = new Vector3[StreamPoints];
            streamWidths = new float[StreamPoints];
            streamSmooth = new Vector3[StreamPoints];
            if (roomRipplesMaterial != null) room = new RoomWaves(transform, roomRipplesMaterial, roomHalfSize, roomHeight);
        }

        /// <summary>
        /// A low-poly ball: an icosahedron (twenty faces) of diameter 1, each face with its own three vertices and normal
        /// so it shades flat, like a cut bead.
        /// </summary>
        static Mesh LowPolySphere()
        {
            float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1)
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            var vertices = new Vector3[faces.Length];
            var normals = new Vector3[faces.Length];
            var triangles = new int[faces.Length];
            for (int f = 0; f < faces.Length; f += 3)
            {
                var a = corners[faces[f]].normalized * 0.5f;
                var b = corners[faces[f + 1]].normalized * 0.5f;
                var c = corners[faces[f + 2]].normalized * 0.5f;
                var n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, a) < 0f) n = -n;
                vertices[f] = a; vertices[f + 1] = b; vertices[f + 2] = c;
                normals[f] = normals[f + 1] = normals[f + 2] = n;
                triangles[f] = f; triangles[f + 1] = f + 1; triangles[f + 2] = f + 2;
            }
            var mesh = new Mesh { name = "Low-poly bead" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A particle system whose particles this world places itself every frame.</summary>
        ParticleSystem MakeManualSystem(string name, int max, bool stretch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = glowMaterial;
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = 0.025f;
            r.lengthScale = 1.2f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        protected override void OnEnter()
        {
            spawned.Clear();
            pending.Clear();
            System.Array.Clear(wasUnder, 0, wasUnder.Length);
            System.Array.Clear(height, 0, height.Length);
            System.Array.Clear(previous, 0, previous.Length);
        }

        protected override void OnExit()
        {
            if (surface != null) surface.gameObject.SetActive(false);
            if (caustics != null) caustics.gameObject.SetActive(false);
            drips.Clear();
            pouring[0] = pouring[1] = false;
            if (loose != null) loose.Clear();
            if (streams != null) foreach (var s in streams) s.SetVisible(false);
            room?.Hide();
            cutoff = 22000f;
            if (lowPass != null) lowPass.enabled = false;
            if (splashes != null) splashes.Clear();
        }

        /// <summary>The pool sits in front of you at table height, or on your real table once the room is scanned.</summary>
        protected override void OnPlace(Camera view)
        {
            var forward = Effects.FlatForward(view);
            var center = view.transform.position + forward * distance + Vector3.down * belowEyes;
            center.y = Mathf.Max(center.y, Effects.FloorY(view) + 0.45f);
            // With a room scan, the pool fills a shallow tray on the real table in front of you.
            bool onTable = Room.TableAhead(view.transform.position, forward, 1.1f, size.y * 0.5f, out _, out var spot);
            if (onTable) center = spot + Vector3.up * 0.035f;
            surface.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
            surface.gameObject.SetActive(true);
            // The water's light falls on the table under it, or on the floor right below it.
            float lit = onTable ? spot.y + 0.003f : Effects.FloorY(view) + 0.004f;
            litPoint = new Vector3(center.x, lit, center.z);
            caustics.transform.SetPositionAndRotation(litPoint, Quaternion.LookRotation(Vector3.down, forward));
            float spread = onTable ? 1.1f : 1.4f;
            caustics.transform.localScale = new Vector3(size.x * spread, size.y * spread, 1f);
            // On the floor, the room's own floor carries this light on outwards, so the patch would only double it.
            caustics.gameObject.SetActive(onTable || room == null);
            room?.Place(view.transform.position, forward, Effects.FloorY(view));
        }

        /// <summary>Each kick drops a ring in the middle of the pool, and sends its light out across the room.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            Disturb(new Vector2(0.5f, 0.5f), 0.05f, -0.006f - music.Kick * 0.004f);
            room?.Ripple(surface.position, RippleColor, 0.45f + music.Kick * 0.55f);
        }

        /// <summary>
        /// Each sung word lands on the water with a ripple and floats there, written on the surface: rising and
        /// falling with the waves and drifting slowly, the line's words side by side across the pool.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            var w = SungWords.Add(word, new Color(0.75f, 0.95f, 1f), 3.6f);
            w.Spot = new Vector2(0.2f + (Sung.Index % 4) * 0.2f + (w.Seed - 0.5f) * 0.06f, 0.45f + (w.Seed - 0.5f) * 0.25f);
            w.Velocity = new Vector3((w.Seed - 0.5f) * 0.02f, 0f, 0.015f);
            Disturb(w.Spot, 0.04f, -0.008f);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            songTime = music.SongTime;
            HandSkin.FocusPosition = surface.position;
            HandSkin.FocusStrength = FadeIn;

            if (music.Upcoming != null && music.Upcoming.Count > 0) SpawnFromNotes(music);
            else SpawnFromAnalysis(music, dt);
            LandDroplets();

            float dip = TouchWithHands(rig, dt);
            Pour(rig, dt);
            Simulate(dt);
            UpdateMesh(music);
            Muffle(music.Bus, dip, dt);
            DrawBeads(music);
            Sing(music, view, dt);

            stir *= Mathf.Exp(-dt * 1.5f);
            float light = (0.25f + music.Band(1) * 0.35f + Mathf.Min(stir, 1f) * 0.6f) * FadeIn;
            block.Clear();
            block.SetFloat("_Strength", light);
            block.SetFloat("_Clock", Now);
            caustics.SetPropertyBlock(block);
            if (room != null)
            {
                room.SetLight(litPoint, light);
                room.Step(music, dt);
            }

            block.Clear();
            block.SetFloat(KickId, music.Kick);
            block.SetFloat(AlphaId, FadeIn);
            surfaceRenderer.SetPropertyBlock(block);
        }

        /// <summary>Each melody note falls in as a droplet, timed to hit the surface as it is sung.</summary>
        void SpawnFromNotes(MusicFrame music)
        {
            foreach (var note in music.Upcoming)
            {
                float ahead = note.Time - songTime;
                if (ahead > dropTime || ahead < 0f || spawned.Contains(note.Time)) continue;
                spawned.Add(note.Time);
                var spot = new Vector2(0.15f + Mathf.Clamp01((note.Midi - 60f) / 20f) * 0.7f, 0.5f + Mathf.Sin(note.Time * 12.9898f) * 0.28f);
                SpawnDroplet(spot, note.Time, ahead, DropColors[Mathf.Abs(note.Midi) % DropColors.Length]);
            }
            if (spawned.Count > 256) spawned.Clear();
        }

        /// <summary>Without a note track, a sudden rise in a band drops a droplet in.</summary>
        void SpawnFromAnalysis(MusicFrame music, float dt)
        {
            onsetCooldown -= dt;
            for (int b = 2; b < MusicBus.BandCount; b++)
            {
                float level = music.Band(b);
                if (level - lastBands[b] > 0.16f && level > 0.45f && onsetCooldown <= 0f)
                {
                    var spot = new Vector2(Random.Range(0.15f, 0.85f), Random.Range(0.25f, 0.75f));
                    SpawnDroplet(spot, songTime + dropTime, dropTime, DropColors[b % DropColors.Length]);
                    onsetCooldown = 0.15f;
                }
                lastBands[b] = level;
            }
        }

        void SpawnDroplet(Vector2 spot, float landTime, float ahead, Color color)
        {
            var target = SurfacePoint(spot);
            const float fall = 0.28f;
            float lifetime = Mathf.Max(0.05f, ahead);
            splashes.Emit(new ParticleSystem.EmitParams
            {
                position = target + Vector3.up * fall,
                // Under the system's gravity, fall exactly onto the spot in the time left.
                velocity = Vector3.up * (-fall / lifetime + 0.5f * Physics.gravity.magnitude * 0.5f * lifetime),
                startColor = color,
                startSize = 0.016f,
                startLifetime = lifetime
            }, 1);
            pending.Add((landTime, spot));
        }

        void LandDroplets()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var (time, spot) = pending[i];
                // Song time jumps back when the song loops; drop anything left over from before.
                if (time - songTime > dropTime + 0.5f) { pending.RemoveAt(i); continue; }
                if (songTime < time) continue;
                Disturb(spot, 0.03f, -0.008f);
                Splash(SurfacePoint(spot), DropColors[i % DropColors.Length], 10, 0.35f);
                pending.RemoveAt(i);
            }
        }

        /// <summary>
        /// Fingertips and palms under the surface push the liquid aside; moving them makes waves, and breaking the
        /// surface splashes. Returns how deep (0-1) the deepest hand is, for muffling the music.
        /// </summary>
        float TouchWithHands(HandRig rig, float dt)
        {
            float dip = 0f;
            if (rig == null) return dip;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                for (int k = 0; k < 6; k++)
                {
                    // While the menu is open the hands are choosing there: they part no water and muffle nothing.
                    if (!hand.IsTracked || MenuOpen)
                    {
                        wasUnder[side, k] = false;
                        continue;
                    }
                    var point = k < 5
                        ? hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k]
                        : hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                    var local = surface.InverseTransformPoint(point);
                    var spot = new Vector2(local.x / size.x + 0.5f, local.z / size.y + 0.5f);
                    bool inside = spot.x > 0f && spot.x < 1f && spot.y > 0f && spot.y < 1f;
                    float depth = -local.y;
                    // Once under, a finger stays under until it is a centimetre clear of the surface, so one resting on
                    // the water doesn't break it again every time tracking wavers.
                    bool under = inside && depth < 0.25f && depth > (wasUnder[side, k] ? -0.01f : 0f);
                    if (under)
                    {
                        Acted("dip");
                        dip = Mathf.Max(dip, Mathf.Clamp01(depth / fullDip));
                        float radius = k < 5 ? 0.012f : 0.035f;
                        float speed = dt > 0f ? (point - lastPoint[side, k]).magnitude / dt : 0f;
                        // A dent where the hand is, deeper the faster it moves, which trails a wake behind it.
                        Disturb(spot, radius, -Mathf.Min(0.004f, depth * 0.05f + speed * 0.004f) * dt * 30f);
                        if (!wasUnder[side, k])
                        {
                            // The finger (or the whole hand, for the palm) lights up as it breaks the surface.
                            Feedback(GestureKind.Touch, side == 0, point, k < 5 ? 0.6f : 1f, k == 5,
                                k < 5 ? GestureFeedback.Of((HandRig.Finger)k) : Fingers.All);
                            Disturb(spot, radius * 1.5f, -0.006f);
                            Splash(SurfacePoint(spot), Effects.Blue, k < 5 ? 4 : 10, 0.25f);
                            // A palm breaking the surface sends its own ring of light out over the room.
                            if (k == 5) room?.Ripple(SurfacePoint(spot), Effects.Blue, 0.6f);
                        }
                    }
                    else if (wasUnder[side, k] && inside)
                    {
                        // Pulling out lifts a little liquid with the hand.
                        Disturb(spot, k < 5 ? 0.015f : 0.04f, 0.004f);
                    }
                    wasUnder[side, k] = under;
                    lastPoint[side, k] = point;
                }
            }
            return dip;
        }

        /// <summary>
        /// Pinching pours water from between thumb and index. Each frame it lets go of a few drops that fall under
        /// gravity; while you keep pinching they stay joined as one stream, drawn as a smooth curve through them from
        /// your fingers down, so it bends as your hand moves. It ripples the water where it lands (or vanishes on the
        /// floor beside it). Let go and the stream's tail falls on as separate drops.
        /// </summary>
        void Pour(HandRig rig, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig?.Get(side == 0);
                // A pinch at the open menu pours nothing, nor does one still held from it once it closes.
                // Once pouring, it pours on until the pinch opens past 0.7, so a wavering pinch doesn't restart it.
                bool now = hand != null && hand.IsTracked && hand.Pinch > (pouring[side] ? 0.7f : 0.8f) && !hand.InSystemGesture
                    && PinchFree(side == 0);
                if (!now)
                {
                    if (pouring[side])
                        for (int i = 0; i < drips.Count; i++)
                            if (drips[i].Side == side && drips[i].Attached) { var d = drips[i]; d.Attached = false; drips[i] = d; }
                    pouring[side] = false;
                    streamDebt[side] = 0f;
                    continue;
                }
                var thumb = hand.TipJoints[0] != null ? hand.TipJoints[0].position : hand.Tips[0];
                var index = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
                pourFrom[side] = (thumb + index) * 0.5f;
                if (!pouring[side])
                {
                    lastPourFrom[side] = pourFrom[side];
                    pourVelocity[side] = hand.Velocity;
                    // The pinching fingers light up with a tick as the water starts to pour.
                    Feedback(GestureKind.Pinch, side == 0, pourFrom[side]);
                }
                pouring[side] = true;
                Acted("pour water");
                pourVelocity[side] = Vector3.Lerp(pourVelocity[side], hand.Velocity, 1f - Mathf.Exp(-6f * dt));
                streamDebt[side] += 80f * dt;
                int count = (int)streamDebt[side];
                // Spread this frame's drops along the way the fingers moved since the last one, and start each a
                // little further on in its fall, so they leave one after another instead of in a clump.
                for (int k = 0; k < count && drips.Count < MaxDrips; k++)
                {
                    float back = (count - k) / (float)count;
                    var velocity = pourVelocity[side] * 0.3f + Vector3.down * 0.15f;
                    float early = back * dt;
                    drips.Add(new Drip
                    {
                        Position = Vector3.Lerp(pourFrom[side], lastPourFrom[side], back) + velocity * early + Physics.gravity * (0.5f * early * early),
                        Velocity = velocity + Physics.gravity * early,
                        Color = Color.Lerp(Effects.Blue, Color.white, Random.Range(0.3f, 0.6f)),
                        Side = side,
                        Attached = true
                    });
                }
                streamDebt[side] -= count;
                lastPourFrom[side] = pourFrom[side];
            }

            float floor = surface.position.y - 2f;
            int n = 0;
            for (int i = drips.Count - 1; i >= 0; i--)
            {
                var d = drips[i];
                d.Velocity += Physics.gravity * dt;
                d.Position += d.Velocity * dt;
                var local = surface.InverseTransformPoint(d.Position);
                var spot = new Vector2(local.x / size.x + 0.5f, local.z / size.y + 0.5f);
                bool over = spot.x > 0f && spot.x < 1f && spot.y > 0f && spot.y < 1f;
                if (over && local.y <= HeightAt(spot))
                {
                    // The stream digs a dent where it lands and keeps throwing water up out of it; a loose drop splashes less.
                    Disturb(spot, d.Attached ? 0.014f : 0.01f, d.Attached ? -0.003f : -0.0025f);
                    if (Random.value < (d.Attached ? 0.4f : 0.35f)) Splash(d.Position, d.Color, d.Attached ? 5 : 3, d.Attached ? 0.75f : 0.4f);
                    drips.RemoveAt(i);
                    continue;
                }
                if (d.Position.y < floor || local.y < -0.6f) { drips.RemoveAt(i); continue; }
                drips[i] = d;
                // Drops still in the stream are drawn as part of it; loose ones as drops of their own.
                if (!d.Attached && n < MaxDrips)
                    looseOut[n++] = new ParticleSystem.Particle
                    {
                        position = d.Position, velocity = d.Velocity, startColor = d.Color, startSize = 0.006f,
                        startLifetime = 5f, remainingLifetime = 5f
                    };
            }
            loose.SetParticles(looseOut, n);
            for (int side = 0; side < 2; side++) DrawStream(side);
        }

        /// <summary>
        /// The stream from one hand: a curve from the fingers down through its drops, newest first. It narrows as the
        /// water speeds up, as a real stream does.
        /// </summary>
        void DrawStream(int side)
        {
            if (streams == null) return;
            var line = streams[side];
            int count = 0;
            if (pouring[side]) streamPath[count++] = pourFrom[side];
            for (int i = drips.Count - 1; i >= 0 && count < StreamPoints; i--)
            {
                var d = drips[i];
                if (d.Side != side || !d.Attached) continue;
                streamPath[count] = d.Position;
                streamWidths[count] = Mathf.Clamp(Mathf.Sqrt(0.6f / Mathf.Max(d.Velocity.magnitude, 0.6f)), 0.35f, 1f);
                count++;
            }
            bool show = count >= 3 && View != null;
            line.SetVisible(show);
            if (!show) return;
            streamWidths[0] = 1f;
            // Round off any small kinks left between drops: each point moves a little towards its neighbours' middle,
            // twice; the ends stay where they are.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int k = 1; k < count - 1; k++)
                    streamSmooth[k] = streamPath[k] * 0.5f + (streamPath[k - 1] + streamPath[k + 1]) * 0.25f;
                for (int k = 1; k < count - 1; k++) streamPath[k] = streamSmooth[k];
            }
            float length = 0f;
            for (int k = 1; k < count; k++) length += Vector3.Distance(streamPath[k], streamPath[k - 1]);
            block.Clear();
            block.SetFloat(LengthId, length);
            block.SetFloat(ClockId, Now);
            line.Renderer.SetPropertyBlock(block);
            line.Set(streamPath, null, streamWidths, count, 0.011f, new Color(1f, 1f, 1f, FadeIn), View.transform.position);
        }

        /// <summary>The sung words drift slowly across the water, rising and falling with its waves.</summary>
        void Sing(MusicFrame music, Camera view, float dt)
        {
            if (SungWords == null) return;
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                w.Spot = new Vector2(Mathf.Clamp(w.Spot.x + w.Velocity.x * dt, 0.1f, 0.9f), Mathf.Clamp(w.Spot.y + w.Velocity.z * dt, 0.1f, 0.9f));
                w.Position = SurfacePoint(w.Spot) + surface.up * (Mathf.Max(HeightAt(w.Spot), 0f) + 0.012f);
                w.Rotation = WordLayer.Lying(w.Position, eye);
                w.Halo = 0.2f + music.Kick * 0.25f;
            }
        }

        float HeightAt(Vector2 spot) => vertices[Nearest(spot)].y;

        /// <summary>The grid vertex nearest a spot (0-1 across the pool).</summary>
        int Nearest(Vector2 spot)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(spot.x * (columns - 1)), 0, columns - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(spot.y * (rows - 1)), 0, rows - 1);
            return z * columns + x;
        }

        // Two ocean waves crossing the pool: their direction in the pool's plane, wavelength (m) and share of the height.
        static readonly (Vector2 direction, float length, float share)[] Swells =
        {
            (new Vector2(0.8f, 0.6f), 0.26f, 1f),
            (new Vector2(-0.4f, 0.92f), 0.17f, 0.6f)
        };

        /// <summary>
        /// The ocean swell at a point on the pool (metres across it), <paramref name="amplitude"/> tall: its height, and in
        /// <paramref name="shift"/> how far the water there is carried sideways. These are Gerstner waves: the water moves
        /// in circles as a wave passes, gathering under the crests, so crests come up sharp and troughs stay wide.
        /// </summary>
        static float Swell(Vector2 p, float t, float amplitude, out Vector2 shift)
        {
            shift = Vector2.zero;
            float h = 0f;
            foreach (var (direction, length, share) in Swells)
            {
                float k = Mathf.PI * 2f / length;
                // Deep-water speed, slowed down to suit a pool this size.
                float omega = Mathf.Sqrt(9.81f * k) * 0.45f;
                float phase = k * Vector2.Dot(direction, p) - omega * t;
                float a = amplitude * share / 1.6f;
                shift += direction * (a * 0.9f * Mathf.Cos(phase));
                h += a * Mathf.Sin(phase);
            }
            return h;
        }

        /// <summary>
        /// The beads dotting the surface: low-poly balls in lines across the water, about two centimetres apart, riding
        /// the ocean swell (see Swell): each rises and falls with it and is carried a little back and forth as the crests
        /// pass, so the lines bunch on the crests and spread in the troughs. They bob on the ripples from hands and drops
        /// too. On a crest a bead swells and glows; towards the edges they shrink away, so the pool has no hard border.
        /// They turn slowly so their facets catch the light. All of them are one instanced draw.
        /// </summary>
        void DrawBeads(MusicFrame music)
        {
            if (facetMaterial == null) return;
            float t = Now;
            var spin = Quaternion.Euler(t * 14f, t * 9f, 0f);
            for (int z = 0; z < BeadRows; z++)
            {
                for (int x = 0; x < BeadColumns; x++)
                {
                    int i = z * BeadColumns + x;
                    var spot = new Vector2((x + 0.5f) / BeadColumns, (z + 0.5f) / BeadRows);
                    var rest = new Vector2((spot.x - 0.5f) * size.x, (spot.y - 0.5f) * size.y);
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(Mathf.Min(spot.x, 1f - spot.x), Mathf.Min(spot.y, 1f - spot.y)) * 9f);
                    float swell = Swell(rest, t, swellHeight, out var shift) * edge;
                    shift *= edge;
                    // The water's own ripples, from hands and drops.
                    float ripple = height[Nearest(spot)];
                    // Up on a crest is bright, down in a trough is deep.
                    float crest = Mathf.Clamp01(0.5f + swell / Mathf.Max(swellHeight, 1e-4f) * 0.6f + ripple * 25f);
                    var local = new Vector3(rest.x + shift.x, swell + ripple + 0.002f, rest.y + shift.y);
                    float diameter = BeadSize * (0.8f + crest * 0.5f) * edge * FadeIn;
                    beadMatrices[i] = Matrix4x4.TRS(surface.TransformPoint(local), spin * beadTurn[i], Vector3.one * diameter);
                    var c = Color.Lerp(BeadDeep, BeadCrest, crest);
                    beadTints[i] = new Vector4(c.r, c.g, c.b, crest * crest * 0.9f + music.Kick * 0.25f);
                }
            }
            beadBlock.SetVectorArray(TintId, beadTints);
            var draw = new RenderParams(facetMaterial)
            {
                matProps = beadBlock,
                layer = gameObject.layer,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(surface.position, new Vector3(size.x + 0.1f, 0.2f, size.x + 0.1f))
            };
            Graphics.RenderMeshInstanced(draw, beadMesh, 0, beadMatrices, BeadCount);
        }

        /// <summary>Pushes the surface down (negative) or up around a spot (0-1 across the pool).</summary>
        void Disturb(Vector2 spot, float radius, float amount)
        {
            stir += Mathf.Abs(amount) * 25f;
            float cx = spot.x * (columns - 1), cz = spot.y * (rows - 1);
            float rx = radius / size.x * (columns - 1), rz = radius / size.y * (rows - 1);
            int x0 = Mathf.Max(1, Mathf.FloorToInt(cx - rx * 2)), x1 = Mathf.Min(columns - 2, Mathf.CeilToInt(cx + rx * 2));
            int z0 = Mathf.Max(1, Mathf.FloorToInt(cz - rz * 2)), z1 = Mathf.Min(rows - 2, Mathf.CeilToInt(cz + rz * 2));
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x - cx) / Mathf.Max(rx, 0.5f), dz = (z - cz) / Mathf.Max(rz, 0.5f);
                    float w = Mathf.Exp(-(dx * dx + dz * dz) * 1.5f);
                    height[z * columns + x] += amount * w;
                }
            }
        }

        /// <summary>Two steps of the wave equation on the height grid per frame.</summary>
        void Simulate(float dt)
        {
            int steps = dt > 0.02f ? 3 : 2;
            const float c2 = 0.3f;
            for (int s = 0; s < steps; s++)
            {
                for (int z = 1; z < rows - 1; z++)
                {
                    for (int x = 1; x < columns - 1; x++)
                    {
                        int i = z * columns + x;
                        float laplacian = height[i - 1] + height[i + 1] + height[i - columns] + height[i + columns] - 4f * height[i];
                        next[i] = (2f * height[i] - previous[i] + c2 * laplacian) * damping * sponge[i];
                    }
                }
                var swap = previous;
                previous = height;
                height = next;
                next = swap;
            }
        }

        /// <summary>
        /// Copies the heights into the mesh, plus the ocean swell rolling across it all the time (see Swell), taller
        /// with the bass and the kick, and rebuilds the normals.
        /// </summary>
        void UpdateMesh(MusicFrame music)
        {
            float t = Now;
            swellHeight = 0.007f + music.Band(1) * 0.009f + music.Kick * 0.004f;
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int i = z * columns + x;
                    float u = x / (float)(columns - 1), v = z / (float)(rows - 1);
                    float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 8f);
                    // The sheet takes the swell's height; only the beads on it are carried sideways.
                    float swell = Swell(new Vector2(vertices[i].x, vertices[i].z), t, swellHeight, out _);
                    vertices[i].y = height[i] + swell * edge;
                }
            }
            float sx = size.x / (columns - 1), sz = size.y / (rows - 1);
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int i = z * columns + x;
                    float hl = vertices[z * columns + Mathf.Max(x - 1, 0)].y, hr = vertices[z * columns + Mathf.Min(x + 1, columns - 1)].y;
                    float hd = vertices[Mathf.Max(z - 1, 0) * columns + x].y, hu = vertices[Mathf.Min(z + 1, rows - 1) * columns + x].y;
                    normals[i] = new Vector3((hl - hr) / (2f * sx), 1f, (hd - hu) / (2f * sz)).normalized;
                }
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
        }

        /// <summary>Muffles the song with a low-pass filter while a hand is in the liquid, like hearing it under water.</summary>
        void Muffle(MusicBus bus, float dip, float dt)
        {
            if (bus == null) return;
            if (lowPass == null)
            {
                lowPass = bus.GetComponent<AudioLowPassFilter>();
                if (lowPass == null) lowPass = bus.gameObject.AddComponent<AudioLowPassFilter>();
            }
            float target = dip > 0f ? Mathf.Lerp(4000f, muffledCutoff, dip) : 22000f;
            // Glide in log space, so the change sounds even.
            cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(cutoff), Mathf.Log(target), 1f - Mathf.Exp(-(dip > 0f ? 10f : 4f) * dt)));
            lowPass.enabled = cutoff < 20000f;
            lowPass.cutoffFrequency = cutoff;
            lowPass.lowpassResonanceQ = 1.2f;
        }

        Vector3 SurfacePoint(Vector2 spot) => surface.TransformPoint(new Vector3((spot.x - 0.5f) * size.x, 0f, (spot.y - 0.5f) * size.y));

        void Splash(Vector3 at, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f + Random.value;
                var v = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * speed * Random.Range(0.2f, 0.5f) + Vector3.up * speed * Random.Range(0.6f, 1.1f);
                splashes.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Vector3.up * 0.003f,
                    velocity = v,
                    startColor = Color.Lerp(color, Color.white, 0.3f),
                    startSize = Random.Range(0.004f, 0.009f),
                    startLifetime = Random.Range(0.35f, 0.6f)
                }, 1);
            }
        }
    }
}
