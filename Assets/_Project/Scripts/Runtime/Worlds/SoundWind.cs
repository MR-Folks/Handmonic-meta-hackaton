using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Sound wind world: the music swims around you as a school of light. Hundreds of small glowing fish circle
    /// you at chest height in a loose, living ring, flocking like real fish: each keeps a little space from its
    /// neighbours, matches their heading and stays with the group. The louder the song, the faster they swim; on each
    /// beat a flash runs through the school and it surges forward. Put your hand in their way and the school splits
    /// around your fingers and palm and pours through the gaps, then closes up again behind you. Light streams over
    /// your skin with them.
    /// </summary>
    public class SoundWind : World
    {
        public override string DisplayName => "Sound wind";

        public override Color Color => new Color(0.49f, 0.95f, 0.79f);

        public override int MenuOrder => 70;

        public override int MenuIcon => 9;

        public override string Hint => "Put your hand in the school and spread your fingers";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.12f, 0.1f, 0.24f, 0.5f),
            Edge = new Color(0.56f, 0.53f, 0.85f),
            Glow = new Color(0.49f, 0.95f, 0.79f),
            Pattern = HandTheme.Effect.Flow,
            PatternColor = new Color(0.6f, 0.95f, 0.9f)
        };

        [Header("School")]
        [SerializeField] int count = 420;
        [Tooltip("Radius of the ring the school swims around you (m), and how thick the ring is.")]
        [SerializeField] float orbit = 0.46f;
        [SerializeField] float thickness = 0.2f;
        [Tooltip("Height of the ring relative to the eyes (m).")]
        [SerializeField] float height = -0.32f;
        [SerializeField] float cruise = 0.32f;

        [Header("Hands")]
        [SerializeField] float tipRadius = 0.022f;
        [SerializeField] float palmRadius = 0.05f;

        const float Neighbourhood = 0.09f;
        const int Cells = 2048;
        static readonly Color[] Shades =
        {
            Effects.Mint, new Color(0.45f, 0.85f, 0.95f), Effects.Blue, Effects.Violet, Effects.Pink, Effects.Gold
        };

        Vector3[] position, velocity;
        float[] lane, lift, phase;
        byte[] shade;
        int[] head, next;
        ParticleSystem system;
        ParticleSystem.Particle[] output;
        readonly Vector3[] obstacles = new Vector3[12];
        readonly float[] radii = new float[12];
        Vector3 centre;
        float surge, flashTime = -10f, flashStrength;

        protected override float FadeInSeconds => 1f / 1.2f;

        protected override int SungWordCount => 4;

        protected override float SungWordHeight => 0.034f;

        protected override float SungWordsAlpha => FadeIn;

        protected override void OnBuild()
        {
            position = new Vector3[count];
            velocity = new Vector3[count];
            lane = new float[count];
            lift = new float[count];
            phase = new float[count];
            shade = new byte[count];
            head = new int[Cells];
            next = new int[count];
            output = new ParticleSystem.Particle[count];
            var random = new System.Random(8);
            for (int i = 0; i < count; i++)
            {
                lane[i] = ((float)random.NextDouble() * 2f - 1f) * 0.5f;
                lift[i] = ((float)random.NextDouble() * 2f - 1f) * 0.5f;
                phase[i] = (float)random.NextDouble() * 6.28f;
                shade[i] = (byte)random.Next(Shades.Length);
            }

            var go = new GameObject("School");
            go.transform.SetParent(transform, false);
            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glowMaterial;
            // Stretched along their heading, so each fish reads as a slim body with a tail.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 2.2f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        protected override void OnExit()
        {
            if (system != null) system.Clear();
        }

        /// <summary>The school circles you at chest height, starting out spread around the ring.</summary>
        protected override void OnPlace(Camera view)
        {
            centre = view.transform.position + Vector3.up * height;
            Scatter();
        }

        /// <summary>On each beat a flash runs through the school and it surges forward.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            surge = 1f;
            // The flash through the school is a flash (World.Flash): at most three a second, a quarter with Less flashing.
            float strength = Flash(1f);
            if (strength <= 0f) return;
            flashTime = Now;
            flashStrength = strength;
        }

        /// <summary>
        /// Each sung word is carried by a fish swimming past in front of you: it rides just above the fish around the
        /// ring, turning to face you, until it fades.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            var eye = View.transform.position;
            // The fish nearest to where you look, a little to the side the school swims from.
            var look = Quaternion.AngleAxis(-20f, Vector3.up) * Effects.FlatForward(View);
            int best = 0;
            float bestScore = float.MinValue;
            for (int k = 0; k < 60; k++)
            {
                int i = Random.Range(0, count);
                var to = position[i] - eye;
                float score = Vector3.Dot(to.normalized, look) - Mathf.Abs(to.magnitude - 0.5f);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            var w = SungWords.Add(word, new Color(0.85f, 1f, 0.97f), 3f);
            w.Slot = best;
            w.Position = position[best];
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            HandSkin.FocusPosition = centre;
            HandSkin.FocusStrength = 0.7f * FadeIn;

            surge *= Mathf.Exp(-dt * 2.5f);

            CollectObstacles(rig);
            Swim(music, Mathf.Min(dt, 0.05f));
            Draw(music);
            Sing(view, dt);
        }

        /// <summary>The sung words ride along just above their fish, turned to face you.</summary>
        void Sing(Camera view, float dt)
        {
            if (SungWords == null) return;
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                var target = position[w.Slot] + Vector3.up * 0.025f;
                w.Position = Vector3.Lerp(w.Position, target, 1f - Mathf.Exp(-12f * dt));
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.25f + surge * 0.35f;
            }
        }

        /// <summary>Starts the school spread around the ring, each fish already swimming its way.</summary>
        void Scatter()
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                float r = orbit + lane[i] * thickness;
                position[i] = centre + new Vector3(Mathf.Cos(a) * r, lift[i] * thickness, Mathf.Sin(a) * r);
                velocity[i] = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * cruise;
            }
        }

        /// <summary>
        /// The fingertips and palms the fish swim round. They only get out of the way, so they keep doing it while the
        /// menu is open too.
        /// </summary>
        void CollectObstacles(HandRig rig)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig != null ? rig.Get(side == 0) : null;
                bool tracked = hand != null && hand.IsTracked;
                for (int k = 0; k < 6; k++)
                {
                    int i = side * 6 + k;
                    radii[i] = tracked ? (k < 5 ? tipRadius : palmRadius) : 0f;
                    if (!tracked) continue;
                    obstacles[i] = k < 5
                        ? hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k]
                        : hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                }
            }
        }

        static int Hash(int x, int y, int z) => ((x * 73856093) ^ (y * 19349663) ^ (z * 83492791)) & (Cells - 1);

        /// <summary>
        /// One step of the flock: separation, alignment and cohesion with neighbours found through a small spatial
        /// hash, a pull along the ring around you, and quick swerves around the hands.
        /// </summary>
        void Swim(MusicFrame music, float dt)
        {
            for (int c = 0; c < Cells; c++) head[c] = -1;
            for (int i = 0; i < count; i++)
            {
                var q = position[i];
                int c = Hash(Mathf.FloorToInt(q.x / Neighbourhood), Mathf.FloorToInt(q.y / Neighbourhood), Mathf.FloorToInt(q.z / Neighbourhood));
                next[i] = head[c];
                head[c] = i;
            }

            float loud = (music.Band(1) + music.Band(2) + music.Band(3)) / 3f;
            float speed = cruise * (0.8f + loud * 0.9f) * (1f + surge * 0.9f);
            float n2 = Neighbourhood * Neighbourhood;
            float t = Now;
            for (int i = 0; i < count; i++)
            {
                var p = position[i];
                var v = velocity[i];
                Vector3 separate = Vector3.zero, align = Vector3.zero, gather = Vector3.zero;
                int neighbours = 0;
                int cx = Mathf.FloorToInt(p.x / Neighbourhood), cy = Mathf.FloorToInt(p.y / Neighbourhood), cz = Mathf.FloorToInt(p.z / Neighbourhood);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int j = head[Hash(cx + dx, cy + dy, cz + dz)]; j >= 0 && neighbours < 14; j = next[j])
                    {
                        if (j == i) continue;
                        var d = p - position[j];
                        float sq = d.sqrMagnitude;
                        if (sq > n2 || sq < 1e-10f) continue;
                        neighbours++;
                        separate += d / sq;
                        align += velocity[j];
                        gather += position[j];
                    }
                }

                var steer = Vector3.zero;
                if (neighbours > 0)
                {
                    steer += separate * 0.0009f;
                    steer += (align / neighbours - v) * 1.4f;
                    steer += (gather / neighbours - p) * 2.2f;
                }

                // Swim around you: along the ring, back towards this fish's own lane in it.
                var flat = new Vector3(p.x - centre.x, 0f, p.z - centre.z);
                float r = flat.magnitude + 1e-4f;
                var outward = flat / r;
                var along = new Vector3(-outward.z, 0f, outward.x);
                float wantR = orbit + lane[i] * thickness + Mathf.Sin(t * 0.4f + phase[i]) * 0.05f;
                float wantY = centre.y + lift[i] * thickness + Mathf.Sin(t * 0.7f + phase[i] * 2f) * 0.04f;
                steer += along * (speed * 1.2f) - v * 0.6f;
                steer += outward * ((wantR - r) * 3f) + Vector3.up * ((wantY - p.y) * 3f);

                // Swerve around the hands: push out of their way, strongest right at the skin.
                for (int k = 0; k < obstacles.Length; k++)
                {
                    float R = radii[k];
                    if (R <= 0f) continue;
                    var d = p - obstacles[k];
                    float reach = R + 0.07f;
                    float sq = d.sqrMagnitude;
                    if (sq > reach * reach) continue;
                    float dist = Mathf.Sqrt(sq) + 1e-4f;
                    var away = d / dist;
                    steer += away * ((reach - dist) / reach) * 22f;
                    if (dist < R) p = obstacles[k] + away * R;
                }

                v += steer * dt;
                float s = v.magnitude;
                float max = speed * 1.8f, min = speed * 0.5f;
                if (s > max) v *= max / s;
                else if (s < min && s > 1e-5f) v *= min / s;
                velocity[i] = v;
                position[i] = p + v * dt;
            }
        }

        void Draw(MusicFrame music)
        {
            float age = Now - flashTime;
            float t = Now;
            for (int i = 0; i < count; i++)
            {
                // The beat's flash runs round the ring.
                var flat = new Vector3(position[i].x - centre.x, 0f, position[i].z - centre.z);
                float angle = Mathf.Atan2(flat.z, flat.x) + Mathf.PI;
                float front = Mathf.Repeat(angle - age * 5f, Mathf.PI * 2f);
                float flash = Mathf.Exp(-front * front * 6f) * Mathf.Exp(-age * 1.5f) * flashStrength;
                var c = Color.Lerp(Shades[shade[i]], Color.white, flash * 0.7f);
                c.a = Mathf.Clamp01((0.75f + flash * 0.4f + music.Band(1 + shade[i] % 5) * 0.25f) * FadeIn);
                float wiggle = 1f + Mathf.Sin(t * 14f + phase[i]) * 0.12f;
                output[i] = new ParticleSystem.Particle
                {
                    position = position[i],
                    velocity = velocity[i],
                    startColor = c,
                    startSize = 0.0085f * wiggle * (1f + flash * 0.4f),
                    startLifetime = 10f,
                    remainingLifetime = 10f
                };
            }
            system.SetParticles(output, count);
        }
    }
}
