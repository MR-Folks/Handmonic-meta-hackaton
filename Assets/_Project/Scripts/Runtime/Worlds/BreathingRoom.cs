using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Breathing room world: the room and your hands move with the music. A faint glowing grid traces the walls and
    /// the floor around you; the walls breathe in and out with the bass and brighten on every kick, and rings on the
    /// floor flow in towards your feet. Your hands are made of the music: each fingertip carries a soft light tuned to
    /// one frequency band, sparks drift off the fingertips as they move, and quick finger movements leave short trails of
    /// light in the air. Every other beat a round wave leaves each palm in the colour of one of the fingers and rolls on
    /// across the wall the palm faces. Push your palm towards a wall (or touch it, if you walk up to it) and a wave rolls
    /// out across it from that spot with a low note. Each sung word flies from your palm to the wall ahead, and the line
    /// builds up there word by word. With a room scan the grid traces your real walls and floor; until then the walls
    /// stand a couple of metres around where you started. The pulse that runs through the hand itself lives in the hand
    /// shader (see HandSkin).
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class BreathingRoom : World
    {
        public override string DisplayName => "Breathing room";

        public override Color Color => new Color(0.75f, 0.6f, 1f);

        public override int MenuOrder => 0;

        public override int MenuIcon => 16;

        public override string Hint => "Spread your fingers, then push a palm towards a wall";

        [Header("Materials")]
        [WorldMaterial("RoomGrid")]
        [SerializeField] Material gridMaterial;
        [SerializeField] Material shellMaterial;
        [SerializeField] Material lineMaterial;

        [Header("Room")]
        [Tooltip("Half the width and depth of the stand-in room (m), and its height.")]
        [SerializeField] Vector2 halfSize = new Vector2(1.8f, 1.8f);
        [SerializeField] float roomHeight = 2.6f;
        [Tooltip("How far the walls breathe with the bass (share of their distance).")]
        [SerializeField] float breathe = 0.04f;

        [Header("Touch")]
        [Tooltip("Palm speed towards a wall (m/s) that sends a wave.")]
        [SerializeField] float pushSpeed = 0.7f;

        [Header("Fingertip lights")]
        [SerializeField] float tipSize = 0.045f;
        [Tooltip("Thumb to little finger. Each finger follows one band: bass, low mids, mids, presence, air.")]
        [SerializeField] Color[] fingerColors =
        {
            new Color(1f, 0.83f, 0.42f), new Color(0.49f, 0.95f, 0.79f), new Color(0.5f, 0.72f, 1f),
            new Color(0.79f, 0.61f, 1f), new Color(1f, 0.56f, 0.78f)
        };

        [Header("Palm waves")]
        [SerializeField] float waveRadius = 0.15f;
        [SerializeField] float waveLife = 1.1f;
        [SerializeField] int maxWaves = 6;
        [Tooltip("A wave leaves the palms once every this many beats.")]
        [SerializeField] int waveEveryBeats = 2;

        [Header("Lyrics")]
        [Tooltip("The distance (m) at which the letters are their true size, so they grow on a further wall to read the same, and how far above your eyes the line sits at that distance.")]
        [SerializeField] Vector2 lineSpot = new Vector2(0.9f, 0.09f);
        [Tooltip("How wide a row of words may get (m) at that distance before it wraps.")]
        [SerializeField] float lineWidth = 0.62f;
        [Tooltip("How long (s) a line stays after its last word starts.")]
        [SerializeField] float lineHold = 2.5f;

        [Header("Trails")]
        [Tooltip("Fingertip speed (m/s) where trails start to show, and where they are fully lit.")]
        [SerializeField] Vector2 trailSpeed = new Vector2(0.35f, 1.1f);

        [Header("Sparks")]
        [SerializeField] float sparksPerMetre = 70f;
        [SerializeField] int sparksPerBeat = 3;

        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int FloorId = Shader.PropertyToID("_Floor");
        static readonly int CenterId = Shader.PropertyToID("_Center");
        static readonly int BreathId = Shader.PropertyToID("_Breath");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int WavesId = Shader.PropertyToID("_Waves");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int AxisId = Shader.PropertyToID("_Axis");
        static readonly int PhaseId = Shader.PropertyToID("_Phase");

        // Longer than any line takes: a line stays until the next one, or until lineHold after its last word.
        const float LineLife = 600f;
        const float LetterHeight = 0.05f;

        /// <summary>A round wave swelling around a palm on the beat.</summary>
        sealed class PalmWave
        {
            public Transform Transform;
            public Renderer Renderer;
            public float Age;
            public float Strength;
            public Color Color;
            public Vector3 Axis;
        }

        // The room.
        readonly List<Renderer> walls = new List<Renderer>();
        // The walls at rest, facing into the room: the scanned walls or four stand-ins.
        readonly List<Room.Surface> surfaces = new List<Room.Surface>();
        Renderer floor;
        Room.Surface floorRect;
        // Waves rolling across the grid: world position and age (s), or age -1 when free.
        readonly Vector4[] waves = new Vector4[8];
        int nextWave;
        readonly float[] cooldown = new float[2];
        MaterialPropertyBlock block;
        AudioPool voices;
        MusicFrame frame;
        Vector3 centre, forward, right;
        float floorY, breath;
        bool scanned;

        // The hands.
        readonly List<PalmWave> palmWaves = new List<PalmWave>();
        readonly Transform[] tipLights = new Transform[10];
        readonly Renderer[] tipRenderers = new Renderer[10];
        Material tipMaterial;
        readonly Vector3[] lastTips = new Vector3[10];
        readonly bool[] hadTip = new bool[10];
        readonly float[] sparkDebt = new float[10];
        const int TrailPoints = 22;
        const float TrailStep = 0.018f;
        readonly LineStrip[] trails = new LineStrip[10];
        readonly Vector3[][] trailHistory = new Vector3[10][];
        readonly float[] trailClock = new float[10];
        readonly float[] trailStrength = new float[10];
        readonly Color[] trailColors = new Color[TrailPoints];
        int waveHue;
        int beats;
        ParticleSystem sparks;

        // The sung line: where each word of it lands, in metres right of and above the line's spot on the wall.
        readonly List<Vector2> lineSlots = new List<Vector2>();
        Vector3 lineAt, lineRight;
        Quaternion lineRotation = Quaternion.identity;
        float lineSize = 1f;
        bool lineShowing;

        protected override float FadeInSeconds => 1f / 1.2f;

        protected override int SungWordCount => 24;

        protected override float SungWordHeight => LetterHeight;

        protected override float SungWordsAlpha => FadeIn;

        /// <summary>Creates the grid, the fingertip lights, the palm wave pool and the spark system. Also run by the editor preview, through Build.</summary>
        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            floor = Effects.MakeQuad("Floor", transform, gridMaterial);
            floor.gameObject.SetActive(false);
            voices = new AudioPool(transform, 4);
            for (int i = 0; i < waves.Length; i++) waves[i] = new Vector4(0f, 0f, 0f, -1f);

            // Fingertip lights are drawn on top of the hand: the finger's own surface is closer to the viewer than
            // the light and would otherwise cut it off. They are light, so showing through the hand looks right.
            tipMaterial = glowMaterial;
            if (glowMaterial != null)
            {
                tipMaterial = new Material(glowMaterial) { name = "Fingertip Glow" };
                tipMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                tipMaterial.renderQueue = glowMaterial.renderQueue + 20;
            }
            for (int i = 0; i < tipLights.Length; i++) MakeTipLight(i);

            for (int i = 0; i < maxWaves; i++)
            {
                var shell = Make(PrimitiveType.Sphere, $"Wave {i}", shellMaterial);
                shell.SetActive(false);
                palmWaves.Add(new PalmWave { Transform = shell.transform, Renderer = shell.GetComponent<Renderer>(), Age = -1f });
            }

            for (int i = 0; i < trails.Length; i++)
            {
                trails[i] = new LineStrip($"Trail {i}", transform, lineMaterial, TrailPoints);
                trails[i].SetVisible(false);
                trailHistory[i] = new Vector3[TrailPoints];
            }

            sparks = Effects.MakeSparkSystem("Sparks", transform, glowMaterial);
            if (SungWords != null) SungWords.Rim = 0.9f;
        }

        protected override void OnExit()
        {
            foreach (var w in walls) w.gameObject.SetActive(false);
            floor.gameObject.SetActive(false);

            // The fingertip lights ride on the hand joints, outside this object. Only hide them here: this runs while
            // the world is being switched off, and Unity refuses to move anything under an object at that moment.
            // UpdateHand takes a light back whenever its finger has no joint.
            for (int i = 0; i < tipLights.Length; i++)
            {
                if (tipLights[i] != null) tipLights[i].gameObject.SetActive(false);
                hadTip[i] = false;
            }
            foreach (var wave in palmWaves) wave.Transform.gameObject.SetActive(false);
            foreach (var trail in trails) trail?.SetVisible(false);
            System.Array.Clear(trailStrength, 0, trailStrength.Length);
            if (sparks != null) sparks.Clear();
            lineShowing = false;
        }

        /// <summary>Finds the walls and floor around where you stand and shows the grid on them.</summary>
        protected override void OnPlace(Camera view)
        {
            centre = view.transform.position;
            forward = Effects.FlatForward(view);
            right = Vector3.Cross(Vector3.up, forward).normalized;
            floorY = Effects.FloorY(view);
            MeasureRoom();
            for (int i = 0; i < walls.Count; i++) walls[i].gameObject.SetActive(i < surfaces.Count);
            floor.gameObject.SetActive(true);
        }

        /// <summary>
        /// Each kick sends a soft ring out from under your feet. Every few beats a wave also leaves each palm in the next
        /// finger's colour and rolls on across the wall it faces.
        /// </summary>
        protected override void OnBeat(MusicFrame music)
        {
            AddWave(new Vector3(centre.x, floorY, centre.z), false);
            if (++beats % Mathf.Max(1, waveEveryBeats) != 0) return;
            waveHue = (waveHue + 1) % fingerColors.Length;
            // While the menu is open the palms are choosing there: no waves burst out among its bubbles and on across the
            // room. The fingertip lights, trails and sparks go on.
            if (MenuOpen) return;
            float strength = 0.6f + 0.4f * music.Kick;
            if (Rig.Left.IsTracked) SpawnWave(Rig.Left, View, strength);
            if (Rig.Right.IsTracked) SpawnWave(Rig.Right, View, strength);
            if (Rig.Left.IsTracked || Rig.Right.IsTracked) Acted("palm wave");
        }

        /// <summary>
        /// Each sung word leaves a palm, turns as it flies and lands in its place on the wall ahead of you. The line's
        /// place is chosen when its first word starts and then stays put, so the row builds up word by word and is still
        /// to read.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            if (SungWords == null) return;
            if (newLine) PlanLine(Music.LineWords, View);
            Launch(Rig, word, View);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            frame = music;
            breath = Mathf.Lerp(breath, Mathf.Clamp01(music.Band(1) * 0.8f + music.Kick * 0.5f), 1f - Mathf.Exp(-8f * dt));

            for (int i = 0; i < waves.Length; i++) if (waves[i].w >= 0f) waves[i].w += dt;
            if (rig != null)
            {
                Push(rig, dt);
                UpdateHand(rig.Left, 0, music.Bands, music.Kick, view, dt, music.Beat);
                UpdateHand(rig.Right, 1, music.Bands, music.Kick, view, dt, music.Beat);
            }
            AdvancePalmWaves(dt);
            Place(music);
            Sing(music, view, dt);
        }

        // ---- The room ----

        /// <summary>
        /// Finds the walls: the scanned ones, or four stand-ins around you (front, right, back, left). The floor
        /// grid covers the space between them.
        /// </summary>
        void MeasureRoom()
        {
            scanned = Room.Around(centre, forward, floorY, halfSize, roomHeight, surfaces, out floorRect);
            while (walls.Count < surfaces.Count)
            {
                var quad = Effects.MakeQuad($"Wall {walls.Count}", transform, gridMaterial);
                quad.gameObject.SetActive(false);
                walls.Add(quad);
            }
        }

        /// <summary>The walls stand where they are, pushed out a little with each breath.</summary>
        void Place(MusicFrame music)
        {
            float s = 1f + breathe * breath;
            for (int i = 0; i < surfaces.Count; i++)
            {
                var surface = surfaces[i];
                // Along the wall's normal, by a share of its distance from where you stand: stand-in walls breathe out,
                // real walls breathe in from their surface (they cannot go behind it).
                float away = Mathf.Max(0.3f, Vector3.Dot(centre - surface.Center, surface.Normal));
                float shift = scanned ? 0.01f + away * breathe * breath : -away * breathe * breath;
                var size = new Vector2(surface.Size.x * s, surface.Size.y);
                var w = walls[i];
                w.transform.SetPositionAndRotation(surface.Center + surface.Normal * shift, Quaternion.LookRotation(-surface.Normal, Vector3.up));
                w.transform.localScale = new Vector3(size.x, size.y, 1f);
                Apply(w, size, false);
            }
            var floorSize = floorRect.Size * s;
            floor.transform.SetPositionAndRotation(floorRect.Center + Vector3.up * 0.003f, Quaternion.LookRotation(Vector3.down, floorRect.Up));
            floor.transform.localScale = new Vector3(floorSize.x, floorSize.y, 1f);
            Apply(floor, floorSize, true);
        }

        void Apply(Renderer r, Vector2 size, bool isFloor)
        {
            block.Clear();
            block.SetVector(SizeId, size);
            block.SetFloat(FloorId, isFloor ? 1f : 0f);
            block.SetVector(CenterId, centre);
            block.SetFloat(BreathId, breath);
            block.SetFloat(ClockId, Now);
            block.SetFloat(AlphaId, FadeIn);
            block.SetVectorArray(WavesId, waves);
            r.SetPropertyBlock(block);
        }

        /// <summary>
        /// A palm pushed quickly towards a wall, or touching it, sends a wave out across the wall from where it points.
        /// </summary>
        void Push(HandRig rig, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                cooldown[side] -= dt;
                var hand = rig.Get(side == 0);
                // A palm pressing at the open menu isn't pushing a wall.
                if (!hand.IsTracked || MenuOpen || cooldown[side] > 0f || hand.PalmNormal.sqrMagnitude < 0.5f) continue;
                var palm = PalmOf(hand);
                var normal = hand.PalmNormal;
                if (Mathf.Abs(normal.y) > 0.7f) continue;
                float towards = Vector3.Dot(hand.Velocity, normal);
                bool pushing = towards > pushSpeed;
                if (!Hit(palm, normal, out var hit, out float distance)) continue;
                if (!pushing && distance > 0.12f) continue;
                AddWave(hit, true);
                cooldown[side] = 0.45f;
                Acted("wall wave");
            }
        }

        /// <summary>Where a ray meets a wall.</summary>
        bool Hit(Vector3 from, Vector3 direction, out Vector3 hit, out float distance) =>
            Room.Hit(surfaces, from, direction, out hit, out distance);

        /// <summary>Where a ray meets a wall, and which wall it is.</summary>
        bool Aim(Vector3 from, Vector3 direction, out Room.Surface wall, out Vector3 hit, out float distance)
        {
            wall = default;
            if (!Hit(from, direction, out hit, out distance)) return false;
            float nearest = float.MaxValue;
            foreach (var face in surfaces)
            {
                float off = Mathf.Abs(Vector3.Dot(hit - face.Center, face.Normal));
                if (off < nearest) { nearest = off; wall = face; }
            }
            return true;
        }

        void AddWave(Vector3 at, bool pressed)
        {
            waves[nextWave] = new Vector4(at.x, at.y, at.z, 0f);
            nextWave = (nextWave + 1) % waves.Length;
            if (pressed) Musical.Play(voices, frame, 57, at, 0.5f);
        }

        // ---- The sung words ----

        /// <summary>
        /// The line's words fly to their places on the wall, and the line fades once it is over. The word being sung
        /// takes the wave's colour; the rest settle to white.
        /// </summary>
        void Sing(MusicFrame music, Camera view, float dt)
        {
            if (SungWords == null || view == null) return;
            var sung = music.LineWords;
            if (lineShowing && (sung == null || music.SongTime > sung[sung.Length - 1].Time + lineHold)) FadeLine();

            var eye = view.transform.position;
            var bright = Color.Lerp(fingerColors[waveHue], Color.white, 0.2f);
            float step = 1f - Mathf.Exp(-4.5f * dt);
            foreach (var w in SungWords.Words)
            {
                // Words of an earlier line stay where they landed while they fade.
                if (!w.Active || w.Line != music.Lyric.Line) continue;
                var target = lineAt + lineRight * w.Spot.x + Vector3.up * w.Spot.y;
                w.Position = Vector3.Lerp(w.Position, target, step);
                w.Size = Mathf.Lerp(w.Size, lineSize, step);
                w.Rotation = Quaternion.Slerp(WordLayer.Facing(w.Position, eye), lineRotation, Mathf.SmoothStep(0f, 1f, w.Age / 0.8f));
                w.Color = Color.Lerp(w.Color, w.Slot == music.Lyric.Word ? bright : Color.white, 1f - Mathf.Exp(-14f * dt));
                // Glowing in flight; the glow gives way to the dark edge that makes it readable on the wall.
                w.Halo = Mathf.Max(0f, 0.4f - w.Age * 0.5f);
            }
        }

        /// <summary>Picks the line's spot on the wall ahead of you and where each of its words will land.</summary>
        void PlanLine(LyricsTrack.Word[] sung, Camera view)
        {
            FadeLine();
            lineSlots.Clear();
            if (sung == null) return;
            lineShowing = true;

            var eye = view.transform.position;
            var ahead = Effects.FlatForward(view);
            if (!Aim(eye, ahead, out var wall, out var hit, out float distance))
            {
                distance = 2f;
                hit = eye + ahead * distance;
                wall = new Room.Surface(hit, -ahead, Vector3.Cross(Vector3.up, ahead), new Vector2(6f, 3f));
            }
            lineSize = Mathf.Clamp(distance / lineSpot.x, 1f, 3.5f);
            lineRotation = Quaternion.LookRotation(-wall.Normal, Vector3.up);
            lineRight = lineRotation * Vector3.right;

            // Slide along the wall if the row would run off its end.
            float width = lineWidth * lineSize;
            float spare = Mathf.Max(0f, wall.Size.x * 0.5f - width * 0.5f);
            float along = Mathf.Clamp(wall.Local(hit).x, -spare, spare);
            lineAt = hit + wall.Right * (along - wall.Local(hit).x) + wall.Normal * 0.02f + Vector3.up * (lineSpot.y * lineSize);

            // Centred rows, each at most lineWidth wide.
            float height = LetterHeight * lineSize, gap = height * 0.3f, y = 0f;
            for (int first = 0; first < sung.Length;)
            {
                float rowWidth = -gap;
                int last = first;
                for (; last < sung.Length; last++)
                {
                    float add = TypeFace.Bold.Width(sung[last].Text) * height + gap;
                    if (last > first && rowWidth + add > width) break;
                    rowWidth += add;
                }
                float x = -rowWidth * 0.5f;
                for (int i = first; i < last; i++)
                {
                    float wide = TypeFace.Bold.Width(sung[i].Text) * height;
                    lineSlots.Add(new Vector2(x + wide * 0.5f, y));
                    x += wide + gap;
                }
                y -= height * 1.4f;
                first = last;
            }
        }

        /// <summary>Sends a word off from a palm, taking turns when both hands are tracked.</summary>
        void Launch(HandRig rig, string text, Camera view)
        {
            int index = Sung.Index;
            var w = SungWords.Add(text, Color.white, LineLife);
            w.Slot = index;
            w.Spot = index < lineSlots.Count ? lineSlots[index] : Vector2.zero;
            w.Size = 0.7f;
            var hand = rig.Left.IsTracked && (!rig.Right.IsTracked || index % 2 == 0) ? rig.Left : rig.Right;
            var eye = view.transform.position;
            w.Position = hand.IsTracked ? PalmOf(hand) + hand.PalmNormal * 0.04f : eye + Effects.FlatForward(view) * 0.35f + Vector3.down * 0.25f;
            w.Rotation = WordLayer.Facing(w.Position, eye);
        }

        void FadeLine()
        {
            foreach (var w in SungWords.Words)
                if (w.Active) w.Life = Mathf.Min(w.Life, w.Age + 0.6f);
            lineShowing = false;
        }

        // ---- The hands ----

        static Vector3 PalmOf(HandRig.Hand hand) => hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;

        void MakeTipLight(int i)
        {
            var quad = Make(PrimitiveType.Quad, $"Tip Light {i}", tipMaterial);
            tipLights[i] = quad.transform;
            tipRenderers[i] = quad.GetComponent<Renderer>();
            quad.SetActive(false);
        }

        void UpdateHand(HandRig.Hand hand, int side, float[] bands, float kick, Camera view, float dt, bool beat)
        {
            for (int f = 0; f < 5; f++)
            {
                int i = side * 5 + f;
                // A light left on a hand joint goes with it if the hand is ever rebuilt; make a new one.
                if (tipLights[i] == null) MakeTipLight(i);
                var light = tipLights[i];
                bool show = hand.IsTracked;
                if (light.gameObject.activeSelf != show) light.gameObject.SetActive(show);
                if (!show)
                {
                    hadTip[i] = false;
                    trailStrength[i] = 0f;
                    trails[i].SetVisible(false);
                    continue;
                }

                var joint = hand.TipJoints[f];
                Vector3 tip = joint != null ? joint.position : hand.Tips[f];
                if (joint != null && light.parent != joint) light.SetParent(joint, false);
                else if (joint == null && light.parent != transform) light.SetParent(transform, false);

                float level = bands != null ? bands[Mathf.Min(f + 1, bands.Length - 1)] : 0.3f;
                float size = tipSize * (0.75f + level * 0.9f + kick * 0.35f);
                float scale = joint != null ? Mathf.Max(joint.lossyScale.x, 1e-4f) : 1f;
                light.localScale = Vector3.one * (size / scale);

                if (view != null)
                {
                    // Face the viewer and sit just in front of the fingertip, so the hand never hides the light.
                    var toView = (view.transform.position - tip).normalized;
                    light.position = tip + toView * 0.007f;
                    light.rotation = Quaternion.LookRotation(-toView, view.transform.up);
                }
                else light.position = tip;

                block.Clear();
                block.SetColor(ColorId, fingerColors[f]);
                block.SetFloat(IntensityId, 0.45f + level * 1.1f + kick * 0.4f);
                tipRenderers[i].SetPropertyBlock(block);

                DrawTrail(i, f, tip, level, view, dt);
                EmitSparks(i, f, tip, beat);
            }
        }

        /// <summary>
        /// A short ribbon of light behind a fingertip that moves quickly: it fades in with speed, tapers to nothing at
        /// its tail and fades away again when the finger slows down.
        /// </summary>
        void DrawTrail(int i, int finger, Vector3 tip, float level, Camera view, float dt)
        {
            var history = trailHistory[i];
            if (!hadTip[i])
            {
                for (int k = 0; k < TrailPoints; k++) history[k] = tip;
                trailClock[i] = 0f;
            }
            float speed = hadTip[i] && dt > 0f ? Vector3.Distance(tip, lastTips[i]) / dt : 0f;
            float target = Mathf.InverseLerp(trailSpeed.x, trailSpeed.y, speed);
            trailStrength[i] = Mathf.Lerp(trailStrength[i], target, 1f - Mathf.Exp(-(target > trailStrength[i] ? 14f : 3f) * dt));

            trailClock[i] += dt;
            while (trailClock[i] >= TrailStep)
            {
                trailClock[i] -= TrailStep;
                for (int k = TrailPoints - 1; k > 0; k--) history[k] = history[k - 1];
            }
            history[0] = tip;

            bool show = trailStrength[i] > 0.02f && view != null;
            trails[i].SetVisible(show);
            if (!show) return;
            var color = fingerColors[finger];
            for (int k = 0; k < TrailPoints; k++)
            {
                float u = k / (float)(TrailPoints - 1);
                var c = Color.Lerp(Color.white, color, Mathf.Min(1f, u * 3f));
                c.a = (1f - u) * (1f - u) * trailStrength[i];
                trailColors[k] = c;
            }
            trails[i].Set(history, trailColors, TrailPoints, 0.006f * (1f + level * 0.6f), Color.white, view.transform.position, false);
        }

        void EmitSparks(int i, int finger, Vector3 tip, bool beat)
        {
            if (sparks == null) return;
            int count = 0;
            if (hadTip[i])
            {
                sparkDebt[i] += Vector3.Distance(tip, lastTips[i]) * sparksPerMetre;
                count = (int)sparkDebt[i];
                sparkDebt[i] -= count;
            }
            if (beat) count += sparksPerBeat;
            lastTips[i] = tip;
            hadTip[i] = true;

            for (int k = 0; k < Mathf.Min(count, 12); k++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = tip + Random.insideUnitSphere * 0.006f,
                    velocity = Random.insideUnitSphere * (beat ? 0.06f : 0.02f) + Vector3.up * 0.015f,
                    startColor = fingerColors[finger],
                    startSize = Random.Range(0.005f, 0.012f),
                    startLifetime = Random.Range(0.8f, 1.6f)
                };
                sparks.Emit(emit, 1);
            }
        }

        void SpawnWave(HandRig.Hand hand, Camera view, float strength)
        {
            SpawnShell(hand, strength);
            SendToWall(hand, view);
        }

        /// <summary>
        /// Carries the beat on across the room as a wave on the grid: the way the palm faces when it faces away from you,
        /// so you can aim it; past the hand when it faces you, so the wave still lands somewhere you can see.
        /// </summary>
        void SendToWall(HandRig.Hand hand, Camera view)
        {
            if (view == null) return;
            var palm = PalmOf(hand);
            var outward = (palm - view.transform.position).normalized;
            var direction = Vector3.Dot(hand.PalmNormal, outward) > 0.2f ? hand.PalmNormal : outward;
            if (Hit(palm, direction, out var hit, out _)) AddWave(hit, false);
        }

        void SpawnShell(HandRig.Hand hand, float strength)
        {
            PalmWave wave = null;
            foreach (var w in palmWaves)
            {
                if (!w.Transform.gameObject.activeSelf) { wave = w; break; }
            }
            if (wave == null) return;

            var normal = hand.PalmNormal.sqrMagnitude > 0.5f ? hand.PalmNormal : Vector3.forward;
            wave.Transform.SetParent(transform, false);
            wave.Transform.position = PalmOf(hand) + normal * 0.015f;
            wave.Age = 0f;
            wave.Strength = strength;
            wave.Color = fingerColors[waveHue];
            wave.Axis = normal;
            wave.Transform.gameObject.SetActive(true);
            ApplyShell(wave);
        }

        void AdvancePalmWaves(float dt)
        {
            foreach (var wave in palmWaves)
            {
                if (!wave.Transform.gameObject.activeSelf) continue;
                wave.Age += dt;
                if (wave.Age >= waveLife)
                {
                    wave.Transform.gameObject.SetActive(false);
                    continue;
                }
                ApplyShell(wave);
            }
        }

        void ApplyShell(PalmWave wave)
        {
            float u = Mathf.Clamp01(wave.Age / waveLife);
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            wave.Transform.localScale = Vector3.one * (Mathf.Lerp(0.03f, waveRadius, eased) * 2f);
            block.Clear();
            // Each beat's wave takes the next finger's colour, starting bright and fading as it swells around the hand.
            block.SetColor(ColorId, Color.Lerp(Color.Lerp(wave.Color, Color.white, 0.35f), wave.Color, Mathf.SmoothStep(0f, 1f, u * 2f)));
            block.SetVector(AxisId, wave.Axis);
            block.SetFloat(PhaseId, eased * 9f);
            block.SetFloat(AlphaId, Mathf.Pow(1f - u, 3f) * wave.Strength * 0.3f);
            wave.Renderer.SetPropertyBlock(block);
        }

        GameObject Make(PrimitiveType shape, string objectName, Material material)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = objectName;
            DebugShapes.RemoveCollider(go);
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }
}
