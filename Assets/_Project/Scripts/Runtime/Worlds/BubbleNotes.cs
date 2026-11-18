using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Bubble notes world, an underwater moment in your room. Every melody note rises from below as a soap bubble,
    /// the moment it is sung, placed by pitch from left (low) to right (high); low notes make big bubbles, high notes
    /// small ones. They wobble with the bass as they float up. Pop one with a fingertip and you hear a soft pop and its
    /// note right at your finger, with a ring and a spray of droplets; brush one with your palm and it drifts away. Your
    /// skin has a soap-film sheen.
    /// All around you swim shoals of small fish, each shoal its own colour, circling you from arm's length to across
    /// the room and never coming near your face. They swim faster when the song is louder, the stripe along each fish
    /// glows with its part of the music, a wave of light runs through them on every beat and now and then a kick makes
    /// one dart ahead. Hold out a hand and some of them come and circle it; close it and they circle tighter and faster;
    /// sweep it quickly and they scatter. They weave round the bubbles and keep clear of a pointing fingertip, so
    /// popping works just as before.
    /// A trail of water winds round each hand and follows it through the air: at rest it coils round the palm, swept
    /// through the air it pours along behind the hand like a smooth ribbon, shedding drops where it whips round.
    /// </summary>
    public class BubbleNotes : World
    {
        public override string DisplayName => "Bubble notes";

        public override Color Color => new Color(1f, 0.6f, 0.85f);

        public override int MenuOrder => 80;

        public override int MenuIcon => 10;

        public override string Hint => "Pop the bubbles with a fingertip; hold out a hand and the fish gather";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.12f, 0.1f, 0.2f, 0.45f),
            Edge = new Color(0.85f, 0.82f, 1f),
            Glow = new Color(1f, 0.6f, 0.85f),
            Pattern = HandTheme.Effect.Film,
            PatternColor = new Color(0.8f, 0.9f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material bubbleMaterial;
        [SerializeField] Material ringMaterial;
        [Tooltip("The fish (Materials/BubbleNotesFish), drawn instanced.")]
        [SerializeField] Material bubbleNotesFishMaterial;
        [Tooltip("The water trails round the hands (Materials/BubbleNotesStream).")]
        [SerializeField] Material bubbleNotesStreamMaterial;

        [Header("Bubbles")]
        [SerializeField] float distance = 0.5f;
        [SerializeField] float width = 0.7f;
        [Tooltip("Where bubbles start and where they pop on their own, relative to the eyes (m).")]
        [SerializeField] Vector2 heightRange = new Vector2(-0.5f, 0.35f);
        [SerializeField] Vector2 riseSpeed = new Vector2(0.09f, 0.14f);
        [SerializeField] int maxBubbles = 36;

        [Header("Fish")]
        [SerializeField] int fishCount = 48;
        [Tooltip("Length of the smallest and the largest fish (m).")]
        [SerializeField] Vector2 fishLength = new Vector2(0.08f, 0.12f);
        [Tooltip("How far from you (m) the shoals circle, nearest and furthest. In a scanned room they stay inside the walls.")]
        [SerializeField] Vector2 swimReach = new Vector2(0.75f, 1.6f);
        [Tooltip("The heights the shoals swim at, relative to your eyes (m).")]
        [SerializeField] Vector2 swimHeight = new Vector2(-0.65f, 0.2f);
        [Tooltip("How fast the fish cruise (m/s) in a quiet part of the song; louder parts are faster.")]
        [SerializeField] float cruise = 0.15f;
        [Tooltip("The closest a fish comes to your eyes (m). Straight in front of your face they keep a little further off.")]
        [SerializeField] float headClearance = 0.4f;
        [Tooltip("How far from the palm (m) the fish circling a hand keep, nearest and furthest. A closed hand draws them in.")]
        [SerializeField] Vector2 circleReach = new Vector2(0.11f, 0.19f);
        [Tooltip("Share of the fish that come to a held-out hand.")]
        [SerializeField, Range(0f, 1f)] float gatherShare = 0.34f;
        [Tooltip("Share of the free fish that dart ahead on a beat.")]
        [SerializeField, Range(0f, 1f)] float startle = 0.12f;
        [Tooltip("How fast the beat's wave of light travels through the fish (m/s).")]
        [SerializeField] float waveSpeed = 1.1f;

        [Header("Water trails")]
        [Tooltip("Width of the water trail where it leaves the hand (m).")]
        [SerializeField] float streamWidth = 0.03f;
        [Tooltip("How far from the palm (m) the trail coils round a hand at rest, and round one sweeping fast.")]
        [SerializeField] Vector2 streamCoil = new Vector2(0.065f, 0.035f);

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int IconId = Shader.PropertyToID("_Icon");
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        static readonly int SelectedId = Shader.PropertyToID("_Selected");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int SwimId = Shader.PropertyToID("_Swim");
        static readonly int LengthId = Shader.PropertyToID("_Length");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        // The water's drops: clear blues and a bright glint.
        static readonly Color[] WaterColors = { new Color(0.3f, 0.7f, 1f), new Color(0.5f, 0.95f, 1f), new Color(0.88f, 0.98f, 1f) };
        // Saturated soap-film colours, one per pitch.
        static readonly Color[] PitchColors =
        {
            new Color(1f, 0.3f, 0.62f), new Color(1f, 0.6f, 0.25f), new Color(1f, 0.85f, 0.3f),
            new Color(0.35f, 1f, 0.75f), new Color(0.35f, 0.65f, 1f), new Color(0.72f, 0.45f, 1f)
        };
        // The fish sing in four voices, low to high (the low mids up to the air band), one colour each: coral, gold,
        // aqua and a deep blue.
        static readonly Color[] VoiceColors =
        {
            new Color(1f, 0.42f, 0.28f), new Color(1f, 0.76f, 0.22f), new Color(0.2f, 0.9f, 0.82f), new Color(0.36f, 0.5f, 1f)
        };
        const int Shoals = 8;
        // Directions round you in which the walls of a scanned room are looked up, for keeping the fish inside.
        const int WallDirections = 32;
        // Points along each water trail, and smoothing passes over the drawn curve.
        const int StreamPoints = 40;
        const int StreamSmoothing = 2;

        sealed class Bubble
        {
            public Renderer Renderer;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Radius;
            public int Midi;
            public float Age;
            public float Phase;
            public float Near;
            public bool Active;
        }

        sealed class Pop
        {
            public Renderer Ring;
            public Renderer Flash;
            public float Age = -1f;
            public float Size;
            public Color Color;
            public bool ByHand;
        }

        sealed class Fish
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public Vector3 Heading;
            /// <summary>Its place in its shoal: ahead, outwards and up (m).</summary>
            public Vector3 Offset;
            public int Shoal;
            public int Voice;
            public float Size;
            public float Phase;
            public float Speed;
            /// <summary>The swimming stroke's phase, and how wide it is.</summary>
            public float Stroke;
            public float Wag;
            /// <summary>A quick flick of the tail (darting, scattering), fading away.</summary>
            public float Flick;
            public float Bank;
            /// <summary>Whether it comes to a held-out hand (below gatherShare), its turn among those that do, and its place circling the hand.</summary>
            public float Eager;
            public int Rank;
            public float Around;
            public float Ring;
            public float Lift;
            /// <summary>0 out in the room, 1 circling a hand.</summary>
            public float Gathered;
            public float Seed;
        }

        readonly List<Bubble> bubbles = new List<Bubble>();
        readonly List<Pop> pops = new List<Pop>();
        readonly float[] lastBands = new float[MusicBus.BandCount];
        readonly float[] cooldown = new float[MusicBus.BandCount];
        MaterialPropertyBlock block;
        AudioPool voices, popVoices;
        ParticleSystem droplets;
        Vector3 center, right, forward, anchor;
        float eyeY, songTime, lookAway;
        bool turning;
        MusicFrame frame;

        Fish[] fish;
        Mesh fishMesh;
        Matrix4x4[] fishMatrices;
        Vector4[] fishTints, fishSwims;
        MaterialPropertyBlock fishBlock;
        ParticleSystem fishBubbles;
        readonly float[] shoalReach = new float[Shoals];
        readonly float[] shoalHeight = new float[Shoals];
        readonly float[] shoalAngle = new float[Shoals];
        readonly float[] shoalTurn = new float[Shoals];
        readonly float[] shoalSway = new float[Shoals];
        readonly float[] wallReach = new float[WallDirections];
        Vector3 fishCentre, waveOrigin;
        float fishFloor, fishCeiling, surge, menuRoom, waveTime = -10f;
        bool fishPlaced;
        // The hands, for the fish: whether each is held out to them, for how long it has been (or since it stopped),
        // closed, pointing, and where its palm and pointing fingertip are.
        readonly bool[] calling = new bool[2];
        readonly bool[] closed = new bool[2];
        readonly bool[] pointing = new bool[2];
        readonly float[] heldOut = new float[2];
        readonly float[] lapsed = new float[2];
        readonly float[] handSpeed = new float[2];
        readonly bool[] tracked = new bool[2];
        readonly bool[] gesture = new bool[2];
        readonly Vector3[] palms = new Vector3[2];
        readonly Vector3[] palmVelocity = new Vector3[2];
        readonly Vector3[] pointer = new Vector3[2];
        readonly Vector3[] handAxis = new Vector3[2];
        readonly Vector3[] handNormal = new Vector3[2];
        // What the fish swim round on each hand: the five fingertips, the palm, the wrist and the middle of three fingers.
        const int HandObstacles = 10;
        readonly Vector3[] obstacles = new Vector3[HandObstacles * 2];
        readonly float[] obstacleRadii = new float[HandObstacles * 2];

        // The water trails: for each hand a chain of points, each chasing the one before, and where they were last frame.
        LineStrip[] streams;
        Vector3[][] chains, chainsBefore;
        Vector3[] streamEven, streamPath;
        float[] streamWidths, streamRun;
        Color[] streamColors;
        readonly float[] streamLevel = new float[2];
        readonly float[] streamPhase = new float[2];
        readonly bool[] streamFresh = { true, true };
        // The hand as the trail follows it: smoothed, steady when the hand is still and close behind it when it moves fast.
        readonly Vector3[] streamPalm = new Vector3[2];
        readonly Vector3[] streamAxis = new Vector3[2];
        readonly Vector3[] streamNormal = new Vector3[2];

        protected override int SungWordCount => 6;

        protected override float SungWordHeight => 0.03f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            for (int i = 0; i < maxBubbles; i++)
            {
                var b = new Bubble { Renderer = Effects.MakeQuad($"Bubble {i}", transform, bubbleMaterial) };
                b.Renderer.gameObject.SetActive(false);
                bubbles.Add(b);
            }
            for (int i = 0; i < 10; i++)
            {
                var p = new Pop { Ring = Effects.MakeQuad($"Pop {i}", transform, ringMaterial), Flash = Effects.MakeQuad($"Pop Flash {i}", transform, glowMaterial) };
                p.Ring.gameObject.SetActive(false);
                p.Flash.gameObject.SetActive(false);
                pops.Add(p);
            }
            voices = new AudioPool(transform, 6);
            // The pops get their own voices, so a quick run of pops never cuts off a note still ringing.
            popVoices = new AudioPool(transform, 2);
            droplets = Effects.MakeSparkSystem("Droplets", transform, glowMaterial);
            var main = droplets.main;
            main.gravityModifier = 0.25f;
            BuildFish();
            BuildStreams();
        }

        void BuildStreams()
        {
            streams = new LineStrip[2];
            chains = new Vector3[2][];
            chainsBefore = new Vector3[2][];
            for (int s = 0; s < 2; s++)
            {
                streams[s] = new LineStrip(s == 0 ? "Water Trail L" : "Water Trail R", transform, bubbleNotesStreamMaterial, StreamPoints);
                streams[s].SetVisible(false);
                chains[s] = new Vector3[StreamPoints];
                chainsBefore[s] = new Vector3[StreamPoints];
            }
            streamEven = new Vector3[StreamPoints];
            streamPath = new Vector3[StreamPoints];
            streamWidths = new float[StreamPoints];
            streamColors = new Color[StreamPoints];
            streamRun = new float[StreamPoints];
        }

        /// <summary>The fish: who swims in which shoal, their sizes, and the one mesh and draw call they share.</summary>
        void BuildFish()
        {
            fishMesh = BuildFishMesh();
            fish = new Fish[fishCount];
            fishMatrices = new Matrix4x4[fishCount];
            fishTints = new Vector4[fishCount];
            fishSwims = new Vector4[fishCount];
            fishBlock = new MaterialPropertyBlock();
            // All the fish are one instanced draw, which the material must allow.
            if (bubbleNotesFishMaterial != null && !bubbleNotesFishMaterial.enableInstancing) bubbleNotesFishMaterial.enableInstancing = true;

            var random = new System.Random(21);
            float Next() => (float)random.NextDouble();
            // Most shoals go one way round and two the other, each group spread evenly round you, at their own
            // distances, heights and paces.
            int with = 0, against = 0;
            for (int s = 0; s < Shoals; s++)
            {
                float share = (s + 0.5f) / Shoals;
                shoalReach[s] = Mathf.Lerp(swimReach.x, swimReach.y, Mathf.Repeat(share * 3f, 1f) * 0.8f + Next() * 0.2f);
                shoalHeight[s] = Mathf.Lerp(swimHeight.x, swimHeight.y, Mathf.Repeat(share * 5f + 0.3f, 1f));
                bool back = s == 2 || s == 5;
                shoalTurn[s] = back ? -1f : 1f;
                shoalAngle[s] = back ? 0.6f + against++ * Mathf.PI : with++ * Mathf.PI * 2f / (Shoals - 2);
                shoalSway[s] = Next() * 6.28f;
            }
            int eager = 0;
            for (int i = 0; i < fishCount; i++)
            {
                int shoal = i % Shoals;
                var f = new Fish
                {
                    Shoal = shoal,
                    Voice = shoal % VoiceColors.Length,
                    Size = Mathf.Lerp(fishLength.x, fishLength.y, Next()),
                    Offset = new Vector3((Next() - 0.5f) * 0.8f, (Next() - 0.5f) * 0.26f, (Next() - 0.5f) * 0.2f),
                    Phase = Next() * 20f,
                    Speed = 0.7f + Next() * 0.6f,
                    Stroke = Next() * 12f,
                    Wag = 0.06f,
                    Eager = Next(),
                    Around = Next() * Mathf.PI * 2f,
                    Ring = Next(),
                    Lift = Next() * 2f - 1f,
                    Seed = Next()
                };
                if (f.Eager < gatherShare) f.Rank = eager++;
                fish[i] = f;
            }
            fishBubbles = Effects.MakeSparkSystem("Fish Bubbles", transform, glowMaterial);
            var main = fishBubbles.main;
            main.maxParticles = 120;
            main.gravityModifier = -0.01f;
        }

        protected override void OnEnter()
        {
            fishPlaced = false;
            turning = false;
            lookAway = 0f;
            surge = 0f;
            menuRoom = 0f;
            waveTime = -10f;
            for (int s = 0; s < 2; s++)
            {
                calling[s] = false;
                tracked[s] = false;
                heldOut[s] = lapsed[s] = 0f;
                streamLevel[s] = 0f;
                streamFresh[s] = true;
            }
        }

        protected override void OnExit()
        {
            foreach (var b in bubbles) Release(b);
            foreach (var p in pops)
            {
                p.Age = -1f;
                p.Ring.gameObject.SetActive(false);
                p.Flash.gameObject.SetActive(false);
            }
            droplets.Clear();
            if (fishBubbles != null) fishBubbles.Clear();
            fishPlaced = false;
            for (int s = 0; s < 2; s++)
            {
                streams[s].SetVisible(false);
                streamLevel[s] = 0f;
                streamFresh[s] = true;
            }
        }

        /// <summary>
        /// The bubbles rise in a band in front of where you look, starting below your eyes. The fish circle you, inside
        /// the walls of a scanned room, above the floor; when the world starts they are already swimming round you.
        /// </summary>
        protected override void OnPlace(Camera view)
        {
            forward = Effects.FlatForward(view);
            right = Vector3.Cross(Vector3.up, forward).normalized;
            anchor = view.transform.position;
            center = anchor + forward * distance;
            eyeY = anchor.y;
            lookAway = 0f;
            turning = false;

            fishCentre = anchor;
            float floor = Effects.FloorY(view);
            fishFloor = floor + 0.25f;
            fishCeiling = Room.Ceiling(eyeY + 1.2f) - 0.2f;
            for (int k = 0; k < WallDirections; k++)
            {
                float a = k * Mathf.PI * 2f / WallDirections;
                var ray = new Ray(new Vector3(anchor.x, eyeY - 0.3f, anchor.z), new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                wallReach[k] = Room.Scanned && Room.RaycastWalls(ray, 6f, out float hit, out _) ? Mathf.Max(headClearance + 0.3f, hit - 0.25f) : 99f;
            }
            if (fish == null || fishPlaced) return;
            fishPlaced = true;
            for (int i = 0; i < fish.Length; i++)
            {
                var f = fish[i];
                f.Position = Home(f, Clock, out var along);
                f.Velocity = along * cruise;
                f.Heading = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.forward;
                f.Gathered = 0f;
                f.Flick = 0f;
                f.Bank = 0f;
            }
        }

        /// <summary>
        /// A bubble for each melody note, released the moment the note is sung. World follows the song for this, so a
        /// song that loops, restarts or changes keeps sending bubbles.
        /// </summary>
        protected override void OnNote(NoteTrack.Note note) => Spawn(note.Midi, Mathf.Sin(note.Time * 78.233f));

        /// <summary>
        /// On each beat the shoals surge ahead, a wave of light runs out through the fish and the water trails shake off
        /// a few drops.
        /// </summary>
        protected override void OnBeat(MusicFrame music)
        {
            surge = 1f;
            waveTime = Clock;
            int side = calling[0] ? 0 : calling[1] ? 1 : -1;
            // From a hand the fish are circling, otherwise from around you.
            waveOrigin = side >= 0 ? palms[side] : fishCentre + Vector3.down * 0.3f;
            for (int s = 0; s < 2; s++)
            {
                if (streamLevel[s] < 0.3f) continue;
                for (int i = 0; i < 2; i++)
                {
                    var at = chains[s][Random.Range(StreamPoints / 3, StreamPoints)];
                    Drop(at, Random.insideUnitSphere * 0.2f + Vector3.up * 0.12f, Random.Range(0.6f, 0.9f));
                }
            }
        }

        /// <summary>
        /// Each sung word rides up inside the newest bubble, sized to fit it. Pop the bubble and the word goes with it.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            int newest = NewestBubble();
            // A word that no melody note brought a bubble for gets one of its own, so no sung word is lost.
            if (newest < 0)
            {
                Spawn(64 + (word.GetHashCode() & 0x7fffffff) % 12, Mathf.Sin(Clock * 78.233f));
                newest = NewestBubble();
                if (newest < 0) return;
            }
            var b = bubbles[newest];
            var w = SungWords.Add(word, Color.Lerp(PitchColors[Mathf.Abs(b.Midi) % PitchColors.Length], Color.white, 0.55f), 6f);
            w.Slot = newest;
            w.Position = b.Position;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            songTime = music.SongTime;
            frame = music;
            // Songs with a melody send their bubbles through OnNote; without one (or between its notes) the bands do.
            if (music.Upcoming == null || music.Upcoming.Count == 0) SpawnFromAnalysis(music, dt);
            FollowGaze(view, dt);
            Float(rig, music, view, dt);
            UpdatePops(view, dt);
            bool menu = MenuOpen;
            ReadHands(rig, view.transform.position, menu, dt);
            Swim(music, view, menu, dt);
            DrawFish();
            for (int s = 0; s < 2; s++) Stream(s, music, view, menu, dt);

            // The sung words ride in their bubbles.
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                var b = bubbles[w.Slot];
                if (!b.Active)
                {
                    // Popped: the word lets go and fades.
                    w.Life = Mathf.Min(w.Life, w.Age + 0.3f);
                    w.Size *= 1f + dt * 2f;
                    continue;
                }
                w.Size = Mathf.Clamp(b.Radius * 1.5f / Mathf.Max(w.Width * 0.03f, 1e-3f), 0.5f, 1.4f);
                w.Position = b.Position + (eye - b.Position).normalized * 0.005f + Vector3.down * (SungWords.Height(w) * 0.35f);
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.15f;
            }
        }

        /// <summary>
        /// Once you have looked well away from the bubbles for a moment (turned to watch the fish, say), new bubbles
        /// start rising in front of where you look now; the ones already rising carry on where they are.
        /// </summary>
        void FollowGaze(Camera view, float dt)
        {
            var look = Effects.FlatForward(view);
            float away = Vector3.Angle(look, forward);
            lookAway = away > 40f ? lookAway + dt : 0f;
            if (lookAway > 1.5f) turning = true;
            if (!turning) return;
            forward = Vector3.RotateTowards(forward, look, 90f * Mathf.Deg2Rad * dt, 0f);
            right = Vector3.Cross(Vector3.up, forward).normalized;
            center = anchor + forward * distance;
            if (Vector3.Angle(look, forward) < 2f) turning = false;
        }

        void SpawnFromAnalysis(MusicFrame music, float dt)
        {
            for (int b = 2; b < MusicBus.BandCount; b++)
            {
                cooldown[b] -= dt;
                float level = music.Band(b);
                if (level - lastBands[b] > 0.16f && level > 0.45f && cooldown[b] <= 0f)
                {
                    Spawn(60 + b * 3 + Random.Range(0, 4), Random.Range(-1f, 1f));
                    cooldown[b] = 0.25f;
                }
                lastBands[b] = level;
            }
        }

        /// <summary>The bubble that appeared most recently, or -1 when none is up.</summary>
        int NewestBubble()
        {
            int newest = -1;
            for (int i = 0; i < bubbles.Count; i++)
                if (bubbles[i].Active && (newest < 0 || bubbles[i].Age < bubbles[newest].Age)) newest = i;
            return newest;
        }

        void Spawn(int midi, float depthJitter)
        {
            Bubble bubble = null;
            foreach (var b in bubbles) if (!b.Active) { bubble = b; break; }
            if (bubble == null) return;
            float x = Mathf.Clamp01((midi - 60f) / 20f) * 2f - 1f;
            bubble.Active = true;
            bubble.Midi = midi;
            bubble.Radius = Mathf.Lerp(0.065f, 0.034f, Mathf.Clamp01((midi - 60f) / 18f)) * Random.Range(0.9f, 1.1f);
            bubble.Position = center + right * (x * width * 0.5f + Random.Range(-0.03f, 0.03f)) + forward * (depthJitter * 0.12f);
            bubble.Position.y = eyeY + heightRange.x;
            bubble.Velocity = Vector3.up * Random.Range(riseSpeed.x, riseSpeed.y);
            bubble.Age = 0f;
            bubble.Phase = Random.value * 10f;
            bubble.Near = 0f;
            bubble.Renderer.gameObject.SetActive(true);
        }

        void Float(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            float t = Now;
            float bass = music.Band(1);
            foreach (var b in bubbles)
            {
                if (!b.Active) continue;
                b.Age += dt;
                // Rise with a gentle sideways sway, easing back to the rising speed after a push.
                float rise = Mathf.Lerp(riseSpeed.x, riseSpeed.y, Mathf.Repeat(b.Phase, 1f));
                var sway = right * (Mathf.Sin(t * 1.3f + b.Phase) * 0.025f) + forward * (Mathf.Cos(t * 0.9f + b.Phase * 1.7f) * 0.012f);
                b.Velocity = Vector3.Lerp(b.Velocity, Vector3.up * rise + sway, dt * 1.2f);

                if (rig != null && TouchHands(rig, b)) continue;

                // Bubbles nudge each other apart instead of overlapping.
                foreach (var o in bubbles)
                {
                    if (o == b || !o.Active) continue;
                    var d = b.Position - o.Position;
                    float gap = b.Radius + o.Radius;
                    if (d.sqrMagnitude < gap * gap && d.sqrMagnitude > 1e-8f) b.Velocity += d.normalized * (gap - d.magnitude) * 4f * dt * 30f;
                }
                b.Position += b.Velocity * dt;

                if (b.Position.y > eyeY + heightRange.y)
                {
                    // Too high: it pops quietly by itself.
                    Burst(b, false);
                    continue;
                }

                float appear = Mathf.Clamp01(b.Age / 0.35f);
                float wobble = Mathf.Sin(t * 7f + b.Phase) * (0.03f + bass * 0.07f);
                float r = b.Radius * (0.6f + 0.4f * appear) * (1f + music.Kick * 0.06f);
                b.Renderer.transform.position = b.Position;
                Effects.Face(b.Renderer.transform, view);
                b.Renderer.transform.localScale = new Vector3(r * 2f * (1f + wobble), r * 2f * (1f - wobble), 1f);
                block.Clear();
                block.SetColor(ColorId, PitchColors[Mathf.Abs(b.Midi) % PitchColors.Length]);
                block.SetFloat(IconId, -1f);
                // Brighter than a menu bubble: a lit rim and a stronger sheen, more so as a finger comes close.
                block.SetFloat(HoverId, 0.6f + b.Near * 0.4f);
                block.SetFloat(SelectedId, 0.55f);
                block.SetFloat(AlphaId, appear);
                b.Renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>A fingertip pops the bubble; a palm pushes it away. Returns true if it popped.</summary>
        bool TouchHands(HandRig rig, Bubble b)
        {
            float nearest = 1f;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                // While the menu is open the fingers are choosing there: they neither pop nor push the bubbles.
                if (!hand.IsTracked || MenuOpen) continue;
                for (int k = 0; k < 5; k++)
                {
                    var tip = hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k];
                    float d = Vector3.Distance(tip, b.Position);
                    if (d < b.Radius * 0.95f && b.Age > 0.25f)
                    {
                        // A soft pop at once, under the bubble's note (which may wait a moment to land on the beat).
                        // Both sound from where the bubble was.
                        popVoices.Play(Synth.Pop(), b.Position, 0.25f);
                        Musical.Play(voices, frame, b.Midi, b.Position, 0.4f);
                        // The popping fingertip lights up; the pop is the sound.
                        Feedback(GestureKind.Touch, side == 0, tip, 1f, false, GestureFeedback.Of((HandRig.Finger)k));
                        Burst(b, true);
                        Acted("pop");
                        return true;
                    }
                    nearest = Mathf.Min(nearest, (d - b.Radius) / 0.06f);
                }
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                var away = b.Position - palm;
                float reach = b.Radius + 0.05f;
                if (away.sqrMagnitude < reach * reach && away.sqrMagnitude > 1e-8f)
                {
                    b.Velocity += away.normalized * 0.25f * (1f - away.magnitude / reach);
                    Acted("push bubble");
                }
            }
            b.Near = Mathf.Clamp01(1f - nearest);
            return false;
        }

        void Burst(Bubble b, bool byHand)
        {
            var color = PitchColors[Mathf.Abs(b.Midi) % PitchColors.Length];
            Pop pop = null;
            float oldest = -1f;
            foreach (var p in pops)
            {
                if (p.Age < 0f) { pop = p; break; }
                if (p.Age > oldest) { oldest = p.Age; pop = p; }
            }
            pop.Age = 0f;
            pop.Size = b.Radius * 2f;
            pop.Color = color;
            pop.Ring.transform.position = b.Position;
            pop.Ring.gameObject.SetActive(true);
            pop.Flash.transform.position = b.Position;
            pop.Flash.gameObject.SetActive(byHand);
            pop.ByHand = byHand;

            int count = byHand ? 22 : 6;
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                droplets.Emit(new ParticleSystem.EmitParams
                {
                    position = b.Position + dir * b.Radius,
                    velocity = dir * Random.Range(0.2f, byHand ? 0.55f : 0.2f),
                    startColor = Color.Lerp(color, Color.white, Random.Range(0.2f, 0.6f)),
                    startSize = Random.Range(0.004f, byHand ? 0.011f : 0.008f),
                    startLifetime = Random.Range(0.35f, 0.7f)
                }, 1);
            }
            Release(b);
        }

        void UpdatePops(Camera view, float dt)
        {
            foreach (var p in pops)
            {
                if (p.Age < 0f) continue;
                p.Age += dt;
                float u = p.Age / 0.5f;
                if (u >= 1f)
                {
                    p.Age = -1f;
                    p.Ring.gameObject.SetActive(false);
                    p.Flash.gameObject.SetActive(false);
                    continue;
                }
                // A ring that bursts outwards, and (for a pop by hand) a quick flash of light.
                float grow = 1f - (1f - u) * (1f - u);
                p.Ring.transform.localScale = Vector3.one * p.Size * (1f + grow * (p.ByHand ? 2.6f : 1.4f));
                Effects.Face(p.Ring.transform, view);
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(Color.white, p.Color, u));
                block.SetFloat(RadiusId, 0.8f);
                block.SetFloat(ThicknessId, 0.07f * (1f - u) + 0.012f);
                block.SetFloat(AlphaId, (1f - u) * (1f - u) * (p.ByHand ? 1.3f : 0.8f));
                p.Ring.SetPropertyBlock(block);

                if (!p.ByHand) continue;
                float flash = Mathf.Exp(-p.Age * 14f);
                p.Flash.transform.localScale = Vector3.one * p.Size * (1.6f + grow * 1.5f);
                Effects.Face(p.Flash.transform, view);
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(Color.white, p.Color, 0.4f));
                block.SetFloat(IntensityId, flash * 1.4f);
                p.Flash.SetPropertyBlock(block);
            }
        }

        static void Release(Bubble b)
        {
            b.Active = false;
            b.Renderer.gameObject.SetActive(false);
        }

        // ---- The fish ----

        /// <summary>
        /// Where a free fish is heading: its place in its shoal, which circles you slowly at its own distance and height,
        /// breathing in and out a little. <paramref name="along"/> is the way the shoal swims there.
        /// </summary>
        Vector3 Home(Fish f, float t, out Vector3 along)
        {
            float reach = shoalReach[f.Shoal] + f.Offset.y + Mathf.Sin(t * 0.21f + f.Phase) * 0.08f;
            float angle = shoalAngle[f.Shoal] + f.Offset.x / Mathf.Max(reach, 0.3f) * shoalTurn[f.Shoal];
            // Inside the walls of a scanned room.
            float k = Mathf.Repeat(angle / (Mathf.PI * 2f), 1f) * WallDirections;
            int k0 = (int)k % WallDirections;
            float wall = Mathf.Lerp(wallReach[k0], wallReach[(k0 + 1) % WallDirections], k - Mathf.Floor(k));
            reach = Mathf.Min(reach, wall);
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            along = new Vector3(-s, 0f, c) * shoalTurn[f.Shoal];
            float y = Mathf.Clamp(fishCentre.y + shoalHeight[f.Shoal] + f.Offset.z + Mathf.Sin(t * 0.33f + f.Phase * 1.7f) * 0.05f, fishFloor, fishCeiling);
            return new Vector3(fishCentre.x + c * reach, y, fishCentre.z + s * reach);
        }

        /// <summary>
        /// Where a fish circling a hand is heading: its own spot on a loose ring round the palm, the ring turning so the
        /// fish swim round and round it. A closed hand draws the ring in and turns it faster; a pointing hand widens it
        /// and moves it back from the fingertip, out of the way of popping.
        /// </summary>
        Vector3 Circle(Fish f, int side, Vector3 head, float t, out Vector3 along)
        {
            float spin = closed[side] ? 2.1f : 0.85f;
            float reach = Mathf.Lerp(circleReach.x, circleReach.y, f.Ring) * (closed[side] ? 0.72f : 1f) * (pointing[side] ? 1.3f : 1f);
            float angle = f.Around + t * spin;
            var centre = palms[side] + Vector3.up * (f.Lift * 0.045f - 0.01f);
            // A hand held near the face has its ring moved out beyond it, so the fish circle in front of and round the
            // hand, never between it and your eyes.
            var fromHead = palms[side] - head;
            float near = fromHead.magnitude;
            var level = new Vector3(fromHead.x, 0f, fromHead.z);
            if (level.sqrMagnitude > 1e-6f) centre += level.normalized * Mathf.Clamp(headClearance + 0.15f + reach - near, 0f, reach * 0.8f);
            if (pointing[side])
            {
                var back = Vector3.ProjectOnPlane(palms[side] - pointer[side], Vector3.up);
                if (back.sqrMagnitude > 1e-6f) centre += back.normalized * 0.04f;
            }
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            along = new Vector3(-s, 0f, c) * (reach * spin);
            return centre + new Vector3(c, 0f, s) * reach + Wobble(f, t, 0.01f);
        }

        /// <summary>Which hand a fish is circling (0 left, 1 right), or -1 when it swims free.</summary>
        int Answer(Fish f)
        {
            if (f.Eager >= gatherShare) return -1;
            if (calling[0] && calling[1]) return f.Rank % 2;
            return calling[0] ? 0 : calling[1] ? 1 : -1;
        }

        /// <summary>
        /// What the hands mean to the fish and the water this frame. A hand held out, open or closed, and fairly still for
        /// a moment calls some of the fish, and keeps calling a little while after it moves on. Never while the menu is
        /// open, nor in Meta's menu gesture: then the fish just swim. The fingertips and palms are also what every fish
        /// swims round. Each palm's speed is measured here from frame to frame, smoothed a little so one frame's
        /// tracking jitter neither scatters the fish nor whips the water round.
        /// </summary>
        void ReadHands(HandRig rig, Vector3 head, bool menu, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig != null ? rig.Get(side == 0) : null;
                bool seen = tracked[side];
                tracked[side] = hand != null && hand.IsTracked;
                for (int k = 0; k < HandObstacles; k++) obstacleRadii[side * HandObstacles + k] = 0f;
                bool was = calling[side];
                if (!tracked[side])
                {
                    calling[side] = false;
                    heldOut[side] = lapsed[side] = 0f;
                    handSpeed[side] = 0f;
                    palmVelocity[side] = Vector3.zero;
                    continue;
                }
                gesture[side] = hand.InSystemGesture;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                var measured = seen && dt > 1e-5f ? (palm - palms[side]) / dt : Vector3.zero;
                palmVelocity[side] = seen ? Vector3.Lerp(palmVelocity[side], measured, 1f - Mathf.Exp(-22f * dt)) : Vector3.zero;
                palms[side] = palm;
                handSpeed[side] = palmVelocity[side].magnitude;
                if (hand.PalmNormal.sqrMagnitude > 0.5f) handNormal[side] = hand.PalmNormal.normalized;
                // Which way the hand points, wrist to fingers (kept from before while the fingers curl in).
                var middle = hand.TipJoints[2] != null ? hand.TipJoints[2].position : hand.Tips[2];
                if ((middle - palm).sqrMagnitude > 0.0025f) handAxis[side] = (middle - palm).normalized;
                else if (handAxis[side].sqrMagnitude < 0.5f) handAxis[side] = Vector3.up;
                closed[side] = hand.Fist >= 0.45f;
                // Pointing: the index finger out, the other fingers curled in.
                float index = Vector3.Distance(hand.Tips[1], hand.Palm.position);
                float others = (Vector3.Distance(hand.Tips[2], hand.Palm.position) + Vector3.Distance(hand.Tips[3], hand.Palm.position)
                    + Vector3.Distance(hand.Tips[4], hand.Palm.position)) / 3f;
                pointing[side] = index > 0.08f && others < 0.065f;
                int o = side * HandObstacles;
                for (int k = 0; k < 5; k++)
                {
                    var tip = hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k];
                    obstacles[o + k] = tip;
                    // A pointing fingertip gets more room, so the fish never cross in front of a pop.
                    obstacleRadii[o + k] = k == 1 && pointing[side] ? 0.055f : 0.028f;
                    if (k == 1) pointer[side] = tip;
                }
                obstacles[o + 5] = palm;
                obstacleRadii[o + 5] = 0.055f;
                obstacles[o + 6] = palm - handAxis[side] * 0.07f;
                obstacleRadii[o + 6] = 0.04f;
                obstacles[o + 7] = (palm + obstacles[o + 1]) * 0.5f;
                obstacles[o + 8] = (palm + obstacles[o + 2]) * 0.5f;
                obstacles[o + 9] = (palm + obstacles[o + 4]) * 0.5f;
                obstacleRadii[o + 7] = obstacleRadii[o + 8] = obstacleRadii[o + 9] = 0.03f;

                var fromHead = palm - head;
                bool out_ = fromHead.magnitude > 0.25f && palm.y > head.y - 0.8f;
                bool wants = !menu && !hand.InSystemGesture && out_ && handSpeed[side] < 0.7f;
                heldOut[side] = wants ? heldOut[side] + dt : 0f;
                lapsed[side] = wants ? 0f : lapsed[side] + dt;
                if (menu || hand.InSystemGesture) calling[side] = false;
                else if (heldOut[side] > 0.3f) calling[side] = true;
                else if (lapsed[side] > 0.6f) calling[side] = false;
                if (calling[side] && !was)
                {
                    // The calling hand lights up with a soft bell as the fish turn to it.
                    Feedback(GestureKind.Done, side == 0, palm, 0.7f);
                    Acted("fish gather");
                }
            }
        }

        /// <summary>
        /// One step of the fish. Free fish follow their shoals round you on slow swirling currents; those a hand calls
        /// swim round it. They keep a little apart, swim round the bubbles and the fingers, dart ahead on some beats and
        /// scatter from a hand that sweeps through them. Near your head they slow down, and none comes closer to your eyes
        /// than headClearance (further straight in front, and well clear of the menu while it is open).
        /// </summary>
        void Swim(MusicFrame music, Camera view, bool menu, float dt)
        {
            if (fish == null || !fishPlaced) return;
            dt = Mathf.Min(dt, 0.05f);
            float t = Clock;
            var head = view.transform.position;
            var gaze = view.transform.forward;

            surge *= Mathf.Exp(-dt * 2.5f);
            menuRoom = Mathf.MoveTowards(menuRoom, menu ? 1f : 0f, dt * 1.5f);
            float loud = (music.Band(1) + music.Band(2) + music.Band(3)) / 3f;
            float pace = cruise * (0.75f + loud * 0.8f) * (1f + surge * 0.35f);
            // All the shoals go round at the same rate give or take a little that comes and goes (the outer ones
            // swimming faster to keep up), so they stay spread all round you instead of bunching up on one side.
            float meanReach = (swimReach.x + swimReach.y) * 0.5f;
            for (int s = 0; s < Shoals; s++)
                shoalAngle[s] += shoalTurn[s] * pace * (1f + 0.12f * Mathf.Sin(t * 0.1f + shoalSway[s])) / Mathf.Max(meanReach, 0.3f) * dt;
            float waveAge = t - waveTime;

            for (int i = 0; i < fish.Length; i++)
            {
                var f = fish[i];
                var p = f.Position;
                var v = f.Velocity;
                int side = Answer(f);
                var toHead = p - head;
                float headDistance = Mathf.Max(toHead.magnitude, 1e-4f);
                var fromHead = toHead / headDistance;

                Vector3 target, along;
                float follow;
                if (side < 0)
                {
                    target = Home(f, t, out along);
                    along *= pace * shoalReach[f.Shoal] / Mathf.Max(meanReach, 0.3f) * f.Speed;
                    follow = 1.6f;
                    // Slow swirling currents, so the shoals meander rather than run on rails.
                    along += Current(p, t) * 0.5f;
                    // Now and then a beat sends a fish darting ahead, never near your head.
                    if (music.Beat && headDistance > 0.9f && Random.value < startle)
                    {
                        var aside = Vector3.Cross(Vector3.up, f.Heading);
                        v += f.Heading * Random.Range(0.16f, 0.3f) + aside * Random.Range(-0.12f, 0.12f);
                        f.Flick = 1f;
                    }
                }
                else
                {
                    target = Circle(f, side, head, t, out along);
                    along += palmVelocity[side];
                    follow = 4f;
                }
                var want = along + (target - p) * (side < 0 ? 0.8f : 3.2f);
                v += (want - v) * (1f - Mathf.Exp(-follow * dt));
                f.Gathered = Mathf.MoveTowards(f.Gathered, side >= 0 && (target - p).sqrMagnitude < 0.01f ? 1f : 0f, dt * 2f);

                // A little space from the other fish.
                for (int j = 0; j < fish.Length; j++)
                {
                    if (j == i) continue;
                    var d = p - fish[j].Position;
                    float sq = d.sqrMagnitude;
                    if (sq > 0.0049f || sq < 1e-10f) continue;
                    float dist = Mathf.Sqrt(sq);
                    v += d / dist * ((0.07f - dist) / 0.07f * 1.6f * dt);
                }

                // Round the bubbles, giving one a finger is near more room.
                foreach (var b in bubbles)
                {
                    if (!b.Active) continue;
                    var d = p - b.Position;
                    float keep = b.Radius + f.Size * 0.4f + b.Near * 0.05f;
                    float sq = d.sqrMagnitude;
                    if (sq > (keep + 0.05f) * (keep + 0.05f) || sq < 1e-10f) continue;
                    float dist = Mathf.Sqrt(sq);
                    var away = d / dist;
                    v += away * (Mathf.Clamp01((keep + 0.05f - dist) / 0.05f) * 3f * dt);
                    if (dist < keep) p = b.Position + away * keep;
                }

                // Round the fingers and palms, and away from a hand sweeping through (not while the menu is open).
                for (int k = 0; k < obstacles.Length; k++)
                {
                    float radius = obstacleRadii[k];
                    if (radius <= 0f) continue;
                    var d = p - obstacles[k];
                    float reach = radius + 0.05f;
                    float sq = d.sqrMagnitude;
                    if (sq > reach * reach || sq < 1e-10f) continue;
                    float dist = Mathf.Sqrt(sq);
                    var away = d / dist;
                    v += away * ((reach - dist) / reach * 4f * dt);
                    if (dist < radius) p = obstacles[k] + away * radius;
                }
                if (!menu)
                    for (int s = 0; s < 2; s++)
                    {
                        if (handSpeed[s] < 0.7f) continue;
                        var d = p - palms[s];
                        float dist = d.magnitude;
                        if (dist > 0.32f || dist < 1e-4f) continue;
                        var away = d / dist;
                        // Never scattered towards your face.
                        float toFace = Vector3.Dot(away, -fromHead);
                        if (toFace > 0f) away = (away + fromHead * toFace).normalized;
                        v += away * (Mathf.Min(handSpeed[s] - 0.7f, 1f) * (1f - dist / 0.32f) * 5f * dt);
                        f.Flick = Mathf.Max(f.Flick, 0.7f);
                    }

                // Your head: a firm limit, a little further off straight in front of your face, and a soft one beyond
                // it that also clears the space in front of you while the menu is open.
                float front = Mathf.Max(0f, Vector3.Dot(fromHead, gaze));
                float clearance = headClearance + front * front * 0.1f;
                float room = clearance + front * front * 0.45f * menuRoom;
                float closeness = Mathf.Clamp01((room + 0.1f - headDistance) / 0.1f);
                if (closeness > 0f)
                {
                    float inward = Vector3.Dot(v, -fromHead);
                    if (inward > 0f) v += fromHead * (inward * closeness);
                    v += fromHead * (closeness * 0.6f * dt);
                }

                // Calm near your head: the closer, the slower, and never faster than a dart elsewhere.
                float limit = Mathf.Lerp(0.07f, 0.55f, Mathf.InverseLerp(headClearance, headClearance + 0.45f, headDistance));
                float speed = v.magnitude;
                if (speed > limit) v *= limit / speed;
                p += v * dt;
                // Inside the firm limit (your head turned towards a fish, say) it eases back out rather than jumping, and
                // never comes nearer than a few centimetres inside it.
                toHead = p - head;
                headDistance = toHead.magnitude;
                if (headDistance < clearance && headDistance > 1e-4f)
                {
                    float eased = Mathf.Max(headClearance - 0.04f, Mathf.MoveTowards(headDistance, clearance, 0.4f * dt));
                    p = head + toHead / headDistance * eased;
                }
                p.y = Mathf.Max(p.y, fishFloor);

                f.Velocity = v;
                f.Position = p;
                Turn(f, v, dt);
                Draw(i, f, music, t, waveAge);

                // Fish circling a hand let out the odd tiny bubble.
                if (f.Gathered > 0.9f && Random.value < dt * 0.25f)
                    fishBubbles.Emit(new ParticleSystem.EmitParams
                    {
                        position = p + f.Heading * (f.Size * 0.5f),
                        velocity = Vector3.up * Random.Range(0.03f, 0.06f),
                        startColor = new Color(0.85f, 0.97f, 1f, 0.8f),
                        startSize = Random.Range(0.003f, 0.0055f),
                        startLifetime = Random.Range(1.2f, 2f)
                    }, 1);
            }
        }

        /// <summary>
        /// Turns a fish smoothly to face where it swims, nose never too steeply up or down, banking a little into its
        /// turns, and beats its tail faster and wider the faster it goes.
        /// </summary>
        void Turn(Fish f, Vector3 v, float dt)
        {
            float speed = v.magnitude;
            if (speed > 0.015f)
            {
                var want = v / speed;
                want.y = Mathf.Clamp(want.y, -0.45f, 0.45f);
                var before = new Vector3(f.Heading.x, 0f, f.Heading.z);
                var turned = Vector3.Slerp(f.Heading, want.normalized, 1f - Mathf.Exp(-(2.5f + speed * 10f) * dt));
                if (turned.sqrMagnitude > 1e-6f) f.Heading = turned.normalized;
                var after = new Vector3(f.Heading.x, 0f, f.Heading.z);
                float yawRate = dt > 0f && before.sqrMagnitude > 1e-6f && after.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(before, after, Vector3.up) / dt : 0f;
                f.Bank = Mathf.Lerp(f.Bank, Mathf.Clamp(-yawRate * 0.25f, -25f, 25f), 1f - Mathf.Exp(-4f * dt));
            }
            f.Flick *= Mathf.Exp(-dt * 3f);
            float wag = 0.045f + Mathf.Clamp01(speed / 0.45f) * 0.09f + f.Flick * 0.08f;
            f.Wag = Mathf.Lerp(f.Wag, wag, 1f - Mathf.Exp(-6f * dt));
            // Kept within two strokes, so the side fins (half as fast) carry on smoothly.
            f.Stroke = Mathf.Repeat(f.Stroke + dt * (7f + speed * 36f + f.Flick * 16f), Mathf.PI * 4f);
        }

        /// <summary>
        /// A fish's place, colour and glow for the instanced draw: its stripes glow with its voice's band, brighter while
        /// it circles a hand, and the beat's wave of light lights it up as it passes.
        /// </summary>
        void Draw(int i, Fish f, MusicFrame music, float t, float waveAge)
        {
            float grow = Mathf.SmoothStep(0f, 1f, FadeIn);
            var rotation = Quaternion.LookRotation(f.Heading, Vector3.up) * Quaternion.Euler(0f, 0f, f.Bank);
            fishMatrices[i] = Matrix4x4.TRS(f.Position, rotation, Vector3.one * (f.Size * grow));
            float front = Vector3.Distance(f.Position, waveOrigin) - waveAge * waveSpeed;
            float flash = Mathf.Exp(-front * front / 0.02f) * Mathf.Exp(-waveAge * 1.1f);
            float glow = 0.15f + music.Band(f.Voice + 2) * 0.8f + music.Kick * 0.15f + f.Gathered * 0.35f + flash * 0.6f;
            var c = VoiceColors[f.Voice].linear;
            fishTints[i] = new Vector4(c.r, c.g, c.b, glow);
            fishSwims[i] = new Vector4(f.Stroke, f.Wag, flash * 0.45f, f.Seed);
        }

        /// <summary>
        /// All the fish in one instanced draw call. The editor's previews step the world many times before each picture
        /// without ever finishing a frame, and instanced draws would pile up there, so outside play mode the same mesh,
        /// material and values go to one renderer per fish instead.
        /// </summary>
        void DrawFish()
        {
            if (fish == null || !fishPlaced || bubbleNotesFishMaterial == null || fishMesh == null) return;
            if (!Application.isPlaying)
            {
                DrawFishPreview();
                return;
            }
            fishBlock.SetVectorArray(TintId, fishTints);
            fishBlock.SetVectorArray(SwimId, fishSwims);
            var draw = new RenderParams(bubbleNotesFishMaterial)
            {
                matProps = fishBlock,
                layer = gameObject.layer,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(fishCentre, new Vector3(8f, 6f, 8f))
            };
            Graphics.RenderMeshInstanced(draw, fishMesh, 0, fishMatrices, fish.Length);
        }

        MeshRenderer[] previewFish;

        void DrawFishPreview()
        {
            if (previewFish == null)
            {
                previewFish = new MeshRenderer[fish.Length];
                for (int i = 0; i < fish.Length; i++)
                {
                    var go = new GameObject($"Fish Preview {i}");
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = fishMesh;
                    previewFish[i] = go.AddComponent<MeshRenderer>();
                    previewFish[i].sharedMaterial = bubbleNotesFishMaterial;
                    previewFish[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    previewFish[i].receiveShadows = false;
                }
            }
            float grow = Mathf.SmoothStep(0f, 1f, FadeIn);
            for (int i = 0; i < fish.Length; i++)
            {
                var f = fish[i];
                previewFish[i].transform.SetPositionAndRotation(f.Position, Quaternion.LookRotation(f.Heading, Vector3.up) * Quaternion.Euler(0f, 0f, f.Bank));
                previewFish[i].transform.localScale = Vector3.one * (f.Size * grow);
                fishBlock.Clear();
                fishBlock.SetVector(TintId, fishTints[i]);
                fishBlock.SetVector(SwimId, fishSwims[i]);
                previewFish[i].SetPropertyBlock(fishBlock);
            }
        }

        /// <summary>A slow, swirling current: smooth in space and time, so the shoals drift in lazy eddies.</summary>
        static Vector3 Current(Vector3 p, float t) => new Vector3(
            Mathf.Sin(p.y * 3.1f + t * 0.4f) + Mathf.Sin(p.z * 2.3f - t * 0.3f),
            Mathf.Sin(p.z * 2.7f + t * 0.35f) * 0.4f,
            Mathf.Sin(p.x * 2.9f - t * 0.45f) + Mathf.Sin(p.y * 1.7f + t * 0.25f)) * 0.08f;

        static Vector3 Wobble(Fish f, float t, float size) => new Vector3(
            Mathf.Sin(t * 0.9f * f.Speed + f.Phase),
            Mathf.Sin(t * 1.3f * f.Speed + f.Phase * 1.7f),
            Mathf.Sin(t * 0.7f * f.Speed + f.Phase * 2.3f)) * size;

        // ---- The water trails ----

        /// <summary>
        /// One hand's water trail. Its head winds round the palm and the back of the hand (behind the fingers, so a
        /// pointing fingertip stays clear for popping) and every point of it chases the one before, so at rest it coils
        /// round the hand and swept through the air it pours along behind it like a ribbon, sagging a little and
        /// shedding drops where it whips round. It follows a smoothed hand, steady when the hand is still and close
        /// behind it when it moves fast, and is drawn through evenly spaced, smoothed points, so it never jitters or
        /// kinks. It fades away while the menu is open and in Meta's menu gesture.
        /// </summary>
        void Stream(int s, MusicFrame music, Camera view, bool menu, float dt)
        {
            var line = streams[s];
            bool on = tracked[s] && !menu && !gesture[s] && bubbleNotesStreamMaterial != null;
            streamLevel[s] = Mathf.MoveTowards(streamLevel[s], on ? 1f : 0f, dt * (on ? 1.4f : 3f));
            float level = streamLevel[s] * FadeIn;
            if (level <= 0.005f || !tracked[s])
            {
                line.SetVisible(false);
                streamFresh[s] = true;
                return;
            }
            dt = Mathf.Min(dt, 0.05f);
            float speed = handSpeed[s];
            if (streamFresh[s])
            {
                streamPalm[s] = palms[s];
                streamAxis[s] = handAxis[s];
                streamNormal[s] = handNormal[s].sqrMagnitude > 0.5f ? handNormal[s] : Vector3.forward;
            }
            else
            {
                streamPalm[s] = Vector3.Lerp(streamPalm[s], palms[s], 1f - Mathf.Exp(-(12f + speed * 50f) * dt));
                streamAxis[s] = Vector3.Slerp(streamAxis[s], handAxis[s], 1f - Mathf.Exp(-12f * dt));
                if (handNormal[s].sqrMagnitude > 0.5f) streamNormal[s] = Vector3.Slerp(streamNormal[s], handNormal[s], 1f - Mathf.Exp(-12f * dt));
            }

            // Directions round the hand: along it (wrist to fingers), out of the palm, and across it.
            var axis = streamAxis[s].sqrMagnitude > 1e-6f ? streamAxis[s].normalized : Vector3.up;
            var a = streamNormal[s] - axis * Vector3.Dot(streamNormal[s], axis);
            if (a.sqrMagnitude < 0.01f) a = Vector3.Cross(axis, Vector3.up);
            if (a.sqrMagnitude < 1e-6f) a = Vector3.right;
            a.Normalize();
            var b = Vector3.Cross(axis, a);

            streamPhase[s] += dt * (s == 0 ? 1f : -1f) * (5.5f + music.Band(2) * 2f);
            float coil = Mathf.Lerp(streamCoil.x, streamCoil.y, Mathf.Clamp01(speed / 1.2f)) * (closed[s] ? 0.8f : 1f);
            var lead = streamPalm[s] + axis * (-0.015f + Mathf.Sin(streamPhase[s] * 0.37f) * 0.02f)
                + (a * Mathf.Cos(streamPhase[s]) + b * Mathf.Sin(streamPhase[s])) * coil;

            var chain = chains[s];
            var last = chainsBefore[s];
            if (streamFresh[s])
            {
                for (int i = 0; i < StreamPoints; i++) chain[i] = last[i] = lead;
                streamFresh[s] = false;
            }
            last[0] = chain[0];
            chain[0] = lead;
            float follow = 1f - Mathf.Exp(-30f * dt);
            for (int i = 1; i < StreamPoints; i++)
            {
                last[i] = chain[i];
                chain[i] = Vector3.Lerp(chain[i], chain[i - 1], follow) + Vector3.down * (0.04f * dt * i / StreamPoints);
            }

            // Drawn through evenly spaced points along the chain, smoothed, so the ribbon bends without kinks.
            float length = Smooth(chain);
            var eye = view.transform.position;
            // Full where it leaves the hand, thinning and fading away towards its tail, swelling a little with the bass.
            for (int i = 0; i < StreamPoints; i++)
            {
                float u = i / (float)(StreamPoints - 1);
                streamWidths[i] = Mathf.Lerp(1f, 0.06f, Mathf.Pow(u, 0.7f)) * (1f + music.Band(1) * 0.3f * Mathf.Sin(u * 13f - Clock * 7f + s * 2f));
                streamColors[i] = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.2f, u * u));
            }
            line.SetVisible(true);
            line.Set(streamPath, streamColors, streamWidths, StreamPoints, streamWidth * (0.6f + 0.4f * level), new Color(1f, 1f, 1f, level), eye);
            block.Clear();
            block.SetFloat(LengthId, Mathf.Max(length, 0.05f));
            block.SetFloat(ClockId, Mathf.Repeat(Clock, 600f));
            line.Renderer.SetPropertyBlock(block);

            // Drops fly off where the trail whips round fast, and now and then one drips from its tail.
            float rate = dt * 2.5f * level;
            for (int i = 3; i < StreamPoints; i += 3)
            {
                var v = (chain[i] - last[i]) / Mathf.Max(dt, 1e-4f);
                float fast = v.magnitude;
                if (fast < 0.7f || Random.value > rate * fast) continue;
                Drop(chain[i], v * 0.45f + Random.insideUnitSphere * 0.1f, Random.Range(0.5f, 0.9f));
            }
            if (Random.value < dt * 1.2f * level)
                Drop(chain[StreamPoints - 1], Random.insideUnitSphere * 0.04f, 0.8f);
        }

        /// <summary>
        /// Resamples <paramref name="chain"/> at even steps along its length into streamPath, then evens out its bends
        /// a little (the ends stay put). Returns the length.
        /// </summary>
        float Smooth(Vector3[] chain)
        {
            streamRun[0] = 0f;
            for (int i = 1; i < StreamPoints; i++) streamRun[i] = streamRun[i - 1] + Vector3.Distance(chain[i], chain[i - 1]);
            float length = streamRun[StreamPoints - 1];
            if (length < 1e-4f)
            {
                for (int i = 0; i < StreamPoints; i++) streamPath[i] = chain[0];
                return length;
            }
            int j = 0;
            for (int i = 0; i < StreamPoints; i++)
            {
                float at = length * i / (StreamPoints - 1);
                while (j < StreamPoints - 2 && streamRun[j + 1] < at) j++;
                float span = streamRun[j + 1] - streamRun[j];
                streamPath[i] = span > 1e-6f ? Vector3.Lerp(chain[j], chain[j + 1], (at - streamRun[j]) / span) : chain[j];
            }
            for (int pass = 0; pass < StreamSmoothing; pass++)
            {
                for (int i = 0; i < StreamPoints; i++) streamEven[i] = streamPath[i];
                for (int i = 1; i < StreamPoints - 1; i++)
                    streamPath[i] = streamEven[i] * 0.5f + (streamEven[i - 1] + streamEven[i + 1]) * 0.25f;
            }
            return length;
        }

        /// <summary>A drop of water, never flung at your face.</summary>
        void Drop(Vector3 at, Vector3 velocity, float life)
        {
            if (View != null)
            {
                var toEye = View.transform.position - at;
                float d = toEye.magnitude;
                if (d > 1e-4f)
                {
                    toEye /= d;
                    float towards = Vector3.Dot(velocity, toEye);
                    if (towards > 0f) velocity -= toEye * (2f * towards);
                }
            }
            float r = Random.value;
            droplets.Emit(new ParticleSystem.EmitParams
            {
                position = at,
                velocity = velocity,
                startColor = r < 0.45f ? WaterColors[0] : r < 0.8f ? WaterColors[1] : WaterColors[2],
                startSize = Random.Range(0.004f, 0.008f),
                startLifetime = life
            }, 1);
        }

        // ---- The fish's mesh ----

        /// <summary>
        /// The fish every instance draws, named BubbleNotesFish: one unit long, nose at +z, back up. A rounded, slightly
        /// flattened body, a forked tail, a fin on the back, one under the belly and two small side fins. uv.x runs from
        /// the nose (0) to the tip of the tail (1), uv.y names the part (0 body, 1 tail, 2 back fin, 3 side fins, 4 belly
        /// fin) and uv2 is the place on a fin, from its root (0) out to its edge (1) and across it (-1 to 1). Each fin is
        /// two sheets back to back, so both its sides are lit properly.
        /// </summary>
        static Mesh BuildFishMesh()
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var finUvs = new List<Vector2>();
            var triangles = new List<int>();

            // The body: rings from just behind the nose back to the root of the tail, round a point at the nose.
            const int Rings = 18, Around = 14;
            const float BodyEnd = 0.74f;
            vertices.Add(new Vector3(0f, -0.004f, 0.5f));
            uvs.Add(Vector2.zero);
            finUvs.Add(Vector2.zero);
            for (int r = 0; r < Rings; r++)
            {
                float u = Mathf.Pow((r + 1f) / Rings, 1.3f);
                float s = u * BodyEnd;
                float h = BodyHalfHeight(u), w = h * 0.5f;
                for (int a = 0; a < Around; a++)
                {
                    float angle = a / (float)Around * Mathf.PI * 2f;
                    float y = Mathf.Sin(angle), x = Mathf.Cos(angle);
                    // A fuller belly and a narrower back.
                    vertices.Add(new Vector3(x * w * (1f - 0.18f * y), y * h * (y < 0f ? 1.06f : 0.94f) - h * 0.04f, 0.5f - s));
                    uvs.Add(new Vector2(s, 0f));
                    finUvs.Add(Vector2.zero);
                }
            }
            int Ring(int r, int a) => 1 + r * Around + (a % Around);
            for (int a = 0; a < Around; a++)
                Triangle(vertices, triangles, 0, Ring(0, a), Ring(0, a + 1), OutOfBody(vertices[Ring(0, a)]) + Vector3.forward);
            for (int r = 0; r < Rings - 1; r++)
                for (int a = 0; a < Around; a++)
                {
                    var outward = OutOfBody(vertices[Ring(r, a)]);
                    Triangle(vertices, triangles, Ring(r, a), Ring(r + 1, a), Ring(r + 1, a + 1), outward);
                    Triangle(vertices, triangles, Ring(r, a), Ring(r + 1, a + 1), Ring(r, a + 1), outward);
                }
            // Close the root of the tail.
            int cap = vertices.Count;
            vertices.Add(new Vector3(0f, -BodyHalfHeight(1f) * 0.04f, 0.5f - BodyEnd));
            uvs.Add(new Vector2(BodyEnd, 0f));
            finUvs.Add(Vector2.zero);
            for (int a = 0; a < Around; a++)
                Triangle(vertices, triangles, cap, Ring(Rings - 1, a), Ring(Rings - 1, a + 1), Vector3.back);

            // The tail: two lobes forking from the root, the notch between them.
            Fin(vertices, uvs, finUvs, triangles, 1f, 4, 7, (along, across) =>
            {
                float edge = 1f - 0.14f * Mathf.Pow(1f - Mathf.Abs(across), 1.3f);
                float s = Mathf.Lerp(0.7f, edge, along);
                float span = Mathf.Lerp(0.03f, 0.16f, Mathf.Pow(along, 0.85f));
                return new Vector3(0f, across * span, 0.5f - s);
            }, Vector3.right);

            // The back fin: a small sail along the top, tallest at the front and sloping back.
            Fin(vertices, uvs, finUvs, triangles, 2f, 3, 5, (along, across) =>
            {
                float b = across * 0.5f + 0.5f;
                float s = Mathf.Lerp(0.26f, 0.5f, b);
                float root = BodyTop(s / BodyEnd) - 0.006f;
                float height = 0.012f + 0.07f * Mathf.Pow(1f - b, 0.7f);
                return new Vector3(0f, root + height * along, 0.5f - s - along * 0.05f);
            }, Vector3.right);

            // The belly fin: a smaller one underneath, further back.
            Fin(vertices, uvs, finUvs, triangles, 4f, 3, 4, (along, across) =>
            {
                float b = across * 0.5f + 0.5f;
                float s = Mathf.Lerp(0.5f, 0.64f, b);
                float root = -BodyHalfHeight(s / BodyEnd) * 1.02f + 0.006f;
                float height = 0.01f + 0.045f * Mathf.Pow(1f - b, 0.7f);
                return new Vector3(0f, root - height * along, 0.5f - s - along * 0.04f);
            }, Vector3.right);

            // Two side fins behind the gills, small paddles reaching back, out and a little down.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side;
                Fin(vertices, uvs, finUvs, triangles, 3f, 3, 3, (along, across) =>
                {
                    float s = Mathf.Lerp(0.19f, 0.25f, across * 0.5f + 0.5f) + along * 0.07f;
                    float u = s / BodyEnd;
                    float h = BodyHalfHeight(u);
                    return new Vector3(x * (h * 0.45f + along * 0.05f), -h * 0.3f - along * 0.03f, 0.5f - s);
                }, new Vector3(0f, 1f, -0.3f));
            }

            var mesh = new Mesh { name = "BubbleNotesFish" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, finUvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // Room for the tail's swing.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(0.6f, 0.5f, 1.1f));
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>Half the body's height at <paramref name="u"/> (0 at the nose, 1 at the root of the tail), in lengths.</summary>
        static float BodyHalfHeight(float u)
        {
            float front = Mathf.Sqrt(Mathf.Sin(Mathf.Min(u / 0.34f, 1f) * Mathf.PI * 0.5f));
            float back = 1f - 0.8f * Mathf.Pow(Mathf.Clamp01((u - 0.34f) / 0.66f), 1.15f);
            return 0.115f * front * back;
        }

        /// <summary>The height of the top of the back at <paramref name="u"/>.</summary>
        static float BodyTop(float u)
        {
            float h = BodyHalfHeight(u);
            return h * 0.94f - h * 0.04f;
        }

        static Vector3 OutOfBody(Vector3 p) => new Vector3(p.x, p.y, 0f);

        /// <summary>Adds a triangle facing <paramref name="outward"/> (front faces wind clockwise as seen from the front).</summary>
        static void Triangle(List<Vector3> vertices, List<int> triangles, int a, int b, int c, Vector3 outward)
        {
            var n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            triangles.Add(a);
            if (Vector3.Dot(n, outward) >= 0f)
            {
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(c);
                triangles.Add(b);
            }
        }

        /// <summary>
        /// A fin as a grid of <paramref name="rows"/> from root to edge by <paramref name="columns"/> across, shaped by
        /// <paramref name="shape"/>, as two sheets back to back facing either way along <paramref name="normal"/>.
        /// </summary>
        static void Fin(List<Vector3> vertices, List<Vector2> uvs, List<Vector2> finUvs, List<int> triangles, float part, int rows, int columns,
            System.Func<float, float, Vector3> shape, Vector3 normal)
        {
            for (int face = 0; face < 2; face++)
            {
                int start = vertices.Count;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < columns; c++)
                    {
                        float along = r / (rows - 1f), across = c / (columns - 1f) * 2f - 1f;
                        var p = shape(along, across);
                        vertices.Add(p);
                        uvs.Add(new Vector2(0.5f - p.z, part));
                        finUvs.Add(new Vector2(along, across));
                    }
                var facing = face == 0 ? normal : -normal;
                for (int r = 0; r < rows - 1; r++)
                    for (int c = 0; c < columns - 1; c++)
                    {
                        int i0 = start + r * columns + c, i1 = i0 + 1, i2 = i0 + columns, i3 = i2 + 1;
                        Triangle(vertices, triangles, i0, i2, i3, facing);
                        Triangle(vertices, triangles, i0, i3, i1, facing);
                    }
            }
        }
    }
}
