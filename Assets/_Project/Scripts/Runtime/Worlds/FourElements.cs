using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Four elements world: your bare hands command fire, water, earth and air, one element at a time. Now and then
    /// the element changes by itself with the music, at the start of a sung line and never to the same one twice; clench
    /// both fists for a moment to call up the next one yourself. Each change bursts out in the new element's colours,
    /// dresses your hands for it, and a short caption names it and says what to do.
    /// <list type="bullet">
    /// <item><b>Fire</b>: flames rise from your open palms and flare on every beat. Push a hand forward quickly to throw
    /// a fireball that flies across the room and bursts into sparks where it lands.</item>
    /// <item><b>Water</b>: a stream of water circles each hand and follows it through the air like a ribbon of liquid,
    /// shedding drops as it whips round. Hold your palms facing each other and a wobbling ball of water forms between
    /// them, rippling with the bass; pull your hands apart and it splashes.</item>
    /// <item><b>Earth</b>: rocks push up out of the floor around you (or out of your table). Lift your palms, palms up, to
    /// raise the rocks in front of that hand; push down, palms down, to sink them. They rumble and shake on the beat, the
    /// floor glows through its cracks around them, and pebbles hover over your palms.</item>
    /// <item><b>Air</b>: currents of wind swirl around your hands. Move a hand round in circles to spin up a whirlwind
    /// on the floor in front of you that keeps turning for a while after you stop. Every beat sends a gust through.</item>
    /// </list>
    /// The sung words are carried by the element: glowing embers rising from the flames, floating in bubbles of water,
    /// carved into the rocks, or blown along on the wind.
    /// </summary>
    public class FourElements : World
    {
        const int Fire = 0, Water = 1, Earth = 2, Air = 3;
        const int StreamPoints = 40;
        const int CurrentPoints = 18;
        const int CurrentsPerHand = 2;
        const int StrandPoints = 34;
        const int TrailPoints = 14;
        const int Strands = 7;
        const int WhirlMotes = 140;
        const int GustMotes = 24;
        const int RoomMotes = 40;
        const int PebblesPerHand = 4;
        // Where a rock's flat front face is cut, in its mesh (radius 0.5): the words are carved there.
        const float FrontFace = 0.22f;

        public override string DisplayName => "Four elements";

        public override Color Color => new Color(1f, 0.56f, 0.36f);

        public override int MenuOrder => 118;

        public override int MenuIcon => 28;

        public override string Hint => "Play the element with your hands; clench both fists to switch";

        public override HandTheme Theme => Themes[current];

        static readonly string[] Names = { "Fire", "Water", "Earth", "Air" };

        static readonly string[] HowTo =
        {
            "Push a palm forward to throw a fireball",
            "Sweep your hands; palms facing make an orb",
            "Lift palms up to raise rocks, push down to sink",
            "Circle a hand to spin up a whirlwind"
        };

        // Each element's colours: its main colour, a second one and a bright one, for bursts, words and lights.
        static readonly Color[] MainColors =
        {
            new Color(1f, 0.45f, 0.12f), new Color(0.25f, 0.66f, 1f), new Color(0.78f, 0.56f, 0.3f), new Color(0.75f, 0.92f, 1f)
        };
        static readonly Color[] SecondColors =
        {
            new Color(1f, 0.76f, 0.28f), new Color(0.45f, 0.95f, 1f), new Color(0.6f, 0.74f, 0.4f), new Color(0.58f, 0.84f, 0.98f)
        };
        static readonly Color[] BrightColors =
        {
            new Color(1f, 0.93f, 0.7f), new Color(0.86f, 0.98f, 1f), new Color(1f, 0.82f, 0.5f), new Color(1f, 1f, 1f)
        };
        static readonly Color DustColor = new Color(0.78f, 0.64f, 0.46f, 0.55f);

        // The note each element plays as it arrives (MIDI), fitted to the song by Musical, with a fifth above it.
        static readonly int[] Chords = { 69, 64, 57, 72 };

        // How the hands look with each element: embers crackling over the skin, ripples, grains of glowing mineral in
        // stone-brown skin, and wind streaming over them.
        static readonly HandTheme[] Themes =
        {
            new HandTheme
            {
                Fill = new Color(0.25f, 0.08f, 0.04f, 0.5f), Edge = new Color(1f, 0.55f, 0.22f), Glow = new Color(1f, 0.85f, 0.4f),
                Pattern = HandTheme.Effect.Electric, PatternColor = new Color(1f, 0.62f, 0.25f)
            },
            new HandTheme
            {
                Fill = new Color(0.04f, 0.14f, 0.3f, 0.5f), Edge = new Color(0.45f, 0.8f, 1f), Glow = new Color(0.7f, 0.95f, 1f),
                Pattern = HandTheme.Effect.Ripples, PatternColor = new Color(0.55f, 0.88f, 1f)
            },
            new HandTheme
            {
                Fill = new Color(0.16f, 0.11f, 0.07f, 0.58f), Edge = new Color(0.86f, 0.64f, 0.36f), Glow = new Color(1f, 0.76f, 0.38f),
                Pattern = HandTheme.Effect.Specks, PatternColor = new Color(1f, 0.68f, 0.32f)
            },
            new HandTheme
            {
                Fill = new Color(0.12f, 0.16f, 0.2f, 0.4f), Edge = new Color(0.8f, 0.95f, 1f), Glow = new Color(0.85f, 1f, 1f),
                Pattern = HandTheme.Effect.Flow, PatternColor = new Color(0.85f, 0.97f, 1f)
            }
        };

        [Header("Materials")]
        [WorldMaterial("FourElementsFlame")]
        [SerializeField] Material flameMaterial;
        [WorldMaterial("FourElementsStream")]
        [SerializeField] Material streamMaterial;
        [WorldMaterial("FourElementsOrb")]
        [SerializeField] Material orbMaterial;
        [WorldMaterial("FourElementsRock")]
        [SerializeField] Material rockMaterial;
        [WorldMaterial("FourElementsCrack")]
        [SerializeField] Material crackMaterial;
        [SerializeField] Material lineMaterial;
        [SerializeField] Material ringMaterial;
        [SerializeField] Material bubbleMaterial;

        [Header("Changing element")]
        [Tooltip("Beats an element lasts before it changes by itself, at the start of the next sung line (or eight beats later in songs without lyrics).")]
        [SerializeField] int beatsPerElement = 32;
        [Tooltip("Beats an element you called up with your fists lasts before it changes by itself.")]
        [SerializeField] int beatsAfterPick = 64;
        [Tooltip("Seconds both fists stay clenched to change the element.")]
        [SerializeField] float fistSeconds = 0.35f;

        [Header("Fire")]
        [Tooltip("Height of the flame on an open palm (m).")]
        [SerializeField] float flameHeight = 0.16f;
        [Tooltip("Speed (m/s) a hand must reach, moving away from you, to throw a fireball.")]
        [SerializeField] float throwSpeed = 1.3f;
        [SerializeField] float fireballSpeed = 3.2f;

        [Header("Water")]
        [Tooltip("Width of the stream where it leaves the hand (m).")]
        [SerializeField] float streamWidth = 0.032f;
        [Tooltip("Distance between the palms (m) at which a ball of water forms between them, nearest and furthest.")]
        [SerializeField] Vector2 orbSpread = new Vector2(0.07f, 0.55f);

        [Header("Earth")]
        [SerializeField] int rockCount = 6;
        [Tooltip("How far (m) the palms lift, palms up, to raise a rock all the way; pushing down as far sinks it.")]
        [SerializeField] float liftReach = 0.3f;
        [Tooltip("How far the rocks stand from you (m), nearest and furthest.")]
        [SerializeField] Vector2 rockDistance = new Vector2(1f, 1.35f);

        [Header("Air")]
        [Tooltip("Turns of a hand in a circle that spin a whirlwind up to full strength.")]
        [SerializeField] float turnsToSpin = 1.25f;
        [Tooltip("Specks of air swirling round each hand.")]
        [SerializeField] int windPerHand = 36;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int BendId = Shader.PropertyToID("_Bend");
        static readonly int FlareId = Shader.PropertyToID("_Flare");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int LengthId = Shader.PropertyToID("_Length");
        static readonly int BassId = Shader.PropertyToID("_Bass");
        static readonly int WaveId = Shader.PropertyToID("_Wave");
        static readonly int KickId = Shader.PropertyToID("_Kick");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int GroundId = Shader.PropertyToID("_Ground");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int InnerId = Shader.PropertyToID("_Inner");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int IconId = Shader.PropertyToID("_Icon");
        static readonly int CoreId = Shader.PropertyToID("_Core");

        sealed class Fireball
        {
            public Renderer Ball, Core;
            public Vector3 Position, Velocity;
            public float Age, Seed;
            public bool Flying;
            /// <summary>Seconds since it burst, for the flash it leaves.</summary>
            public float Burst = 9f;
        }

        sealed class Rock
        {
            public Transform Transform;
            public Renderer Renderer, Crack;
            /// <summary>Where it stands on the ground, and the level way to it from where you stood.</summary>
            public Vector3 Base, Direction;
            public Quaternion Facing;
            /// <summary>Its angle from straight ahead (degrees, right positive), for which hand raises it.</summary>
            public float Angle;
            /// <summary>Its size where it stands (smaller on furniture near you) and at full size, and its cracked ground's.</summary>
            public float Width, Depth, Height, FullWidth, FullDepth, FullHeight, CrackSize;
            public float Ground, Seed;
            /// <summary>How far out of the ground it is (0 sunk, 1 all the way up), where it is heading, and how fast.</summary>
            public float Rise, Target, Speed;
            public float Shake, Glow;
            public Color Tint;
        }

        sealed class Whirl
        {
            public LineStrip[] Strands;
            public Vector3 Base, Top, Drift;
            /// <summary>0-1: how strongly it spins; it dies away at 0.</summary>
            public float Energy;
            /// <summary>1 while a circling hand drives it, falling to 0 once the hand lets it go.</summary>
            public float Driven;
            public float Spin;
            public bool Active;
        }

        // Which element is in charge, and how present each one is (0-1) while one fades over to the next.
        int current = Fire;
        readonly float[] presence = new float[4];
        int beatsSince, waitBeats = 32;
        float switchTime = -10f, lastAction = -10f;
        HandTheme themeFrom = Themes[Fire];
        float fistTime;
        bool fistArmed = true;

        MaterialPropertyBlock block;
        AudioPool voices;
        ParticleSystem motes, falling, wind;
        ParticleSystem.Particle[] windOut;
        int windCount;
        Vector3 head, forward = Vector3.forward, right = Vector3.right;
        float floorY, ambientDebt;
        bool menuOpen;

        // The hands this frame.
        readonly bool[] tracked = new bool[2];
        readonly bool[] gesture = new bool[2];
        readonly Vector3[] palm = new Vector3[2];
        readonly Vector3[] normal = { Vector3.back, Vector3.back };
        readonly Vector3[] pointing = { Vector3.up, Vector3.up };
        readonly Vector3[] velocity = new Vector3[2];
        readonly float[] fist = new float[2];

        // Changing element: the burst, the glow gathering between clenched fists, the caption.
        Renderer flash, ring, charge;
        float flashAge = 9f;
        Vector3 flashAt;
        Color flashColor;
        Transform captionRoot;
        TypeLabel captionTitle, captionLine;
        float captionAge = 99f;
        bool captionDue = true, captionPlaced;
        Color captionColor;

        // Fire.
        Renderer[] flames, palmLights;
        readonly float[] flameLevel = new float[2];
        readonly float[] emberDebt = new float[2];
        readonly float[] throwWait = new float[2];
        Fireball[] fireballs;
        bool previewThrow;

        // Water.
        LineStrip[] streams;
        Vector3[][] chains, before;
        Vector3[] path;
        Color[] pathColors;
        float[] pathWidths;
        readonly float[] streamLevel = new float[2];
        readonly float[] streamPhase = new float[2];
        readonly bool[] streamFresh = { true, true };
        Transform orb;
        Renderer orbRenderer;
        float orbLevel, orbRadius = 0.08f, orbWave = 9f;
        Vector3 orbCentre;
        bool orbHeld, orbSounded;
        Renderer[] bubbles;

        // Earth.
        Rock[] rocks;
        Transform[] pebbles;
        Renderer[] pebbleRenderers;
        readonly float[] pebbleLevel = new float[2];
        float rumbleWait, previewLift = -1f;
        bool raiseRocks = true;

        // Air.
        LineStrip[] currents, trails;
        Whirl[] whirls;
        readonly float[] airLevel = new float[2];
        readonly float[] airPhase = new float[2];
        readonly Vector3[] circleCentre = new Vector3[2];
        readonly Vector3[] spinAxis = new Vector3[2];
        readonly float[] turned = new float[2];
        readonly bool[] wasTracked = new bool[2];
        float[] windRadius, windHeight, windAngle, windSpeed;
        float[] whirlU, whirlAngle, whirlReach;
        Vector3[] gustFrom, gustVelocity;
        float[] gustAge;
        int nextGust;
        float gust, windSign = 1f, roomPhase;
        bool previewWhirl;

        // Per sung word (by its place in SungWords): where it came from and where it goes, by element.
        Vector3[] wordAnchor, wordAim;
        float[] wordStart, wordEnd, wordLift;
        int[] wordRock;
        bool[] popped;

        static AudioClip whoosh, crackle, splash, rumble;

        protected override int SungWordCount => 6;

        protected override float SungWordHeight => 0.038f;

        protected override float SungWordsAlpha => FadeIn;

        // ---- Building ----

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            voices = new AudioPool(transform, 6);
            MakeSounds();
            // Over the bubbles, flames and water, with a firm dark edge, so the words stay readable.
            if (SungWords != null)
            {
                SungWords.RenderQueue = 3200;
                SungWords.Rim = 0.7f;
            }

            motes = Effects.MakeSparkSystem("Motes", transform, glowMaterial);
            var main = motes.main;
            main.maxParticles = 1000;
            main.gravityModifier = -0.015f;
            Drag(motes, 1.4f);
            falling = Effects.MakeSparkSystem("Falling", transform, glowMaterial);
            main = falling.main;
            main.maxParticles = 700;
            main.gravityModifier = 0.5f;
            Drag(falling, 0.5f);
            var streaks = falling.GetComponent<ParticleSystemRenderer>();
            streaks.renderMode = ParticleSystemRenderMode.Stretch;
            streaks.velocityScale = 0.035f;
            streaks.lengthScale = 1.4f;
            int windMax = 2 * windPerHand + 2 * WhirlMotes + GustMotes + RoomMotes;
            wind = MakeManualSystem("Wind", windMax);
            windOut = new ParticleSystem.Particle[windMax];

            flash = Effects.MakeQuad("Change Flash", transform, glowMaterial);
            ring = Effects.MakeQuad("Change Ring", transform, ringMaterial);
            charge = Effects.MakeQuad("Fist Charge", transform, glowMaterial);
            Show(flash, false);
            Show(ring, false);
            Show(charge, false);
            captionRoot = new GameObject("Caption").transform;
            captionRoot.SetParent(transform, false);
            captionTitle = new TypeLabel("Caption Title", captionRoot, 0.026f);
            captionLine = new TypeLabel("Caption Line", captionRoot, 0.017f);
            captionRoot.gameObject.SetActive(false);

            path = new Vector3[Mathf.Max(StreamPoints, StrandPoints)];
            pathColors = new Color[path.Length];
            pathWidths = new float[path.Length];
            int words = SungWordCount;
            wordAnchor = new Vector3[words];
            wordAim = new Vector3[words];
            wordStart = new float[words];
            wordEnd = new float[words];
            wordLift = new float[words];
            wordRock = new int[words];
            popped = new bool[words];

            BuildFire();
            BuildWater();
            BuildEarth();
            BuildAir();
            waitBeats = beatsPerElement;
        }

        void BuildFire()
        {
            flames = new Renderer[2];
            palmLights = new Renderer[2];
            // The fire's light on the palm: a soft glow without a white core that shows over the hand, so the curve of
            // the palm and fingers never cuts it off.
            var warmth = glowMaterial;
            if (glowMaterial != null)
            {
                warmth = new Material(glowMaterial) { name = "Palm Light" };
                warmth.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                warmth.SetFloat(CoreId, 0f);
            }
            for (int s = 0; s < 2; s++)
            {
                palmLights[s] = Effects.MakeQuad(s == 0 ? "Palm Light L" : "Palm Light R", transform, warmth);
                flames[s] = Effects.MakeQuad(s == 0 ? "Flame L" : "Flame R", transform, flameMaterial);
                Show(palmLights[s], false);
                Show(flames[s], false);
            }
            fireballs = new Fireball[4];
            for (int i = 0; i < fireballs.Length; i++)
            {
                var f = new Fireball
                {
                    Core = Effects.MakeQuad($"Fireball Core {i}", transform, glowMaterial),
                    Ball = Effects.MakeQuad($"Fireball {i}", transform, flameMaterial),
                    Seed = i * 7.31f
                };
                Show(f.Core, false);
                Show(f.Ball, false);
                fireballs[i] = f;
            }
        }

        void BuildWater()
        {
            streams = new LineStrip[2];
            chains = new Vector3[2][];
            before = new Vector3[2][];
            for (int s = 0; s < 2; s++)
            {
                streams[s] = new LineStrip(s == 0 ? "Stream L" : "Stream R", transform, streamMaterial, StreamPoints);
                streams[s].SetVisible(false);
                chains[s] = new Vector3[StreamPoints];
                before[s] = new Vector3[StreamPoints];
            }
            var go = new GameObject("Water Orb");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = BuildBall(14);
            orbRenderer = go.AddComponent<MeshRenderer>();
            orbRenderer.sharedMaterial = orbMaterial;
            orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            orbRenderer.receiveShadows = false;
            orb = go.transform;
            go.SetActive(false);
            bubbles = new Renderer[SungWordCount];
            for (int i = 0; i < bubbles.Length; i++)
            {
                bubbles[i] = Effects.MakeQuad($"Word Bubble {i}", transform, bubbleMaterial);
                Show(bubbles[i], false);
            }
        }

        void BuildEarth()
        {
            rocks = new Rock[Mathf.Max(1, rockCount)];
            var random = new System.Random(23);
            for (int i = 0; i < rocks.Length; i++)
            {
                var go = new GameObject($"Rock {i}");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = BuildRock(31 + i * 17, FrontFace);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = rockMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                float width = Mathf.Lerp(0.3f, 0.44f, (float)random.NextDouble());
                float shade = Mathf.Lerp(0.85f, 1.1f, (float)random.NextDouble());
                rocks[i] = new Rock
                {
                    Transform = go.transform,
                    Renderer = r,
                    Crack = Effects.MakeQuad($"Rock Cracks {i}", transform, crackMaterial),
                    Seed = (float)random.NextDouble(),
                    FullWidth = width,
                    FullDepth = width * Mathf.Lerp(0.78f, 0.95f, (float)random.NextDouble()),
                    FullHeight = Mathf.Lerp(0.6f, 0.95f, (float)random.NextDouble()),
                    Tint = new Color(shade, shade * Mathf.Lerp(0.95f, 1.02f, (float)random.NextDouble()), shade * Mathf.Lerp(0.9f, 1f, (float)random.NextDouble()), 1f)
                };
                rocks[i].Width = rocks[i].FullWidth;
                rocks[i].Depth = rocks[i].FullDepth;
                rocks[i].Height = rocks[i].FullHeight;
                rocks[i].CrackSize = Mathf.Max(rocks[i].Width, rocks[i].Depth) * 2.6f;
                go.SetActive(false);
                Show(rocks[i].Crack, false);
            }
            var stone = BuildRock(7, 0.5f);
            pebbles = new Transform[2 * PebblesPerHand];
            pebbleRenderers = new Renderer[pebbles.Length];
            for (int i = 0; i < pebbles.Length; i++)
            {
                var go = new GameObject($"Pebble {i}");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = stone;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = rockMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                pebbles[i] = go.transform;
                pebbleRenderers[i] = r;
                go.SetActive(false);
            }
        }

        void BuildAir()
        {
            // Soft lines of wind, without the usual white-hot core.
            var soft = new MaterialPropertyBlock();
            soft.SetFloat(CoreId, 0.12f);
            currents = new LineStrip[2 * CurrentsPerHand];
            for (int i = 0; i < currents.Length; i++)
            {
                currents[i] = new LineStrip($"Wind Current {i}", transform, lineMaterial, CurrentPoints);
                currents[i].Renderer.SetPropertyBlock(soft);
                currents[i].SetVisible(false);
            }
            trails = new LineStrip[SungWordCount];
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i] = new LineStrip($"Word Wind {i}", transform, lineMaterial, TrailPoints);
                trails[i].Renderer.SetPropertyBlock(soft);
                trails[i].SetVisible(false);
            }
            whirls = new Whirl[2];
            for (int i = 0; i < whirls.Length; i++)
            {
                var w = new Whirl { Strands = new LineStrip[Strands] };
                for (int k = 0; k < Strands; k++)
                {
                    w.Strands[k] = new LineStrip($"Whirlwind {i} Strand {k}", transform, lineMaterial, StrandPoints);
                    w.Strands[k].Renderer.SetPropertyBlock(soft);
                    w.Strands[k].SetVisible(false);
                }
                whirls[i] = w;
            }
            var random = new System.Random(41);
            int n = 2 * windPerHand;
            windRadius = new float[n];
            windHeight = new float[n];
            windAngle = new float[n];
            windSpeed = new float[n];
            for (int i = 0; i < n; i++)
            {
                windRadius[i] = Mathf.Lerp(0.055f, 0.1f, (float)random.NextDouble());
                windHeight[i] = Mathf.Lerp(-0.03f, 0.1f, (float)random.NextDouble());
                windAngle[i] = (float)random.NextDouble() * Mathf.PI * 2f;
                windSpeed[i] = Mathf.Lerp(0.75f, 1.3f, (float)random.NextDouble());
            }
            whirlU = new float[WhirlMotes];
            whirlAngle = new float[WhirlMotes];
            whirlReach = new float[WhirlMotes];
            for (int i = 0; i < WhirlMotes; i++)
            {
                whirlU[i] = Mathf.Pow((float)random.NextDouble(), 0.8f);
                whirlAngle[i] = (float)random.NextDouble() * Mathf.PI * 2f;
                whirlReach[i] = Mathf.Lerp(0.8f, 1.2f, (float)random.NextDouble());
            }
            gustFrom = new Vector3[GustMotes];
            gustVelocity = new Vector3[GustMotes];
            gustAge = new float[GustMotes];
            for (int i = 0; i < GustMotes; i++) gustAge[i] = 9f;
        }

        /// <summary>A particle system whose particles this world places itself every frame, stretched along their way.</summary>
        ParticleSystem MakeManualSystem(string name, int max)
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
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 1.2f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>Air resistance, so bursts fly out and then slow down and float.</summary>
        static void Drag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.dampen = 0f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        // ---- Starting, stopping and placing ----

        /// <summary>A fresh start, with an element picked at random.</summary>
        protected override void OnEnter()
        {
            current = Random.Range(0, 4);
            for (int e = 0; e < 4; e++) presence[e] = 0f;
            beatsSince = 0;
            waitBeats = beatsPerElement;
            switchTime = lastAction = -10f;
            themeFrom = Themes[current];
            fistTime = 0f;
            fistArmed = true;
            captionDue = true;
            captionAge = 99f;
            flashAge = 9f;
            orbLevel = 0f;
            orbHeld = orbSounded = false;
            gust = 0f;
            raiseRocks = true;
            for (int s = 0; s < 2; s++)
            {
                flameLevel[s] = streamLevel[s] = airLevel[s] = pebbleLevel[s] = turned[s] = throwWait[s] = 0f;
                streamFresh[s] = true;
                wasTracked[s] = false;
            }
            foreach (var f in fireballs)
            {
                f.Flying = false;
                f.Burst = 9f;
            }
            foreach (var w in whirls)
            {
                w.Active = false;
                w.Energy = 0f;
            }
            foreach (var r in rocks) r.Rise = r.Target = r.Speed = 0f;
        }

        protected override void OnExit()
        {
            for (int s = 0; s < 2; s++)
            {
                Show(flames[s], false);
                Show(palmLights[s], false);
                streams[s].SetVisible(false);
            }
            foreach (var f in fireballs)
            {
                f.Flying = false;
                Show(f.Ball, false);
                Show(f.Core, false);
            }
            orb.gameObject.SetActive(false);
            foreach (var b in bubbles) Show(b, false);
            foreach (var r in rocks)
            {
                Show(r.Renderer, false);
                Show(r.Crack, false);
            }
            foreach (var p in pebbles) p.gameObject.SetActive(false);
            foreach (var c in currents) c.SetVisible(false);
            foreach (var t in trails) t.SetVisible(false);
            foreach (var w in whirls)
            {
                w.Active = false;
                foreach (var strand in w.Strands) strand.SetVisible(false);
            }
            Show(flash, false);
            Show(ring, false);
            Show(charge, false);
            captionRoot.gameObject.SetActive(false);
            motes.Clear();
            falling.Clear();
            wind.Clear();
            HandSkin.FocusStrength = 0f;
            HandSkin.Charge[0] = HandSkin.Charge[1] = 0f;
        }

        /// <summary>The rocks stand in an arc in front of you; placed again (sunk, to rise anew) when you move.</summary>
        protected override void OnPlace(Camera view)
        {
            head = view.transform.position;
            forward = Effects.FlatForward(view);
            right = Vector3.Cross(Vector3.up, forward).normalized;
            floorY = Effects.FloorY(view);
            PlaceRocks();
            raiseRocks = true;
        }

        // ---- The music ----

        protected override void OnBeat(MusicFrame music)
        {
            beatsSince++;
            BeatFire(music);
            BeatWater();
            BeatEarth(music);
            BeatAir(music);
            // In songs without lyrics, or a long gap between lines, the element changes on the beat instead.
            if (beatsSince >= waitBeats + 8 && beatsSince % 4 == 0 && !Busy) ChangeByItself();
        }

        protected override void OnWord(string word, bool newLine)
        {
            // A new line is the moment for the element to change by itself; the line's words then belong to the new one.
            if (newLine && beatsSince >= waitBeats && !Busy) ChangeByItself();
            AddWord(word, newLine);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            var menu = WorldMenu.Instance;
            menuOpen = menu != null && menu.IsOpen;
            ReadHands(rig, dt);
            for (int e = 0; e < 4; e++)
                presence[e] = Mathf.MoveTowards(presence[e], e == current ? 1f : 0f, dt * (e == current ? 1.6f : 1.2f));
            Fists(dt);
            StepFire(music, view, dt);
            StepWater(music, view, dt);
            StepEarth(music, dt);
            StepAir(music, view, dt);
            Ambient(music, view, dt);
            ChangeEffects(view, dt);
            StepWords(music, view, dt);
            DressHands();
            StepCaption(view, dt);
        }

        void ReadHands(HandRig rig, float dt)
        {
            for (int s = 0; s < 2; s++)
            {
                var hand = rig != null ? rig.Get(s == 0) : null;
                bool was = tracked[s];
                tracked[s] = hand != null && hand.IsTracked;
                if (!tracked[s])
                {
                    velocity[s] = Vector3.zero;
                    gesture[s] = false;
                    continue;
                }
                palm[s] = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                if (hand.PalmNormal.sqrMagnitude > 0.5f) normal[s] = hand.PalmNormal;
                // Which way the hand points, wrist to fingers (kept from before while the fingers curl in).
                var tip = hand.TipJoints[2] != null ? hand.TipJoints[2].position : hand.Tips[2];
                var toTip = tip - palm[s];
                if (toTip.sqrMagnitude > 0.0025f) pointing[s] = toTip.normalized;
                fist[s] = hand.Fist;
                gesture[s] = hand.InSystemGesture;
                // Smoothed a little: one frame's tracking jitter is not a throw.
                velocity[s] = was ? Vector3.Lerp(velocity[s], hand.Velocity, 1f - Mathf.Exp(-22f * dt)) : Vector3.zero;
            }
        }

        // ---- Changing element ----

        /// <summary>True while you are in the middle of something with the element; it waits for you, up to a point.</summary>
        bool Busy => (Clock - lastAction < 2.5f || fistTime > 0f) && beatsSince < waitBeats * 2;

        /// <summary>Both fists clenched for a moment call up the next element; open a hand before the next change.</summary>
        void Fists(float dt)
        {
            bool both = tracked[0] && tracked[1] && !gesture[0] && !gesture[1] && !menuOpen;
            bool clenched = both && fist[0] > 0.8f && fist[1] > 0.8f;
            if (!tracked[0] || !tracked[1] || fist[0] < 0.5f || fist[1] < 0.5f) fistArmed = true;
            fistTime = clenched && fistArmed ? fistTime + dt : Mathf.MoveTowards(fistTime, 0f, dt * 3f);
            if (fistTime < fistSeconds) return;
            fistTime = 0f;
            fistArmed = false;
            Change((current + 1) % 4, true, (palm[0] + palm[1]) * 0.5f);
        }

        void ChangeByItself()
        {
            int next = Random.Range(0, 3);
            if (next >= current) next++;
            Change(next, false, BurstPoint());
        }

        /// <summary>Where a change bursts out when no fists called it: between your hands, or in front of you.</summary>
        Vector3 BurstPoint()
        {
            if (tracked[0] && tracked[1]) return (palm[0] + palm[1]) * 0.5f;
            if (tracked[0] || tracked[1]) return palm[tracked[0] ? 0 : 1];
            var eye = View != null ? View.transform.position : head;
            var ahead = View != null ? Effects.FlatForward(View) : forward;
            return eye + ahead * 0.5f + Vector3.down * 0.28f;
        }

        void Change(int next, bool picked, Vector3 at)
        {
            if (next == current) return;
            themeFrom = HandSkin.Theme;
            current = next;
            switchTime = Clock;
            beatsSince = 0;
            waitBeats = picked ? beatsAfterPick : beatsPerElement;
            if (picked) lastAction = Clock;
            Burst(next, at);
            ShowCaption(next);
            // The words still showing belong to the element that is leaving, and go with it.
            if (SungWords != null)
            {
                var words = SungWords.Words;
                for (int i = 0; i < words.Count; i++)
                    if (words[i].Active) words[i].Life = Mathf.Min(words[i].Life, words[i].Age + 0.8f);
            }
            if (next == Earth) raiseRocks = true;
            if (next == Water) streamFresh[0] = streamFresh[1] = true;
            Musical.Play(voices, Music, Chords[next], at, 0.28f);
            Musical.Play(voices, Music, Chords[next] + 7, at, 0.2f);
        }

        /// <summary>A short burst in the new element's colours: sparks, a splash of drops, dust and chips, or a swirl.</summary>
        void Burst(int e, Vector3 at)
        {
            flashAge = 0f;
            flashAt = at;
            flashColor = MainColors[e];
            switch (e)
            {
                case Fire:
                    for (int i = 0; i < 60; i++)
                    {
                        var d = AwayFromEyes(Random.onUnitSphere, at);
                        Emit(motes, at + d * 0.03f, d * Random.Range(0.6f, 1.4f) + Vector3.up * 0.2f, Pick(Fire), Random.Range(0.008f, 0.016f), Random.Range(0.6f, 1.1f));
                    }
                    for (int i = 0; i < 24; i++)
                        Emit(falling, at, AwayFromEyes(Random.onUnitSphere, at) * Random.Range(1f, 2f) + Vector3.up * 0.5f, BrightColors[Fire],
                            Random.Range(0.004f, 0.008f), Random.Range(0.5f, 0.9f));
                    break;
                case Water:
                    for (int i = 0; i < 70; i++)
                    {
                        float a = Random.value * Mathf.PI * 2f;
                        var outward = AwayFromEyes(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), at);
                        Emit(falling, at, outward * Random.Range(0.5f, 1.3f) + Vector3.up * Random.Range(0.5f, 1.3f), Pick(Water), Random.Range(0.005f, 0.011f), Random.Range(0.7f, 1.1f));
                    }
                    for (int i = 0; i < 24; i++)
                        Emit(motes, at + Random.insideUnitSphere * 0.05f, AwayFromEyes(Random.insideUnitSphere, at) * 0.5f, BrightColors[Water], Random.Range(0.004f, 0.008f),
                            Random.Range(0.6f, 1.2f));
                    break;
                case Earth:
                    for (int i = 0; i < 36; i++)
                        Emit(falling, at, AwayFromEyes(Random.onUnitSphere, at) * Random.Range(0.6f, 1.4f) + Vector3.up * 0.6f, Pick(Earth), Random.Range(0.009f, 0.017f),
                            Random.Range(0.6f, 1f));
                    for (int i = 0; i < 40; i++)
                        Emit(motes, at + Random.insideUnitSphere * 0.04f, AwayFromEyes(Random.onUnitSphere, at) * Random.Range(0.3f, 0.7f), DustColor, Random.Range(0.012f, 0.024f),
                            Random.Range(0.8f, 1.5f));
                    break;
                default:
                    for (int i = 0; i < 72; i++)
                    {
                        float a = i / 72f * Mathf.PI * 2f;
                        var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        var swirl = Vector3.Cross(Vector3.up, outward);
                        var spin = AwayFromEyes(outward * Random.Range(0.5f, 1.1f) + swirl * 0.8f, at);
                        Emit(motes, at + outward * 0.04f, spin + Vector3.up * Random.Range(-0.2f, 0.3f), Pick(Air), Random.Range(0.006f, 0.012f), Random.Range(0.6f, 1f));
                    }
                    gust = 1f;
                    break;
            }
        }

        /// <summary>The flash and ring of a change, and the next element gathering between clenched fists.</summary>
        void ChangeEffects(Camera view, float dt)
        {
            flashAge += dt;
            bool flashing = flashAge < 0.7f;
            Show(flash, flashing);
            Show(ring, flashing);
            if (flashing)
            {
                float u = flashAge / 0.7f;
                flash.transform.position = flashAt;
                flash.transform.localScale = Vector3.one * (0.14f + u * 0.5f);
                Effects.Face(flash.transform, view);
                block.Clear();
                block.SetColor(ColorId, flashColor);
                block.SetFloat(IntensityId, (1f - u) * (1f - u) * 0.75f);
                flash.SetPropertyBlock(block);
                ring.transform.position = flashAt;
                ring.transform.localScale = Vector3.one * 0.6f;
                Effects.Face(ring.transform, view);
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(flashColor, Color.white, 0.2f));
                block.SetFloat(RadiusId, 0.1f + Mathf.Sqrt(u) * 0.8f);
                block.SetFloat(ThicknessId, 0.025f + u * 0.02f);
                block.SetFloat(AlphaId, (1f - u) * 0.8f);
                ring.SetPropertyBlock(block);
            }

            float gathered = fistTime / fistSeconds;
            bool gathering = gathered > 0.01f && tracked[0] && tracked[1];
            Show(charge, gathering);
            if (!gathering) return;
            int next = (current + 1) % 4;
            var middle = (palm[0] + palm[1]) * 0.5f;
            charge.transform.position = middle;
            charge.transform.localScale = Vector3.one * (0.05f + 0.13f * gathered);
            Effects.Face(charge.transform, view);
            block.Clear();
            block.SetColor(ColorId, MainColors[next]);
            block.SetFloat(IntensityId, gathered * 0.9f);
            charge.SetPropertyBlock(block);
            if (Random.value < dt * 70f * gathered)
            {
                var d = Random.onUnitSphere;
                Emit(motes, middle + d * 0.14f, -d * 0.35f, Pick(next), 0.006f, 0.35f);
            }
        }

        void ShowCaption(int e)
        {
            captionTitle.Text = Names[e];
            captionLine.Text = HowTo[e];
            captionColor = Color.Lerp(Color.white, MainColors[e], 0.55f);
            // The world's own hint shows for its first few seconds: wait for it to go.
            captionAge = Mathf.Min(0f, Clock - 4.8f);
            captionPlaced = false;
            captionDue = false;
        }

        /// <summary>The element's name and what to do with it float in front of you for a few seconds after it arrives.</summary>
        void StepCaption(Camera view, float dt)
        {
            if (captionDue && Clock > 4.8f) ShowCaption(current);
            captionAge += dt;
            bool show = captionAge >= 0f && captionAge < 3.2f && !menuOpen;
            if (captionRoot.gameObject.activeSelf != show) captionRoot.gameObject.SetActive(show);
            if (!show) return;
            var ahead = Effects.FlatForward(view);
            var target = view.transform.position + ahead * 0.65f + Vector3.down * 0.17f;
            var rotation = Quaternion.LookRotation(ahead, Vector3.up);
            float follow = captionPlaced ? 1f - Mathf.Exp(-3f * dt) : 1f;
            captionRoot.SetPositionAndRotation(Vector3.Lerp(captionRoot.position, target, follow), Quaternion.Slerp(captionRoot.rotation, rotation, follow));
            captionPlaced = true;
            float alpha = Mathf.Clamp01(Mathf.Min(captionAge / 0.3f, (3.2f - captionAge) / 0.7f));
            captionTitle.Transform.localPosition = new Vector3(0f, 0.026f, 0f);
            captionLine.Transform.localPosition = new Vector3(0f, -0.008f, 0f);
            captionTitle.Color = new Color(captionColor.r, captionColor.g, captionColor.b, alpha);
            captionLine.Color = new Color(1f, 1f, 1f, alpha * 0.92f);
        }

        /// <summary>The hands take on the element's look, blending over from the last one, and its effect inputs.</summary>
        void DressHands()
        {
            var to = Themes[current];
            float k = Mathf.Clamp01((Clock - switchTime) / 0.6f);
            HandSkin.Theme = k >= 1f ? to : new HandTheme
            {
                Fill = Color.Lerp(themeFrom.Fill, to.Fill, k),
                Edge = Color.Lerp(themeFrom.Edge, to.Edge, k),
                Glow = Color.Lerp(themeFrom.Glow, to.Glow, k),
                PatternColor = Color.Lerp(themeFrom.PatternColor, to.PatternColor, k),
                Pattern = k < 0.5f ? themeFrom.Pattern : to.Pattern
            };
            HandSkin.FocusStrength = 0f;
            HandSkin.Charge[0] = HandSkin.Charge[1] = 0f;
            bool any = tracked[0] || tracked[1];
            var middle = tracked[0] && tracked[1] ? (palm[0] + palm[1]) * 0.5f : palm[tracked[0] ? 0 : 1];
            switch (current)
            {
                case Water:
                    // The palms light up with ripples as the ball of water forms between them.
                    for (int s = 0; s < 2; s++) HandSkin.Charge[s] = Mathf.Clamp01(orbLevel * 0.9f + streamLevel[s] * 0.15f) * presence[Water];
                    break;
                case Air:
                    if (!any) break;
                    // The wind streams over the skin from upwind.
                    HandSkin.FocusPosition = middle - right * (windSign * 0.5f) + Vector3.up * 0.05f;
                    HandSkin.FocusStrength = presence[Air] * (0.7f + gust * 0.3f);
                    break;
            }
        }

        // ---- Fire ----

        /// <summary>
        /// Flames on the open palms (taller with the palm up, leaning as the hand moves, flaring on the beat), embers
        /// rising from them, and a quick push away from you throws a fireball.
        /// </summary>
        void StepFire(MusicFrame music, Camera view, float dt)
        {
            float p = presence[Fire];
            var eye = view.transform.position;
            for (int s = 0; s < 2; s++)
            {
                throwWait[s] -= dt;
                bool lit = p > 0f && tracked[s] && fist[s] < 0.55f;
                flameLevel[s] = Mathf.MoveTowards(flameLevel[s], lit ? 1f : 0f, dt * (lit ? 2.2f : 4f));
                float level = flameLevel[s] * p;
                bool show = level > 0.01f && tracked[s];
                Show(flames[s], show);
                Show(palmLights[s], show);
                if (!show) continue;

                float up = Mathf.Clamp01(normal[s].y * 0.5f + 0.5f);
                float flare = music.Kick;
                float height = flameHeight * (0.8f + 0.35f * up) * Mathf.SmoothStep(0f, 1f, level) * (1f + flare * 0.4f + music.Band(1) * 0.12f);
                var root = palm[s] + normal[s] * 0.03f;
                // Upright like a real flame, turned only about the vertical to face you.
                var away = root - eye;
                away.y = 0f;
                var rotation = Quaternion.LookRotation(away.sqrMagnitude > 1e-6f ? away : forward, Vector3.up);
                var upright = Vector3.up;
                float lean = Mathf.Clamp(-Vector3.Dot(velocity[s], rotation * Vector3.right) * 0.45f, -0.7f, 0.7f);
                var flame = flames[s];
                flame.transform.SetPositionAndRotation(root + upright * (height * 0.45f), rotation);
                flame.transform.localScale = new Vector3(height * 0.72f, height, 1f);
                block.Clear();
                block.SetFloat(ClockId, ShaderClock);
                block.SetFloat(SeedId, s * 13.7f);
                block.SetVector(BendId, new Vector4(lean, 0f, 0f, 0f));
                block.SetFloat(FlareId, flare);
                block.SetFloat(ShapeId, 0f);
                block.SetFloat(AlphaId, Mathf.Clamp01(level * 1.5f));
                flame.SetPropertyBlock(block);

                // A warm light on the palm under the flame.
                var light = palmLights[s];
                light.transform.position = root + upright * (height * 0.2f);
                light.transform.localScale = Vector3.one * (0.1f + height * 0.55f);
                Effects.Face(light.transform, view);
                block.Clear();
                block.SetColor(ColorId, MainColors[Fire]);
                block.SetFloat(IntensityId, (0.2f + flare * 0.22f) * level);
                light.SetPropertyBlock(block);

                // Embers rise from the flame, more of them with the treble, drifting a little away from your face.
                emberDebt[s] += dt * level * (8f + music.Band(5) * 16f);
                var drift = away.normalized * 0.06f;
                while (emberDebt[s] >= 1f)
                {
                    emberDebt[s] -= 1f;
                    var at = root + upright * (height * Random.Range(0.25f, 0.8f)) + Random.insideUnitSphere * (height * 0.12f);
                    Emit(motes, at, Vector3.up * Random.Range(0.18f, 0.4f) + drift + velocity[s] * 0.2f + Random.insideUnitSphere * 0.05f, Pick(Fire),
                        Random.Range(0.003f, 0.007f), Random.Range(0.7f, 1.4f));
                }

                if (p > 0.6f && !menuOpen && !gesture[s] && throwWait[s] <= 0f) TryThrow(s, eye);
            }
            if (previewThrow && tracked[1])
            {
                previewThrow = false;
                Throw(1, (forward + Vector3.up * 0.1f).normalized);
            }
            StepFireballs(music, view, dt);
        }

        /// <summary>A hand pushed quickly away from you, mostly forwards, throws a fireball the way it moves.</summary>
        void TryThrow(int s, Vector3 eye)
        {
            var v = velocity[s];
            var away = palm[s] - eye;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) return;
            away.Normalize();
            float outward = Vector3.Dot(v, away);
            if (outward < throwSpeed || outward < v.magnitude * 0.6f) return;
            Throw(s, (v.normalized + away * 0.6f).normalized);
        }

        void Throw(int s, Vector3 direction)
        {
            Fireball ball = null;
            foreach (var f in fireballs)
                if (!f.Flying && (ball == null || f.Burst > ball.Burst)) ball = f;
            if (ball == null)
                foreach (var f in fireballs)
                    if (ball == null || f.Age > ball.Age) ball = f;
            // Not too steeply into the floor or up at the ceiling.
            direction.y = Mathf.Clamp(direction.y, -0.35f, 0.45f);
            direction.Normalize();
            ball.Flying = true;
            ball.Age = 0f;
            ball.Position = palm[s] + direction * 0.06f;
            ball.Velocity = direction * fireballSpeed;
            throwWait[s] = 0.4f;
            // The flame leaves the hand with the throw and grows back.
            flameLevel[s] = Mathf.Min(flameLevel[s], 0.25f);
            lastAction = Clock;
            for (int i = 0; i < 14; i++)
                Emit(motes, palm[s] + normal[s] * 0.04f, direction * Random.Range(0.3f, 0.9f) + Random.insideUnitSphere * 0.3f, Pick(Fire), Random.Range(0.005f, 0.01f), Random.Range(0.3f, 0.6f));
            voices.Play(whoosh, palm[s], 0.3f, Random.Range(0.9f, 1.1f));
        }

        void StepFireballs(MusicFrame music, Camera view, float dt)
        {
            var eye = view.transform.position;
            foreach (var f in fireballs)
            {
                if (f.Flying)
                {
                    f.Age += dt;
                    var from = f.Position;
                    f.Velocity += Vector3.down * (0.5f * dt);
                    f.Position += f.Velocity * dt;
                    for (int k = 0; k < 3; k++)
                    {
                        var at = Vector3.Lerp(from, f.Position, Random.value) + Random.insideUnitSphere * 0.02f;
                        Emit(motes, at, -f.Velocity * 0.08f + Random.insideUnitSphere * 0.12f + Vector3.up * 0.1f, Pick(Fire),
                            Random.Range(0.006f, 0.013f), Random.Range(0.3f, 0.6f));
                    }
                    if (Lands(from, f.Position, out var hit, out var surface)) Explode(f, hit, surface);
                    else if (f.Age > 1.8f) Explode(f, f.Position, -f.Velocity.normalized);
                }
                else f.Burst += dt;

                bool flying = f.Flying;
                bool flashing = !flying && f.Burst < 0.35f;
                Show(f.Ball, flying);
                Show(f.Core, flying || flashing);
                if (flying)
                {
                    var rotation = Quaternion.LookRotation(f.Position - eye, Vector3.up);
                    var back = -f.Velocity.normalized;
                    var trail = new Vector4(Vector3.Dot(back, rotation * Vector3.right), Vector3.Dot(back, rotation * Vector3.up), 0f, 0f);
                    float grow = Mathf.Clamp01(f.Age / 0.12f);
                    f.Ball.transform.SetPositionAndRotation(f.Position, rotation);
                    f.Ball.transform.localScale = Vector3.one * (0.27f * grow * (1f + music.Kick * 0.2f));
                    block.Clear();
                    block.SetFloat(ClockId, ShaderClock);
                    block.SetFloat(SeedId, f.Seed);
                    block.SetVector(BendId, trail);
                    block.SetFloat(FlareId, music.Kick);
                    block.SetFloat(ShapeId, 1f);
                    block.SetFloat(AlphaId, 1f);
                    f.Ball.SetPropertyBlock(block);
                    f.Core.transform.position = f.Position;
                    f.Core.transform.localScale = Vector3.one * (0.11f * grow);
                    Effects.Face(f.Core.transform, view);
                    block.Clear();
                    block.SetColor(ColorId, BrightColors[Fire]);
                    block.SetFloat(IntensityId, 0.9f);
                    f.Core.SetPropertyBlock(block);
                }
                else if (flashing)
                {
                    // The flash where it burst.
                    float u = f.Burst / 0.35f;
                    f.Core.transform.position = f.Position;
                    f.Core.transform.localScale = Vector3.one * (0.12f + u * 0.5f);
                    Effects.Face(f.Core.transform, view);
                    block.Clear();
                    block.SetColor(ColorId, MainColors[Fire]);
                    block.SetFloat(IntensityId, (1f - u) * (1f - u) * 0.8f);
                    f.Core.SetPropertyBlock(block);
                }
            }
        }

        /// <summary>Where a fireball moving from one point to the next meets the floor, a furniture top or a scanned wall.</summary>
        bool Lands(Vector3 from, Vector3 to, out Vector3 hit, out Vector3 surface)
        {
            float ground = Room.GroundBelow(to, floorY);
            if (to.y <= ground + 0.02f)
            {
                hit = new Vector3(to.x, ground + 0.02f, to.z);
                surface = Vector3.up;
                return true;
            }
            var walls = Room.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                float a = Vector3.Dot(from - w.Center, w.Normal), b = Vector3.Dot(to - w.Center, w.Normal);
                if (a < 0f || b > 0f) continue;
                var p = Vector3.Lerp(from, to, a / Mathf.Max(a - b, 1e-5f));
                if (!w.Contains(p, -0.1f)) continue;
                hit = p + w.Normal * 0.02f;
                surface = w.Normal;
                return true;
            }
            // Without a room scan, it bursts about where a wall would be.
            var flat = new Vector3(to.x - head.x, 0f, to.z - head.z);
            if (!Room.Scanned && flat.magnitude > 3.2f)
            {
                hit = to;
                surface = -flat.normalized;
                return true;
            }
            hit = to;
            surface = Vector3.up;
            return false;
        }

        /// <summary>A fireball bursts into sparks that bounce off the surface it hit, with a crackle.</summary>
        void Explode(Fireball f, Vector3 at, Vector3 surface)
        {
            f.Flying = false;
            f.Burst = 0f;
            f.Position = at;
            for (int i = 0; i < 64; i++)
            {
                var d = Random.onUnitSphere;
                if (Vector3.Dot(d, surface) < 0f) d = Vector3.Reflect(d, surface);
                Emit(falling, at, d * Random.Range(0.7f, 2.3f) + surface * 0.3f, Pick(Fire), Random.Range(0.004f, 0.009f), Random.Range(0.45f, 1f));
            }
            for (int i = 0; i < 26; i++)
                Emit(motes, at + Random.insideUnitSphere * 0.05f, Random.insideUnitSphere * 0.35f + surface * 0.15f, Pick(Fire), Random.Range(0.008f, 0.016f), Random.Range(0.5f, 1.2f));
            voices.Play(crackle, at, 0.3f, Random.Range(0.9f, 1.15f));
        }

        /// <summary>Each beat throws a spurt of sparks up from every flame.</summary>
        void BeatFire(MusicFrame music)
        {
            if (presence[Fire] <= 0.2f) return;
            for (int s = 0; s < 2; s++)
            {
                float level = flameLevel[s] * presence[Fire];
                if (level < 0.3f || !tracked[s]) continue;
                var root = palm[s] + normal[s] * 0.03f;
                int n = 5 + (int)(music.Kick * 5f);
                for (int i = 0; i < n; i++)
                {
                    var spurt = Vector3.up * Random.Range(0.4f, 0.8f) + AwayFromEyes(Random.insideUnitSphere, root) * 0.3f;
                    Emit(motes, root + Random.insideUnitSphere * 0.02f, spurt * (0.8f + level * 0.4f), Pick(Fire), Random.Range(0.004f, 0.008f), Random.Range(0.5f, 0.9f));
                }
            }
        }

        // ---- Water ----

        /// <summary>The streams around the hands, and the ball of water held between palms facing each other.</summary>
        void StepWater(MusicFrame music, Camera view, float dt)
        {
            float p = presence[Water];
            orbWave += dt;
            bool holding = false;
            if (p > 0.5f && tracked[0] && tracked[1] && !menuOpen && fist[0] < 0.5f && fist[1] < 0.5f)
            {
                var gap = palm[1] - palm[0];
                float spread = gap.magnitude;
                if (spread > orbSpread.x && spread < orbSpread.y)
                {
                    var across = gap / spread;
                    holding = Vector3.Dot(normal[0], across) > 0.35f && Vector3.Dot(normal[1], -across) > 0.35f;
                    if (holding)
                    {
                        var middle = (palm[0] + palm[1]) * 0.5f;
                        float radius = Mathf.Clamp(spread * 0.34f, 0.035f, 0.16f);
                        bool fresh = orbLevel <= 0.01f;
                        orbCentre = fresh ? middle : Vector3.Lerp(orbCentre, middle, 1f - Mathf.Exp(-18f * dt));
                        orbRadius = fresh ? radius : Mathf.Lerp(orbRadius, radius, 1f - Mathf.Exp(-10f * dt));
                        lastAction = Clock;
                    }
                }
            }
            // Pulled apart, the ball of water splashes.
            if (orbHeld && !holding && orbLevel > 0.55f) Splash(orbCentre, orbRadius);
            orbHeld = holding;
            orbLevel = Mathf.MoveTowards(orbLevel, holding ? 1f : 0f, dt * (holding ? 2f : 4f));
            if (holding && orbLevel > 0.6f && !orbSounded)
            {
                orbSounded = true;
                Musical.Play(voices, music, 64, orbCentre, 0.25f, pluck: true);
            }
            if (orbLevel <= 0f) orbSounded = false;
            DrawOrb(music, p, dt);

            var eye = view.transform.position;
            for (int s = 0; s < 2; s++) Stream(s, p, music, eye, dt);
        }

        /// <summary>
        /// One hand's stream: its head winds round the hand, in front of the palm and behind its back, and every point
        /// of it chases the one before, so at rest it coils round the hand and swept through the air it pours along
        /// behind it like a ribbon of water, sagging a little and shedding drops where it moves fast. It flows into the
        /// ball of water when there is one.
        /// </summary>
        void Stream(int s, float p, MusicFrame music, Vector3 eye, float dt)
        {
            var line = streams[s];
            bool on = p > 0f && tracked[s];
            streamLevel[s] = Mathf.MoveTowards(streamLevel[s], on ? 1f : 0f, dt * (on ? 1.4f : 2.5f));
            float level = streamLevel[s] * p;
            if (level <= 0.005f || !tracked[s])
            {
                line.SetVisible(false);
                streamFresh[s] = true;
                return;
            }
            var chain = chains[s];
            var last = before[s];
            float speed = velocity[s].magnitude;
            streamPhase[s] += dt * (s == 0 ? 1f : -1f) * (7.5f + music.Band(2) * 2.5f);
            // The head winds round the hand itself, in front of the palm and behind its back.
            Around(s, out var axis, out var a, out var b);
            float coil = Mathf.Lerp(0.075f, 0.04f, Mathf.Clamp01(speed / 1.2f)) * (fist[s] > 0.6f ? 0.75f : 1f);
            var lead = palm[s] + axis * (0.035f + Mathf.Sin(streamPhase[s] * 0.37f) * 0.035f) + (a * Mathf.Cos(streamPhase[s]) + b * Mathf.Sin(streamPhase[s])) * coil;
            lead = Vector3.Lerp(lead, orbCentre, Mathf.SmoothStep(0f, 1f, orbLevel));
            if (streamFresh[s])
            {
                for (int i = 0; i < StreamPoints; i++) chain[i] = last[i] = lead;
                streamFresh[s] = false;
            }
            last[0] = chain[0];
            chain[0] = lead;
            float follow = 1f - Mathf.Exp(-36f * dt);
            float length = 0f;
            for (int i = 1; i < StreamPoints; i++)
            {
                last[i] = chain[i];
                chain[i] = Vector3.Lerp(chain[i], chain[i - 1], follow) + Vector3.down * (0.06f * dt * i / StreamPoints);
                length += Vector3.Distance(chain[i], chain[i - 1]);
            }
            for (int i = 0; i < StreamPoints; i++)
            {
                float u = i / (float)(StreamPoints - 1);
                path[i] = chain[i];
                pathWidths[i] = Mathf.Lerp(1f, 0.3f, u) * (1f + music.Band(1) * 0.35f * Mathf.Sin(u * 13f - Clock * 8f + s * 2f));
            }
            line.SetVisible(true);
            line.Set(path, null, pathWidths, StreamPoints, streamWidth * (0.6f + 0.4f * level), new Color(1f, 1f, 1f, level * (1f - orbLevel * 0.75f)), eye);
            block.Clear();
            block.SetFloat(LengthId, Mathf.Max(length, 0.05f));
            block.SetFloat(ClockId, ShaderClock);
            line.Renderer.SetPropertyBlock(block);

            // Drops fly off where the stream whips round fast, and now and then one drips from its tail.
            float rate = dt * 2.5f * level;
            for (int i = 3; i < StreamPoints; i += 3)
            {
                var v = (chain[i] - last[i]) / Mathf.Max(dt, 1e-4f);
                float fast = v.magnitude;
                if (fast < 0.7f || Random.value > rate * fast) continue;
                Emit(falling, chain[i], v * 0.45f + Random.insideUnitSphere * 0.12f, Pick(Water), Random.Range(0.004f, 0.008f), Random.Range(0.5f, 0.9f));
            }
            if (Random.value < dt * 1.5f * level)
                Emit(falling, chain[StreamPoints - 1], Random.insideUnitSphere * 0.05f, Pick(Water), Random.Range(0.004f, 0.007f), 0.8f);
        }

        void DrawOrb(MusicFrame music, float p, float dt)
        {
            float level = orbLevel * p;
            bool show = level > 0.01f;
            if (orb.gameObject.activeSelf != show) orb.gameObject.SetActive(show);
            if (!show) return;
            float size = orbRadius * 2f * Mathf.SmoothStep(0f, 1f, level) * (1f + music.Kick * 0.05f);
            orb.SetPositionAndRotation(orbCentre, Quaternion.Euler(0f, Clock * 25f, Clock * 9f));
            orb.localScale = Vector3.one * size;
            block.Clear();
            block.SetFloat(BassId, music.Band(1));
            block.SetFloat(WaveId, orbWave);
            block.SetFloat(KickId, music.Kick);
            block.SetFloat(ClockId, ShaderClock);
            block.SetFloat(AlphaId, Mathf.Clamp01(level * 1.4f));
            orbRenderer.SetPropertyBlock(block);
            // Tiny bubbles rise inside it.
            if (Random.value < dt * 10f * level)
                Emit(motes, orbCentre + Random.insideUnitSphere * (size * 0.35f), Vector3.up * 0.04f, BrightColors[Water], 0.004f, 0.6f);
        }

        void Splash(Vector3 at, float radius)
        {
            for (int i = 0; i < 56; i++)
            {
                var d = AwayFromEyes(Random.onUnitSphere, at);
                Emit(falling, at + d * radius, d * Random.Range(0.5f, 1.4f) + Vector3.up * 0.4f, Pick(Water), Random.Range(0.005f, 0.011f), Random.Range(0.6f, 1.1f));
            }
            for (int i = 0; i < 16; i++)
                Emit(motes, at + Random.insideUnitSphere * radius, Random.insideUnitSphere * 0.4f, BrightColors[Water], Random.Range(0.004f, 0.008f), Random.Range(0.5f, 1f));
            voices.Play(splash, at, 0.35f, Random.Range(0.9f, 1.1f));
            orbWave = 0f;
        }

        /// <summary>Each beat ripples the ball of water and shakes a few drops off the streams.</summary>
        void BeatWater()
        {
            if (presence[Water] <= 0.2f) return;
            orbWave = 0f;
            for (int s = 0; s < 2; s++)
            {
                if (streamLevel[s] < 0.3f || !tracked[s]) continue;
                for (int i = 0; i < 3; i++)
                    Emit(falling, chains[s][Random.Range(StreamPoints / 3, StreamPoints)], Random.insideUnitSphere * 0.3f + Vector3.up * 0.2f, Pick(Water),
                        Random.Range(0.004f, 0.008f), Random.Range(0.5f, 0.8f));
            }
            if (orbLevel < 0.5f) return;
            for (int i = 0; i < 6; i++)
            {
                var d = Random.onUnitSphere;
                Emit(falling, orbCentre + d * orbRadius, d * Random.Range(0.3f, 0.6f) + Vector3.up * 0.3f, Pick(Water), Random.Range(0.004f, 0.007f), Random.Range(0.5f, 0.8f));
            }
        }

        // ---- Earth ----

        /// <summary>
        /// The rocks stand in an arc in front of you, a metre or so away and clear of the walls. Where a table, sofa or
        /// shelf stands between you and a rock's place, the rock rises out of it rather than hiding behind it: smaller,
        /// as it is nearer, short enough to stay well below your face, and with its cracks kept on the furniture.
        /// </summary>
        void PlaceRocks()
        {
            var flat = new Vector3(head.x, 0f, head.z);
            for (int i = 0; i < rocks.Length; i++)
            {
                var r = rocks[i];
                float t = rocks.Length > 1 ? i / (float)(rocks.Length - 1) : 0.5f;
                r.Angle = Mathf.Lerp(-58f, 58f, t) + (r.Seed - 0.5f) * 8f;
                r.Direction = Quaternion.AngleAxis(r.Angle, Vector3.up) * forward;
                float distance = Mathf.Lerp(rockDistance.x, rockDistance.y, Mathf.Repeat(r.Seed * 7.3f, 1f));
                distance = Mathf.Max(0.65f, Mathf.Min(distance, WallDistance(head, r.Direction) - r.FullWidth * 0.8f - 0.1f));
                float onTop = -1f;
                for (float d = 0.55f; d < distance; d += 0.05f)
                {
                    var probe = flat + r.Direction * d + Vector3.up * (head.y - 0.3f);
                    if (Room.TopBelow(probe, out var top) && top.Center.y > floorY + 0.2f && top.Contains(probe, Mathf.Min(0.15f, r.FullWidth * 0.4f))) onTop = d;
                }
                if (onTop > 0f) distance = onTop;
                var spot = flat + r.Direction * distance + Vector3.up * (head.y - 0.3f);
                bool raised = Room.TopBelow(spot, out var under) && under.Center.y > floorY + 0.2f;
                r.Ground = raised ? under.Center.y : floorY;
                float scale = raised ? Mathf.Clamp(distance / 1.15f, 0.55f, 1f) : 1f;
                r.Width = r.FullWidth * scale;
                r.Depth = r.FullDepth * scale;
                r.Height = Mathf.Max(0.2f, Mathf.Min(r.FullHeight * scale, head.y - (raised ? 0.5f : 0.3f) - r.Ground));
                r.Base = new Vector3(spot.x, r.Ground, spot.z);
                r.Facing = Quaternion.LookRotation(r.Direction, Vector3.up) * Quaternion.AngleAxis((r.Seed - 0.5f) * 20f, Vector3.up);
                r.Rise = r.Target = r.Speed = 0f;
                // The cracked ground round its foot: on furniture, only as wide as fits on top of it.
                float footprint = Mathf.Max(r.Width, r.Depth);
                r.CrackSize = footprint * 2.6f;
                if (raised)
                {
                    var local = under.Local(r.Base);
                    float room = Mathf.Min(under.Size.x * 0.5f - Mathf.Abs(local.x), under.Size.y * 0.5f - Mathf.Abs(local.y));
                    r.CrackSize = Mathf.Min(r.CrackSize, room * 2f);
                    if (r.CrackSize < footprint * 1.3f) r.CrackSize = 0f;
                }
            }
        }

        /// <summary>How far along a level ray the nearest scanned wall is (infinity without a scan).</summary>
        static float WallDistance(Vector3 from, Vector3 direction)
        {
            float best = float.MaxValue;
            var walls = Room.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                float facing = -Vector3.Dot(direction, w.Normal);
                if (facing < 0.1f) continue;
                float d = Vector3.Dot(from - w.Center, w.Normal) / facing;
                if (d <= 0f || d >= best) continue;
                if (Mathf.Abs(w.Local(from + direction * d).x) > w.Size.x * 0.5f + 0.1f) continue;
                best = d;
            }
            return best;
        }

        /// <summary>
        /// Palms up and lifting raise the rocks on that hand's side; palms down and pushing sink them, as far as the
        /// hand moves. The rocks follow with weight, rumbling, cracking the floor and throwing up dust as they go.
        /// </summary>
        void StepEarth(MusicFrame music, float dt)
        {
            float p = presence[Earth];
            bool active = current == Earth;
            if (active && raiseRocks)
            {
                raiseRocks = false;
                // They push up out of the ground as the element arrives, to varied heights.
                foreach (var r in rocks) r.Target = Mathf.Max(r.Target, 0.42f + 0.25f * Mathf.Repeat(r.Seed * 3.7f, 1f));
            }
            if (active && previewLift >= 0f)
            {
                foreach (var r in rocks) r.Target = Mathf.Max(r.Target, previewLift * (0.75f + 0.25f * r.Seed));
                previewLift = -1f;
            }
            for (int s = 0; s < 2 && active && p > 0.5f && !menuOpen; s++)
            {
                if (!tracked[s] || fist[s] > 0.6f || gesture[s]) continue;
                float vy = velocity[s].y;
                float ny = normal[s].y;
                float lift = ny > 0.4f && vy > 0.07f ? vy : ny < -0.4f && vy < -0.07f ? vy : 0f;
                if (lift == 0f) continue;
                lastAction = Clock;
                // The hand moves the rocks on its own side most: across from left to right, it reaches from the
                // leftmost rock to the rightmost.
                float side = Mathf.Clamp(Vector3.Dot(palm[s] - head, right) / 0.35f, -1f, 1f) * 60f;
                foreach (var r in rocks)
                {
                    float d = (r.Angle - side) / 38f;
                    r.Target = Mathf.Clamp01(r.Target + lift * dt / liftReach * Mathf.Exp(-d * d));
                }
            }

            float moving = 0f;
            var loudest = head;
            float most = 0f;
            foreach (var r in rocks)
            {
                float m = StepRock(r, music, active, p, dt);
                moving += m;
                if (m > most)
                {
                    most = m;
                    loudest = r.Base;
                }
            }
            rumbleWait -= dt;
            if (moving > 0.5f && rumbleWait <= 0f)
            {
                voices.Play(rumble, loudest, Mathf.Clamp01(moving) * 0.3f, Random.Range(0.9f, 1.1f));
                rumbleWait = 0.9f;
            }
            Pebbles(music, p, dt);
        }

        /// <summary>One rock: it springs towards its height, shakes while it moves, and the floor around it cracks and glows.</summary>
        float StepRock(Rock r, MusicFrame music, bool active, float p, float dt)
        {
            if (!active) r.Target = 0f;
            float pull = (r.Target - r.Rise) * 55f - r.Speed * 11f;
            r.Speed += pull * dt;
            r.Rise = Mathf.Clamp(r.Rise + r.Speed * dt, 0f, 1.05f);
            float moving = Mathf.Abs(r.Speed);
            bool visible = r.Rise > 0.003f || r.Target > 0.003f;
            Show(r.Renderer, visible);
            Show(r.Crack, visible && r.CrackSize > 0f);
            if (!visible) return 0f;
            r.Shake = Mathf.Max(r.Shake * Mathf.Exp(-dt * 8f), Mathf.Clamp01(moving * 1.2f));
            float t = Clock;
            var jitter = (right * Mathf.Sin(t * 67f + r.Seed * 40f) + forward * Mathf.Sin(t * 59f + r.Seed * 23f)) * (0.007f * r.Shake);
            var tilt = Quaternion.Euler(Mathf.Sin(t * 53f + r.Seed * 9f) * 1.2f * r.Shake, 0f, Mathf.Sin(t * 47f + r.Seed * 13f) * 1.2f * r.Shake);
            var centre = r.Base + Vector3.up * (r.Height * (r.Rise * 0.92f - 0.5f)) + jitter;
            r.Transform.SetPositionAndRotation(centre, r.Facing * tilt);
            r.Transform.localScale = new Vector3(r.Width, r.Height, r.Depth);
            float risen = Mathf.Clamp01(r.Rise * 3f);
            r.Glow = Mathf.Lerp(r.Glow, 0.15f + moving * 1.5f, 1f - Mathf.Exp(-8f * dt));
            block.Clear();
            block.SetFloat(GlowId, (r.Glow + music.Kick * 0.45f * risen) * Mathf.Max(p, 0.4f));
            block.SetFloat(GroundId, r.Ground);
            block.SetFloat(SeedId, r.Seed * 10f);
            block.SetFloat(ClockId, ShaderClock);
            block.SetColor(TintId, r.Tint);
            r.Renderer.SetPropertyBlock(block);

            float footprint = Mathf.Max(r.Width, r.Depth);
            if (r.CrackSize > 0f)
            {
                float size = r.CrackSize;
                r.Crack.transform.SetPositionAndRotation(new Vector3(r.Base.x, r.Ground + 0.004f, r.Base.z), Quaternion.LookRotation(Vector3.down, r.Direction));
                r.Crack.transform.localScale = new Vector3(size, size, 1f);
                block.Clear();
                block.SetFloat(GlowId, (0.25f + moving * 2.2f + music.Kick * 0.35f * risen) * risen * Mathf.Max(p, 0.3f));
                block.SetFloat(InnerId, footprint / size);
                block.SetFloat(SeedId, r.Seed * 17f);
                r.Crack.SetPropertyBlock(block);
            }

            // Dust where it moves, and chips falling off it as it shoves up.
            if (moving > 0.15f && Random.value < dt * 40f * moving)
            {
                float a = Random.value * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Emit(motes, r.Base + outward * (footprint * 0.55f) + Vector3.up * 0.02f, outward * Random.Range(0.1f, 0.3f) + Vector3.up * Random.Range(0.05f, 0.2f), DustColor,
                    Random.Range(0.012f, 0.026f), Random.Range(0.8f, 1.6f));
            }
            if (r.Speed > 0.4f && Random.value < dt * 18f * r.Speed)
            {
                var top = centre + Vector3.up * (r.Height * 0.4f) + Random.insideUnitSphere * (r.Width * 0.3f);
                Emit(falling, top, Random.insideUnitSphere * 0.4f + Vector3.up * 0.3f, Pick(Earth), Random.Range(0.008f, 0.014f), Random.Range(0.5f, 0.9f));
            }
            return moving;
        }

        /// <summary>A few pebbles hover over each open palm, turning slowly and hopping on the beat.</summary>
        void Pebbles(MusicFrame music, float p, float dt)
        {
            for (int s = 0; s < 2; s++)
            {
                bool on = p > 0.05f && tracked[s] && fist[s] < 0.55f;
                pebbleLevel[s] = Mathf.MoveTowards(pebbleLevel[s], on ? 1f : 0f, dt * 2.5f);
                float level = pebbleLevel[s] * p;
                var n = normal[s];
                var a = Vector3.Cross(n, Vector3.up);
                if (a.sqrMagnitude < 0.04f) a = Vector3.Cross(n, forward);
                a.Normalize();
                var b = Vector3.Cross(n, a);
                for (int k = 0; k < PebblesPerHand; k++)
                {
                    int i = s * PebblesPerHand + k;
                    var pebble = pebbles[i];
                    bool show = level > 0.02f && tracked[s];
                    if (pebble.gameObject.activeSelf != show) pebble.gameObject.SetActive(show);
                    if (!show) continue;
                    float angle = Clock * (s == 0 ? 0.9f : -0.9f) + k * Mathf.PI * 0.5f;
                    float lift = 0.065f + 0.012f * Mathf.Sin(Clock * 2.1f + k * 1.7f) + music.Kick * 0.018f;
                    var spot = palm[s] + n * lift + (a * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * (0.045f + 0.012f * (k % 2));
                    pebble.SetPositionAndRotation(spot, Quaternion.Euler(Clock * 40f + k * 70f, Clock * 25f + k * 30f, k * 45f));
                    pebble.localScale = Vector3.one * ((0.017f + 0.006f * (k % 3)) * Mathf.SmoothStep(0f, 1f, level));
                    block.Clear();
                    block.SetFloat(GlowId, 0.3f + music.Kick * 0.6f);
                    block.SetFloat(GroundId, -100f);
                    block.SetFloat(SeedId, i * 3.3f);
                    block.SetFloat(ClockId, ShaderClock);
                    block.SetColor(TintId, new Color(1.15f, 1.1f, 1.05f, 1f));
                    pebbleRenderers[i].SetPropertyBlock(block);
                }
            }
        }

        /// <summary>Each beat shakes every risen rock and puffs dust from its foot.</summary>
        void BeatEarth(MusicFrame music)
        {
            if (presence[Earth] <= 0.2f) return;
            foreach (var r in rocks)
            {
                if (r.Rise < 0.08f) continue;
                r.Shake = Mathf.Max(r.Shake, 0.55f + music.Kick * 0.3f);
                for (int i = 0; i < 4; i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Emit(motes, r.Base + outward * (Mathf.Max(r.Width, r.Depth) * 0.55f) + Vector3.up * 0.02f, outward * Random.Range(0.15f, 0.35f) + Vector3.up * 0.1f,
                        DustColor, Random.Range(0.012f, 0.022f), Random.Range(0.7f, 1.3f));
                }
            }
        }

        // ---- Air ----

        /// <summary>The wind around the hands, the whirlwinds, the beat's gusts and a slow wind circling the room.</summary>
        void StepAir(MusicFrame music, Camera view, float dt)
        {
            float p = presence[Air];
            var eye = view.transform.position;
            gust = Mathf.Max(0f, gust - dt * 2.2f);
            roomPhase += dt * windSign;
            windCount = 0;
            for (int s = 0; s < 2; s++)
            {
                Circling(s, p, eye, dt);
                Currents(s, p, music, eye, dt);
            }
            if (previewWhirl)
            {
                previewWhirl = false;
                var w = whirls[0];
                w.Active = true;
                w.Energy = 0.9f;
                w.Driven = 0f;
                w.Base = new Vector3(eye.x, floorY, eye.z) + forward * 0.85f + right * 0.1f;
                w.Top = w.Base + Vector3.up;
            }
            StepWhirls(music, p, eye, dt);
            Gusts(p, dt);
            RoomWind(p);
            wind.SetParticles(windOut, windCount);
        }

        /// <summary>
        /// Tracks how fast a hand turns round the middle of its own recent movement. A hand going round in circles
        /// keeps turning one way and drives a whirlwind; a sweep or a wave does not, as it never turns far.
        /// </summary>
        void Circling(int s, float p, Vector3 eye, float dt)
        {
            if (!tracked[s])
            {
                turned[s] = 0f;
                spinAxis[s] = Vector3.zero;
                wasTracked[s] = false;
                return;
            }
            if (!wasTracked[s])
            {
                circleCentre[s] = palm[s];
                wasTracked[s] = true;
            }
            circleCentre[s] = Vector3.Lerp(circleCentre[s], palm[s], 1f - Mathf.Exp(-2.5f * dt));
            var r = palm[s] - circleCentre[s];
            float r2 = r.sqrMagnitude;
            var omega = r2 > 0.0006f ? Vector3.Cross(r, velocity[s]) / r2 : Vector3.zero;
            spinAxis[s] = Vector3.Lerp(spinAxis[s], omega, 1f - Mathf.Exp(-6f * dt));
            float rate = spinAxis[s].magnitude;
            bool circling = p > 0.5f && current == Air && !menuOpen && rate > 2.6f && velocity[s].magnitude > 0.22f && fist[s] < 0.7f;
            turned[s] = circling ? turned[s] + rate * dt : Mathf.MoveTowards(turned[s], 0f, dt * 6f);
            if (!circling) return;
            lastAction = Clock;
            // Half a turn starts a whirlwind; it grows to full strength over the next turns.
            if (turned[s] > Mathf.PI) Drive(s, rate, eye, dt);
        }

        /// <summary>A circling hand spins up its whirlwind on the floor a little beyond it, its top flaring round the hand.</summary>
        void Drive(int s, float rate, Vector3 eye, float dt)
        {
            var w = whirls[s];
            var flat = palm[s] - eye;
            flat.y = 0f;
            float reach = flat.magnitude;
            var direction = reach > 0.05f ? flat / reach : forward;
            var spot = new Vector3(eye.x, 0f, eye.z) + direction * Mathf.Clamp(reach + 0.3f, 0.6f, 1.2f);
            spot.y = Room.GroundBelow(new Vector3(spot.x, palm[s].y - 0.05f, spot.z), floorY);
            if (!w.Active)
            {
                w.Active = true;
                w.Energy = 0f;
                w.Base = spot;
                w.Drift = Vector3.zero;
                w.Spin = Random.value * 6f;
                voices.Play(whoosh, spot + Vector3.up * 0.5f, 0.25f, 0.8f);
            }
            w.Base = Vector3.Lerp(w.Base, spot, 1f - Mathf.Exp(-2f * dt));
            w.Top = palm[s] + direction * 0.06f;
            w.Driven = 1f;
            w.Energy = Mathf.Min(1f, w.Energy + dt * rate / (Mathf.PI * 2f * Mathf.Max(0.2f, turnsToSpin)));
        }

        void StepWhirls(MusicFrame music, float p, Vector3 eye, float dt)
        {
            for (int i = 0; i < whirls.Length; i++)
            {
                var w = whirls[i];
                if (w.Active)
                {
                    // Let go, it keeps turning for several seconds; once the element has gone, it dies down quickly.
                    w.Driven = Mathf.MoveTowards(w.Driven, 0f, dt * 2f);
                    if (current != Air) w.Energy -= dt * 0.6f;
                    else if (w.Driven <= 0f) w.Energy -= dt * 0.16f;
                    if (w.Energy <= 0f) w.Active = false;
                }
                if (!w.Active)
                {
                    foreach (var strand in w.Strands) strand.SetVisible(false);
                    continue;
                }
                if (w.Driven < 0.5f)
                {
                    // Free, it wanders slowly about the floor in front of you.
                    var wander = new Vector3(Mathf.Sin(Clock * 0.37f + i * 3f), 0f, Mathf.Cos(Clock * 0.29f + i * 5f)) * 0.12f;
                    w.Drift = Vector3.Lerp(w.Drift, wander, dt);
                    w.Base += w.Drift * dt;
                    var flat = new Vector3(w.Base.x - eye.x, 0f, w.Base.z - eye.z);
                    float d = flat.magnitude;
                    if (d > 1e-3f && d < 0.6f) w.Base += flat / d * (0.6f - d);
                    if (d > 1.6f) w.Base -= flat / d * (d - 1.6f);
                }
                float size = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(w.Energy * 1.4f));
                var free = new Vector3(w.Base.x, w.Base.y + Mathf.Lerp(0.45f, 0.95f, size), w.Base.z);
                var top = Vector3.Lerp(free, w.Top, w.Driven);
                w.Spin += dt * (5f + 9f * w.Energy) * (1f + gust * 0.6f + music.Band(1) * 0.3f);
                DrawWhirl(w, top, size, p, music, eye);
                if (Random.value < dt * 30f * size)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Emit(motes, w.Base + outward * 0.04f + Vector3.up * 0.02f, outward * 0.25f + Vector3.Cross(Vector3.up, outward) * 0.35f + Vector3.up * 0.15f, DustColor,
                        Random.Range(0.008f, 0.016f), Random.Range(0.6f, 1.1f));
                }
            }
        }

        /// <summary>
        /// A funnel of wind, narrow at its foot and flaring at the top: swirls of air circle it, climbing as they turn,
        /// dusty near the ground, and specks ride round it, fastest at the bottom.
        /// </summary>
        void DrawWhirl(Whirl w, Vector3 top, float size, float p, MusicFrame music, Vector3 eye)
        {
            float fade = Mathf.Clamp01(w.Energy * 4f) * Mathf.Max(p, 0.3f);
            float swell = 1f + music.Kick * 0.15f;
            for (int k = 0; k < Strands; k++)
            {
                var strand = w.Strands[k];
                strand.SetVisible(true);
                // Each swirl circles the funnel at its own height, climbing slowly and fading out at the top as another
                // forms at the foot; the lower ones turn faster.
                float height = Mathf.Repeat(k / (float)Strands + w.Spin * 0.015f, 1f);
                float heightFade = Mathf.Sin(height * Mathf.PI);
                float start = w.Spin * (1.7f - height * 0.8f) + k * 2.4f;
                for (int i = 0; i < StrandPoints; i++)
                {
                    float t = i / (float)(StrandPoints - 1);
                    float u = Mathf.Clamp01(height + t * 0.08f);
                    path[i] = Funnel(w, top, u, start - t * Mathf.PI * 1.3f, size, swell, 1f);
                    var col = Color.Lerp(DustColor, MainColors[Air], Mathf.Clamp01(u * 2.5f));
                    col.a = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.8f) * Mathf.Sqrt(1f - t * 0.6f) * heightFade * (0.4f + 0.4f * size);
                    pathColors[i] = col;
                }
                strand.Set(path, pathColors, StrandPoints, 0.005f * (0.7f + 0.6f * size), new Color(1f, 1f, 1f, fade), eye, false);
            }
            float spinRate = 5f + 9f * w.Energy;
            for (int j = 0; j < WhirlMotes && windCount < windOut.Length; j++)
            {
                float u = whirlU[j];
                float a = w.Spin * (1.25f - u * 0.45f) + whirlAngle[j] + u * 7f;
                var at = Funnel(w, top, u, a, size, swell, whirlReach[j]);
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var col = Color.Lerp(DustColor, BrightColors[Air], Mathf.Clamp01(u * 2f));
                col.a = fade * (0.3f + 0.45f * Mathf.Sin(u * Mathf.PI));
                float radius = Vector3.Distance(at, Vector3.Lerp(w.Base, top, u));
                windOut[windCount++] = new ParticleSystem.Particle
                {
                    position = at,
                    velocity = Vector3.Cross(Vector3.up, outward) * Mathf.Min(radius * spinRate, 1.4f) * 0.6f + Vector3.up * 0.15f,
                    startColor = col,
                    startSize = 0.004f + 0.003f * u,
                    startLifetime = 10f,
                    remainingLifetime = 10f
                };
            }
        }

        /// <summary>A point on a whirlwind's funnel at height share <paramref name="u"/> and angle <paramref name="a"/>; its middle sways.</summary>
        Vector3 Funnel(Whirl w, Vector3 top, float u, float a, float size, float swell, float reach)
        {
            var c = Vector3.Lerp(w.Base, top, u);
            c += (right * Mathf.Sin(u * 3f + Clock * 1.3f) + forward * Mathf.Cos(u * 2.4f + Clock * 1.1f)) * (0.12f * u * (1f - u) * size);
            float radius = (0.025f + 0.18f * Mathf.Pow(u, 1.4f)) * (0.4f + 0.6f * size) * swell * reach;
            return c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
        }

        /// <summary>Wind swirling round a hand: motes and three arcs of air, faster on the beat's gusts and as you circle.</summary>
        void Currents(int s, float p, MusicFrame music, Vector3 eye, float dt)
        {
            bool on = p > 0f && tracked[s];
            airLevel[s] = Mathf.MoveTowards(airLevel[s], on ? 1f : 0f, dt * 2f);
            float level = airLevel[s] * p;
            bool show = level > 0.01f && tracked[s];
            for (int k = 0; k < CurrentsPerHand; k++) currents[s * CurrentsPerHand + k].SetVisible(show);
            if (!show) return;
            float spinning = Mathf.Clamp01(turned[s] / (Mathf.PI * 2f));
            float turn = s == 0 ? 1f : -1f;
            float speedUp = 1f + gust * 1.2f + spinning * 1.5f + music.Band(3) * 0.4f;
            airPhase[s] += dt * speedUp * turn;
            float puff = 1f + gust * 0.4f - spinning * 0.3f;
            // The air winds round the hand itself, like the water does, from the palm up to the fingers.
            Around(s, out var axis, out var across, out var side);
            var c = palm[s];
            int start = s * windPerHand;
            for (int j = 0; j < windPerHand && windCount < windOut.Length; j++)
            {
                int q = start + j;
                float a = windAngle[q] + airPhase[s] * windSpeed[q] * 5f;
                float radius = windRadius[q] * puff;
                float h = windHeight[q] * (1f - spinning * 0.3f) + Mathf.Sin(a * 2f + q) * 0.008f;
                var round = across * Mathf.Cos(a) + side * Mathf.Sin(a);
                var tangent = side * Mathf.Cos(a) - across * Mathf.Sin(a);
                float twinkle = 0.5f + 0.5f * Mathf.Sin(Clock * 3f + q * 1.7f);
                var col = Color.Lerp(MainColors[Air], BrightColors[Air], twinkle);
                col.a = level * (0.2f + 0.45f * twinkle);
                windOut[windCount++] = new ParticleSystem.Particle
                {
                    position = c + axis * h + round * radius,
                    // Stretched a little along their way, so they read as specks of air rushing round.
                    velocity = tangent * (radius * windSpeed[q] * 1.6f * speedUp * turn),
                    startColor = col,
                    startSize = 0.0032f + 0.0015f * twinkle,
                    startLifetime = 10f,
                    remainingLifetime = 10f
                };
            }
            for (int k = 0; k < CurrentsPerHand; k++)
            {
                float radius = (0.075f + k * 0.018f) * puff;
                float h = 0.005f + k * 0.05f;
                float lead = airPhase[s] * (4.6f + k * 0.9f) + k * 2.6f;
                for (int i = 0; i < CurrentPoints; i++)
                {
                    float u = i / (float)(CurrentPoints - 1);
                    float a = lead - u * 1.7f * turn;
                    // Each arc climbs a little as it goes round, a short stretch of a spiral.
                    path[i] = c + axis * (h - u * 0.02f) + (across * Mathf.Cos(a) + side * Mathf.Sin(a)) * radius;
                    pathColors[i] = new Color(1f, 1f, 1f, (1f - u) * (1f - u) * Mathf.Clamp01(u * 8f));
                }
                var tint = BrightColors[Air];
                currents[s * CurrentsPerHand + k].Set(path, pathColors, CurrentPoints, 0.0028f * (1f + gust * 0.4f), new Color(tint.r, tint.g, tint.b, level * 0.6f), eye, false);
            }
        }

        /// <summary>Each beat sends a gust of streaks across in front of you; every eight beats the wind turns about.</summary>
        void BeatAir(MusicFrame music)
        {
            if (presence[Air] <= 0.2f || View == null) return;
            gust = 1f;
            if (Beats % 8 == 0) windSign = -windSign;
            var eye = View.transform.position;
            var ahead = Effects.FlatForward(View);
            var side = Vector3.Cross(Vector3.up, ahead);
            int n = 6 + (int)(music.Kick * 4f);
            for (int k = 0; k < n; k++)
            {
                int i = nextGust;
                nextGust = (nextGust + 1) % GustMotes;
                gustFrom[i] = eye + ahead * Random.Range(0.6f, 1.6f) - side * (windSign * Random.Range(0.6f, 0.9f)) + Vector3.up * Random.Range(-0.55f, 0.15f);
                gustVelocity[i] = side * (windSign * Random.Range(1.3f, 2f)) + Vector3.up * Random.Range(-0.05f, 0.1f);
                gustAge[i] = 0f;
            }
        }

        void Gusts(float p, float dt)
        {
            for (int i = 0; i < GustMotes; i++)
            {
                gustAge[i] += dt;
                if (gustAge[i] > 1.1f || windCount >= windOut.Length) continue;
                var col = BrightColors[Air];
                col.a = Mathf.Sin(gustAge[i] / 1.1f * Mathf.PI) * 0.45f * p;
                windOut[windCount++] = new ParticleSystem.Particle
                {
                    position = gustFrom[i] + gustVelocity[i] * gustAge[i],
                    velocity = gustVelocity[i] * 0.8f,
                    startColor = col,
                    startSize = 0.004f,
                    startLifetime = 10f,
                    remainingLifetime = 10f
                };
            }
        }

        /// <summary>A slow wind circling the room around you, a mote here and there.</summary>
        void RoomWind(float p)
        {
            if (p <= 0.01f) return;
            for (int i = 0; i < RoomMotes && windCount < windOut.Length; i++)
            {
                float radius = 0.9f + Hash(i) * 1.3f;
                float speed = (0.25f + Hash(i + 50) * 0.35f) / radius;
                float a = Hash(i + 100) * Mathf.PI * 2f + roomPhase * speed;
                float y = floorY + 0.3f + Hash(i + 150) * 1.7f + Mathf.Sin(Clock * 0.7f + i) * 0.05f;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var col = BrightColors[Air];
                col.a = Mathf.Sin(Mathf.Repeat(Clock * 0.2f + Hash(i + 200), 1f) * Mathf.PI) * 0.35f * p;
                windOut[windCount++] = new ParticleSystem.Particle
                {
                    position = new Vector3(head.x, y, head.z) + outward * radius,
                    velocity = Vector3.Cross(Vector3.up, outward) * (speed * radius * windSign),
                    startColor = col,
                    startSize = 0.006f,
                    startLifetime = 10f,
                    remainingLifetime = 10f
                };
            }
        }

        // ---- The room around you ----

        /// <summary>A little of the element drifts through the room: embers rising, drops of water hanging, dust floating.</summary>
        void Ambient(MusicFrame music, Camera view, float dt)
        {
            float p = presence[current];
            ambientDebt += dt * p * (5f + music.Band(4) * 5f);
            var eye = view.transform.position;
            while (ambientDebt >= 1f)
            {
                ambientDebt -= 1f;
                float a = Random.value * Mathf.PI * 2f;
                float reach = Mathf.Lerp(0.6f, 2.4f, Mathf.Sqrt(Random.value));
                var at = new Vector3(eye.x + Mathf.Cos(a) * reach, 0f, eye.z + Mathf.Sin(a) * reach);
                switch (current)
                {
                    case Fire:
                        at.y = floorY + Random.Range(0.05f, 0.9f);
                        Emit(motes, at, Vector3.up * Random.Range(0.1f, 0.25f) + Random.insideUnitSphere * 0.03f, Pick(Fire), Random.Range(0.004f, 0.008f), Random.Range(3f, 5f));
                        break;
                    case Water:
                        at.y = Random.Range(floorY + 0.4f, eye.y + 0.4f);
                        Emit(motes, at, Random.insideUnitSphere * 0.03f, Pick(Water), Random.Range(0.003f, 0.006f), Random.Range(3f, 5f));
                        break;
                    case Earth:
                        at.y = floorY + Random.Range(0.05f, 1.3f);
                        Emit(motes, at, Random.insideUnitSphere * 0.02f, DustColor, Random.Range(0.004f, 0.009f), Random.Range(3.5f, 6f));
                        break;
                }
            }
        }

        // ---- The sung words ----

        void AddWord(string text, bool newLine)
        {
            if (SungWords == null || View == null) return;
            int e = current;
            var w = SungWords.Add(text, BrightColors[e], e == Earth ? 3.6f : 3.2f, newLine);
            w.Slot = e;
            int index = 0;
            var words = SungWords.Words;
            for (int k = 0; k < words.Count; k++)
                if (words[k] == w)
                {
                    index = k;
                    break;
                }
            popped[index] = false;
            var eye = View.transform.position;
            var ahead = Effects.FlatForward(View);
            var side = Vector3.Cross(Vector3.up, ahead);
            int s = Sung.Index % 2 == 0 ? 1 : 0;
            if (!tracked[s]) s = 1 - s;
            switch (e)
            {
                case Fire:
                {
                    // Rising off the tip of a flame, a little beyond the hand.
                    Vector3 from;
                    if (tracked[s] && flameLevel[s] > 0.2f)
                    {
                        var tip = palm[s] + normal[s] * 0.03f + Vector3.up * (flameHeight * 0.9f);
                        from = eye + (tip - eye).normalized * 0.55f;
                    }
                    else from = eye + ahead * 0.7f + Vector3.down * 0.18f + side * ((Sung.Index % 3 - 1) * 0.12f);
                    w.Position = from;
                    w.Velocity = Vector3.up * 0.06f + (from - eye).normalized * 0.02f;
                    break;
                }
                case Water:
                {
                    // Out of the ball of water or a stream, drifting to float in front of you.
                    Vector3 from;
                    if (orbLevel > 0.5f) from = orbCentre + Vector3.up * (orbRadius + 0.03f);
                    else if (tracked[s]) from = palm[s] + normal[s] * 0.05f + Vector3.up * 0.05f;
                    else from = eye + ahead * 0.55f + Vector3.down * 0.25f;
                    float angle = ((Sung.Index % 5) - 2) * 13f + (w.Seed - 0.5f) * 6f;
                    wordAnchor[index] = from;
                    wordAim[index] = eye + Quaternion.AngleAxis(angle, Vector3.up) * ahead * 0.66f + Vector3.up * (-0.06f + (Sung.Index % 2) * 0.07f);
                    w.Position = from;
                    break;
                }
                case Earth:
                {
                    // Carved into the face of one of the rocks in front of you, moving along them as the line goes on;
                    // the rock rises to show it.
                    int r = rocks.Length > 2 ? 1 + Sung.Index % (rocks.Length - 2) : 0;
                    // The word carved there before fades out quickly to make room.
                    for (int k = 0; k < words.Count; k++)
                        if (k != index && words[k].Active && words[k].Slot == Earth && wordRock[k] == r)
                            words[k].Life = Mathf.Min(words[k].Life, words[k].Age + 0.15f);
                    wordRock[index] = r;
                    rocks[r].Target = Mathf.Max(rocks[r].Target, 0.72f);
                    float face = 0.86f * rocks[r].Width;
                    w.Size = Mathf.Clamp(face * 0.9f / Mathf.Max(w.Width * SungWordHeight, 1e-3f), 1.2f, 2.2f);
                    // Chips fly as it is carved.
                    var at = rocks[r].Transform.position + rocks[r].Facing * Vector3.back * (FrontFace * rocks[r].Depth);
                    for (int k = 0; k < 10; k++)
                        Emit(falling, at + Random.insideUnitSphere * 0.05f, rocks[r].Facing * Vector3.back * Random.Range(0.3f, 0.7f) + Random.insideUnitSphere * 0.3f,
                            Pick(Earth), Random.Range(0.005f, 0.01f), Random.Range(0.4f, 0.8f));
                    break;
                }
                default:
                {
                    // Blown in from upwind, slowing as it comes to rest in front of you.
                    wordAnchor[index] = eye;
                    wordAim[index] = ahead;
                    wordStart[index] = -windSign * (30f + w.Seed * 8f);
                    wordEnd[index] = windSign * (2f + w.Seed * 8f);
                    wordLift[index] = -0.1f + ((Sung.Index % 3) - 1) * 0.065f;
                    w.Position = BlownAt(index, w, 0f);
                    break;
                }
            }
        }

        void StepWords(MusicFrame music, Camera view, float dt)
        {
            if (SungWords == null) return;
            var eye = view.transform.position;
            var words = SungWords.Words;
            for (int i = 0; i < words.Count; i++)
            {
                var w = words[i];
                bool bubble = false, trail = false;
                if (w.Active)
                    switch (w.Slot)
                    {
                        case Fire:
                            EmberWord(w, eye, music, dt);
                            break;
                        case Water:
                            bubble = BubbleWord(i, w, view, music);
                            break;
                        case Earth:
                            CarvedWord(i, w, music);
                            break;
                        default:
                            trail = BlownWord(i, w, eye, music);
                            break;
                    }
                Show(bubbles[i], bubble);
                trails[i].SetVisible(trail);
            }
        }

        /// <summary>A word of fire: it floats up like an ember, flickering, cooling from gold to red, shedding sparks.</summary>
        void EmberWord(WordLayer.Word w, Vector3 eye, MusicFrame music, float dt)
        {
            w.Position += w.Velocity * dt;
            w.Velocity = Vector3.Lerp(w.Velocity, Vector3.up * 0.035f, 1f - Mathf.Exp(-1.5f * dt));
            w.Rotation = WordLayer.Facing(w.Position, eye);
            float heat = Mathf.Clamp01(1f - w.Age / w.Life);
            float flicker = Mathf.Sin(w.Age * 17f + w.Seed * 30f) * 0.5f + Mathf.Sin(w.Age * 29f + w.Seed * 11f) * 0.5f;
            w.Color = Color.Lerp(new Color(1f, 0.42f, 0.1f), new Color(1f, 0.84f, 0.38f), heat * 0.85f + flicker * 0.08f);
            w.Halo = 0.22f + heat * 0.15f + flicker * 0.05f + music.Kick * 0.2f;
            if (Random.value < dt * 7f)
            {
                float height = SungWords.Height(w);
                var at = w.Position + (w.Rotation * Vector3.right) * (Random.Range(-0.5f, 0.5f) * w.Width * height) + Vector3.up * (height * Random.Range(0f, 0.7f));
                Emit(motes, at, Vector3.up * Random.Range(0.06f, 0.15f) + Random.insideUnitSphere * 0.03f, Pick(Fire), Random.Range(0.003f, 0.006f), Random.Range(0.5f, 1f));
            }
        }

        /// <summary>A word of water: inside a wobbling bubble that drifts over to float in front of you, then pops.</summary>
        bool BubbleWord(int i, WordLayer.Word w, Camera view, MusicFrame music)
        {
            float glide = 1f - Mathf.Exp(-w.Age * 1.6f);
            var wobble = new Vector3(Mathf.Sin(w.Age * 1.7f + w.Seed * 9f), Mathf.Sin(w.Age * 1.3f + w.Seed * 4f) * 0.6f, 0f) * 0.006f;
            w.Position = Vector3.Lerp(wordAnchor[i], wordAim[i], glide) + Vector3.up * (w.Age * 0.012f) + wobble;
            w.Rotation = WordLayer.Facing(w.Position, view.transform.position);
            w.Color = new Color(0.86f, 0.97f, 1f);
            w.Halo = 0f;
            float height = SungWords.Height(w);
            float width = w.Width * height;
            var centre = w.Position + Vector3.up * (height * 0.36f);
            float radius = Mathf.Max(0.05f, width * 0.5f + 0.022f);
            if (w.Age > w.Life - 0.45f)
            {
                if (!popped[i])
                {
                    popped[i] = true;
                    for (int k = 0; k < 24; k++)
                    {
                        var d = Random.onUnitSphere;
                        Emit(falling, centre + d * radius, d * Random.Range(0.3f, 0.8f), Pick(Water), Random.Range(0.004f, 0.008f), Random.Range(0.4f, 0.8f));
                    }
                    voices.Play(splash, centre, 0.12f, 1.6f);
                }
                return false;
            }
            var b = bubbles[i];
            b.transform.position = centre;
            b.transform.localScale = Vector3.one * (radius * 2f * (1f + 0.035f * Mathf.Sin(w.Age * 5f + w.Seed * 20f) + music.Band(1) * 0.04f));
            Effects.Face(b.transform, view);
            block.Clear();
            block.SetColor(ColorId, new Color(0.45f, 0.8f, 1f, 1f));
            block.SetFloat(IconId, -1f);
            block.SetFloat(AlphaId, Mathf.Clamp01(w.Age / 0.3f) * FadeIn);
            b.SetPropertyBlock(block);
            return true;
        }

        /// <summary>A word of earth: carved into a rock's face, glowing hot at first and cooling to amber, shaking with it.</summary>
        void CarvedWord(int i, WordLayer.Word w, MusicFrame music)
        {
            var r = rocks[wordRock[i]];
            if (!r.Renderer.gameObject.activeSelf)
            {
                w.Alpha = 0f;
                return;
            }
            var face = r.Transform.rotation;
            w.Position = r.Transform.position + face * Vector3.up * (r.Height * 0.12f) + face * Vector3.back * (FrontFace * r.Depth + 0.006f);
            w.Rotation = face;
            float fresh = Mathf.Clamp01(1f - w.Age / 1.2f);
            w.Color = Color.Lerp(new Color(1f, 0.48f, 0.1f), new Color(1f, 0.78f, 0.3f), fresh * 0.7f + 0.3f);
            w.Halo = 0.2f + fresh * 0.15f + music.Kick * 0.15f;
            w.Alpha = Mathf.Clamp01((w.Position.y - r.Ground - 0.02f) / 0.05f);
        }

        /// <summary>A word of air: blown along on the wind with a streak of air behind it, bobbing as it slows.</summary>
        bool BlownWord(int i, WordLayer.Word w, Vector3 eye, MusicFrame music)
        {
            w.Position = BlownAt(i, w, w.Age);
            w.Rotation = WordLayer.Facing(w.Position, eye) * Quaternion.Euler(0f, 0f, Mathf.Sin(w.Age * 1.9f + w.Seed * 5f) * 6f);
            w.Color = new Color(0.95f, 0.99f, 1f);
            w.Halo = 0.06f + music.Kick * 0.15f;
            float height = SungWords.Height(w);
            var behind = w.Position - BlownAt(i, w, Mathf.Max(0f, w.Age - 0.05f));
            var tangent = behind.sqrMagnitude > 1e-8f ? behind.normalized : Vector3.zero;
            var back = tangent * (w.Width * height * 0.5f + 0.01f);
            for (int k = 0; k < TrailPoints; k++)
            {
                path[k] = BlownAt(i, w, Mathf.Max(0f, w.Age - k * 0.05f)) - back + Vector3.up * (height * 0.3f);
                pathColors[k] = new Color(1f, 1f, 1f, 1f - k / (float)(TrailPoints - 1));
            }
            float alpha = Mathf.Clamp01(w.Age / 0.25f) * Mathf.Clamp01((w.Life - w.Age) / 0.6f) * FadeIn;
            var tint = MainColors[Air];
            trails[i].Set(path, pathColors, TrailPoints, 0.006f, new Color(tint.r, tint.g, tint.b, alpha * 0.7f), eye, false);
            return true;
        }

        Vector3 BlownAt(int i, WordLayer.Word w, float age)
        {
            float u = Mathf.Clamp01(age / w.Life);
            float ease = 1f - Mathf.Pow(1f - u, 2.4f);
            var direction = Quaternion.AngleAxis(Mathf.Lerp(wordStart[i], wordEnd[i], ease), Vector3.up) * wordAim[i];
            return wordAnchor[i] + direction * 0.85f + Vector3.up * (wordLift[i] + Mathf.Sin(age * 2.3f + w.Seed * 6f) * 0.018f);
        }

        // ---- Small helpers ----

        /// <summary>
        /// Directions for winding things round a hand: along it (wrist to fingers), out of the palm, and across it.
        /// </summary>
        void Around(int s, out Vector3 axis, out Vector3 across, out Vector3 side)
        {
            axis = pointing[s];
            across = normal[s] - axis * Vector3.Dot(normal[s], axis);
            if (across.sqrMagnitude < 0.01f) across = Vector3.Cross(axis, Vector3.up);
            if (across.sqrMagnitude < 1e-6f) across = Vector3.right;
            across.Normalize();
            side = Vector3.Cross(axis, across);
        }

        static void Emit(ParticleSystem ps, Vector3 at, Vector3 velocity, Color color, float size, float life)
        {
            ps.Emit(new ParticleSystem.EmitParams { position = at, velocity = velocity, startColor = color, startSize = size, startLifetime = life }, 1);
        }

        /// <summary>
        /// A direction for something flying out from <paramref name="at"/>, turned away if it would head for your eyes, so
        /// nothing from a burst near your hands flies at your face.
        /// </summary>
        Vector3 AwayFromEyes(Vector3 direction, Vector3 at)
        {
            var eye = View != null ? View.transform.position : head;
            var toEye = eye - at;
            if (toEye.sqrMagnitude < 1e-6f) return direction;
            toEye.Normalize();
            float towards = Vector3.Dot(direction, toEye);
            return towards > 0f ? direction - toEye * (2f * towards) : direction;
        }

        /// <summary>One of the element's colours, at random.</summary>
        static Color Pick(int e)
        {
            float r = Random.value;
            return r < 0.45f ? MainColors[e] : r < 0.8f ? SecondColors[e] : BrightColors[e];
        }

        static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + 78.233f) * 43758.5453f, 1f);

        static void Show(Renderer r, bool visible)
        {
            if (r.gameObject.activeSelf != visible) r.gameObject.SetActive(visible);
        }

        /// <summary>The world's clock for the shaders, kept small (ten minutes round) so their patterns stay precise.</summary>
        float ShaderClock => Mathf.Repeat(Clock, 600f);

        /// <summary>
        /// For previews and screenshots: makes <paramref name="element"/> (0 fire, 1 water, 2 earth, 3 air) the one in
        /// charge at once, without a burst. With <paramref name="act"/>, the element also makes its own move on the next
        /// frame: a fireball thrown from the right hand, the rocks raised high, or a whirlwind spinning in front of you.
        /// </summary>
        public void PreviewElement(int element, bool act)
        {
            Build();
            current = Mathf.Clamp(element, 0, 3);
            for (int e = 0; e < 4; e++) presence[e] = e == current ? 1f : 0f;
            switchTime = -10f;
            themeFrom = Themes[current];
            HandSkin.Theme = Themes[current];
            if (!act) return;
            if (current == Fire) previewThrow = true;
            else if (current == Earth) previewLift = 0.95f;
            else if (current == Air) previewWhirl = true;
        }

        // ---- Meshes and sounds, made once ----

        /// <summary>
        /// A rock: a ball pushed in and out by a few broad bumps, sliced flat in places like split stone, with a flat face
        /// at the front (-z, where words are carved) and flat underneath. Each triangle has its own corners, so the
        /// facets stay crisp. Radius about 0.5.
        /// </summary>
        static Mesh BuildRock(int seed, float front)
        {
            var random = new System.Random(seed);
            var points = new List<Vector3>();
            var triangles = new List<int>();
            Icosphere(points, triangles, 2);
            var bumps = new Vector4[6];
            for (int k = 0; k < bumps.Length; k++)
            {
                var d = RandomDirection(random);
                bumps[k] = new Vector4(d.x, d.y, d.z, (float)random.NextDouble() * 6.28f);
            }
            var cuts = new Vector4[3];
            for (int k = 0; k < cuts.Length; k++)
            {
                var d = RandomDirection(random);
                cuts[k] = new Vector4(d.x, d.y, d.z, Mathf.Lerp(0.36f, 0.45f, (float)random.NextDouble()));
            }
            for (int i = 0; i < points.Count; i++)
            {
                var d = points[i];
                float r = 1f;
                for (int k = 0; k < bumps.Length; k++)
                    r += 0.09f * Mathf.Cos(Vector3.Dot(d, bumps[k]) * (2.2f + k * 0.6f) + bumps[k].w);
                var p = d * (0.5f * r);
                foreach (var cut in cuts)
                {
                    var n = (Vector3)cut;
                    float over = Vector3.Dot(p, n) - cut.w;
                    if (over > 0f) p -= n * over;
                }
                if (p.z < -front) p.z = -front;
                if (p.y < -0.5f) p.y = -0.5f;
                points[i] = p;
            }
            var vertices = new Vector3[triangles.Count];
            var normals = new Vector3[triangles.Count];
            var indices = new int[triangles.Count];
            for (int t = 0; t < triangles.Count; t += 3)
            {
                var a = points[triangles[t]];
                var b = points[triangles[t + 1]];
                var c = points[triangles[t + 2]];
                var n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, a + b + c) < 0f)
                {
                    (b, c) = (c, b);
                    n = -n;
                }
                n.Normalize();
                vertices[t] = a;
                vertices[t + 1] = b;
                vertices[t + 2] = c;
                normals[t] = normals[t + 1] = normals[t + 2] = n;
                indices[t] = t;
                indices[t + 1] = t + 1;
                indices[t + 2] = t + 2;
            }
            var mesh = new Mesh { name = "Rock" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 RandomDirection(System.Random random)
        {
            float z = (float)random.NextDouble() * 2f - 1f;
            float a = (float)random.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z);
        }

        /// <summary>A unit sphere from an icosahedron, each triangle split in four <paramref name="subdivisions"/> times.</summary>
        static void Icosphere(List<Vector3> points, List<int> triangles, int subdivisions)
        {
            float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var corners = new[]
            {
                new Vector3(-1f, g, 0f), new Vector3(1f, g, 0f), new Vector3(-1f, -g, 0f), new Vector3(1f, -g, 0f),
                new Vector3(0f, -1f, g), new Vector3(0f, 1f, g), new Vector3(0f, -1f, -g), new Vector3(0f, 1f, -g),
                new Vector3(g, 0f, -1f), new Vector3(g, 0f, 1f), new Vector3(-g, 0f, -1f), new Vector3(-g, 0f, 1f)
            };
            foreach (var c in corners) points.Add(c.normalized);
            triangles.AddRange(new[]
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            });
            var middles = new Dictionary<long, int>();
            int Middle(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (middles.TryGetValue(key, out int m)) return m;
                m = points.Count;
                points.Add(((points[a] + points[b]) * 0.5f).normalized);
                middles[key] = m;
                return m;
            }
            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles.Clear();
                triangles.AddRange(next);
            }
        }

        /// <summary>
        /// The ball of water's mesh: the six faces of a cube, each a fine grid, blown out into a ball of radius 0.5, its
        /// triangles all facing out. The Orb shader bends it into its wobbling shape.
        /// </summary>
        static Mesh BuildBall(int grid)
        {
            int side = grid + 1;
            var vertices = new Vector3[6 * side * side];
            var triangles = new int[6 * grid * grid * 6];
            Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int t = 0;
            void Add(int a, int b, int c)
            {
                if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), vertices[a]) < 0f) (b, c) = (c, b);
                triangles[t++] = a;
                triangles[t++] = b;
                triangles[t++] = c;
            }
            for (int f = 0; f < faces.Length; f++)
            {
                var normal = faces[f];
                var across = new Vector3(normal.y, normal.z, normal.x);
                var along = Vector3.Cross(normal, across);
                int start = f * side * side;
                for (int y = 0; y < side; y++)
                    for (int x = 0; x < side; x++)
                    {
                        float u = Mathf.Tan((x / (float)grid - 0.5f) * Mathf.PI * 0.5f);
                        float v = Mathf.Tan((y / (float)grid - 0.5f) * Mathf.PI * 0.5f);
                        vertices[start + y * side + x] = (normal + across * u + along * v).normalized * 0.5f;
                    }
                for (int y = 0; y < grid; y++)
                    for (int x = 0; x < grid; x++)
                    {
                        int i = start + y * side + x;
                        Add(i, i + side, i + 1);
                        Add(i + 1, i + side, i + side + 1);
                    }
            }
            var mesh = new Mesh { name = "Water orb" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            // The shader makes it wobble, so keep the bounds generous.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1.6f);
            return mesh;
        }

        static void MakeSounds()
        {
            if (whoosh != null) return;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            whoosh = WhooshSound(rate);
            crackle = CrackleSound(rate);
            splash = SplashSound(rate);
            rumble = RumbleSound(rate);
        }

        /// <summary>A soft rush of air: noise through a filter that opens and closes again.</summary>
        static AudioClip WhooshSound(int rate)
        {
            int n = (int)(rate * 0.55f);
            var data = new float[n];
            var random = new System.Random(7);
            float low = 0f, floor = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float swell = Mathf.Sin(Mathf.Clamp01(t / 0.55f) * Mathf.PI);
                float cutoff = 250f + 2200f * swell * swell;
                low += ((float)(random.NextDouble() * 2.0 - 1.0) - low) * (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / rate));
                floor += (low - floor) * 0.02f;
                data[i] = (low - floor) * swell * 0.7f;
            }
            return Store("Four elements whoosh", data, rate);
        }

        /// <summary>The crackle of a fireball bursting: sharp little pops over a short hiss.</summary>
        static AudioClip CrackleSound(int rate)
        {
            int n = (int)(rate * 0.6f);
            var data = new float[n];
            var random = new System.Random(11);
            float pop = 0f, hiss = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                pop *= 0.93f;
                if (random.NextDouble() < 0.0022 * Mathf.Exp(-t * 3f)) pop = (float)(random.NextDouble() * 2.0 - 1.0);
                hiss += (white - hiss) * 0.4f;
                data[i] = (pop * 0.8f + hiss * 0.3f * Mathf.Exp(-t * 9f)) * Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 4f) * 0.55f;
            }
            return Store("Four elements crackle", data, rate);
        }

        /// <summary>A splash: a bright burst of noise and a few rising bubble blips.</summary>
        static AudioClip SplashSound(int rate)
        {
            int n = (int)(rate * 0.45f);
            var data = new float[n];
            var random = new System.Random(13);
            float low = 0f;
            var starts = new float[4];
            var pitches = new float[4];
            for (int k = 0; k < starts.Length; k++)
            {
                starts[k] = 0.02f + (float)random.NextDouble() * 0.18f;
                pitches[k] = 500f + (float)random.NextDouble() * 700f;
            }
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.12f;
                float s = (white - low) * Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 11f) * 0.5f;
                for (int k = 0; k < starts.Length; k++)
                {
                    float u = t - starts[k];
                    if (u < 0f) continue;
                    s += Mathf.Sin(2f * Mathf.PI * pitches[k] * u * (1f + u * 6f)) * Mathf.Exp(-u * 35f) * 0.25f;
                }
                data[i] = s * 0.8f;
            }
            return Store("Four elements splash", data, rate);
        }

        /// <summary>The low grind of rock moving: deep filtered noise with a hum under it.</summary>
        static AudioClip RumbleSound(int rate)
        {
            int n = (int)(rate * 0.9f);
            var data = new float[n];
            var random = new System.Random(17);
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * 90f / rate);
            float low = 0f, lower = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                low += ((float)(random.NextDouble() * 2.0 - 1.0) - low) * a;
                lower += (low - lower) * a;
                float env = Mathf.Min(1f, t / 0.08f) * Mathf.Exp(-t * 2.6f);
                data[i] = (lower * 9f + Mathf.Sin(2f * Mathf.PI * 48f * t) * 0.3f * (0.7f + 0.3f * Mathf.Sin(t * 37f))) * env * 0.6f;
            }
            return Store("Four elements rumble", data, rate);
        }

        static AudioClip Store(string name, float[] data, int rate)
        {
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
