using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Orbit type world: the lyrics as solid 3D type in your room, in white and soft grey. The word being sung
    /// arrives in front of you in big letters with real depth, each letter flying in from the orbits with a spin and
    /// pulsing on the kick. When the next word comes, the last one shrinks and joins an orbit around you, so the line
    /// builds up circling your head like planets, and when the line ends its words dissolve. Three tilted orbit rings
    /// with turning beads circle the room. Push the letters with your fingers, flick a ring to spin it, or make a fist
    /// to pull the word towards your hand. Your hands are printed in halftone dots.
    /// Left out of the menu and the scene for now, to keep the menu to fewer worlds: take [HiddenWorld] off and run
    /// Update Worlds in Main Scene to bring it back (and give it its ghost demo in GhostMotions.WorldTable again).
    /// </summary>
    [HiddenWorld]
    public class OrbitType : World
    {
        public override string DisplayName => "Orbit type";

        public override Color Color => new Color(0.88f, 0.89f, 0.93f);

        public override int MenuOrder => 120;

        public override int MenuIcon => 12;

        public override bool IsLyric => true;

        public override string Hint => "Push the letters, flick a ring, or make a fist";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.12f, 0.12f, 0.14f, 0.5f),
            Edge = new Color(0.88f, 0.89f, 0.93f),
            Glow = new Color(0.79f, 0.61f, 1f),
            Pattern = HandTheme.Effect.Halftone,
            PatternColor = new Color(0.95f, 0.95f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material lineMaterial;

        [Header("Type")]
        [Tooltip("How far in front of the eyes the sung word stands (m), and its height relative to the eyes.")]
        [SerializeField] float distance = 0.95f;
        [SerializeField] float height = -0.08f;
        [Tooltip("Letter height of the sung word, and of words in orbit (m).")]
        [SerializeField] float letterHeight = 0.24f;
        [SerializeField] float orbitLetterHeight = 0.075f;
        [Tooltip("Depth of the letters as a share of their height.")]
        [SerializeField] float depth = 0.16f;

        [Header("Orbits")]
        [SerializeField] float[] ringRadii = { 0.95f, 1.2f, 1.45f };
        [SerializeField] float[] ringTilts = { 12f, -18f, 26f };

        [Header("Touch")]
        [SerializeField] float pushRadius = 0.13f;
        [SerializeField] float pullRadius = 0.8f;

        static readonly Color Ink = new Color(0.97f, 0.97f, 0.99f);
        static readonly Color Shadow = new Color(0.32f, 0.31f, 0.38f);
        static readonly Color Touch = new Color(0.79f, 0.61f, 1f);
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        const int Layers = 6;
        const int Pool = 96;
        const int BeadsPerRing = 36;

        enum Role { Hidden, Main, Orbit, Gone }

        sealed class Letter
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public char Char;
            public Role Role;
            public int Word;
            public float Offset;
            public float Width;
            public float Age;
            public float Delay;
            public float Size;
            public float Alpha;
            public Vector3 Position;
            public Vector3 Push;
            public Quaternion Rotation = Quaternion.identity;
            public float Spin;
        }

        readonly List<Letter> letters = new List<Letter>();
        readonly Vector3[,] lastTips = new Vector3[2, 5];
        Material material;
        MaterialPropertyBlock block;
        LineStrip[] rings;
        Vector3[] ringPath;
        ParticleSystem beads;
        ParticleSystem.Particle[] beadOut;
        float[] ringSpin, ringSpinVelocity, ringTouch;
        Vector3 head, forward, right;
        string word = "", wordKey = "";
        int wordCount, line = -1, fallbackIndex = -1;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            material = new Material(letteringMaterial) { name = "Orbit Lettering", mainTexture = TypeFace.Bold.Atlas };
            material.SetFloat("_Rim", 0f);
            for (int i = 0; i < Pool; i++)
            {
                var letter = new Letter();
                letter.Renderer = Glyphs.MakeLetter($"Letter {i}", transform, material, out letter.Mesh);
                letter.Renderer.gameObject.SetActive(false);
                letters.Add(letter);
            }
            int ringCount = ringRadii.Length;
            rings = new LineStrip[ringCount];
            ringPath = new Vector3[129];
            for (int r = 0; r < ringCount; r++) rings[r] = new LineStrip($"Orbit {r}", transform, lineMaterial, ringPath.Length);
            ringSpin = new float[ringCount];
            ringSpinVelocity = new float[ringCount];
            ringTouch = new float[ringCount];

            var go = new GameObject("Beads");
            go.transform.SetParent(transform, false);
            beads = go.AddComponent<ParticleSystem>();
            beads.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = beads.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = ringCount * BeadsPerRing;
            var emission = beads.emission;
            emission.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beadOut = new ParticleSystem.Particle[ringCount * BeadsPerRing];
        }

        protected override void OnEnter()
        {
            word = wordKey = "";
            line = -1;
            foreach (var l in letters) Hide(l);
        }

        protected override void OnExit()
        {
            foreach (var l in letters) Hide(l);
            foreach (var r in rings) r.SetVisible(false);
            beads.Clear();
        }

        static void Hide(Letter l)
        {
            l.Role = Role.Hidden;
            l.Renderer.gameObject.SetActive(false);
        }

        /// <summary>The orbits circle your head where you are, and the sung word stands in front of you.</summary>
        protected override void OnPlace(Camera view)
        {
            head = view.transform.position;
            forward = Effects.FlatForward(view);
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {

            UpdateWord(music);
            SpinRings(rig, dt);
            DrawRings(music, view);
            PlaceLetters(rig, music, view, dt);
            material.SetFloat(AlphaId, FadeIn);
        }

        /// <summary>The sung word in capitals; without lyrics, the song title's words change every four beats.</summary>
        void UpdateWord(MusicFrame music)
        {
            string next = music.Word;
            string key = $"{music.Lyric.Line}:{music.Lyric.Word}";
            if (string.IsNullOrEmpty(next))
            {
                if (music.LineWords != null) return;
                var title = music.Bus != null ? music.Bus.Title : "Music";
                var parts = title.Split(' ');
                int beat = music.Bus != null ? music.Bus.BeatCount / 4 : 0;
                if (beat == fallbackIndex) return;
                fallbackIndex = beat;
                next = parts[beat % parts.Length];
                key = "title:" + beat;
            }
            if (key == wordKey) return;
            wordKey = key;

            // A new line: the words in orbit from the last one dissolve.
            if (music.Lyric.Line != line)
            {
                line = music.Lyric.Line;
                foreach (var l in letters) if (l.Role == Role.Orbit) { l.Role = Role.Gone; l.Age = 0f; }
            }
            // The word on show goes into orbit.
            foreach (var l in letters) if (l.Role == Role.Main) { l.Role = Role.Orbit; l.Age = 0f; }

            word = next.ToUpperInvariant();
            wordCount++;
            float x = 0f;
            var chosen = new List<Letter>();
            foreach (char ch in word)
            {
                var l = Free();
                if (l == null) break;
                l.Char = ch;
                l.Size = letterHeight;
                l.Role = Role.Main;
                l.Word = wordCount;
                l.Age = 0f;
                l.Delay = chosen.Count * 0.045f;
                l.Alpha = 0f;
                l.Push = Vector3.zero;
                l.Spin = 180f + Random.Range(-60f, 60f);
                // Fly in from somewhere on the middle orbit.
                float a = Random.value * Mathf.PI * 2f;
                l.Position = head + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * ringRadii[Mathf.Min(1, ringRadii.Length - 1)];
                Rebuild(l);
                l.Offset = x + l.Width * 0.5f;
                x += l.Width;
                l.Renderer.gameObject.SetActive(true);
                chosen.Add(l);
            }
            foreach (var l in chosen) l.Offset -= x * 0.5f;
        }

        Letter Free()
        {
            foreach (var l in letters) if (l.Role == Role.Hidden) return l;
            // Out of letters: take the oldest one in orbit.
            Letter oldest = null;
            foreach (var l in letters) if (l.Role != Role.Main && (oldest == null || l.Word < oldest.Word)) oldest = l;
            return oldest;
        }

        void Rebuild(Letter l)
        {
            l.Width = Glyphs.Build(l.Mesh, TypeFace.Bold, l.Char, 1f, Layers, depth, Ink, Shadow);
        }

        /// <summary>
        /// Puts each letter where its role wants it: the sung word in front of you, earlier words of the line on the
        /// rings around you, dissolving words drifting off. Letters ease there, so every change is a motion.
        /// </summary>
        void PlaceLetters(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            var eye = view.transform.position;
            var front = head + forward * distance + Vector3.up * height;
            bool fistPull = false;
            Vector3 pullTo = Vector3.zero;
            for (int side = 0; side < 2; side++) pulling[side] = pushing[side] = false;
            if (rig != null)
            {
                for (int side = 0; side < 2; side++)
                {
                    var hand = rig.Get(side == 0);
                    // While the menu is open the hands are choosing there: no fist pulls, no finger pushes (PushFrom).
                    if (!hand.IsTracked || hand.Fist < 0.6f || MenuOpen) continue;
                    var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                    if (Vector3.Distance(palm, front) < pullRadius) { fistPull = true; pullTo = palm; pulling[side] = true; }
                }
                if (fistPull) Acted("fist pull");
            }

            foreach (var l in letters)
            {
                if (l.Role == Role.Hidden) continue;
                l.Age += dt;
                Vector3 target;
                float size;
                float alpha = 1f;
                if (l.Role == Role.Main)
                {
                    float t = Mathf.Clamp01((l.Age - l.Delay) / 0.4f);
                    target = front + right * l.Offset * letterHeight;
                    if (fistPull) target = Vector3.Lerp(target, pullTo + right * l.Offset * letterHeight * 0.5f, 0.6f);
                    size = letterHeight * (1f + music.Kick * 0.08f);
                    alpha = t;
                    l.Spin = Mathf.Lerp(l.Spin, 0f, 1f - Mathf.Exp(-9f * dt * (t > 0f ? 1f : 0f)));
                }
                else
                {
                    // Words in orbit sit on the rings, each word on its own ring and angle, turning slowly.
                    int ring = l.Word % rings.Length;
                    // Seen from inside the ring, a growing angle runs right to left, so the letters run the other way to read.
                    float angle = l.Word * 1.3f + ringSpin[ring] - l.Offset * letterHeight / ringRadii[ring] * (orbitLetterHeight / letterHeight);
                    target = RingPoint(ring, angle);
                    size = orbitLetterHeight;
                    l.Spin = Mathf.Lerp(l.Spin, 0f, 1f - Mathf.Exp(-4f * dt));
                    if (l.Role == Role.Gone)
                    {
                        alpha = 1f - Mathf.Clamp01(l.Age / 1.2f);
                        target += Vector3.up * l.Age * 0.12f;
                        if (alpha <= 0f) { Hide(l); continue; }
                    }
                }

                l.Push = Vector3.Lerp(l.Push, PushFrom(rig, l.Position, size), 1f - Mathf.Exp(-12f * dt));
                l.Position = Vector3.Lerp(l.Position, target, 1f - Mathf.Exp(-(l.Role == Role.Main ? 7f : 3f) * dt));
                l.Size = Mathf.Lerp(l.Size, size, 1f - Mathf.Exp(-6f * dt));
                l.Alpha = alpha;

                var p = l.Position + l.Push;
                var look = p - eye;
                var rotation = look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look, Vector3.up) : Quaternion.identity;
                l.Renderer.transform.SetPositionAndRotation(p, rotation * Quaternion.Euler(0f, l.Spin, 0f));
                l.Renderer.transform.localScale = Vector3.one * l.Size;
                bool touched = l.Push.sqrMagnitude > 0.0001f;
                block.Clear();
                block.SetColor(ColorId, touched ? Color.Lerp(Color.white, Touch, 0.6f) : Color.white);
                block.SetFloat(AlphaId, l.Alpha * FadeIn);
                l.Renderer.SetPropertyBlock(block);
            }

            // Feedback as each hand starts to pull the word with a fist, push letters with a fingertip or spin a ring.
            if (rig == null) return;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                FeedbackOnStart(side, pulling[side], GestureKind.Fist, side == 0, HandFistPoint(hand), 0.8f);
                FeedbackOnStart(2 + side, pushing[side], GestureKind.Touch, side == 0, touchAt[side], 0.7f, true,
                    GestureFeedback.Of((HandRig.Finger)touchFinger[side]), 0.6f);
                FeedbackOnStart(4 + side, spinning[side], GestureKind.Touch, side == 0, spinAt[side], 0.7f, true,
                    GestureFeedback.Of((HandRig.Finger)spinFinger[side]), 0.6f);
            }
        }

        // What each hand does to the letters this frame, for the feedback.
        readonly bool[] pulling = new bool[2], pushing = new bool[2], spinning = new bool[2];
        readonly Vector3[] touchAt = new Vector3[2], spinAt = new Vector3[2];
        readonly int[] touchFinger = new int[2], spinFinger = new int[2];

        /// <summary>Fingertips and palms close to a letter push it away; it springs back when they leave.</summary>
        Vector3 PushFrom(HandRig rig, Vector3 at, float size)
        {
            var push = Vector3.zero;
            if (rig == null || MenuOpen) return push;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                if (!hand.IsTracked || hand.Fist > 0.6f) continue;
                for (int k = 0; k < 6; k++)
                {
                    var point = k < 5 ? (hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k])
                        : hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                    var centre = at + Vector3.up * size * 0.35f;
                    var away = centre - point;
                    float d = away.magnitude;
                    float reach = pushRadius * (size / letterHeight * 0.6f + 0.4f);
                    if (d >= reach || d < 1e-4f) continue;
                    push += away / d * (reach - d) * 1.1f;
                    if (k < 5)
                    {
                        pushing[side] = true;
                        touchAt[side] = point;
                        touchFinger[side] = k;
                    }
                    Acted("push letter");
                }
            }
            return push;
        }

        Vector3 RingPoint(int ring, float angle)
        {
            var tilt = Quaternion.AngleAxis(ringTilts[ring % ringTilts.Length], right) * Quaternion.AngleAxis(ring * 40f, Vector3.up);
            var local = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadii[ring];
            return head + Vector3.up * (height - 0.05f) + tilt * local;
        }

        /// <summary>A fingertip moving along a ring spins it that way; the spin eases back to a slow turn.</summary>
        void SpinRings(HandRig rig, float dt)
        {
            for (int r = 0; r < rings.Length; r++)
            {
                ringTouch[r] = Mathf.MoveTowards(ringTouch[r], 0f, dt * 2f);
                ringSpinVelocity[r] *= Mathf.Exp(-dt * 0.8f);
            }
            spinning[0] = spinning[1] = false;
            if (rig != null && dt > 0f)
            {
                for (int side = 0; side < 2; side++)
                {
                    var hand = rig.Get(side == 0);
                    for (int k = 0; k < 5; k++)
                    {
                        var tip = hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k];
                        var move = (tip - lastTips[side, k]) / dt;
                        lastTips[side, k] = tip;
                        // A fingertip at the open menu spins no ring.
                        if (!hand.IsTracked || move.magnitude > 6f || MenuOpen) continue;
                        for (int r = 0; r < rings.Length; r++)
                        {
                            // The ring's nearest point to the fingertip, and the ring's direction there.
                            var tilt = Quaternion.AngleAxis(ringTilts[r % ringTilts.Length], right) * Quaternion.AngleAxis(r * 40f, Vector3.up);
                            var local = Quaternion.Inverse(tilt) * (tip - (head + Vector3.up * (height - 0.05f)));
                            var flat = new Vector2(local.x, local.z);
                            if (Mathf.Abs(flat.magnitude - ringRadii[r]) > 0.05f || Mathf.Abs(local.y) > 0.05f) continue;
                            var tangent = tilt * new Vector3(-flat.y, 0f, flat.x).normalized;
                            float push = Vector3.Dot(move, tangent) / ringRadii[r];
                            ringSpinVelocity[r] = Mathf.Lerp(ringSpinVelocity[r], Mathf.Clamp(push, -6f, 6f), 0.5f);
                            ringTouch[r] = 1f;
                            spinning[side] = true;
                            spinAt[side] = tip;
                            spinFinger[side] = k;
                            Acted("spin ring");
                        }
                    }
                }
            }
            for (int r = 0; r < rings.Length; r++) ringSpin[r] += (0.08f * (r % 2 == 0 ? 1f : -1f) + ringSpinVelocity[r]) * dt;
        }

        void DrawRings(MusicFrame music, Camera view)
        {
            int n = 0;
            float hat = music.Band(5);
            for (int r = 0; r < rings.Length; r++)
            {
                for (int i = 0; i < ringPath.Length; i++)
                    ringPath[i] = RingPoint(r, i / (float)(ringPath.Length - 1) * Mathf.PI * 2f);
                var c = Color.Lerp(new Color(0.86f, 0.87f, 0.92f), Touch, ringTouch[r]);
                c.a = (0.28f + music.Kick * 0.15f + ringTouch[r] * 0.4f) * FadeIn;
                rings[r].Set(ringPath, ringPath.Length, 0.0025f * (1f + music.Kick * 0.5f), c, view.transform.position, false);

                for (int b = 0; b < BeadsPerRing; b++)
                {
                    var p = RingPoint(r, b / (float)BeadsPerRing * Mathf.PI * 2f + ringSpin[r] * 3f);
                    var bc = Color.Lerp(new Color(0.93f, 0.94f, 0.97f), Touch, ringTouch[r]);
                    bc.a = (b % 3 == 0 ? 0.9f : 0.5f) * FadeIn;
                    beadOut[n++] = new ParticleSystem.Particle
                    {
                        position = p,
                        startSize = (b % 3 == 0 ? 0.012f : 0.007f) * (1f + hat * 0.6f + ringTouch[r] * 0.5f),
                        startColor = bc,
                        startLifetime = 10f,
                        remainingLifetime = 10f
                    };
                }
            }
            beads.SetParticles(beadOut, n);
        }
    }
}
