using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Sand table world: a dark glass plate at table height becomes a vibrating Chladni plate. Sand on it is
    /// shaken where the plate moves and comes to rest on the lines that stay still, so it gathers into the true
    /// shapes of sound: cos(n x) cos(m y) - cos(m x) cos(n y) for vibration mode n, m. The music shakes it harder on
    /// each kick and moves to a new mode every eight bars of beats; hover a hand over the plate and sliding it from
    /// left to right picks the mode yourself, lower is stronger. The plate lights your hand from below.
    /// </summary>
    public class SandTable : World
    {
        public override int MenuIcon => 11;

        public override string Hint => "Hover a hand over the plate and slide it across";

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
        [SerializeField] Material glowMaterial;

        [Header("Plate")]
        [SerializeField] float plateSize = 0.46f;
        [SerializeField] float distance = 0.48f;
        [SerializeField] float belowEyes = 0.52f;
        [SerializeField] int grains = 2400;
        [SerializeField] float grainSize = 0.0045f;
        [Tooltip("How high above the plate (m) a hand still counts as hovering over it.")]
        [SerializeField] float hoverHeight = 0.3f;

        // Vibration modes from simple to busy, as on the concept page.
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
        TextMesh label;
        ParticleSystem sand;
        ParticleSystem.Particle[] output;
        Vector2[] grain;
        float[] hop;
        float[] tone;
        MaterialPropertyBlock block;
        System.Random random;
        int mode = -1, beats;
        float shake, fade;
        bool anchored, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            random = new System.Random(5);
            grain = new Vector2[grains];
            hop = new float[grains];
            tone = new float[grains];
            output = new ParticleSystem.Particle[grains];
            ScatterSand();

            plate = new GameObject("Plate").transform;
            plate.SetParent(transform, false);
            plateRenderer = Effects.MakeQuad("Plate Surface", plate, plateMaterial);
            // The quad faces -Z; lay it flat facing up.
            plateRenderer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            plateRenderer.transform.localScale = Vector3.one * plateSize;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelGo = new GameObject("Mode");
            labelGo.transform.SetParent(plate, false);
            label = labelGo.AddComponent<TextMesh>();
            label.font = font;
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.fontSize = 40;
            label.characterSize = 0.0012f;
            label.anchor = TextAnchor.MiddleLeft;
            labelGo.transform.localPosition = new Vector3(-plateSize * 0.42f, 0.002f, -plateSize * 0.42f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

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

            plate.gameObject.SetActive(false);
        }

        void ScatterSand()
        {
            for (int i = 0; i < grains; i++)
            {
                grain[i] = new Vector2((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f);
                tone[i] = (float)random.NextDouble();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
            fade = 0f;
            mode = -1;
            if (grain != null) ScatterSand();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (plate != null) plate.gameObject.SetActive(false);
            if (sand != null) sand.Clear();
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            if (anchored && ShouldReanchor(view, dt)) anchored = false;
            if (!anchored)
            {
                var forward = Effects.FlatForward(view);
                var center = view.transform.position + forward * distance + Vector3.down * belowEyes;
                center.y = Mathf.Max(center.y, Effects.FloorY(view) + 0.45f);
                plate.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
                plate.gameObject.SetActive(true);
                MarkAnchored(view);
                anchored = true;
            }
            fade = Mathf.MoveTowards(fade, 1f, dt * 2f);

            // A hovering hand picks the mode by where it is across the plate; otherwise the music moves it on.
            float press = Hover(rig, out float across);
            if (music.Beat) beats++;
            int wanted = press > 0f
                ? Mathf.Clamp(Mathf.FloorToInt((across * 0.5f + 0.5f) * Modes.Length), 0, Modes.Length - 1)
                : (beats / 8) % Modes.Length;
            if (wanted != mode)
            {
                if (mode >= 0) shake = 1f;
                mode = wanted;
            }
            shake *= Mathf.Exp(-dt * 1.8f);
            HandSkin.FocusPosition = plate.position;
            HandSkin.FocusStrength = Mathf.Max(press, 0.25f) * fade;

            float amp = 0.02f + music.Kick * 0.06f + music.Band(1) * 0.04f + shake * 0.06f + press * 0.04f;
            Shake(Modes[mode], amp, dt);
            Draw(music, amp);

            block.Clear();
            block.SetFloat(NId, Modes[mode].x);
            block.SetFloat(MId, Modes[mode].y);
            block.SetFloat(KickId, music.Kick);
            block.SetFloat(AlphaId, fade);
            plateRenderer.SetPropertyBlock(block);
            label.text = $"mode {Modes[mode].x}:{Modes[mode].y}";
            label.color = new Color(Sand.r, Sand.g, Sand.b, 0.45f * fade);
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
                if (!hand.IsTracked) continue;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                var local = plate.InverseTransformPoint(palm) / (plateSize * 0.5f);
                float height = plate.InverseTransformPoint(palm).y;
                if (Mathf.Abs(local.x) > 1.1f || Mathf.Abs(local.z) > 1.1f || height < -0.05f || height > hoverHeight) continue;
                float strength = 1f - Mathf.Clamp01(height / hoverHeight);
                if (strength > best)
                {
                    best = strength;
                    across = Mathf.Clamp(local.x, -1f, 1f);
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
                var p = grain[i];
                float x = (p.x + 1f) * halfPi, y = (p.y + 1f) * halfPi;
                float f = Mathf.Cos(m.x * x) * Mathf.Cos(m.y * y) - Mathf.Cos(m.y * x) * Mathf.Cos(m.x * y);
                float jolt = (Mathf.Abs(f) + 0.015f) * amp * frames;
                p.x = Mathf.Clamp(p.x + ((float)random.NextDouble() - 0.5f) * jolt, -0.95f, 0.95f);
                p.y = Mathf.Clamp(p.y + ((float)random.NextDouble() - 0.5f) * jolt, -0.95f, 0.95f);
                grain[i] = p;
                hop[i] = Mathf.Lerp(hop[i], Mathf.Abs(f) * amp, 0.5f);
            }
        }

        void Draw(MusicFrame music, float amp)
        {
            float half = plateSize * 0.5f * 0.94f;
            for (int i = 0; i < grains; i++)
            {
                // Grains on the moving parts hop off the plate a little; settled ones glint.
                float lift = 0.0015f + hop[i] * 0.12f * (0.5f + tone[i]);
                var local = new Vector3(grain[i].x * half, lift, grain[i].y * half);
                float settled = 1f - Mathf.Clamp01(hop[i] / Mathf.Max(amp, 1e-4f) * 2f);
                var c = Color.Lerp(Sand * 0.75f, Color.Lerp(Sand, Color.white, 0.35f), tone[i] * 0.5f + settled * 0.5f);
                c.a = (0.55f + settled * 0.4f + music.Kick * 0.1f) * fade;
                output[i] = new ParticleSystem.Particle
                {
                    position = plate.TransformPoint(local),
                    startColor = c,
                    startSize = grainSize * (0.75f + tone[i] * 0.5f),
                    startLifetime = 10f,
                    remainingLifetime = 10f
                };
            }
            sand.SetParticles(output, grains);
        }
    }
}
