using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MusicInYourHands
{
    /// <summary>
    /// The Flower dance world: a garden of cartoon flowers springs up out of the floor in a loose arc around you (and,
    /// once the room is scanned, on your table, sofa and shelves) and dances to the music. Five kinds grow there:
    /// daisies, cup flowers, star flowers, puffballs and tall sunbursts, each with a bendy stem, two leaves and a big
    /// head that turns to face you. They sway from side to side on the beat, stems bending and heads bobbing with a
    /// little squash and stretch while their leaves lift like arms, and the beat ripples out across the garden from
    /// the flowers in front of you. On every beat a few flowers get bold: they pop bigger, open their petals wide and
    /// glow in rich colours, then relax. Each sung word floats up out of a bold flower in a puff of pollen.
    /// Your hands are the sun: a warm glow shines from each palm, and the whole garden turns to it. Wherever you move
    /// your hand, the flowers lean that way and turn their faces round towards it, still swaying from side to side on
    /// the beat; they lean further and open a little as it comes near. Hold your palm over a flower and it stretches up
    /// to meet it, glowing, with a soft note and pollen rising to your palm.
    /// The whole garden is one mesh drawn in one go: the shader bends and poses every flower from a few values per
    /// flower that this class sets each frame.
    /// </summary>
    public class FlowerDance : World
    {
        public override string DisplayName => "Flower dance";

        public override Color Color => new Color(0.6f, 0.9f, 0.42f);

        public override int MenuOrder => 116;

        public override int MenuIcon => 26;

        public override string Hint => "Your hand is the sun: move it and the flowers lean after it";

        // Sun-warm hands: golden edges and a glow of heat.
        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.3f, 0.18f, 0.05f, 0.45f),
            Edge = new Color(1f, 0.86f, 0.45f),
            Glow = new Color(1f, 0.7f, 0.3f),
            Pattern = HandTheme.Effect.Heat,
            PatternColor = new Color(1f, 0.8f, 0.35f)
        };

        [Header("Materials")]
        [Tooltip("The garden (Materials/FlowerDance): its shader bends and poses every flower.")]
        [SerializeField] Material flowerDanceMaterial;

        [Header("Garden")]
        [Tooltip("How many flowers grow. Once the room is scanned, some of them grow on the furniture instead.")]
        [SerializeField, Range(8, MaxFlowers)] int flowerCount = 40;
        [Tooltip("How far from you the front and the back rows of flowers stand (m).")]
        [SerializeField] Vector2 rowDistance = new Vector2(1.2f, 2.05f);
        [Tooltip("How far round you the garden reaches on each side (degrees).")]
        [SerializeField] float arc = 100f;
        [Tooltip("How many flowers may grow on tables and furniture once the room is scanned.")]
        [SerializeField] int onFurniture = 8;
        [Tooltip("Their size there, against the flowers on the floor.")]
        [SerializeField] float furnitureSize = 0.6f;

        [Header("Dance")]
        [Tooltip("Seconds per beat while the music gives no beat to follow.")]
        [SerializeField] float steadyBeat = 0.6f;
        [Tooltip("How far the flowers sway from side to side, as a share of their height.")]
        [SerializeField] float sway = 0.26f;
        [Tooltip("How many beats the dance takes to ripple from the flowers in front of you out to the ends of the garden.")]
        [SerializeField] float ripple = 0.35f;
        [Tooltip("How many flowers get bold on each beat.")]
        [SerializeField] int boldPerBeat = 3;
        [Tooltip("Seconds a flower's tip takes to glide after its dance pose: more is softer, less follows the beat more tightly.")]
        [SerializeField] float tipGlide = 0.08f;

        [Header("Hands")]
        [Tooltip("How far the garden leans after your hand (the sun) at most, as a share of each flower's height.")]
        [SerializeField] float sunLean = 0.4f;
        [Tooltip("How far (m) your hand goes from the middle in front of your chest for the garden to lean its furthest that way.")]
        [SerializeField] float sunRange = 0.35f;
        [Tooltip("Seconds a flower's lean takes to glide after the sun as your hand moves.")]
        [SerializeField] float leanGlide = 0.3f;
        [Tooltip("How far round towards your hand the flowers turn their faces (0: they keep facing you; 1: they face your hand).")]
        [SerializeField, Range(0f, 1f)] float sunFace = 0.45f;
        [Tooltip("The size (m) of the sun's glow on your palm.")]
        [SerializeField] float sunSize = 0.14f;
        [Tooltip("How near (m) a hand must come to a flower's head for the flower to lean further, its tip towards your palm.")]
        [SerializeField] float leanReach = 0.6f;
        [Tooltip("How far to the side (m) of a flower's head a palm held over it still draws it up.")]
        [SerializeField] float overReach = 0.2f;
        [Tooltip("How much taller a flower can stretch to reach a palm, as a share of its height.")]
        [SerializeField] float maxStretch = 0.7f;

        [Header("Words")]
        [Tooltip("Seconds each sung word floats before it fades.")]
        [SerializeField] float wordLife = 3.4f;
        [Tooltip("How fast the sung words float up (m/s).")]
        [SerializeField] float wordRise = 0.12f;

        // As many as the shader's arrays hold (FLOWERS in FlowerDance.shader).
        const int MaxFlowers = 48;
        const int HaloCount = 10;

        // The garden mesh's parts, as the shader knows them (uv0.z).
        const float StemPart = 0f, LeafPart = 1f, PetalPart = 2f, DiscPart = 3f, CalyxPart = 4f, GrassPart = 5f, ShadowPart = 6f;

        static readonly int RootId = Shader.PropertyToID("_Root");
        static readonly int PoseId = Shader.PropertyToID("_Pose");
        static readonly int LookId = Shader.PropertyToID("_Look");
        static readonly int LeafId = Shader.PropertyToID("_Leaf");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int CoreId = Shader.PropertyToID("_Core");

        static readonly Color[] Greens = { new Color(0.33f, 0.66f, 0.24f), new Color(0.41f, 0.73f, 0.27f), new Color(0.28f, 0.6f, 0.3f) };
        static readonly Color[] DaisyTips = { new Color(1f, 0.56f, 0.74f), new Color(0.76f, 0.62f, 1f), new Color(0.52f, 0.76f, 1f), new Color(1f, 0.74f, 0.4f) };
        static readonly Color[] CupColors =
        {
            new Color(0.97f, 0.27f, 0.32f), new Color(1f, 0.47f, 0.4f), new Color(1f, 0.43f, 0.68f), new Color(1f, 0.6f, 0.2f),
            new Color(1f, 0.83f, 0.27f), new Color(0.72f, 0.42f, 0.96f)
        };
        static readonly Color[] StarColors = { new Color(0.63f, 0.43f, 1f), new Color(0.4f, 0.6f, 1f), new Color(0.93f, 0.4f, 0.87f) };
        static readonly Color[] PuffColors = { new Color(0.74f, 0.44f, 0.97f), new Color(0.97f, 0.4f, 0.72f), new Color(1f, 0.62f, 0.5f), new Color(0.5f, 0.73f, 1f) };
        static readonly Color[] SunColors = { new Color(1f, 0.8f, 0.15f), new Color(1f, 0.64f, 0.18f) };
        static readonly Color PollenColor = new Color(1f, 0.88f, 0.45f);

        enum Kind { Daisy, Cup, Star, Puff, Sunburst }

        enum Shape { Round, Slender, Pointed }

        /// <summary>How a kind of flower is built and how it dances.</summary>
        struct Style
        {
            public Vector2 Height;  // stem length range (m)
            public float Stem;      // stem radius at the root (m)
            public float Face;      // how far its face turns towards you (degrees from straight up)
            public float Sway;      // its sway against the garden's
            public float Bob;       // how much it squashes on the beat
            public float Nod;       // how far its head nods on the beat (radians)
            public float Lift;      // how far its leaves lift on the beat (radians)
            public bool HalfTime;   // sways across once every two beats
            public int Note;        // the note it sings when it reaches a palm (MIDI, fitted to the song)
        }

        static readonly Style[] Styles =
        {
            new Style { Height = new Vector2(0.42f, 0.56f), Stem = 0.0095f, Face = 55f, Sway = 1.1f, Bob = 1.2f, Nod = 0.3f, Lift = 0.5f, Note = 72 },
            new Style { Height = new Vector2(0.46f, 0.62f), Stem = 0.011f, Face = 16f, Sway = 1f, Bob = 1f, Nod = 0.2f, Lift = 0.32f, Note = 67 },
            new Style { Height = new Vector2(0.56f, 0.74f), Stem = 0.011f, Face = 42f, Sway = 0.95f, Bob = 1f, Nod = 0.26f, Lift = 0.45f, Note = 76 },
            new Style { Height = new Vector2(0.8f, 1.02f), Stem = 0.012f, Face = 25f, Sway = 0.6f, Bob = 1.5f, Nod = 0.14f, Lift = 0.4f, Note = 64 },
            new Style { Height = new Vector2(1.05f, 1.35f), Stem = 0.017f, Face = 50f, Sway = 0.9f, Bob = 0.8f, Nod = 0.12f, Lift = 0.3f, HalfTime = true, Note = 60 }
        };

        // Cartoon proportions: every head is built this much bigger than life.
        const float HeadSize = 1.25f;

        // The order the kinds come in, so every row gets a mix.
        static readonly Kind[] Planting =
        {
            Kind.Daisy, Kind.Cup, Kind.Star, Kind.Sunburst, Kind.Daisy, Kind.Puff, Kind.Cup, Kind.Star, Kind.Daisy, Kind.Sunburst, Kind.Cup, Kind.Puff
        };

        /// <summary>One flower: its shape (fixed when the garden is built), where it grows and how it is moving.</summary>
        sealed class Flower
        {
            public Kind Kind;
            public float Height;        // the stem's length (m) at full size
            public float Head;          // the head's radius (m) at full size
            public float Face;          // how far its face turns towards you (degrees from straight up)
            public float Neck;          // from the stem's tip to the middle of its face (m)
            public Color Petal;         // its main colour, for its glow, its pollen and its words
            public float RowKey;        // sorts the flowers into rows: shortest in front

            public bool Planted;
            public Vector3 Root;
            public float Yaw;           // radians about up; the flower's own +Z faces you
            public float Size;          // 1 on the floor, smaller on furniture
            public float Delay;         // beats after the flowers in front of you that the dance reaches it
            public float Side;          // which way it sways on even beats
            public float Sprout;        // the Clock time it starts to grow

            public float BoldAge = 99f; // seconds since it last got bold
            public float Spoke = -99f;  // the Clock time a word last came out of it
            public Vector2 Lean;        // towards the hands (share of its height, its own x and z)
            public Vector2 LeanSpeed;   // how fast Lean is gliding, for its spring
            public Vector2 Tip;         // where its tip is this frame, gliding after the dance's pose
            public Vector2 TipSpeed;    // how fast Tip is gliding, for its spring
            public float Stretch;       // up towards a palm (extra share of its height)
            public float Open;          // petals opened by the hands
            public float Warm;          // glow from the hands
            public float Over;          // how strongly a palm is over it
            public Vector3 Palm;        // the palm it reaches for
            public bool Touched;        // it reached a palm and the palm is still there
            public float Chime;         // seconds until it may chime again
            public float Grown;         // its size this frame
            public Vector3 FacePoint;   // the middle of its face this frame (room space)
        }

        Flower[] flowers;
        int[] order;
        Mesh mesh;
        MeshRenderer garden;
        MaterialPropertyBlock block, haloBlock;
        readonly Vector4[] roots = new Vector4[MaxFlowers];
        readonly Vector4[] poses = new Vector4[MaxFlowers];
        readonly Vector4[] looks = new Vector4[MaxFlowers];
        readonly Vector4[] leaves = new Vector4[MaxFlowers];
        readonly float[] glows = new float[MaxFlowers];
        Renderer[] halos;
        readonly int[] haloOwners = new int[HaloCount];
        readonly List<Room.Surface> nearTops = new List<Room.Surface>();
        readonly int[] rowSlots = new int[MaxFlowers];
        readonly int[] trySlots = new int[MaxFlowers];
        ParticleSystem pollen;
        AudioPool voices;
        Vector3 eye;
        float beat, beatError, energy, tempo;
        /// <summary>The way the garden faced you when it was planted, for reading where your hand (the sun) is.</summary>
        Vector3 gardenForward = Vector3.forward;
        /// <summary>
        /// Where the sun pulls the whole garden (room space, up to 1 long): to your left or right, and away from you,
        /// as far as your hand is from the middle in front of your chest. It glides, so the garden sways after it.
        /// </summary>
        Vector3 sunPull, sunPullSpeed;
        /// <summary>How fully the sun shines (0 with no hand out).</summary>
        float sunShine;
        /// <summary>The sun on each palm: its glow, where it is (following the palm a little behind) and how fully it shows (0-1).</summary>
        Renderer[] suns;
        readonly Vector3[] sunAt = new Vector3[2];
        readonly float[] sunShown = new float[2];

        static readonly Color SunColor = new Color(1f, 0.82f, 0.38f);

        protected override float FadeInSeconds => 1f;

        protected override int SungWordCount => 6;

        protected override float SungWordHeight => 0.075f;

        protected override float SungWordsAlpha => FadeIn;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            haloBlock = new MaterialPropertyBlock();
            var random = new System.Random(26);
            int count = Mathf.Clamp(flowerCount, 1, MaxFlowers);
            flowers = new Flower[count];
            order = new int[count];
            var keys = new float[count];
            var builder = new MeshBuilder();
            for (int i = 0; i < count; i++)
            {
                var f = new Flower { Kind = Planting[i % Planting.Length] };
                BuildFlower(builder, i, f, random);
                f.RowKey = f.Height + (Next(random) - 0.5f) * 0.18f;
                flowers[i] = f;
                order[i] = i;
                keys[i] = f.RowKey;
            }
            System.Array.Sort(keys, order);
            mesh = builder.Build("Flower garden");

            var go = new GameObject("Garden");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            garden = go.AddComponent<MeshRenderer>();
            garden.sharedMaterial = flowerDanceMaterial;
            garden.shadowCastingMode = ShadowCastingMode.Off;
            garden.receiveShadows = false;
            garden.lightProbeUsage = LightProbeUsage.Off;
            garden.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(false);

            halos = new Renderer[HaloCount];
            for (int h = 0; h < HaloCount; h++)
            {
                halos[h] = Effects.MakeQuad($"Flower Glow {h}", transform, glowMaterial);
                halos[h].gameObject.SetActive(false);
            }
            suns = new Renderer[2];
            for (int s = 0; s < 2; s++)
            {
                suns[s] = Effects.MakeQuad(s == 0 ? "Sun L" : "Sun R", transform, glowMaterial);
                suns[s].gameObject.SetActive(false);
            }
            pollen = Effects.MakeSparkSystem("Pollen", transform, glowMaterial);
            var main = pollen.main;
            main.maxParticles = 400;
            main.gravityModifier = -0.01f;
            voices = new AudioPool(transform, 4);
            // A firm dark edge keeps the pale words readable over the flowers and a bright room.
            if (SungWords != null) SungWords.Rim = 0.8f;
        }

        protected override void OnEnter()
        {
            beat = 0f;
            beatError = 0f;
            tempo = 0f;
            sunShown[0] = sunShown[1] = 0f;
            sunPull = sunPullSpeed = Vector3.zero;
            sunShine = 0f;
            foreach (var f in flowers)
            {
                f.BoldAge = 99f;
                f.Spoke = -99f;
            }
        }

        protected override void OnExit()
        {
            if (garden != null) garden.gameObject.SetActive(false);
            if (halos != null)
                foreach (var h in halos) h.gameObject.SetActive(false);
            if (suns != null)
                foreach (var s in suns) s.gameObject.SetActive(false);
            if (pollen != null) pollen.Clear();
        }

        /// <summary>
        /// The garden grows round wherever you are: the shortest flowers on any furniture near you (once the room is
        /// scanned), the rest in rows on the floor. Each flower springs up out of the ground, the ones in front of you
        /// first.
        /// </summary>
        protected override void OnPlace(Camera view)
        {
            var head = view.transform.position;
            var forward = Effects.FlatForward(view);
            float floorY = Effects.FloorY(view);
            gardenForward = forward;
            foreach (var f in flowers) f.Planted = false;
            int used = Room.Scanned ? PlantOnFurniture(head, forward, floorY) : 0;
            PlantRows(head, forward, floorY, used);
            garden.gameObject.SetActive(true);
            // The shader moves every flower into place, so the bounds the mesh was built with mean nothing: make them
            // cover the garden round you, in the garden's own space, the way the other worlds set their bounds.
            var centre = garden.transform.InverseTransformPoint(new Vector3(head.x, floorY + 1.3f, head.z));
            mesh.bounds = new Bounds(centre, new Vector3(9f, 4f, 9f));
        }

        /// <summary>On each beat a few flowers get bold, mostly ones you can see.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            int count = boldPerBeat + (music.Kick > 0.85f ? 1 : 0);
            for (int n = 0; n < count; n++)
            {
                int pick = -1;
                float best = float.MinValue;
                for (int tries = 0; tries < 8; tries++)
                {
                    int i = Random.Range(0, flowers.Length);
                    var f = flowers[i];
                    if (!f.Planted || f.BoldAge < 1.2f || f.Grown < 0.5f * f.Size) continue;
                    float score = Seen(f) + Random.value * 0.7f;
                    if (score <= best) continue;
                    best = score;
                    pick = i;
                }
                if (pick >= 0) Embolden(flowers[pick]);
            }
        }

        /// <summary>
        /// Each sung word floats up out of a bold flower you can see (one gets bold for it if none is), in a puff of
        /// pollen, and rises rocking like a petal on a breath of air before it fades. The flower is chosen clear of
        /// the words still in the air, and rather from the middle and back of the garden, above your hands.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            int pick = -1;
            float best = float.MinValue;
            var from = View.transform.position;
            for (int i = 0; i < flowers.Length; i++)
            {
                var f = flowers[i];
                if (!f.Planted || f.Grown < 0.3f * f.Size) continue;
                float back = Mathf.Clamp01((FlatDistance(f.Root, from) - 1.1f) / 0.8f);
                float score = Seen(f) * 2f + (f.BoldAge < 0.6f ? 1.2f : 0f) + back * 0.8f - Crowding(f, from)
                              - (Clock - f.Spoke < wordLife ? 2f : 0f) + Random.value * 0.8f;
                if (score <= best) continue;
                best = score;
                pick = i;
            }
            if (pick < 0) return;
            var flower = flowers[pick];
            if (flower.BoldAge > 0.4f) Embolden(flower);
            flower.Spoke = Clock;
            var w = SungWords.Add(word, Color.Lerp(flower.Petal, Color.white, 0.45f), wordLife);
            w.Slot = pick;
            w.Position = flower.FacePoint + Vector3.up * (flower.Head * flower.Grown * 1.1f + 0.05f);
            w.Velocity = Vector3.up * wordRise;
            // Its full size, kept in Spot: big enough to read from where you stand.
            w.Spot = new Vector2(Mathf.Clamp(Vector3.Distance(from, w.Position) / 1.3f, 0.8f, 1.8f), 0f);
            w.Size = w.Spot.x * 0.5f;
            Puff(flower, 14, 0.12f);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            KeepTime(music, dt);
            float loud = Mathf.Clamp01((music.Band(1) + music.Band(2) + music.Band(3)) / 2.2f);
            energy = Mathf.Lerp(energy, loud, 1f - Mathf.Exp(-dt * 3f));
            eye = view.transform.position;
            FollowSun(rig, view, dt);
            for (int i = 0; i < flowers.Length; i++) Dance(i, rig, dt);
            block.SetVectorArray(RootId, roots);
            block.SetVectorArray(PoseId, poses);
            block.SetVectorArray(LookId, looks);
            block.SetVectorArray(LeafId, leaves);
            garden.SetPropertyBlock(block);
            Glow(view);
            Sing(view, dt);
        }

        // ---- The dance ----

        /// <summary>
        /// The dance's own beat clock: it counts beats, landing on whole numbers with the song's beats, and runs on at
        /// the song's tempo between them (or at a steady tempo when there is no beat), so the flowers never stop or jump.
        /// The tempo it runs at glides to the song's, and a beat that comes a little early or late is eased into over
        /// about half a second rather than caught up at once, so the dance never lurches.
        /// </summary>
        void KeepTime(MusicFrame music, float dt)
        {
            bool live = music.BeatInterval > 0.25f && music.BeatInterval < 2f && music.BeatAge < music.BeatInterval * 4f;
            float interval = live ? music.BeatInterval : steadyBeat;
            tempo = tempo <= 0f ? interval : Mathf.Lerp(tempo, interval, 1f - Mathf.Exp(-dt * 1.5f));
            beat += dt / tempo;
            if (live && music.Beat)
            {
                // The beat came BeatAge seconds ago, when the clock should have shown a whole number. A tiny miss is
                // left alone: detected beats wobble by that much, and chasing it would make the flowers twitch.
                float then = beat - music.BeatAge / tempo;
                float miss = Mathf.Round(then) - then;
                beatError = Mathf.Abs(miss) < 0.04f ? 0f : miss;
            }
            float pull = beatError * Mathf.Min(1f, dt * 2.5f);
            beat += pull;
            beatError -= pull;
        }

        /// <summary>
        /// One flower's dance this frame: it sways smoothly from side to side, reaching a side on each beat; dips, nods
        /// and lifts its leaves as each beat lands; swells when it is bold; leans and turns after the sun (your hand);
        /// and stretches up to a palm over it. Every move rises and eases out smoothly, and the tip glides after its pose
        /// on a spring, so nothing snaps. Fills its row of the shader's arrays.
        /// </summary>
        void Dance(int i, HandRig rig, float dt)
        {
            var f = flowers[i];
            if (!f.Planted)
            {
                leaves[i] = Vector4.zero;
                glows[i] = 0f;
                return;
            }
            var style = Styles[(int)f.Kind];
            float size = f.Size * Grow(Clock - f.Sprout);
            f.Grown = size;

            // It turns slowly to keep its face to you, and round towards the sun, the further the further out your hand is.
            var lookAt = eye + sunPull * (2f * sunFace);
            float want = YawTowards(f.Root, lookAt);
            f.Yaw += Mathf.DeltaAngle(f.Yaw * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad * (1f - Mathf.Exp(-dt * 1.5f));

            // The beat reaches it a little after the flowers in front of you, so the dance ripples outwards.
            float b = beat - f.Delay;
            float tb = b - Mathf.Floor(b);
            float sb = style.HalfTime ? b * 0.5f : b;
            // One smooth swing from side to side, at a side on every beat (every other beat for the tall ones), and
            // tallest as it passes the middle.
            float swing = f.Side * Mathf.Cos(Mathf.PI * sb);
            float upright = Mathf.Sin(Mathf.PI * sb);
            upright *= upright;
            // As a beat lands it dips, nods and lifts its leaves, rising into each and easing out again.
            float hit = Pulse(tb, 0.15f);
            float squash = (-0.08f * hit + 0.035f * upright) * style.Bob;
            float flap = style.Lift * (0.3f + 0.7f * Pulse(tb, 0.18f));

            // Bold: it swells bigger, opens wide and glows, then relaxes over about a second.
            f.BoldAge += dt;
            float a = f.BoldAge;
            float pop = Pulse(a, 0.2f);
            float open = Pulse(a, 0.25f);
            float bold = Mathf.SmoothStep(0f, 1f, a / 0.15f) * Mathf.Exp(-Mathf.Max(0f, a - 0.15f) * 1.5f);

            // It dances bigger as the music gets louder; a flower reaching up to a palm holds still under it.
            float over = Reach(f, rig, size, dt);
            float calm = 1f - 0.75f * over;
            float amount = sway * style.Sway * (0.45f + 0.75f * energy) * calm;
            var pose = new Vector2(swing * amount, 0.04f * hit * calm) + f.Lean;
            // The tip glides after its pose, so a beat that comes early or a sudden hand never makes it snap.
            f.Tip = Vector2.SmoothDamp(f.Tip, pose, ref f.TipSpeed, tipGlide, Mathf.Infinity, dt);
            var tip = f.Tip;
            roots[i] = new Vector4(f.Root.x, f.Root.y, f.Root.z, f.Yaw);
            poses[i] = new Vector4(tip.x, tip.y, (1f + squash * 0.5f * calm + 0.06f * pop) * (1f + f.Stretch), 1f + 0.45f * pop + 0.05f * energy);
            looks[i] = new Vector4(Mathf.Clamp01(Mathf.Max(open, f.Open)), Mathf.Clamp01(Mathf.Max(bold, f.Warm)), squash * 1.6f * calm, style.Nod * hit * calm);
            leaves[i] = new Vector4(flap * (0.55f + 0.45f * Mathf.Max(0f, -swing)), flap * (0.55f + 0.45f * Mathf.Max(0f, swing)), size, 0f);
            f.FacePoint = FaceOf(f, poses[i], size);
            glows[i] = size > 0.05f ? Mathf.Max(bold, f.Warm * 0.8f) : 0f;
        }

        /// <summary>
        /// A smooth pulse <paramref name="t"/> (seconds or beats) after it starts: it rises from 0 to 1 at
        /// <paramref name="peak"/> without a jolt, then eases back out.
        /// </summary>
        static float Pulse(float t, float peak)
        {
            if (t <= 0f) return 0f;
            float x = t / peak;
            return x * Mathf.Exp(1f - x);
        }

        /// <summary>How far a flower has grown, t seconds after it starts: it springs up with a little overshoot.</summary>
        static float Grow(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1.2f) return 1f;
            float x = t / 0.7f;
            return 1f - Mathf.Exp(-x * 4.5f) * Mathf.Cos(x * 5.5f);
        }

        /// <summary>
        /// The sun (your hands) draws every flower towards it: wherever the hand is, the flower leans that way and opens
        /// a little, and leans further, its tip towards the palm, as the hand comes near. A palm held flat over it pulls
        /// it up to meet the palm, wide open and glowing, with a soft note as it arrives and pollen rising to the palm.
        /// Returns how strongly a palm is over it (0-1).
        /// </summary>
        float Reach(Flower f, HandRig rig, float size, float dt)
        {
            var lean = Vector2.zero;
            var under = Vector2.zero;
            float stretch = 0f, open = 0f, warm = 0f, over = 0f;
            float height = Mathf.Max(f.Height * size, 0.05f);
            var head = f.Root + Vector3.up * (height + f.Neck * size);
            if (size > 0.15f && sunShine > 0f)
            {
                // The whole garden leans the way the sun pulls it: towards wherever your hand is.
                var pull = ToLocal(sunPull, f.Yaw);
                lean += new Vector2(pull.x, pull.z) * sunLean;
                open = 0.25f * sunShine;
            }
            if (rig != null && size > 0.15f)
            {
                for (int s = 0; s < 2; s++)
                {
                    var hand = rig.Get(s == 0);
                    if (!hand.IsTracked || hand.InSystemGesture) continue;
                    var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                    // Leaning follows the sun on the palm, which trails the hand a little, so tracking tremor doesn't shake the flowers.
                    var shining = sunShown[s] > 0f ? sunAt[s] : palm;
                    var local = ToLocal(shining - f.Root, f.Yaw);
                    // The lean that would bring its tip under the palm.
                    var toward = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / height, 0.5f);
                    float near = Smooth01((leanReach - Vector3.Distance(shining, head)) / Mathf.Max(leanReach - 0.1f, 0.01f));
                    lean += toward * (near * 0.75f);
                    open = Mathf.Max(open, near * 0.5f);
                    warm = Mathf.Max(warm, near * 0.3f);
                    // While the menu is open a flower still leans towards a hand near it, but no palm draws it up.
                    if (MenuOpen) continue;
                    // Over it: the palm faces down, not far to the side, somewhere above its head.
                    float down = Smooth01((-hand.PalmNormal.y - 0.4f) / 0.35f);
                    float across = new Vector2(palm.x - head.x, palm.z - head.z).magnitude;
                    float above = palm.y - head.y;
                    float w = down * Smooth01((overReach + f.Head * size - across) / (overReach * 0.6f))
                              * Smooth01((above + 0.04f) / 0.08f) * Smooth01((1.1f - above) / 0.3f);
                    if (w <= over) continue;
                    over = w;
                    under = toward;
                    f.Palm = palm;
                    stretch = Mathf.Clamp((palm.y - 0.08f - f.Root.y - f.Neck * size) / height - 1f, 0f, maxStretch);
                }
                if (over > 0f) Acted("palm over flower");
            }
            float follow = 1f - Mathf.Exp(-dt * 4f);
            f.Lean = Vector2.SmoothDamp(f.Lean, Vector2.Lerp(Vector2.ClampMagnitude(lean, 0.6f), under, over), ref f.LeanSpeed, leanGlide, Mathf.Infinity, dt);
            f.Stretch = Mathf.Lerp(f.Stretch, stretch * over, 1f - Mathf.Exp(-dt * 2.5f));
            f.Open = Mathf.Lerp(f.Open, Mathf.Max(open, over), follow);
            f.Warm = Mathf.Lerp(f.Warm, Mathf.Max(warm, over * 0.55f), follow);
            f.Over = Mathf.Lerp(f.Over, over, follow);

            f.Chime -= dt;
            if (f.Over > 0.6f && !f.Touched && f.Chime <= 0f)
            {
                f.Touched = true;
                f.Chime = 1.2f;
                Musical.Play(voices, Music, Styles[(int)f.Kind].Note, f.FacePoint, 0.35f);
                Puff(f, 10, 0.1f);
            }
            else if (f.Over < 0.25f) f.Touched = false;
            // A thin stream of pollen rises from its face to the palm.
            if (f.Over > 0.35f && Random.value < dt * 16f * f.Over) Drift(f);
            return f.Over;
        }

        /// <summary>
        /// Your hands are the sun: a warm glow on each tracked palm that swells on every beat and burns brighter as the
        /// music gets louder. The sun trails the palm slightly, so the flowers move smoothly instead of trembling with the
        /// tracking. Where it is against the middle in front of your chest (to your left or right, nearer or further)
        /// sets how the whole garden leans and turns.
        /// </summary>
        void FollowSun(HandRig rig, Camera view, float dt)
        {
            float follow = 1f - Mathf.Exp(-dt * 6f);
            float pulse = Pulse(beat - Mathf.Floor(beat), 0.15f);
            for (int s = 0; s < 2; s++)
            {
                var hand = rig != null ? rig.Get(s == 0) : null;
                bool lit = hand != null && hand.IsTracked && !hand.InSystemGesture;
                if (lit)
                {
                    var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                    var at = palm + hand.PalmNormal * 0.025f;
                    sunAt[s] = sunShown[s] <= 0f ? at : Vector3.Lerp(sunAt[s], at, follow);
                }
                sunShown[s] = Mathf.MoveTowards(sunShown[s], lit ? 1f : 0f, dt * 3f);

                var glow = suns[s];
                bool show = sunShown[s] > 0.01f;
                if (glow.gameObject.activeSelf != show) glow.gameObject.SetActive(show);
                if (!show) continue;
                glow.transform.position = sunAt[s];
                Effects.Face(glow.transform, view);
                glow.transform.localScale = Vector3.one * (sunSize * (1f + 0.35f * pulse + 0.2f * energy));
                haloBlock.Clear();
                haloBlock.SetColor(ColorId, SunColor);
                haloBlock.SetFloat(IntensityId, (0.8f + 0.6f * energy + 0.7f * pulse) * sunShown[s] * FadeIn);
                haloBlock.SetFloat(CoreId, 0.55f);
                glow.SetPropertyBlock(haloBlock);
            }

            // The pull on the garden: with both hands out the sun is between them.
            var forward = gardenForward;
            var right = Vector3.Cross(Vector3.up, forward);
            var chest = eye + forward * 0.3f + Vector3.down * 0.3f;
            var sum = Vector3.zero;
            float weight = 0f;
            sunShine = 0f;
            for (int s = 0; s < 2; s++)
            {
                if (sunShown[s] <= 0f) continue;
                sum += sunAt[s] * sunShown[s];
                weight += sunShown[s];
                sunShine = Mathf.Max(sunShine, sunShown[s]);
            }
            var target = Vector3.zero;
            if (weight > 0f)
            {
                var offset = sum / weight - chest;
                float across = Mathf.Clamp(Vector3.Dot(offset, right) / sunRange, -1f, 1f);
                float away = Mathf.Clamp(Vector3.Dot(offset, forward) / sunRange, -1f, 1f);
                target = Vector3.ClampMagnitude(right * across + forward * (away * 0.6f), 1f) * sunShine;
            }
            sunPull = Vector3.SmoothDamp(sunPull, target, ref sunPullSpeed, 0.25f, Mathf.Infinity, dt);
        }

        void Embolden(Flower f)
        {
            f.BoldAge = 0f;
            Puff(f, 8, 0.16f);
        }

        /// <summary>How much a word rising from the flower would overlap the words already floating, as seen from the eye.</summary>
        float Crowding(Flower f, Vector3 from)
        {
            float crowd = 0f;
            var start = f.FacePoint + Vector3.up * 0.1f - from;
            var words = SungWords.Words;
            for (int i = 0; i < words.Count; i++)
            {
                if (!words[i].Active) continue;
                float apart = Vector3.Angle(start, words[i].Position - from);
                if (apart < 12f) crowd += 1.6f * (1f - apart / 12f);
            }
            return crowd;
        }

        /// <summary>How squarely you look at the flower: 1 straight at it, 0 at 60 degrees off or more.</summary>
        float Seen(Flower f)
        {
            if (View == null) return 0f;
            var to = (f.FacePoint - View.transform.position).normalized;
            return Mathf.Clamp01((Vector3.Dot(to, View.transform.forward) - 0.5f) / 0.5f);
        }

        /// <summary>A puff of pollen bursting out of the flower's face.</summary>
        void Puff(Flower f, int count, float speed)
        {
            float radius = f.Head * Mathf.Max(f.Grown, 0.3f);
            for (int k = 0; k < count; k++)
            {
                var direction = Random.onUnitSphere;
                pollen.Emit(new ParticleSystem.EmitParams
                {
                    position = f.FacePoint + direction * (radius * 0.4f),
                    velocity = direction * (speed * Random.Range(0.5f, 1f)) + Vector3.up * Random.Range(0.02f, 0.08f),
                    startColor = k % 3 == 0 ? PollenColor : Color.Lerp(f.Petal, Color.white, 0.35f),
                    startSize = Random.Range(0.006f, 0.012f) * Mathf.Max(f.Grown, 0.6f),
                    startLifetime = Random.Range(0.9f, 1.6f)
                }, 1);
            }
        }

        /// <summary>One mote of pollen drifting up from the flower's face to the palm over it.</summary>
        void Drift(Flower f)
        {
            var from = f.FacePoint + Random.insideUnitSphere * (f.Head * f.Grown * 0.4f);
            float life = Random.Range(0.8f, 1.2f);
            pollen.Emit(new ParticleSystem.EmitParams
            {
                position = from,
                velocity = (f.Palm - from) * (0.9f / life),
                startColor = Random.value < 0.5f ? PollenColor : Color.Lerp(f.Petal, Color.white, 0.4f),
                startSize = Random.Range(0.005f, 0.01f),
                startLifetime = life
            }, 1);
        }

        /// <summary>A soft halo of light behind the heads of the flowers glowing most, each in its own colour.</summary>
        void Glow(Camera view)
        {
            for (int h = 0; h < HaloCount; h++) haloOwners[h] = -1;
            for (int i = 0; i < flowers.Length; i++)
            {
                float g = glows[i];
                if (g < 0.05f) continue;
                int slot = HaloCount;
                while (slot > 0 && (haloOwners[slot - 1] < 0 || glows[haloOwners[slot - 1]] < g)) slot--;
                if (slot >= HaloCount) continue;
                for (int h = HaloCount - 1; h > slot; h--) haloOwners[h] = haloOwners[h - 1];
                haloOwners[slot] = i;
            }
            var from = view.transform.position;
            for (int h = 0; h < HaloCount; h++)
            {
                var halo = halos[h];
                int i = haloOwners[h];
                bool on = i >= 0;
                if (halo.gameObject.activeSelf != on) halo.gameObject.SetActive(on);
                if (!on) continue;
                var f = flowers[i];
                float g = glows[i];
                float radius = f.Head * f.Grown * poses[i].w;
                var away = (f.FacePoint - from).normalized;
                halo.transform.position = f.FacePoint + away * (radius * 0.6f);
                halo.transform.localScale = Vector3.one * (radius * (2.8f + 1.6f * g));
                Effects.Face(halo.transform, view);
                Color.RGBToHSV(f.Petal, out float hue, out float saturation, out _);
                haloBlock.Clear();
                haloBlock.SetColor(ColorId, Color.HSVToRGB(hue, Mathf.Min(1f, saturation * 1.2f + 0.1f), 1f));
                haloBlock.SetFloat(IntensityId, g * 0.85f);
                haloBlock.SetFloat(CoreId, 0f);
                halo.SetPropertyBlock(haloBlock);
            }
        }

        /// <summary>The sung words float up, swelling to full size as they leave their flower and rocking gently.</summary>
        void Sing(Camera view, float dt)
        {
            if (SungWords == null) return;
            var words = SungWords.Words;
            var right = view.transform.right;
            var from = view.transform.position;
            for (int i = 0; i < words.Count; i++)
            {
                var w = words[i];
                if (!w.Active) continue;
                float drift = Mathf.Cos(w.Age * 2.2f + w.Seed * 6.3f) * 0.05f;
                w.Position += (w.Velocity + right * drift) * dt;
                w.Velocity *= Mathf.Exp(-dt * 0.3f);
                w.Size = w.Spot.x * (0.5f + 0.5f * Smooth01(w.Age / 0.4f));
                w.Rotation = WordLayer.Facing(w.Position, from) * Quaternion.Euler(0f, 0f, Mathf.Sin(w.Age * 1.8f + w.Seed * 5f) * 7f);
                w.Halo = 0.1f + 0.3f * Mathf.Exp(-w.Age * 3f);
            }
        }

        // ---- Planting ----

        /// <summary>
        /// Once the room is scanned, the shortest flowers grow on the tables and furniture near you, small, a few to each
        /// top. Returns how many it planted.
        /// </summary>
        int PlantOnFurniture(Vector3 head, Vector3 forward, float floorY)
        {
            nearTops.Clear();
            var tops = Room.Tops;
            for (int i = 0; i < tops.Count; i++)
            {
                var top = tops[i];
                if (FlatDistance(top.Center, head) < 2.8f && top.Center.y > floorY + 0.25f && top.Center.y < head.y - 0.15f) nearTops.Add(top);
            }
            nearTops.Sort((x, y) => FlatDistance(x.Center, head).CompareTo(FlatDistance(y.Center, head)));
            var random = new System.Random(29 + Room.Version);
            int planted = 0;
            foreach (var top in nearTops)
            {
                int count = Mathf.Min(Mathf.Clamp(Mathf.RoundToInt(top.Size.x * top.Size.y / 0.12f), 1, 3), Mathf.Min(onFurniture, order.Length) - planted);
                bool wide = top.Size.x >= top.Size.y;
                var along = wide ? top.Right : top.Up;
                var across = wide ? top.Up : top.Right;
                float length = wide ? top.Size.x : top.Size.y;
                float depth = wide ? top.Size.y : top.Size.x;
                for (int c = 0; c < count; c++)
                {
                    float t = count == 1 ? 0f : (c / (count - 1f) - 0.5f) * 0.62f;
                    var spot = top.Clamp(top.Center + along * (t * length) + across * ((Next(random) - 0.5f) * 0.3f * depth), 0.08f);
                    var flat = Vector3.ProjectOnPlane(spot - head, Vector3.up);
                    Plant(flowers[order[planted]], spot, furnitureSize, head, Vector3.SignedAngle(forward, flat, Vector3.up), 0, random);
                    planted++;
                }
                if (planted >= onFurniture || planted >= order.Length) break;
            }
            return planted;
        }

        /// <summary>
        /// Plants the rest of the flowers in three loose rows round you, the shortest in front and the tallest at the
        /// back, the back row reaching furthest round. With a room scan they stay inside the walls and off the furniture.
        /// </summary>
        void PlantRows(Vector3 head, Vector3 forward, float floorY, int from)
        {
            int left = order.Length - from;
            if (left <= 0) return;
            var random = new System.Random(17 + Room.Version);
            int front = Mathf.RoundToInt(left * 0.3f), middle = Mathf.RoundToInt(left * 0.33f);
            int next = from;
            var foot = new Vector3(head.x, floorY, head.z);
            for (int row = 0; row < 3; row++)
            {
                int count = row == 0 ? front : row == 1 ? middle : left - front - middle;
                Mix(next, count, random);
                next += count;
                float distance = Mathf.Lerp(rowDistance.x, rowDistance.y, row * 0.5f);
                float reach = arc * (0.86f + 0.07f * row);
                for (int q = 0; q < count; q++)
                {
                    // Spread round you, closer together in front.
                    float x = Mathf.Clamp((q + 0.5f + (Next(random) - 0.5f) * 0.55f) / count * 2f - 1f, -1f, 1f);
                    float angle = reach * Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), 1.3f);
                    var direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                    // An oval: furthest in front, a little nearer at your sides.
                    float side = angle / 90f;
                    float d = (distance + (Next(random) - 0.5f) * 0.22f) * (1f - 0.15f * side * side);
                    if (!Fits(head, direction, floorY, ref d)) continue;
                    Plant(flowers[rowSlots[q]], foot + direction * d, 1f, head, angle, row, random);
                }
            }
        }

        /// <summary>
        /// Puts a row's flowers (<paramref name="count"/> of them from <paramref name="from"/> in planting order) in a
        /// mixed order in rowSlots: the best of a few shuffles, with the fewest neighbours of the same kind.
        /// </summary>
        void Mix(int from, int count, System.Random random)
        {
            int best = int.MaxValue;
            for (int attempt = 0; attempt < 16 && best > 0; attempt++)
            {
                for (int q = 0; q < count; q++) trySlots[q] = order[from + q];
                for (int q = count - 1; q > 0; q--)
                {
                    int r = random.Next(q + 1);
                    (trySlots[q], trySlots[r]) = (trySlots[r], trySlots[q]);
                }
                int same = 0;
                for (int q = 1; q < count; q++)
                    if (flowers[trySlots[q]].Kind == flowers[trySlots[q - 1]].Kind) same++;
                if (same >= best) continue;
                best = same;
                System.Array.Copy(trySlots, rowSlots, count);
            }
        }

        /// <summary>
        /// With a room scan, keeps a spot <paramref name="distance"/> along <paramref name="direction"/> inside the walls
        /// and clear of the furniture, bringing it nearer if it must. False when there is no room for a flower that way.
        /// </summary>
        static bool Fits(Vector3 head, Vector3 direction, float floorY, ref float distance)
        {
            if (!Room.Scanned) return true;
            var foot = new Vector3(head.x, floorY, head.z);
            if (Room.Hit(Room.Walls, foot + Vector3.up * 0.3f, direction, out _, out float wall)) distance = Mathf.Min(distance, wall - 0.22f);
            for (int tries = 0; tries < 3; tries++)
            {
                if (distance < 0.7f) return false;
                if (!UnderFurniture(foot + direction * distance, floorY)) return true;
                distance -= 0.3f;
            }
            return false;
        }

        /// <summary>Whether a spot on the floor is under (or right beside) a piece of furniture.</summary>
        static bool UnderFurniture(Vector3 spot, float floorY)
        {
            var tops = Room.Tops;
            for (int i = 0; i < tops.Count; i++)
                if (tops[i].Center.y > floorY + 0.12f && tops[i].Contains(spot, -0.12f)) return true;
            return false;
        }

        /// <summary>
        /// Plants a flower at a spot, facing you. The dance reaches it later the further round you it is; it sways
        /// against the row in front of it; and it starts to grow a moment after the flowers in front of you.
        /// </summary>
        void Plant(Flower f, Vector3 spot, float size, Vector3 head, float angle, int row, System.Random random)
        {
            float round = Mathf.Clamp01(Mathf.Abs(angle) / Mathf.Max(arc, 1f));
            f.Planted = true;
            f.Root = spot;
            f.Size = size;
            f.Yaw = YawTowards(spot, head);
            f.Delay = ripple * round + (Next(random) - 0.5f) * 0.06f;
            f.Side = row == 1 ? -1f : 1f;
            f.Sprout = Clock + 0.1f + 0.55f * round + 0.14f * row + Next(random) * 0.12f;
            f.Grown = 0f;
            f.BoldAge = 99f;
            f.Lean = f.LeanSpeed = f.Tip = f.TipSpeed = Vector2.zero;
            f.Stretch = f.Open = f.Warm = f.Over = 0f;
            f.Touched = false;
        }

        // ---- Where things are ----

        /// <summary>The middle of the flower's face, where the shader poses it (room space).</summary>
        static Vector3 FaceOf(Flower f, Vector4 pose, float size)
        {
            StemCurve(1f, new Vector2(pose.x, pose.y), f.Height * pose.z * size, out var tip, out var along);
            var turned = Vector3.up + (along - Vector3.up) * 1.3f;
            turned.y = Mathf.Max(turned.y, 0.1f);
            var centre = Quaternion.Euler(f.Face, 0f, 0f) * (Vector3.up * (f.Neck * pose.w * size));
            return f.Root + Yaw(tip + UpTo(turned.normalized, centre), f.Yaw);
        }

        /// <summary>The stem's centre line at s (0 root, 1 tip) and its direction there, as the shader's Stem.</summary>
        static void StemCurve(float s, Vector2 lean, float h, out Vector3 p, out Vector3 t)
        {
            float amount = lean.magnitude;
            if (amount > 0.85f) lean *= 0.85f / amount;
            var p1 = new Vector3(0f, 0.5f * h, 0f);
            var p2 = new Vector3(lean.x, Mathf.Sqrt(Mathf.Max(1f - lean.sqrMagnitude, 0.2f)), lean.y) * h;
            p = 2f * (1f - s) * s * p1 + s * s * p2;
            t = (2f * (1f - s) * p1 + 2f * s * (p2 - p1) + new Vector3(0f, 1e-4f, 0f)).normalized;
        }

        /// <summary>Turns x the way straight up turns into the unit direction t, as the shader's UpTo.</summary>
        static Vector3 UpTo(Vector3 t, Vector3 x)
        {
            var v = new Vector3(t.z, 0f, -t.x);
            var vx = Vector3.Cross(v, x);
            return x + vx + Vector3.Cross(v, vx) / (1f + t.y);
        }

        /// <summary>From a flower's own space (+Z towards you) to the room's directions.</summary>
        static Vector3 Yaw(Vector3 p, float yaw)
        {
            float s = Mathf.Sin(yaw), c = Mathf.Cos(yaw);
            return new Vector3(p.x * c + p.z * s, p.y, -p.x * s + p.z * c);
        }

        /// <summary>From the room's directions to a flower's own space.</summary>
        static Vector3 ToLocal(Vector3 v, float yaw)
        {
            float s = Mathf.Sin(yaw), c = Mathf.Cos(yaw);
            return new Vector3(v.x * c - v.z * s, v.y, v.x * s + v.z * c);
        }

        static float YawTowards(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z);

        static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static float Next(System.Random random) => (float)random.NextDouble();

        // ---- Building the garden ----

        /// <summary>Adds one flower to the garden mesh: stem, two leaves, a tuft of grass, its head and its shadow.</summary>
        static void BuildFlower(MeshBuilder m, int id, Flower f, System.Random random)
        {
            var style = Styles[(int)f.Kind];
            f.Height = Mathf.Lerp(style.Height.x, style.Height.y, Next(random));
            f.Face = style.Face + (Next(random) - 0.5f) * 10f;
            var green = Greens[random.Next(Greens.Length)];
            float height = f.Height;
            Stem(m, id, height, style.Stem, green);
            // Two leaves on opposite sides, at different heights, like arms. Cup flowers have long leaves from the ground.
            bool tall = f.Kind == Kind.Cup;
            for (int side = 0; side < 2; side++)
            {
                float angle = (side == 0 ? Mathf.PI : 0f) + (Next(random) - 0.5f) * 0.9f;
                float s = tall ? 0.05f + 0.06f * side : Mathf.Lerp(0.2f, 0.4f, Next(random)) + 0.06f * side;
                float length = tall ? height * Mathf.Lerp(0.42f, 0.5f, Next(random)) : Mathf.Clamp(height * Mathf.Lerp(0.26f, 0.34f, Next(random)), 0.1f, 0.26f);
                float width = length * (tall ? 0.24f : 0.36f);
                float rise = tall ? 68f : Mathf.Lerp(32f, 50f, Next(random));
                float droop = tall ? 38f : Mathf.Lerp(45f, 70f, Next(random));
                Leaf(m, id, height, style.Stem, s, angle, length, width, rise, droop, side, Color.Lerp(green, Color.white, 0.06f * side));
            }
            Grass(m, id, height, random, green);
            Head(m, id, f, style, random, green);
            Shadow(m, id, height, f.Head * 1.3f + 0.03f);
        }

        /// <summary>
        /// The flower's head, in head space: the stem's tip at the origin and the head's axis up, then turned towards
        /// you (+Z) by its face angle and made HeadSize times bigger. A green calyx cups it and the petals are hinged
        /// round its centre. Each petal has a closed and an open place; the shader blends between them.
        /// </summary>
        static void Head(MeshBuilder m, int id, Flower f, Style style, System.Random random, Color green)
        {
            var face = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(f.Face, 0f, 0f), Vector3.one * HeadSize);
            float height = f.Height;
            float turn = Next(random) * Mathf.PI * 2f;
            const float Tau = Mathf.PI * 2f;
            switch (f.Kind)
            {
                case Kind.Daisy:
                {
                    // Fourteen slender white petals, tinted at the tips, round a golden dome.
                    var tip = DaisyTips[random.Next(DaisyTips.Length)];
                    var white = new Color(0.98f, 0.97f, 0.94f);
                    const float neck = 0.012f, disc = 0.024f;
                    Calyx(m, id, height, face, style.Stem, disc * 0.8f, neck, green);
                    for (int k = 0; k < 14; k++)
                        Petal(m, id, height, face, turn + k * Tau / 14f, disc * 0.85f, neck + 0.004f, 0.072f, 0.02f,
                            80f, 102f, 0.2f, 12f, Shape.Slender, white, Color.Lerp(white, tip, 0.85f));
                    Disc(m, id, height, face, disc, 0.012f, neck + 0.002f, new Color(1f, 0.8f, 0.2f), new Color(1f, 0.62f, 0.12f));
                    f.Head = 0.095f * HeadSize;
                    f.Neck = (neck + 0.012f) * HeadSize;
                    f.Petal = tip;
                    break;
                }
                case Kind.Cup:
                {
                    // Three inner and three outer petals make a closed cup that opens into a six-pointed star.
                    var color = CupColors[random.Next(CupColors.Length)];
                    var deep = Color.Lerp(color, new Color(0.45f, 0.08f, 0.15f), 0.35f);
                    var light = Color.Lerp(color, Color.white, 0.18f);
                    const float neck = 0.014f;
                    Calyx(m, id, height, face, style.Stem, 0.016f, neck, green);
                    Disc(m, id, height, face, 0.013f, 0.012f, neck, new Color(1f, 0.85f, 0.3f), new Color(0.95f, 0.65f, 0.2f));
                    for (int k = 0; k < 3; k++)
                        Petal(m, id, height, face, turn + k * Tau / 3f, 0.008f, neck + 0.002f, 0.092f, 0.064f,
                            10f, 60f, 0.75f, -6f, Shape.Round, deep, light);
                    for (int k = 0; k < 3; k++)
                        Petal(m, id, height, face, turn + (k + 0.5f) * Tau / 3f, 0.011f, neck, 0.098f, 0.068f,
                            17f, 74f, 0.7f, -4f, Shape.Round, deep, color);
                    f.Head = 0.07f * HeadSize;
                    f.Neck = (neck + 0.045f) * HeadSize;
                    f.Petal = color;
                    break;
                }
                case Kind.Star:
                {
                    // Five pointed petals that flick back at the tips, with five small pale ones inside.
                    var color = StarColors[random.Next(StarColors.Length)];
                    var deep = Color.Lerp(color, new Color(0.2f, 0.1f, 0.45f), 0.3f);
                    var pale = Color.Lerp(color, Color.white, 0.65f);
                    const float neck = 0.012f, disc = 0.016f;
                    Calyx(m, id, height, face, style.Stem, disc * 0.9f, neck, green);
                    for (int k = 0; k < 5; k++)
                        Petal(m, id, height, face, turn + k * Tau / 5f, disc * 0.7f, neck + 0.002f, 0.086f, 0.06f,
                            56f, 96f, 0.32f, 22f, Shape.Pointed, deep, Color.Lerp(color, Color.white, 0.12f));
                    for (int k = 0; k < 5; k++)
                        Petal(m, id, height, face, turn + (k + 0.5f) * Tau / 5f, disc * 0.6f, neck + 0.005f, 0.034f, 0.024f,
                            42f, 72f, 0.3f, 10f, Shape.Round, pale, Color.white);
                    Disc(m, id, height, face, disc, 0.01f, neck + 0.004f, new Color(1f, 0.95f, 0.65f), new Color(1f, 0.82f, 0.35f));
                    f.Head = 0.1f * HeadSize;
                    f.Neck = (neck + 0.012f) * HeadSize;
                    f.Petal = color;
                    break;
                }
                case Kind.Puff:
                {
                    // A ball of small round petals, ring above ring round a core; open, it puffs up bigger.
                    var color = PuffColors[random.Next(PuffColors.Length)];
                    var deep = Color.Lerp(color, new Color(0.25f, 0.08f, 0.3f), 0.4f);
                    var light = Color.Lerp(color, Color.white, 0.22f);
                    const float neck = 0.012f, middle = 0.045f;
                    Calyx(m, id, height, face, style.Stem, 0.014f, neck, green);
                    Disc(m, id, height, face, 0.028f, 0.03f, middle, deep, deep);
                    Disc(m, id, height, face, 0.028f, -0.03f, middle, deep, deep);
                    int[] counts = { 4, 7, 9, 7, 4 };
                    float[] tilts = { 16f, 54f, 90f, 126f, 160f };
                    for (int r = 0; r < counts.Length; r++)
                        for (int k = 0; k < counts[r]; k++)
                            Petal(m, id, height, face, turn + (k + r * 0.5f) * Tau / counts[r], 0.012f, middle, 0.05f, 0.042f,
                                tilts[r], Mathf.Min(tilts[r] + 14f, 172f), 0.55f, 0f, Shape.Round, deep, light, 1.2f);
                    f.Head = 0.085f * HeadSize;
                    f.Neck = (middle) * HeadSize;
                    f.Petal = color;
                    break;
                }
                default:
                {
                    // A big brown centre in two rings of petals: long orange ones behind, rounder golden ones in front.
                    var front = SunColors[random.Next(SunColors.Length)];
                    var back = Color.Lerp(front, new Color(1f, 0.4f, 0.1f), 0.45f);
                    var heart = new Color(0.5f, 0.28f, 0.12f);
                    const float neck = 0.016f, disc = 0.056f;
                    Calyx(m, id, height, face, style.Stem, disc * 0.7f, neck, green);
                    for (int k = 0; k < 16; k++)
                        Petal(m, id, height, face, turn + k * Tau / 16f, disc * 0.9f, neck + 0.002f, 0.09f, 0.03f,
                            86f, 104f, 0.25f, 10f, Shape.Pointed, Color.Lerp(back, heart, 0.25f), back);
                    for (int k = 0; k < 16; k++)
                        Petal(m, id, height, face, turn + (k + 0.5f) * Tau / 16f, disc * 0.93f, neck + 0.005f, 0.082f, 0.032f,
                            76f, 98f, 0.25f, 12f, Shape.Round, Color.Lerp(front, back, 0.5f), front);
                    Disc(m, id, height, face, disc, 0.022f, neck + 0.004f, heart, new Color(0.32f, 0.16f, 0.07f));
                    f.Head = 0.15f * HeadSize;
                    f.Neck = (neck + 0.02f) * HeadSize;
                    f.Petal = front;
                    break;
                }
            }
        }

        /// <summary>The stem: a slender tube from the root up to the head, tapering as it goes.</summary>
        static void Stem(MeshBuilder m, int id, float height, float radius, Color green)
        {
            const int sides = 6, rings = 9;
            int start = m.Count;
            for (int r = 0; r < rings; r++)
            {
                float s = r / (float)(rings - 1);
                float ring = radius * Mathf.Lerp(1f, 0.65f, s);
                for (int k = 0; k <= sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    m.Add(n * ring + Vector3.up * (s * height), n, green, s, k / (float)sides, StemPart, id, Vector4.zero, new Vector4(s, 0f, 0f, height));
                }
            }
            m.Grid(start, rings, sides + 1);
        }

        /// <summary>
        /// A leaf growing out of the stem at <paramref name="s"/> (share of its height) towards <paramref name="angle"/>:
        /// it rises at <paramref name="rise"/> degrees, droops by <paramref name="droop"/> towards its tip and folds a
        /// little along its midrib. It lifts like an arm about its hinge; <paramref name="side"/> says which arm it is.
        /// </summary>
        static void Leaf(MeshBuilder m, int id, float height, float stem, float s, float angle, float length, float width,
            float rise, float droop, int side, Color green)
        {
            const int along = 6, across = 3;
            var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var sideways = Vector3.Cross(Vector3.up, outward);
            var hinge = Vector3.Cross(outward, Vector3.up).normalized;
            var data1 = new Vector4(hinge.x, hinge.y, hinge.z, side);
            var data2 = new Vector4(s, 0f, 0f, height);
            var p = Vector3.up * (s * height) + outward * (stem * 0.6f);
            int start = m.Count;
            for (int i = 0; i < along; i++)
            {
                float u = i / (float)(along - 1);
                if (i > 0)
                {
                    float mid = (i - 0.5f) / (along - 1);
                    p += Rising(outward, rise - droop * mid * mid) * (length / (along - 1));
                }
                float beta = (rise - droop * u * u) * Mathf.Deg2Rad;
                var upper = -outward * Mathf.Sin(beta) + Vector3.up * Mathf.Cos(beta);
                float half = width * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.7f)), 0.75f);
                for (int j = 0; j < across; j++)
                {
                    float v = j - 1f;
                    var q = p + sideways * (v * half) + upper * (Mathf.Abs(v) * half * 0.4f);
                    var n = (upper - sideways * (v * 0.4f)).normalized;
                    m.Add(q, n, green, u, v, LeafPart, id, data1, data2);
                }
            }
            m.Grid(start, along, across);
        }

        /// <summary>A small tuft of grass blades round the root.</summary>
        static void Grass(MeshBuilder m, int id, float height, System.Random random, Color green)
        {
            const int blades = 5, rows = 4;
            var data2 = new Vector4(0f, 0f, 0f, height);
            for (int b = 0; b < blades; b++)
            {
                float a = (b + Next(random) * 0.7f) * Mathf.PI * 2f / blades;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var sideways = Vector3.Cross(Vector3.up, outward);
                float tall = Mathf.Lerp(0.06f, 0.13f, Next(random));
                float lean = Mathf.Lerp(0.2f, 0.6f, Next(random));
                var foot = outward * Mathf.Lerp(0.01f, 0.035f, Next(random));
                int start = m.Count;
                for (int r = 0; r < rows; r++)
                {
                    float u = r / (float)(rows - 1);
                    var c = foot + outward * (lean * tall * u * u) + Vector3.up * (tall * u);
                    var along = Vector3.up + outward * (2f * lean * u);
                    var n = Vector3.Cross(sideways, along).normalized;
                    float half = 0.007f * (1f - u);
                    m.Add(c - sideways * half, n, green, u, -1f, GrassPart, id, Vector4.zero, data2);
                    m.Add(c + sideways * half, n, green, u, 1f, GrassPart, id, Vector4.zero, data2);
                }
                m.Grid(start, rows, 2);
            }
        }

        /// <summary>The flower's contact shadow: a quad the shader lays on the ground under it.</summary>
        static void Shadow(MeshBuilder m, int id, float height, float radius)
        {
            int start = m.Count;
            var data1 = new Vector4(radius, 0f, 0f, 0f);
            var data2 = new Vector4(0f, 0f, 0f, height);
            for (int z = -1; z <= 1; z += 2)
                for (int x = -1; x <= 1; x += 2)
                    m.Add(new Vector3(x, 0f, z), Vector3.up, Color.black, 0f, 0f, ShadowPart, id, data1, data2);
            m.Grid(start, 2, 2);
        }

        /// <summary>The green cup under the head, from the stem's tip up to the head's centre.</summary>
        static void Calyx(MeshBuilder m, int id, float height, Matrix4x4 face, float stem, float top, float neck, Color green)
        {
            const int sides = 8, rings = 4;
            int start = m.Count;
            var dark = Color.Lerp(green, Color.black, 0.15f);
            for (int r = 0; r < rings; r++)
            {
                float t = r / (float)(rings - 1);
                // It starts as wide as the stem's tip, whatever the head's size.
                float radius = Mathf.Lerp(stem * 0.65f / HeadSize, top, t * t * (3f - 2f * t));
                for (int k = 0; k <= sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var p = face.MultiplyPoint3x4(outward * radius + Vector3.up * (t * neck));
                    var n = face.MultiplyVector(outward - Vector3.up * 0.5f).normalized;
                    m.Add(p, n, Color.Lerp(dark, green, t), t, k / (float)sides, CalyxPart, id, Point(p), Normal(n, height));
                }
            }
            m.Grid(start, rings, sides + 1);
        }

        /// <summary>
        /// The flower's centre: a low dome of <paramref name="radius"/> rising <paramref name="dome"/> above
        /// <paramref name="y"/> (a negative dome faces down). Its seeds are drawn by the shader.
        /// </summary>
        static void Disc(MeshBuilder m, int id, float height, Matrix4x4 face, float radius, float dome, float y, Color middle, Color edge)
        {
            const int rings = 4, segments = 12;
            int start = m.Count;
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float theta = t * Mathf.PI * 0.5f;
                for (int k = 0; k <= segments; k++)
                {
                    float a = k * Mathf.PI * 2f / segments;
                    var flat = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Mathf.Sin(theta);
                    var p = face.MultiplyPoint3x4(new Vector3(flat.x * radius, y + Mathf.Cos(theta) * dome, flat.z * radius));
                    var n = face.MultiplyVector(new Vector3(flat.x / radius, Mathf.Cos(theta) / dome, flat.z / radius)).normalized;
                    m.Add(p, n, Color.Lerp(middle, edge, t * t), flat.x, flat.z, DiscPart, id, Point(p), Normal(n, height));
                }
            }
            m.Grid(start, rings + 1, segments + 1);
        }

        /// <summary>
        /// One petal, hinged on the head at <paramref name="azimuth"/>, <paramref name="hinge"/> out from the axis and
        /// <paramref name="y"/> up it. Closed, it leans <paramref name="tilt"/> degrees out from the axis; open,
        /// <paramref name="openTilt"/> degrees and <paramref name="openLength"/> times as long. It curls back by
        /// <paramref name="curl"/> degrees towards its tip, cups across its width and narrows by its shape.
        /// </summary>
        static void Petal(MeshBuilder m, int id, float height, Matrix4x4 face, float azimuth, float hinge, float y, float length,
            float width, float tilt, float openTilt, float cup, float curl, Shape shape, Color inner, Color outer, float openLength = 1f)
        {
            const int along = 6, across = 3;
            var radial = new Vector3(Mathf.Cos(azimuth), 0f, Mathf.Sin(azimuth));
            var side = new Vector3(-Mathf.Sin(azimuth), 0f, Mathf.Cos(azimuth));
            var closed = radial * hinge + Vector3.up * y;
            var open = closed;
            int start = m.Count;
            for (int i = 0; i < along; i++)
            {
                float u = i / (float)(along - 1);
                if (i > 0)
                {
                    float mid = (i - 0.5f) / (along - 1);
                    closed += Outward(radial, tilt + curl * mid * mid) * (length / (along - 1));
                    open += Outward(radial, openTilt + curl * 1.4f * mid * mid) * (length * openLength / (along - 1));
                }
                var closedUp = Inward(radial, tilt + curl * u * u);
                var openUp = Inward(radial, openTilt + curl * 1.4f * u * u);
                float half = width * 0.5f * Width(shape, u);
                var color = Color.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, u));
                for (int j = 0; j < across; j++)
                {
                    float v = j - 1f;
                    var spread = side * (v * half);
                    var closedPoint = closed + spread + closedUp * (cup * half * v * v);
                    var openPoint = open + spread * openLength + openUp * (cup * 0.6f * half * v * v);
                    var closedNormal = (closedUp - side * (2f * cup * v)).normalized;
                    var openNormal = (openUp - side * (1.2f * cup * v)).normalized;
                    m.Add(face.MultiplyPoint3x4(closedPoint), face.MultiplyVector(closedNormal).normalized, color, u, v, PetalPart, id,
                        Point(face.MultiplyPoint3x4(openPoint)), Normal(face.MultiplyVector(openNormal).normalized, height));
                }
            }
            m.Grid(start, along, across);
        }

        /// <summary>How wide a petal is along its length (0 at its hinge, 1 at its tip), as a share of its width.</summary>
        static float Width(Shape shape, float u)
        {
            switch (shape)
            {
                case Shape.Round:
                    return Mathf.Pow(Mathf.Sin(Mathf.PI * (0.1f + 0.85f * Mathf.Pow(u, 0.8f))), 0.5f);
                case Shape.Slender:
                    return Mathf.Pow(Mathf.Sin(Mathf.PI * (0.08f + 0.9f * u)), 0.35f);
                default:
                    return Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.6f)) * (1f - 0.2f * u);
            }
        }

        /// <summary>Along a petal leaning <paramref name="degrees"/> out from the head's axis.</summary>
        static Vector3 Outward(Vector3 radial, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return radial * Mathf.Sin(a) + Vector3.up * Mathf.Cos(a);
        }

        /// <summary>The inner face of a petal at that lean: towards the axis when it stands up, up when it lies flat.</summary>
        static Vector3 Inward(Vector3 radial, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return -radial * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
        }

        /// <summary>Along a leaf rising <paramref name="degrees"/> above level.</summary>
        static Vector3 Rising(Vector3 outward, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return outward * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
        }

        static Vector4 Point(Vector3 p) => new Vector4(p.x, p.y, p.z, 0f);

        static Vector4 Normal(Vector3 n, float height) => new Vector4(n.x, n.y, n.z, height);

        /// <summary>The garden mesh as it is built: each vertex's rest shape and the extra data the shader reads.</summary>
        sealed class MeshBuilder
        {
            readonly List<Vector3> positions = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Color> colors = new List<Color>();
            readonly List<Vector4> uv0 = new List<Vector4>();
            readonly List<Vector4> uv1 = new List<Vector4>();
            readonly List<Vector4> uv2 = new List<Vector4>();
            readonly List<int> triangles = new List<int>();

            public int Count => positions.Count;

            /// <summary>
            /// Adds a vertex: its rest place and normal, its colour (sRGB), where it lies on its part (u along, v across),
            /// its part and flower, and the part's own data (uv1, uv2: see FlowerDance.shader).
            /// </summary>
            public void Add(Vector3 position, Vector3 normal, Color color, float u, float v, float part, int flower, Vector4 data1, Vector4 data2)
            {
                positions.Add(position);
                normals.Add(normal);
                colors.Add(color.linear);
                uv0.Add(new Vector4(u, v, part, flower));
                uv1.Add(data1);
                uv2.Add(data2);
            }

            /// <summary>Joins vertices added row by row, <paramref name="columns"/> to a row, into triangles.</summary>
            public void Grid(int start, int rows, int columns)
            {
                for (int r = 0; r < rows - 1; r++)
                {
                    for (int c = 0; c < columns - 1; c++)
                    {
                        int a = start + r * columns + c;
                        triangles.Add(a);
                        triangles.Add(a + columns);
                        triangles.Add(a + 1);
                        triangles.Add(a + 1);
                        triangles.Add(a + columns);
                        triangles.Add(a + columns + 1);
                    }
                }
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                if (positions.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetUVs(0, uv0);
                mesh.SetUVs(1, uv1);
                mesh.SetUVs(2, uv2);
                mesh.SetTriangles(triangles, 0);
                return mesh;
            }
        }
    }
}
