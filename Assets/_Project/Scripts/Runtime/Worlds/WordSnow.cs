using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Word snow world: the lyrics fall like snow. Each sung word drifts down from above, swaying and slowly
    /// tumbling among fine flakes, and settles on the floor around you, where it lies for a while before melting
    /// away. Hold out your hand palm up and words land on it and ride along as you move; make a fist and they drop.
    /// Once the room is scanned, words also settle on your table, sofa, bed and shelves; until then, on the floor.
    /// </summary>
    public class WordSnow : World
    {
        public override int MenuIcon => 21;

        public override bool IsLyric => true;

        public override string Hint => "Hold out your hand palm up; make a fist to drop";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.12f, 0.14f, 0.2f, 0.5f),
            Edge = new Color(0.85f, 0.92f, 1f),
            Glow = new Color(0.7f, 0.85f, 1f),
            Pattern = HandTheme.Effect.Specks,
            PatternColor = new Color(0.85f, 0.95f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material letteringMaterial;
        [SerializeField] Material glowMaterial;

        [Header("Snow")]
        [SerializeField] float letterHeight = 0.075f;
        [SerializeField] float fallSpeed = 0.22f;
        [Tooltip("Seconds a word lies on the floor before it melts away.")]
        [SerializeField] float lieTime = 25f;
        [SerializeField] int maxWords = 48;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        const int FontSize = 96;

        enum State { Free, Falling, Resting, Held }

        sealed class Flake
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public State State;
            public Vector3 Position;
            public Vector3 Velocity;
            public Quaternion Rotation;
            public Vector3 Spin;
            public Vector3 Offset;
            public int Hand;
            public float Width;
            public float Age;
            public float Phase;
        }

        readonly List<Flake> flakes = new List<Flake>();
        readonly LyricCursor cursor = new LyricCursor();
        Font font;
        Material material;
        MaterialPropertyBlock block;
        ParticleSystem snow;
        Vector3 head, forward;
        float floorY, ceiling, snowDebt, fade;
        bool anchored, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            material = new Material(letteringMaterial) { name = "Snow Lettering", mainTexture = font.material.mainTexture };
            Font.textureRebuilt += OnFontRebuilt;
            for (int i = 0; i < maxWords; i++)
            {
                var f = new Flake();
                f.Renderer = Glyphs.MakeLetter($"Word {i}", transform, material, out f.Mesh);
                f.Renderer.gameObject.SetActive(false);
                flakes.Add(f);
            }
            snow = Effects.MakeSparkSystem("Snow", transform, glowMaterial);
            var main = snow.main;
            main.gravityModifier = 0.006f;
            main.maxParticles = 600;
        }

        void OnFontRebuilt(Font rebuilt)
        {
            if (rebuilt == font && material != null) material.mainTexture = font.material.mainTexture;
        }

        void OnDestroy() => Font.textureRebuilt -= OnFontRebuilt;

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
            fade = 0f;
            cursor.Reset();
            if (built) foreach (var f in flakes) Melt(f);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (!built) return;
            foreach (var f in flakes) Melt(f);
            snow.Clear();
        }

        static void Melt(Flake f)
        {
            f.State = State.Free;
            f.Renderer.gameObject.SetActive(false);
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
                floorY = Effects.FloorY(view);
                ceiling = Mathf.Min(Mathf.Max(head.y + 0.7f, floorY + 2.3f), Room.Ceiling(float.MaxValue) - 0.05f);
                MarkAnchored(view);
                anchored = true;
            }
            fade = Mathf.MoveTowards(fade, 1f, dt * 1.5f);
            if (cursor.Advance(music, out _)) Drop(cursor.Word);
            Fall(rig, music, view, dt);
            Flurry(music, dt);
        }

        /// <summary>A new word starts falling from above, somewhere in front of you and to the sides.</summary>
        void Drop(string text)
        {
            Flake f = null;
            foreach (var x in flakes) if (x.State == State.Free) { f = x; break; }
            if (f == null)
                foreach (var x in flakes) if (x.State == State.Resting && (f == null || x.Age > f.Age)) f = x;
            if (f == null) return;
            f.Width = Glyphs.BuildWord(f.Mesh, font, text, FontSize, FontStyle.Bold, 1f, 4, 0.12f, Color.white, new Color(0.6f, 0.7f, 0.85f));
            float angle = Random.Range(-100f, 100f);
            float reach = Mathf.Lerp(0.35f, 1.6f, Mathf.Sqrt(Random.value));
            var dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            f.Position = new Vector3(head.x, ceiling, head.z) + dir * reach;
            // With a room scan, some words fall over the furniture near you, to settle on it.
            if (Room.Tops.Count > 0 && Random.value < 0.45f)
            {
                var top = Room.Tops[Random.Range(0, Room.Tops.Count)];
                if (Vector3.ProjectOnPlane(top.Center - head, Vector3.up).magnitude < 2.6f)
                {
                    var spot = top.Center + top.Right * (Random.Range(-0.4f, 0.4f) * top.Size.x) + top.Up * (Random.Range(-0.4f, 0.4f) * top.Size.y);
                    f.Position = new Vector3(spot.x, ceiling, spot.z);
                }
            }
            f.Velocity = Vector3.down * fallSpeed;
            f.Rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(f.Position - head, Vector3.up).normalized + Vector3.down * 0.2f, Vector3.up);
            f.Spin = new Vector3(Random.Range(-20f, 20f), Random.Range(-35f, 35f), Random.Range(-15f, 15f));
            f.Phase = Random.value * 6.28f;
            f.Age = 0f;
            f.State = State.Falling;
            f.Renderer.gameObject.SetActive(true);
        }

        void Fall(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            foreach (var f in flakes)
            {
                if (f.State == State.Free) continue;
                f.Age += dt;
                float alpha = 1f;
                switch (f.State)
                {
                    case State.Falling:
                        // Sway and tumble on the way down.
                        var sway = new Vector3(Mathf.Sin(f.Age * 1.3f + f.Phase), 0f, Mathf.Cos(f.Age * 1.1f + f.Phase * 1.7f)) * 0.06f;
                        f.Position += (f.Velocity + sway) * dt;
                        f.Rotation = Quaternion.Euler(f.Spin * dt) * f.Rotation;
                        if (rig != null) TryCatch(rig, f);
                        float ground = Room.GroundBelow(f.Position, floorY);
                        if (f.State == State.Falling && f.Position.y <= ground + 0.01f)
                        {
                            f.State = State.Resting;
                            f.Age = 0f;
                            f.Position.y = ground + 0.006f;
                            // Lie down where it landed, reading towards you.
                            var toward = Vector3.ProjectOnPlane(f.Position - head, Vector3.up);
                            f.Rotation = Quaternion.LookRotation(Vector3.down, toward.sqrMagnitude > 1e-6f ? toward.normalized : forward)
                                * Quaternion.Euler(0f, 0f, Random.Range(-15f, 15f));
                        }
                        break;
                    case State.Resting:
                        alpha = 1f - Mathf.Clamp01((f.Age - lieTime) / 3f);
                        if (alpha <= 0f) { Melt(f); continue; }
                        break;
                    case State.Held:
                        var hand = rig?.Get(f.Hand == 0);
                        if (hand == null || !hand.IsTracked || hand.Fist > 0.6f || hand.PalmNormal.y < 0.2f)
                        {
                            // Dropped: it falls from the hand.
                            f.State = State.Falling;
                            f.Velocity = Vector3.down * fallSpeed * 2f + (hand != null ? hand.Velocity * 0.5f : Vector3.zero);
                            break;
                        }
                        var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                        var basis = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, hand.PalmNormal).normalized, hand.PalmNormal);
                        f.Position = Vector3.Lerp(f.Position, palm + basis * f.Offset, 1f - Mathf.Exp(-14f * dt));
                        f.Rotation = Quaternion.Slerp(f.Rotation, basis * Quaternion.Euler(90f, 0f, 0f), 1f - Mathf.Exp(-8f * dt));
                        break;
                }
                f.Renderer.transform.SetPositionAndRotation(f.Position, f.Rotation);
                f.Renderer.transform.localScale = Vector3.one * letterHeight;
                block.Clear();
                block.SetColor(ColorId, f.State == State.Held ? new Color(1f, 0.95f, 0.85f) : new Color(0.92f, 0.96f, 1f));
                block.SetFloat(AlphaId, alpha * fade * Mathf.Clamp01(f.Age / 0.6f + (f.State == State.Falling ? 0f : 1f)));
                f.Renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>A palm held up under a falling word catches it.</summary>
        void TryCatch(HandRig rig, Flake f)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                if (!hand.IsTracked || hand.Fist > 0.5f || hand.PalmNormal.y < 0.5f) continue;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                var flat = Vector3.ProjectOnPlane(f.Position - palm, Vector3.up);
                float above = f.Position.y - palm.y;
                if (flat.magnitude > 0.1f || above > 0.06f || above < -0.04f) continue;
                f.State = State.Held;
                f.Hand = side;
                int stacked = 0;
                foreach (var o in flakes) if (o != f && o.State == State.Held && o.Hand == side) stacked++;
                f.Offset = new Vector3(Random.Range(-0.02f, 0.02f), 0.012f + stacked * 0.012f, Random.Range(-0.02f, 0.02f));
                return;
            }
        }

        /// <summary>Fine snow drifting down all around, a little heavier with the music.</summary>
        void Flurry(MusicFrame music, float dt)
        {
            snowDebt += (18f + music.Band(5) * 30f) * dt;
            int n = (int)snowDebt;
            snowDebt -= n;
            for (int i = 0; i < n; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float reach = Mathf.Sqrt(Random.Range(0.1f, 6f));
                snow.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(head.x + Mathf.Cos(angle) * reach, ceiling + 0.1f, head.z + Mathf.Sin(angle) * reach),
                    velocity = Vector3.down * Random.Range(0.12f, 0.25f) + Random.insideUnitSphere * 0.03f,
                    startColor = new Color(0.9f, 0.95f, 1f, 0.75f),
                    startSize = Random.Range(0.004f, 0.009f),
                    startLifetime = (ceiling - floorY) / 0.18f
                }, 1);
            }
        }
    }
}
