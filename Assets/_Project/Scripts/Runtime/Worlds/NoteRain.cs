using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Note rain world: the melody falls as drops of light. Each sung note is a glowing teardrop that lands on the
    /// floor exactly as the note is sung, placed by pitch from left (low) to right (high), like a piano roll on your
    /// floor; a ring on the floor shows where each one will land, tightening as it arrives. A fine drizzle of small
    /// drops falls all around you with the hi-hats and a few more with every beat, so the room is never dry.
    /// Landings leave glowing marks that pulse on the beat, and your palm lights up just before a drop lands in it. Catch drops in your palm instead: each one splashes
    /// with a soft chime and ripples spread across your hand as it fills with light. Songs without a note track rain
    /// on what the analyser hears.
    /// </summary>
    public class NoteRain : World
    {
        public override string DisplayName => "Note rain";

        public override Color Color => new Color(0.5f, 0.72f, 1f);

        public override int MenuOrder => 40;

        public override int MenuIcon => 4;

        public override string Hint => "Hold out a palm and catch the falling notes";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.07f, 0.13f, 0.28f, 0.5f),
            Edge = new Color(0.5f, 0.72f, 1f),
            Glow = new Color(0.75f, 0.95f, 1f),
            Pattern = HandTheme.Effect.Ripples,
            PatternColor = new Color(0.6f, 0.85f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material streakMaterial;
        [SerializeField] Material ringMaterial;

        [Header("Rain")]
        [SerializeField] float areaWidth = 0.9f;
        [SerializeField] float areaDepth = 0.4f;
        [SerializeField] float distance = 0.5f;
        [Tooltip("Where drops appear, relative to the eyes.")]
        [SerializeField] float spawnHeight = 0.35f;
        [Tooltip("Seconds from appearing to landing on the floor.")]
        [SerializeField] float fallTime = 1.8f;
        [SerializeField] float catchRadius = 0.075f;
        [SerializeField] int maxDrops = 120;
        [Tooltip("Small drizzle drops per second at full treble, and extra drops on each beat.")]
        [SerializeField] float drizzleRate = 30f;
        [SerializeField] int dropsPerBeat = 4;
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
            public Renderer Target;
            public Vector3 Position;
            public float LandTime;
            public bool Synced;
            public Color Color;
            public float Size;
            public int Midi;
            public bool Active;
            public bool Drizzle;
            // What it lands on: the floor, or the furniture top under it once the room is scanned.
            public float Ground;
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
        readonly float[] expect = new float[2];
        readonly Color[] expectColor = { Effects.Blue, Effects.Blue };
        readonly Color[] chargeColor = { Effects.Blue, Effects.Blue };
        Renderer[] palmGlows;
        AudioPool voices;
        ParticleSystem sparks;
        MaterialPropertyBlock block;
        Vector3 center, right, forward, head;
        float floorY, spawnY, songTime, beatGlow, drizzleDebt;
        MusicFrame frame;

        protected override int SungWordCount => 6;

        protected override float SungWordHeight => 0.04f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            for (int i = 0; i < maxDrops; i++)
            {
                var drop = new Drop
                {
                    Streak = Effects.MakeQuad($"Drop {i}", transform, streakMaterial),
                    Head = Effects.MakeQuad($"Drop Head {i}", transform, glowMaterial),
                    Target = Effects.MakeQuad($"Drop Target {i}", transform, ringMaterial)
                };
                drop.Streak.gameObject.SetActive(false);
                drop.Head.gameObject.SetActive(false);
                drop.Target.gameObject.SetActive(false);
                drops.Add(drop);
            }
            for (int i = 0; i < 40; i++)
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

        protected override void OnEnter()
        {
            spawned.Clear();
        }

        protected override void OnExit()
        {
            foreach (var d in drops) Release(d);
            foreach (var m in marks)
            {
                m.Age = -1f;
                m.Ring.gameObject.SetActive(false);
                m.Glow.gameObject.SetActive(false);
            }
            charge[0] = charge[1] = 0f;
        }

        /// <summary>The rain falls in front of you, from a little above your eyes down to the floor.</summary>
        protected override void OnPlace(Camera view)
        {
            forward = Effects.FlatForward(view);
            right = Vector3.Cross(Vector3.up, forward).normalized;
            center = view.transform.position + forward * distance;
            head = view.transform.position;
            floorY = Effects.FloorY(view);
            spawnY = view.transform.position.y + spawnHeight;
        }

        /// <summary>
        /// Each sung word falls with the rain in front of you, a little left or right along the line, and lands with a
        /// splash in a puddle of light, where it lies for a moment.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            var w = SungWords.Add(word, Color.Lerp(Effects.Blue, Color.white, 0.45f), 7f);
            float x = ((Sung.Index % 5) - 2) * 0.16f + (w.Seed - 0.5f) * 0.06f;
            w.Position = center + right * x + forward * 0.1f + Vector3.up * (head.y + 0.12f - center.y);
            w.Velocity = Vector3.down * 0.06f;
            w.Slot = 0;
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            songTime = music.SongTime;
            frame = music;
            beatGlow = music.Beat ? 1f : Mathf.MoveTowards(beatGlow, 0f, dt * 2.5f);

            bool synced = music.Upcoming != null && music.Upcoming.Count > 0;
            if (synced) SpawnFromNotes(music);
            else SpawnFromAnalysis(music, dt);
            SpawnDrizzle(music, dt);
            UpdateDrops(rig, view, dt);
            UpdateMarks(dt);
            UpdatePalms(rig, view, music, dt);
            Sing(view, dt);
        }

        /// <summary>The sung words fall, land with a splash and lie in their puddles for a moment.</summary>
        void Sing(Camera view, float dt)
        {
            if (SungWords == null) return;
            var eye = view.transform.position;
            foreach (var w in SungWords.Words)
            {
                if (!w.Active) continue;
                float ground = Room.GroundBelow(w.Position, floorY);
                if (w.Slot == 0)
                {
                    // Slow while you read it, then faster, like a drop.
                    w.Velocity += Vector3.down * (0.25f * dt);
                    w.Position += w.Velocity * dt;
                    w.Rotation = WordLayer.Facing(w.Position, eye);
                    if (w.Position.y <= ground + 0.01f)
                    {
                        // Landed: a splash, then it lies in its puddle.
                        w.Slot = 1;
                        w.Position.y = ground + 0.005f;
                        w.Life = w.Age + 2.5f;
                        var at = new Vector3(w.Position.x, ground + 0.004f, w.Position.z);
                        AddMark(at, Vector3.up, Effects.Blue, 0.35f, true);
                        Burst(at, Effects.Blue, 10, 0.25f, true);
                    }
                }
                else w.Rotation = WordLayer.Lying(w.Position, eye);
                w.Halo = 0.15f + beatGlow * 0.3f;
            }
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

        /// <summary>
        /// The drizzle between melody notes: small pale drops with the treble, and a few more on each beat. They fall
        /// faster than melody drops and leave small marks, so the melody stays the star.
        /// </summary>
        void SpawnDrizzle(MusicFrame music, float dt)
        {
            drizzleDebt += drizzleRate * Mathf.Clamp01(music.Band(5) * 1.2f) * dt;
            int count = (int)drizzleDebt;
            drizzleDebt -= count;
            if (music.Beat) count += dropsPerBeat;
            for (int i = 0; i < count; i++)
            {
                // All around you, from arm's length to the far side of the room, never straight through your face.
                float angle = Random.value * Mathf.PI * 2f;
                float reach = Mathf.Sqrt(Random.Range(0.35f * 0.35f, 2.2f * 2.2f));
                var at = head + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
                at.y = spawnY + 0.3f;
                var drop = Spawn(84 + Random.Range(0, 12), Vector2.zero, true, at);
                if (drop != null) drop.LandTime = songTime + fallTime * 0.6f;
            }
        }

        Drop Spawn(int midi, Vector2 spot, bool drizzle = false, Vector3? at = null)
        {
            Drop drop = null;
            foreach (var d in drops)
            {
                if (!d.Active) { drop = d; break; }
            }
            if (drop == null) return null;
            drop.Active = true;
            drop.Synced = false;
            drop.Drizzle = drizzle;
            drop.Midi = midi;
            drop.Position = center + right * (spot.x * areaWidth * 0.5f) + forward * (spot.y * areaDepth * 0.5f);
            drop.Position.y = spawnY;
            if (at.HasValue) drop.Position = at.Value;
            drop.Ground = Room.GroundBelow(drop.Position, floorY);
            drop.Color = PitchColors[Mathf.Abs(midi) % PitchColors.Length];
            drop.Size = drizzle ? Random.Range(0.007f, 0.011f) : 0.026f + Mathf.Clamp01((72f - midi) / 12f) * 0.01f;
            if (drizzle) drop.Color = Color.Lerp(Effects.Blue, Color.white, Random.Range(0.1f, 0.35f));
            drop.Streak.gameObject.SetActive(true);
            drop.Head.gameObject.SetActive(true);
            drop.Target.gameObject.SetActive(!drizzle);
            return drop;
        }

        void UpdateDrops(HandRig rig, Camera view, float dt)
        {
            float speed = (spawnY - floorY) / fallTime;
            expect[0] = expect[1] = 0f;
            foreach (var drop in drops)
            {
                if (!drop.Active) continue;
                // Synced drops are placed from the song clock, so they always land on their note.
                if (drop.Synced) drop.Position.y = drop.Ground + Mathf.Clamp01((drop.LandTime - songTime) / fallTime) * (spawnY - drop.Ground);
                else drop.Position.y -= speed * (drop.Drizzle ? 1.7f : 1f) * dt;

                if (TryCatch(rig, drop)) continue;
                if (!drop.Drizzle)
                {
                    Anticipate(rig, drop);
                    DrawTarget(drop);
                }

                if (drop.Position.y <= drop.Ground + 0.003f)
                {
                    Land(drop);
                    continue;
                }

                // The streak hangs above the head, facing the viewer around the vertical.
                float length = Mathf.Clamp(speed * 0.2f, 0.08f, 0.24f) * (drop.Drizzle ? 0.35f : 1f);
                var toView = Vector3.ProjectOnPlane(view.transform.position - drop.Position, Vector3.up);
                var facing = toView.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(-toView.normalized, Vector3.up) : Quaternion.identity;
                drop.Streak.transform.SetPositionAndRotation(drop.Position + Vector3.up * (length * 0.5f - length * 0.1f), facing);
                drop.Streak.transform.localScale = new Vector3(drop.Size * 1.3f, length, 1f);
                block.Clear();
                block.SetColor(ColorId, drop.Color);
                block.SetFloat(IntensityId, drop.Drizzle ? 0.45f : 1.3f);
                drop.Streak.SetPropertyBlock(block);

                drop.Head.transform.position = drop.Position;
                drop.Head.transform.localScale = Vector3.one * drop.Size * 2.6f;
                Effects.Face(drop.Head.transform, view);
                block.Clear();
                block.SetColor(ColorId, drop.Color);
                block.SetFloat(IntensityId, drop.Drizzle ? 0.5f : 1.1f);
                drop.Head.SetPropertyBlock(block);
            }
        }

        /// <summary>A ring on the floor where a melody drop will land: wide and faint at first, tight and bright as it arrives.</summary>
        void DrawTarget(Drop drop)
        {
            float due = drop.Synced ? 1f - Mathf.Clamp01((drop.LandTime - songTime) / fallTime) : 1f - Mathf.Clamp01((drop.Position.y - drop.Ground) / Mathf.Max(spawnY - drop.Ground, 0.01f));
            drop.Target.transform.SetPositionAndRotation(new Vector3(drop.Position.x, drop.Ground + 0.003f, drop.Position.z), Quaternion.Euler(90f, 0f, 0f));
            drop.Target.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 0.07f, due * due);
            block.Clear();
            block.SetColor(ColorId, drop.Color);
            block.SetFloat(RadiusId, 0.75f);
            block.SetFloat(ThicknessId, Mathf.Lerp(0.02f, 0.05f, due));
            block.SetFloat(AlphaId, due * 0.85f);
            drop.Target.SetPropertyBlock(block);
        }

        /// <summary>A palm under a falling melody drop glows in its colour, brighter the closer it gets.</summary>
        void Anticipate(HandRig rig, Drop drop)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                if (!hand.IsTracked) continue;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                var flat = Vector3.ProjectOnPlane(drop.Position - palm, Vector3.up);
                float height = drop.Position.y - palm.y;
                if (flat.magnitude > catchRadius * 1.3f || height < 0f || height > 0.45f) continue;
                float near = 1f - height / 0.45f;
                if (near > expect[side])
                {
                    expect[side] = near;
                    expectColor[side] = drop.Color;
                }
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

                var normal = hand.PalmNormal.sqrMagnitude > 0.5f ? hand.PalmNormal : Vector3.up;
                if (drop.Drizzle)
                {
                    // Drizzle just tickles the palm: a little light and a tiny ripple, no chime.
                    charge[side] = Mathf.Min(1.4f, charge[side] + 0.04f);
                    AddMark(cup, normal, drop.Color, 0.08f, false);
                    Release(drop);
                    return true;
                }
                charge[side] = Mathf.Min(1.4f, charge[side] + 0.2f);
                chargeColor[side] = Color.Lerp(chargeColor[side], drop.Color, 0.5f);
                AddMark(cup, normal, drop.Color, 0.15f, false);
                Burst(cup, drop.Color, 10, 0.18f, false);
                // Soft, so it decorates the sung note rather than competing with it.
                Musical.Play(voices, frame, drop.Midi, cup, 0.3f);
                Release(drop);
                return true;
            }
            return false;
        }

        void Land(Drop drop)
        {
            var at = new Vector3(drop.Position.x, drop.Ground + 0.004f, drop.Position.z);
            if (drop.Drizzle)
            {
                AddMark(at, Vector3.up, drop.Color, 0.12f, false);
                Burst(at, drop.Color, 3, 0.12f, true);
            }
            else
            {
                AddMark(at, Vector3.up, drop.Color, 0.5f, true);
                Burst(at, drop.Color, 18, 0.3f, true);
            }
            Release(drop);
        }

        static void Release(Drop drop)
        {
            drop.Active = false;
            drop.Streak.gameObject.SetActive(false);
            drop.Head.gameObject.SetActive(false);
            drop.Target.gameObject.SetActive(false);
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
                float life = m.OnFloor ? 4f : 0.7f;
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
                block.SetFloat(ThicknessId, m.OnFloor ? 0.035f : 0.025f);
                block.SetFloat(AlphaId, u < 1f ? Mathf.Pow(1f - u, 1.5f) : 0f);
                m.Ring.SetPropertyBlock(block);

                if (m.OnFloor)
                {
                    float fade = 1f - Mathf.Clamp01((m.Age - 0.3f) / (life - 0.3f));
                    m.Glow.transform.localScale = Vector3.one * (0.16f + beatGlow * 0.05f);
                    block.Clear();
                    block.SetColor(ColorId, m.Color);
                    block.SetFloat(IntensityId, fade * (1.1f + beatGlow * 0.8f));
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
                bool show = hand.IsTracked && (charge[side] > 0.01f || expect[side] > 0.01f);
                if (glow.gameObject.activeSelf != show) glow.gameObject.SetActive(show);
                if (!show) continue;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                glow.transform.position = palm + hand.PalmNormal * 0.015f;
                float ready = expect[side] * expect[side];
                glow.transform.localScale = Vector3.one * (0.06f + charge[side] * 0.05f + ready * 0.04f) * (1f + music.Kick * 0.15f);
                Effects.Face(glow.transform, view);
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(chargeColor[side], expectColor[side], ready / Mathf.Max(ready + charge[side], 1e-3f)));
                block.SetFloat(IntensityId, charge[side] * (0.7f + music.Kick * 0.4f) + ready * 0.9f);
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
