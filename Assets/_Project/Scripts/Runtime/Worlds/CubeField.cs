using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MusicInYourHands
{
    /// <summary>
    /// The Cube field world: a field of tall blocks of coloured light stands on the floor all around you, out to about
    /// four metres. They rise and fall with the music, each to its own height and in its own time, so the whole field
    /// undulates. Right around you they stay low, barely above the floor, and the further away they stand the higher
    /// they can climb and the wider they swing. The music's bands are spread around you, the low ones in pink on your
    /// left, the middle in violet ahead and behind, the high ones in blue on your right, and each block moves and glows
    /// with its band. Every beat sends a ripple of light out from your feet that bumps the blocks up as it passes.
    /// Raise your hands and the field rises with them, up to twice its height with your hands above your head; lower
    /// them to your hips and it sinks almost flat. Each hand leans its own side of the field, the hands light up as
    /// they lift, and soft notes ring as the field passes each level. Each sung word stands on top of a block in front
    /// of you, which rises to hold it up and rides up and down with the music. With a room scan the field stops at
    /// your walls and leaves the floor under your furniture clear.
    /// </summary>
    public class CubeField : World
    {
        public override string DisplayName => "Cube field";

        public override Color Color => new Color(0.6f, 0.5f, 1f);

        public override int MenuOrder => 117;

        public override int MenuIcon => 27;

        public override string Hint => "Raise your hands to lift the blocks";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.09f, 0.08f, 0.22f, 0.5f),
            Edge = new Color(0.58f, 0.6f, 1f),
            Glow = new Color(1f, 0.4f, 0.72f),
            Pattern = HandTheme.Effect.Ripples,
            PatternColor = new Color(0.4f, 0.9f, 1f)
        };

        /// <summary>The most blocks the shader takes: the size of _Blocks in CubeField.shader.</summary>
        public const int MaxBlocks = 640;

        [Header("Materials")]
        [SerializeField] Material cubeFieldMaterial;

        [Header("Field")]
        [Tooltip("How far the field reaches out from where you stand (m).")]
        [SerializeField] float radius = 4.2f;
        [Tooltip("From one block to the next (m).")]
        [SerializeField] float spacing = 0.3f;
        [Tooltip("How wide each block is (m).")]
        [SerializeField] float blockWidth = 0.17f;
        [Tooltip("The floor where you stand stays clear of blocks, this far around you (m).")]
        [SerializeField] float clearing = 0.45f;
        [Tooltip("Seconds the field takes to grow out of the floor around you.")]
        [SerializeField] float growSeconds = 1.4f;

        [Header("Heights")]
        [Tooltip("The tallest a block may stand right next to you and at the far edge (m), with your hands at chest height.")]
        [SerializeField] float nearHeight = 0.02f;
        [SerializeField] float farHeight = 1.5f;
        [Tooltip("Distances from you (m) over which blocks go from their near height to their far height. In a scanned " +
                 "room closer than that, they reach their (lower) full height at the walls.")]
        [SerializeField] Vector2 growDistance = new Vector2(0.7f, 4f);
        [Tooltip("How the height climbs with distance: above 1, blocks stay low for longer before they grow.")]
        [SerializeField] float steepness = 1.75f;
        [Tooltip("How quickly blocks rise to their height, and sink back (per second).")]
        [SerializeField] float riseSpeed = 9f;
        [SerializeField] float fallSpeed = 3.5f;
        [Tooltip("No block grows taller than this (m), nor into a scanned ceiling.")]
        [SerializeField] float maxHeight = 2.5f;

        [Header("Beat ripple")]
        [Tooltip("How fast each beat's ripple runs out from your feet (m/s), and how wide it is (m).")]
        [SerializeField] float rippleSpeed = 3.4f;
        [SerializeField] float rippleWidth = 0.45f;
        [Tooltip("How far the ripple lifts a block, as a share of the tallest it may stand.")]
        [SerializeField] float rippleLift = 0.3f;

        [Header("Hands")]
        [Tooltip("How tall the field stands with your hands at your hips or lower (x), and above your head (y).")]
        [SerializeField] Vector2 liftRange = new Vector2(0.3f, 2f);
        [Tooltip("How far below your eyes your hips are (m).")]
        [SerializeField] float hipsBelowEyes = 0.75f;
        [Tooltip("How far above your eyes counts as over your head (m).")]
        [SerializeField] float overHead = 0.12f;
        [Tooltip("How quickly the field follows your hands (per second).")]
        [SerializeField] float liftResponse = 3f;
        [Tooltip("How loud the soft notes are that ring as the field passes each level.")]
        [SerializeField] float chimeVolume = 0.22f;

        [Header("Words")]
        [Tooltip("How far in front of you the sung words stand (m); every other word stands a step further back.")]
        [SerializeField] float wordDistance = 2f;
        [SerializeField] float wordStagger = 0.3f;
        [Tooltip("The angle between one word of a line and the next (degrees), and the most a line fans out to either side.")]
        [SerializeField] float wordSpread = 12f;
        [SerializeField] float lineSpread = 32f;
        [Tooltip("How high a block carrying a word rises at least (m), with your hands at chest height.")]
        [SerializeField] float wordStand = 0.5f;
        [Tooltip("Seconds each word stays.")]
        [SerializeField] float wordLife = 4.5f;

        static readonly int BlocksId = Shader.PropertyToID("_Blocks");
        static readonly int WidthId = Shader.PropertyToID("_Width");
        // The levels of lift that ring a note as your hands pass them, and the notes (fitted to the song as they play).
        static readonly float[] ChimeLevels = { 0.6f, 1f, 1.4f, 1.8f };
        static readonly int[] ChimeNotes = { 57, 60, 64, 67 };

        struct Ripple
        {
            public Vector2 Origin;
            public float Age, Strength;
        }

        protected override int SungWordCount => 6;

        protected override float SungWordHeight => 0.11f;

        protected override float SungWordsAlpha => FadeIn;

        Transform field;
        MeshRenderer fieldRenderer;
        MaterialPropertyBlock block;
        AudioPool voices;

        // Per block, nearest the middle first, as in the mesh.
        int count;
        Vector2[] spots;
        float[] middle, rim, build, swing, pace, phase, band, rest, hold;
        // Where it starts to climb (m from you), where it reaches its full height, and that height: nearer and lower
        // where a scanned wall is close.
        float[] climbFrom, climbTo, full;
        bool[] open, wordOn;
        Vector4[] data;

        // The words standing this frame: where their blocks are from you, which blocks, and how far each has come in.
        readonly Vector2[] sights = new Vector2[8];
        readonly int[] sightSlot = new int[8];
        readonly float[] sightHold = new float[8];

        readonly Ripple[] ripples = new Ripple[4];
        int nextRipple;
        readonly float[] levels = new float[6];
        float liftL = 1f, liftR = 1f;
        int chimeLevel = 1;
        float chimeWait;

        float floorY, grow, ceiling = float.MaxValue;
        bool moving;
        Vector3 nextCentre, nextForward;
        float nextFloor;

        protected override float FadeInSeconds => 0.8f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            voices = new AudioPool(transform, 3);
            field = new GameObject("Field").transform;
            field.SetParent(transform, false);
            field.gameObject.AddComponent<MeshFilter>().sharedMesh = BuildMesh();
            fieldRenderer = field.gameObject.AddComponent<MeshRenderer>();
            fieldRenderer.sharedMaterial = cubeFieldMaterial != null ? cubeFieldMaterial : FallbackMaterial();
            fieldRenderer.shadowCastingMode = ShadowCastingMode.Off;
            fieldRenderer.receiveShadows = false;
            fieldRenderer.lightProbeUsage = LightProbeUsage.Off;
            fieldRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Restart();
        }

        protected override void OnEnter() => Restart();

        protected override void OnExit()
        {
            HandSkin.Charge[0] = HandSkin.Charge[1] = 0f;
        }

        /// <summary>A fresh start: the field grows out of the floor again, at the hands' resting height.</summary>
        void Restart()
        {
            grow = 0f;
            moving = false;
            liftL = liftR = 1f;
            chimeLevel = LevelOf(1f);
            for (int i = 0; i < ripples.Length; i++) ripples[i].Age = -1f;
            System.Array.Clear(levels, 0, levels.Length);
            if (rest != null) System.Array.Clear(rest, 0, count);
            if (hold != null) System.Array.Clear(hold, 0, count);
        }

        /// <summary>
        /// The field stands around where you are. When it starts it grows at once; after you move, it sinks into the
        /// floor and grows again around you.
        /// </summary>
        protected override void OnPlace(Camera view)
        {
            nextCentre = view.transform.position;
            nextForward = Effects.FlatForward(view);
            nextFloor = Effects.FloorY(view);
            if (grow <= 0f)
            {
                Settle();
                return;
            }
            moving = true;
            ClearWords();
        }

        /// <summary>The words go down with the old field.</summary>
        void ClearWords()
        {
            var words = SungWords != null ? SungWords.Words : null;
            for (int k = 0; words != null && k < words.Count; k++)
                if (words[k].Active) words[k].Life = Mathf.Min(words[k].Life, words[k].Age + 0.4f);
        }

        /// <summary>Each beat sends a ripple out from your feet, bumping the blocks up as it passes.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            var feet = field.InverseTransformPoint(View.transform.position);
            ripples[nextRipple] = new Ripple
            {
                Origin = new Vector2(feet.x, feet.z),
                Age = 0f,
                Strength = 0.6f + 0.4f * Mathf.Clamp01(music.Kick)
            };
            nextRipple = (nextRipple + 1) % ripples.Length;
        }

        /// <summary>
        /// Each sung word stands on top of a block in front of you, the words of a line fanning out from left to
        /// right as they are sung, every other one a step further back.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            int i = PickBlock(Sung.Index, Sung.LineTexts.Length, Sung.Line);
            if (i < 0) return;
            var w = SungWords.Add(word, Color.Lerp(Color, Color.white, 0.6f), wordLife);
            w.Slot = i;
            var spot = field.TransformPoint(new Vector3(spots[i].x, 0f, spots[i].y));
            float distance = Vector3.ProjectOnPlane(spot - View.transform.position, Vector3.up).magnitude;
            w.Size = Mathf.Clamp(distance / wordDistance, 0.85f, 1.3f);
            wordOn[i] = true;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Grow(dt);
            Lift(rig, music, view, dt);
            Listen(music, dt);
            for (int k = 0; k < ripples.Length; k++)
            {
                if (ripples[k].Age < 0f) continue;
                ripples[k].Age += dt;
                if (ripples[k].Age > 2.2f) ripples[k].Age = -1f;
            }
            MarkWords();
            Shape(view, dt);
            PlaceWords(music, view);
            block.SetVectorArray(BlocksId, data);
            block.SetFloat(WidthId, blockWidth);
            fieldRenderer.SetPropertyBlock(block);
        }

        /// <summary>The field grows out of the floor from the middle outwards; while it moves, it sinks first.</summary>
        void Grow(float dt)
        {
            if (moving)
            {
                grow = Mathf.MoveTowards(grow, 0f, dt * 2.5f);
                if (grow <= 0f) Settle();
            }
            else grow = Mathf.MoveTowards(grow, 1f, dt / Mathf.Max(0.05f, growSeconds));
        }

        /// <summary>Stands the field on the floor around where you are, lined up with the way you face.</summary>
        void Settle()
        {
            moving = false;
            floorY = nextFloor;
            field.SetPositionAndRotation(new Vector3(nextCentre.x, floorY, nextCentre.z), Quaternion.LookRotation(nextForward, Vector3.up));
            ceiling = Room.Ceiling(float.MaxValue) - floorY - 0.08f;
            MeasureRoom();
            System.Array.Clear(rest, 0, count);
            for (int k = 0; k < ripples.Length; k++) ripples[k].Age = -1f;
            ClearWords();
        }

        /// <summary>
        /// With a room scan, the field stops at your walls and leaves the floor under your furniture clear, and in a
        /// room smaller than the field the blocks reach their full height (a little lower) by the walls, so even a small
        /// room gets a skyline around it.
        /// </summary>
        void MeasureRoom()
        {
            var centre = field.position;
            var from = centre + Vector3.up * 0.4f;
            bool walls = Room.Scanned && Room.Walls.Count > 0;
            var tops = Room.Tops;
            float margin = blockWidth * 0.5f + 0.05f;
            for (int i = 0; i < count; i++)
            {
                var p = field.TransformPoint(new Vector3(spots[i].x, 0f, spots[i].y));
                bool clear = true;
                float reach = growDistance.y;
                if (walls)
                {
                    var flat = new Vector3(p.x - centre.x, 0f, p.z - centre.z);
                    float d = flat.magnitude;
                    if (d > 1e-3f && Room.Hit(Room.Walls, from, flat / d, out _, out float wall))
                    {
                        if (wall < d + margin) clear = false;
                        // Never so close that the blocks near you grow tall: they still start low.
                        reach = Mathf.Clamp(wall, growDistance.x + 1.3f, growDistance.y);
                    }
                }
                for (int k = 0; clear && k < tops.Count; k++)
                    if (tops[k].Center.y > floorY + 0.12f && tops[k].Contains(p, -margin)) clear = false;
                open[i] = clear;
                climbTo[i] = reach;
                climbFrom[i] = growDistance.x;
                full[i] = farHeight * Mathf.Sqrt(reach / Mathf.Max(0.1f, growDistance.y));
            }
        }

        /// <summary>
        /// Your hands set how tall the field stands: each hand's height, from your hips (low) to above your head
        /// (tall), eased so the field follows smoothly. A hand out of view goes along with the other; with neither in
        /// view, the field stands as tall as it does with your hands at chest height. The hands light up as they lift.
        /// </summary>
        void Lift(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            float eye = view.transform.position.y;
            bool left = rig != null && rig.Left.IsTracked, right = rig != null && rig.Right.IsTracked;
            float l = left ? LiftOf(rig.Left, eye) : 1f, r = right ? LiftOf(rig.Right, eye) : 1f;
            if (!left && right) l = r;
            if (!right && left) r = l;
            float k = 1f - Mathf.Exp(-liftResponse * dt);
            liftL = Mathf.Lerp(liftL, l, k);
            liftR = Mathf.Lerp(liftR, r, k);
            HandSkin.Charge[0] = Mathf.InverseLerp(liftRange.x, liftRange.y, liftL);
            HandSkin.Charge[1] = Mathf.InverseLerp(liftRange.x, liftRange.y, liftR);
            Chime(left || right, music, view, dt);
        }

        float LiftOf(HandRig.Hand hand, float eye)
        {
            var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
            return Mathf.Lerp(liftRange.x, liftRange.y, Mathf.InverseLerp(eye - hipsBelowEyes, eye + overHead, palm.y));
        }

        /// <summary>A soft note each time the lift passes a level: rising as your hands go up, falling as they come down.</summary>
        void Chime(bool hands, MusicFrame music, Camera view, float dt)
        {
            chimeWait -= dt;
            float lift = (liftL + liftR) * 0.5f;
            int level = chimeLevel;
            while (level < ChimeLevels.Length && lift > ChimeLevels[level] + 0.04f) level++;
            while (level > 0 && lift < ChimeLevels[level - 1] - 0.04f) level--;
            if (level == chimeLevel) return;
            bool up = level > chimeLevel;
            int passed = up ? level - 1 : level;
            chimeLevel = level;
            if (!hands || chimeWait > 0f) return;
            chimeWait = 0.15f;
            var at = view.transform.position + Effects.FlatForward(view) * 1.2f + Vector3.down * 0.4f;
            Musical.Play(voices, music, ChimeNotes[passed], at, chimeVolume * (up ? 1f : 0.7f));
        }

        static int LevelOf(float lift)
        {
            int level = 0;
            while (level < ChimeLevels.Length && lift > ChimeLevels[level]) level++;
            return level;
        }

        /// <summary>Each band's level, quick to rise and slower to fall, with the kick in the two lowest bands.</summary>
        void Listen(MusicFrame music, float dt)
        {
            float up = 1f - Mathf.Exp(-14f * dt), down = 1f - Mathf.Exp(-4f * dt);
            for (int b = 0; b < levels.Length; b++)
            {
                float target = music.Band(b);
                if (b < 2) target = Mathf.Max(target, music.Kick * 0.8f);
                levels[b] = Mathf.Lerp(levels[b], target, target > levels[b] ? up : down);
            }
        }

        /// <summary>The level of the music at a place in the spectrum, 0 (sub) to 5 (air).</summary>
        float Level(float at)
        {
            int lo = Mathf.Min((int)at, levels.Length - 2);
            return Mathf.Lerp(levels[lo], levels[lo + 1], at - lo);
        }

        void MarkWords()
        {
            System.Array.Clear(wordOn, 0, count);
            if (SungWords == null) return;
            var words = SungWords.Words;
            for (int k = 0; k < words.Count; k++)
                if (words[k].Active && words[k].Slot >= 0 && words[k].Slot < count) wordOn[words[k].Slot] = true;
        }

        /// <summary>
        /// Every block's height, colour, glow and opacity for this frame. Near you a block stays low; further out it
        /// can climb higher and swing wider. Each one follows its band of the music, its own slow wave and a drift
        /// that rolls across the field, all scaled by your hands, and the beat ripples bump it up as they pass. A block
        /// carrying a word rises to hold it up, and the blocks between you and it duck so the word stays in view.
        /// </summary>
        void Shape(Camera view, float dt)
        {
            var head = field.InverseTransformPoint(view.transform.position);
            var right = field.InverseTransformDirection(Vector3.Cross(Vector3.up, Effects.FlatForward(view)));
            float t = Clock;
            float rise = 1f - Mathf.Exp(-riseSpeed * dt), fall = 1f - Mathf.Exp(-fallSpeed * dt);
            float front = grow * (radius + 0.8f);
            float top = Mathf.Min(maxHeight, ceiling);
            int seen = Sightlines(head);
            for (int i = 0; i < count; i++)
            {
                if (!open[i])
                {
                    data[i] = Vector4.zero;
                    continue;
                }
                var s = spots[i];
                float dx = s.x - head.x, dz = s.y - head.z;
                float near = Mathf.Sqrt(dx * dx + dz * dz);
                float climb = Mathf.Clamp01((near - climbFrom[i]) / Mathf.Max(0.01f, climbTo[i] - climbFrom[i]));
                float reach = nearHeight + (full[i] - nearHeight) * Mathf.Pow(climb, steepness);
                // Blocks on your right follow your right hand, those on your left your left, those ahead and behind both.
                float side = near > 1e-3f ? (dx * right.x + dz * right.z) / near : 0f;
                float lift = Mathf.Lerp(liftL, liftR, Mathf.Clamp01(0.5f + side * 0.75f));

                float level = Level(band[i]);
                float wave = 0.5f + 0.5f * Mathf.Sin(t * pace[i] + phase[i]);
                float drift = Mathf.PerlinNoise(s.x * 0.42f + t * 0.11f, s.y * 0.42f - t * 0.08f);
                float share = 0.12f + 0.3f * build[i] + 0.45f * level * (0.6f + 0.8f * build[i]) + 0.25f * swing[i] * wave + 0.3f * (drift - 0.45f);
                float target = reach * lift * Mathf.Clamp(share, 0.03f, 1.4f);
                rest[i] = Mathf.Lerp(rest[i], target, target > rest[i] ? rise : fall);

                float bump = 0f;
                for (int k = 0; k < ripples.Length; k++)
                {
                    var r = ripples[k];
                    if (r.Age < 0f) continue;
                    float ox = s.x - r.Origin.x, oz = s.y - r.Origin.y;
                    float off = (Mathf.Sqrt(ox * ox + oz * oz) - r.Age * rippleSpeed) / rippleWidth;
                    if (off > 3f || off < -3f) continue;
                    bump += r.Strength * Mathf.Exp(-off * off) * Mathf.Exp(-r.Age * 0.8f);
                }
                // A word's block rises quickly as the word appears, and settles back slowly after.
                hold[i] = Mathf.MoveTowards(hold[i], wordOn[i] ? 1f : 0f, dt * (wordOn[i] ? 6f : 2.5f));
                float duck = Duck(i, dx, dz, seen);

                // Grown from the middle outwards as the field starts, and sunk from the rim inwards as it moves.
                float grown = Mathf.Clamp01((front - middle[i]) / 0.8f);
                float h = rest[i] + Mathf.Max(reach, 0.12f) * lift * rippleLift * bump * (1f - 0.6f * hold[i]);
                // A block carrying a word rises to stand it at an easy height to read.
                h = Mathf.Lerp(h, Mathf.Max(h * 1.2f, wordStand * lift), hold[i]);
                h *= grown * (1f - 0.8f * duck);
                float glow = level * 0.4f + bump * 1.2f + hold[i] * 0.5f;
                float alpha = FadeIn * rim[i] * Mathf.Clamp01(grown * 4f);
                data[i] = new Vector4(Mathf.Clamp(h, 0.006f, top), band[i] / 5f, glow, alpha);
            }
        }

        /// <summary>Notes where the words stand this frame, as seen from you; returns how many.</summary>
        int Sightlines(Vector3 head)
        {
            int seen = 0;
            var words = SungWords != null ? SungWords.Words : null;
            for (int k = 0; words != null && k < words.Count && seen < sights.Length; k++)
            {
                var w = words[k];
                if (!w.Active || w.Slot < 0 || w.Slot >= count) continue;
                sights[seen] = new Vector2(spots[w.Slot].x - head.x, spots[w.Slot].y - head.z);
                sightSlot[seen] = w.Slot;
                sightHold[seen] = hold[w.Slot];
                seen++;
            }
            return seen;
        }

        /// <summary>
        /// How far block <paramref name="i"/> (at dx, dz from you) ducks, 0-1: the blocks in a narrow wedge between you
        /// and a word, and those right beside the word's block, bow down so the word stands clear.
        /// </summary>
        float Duck(int i, float dx, float dz, int seen)
        {
            float duck = 0f;
            for (int k = 0; k < seen; k++)
            {
                if (sightSlot[k] == i) continue;
                var a = sights[k];
                float length = a.sqrMagnitude;
                if (length < 1e-4f) continue;
                // Along the line from you (0) to the word (1), and how far off it.
                float along = (dx * a.x + dz * a.y) / length;
                if (along <= 0.02f || along >= 1.12f) continue;
                float ox = dx - a.x * along, oz = dz - a.y * along;
                float off = Mathf.Sqrt(ox * ox + oz * oz);
                float wide = 0.12f + 0.28f * Mathf.Min(along, 1f);
                duck = Mathf.Max(duck, sightHold[k] * (1f - Smooth(wide, wide + 0.15f, off)));
            }
            return duck;
        }

        static float Smooth(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Each sung word stands on top of its block, turned to you, and rides up and down with it.</summary>
        void PlaceWords(MusicFrame music, Camera view)
        {
            if (SungWords == null) return;
            var eye = view.transform.position;
            var words = SungWords.Words;
            for (int k = 0; k < words.Count; k++)
            {
                var w = words[k];
                if (!w.Active) continue;
                int i = Mathf.Clamp(w.Slot, 0, count - 1);
                var top = field.TransformPoint(new Vector3(spots[i].x, data[i].x, spots[i].y));
                w.Position = top + Vector3.up * (SungWords.Height(w) * 0.3f + 0.012f);
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.25f + music.Kick * 0.2f;
                w.Alpha = Mathf.Clamp01(data[i].w * 1.5f);
            }
        }

        /// <summary>
        /// The block a sung word stands on: the one nearest a spot in front of you, fanned out from left to right by
        /// the word's place in its line (of <paramref name="words"/> words), away from the words already standing.
        /// Neighbouring words, and the same place in the next line, stand at different distances.
        /// </summary>
        int PickBlock(int index, int words, int line)
        {
            var eye = View.transform.position;
            float angle;
            if (words > 1)
            {
                float step = Mathf.Min(wordSpread, lineSpread * 2f / (words - 1));
                angle = (Mathf.Min(index, words - 1) - (words - 1) * 0.5f) * step;
            }
            else angle = ((index % 5) - 2) * wordSpread;
            float distance = wordDistance + ((index + line) & 1) * wordStagger;
            var aim = field.InverseTransformPoint(eye + Quaternion.AngleAxis(angle, Vector3.up) * Effects.FlatForward(View) * distance);
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!open[i] || data[i].w < 0.05f) continue;
                float dx = spots[i].x - aim.x, dz = spots[i].y - aim.z;
                float score = dx * dx + dz * dz;
                if (score >= bestScore || Crowded(i)) continue;
                best = i;
                bestScore = score;
            }
            return best;
        }

        /// <summary>True when a word already stands on this block or one next to it.</summary>
        bool Crowded(int i)
        {
            var words = SungWords.Words;
            for (int k = 0; k < words.Count; k++)
            {
                var w = words[k];
                if (w.Active && w.Slot >= 0 && w.Slot < count && (spots[w.Slot] - spots[i]).sqrMagnitude < spacing * spacing * 2.5f) return true;
            }
            return false;
        }

        /// <summary>
        /// The field's one mesh: a block on every grid point within the radius, outside the clearing where you stand,
        /// nearest first (so each block hides the ones behind it). Each block is four sides and a top, 1 m tall; the
        /// shader stands it up to its own height. Also gives every block its own character: how tall it tends to
        /// stand, how far and how fast it swings, and which part of the music it follows.
        /// </summary>
        Mesh BuildMesh()
        {
            var cells = new List<Vector2>();
            int n = Mathf.CeilToInt(radius / spacing);
            for (int x = -n; x <= n; x++)
                for (int z = -n; z <= n; z++)
                {
                    var p = new Vector2(x, z) * spacing;
                    float d = p.magnitude;
                    if (d >= clearing && d <= radius) cells.Add(p);
                }
            cells.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
            count = Mathf.Min(cells.Count, MaxBlocks);

            spots = new Vector2[count];
            middle = new float[count];
            rim = new float[count];
            build = new float[count];
            swing = new float[count];
            pace = new float[count];
            phase = new float[count];
            band = new float[count];
            rest = new float[count];
            hold = new float[count];
            climbFrom = new float[count];
            climbTo = new float[count];
            full = new float[count];
            open = new bool[count];
            wordOn = new bool[count];
            data = new Vector4[MaxBlocks];

            var random = new System.Random(27);
            float Next() => (float)random.NextDouble();
            var vertices = new Vector3[count * 20];
            var normals = new Vector3[count * 20];
            var faces = new Vector2[count * 20];
            var ids = new Vector2[count * 20];
            var triangles = new int[count * 30];
            float w = blockWidth, hw = blockWidth * 0.5f;
            int v = 0, t = 0;
            for (int i = 0; i < count; i++)
            {
                var c = cells[i];
                spots[i] = c;
                middle[i] = c.magnitude;
                rim[i] = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius - 0.7f, radius, middle[i]));
                build[i] = Next();
                swing[i] = 0.3f + 0.7f * Next();
                pace[i] = Mathf.Lerp(0.7f, 2.1f, Next());
                phase[i] = Next() * Mathf.PI * 2f;
                // The spectrum spread around you: the low bands on your left, the high ones on your right, mixed in
                // rings and patches further out, and a little different from block to block.
                float turn = Mathf.Atan2(c.x, c.y);
                float spectrum = 2.5f + 2.2f * Mathf.Sin(turn) + 0.9f * Mathf.Sin(middle[i] * 1.6f - turn * 2f + 1f);
                band[i] = Mathf.Clamp(spectrum + (Next() - 0.5f) * 0.8f, 0f, 5f);
                open[i] = true;
                climbFrom[i] = growDistance.x;
                climbTo[i] = growDistance.y;
                full[i] = farHeight;

                var foot = new Vector3(c.x, 0f, c.y);
                int first = v;
                // Four sides, each seen from outside with its bottom left corner first.
                for (int f = 0; f < 4; f++)
                {
                    var normal = Quaternion.Euler(0f, f * 90f, 0f) * Vector3.forward;
                    var across = Vector3.Cross(normal, Vector3.up);
                    var bottomLeft = foot + normal * hw - across * hw;
                    var bottomRight = foot + normal * hw + across * hw;
                    Quad(vertices, normals, faces, triangles, ref v, ref t, bottomLeft, bottomRight, bottomLeft + Vector3.up, bottomRight + Vector3.up,
                        normal, Vector2.zero, new Vector2(w, 0f), Vector2.zero, new Vector2(w, 0f));
                }
                var lid = foot + Vector3.up;
                Quad(vertices, normals, faces, triangles, ref v, ref t,
                    lid + new Vector3(-hw, 0f, -hw), lid + new Vector3(hw, 0f, -hw), lid + new Vector3(-hw, 0f, hw), lid + new Vector3(hw, 0f, hw),
                    Vector3.up, Vector2.zero, new Vector2(w, 0f), new Vector2(0f, w), new Vector2(w, w));
                var id = new Vector2(i, Next());
                for (int k = first; k < v; k++) ids[k] = id;
            }

            var mesh = new Mesh { name = "Cube field" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = faces;
            mesh.uv2 = ids;
            mesh.triangles = triangles;
            // The shader stands the blocks up, so the bounds cover the tallest they may grow.
            mesh.bounds = new Bounds(new Vector3(0f, maxHeight * 0.5f, 0f), new Vector3(radius * 2f + 1f, maxHeight + 0.5f, radius * 2f + 1f));
            return mesh;
        }

        /// <summary>One face: two triangles, wound so they face the way <paramref name="normal"/> points.</summary>
        static void Quad(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[] triangles, ref int v, ref int t,
            Vector3 bottomLeft, Vector3 bottomRight, Vector3 topLeft, Vector3 topRight, Vector3 normal,
            Vector2 uvBottomLeft, Vector2 uvBottomRight, Vector2 uvTopLeft, Vector2 uvTopRight)
        {
            vertices[v] = bottomLeft;
            vertices[v + 1] = bottomRight;
            vertices[v + 2] = topLeft;
            vertices[v + 3] = topRight;
            normals[v] = normals[v + 1] = normals[v + 2] = normals[v + 3] = normal;
            uvs[v] = uvBottomLeft;
            uvs[v + 1] = uvBottomRight;
            uvs[v + 2] = uvTopLeft;
            uvs[v + 3] = uvTopRight;
            triangles[t] = v;
            triangles[t + 1] = v + 2;
            triangles[t + 2] = v + 3;
            triangles[t + 3] = v;
            triangles[t + 4] = v + 3;
            triangles[t + 5] = v + 1;
            v += 4;
            t += 6;
        }

        static Material FallbackMaterial()
        {
            var shader = Shader.Find("MusicInYourHands/CubeField");
            return shader != null ? new Material(shader) : null;
        }
    }
}
