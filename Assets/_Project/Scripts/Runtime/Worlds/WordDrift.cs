using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Word drift world: the lyrics float through your room. Each sung word appears in the distance as soft 3D
    /// type and drifts towards you, growing as it comes, and slides past beside your head; fine dust drifts with it.
    /// Touch a word and it stops and waits in front of you, glowing, for a few seconds before it carries on.
    /// </summary>
    public class WordDrift : World
    {
        public override int MenuIcon => 19;

        public override bool IsLyric => true;

        public override string Hint => "Touch a word to hold it";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.1f, 0.12f, 0.2f, 0.5f),
            Edge = new Color(0.75f, 0.85f, 1f),
            Glow = new Color(1f, 0.83f, 0.42f),
            Pattern = HandTheme.Effect.Specks,
            PatternColor = new Color(0.8f, 0.9f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material letteringMaterial;
        [SerializeField] Material glowMaterial;

        [Header("Drift")]
        [Tooltip("How far away words appear (m), and how long they take to reach you (s).")]
        [SerializeField] float startDistance = 3.2f;
        [SerializeField] float travelTime = 5.5f;
        [Tooltip("Letter height far away and close by (m).")]
        [SerializeField] Vector2 letterHeight = new Vector2(0.09f, 0.17f);
        [SerializeField] float holdTime = 6f;

        static readonly Color[] LineColors =
        {
            new Color(0.85f, 0.9f, 1f), new Color(1f, 0.85f, 0.7f), new Color(0.8f, 1f, 0.9f), new Color(1f, 0.8f, 0.92f)
        };
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        const int FontSize = 96;
        const int Pool = 24;

        sealed class Drifter
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public string Text;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Width;
            public float Age = -1f;
            public float Held = -1f;
            public float Glow;
            public Color Color;
        }

        readonly List<Drifter> drifters = new List<Drifter>();
        readonly LyricCursor cursor = new LyricCursor();
        Font font;
        Material material;
        MaterialPropertyBlock block;
        ParticleSystem dust;
        Vector3 head, forward, right;
        int lineNumber;
        float dustDebt, fade;
        bool anchored, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            material = new Material(letteringMaterial) { name = "Drift Lettering", mainTexture = font.material.mainTexture };
            Font.textureRebuilt += OnFontRebuilt;
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

        void OnFontRebuilt(Font rebuilt)
        {
            if (rebuilt != font || material == null) return;
            material.mainTexture = font.material.mainTexture;
            foreach (var d in drifters) if (d.Age >= 0f) Rebuild(d);
        }

        void OnDestroy() => Font.textureRebuilt -= OnFontRebuilt;

        void Rebuild(Drifter d) =>
            d.Width = Glyphs.BuildWord(d.Mesh, font, d.Text, FontSize, FontStyle.Bold, 1f, 5, 0.12f, Color.white, new Color(0.35f, 0.38f, 0.5f));

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
            fade = 0f;
            cursor.Reset();
            if (built) foreach (var d in drifters) Retire(d);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (!built) return;
            foreach (var d in drifters) Retire(d);
            dust.Clear();
        }

        static void Retire(Drifter d)
        {
            d.Age = -1f;
            d.Held = -1f;
            d.Renderer.gameObject.SetActive(false);
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            if (anchored && ShouldReanchor(view, dt)) anchored = false;
            if (!anchored)
            {
                head = view.transform.position;
                forward = Effects.FlatForward(view);
                right = Vector3.Cross(Vector3.up, forward).normalized;
                MarkAnchored(view);
                anchored = true;
            }
            fade = Mathf.MoveTowards(fade, 1f, dt * 1.5f);
            if (cursor.Advance(music, out bool newLine))
            {
                if (newLine) lineNumber++;
                Launch(cursor.Word);
            }
            Drift(rig, music, view, dt);
            Dust(music, dt);
        }

        /// <summary>A new word appears in the distance, aimed to slide past beside your head.</summary>
        void Launch(string text)
        {
            Drifter d = null;
            foreach (var x in drifters) if (x.Age < 0f) { d = x; break; }
            if (d == null)
                foreach (var x in drifters) if (x.Held < 0f && (d == null || x.Age > d.Age)) d = x;
            if (d == null) return;
            d.Text = text;
            Rebuild(d);
            float side = Random.value < 0.5f ? -1f : 1f;
            d.Position = head + forward * startDistance + right * Random.Range(-0.9f, 0.9f) + Vector3.up * Random.Range(-0.35f, 0.3f);
            var pass = head + right * side * Random.Range(0.28f, 0.45f) + Vector3.up * Random.Range(-0.15f, 0.12f);
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
            var front = eye + Effects.FlatForward(view) * 0.45f + Vector3.down * 0.08f;
            foreach (var d in drifters)
            {
                if (d.Age < 0f) continue;
                d.Age += dt;
                float near = Mathf.Clamp01(1f - Vector3.Dot(d.Position - head, forward) / startDistance);
                float size = Mathf.Lerp(letterHeight.x, letterHeight.y, near * near);

                if (d.Held >= 0f)
                {
                    // Held: float to the front and wait there, bobbing gently.
                    d.Held += dt;
                    var wait = front + Vector3.up * Mathf.Sin(d.Held * 1.6f) * 0.01f;
                    d.Position = Vector3.Lerp(d.Position, wait, 1f - Mathf.Exp(-5f * dt));
                    size = letterHeight.y * 0.85f;
                    d.Glow = Mathf.MoveTowards(d.Glow, 1f, dt * 3f);
                    if (d.Held > holdTime)
                    {
                        d.Held = -1f;
                        d.Velocity = (head + right * 0.4f - d.Position).normalized * (startDistance / travelTime);
                    }
                }
                else
                {
                    d.Position += d.Velocity * dt;
                    d.Glow = Mathf.MoveTowards(d.Glow, 0f, dt);
                    if (rig != null && TouchedBy(rig, d, size)) d.Held = 0f;
                }

                float behind = Vector3.Dot(d.Position - head, forward);
                float alpha = Mathf.Clamp01(d.Age / 0.8f) * Mathf.Clamp01((behind + 0.25f) / 0.35f);
                if (behind < -0.3f) { Retire(d); continue; }

                var look = d.Position - eye;
                d.Renderer.transform.SetPositionAndRotation(d.Position, look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look, Vector3.up) : Quaternion.identity);
                d.Renderer.transform.localScale = Vector3.one * size * (1f + music.Kick * 0.05f);
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(d.Color, new Color(1f, 0.86f, 0.5f), d.Glow));
                block.SetFloat(AlphaId, alpha * fade);
                d.Renderer.SetPropertyBlock(block);
            }
        }

        bool TouchedBy(HandRig rig, Drifter d, float size)
        {
            var centre = d.Position + Vector3.up * size * 0.35f;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                if (!hand.IsTracked) continue;
                for (int k = 0; k < 5; k++)
                {
                    var tip = hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k];
                    if (Vector3.Distance(tip, centre) < d.Width * size * 0.5f + 0.05f) return true;
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
