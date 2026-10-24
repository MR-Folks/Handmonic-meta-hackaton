using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Firefly choir world: a swarm of warm lights drifts around you, blinking with the music. Hold out an open
    /// hand and they come to it, landing on your fingertips and circling your palm. Each kick startles some of them
    /// up and away before they settle again. Close your hand and they swirl around your fist.
    /// </summary>
    public class FireflyChoir : World
    {
        [SerializeField] Material glowMaterial;
        [SerializeField] int count = 44;
        [SerializeField] float cloudDistance = 0.6f;
        [SerializeField] float cloudRadius = 0.45f;
        [SerializeField] float flySize = 0.016f;
        [Tooltip("Share of the swarm that a kick startles.")]
        [SerializeField, Range(0f, 1f)] float startle = 0.4f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly Color Warm = new Color(1f, 0.83f, 0.45f);
        static readonly Color Ember = new Color(1f, 0.6f, 0.3f);
        static readonly Color Cool = new Color(0.75f, 0.95f, 0.7f);

        sealed class Fly
        {
            public Renderer Glow;
            public Vector3 Position;
            public Vector3 Velocity;
            public Vector3 Home;
            public float Phase;
            public float Speed;
            public float Startled;
            public Color Color;
            public float Landed;
        }

        Fly[] flies;
        Renderer[] palmGlows;
        MaterialPropertyBlock block;
        Vector3 center;
        bool anchored, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            flies = new Fly[count];
            var random = new System.Random(11);
            for (int i = 0; i < count; i++)
            {
                float r = (float)random.NextDouble();
                flies[i] = new Fly
                {
                    Glow = Effects.MakeQuad($"Firefly {i}", transform, glowMaterial),
                    Home = new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1),
                    Phase = (float)random.NextDouble() * 20f,
                    Speed = 0.6f + (float)random.NextDouble() * 0.8f,
                    Color = r < 0.6f ? Warm : r < 0.85f ? Ember : Cool
                };
                flies[i].Glow.gameObject.SetActive(false);
            }
            palmGlows = new[] { Effects.MakeQuad("Palm Glow L", transform, glowMaterial), Effects.MakeQuad("Palm Glow R", transform, glowMaterial) };
            foreach (var g in palmGlows) g.gameObject.SetActive(false);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (flies == null) return;
            foreach (var f in flies) f.Glow.gameObject.SetActive(false);
            foreach (var g in palmGlows) g.gameObject.SetActive(false);
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            float t = Now;
            if (!anchored)
            {
                center = view.transform.position + Effects.FlatForward(view) * cloudDistance + Vector3.down * 0.1f;
                foreach (var f in flies)
                {
                    f.Position = center + f.Home * cloudRadius;
                    f.Velocity = Vector3.zero;
                    f.Glow.gameObject.SetActive(true);
                }
                anchored = true;
            }

            // Which hands are calling the swarm, and how.
            var open = new bool[2];
            var fist = new bool[2];
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                open[side] = hand.IsTracked && hand.Fist < 0.45f;
                fist[side] = hand.IsTracked && hand.Fist >= 0.45f;
            }
            int handsCalling = (open[0] || fist[0] ? 1 : 0) + (open[1] || fist[1] ? 1 : 0);
            var landedOn = new float[2];

            for (int i = 0; i < flies.Length; i++)
            {
                var f = flies[i];
                if (music.Beat && Random.value < startle)
                {
                    f.Velocity += Random.onUnitSphere * Random.Range(0.4f, 0.9f) + Vector3.up * 0.3f;
                    f.Startled = 0.45f;
                }
                f.Startled -= dt;

                // Pick where this firefly wants to be.
                Vector3 target;
                float pullStrength;
                int side = handsCalling == 0 ? -1 : handsCalling == 2 ? i % 2 : (open[0] || fist[0] ? 0 : 1);
                if (side >= 0)
                {
                    var hand = rig.Get(side == 0);
                    var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                    if (fist[side])
                    {
                        float a = t * (1.6f + f.Speed) + f.Phase;
                        float radius = 0.08f + (i % 4) * 0.02f;
                        var axis = Quaternion.AngleAxis(f.Phase * 40f, Vector3.up) * Vector3.right;
                        target = palm + Quaternion.AngleAxis(a * Mathf.Rad2Deg, axis) * (Vector3.up * radius);
                        pullStrength = 10f;
                    }
                    else
                    {
                        int slot = i / 2 % 8;
                        if (slot < 5)
                        {
                            var tip = hand.TipJoints[slot] != null ? hand.TipJoints[slot].position : hand.Tips[slot];
                            target = tip + hand.PalmNormal * 0.012f + Wobble(f, t, 0.006f);
                        }
                        else
                        {
                            float a = t * 0.9f * f.Speed + f.Phase;
                            var orbit = Vector3.ProjectOnPlane(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), hand.PalmNormal).normalized;
                            target = palm + hand.PalmNormal * (0.04f + (slot - 5) * 0.02f) + orbit * (0.05f + (slot - 5) * 0.015f);
                        }
                        pullStrength = 9f;
                    }
                    if (Vector3.Distance(f.Position, target) < 0.02f) landedOn[side] += 1f;
                }
                else
                {
                    target = center + f.Home * cloudRadius + Wobble(f, t, 0.12f);
                    pullStrength = 1.6f;
                }

                if (f.Startled > 0f) pullStrength *= 0.15f;
                f.Velocity += (target - f.Position) * (pullStrength * dt);
                f.Velocity *= Mathf.Exp(-dt * (side >= 0 ? 6f : 2.2f));
                f.Position += f.Velocity * dt;
                f.Landed = Mathf.MoveTowards(f.Landed, Vector3.Distance(f.Position, target) < 0.02f ? 1f : 0f, dt * 4f);

                // Each firefly blinks on its own rhythm, brighter with the highs and when resting on a hand.
                float blink = 0.5f + 0.5f * Mathf.Sin(t * (2.4f + f.Speed * 2f) + f.Phase);
                float glow = 0.35f + blink * 0.55f + music.Band(5) * 0.5f + f.Landed * 0.4f + music.Kick * 0.25f;
                f.Glow.transform.position = f.Position;
                f.Glow.transform.localScale = Vector3.one * flySize * (1.6f + blink * 0.6f + f.Landed * 0.5f);
                Effects.Face(f.Glow.transform, view);
                block.Clear();
                block.SetColor(ColorId, f.Color);
                block.SetFloat(IntensityId, glow);
                f.Glow.SetPropertyBlock(block);
            }

            // A warm glow on the palm that grows with the fireflies resting on the hand.
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                var g = palmGlows[side];
                float share = landedOn[side] / Mathf.Max(1f, flies.Length * 0.5f);
                bool show = hand.IsTracked && share > 0.05f;
                if (g.gameObject.activeSelf != show) g.gameObject.SetActive(show);
                if (!show) continue;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                g.transform.position = palm + hand.PalmNormal * 0.015f;
                g.transform.localScale = Vector3.one * (0.09f + share * 0.08f);
                Effects.Face(g.transform, view);
                block.Clear();
                block.SetColor(ColorId, Warm);
                block.SetFloat(IntensityId, share * 0.8f);
                g.SetPropertyBlock(block);
            }
        }

        static Vector3 Wobble(Fly f, float t, float size) => new Vector3(
            Mathf.Sin(t * 0.9f * f.Speed + f.Phase),
            Mathf.Sin(t * 1.3f * f.Speed + f.Phase * 1.7f),
            Mathf.Sin(t * 0.7f * f.Speed + f.Phase * 2.3f)) * size;
    }
}
