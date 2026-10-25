using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Sound wind world: the music blows out of a glowing vent across the room as a wind of light, each streak
    /// coloured by the band it came from, gusting with the kick. It flows towards you and past you. Hold your hand in
    /// it and it streams around your fingers: spread them and it pours through the gaps, close your hand and it parts
    /// around your fist. Streaks touching your hand flare up, and light streams over your skin in the wind.
    /// </summary>
    public class SoundWind : World
    {
        public override int MenuIcon => 9;

        public override string Hint => "Hold your hand in the wind and spread your fingers";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.12f, 0.1f, 0.24f, 0.5f),
            Edge = new Color(0.56f, 0.53f, 0.85f),
            Glow = new Color(0.49f, 0.95f, 0.79f),
            Pattern = HandTheme.Effect.Flow,
            PatternColor = new Color(0.6f, 0.95f, 0.9f)
        };

        [Header("Materials")]
        [SerializeField] Material glowMaterial;
        [SerializeField] Material ringMaterial;

        [Header("Wind")]
        [Tooltip("Where the wind comes from, relative to the eyes: right, up, forward (m).")]
        [SerializeField] Vector3 vent = new Vector3(-0.35f, -0.15f, 1.3f);
        [Tooltip("Where it blows towards, relative to the eyes: right, up, forward (m).")]
        [SerializeField] Vector3 aim = new Vector3(0.15f, -0.35f, 0.25f);
        [SerializeField] int maxParticles = 1400;
        [SerializeField] float baseRate = 90f;
        [SerializeField] float speed = 0.8f;
        [SerializeField] float lifetime = 3.2f;

        [Header("Hands")]
        [SerializeField] float tipRadius = 0.016f;
        [SerializeField] float palmRadius = 0.04f;
        [SerializeField] float fistRadius = 0.065f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly Color[] BandColors = { Effects.Pink, Effects.Orange, Effects.Gold, Effects.Mint, Effects.Blue, Effects.Violet };

        Vector3[] position, velocity;
        float[] age;
        byte[] band;
        float[] heat;
        int alive;
        ParticleSystem.Particle[] output;
        ParticleSystem system;
        Renderer ventGlow, ventRing, ventRing2;
        MaterialPropertyBlock block;
        readonly Vector3[] obstacles = new Vector3[12];
        readonly float[] radii = new float[12];
        Vector3 source, flow, right;
        float emitDebt, fade, ringAge = 9f;
        bool anchored, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            position = new Vector3[maxParticles];
            velocity = new Vector3[maxParticles];
            age = new float[maxParticles];
            band = new byte[maxParticles];
            heat = new float[maxParticles];
            output = new ParticleSystem.Particle[maxParticles];

            var go = new GameObject("Wind");
            go.transform.SetParent(transform, false);
            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startSpeed = 0f;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glowMaterial;
            // Stretched along their motion, so each particle reads as a streak of wind.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ventGlow = Effects.MakeQuad("Vent Glow", transform, glowMaterial);
            ventRing = Effects.MakeQuad("Vent Ring", transform, ringMaterial);
            ventRing2 = Effects.MakeQuad("Vent Pulse", transform, ringMaterial);
            SetVentVisible(false);
        }

        void SetVentVisible(bool visible)
        {
            ventGlow.gameObject.SetActive(visible);
            ventRing.gameObject.SetActive(visible);
            ventRing2.gameObject.SetActive(visible);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
            alive = 0;
            fade = 0f;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            alive = 0;
            if (system != null) system.Clear();
            if (ventGlow != null) SetVentVisible(false);
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            if (anchored && ShouldReanchor(view, dt)) anchored = false;
            if (!anchored)
            {
                var head = view.transform.position;
                var forward = Effects.FlatForward(view);
                right = Vector3.Cross(Vector3.up, forward).normalized;
                source = head + right * vent.x + Vector3.up * vent.y + forward * vent.z;
                var target = head + right * aim.x + Vector3.up * aim.y + forward * aim.z;
                flow = (target - source).normalized;
                SetVentVisible(true);
                MarkAnchored(view);
                anchored = true;
            }
            fade = Mathf.MoveTowards(fade, 1f, dt * 1.5f);
            HandSkin.FocusPosition = source;
            HandSkin.FocusStrength = fade;

            CollectObstacles(rig);
            Emit(music, dt);
            Move(music, dt);
            Draw();
            DrawVent(music, view, dt);
        }

        /// <summary>Fingertips and palms the wind has to flow around; a closed hand is one bigger lump.</summary>
        void CollectObstacles(HandRig rig)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig != null ? rig.Get(side == 0) : null;
                bool tracked = hand != null && hand.IsTracked;
                bool fist = tracked && hand.Fist > 0.6f;
                for (int k = 0; k < 6; k++)
                {
                    int i = side * 6 + k;
                    if (!tracked || (fist && k < 5)) { radii[i] = 0f; continue; }
                    if (k < 5)
                    {
                        obstacles[i] = hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k];
                        radii[i] = tipRadius;
                    }
                    else
                    {
                        obstacles[i] = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                        radii[i] = fist ? fistRadius : palmRadius;
                    }
                }
            }
        }

        /// <summary>New streaks leave the vent with the music: steady with the bass, in gusts on each kick.</summary>
        void Emit(MusicFrame music, float dt)
        {
            float level = (music.Band(1) + music.Band(2) + music.Band(3) + music.Band(4)) * 0.25f;
            emitDebt += (baseRate * (0.4f + level * 1.4f) + music.Kick * 450f) * dt * fade;
            int count = (int)emitDebt;
            emitDebt -= count;
            var up = Vector3.Cross(flow, right).normalized;
            for (int n = 0; n < count && alive < maxParticles; n++)
            {
                int b = Random.Range(1, MusicBus.BandCount);
                float a = Random.value * Mathf.PI * 2f, r = Mathf.Sqrt(Random.value) * 0.07f;
                int i = alive++;
                position[i] = source + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * r;
                float s = speed * (0.7f + music.Band(b) * 0.6f + music.Kick * 0.4f);
                velocity[i] = (flow + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * 0.12f + Random.insideUnitSphere * 0.05f) * s;
                age[i] = 0f;
                band[i] = (byte)b;
                heat[i] = 0f;
            }
        }

        void Move(MusicFrame music, float dt)
        {
            float t = Now;
            for (int i = 0; i < alive; i++)
            {
                age[i] += dt;
                var p = position[i];
                // Slowly back towards the main stream, plus a lazy swirl so it reads as air rather than rays.
                var v = velocity[i];
                float s = v.magnitude;
                var wanted = flow * Mathf.Max(s, speed * 0.6f);
                v = Vector3.Lerp(v, wanted, dt * 0.6f);
                v += new Vector3(Mathf.Sin(p.y * 7f + t * 1.3f), Mathf.Sin(p.z * 6f - t * 1.1f) * 0.6f, Mathf.Sin(p.x * 8f + t * 0.9f)) * (0.12f * dt);

                // Flow around the hands: lose the part of the motion heading into an obstacle and slip past it.
                float hot = heat[i] * Mathf.Exp(-dt * 4f);
                for (int k = 0; k < obstacles.Length; k++)
                {
                    float R = radii[k];
                    if (R <= 0f) continue;
                    var d = p - obstacles[k];
                    float reach = R * 2.6f;
                    float sq = d.sqrMagnitude;
                    if (sq > reach * reach) continue;
                    float dist = Mathf.Sqrt(sq) + 1e-5f;
                    var nrm = d / dist;
                    float into = Vector3.Dot(v, nrm);
                    float closeness = 1f - dist / reach;
                    if (into < 0f) v -= nrm * into * Mathf.Min(1f, closeness * 1.6f);
                    if (dist < R) p = obstacles[k] + nrm * R;
                    hot = Mathf.Max(hot, closeness);
                }
                heat[i] = hot;
                velocity[i] = v;
                position[i] = p + v * dt;

                if (age[i] > lifetime)
                {
                    // Recycle: move the last particle into this slot.
                    alive--;
                    position[i] = position[alive];
                    velocity[i] = velocity[alive];
                    age[i] = age[alive];
                    band[i] = band[alive];
                    heat[i] = heat[alive];
                    i--;
                }
            }
        }

        void Draw()
        {
            for (int i = 0; i < alive; i++)
            {
                float life = age[i] / lifetime;
                float alpha = Mathf.Clamp01(age[i] / 0.25f) * (1f - life * life) * (0.55f + heat[i] * 0.6f) * fade;
                var c = Color.Lerp(BandColors[band[i]], Color.white, heat[i] * 0.45f);
                c.a = Mathf.Clamp01(alpha);
                output[i] = new ParticleSystem.Particle
                {
                    position = position[i],
                    velocity = velocity[i],
                    startColor = c,
                    startSize = 0.0035f + heat[i] * 0.005f,
                    startLifetime = lifetime,
                    remainingLifetime = Mathf.Max(0.01f, lifetime - age[i])
                };
            }
            system.SetParticles(output, alive);
        }

        /// <summary>The vent: a soft glow with a ring that breathes with the bass and a pulse ring on each kick.</summary>
        void DrawVent(MusicFrame music, Camera view, float dt)
        {
            if (music.Beat) ringAge = 0f;
            ringAge += dt;
            var facing = Quaternion.LookRotation(flow, Vector3.up);
            ventGlow.transform.position = source;
            ventGlow.transform.localScale = Vector3.one * (0.22f + music.Kick * 0.08f);
            Effects.Face(ventGlow.transform, view);
            block.Clear();
            block.SetColor(ColorId, Color.Lerp(Effects.Violet, Effects.Mint, music.Band(1)));
            block.SetFloat(IntensityId, (0.4f + music.Kick * 0.5f) * fade);
            ventGlow.SetPropertyBlock(block);

            ventRing.transform.SetPositionAndRotation(source, facing);
            ventRing.transform.localScale = Vector3.one * (0.17f + music.Band(1) * 0.03f);
            block.Clear();
            block.SetColor(ColorId, Effects.Mint);
            block.SetFloat(RadiusId, 0.8f);
            block.SetFloat(ThicknessId, 0.04f);
            block.SetFloat(AlphaId, (0.5f + music.Kick * 0.4f) * fade);
            ventRing.SetPropertyBlock(block);

            float u = Mathf.Clamp01(ringAge / 0.7f);
            ventRing2.transform.SetPositionAndRotation(source + flow * (u * 0.12f), facing);
            ventRing2.transform.localScale = Vector3.one * (0.17f + u * 0.2f);
            block.Clear();
            block.SetColor(ColorId, Effects.Violet);
            block.SetFloat(RadiusId, 0.8f);
            block.SetFloat(ThicknessId, 0.03f);
            block.SetFloat(AlphaId, (1f - u) * (1f - u) * fade);
            ventRing2.SetPropertyBlock(block);
        }
    }
}
