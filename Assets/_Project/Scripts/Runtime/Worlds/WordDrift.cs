using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Word drift world: the lyrics float through your room. Each sung word appears in the distance as soft 3D
    /// type and drifts towards you along one of several lanes, growing as it comes, then fades out as it slides past
    /// your head; fine dust drifts with it. Words ease apart when they would cover each other, so each stays readable.
    /// Touch a word and it stops and waits in front of you, glowing, for a few seconds before it carries on. Several
    /// can wait at once, each on its own row.
    /// </summary>
    public class WordDrift : World
    {
        public override string DisplayName => "Word drift";

        public override Color Color => new Color(0.8f, 0.88f, 1f);

        public override int MenuOrder => 150;

        public override int MenuIcon => 19;

        public override bool IsLyric => true;

        public override string Hint => "Touch a word to hold it";

        /// <summary>The words drift through the room towards you, and wait in front of you when touched.</summary>
        public override Space Spaces => Space.AroundYou | Space.InFront;

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.1f, 0.12f, 0.2f, 0.5f),
            Edge = new Color(0.75f, 0.85f, 1f),
            Glow = new Color(1f, 0.83f, 0.42f),
            Pattern = HandTheme.Effect.Specks,
            PatternColor = new Color(0.8f, 0.9f, 1f)
        };

        [Header("Drift")]
        [Tooltip("How far away words appear (m), and how long they take to reach you (s).")]
        [SerializeField] float startDistance = 3.2f;
        [SerializeField] float travelTime = 5.5f;
        [Tooltip("Letter height far away and close by (m).")]
        [SerializeField] Vector2 letterHeight = new Vector2(0.09f, 0.14f);
        [SerializeField] float holdTime = 6f;

        static readonly Color[] LineColors =
        {
            new Color(0.85f, 0.9f, 1f), new Color(1f, 0.85f, 0.7f), new Color(0.8f, 1f, 0.9f), new Color(1f, 0.8f, 0.92f)
        };
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        const int Pool = 24;
        const int MaxHeld = 4;
        const float HeldHeight = 0.075f;
        const float HeldDistance = 0.6f;
        // Where each lane starts in the distance (m to the side, m up), spread around the middle of your view.
        static readonly Vector2[] Lanes =
        {
            new Vector2(-1.25f, 0.02f), new Vector2(0.95f, 0.34f), new Vector2(-0.55f, -0.36f),
            new Vector2(1.25f, -0.06f), new Vector2(-0.9f, 0.36f), new Vector2(0.55f, -0.34f)
        };
        // The rows held words wait on, as heights around the middle.
        static readonly float[] HeldRows = { 0f, -0.1f, 0.1f, -0.2f };

        sealed class Drifter
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public string Text;
            public Vector3 Position;
            public Vector3 Velocity;
            /// <summary>Width at a letter height of 1.</summary>
            public float Width;
            public float Size;
            public float Age = -1f;
            public float Held = -1f;
            public int Row;
            public float Glow;
            public Color Color;
        }

        readonly List<Drifter> drifters = new List<Drifter>();
        Material material;
        MaterialPropertyBlock block;
        ParticleSystem dust;
        Vector3 head, forward, right;
        int lineNumber, lane;
        float dustDebt, touchPause;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            material = new Material(letteringMaterial) { name = "Drift Lettering", mainTexture = TypeFace.Bold.Atlas };
            for (int i = 0; i < Pool; i++)
            {
                var d = new Drifter();
                d.Renderer = Glyphs.MakeLetter($"Word {i}", transform, material, out d.Mesh);
                d.Renderer.gameObject.SetActive(false);
                drifters.Add(d);
            }
            dust = Effects.MakeSparkSystem("Dust", transform, glowMaterial);
            var main = dust.main;
            main.gravityModifier = 0f;
        }

        protected override void OnEnter()
        {
            foreach (var d in drifters) Retire(d);
        }

        protected override void OnExit()
        {
            foreach (var d in drifters) Retire(d);
            dust.Clear();
        }

        static void Retire(Drifter d)
        {
            d.Age = -1f;
            d.Held = -1f;
            d.Renderer.gameObject.SetActive(false);
        }

        /// <summary>Words come towards where you stand, from the way you face.</summary>
        protected override void OnPlace(Camera view)
        {
            head = view.transform.position;
            forward = Effects.FlatForward(view);
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }

        protected override void OnWord(string word, bool newLine)
        {
            if (newLine) lineNumber++;
            Launch(word);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Drift(rig, music, view, dt);
            Dust(music, dt);
        }

        /// <summary>A new word appears in the distance, in the next lane, aimed to slide past your head.</summary>
        void Launch(string text)
        {
            Drifter d = null;
            foreach (var x in drifters) if (x.Age < 0f) { d = x; break; }
            if (d == null)
                foreach (var x in drifters) if (x.Held < 0f && (d == null || x.Age > d.Age)) d = x;
            if (d == null) return;
            d.Text = text;
            d.Width = Glyphs.BuildWord(d.Mesh, TypeFace.Bold, text, 1f, 1, 0f, Color.white, Color.white);
            var start = Lanes[lane];
            lane = (lane + 1) % Lanes.Length;
            d.Position = head + forward * startDistance + right * start.x + Vector3.up * start.y;
            // Lanes never cross: each stays on its own side and height of your view as it comes.
            var pass = head + right * (start.x * 0.75f) + Vector3.up * (start.y * 0.8f);
            d.Velocity = (pass - d.Position) / travelTime;
            d.Age = 0f;
            d.Held = -1f;
            d.Glow = 0f;
            d.Color = LineColors[lineNumber % LineColors.Length];
            d.Renderer.gameObject.SetActive(true);
        }

        void Drift(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            var eye = view.transform.position;
            var front = eye + Effects.FlatForward(view) * HeldDistance + Vector3.down * 0.04f;
            touchPause -= dt;
            foreach (var d in drifters)
            {
                if (d.Age < 0f) continue;
                d.Age += dt;
                float near = Mathf.Clamp01(1f - Vector3.Dot(d.Position - head, forward) / startDistance);
                d.Size = Mathf.Lerp(letterHeight.x, letterHeight.y, near * near);

                if (d.Held >= 0f)
                {
                    // Held: float to its row in front of you and wait there, bobbing gently.
                    d.Held += dt;
                    var wait = front + Vector3.up * (HeldRows[d.Row] + Mathf.Sin(d.Held * 1.6f + d.Row) * 0.006f);
                    d.Position = Vector3.Lerp(d.Position, wait, 1f - Mathf.Exp(-5f * dt));
                    d.Size = HeldHeight;
                    d.Glow = Mathf.MoveTowards(d.Glow, 1f, dt * 3f);
                    if (d.Held > holdTime) Release(d);
                }
                else
                {
                    d.Position += d.Velocity * dt;
                    d.Glow = Mathf.MoveTowards(d.Glow, 0f, dt);
                    if (rig != null && touchPause <= 0f && TouchedBy(rig, d))
                    {
                        Hold(d);
                        touchPause = 0.25f;
                    }
                }
            }

            Separate(eye, dt);

            foreach (var d in drifters)
            {
                if (d.Age < 0f) continue;
                float ahead = Vector3.Dot(d.Position - eye, Effects.FlatForward(view));
                if (d.Held < 0f && ahead < 0.2f) { Retire(d); continue; }
                float alpha = Mathf.Clamp01(d.Age / 0.8f) * (d.Held >= 0f ? 1f : Mathf.Clamp01((ahead - 0.2f) / 0.4f));

                var toWord = d.Position - eye;
                d.Renderer.transform.SetPositionAndRotation(d.Position, toWord.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(toWord, Vector3.up) : Quaternion.identity);
                d.Renderer.transform.localScale = Vector3.one * d.Size * (1f + music.Kick * 0.05f);
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(d.Color, new Color(1f, 0.86f, 0.5f), d.Glow));
                block.SetFloat(AlphaId, alpha * FadeIn);
                d.Renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>Holds the first word on its way, as a touch would, for the editor screenshots.</summary>
        public void PreviewHold()
        {
            foreach (var d in drifters)
            {
                if (d.Age < 0f || d.Held >= 0f) continue;
                Hold(d);
                return;
            }
        }

        /// <summary>Puts a word on a free row; when every row is taken, the one that has waited longest moves on.</summary>
        void Hold(Drifter d)
        {
            Drifter oldest = null;
            int used = 0, held = 0;
            foreach (var o in drifters)
            {
                if (o.Age < 0f || o.Held < 0f) continue;
                held++;
                used |= 1 << o.Row;
                if (oldest == null || o.Held > oldest.Held) oldest = o;
            }
            if (held >= MaxHeld)
            {
                used &= ~(1 << oldest.Row);
                Release(oldest);
            }
            for (int row = 0; row < HeldRows.Length; row++)
            {
                if ((used & (1 << row)) != 0) continue;
                d.Row = row;
                break;
            }
            d.Held = 0f;
        }

        void Release(Drifter d)
        {
            d.Held = -1f;
            float side = Vector3.Dot(d.Position - head, right) < 0f ? -1f : 1f;
            d.Velocity = (head + right * side * 0.6f - d.Position).normalized * (startDistance / travelTime);
        }

        /// <summary>
        /// Eases apart words that cover each other as seen from where you stand, comparing them on a plane one metre
        /// ahead of you, where a word's size is how large it looks. Held words stay where they are.
        /// </summary>
        void Separate(Vector3 eye, float dt)
        {
            for (int i = 0; i < drifters.Count; i++)
            {
                var a = drifters[i];
                if (a.Age < 0f) continue;
                var fromEye = a.Position - eye;
                float depthA = Mathf.Max(Vector3.Dot(fromEye, forward), 0.3f);
                float ax = Vector3.Dot(fromEye, right) / depthA, ay = fromEye.y / depthA;
                for (int j = i + 1; j < drifters.Count; j++)
                {
                    var b = drifters[j];
                    if (b.Age < 0f || (a.Held >= 0f && b.Held >= 0f)) continue;
                    fromEye = b.Position - eye;
                    float depthB = Mathf.Max(Vector3.Dot(fromEye, forward), 0.3f);
                    float dx = Vector3.Dot(fromEye, right) / depthB - ax, dy = fromEye.y / depthB - ay;
                    float roomX = (a.Width * a.Size / depthA + b.Width * b.Size / depthB) * 0.5f + 0.02f - Mathf.Abs(dx);
                    float roomY = (a.Size / depthA + b.Size / depthB) * 0.65f + 0.01f - Mathf.Abs(dy);
                    if (roomX <= 0f || roomY <= 0f) continue;
                    // The shorter way out.
                    var away = roomY < roomX ? Vector3.up * (dy < 0f ? -1f : 1f) : right * (dx < 0f ? -1f : 1f);
                    float push = Mathf.Min(roomX, roomY) * 2.5f * dt;
                    if (a.Held < 0f) a.Position -= away * (push * depthA);
                    if (b.Held < 0f) b.Position += away * (push * depthB);
                }
            }
        }

        /// <summary>A fingertip on the word's face: within its width and height, and close to its plane.</summary>
        bool TouchedBy(HandRig rig, Drifter d)
        {
            var centre = d.Position + Vector3.up * d.Size * 0.35f;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                // While the menu is open the fingers are choosing there.
                if (!hand.IsTracked || MenuOpen) continue;
                for (int k = 0; k < 5; k++)
                {
                    var tip = hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k];
                    var offset = tip - centre;
                    if (Mathf.Abs(Vector3.Dot(offset, right)) < d.Width * d.Size * 0.5f + 0.02f &&
                        Mathf.Abs(offset.y) < d.Size * 0.6f + 0.02f && Mathf.Abs(Vector3.Dot(offset, forward)) < 0.1f) return true;
                }
            }
            return false;
        }

        /// <summary>Fine dust drifting towards you through the room, a little thicker with the music.</summary>
        void Dust(MusicFrame music, float dt)
        {
            dustDebt += (6f + music.Band(4) * 14f) * dt;
            int n = (int)dustDebt;
            dustDebt -= n;
            for (int i = 0; i < n; i++)
            {
                var from = head + forward * Random.Range(1.5f, 3.5f) + right * Random.Range(-1.4f, 1.4f) + Vector3.up * Random.Range(-0.6f, 0.6f);
                dust.Emit(new ParticleSystem.EmitParams
                {
                    position = from,
                    velocity = -forward * Random.Range(0.3f, 0.6f),
                    startColor = new Color(0.85f, 0.9f, 1f, 0.6f),
                    startSize = Random.Range(0.003f, 0.006f),
                    startLifetime = Random.Range(3f, 5f)
                }, 1);
            }
        }
    }
}
