using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Note rain world: the melody falls as drops of light. Each sung note is a glowing teardrop that lands on the
    /// floor exactly as the note is sung, placed by pitch from left (low) to right (high), like a piano roll on your
    /// floor. Landings leave glowing marks that pulse on the beat. Catch drops in your palm instead: each one splashes
    /// with a soft chime and ripples spread across your hand as it fills with light. Songs without a note track rain
    /// on what the analyser hears.
    /// </summary>
    public class NoteRain : World
    {
        public override int MenuIcon => 4;

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.07f, 0.13f, 0.28f, 0.5f),
            Edge = new Color(0.5f, 0.72f, 1f),
            Glow = new Color(0.75f, 0.95f, 1f),
            Pattern = HandTheme.Effect.Ripples,
            PatternColor = new Color(0.6f, 0.85f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material glowMaterial;
        [SerializeField] Material streakMaterial;
        [SerializeField] Material ringMaterial;

        [Header("Rain")]
        [SerializeField] float areaWidth = 0.9f;
        [SerializeField] float areaDepth = 0.5f;
        [SerializeField] float distance = 0.55f;
        [Tooltip("Where drops appear, relative to the eyes.")]
        [SerializeField] float spawnHeight = 0.5f;
        [Tooltip("Seconds from appearing to landing on the floor.")]
        [SerializeField] float fallTime = 1.8f;
        [SerializeField] float catchRadius = 0.075f;
        [SerializeField] int maxDrops = 40;
        [Tooltip("For songs without notes: how much a band must jump, and how loud it must be, to count as a note.")]
        [SerializeField] float onsetRise = 0.16f;
        [SerializeField] float onsetLevel = 0.45f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly Color[] PitchColors = { Effects.Pink, Effects.Orange, Effects.Gold, Effects.Mint, Effects.Blue, Effects.Violet };

        sealed class Drop
        {
            public Renderer Streak;
            public Renderer Head;
            public Vector3 Position;
            public float LandTime;
            public bool Synced;
            public Color Color;
            public float Size;
            public int Midi;
            public bool Active;
        }

        sealed class Mark
        {
            public Renderer Ring;
            public Renderer Glow;
            public float Age = -1f;
            public float Size;
            public Color Color;
            public bool OnFloor;
        }

        readonly List<Drop> drops = new List<Drop>();
        readonly List<Mark> marks = new List<Mark>();
        readonly HashSet<float> spawned = new HashSet<float>();
        readonly float[] lastBands = new float[MusicBus.BandCount];
        readonly float[] cooldown = new float[MusicBus.BandCount];
        readonly float[] charge = new float[2];
        readonly Color[] chargeColor = { Effects.Blue, Effects.Blue };
        Renderer[] palmGlows;
        AudioPool voices;
        ParticleSystem sparks;
        MaterialPropertyBlock block;
        Vector3 center, right, forward;
        float floorY, spawnY, songTime, beatGlow;
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
                    Streak = Effects.MakeQuad($"Drop {i}", transform, streakMaterial),
                    Head = Effects.MakeQuad($"Drop Head {i}", transform, glowMaterial)
                };
                drop.Streak.gameObject.SetActive(false);
                drop.Head.gameObject.SetActive(false);
                drops.Add(drop);
            }
            for (int i = 0; i < 28; i++)
            {
                var mark = new Mark { Ring = Effects.MakeQuad($"Splash {i}", transform, ringMaterial), Glow = Effects.MakeQuad($"Mark {i}", transform, glowMaterial) };
                mark.Ring.gameObject.SetActive(false);
                mark.Glow.gameObject.SetActive(false);
                marks.Add(mark);
            }
            palmGlows = new[] { Effects.MakeQuad("Palm Glow L", transform, glowMaterial), Effects.MakeQuad("Palm Glow R", transform, glowMaterial) };
            voices = new AudioPool(transform, 6);
            sparks = Effects.MakeSparkSystem("Sparks", transform, glowMaterial);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
            spawned.Clear();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (var d in drops) Release(d);
            foreach (var m in marks)
            {
                m.Age = -1f;
                m.Ring.gameObject.SetActive(false);
                m.Glow.gameObject.SetActive(false);
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
                spawnY = view.transform.position.y + spawnHeight;
                anchored = true;
            }
            songTime = music.SongTime;
            beatGlow = music.Beat ? 1f : Mathf.MoveTowards(beatGlow, 0f, dt * 2.5f);

            bool synced = music.Upcoming != null && music.Upcoming.Count > 0;
            if (synced) SpawnFromNotes(music);
            else SpawnFromAnalysis(music, dt);
            UpdateDrops(rig, view, dt);
            UpdateMarks(dt);
            UpdatePalms(rig, view, music, dt);
        }

        /// <summary>Each note appears <see cref="fallTime"/> before it is sung, so it lands on the floor on the note.</summary>
        void SpawnFromNotes(MusicFrame music)
        {
            foreach (var note in music.Upcoming)
            {
                float ahead = note.Time - songTime;
                if (ahead > fallTime || ahead < 0f || spawned.Contains(note.Time)) continue;
                spawned.Add(note.Time);
                float x = Mathf.Clamp01((note.Midi - 60f) / 20f) * 2f - 1f;
                float z = Mathf.Sin(note.Time * 12.9898f) * 0.8f;
                var drop = Spawn(note.Midi, new Vector2(x, z));
                if (drop == null) continue;
                drop.Synced = true;
                drop.LandTime = note.Time;
            }
            if (spawned.Count > 256) spawned.Clear();
        }

        void SpawnFromAnalysis(MusicFrame music, float dt)
        {
            for (int b = 2; b < MusicBus.BandCount; b++)
            {
                cooldown[b] -= dt;
                float level = music.Band(b);
                if (level - lastBands[b] > onsetRise && level > onsetLevel && cooldown[b] <= 0f)
                {
                    var drop = Spawn(60 + b * 3 + Random.Range(0, 4), new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
                    if (drop != null) drop.LandTime = songTime + fallTime;
                    cooldown[b] = 0.12f;
                }
                lastBands[b] = level;
            }
        }

        Drop Spawn(int midi, Vector2 spot)
        {
            Drop drop = null;
            foreach (var d in drops)
            {
                if (!d.Active) { drop = d; break; }
            }
            if (drop == null) return null;
            drop.Active = true;
            drop.Synced = false;
            drop.Midi = midi;
            drop.Position = center + right * (spot.x * areaWidth * 0.5f) + forward * (spot.y * areaDepth * 0.5f);
            drop.Position.y = spawnY;
            drop.Color = PitchColors[Mathf.Abs(midi) % PitchColors.Length];
            drop.Size = 0.018f + Mathf.Clamp01((72f - midi) / 12f) * 0.008f;
            drop.Streak.gameObject.SetActive(true);
            drop.Head.gameObject.SetActive(true);
            return drop;
        }

        void UpdateDrops(HandRig rig, Camera view, float dt)
        {
            float speed = (spawnY - floorY) / fallTime;
            foreach (var drop in drops)
            {
                if (!drop.Active) continue;
                // Synced drops are placed from the song clock, so they always land on their note.
                if (drop.Synced) drop.Position.y = floorY + Mathf.Clamp01((drop.LandTime - songTime) / fallTime) * (spawnY - floorY);
                else drop.Position.y -= speed * dt;

                if (TryCatch(rig, drop)) continue;

                if (drop.Position.y <= floorY + 0.003f)
                {
                    Land(drop);
                    continue;
                }

                // The streak hangs above the head, facing the viewer around the vertical.
                float length = Mathf.Clamp(speed * 0.12f, 0.05f, 0.14f);
                var toView = Vector3.ProjectOnPlane(view.transform.position - drop.Position, Vector3.up);
                var facing = toView.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(-toView.normalized, Vector3.up) : Quaternion.identity;
                drop.Streak.transform.SetPositionAndRotation(drop.Position + Vector3.up * (length * 0.5f - drop.Size * 0.3f), facing);
                drop.Streak.transform.localScale = new Vector3(drop.Size * 1.3f, length, 1f);
                block.Clear();
                block.SetColor(ColorId, drop.Color);
                block.SetFloat(IntensityId, 1.3f);
                drop.Streak.SetPropertyBlock(block);

                drop.Head.transform.position = drop.Position;
                drop.Head.transform.localScale = Vector3.one * drop.Size * 2.6f;
                Effects.Face(drop.Head.transform, view);
                block.Clear();
                block.SetColor(ColorId, drop.Color);
                block.SetFloat(IntensityId, 1.1f);
                drop.Head.SetPropertyBlock(block);
            }
        }

        bool TryCatch(HandRig rig, Drop drop)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                if (!hand.IsTracked) continue;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                var cup = palm + hand.PalmNormal * 0.02f;
                var flat = Vector3.ProjectOnPlane(drop.Position - cup, Vector3.up);
                float height = drop.Position.y - cup.y;
                if (flat.magnitude >= catchRadius || height > 0.04f || height < -0.05f) continue;

                charge[side] = Mathf.Min(1.4f, charge[side] + 0.2f);
                chargeColor[side] = Color.Lerp(chargeColor[side], drop.Color, 0.5f);
                AddMark(cup, hand.PalmNormal.sqrMagnitude > 0.5f ? hand.PalmNormal : Vector3.up, drop.Color, 0.15f, false);
                Burst(cup, drop.Color, 10, 0.18f, false);
                // Soft, so it decorates the sung note rather than competing with it.
                voices.Play(Synth.Chime(440f * Mathf.Pow(2f, (drop.Midi + 12 - 69) / 12f)), cup, 0.25f);
                Release(drop);
                return true;
            }
            return false;
        }

        void Land(Drop drop)
        {
            var at = new Vector3(drop.Position.x, floorY + 0.004f, drop.Position.z);
            AddMark(at, Vector3.up, drop.Color, 0.32f, true);
            Burst(at, drop.Color, 14, 0.25f, true);
            Release(drop);
        }

        static void Release(Drop drop)
        {
            drop.Active = false;
            drop.Streak.gameObject.SetActive(false);
            drop.Head.gameObject.SetActive(false);
        }

        void AddMark(Vector3 at, Vector3 normal, Color color, float size, bool onFloor)
        {
            Mark mark = null;
            float oldest = -1f;
            foreach (var m in marks)
            {
                if (m.Age < 0f) { mark = m; break; }
                if (m.Age > oldest) { oldest = m.Age; mark = m; }
            }
            mark.Age = 0f;
            mark.Size = size;
            mark.Color = color;
            mark.OnFloor = onFloor;
            var rotation = Quaternion.LookRotation(normal);
            mark.Ring.transform.SetPositionAndRotation(at, rotation);
            mark.Glow.transform.SetPositionAndRotation(at + normal * 0.002f, rotation);
            mark.Ring.gameObject.SetActive(true);
            mark.Glow.gameObject.SetActive(onFloor);
        }

        /// <summary>A splash ring spreads from each landing; on the floor a glowing mark stays and pulses with the beat.</summary>
        void UpdateMarks(float dt)
        {
            foreach (var m in marks)
            {
                if (m.Age < 0f) continue;
                m.Age += dt;
                float life = m.OnFloor ? 3f : 0.7f;
                if (m.Age >= life)
                {
                    m.Age = -1f;
                    m.Ring.gameObject.SetActive(false);
                    m.Glow.gameObject.SetActive(false);
                    continue;
                }
                float u = Mathf.Clamp01(m.Age / 0.8f);
                m.Ring.transform.localScale = Vector3.one * m.Size;
                block.Clear();
                block.SetColor(ColorId, m.Color);
                block.SetFloat(RadiusId, Mathf.Lerp(0.05f, 0.85f, 1f - Mathf.Pow(1f - u, 3f)));
                block.SetFloat(ThicknessId, 0.025f);
                block.SetFloat(AlphaId, u < 1f ? Mathf.Pow(1f - u, 2f) : 0f);
                m.Ring.SetPropertyBlock(block);

                if (m.OnFloor)
                {
                    float fade = 1f - Mathf.Clamp01((m.Age - 0.3f) / (life - 0.3f));
                    m.Glow.transform.localScale = Vector3.one * (0.09f + beatGlow * 0.03f);
                    block.Clear();
                    block.SetColor(ColorId, m.Color);
                    block.SetFloat(IntensityId, fade * (0.6f + beatGlow * 0.6f));
                    m.Glow.SetPropertyBlock(block);
                }
            }
        }

        void UpdatePalms(HandRig rig, Camera view, MusicFrame music, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                var glow = palmGlows[side];
                charge[side] = Mathf.MoveTowards(charge[side], 0f, dt * 0.12f);
                HandSkin.Charge[side] = Mathf.Clamp01(charge[side]);
                bool show = hand.IsTracked && charge[side] > 0.01f;
                if (glow.gameObject.activeSelf != show) glow.gameObject.SetActive(show);
                if (!show) continue;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                glow.transform.position = palm + hand.PalmNormal * 0.015f;
                glow.transform.localScale = Vector3.one * (0.06f + charge[side] * 0.05f) * (1f + music.Kick * 0.15f);
                Effects.Face(glow.transform, view);
                block.Clear();
                block.SetColor(ColorId, chargeColor[side]);
                block.SetFloat(IntensityId, charge[side] * (0.7f + music.Kick * 0.4f));
                glow.SetPropertyBlock(block);
            }
        }

        /// <summary>A splash: droplets thrown up and out in a crown (on the floor) or a spray (in the hand).</summary>
        void Burst(Vector3 at, Color color, int count, float speed, bool crown)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 v;
                if (crown)
                {
                    float a = i / (float)count * Mathf.PI * 2f;
                    v = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * speed * 0.6f + Vector3.up * speed * Random.Range(0.8f, 1.3f);
                }
                else
                {
                    v = Random.insideUnitSphere * speed;
                    v.y = Mathf.Abs(v.y) + speed * 0.3f;
                }
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
