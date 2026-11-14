using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Sand table world: a dark glass plate at table height covered in sand that you can play with. The music
    /// shakes the plate like a Chladni plate: sand is jostled where the plate moves and comes to rest on the lines that
    /// stay still, gathering into the true shapes of sound, cos(n x) cos(m y) - cos(m x) cos(n y) for vibration mode
    /// n, m, and it jumps on every kick. Touch the plate and your fingers plough grooves through the sand; hold a hand
    /// just above it and the sand blows away from under your palm; slide a hovering hand from left to right to change
    /// the pattern; pinch above the plate to pour a stream of sand where you like. The plate lights your hand from below.
    /// </summary>
    public class SandTable : World
    {
        public override string DisplayName => "Sand table";

        public override Color Color => new Color(1f, 0.86f, 0.68f);

        public override int MenuOrder => 90;

        public override int MenuIcon => 11;

        public override string Hint => "Touch the sand, hover to change the pattern, pinch to pour";

        /// <summary>The plate lies in front of you, and a pinch pours sand onto it.</summary>
        public override Space Spaces => Space.InFront | Space.Pinch;

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.12f, 0.09f, 0.07f, 0.55f),
            Edge = new Color(1f, 0.86f, 0.68f),
            Glow = new Color(0.49f, 0.95f, 0.79f),
            Pattern = HandTheme.Effect.Uplight,
            PatternColor = new Color(1f, 0.86f, 0.68f)
        };

        [Header("Materials")]
        [SerializeField] Material plateMaterial;

        [Header("Plate")]
        [SerializeField] float plateSize = 0.5f;
        [SerializeField] float distance = 0.46f;
        [SerializeField] float belowEyes = 0.52f;
        [SerializeField] int grains = 3200;
        [SerializeField] float grainSize = 0.004f;
        [Tooltip("How high above the plate (m) a hand still counts as hovering over it.")]
        [SerializeField] float hoverHeight = 0.25f;

        // Vibration modes from simple to busy.
        static readonly Vector2Int[] Modes =
        {
            new Vector2Int(1, 2), new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(1, 4),
            new Vector2Int(2, 5), new Vector2Int(3, 4), new Vector2Int(3, 5), new Vector2Int(2, 7)
        };

        static readonly int NId = Shader.PropertyToID("_N");
        static readonly int MId = Shader.PropertyToID("_M");
        static readonly int KickId = Shader.PropertyToID("_Kick");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly Color Sand = new Color(1f, 0.86f, 0.68f);

        Transform plate;
        Renderer plateRenderer;
        TypeLabel label;
        ParticleSystem sand;
        ParticleSystem.Particle[] output;
        Vector2[] grain;
        float[] hop, tone;
        // Grains in the air, poured from a pinch: their world position and velocity; NaN x when resting on the plate.
        Vector3[] airPos, airVel;
        readonly Vector2[,] lastContact = new Vector2[2, 6];
        readonly bool[,] hadContact = new bool[2, 6];
        readonly float[] pourDebt = new float[2];
        MaterialPropertyBlock block;
        System.Random random;
        AudioPool voices;
        MusicFrame frame;
        int mode = -1, beats, pourNext;
        float shake, half;

        protected override float FadeInSeconds => 1f / 2f;

        protected override int SungWordCount => 5;

        protected override float SungWordHeight => 0.03f;

        protected override float SungWordsAlpha => FadeIn;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            random = new System.Random(5);
            grain = new Vector2[grains];
            hop = new float[grains];
            tone = new float[grains];
            airPos = new Vector3[grains];
            airVel = new Vector3[grains];
            output = new ParticleSystem.Particle[grains];
            ScatterSand();

            plate = new GameObject("Plate").transform;
            plate.SetParent(transform, false);
            plateRenderer = Effects.MakeQuad("Plate Surface", plate, plateMaterial);
            // The quad faces -Z; lay it flat facing up.
            plateRenderer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            plateRenderer.transform.localScale = Vector3.one * plateSize;

            label = new TypeLabel("Mode", plate, 0.013f, 1f);
            label.Transform.localPosition = new Vector3(0f, 0.002f, -plateSize * 0.53f);
            label.Transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var go = new GameObject("Sand");
            go.transform.SetParent(transform, false);
            sand = go.AddComponent<ParticleSystem>();
            sand.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sand.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = grains;
            main.startSpeed = 0f;
            var emission = sand.emission;
            emission.enabled = false;
            var shape = sand.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glowMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            voices = new AudioPool(transform, 3);
            plate.gameObject.SetActive(false);
        }

        void ScatterSand()
        {
            for (int i = 0; i < grains; i++)
            {
                grain[i] = new Vector2((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f) * 0.95f;
                tone[i] = (float)random.NextDouble();
                airPos[i].x = float.NaN;
            }
        }

        protected override void OnEnter()
        {
            mode = -1;
            System.Array.Clear(hadContact, 0, hadContact.Length);
            ScatterSand();
        }

        protected override void OnExit()
        {
            plate.gameObject.SetActive(false);
            sand.Clear();
        }

        /// <summary>Sets the plate down at table height in front of you.</summary>
        protected override void OnPlace(Camera view)
        {
            var forward = Effects.FlatForward(view);
            var center = view.transform.position + forward * distance + Vector3.down * belowEyes;
            center.y = Mathf.Max(center.y, Effects.FloorY(view) + 0.45f);
            // With a room scan, the plate rests on the real table in front of you.
            if (Room.TableAhead(view.transform.position, forward, 1.1f, plateSize * 0.5f, out _, out var spot))
                center = spot + Vector3.up * 0.02f;
            plate.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
            plate.gameObject.SetActive(true);
        }

        /// <summary>Counts the beats: without a hovering hand, the music moves the pattern on every eighth beat.</summary>
        protected override void OnBeat(MusicFrame music) => beats++;

        /// <summary>
        /// Each sung word appears written in the sand along the plate's near edge, the line's words side by side,
        /// and shivers as the plate rings.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            if (newLine)
                foreach (var o in SungWords.Words) if (o.Active) o.Life = Mathf.Min(o.Life, o.Age + 0.5f);
            var w = SungWords.Add(word, Sand, 5f);
            w.Spot = new Vector2(-0.6f + (Sung.Index % 4) * 0.4f, -0.72f);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            frame = music;
            half = plateSize * 0.5f * 0.95f;

            // A hovering hand picks the mode by where it is across the plate; otherwise the music moves it on.
            float press = Hover(rig, out float across);
            int wanted = press > 0f
                ? Mathf.Clamp(Mathf.FloorToInt((across * 0.5f + 0.5f) * Modes.Length), 0, Modes.Length - 1)
                : (beats / 8) % Modes.Length;
            if (wanted != mode)
            {
                if (mode >= 0)
                {
                    shake = 1f;
                    if (press > 0f) Musical.Play(voices, music, 57 + wanted * 3, plate.position, 0.3f, pluck: true);
                }
                mode = wanted;
            }
            shake *= Mathf.Exp(-dt * 1.8f);
            HandSkin.FocusPosition = plate.position;
            HandSkin.FocusStrength = Mathf.Max(press, 0.25f) * FadeIn;

            float amp = 0.018f + music.Kick * 0.06f + music.Band(1) * 0.04f + shake * 0.06f;
            Shake(Modes[mode], amp, dt);
            if (rig != null)
            {
                Touch(rig, dt);
                Pour(rig, dt);
            }
            Fall(dt);
            Draw(music, amp);

            block.Clear();
            block.SetFloat(NId, Modes[mode].x);
            block.SetFloat(MId, Modes[mode].y);
            block.SetFloat(KickId, music.Kick);
            block.SetFloat(AlphaId, FadeIn);
            plateRenderer.SetPropertyBlock(block);
            label.Text = $"mode {Modes[mode].x}:{Modes[mode].y}";
            label.Color = new Color(Sand.r, Sand.g, Sand.b, 0.75f * FadeIn);

            // The sung words lie in the sand and shiver as the plate rings.
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                var jitter = Random.insideUnitCircle * (amp * 0.04f);
                w.Position = plate.TransformPoint(new Vector3(w.Spot.x * half + jitter.x, 0.003f, w.Spot.y * half + jitter.y));
                w.Rotation = WordLayer.Lying(w.Position, eye);
                w.Halo = 0.1f + music.Kick * 0.15f;
            }
        }

        /// <summary>How strongly a hand hovers over the plate (0-1, lower is stronger), and where across it (-1..1).</summary>
        float Hover(HandRig rig, out float across)
        {
            across = 0f;
            float best = 0f;
            if (rig == null) return 0f;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                // While the menu is open the hands are choosing there.
                if (!hand.IsTracked || hand.Pinch > 0.8f || MenuOpen) continue;
                var local = plate.InverseTransformPoint(Palm(hand));
                if (Mathf.Abs(local.x) > half * 1.15f || Mathf.Abs(local.z) > half * 1.15f || local.y < 0.03f || local.y > hoverHeight) continue;
                float strength = 1f - Mathf.Clamp01(local.y / hoverHeight);
                if (strength > best)
                {
                    best = strength;
                    across = Mathf.Clamp(local.x / half, -1f, 1f);
                }
            }
            return best;
        }

        /// <summary>
        /// Each grain is jostled in proportion to how much the plate moves under it, so grains drift off the moving
        /// parts and settle on the still lines, like real sand on a Chladni plate.
        /// </summary>
        void Shake(Vector2Int m, float amp, float dt)
        {
            float frames = Mathf.Sqrt(Mathf.Max(dt, 1e-4f) * 60f);
            const float halfPi = Mathf.PI * 0.5f;
            for (int i = 0; i < grains; i++)
            {
                if (!float.IsNaN(airPos[i].x)) continue;
                var p = grain[i];
                float x = (p.x + 1f) * halfPi, y = (p.y + 1f) * halfPi;
                float f = Mathf.Cos(m.x * x) * Mathf.Cos(m.y * y) - Mathf.Cos(m.y * x) * Mathf.Cos(m.x * y);
                float jolt = (Mathf.Abs(f) + 0.015f) * amp * frames;
                p.x = Mathf.Clamp(p.x + ((float)random.NextDouble() - 0.5f) * jolt, -0.98f, 0.98f);
                p.y = Mathf.Clamp(p.y + ((float)random.NextDouble() - 0.5f) * jolt, -0.98f, 0.98f);
                grain[i] = p;
                hop[i] = Mathf.Lerp(hop[i], Mathf.Abs(f) * amp, 0.5f);
            }
        }

        /// <summary>
        /// Fingertips and palms on the plate plough the sand aside along their path; a palm held just above the plate
        /// blows the sand away from under it.
        /// </summary>
        void Touch(HandRig rig, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                for (int k = 0; k < 6; k++)
                {
                    if (!hand.IsTracked || MenuOpen) { hadContact[side, k] = false; continue; }
                    var point = k < 5 ? (hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k]) : Palm(hand);
                    var local = plate.InverseTransformPoint(point);
                    var c = new Vector2(local.x, local.z) / half;
                    bool over = Mathf.Abs(c.x) < 1.1f && Mathf.Abs(c.y) < 1.1f;
                    bool touching = over && local.y < 0.032f && local.y > -0.06f;
                    float blow = k == 5 && over && !touching ? 1f - Mathf.Clamp01((local.y - 0.02f) / 0.14f) : 0f;
                    var motion = hadContact[side, k] ? c - lastContact[side, k] : Vector2.zero;
                    lastContact[side, k] = c;
                    hadContact[side, k] = touching;
                    if (!touching && blow <= 0f) continue;

                    float radius = (touching ? (k < 5 ? 0.022f : 0.05f) : 0.1f) / half;
                    float r2 = radius * radius;
                    for (int i = 0; i < grains; i++)
                    {
                        if (!float.IsNaN(airPos[i].x)) continue;
                        var d = grain[i] - c;
                        float sq = d.sqrMagnitude;
                        if (sq > r2) continue;
                        float dist = Mathf.Sqrt(sq) + 1e-5f;
                        var outward = d / dist;
                        if (touching)
                        {
                            // Pushed to the edge of the furrow, and a little along with the finger.
                            grain[i] = c + outward * (radius * (1f + (float)random.NextDouble() * 0.15f)) + motion * 0.35f;
                            hop[i] = 0.02f;
                        }
                        else grain[i] += outward * (blow * 0.25f * dt * (1f - dist / radius) * 4f);
                        grain[i] = new Vector2(Mathf.Clamp(grain[i].x, -0.98f, 0.98f), Mathf.Clamp(grain[i].y, -0.98f, 0.98f));
                    }
                }
            }
        }

        /// <summary>Pinching above the plate pours a stream of sand: grains taken from the far edges fall from your fingers.</summary>
        void Pour(HandRig rig, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                bool pouring = hand.IsTracked && hand.Pinch > 0.8f && !hand.InSystemGesture && !MenuOpen;
                if (!pouring) { pourDebt[side] = 0f; continue; }
                var thumb = hand.TipJoints[0] != null ? hand.TipJoints[0].position : hand.Tips[0];
                var index = hand.TipJoints[1] != null ? hand.TipJoints[1].position : hand.Tips[1];
                var from = (thumb + index) * 0.5f;
                var local = plate.InverseTransformPoint(from);
                if (Mathf.Abs(local.x) > half * 1.2f || Mathf.Abs(local.z) > half * 1.2f || local.y < 0.02f || local.y > 0.6f) continue;
                pourDebt[side] += 120f * dt;
                while (pourDebt[side] >= 1f)
                {
                    pourDebt[side] -= 1f;
                    int i = pourNext;
                    pourNext = (pourNext + 7) % grains;
                    airPos[i] = from + Random.insideUnitSphere * 0.003f;
                    airVel[i] = hand.Velocity * 0.3f + Random.insideUnitSphere * 0.02f;
                }
            }
        }

        /// <summary>Poured grains fall and land as sand where they hit the plate.</summary>
        void Fall(float dt)
        {
            for (int i = 0; i < grains; i++)
            {
                if (float.IsNaN(airPos[i].x)) continue;
                airVel[i] += Physics.gravity * dt;
                airPos[i] += airVel[i] * dt;
                var local = plate.InverseTransformPoint(airPos[i]);
                if (local.y > 0.002f) continue;
                var c = new Vector2(local.x, local.z) / half;
                if (Mathf.Abs(c.x) <= 1f && Mathf.Abs(c.y) <= 1f)
                {
                    grain[i] = c + Random.insideUnitCircle * 0.02f;
                    hop[i] = 0.01f;
                }
                // Grains that miss the plate return somewhere random on it, out of sight under the others.
                else grain[i] = new Vector2((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f) * 0.95f;
                airPos[i].x = float.NaN;
            }
        }

        void Draw(MusicFrame music, float amp)
        {
            for (int i = 0; i < grains; i++)
            {
                Vector3 world;
                float settled;
                if (!float.IsNaN(airPos[i].x))
                {
                    world = airPos[i];
                    settled = 0.6f;
                }
                else
                {
                    // Grains on the moving parts hop off the plate a little; settled ones glint.
                    float lift = 0.0015f + hop[i] * 0.12f * (0.5f + tone[i]);
                    world = plate.TransformPoint(new Vector3(grain[i].x * half, lift, grain[i].y * half));
                    settled = 1f - Mathf.Clamp01(hop[i] / Mathf.Max(amp, 1e-4f) * 2f);
                }
                var c = Color.Lerp(Sand * 0.75f, Color.Lerp(Sand, Color.white, 0.35f), tone[i] * 0.5f + settled * 0.5f);
                c.a = (0.55f + settled * 0.4f + music.Kick * 0.1f) * FadeIn;
                output[i] = new ParticleSystem.Particle
                {
                    position = world,
                    startColor = c,
                    startSize = grainSize * (0.75f + tone[i] * 0.5f),
                    startLifetime = 10f,
                    remainingLifetime = 10f
                };
            }
            sand.SetParticles(output, grains);
        }

        static Vector3 Palm(HandRig.Hand hand) => hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
    }
}
