using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Air strings world: six glowing strings stretched across the space in front of you, each shimmering with
    /// its own frequency band. Push a fingertip into a string and it bends with your finger while your visible hand
    /// is held back and trembles (fake resistance). Pull away and it twangs: a plucked note in the song's key, a
    /// ringing vibration and a spray of sparks.
    /// </summary>
    public class AirStrings : World
    {
        [Header("Materials")]
        [SerializeField] Material lineMaterial;
        [SerializeField] Material glowMaterial;

        [Header("Layout")]
        [SerializeField] float span = 1.1f;
        [SerializeField] float distance = 0.42f;
        [Tooltip("Height of the lowest string relative to the eyes, in metres.")]
        [SerializeField] float lowest = -0.36f;
        [SerializeField] float spacing = 0.075f;
        [SerializeField] int points = 72;

        [Header("Touch")]
        [SerializeField] float grabRadius = 0.018f;
        [Tooltip("How far a string can be pulled before it slips off the finger and twangs.")]
        [SerializeField] float releaseDistance = 0.05f;
        [SerializeField] float resistance = 0.35f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly Color[] Colors = { Effects.Pink, Effects.Orange, Effects.Gold, Effects.Mint, Effects.Blue, Effects.Violet };
        // Lowest string first: A3 C4 D4 E4 G4 A4.
        static readonly int[] Notes = { 0, 1, 2, 3, 4, 5 };

        sealed class GuitarString
        {
            public LineStrip Line;
            public Renderer[] Posts;
            public float Ring;
            public Vector3 RingDirection;
            public bool Held;
            public bool HeldLeft;
            public int HeldFinger;
            public float HeldU;
            public Vector3 Pull;
            public float Glow;
        }

        GuitarString[] strings;
        Vector3[] path;
        AudioPool voices;
        ParticleSystem sparks;
        MaterialPropertyBlock block;
        Vector3 origin, right;
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
                    RingDirection = Vector3.up
                };
            }
            voices = new AudioPool(transform, 6);
            sparks = Effects.MakeSparkSystem("Sparks", transform, glowMaterial);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
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

            var leftOffset = Vector3.zero;
            var rightOffset = Vector3.zero;
            float t = Now;
            for (int i = 0; i < strings.Length; i++)
            {
                var s = strings[i];
                var up = Vector3.up * (lowest + i * spacing);
                var p0 = origin - right * (span * 0.5f) + up;
                var p1 = origin + right * (span * 0.5f) + up;
                Touch(rig, s, i, p0, p1, music);

                if (s.Held)
                {
                    // The finger is held back by the string and trembles with it.
                    var hand = rig.Get(s.HeldLeft);
                    var tremble = Random.insideUnitSphere * (0.0015f + s.Pull.magnitude * 0.03f);
                    var offset = -s.Pull * resistance + tremble;
                    if (s.HeldLeft) leftOffset += offset; else rightOffset += offset;
                    if (!hand.IsTracked) s.Held = false;
                }

                s.Ring *= Mathf.Exp(-dt * 2.6f);
                s.Glow = Mathf.MoveTowards(s.Glow, 0f, dt * 1.5f);
                float band = music.Band(i);
                float shimmer = 0.0012f + band * 0.005f;
                for (int j = 0; j < points; j++)
                {
                    float u = j / (float)(points - 1);
                    float envelope = Mathf.Sin(Mathf.PI * u);
                    var p = Vector3.Lerp(p0, p1, u);
                    p += Vector3.up * (Mathf.Sin(Mathf.PI * u * (i % 3 + 1)) * Mathf.Sin(t * (30f + i * 7f)) * shimmer);
                    p += s.RingDirection * (envelope * s.Ring * Mathf.Sin(t * (38f + i * 9f)));
                    if (s.Held)
                    {
                        float bend = u < s.HeldU ? u / Mathf.Max(s.HeldU, 1e-3f) : (1f - u) / Mathf.Max(1f - s.HeldU, 1e-3f);
                        p += s.Pull * bend;
                    }
                    path[j] = p;
                }

                var color = Colors[i];
                color.a = 0.75f + band * 0.35f + s.Glow * 0.5f;
                s.Line.Set(path, points, 0.006f * (1f + band * 0.8f + s.Glow * 1.5f + s.Ring * 20f), color, view.transform.position, false);

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
            }

            Effects.OffsetHand(rig.Left, leftOffset);
            Effects.OffsetHand(rig.Right, rightOffset);
        }

        void Touch(HandRig rig, GuitarString s, int index, Vector3 p0, Vector3 p1, MusicFrame music)
        {
            var dir = p1 - p0;
            float length = dir.magnitude;
            dir /= length;

            if (s.Held)
            {
                var hand = rig.Get(s.HeldLeft);
                var tip = hand.TipJoints[s.HeldFinger] != null ? hand.TipJoints[s.HeldFinger].position : hand.Tips[s.HeldFinger];
                // Measure the pull from where the finger would be without the resistance offset applied.
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
            voices.Play(Synth.Pluck(Synth.Pentatonic[Notes[index]]), at, 0.9f);
            for (int k = 0; k < 14; k++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = (Random.insideUnitSphere + s.RingDirection * 0.6f) * 0.12f,
                    startColor = Colors[index],
                    startSize = Random.Range(0.005f, 0.012f),
                    startLifetime = Random.Range(0.5f, 1.1f)
                };
                sparks.Emit(emit, 1);
            }
        }

        /// <summary>For previews: holds a string on a fingertip as if the player had pushed into it.</summary>
        public void PreviewHold(int index, bool left, int finger, float u, Vector3 pull)
        {
            Build();
            var s = strings[index];
            s.Held = true;
            s.HeldLeft = left;
            s.HeldFinger = finger;
            s.HeldU = u;
            s.Pull = pull;
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
