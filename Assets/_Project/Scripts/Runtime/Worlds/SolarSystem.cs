using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Solar system world: you stand where the sun would be, and the planets go round you, at about chest height
    /// and at the size of your room. The four small inner planets pass within reach of your hands, a belt of glinting
    /// asteroids turns just beyond them, and the four giants circle further out across the room, each planet on its own
    /// slightly tipped orbit, the inner ones faster than the outer (Kepler's rule, sped up). Faint orbit lines trail
    /// light behind each planet; Saturn has its rings, Earth its Moon, Jupiter its four big moons and Saturn Titan.
    /// The sun is not on you: it floats on a circle round you, a little above your eyes, at a random spot to one side of
    /// where you look each time the world starts or you move, and drifts slowly to and fro along that circle. It lights
    /// the planets from where it is, so their day sides face it: planets between you and the sun show you mostly their
    /// night side, those on your far side from it are full.
    /// With a room scan, the orbits beyond Mars draw in to suit the room, and a planet that still reaches a wall fades
    /// into it, its orbit line too, and comes back out further round. Nothing comes at your face: near your head
    /// everything keeps below your eyes and a little away from them, dipping under your face if you lean in, and the sun
    /// keeps its distance if you walk towards it. When you move elsewhere (walk away, sit down, stand up), the whole
    /// system glides over to centre on you again.
    /// The music: the sun's glow and corona pulse with the kick and the bass and flare on every beat, when a wave of
    /// light also runs out from the sun across the orbits and planets; each planet brightens with its own band (the big
    /// outer planets with the low ones, the small inner planets with the high ones); everything turns faster as the
    /// song gets louder; and melody notes make planets twinkle. Each sung word is a comet with a glowing tail pointing
    /// away from the sun: it swoops in from further out and settles at its place on an arc round you, in front of you
    /// and just below your eyes, the words of a line laid out from left to right so the line reads like a sentence,
    /// then it flies off round you as it fades.
    /// Your hands: bring your fingers close to a planet and a ring lights round it; pinch to pick it up and carry it,
    /// while a dashed ring shows the orbit it will take: round you, as far out as you hold it. Let go and it settles
    /// onto that orbit, at the speed that distance gives it. Hold an open palm out to the sun and it warms: it burns
    /// brighter, its corona reaches out, embers stream across to your hand and a soft tone rises.
    /// </summary>
    public class SolarSystem : World
    {
        public override string DisplayName => "Solar system";

        public override Color Color => new Color(1f, 0.64f, 0.26f);

        public override int MenuOrder => 115;

        public override int MenuIcon => 25;

        public override string Hint => "Pinch a planet to move it; hold a palm out to the sun";

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

        [Header("Around you")]
        [Tooltip("How far below your eyes the planets go round you (m): about chest height, where your hands are.")]
        [SerializeField] float orbitDrop = 0.32f;
        [Tooltip("Near your head, everything keeps at least this far below your eyes (m), dipping under your face if you lean in.")]
        [SerializeField] float headRoom = 0.22f;
        [Tooltip("The closest a planet comes to your eyes (m), unless you carry it there.")]
        [SerializeField] float headGap = 0.3f;
        [Tooltip("How far inside the scanned walls the outer orbits keep (m); a planet that still reaches a wall fades into it.")]
        [SerializeField] float wallMargin = 0.3f;
        [Tooltip("How fast at most (m/s) the system glides over to centre on you again once you have moved.")]
        [SerializeField] float glideSpeed = 0.5f;

        [Header("The sun")]
        [Tooltip("The radius of the sun's circle round you (m, in a big room), and how far above your eyes it floats.")]
        [SerializeField] float sunRing = 1.12f;
        [SerializeField] float sunRise = 0.2f;
        [Tooltip("How far round from where you look (degrees, to either side) the sun appears: at least and at most.")]
        [SerializeField] Vector2 sunStart = new Vector2(15f, 40f);
        [Tooltip("How far (degrees, each way) the sun drifts to and fro along its circle, and the seconds it takes to go there and back.")]
        [SerializeField] float sunSway = 30f;
        [SerializeField] float sunSwayTime = 100f;
        [Tooltip("The closest the sun comes to your head (m, level distance) if you walk towards it.")]
        [SerializeField] float sunGap = 0.85f;

        [Header("Orbits")]
        [Tooltip("Seconds Earth takes to go round you; the others follow Kepler's third law from their distance.")]
        [SerializeField] float yearSeconds = 28f;
        [Tooltip("How much faster everything turns when the music is at its loudest (1 = twice as fast).")]
        [SerializeField] float speedSwell = 0.8f;
        [Tooltip("The closest and furthest orbit (m from you) a planet can be let go into.")]
        [SerializeField] Vector2 newOrbits = new Vector2(0.36f, 3f);
        [Tooltip("How many glinting asteroids the belt beyond Mars holds (all one mesh).")]
        [SerializeField] int asteroidCount = 900;

        [Header("Hands")]
        [Tooltip("How close (m) a pinch must come to a planet's surface to pick it up.")]
        [SerializeField] float grabReach = 0.05f;
        [Tooltip("How straight at the sun (degrees) an open palm must face to warm it.")]
        [SerializeField] float warmAim = 28f;
        [Tooltip("Seconds of an open palm held out to the sun to warm it fully.")]
        [SerializeField] float warmUp = 1.5f;

        [Header("Comets")]
        [Tooltip("Seconds a sung word's comet takes to fly past.")]
        [SerializeField] float cometLife = 3.6f;
        [Tooltip("How far from you (m) the sung words settle, and how far below your eyes.")]
        [SerializeField] float wordRing = 1f;
        [SerializeField] float wordDrop = 0.12f;
        [Tooltip("How far round you (degrees) a comet sweeps in from, beside its place.")]
        [SerializeField] float cometSweep = 12f;

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
        static readonly int WaveId = Shader.PropertyToID("_Wave");
        static readonly int OrbitId = Shader.PropertyToID("_Orbit");
        static readonly int OmegaId = Shader.PropertyToID("_Omega");
        static readonly int ReferenceId = Shader.PropertyToID("_Reference");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int PlanetPosId = Shader.PropertyToID("_PlanetPos");
        static readonly int PlanetRadiusId = Shader.PropertyToID("_PlanetRadius");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int InnerGlowId = Shader.PropertyToID("_InnerGlow");
        // Shared with the orbit and belt shaders (SolarSystemRoom.hlsl): the room round you and where your eyes are.
        static readonly int ReachId = Shader.PropertyToID("_SolarSystemReach");
        static readonly int RoomId = Shader.PropertyToID("_SolarSystemRoom");
        static readonly int EyesId = Shader.PropertyToID("_SolarSystemHead");
        static readonly int ClearId = Shader.PropertyToID("_SolarSystemClear");

        /// <summary>The sun's radius (m) in a big room.</summary>
        const float SunRadius = 0.095f;
        /// <summary>Earth's orbit (m), which sets the year, and Mars', the last orbit that always stays within reach.</summary>
        const float EarthOrbit = 0.61f;
        const float MarsOrbit = 0.7f;
        /// <summary>Neptune's orbit in a big room (m); in a smaller one everything beyond Mars draws in, down to LeastFit of the way.</summary>
        const float OuterOrbit = 2.1f;
        const float LeastFit = 0.7f;
        /// <summary>How many directions round you the walls are measured in, and the distance where there is no wall.</summary>
        const int ReachSteps = 64;
        const float Open = 100f;
        /// <summary>Something fades from this far inside a wall to this far beyond it (m).</summary>
        const float FadeInside = 0.3f;
        const float FadeBeyond = 0.05f;
        /// <summary>The dip under your face is full within this level distance of your eyes (m) and gone beyond the second.</summary>
        const float ClearNear = 0.5f;
        const float ClearFar = 1.05f;
        /// <summary>The beat's wave of light from the sun: its speed (m/s) and width (m).</summary>
        const float WaveSpeed = 3.5f;
        const float WaveWidth = 0.3f;
        const int Saturn = 5;
        const int Giants = 4;
        const int RingSegments = 192;
        const int TailPoints = 18;
        const int DropPoints = 6;
        /// <summary>Room between two words of a line laid out along the comets' arc, in letter heights.</summary>
        const float WordGap = 0.8f;
        static readonly Vector2 BeltRadii = new Vector2(0.83f, 0.95f);
        static readonly Color CometColor = new Color(0.82f, 0.94f, 1f);
        static readonly Color SunGold = new Color(1f, 0.78f, 0.42f);
        static readonly Color Ember = new Color(1f, 0.45f, 0.14f);

        /// <summary>How a body's surface is drawn: the planet shader's _Kind.</summary>
        enum Look { Sun = 0, Rocky = 1, Cloudy = 2, Earth = 3, Mars = 4, Banded = 5, Ice = 6 }

        /// <summary>
        /// A planet: its size and orbit (m, in a big room), how its orbit and its axis are tipped (degrees), its day
        /// (seconds, negative for the planets that turn backwards), where it starts round you (degrees from your right,
        /// 90 ahead), its look and the band of the music it shines with; then what changes as it goes round and as your
        /// hands move it.
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
            /// <summary>The orbit it is on (m from you), the one it is easing to, how far round it is (radians) and how fast it goes.</summary>
            public float Orbit, Target, Angle, Speed, Spin;
            /// <summary>Where it is in the system's frame, where it was let go, and where it is drawn in the room.</summary>
            public Vector3 Local, From, World;
            /// <summary>0 when it has just been let go, 1 once it has glided onto its new orbit.</summary>
            public float Settle = 1f;
            /// <summary>Carried now; moved by your hands (so the room no longer sets its orbit).</summary>
            public bool Held, Moved;
            /// <summary>Its drawn radius (m); how much of it shows in the room (walls, your eyes); that times how far it has formed.</summary>
            public float Radius, Fade, Shown;
            public float Glow, Twinkle, Lit, Flash;
        }

        /// <summary>A moon: which planet it circles, its size, distance (m) and period (s).</summary>
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
            /// <summary>The orbit the carried planet would take if let go now: radius (m) and angle (radians).</summary>
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
        /// Per sung word (by its place in SungWords.Words): where its comet settles, round the point you stood at when
        /// its line began (that point, the direction in radians round from +x towards +z, how far out and how high),
        /// and which way round it drifts (+1 towards larger angles, your left; -1 your right).
        /// </summary>
        Vector3[] cometCentre;
        float[] cometAngle, cometRadius, cometHeight, cometSide;
        Vector3[] path;
        Color[] pathColors;
        float[] pathWidths;
        ParticleSystem sparks;
        AudioPool voices;
        AudioSource sunVoice;
        MaterialPropertyBlock block;

        /// <summary>How far the walls are (m) in each of ReachSteps directions round the centre, sorted, and packed for the shaders.</summary>
        readonly float[] reach = new float[ReachSteps];
        readonly float[] sorted = new float[ReachSteps];
        readonly Vector4[] packed = new Vector4[ReachSteps / 4];

        /// <summary>Where the system is centred (below your head, at orbit height), and where it is drawn while it glides there.</summary>
        Vector3 centre, shownCentre;
        Vector3 sunAt, warmPalm, lineCentre;
        Quaternion facing = Quaternion.identity;
        /// <summary>How far the room lets the orbits beyond Mars reach out (LeastFit-1), and the size that gives the bodies out there.</summary>
        float fit = 1f, sizeFit = 1f, beltScale = 1f;
        /// <summary>The sun's spot on its circle and where it is now (degrees round from your right), its circle's radius and its height.</summary>
        float sunBase, sunAngle, sunRadius, sunHeight;
        float lineAngle, lineRadius, lineHeight;
        float orbitClock, pace = 1f, energy, sunLight = 1f;
        float flare, flareAge = 9f, warmth, emberDebt;
        bool fresh = true, sunPlaced, lineSet, burst;

        protected override float FadeInSeconds => 2f;

        protected override int SungWordCount => 8;

        protected override float SungWordHeight => 0.056f;

        protected override float SungWordsAlpha => FadeIn;

        static Planet[] MakePlanets() => new[]
        {
            new Planet
            {
                Name = "Mercury", Size = 0.02f, Home = 0.4f, Inclination = 6f, Node = 30f, Axis = 2f, Day = 20f, Start = 329f,
                Look = Look.Rocky, Band = 5, A = new Color(0.66f, 0.62f, 0.58f), B = new Color(0.38f, 0.35f, 0.33f), C = Color.white,
                Air = new Color(0.85f, 0.82f, 0.78f, 0.35f), Seed = 1.3f
            },
            new Planet
            {
                Name = "Venus", Size = 0.03f, Home = 0.5f, Inclination = 3f, Node = 80f, Axis = 3f, Day = -30f, Start = 43f,
                Look = Look.Cloudy, Band = 4, A = new Color(0.98f, 0.9f, 0.68f), B = new Color(0.86f, 0.68f, 0.42f), C = Color.white,
                Air = new Color(1f, 0.88f, 0.6f, 0.7f), Seed = 2.7f
            },
            new Planet
            {
                Name = "Earth", Size = 0.032f, Home = EarthOrbit, Inclination = 1.5f, Node = 0f, Axis = 23.4f, Day = 8f, Start = 41f,
                Look = Look.Earth, Band = 3, A = new Color(0.07f, 0.25f, 0.65f), B = new Color(0.22f, 0.5f, 0.2f), C = new Color(0.72f, 0.62f, 0.4f),
                Air = new Color(0.45f, 0.72f, 1f, 1f), Seed = 4.1f
            },
            new Planet
            {
                Name = "Mars", Size = 0.025f, Home = MarsOrbit, Inclination = 2.5f, Node = 140f, Axis = 25f, Day = 8.2f, Start = 3f,
                Look = Look.Mars, Band = 2, A = new Color(0.88f, 0.43f, 0.2f), B = new Color(0.56f, 0.24f, 0.12f), C = Color.white,
                Air = new Color(1f, 0.62f, 0.42f, 0.45f), Seed = 5.9f
            },
            new Planet
            {
                Name = "Jupiter", Size = 0.08f, Home = 1.38f, Inclination = 2f, Node = 200f, Axis = 3f, Day = 6f, Start = 54f,
                Look = Look.Banded, Band = 1, A = new Color(0.94f, 0.87f, 0.75f), B = new Color(0.74f, 0.52f, 0.34f), C = new Color(0.84f, 0.38f, 0.22f),
                Air = new Color(1f, 0.85f, 0.65f, 0.6f), Seed = 7.2f, Spot = 1f
            },
            new Planet
            {
                Name = "Saturn", Size = 0.066f, Home = 1.64f, Inclination = 3f, Node = 260f, Axis = 26.7f, Day = 6.5f, Start = 108f,
                Look = Look.Banded, Band = 1, A = new Color(0.96f, 0.88f, 0.66f), B = new Color(0.82f, 0.7f, 0.47f), C = new Color(0.9f, 0.8f, 0.6f),
                Air = new Color(1f, 0.9f, 0.65f, 0.55f), Seed = 8.8f
            },
            new Planet
            {
                Name = "Uranus", Size = 0.046f, Home = 1.88f, Inclination = 1.5f, Node = 320f, Axis = 98f, Day = -7f, Start = 215f,
                Look = Look.Ice, Band = 0, A = new Color(0.64f, 0.9f, 0.93f), B = new Color(0.5f, 0.8f, 0.86f), C = new Color(0.42f, 0.72f, 0.82f),
                Air = new Color(0.62f, 0.96f, 1f, 0.7f), Seed = 10.4f
            },
            new Planet
            {
                Name = "Neptune", Size = 0.045f, Home = OuterOrbit, Inclination = 2.5f, Node = 100f, Axis = 28f, Day = 6.8f, Start = 300f,
                Look = Look.Ice, Band = 0, A = new Color(0.28f, 0.46f, 0.98f), B = new Color(0.17f, 0.3f, 0.8f), C = new Color(0.08f, 0.14f, 0.42f),
                Air = new Color(0.46f, 0.62f, 1f, 0.8f), Seed = 12.6f, Spot = 1f
            }
        };

        static Moon[] MakeMoons() => new[]
        {
            // The Moon, its orbit tipped a few degrees.
            new Moon { Planet = 2, Size = 0.009f, Distance = 0.065f, Period = 6f, Start = 0f, Tilt = 5f, Look = Look.Rocky,
                A = new Color(0.8f, 0.79f, 0.77f), B = new Color(0.46f, 0.46f, 0.48f), Seed = 20f },
            // Jupiter's four big moons, keeping step with each other (1 : 2 : 4) as the real ones do.
            new Moon { Planet = 4, Size = 0.0075f, Distance = 0.13f, Period = 3f, Start = 40f, Equator = true, Look = Look.Rocky,
                A = new Color(0.98f, 0.86f, 0.42f), B = new Color(0.84f, 0.5f, 0.22f), Seed = 21f },
            new Moon { Planet = 4, Size = 0.0068f, Distance = 0.155f, Period = 6f, Start = 170f, Equator = true, Look = Look.Rocky,
                A = new Color(0.95f, 0.91f, 0.85f), B = new Color(0.7f, 0.56f, 0.43f), Seed = 22f },
            new Moon { Planet = 4, Size = 0.0092f, Distance = 0.185f, Period = 12f, Start = 260f, Equator = true, Look = Look.Rocky,
                A = new Color(0.64f, 0.59f, 0.54f), B = new Color(0.43f, 0.39f, 0.36f), Seed = 23f },
            new Moon { Planet = 4, Size = 0.0085f, Distance = 0.22f, Period = 24f, Start = 320f, Equator = true, Look = Look.Rocky,
                A = new Color(0.47f, 0.43f, 0.39f), B = new Color(0.31f, 0.28f, 0.26f), Seed = 24f },
            // Titan in its orange haze, outside Saturn's rings, in their plane.
            new Moon { Planet = Saturn, Size = 0.009f, Distance = 0.2f, Period = 10f, Start = 120f, Equator = true, Look = Look.Cloudy,
                A = new Color(0.94f, 0.7f, 0.34f), B = new Color(0.78f, 0.52f, 0.25f), Seed = 25f }
        };

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            system = new GameObject("System").transform;
            system.SetParent(transform, false);
            // Hidden until the first frame has placed it round you.
            system.gameObject.SetActive(false);

            sunRenderer = MakeSphere("Sun", system);
            sun = sunRenderer.transform;
            corona = Effects.MakeQuad("Corona", transform, coronaMaterial);
            Show(corona, false);

            var ringMesh = BuildRingMesh(RingSegments);
            planets = MakePlanets();
            foreach (var p in planets)
            {
                p.Renderer = MakeSphere(p.Name, system);
                p.Body = p.Renderer.transform;
                p.Halo = Effects.MakeQuad($"{p.Name} Glow", transform, glowMaterial);
                Show(p.Halo, false);
                p.Ring = MakeRing($"{p.Name} Orbit", ringMesh);
                // Tipped about its line of nodes, so the planet's angle round its orbit is also its direction round you.
                p.Plane = Quaternion.Euler(0f, p.Node, 0f) * Quaternion.Euler(p.Inclination, 0f, 0f) * Quaternion.Euler(0f, -p.Node, 0f);
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
            beltObject.AddComponent<MeshFilter>().sharedMesh = BuildBeltMesh(Mathf.Max(asteroidCount, 1), BeltRadii);
            belt = beltObject.AddComponent<MeshRenderer>();
            belt.sharedMaterial = beltMaterial;
            belt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            belt.receiveShadows = false;

            heads = new Renderer[SungWordCount];
            tails = new LineStrip[SungWordCount];
            cometCentre = new Vector3[SungWordCount];
            cometAngle = new float[SungWordCount];
            cometRadius = new float[SungWordCount];
            cometHeight = new float[SungWordCount];
            cometSide = new float[SungWordCount];
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
            sparks.Clear();
            if (sunVoice != null) sunVoice.Stop();
            HandSkin.FocusStrength = 0f;
        }

        /// <summary>Everything as it starts: the planets on their own orbits, the sun calm, placed afresh round you.</summary>
        void ResetSystem()
        {
            orbitClock = energy = warmth = flare = 0f;
            flareAge = 9f;
            burst = sunPlaced = lineSet = false;
            fresh = true;
            foreach (var p in planets)
            {
                p.Orbit = p.Target = p.Home;
                p.Angle = p.Start * Mathf.Deg2Rad;
                p.Speed = Kepler(p.Orbit);
                p.Spin = 0f;
                p.Held = p.Moved = false;
                p.Settle = 1f;
                p.Glow = p.Twinkle = p.Lit = p.Flash = p.Fade = p.Shown = 0f;
                p.Local = OnOrbit(p);
            }
            foreach (var m in moons) m.Angle = m.Start * Mathf.Deg2Rad;
            foreach (var g in grips) g.Clear();
        }

        /// <summary>
        /// Centres the system on you: on the floor below your head, its orbits at about chest height. The first time,
        /// the planets start round you from where you look; after you move, the system glides over to you. Measures the
        /// room (with a scan, the orbits beyond Mars draw in to suit it) and gives the sun a new spot on its circle,
        /// to one side of where you look.
        /// </summary>
        protected override void OnPlace(Camera view)
        {
            var eye = view.transform.position;
            var forward = Effects.FlatForward(view);
            float floorY = Effects.FloorY(view);
            centre = new Vector3(eye.x, Mathf.Max(eye.y - orbitDrop, floorY + 0.4f), eye.z);
            // Above your eyes, but under a scanned ceiling.
            sunHeight = Mathf.Min(eye.y + sunRise, Room.Ceiling(float.MaxValue) - 0.35f);
            if (fresh)
            {
                facing = Quaternion.LookRotation(forward, Vector3.up);
                shownCentre = centre;
            }
            MeasureRoom();
            foreach (var p in planets)
            {
                if (p.Moved) continue;
                p.Target = Fitted(p.Home);
                if (fresh) p.Orbit = p.Target;
            }
            ChooseSunSpot(forward);
            fresh = false;
        }

        /// <summary>
        /// How far the scanned walls are round the centre, in ReachSteps directions (handed to the orbit and belt
        /// shaders too), and how far out that lets the orbits beyond Mars reach: most directions have room for them, and
        /// a planet that still meets a wall fades into it.
        /// </summary>
        void MeasureRoom()
        {
            bool walls = Room.Scanned && Room.Walls.Count > 0;
            for (int i = 0; i < ReachSteps; i++)
            {
                float a = i * (Mathf.PI * 2f / ReachSteps);
                var direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                reach[i] = walls && Room.Hit(Room.Walls, centre, direction, out _, out float d) ? d : Open;
                sorted[i] = reach[i];
            }
            for (int i = 0; i < packed.Length; i++)
                packed[i] = new Vector4(reach[i * 4], reach[i * 4 + 1], reach[i * 4 + 2], reach[i * 4 + 3]);
            Shader.SetGlobalVectorArray(ReachId, packed);
            Shader.SetGlobalVector(RoomId, new Vector4(centre.x, centre.z, FadeInside, FadeBeyond));

            System.Array.Sort(sorted);
            float room = sorted[ReachSteps * 3 / 10] - wallMargin;
            fit = walls ? Mathf.Clamp((room - MarsOrbit) / (OuterOrbit - MarsOrbit), LeastFit, 1f) : 1f;
            sizeFit = Mathf.Lerp(0.85f, 1f, (fit - LeastFit) / (1f - LeastFit));
            float middle = (BeltRadii.x + BeltRadii.y) * 0.5f;
            beltScale = Fitted(middle) / middle;
        }

        /// <summary>
        /// A new random spot for the sun on its circle, to one side of where you look (never straight ahead) and where
        /// the room has space for it. The first time it appears there; later it glides round its circle to it.
        /// </summary>
        void ChooseSunSpot(Vector3 forward)
        {
            var f = Quaternion.Inverse(facing) * forward;
            float look = Mathf.Atan2(f.z, f.x) * Mathf.Rad2Deg;
            float full = Fitted(sunRing);
            float best = look + sunStart.y, bestRoom = -1f;
            for (int k = 0; k < 12; k++)
            {
                float angle = look + Random.Range(sunStart.x, sunStart.y) * (Random.value < 0.5f ? -1f : 1f);
                float room = SunRingAt(angle);
                if (room > bestRoom + 0.01f)
                {
                    bestRoom = room;
                    best = angle;
                }
                if (room >= full - 0.01f) break;
            }
            // The sway goes on from where it is, so the new spot is where the sun's drift centres now.
            sunBase = best - Sway();
        }

        /// <summary>How far the sun has drifted from its spot (degrees): slowly to and fro along its circle.</summary>
        float Sway() => sunSway * Mathf.Sin(Clock * Mathf.PI * 2f / Mathf.Max(sunSwayTime, 1f));

        /// <summary>The radius of the sun's circle in that direction (degrees round from your right): its full size, or less where a wall is near.</summary>
        float SunRingAt(float angle)
        {
            float room = ReachAlong(Around(angle * Mathf.Deg2Rad)) - SunRadius * sizeFit - 0.25f;
            return Mathf.Max(Mathf.Min(Fitted(sunRing), room), sunGap + 0.05f);
        }

        /// <summary>Each beat flares the sun and sends a wave of light out from it across the orbits.</summary>
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
            if (p.Shown < 0.1f) return;
            float far = View != null ? Vector3.Distance(p.World, View.transform.position) : 1f;
            Burst(p.World, Color.Lerp(p.Air, Color.white, 0.5f), 7, 0.05f + far * 0.02f, 0.008f + far * 0.005f);
        }

        /// <summary>
        /// Each sung word becomes a comet. The words of a line slow down at their own places along an arc round you, in
        /// front of where you looked as the line began and just below your eyes, laid out like the line itself: each
        /// word as far along the arc as its middle is along the line, so the line reads from left to right and long
        /// words get the room they need. Comets on the left half drift to the left and those on the right half to the
        /// right, so neighbours drift apart. A new line gives the last one's words a second at most to fade.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            var words = SungWords.Words;
            if (newLine)
                for (int i = 0; i < words.Count; i++)
                    if (words[i].Active) words[i].Life = Mathf.Min(words[i].Life, words[i].Age + 1f);
            if (newLine || !lineSet) StartLine();
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
            // Metres from the middle of the line (to the right), as an angle round the arc: at most 60 degrees each way.
            float along = (middle - total * 0.5f) * SungWordHeight;
            float angle = Mathf.Clamp(along / lineRadius, -1.05f, 1.05f);

            int slot = 0;
            while (slot < words.Count - 1 && words[slot] != w) slot++;
            cometCentre[slot] = lineCentre;
            cometAngle[slot] = lineAngle - angle;
            cometRadius[slot] = lineRadius;
            cometHeight[slot] = lineHeight;
            cometSide[slot] = angle < 0f ? 1f : -1f;
        }

        /// <summary>A new line of words: its arc in front of where you look now, just below your eyes, inside the room.</summary>
        void StartLine()
        {
            var eye = View.transform.position;
            var forward = Effects.FlatForward(View);
            lineCentre = eye;
            lineAngle = Mathf.Atan2(forward.z, forward.x);
            float room = Open;
            for (int k = -1; k <= 1; k++) room = Mathf.Min(room, ReachAlong(Quaternion.AngleAxis(k * 30f, Vector3.up) * forward));
            lineRadius = Mathf.Clamp(room - 0.3f, 0.6f, wordRing);
            lineHeight = eye.y - wordDrop;
            lineSet = true;
        }

        /// <summary>A pinch close to a planet picks it up; a pinch in empty space shows a small light between the fingers.</summary>
        protected override void OnPinch(HandRig.Hand hand, bool left)
        {
            var grip = grips[left ? 0 : 1];
            grip.Point = PinchPoint(hand);
            int near = Nearest(grip.Point);
            if (near < 0)
            {
                grip.Free = true;
                return;
            }
            PickUp(grip, near);
            Acted("pick up planet");
        }

        protected override void OnRelease(HandRig.Hand hand, bool left)
        {
            var grip = grips[left ? 0 : 1];
            if (grip.Held >= 0) LetGo(grip);
            grip.Free = false;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            if (!system.gameObject.activeSelf) system.gameObject.SetActive(true);
            var eye = view.transform.position;
            Shader.SetGlobalVector(EyesId, eye);
            Shader.SetGlobalVector(ClearId, new Vector4(headRoom, headGap, ClearNear, ClearFar));
            Listen(music, dt);
            Glide(dt);
            Turn(dt);
            MoveSun(eye, dt);
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
            HandSkin.FocusPosition = sunAt;
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

        /// <summary>The beat's wave of light at this distance from the sun (m), as it runs outwards.</summary>
        float Wave(float distance)
        {
            float x = (flareAge * WaveSpeed - distance) / WaveWidth;
            return Mathf.Exp(-x * x) * Mathf.Exp(-flareAge * 1.2f) * Mathf.Min(flare, 1f) * 0.8f;
        }

        // ---- Placing ----

        /// <summary>The system glides over to where it is centred now, gently, so nothing sweeps past you fast.</summary>
        void Glide(float dt)
        {
            var step = (centre - shownCentre) * (1f - Mathf.Exp(-2.5f * dt));
            float most = glideSpeed * dt;
            if (step.sqrMagnitude > most * most) step = step.normalized * most;
            shownCentre += step;
            system.SetPositionAndRotation(shownCentre, facing);
        }

        /// <summary>
        /// The sun drifts slowly to and fro along its circle round you, drawing in where a wall is near; it glides
        /// round the circle to a new spot, and keeps its distance if you walk towards it.
        /// </summary>
        void MoveSun(Vector3 eye, float dt)
        {
            float target = sunBase + Sway();
            if (!sunPlaced) sunAngle = target;
            float step = Mathf.DeltaAngle(sunAngle, target) * (1f - Mathf.Exp(-2.5f * dt));
            sunAngle += Mathf.Clamp(step, -40f * dt, 40f * dt);
            float ring = SunRingAt(sunAngle);
            sunRadius = sunPlaced ? Mathf.Lerp(sunRadius, ring, 1f - Mathf.Exp(-2f * dt)) : ring;
            var outward = Around(sunAngle * Mathf.Deg2Rad);
            var at = new Vector3(shownCentre.x, sunHeight, shownCentre.z) + outward * sunRadius;
            var flat = new Vector3(at.x - eye.x, 0f, at.z - eye.z);
            float d = flat.magnitude;
            if (d < sunGap) at += (d > 1e-4f ? flat / d : outward) * (sunGap - d);
            sunAt = sunPlaced ? Vector3.Lerp(sunAt, at, 1f - Mathf.Exp(-6f * dt)) : at;
            sunPlaced = true;
        }

        /// <summary>A direction round you in the system's frame (radians from your right when the world started, 90 degrees ahead).</summary>
        Vector3 Around(float angle) => facing * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        /// <summary>An orbit's radius in this room: the inner planets' stay within reach, those beyond Mars draw in to suit a small room.</summary>
        float Fitted(float radius) => radius <= MarsOrbit ? radius : MarsOrbit + (radius - MarsOrbit) * fit;

        /// <summary>A planet's size in this room: the giants are a little smaller in a small room.</summary>
        float SizeOf(int i) => planets[i].Size * (i >= Giants ? sizeFit : 1f);

        /// <summary>How far the walls are from the centre (m) in this direction.</summary>
        float ReachAlong(Vector3 direction)
        {
            float f = Mathf.Repeat(Mathf.Atan2(direction.z, direction.x) / (Mathf.PI * 2f), 1f) * ReachSteps;
            int i = Mathf.Min((int)f, ReachSteps - 1);
            return Mathf.Lerp(reach[i], reach[(i + 1) % ReachSteps], f - i);
        }

        /// <summary>1 inside the room, fading to 0 as something of this size (m) goes into a scanned wall (as the shaders do).</summary>
        float InRoom(Vector3 at, float size)
        {
            var flat = new Vector3(at.x - centre.x, 0f, at.z - centre.z);
            float limit = ReachAlong(flat);
            float r = flat.magnitude + size * 0.5f;
            return 1f - Mathf.SmoothStep(0f, 1f, (r - (limit - FadeInside)) / (FadeInside + FadeBeyond));
        }

        /// <summary>
        /// Moves something near your head out of the way (the orbit and belt shaders do the same): within about a metre
        /// (level distance) it keeps below your eyes, dipping under your face as you lean in, and it never comes closer
        /// than headGap to them.
        /// </summary>
        Vector3 Clear(Vector3 at, float size, Vector3 eye)
        {
            float dx = at.x - eye.x, dz = at.z - eye.z;
            float level = Mathf.Sqrt(dx * dx + dz * dz);
            float under = headRoom * (1f - Mathf.SmoothStep(0f, 1f, (level - ClearNear) / (ClearFar - ClearNear)));
            at.y = SmoothMin(at.y, eye.y - under - size, 0.1f);
            var away = at - eye;
            float d = away.magnitude;
            float least = headGap + size;
            if (d < least + 0.1f) at = eye + (d > 1e-4f ? away / d : Vector3.down) * -SmoothMin(-d, -least, 0.1f);
            return at;
        }

        /// <summary>The smaller of a and b, rounded off over k so things bend away smoothly instead of with a kink.</summary>
        static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }

        /// <summary>1, fading to 0 as something comes closer than 18 cm to your eyes (a carried planet can still be looked at closely).</summary>
        static float AwayFromEyes(Vector3 at, Vector3 eye) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.18f, Vector3.Distance(at, eye)));

        // ---- The orbits ----

        /// <summary>Angular speed (rad/s) on an orbit of the given radius: Kepler's third law, scaled to Earth's year.</summary>
        float Kepler(float radius) => Mathf.PI * 2f / Mathf.Max(yearSeconds, 1f) * Mathf.Pow(EarthOrbit / Mathf.Max(radius, 0.1f), 1.5f);

        static Vector3 OnOrbit(Planet p) => p.Plane * new Vector3(Mathf.Cos(p.Angle), 0f, Mathf.Sin(p.Angle)) * p.Orbit;

        /// <summary>
        /// Moves everything round. A planet's speed eases towards the one its orbit gives it, so one let go into a new
        /// orbit speeds up or slows down smoothly while it glides onto that orbit; one the room moved eases onto its
        /// new orbit.
        /// </summary>
        void Turn(float dt)
        {
            orbitClock += dt * pace;
            foreach (var p in planets)
            {
                if (!p.Moved) p.Orbit = Mathf.Lerp(p.Orbit, p.Target, 1f - Mathf.Exp(-1.5f * dt));
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

        /// <summary>The planet whose surface is closest to the point, if it is within reach, free and showing.</summary>
        int Nearest(Vector3 point)
        {
            int best = -1;
            float bestGap = grabReach;
            for (int i = 0; i < planets.Length; i++)
            {
                var p = planets[i];
                if (p.Held || p.Shown < 0.6f) continue;
                float gap = Vector3.Distance(point, p.World) - p.Radius;
                if (gap >= bestGap) continue;
                bestGap = gap;
                best = i;
            }
            return best;
        }

        /// <summary>Each hand: which planet it would pick up (lit before you pinch), and the planet it carries, which follows the fingers.</summary>
        void Hands(HandRig rig, float dt)
        {
            for (int s = 0; s < 2; s++)
            {
                var hand = rig.Get(s == 0);
                var grip = grips[s];
                // While the menu is open the hands are choosing there: no planet lights up for them, and no palm warms the
                // sun. (No pinch picks a planet up then either, and one carried as the menu opens is let go: OnRelease.)
                grip.Tracked = hand.IsTracked && !hand.InSystemGesture && !MenuOpen;
                if (hand.IsTracked) grip.Point = PinchPoint(hand);
                grip.Pinch = grip.Tracked ? hand.Pinch : 0f;
                grip.Near = grip.Tracked && grip.Held < 0 && !IsPinching(s == 0) ? Nearest(grip.Point) : -1;
                if (grip.Near >= 0) Acted("light planet");
                int target = grip.Held >= 0 ? grip.Held : grip.Near;
                if (target >= 0) grip.Shown = target;
                grip.Light = Mathf.MoveTowards(grip.Light, target >= 0 ? 1f : 0f, dt * (target >= 0 ? 8f : 4f));
                if (grip.Held < 0) continue;
                Acted("carry planet");

                // The carried planet follows the fingers; the offset from where it was picked up eases away, so it
                // slides in between them. It can't go into the sun.
                var p = planets[grip.Held];
                grip.Offset *= Mathf.Exp(-10f * dt);
                var at = grip.Point + grip.Offset;
                var fromSun = at - sunAt;
                float closest = SunRadius * sizeFit + SizeOf(grip.Held) + 0.02f;
                if (fromSun.sqrMagnitude < closest * closest)
                    at = sunAt + (fromSun.sqrMagnitude > 1e-8f ? fromSun.normalized : Vector3.down) * closest;
                p.Local = Vector3.Lerp(p.Local, system.InverseTransformPoint(at), 1f - Mathf.Exp(-30f * dt));
                // Its new orbit: round you, as far out as it is now, in its own orbit's plane.
                var flat = Quaternion.Inverse(p.Plane) * p.Local;
                grip.Radius = Mathf.Clamp(new Vector2(flat.x, flat.z).magnitude, newOrbits.x, newOrbits.y);
                grip.Angle = Mathf.Atan2(flat.z, flat.x);
            }
            for (int i = 0; i < planets.Length; i++)
            {
                float lit = 0f;
                foreach (var g in grips)
                    if (g.Shown == i) lit = Mathf.Max(lit, g.Light * (g.Held == i ? 1f : 0.55f + g.Pinch * 0.45f));
                planets[i].Lit = lit;
            }
        }

        /// <summary>Picks a planet up: a burst of sparks and a note, higher for the inner planets.</summary>
        void PickUp(Grip grip, int index)
        {
            var p = planets[index];
            grip.Held = index;
            grip.Free = false;
            grip.Offset = p.World - grip.Point;
            grip.Radius = p.Orbit;
            grip.Angle = p.Angle;
            p.Held = true;
            p.Settle = 1f;
            // Carried on from where it shows, so it doesn't jump.
            p.Local = system.InverseTransformPoint(p.World);
            Burst(p.World, Color.Lerp(p.Air, Color.white, 0.4f), 14, 0.12f, 0.012f);
            Musical.Play(voices, Music, 74 - index * 2, p.World, 0.35f);
        }

        /// <summary>
        /// Lets a planet go: its new orbit goes round you at the distance where you let go, in its own orbit's plane. It
        /// glides onto that orbit, the ring flashes, sparks run along it, and a note plays, higher for a closer orbit.
        /// </summary>
        void LetGo(Grip grip)
        {
            var p = planets[grip.Held];
            grip.Held = -1;
            p.Held = false;
            var flat = Quaternion.Inverse(p.Plane) * p.Local;
            p.Orbit = p.Target = Mathf.Clamp(new Vector2(flat.x, flat.z).magnitude, newOrbits.x, newOrbits.y);
            p.Moved = true;
            p.Angle = Mathf.Repeat(Mathf.Atan2(flat.z, flat.x), Mathf.PI * 2f);
            p.From = p.Local;
            p.Settle = 0f;
            p.Flash = 1.6f;
            // A trail of sparks along the new orbit, ahead of and behind the planet, a few centimetres apart.
            float spacing = 0.025f / p.Orbit;
            for (int i = 0; i < 28; i++)
            {
                float a = p.Angle + (i - 14) * spacing;
                var at = system.TransformPoint(p.Plane * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * p.Orbit);
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = Random.insideUnitSphere * 0.02f,
                    startColor = Color.Lerp(p.Air, Color.white, 0.6f),
                    startSize = Random.Range(0.01f, 0.02f),
                    startLifetime = Random.Range(0.7f, 1.2f)
                }, 1);
            }
            int note = Mathf.RoundToInt(Mathf.Lerp(79f, 57f, Mathf.InverseLerp(newOrbits.x, 2.2f, p.Orbit)));
            Musical.Play(voices, Music, note, p.World, 0.45f);
        }

        /// <summary>
        /// An open palm held out to the sun (facing it, away from your face) warms it over a second or two, and it cools
        /// slowly once you take it away. Embers stream across from the sun to the palm; fully warmed, the sun bursts
        /// into a big flare.
        /// </summary>
        void Warm(HandRig rig, MusicFrame music, float dt)
        {
            float want = 0f;
            var at = sunAt;
            var head = View.transform.position;
            for (int s = 0; s < 2; s++)
            {
                var hand = rig.Get(s == 0);
                var grip = grips[s];
                if (!grip.Tracked || grip.Held >= 0 || IsPinching(s == 0) || hand.Fist > 0.45f || hand.Pinch > 0.6f) continue;
                var palm = PalmPoint(hand);
                float toward = Mathf.InverseLerp(warmAim + 15f, warmAim * 0.5f, Vector3.Angle(hand.PalmNormal, at - palm));
                float outward = Mathf.InverseLerp(0.12f, 0.3f, new Vector2(palm.x - head.x, palm.z - head.z).magnitude);
                float w = toward * outward;
                if (w <= want) continue;
                want = w;
                warmPalm = palm;
            }
            if (want > 0f) Acted("warm sun");
            warmth = Mathf.MoveTowards(warmth, want, dt / (want > warmth ? warmUp : 2.5f));
            if (warmth > 0.97f && !burst)
            {
                burst = true;
                flareAge = 0f;
                flare = 1.6f;
                Burst(at, SunGold, 40, 0.35f, 0.02f);
                Musical.Play(voices, music, 76, at, 0.4f);
            }
            else if (warmth < 0.6f) burst = false;

            if (want < 0.05f) return;
            emberDebt += warmth * 60f * dt;
            int n = (int)emberDebt;
            emberDebt -= n;
            // They leave the sun's rim as you see it and stream across towards the palm.
            var look = (at - head).normalized;
            float surface = SunRadius * sizeFit;
            for (int i = 0; i < n; i++)
            {
                var rim = Vector3.ProjectOnPlane(Random.onUnitSphere, look);
                rim = rim.sqrMagnitude > 1e-6f ? rim.normalized : Vector3.up;
                var from = at + rim * (surface * 1.05f);
                float life = Random.Range(1.1f, 1.6f);
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = from,
                    velocity = rim * Random.Range(0.08f, 0.16f) + (warmPalm - from) / life * Random.Range(0.65f, 0.95f),
                    startColor = Color.Lerp(new Color(1f, 0.97f, 0.85f), Ember, Random.value * 0.5f),
                    startSize = Random.Range(0.02f, 0.036f),
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
                sunVoice.transform.position = sunAt;
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
            float radius = SunRadius * sizeFit * grow * pulse;
            float room = InRoom(sunAt, SunRadius);
            sun.position = sunAt;
            sun.localRotation = Quaternion.Euler(0f, Clock * 4f, 0f);
            sun.localScale = Vector3.one * (radius * 2f);
            block.Clear();
            block.SetFloat(KindId, (float)Look.Sun);
            block.SetFloat(SunPowerId, 0.95f + music.Kick * 0.15f + music.Band(1) * 0.08f + warmth * 0.25f);
            block.SetFloat(ClockId, Clock);
            block.SetFloat(AlphaId, Mathf.Clamp01(grow * 2f) * room);
            sunRenderer.SetPropertyBlock(block);

            // The corona's quad sits just in front of the sun, turned to you, so its glow can lift the disc's edge.
            var toEye = view.transform.position - sunAt;
            float d = Mathf.Max(toEye.magnitude, 1e-3f);
            float half = radius * 3.4f * (1f + warmth * 0.45f + music.Kick * 0.1f + music.Band(1) * 0.08f);
            float lift = Mathf.Min(radius * 1.02f, d * 0.5f);
            Show(corona, grow * room > 0.001f);
            corona.transform.position = sunAt + toEye / d * lift;
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
            block.SetFloat(AlphaId, grow * room);
            corona.SetPropertyBlock(block);
        }

        /// <summary>
        /// The planets with their glows and orbit lines, the moons, Saturn's rings and the asteroid belt. Each planet
        /// keeps out of your face's way (Clear), fades into a scanned wall it reaches, and catches the beat's wave of
        /// light as it passes.
        /// </summary>
        void DrawPlanets(MusicFrame music, Camera view)
        {
            var eye = view.transform.position;
            var wave = new Vector4(flareAge, Mathf.Min(flare, 1f), WaveSpeed, WaveWidth);
            for (int i = 0; i < planets.Length; i++)
            {
                var p = planets[i];
                float grow = Grow(i + 1f);
                float size = SizeOf(i);
                var at = system.TransformPoint(p.Local);
                // A planet just let go near your face eases out of the way as it settles.
                if (!p.Held) at = Vector3.Lerp(at, Clear(at, size, eye), Mathf.SmoothStep(0f, 1f, p.Settle));
                float room = p.Held ? 1f : InRoom(at, size);
                p.World = at;
                p.Fade = room * AwayFromEyes(at, eye);
                p.Shown = grow * p.Fade;
                p.Radius = size * grow * (1f + p.Twinkle * 0.15f + p.Lit * 0.08f) * Mathf.Lerp(0.6f, 1f, room);
                p.Body.position = at;
                p.Body.localRotation = Quaternion.Euler(0f, 0f, p.Axis) * Quaternion.Euler(0f, p.Spin, 0f);
                p.Body.localScale = Vector3.one * (p.Radius * 2f);
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
                block.SetFloat(GlowId, p.Glow + Wave(Vector3.Distance(at, sunAt)) * 0.8f);
                block.SetFloat(TwinkleId, p.Twinkle);
                block.SetFloat(HighlightId, p.Lit);
                block.SetFloat(ClockId, Clock);
                block.SetFloat(AlphaId, Mathf.Clamp01(grow * 2f) * p.Fade);
                p.Renderer.SetPropertyBlock(block);

                // A soft glow of its own colour round it, brighter with its band and when it twinkles.
                bool halo = p.Shown > 0.002f;
                Show(p.Halo, halo);
                if (halo)
                {
                    p.Halo.transform.position = at;
                    Effects.Face(p.Halo.transform, view);
                    p.Halo.transform.localScale = Vector3.one * (p.Radius * 2f * (2.6f + p.Glow * 1.2f + p.Twinkle * 2.5f + p.Lit));
                    block.Clear();
                    block.SetColor(ColorId, new Color(p.Air.r, p.Air.g, p.Air.b, 1f));
                    block.SetFloat(IntensityId, (0.08f + p.Glow * 0.38f + p.Twinkle * 1.1f + p.Lit * 0.5f) * p.Shown);
                    block.SetFloat(CoreId, 0f);
                    p.Halo.SetPropertyBlock(block);
                }

                // Its orbit: faint, with a trail of light behind the planet; dimmer while the planet is carried.
                var ring = p.Ring.transform;
                ring.localRotation = p.Plane;
                ring.localScale = Vector3.one * p.Orbit;
                var tint = Color.Lerp(p.Air, Color.white, 0.2f);
                tint.a = 0.24f;
                block.Clear();
                block.SetColor(ColorId, tint);
                block.SetFloat(AlphaId, grow * (p.Held ? 0.3f : 1f));
                block.SetFloat(WidthId, 0.0019f);
                block.SetFloat(HeadId, Mathf.Repeat(p.Angle / (Mathf.PI * 2f), 1f));
                block.SetFloat(TrailId, p.Held ? 0f : 0.45f + p.Glow * 0.5f);
                block.SetFloat(PulseId, p.Flash);
                block.SetFloat(DashesId, 0f);
                block.SetFloat(ClockId, Clock);
                block.SetVector(SunPosId, sunAt);
                block.SetVector(WaveId, wave);
                p.Ring.SetPropertyBlock(block);
            }

            foreach (var m in moons)
            {
                var parent = planets[m.Planet];
                float grow = Grow(m.Planet + 1.3f);
                float scale = m.Planet >= Giants ? sizeFit : 1f;
                var plane = m.Equator ? Quaternion.Euler(0f, 0f, parent.Axis) : Quaternion.Euler(m.Tilt, 0f, 0f);
                var offset = plane * new Vector3(Mathf.Cos(m.Angle), 0f, Mathf.Sin(m.Angle)) * (m.Distance * scale * grow);
                var at = parent.World + system.rotation * offset;
                float fade = parent.Fade * AwayFromEyes(at, eye);
                m.Body.position = at;
                m.Body.localScale = Vector3.one * (m.Size * scale * 2f * grow * Mathf.Lerp(0.6f, 1f, fade));
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
                block.SetFloat(AlphaId, Mathf.Clamp01(grow * 2f) * fade);
                m.Renderer.SetPropertyBlock(block);
            }

            var saturn = planets[Saturn];
            block.Clear();
            block.SetVector(SunPosId, sunAt);
            block.SetVector(PlanetPosId, saturn.World);
            block.SetFloat(PlanetRadiusId, saturn.Body.lossyScale.x * 0.5f);
            block.SetFloat(SunPowerId, sunLight);
            block.SetFloat(GlowId, saturn.Glow);
            block.SetFloat(AlphaId, saturn.Shown);
            rings.SetPropertyBlock(block);

            belt.transform.localScale = Vector3.one * beltScale;
            block.Clear();
            block.SetFloat(OrbitId, orbitClock);
            block.SetFloat(OmegaId, Mathf.PI * 2f / Mathf.Max(yearSeconds, 1f));
            block.SetFloat(ReferenceId, EarthOrbit);
            block.SetFloat(SizeId, 0.003f);
            block.SetFloat(GlowId, Mathf.Clamp01(music.Band(5) * 0.9f));
            block.SetFloat(ClockId, Clock);
            block.SetFloat(AlphaId, Grow(4.5f));
            block.SetVector(SunPosId, sunAt);
            block.SetVector(WaveId, wave);
            belt.SetPropertyBlock(block);
        }

        /// <summary>
        /// Where the comet of sung word <paramref name="slot"/> is, <paramref name="u"/> of the way through its life: it
        /// swoops in from further out and higher, a little from the middle of its line, to its place on the arc, where
        /// it slows to a gentle drift outwards along the arc (the word is easy to read there), then flies off round you
        /// and up as it fades, so it never crosses its neighbours.
        /// </summary>
        Vector3 Comet(int slot, float u)
        {
            const float arrive = 0.28f, leave = 0.78f;
            float turn, reachOut, rise;
            if (u < arrive)
            {
                float k = 1f - u / arrive;
                k *= k;
                turn = -cometSweep * k;
                reachOut = 1f + 0.5f * k;
                rise = 0.3f * k;
            }
            else if (u < leave)
            {
                float k = (u - arrive) / (leave - arrive);
                turn = 3f * k;
                reachOut = 1f + 0.02f * k;
                rise = 0f;
            }
            else
            {
                float k = (u - leave) / (1f - leave);
                turn = 3f + 5f * k + 14f * k * k;
                reachOut = 1.02f + 0.45f * k * k;
                rise = 0.28f * k * k;
            }
            float a = cometAngle[slot] + cometSide[slot] * turn * Mathf.Deg2Rad;
            float r = cometRadius[slot] * reachOut;
            var c = cometCentre[slot];
            return new Vector3(c.x + Mathf.Cos(a) * r, cometHeight[slot] + rise, c.z + Mathf.Sin(a) * r);
        }

        /// <summary>
        /// The sung words as comets: a bright head with the word riding just beside it, and a tail that points away
        /// from the sun, longer near the sun, bending a little along the way it came. A comet fades into a scanned wall,
        /// and out of the way if you walk right up to it.
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
                var head = Comet(i, u);
                float keep = InRoom(head, 0f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.5f, Vector3.Distance(head, eye)));
                float fade = Mathf.Clamp01(w.Age / 0.3f) * Mathf.Clamp01((w.Life - w.Age) / 0.6f) * FadeIn * keep;
                // Away from the sun, turned a little across your view so the tail never hides end-on.
                var away = head - sunAt;
                float fromSun = away.magnitude;
                away = fromSun > 1e-4f ? away / fromSun : Vector3.down;
                var sight = (head - eye).normalized;
                away -= sight * (Vector3.Dot(away, sight) * 0.6f);
                away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.down;
                float length = 0.16f + 0.22f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.6f, 0.4f, fromSun));
                for (int k = 0; k < TailPoints; k++)
                {
                    float t = k / (float)(TailPoints - 1);
                    var behind = Comet(i, Mathf.Max(0f, u - t * 0.08f));
                    path[k] = Vector3.Lerp(behind, head + away * (t * length), 0.6f);
                    var c = Color.Lerp(Color.white, new Color(0.35f, 0.62f, 1f), Mathf.Sqrt(t));
                    c.a = Mathf.Pow(1f - t, 1.1f) * fade;
                    pathColors[k] = c;
                    pathWidths[k] = Mathf.Lerp(1f, 0.2f, t);
                }
                tails[i].Set(path, pathColors, pathWidths, TailPoints, 0.035f, Color.white, eye, false);

                heads[i].transform.position = head;
                Effects.Face(heads[i].transform, view);
                heads[i].transform.localScale = Vector3.one * (0.06f * (1f + music.Kick * 0.25f));
                block.Clear();
                block.SetColor(ColorId, CometColor);
                block.SetFloat(IntensityId, 1.5f * fade);
                heads[i].SetPropertyBlock(block);

                // The word rides just below its head while the tail points up as you look (above it while the tail
                // points down), so the tail never crosses it and the line keeps its spacing.
                w.Size = 1f;
                float letters = SungWordHeight;
                float below = Vector3.Dot(away, up) >= 0f ? -1f : 1f;
                w.Position = head + up * (below * letters * 0.95f - letters * 0.36f);
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.22f + music.Kick * 0.15f;
                w.Alpha = keep;
            }
        }

        /// <summary>
        /// Feedback for the hands: a ring round the planet within reach (brighter as the fingers close), the dashed
        /// orbit round you that a carried planet will take, with a line from it to where it will join that orbit, and a
        /// small light between fingers pinching empty space.
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
                    float r = p.Radius + 0.012f;
                    grip.Reach.transform.position = p.World;
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
                    block.SetColor(ColorId, new Color(1f, 0.95f, 0.82f, 0.8f));
                    block.SetFloat(AlphaId, grip.PreviewAlpha);
                    block.SetFloat(WidthId, 0.0024f);
                    block.SetFloat(HeadId, Mathf.Repeat(grip.PreviewAngle / (Mathf.PI * 2f), 1f));
                    block.SetFloat(TrailId, 1f);
                    block.SetFloat(PulseId, 0f);
                    block.SetFloat(DashesId, Mathf.Max(12f, Mathf.Round(Mathf.PI * 2f * grip.PreviewRadius / 0.1f)));
                    block.SetFloat(ClockId, Clock);
                    grip.Preview.SetPropertyBlock(block);
                }

                bool dropping = false;
                if (carrying)
                {
                    var p = planets[grip.Held];
                    var foot = system.TransformPoint(p.Plane * new Vector3(Mathf.Cos(grip.Angle), 0f, Mathf.Sin(grip.Angle)) * grip.Radius);
                    var top = p.World;
                    dropping = Vector3.Distance(top, foot) > 0.02f;
                    if (dropping)
                    {
                        for (int k = 0; k < DropPoints; k++)
                        {
                            float t = k / (float)(DropPoints - 1);
                            path[k] = Vector3.Lerp(top, foot, t);
                            pathColors[k] = new Color(1f, 0.95f, 0.85f, Mathf.Lerp(0.15f, 0.7f, t));
                        }
                        grip.Drop.Set(path, pathColors, DropPoints, 0.003f, Color.white, eye, false);
                    }
                }
                grip.Drop.SetVisible(dropping);

                bool nib = grip.Free && grip.Tracked && IsPinching(s == 0);
                Show(grip.Nib, nib);
                if (nib)
                {
                    grip.Nib.transform.position = grip.Point;
                    Effects.Face(grip.Nib.transform, view);
                    grip.Nib.transform.localScale = Vector3.one * 0.022f;
                    block.Clear();
                    block.SetColor(ColorId, SunGold);
                    block.SetFloat(IntensityId, 0.5f);
                    grip.Nib.SetPropertyBlock(block);
                }
            }
        }

        void Burst(Vector3 at, Color color, int count, float speed, float size)
        {
            for (int i = 0; i < count; i++)
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = Random.onUnitSphere * (speed * Random.Range(0.4f, 1f)),
                    startColor = color,
                    startSize = size * Random.Range(0.6f, 1.2f),
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
            // Room for the dip under your face that the shader may add.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2.2f, 1.2f, 2.2f));
            return mesh;
        }

        /// <summary>
        /// The asteroid belt's mesh: a quad per asteroid whose four corners all hold its orbit (radius, height, starting
        /// angle), with its seed and size in the second UV. Most are small, a few larger; they crowd towards the middle
        /// of the belt. The shader moves them round you and turns them to face you.
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
                float height = ((float)random.NextDouble() - 0.5f) * 0.05f;
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
            // The shader moves the asteroids round you, so the bounds cover the whole belt (and the dip under your face).
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(radii.y * 2.2f, 1f, radii.y * 2.2f));
            return mesh;
        }
    }
}
