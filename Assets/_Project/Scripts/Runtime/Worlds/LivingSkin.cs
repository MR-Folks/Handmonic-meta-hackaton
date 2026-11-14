using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Living skin world: your hands are made of the music. Each fingertip carries a soft light tuned to one
    /// frequency band, every beat sends a round wave out of each palm in the colour of one of the fingers, sparks drift
    /// off the fingertips as they move, and quick finger movements leave short trails of light in the air. Each wave
    /// carries on across the room and lands on it, lighting veins in the walls (RoomWaves), and each sung word flies
    /// from your palm to the wall ahead. The pulse that runs through the hand itself lives in the hand shader (see HandSkin).
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class LivingSkin : World
    {
        public override string DisplayName => "Living skin";

        public override Color Color => new Color(1f, 0.37f, 0.68f);

        public override int MenuOrder => 0;

        public override int MenuIcon => 0;

        public override string Hint => "Hold up your hands and spread your fingers";

        /// <summary>Lights on your fingertips, and waves from your palms that spread over the walls, floor and ceiling.</summary>
        public override Space Spaces => Space.Hands | Space.Walls | Space.RoomLight;

        [Header("Materials")]
        [SerializeField] Material shellMaterial;
        [SerializeField] Material lineMaterial;
        [WorldMaterial("LivingWalls")]
        [SerializeField] Material wallMaterial;

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

        [Header("Room")]
        [Tooltip("Half the width and depth of the stand-in room (m), and its height.")]
        [SerializeField] Vector2 halfSize = new Vector2(1.8f, 1.8f);
        [SerializeField] float roomHeight = 2.6f;

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

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int AxisId = Shader.PropertyToID("_Axis");
        static readonly int PhaseId = Shader.PropertyToID("_Phase");

        // Longer than any line takes: a line stays until the next one, or until lineHold after its last word.
        const float LineLife = 600f;
        const float LetterHeight = 0.05f;

        sealed class Wave
        {
            public Transform Transform;
            public Renderer Renderer;
            public float Age;
            public float Strength;
            public Color Color;
            public Vector3 Axis;
        }

        readonly List<Wave> waves = new List<Wave>();
        readonly Transform[] tipLights = new Transform[10];
        readonly Renderer[] tipRenderers = new Renderer[10];
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
        ParticleSystem sparks;
        MaterialPropertyBlock block;
        // Where each word of the current line lands: metres right of and above the line's spot on the wall.
        readonly List<Vector2> lineSlots = new List<Vector2>();
        Vector3 lineAt, lineRight;
        Quaternion lineRotation = Quaternion.identity;
        float lineSize = 1f;
        bool lineShowing;
        RoomWaves walls;
        int beats;

        protected override int SungWordCount => 24;

        protected override float SungWordHeight => LetterHeight;

        protected override void OnExit()
        {
            // Fingertip lights ride on the hand joints, outside this object, so take them back and hide them.
            for (int i = 0; i < tipLights.Length; i++)
            {
                if (tipLights[i] == null) continue;
                tipLights[i].SetParent(transform, false);
                tipLights[i].gameObject.SetActive(false);
                hadTip[i] = false;
            }
            foreach (var wave in waves) wave.Transform.gameObject.SetActive(false);
            foreach (var trail in trails) trail?.SetVisible(false);
            System.Array.Clear(trailStrength, 0, trailStrength.Length);
            if (sparks != null) sparks.Clear();
            lineShowing = false;
            walls?.Hide();
        }

        /// <summary>Creates the lights, the wave pool and the spark system. Also run by the editor preview, through Build.</summary>
        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();

            // Fingertip lights are drawn on top of the hand: the finger's own surface is closer to the viewer than
            // the light and would otherwise cut it off. They are light, so showing through the hand looks right.
            Material tipMaterial = glowMaterial;
            if (glowMaterial != null)
            {
                tipMaterial = new Material(glowMaterial) { name = "Fingertip Glow" };
                tipMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                tipMaterial.renderQueue = glowMaterial.renderQueue + 20;
            }

            for (int i = 0; i < tipLights.Length; i++)
            {
                var quad = Make(PrimitiveType.Quad, $"Tip Light {i}", tipMaterial);
                tipLights[i] = quad.transform;
                tipRenderers[i] = quad.GetComponent<Renderer>();
                quad.SetActive(false);
            }

            for (int i = 0; i < maxWaves; i++)
            {
                var shell = Make(PrimitiveType.Sphere, $"Wave {i}", shellMaterial);
                shell.SetActive(false);
                waves.Add(new Wave { Transform = shell.transform, Renderer = shell.GetComponent<Renderer>(), Age = -1f });
            }

            for (int i = 0; i < trails.Length; i++)
            {
                trails[i] = new LineStrip($"Trail {i}", transform, lineMaterial, TrailPoints);
                trails[i].SetVisible(false);
                trailHistory[i] = new Vector3[TrailPoints];
            }

            sparks = Effects.MakeSparkSystem("Sparks", transform, glowMaterial);
            if (SungWords != null) SungWords.Rim = 0.9f;
            if (wallMaterial != null) walls = new RoomWaves(transform, wallMaterial, halfSize, roomHeight);
        }

        /// <summary>Lays the room's walls around where you stand, for the waves and the sung words to land on.</summary>
        protected override void OnPlace(Camera view)
        {
            if (walls != null) walls.Place(view.transform.position, Effects.FlatForward(view), Effects.FloorY(view));
        }

        /// <summary>Every few beats a wave leaves each palm in the next finger's colour and carries on across the room.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            if (++beats % Mathf.Max(1, waveEveryBeats) != 0) return;
            waveHue = (waveHue + 1) % fingerColors.Length;
            float strength = 0.6f + 0.4f * music.Kick;
            if (Rig.Left.IsTracked) SpawnWave(Rig.Left, View, strength);
            if (Rig.Right.IsTracked) SpawnWave(Rig.Right, View, strength);
        }

        /// <summary>
        /// Each sung word leaves a palm, turns as it flies and lands in its place on the wall ahead of you. The line's
        /// place is chosen when its first word starts and then stays put, so the row builds up word by word and is still
        /// to read.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            if (walls == null) return;
            if (newLine) PlanLine(Music.LineWords, View);
            Launch(Rig, word, View);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            UpdateHand(rig.Left, 0, music.Bands, music.Kick, view, dt, music.Beat);
            UpdateHand(rig.Right, 1, music.Bands, music.Kick, view, dt, music.Beat);
            AdvanceWaves(dt);
            walls?.Step(music, dt);
            Sing(music, view, dt);
        }

        /// <summary>
        /// The line's words fly to their places on the wall, and the line fades once it is over. The word being sung
        /// takes the wave's colour; the rest settle to white.
        /// </summary>
        void Sing(MusicFrame music, Camera view, float dt)
        {
            if (SungWords == null || walls == null) return;
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
            var forward = Effects.FlatForward(view);
            if (!walls.Aim(eye, forward, out var wall, out var hit, out float distance))
            {
                distance = 2f;
                hit = eye + forward * distance;
                wall = new Room.Surface(hit, -forward, Vector3.Cross(Vector3.up, forward), new Vector2(6f, 3f));
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

        static Vector3 PalmOf(HandRig.Hand hand) => hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;

        void UpdateHand(HandRig.Hand hand, int side, float[] bands, float kick, Camera view, float dt, bool beat)
        {
            for (int f = 0; f < 5; f++)
            {
                int i = side * 5 + f;
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
            Spawn(hand, strength);
            SendToRoom(hand, view, strength);
        }

        /// <summary>
        /// Carries the beat on across the room: the way the palm faces when it faces away from you, so you can aim it;
        /// past the hand when it faces you, so the wave still lands somewhere you can see.
        /// </summary>
        void SendToRoom(HandRig.Hand hand, Camera view, float strength)
        {
            if (walls == null || view == null) return;
            var palm = PalmOf(hand);
            var outward = (palm - view.transform.position).normalized;
            var direction = Vector3.Dot(hand.PalmNormal, outward) > 0.2f ? hand.PalmNormal : outward;
            walls.Send(palm, direction, fingerColors[waveHue], strength * 0.5f);
        }

        void Spawn(HandRig.Hand hand, float strength)
        {
            Wave wave = null;
            foreach (var w in waves)
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
            ApplyWave(wave);
        }

        void AdvanceWaves(float dt)
        {
            foreach (var wave in waves)
            {
                if (!wave.Transform.gameObject.activeSelf) continue;
                wave.Age += dt;
                if (wave.Age >= waveLife)
                {
                    wave.Transform.gameObject.SetActive(false);
                    continue;
                }
                ApplyWave(wave);
            }
        }

        void ApplyWave(Wave wave)
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
