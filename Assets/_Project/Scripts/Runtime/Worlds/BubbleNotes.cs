using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Bubble notes world: every melody note rises from below as a soap bubble, the moment it is sung, placed by
    /// pitch from left (low) to right (high); low notes make big bubbles, high notes small ones. They wobble with the
    /// bass as they float up. Pop one with a fingertip and you hear its note right at your finger, with a ring and a
    /// spray of droplets; brush one with your palm and it drifts away. Your skin has a soap-film sheen.
    /// </summary>
    public class BubbleNotes : World
    {
        public override string DisplayName => "Bubble notes";

        public override Color Color => new Color(1f, 0.6f, 0.85f);

        public override int MenuOrder => 80;

        public override int MenuIcon => 10;

        public override string Hint => "Pop the bubbles with a fingertip";

        /// <summary>The bubbles rise in front of you, within reach.</summary>
        public override Space Spaces => Space.InFront;

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.12f, 0.1f, 0.2f, 0.45f),
            Edge = new Color(0.85f, 0.82f, 1f),
            Glow = new Color(1f, 0.6f, 0.85f),
            Pattern = HandTheme.Effect.Film,
            PatternColor = new Color(0.8f, 0.9f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material bubbleMaterial;
        [SerializeField] Material ringMaterial;

        [Header("Bubbles")]
        [SerializeField] float distance = 0.5f;
        [SerializeField] float width = 0.7f;
        [Tooltip("Where bubbles start and where they pop on their own, relative to the eyes (m).")]
        [SerializeField] Vector2 heightRange = new Vector2(-0.5f, 0.35f);
        [SerializeField] Vector2 riseSpeed = new Vector2(0.09f, 0.14f);
        [SerializeField] int maxBubbles = 36;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int IconId = Shader.PropertyToID("_Icon");
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        static readonly int SelectedId = Shader.PropertyToID("_Selected");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        // Saturated soap-film colours, one per pitch.
        static readonly Color[] PitchColors =
        {
            new Color(1f, 0.3f, 0.62f), new Color(1f, 0.6f, 0.25f), new Color(1f, 0.85f, 0.3f),
            new Color(0.35f, 1f, 0.75f), new Color(0.35f, 0.65f, 1f), new Color(0.72f, 0.45f, 1f)
        };

        sealed class Bubble
        {
            public Renderer Renderer;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Radius;
            public int Midi;
            public float Age;
            public float Phase;
            public float Near;
            public bool Active;
        }

        sealed class Pop
        {
            public Renderer Ring;
            public Renderer Flash;
            public float Age = -1f;
            public float Size;
            public Color Color;
            public bool ByHand;
        }

        readonly List<Bubble> bubbles = new List<Bubble>();
        readonly List<Pop> pops = new List<Pop>();
        readonly HashSet<float> spawned = new HashSet<float>();
        readonly float[] lastBands = new float[MusicBus.BandCount];
        readonly float[] cooldown = new float[MusicBus.BandCount];
        MaterialPropertyBlock block;
        AudioPool voices;
        ParticleSystem droplets;
        Vector3 center, right, forward;
        float eyeY, songTime;
        MusicFrame frame;

        protected override int SungWordCount => 6;

        protected override float SungWordHeight => 0.03f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            for (int i = 0; i < maxBubbles; i++)
            {
                var b = new Bubble { Renderer = Effects.MakeQuad($"Bubble {i}", transform, bubbleMaterial) };
                b.Renderer.gameObject.SetActive(false);
                bubbles.Add(b);
            }
            for (int i = 0; i < 10; i++)
            {
                var p = new Pop { Ring = Effects.MakeQuad($"Pop {i}", transform, ringMaterial), Flash = Effects.MakeQuad($"Pop Flash {i}", transform, glowMaterial) };
                p.Ring.gameObject.SetActive(false);
                p.Flash.gameObject.SetActive(false);
                pops.Add(p);
            }
            voices = new AudioPool(transform, 6);
            droplets = Effects.MakeSparkSystem("Droplets", transform, glowMaterial);
            var main = droplets.main;
            main.gravityModifier = 0.25f;
        }

        protected override void OnEnter()
        {
            spawned.Clear();
        }

        protected override void OnExit()
        {
            foreach (var b in bubbles) Release(b);
            foreach (var p in pops)
            {
                p.Age = -1f;
                p.Ring.gameObject.SetActive(false);
                p.Flash.gameObject.SetActive(false);
            }
            droplets.Clear();
        }

        /// <summary>The bubbles rise in a band in front of where you look, starting below your eyes.</summary>
        protected override void OnPlace(Camera view)
        {
            forward = Effects.FlatForward(view);
            right = Vector3.Cross(Vector3.up, forward).normalized;
            center = view.transform.position + forward * distance;
            eyeY = view.transform.position.y;
        }

        /// <summary>
        /// Each sung word rides up inside the newest bubble, sized to fit it. Pop the bubble and the word goes with it.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            int newest = -1;
            for (int i = 0; i < bubbles.Count; i++)
                if (bubbles[i].Active && (newest < 0 || bubbles[i].Age < bubbles[newest].Age)) newest = i;
            if (newest < 0) return;
            var b = bubbles[newest];
            var w = SungWords.Add(word, Color.Lerp(PitchColors[Mathf.Abs(b.Midi) % PitchColors.Length], Color.white, 0.55f), 6f);
            w.Slot = newest;
            w.Position = b.Position;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            songTime = music.SongTime;
            frame = music;
            if (music.Upcoming != null && music.Upcoming.Count > 0) SpawnFromNotes(music);
            else SpawnFromAnalysis(music, dt);
            Float(rig, music, view, dt);
            UpdatePops(view, dt);

            // The sung words ride in their bubbles.
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                var b = bubbles[w.Slot];
                if (!b.Active)
                {
                    // Popped: the word lets go and fades.
                    w.Life = Mathf.Min(w.Life, w.Age + 0.3f);
                    w.Size *= 1f + dt * 2f;
                    continue;
                }
                w.Size = Mathf.Clamp(b.Radius * 1.5f / Mathf.Max(w.Width * 0.03f, 1e-3f), 0.5f, 1.4f);
                w.Position = b.Position + (eye - b.Position).normalized * 0.005f + Vector3.down * (SungWords.Height(w) * 0.35f);
                w.Rotation = WordLayer.Facing(w.Position, eye);
                w.Halo = 0.15f;
            }
        }

        /// <summary>A bubble for each melody note, released the moment the note is sung.</summary>
        void SpawnFromNotes(MusicFrame music)
        {
            foreach (var note in music.Upcoming)
            {
                float ahead = note.Time - songTime;
                if (ahead > 0.05f || ahead < -0.3f || spawned.Contains(note.Time)) continue;
                spawned.Add(note.Time);
                Spawn(note.Midi, Mathf.Sin(note.Time * 78.233f));
            }
            if (spawned.Count > 256) spawned.Clear();
        }

        void SpawnFromAnalysis(MusicFrame music, float dt)
        {
            for (int b = 2; b < MusicBus.BandCount; b++)
            {
                cooldown[b] -= dt;
                float level = music.Band(b);
                if (level - lastBands[b] > 0.16f && level > 0.45f && cooldown[b] <= 0f)
                {
                    Spawn(60 + b * 3 + Random.Range(0, 4), Random.Range(-1f, 1f));
                    cooldown[b] = 0.25f;
                }
                lastBands[b] = level;
            }
        }

        void Spawn(int midi, float depthJitter)
        {
            Bubble bubble = null;
            foreach (var b in bubbles) if (!b.Active) { bubble = b; break; }
            if (bubble == null) return;
            float x = Mathf.Clamp01((midi - 60f) / 20f) * 2f - 1f;
            bubble.Active = true;
            bubble.Midi = midi;
            bubble.Radius = Mathf.Lerp(0.065f, 0.034f, Mathf.Clamp01((midi - 60f) / 18f)) * Random.Range(0.9f, 1.1f);
            bubble.Position = center + right * (x * width * 0.5f + Random.Range(-0.03f, 0.03f)) + forward * (depthJitter * 0.12f);
            bubble.Position.y = eyeY + heightRange.x;
            bubble.Velocity = Vector3.up * Random.Range(riseSpeed.x, riseSpeed.y);
            bubble.Age = 0f;
            bubble.Phase = Random.value * 10f;
            bubble.Near = 0f;
            bubble.Renderer.gameObject.SetActive(true);
        }

        void Float(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            float t = Now;
            float bass = music.Band(1);
            foreach (var b in bubbles)
            {
                if (!b.Active) continue;
                b.Age += dt;
                // Rise with a gentle sideways sway, easing back to the rising speed after a push.
                float rise = Mathf.Lerp(riseSpeed.x, riseSpeed.y, Mathf.Repeat(b.Phase, 1f));
                var sway = right * (Mathf.Sin(t * 1.3f + b.Phase) * 0.025f) + forward * (Mathf.Cos(t * 0.9f + b.Phase * 1.7f) * 0.012f);
                b.Velocity = Vector3.Lerp(b.Velocity, Vector3.up * rise + sway, dt * 1.2f);

                if (rig != null && TouchHands(rig, b)) continue;

                // Bubbles nudge each other apart instead of overlapping.
                foreach (var o in bubbles)
                {
                    if (o == b || !o.Active) continue;
                    var d = b.Position - o.Position;
                    float gap = b.Radius + o.Radius;
                    if (d.sqrMagnitude < gap * gap && d.sqrMagnitude > 1e-8f) b.Velocity += d.normalized * (gap - d.magnitude) * 4f * dt * 30f;
                }
                b.Position += b.Velocity * dt;

                if (b.Position.y > eyeY + heightRange.y)
                {
                    // Too high: it pops quietly by itself.
                    Burst(b, false);
                    continue;
                }

                float appear = Mathf.Clamp01(b.Age / 0.35f);
                float wobble = Mathf.Sin(t * 7f + b.Phase) * (0.03f + bass * 0.07f);
                float r = b.Radius * (0.6f + 0.4f * appear) * (1f + music.Kick * 0.06f);
                b.Renderer.transform.position = b.Position;
                Effects.Face(b.Renderer.transform, view);
                b.Renderer.transform.localScale = new Vector3(r * 2f * (1f + wobble), r * 2f * (1f - wobble), 1f);
                block.Clear();
                block.SetColor(ColorId, PitchColors[Mathf.Abs(b.Midi) % PitchColors.Length]);
                block.SetFloat(IconId, -1f);
                // Brighter than a menu bubble: a lit rim and a stronger sheen, more so as a finger comes close.
                block.SetFloat(HoverId, 0.6f + b.Near * 0.4f);
                block.SetFloat(SelectedId, 0.55f);
                block.SetFloat(AlphaId, appear);
                b.Renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>A fingertip pops the bubble; a palm pushes it away. Returns true if it popped.</summary>
        bool TouchHands(HandRig rig, Bubble b)
        {
            float nearest = 1f;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                // While the menu is open the fingers are choosing there, not popping.
                if (!hand.IsTracked || MenuOpen) continue;
                for (int k = 0; k < 5; k++)
                {
                    var tip = hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k];
                    float d = Vector3.Distance(tip, b.Position);
                    if (d < b.Radius * 0.95f && b.Age > 0.25f)
                    {
                        Musical.Play(voices, frame, b.Midi, tip, 0.4f);
                        Burst(b, true);
                        return true;
                    }
                    nearest = Mathf.Min(nearest, (d - b.Radius) / 0.06f);
                }
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                var away = b.Position - palm;
                float reach = b.Radius + 0.05f;
                if (away.sqrMagnitude < reach * reach && away.sqrMagnitude > 1e-8f)
                    b.Velocity += away.normalized * 0.25f * (1f - away.magnitude / reach);
            }
            b.Near = Mathf.Clamp01(1f - nearest);
            return false;
        }

        void Burst(Bubble b, bool byHand)
        {
            var color = PitchColors[Mathf.Abs(b.Midi) % PitchColors.Length];
            Pop pop = null;
            float oldest = -1f;
            foreach (var p in pops)
            {
                if (p.Age < 0f) { pop = p; break; }
                if (p.Age > oldest) { oldest = p.Age; pop = p; }
            }
            pop.Age = 0f;
            pop.Size = b.Radius * 2f;
            pop.Color = color;
            pop.Ring.transform.position = b.Position;
            pop.Ring.gameObject.SetActive(true);
            pop.Flash.transform.position = b.Position;
            pop.Flash.gameObject.SetActive(byHand);
            pop.ByHand = byHand;

            int count = byHand ? 22 : 6;
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                droplets.Emit(new ParticleSystem.EmitParams
                {
                    position = b.Position + dir * b.Radius,
                    velocity = dir * Random.Range(0.2f, byHand ? 0.55f : 0.2f),
                    startColor = Color.Lerp(color, Color.white, Random.Range(0.2f, 0.6f)),
                    startSize = Random.Range(0.004f, byHand ? 0.011f : 0.008f),
                    startLifetime = Random.Range(0.35f, 0.7f)
                }, 1);
            }
            Release(b);
        }

        void UpdatePops(Camera view, float dt)
        {
            foreach (var p in pops)
            {
                if (p.Age < 0f) continue;
                p.Age += dt;
                float u = p.Age / 0.5f;
                if (u >= 1f)
                {
                    p.Age = -1f;
                    p.Ring.gameObject.SetActive(false);
                    p.Flash.gameObject.SetActive(false);
                    continue;
                }
                // A ring that bursts outwards, and (for a pop by hand) a quick flash of light.
                float grow = 1f - (1f - u) * (1f - u);
                p.Ring.transform.localScale = Vector3.one * p.Size * (1f + grow * (p.ByHand ? 2.6f : 1.4f));
                Effects.Face(p.Ring.transform, view);
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(Color.white, p.Color, u));
                block.SetFloat(RadiusId, 0.8f);
                block.SetFloat(ThicknessId, 0.07f * (1f - u) + 0.012f);
                block.SetFloat(AlphaId, (1f - u) * (1f - u) * (p.ByHand ? 1.3f : 0.8f));
                p.Ring.SetPropertyBlock(block);

                if (!p.ByHand) continue;
                float flash = Mathf.Exp(-p.Age * 14f);
                p.Flash.transform.localScale = Vector3.one * p.Size * (1.6f + grow * 1.5f);
                Effects.Face(p.Flash.transform, view);
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(Color.white, p.Color, 0.4f));
                block.SetFloat(IntensityId, flash * 1.4f);
                p.Flash.SetPropertyBlock(block);
            }
        }

        static void Release(Bubble b)
        {
            b.Active = false;
            b.Renderer.gameObject.SetActive(false);
        }
    }
}
