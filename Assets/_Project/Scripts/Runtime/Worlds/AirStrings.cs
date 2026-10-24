using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Air strings world: six glowing strings across the space in front of you, tuned to the notes of the song's
    /// melody. They wave with their frequency bands, and each beat sends a pulse running along them. A glowing bead
    /// slides along the string of each coming melody note and reaches the middle exactly when it is sung: pluck that
    /// string then and you play the melody with the song (a golden burst says you were on time). Push a fingertip in
    /// and the string bends with it while your visible hand is held back and trembles (fake resistance); pull away
    /// and it twangs with its note, a ringing wave and sparks.
    /// </summary>
    public class AirStrings : World
    {
        public override int MenuIcon => 3;

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.06f, 0.18f, 0.16f, 0.55f),
            Edge = new Color(0.49f, 0.95f, 0.79f),
            Glow = new Color(1f, 0.83f, 0.42f),
            Pattern = HandTheme.Effect.Electric,
            PatternColor = new Color(0.7f, 1f, 0.9f)
        };

        [Header("Materials")]
        [SerializeField] Material lineMaterial;
        [SerializeField] Material glowMaterial;
        [SerializeField] Material ringMaterial;

        [Header("Layout")]
        [SerializeField] float span = 1.1f;
        [SerializeField] float distance = 0.42f;
        [Tooltip("Height of the lowest string relative to the eyes, in metres.")]
        [SerializeField] float lowest = -0.42f;
        [SerializeField] float spacing = 0.062f;
        [SerializeField] int points = 72;

        [Header("Touch")]
        [SerializeField] float grabRadius = 0.018f;
        [Tooltip("How far a string can be pulled before it slips off the finger and twangs.")]
        [SerializeField] float releaseDistance = 0.05f;
        [SerializeField] float resistance = 0.35f;

        [Header("Melody guide")]
        [Tooltip("How many seconds ahead a note's bead appears on its string.")]
        [SerializeField] float lead = 1.6f;
        [Tooltip("How close to the note a pluck must be to count as on time, in seconds.")]
        [SerializeField] float window = 0.2f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly Color[] Colors = { Effects.Pink, Effects.Orange, Effects.Gold, Effects.Mint, Effects.Blue, Effects.Violet };
        // The melody's notes, lowest string first: E4 G4 A4 C5 D5 E5 (A minor pentatonic).
        static readonly int[] Tuning = { 64, 67, 69, 72, 74, 76 };
        const int Beads = 10;

        sealed class GuitarString
        {
            public LineStrip Line;
            public Renderer[] Posts;
            public Renderer Target;
            public float Ring;
            public Vector3 RingDirection = Vector3.up;
            public bool Held;
            public bool HeldLeft;
            public int HeldFinger;
            public float HeldU;
            public Vector3 Pull;
            public float Glow;
            public float Perfect;
            public float NextNote = -1f;
        }

        GuitarString[] strings;
        Renderer[] beads;
        Vector3[] path;
        AudioPool voices;
        ParticleSystem sparks;
        MaterialPropertyBlock block;
        readonly HashSet<float> playedNotes = new HashSet<float>();
        Vector3 origin, right;
        float beatTime = -10f, songTime;
        bool anchored, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            path = new Vector3[points];
            strings = new GuitarString[Colors.Length];
            for (int i = 0; i < strings.Length; i++)
            {
                strings[i] = new GuitarString
                {
                    Line = new LineStrip($"String {i}", transform, lineMaterial, points),
                    Posts = new[] { Effects.MakeQuad($"Post {i}a", transform, glowMaterial), Effects.MakeQuad($"Post {i}b", transform, glowMaterial) },
                    Target = Effects.MakeQuad($"Target {i}", transform, ringMaterial)
                };
            }
            beads = new Renderer[Beads];
            for (int i = 0; i < Beads; i++) beads[i] = Effects.MakeQuad($"Note Bead {i}", transform, glowMaterial);
            voices = new AudioPool(transform, 6);
            sparks = Effects.MakeSparkSystem("Sparks", transform, glowMaterial);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
            playedNotes.Clear();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (HandRig.Instance != null)
            {
                Effects.OffsetHand(HandRig.Instance.Left, Vector3.zero);
                Effects.OffsetHand(HandRig.Instance.Right, Vector3.zero);
            }
            if (strings != null) foreach (var s in strings) s.Held = false;
        }

        Vector3 End(int i, bool right0)
        {
            var up = Vector3.up * (lowest + i * spacing);
            return origin + right * (span * (right0 ? 0.5f : -0.5f)) + up;
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            if (!anchored)
            {
                // Hang the strings in front of where the player is looking when the world starts.
                var forward = Effects.FlatForward(view);
                origin = view.transform.position + forward * distance;
                right = Vector3.Cross(Vector3.up, forward).normalized;
                anchored = true;
            }
            songTime = music.SongTime;
            if (music.Beat) beatTime = Now;

            var leftOffset = Vector3.zero;
            var rightOffset = Vector3.zero;
            float t = Now;
            float pulseAge = t - beatTime;
            foreach (var s in strings) s.NextNote = -1f;
            UpdateGuide(music, view);

            for (int i = 0; i < strings.Length; i++)
            {
                var s = strings[i];
                var p0 = End(i, false);
                var p1 = End(i, true);
                Touch(rig, s, i, p0, p1);

                if (s.Held)
                {
                    // The finger is held back by the string and trembles with it.
                    var tremble = Random.insideUnitSphere * (0.0015f + s.Pull.magnitude * 0.03f);
                    var offset = -s.Pull * resistance + tremble;
                    if (s.HeldLeft) leftOffset += offset; else rightOffset += offset;
                    if (!rig.Get(s.HeldLeft).IsTracked) s.Held = false;
                }

                s.Ring *= Mathf.Exp(-dt * 2.4f);
                s.Glow = Mathf.MoveTowards(s.Glow, 0f, dt * 1.5f);
                s.Perfect = Mathf.MoveTowards(s.Perfect, 0f, dt * 1.2f);
                float band = music.Band(i);
                // Standing waves that grow with the string's band, like the concept page.
                float wave = 0.003f + band * 0.012f;
                for (int j = 0; j < points; j++)
                {
                    float u = j / (float)(points - 1);
                    float envelope = Mathf.Sin(Mathf.PI * u);
                    var p = Vector3.Lerp(p0, p1, u);
                    p += Vector3.up * (Mathf.Sin(Mathf.PI * u * (i % 3 + 1)) * Mathf.Sin(t * (6f + i * 1.3f)) * wave * envelope);
                    // Each beat sends a pulse running along every string, slightly staggered.
                    float front = (pulseAge - i * 0.04f) * 1.8f;
                    p += Vector3.up * (Mathf.Exp(-(u - front) * (u - front) / 0.004f) * 0.012f * Mathf.Exp(-pulseAge * 1.6f) * envelope);
                    p += s.RingDirection * (envelope * s.Ring * Mathf.Sin(t * (38f + i * 9f)));
                    if (s.Held)
                    {
                        float bend = u < s.HeldU ? u / Mathf.Max(s.HeldU, 1e-3f) : (1f - u) / Mathf.Max(1f - s.HeldU, 1e-3f);
                        p += s.Pull * bend;
                    }
                    path[j] = p;
                }

                var color = Color.Lerp(Colors[i], Color.white, s.Perfect * 0.5f);
                color.a = 0.75f + band * 0.35f + s.Glow * 0.5f;
                s.Line.Set(path, points, 0.008f * (1f + band * 0.8f + s.Glow * 1.5f + s.Ring * 20f + s.Perfect), color, view.transform.position, false);

                for (int k = 0; k < 2; k++)
                {
                    var post = s.Posts[k];
                    post.transform.position = k == 0 ? p0 : p1;
                    post.transform.localScale = Vector3.one * 0.022f;
                    Effects.Face(post.transform, view);
                    block.Clear();
                    block.SetColor(ColorId, Colors[i]);
                    block.SetFloat(IntensityId, 0.5f + band * 0.4f);
                    post.SetPropertyBlock(block);
                }

                // A soft ring marks where to pluck, brightening as this string's next note arrives.
                float due = s.NextNote < 0f ? 0f : 1f - Mathf.Clamp01((s.NextNote - songTime) / lead);
                bool showTarget = due > 0f;
                if (s.Target.gameObject.activeSelf != showTarget) s.Target.gameObject.SetActive(showTarget);
                if (showTarget)
                {
                    s.Target.transform.position = Vector3.Lerp(p0, p1, 0.5f);
                    s.Target.transform.localScale = Vector3.one * Mathf.Lerp(0.09f, 0.05f, due);
                    Effects.Face(s.Target.transform, view);
                    block.Clear();
                    block.SetColor(ColorId, Colors[i]);
                    block.SetFloat(RadiusId, 0.7f);
                    block.SetFloat(ThicknessId, 0.05f);
                    block.SetFloat(AlphaId, due * due);
                    s.Target.SetPropertyBlock(block);
                }
            }

            Effects.OffsetHand(rig.Left, leftOffset);
            Effects.OffsetHand(rig.Right, rightOffset);
        }

        static int StringFor(int midi)
        {
            int best = 0;
            for (int i = 1; i < Tuning.Length; i++)
                if (Mathf.Abs(Tuning[i] - midi) < Mathf.Abs(Tuning[best] - midi)) best = i;
            return best;
        }

        /// <summary>Beads slide in from the right along the string of each coming note, reaching the middle on time.</summary>
        void UpdateGuide(MusicFrame music, Camera view)
        {
            int used = 0;
            if (music.Upcoming != null)
            {
                foreach (var note in music.Upcoming)
                {
                    float ahead = note.Time - songTime;
                    if (ahead < -0.15f || ahead > lead || used >= beads.Length) continue;
                    int i = StringFor(note.Midi);
                    var s = strings[i];
                    if (s.NextNote < 0f || note.Time < s.NextNote) s.NextNote = note.Time;
                    if (playedNotes.Contains(note.Time)) continue;

                    float u = 0.5f + 0.47f * Mathf.Clamp01(ahead / lead);
                    var bead = beads[used++];
                    bead.gameObject.SetActive(true);
                    bead.transform.position = Vector3.Lerp(End(i, false), End(i, true), u);
                    float close = 1f - Mathf.Clamp01(Mathf.Abs(ahead) / lead);
                    bead.transform.localScale = Vector3.one * (0.03f + close * 0.025f);
                    Effects.Face(bead.transform, view);
                    block.Clear();
                    block.SetColor(ColorId, Colors[i]);
                    block.SetFloat(IntensityId, 0.6f + close * 1.2f);
                    bead.SetPropertyBlock(block);
                }
            }
            for (int k = used; k < beads.Length; k++)
                if (beads[k].gameObject.activeSelf) beads[k].gameObject.SetActive(false);
        }

        void Touch(HandRig rig, GuitarString s, int index, Vector3 p0, Vector3 p1)
        {
            var dir = p1 - p0;
            float length = dir.magnitude;
            dir /= length;

            if (s.Held)
            {
                var hand = rig.Get(s.HeldLeft);
                var tip = hand.TipJoints[s.HeldFinger] != null ? hand.TipJoints[s.HeldFinger].position : hand.Tips[s.HeldFinger];
                var rest = p0 + dir * (s.HeldU * length);
                var pull = Vector3.ProjectOnPlane(tip - rest, dir);
                s.Pull = Vector3.Lerp(s.Pull, pull, 0.6f);
                if (pull.magnitude > releaseDistance || !hand.IsTracked) Pluck(s, index, rest, pull);
                return;
            }

            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                if (!hand.IsTracked) continue;
                for (int f = 1; f < 5; f++)
                {
                    var tip = hand.TipJoints[f] != null ? hand.TipJoints[f].position : hand.Tips[f];
                    float u = Vector3.Dot(tip - p0, dir) / length;
                    if (u < 0.04f || u > 0.96f) continue;
                    var closest = p0 + dir * (u * length);
                    if (Vector3.Distance(tip, closest) > grabRadius) continue;
                    s.Held = true;
                    s.HeldLeft = side == 0;
                    s.HeldFinger = f;
                    s.HeldU = u;
                    s.Pull = Vector3.zero;
                    return;
                }
            }
        }

        void Pluck(GuitarString s, int index, Vector3 at, Vector3 pull)
        {
            s.Held = false;
            s.Ring = Mathf.Clamp(pull.magnitude * 0.5f, 0.006f, 0.025f);
            s.RingDirection = pull.sqrMagnitude > 1e-8f ? pull.normalized : Vector3.up;
            s.Glow = 1f;
            s.Pull = Vector3.zero;

            // On time with the melody? Then it is that note, played with the song.
            bool onTime = s.NextNote >= 0f && Mathf.Abs(s.NextNote - songTime) < window;
            if (onTime)
            {
                playedNotes.Add(s.NextNote);
                s.Perfect = 1f;
            }
            var frequency = 440f * Mathf.Pow(2f, (Tuning[index] - 69) / 12f);
            voices.Play(Synth.Pluck(frequency), at, onTime ? 1f : 0.8f);

            int count = onTime ? 28 : 14;
            for (int k = 0; k < count; k++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = (Random.insideUnitSphere + s.RingDirection * 0.6f) * (onTime ? 0.2f : 0.12f),
                    startColor = onTime && k % 2 == 0 ? Effects.Gold : Colors[index],
                    startSize = Random.Range(0.005f, onTime ? 0.016f : 0.012f),
                    startLifetime = Random.Range(0.5f, 1.2f)
                };
                sparks.Emit(emit, 1);
            }
        }

        /// <summary>For previews: rings a string as if it had just been plucked.</summary>
        public void PreviewPluck(int index, float amount)
        {
            Build();
            strings[index].Ring = amount;
            strings[index].Glow = 1f;
        }
    }
}
