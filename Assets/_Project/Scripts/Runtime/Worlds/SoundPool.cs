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
    /// Cup both hands under the water, palms up, as if scooping some up to drink, and you hold the whole pool: raise
    /// your hands and it rises with them, lower them and it sinks. The water mounds up over each palm and glows softly
    /// there, the beads ride the mounds, water drips from the edges of your hands and sloshes quietly as it moves, and
    /// the room's water light rises with it. It never comes higher than a little below your eyes, nor lower than just
    /// above the floor (or its table). Take your hands away (out to the side, apart, or palms turned down) and it
    /// stays at its new height, also when the world sets itself up again after you move.
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

        [Header("Lifting")]
        [Tooltip("The fastest (m/s) the pool rises or sinks while you hold it.")]
        [SerializeField] float liftSpeed = 0.6f;
        [Tooltip("How quickly the held pool catches up with your palms (per second); lower feels heavier.")]
        [SerializeField] float liftFollow = 8f;
        [Tooltip("The highest the pool goes: this far (m) below your eyes.")]
        [SerializeField] float eyeClearance = 0.25f;
        [Tooltip("The lowest the pool goes: this far (m) above the floor. On a table it goes no lower than it rests there.")]
        [SerializeField] float floorClearance = 0.15f;
        [Tooltip("How long (s) both cupped hands stay under the water before they hold the pool.")]
        [SerializeField] float holdDelay = 0.2f;

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
        static readonly int PalmLId = Shader.PropertyToID("_PalmL");
        static readonly int PalmRId = Shader.PropertyToID("_PalmR");
        static readonly int SharpnessId = Shader.PropertyToID("_Sharpness");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int DriftId = Shader.PropertyToID("_Drift");
        // The melody's droplets: pale blues and aquas, a little apart so neighbouring notes differ, all of them water.
        static readonly Color[] DropColors =
        {
            new Color(0.5f, 0.78f, 1f), new Color(0.45f, 0.88f, 0.9f), new Color(0.62f, 0.9f, 1f), new Color(0.42f, 0.7f, 0.98f)
        };
        // The beads' palette: a narrow band of water colours, blue to teal, and the paler aqua they take on over a palm.
        static readonly Color BeadBlue = new Color(0.2f, 0.5f, 0.84f);
        static readonly Color BeadTeal = new Color(0.17f, 0.62f, 0.7f);
        static readonly Color BeadLight = new Color(0.42f, 0.8f, 0.92f);
        static readonly Color RippleColor = new Color(0.45f, 0.85f, 1f);
        static readonly Color DripColor = new Color(0.65f, 0.92f, 1f, 0.95f);

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
        ParticleSystem splashes, loose, handDrips;
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

        // ---- Holding the pool ----
        const float TableRest = 0.035f, MoundHeight = 0.02f, MoundRadius = 0.075f;
        bool holding;
        // How long both hands have been cupped under the water (s) before holding, and how long since they stopped
        // (s) while holding, so a moment of lost tracking doesn't drop the pool.
        float holdTimer, letGo;
        // The surface's height over the palms when it was picked up, and the gap between the palms then.
        float holdOffset, holdGap;
        // How much the pool is held (0-1, eased): the mounds over the palms and their light.
        float holdWeight;
        // How fast the pool is rising (+) or sinking (m/s), smoothed.
        float liftVelocity;
        // Where each palm is under the pool (metres across it), smoothed, and how tall the water mounds over it.
        readonly Vector2[] moundAt = new Vector2[2];
        readonly float[] moundHeight = new float[2];
        readonly Vector3[] palmAt = new Vector3[2];
        readonly float[] dripDebt = new float[2];
        readonly int[] dripTurn = new int[2];
        // Where the pool sits: the table top or floor under it, whether it is a table, its usual height there, and
        // how wide its light patch is there.
        float groundY, restY, causticSpread = 1.4f;
        bool onTable;
        // A height chosen by lifting the pool, kept as a height over the table or floor it was chosen over.
        bool chosen, chosenOnTable;
        float chosenAbove;
        AudioPool voices;
        AudioSource slosh;
        AudioClip scoopClip, dropsClip;

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

            // Drops falling from the edges of the hands that hold the pool up.
            handDrips = Effects.MakeSparkSystem("Hand drips", transform, glowMaterial);
            var dripMain = handDrips.main;
            dripMain.gravityModifier = 1f;
            dripMain.maxParticles = 160;
            var dripNoise = handDrips.noise;
            dripNoise.enabled = false;
            var dripRenderer = handDrips.GetComponent<ParticleSystemRenderer>();
            dripRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            dripRenderer.velocityScale = 0.02f;
            dripRenderer.lengthScale = 1.2f;

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

            voices = new AudioPool(transform, 3);
            scoopClip = ScoopSound();
            dropsClip = DropsSound();
            var sloshGo = new GameObject("Slosh");
            sloshGo.transform.SetParent(transform, false);
            slosh = sloshGo.AddComponent<AudioSource>();
            slosh.clip = SloshSound();
            slosh.loop = true;
            slosh.playOnAwake = false;
            slosh.spatialBlend = 1f;
            slosh.minDistance = 0.4f;
            slosh.dopplerLevel = 0f;
            slosh.volume = 0f;
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
            holding = false;
            holdTimer = letGo = holdWeight = liftVelocity = 0f;
            moundHeight[0] = moundHeight[1] = 0f;
        }

        protected override void OnExit()
        {
#if UNITY_EDITOR
            previewBeads = false;
#endif
            Release(true);
            holdWeight = 0f;
            moundHeight[0] = moundHeight[1] = 0f;
            if (surface != null) surface.gameObject.SetActive(false);
            if (caustics != null) caustics.gameObject.SetActive(false);
            drips.Clear();
            pouring[0] = pouring[1] = false;
            if (loose != null) loose.Clear();
            if (handDrips != null) handDrips.Clear();
            if (streams != null) foreach (var s in streams) s.SetVisible(false);
            room?.Hide();
            cutoff = 22000f;
            if (lowPass != null) lowPass.enabled = false;
            if (splashes != null) splashes.Clear();
            if (slosh != null)
            {
                slosh.volume = 0f;
                slosh.Stop();
            }
        }

        /// <summary>
        /// The pool sits in front of you at table height, or on your real table once the room is scanned. A height you
        /// chose by lifting it stays, as a height over the table or floor, as long as it still makes sense where you are
        /// now: over the same kind of ground, and inside the comfortable range for your eyes' height.
        /// </summary>
        protected override void OnPlace(Camera view)
        {
            Release(true);
            var forward = Effects.FlatForward(view);
            float floor = Effects.FloorY(view);
            var center = view.transform.position + forward * distance + Vector3.down * belowEyes;
            center.y = Mathf.Max(center.y, floor + 0.45f);
            // With a room scan, the pool fills a shallow tray on the real table in front of you.
            onTable = Room.TableAhead(view.transform.position, forward, 1.1f, size.y * 0.5f, out _, out var spot);
            if (onTable) center = spot + Vector3.up * TableRest;
            groundY = onTable ? spot.y : floor;
            restY = center.y;
            if (chosen && chosenOnTable == onTable)
            {
                float y = groundY + chosenAbove;
                if (y >= Lowest - 0.005f && y <= Highest(view) + 0.005f) center.y = y;
            }
            surface.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
            surface.gameObject.SetActive(true);
            // The water's light falls on the table under it, or on the floor right below it.
            float lit = onTable ? spot.y + 0.003f : floor + 0.004f;
            litPoint = new Vector3(center.x, lit, center.z);
            caustics.transform.SetPositionAndRotation(litPoint, Quaternion.LookRotation(Vector3.down, forward));
            causticSpread = onTable ? 1.1f : 1.4f;
            caustics.transform.localScale = new Vector3(size.x * causticSpread, size.y * causticSpread, 1f);
            // On the floor, the room's own floor carries this light on outwards, so the patch would only double it.
            caustics.gameObject.SetActive(onTable || room == null);
            room?.Place(view.transform.position, forward, floor);
        }

        /// <summary>
        /// How far down (m, below the surface's rest) the water may dip before it meets the table or floor under it: a dent
        /// in a pool resting on a table bottoms out on the tray instead of sinking into the table.
        /// </summary>
        float TrayBottom => groundY + 0.004f - surface.position.y;

        /// <summary>The lowest the pool goes: a little above the floor, or resting on its table.</summary>
        float Lowest => groundY + (onTable ? TableRest : floorClearance);

        /// <summary>The highest the pool goes: well below the eyes, so it never comes near your face.</summary>
        float Highest(Camera view) => Mathf.Max(view.transform.position.y - eyeClearance, Lowest);

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
            Lift(rig, view, dt);
            HandSkin.FocusPosition = surface.position;
            HandSkin.FocusStrength = FadeIn;

            if (music.Upcoming != null && music.Upcoming.Count > 0) SpawnFromNotes(music);
            else SpawnFromAnalysis(music, dt);
            LandDroplets();

            float dip = TouchWithHands(rig, dt);
            Pour(rig, dt);
            Simulate(dt);
            UpdateMesh(dt);
            Muffle(music.Bus, dip, dt);
            DrawBeads();
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
        /// and with how stirred the water is, never jumping. It rises with the pool when you lift it, and the patch on
        /// the table spreads out and dims the higher the pool is held over it. Also the water's own shading.
        /// </summary>
        void Shine(float dt)
        {
            stir *= Mathf.Exp(-dt * 1.5f);
            float want = 0.2f + bassSlow * 0.1f + Mathf.Min(stir, 1f) * 0.22f + holdWeight * 0.05f;
            lightLevel = Mathf.Lerp(lightLevel, want, 1f - Mathf.Exp(-2.5f * dt));
            float light = lightLevel * FadeIn;
            float raised = Mathf.Max(0f, surface.position.y - restY);
            float spread = causticSpread + raised * 1.5f;
            caustics.transform.localScale = new Vector3(size.x * spread, size.y * spread, 1f);
            float thin = causticSpread / spread;
            block.Clear();
            block.SetFloat(StrengthId, light * thin * thin * 1.4f);
            block.SetFloat(ClockId, Now);
            block.SetFloat(SharpnessId, 6f);
            block.SetFloat(FillId, 0.12f);
            block.SetFloat(DriftId, 0.5f);
            caustics.SetPropertyBlock(block);
            if (room != null)
            {
                room.SetLight(litPoint + Vector3.up * raised, light);
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
            for (int s = 0; s < 2; s++)
            {
                var at = surface.TransformPoint(new Vector3(moundAt[s].x, 0f, moundAt[s].y));
                block.SetVector(s == 0 ? PalmLId : PalmRId, new Vector4(at.x, at.y, at.z, moundHeight[s] / MoundHeight * 0.6f));
            }
            surfaceRenderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// Holding the pool. Both hands cupped under the water (palms roughly up, not pinching, inside the pool) for a
        /// moment pick it up: from then on it follows the height of your palms, keeping the gap it had over them, eased
        /// and never faster than liftSpeed, between Lowest and Highest. Taking the hands away lets it go where it is:
        /// out past its sides, apart, palms turned down, a pinch, out of the water or far below it. A pool lifted above
        /// its usual height also sinks away gently if your eyes come down near it (you sat down, say).
        /// </summary>
        void Lift(HandRig rig, Camera view, float dt)
        {
            bool menu = MenuOpen;
            bool both = false;
            if (rig != null && !menu)
            {
                bool left = Cupped(rig.Left, 0);
                bool right = Cupped(rig.Right, 1);
                both = left && right;
            }
            float gap = both ? Vector3.Distance(palmAt[0], palmAt[1]) : 0f;
            if (holding)
            {
                bool keep = both && gap < holdGap + 0.18f;
                letGo = keep ? 0f : letGo + dt;
                if (menu || letGo > 0.15f) Release(false);
            }
            else
            {
                bool ready = both && gap < 0.6f;
                holdTimer = ready ? holdTimer + dt : 0f;
                if (ready && holdTimer >= holdDelay) PickUp(gap);
            }

            float y = surface.position.y, target = y;
            if (holding && letGo == 0f)
            {
                Acted("hold the pool");
                target = Mathf.Clamp((palmAt[0].y + palmAt[1].y) * 0.5f + holdOffset, Lowest, Highest(view));
            }
            else if (y > restY + 0.01f && y > Highest(view))
                target = Highest(view);
            float eased = Mathf.Lerp(y, target, 1f - Mathf.Exp(-liftFollow * dt));
            float step = Mathf.Clamp(eased - y, -liftSpeed * dt, liftSpeed * dt);
            if (step != 0f)
            {
                var p = surface.position;
                p.y += step;
                surface.position = p;
            }
            liftVelocity = Mathf.Lerp(liftVelocity, dt > 0f ? step / dt : 0f, 1f - Mathf.Exp(-10f * dt));

            // The mounds over the palms rise and settle softly, swelling a little as the pool is lifted.
            holdWeight = Mathf.MoveTowards(holdWeight, holding ? 1f : 0f, dt * 2f);
            float press = 1f + Mathf.Clamp(liftVelocity * 0.8f, -0.25f, 0.35f);
            for (int s = 0; s < 2; s++)
            {
                if (holding)
                {
                    var local = surface.InverseTransformPoint(palmAt[s]);
                    moundAt[s] = Vector2.Lerp(moundAt[s], new Vector2(local.x, local.z), 1f - Mathf.Exp(-12f * dt));
                }
                moundHeight[s] = MoundHeight * holdWeight * press;
            }
            if (holding && letGo == 0f && rig != null)
                for (int s = 0; s < 2; s++) DripFrom(rig.Get(s == 0), s, 8f + Mathf.Abs(liftVelocity) * 30f, dt);

            if (slosh != null && Application.isPlaying)
            {
                float want = holding ? 0.04f + Mathf.Min(Mathf.Abs(liftVelocity) / liftSpeed, 1f) * 0.2f : 0f;
                slosh.volume = Mathf.MoveTowards(slosh.volume, want, dt * 0.8f);
                slosh.transform.position = surface.position;
                if (slosh.volume > 0.001f && !slosh.isPlaying) slosh.Play();
                else if (slosh.volume <= 0.001f && slosh.isPlaying) slosh.Stop();
            }
        }

        /// <summary>
        /// Whether a hand is cupped under the water, ready to hold the pool (or, while it does, still holding it), with
        /// its palm in <see cref="palmAt"/>. Once holding, a hand may tip, drift out past the edge or come up near the
        /// surface a little before it counts as gone.
        /// </summary>
        bool Cupped(HandRig.Hand hand, int side)
        {
            if (!hand.IsTracked || hand.InSystemGesture) return false;
            palmAt[side] = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
            if (Vector3.Dot(hand.PalmNormal, Vector3.up) < (holding ? 0.1f : 0.45f)) return false;
            // A pinch pours instead, and a fist holds no water.
            if (hand.Pinch > (holding ? 0.75f : 0.5f) || hand.Fist > 0.85f) return false;
            var local = surface.InverseTransformPoint(palmAt[side]);
            float margin = holding ? 0.04f : -0.01f;
            if (Mathf.Abs(local.x) > size.x * 0.5f + margin || Mathf.Abs(local.z) > size.y * 0.5f + margin) return false;
            float depth = -local.y;
            return holding ? depth > -0.1f && depth < 0.32f : depth > 0.01f && depth < 0.25f;
        }

        /// <summary>Both hands take the pool: the water swells up around the palms with a soft scooping sound.</summary>
        void PickUp(float gap)
        {
            holding = true;
            letGo = 0f;
            holdGap = gap;
            holdOffset = surface.position.y - (palmAt[0].y + palmAt[1].y) * 0.5f;
            for (int s = 0; s < 2; s++)
            {
                var local = surface.InverseTransformPoint(palmAt[s]);
                moundAt[s] = new Vector2(local.x, local.z);
                Disturb(new Vector2(local.x / size.x + 0.5f, local.z / size.y + 0.5f), 0.05f, 0.004f);
            }
            Acted("lift the pool");
            voices?.Play(scoopClip, surface.position, 0.3f);
        }

        /// <summary>
        /// The hands let the pool go and it stays where it is: that height is remembered, over the table or floor under
        /// it, for when the world sets itself up again. Unless <paramref name="quiet"/>, the last water runs off the hands.
        /// </summary>
        void Release(bool quiet)
        {
            if (!holding) return;
            holding = false;
            holdTimer = letGo = 0f;
            float y = surface.position.y;
            chosen = Mathf.Abs(y - restY) > 0.01f;
            chosenAbove = y - groundY;
            chosenOnTable = onTable;
            if (quiet) return;
            var rig = Rig;
            if (rig != null)
                for (int s = 0; s < 2; s++)
                    if (rig.Get(s == 0).IsTracked) DripFrom(rig.Get(s == 0), s, 0f, 0f, 6);
            voices?.Play(dropsClip, surface.position, 0.2f);
        }

        /// <summary>
        /// Drops run off the edges of a hand holding the pool and fall to the floor or table under it: a few a second,
        /// more while it moves, plus <paramref name="extra"/> at once.
        /// </summary>
        void DripFrom(HandRig.Hand hand, int side, float rate, float dt, int extra = 0)
        {
            if (handDrips == null || !hand.IsTracked) return;
            dripDebt[side] += rate * dt * holdWeight + extra;
            var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
            while (dripDebt[side] >= 1f)
            {
                dripDebt[side] -= 1f;
                Vector3 at;
                switch (dripTurn[side]++ % 4)
                {
                    case 0: at = Tip(hand, 4); break;
                    case 1: at = Tip(hand, 0); break;
                    // The heel of the palm, and its outer edge below the little finger.
                    case 2: at = palm + (palm - Tip(hand, 2)).normalized * 0.04f; break;
                    default: at = palm + (Tip(hand, 4) - Tip(hand, 0)).normalized * 0.04f; break;
                }
                at += Random.insideUnitSphere * 0.006f + Vector3.down * 0.01f;
                float fall = Mathf.Max(at.y - groundY, 0.05f);
                handDrips.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = new Vector3(Random.Range(-0.03f, 0.03f), -0.06f + liftVelocity * 0.5f, Random.Range(-0.03f, 0.03f)),
                    startColor = DripColor,
                    startSize = Random.Range(0.004f, 0.0065f),
                    startLifetime = Mathf.Clamp(Mathf.Sqrt(2f * fall / 9.81f), 0.2f, 0.9f)
                }, 1);
            }
        }

        static Vector3 Tip(HandRig.Hand hand, int finger) => hand.TipJoints[finger] != null ? hand.TipJoints[finger].position : hand.Tips[finger];

        /// <summary>How tall the water mounds over the holding palms at a point on the pool (metres across it).</summary>
        float Mound(float x, float z)
        {
            float h = 0f;
            const float r2 = MoundRadius * MoundRadius;
            for (int s = 0; s < 2; s++)
            {
                if (moundHeight[s] <= 0f) continue;
                float dx = x - moundAt[s].x, dz = z - moundAt[s].y;
                float d2 = dx * dx + dz * dz;
                if (d2 < r2 * 9f) h += moundHeight[s] * Mathf.Exp(-d2 / r2);
            }
            return h;
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
        /// surface splashes. Hands holding the pool up part no water: it mounds over them instead (see Lift). Returns
        /// how deep (0-1) the deepest hand is, for muffling the music.
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
                        if (!holding) Disturb(spot, radius, -Mathf.Min(0.004f, depth * 0.05f + speed * 0.004f) * dt * 30f);
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
                bool now = hand != null && hand.IsTracked && hand.Pinch > 0.8f && !hand.InSystemGesture && PinchFree(side == 0);
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
        /// too, and ride the mounds over palms holding the pool. Their colour stays in a narrow band of water blues and
        /// teals that drifts slowly across the pool, paler over the palms. Their light is calm between beats (only a
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
                    // The water's own ripples, from hands and drops, and the mounds over palms holding the pool.
                    float ripple = height[Nearest(spot)];
                    float mound = Mound(px, pz) * edge;
                    float crest = Mathf.Clamp01(0.5f + swell / Mathf.Max(swellHeight, 1e-4f) * 0.5f);
                    float ring = RingGlow(Mathf.Sqrt(px * px + pz * pz));
                    float held = mound / MoundHeight;
                    var local = new Vector3(px, Mathf.Max(swell + ripple + mound, bottom) + 0.002f, pz);
                    float diameter = BeadSize * (0.92f + crest * 0.06f + pulse * 0.14f + ring * 0.2f + held * 0.1f) * edge * FadeIn;
                    beadMatrices[i] = Matrix4x4.TRS(surface.TransformPoint(local), spin * beadTurn[i], Vector3.one * diameter);
                    // A slow tide of colour, blue to teal, drifting across the pool over many seconds.
                    float tide = 0.5f + 0.5f * Mathf.Sin(rest.x * 6f + rest.y * 4f + t * 0.3f + Mathf.Sin(rest.y * 7f - t * 0.2f));
                    var c = Color.Lerp(Color.Lerp(BeadBlue, BeadTeal, tide), BeadLight, Mathf.Min(held, 1f) * 0.5f);
                    float glow = 0.14f + crest * 0.05f + pulse * 0.3f + ring * 0.32f + held * 0.3f;
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
        /// with the bass, and the mounds over palms holding the pool, and rebuilds the normals. The swell eases with the
        /// slow bass rather than jumping on every kick.
        /// </summary>
        void UpdateMesh(float dt)
        {
            float t = Now;
            swellHeight = Mathf.Lerp(swellHeight, 0.007f + bassSlow * 0.009f + pulse * 0.002f, 1f - Mathf.Exp(-3f * dt));
            bool mounds = moundHeight[0] > 0f || moundHeight[1] > 0f;
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
                    if (mounds) swell += Mound(vertices[i].x, vertices[i].z);
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

        /// <summary>Hands taking the pool: a soft, low bubble gliding up, inside a dark swish of moving water.</summary>
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

        /// <summary>The pool let go: the last two drops falling off the hands into it.</summary>
        static AudioClip DropsSound()
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
            return MakeClip("pool drops", data, rate, 0.45f);
        }

        /// <summary>
        /// Water sloshing in the hands, looped while you hold the pool: dark, gently rolling noise with a few small
        /// bubbles in it. It joins up with itself, so it loops without a click.
        /// </summary>
        static AudioClip SloshSound()
        {
            int rate = Rate, n = rate * 2, fade = rate / 10;
            var noise = new float[n + fade];
            var random = new System.Random(5);
            float low = 0f, lower = 0f;
            for (int i = 0; i < noise.Length; i++)
            {
                low += ((float)(random.NextDouble() * 2 - 1) - low) * 0.06f;
                lower += (low - lower) * 0.08f;
                noise[i] = lower;
            }
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                // The end runs on into the start (equal-power crossfade), so the loop has no seam.
                float s = i < fade
                    ? noise[i] * Mathf.Sqrt(i / (float)fade) + noise[n + i] * Mathf.Sqrt(1f - i / (float)fade)
                    : noise[i];
                // Rolling: whole cycles over the loop, so it joins up.
                float roll = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 1.5f * t)) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2.5f * t + 1f));
                s *= roll * 9f;
                // A few small bubbles.
                for (int b = 0; b < 4; b++)
                {
                    float age = t - (0.2f + b * 0.43f);
                    if (age < 0f || age > 0.06f) continue;
                    float f = 480f + b * 130f + 500f * age / 0.06f;
                    s += Mathf.Sin(2f * Mathf.PI * f * age) * Mathf.Exp(-age * 60f) * 0.25f;
                }
                data[i] = s;
            }
            return MakeClip("pool slosh", data, rate, 0.5f);
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
