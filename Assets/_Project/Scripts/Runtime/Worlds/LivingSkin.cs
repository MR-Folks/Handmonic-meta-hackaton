using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Living skin world: your hands are made of the music. Each fingertip carries a soft light tuned to one
    /// frequency band, every beat sends a wave out of each palm, and sparks drift off the fingertips as they move.
    /// The pulse that runs through the hand itself lives in the hand shader (see HandSkin).
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class LivingSkin : World
    {
        public override int MenuIcon => 0;

        public override string Hint => "Hold up your hands and spread your fingers";

        [Header("Materials")]
        [SerializeField] Material glowMaterial;
        [SerializeField] Material ringMaterial;

        [Header("Fingertip lights")]
        [SerializeField] float tipSize = 0.045f;
        [Tooltip("Thumb to little finger. Each finger follows one band: bass, low mids, mids, presence, air.")]
        [SerializeField] Color[] fingerColors =
        {
            new Color(1f, 0.83f, 0.42f), new Color(0.49f, 0.95f, 0.79f), new Color(0.5f, 0.72f, 1f),
            new Color(0.79f, 0.61f, 1f), new Color(1f, 0.56f, 0.78f)
        };

        [Header("Palm waves")]
        [SerializeField] float waveRadius = 0.14f;
        [SerializeField] float waveLife = 1.1f;
        [SerializeField] Color waveColor = new Color(0.62f, 0.56f, 1f);
        [SerializeField] Color waveBeatColor = new Color(1f, 0.37f, 0.68f);
        [SerializeField] int maxWaves = 16;

        [Header("Sparks")]
        [SerializeField] float sparksPerMetre = 70f;
        [SerializeField] int sparksPerBeat = 3;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");

        sealed class Wave
        {
            public Transform Transform;
            public Renderer Renderer;
            public float Age;
            public float Strength;
        }

        readonly List<Wave> waves = new List<Wave>();
        readonly Transform[] tipLights = new Transform[10];
        readonly Renderer[] tipRenderers = new Renderer[10];
        readonly Vector3[] lastTips = new Vector3[10];
        readonly bool[] hadTip = new bool[10];
        readonly float[] sparkDebt = new float[10];
        ParticleSystem sparks;
        MaterialPropertyBlock block;
        bool built;

        protected override void OnDisable()
        {
            base.OnDisable();
            // Fingertip lights ride on the hand joints, outside this object, so take them back and hide them.
            for (int i = 0; i < tipLights.Length; i++)
            {
                if (tipLights[i] == null) continue;
                tipLights[i].SetParent(transform, false);
                tipLights[i].gameObject.SetActive(false);
                hadTip[i] = false;
            }
            foreach (var wave in waves) wave.Transform.gameObject.SetActive(false);
            if (sparks != null) sparks.Clear();
        }

        /// <summary>Creates the lights, the wave pool and the spark system. Also called by the editor preview.</summary>
        public override void Build()
        {
            if (built) return;
            built = true;
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
                var quad = MakeQuad($"Tip Light {i}", tipMaterial);
                tipLights[i] = quad.transform;
                tipRenderers[i] = quad.GetComponent<Renderer>();
                quad.SetActive(false);
            }

            for (int i = 0; i < maxWaves; i++)
            {
                var quad = MakeQuad($"Wave {i}", ringMaterial);
                quad.SetActive(false);
                waves.Add(new Wave { Transform = quad.transform, Renderer = quad.GetComponent<Renderer>(), Age = -1f });
            }

            sparks = Effects.MakeSparkSystem("Sparks", transform, glowMaterial);
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            UpdateHand(rig.Left, 0, music.Bands, music.Kick, view, dt, music.Beat);
            UpdateHand(rig.Right, 1, music.Bands, music.Kick, view, dt, music.Beat);
            if (music.Beat)
            {
                float strength = 0.6f + 0.4f * music.Kick;
                if (rig.Left.IsTracked) SpawnWaves(rig.Left, strength);
                if (rig.Right.IsTracked) SpawnWaves(rig.Right, strength);
            }
            AdvanceWaves(dt);
        }

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

                EmitSparks(i, f, tip, beat);
            }
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

        void SpawnWaves(HandRig.Hand hand, float strength)
        {
            Spawn(hand, 0f, strength);
            // A softer echo just behind the first wave makes each beat feel deeper.
            Spawn(hand, -0.14f, strength * 0.5f);
        }

        void Spawn(HandRig.Hand hand, float age, float strength)
        {
            Wave wave = null;
            foreach (var w in waves)
            {
                if (!w.Transform.gameObject.activeSelf) { wave = w; break; }
            }
            if (wave == null) return;

            var normal = hand.PalmNormal.sqrMagnitude > 0.5f ? hand.PalmNormal : Vector3.forward;
            var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
            wave.Transform.SetParent(transform, false);
            wave.Transform.SetPositionAndRotation(palm + normal * 0.015f, Quaternion.LookRotation(normal));
            wave.Transform.localScale = Vector3.one * waveRadius * 2f;
            wave.Age = age;
            wave.Strength = strength;
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
            bool started = wave.Age >= 0f;
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            block.Clear();
            block.SetColor(ColorId, Color.Lerp(waveBeatColor, waveColor, Mathf.SmoothStep(0f, 1f, u * 1.6f)));
            block.SetFloat(RadiusId, Mathf.Lerp(0.04f, 0.85f, eased));
            block.SetFloat(ThicknessId, Mathf.Lerp(0.012f, 0.03f, u));
            block.SetFloat(AlphaId, started ? Mathf.Pow(1f - u, 2.2f) * wave.Strength : 0f);
            wave.Renderer.SetPropertyBlock(block);
        }

        GameObject MakeQuad(string objectName, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = objectName;
            DebugShapes.RemoveCollider(quad);
            quad.transform.SetParent(transform, false);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return quad;
        }
    }
}
