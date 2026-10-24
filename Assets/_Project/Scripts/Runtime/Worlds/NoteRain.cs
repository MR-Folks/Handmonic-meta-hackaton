using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Note rain world: the music falls as drops of light in front of you, one for each note the analyser hears
    /// and a big golden one on each beat. Catch a drop in your palm and it splashes with a chime while your palm
    /// slowly fills with light. Drops you miss splash on the floor.
    /// </summary>
    public class NoteRain : World
    {
        [Header("Materials")]
        [SerializeField] Material glowMaterial;
        [SerializeField] Material lineMaterial;
        [SerializeField] Material ringMaterial;

        [Header("Rain")]
        [SerializeField] float areaWidth = 0.8f;
        [SerializeField] float areaDepth = 0.45f;
        [SerializeField] float distance = 0.45f;
        [Tooltip("Where drops appear, relative to the eyes.")]
        [SerializeField] float spawnHeight = 0.45f;
        [SerializeField] float fallSpeed = 0.5f;
        [SerializeField] float catchRadius = 0.075f;
        [SerializeField] int maxDrops = 40;
        [Tooltip("How much a band must jump, and how loud it must be, to count as a new note.")]
        [SerializeField] float onsetRise = 0.16f;
        [SerializeField] float onsetLevel = 0.45f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly Color[] BandColors = { Effects.Pink, Effects.Pink, Effects.Orange, Effects.Mint, Effects.Blue, Effects.Violet };

        sealed class Drop
        {
            public Renderer Glow;
            public LineStrip Trail;
            public Vector3 Position;
            public Color Color;
            public float Size;
            public int Note;
            public bool Active;
        }

        sealed class Splash
        {
            public Renderer Ring;
            public float Age = -1f;
            public float Size;
            public Color Color;
        }

        readonly List<Drop> drops = new List<Drop>();
        readonly List<Splash> splashes = new List<Splash>();
        readonly float[] lastBands = new float[MusicBus.BandCount];
        readonly float[] cooldown = new float[MusicBus.BandCount];
        readonly float[] charge = new float[2];
        readonly Color[] chargeColor = { Effects.Gold, Effects.Gold };
        readonly Vector3[] trail = new Vector3[2];
        Renderer[] palmGlows;
        AudioPool voices;
        ParticleSystem sparks;
        MaterialPropertyBlock block;
        Vector3 center, right, forward;
        float drizzle, floorY;
        bool anchored, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            for (int i = 0; i < maxDrops; i++)
            {
                var drop = new Drop
                {
                    Glow = Effects.MakeQuad($"Drop {i}", transform, glowMaterial),
                    Trail = new LineStrip($"Trail {i}", transform, lineMaterial, 2)
                };
                drop.Glow.gameObject.SetActive(false);
                drop.Trail.SetVisible(false);
                drops.Add(drop);
            }
            for (int i = 0; i < 16; i++)
            {
                var ring = Effects.MakeQuad($"Splash {i}", transform, ringMaterial);
                ring.gameObject.SetActive(false);
                splashes.Add(new Splash { Ring = ring });
            }
            palmGlows = new[] { Effects.MakeQuad("Palm Glow L", transform, glowMaterial), Effects.MakeQuad("Palm Glow R", transform, glowMaterial) };
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
            foreach (var d in drops)
            {
                d.Active = false;
                d.Glow.gameObject.SetActive(false);
                d.Trail.SetVisible(false);
            }
            foreach (var s in splashes)
            {
                s.Age = -1f;
                s.Ring.gameObject.SetActive(false);
            }
            charge[0] = charge[1] = 0f;
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            if (!anchored)
            {
                forward = Effects.FlatForward(view);
                right = Vector3.Cross(Vector3.up, forward).normalized;
                center = view.transform.position + forward * distance;
                floorY = Effects.FloorY(view);
                anchored = true;
            }

            SpawnFromMusic(music, dt);
            UpdateDrops(rig, view, dt);
            UpdateSplashes(dt);
            UpdatePalms(rig, view, music, dt);
        }

        /// <summary>For previews: drops a note at a spot in the rain area (u, v from -1 to 1), at a height below the top.</summary>
        public void PreviewDrop(float u, float v, float below, int band, bool big)
        {
            Build();
            Spawn(band, big, new Vector2(u, v), below);
        }

        void SpawnFromMusic(MusicFrame music, float dt)
        {
            for (int b = 2; b < MusicBus.BandCount; b++)
            {
                cooldown[b] -= dt;
                float level = music.Band(b);
                if (level - lastBands[b] > onsetRise && level > onsetLevel && cooldown[b] <= 0f)
                {
                    Spawn(b, false, RandomSpot(), 0f);
                    cooldown[b] = 0.12f;
                }
                lastBands[b] = level;
            }
            if (music.Beat) Spawn(1, true, RandomSpot(), 0f);

            // A light drizzle that follows the music's energy, so the rain never stops completely.
            drizzle += dt * (1.4f + music.Band(3) * 3f);
            while (drizzle >= 1f)
            {
                drizzle -= 1f;
                Spawn(Random.Range(2, MusicBus.BandCount), false, RandomSpot(), 0f);
            }
        }

        static Vector2 RandomSpot() => new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));

        void Spawn(int band, bool big, Vector2 spot, float below)
        {
            Drop drop = null;
            foreach (var d in drops)
            {
                if (!d.Active) { drop = d; break; }
            }
            if (drop == null) return;
            drop.Active = true;
            drop.Position = center + right * (spot.x * areaWidth * 0.5f) + forward * (spot.y * areaDepth * 0.5f)
                            + Vector3.up * (spawnHeight - below);
            drop.Color = big ? Effects.Gold : BandColors[band];
            drop.Size = big ? 0.034f : Random.Range(0.016f, 0.024f);
            // Higher bands ring higher notes.
            drop.Note = Mathf.Clamp(band + (big ? 0 : 3) + Random.Range(0, 3), 0, Synth.Pentatonic.Length - 1);
            drop.Glow.gameObject.SetActive(true);
            drop.Trail.SetVisible(true);
        }

        void UpdateDrops(HandRig rig, Camera view, float dt)
        {
            foreach (var drop in drops)
            {
                if (!drop.Active) continue;
                drop.Position += Vector3.down * (fallSpeed * dt * (drop.Size > 0.03f ? 0.85f : 1f));

                bool caught = false;
                for (int side = 0; side < 2 && !caught; side++)
                {
                    var hand = rig.Get(side == 0);
                    if (!hand.IsTracked) continue;
                    var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                    var cup = palm + hand.PalmNormal * 0.02f;
                    var flat = Vector3.ProjectOnPlane(drop.Position - cup, Vector3.up);
                    float height = drop.Position.y - cup.y;
                    if (flat.magnitude < catchRadius && height < 0.04f && height > -0.05f)
                    {
                        caught = true;
                        Catch(drop, side, cup, hand.PalmNormal);
                    }
                }
                if (caught) continue;

                if (drop.Position.y < floorY + 0.01f)
                {
                    SpawnSplash(new Vector3(drop.Position.x, floorY + 0.005f, drop.Position.z), Vector3.up, drop.Color, 0.25f);
                    Burst(drop.Position, drop.Color, 5, 0.12f);
                    Release(drop);
                    continue;
                }

                drop.Glow.transform.position = drop.Position;
                drop.Glow.transform.localScale = Vector3.one * drop.Size * 2.2f;
                Effects.Face(drop.Glow.transform, view);
                block.Clear();
                block.SetColor(ColorId, drop.Color);
                block.SetFloat(IntensityId, 1.4f);
                drop.Glow.SetPropertyBlock(block);

                trail[0] = drop.Position + Vector3.up * 0.09f;
                trail[1] = drop.Position;
                var c = drop.Color;
                c.a = 0.55f;
                drop.Trail.Set(trail, 2, drop.Size * 0.6f, c, view.transform.position, false);
            }
        }

        void Catch(Drop drop, int side, Vector3 at, Vector3 normal)
        {
            charge[side] = Mathf.Min(1.5f, charge[side] + (drop.Size > 0.03f ? 0.45f : 0.22f));
            chargeColor[side] = Color.Lerp(chargeColor[side], drop.Color, 0.5f);
            SpawnSplash(at, normal.sqrMagnitude > 0.5f ? normal : Vector3.up, drop.Color, 0.16f);
            Burst(at, drop.Color, drop.Size > 0.03f ? 16 : 8, 0.2f);
            voices.Play(Synth.Chime(Synth.Pentatonic[drop.Note]), at, drop.Size > 0.03f ? 0.8f : 0.55f);
            Release(drop);
        }

        static void Release(Drop drop)
        {
            drop.Active = false;
            drop.Glow.gameObject.SetActive(false);
            drop.Trail.SetVisible(false);
        }

        void SpawnSplash(Vector3 at, Vector3 normal, Color color, float size)
        {
            Splash splash = null;
            foreach (var s in splashes)
            {
                if (s.Age < 0f) { splash = s; break; }
            }
            if (splash == null) return;
            splash.Age = 0f;
            splash.Size = size;
            splash.Color = color;
            splash.Ring.transform.SetPositionAndRotation(at, Quaternion.LookRotation(normal));
            splash.Ring.gameObject.SetActive(true);
        }

        void UpdateSplashes(float dt)
        {
            foreach (var s in splashes)
            {
                if (s.Age < 0f) continue;
                s.Age += dt;
                float u = s.Age / 0.7f;
                if (u >= 1f)
                {
                    s.Age = -1f;
                    s.Ring.gameObject.SetActive(false);
                    continue;
                }
                s.Ring.transform.localScale = Vector3.one * s.Size;
                block.Clear();
                block.SetColor(ColorId, s.Color);
                block.SetFloat(RadiusId, Mathf.Lerp(0.05f, 0.85f, 1f - Mathf.Pow(1f - u, 3f)));
                block.SetFloat(ThicknessId, 0.03f);
                block.SetFloat(AlphaId, Mathf.Pow(1f - u, 2f));
                s.Ring.SetPropertyBlock(block);
            }
        }

        void UpdatePalms(HandRig rig, Camera view, MusicFrame music, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                var glow = palmGlows[side];
                charge[side] = Mathf.MoveTowards(charge[side], 0f, dt * 0.12f);
                bool show = hand.IsTracked && charge[side] > 0.01f;
                if (glow.gameObject.activeSelf != show) glow.gameObject.SetActive(show);
                if (!show) continue;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                glow.transform.position = palm + hand.PalmNormal * 0.012f;
                glow.transform.localScale = Vector3.one * (0.07f + charge[side] * 0.06f) * (1f + music.Kick * 0.15f);
                Effects.Face(glow.transform, view);
                block.Clear();
                block.SetColor(ColorId, chargeColor[side]);
                block.SetFloat(IntensityId, charge[side] * (0.9f + music.Kick * 0.4f));
                glow.SetPropertyBlock(block);
            }
        }

        void Burst(Vector3 at, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                var v = Random.insideUnitSphere * speed;
                v.y = Mathf.Abs(v.y) + speed * 0.3f;
                var emit = new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = v,
                    startColor = color,
                    startSize = Random.Range(0.005f, 0.011f),
                    startLifetime = Random.Range(0.4f, 0.9f)
                };
                sparks.Emit(emit, 1);
            }
        }
    }
}
