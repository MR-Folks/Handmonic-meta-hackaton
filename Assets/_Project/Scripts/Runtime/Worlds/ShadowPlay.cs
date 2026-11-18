using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Shadow play world: the room goes dark and a lamp low behind you lights the wall in front, and your hands cast
    /// their shadows on it: the tracked hand mesh itself, every vertex projected from the lamp onto the wall, so the
    /// shadow has the exact shape your hand makes, big when close to you and small near the wall, as real shadows are.
    /// Your arms, shoulders, neck and head cast softer ones too, your head high up. With body tracking the arms follow
    /// your real joints: each forearm runs from the hand's own wrist to your elbow, the upper arm on to your shoulder, and
    /// the shoulders and neck join them, so the silhouette bends at the elbow and holds together like a real arm. Without
    /// a body (no permission, or not tracked) each forearm is estimated from the hand alone and the rest fades away.
    /// With the full body tracked your trunk and legs cast theirs too (the legs are Meta's Generative Legs, estimated from
    /// the upper body rather than seen): seated, the thighs point at the wall and the shins come down it to the floor;
    /// standing, most of a leg's shadow falls on the floor below the wall, as a lamp behind you would cast it. The trunk
    /// and legs ring none of the lights. On every beat your shadow leaves a coloured echo that drifts up and grows as it fades. Small notes
    /// of light float in the lamp's pool; cover one with a shadow and it rings, a higher note higher up the wall, so
    /// your shadow plays along with the song. The lamp breathes with the bass. Each sung word lands on one of the lights
    /// and drifts with it, just above it; ring that light and the word fades slowly away as its ring spreads.
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
        [Tooltip("Where the lamp is from your head when the world starts (m): to the right, up, and forward (negative is behind). Low behind you, like a floor lamp, so your hands' shadows land in the middle of the wall and your head's falls high above them. " +
                 "Far enough back that your shoulders, nearer the lamp than your hands, don't cast giant shadows.")]
        [SerializeField] Vector3 lampOffset = new Vector3(0.25f, -0.7f, -1.25f);
        [Tooltip("The lamp's radius (m): bigger makes softer shadow edges.")]
        [SerializeField] float lampRadius = 0.032f;
        [SerializeField] Color lampColor = new Color(1f, 0.78f, 0.52f);

        [Header("Arms")]
        [Tooltip("The arm's radius (m) at the wrist, where its shadow meets the hand's.")]
        [SerializeField] float wristRadius = 0.025f;
        [Tooltip("The forearm's radius (m) at the elbow; it widens evenly from the wrist.")]
        [SerializeField] float forearmRadius = 0.038f;
        [Tooltip("The upper arm's radius (m) at the elbow and at the shoulder.")]
        [SerializeField] Vector2 upperArmRadius = new Vector2(0.041f, 0.046f);
        [Tooltip("The shoulders' radius (m) at the neck and at each shoulder.")]
        [SerializeField] Vector2 shoulderRadius = new Vector2(0.055f, 0.05f);
        [SerializeField] float neckRadius = 0.048f;
        [Tooltip("Without a tracked body: how far (m) the estimated forearm reaches back from the wrist.")]
        [SerializeField] float estimatedForearm = 0.255f;
        [Tooltip("Seconds the arms take to change between your tracked body and the hands-only estimate.")]
        [SerializeField] float armFade = 0.35f;
        [Tooltip("Believable lengths (m) of a forearm (hand's wrist to elbow) and an upper arm (elbow to shoulder). " +
                 "Outside them the body's joints are not trusted and the arm fades to the estimate.")]
        [SerializeField] Vector2 forearmLength = new Vector2(0.18f, 0.34f);
        [SerializeField] Vector2 upperArmLength = new Vector2(0.2f, 0.4f);
        [Tooltip("How far (m) the body's wrist may be from the hand's before the body's arm is not trusted.")]
        [SerializeField] float wristMismatch = 0.15f;

        [Header("Body")]
        [Tooltip("The trunk's radius (m) at the chest and at the hips.")]
        [SerializeField] Vector2 trunkRadius = new Vector2(0.13f, 0.115f);
        [Tooltip("The hips' radius (m) between the legs and at each hip joint.")]
        [SerializeField] Vector2 hipRadius = new Vector2(0.09f, 0.08f);
        [Tooltip("A thigh's radius (m) at the hip and at the knee.")]
        [SerializeField] Vector2 thighRadius = new Vector2(0.075f, 0.055f);
        [Tooltip("A shin's radius (m) at the knee and at the ankle.")]
        [SerializeField] Vector2 shinRadius = new Vector2(0.05f, 0.035f);
        [Tooltip("A foot's radius (m) at the ankle and at the toes.")]
        [SerializeField] Vector2 footRadius = new Vector2(0.035f, 0.025f);
        [Tooltip("Believable lengths (m) of the trunk (chest to hips) and of a thigh or a shin. Outside them the body's " +
                 "joints are not trusted and the part fades away.")]
        [SerializeField] Vector2 trunkLength = new Vector2(0.18f, 0.7f);
        [SerializeField] Vector2 legLength = new Vector2(0.28f, 0.65f);
        [Tooltip("How far (m) a hip joint may be from the hips before the leg is not trusted.")]
        [SerializeField] float hipSpread = 0.25f;

        [Header("Music")]
        [Tooltip("Seconds a beat's coloured echo of your shadow lasts.")]
        [SerializeField] float echoLife = 1.8f;
        [SerializeField] int lightNotes = 22;

        [Header("Lyrics")]
        [Tooltip("Seconds a sung word stays on its light if no shadow rings the light first.")]
        [SerializeField] float wordLife = 6f;
        [Tooltip("Seconds a word takes to fade away once its light rings, as the ring spreads.")]
        [SerializeField] float wordFade = 1.6f;
        [Tooltip("How close (m on the wall) a new word may land to a light already carrying one, while others are free.")]
        [SerializeField] float wordSpacing = 0.2f;

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

        /// <summary>The parts of the shadow drawn as capsules. Those from the trunk on ring none of the lights.</summary>
        public enum ShadowPart { Head, Neck, Shoulder, Forearm, UpperArm, Trunk, Hip, Thigh, Shin, Foot }

        /// <summary>
        /// A tapered capsule on the wall, in metres along the wall's Right and Up: radius and soft edge at each end, and how
        /// dark it is (1 a full shadow; less while a part fades in or out).
        /// </summary>
        struct Cap
        {
            public Vector2 A, B;
            public float Radius, RadiusB, Soft, SoftB, Strength;
            public ShadowPart Part;
            public bool Left;
        }

        sealed class Echo
        {
            public readonly Cap[] Caps = new Cap[CapsPerShadow];
            public int Count;
            public float Age = 99f;
            public Color Color;
            /// <summary>The hands' shadow when the echo was made, flat on the wall.</summary>
            public Mesh Hands;
            public bool HasHands;
            public readonly List<Vector3> Points = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
        }

        /// <summary>One hand's shadow this frame: its mesh projected onto the wall (x, y in metres, z 0).</summary>
        sealed class HandShadow
        {
            public Mesh Baked, Flat;
            public readonly List<Vector3> Points = new List<Vector3>();
            public int[] Triangles;
            public SkinnedMeshRenderer Source;
            public bool Valid;
            public Vector2 Min, Max;
        }

        sealed class Note
        {
            public Renderer Light;
            public Vector2 Position, Velocity;
            public float Ring, Rest, Seed;
            public bool Covered;
            /// <summary>The sung word riding on this light, if any, and seconds since the light rang it away (-1 not yet).</summary>
            public WordLayer.Word Word;
            public float Fading = -1f;
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
        // Per hand: the wrist joint of the visible hand (and the mesh it came from), which way the forearm leaves the
        // hand, how far the arm has faded to the body (0 estimate, 1 body), and the last believable elbow and shoulder.
        readonly Transform[] wristJoints = new Transform[2];
        readonly SkinnedMeshRenderer[] wristSources = new SkinnedMeshRenderer[2];
        readonly Vector3[] handBack = { Vector3.down, Vector3.down };
        readonly float[] armBlend = new float[2];
        readonly Vector3[] lastElbow = new Vector3[2], lastShoulder = new Vector3[2], lastShoulders = new Vector3[2];
        float torsoBlend;
        Vector3 lastNeck, lastHead;
        // The trunk and each leg: how far they have faded in, and their last believable joints.
        float trunkBlend;
        Vector3 lastChest, lastHips;
        readonly float[] legBlend = new float[2];
        readonly Vector3[] lastPelvis = new Vector3[2], lastHip = new Vector3[2], lastKnee = new Vector3[2],
            lastAnkle = new Vector3[2], lastToes = new Vector3[2];
        readonly HandShadow[] handShadows = { new HandShadow(), new HandShadow() };
        readonly MaterialPropertyBlock[] fills = new MaterialPropertyBlock[1 + Echoes];
        static readonly int FillId = Shader.PropertyToID("_Fill");

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> spots = new List<Vector2>();
        readonly List<Vector4> ends = new List<Vector4>();
        readonly List<Vector4> shapes = new List<Vector4>();
        readonly List<Vector2> tapers = new List<Vector2>();
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

        // Enough for a line's words to stay on their lights a while before the oldest is taken back.
        protected override int SungWordCount => 8;

        protected override float SungWordHeight => 0.045f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            for (int i = 0; i < Echoes; i++)
            {
                echoes[i] = new Echo { Hands = new Mesh { name = $"Shadow Echo {i}" } };
                echoes[i].Hands.MarkDynamic();
            }
            for (int i = 0; i < fills.Length; i++) fills[i] = new MaterialPropertyBlock();
            foreach (var h in handShadows)
            {
                h.Baked = new Mesh { name = "Baked Hand" };
                h.Flat = new Mesh { name = "Hand Shadow" };
                h.Flat.MarkDynamic();
            }
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
            // The arms follow the tracked body while you are here; body tracking runs only then.
            BodyRig.Wanted = true;
            armBlend[0] = armBlend[1] = torsoBlend = trunkBlend = 0f;
            legBlend[0] = legBlend[1] = 0f;
        }

        protected override void OnExit()
        {
            BodyRig.Wanted = false;
            HandSkin.FocusStrength = 0f;
            foreach (var n in notes)
            {
                n.Light.gameObject.SetActive(false);
                n.Word = null;
                n.Fading = -1f;
            }
            foreach (var r in ripples) { r.Age = 99f; r.Ring.gameObject.SetActive(false); }
        }

        void OnDestroy()
        {
            if (mask != null) { mask.Release(); Discard(mask); }
            if (veilInstance != null) Discard(veilInstance);
            if (capsules != null) Discard(capsules);
            foreach (var e in echoes) if (e?.Hands != null) Discard(e.Hands);
            foreach (var h in handShadows)
            {
                if (h.Baked != null) Discard(h.Baked);
                if (h.Flat != null) Discard(h.Flat);
            }
            commands?.Release();
        }

        /// <summary>On the beat, the shadow as it is now becomes a coloured echo.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            if (liveCount > 0) Remember();
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Shadows(rig, view, dt);
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

            // About 200 pixels per metre, with 4x antialiasing, keeps the edges of fingers' shadows smooth.
            int width = 1024, height = Mathf.Clamp(Mathf.RoundToInt(1024f * wall.Size.y / Mathf.Max(wall.Size.x, 0.1f)), 256, 1024);
            if (mask == null || mask.width != width || mask.height != height)
            {
                if (mask != null) { mask.Release(); Discard(mask); }
                mask = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "Shadow Mask", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, antiAliasing = 4
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

        /// <summary>
        /// Adds the shadow of a tapered capsule between two points, <paramref name="radiusA"/> thick at the first and
        /// <paramref name="radiusB"/> at the second, each end magnified and softened as much as the lamp makes it there,
        /// and <paramref name="strength"/> dark.
        /// </summary>
        void AddShadow(Vector3 a, Vector3 b, float radiusA, float radiusB, float strength = 1f, ShadowPart part = ShadowPart.Head, bool left = false)
        {
            if (liveCount >= CapsPerShadow) return;
            if (!Project(a, out var la, out float ma) || !Project(b, out var lb, out float mb)) return;
            live[liveCount++] = new Cap
            {
                A = la, B = lb, Radius = radiusA * ma, RadiusB = radiusB * mb, Soft = Penumbra(ma), SoftB = Penumbra(mb),
                Strength = strength, Part = part, Left = left
            };
        }

        /// <summary>How wide the soft edge of a shadow is where a point is magnified this much (m on the wall).</summary>
        float Penumbra(float magnify) => lampRadius * (magnify - 1f) * 0.5f + 0.003f;

        static float Ease(float t) => t * t * (3f - 2f * t);

        /// <summary>
        /// The shadow of this frame: each tracked hand's own mesh projected from the lamp onto the wall, plus your arms,
        /// shoulders, neck, head, trunk and legs as soft capsules.
        /// </summary>
        void Shadows(HandRig rig, Camera view, float dt)
        {
            liveCount = 0;
            foreach (var h in handShadows) h.Valid = false;
            var head = view.transform.position;
            var forward = Effects.FlatForward(view);
            // The head, on the headset's own pose.
            AddShadow(head - forward * 0.06f + Vector3.up * 0.04f, head - forward * 0.06f - Vector3.up * 0.04f, 0.085f, 0.085f);
            Shoulders(dt);
            Trunk(dt);
            Legs(dt);
            if (rig == null) return;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                if (!hand.IsTracked) continue;
                Arm(hand, side, dt);
                ProjectHand(hand.Mesh, handShadows[side]);
            }
        }

        /// <summary>
        /// One arm. The forearm always starts at the visible hand's own wrist joint, so it meets the hand's shadow exactly.
        /// With a believable tracked body it runs to your real elbow and the upper arm on to your shoulder; otherwise it
        /// reaches back from the wrist the way the hand points, from the palm through the wrist (joints that stay put when
        /// the fingers curl, so a fist doesn't swing it). The two fade into each other as the body comes and goes.
        /// </summary>
        void Arm(HandRig.Hand hand, int side, float dt)
        {
            bool left = side == 0;
            var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
            var wrist = HandWrist(hand, side, palm);
            var estimatedElbow = wrist + handBack[side] * estimatedForearm;

            var arm = BodyRig.Get(left);
            bool believable = BodyRig.IsTracked && arm.Valid && Believable(wrist, arm);
            if (believable)
            {
                lastElbow[side] = arm.Elbow;
                lastShoulder[side] = arm.Shoulder;
            }
            armBlend[side] = Mathf.MoveTowards(armBlend[side], believable ? 1f : 0f, dt / Mathf.Max(armFade, 0.01f));
            float body = Ease(armBlend[side] * BodyRig.Blend);

            var elbow = Vector3.Lerp(estimatedElbow, lastElbow[side], body);
            AddShadow(wrist, elbow, wristRadius, forearmRadius, 1f, ShadowPart.Forearm, left);
            if (body > 0.01f) AddShadow(elbow, lastShoulder[side], upperArmRadius.x, upperArmRadius.y, body, ShadowPart.UpperArm, left);
        }

        /// <summary>Whether the body's arm fits this hand: a forearm and upper arm of believable lengths, wrists together.</summary>
        bool Believable(Vector3 wrist, BodyRig.Arm arm)
        {
            float forearm = Vector3.Distance(wrist, arm.Elbow);
            float upper = Vector3.Distance(arm.Elbow, arm.Shoulder);
            return forearm >= forearmLength.x && forearm <= forearmLength.y && upper >= upperArmLength.x && upper <= upperArmLength.y
                && Vector3.Distance(arm.Wrist, wrist) <= wristMismatch;
        }

        /// <summary>
        /// The visible hand's wrist joint (its mesh's root bone), noting which way the forearm leaves the hand: from the
        /// palm back through the wrist. Without the hand's skeleton, estimated from the fingertips as last seen.
        /// </summary>
        Vector3 HandWrist(HandRig.Hand hand, int side, Vector3 palm)
        {
            var mesh = hand.Mesh;
            if (mesh != wristSources[side])
            {
                wristSources[side] = mesh;
                wristJoints[side] = mesh != null ? mesh.rootBone : null;
            }
            var joint = wristJoints[side];
            if (joint != null)
            {
                var wrist = joint.position;
                var back = wrist - palm;
                if (back.sqrMagnitude > 0.02f * 0.02f) handBack[side] = back.normalized;
                return wrist;
            }
            for (int f = 0; f < 5; f++) tips[f] = hand.TipJoints[f] != null ? hand.TipJoints[f].position : hand.Tips[f];
            var along = (tips[1] + tips[2] + tips[3] + tips[4]) * 0.25f - palm;
            if (along.magnitude > 0.035f) handAlong[side] = along.normalized;
            handBack[side] = -handAlong[side];
            return palm - handAlong[side] * 0.045f;
        }

        /// <summary>
        /// The shoulders and neck, from the tracked body: a line from the neck out to each shoulder, where the upper arms
        /// start, and the neck up to the head. They fade in with a believable body and out without one.
        /// </summary>
        void Shoulders(float dt)
        {
            var l = BodyRig.Left;
            var r = BodyRig.Right;
            float width = Vector3.Distance(l.Shoulder, r.Shoulder);
            float neck = Vector3.Distance(BodyRig.Neck, BodyRig.Head);
            bool believable = BodyRig.IsTracked && BodyRig.SpineValid && l.Valid && r.Valid && width > 0.22f && width < 0.6f && neck < 0.3f;
            if (believable)
            {
                lastNeck = BodyRig.Neck;
                lastHead = BodyRig.Head;
                lastShoulders[0] = l.Shoulder;
                lastShoulders[1] = r.Shoulder;
            }
            torsoBlend = Mathf.MoveTowards(torsoBlend, believable ? 1f : 0f, dt / Mathf.Max(armFade, 0.01f));
            float body = Ease(torsoBlend * BodyRig.Blend);
            if (body <= 0.01f) return;
            AddShadow(lastNeck, lastShoulders[0], shoulderRadius.x, shoulderRadius.y, body, ShadowPart.Shoulder, true);
            AddShadow(lastNeck, lastShoulders[1], shoulderRadius.x, shoulderRadius.y, body, ShadowPart.Shoulder, false);
            AddShadow(lastNeck, lastHead, neckRadius, neckRadius, body, ShadowPart.Neck);
        }

        /// <summary>
        /// The trunk, from the tracked body: one broad capsule from the chest down to the hips, under the shoulders. It
        /// fades in with a believable body and out without one.
        /// </summary>
        void Trunk(float dt)
        {
            float length = Vector3.Distance(BodyRig.Chest, BodyRig.Hips);
            bool believable = BodyRig.IsTracked && BodyRig.SpineValid && BodyRig.HipsValid && length >= trunkLength.x && length <= trunkLength.y;
            if (believable)
            {
                lastChest = BodyRig.Chest;
                lastHips = BodyRig.Hips;
            }
            trunkBlend = Mathf.MoveTowards(trunkBlend, believable ? 1f : 0f, dt / Mathf.Max(armFade, 0.01f));
            float body = Ease(trunkBlend * BodyRig.Blend);
            if (body > 0.01f) AddShadow(lastChest, lastHips, trunkRadius.x, trunkRadius.y, body, ShadowPart.Trunk);
        }

        /// <summary>
        /// The legs, from full-body tracking: for each, the hips out to its hip joint, the thigh on to the knee, the shin
        /// to the ankle and the foot to the toes, tapering down. A leg fades in when its joints are believable and out
        /// without them (the upper body alone has no legs).
        /// </summary>
        void Legs(float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                var leg = BodyRig.GetLeg(left);
                float thigh = Vector3.Distance(leg.Hip, leg.Knee), shin = Vector3.Distance(leg.Knee, leg.Ankle);
                bool believable = BodyRig.IsTracked && BodyRig.HipsValid && leg.Valid
                    && thigh >= legLength.x && thigh <= legLength.y && shin >= legLength.x && shin <= legLength.y
                    && Vector3.Distance(BodyRig.Hips, leg.Hip) <= hipSpread;
                if (believable)
                {
                    lastPelvis[side] = BodyRig.Hips;
                    lastHip[side] = leg.Hip;
                    lastKnee[side] = leg.Knee;
                    lastAnkle[side] = leg.Ankle;
                    lastToes[side] = leg.Toes;
                }
                legBlend[side] = Mathf.MoveTowards(legBlend[side], believable ? 1f : 0f, dt / Mathf.Max(armFade, 0.01f));
                float body = Ease(legBlend[side] * BodyRig.Blend);
                if (body <= 0.01f) continue;
                AddShadow(lastPelvis[side], lastHip[side], hipRadius.x, hipRadius.y, body, ShadowPart.Hip, left);
                AddShadow(lastHip[side], lastKnee[side], thighRadius.x, thighRadius.y, body, ShadowPart.Thigh, left);
                AddShadow(lastKnee[side], lastAnkle[side], shinRadius.x, shinRadius.y, body, ShadowPart.Shin, left);
                AddShadow(lastAnkle[side], lastToes[side], footRadius.x, footRadius.y, body, ShadowPart.Foot, left);
            }
        }

        /// <summary>
        /// Projects a hand mesh, as it is posed now, from the lamp onto the wall. Any vertex the lamp cannot cast onto
        /// the wall (behind the lamp or past the wall) drops this hand's shadow for the frame.
        /// </summary>
        void ProjectHand(SkinnedMeshRenderer source, HandShadow shadow)
        {
            // The hand mesh must be readable to bake it on the headset (ProjectSetup gives the hands a readable copy).
            if (source == null || source.sharedMesh == null || !source.sharedMesh.isReadable) return;
            if (shadow.Source != source)
            {
                shadow.Source = source;
                shadow.Triangles = source.sharedMesh.triangles;
                shadow.Flat.Clear();
            }
            source.BakeMesh(shadow.Baked, true);
            shadow.Baked.GetVertices(shadow.Points);
            var t = source.transform;
            var toWorld = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);

            float lampHeight = Vector3.Dot(lamp - wall.Center, wall.Normal);
            if (lampHeight < 0.05f) return;
            var min = Vector2.positiveInfinity;
            var max = Vector2.negativeInfinity;
            var points = shadow.Points;
            for (int i = 0; i < points.Count; i++)
            {
                var p = toWorld.MultiplyPoint3x4(points[i]);
                float height = Vector3.Dot(p - wall.Center, wall.Normal);
                if (height < 0.003f || height > lampHeight - 0.03f) return;
                var onWall = lamp + (p - lamp) * (lampHeight / (lampHeight - height)) - wall.Center;
                var local = new Vector2(Vector3.Dot(onWall, wall.Right), Vector3.Dot(onWall, wall.Up));
                points[i] = new Vector3(local.x, local.y, 0f);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            var flat = shadow.Flat;
            flat.SetVertices(points);
            if (flat.GetIndexCount(0) != (uint)shadow.Triangles.Length) flat.SetTriangles(shadow.Triangles, 0, false);
            flat.bounds = new Bounds(Vector3.zero, new Vector3(wall.Size.x, wall.Size.y, 2f) * 2f);
            shadow.Min = min;
            shadow.Max = max;
            shadow.Valid = true;
        }

        void Remember()
        {
            var e = echoes[nextEcho];
            nextEcho = (nextEcho + 1) % Echoes;
            // The echo is the hands, forearms and head: upper arms, shoulders, trunk and legs, grown and coloured, would
            // fill the wall.
            e.Count = 0;
            for (int i = 0; i < liveCount; i++)
                if (live[i].Part == ShadowPart.Head || live[i].Part == ShadowPart.Forearm) e.Caps[e.Count++] = live[i];
            e.Age = 0f;
            e.Color = EchoColors[colorIndex++ % EchoColors.Length];

            // Both hands' shadows, as they are on the beat, in one mesh.
            e.Points.Clear();
            e.Triangles.Clear();
            foreach (var h in handShadows)
            {
                if (!h.Valid) continue;
                int start = e.Points.Count;
                e.Points.AddRange(h.Points);
                foreach (int i in h.Triangles) e.Triangles.Add(start + i);
            }
            e.HasHands = e.Triangles.Count > 0;
            e.Hands.Clear();
            if (!e.HasHands) return;
            e.Hands.indexFormat = e.Points.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            e.Hands.SetVertices(e.Points);
            e.Hands.SetTriangles(e.Triangles, 0, false);
            e.Hands.bounds = new Bounds(Vector3.zero, new Vector3(wall.Size.x, wall.Size.y, 2f) * 2f);
        }

        /// <summary>Draws the shadow and the echoes into the wall's mask texture.</summary>
        void DrawMask()
        {
            if (mask == null || shadowMaskMaterial == null) return;
            vertices.Clear();
            spots.Clear();
            ends.Clear();
            shapes.Clear();
            tapers.Clear();
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
                    c.RadiusB *= grow;
                    c.Soft += t * 0.05f;
                    c.SoftB += t * 0.05f;
                    AddQuad(c, 1 + k, strength);
                }
            }

            capsules.Clear();
            capsules.SetVertices(vertices);
            capsules.SetUVs(0, spots);
            capsules.SetUVs(1, ends);
            capsules.SetUVs(2, shapes);
            capsules.SetUVs(3, tapers);
            capsules.SetTriangles(triangles, 0);
            capsules.bounds = new Bounds(Vector3.zero, new Vector3(wall.Size.x, wall.Size.y, 2f) * 2f);

            var half = wall.Size * 0.5f;
            commands.Clear();
            commands.SetRenderTarget(mask);
            commands.ClearRenderTarget(false, true, Color.clear);
            commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(-half.x, half.x, -half.y, half.y, -1f, 1f));
            if (triangles.Count > 0) commands.DrawMesh(capsules, Matrix4x4.identity, shadowMaskMaterial, 0, 0);

            // The hands themselves: their projected meshes, solid, in the shadow channel.
            fills[0].SetVector(FillId, new Vector4(1f, 0f, 0f, 0f));
            foreach (var h in handShadows)
                if (h.Valid) commands.DrawMesh(h.Flat, Matrix4x4.identity, shadowMaskMaterial, 0, 1, fills[0]);

            // And their echoes, drifting up and growing from the middle of the pool like the capsules above.
            for (int k = 0; k < Echoes; k++)
            {
                var e = echoes[k];
                if (e.Age >= echoLife || !e.HasHands) continue;
                float t = e.Age / echoLife;
                float grow = 1f + t * 0.45f;
                var lift = upOnWall * (e.Age * 0.22f);
                float strength = Mathf.Pow(1f - t, 1.5f) * Mathf.Clamp01(e.Age / 0.05f);
                var fill = new Vector4(0f, k == 0 ? strength : 0f, k == 1 ? strength : 0f, k == 2 ? strength : 0f);
                fills[1 + k].SetVector(FillId, fill);
                var place = Matrix4x4.Translate(new Vector3(pool.x + lift.x, pool.y + lift.y, 0f))
                    * Matrix4x4.Scale(new Vector3(grow, grow, 1f)) * Matrix4x4.Translate(new Vector3(-pool.x, -pool.y, 0f));
                commands.DrawMesh(e.Hands, place, shadowMaskMaterial, 0, 1, fills[1 + k]);
            }
            Graphics.ExecuteCommandBuffer(commands);
        }

        void AddQuad(Cap c, int channel, float strength)
        {
            float pad = Mathf.Max(c.Radius + c.Soft, c.RadiusB + c.SoftB);
            var min = Vector2.Min(c.A, c.B) - Vector2.one * pad;
            var max = Vector2.Max(c.A, c.B) + Vector2.one * pad;
            int v = vertices.Count;
            var ab = new Vector4(c.A.x, c.A.y, c.B.x, c.B.y);
            var shape = new Vector4(c.Radius, c.Soft, channel, strength * c.Strength);
            var taper = new Vector2(c.RadiusB, c.SoftB);
            for (int k = 0; k < 4; k++)
            {
                var corner = new Vector2(k == 1 || k == 2 ? max.x : min.x, k >= 2 ? max.y : min.y);
                vertices.Add(new Vector3(corner.x, corner.y, 0f));
                spots.Add(corner);
                ends.Add(ab);
                shapes.Add(shape);
                tapers.Add(taper);
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

        /// <summary>
        /// How the shadow covers this point on the wall: 2 where a part that rings the lights covers it (the hands, arms,
        /// shoulders, neck or head), 1 where only the trunk or legs do, 0 where nothing does.
        /// </summary>
        int ShadowAt(Vector2 p)
        {
            int at = 0;
            for (int i = 0; i < liveCount; i++)
            {
                var c = live[i];
                // A part only half faded in doesn't ring anything yet.
                if (c.Strength < 0.5f) continue;
                bool rings = Rings(c.Part);
                if (at == 1 && !rings) continue;
                var ab = c.B - c.A;
                float h = Mathf.Clamp01(Vector2.Dot(p - c.A, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
                if ((p - c.A - ab * h).magnitude >= Mathf.Lerp(c.Radius + c.Soft * 0.3f, c.RadiusB + c.SoftB * 0.3f, h)) continue;
                if (rings) return 2;
                at = 1;
            }
            foreach (var s in handShadows)
                if (InHandShadow(s, p)) return 2;
            return at;
        }

        // The trunk and legs darken the wall but ring nothing: they would cover the middle of the pool all the time, and
        // the lights are the hands' to play.
        static bool Rings(ShadowPart part) => part < ShadowPart.Trunk;

        static bool InHandShadow(HandShadow s, Vector2 p)
        {
            if (!s.Valid || p.x < s.Min.x || p.y < s.Min.y || p.x > s.Max.x || p.y > s.Max.y) return false;
            var points = s.Points;
            var tris = s.Triangles;
            for (int i = 0; i < tris.Length; i += 3)
                if (InTriangle(p, points[tris[i]], points[tris[i + 1]], points[tris[i + 2]])) return true;
            return false;
        }

        static bool InTriangle(Vector2 p, Vector3 a, Vector3 b, Vector3 c)
        {
            // Quick reject on the triangle's box, then which side of each edge the point is on.
            if (p.x < Mathf.Min(a.x, Mathf.Min(b.x, c.x)) || p.x > Mathf.Max(a.x, Mathf.Max(b.x, c.x))) return false;
            if (p.y < Mathf.Min(a.y, Mathf.Min(b.y, c.y)) || p.y > Mathf.Max(a.y, Mathf.Max(b.y, c.y))) return false;
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f, positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
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

                int shade = ShadowAt(n.Position);
                bool covered = shade == 2;
                // While the menu is open the hands are choosing there: their shadows still fall, but ring nothing, and a
                // light still covered when the menu closes rings only once it is uncovered and covered again.
                if (covered && !n.Covered && n.Rest <= 0f && !MenuOpen)
                {
                    Acted("ring note");
                    float height = Vector2.Dot(n.Position - pool, upOnWall) / poolRadius * 0.5f + 0.5f;
                    var at = OnWall(n.Position, 0.02f);
                    int midi = Musical.Play(voices, music, 60 + Mathf.RoundToInt(Mathf.Clamp01(height) * 19f), at, 0.45f);
                    // The hand whose shadow rang it lights up (an arm's shadow lights none); the note is the sound.
                    for (int side = 0; side < 2; side++)
                        if (InHandShadow(handShadows[side], n.Position)) GestureFeedback.Light(side == 0, Fingers.All, 0.7f);
                    n.Ring = 1f;
                    n.Rest = 0.35f;
                    Splash(n.Position, NoteColor(midi));
                    // The word riding on this light fades away as the ring spreads (Sing).
                    if (n.Word != null && n.Fading < 0f) n.Fading = 0f;
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
                block.SetFloat(IntensityId, (0.25f + lit * 1.1f) * twinkle * (shade > 0 ? 0.35f : 1f) + n.Ring * 1.6f);
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
        /// <summary>
        /// A sung word lands on one of the lights: the best lit that carries no word and lies under no shadow, kept a
        /// little apart from the lights already carrying words while any are free. It rides along just above its light
        /// (Sing) until the light rings, or its life runs out.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            var w = SungWords.Add(word, new Color(1f, 0.9f, 0.75f), wordLife);
            // All words in use: the layer has taken the oldest back, so its light carries nothing now.
            foreach (var n in notes)
                if (n.Word == w) n.Word = null;
            Note best = null;
            float bestScore = float.MinValue;
            foreach (var n in notes)
            {
                if (n.Word != null || n.Covered) continue;
                float score = Lit(n.Position, 1f) + Random.value * 0.15f;
                foreach (var o in notes)
                    if (o.Word != null && (o.Position - n.Position).magnitude < wordSpacing) score -= 1f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = n;
                }
            }
            if (best == null)
            {
                // Every light busy or in shadow: the word shows above the pool and drifts up, as words did before.
                var upOnWall = new Vector2(Vector3.Dot(Vector3.up, wall.Right), Vector3.Dot(Vector3.up, wall.Up));
                w.Position = OnWall(pool + upOnWall * (poolRadius * 0.62f), 0.02f);
                w.Velocity = Vector3.up * 0.02f;
                return;
            }
            best.Word = w;
            best.Fading = -1f;
            w.Position = WordAt(best);
        }

        /// <summary>Where a light's word sits: just above the light, so the light still shows under it.</summary>
        Vector3 WordAt(Note n)
        {
            var upOnWall = new Vector2(Vector3.Dot(Vector3.up, wall.Right), Vector3.Dot(Vector3.up, wall.Up));
            return OnWall(n.Position + upOnWall * 0.032f, 0.02f);
        }

        /// <summary>
        /// The words face out of the wall and glow on the kick. Each rides just above its light; once the light has rung,
        /// the word fades slowly away and grows a little as the ring spreads, then lets the light go.
        /// </summary>
        void Sing(MusicFrame music, Camera view, float dt)
        {
            var facing = Quaternion.LookRotation(-wall.Normal, Vector3.up);
            // By index: a foreach over the read-only list would allocate an enumerator every frame.
            var words = SungWords.Words;
            for (int i = 0; i < words.Count; i++)
            {
                var w = words[i];
                if (!w.Active) continue;
                w.Position += w.Velocity * dt;
                w.Rotation = facing;
                w.Halo = 0.3f + music.Kick * 0.3f;
            }
            for (int i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                var w = n.Word;
                if (w == null) continue;
                // Its life ran out.
                if (!w.Active)
                {
                    n.Word = null;
                    continue;
                }
                w.Position = WordAt(n);
                if (n.Fading < 0f) continue;
                n.Fading += dt;
                float t = Mathf.Clamp01(n.Fading / Mathf.Max(wordFade, 0.01f));
                w.Alpha = 1f - Ease(t);
                w.Size = 1f + t * 0.25f;
                if (t < 1f) continue;
                // Gone: the layer puts it away, and the light can carry a new word.
                w.Life = w.Age;
                n.Word = null;
                n.Fading = -1f;
            }
        }

#if UNITY_EDITOR
        /// <summary>For the editor checks: how many capsules this frame's shadow has (the hands' own meshes aside).</summary>
        public int PreviewCapsuleCount => liveCount;

        /// <summary>For the editor checks: one of this frame's capsules, on the wall in metres along its Right and Up.</summary>
        public void PreviewCapsule(int i, out ShadowPart part, out bool left, out Vector2 a, out Vector2 b, out float radiusA,
            out float radiusB, out float strength)
        {
            var c = live[i];
            part = c.Part;
            left = c.Left;
            a = c.A;
            b = c.B;
            radiusA = c.Radius;
            radiusB = c.RadiusB;
            strength = c.Strength;
        }

        /// <summary>For the editor checks: where the lamp casts a point's shadow on the wall.</summary>
        public bool PreviewProject(Vector3 p, out Vector2 onWall) => Project(p, out onWall, out _);

        /// <summary>For the editor checks: whether a point on the wall is inside one hand's own shadow (0 left, 1 right).</summary>
        public bool PreviewInHandShadow(int side, Vector2 p) => InHandShadow(handShadows[side], p);

        /// <summary>For the editor checks: 2 where the shadow would ring a light here, 1 where only the trunk or legs cover it, else 0.</summary>
        public int PreviewShadowAt(Vector2 p) => ShadowAt(p);

        /// <summary>For the editor checks: sings a word, as the lyrics would.</summary>
        public void PreviewSing(string word) => OnWord(word, false);

        /// <summary>
        /// For the editor checks: the first word riding on a light, where it and its light are on the wall, and how much
        /// of it shows (0-1, as the layer draws it). False when no light carries a word.
        /// </summary>
        public bool PreviewWord(out Vector2 word, out Vector2 light, out float shown)
        {
            foreach (var n in notes)
            {
                var w = n.Word;
                if (w == null || !w.Active) continue;
                word = wall.Local(w.Position);
                light = n.Position;
                shown = Mathf.Clamp01(w.Age / 0.25f) * Mathf.Clamp01((w.Life - w.Age) / 0.6f) * w.Alpha;
                return true;
            }
            word = light = default;
            shown = 0f;
            return false;
        }

        /// <summary>For the editor checks: rings the light under the first word, as a shadow covering it would.</summary>
        public void PreviewRingWord()
        {
            foreach (var n in notes)
                if (n.Word != null && n.Fading < 0f)
                {
                    n.Fading = 0f;
                    return;
                }
        }
#endif
    }
}
