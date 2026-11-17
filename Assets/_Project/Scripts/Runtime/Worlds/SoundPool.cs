using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Sound pool world: a liquid surface at table height in front of you. The bass rolls slow swells across it,
    /// each kick drops a ring in the middle, and every melody note falls in as a glowing droplet exactly when it is
    /// sung, placed by pitch from left (low) to right (high). The surface is dotted with an even grid of low-poly beads
    /// in calm blues and teals that ride its waves. They glow softly and steadily, and pulse together on each beat: a
    /// gentle brightening that has faded before the next one, with a ring of light running out over them in step with
    /// the kick's ring in the water. The stirred water throws soft, shifting light on the floor below, and the same
    /// light carries on across the whole room, dimming with distance (RoomWaves): every other beat, and each hand
    /// that breaks the surface, sends a gentle swell of it out over the walls, floor and ceiling.
    /// Dip your hand in and the liquid parts around your fingers and ripples out as you move, and the music bends
    /// around it: the deeper your hand, the more muffled the song sounds, as if you were hearing it under water. Your
    /// hand glistens below the waterline. Pinch above the pool and a clear stream pours from your fingers, curving as
    /// your hand moves, thinning as it falls and breaking into beads where it meets the water; let go and its tail
    /// falls as drops.
    /// Cup both hands under the water, palms up, and bring them up through the surface, as if to drink: a handful of
    /// the pool comes up between them, a small body of clear water in the bowl of your palms that follows them,
    /// rippling gently with the music, while the surface below just ripples where it left. How long you keep it depends
    /// on how closed your hands are: held together it only drips slowly from the seam, about a drop a second, and lasts
    /// a quarter of a minute; open the seam and it drips faster, then runs out in a thin stream; open them wide, or tip
    /// or turn your palms, and it pours out at once. It shrinks as it leaks, and the drops fall back into the pool with
    /// small ripples. Lower your hands into the pool and what is left goes back in. One cupped hand brings up a little
    /// that soon runs out between the fingers.
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
        [Tooltip("The handful of water scooped up in your hands (Materials/SoundPoolHandful).")]
        [WorldMaterial("SoundPoolHandful")]
        [SerializeField] Material handfulMaterial;

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

        [Header("Scooping")]
        [Tooltip("The gap (m) along the seam between your hands at or under which they hold their water, dripping slowly.")]
        [SerializeField] float sealGap = 0.02f;
        [Tooltip("The gap (m) past which the water pours out between your hands at once.")]
        [SerializeField] float spillGap = 0.1f;
        [Tooltip("How long (s) a full handful lasts in hands held closed.")]
        [SerializeField] float sealedSeconds = 17.5f;
        [Tooltip("How far (degrees) your palms may tip away from up before water runs over their edges.")]
        [SerializeField] float dripTilt = 35f;
        [Tooltip("How far (degrees) your palms tip before the water pours out at once.")]
        [SerializeField] float spillTilt = 75f;

        static readonly int KickId = Shader.PropertyToID("_Kick");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int LengthId = Shader.PropertyToID("_Length");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int StrengthId = Shader.PropertyToID("_Strength");
        static readonly int SpecId = Shader.PropertyToID("_Spec");
        static readonly int SpecPowerId = Shader.PropertyToID("_SpecPower");
        static readonly int LipBeatId = Shader.PropertyToID("_LipBeat");
        static readonly int CrestGainId = Shader.PropertyToID("_CrestGain");
        static readonly int RippleId = Shader.PropertyToID("_Ripple");
        static readonly int SharpnessId = Shader.PropertyToID("_Sharpness");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int DriftId = Shader.PropertyToID("_Drift");
        // The melody's droplets: pale blues and aquas, a little apart so neighbouring notes differ, all of them water.
        static readonly Color[] DropColors =
        {
            new Color(0.5f, 0.78f, 1f), new Color(0.45f, 0.88f, 0.9f), new Color(0.62f, 0.9f, 1f), new Color(0.42f, 0.7f, 0.98f)
        };
        // The beads' palette: a narrow band of water colours, blue to teal.
        static readonly Color BeadBlue = new Color(0.2f, 0.5f, 0.84f);
        static readonly Color BeadTeal = new Color(0.17f, 0.62f, 0.7f);
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
        // How tall the ocean swell is this frame (m), from the bass.
        float swellHeight;
        Matrix4x4[] beadMatrices;
        Vector4[] beadTints;
        MaterialPropertyBlock beadBlock;
        // This world's own copies of the bead and room light materials, with a calmer look than the shared defaults.
        Material beadLook, roomLook;
        LineStrip[] streams;
        Vector3[] streamPath;
        float[] streamWidths;
        RoomWaves room;
        readonly List<Drip> drips = new List<Drip>(MaxDrips);
        // The streams: one poured from each hand's pinch, and one running out of a handful (ScoopStream).
        const int Streams = 3, ScoopStream = 2;
        readonly float[] streamDebt = new float[Streams];
        readonly bool[] pouring = new bool[Streams];
        readonly Vector3[] pourFrom = new Vector3[Streams];
        readonly Vector3[] lastPourFrom = new Vector3[Streams];
        // The source's speed with tracking jitter smoothed out, so neighbouring drops leave it alike.
        readonly Vector3[] pourVelocity = new Vector3[Streams];
        Vector3[] streamSmooth;
        Vector3 litPoint;
        float stir;
        // The beads over the surface, about two centimetres apart, in lines across the pool.
        const int BeadColumns = 35, BeadRows = 24, BeadCount = BeadColumns * BeadRows;
        const float BeadSize = 0.0045f;
        const int MaxDrips = 260;
        const int StreamPoints = 48;
        // The wave simulation's speed: squared, in grid cells per step.
        const float WaveC2 = 0.3f;
        MaterialPropertyBlock block;
        AudioLowPassFilter lowPass;
        readonly HashSet<float> spawned = new HashSet<float>();
        readonly List<(float time, Vector2 spot)> pending = new List<(float, Vector2)>();
        readonly bool[,] wasUnder = new bool[2, 6];
        readonly Vector3[,] lastPoint = new Vector3[2, 6];
        readonly float[] lastBands = new float[MusicBus.BandCount];
        float cutoff = 22000f, songTime, onsetCooldown;

        // ---- The beat ----
        // Each beat's pulse: a quick, soft rise (PulseAttack) and a smooth fall that is over before the next beat. Pulses
        // come no closer than MinPulseGap, so the light never changes faster than a few times a second.
        const float PulseAttack = 0.07f, MinPulseGap = 0.36f;
        float pulseAge = 99f, pulseLength = 0.45f, pulse;
        int pulses;
        // The bass, smoothed over about a second, so the light and the swell follow the song's sections and not every
        // wobble of it.
        float bassSlow = 0.3f;
        // How bright the water light is, eased.
        float lightLevel;
        // Rings of light over the beads, running out from the middle with each pulse's ring in the water: how far each
        // has come (m), and how strong it is.
        const int Rings = 3;
        const float RingWidth = 0.06f, RingReach = 0.45f;
        readonly float[] ringRadius = { 99f, 99f, 99f };
        readonly float[] ringStrength = new float[Rings];
        int nextRing;
        // How far (m) a ripple travels in the water per simulation step.
        float waveStep;

        // ---- A handful of water ----
        // How much a handful leaks while pouring out at once (share of a full one per second), and how high (m) over
        // the palms' joints its rim sits, so it rests on the skin of the palms rather than inside them.
        const float SpillRate = 3.5f, HandfulRise = 0.018f;
        // Whether cupped hands are under the water, ready to scoop as they come up: both together, or each alone.
        bool pairUnder;
        readonly bool[] handUnder = new bool[2];
        // The hands holding water (1 left, 2 right, 3 both), and how much (1 is a full double handful; 0 none).
        int scoopHands;
        float scoopVolume;
        // The seam gap (m) and palm tilt (degrees), smoothed against tracking jitter; how long the hands have been
        // lost, and wide open or tipped; whether the water is pouring out at once.
        float seamGap, palmTilt, lostFor, spillFor;
        bool spilling, leakOverRim;
        // Drops owed to the leak, and how wide its stream is (m).
        float dripDebt, scoopStreamWidth;
        // The bowl the hands make: the palms' middle, the way across from left to right palm, the palms' distance,
        // how fast they move, where water gets through the seam, over the lowest rim, and where it leaks now.
        Vector3 bowlBase, bowlAcross = Vector3.right, bowlVelocity, seamPoint, rimPoint, leakPoint;
        float bowlSpan;
        // The handful as drawn: where it is and how it is turned (eased after the hands).
        MeshRenderer handful;
        Vector3 handfulAt;
        Quaternion handfulTurn = Quaternion.identity;
        // The table top or floor under the pool, and the floor, where falling drops vanish.
        float groundY, floorY;
        float dripSoundAt = -9f;
        AudioPool voices;
        AudioClip scoopClip, spillClip, dripClip;

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
            waveStep = Mathf.Sqrt(WaveC2) * size.x / (columns - 1);

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
            if (facetMaterial != null)
            {
                // Calm beads: facets only a little apart in light, a faint soft sheen instead of sharp glints that
                // twinkle as they turn, and a quiet rim. All of them are one instanced draw, which the copy allows.
                beadLook = new Material(facetMaterial) { name = "Sound pool beads", enableInstancing = true };
                beadLook.SetFloat("_Shade", 0.35f);
                beadLook.SetFloat("_Glint", 0.1f);
                beadLook.SetFloat("_GlintPower", 10f);
                beadLook.SetFloat("_Rim", 0.22f);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += DrawPreviewBeads;
                    previewHooked = true;
                }
#endif
            }

            looseOut = new ParticleSystem.Particle[MaxDrips];
            loose = MakeManualSystem("Drops", MaxDrips, true);
            if (streamMaterial != null)
            {
                streams = new LineStrip[Streams];
                for (int s = 0; s < Streams; s++)
                {
                    streams[s] = new LineStrip(s == 0 ? "Pour L" : s == 1 ? "Pour R" : "Handful stream", transform, streamMaterial, StreamPoints);
                    streams[s].SetVisible(false);
                }
            }
            streamPath = new Vector3[StreamPoints];
            streamWidths = new float[StreamPoints];
            streamSmooth = new Vector3[StreamPoints];
            if (roomRipplesMaterial != null)
            {
                // Calm room light: softer, wider lines with a little even light between them, the finest detail far
                // off melted away so it doesn't shimmer as you move, a slower drift, and wide, gentle swells.
                roomLook = new Material(roomRipplesMaterial) { name = "Sound pool room light" };
                roomLook.SetFloat(SharpnessId, 6f);
                roomLook.SetFloat(FillId, 0.12f);
                roomLook.SetFloat("_Smooth", 1f);
                roomLook.SetFloat("_RingWidth", 0.6f);
                roomLook.SetFloat("_RingGain", 0.5f);
                roomLook.SetFloat(DriftId, 0.5f);
                room = new RoomWaves(transform, roomLook, roomHalfSize, roomHeight);
            }

            if (handfulMaterial != null)
            {
                var go = new GameObject("Handful");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = HandfulMesh();
                handful = go.AddComponent<MeshRenderer>();
                handful.sharedMaterial = handfulMaterial;
                handful.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                handful.receiveShadows = false;
                go.SetActive(false);
            }

            voices = new AudioPool(transform, 6);
            scoopClip = ScoopSound();
            spillClip = SpillSound();
            dripClip = DripSound();
        }

        void OnDestroy()
        {
            if (beadLook != null) Discard(beadLook);
            if (roomLook != null) Discard(roomLook);
#if UNITY_EDITOR
            if (previewHooked) UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= DrawPreviewBeads;
#endif
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
            pulseAge = 99f;
            pulse = 0f;
            lightLevel = 0f;
            for (int r = 0; r < Rings; r++) ringRadius[r] = 99f;
            pairUnder = handUnder[0] = handUnder[1] = false;
            Empty();
        }

        protected override void OnExit()
        {
#if UNITY_EDITOR
            previewBeads = false;
#endif
            Empty();
            if (handful != null) handful.gameObject.SetActive(false);
            if (surface != null) surface.gameObject.SetActive(false);
            if (caustics != null) caustics.gameObject.SetActive(false);
            drips.Clear();
            System.Array.Clear(pouring, 0, Streams);
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
            floorY = Effects.FloorY(view);
            var center = view.transform.position + forward * distance + Vector3.down * belowEyes;
            center.y = Mathf.Max(center.y, floorY + 0.45f);
            // With a room scan, the pool fills a shallow tray on the real table in front of you.
            bool onTable = Room.TableAhead(view.transform.position, forward, 1.1f, size.y * 0.5f, out _, out var spot);
            if (onTable) center = spot + Vector3.up * 0.035f;
            groundY = onTable ? spot.y : floorY;
            surface.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
            surface.gameObject.SetActive(true);
            // The water's light falls on the table under it, or on the floor right below it.
            float lit = onTable ? spot.y + 0.003f : floorY + 0.004f;
            litPoint = new Vector3(center.x, lit, center.z);
            caustics.transform.SetPositionAndRotation(litPoint, Quaternion.LookRotation(Vector3.down, forward));
            float spread = onTable ? 1.1f : 1.4f;
            caustics.transform.localScale = new Vector3(size.x * spread, size.y * spread, 1f);
            // On the floor, the room's own floor carries this light on outwards, so the patch would only double it.
            caustics.gameObject.SetActive(onTable || room == null);
            room?.Place(view.transform.position, forward, floorY);
        }

        /// <summary>
        /// How far down (m, below the surface's rest) the water may dip before it meets the table or floor under it: a dent
        /// in a pool resting on a table bottoms out on the tray instead of sinking into the table.
        /// </summary>
        float TrayBottom => groundY + 0.004f - surface.position.y;

        /// <summary>
        /// Each kick drops a ring in the middle of the pool. A beat also starts the beads' pulse and a ring of light
        /// running out over them with the ring in the water, and every other one sends a soft swell of light out across
        /// the room. Beats closer together than MinPulseGap still ring the water but start no new pulse.
        /// </summary>
        protected override void OnBeat(MusicFrame music)
        {
            Disturb(new Vector2(0.5f, 0.5f), 0.05f, -0.006f - music.Kick * 0.004f);
            if (pulseAge < MinPulseGap) return;
            pulseAge = 0f;
            float interval = music.BeatInterval > 0.2f ? music.BeatInterval : 0.6f;
            pulseLength = Mathf.Clamp(interval * 0.75f, 0.3f, 0.6f);
            ringRadius[nextRing] = 0f;
            ringStrength[nextRing] = 0.8f + bassSlow * 0.2f;
            nextRing = (nextRing + 1) % Rings;
            if (++pulses % 2 == 1) room?.Ripple(surface.position, RippleColor, 0.25f + bassSlow * 0.15f);
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
            Beat(music, dt);
            HandSkin.FocusPosition = surface.position;
            HandSkin.FocusStrength = FadeIn;

            if (music.Upcoming != null && music.Upcoming.Count > 0) SpawnFromNotes(music);
            else SpawnFromAnalysis(music, dt);
            LandDroplets();

            float dip = TouchWithHands(rig, dt);
            Scoop(rig, dt);
            Pour(rig, dt);
            Simulate(dt);
            UpdateMesh(dt);
            Muffle(music.Bus, dip, dt);
            DrawBeads();
            DrawHandful(dt);
            Sing(view, dt);
            Shine(dt);
        }

        /// <summary>
        /// The beat's pulse for this frame (0-1): a quick, soft rise and a smooth fall that is over before the next beat,
        /// a little stronger in louder parts of the song. Also the slow bass that the swell and the light follow.
        /// </summary>
        void Beat(MusicFrame music, float dt)
        {
            bassSlow = Mathf.Lerp(bassSlow, Mathf.Max(music.Band(0), music.Band(1)), 1f - Mathf.Exp(-1.2f * dt));
            pulseAge += dt;
            float shape;
            if (pulseAge < PulseAttack) shape = Mathf.SmoothStep(0f, 1f, pulseAge / PulseAttack);
            else
            {
                float d = Mathf.Clamp01((pulseAge - PulseAttack) / (pulseLength - PulseAttack));
                shape = 1f - d * d * (3f - 2f * d);
            }
            pulse = shape * (0.75f + bassSlow * 0.25f);
        }

        /// <summary>
        /// The water light on the floor below, on the table under the pool and across the room: easing with the bass
        /// and with how stirred the water is, never jumping. Also the water's own shading.
        /// </summary>
        void Shine(float dt)
        {
            stir *= Mathf.Exp(-dt * 1.5f);
            float want = 0.2f + bassSlow * 0.1f + Mathf.Min(stir, 1f) * 0.22f;
            lightLevel = Mathf.Lerp(lightLevel, want, 1f - Mathf.Exp(-2.5f * dt));
            float light = lightLevel * FadeIn;
            block.Clear();
            block.SetFloat(StrengthId, light * 1.4f);
            block.SetFloat(ClockId, Now);
            block.SetFloat(SharpnessId, 6f);
            block.SetFloat(FillId, 0.12f);
            block.SetFloat(DriftId, 0.5f);
            caustics.SetPropertyBlock(block);
            if (room != null)
            {
                room.SetLight(litPoint, light);
                room.Step(Music, dt);
            }

            block.Clear();
            block.SetFloat(KickId, pulse);
            block.SetFloat(AlphaId, FadeIn);
            block.SetFloat(SpecId, 1f);
            block.SetFloat(SpecPowerId, 40f);
            block.SetFloat(LipBeatId, 0.35f);
            // Softer crests, so the swell rolls through gentle shades instead of patches of bright and deep.
            block.SetFloat(CrestGainId, 60f);
            surfaceRenderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// Scooping. Cupped hands (palms up, not pinching or making a fist) under the water and brought up through the
        /// surface carry a handful of it up (FindScoop): both together when the seam between them is narrower than
        /// spillGap, or one alone. While held, the handful follows the hands (Bowl) and leaks (Leak) as fast as the hands
        /// are open and tipped, until it is empty, pours out at once (Spill), or goes back into the pool (Merge). Hands
        /// lost for a moment keep their water; the open menu, or hands lost for longer, let it fall.
        /// </summary>
        void Scoop(HandRig rig, float dt)
        {
            bool menu = MenuOpen;
            if (scoopVolume <= 0f)
            {
                FindScoop(rig, menu);
                return;
            }
            bool useLeft = (scoopHands & 1) != 0, useRight = (scoopHands & 2) != 0;
            bool tracked = rig != null && (!useLeft || rig.Left.IsTracked) && (!useRight || rig.Right.IsTracked);
            lostFor = tracked ? 0f : lostFor + dt;
            float rate = 1f / sealedSeconds;
            if (tracked)
            {
                Bowl(rig, useLeft, useRight, out float gap, out float tilt, out bool squeezed);
                // Smoothed, so tracking jitter doesn't make the leak flicker.
                float follow = 1f - Mathf.Exp(-12f * dt);
                seamGap = Mathf.Lerp(seamGap, gap, follow);
                palmTilt = Mathf.Lerp(palmTilt, tilt, follow);
                // Lowered back into the pool, what is left goes back in.
                var local = surface.InverseTransformPoint(bowlBase);
                if (!menu && local.y < -0.005f && Mathf.Abs(local.x) < size.x * 0.5f && Mathf.Abs(local.z) < size.y * 0.5f)
                {
                    Merge(new Vector2(local.x / size.x + 0.5f, local.z / size.y + 0.5f));
                    return;
                }
                if (!spilling && !menu)
                {
                    // Closed, a drop a second; opening the seam or tipping the palms lets it out faster and faster.
                    float open = Mathf.InverseLerp(sealGap, spillGap, seamGap);
                    float tip = Mathf.InverseLerp(dripTilt, spillTilt, palmTilt);
                    rate += open * open * 1.6f + tip * tip * 2.5f;
                    leakOverRim = tip * tip * 2.5f > open * open * 1.6f;
                    // Wide open or tipped right over (held a moment, against jitter), or squeezed: it all pours out.
                    spillFor = seamGap > spillGap || palmTilt > spillTilt ? spillFor + dt : 0f;
                    if (squeezed || spillFor > 0.1f) Spill(true);
                }
                leakPoint = leakOverRim ? rimPoint : seamPoint;
            }
            // The menu takes the hands, and hands lost for more than a moment hold nothing: the water falls.
            if (!spilling && (menu || lostFor > 0.5f)) Spill(false);
            Leak(spilling ? SpillRate : rate, dt);
        }

        /// <summary>
        /// Watches for cupped hands coming up through the surface. Two cupped hands under the water with a seam narrower
        /// than spillGap make one bowl, which scoops as the middle between them comes up through it; a cupped hand on its
        /// own scoops as it comes up. A hand counts as under from 5 mm below the surface and as out from 5 mm above it,
        /// so it can't scoop again and again while it hovers at the surface.
        /// </summary>
        void FindScoop(HandRig rig, bool menu)
        {
            float depthL = 0f, depthR = 0f;
            bool left = rig != null && !menu && Cupped(rig.Left, out depthL);
            bool right = rig != null && !menu && Cupped(rig.Right, out depthR);
            if (left && right && SeamGap(rig.Left, rig.Right, out _) < spillGap)
            {
                handUnder[0] = handUnder[1] = false;
                float depth = (depthL + depthR) * 0.5f;
                if (depth < -0.005f)
                {
                    bool ready = pairUnder;
                    pairUnder = false;
                    if (ready) TakeScoop(rig, 3);
                }
                else if (depth > 0.005f) pairUnder = depth < 0.25f;
                return;
            }
            pairUnder = false;
            for (int side = 0; side < 2; side++)
            {
                bool cupped = side == 0 ? left : right;
                float depth = side == 0 ? depthL : depthR;
                if (!cupped)
                {
                    handUnder[side] = false;
                    continue;
                }
                if (depth < -0.005f)
                {
                    bool ready = handUnder[side];
                    handUnder[side] = false;
                    if (ready)
                    {
                        TakeScoop(rig, side == 0 ? 1 : 2);
                        return;
                    }
                }
                else if (depth > 0.005f) handUnder[side] = depth < 0.25f;
            }
        }

        /// <summary>
        /// Whether a hand is cupped (palm within 60 degrees of up, not pinching, not a fist) over or in the pool, and how
        /// deep its palm is (negative above the surface).
        /// </summary>
        bool Cupped(HandRig.Hand hand, out float depth)
        {
            depth = 0f;
            if (!hand.IsTracked || hand.InSystemGesture || hand.Pinch > 0.5f || hand.Fist > 0.85f) return false;
            if (Vector3.Dot(hand.PalmNormal, Vector3.up) < 0.5f) return false;
            var local = surface.InverseTransformPoint(Palm(hand));
            depth = -local.y;
            return Mathf.Abs(local.x) < size.x * 0.5f && Mathf.Abs(local.z) < size.y * 0.5f;
        }

        /// <summary>
        /// The hands come up with a handful: the closer together, the more they bring (a full one with the seam closed, a
        /// third with it nearly as wide as spillGap, and a third in one hand). The surface dips and rings where the water
        /// left, with a soft scooping sound.
        /// </summary>
        void TakeScoop(HandRig rig, int hands)
        {
            scoopHands = hands;
            Bowl(rig, (hands & 1) != 0, (hands & 2) != 0, out float gap, out float tilt, out _);
            seamGap = gap;
            palmTilt = tilt;
            scoopVolume = hands == 3 ? Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(sealGap, spillGap, gap)) : 0.35f;
            spilling = leakOverRim = false;
            lostFor = spillFor = 0f;
            dripDebt = 0.5f;
            leakPoint = seamPoint;
            handfulAt = bowlBase + Vector3.up * HandfulRise;
            handfulTurn = Quaternion.LookRotation(Vector3.Cross(bowlAcross, Vector3.up), Vector3.up);
            var local = surface.InverseTransformPoint(bowlBase);
            var spot = new Vector2(local.x / size.x + 0.5f, local.z / size.y + 0.5f);
            Disturb(spot, hands == 3 ? 0.05f : 0.035f, -0.002f - 0.004f * scoopVolume);
            Splash(SurfacePoint(spot), Effects.Blue, 6, 0.25f);
            Acted("scoop water");
            voices?.Play(scoopClip, bowlBase, 0.25f);
        }

        /// <summary>
        /// The bowl the holding hands make this frame: where it is (bowlBase, bowlAcross, bowlSpan, bowlVelocity); how open
        /// it is, as the widest gap along the seam between two hands (one hand alone counts as partly open, its fingers
        /// letting water through); how far the palms tip; where water gets out first (seamPoint under the seam, rimPoint
        /// over the lowest rim); and whether a hand squeezes it out (a fist or a pinch).
        /// </summary>
        void Bowl(HandRig rig, bool useLeft, bool useRight, out float gap, out float tilt, out bool squeezed)
        {
            if (useLeft && useRight)
            {
                HandRig.Hand left = rig.Left, right = rig.Right;
                Vector3 pl = Palm(left), pr = Palm(right);
                var across = Vector3.ProjectOnPlane(pr - pl, Vector3.up);
                bowlSpan = across.magnitude;
                if (bowlSpan > 1e-3f) bowlAcross = across / bowlSpan;
                bowlBase = (pl + pr) * 0.5f;
                bowlVelocity = (left.Velocity + right.Velocity) * 0.5f;
                gap = SeamGap(left, right, out seamPoint);
                float tl = Tilt(left, bowlAcross), tr = Tilt(right, -bowlAcross);
                tilt = Mathf.Max(tl, tr);
                rimPoint = tl >= tr ? RimBelow(left, bowlAcross) : RimBelow(right, -bowlAcross);
                squeezed = left.Fist > 0.85f || right.Fist > 0.85f || left.Pinch > 0.8f || right.Pinch > 0.8f;
            }
            else
            {
                var hand = useLeft ? rig.Left : rig.Right;
                bowlBase = Palm(hand);
                // Across the palm from left to right: thumb to little finger on the left hand, the other way on the right.
                var across = Vector3.ProjectOnPlane(useLeft ? Tip(hand, 4) - Tip(hand, 1) : Tip(hand, 1) - Tip(hand, 4), Vector3.up);
                if (across.sqrMagnitude > 1e-6f) bowlAcross = across.normalized;
                bowlSpan = 0.05f;
                bowlVelocity = hand.Velocity;
                gap = Mathf.Lerp(sealGap, spillGap, 0.4f);
                seamPoint = (Tip(hand, 3) + Tip(hand, 4)) * 0.5f;
                tilt = Tilt(hand, Vector3.zero);
                rimPoint = RimBelow(hand, Vector3.zero);
                squeezed = hand.Fist > 0.85f || hand.Pinch > 0.8f;
            }
            seamPoint += Vector3.down * 0.012f;
        }

        /// <summary>
        /// The widest gap along the seam between two cupped hands: between the edges of the palms below the little fingers,
        /// and between the little fingertips. Also the middle of that gap, where water gets through first.
        /// </summary>
        static float SeamGap(HandRig.Hand left, HandRig.Hand right, out Vector3 at)
        {
            Vector3 el = PalmEdge(left), er = PalmEdge(right), tl = Tip(left, 4), tr = Tip(right, 4);
            float edges = Vector3.Distance(el, er), tips = Vector3.Distance(tl, tr);
            at = edges >= tips ? (el + er) * 0.5f : (tl + tr) * 0.5f;
            return Mathf.Max(edges, tips);
        }

        /// <summary>The edge of a palm below its little finger, about four centimetres out from its middle.</summary>
        static Vector3 PalmEdge(HandRig.Hand hand)
        {
            var side = Vector3.ProjectOnPlane(Tip(hand, 4) - Tip(hand, 1), hand.PalmNormal);
            return Palm(hand) + (side.sqrMagnitude > 1e-6f ? side.normalized * 0.04f : Vector3.zero);
        }

        /// <summary>
        /// How far (degrees) a palm tips away from up, not counting a lean of up to 30 degrees towards the other hand:
        /// palms leaning together make the bowl.
        /// </summary>
        static float Tilt(HandRig.Hand hand, Vector3 inward) => Vector3.Angle(Leaned(hand, inward), Vector3.up);

        /// <summary>The point over the lowest rim of a tipped palm, where water runs off it first.</summary>
        static Vector3 RimBelow(HandRig.Hand hand, Vector3 inward)
        {
            var downhill = Vector3.ProjectOnPlane(Leaned(hand, inward), Vector3.up);
            return Palm(hand) + (downhill.sqrMagnitude > 1e-6f ? downhill.normalized * 0.045f : Vector3.zero) + Vector3.down * 0.01f;
        }

        /// <summary>A palm's normal without any lean of up to 30 degrees towards <paramref name="inward"/>.</summary>
        static Vector3 Leaned(HandRig.Hand hand, Vector3 inward)
        {
            var n = hand.PalmNormal;
            return n - inward * Mathf.Clamp(Vector3.Dot(n, inward), 0f, 0.5f);
        }

        static Vector3 Palm(HandRig.Hand hand) => hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;

        static Vector3 Tip(HandRig.Hand hand, int finger) => hand.TipJoints[finger] != null ? hand.TipJoints[finger].position : hand.Tips[finger];

        /// <summary>
        /// Water leaving the handful at <paramref name="rate"/> (share of a full handful per second) from leakPoint: drops,
        /// one a second from hands held closed and up to nine a second as they open, and from about six a second also a
        /// thin stream, thicker the faster it runs. The handful shrinks by as much; once it is empty it is gone.
        /// </summary>
        void Leak(float rate, float dt)
        {
            scoopVolume -= rate * dt;
            dripDebt += Mathf.Min(rate * sealedSeconds, 9f) * dt;
            while (dripDebt >= 1f)
            {
                dripDebt -= 1f;
                if (drips.Count < MaxDrips) drips.Add(ScoopDrop(leakPoint + Random.insideUnitSphere * 0.004f, bowlVelocity * 0.3f + Vector3.down * 0.05f));
            }
            scoopStreamWidth = Mathf.Lerp(0.003f, 0.012f, Mathf.InverseLerp(0.35f, 1.6f, rate));
            if (rate > 0.35f && scoopVolume > 0f) Stream(ScoopStream, leakPoint, bowlVelocity, dt);
            else StopStream(ScoopStream);
            if (scoopVolume <= 0.01f) Empty();
        }

        Drip ScoopDrop(Vector3 at, Vector3 velocity) => new Drip
        {
            Position = at,
            Velocity = velocity,
            Color = Color.Lerp(Effects.Blue, Color.white, Random.Range(0.3f, 0.6f)),
            Side = ScoopStream,
            Attached = false
        };

        /// <summary>
        /// The water pours out at once, from under the seam or over the lowest rim: a burst of drops, then the rest runs out
        /// as a stream in a moment (SpillRate). <paramref name="byHands"/> when the hands did it (opened wide, tipped over or
        /// squeezed), not the menu or lost tracking.
        /// </summary>
        void Spill(bool byHands)
        {
            spilling = true;
            if (byHands) Acted("pour out the water");
            for (int i = 0; i < 8 && drips.Count < MaxDrips; i++)
                drips.Add(ScoopDrop(leakPoint + Random.insideUnitSphere * 0.02f, bowlVelocity * 0.3f + Random.insideUnitSphere * 0.08f));
            voices?.Play(spillClip, leakPoint, 0.22f);
        }

        /// <summary>The hands went back into the pool: what is left of the handful joins it with a small splash.</summary>
        void Merge(Vector2 spot)
        {
            Disturb(spot, 0.04f, -0.002f - 0.004f * scoopVolume);
            Splash(SurfacePoint(spot), Effects.Blue, 4 + (int)(8f * scoopVolume), 0.3f);
            Acted("pour the water back");
            voices?.Play(scoopClip, bowlBase, 0.15f, 1.3f);
            Empty();
        }

        /// <summary>The hands hold no water any more.</summary>
        void Empty()
        {
            scoopVolume = 0f;
            scoopHands = 0;
            spilling = false;
            StopStream(ScoopStream);
        }

        /// <summary>
        /// The handful: a small lens of water in the bowl of the hands, level on top whatever they do, following them
        /// (eased a little against tracking jitter), shrinking as it leaks, and rippling gently with the beat and as the
        /// hands move. Held in two hands it stretches across them.
        /// </summary>
        void DrawHandful(float dt)
        {
            if (handful == null) return;
            bool show = scoopVolume > 0f;
            if (handful.gameObject.activeSelf != show) handful.gameObject.SetActive(show);
            if (!show) return;
            float fill = Mathf.Sqrt(Mathf.Clamp01(scoopVolume));
            bool both = scoopHands == 3;
            float across = Mathf.Lerp(0.035f, both ? Mathf.Clamp(bowlSpan + 0.03f, 0.08f, 0.15f) : 0.06f, fill);
            float along = Mathf.Lerp(0.03f, both ? 0.09f : 0.065f, fill);
            float deep = Mathf.Lerp(0.004f, 0.016f, fill);
            float follow = 1f - Mathf.Exp(-25f * dt);
            handfulAt = Vector3.Lerp(handfulAt, bowlBase + Vector3.up * HandfulRise, follow);
            handfulTurn = Quaternion.Slerp(handfulTurn, Quaternion.LookRotation(Vector3.Cross(bowlAcross, Vector3.up), Vector3.up), follow);
            handful.transform.SetPositionAndRotation(handfulAt, handfulTurn);
            handful.transform.localScale = new Vector3(across, deep, along);
            block.Clear();
            block.SetFloat(RippleId, 0.25f + pulse * 0.5f + Mathf.Min(bowlVelocity.magnitude, 0.5f) * 0.8f);
            block.SetFloat(ClockId, Now);
            block.SetFloat(AlphaId, FadeIn * Mathf.Clamp01(scoopVolume / 0.05f));
            handful.SetPropertyBlock(block);
        }

        /// <summary>
        /// The handful's mesh: two discs of diameter 1 in rings round the middle, one for the water's surface (uv.y 1) and
        /// one for the bowl under it (uv.y 0). The SoundPoolHandful shader shapes them.
        /// </summary>
        static Mesh HandfulMesh()
        {
            const int Rings = 6, Segments = 28, PerDisc = 1 + Rings * Segments;
            var vertices = new Vector3[PerDisc * 2];
            var uvs = new Vector2[PerDisc * 2];
            var triangles = new int[2 * (Segments * 3 + (Rings - 1) * Segments * 6)];
            int t = 0;
            for (int disc = 0; disc < 2; disc++)
            {
                int o = disc * PerDisc;
                float top = disc == 0 ? 1f : 0f;
                vertices[o] = Vector3.zero;
                uvs[o] = new Vector2(0f, top);
                for (int r = 0; r < Rings; r++)
                {
                    for (int s = 0; s < Segments; s++)
                    {
                        float q = (r + 1) / (float)Rings, a = s * Mathf.PI * 2f / Segments;
                        int i = o + 1 + r * Segments + s;
                        vertices[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (q * 0.5f);
                        uvs[i] = new Vector2(q, top);
                    }
                }
                for (int s = 0; s < Segments; s++)
                {
                    triangles[t++] = o; triangles[t++] = o + 1 + (s + 1) % Segments; triangles[t++] = o + 1 + s;
                }
                for (int r = 0; r < Rings - 1; r++)
                {
                    for (int s = 0; s < Segments; s++)
                    {
                        int a0 = o + 1 + r * Segments + s, a1 = o + 1 + r * Segments + (s + 1) % Segments;
                        int b0 = a0 + Segments, b1 = a1 + Segments;
                        triangles[t++] = a0; triangles[t++] = a1; triangles[t++] = b0;
                        triangles[t++] = a1; triangles[t++] = b1; triangles[t++] = b0;
                    }
                }
            }
            var mesh = new Mesh { name = "Handful" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            // The shader raises the surface to 1 and lowers the bowl to -0.6.
            mesh.bounds = new Bounds(new Vector3(0f, 0.25f, 0f), new Vector3(1.1f, 2f, 1.1f));
            return mesh;
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
                    bool under = inside && depth > 0f && depth < 0.25f;
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
                            Disturb(spot, radius * 1.5f, -0.006f);
                            Splash(SurfacePoint(spot), Effects.Blue, k < 5 ? 4 : 10, 0.25f);
                            // A palm breaking the surface sends its own soft swell of light out over the room.
                            if (k == 5) room?.Ripple(SurfacePoint(spot), Effects.Blue, 0.3f);
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
        /// Pinching pours water from between thumb and index (Stream). Each frame it lets go of a few drops that fall
        /// under gravity; while you keep pinching they stay joined as one stream, drawn as a smooth curve through them from
        /// your fingers down, so it bends as your hand moves. It ripples the water where it lands (or vanishes on the
        /// floor or table beside it). Let go and the stream's tail falls on as separate drops. A handful's drops and
        /// stream (Leak) fall the same way, with a quiet drip as they land in the pool.
        /// </summary>
        void Pour(HandRig rig, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig?.Get(side == 0);
                // A pinch at the open menu pours nothing, nor does one still held from it once it closes.
                bool now = hand != null && hand.IsTracked && hand.Pinch > 0.8f && !hand.InSystemGesture && PinchFree(side == 0);
                if (!now)
                {
                    StopStream(side);
                    continue;
                }
                Acted("pour water");
                Stream(side, (Tip(hand, 0) + Tip(hand, 1)) * 0.5f, hand.Velocity, dt);
            }

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
                    if (d.Side == ScoopStream && Application.isPlaying && Clock - dripSoundAt > 0.12f)
                    {
                        dripSoundAt = Clock;
                        voices?.Play(dripClip, d.Position, d.Attached ? 0.04f : 0.07f, Random.Range(0.85f, 1.3f));
                    }
                    drips.RemoveAt(i);
                    continue;
                }
                if (d.Position.y < Room.GroundBelow(d.Position, floorY)) { drips.RemoveAt(i); continue; }
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
            for (int side = 0; side < Streams; side++) DrawStream(side);
        }

        /// <summary>
        /// One frame of a stream of water leaving <paramref name="from"/> (moving at <paramref name="velocity"/>): it lets
        /// go of a few drops that stay joined as one stream, spread along the way the source moved since the last frame
        /// and each started a little further on in its fall, so they leave one after another instead of in a clump.
        /// </summary>
        void Stream(int side, Vector3 from, Vector3 velocity, float dt)
        {
            pourFrom[side] = from;
            if (!pouring[side])
            {
                lastPourFrom[side] = from;
                pourVelocity[side] = velocity;
            }
            pouring[side] = true;
            pourVelocity[side] = Vector3.Lerp(pourVelocity[side], velocity, 1f - Mathf.Exp(-6f * dt));
            streamDebt[side] += 80f * dt;
            int count = (int)streamDebt[side];
            for (int k = 0; k < count && drips.Count < MaxDrips; k++)
            {
                float back = (count - k) / (float)count;
                var v = pourVelocity[side] * 0.3f + Vector3.down * 0.15f;
                float early = back * dt;
                drips.Add(new Drip
                {
                    Position = Vector3.Lerp(from, lastPourFrom[side], back) + v * early + Physics.gravity * (0.5f * early * early),
                    Velocity = v + Physics.gravity * early,
                    Color = Color.Lerp(Effects.Blue, Color.white, Random.Range(0.3f, 0.6f)),
                    Side = side,
                    Attached = true
                });
            }
            streamDebt[side] -= count;
            lastPourFrom[side] = from;
        }

        /// <summary>Ends a stream: its tail falls on as separate drops.</summary>
        void StopStream(int side)
        {
            if (pouring[side])
                for (int i = 0; i < drips.Count; i++)
                    if (drips[i].Side == side && drips[i].Attached) { var d = drips[i]; d.Attached = false; drips[i] = d; }
            pouring[side] = false;
            streamDebt[side] = 0f;
        }

        /// <summary>
        /// A stream (from a pinch, or out of a handful): a curve from where it leaves down through its drops, newest
        /// first. It narrows as the water speeds up, as a real stream does.
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
            float width = side == ScoopStream ? scoopStreamWidth : 0.011f;
            line.Set(streamPath, null, streamWidths, count, width, new Color(1f, 1f, 1f, FadeIn), View.transform.position);
        }

        /// <summary>The sung words drift slowly across the water, rising and falling with its waves.</summary>
        void Sing(Camera view, float dt)
        {
            if (SungWords == null) return;
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                w.Spot = new Vector2(Mathf.Clamp(w.Spot.x + w.Velocity.x * dt, 0.1f, 0.9f), Mathf.Clamp(w.Spot.y + w.Velocity.z * dt, 0.1f, 0.9f));
                w.Position = SurfacePoint(w.Spot) + surface.up * (Mathf.Max(HeightAt(w.Spot), 0f) + 0.012f);
                w.Rotation = WordLayer.Lying(w.Position, eye);
                w.Halo = 0.2f + pulse * 0.2f;
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
        /// How much the rings of light running out over the beads light a bead <paramref name="d"/> metres from the
        /// middle: each a soft band that fades in as it leaves the middle and out as it nears the edges.
        /// </summary>
        float RingGlow(float d)
        {
            float g = 0f;
            for (int r = 0; r < Rings; r++)
            {
                float radius = ringRadius[r];
                if (radius >= RingReach) continue;
                float x = (d - radius) / RingWidth;
                if (x * x > 9f) continue;
                float life = 1f - radius / RingReach;
                g += Mathf.Exp(-x * x) * life * life * Mathf.Clamp01(radius / 0.05f) * ringStrength[r];
            }
            return Mathf.Min(g, 1f);
        }

        /// <summary>
        /// The beads dotting the surface: low-poly balls in lines across the water, about two centimetres apart, riding
        /// the ocean swell (see Swell): each rises and falls with it and is carried a little back and forth as the crests
        /// pass, so the lines bunch on the crests and spread in the troughs. They bob on the ripples from hands and drops
        /// too. Their colour stays in a narrow band of water blues and teals that drifts slowly across the pool. Their
        /// light is calm between beats (only a
        /// touch brighter up on a crest) and swells together on the beat (<see cref="pulse"/>), and where a ring of
        /// light runs past (RingGlow); they swell a little with it. Towards the edges they shrink away, so the pool has no
        /// hard border. They turn slowly so their facets catch the light. All of them are one instanced draw.
        /// </summary>
        void DrawBeads()
        {
            if (beadLook == null) return;
            float t = Now;
            var spin = Quaternion.Euler(t * 5f, t * 3.5f, 0f);
            float bottom = TrayBottom;
            for (int z = 0; z < BeadRows; z++)
            {
                for (int x = 0; x < BeadColumns; x++)
                {
                    int i = z * BeadColumns + x;
                    var spot = new Vector2((x + 0.5f) / BeadColumns, (z + 0.5f) / BeadRows);
                    var rest = new Vector2((spot.x - 0.5f) * size.x, (spot.y - 0.5f) * size.y);
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(Mathf.Min(spot.x, 1f - spot.x), Mathf.Min(spot.y, 1f - spot.y)) * 9f);
                    float swell = Swell(rest, t, swellHeight, out var shift) * edge;
                    shift *= edge * 0.7f;
                    float px = rest.x + shift.x, pz = rest.y + shift.y;
                    // The water's own ripples, from hands and drops.
                    float ripple = height[Nearest(spot)];
                    float crest = Mathf.Clamp01(0.5f + swell / Mathf.Max(swellHeight, 1e-4f) * 0.5f);
                    float ring = RingGlow(Mathf.Sqrt(px * px + pz * pz));
                    var local = new Vector3(px, Mathf.Max(swell + ripple, bottom) + 0.002f, pz);
                    float diameter = BeadSize * (0.92f + crest * 0.06f + pulse * 0.14f + ring * 0.2f) * edge * FadeIn;
                    beadMatrices[i] = Matrix4x4.TRS(surface.TransformPoint(local), spin * beadTurn[i], Vector3.one * diameter);
                    // A slow tide of colour, blue to teal, drifting across the pool over many seconds.
                    float tide = 0.5f + 0.5f * Mathf.Sin(rest.x * 6f + rest.y * 4f + t * 0.3f + Mathf.Sin(rest.y * 7f - t * 0.2f));
                    var c = Color.Lerp(BeadBlue, BeadTeal, tide);
                    float glow = 0.14f + crest * 0.05f + pulse * 0.3f + ring * 0.32f;
                    beadTints[i] = new Vector4(c.r, c.g, c.b, glow);
                }
            }
            beadBlock.SetVectorArray(TintId, beadTints);
#if UNITY_EDITOR
            // The editor previews step many frames before each render, and draws queued here would all show at once
            // (a smear of every past position), so there the beads are drawn as each camera starts (DrawPreviewBeads).
            if (!Application.isPlaying)
            {
                previewBeads = true;
                return;
            }
#endif
            Graphics.RenderMeshInstanced(BeadParams(null), beadMesh, 0, beadMatrices, BeadCount);
        }

        RenderParams BeadParams(Camera only) => new RenderParams(beadLook)
        {
            matProps = beadBlock,
            layer = gameObject.layer,
            camera = only,
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = new Bounds(surface.position, new Vector3(size.x + 0.1f, 0.2f, size.x + 0.1f))
        };

#if UNITY_EDITOR
        // Whether the editor preview has beads to draw: set by DrawBeads, cleared when the world is left.
        bool previewBeads, previewHooked;

        /// <summary>Editor previews only: draws the beads as they are now into the world's camera as it starts rendering.</summary>
        void DrawPreviewBeads(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        {
            if (this == null)
            {
                UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= DrawPreviewBeads;
                return;
            }
            if (!previewBeads || camera != View || surface == null || !surface.gameObject.activeInHierarchy) return;
            Graphics.RenderMeshInstanced(BeadParams(camera), beadMesh, 0, beadMatrices, BeadCount);
        }
#endif

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

        /// <summary>
        /// Two steps of the wave equation on the height grid per frame (three on slow frames). The rings of light over
        /// the beads move on with the water's own ripples, a step at a time, so they stay on the kick's ring.
        /// </summary>
        void Simulate(float dt)
        {
            int steps = dt > 0.02f ? 3 : 2;
            for (int s = 0; s < steps; s++)
            {
                for (int z = 1; z < rows - 1; z++)
                {
                    for (int x = 1; x < columns - 1; x++)
                    {
                        int i = z * columns + x;
                        float laplacian = height[i - 1] + height[i + 1] + height[i - columns] + height[i + columns] - 4f * height[i];
                        next[i] = (2f * height[i] - previous[i] + WaveC2 * laplacian) * damping * sponge[i];
                    }
                }
                var swap = previous;
                previous = height;
                height = next;
                next = swap;
            }
            for (int r = 0; r < Rings; r++)
                if (ringRadius[r] < RingReach) ringRadius[r] += waveStep * steps;
        }

        /// <summary>
        /// Copies the heights into the mesh, plus the ocean swell rolling across it all the time (see Swell), taller
        /// with the bass, and rebuilds the normals. The swell eases with the slow bass rather than jumping on every kick.
        /// </summary>
        void UpdateMesh(float dt)
        {
            float t = Now;
            swellHeight = Mathf.Lerp(swellHeight, 0.007f + bassSlow * 0.009f + pulse * 0.002f, 1f - Mathf.Exp(-3f * dt));
            float bottom = TrayBottom;
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int i = z * columns + x;
                    float u = x / (float)(columns - 1), v = z / (float)(rows - 1);
                    float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 8f);
                    // The sheet takes the swell's height; only the beads on it are carried sideways.
                    float swell = Swell(new Vector2(vertices[i].x, vertices[i].z), t, swellHeight, out _);
                    vertices[i].y = Mathf.Max(height[i] + swell * edge, bottom);
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

        // ---- The water's sounds, made once in code ----

        static int Rate => AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;

        /// <summary>Water scooped up in the hands: a soft, low bubble gliding up, inside a dark swish of moving water.</summary>
        static AudioClip ScoopSound()
        {
            int rate = Rate, n = (int)(rate * 0.55f);
            var data = new float[n];
            var random = new System.Random(11);
            float phase = 0f, low = 0f, lower = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = 190f + 240f * (1f - Mathf.Exp(-t * 14f));
                phase += 2f * Mathf.PI * f / rate;
                float bubble = Mathf.Sin(phase) * Mathf.Min(1f, t / 0.012f) * Mathf.Exp(-t * 10f);
                low += ((float)(random.NextDouble() * 2 - 1) - low) * 0.05f;
                lower += (low - lower) * 0.05f;
                float swish = lower * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.5f));
                data[i] = bubble * 0.5f + swish * 9f;
            }
            return MakeClip("pool scoop", data, rate, 0.5f);
        }

        /// <summary>A handful poured out at once: two soft drops, the second smaller.</summary>
        static AudioClip SpillSound()
        {
            int rate = Rate, n = (int)(rate * 0.4f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float s = 0f;
                for (int d = 0; d < 2; d++)
                {
                    float age = t - d * 0.13f;
                    if (age < 0f) continue;
                    float f = (d == 0 ? 620f : 760f) + 700f * (1f - Mathf.Exp(-age * 50f));
                    s += Mathf.Sin(phase * f) * Mathf.Min(1f, age / 0.002f) * Mathf.Exp(-age * 32f) * (d == 0 ? 1f : 0.6f);
                }
                phase += 2f * Mathf.PI / rate;
                data[i] = s;
            }
            return MakeClip("pool spill", data, rate, 0.45f);
        }

        /// <summary>
        /// A single small drop landing in the pool, for a handful's drips: a short, high bubble.
        /// </summary>
        static AudioClip DripSound()
        {
            int rate = Rate, n = (int)(rate * 0.15f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                phase += 2f * Mathf.PI * (900f + 900f * (1f - Mathf.Exp(-t * 60f))) / rate;
                data[i] = Mathf.Sin(phase) * Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * 45f);
            }
            return MakeClip("pool drip", data, rate, 0.4f);
        }

        /// <summary>An audio clip of <paramref name="data"/>, scaled so its loudest sample is <paramref name="peak"/>.</summary>
        static AudioClip MakeClip(string name, float[] data, int rate, float peak)
        {
            float max = 1e-6f;
            foreach (var s in data) max = Mathf.Max(max, Mathf.Abs(s));
            for (int i = 0; i < data.Length; i++) data[i] *= peak / max;
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
