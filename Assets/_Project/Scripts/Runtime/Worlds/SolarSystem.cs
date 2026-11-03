using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Solar system world: a small solar system floats in front of you at chest height, about a metre across and
    /// tipped a little towards you. A glowing sun sits in the middle and the eight planets circle it on their own
    /// slightly tilted orbits, sized and spaced so all of them read at this scale, the inner ones faster than the outer
    /// (Kepler's rule, sped up). Faint orbit rings trail light behind each planet; Saturn has its rings, Earth its Moon,
    /// Jupiter its four big moons and Saturn Titan, and a belt of glinting asteroids turns between Mars and Jupiter.
    /// The music: the sun's glow and corona pulse with the kick and the bass and flare on every beat, when a ripple of
    /// light also runs out through the orbits; each planet brightens with its own band (the big outer planets with the
    /// low ones, the small inner planets with the high ones); everything turns faster as the song gets louder; and
    /// melody notes make planets twinkle. Each sung word is a small comet with a glowing tail pointing away from the
    /// sun: it swoops in from behind the sun and slows down at its place on an arc above it, as you look, the words of
    /// a line laid out across the arc from left to right so the line reads like a sentence, then it flies off as it
    /// fades.
    /// Your hands: bring your fingers close to a planet and a ring lights round it; pinch to pick it up and carry it
    /// anywhere, while a dashed ring shows the orbit it will take. Let go and it settles onto that orbit, at the speed
    /// that distance gives it. Pinch empty space with both hands and pull them apart or together to resize the whole
    /// system (and move them to move it). Hold an open palm to the sun and it warms: it burns brighter, its corona
    /// reaches out, embers drift to your hand and a soft tone rises.
    /// </summary>
    public class SolarSystem : World
    {
        public override string DisplayName => "Solar system";

        public override Color Color => new Color(1f, 0.64f, 0.26f);

        public override int MenuOrder => 115;

        public override int MenuIcon => 25;

        public override string Hint => "Pinch a planet to move it; pinch with both hands to resize";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.1f, 0.08f, 0.16f, 0.5f),
            Edge = new Color(1f, 0.8f, 0.55f),
            Glow = new Color(1f, 0.55f, 0.25f),
            Pattern = HandTheme.Effect.Heat,
            PatternColor = new Color(1f, 0.62f, 0.3f)
        };

        [Header("Materials")]
        [WorldMaterial("SolarSystemPlanet")]
        [SerializeField] Material planetMaterial;
        [WorldMaterial("SolarSystemCorona")]
        [SerializeField] Material coronaMaterial;
        [WorldMaterial("SolarSystemOrbit")]
        [SerializeField] Material orbitMaterial;
        [WorldMaterial("SolarSystemBelt")]
        [SerializeField] Material beltMaterial;
        [WorldMaterial("SolarSystemRings")]
        [SerializeField] Material ringsMaterial;
        [SerializeField] Material ringMaterial;
        [SerializeField] Material lineMaterial;

        [Header("Placing")]
        [Tooltip("How far in front of your eyes the sun floats (m), and how far below them.")]
        [SerializeField] float distance = 0.68f;
        [SerializeField] float drop = 0.27f;
        [Tooltip("How far the system is tipped towards you (degrees), so its orbits read as open ellipses.")]
        [SerializeField] float tilt = 24f;
        [Tooltip("The smallest and largest size pinching with both hands can give the system.")]
        [SerializeField] Vector2 scaleRange = new Vector2(0.5f, 2f);

        [Header("Orbits")]
        [Tooltip("Seconds Earth takes to go round the sun; the others follow Kepler's third law from their distance.")]
        [SerializeField] float earthYear = 16f;
        [Tooltip("How much faster everything turns when the music is at its loudest (1 = twice as fast).")]
        [SerializeField] float speedSwell = 0.8f;
        [Tooltip("The closest and furthest orbit (m from the sun at full size) a planet can be let go into.")]
        [SerializeField] Vector2 orbitRange = new Vector2(0.075f, 0.62f);
        [Tooltip("How many glinting asteroids the belt between Mars and Jupiter holds (all one mesh).")]
        [SerializeField] int asteroids = 520;

        [Header("Hands")]
        [Tooltip("How close (m) a pinch must come to a planet's surface to pick it up.")]
        [SerializeField] float reach = 0.035f;
        [Tooltip("How close (m) an open palm must come to the sun's surface to warm it.")]
        [SerializeField] float warmReach = 0.17f;
        [Tooltip("Seconds of an open palm by the sun to warm it fully.")]
        [SerializeField] float warmUp = 1.5f;

        [Header("Comets")]
        [Tooltip("Seconds a sung word's comet takes to swing round the sun.")]
        [SerializeField] float cometLife = 3.6f;
        [Tooltip("How close the comets pass the sun at least and at most (m at full size): a long line needs a wider arc.")]
        [SerializeField] Vector2 cometReach = new Vector2(0.27f, 0.42f);
        [Tooltip("How far round the sun (degrees) a comet swings in from, behind it, to reach its place.")]
        [SerializeField] float cometSwing = 60f;

        static readonly int KindId = Shader.PropertyToID("_Kind");
        static readonly int ColorAId = Shader.PropertyToID("_ColorA");
        static readonly int ColorBId = Shader.PropertyToID("_ColorB");
        static readonly int ColorCId = Shader.PropertyToID("_ColorC");
        static readonly int AirId = Shader.PropertyToID("_Air");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int SpotId = Shader.PropertyToID("_Spot");
        static readonly int SunPosId = Shader.PropertyToID("_SunPos");
        static readonly int SunPowerId = Shader.PropertyToID("_SunPower");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int TwinkleId = Shader.PropertyToID("_Twinkle");
        static readonly int HighlightId = Shader.PropertyToID("_Highlight");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int CoreId = Shader.PropertyToID("_Core");
        static readonly int DiscId = Shader.PropertyToID("_Disc");
        static readonly int FlareId = Shader.PropertyToID("_Flare");
        static readonly int FlareAgeId = Shader.PropertyToID("_FlareAge");
        static readonly int WarmId = Shader.PropertyToID("_Warm");
        static readonly int WidthId = Shader.PropertyToID("_Width");
        static readonly int HeadId = Shader.PropertyToID("_Head");
        static readonly int TrailId = Shader.PropertyToID("_Trail");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int DashesId = Shader.PropertyToID("_Dashes");
        static readonly int OrbitId = Shader.PropertyToID("_Orbit");
        static readonly int OmegaId = Shader.PropertyToID("_Omega");
        static readonly int ReferenceId = Shader.PropertyToID("_Reference");
        static readonly int PlanetPosId = Shader.PropertyToID("_PlanetPos");
        static readonly int PlanetRadiusId = Shader.PropertyToID("_PlanetRadius");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int InnerGlowId = Shader.PropertyToID("_InnerGlow");

        /// <summary>The sun's radius and Earth's orbit, in metres at full size.</summary>
        const float SunRadius = 0.044f;
        const float EarthOrbit = 0.16f;
        /// <summary>How far out the system reaches (Neptune's orbit and a little more), in metres at full size.</summary>
        const float OuterEdge = 0.56f;
        const int Saturn = 5;
        const int RingSegments = 160;
        const int TailPoints = 18;
        const int BridgePoints = 16;
        const int DropPoints = 6;
        /// <summary>Room between two words of a line laid out along the comets' arc, in letter heights.</summary>
        const float WordGap = 0.8f;
        static readonly Vector2 BeltRadii = new Vector2(0.232f, 0.278f);
        static readonly Color CometColor = new Color(0.82f, 0.94f, 1f);
        static readonly Color SunGold = new Color(1f, 0.78f, 0.42f);
        static readonly Color Ember = new Color(1f, 0.45f, 0.14f);

        /// <summary>How a body's surface is drawn: the planet shader's _Kind.</summary>
        enum Look { Sun = 0, Rocky = 1, Cloudy = 2, Earth = 3, Mars = 4, Banded = 5, Ice = 6 }

        /// <summary>
        /// A planet: its size and orbit in metres at full size, how its orbit and its axis are tipped (degrees), its day
        /// (seconds, negative for the planets that turn backwards), where it starts round the sun, its look and the band
        /// of the music it shines with; then what changes as it goes round and as your hands move it.
        /// </summary>
        sealed class Planet
        {
            public string Name;
            public float Size, Home, Inclination, Node, Axis, Day, Start;
            public Look Look;
            public int Band;
            public Color A, B, C, Air;
            public float Seed, Spot;

            public Transform Body;
            public Renderer Renderer, Halo, Ring;
            public Quaternion Plane;
            /// <summary>The orbit it is on (m at full size), how far round it is (radians) and how fast it goes.</summary>
            public float Orbit, Angle, Speed, Spin;
            /// <summary>Where it is now, and where it was let go, in the system's frame.</summary>
            public Vector3 Local, From;
            /// <summary>0 when it has just been let go, 1 once it has glided onto its new orbit.</summary>
            public float Settle = 1f;
            public bool Held;
            public float Glow, Twinkle, Lit, Flash;
        }

        /// <summary>A moon: which planet it circles, its size, distance (m at full size) and period (s).</summary>
        sealed class Moon
        {
            public int Planet;
            public float Size, Distance, Period, Start, Tilt;
            /// <summary>Circles in its planet's equator (with Saturn's rings), rather than near the planets' plane.</summary>
            public bool Equator;
            public Look Look;
            public Color A, B;
            public float Seed;
            public Transform Body;
            public Renderer Renderer;
            public float Angle;
        }

        /// <summary>One hand: what it is near, what it carries, and its feedback (the reach ring, the new orbit, the light between pinched fingers).</summary>
        sealed class Grip
        {
            public bool Tracked, Free;
            public Vector3 Point, Offset;
            public float Pinch;
            /// <summary>The planet within reach, the planet carried, and the planet the reach ring is on (it stays while the ring fades).</summary>
            public int Near = -1, Held = -1, Shown = -1;
            public float Light;
            /// <summary>The orbit the carried planet would take if let go now: radius (m at full size) and angle (radians).</summary>
            public float Radius, Angle;
            public int PreviewPlanet = -1;
            public float PreviewRadius, PreviewAngle, PreviewAlpha;
            public Renderer Reach, Nib, Preview;
            public LineStrip Drop;

            public void Clear()
            {
                Free = false;
                Near = Held = Shown = PreviewPlanet = -1;
                Light = PreviewAlpha = 0f;
                Offset = Vector3.zero;
            }
        }

        Transform system, sun;
        Renderer sunRenderer, corona, belt, rings;
        Planet[] planets;
        Moon[] moons;
        readonly Grip[] grips = { new Grip(), new Grip() };
        Renderer[] heads;
        LineStrip[] tails;
        /// <summary>
        /// Per sung word (by its place in SungWords.Words): how far from the sun its comet slows down (m at full size,
        /// negative for a word on the left half of the arc, which drifts to the left), and where round the arc that is
        /// (degrees from the point straight behind the sun, which the word's Spot holds as a direction).
        /// </summary>
        float[] cometArc, cometAngle;
        LineStrip bridge;
        Vector3[] path;
        Color[] pathColors;
        float[] pathWidths;
        ParticleSystem sparks;
        AudioPool voices;
        AudioSource sunVoice;
        MaterialPropertyBlock block;

        Vector3 centre, warmPalm, resizeMid;
        Quaternion facing = Quaternion.identity;
        float scale = 1f, shownScale = 1f, resizeFrom, resizeScale, resizeLight;
        float orbitClock, pace = 1f, energy, sunLight = 1f;
        float flare, flareAge = 9f, warmth, emberDebt;
        bool resizing, burst;

        protected override float FadeInSeconds => 2f;

        protected override int SungWordCount => 8;

        protected override float SungWordHeight => 0.04f;

        protected override float SungWordsAlpha => FadeIn;

        static Planet[] MakePlanets() => new[]
        {
            new Planet
            {
                Name = "Mercury", Size = 0.0085f, Home = 0.085f, Inclination = 9f, Node = 30f, Axis = 2f, Day = 20f, Start = 35f,
                Look = Look.Rocky, Band = 5, A = new Color(0.66f, 0.62f, 0.58f), B = new Color(0.38f, 0.35f, 0.33f), C = Color.white,
                Air = new Color(0.85f, 0.82f, 0.78f, 0.35f), Seed = 1.3f
            },
            new Planet
            {
                Name = "Venus", Size = 0.013f, Home = 0.12f, Inclination = 4f, Node = 80f, Axis = 3f, Day = -30f, Start = 160f,
                Look = Look.Cloudy, Band = 4, A = new Color(0.98f, 0.9f, 0.68f), B = new Color(0.86f, 0.68f, 0.42f), C = Color.white,
                Air = new Color(1f, 0.88f, 0.6f, 0.7f), Seed = 2.7f
            },
            new Planet
            {
                Name = "Earth", Size = 0.0135f, Home = EarthOrbit, Inclination = 1.5f, Node = 0f, Axis = 23.4f, Day = 6f, Start = 250f,
                Look = Look.Earth, Band = 3, A = new Color(0.07f, 0.25f, 0.65f), B = new Color(0.22f, 0.5f, 0.2f), C = new Color(0.72f, 0.62f, 0.4f),
                Air = new Color(0.45f, 0.72f, 1f, 1f), Seed = 4.1f
            },
            new Planet
            {
                Name = "Mars", Size = 0.0105f, Home = 0.205f, Inclination = 3f, Node = 140f, Axis = 25f, Day = 6.2f, Start = 110f,
                Look = Look.Mars, Band = 2, A = new Color(0.88f, 0.43f, 0.2f), B = new Color(0.56f, 0.24f, 0.12f), C = Color.white,
                Air = new Color(1f, 0.62f, 0.42f, 0.45f), Seed = 5.9f
            },
            new Planet
            {
                Name = "Jupiter", Size = 0.03f, Home = 0.31f, Inclination = 2f, Node = 200f, Axis = 3f, Day = 4f, Start = 310f,
                Look = Look.Banded, Band = 1, A = new Color(0.94f, 0.87f, 0.75f), B = new Color(0.74f, 0.52f, 0.34f), C = new Color(0.84f, 0.38f, 0.22f),
                Air = new Color(1f, 0.85f, 0.65f, 0.6f), Seed = 7.2f, Spot = 1f
            },
            new Planet
            {
                Name = "Saturn", Size = 0.025f, Home = 0.39f, Inclination = 3.5f, Node = 260f, Axis = 26.7f, Day = 4.4f, Start = 200f,
                Look = Look.Banded, Band = 1, A = new Color(0.96f, 0.88f, 0.66f), B = new Color(0.82f, 0.7f, 0.47f), C = new Color(0.9f, 0.8f, 0.6f),
                Air = new Color(1f, 0.9f, 0.65f, 0.55f), Seed = 8.8f
            },
            new Planet
            {
                Name = "Uranus", Size = 0.018f, Home = 0.46f, Inclination = 2f, Node = 320f, Axis = 98f, Day = -5f, Start = 60f,
                Look = Look.Ice, Band = 0, A = new Color(0.64f, 0.9f, 0.93f), B = new Color(0.5f, 0.8f, 0.86f), C = new Color(0.42f, 0.72f, 0.82f),
                Air = new Color(0.62f, 0.96f, 1f, 0.7f), Seed = 10.4f
            },
            new Planet
            {
                Name = "Neptune", Size = 0.0175f, Home = 0.53f, Inclination = 3f, Node = 100f, Axis = 28f, Day = 4.6f, Start = 145f,
                Look = Look.Ice, Band = 0, A = new Color(0.28f, 0.46f, 0.98f), B = new Color(0.17f, 0.3f, 0.8f), C = new Color(0.08f, 0.14f, 0.42f),
                Air = new Color(0.46f, 0.62f, 1f, 0.8f), Seed = 12.6f, Spot = 1f
            }
        };

        static Moon[] MakeMoons() => new[]
        {
            // The Moon, its orbit tipped a few degrees.
            new Moon { Planet = 2, Size = 0.0038f, Distance = 0.027f, Period = 4f, Start = 0f, Tilt = 5f, Look = Look.Rocky,
                A = new Color(0.8f, 0.79f, 0.77f), B = new Color(0.46f, 0.46f, 0.48f), Seed = 20f },
            // Jupiter's four big moons, keeping step with each other (1 : 2 : 4) as the real ones do.
            new Moon { Planet = 4, Size = 0.0028f, Distance = 0.042f, Period = 2f, Start = 40f, Equator = true, Look = Look.Rocky,
                A = new Color(0.98f, 0.86f, 0.42f), B = new Color(0.84f, 0.5f, 0.22f), Seed = 21f },
            new Moon { Planet = 4, Size = 0.0025f, Distance = 0.05f, Period = 4f, Start = 170f, Equator = true, Look = Look.Rocky,
                A = new Color(0.95f, 0.91f, 0.85f), B = new Color(0.7f, 0.56f, 0.43f), Seed = 22f },
            new Moon { Planet = 4, Size = 0.0034f, Distance = 0.06f, Period = 8f, Start = 260f, Equator = true, Look = Look.Rocky,
                A = new Color(0.64f, 0.59f, 0.54f), B = new Color(0.43f, 0.39f, 0.36f), Seed = 23f },
            new Moon { Planet = 4, Size = 0.0031f, Distance = 0.071f, Period = 16f, Start = 320f, Equator = true, Look = Look.Rocky,
                A = new Color(0.47f, 0.43f, 0.39f), B = new Color(0.31f, 0.28f, 0.26f), Seed = 24f },
            // Titan in its orange haze, outside Saturn's rings, in their plane.
            new Moon { Planet = Saturn, Size = 0.0034f, Distance = 0.074f, Period = 7f, Start = 120f, Equator = true, Look = Look.Cloudy,
                A = new Color(0.94f, 0.7f, 0.34f), B = new Color(0.78f, 0.52f, 0.25f), Seed = 25f }
        };

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            system = new GameObject("System").transform;
            system.SetParent(transform, false);

            sunRenderer = MakeSphere("Sun", system);
            sun = sunRenderer.transform;
            corona = Effects.MakeQuad("Corona", transform, coronaMaterial);

            var ringMesh = BuildRingMesh(RingSegments);
            planets = MakePlanets();
            foreach (var p in planets)
            {
                p.Renderer = MakeSphere(p.Name, system);
                p.Body = p.Renderer.transform;
                p.Halo = Effects.MakeQuad($"{p.Name} Glow", transform, glowMaterial);
                p.Ring = MakeRing($"{p.Name} Orbit", ringMesh);
                p.Plane = Quaternion.Euler(0f, p.Node, 0f) * Quaternion.Euler(p.Inclination, 0f, 0f);
            }
            // Saturn's rings lie in its equator, out to 2.3 times its radius (the sphere's radius is 0.5).
            rings = Effects.MakeQuad("Saturn Rings", planets[Saturn].Body, ringsMaterial);
            rings.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rings.transform.localScale = Vector3.one * 2.3f;

            moons = MakeMoons();
            foreach (var m in moons)
            {
                m.Renderer = MakeSphere($"Moon of {planets[m.Planet].Name}", system);
                m.Body = m.Renderer.transform;
            }

            var beltObject = new GameObject("Asteroid Belt");
            beltObject.transform.SetParent(system, false);
            beltObject.AddComponent<MeshFilter>().sharedMesh = BuildBeltMesh(asteroids, BeltRadii);
            belt = beltObject.AddComponent<MeshRenderer>();
            belt.sharedMaterial = beltMaterial;
            belt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            belt.receiveShadows = false;

            heads = new Renderer[SungWordCount];
            tails = new LineStrip[SungWordCount];
            cometArc = new float[SungWordCount];
            cometAngle = new float[SungWordCount];
            for (int i = 0; i < heads.Length; i++)
            {
                heads[i] = Effects.MakeQuad($"Comet {i}", transform, glowMaterial);
                heads[i].gameObject.SetActive(false);
                tails[i] = new LineStrip($"Comet Tail {i}", transform, lineMaterial, TailPoints);
                tails[i].SetVisible(false);
            }
            path = new Vector3[TailPoints];
            pathColors = new Color[TailPoints];
            pathWidths = new float[TailPoints];

            for (int s = 0; s < 2; s++)
            {
                var grip = grips[s];
                string side = s == 0 ? "L" : "R";
                grip.Reach = Effects.MakeQuad($"Reach {side}", transform, ringMaterial);
                grip.Nib = Effects.MakeQuad($"Pinch {side}", transform, glowMaterial);
                grip.Preview = MakeRing($"New Orbit {side}", ringMesh);
                grip.Drop = new LineStrip($"Drop {side}", transform, lineMaterial, DropPoints);
                Show(grip.Reach, false);
                Show(grip.Nib, false);
                Show(grip.Preview, false);
                grip.Drop.SetVisible(false);
            }
            bridge = new LineStrip("Resize", transform, lineMaterial, BridgePoints);
            bridge.SetVisible(false);

            sparks = Effects.MakeSparkSystem("Sparks", transform, glowMaterial);
            var main = sparks.main;
            main.maxParticles = 500;
            voices = new AudioPool(transform, 6);
            var voice = new GameObject("Sun Voice");
            voice.transform.SetParent(transform, false);
            sunVoice = voice.AddComponent<AudioSource>();
            sunVoice.playOnAwake = false;
            sunVoice.loop = true;
            sunVoice.spatialBlend = 1f;
            sunVoice.minDistance = 0.4f;
            sunVoice.maxDistance = 6f;
            sunVoice.dopplerLevel = 0f;
            sunVoice.volume = 0f;

            ResetSystem();
        }

        protected override void OnEnter() => ResetSystem();

        protected override void OnExit()
        {
            foreach (var h in heads) Show(h, false);
            foreach (var t in tails) t.SetVisible(false);
            foreach (var g in grips)
            {
                Show(g.Reach, false);
                Show(g.Nib, false);
                Show(g.Preview, false);
                g.Drop.SetVisible(false);
            }
            bridge.SetVisible(false);
            sparks.Clear();
            if (sunVoice != null) sunVoice.Stop();
            HandSkin.FocusStrength = 0f;
        }

        /// <summary>Everything as it starts: the planets on their own orbits, the system at full size, the sun calm.</summary>
        void ResetSystem()
        {
            scale = shownScale = 1f;
            orbitClock = energy = warmth = flare = resizeLight = 0f;
            flareAge = 9f;
            resizing = burst = false;
            foreach (var p in planets)
            {
                p.Orbit = p.Home;
                p.Angle = p.Start * Mathf.Deg2Rad;
                p.Speed = Kepler(p.Orbit);
                p.Spin = 0f;
                p.Held = false;
                p.Settle = 1f;
                p.Glow = p.Twinkle = p.Lit = p.Flash = 0f;
                p.Local = OnOrbit(p);
            }
            foreach (var m in moons) m.Angle = m.Start * Mathf.Deg2Rad;
            foreach (var g in grips) g.Clear();
        }

        /// <summary>
        /// The sun floats in front of you at chest height, the system tipped towards you. In a small room it comes a
        /// little closer, so its far side stays clear of the wall ahead, and it never sits down on a table or the floor.
        /// </summary>
        protected override void OnPlace(Camera view)
        {
            var eye = view.transform.position;
            var forward = Effects.FlatForward(view);
            float away = distance;
            if (Room.WallAhead(eye, forward, distance + OuterEdge * scale + 0.3f, out _, out var wall))
            {
                float room = Vector3.ProjectOnPlane(wall - eye, Vector3.up).magnitude;
                away = Mathf.Clamp(room - OuterEdge * scale - 0.08f, 0.45f, distance);
            }
            centre = eye + forward * away + Vector3.down * drop;
            float ground = Room.GroundBelow(centre, Effects.FloorY(view));
            centre.y = Mathf.Max(centre.y, ground + 0.3f);
            facing = Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>Each beat flares the sun and sends a ripple of light out through the orbits.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            flareAge = 0f;
            flare = Mathf.Clamp01(0.55f + music.Kick * 0.45f);
        }

        /// <summary>A melody note makes a planet twinkle: high notes the inner planets, low notes the outer ones.</summary>
        protected override void OnNote(NoteTrack.Note note)
        {
            int midi = note.Midi;
            while (midi < 58) midi += 12;
            while (midi > 80) midi -= 12;
            int i = Mathf.Clamp(7 - Mathf.RoundToInt((midi - 60) * 7f / 16f), 0, planets.Length - 1);
            var p = planets[i];
            p.Twinkle = 1f;
            Burst(p.Body.position, Color.Lerp(p.Air, Color.white, 0.5f), 7, 0.05f);
        }

        /// <summary>
        /// Each sung word becomes a comet. The words of a line slow down at their own places along an arc on the far
        /// side of the sun (above it, as you look), laid out like the line itself: each word as far along the arc as
        /// its middle is along the line, so the line reads from left to right and long words get the room they need
        /// (a long line widens the arc). Comets on the left half travel to the left and those on the right half to
        /// the right, so they come in from behind the sun and neighbours drift apart. A new line gives the last
        /// one's words a second at most to fade.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            var words = SungWords.Words;
            if (newLine)
                for (int i = 0; i < words.Count; i++)
                    if (words[i].Active) words[i].Life = Mathf.Min(words[i].Life, words[i].Age + 1f);
            var w = SungWords.Add(word, CometColor, cometLife);

            var texts = Sung.LineTexts;
            float total = 0f, middle = 0f;
            for (int k = 0; k < texts.Length; k++)
            {
                if (k > 0) total += WordGap;
                float width = TypeFace.Bold.Width(texts[k]);
                if (k == Sung.Index) middle = total + width * 0.5f;
                total += width;
            }
            if (Sung.Index >= texts.Length) middle = total * 0.5f;
            // Metres from the middle of the line, and an arc wide enough that the whole line fits across it.
            float along = (middle - total * 0.5f) * SungWordHeight;
            float reachOut = Mathf.Clamp(total * SungWordHeight * 0.5f / 0.9f, cometReach.x, cometReach.y);
            float angle = Mathf.Asin(Mathf.Clamp(along / reachOut, -0.95f, 0.95f)) * Mathf.Rad2Deg;

            var eye = system.InverseTransformPoint(View.transform.position);
            var away = new Vector3(-eye.x, 0f, -eye.z);
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward;
            w.Spot = new Vector2(away.x, away.z);
            int slot = 0;
            while (slot < words.Count - 1 && words[slot] != w) slot++;
            cometArc[slot] = angle < 0f ? -reachOut : reachOut;
            cometAngle[slot] = angle;
        }

        /// <summary>A pinch close to a planet picks it up; a pinch in empty space may be half of a two-hand resize.</summary>
        protected override void OnPinch(HandRig.Hand hand, bool left)
        {
            var grip = grips[left ? 0 : 1];
            grip.Point = PinchPoint(hand);
            int near = Nearest(grip.Point);
            if (near >= 0) PickUp(grip, near);
            else grip.Free = true;
        }

        protected override void OnRelease(HandRig.Hand hand, bool left)
        {
            var grip = grips[left ? 0 : 1];
            if (grip.Held >= 0) LetGo(grip);
            grip.Free = false;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Listen(music, dt);
            Turn(dt);
            shownScale = Mathf.Lerp(shownScale, scale, 1f - Mathf.Exp(-20f * dt));
            system.SetPositionAndRotation(centre, facing * Quaternion.Euler(-tilt, 0f, 0f));
            system.localScale = Vector3.one * shownScale;
            if (rig != null)
            {
                Hands(rig, dt);
                Warm(rig, music, dt);
            }
            DrawSun(music, view);
            DrawPlanets(music, view);
            DrawComets(music, view);
            DrawHands(view, dt);
            Hum(music);
            HandSkin.FocusPosition = sun.position;
            HandSkin.FocusStrength = (0.35f + warmth * 0.65f) * FadeIn;
        }

        // ---- The music ----

        /// <summary>Follows the music: how loud it is (the orbits' pace), the sunlight, each planet's band.</summary>
        void Listen(MusicFrame music, float dt)
        {
            float loud = (music.Band(1) + music.Band(2) + music.Band(3) + music.Band(4)) * 0.25f;
            energy = Mathf.Lerp(energy, Mathf.Clamp01(loud * 1.15f + music.Kick * 0.1f), 1f - Mathf.Exp(-2.5f * dt));
            pace = 1f + energy * speedSwell;
            flareAge += dt;
            sunLight = 1f + music.Kick * 0.25f + music.Band(1) * 0.15f + warmth * 0.5f;
            foreach (var p in planets)
            {
                float band = Mathf.Clamp01((music.Band(p.Band) - 0.2f) * 1.4f);
                p.Glow = Mathf.Lerp(p.Glow, band, 1f - Mathf.Exp(-10f * dt));
                p.Twinkle = Mathf.MoveTowards(p.Twinkle, 0f, dt * 2f);
                p.Flash = Mathf.MoveTowards(p.Flash, 0f, dt * 1.2f);
            }
        }

        /// <summary>The beat's ripple of light at an orbit of the given radius (m at full size), as it runs outwards.</summary>
        float Ripple(float radius)
        {
            float x = (flareAge * 1.3f - radius) / 0.06f;
            return Mathf.Exp(-x * x) * Mathf.Exp(-flareAge * 1.2f) * Mathf.Min(flare, 1f) * 0.8f;
        }

        // ---- The orbits ----

        /// <summary>Angular speed (rad/s) on an orbit of the given radius: Kepler's third law, scaled to Earth's year.</summary>
        float Kepler(float radius) => Mathf.PI * 2f / earthYear * Mathf.Pow(EarthOrbit / Mathf.Max(radius, 0.02f), 1.5f);

        static Vector3 OnOrbit(Planet p) => p.Plane * new Vector3(Mathf.Cos(p.Angle), 0f, Mathf.Sin(p.Angle)) * p.Orbit;

        /// <summary>
        /// Moves everything round. A planet's speed eases towards the one its orbit gives it, so one let go into a new
        /// orbit speeds up or slows down smoothly while it glides onto that orbit.
        /// </summary>
        void Turn(float dt)
        {
            orbitClock += dt * pace;
            foreach (var p in planets)
            {
                p.Speed = Mathf.Lerp(p.Speed, Kepler(p.Orbit), 1f - Mathf.Exp(-1.8f * dt));
                p.Spin = Mathf.Repeat(p.Spin + 360f / p.Day * dt * pace, 360f);
                if (p.Held) continue;
                p.Angle = Mathf.Repeat(p.Angle + p.Speed * dt * pace, Mathf.PI * 2f);
                p.Settle = Mathf.MoveTowards(p.Settle, 1f, dt / 0.9f);
                var target = OnOrbit(p);
                p.Local = p.Settle < 1f ? Vector3.Lerp(p.From, target, Mathf.SmoothStep(0f, 1f, p.Settle)) : target;
            }
            foreach (var m in moons) m.Angle = Mathf.Repeat(m.Angle + Mathf.PI * 2f / m.Period * dt * pace, Mathf.PI * 2f);
        }

        /// <summary>The sung words' size: they follow the system's size, but only so far, to stay readable.</summary>
        float WordSize => Mathf.Clamp(shownScale, 0.8f, 1.5f);

        /// <summary>How far body k has formed as the world fades in (0 the sun, 1-8 the planets outwards): the sun first, then each planet in turn.</summary>
        float Grow(float k) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(FadeIn * 2.4f - k * 0.15f));

        // ---- The hands ----

        static Vector3 PinchPoint(HandRig.Hand hand)
        {
            var thumb = hand.TipJoints[0] != null ? hand.TipJoints[0].position : hand.Tips[0];
            var index = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
            return (thumb + index) * 0.5f;
        }

        static Vector3 PalmPoint(HandRig.Hand hand) => hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;

        /// <summary>The planet whose surface is closest to the point, if it is within reach and free.</summary>
        int Nearest(Vector3 point)
        {
            int best = -1;
            float bestGap = reach;
            for (int i = 0; i < planets.Length; i++)
            {
                var p = planets[i];
                if (p.Held || Grow(i + 1f) < 0.9f) continue;
                float gap = Vector3.Distance(point, p.Body.position) - p.Size * shownScale;
                if (gap >= bestGap) continue;
                bestGap = gap;
                best = i;
            }
            return best;
        }

        /// <summary>
        /// Each hand: which planet it would pick up (lit before you pinch), and the planet it carries, which follows
        /// the fingers; then the two-hand resize.
        /// </summary>
        void Hands(HandRig rig, float dt)
        {
            for (int s = 0; s < 2; s++)
            {
                var hand = rig.Get(s == 0);
                var grip = grips[s];
                grip.Tracked = hand.IsTracked && !hand.InSystemGesture;
                if (hand.IsTracked) grip.Point = PinchPoint(hand);
                grip.Pinch = grip.Tracked ? hand.Pinch : 0f;
                grip.Near = grip.Tracked && grip.Held < 0 && !IsPinching(s == 0) ? Nearest(grip.Point) : -1;
                int target = grip.Held >= 0 ? grip.Held : grip.Near;
                if (target >= 0) grip.Shown = target;
                grip.Light = Mathf.MoveTowards(grip.Light, target >= 0 ? 1f : 0f, dt * (target >= 0 ? 8f : 4f));
                if (grip.Held < 0) continue;

                // The carried planet follows the fingers; the offset from where it was picked up eases away, so it
                // slides in between them. It can't go into the sun.
                var p = planets[grip.Held];
                grip.Offset *= Mathf.Exp(-10f * dt);
                var local = system.InverseTransformPoint(grip.Point + grip.Offset);
                float closest = SunRadius + p.Size + 0.01f;
                if (local.magnitude < closest) local = local.sqrMagnitude > 1e-8f ? local.normalized * closest : Vector3.right * closest;
                p.Local = Vector3.Lerp(p.Local, local, 1f - Mathf.Exp(-30f * dt));
                var flat = Quaternion.Inverse(p.Plane) * p.Local;
                grip.Radius = Mathf.Clamp(new Vector2(flat.x, flat.z).magnitude, orbitRange.x, orbitRange.y);
                grip.Angle = Mathf.Atan2(flat.z, flat.x);
            }
            for (int i = 0; i < planets.Length; i++)
            {
                float lit = 0f;
                foreach (var g in grips)
                    if (g.Shown == i) lit = Mathf.Max(lit, g.Light * (g.Held == i ? 1f : 0.55f + g.Pinch * 0.45f));
                planets[i].Lit = lit;
            }
            Resize(dt);
        }

        /// <summary>Picks a planet up: a burst of sparks and a note, higher for the inner planets.</summary>
        void PickUp(Grip grip, int index)
        {
            var p = planets[index];
            grip.Held = index;
            grip.Free = false;
            grip.Offset = p.Body.position - grip.Point;
            grip.Radius = p.Orbit;
            grip.Angle = p.Angle;
            p.Held = true;
            p.Settle = 1f;
            Burst(p.Body.position, Color.Lerp(p.Air, Color.white, 0.4f), 14, 0.12f);
            Musical.Play(voices, Music, 74 - index * 2, p.Body.position, 0.35f);
        }

        /// <summary>
        /// Lets a planet go: its new orbit is the distance from the sun where you let go, in its own orbit's plane. It
        /// glides onto that orbit, the ring flashes, and a note plays, higher for a closer orbit.
        /// </summary>
        void LetGo(Grip grip)
        {
            var p = planets[grip.Held];
            grip.Held = -1;
            p.Held = false;
            var flat = Quaternion.Inverse(p.Plane) * p.Local;
            p.Orbit = Mathf.Clamp(new Vector2(flat.x, flat.z).magnitude, orbitRange.x, orbitRange.y);
            p.Angle = Mathf.Repeat(Mathf.Atan2(flat.z, flat.x), Mathf.PI * 2f);
            p.From = p.Local;
            p.Settle = 0f;
            p.Flash = 1.6f;
            // A trail of sparks along the new orbit, ahead of and behind the planet.
            for (int i = 0; i < 28; i++)
            {
                float a = p.Angle + (i - 14) * 0.04f;
                var at = system.TransformPoint(p.Plane * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * p.Orbit);
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = Random.insideUnitSphere * 0.02f,
                    startColor = Color.Lerp(p.Air, Color.white, 0.6f),
                    startSize = Random.Range(0.008f, 0.016f),
                    startLifetime = Random.Range(0.7f, 1.2f)
                }, 1);
            }
            int note = Mathf.RoundToInt(Mathf.Lerp(79f, 57f, Mathf.InverseLerp(orbitRange.x, orbitRange.y, p.Orbit)));
            Musical.Play(voices, Music, note, p.Body.position, 0.45f);
        }

        /// <summary>
        /// Both hands pinching empty space resize the system with the distance between them, and move it as they move
        /// together; a soft note marks the start and the end.
        /// </summary>
        void Resize(float dt)
        {
            bool both = grips[0].Free && grips[1].Free && grips[0].Tracked && grips[1].Tracked;
            if (both)
            {
                var a = grips[0].Point;
                var b = grips[1].Point;
                float apart = Mathf.Max(Vector3.Distance(a, b), 0.02f);
                var mid = (a + b) * 0.5f;
                if (!resizing)
                {
                    resizing = true;
                    resizeFrom = apart;
                    resizeScale = scale;
                    resizeMid = mid;
                    Musical.Play(voices, Music, 52, mid, 0.35f);
                }
                scale = Mathf.Clamp(resizeScale * apart / resizeFrom, scaleRange.x, scaleRange.y);
                centre = Keep(centre + (mid - resizeMid));
                resizeMid = mid;
            }
            else if (resizing)
            {
                resizing = false;
                Musical.Play(voices, Music, 59, centre, 0.3f);
            }
            resizeLight = Mathf.MoveTowards(resizeLight, resizing ? 1f : 0f, dt * 4f);
        }

        /// <summary>
        /// Keeps the sun in front of you and a little below your eyes, so the orbits pass below your face rather than
        /// through it: not too close, not across the room, not on the floor.
        /// </summary>
        Vector3 Keep(Vector3 at)
        {
            if (View == null) return at;
            var eye = View.transform.position;
            var flat = new Vector3(at.x - eye.x, 0f, at.z - eye.z);
            float d = flat.magnitude;
            if (d < 0.4f) flat = d > 1e-4f ? flat / d * 0.4f : Effects.FlatForward(View) * 0.4f;
            else if (d > 2f) flat = flat / d * 2f;
            float y = Mathf.Clamp(at.y, Effects.FloorY(View) + 0.25f, eye.y - 0.1f);
            return new Vector3(eye.x + flat.x, y, eye.z + flat.z);
        }

        /// <summary>1, fading to 0 as something comes closer than 18 cm to your eyes (a carried planet can still be looked at closely).</summary>
        static float AwayFromEyes(Vector3 at, Vector3 eye) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.18f, Vector3.Distance(at, eye)));

        /// <summary>
        /// An open palm held to the sun warms it over a second or two, and it cools slowly once you take it away.
        /// Embers drift from the sun to the palm; fully warmed, the sun bursts into a big flare.
        /// </summary>
        void Warm(HandRig rig, MusicFrame music, float dt)
        {
            float want = 0f;
            var at = sun.position;
            float surface = SunRadius * shownScale;
            for (int s = 0; s < 2; s++)
            {
                var hand = rig.Get(s == 0);
                var grip = grips[s];
                if (!grip.Tracked || grip.Held >= 0 || IsPinching(s == 0) || hand.Fist > 0.45f || hand.Pinch > 0.6f) continue;
                var palm = PalmPoint(hand);
                var to = at - palm;
                float d = to.magnitude;
                float close = Mathf.InverseLerp(warmReach, 0.06f, d - surface);
                float toward = Mathf.InverseLerp(0.15f, 0.65f, Vector3.Dot(hand.PalmNormal, to / Mathf.Max(d, 1e-4f)));
                if (close * toward <= want) continue;
                want = close * toward;
                warmPalm = palm;
            }
            warmth = Mathf.MoveTowards(warmth, want, dt / (want > warmth ? warmUp : 2.5f));
            if (warmth > 0.97f && !burst)
            {
                burst = true;
                flareAge = 0f;
                flare = 1.6f;
                Burst(at, SunGold, 40, 0.25f);
                Musical.Play(voices, music, 76, at, 0.4f);
            }
            else if (warmth < 0.6f) burst = false;

            if (want < 0.05f) return;
            emberDebt += warmth * 45f * dt;
            int n = (int)emberDebt;
            emberDebt -= n;
            // They leave the sun's rim as you see it and stream out and round towards the palm, so they show past
            // the edges of the hand held in front of it.
            var look = View != null ? (at - View.transform.position).normalized : Vector3.forward;
            for (int i = 0; i < n; i++)
            {
                var rim = Vector3.ProjectOnPlane(Random.onUnitSphere, look);
                rim = rim.sqrMagnitude > 1e-6f ? rim.normalized : Vector3.up;
                var from = at + rim * (surface * 1.05f);
                float life = Random.Range(1f, 1.6f);
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = from,
                    velocity = rim * Random.Range(0.07f, 0.14f) + (warmPalm - from) / life * Random.Range(0.3f, 0.55f),
                    startColor = Color.Lerp(new Color(1f, 0.97f, 0.85f), Ember, Random.value * 0.5f),
                    startSize = Random.Range(0.012f, 0.022f),
                    startLifetime = life
                }, 1);
            }
        }

        /// <summary>The warm sun's soft tone: a pad in the song's key that swells as the sun warms.</summary>
        void Hum(MusicFrame music)
        {
            if (!Application.isPlaying || sunVoice == null) return;
            float volume = warmth * warmth * 0.3f;
            if (volume > 0.002f)
            {
                if (!sunVoice.isPlaying)
                {
                    if (sunVoice.clip == null) sunVoice.clip = MakeWarmTone();
                    sunVoice.pitch = Mathf.Pow(2f, (Musical.Pitch(music, 57) - 57) / 12f);
                    sunVoice.Play();
                }
                sunVoice.volume = volume;
                sunVoice.transform.position = sun.position;
            }
            else if (sunVoice.isPlaying) sunVoice.Stop();
        }

        /// <summary>
        /// A soft, warm pad on A (220 Hz with its fifth, its octave and a slow swell) that loops without a seam: every
        /// part of it goes round a whole number of times in the two seconds.
        /// </summary>
        static AudioClip MakeWarmTone()
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int length = rate * 2;
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float swell = 0.8f + 0.2f * Mathf.Sin(Mathf.PI * t);
                float s = Mathf.Sin(2f * Mathf.PI * 220f * t) + 0.45f * Mathf.Sin(2f * Mathf.PI * 330f * t)
                          + 0.3f * Mathf.Sin(2f * Mathf.PI * 440.5f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 660f * t);
                data[i] = s * swell * 0.14f;
            }
            var clip = AudioClip.Create("Sun tone", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---- Drawing ----

        /// <summary>The sun: a boiling ball that swells a little on the kick, with its corona and the beat's flare.</summary>
        void DrawSun(MusicFrame music, Camera view)
        {
            float grow = Grow(0f);
            float pulse = 1f + music.Kick * 0.035f + warmth * 0.08f;
            sun.localPosition = Vector3.zero;
            sun.localRotation = Quaternion.Euler(0f, Clock * 4f, 0f);
            sun.localScale = Vector3.one * (SunRadius * 2f * grow * pulse);
            block.Clear();
            block.SetFloat(KindId, (float)Look.Sun);
            block.SetFloat(SunPowerId, 0.95f + music.Kick * 0.15f + music.Band(1) * 0.08f + warmth * 0.25f);
            block.SetFloat(ClockId, Clock);
            block.SetFloat(AlphaId, Mathf.Clamp01(grow * 2f));
            sunRenderer.SetPropertyBlock(block);

            // The corona's quad sits just in front of the sun, turned to you, so its glow can lift the disc's edge.
            var at = sun.position;
            var toEye = view.transform.position - at;
            float d = Mathf.Max(toEye.magnitude, 1e-3f);
            float radius = SunRadius * shownScale * grow * pulse;
            float half = radius * 3.4f * (1f + warmth * 0.45f + music.Kick * 0.1f + music.Band(1) * 0.08f);
            float lift = Mathf.Min(radius * 1.02f, d * 0.5f);
            Show(corona, grow > 0.001f);
            corona.transform.position = at + toEye / d * lift;
            Effects.Face(corona.transform, view);
            corona.transform.localScale = Vector3.one * (half * 2f);
            float apparent = (d - lift) * radius / Mathf.Sqrt(Mathf.Max(d * d - radius * radius, 1e-6f));
            block.Clear();
            block.SetFloat(DiscId, Mathf.Clamp(apparent / Mathf.Max(half, 1e-5f), 0.05f, 0.95f));
            block.SetFloat(GlowId, 0.55f + music.Kick * 0.6f + music.Band(0) * 0.25f + music.Band(1) * 0.3f + warmth * 0.7f);
            block.SetFloat(FlareId, flare);
            block.SetFloat(FlareAgeId, flareAge);
            block.SetFloat(WarmId, warmth);
            block.SetFloat(ClockId, Clock);
            block.SetFloat(AlphaId, grow);
            corona.SetPropertyBlock(block);
        }

        /// <summary>
        /// The planets with their glows and orbit rings, the moons, Saturn's rings and the asteroid belt. A planet or
        /// moon that comes close to your eyes (a big system, or one brought close) fades out of the way.
        /// </summary>
        void DrawPlanets(MusicFrame music, Camera view)
        {
            var sunAt = sun.position;
            var eye = view.transform.position;
            for (int i = 0; i < planets.Length; i++)
            {
                var p = planets[i];
                p.Body.localPosition = p.Local;
                float grow = Grow(i + 1f) * AwayFromEyes(p.Body.position, eye);
                p.Body.localRotation = Quaternion.Euler(0f, 0f, p.Axis) * Quaternion.Euler(0f, p.Spin, 0f);
                p.Body.localScale = Vector3.one * (p.Size * 2f * grow * (1f + p.Twinkle * 0.15f + p.Lit * 0.08f));
                block.Clear();
                block.SetFloat(KindId, (float)p.Look);
                block.SetColor(ColorAId, p.A);
                block.SetColor(ColorBId, p.B);
                block.SetColor(ColorCId, p.C);
                block.SetColor(AirId, p.Air);
                block.SetFloat(SeedId, p.Seed);
                block.SetFloat(SpotId, p.Spot);
                block.SetVector(SunPosId, sunAt);
                block.SetFloat(SunPowerId, sunLight);
                block.SetFloat(GlowId, p.Glow);
                block.SetFloat(TwinkleId, p.Twinkle);
                block.SetFloat(HighlightId, p.Lit);
                block.SetFloat(ClockId, Clock);
                block.SetFloat(AlphaId, Mathf.Clamp01(grow * 2f));
                p.Renderer.SetPropertyBlock(block);

                // A soft glow of its own colour round it, brighter with its band and when it twinkles.
                float size = p.Size * shownScale * grow;
                p.Halo.transform.position = p.Body.position;
                Effects.Face(p.Halo.transform, view);
                p.Halo.transform.localScale = Vector3.one * (size * 2f * (2.6f + p.Glow * 1.2f + p.Twinkle * 2.5f + p.Lit));
                block.Clear();
                block.SetColor(ColorId, new Color(p.Air.r, p.Air.g, p.Air.b, 1f));
                block.SetFloat(IntensityId, (0.08f + p.Glow * 0.38f + p.Twinkle * 1.1f + p.Lit * 0.5f) * grow);
                block.SetFloat(CoreId, 0f);
                p.Halo.SetPropertyBlock(block);

                // Its orbit: faint, with a trail of light behind the planet; dimmer while the planet is carried.
                var ring = p.Ring.transform;
                ring.localRotation = p.Plane;
                ring.localScale = Vector3.one * p.Orbit;
                var tint = Color.Lerp(p.Air, Color.white, 0.2f);
                tint.a = 0.2f;
                block.Clear();
                block.SetColor(ColorId, tint);
                block.SetFloat(AlphaId, Grow(i + 1f) * (p.Held ? 0.3f : 1f) * (1f + resizeLight * 1.2f));
                block.SetFloat(WidthId, 0.0008f);
                block.SetFloat(HeadId, Mathf.Repeat(p.Angle / (Mathf.PI * 2f), 1f));
                block.SetFloat(TrailId, p.Held ? 0f : 0.45f + p.Glow * 0.5f);
                block.SetFloat(PulseId, Ripple(p.Orbit) + p.Flash);
                block.SetFloat(DashesId, 0f);
                block.SetFloat(ClockId, Clock);
                p.Ring.SetPropertyBlock(block);
            }

            foreach (var m in moons)
            {
                var parent = planets[m.Planet];
                float grow = Grow(m.Planet + 1.3f);
                var plane = m.Equator ? Quaternion.Euler(0f, 0f, parent.Axis) : Quaternion.Euler(m.Tilt, 0f, 0f);
                m.Body.localPosition = parent.Local + plane * new Vector3(Mathf.Cos(m.Angle), 0f, Mathf.Sin(m.Angle)) * (m.Distance * grow);
                grow *= AwayFromEyes(m.Body.position, eye);
                m.Body.localScale = Vector3.one * (m.Size * 2f * grow);
                block.Clear();
                block.SetFloat(KindId, (float)m.Look);
                block.SetColor(ColorAId, m.A);
                block.SetColor(ColorBId, m.B);
                block.SetColor(AirId, new Color(0.85f, 0.85f, 0.85f, 0.3f));
                block.SetFloat(SeedId, m.Seed);
                block.SetVector(SunPosId, sunAt);
                block.SetFloat(SunPowerId, sunLight);
                block.SetFloat(GlowId, parent.Glow * 0.5f);
                block.SetFloat(TwinkleId, parent.Twinkle * 0.6f);
                block.SetFloat(HighlightId, parent.Lit * 0.5f);
                block.SetFloat(ClockId, Clock);
                block.SetFloat(AlphaId, Mathf.Clamp01(grow * 2f));
                m.Renderer.SetPropertyBlock(block);
            }

            var saturn = planets[Saturn];
            block.Clear();
            block.SetVector(SunPosId, sunAt);
            block.SetVector(PlanetPosId, saturn.Body.position);
            block.SetFloat(PlanetRadiusId, saturn.Body.lossyScale.x * 0.5f);
            block.SetFloat(SunPowerId, sunLight);
            block.SetFloat(GlowId, saturn.Glow);
            block.SetFloat(AlphaId, Grow(Saturn + 1f));
            rings.SetPropertyBlock(block);

            block.Clear();
            block.SetFloat(OrbitId, orbitClock);
            block.SetFloat(OmegaId, Mathf.PI * 2f / earthYear);
            block.SetFloat(ReferenceId, EarthOrbit);
            block.SetFloat(GlowId, Mathf.Clamp01(music.Band(5) * 0.9f + Ripple((BeltRadii.x + BeltRadii.y) * 0.5f) * 1.5f));
            block.SetFloat(ClockId, Clock);
            block.SetFloat(AlphaId, Grow(4.5f));
            belt.SetPropertyBlock(block);
        }

        /// <summary>
        /// Where the comet of sung word <paramref name="slot"/> is, <paramref name="u"/> of the way through its life, in
        /// the system's frame, going round the sun: it swoops in from behind the sun, further out and higher, curving
        /// in to its place on the arc, where it slows to a gentle drift outwards (the word is easy to read there),
        /// then flies off outwards and up as it fades, so it never crosses its neighbours.
        /// </summary>
        Vector3 Comet(WordLayer.Word w, int slot, float u)
        {
            const float arrive = 0.2f, leave = 0.78f;
            float arc = cometArc[slot];
            // The words grow and shrink less than the system does (WordSize), and their arc follows the words, so a
            // line keeps its spacing at any size.
            float q = Mathf.Abs(arc) * WordSize / Mathf.Max(shownScale, 0.1f);
            float side = arc < 0f ? -1f : 1f;
            float turn, reachOut, rise;
            if (u < arrive)
            {
                float k = 1f - u / arrive;
                k *= k * k;
                turn = -cometSwing * k;
                reachOut = 1f + 0.55f * k;
                rise = 0.1f * k;
            }
            else if (u < leave)
            {
                float k = (u - arrive) / (leave - arrive);
                turn = 8f * k;
                reachOut = 1f + 0.02f * k;
                rise = 0f;
            }
            else
            {
                float k = (u - leave) / (1f - leave);
                turn = 8f + 3f * k + 9f * k * k;
                reachOut = 1.02f + 0.6f * k * k;
                rise = 0.1f * k * k;
            }
            var direction = Quaternion.Euler(0f, cometAngle[slot] + side * turn, 0f) * new Vector3(w.Spot.x, 0f, w.Spot.y);
            return direction * (q * reachOut) + Vector3.up * (0.05f + rise);
        }

        /// <summary>
        /// The sung words as comets: a bright head with the word riding just above it, and a tail that points away
        /// from the sun, growing longer as the comet nears it and bending a little along the way it came.
        /// </summary>
        void DrawComets(MusicFrame music, Camera view)
        {
            var words = SungWords != null ? SungWords.Words : null;
            var eye = view.transform.position;
            var up = view.transform.up;
            for (int i = 0; i < heads.Length; i++)
            {
                var w = words != null && i < words.Count ? words[i] : null;
                bool show = w != null && w.Active;
                Show(heads[i], show);
                tails[i].SetVisible(show);
                if (!show) continue;
                // The comet keeps to its own timetable; the word's life only fades it (sooner when a new line starts).
                float u = Mathf.Clamp01(w.Age / cometLife);
                float fade = Mathf.Clamp01(w.Age / 0.3f) * Mathf.Clamp01((w.Life - w.Age) / 0.6f) * FadeIn;
                var local = Comet(w, i, u);
                var head = system.TransformPoint(local);
                // Away from the sun, tipped up out of the plane a little so the tail never hides edge-on.
                var away = new Vector3(local.x, 0f, local.z);
                float fromSun = away.magnitude;
                away = fromSun > 1e-4f ? away / fromSun : Vector3.forward;
                away = (away + Vector3.up * 0.35f).normalized;
                float place = Mathf.Abs(cometArc[i]) * WordSize / Mathf.Max(shownScale, 0.1f);
                float close = Mathf.Clamp01(place / Mathf.Max(fromSun, 1e-3f));
                float length = 0.07f + 0.13f * close * close;
                for (int k = 0; k < TailPoints; k++)
                {
                    float t = k / (float)(TailPoints - 1);
                    var behind = Comet(w, i, Mathf.Max(0f, u - t * 0.08f));
                    path[k] = system.TransformPoint(Vector3.Lerp(behind, local + away * (t * length), 0.65f));
                    var c = Color.Lerp(Color.white, new Color(0.35f, 0.62f, 1f), Mathf.Sqrt(t));
                    c.a = Mathf.Pow(1f - t, 1.1f) * fade;
                    pathColors[k] = c;
                    pathWidths[k] = Mathf.Lerp(1f, 0.2f, t);
                }
                tails[i].Set(path, pathColors, pathWidths, TailPoints, 0.022f * shownScale, Color.white, eye, false);

                heads[i].transform.position = head;
                Effects.Face(heads[i].transform, view);
                heads[i].transform.localScale = Vector3.one * (0.04f * shownScale * (1f + music.Kick * 0.25f));
                block.Clear();
                block.SetColor(ColorId, CometColor);
                block.SetFloat(IntensityId, 1.5f * fade);
                heads[i].SetPropertyBlock(block);

                // The word rides just below its head while the tail points up as you look (above it while the tail
                // points down), so the tail never crosses it and the line keeps its spacing.
                w.Size = WordSize;
                float letters = SungWordHeight * w.Size;
                float below = Vector3.Dot(system.TransformDirection(away), up) >= 0f ? -1f : 1f;
                w.Position = head + up * (below * letters * 0.95f - letters * 0.36f);
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.22f + music.Kick * 0.15f;
            }
        }

        /// <summary>
        /// Feedback for the hands: a ring round the planet within reach (brighter as the fingers close), the dashed
        /// orbit a carried planet will take with a line dropping from it to that orbit, a small light between fingers
        /// pinching empty space, and a band of light between the hands while they resize the system.
        /// </summary>
        void DrawHands(Camera view, float dt)
        {
            var eye = view.transform.position;
            for (int s = 0; s < 2; s++)
            {
                var grip = grips[s];
                bool carrying = grip.Held >= 0;

                bool lit = grip.Light > 0.01f && grip.Shown >= 0;
                Show(grip.Reach, lit);
                if (lit)
                {
                    var p = planets[grip.Shown];
                    float pinch = carrying ? 1f : grip.Pinch;
                    float r = p.Size * shownScale + 0.008f;
                    grip.Reach.transform.position = p.Body.position;
                    Effects.Face(grip.Reach.transform, view);
                    grip.Reach.transform.localScale = Vector3.one * (r * 2f / 0.72f * (carrying ? 1f : 1.25f - pinch * 0.25f));
                    block.Clear();
                    block.SetColor(ColorId, Color.Lerp(p.Air, Color.white, 0.6f));
                    block.SetFloat(RadiusId, 0.72f);
                    block.SetFloat(ThicknessId, 0.035f);
                    block.SetFloat(InnerGlowId, 0f);
                    block.SetFloat(AlphaId, grip.Light * (0.45f + pinch * 0.55f));
                    grip.Reach.SetPropertyBlock(block);
                }

                if (carrying)
                {
                    grip.PreviewPlanet = grip.Held;
                    grip.PreviewRadius = grip.Radius;
                    grip.PreviewAngle = grip.Angle;
                }
                grip.PreviewAlpha = Mathf.MoveTowards(grip.PreviewAlpha, carrying ? 1f : 0f, dt * (carrying ? 4f : 1.6f));
                bool preview = grip.PreviewAlpha > 0.01f && grip.PreviewPlanet >= 0;
                Show(grip.Preview, preview);
                if (preview)
                {
                    var p = planets[grip.PreviewPlanet];
                    var ring = grip.Preview.transform;
                    ring.localRotation = p.Plane;
                    ring.localScale = Vector3.one * grip.PreviewRadius;
                    block.Clear();
                    block.SetColor(ColorId, new Color(1f, 0.95f, 0.82f, 0.9f));
                    block.SetFloat(AlphaId, grip.PreviewAlpha);
                    block.SetFloat(WidthId, 0.0016f);
                    block.SetFloat(HeadId, Mathf.Repeat(grip.PreviewAngle / (Mathf.PI * 2f), 1f));
                    block.SetFloat(TrailId, 1f);
                    block.SetFloat(PulseId, 0f);
                    block.SetFloat(DashesId, Mathf.Max(8f, Mathf.Round(Mathf.PI * 2f * grip.PreviewRadius / 0.016f)));
                    block.SetFloat(ClockId, Clock);
                    grip.Preview.SetPropertyBlock(block);
                }

                bool dropping = false;
                if (carrying)
                {
                    var p = planets[grip.Held];
                    var flat = Quaternion.Inverse(p.Plane) * p.Local;
                    var foot = system.TransformPoint(p.Plane * new Vector3(flat.x, 0f, flat.z));
                    var top = p.Body.position;
                    dropping = Vector3.Distance(top, foot) > 0.012f;
                    if (dropping)
                    {
                        for (int k = 0; k < DropPoints; k++)
                        {
                            float t = k / (float)(DropPoints - 1);
                            path[k] = Vector3.Lerp(top, foot, t);
                            pathColors[k] = new Color(1f, 0.95f, 0.85f, Mathf.Lerp(0.15f, 0.7f, t));
                        }
                        grip.Drop.Set(path, pathColors, DropPoints, 0.0022f, Color.white, eye, false);
                    }
                }
                grip.Drop.SetVisible(dropping);

                bool nib = grip.Free && grip.Tracked && IsPinching(s == 0);
                Show(grip.Nib, nib);
                if (nib)
                {
                    grip.Nib.transform.position = grip.Point;
                    Effects.Face(grip.Nib.transform, view);
                    grip.Nib.transform.localScale = Vector3.one * (0.022f + resizeLight * 0.012f);
                    block.Clear();
                    block.SetColor(ColorId, SunGold);
                    block.SetFloat(IntensityId, 0.5f + resizeLight * 0.7f);
                    grip.Nib.SetPropertyBlock(block);
                }
            }

            bool bridging = resizeLight > 0.01f && grips[0].Tracked && grips[1].Tracked;
            bridge.SetVisible(bridging);
            if (!bridging) return;
            var a = grips[0].Point;
            var b = grips[1].Point;
            for (int k = 0; k < BridgePoints; k++)
            {
                float t = k / (float)(BridgePoints - 1);
                path[k] = Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.012f);
                float shimmer = 0.6f + 0.4f * Mathf.Sin(t * 18f - Clock * 6f);
                pathColors[k] = new Color(SunGold.r, SunGold.g, SunGold.b, shimmer);
            }
            bridge.Set(path, pathColors, BridgePoints, 0.004f, new Color(1f, 1f, 1f, resizeLight * 0.8f), eye);
        }

        void Burst(Vector3 at, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = Random.onUnitSphere * (speed * Random.Range(0.4f, 1f)),
                    startColor = color,
                    startSize = Random.Range(0.007f, 0.014f),
                    startLifetime = Random.Range(0.5f, 0.9f)
                }, 1);
        }

        static void Show(Renderer r, bool visible)
        {
            if (r.gameObject.activeSelf != visible) r.gameObject.SetActive(visible);
        }

        // ---- Building ----

        Renderer MakeSphere(string name, Transform parent)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            DebugShapes.RemoveCollider(sphere);
            sphere.transform.SetParent(parent, false);
            var r = sphere.GetComponent<Renderer>();
            r.sharedMaterial = planetMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        Renderer MakeRing(string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(system, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = orbitMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>
        /// A unit circle in the XZ plane for the orbit shader: each point twice (uv.y 0 and 1, the strip's two sides),
        /// with the circle's direction in the normal and the way round in uv.x.
        /// </summary>
        static Mesh BuildRingMesh(int segments)
        {
            int n = segments + 1;
            var vertices = new Vector3[n * 2];
            var normals = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var point = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var along = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                for (int side = 0; side < 2; side++)
                {
                    vertices[i * 2 + side] = point;
                    normals[i * 2 + side] = along;
                    uvs[i * 2 + side] = new Vector2(i / (float)segments, side);
                }
                if (i == segments) continue;
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Solar system orbit" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2.2f, 0.2f, 2.2f));
            return mesh;
        }

        /// <summary>
        /// The asteroid belt's mesh: a quad per asteroid whose four corners all hold its orbit (radius, height, starting
        /// angle), with its seed and size in the second UV. Most are small, a few larger; they crowd towards the middle
        /// of the belt. The shader moves them round and turns them to face you.
        /// </summary>
        static Mesh BuildBeltMesh(int count, Vector2 radii)
        {
            var random = new System.Random(17);
            var vertices = new Vector3[count * 4];
            var uvs = new Vector2[count * 4];
            var seeds = new Vector2[count * 4];
            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                float across = ((float)random.NextDouble() + (float)random.NextDouble()) * 0.5f;
                float radius = Mathf.Lerp(radii.x, radii.y, across);
                float height = ((float)random.NextDouble() - 0.5f) * 0.018f;
                float start = (float)random.NextDouble() * Mathf.PI * 2f;
                float size = 0.5f + Mathf.Pow((float)random.NextDouble(), 3f) * 1.3f;
                var orbit = new Vector3(radius, height, start);
                var seed = new Vector2((float)random.NextDouble(), size);
                int v = i * 4;
                for (int c = 0; c < 4; c++)
                {
                    vertices[v + c] = orbit;
                    seeds[v + c] = seed;
                }
                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(0f, 1f);
                uvs[v + 3] = new Vector2(1f, 1f);
                int t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Solar system belt" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.uv2 = seeds;
            mesh.triangles = triangles;
            // The shader moves the asteroids round the sun, so the bounds cover the whole belt.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(radii.y * 2.2f, 0.1f, radii.y * 2.2f));
            return mesh;
        }
    }
}
