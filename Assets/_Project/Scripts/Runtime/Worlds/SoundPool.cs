using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Sound pool world: a liquid surface at table height in front of you. The bass rolls slow swells across it,
    /// each kick drops a ring in the middle, and every melody note falls in as a glowing droplet exactly when it is
    /// sung, placed by pitch from left (low) to right (high). Dip your hand in and the liquid parts around your
    /// fingers and ripples out as you move, and the music bends around it: the deeper your hand, the more muffled the
    /// song sounds, as if you were hearing it under water. Your hand glistens below the waterline.
    /// </summary>
    public class SoundPool : World
    {
        public override int MenuIcon => 8;

        public override string Hint => "Dip your hand into the pool and listen";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.05f, 0.14f, 0.2f, 0.5f),
            Edge = new Color(0.37f, 0.83f, 1f),
            Glow = new Color(0.75f, 0.95f, 1f),
            Pattern = HandTheme.Effect.Submerged,
            PatternColor = new Color(0.45f, 0.85f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material waterMaterial;
        [SerializeField] Material glowMaterial;

        [Header("Pool")]
        [SerializeField] Vector2 size = new Vector2(0.7f, 0.48f);
        [Tooltip("How far in front of the eyes the pool's centre is (m).")]
        [SerializeField] float distance = 0.5f;
        [Tooltip("How far below the eyes the surface is (m), like a table top.")]
        [SerializeField] float belowEyes = 0.52f;
        [SerializeField] int columns = 72;
        [SerializeField] int rows = 50;
        [Tooltip("How quickly ripples die away (per simulation step).")]
        [SerializeField, Range(0.9f, 1f)] float damping = 0.988f;

        [Header("Feel")]
        [Tooltip("Low-pass cutoff (Hz) with the hand fully dipped.")]
        [SerializeField] float muffledCutoff = 650f;
        [Tooltip("How deep (m) the hand must go for the music to be fully muffled.")]
        [SerializeField] float fullDip = 0.1f;
        [Tooltip("Seconds a melody droplet takes to fall into the pool.")]
        [SerializeField] float dropTime = 0.5f;

        static readonly int KickId = Shader.PropertyToID("_Kick");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly Color[] DropColors = { Effects.Blue, Effects.Mint, new Color(0.6f, 0.92f, 1f), Effects.Violet };

        Transform surface;
        Mesh mesh;
        MeshRenderer surfaceRenderer;
        Vector3[] vertices, normals;
        float[] height, previous, next, sponge;
        ParticleSystem splashes;
        MaterialPropertyBlock block;
        AudioLowPassFilter lowPass;
        readonly HashSet<float> spawned = new HashSet<float>();
        readonly List<(float time, Vector2 spot)> pending = new List<(float, Vector2)>();
        readonly bool[,] wasUnder = new bool[2, 6];
        readonly Vector3[,] lastPoint = new Vector3[2, 6];
        readonly float[] lastBands = new float[MusicBus.BandCount];
        float cutoff = 22000f, fade, songTime, onsetCooldown;
        bool anchored, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            int count = columns * rows;
            height = new float[count];
            previous = new float[count];
            next = new float[count];
            sponge = new float[count];
            vertices = new Vector3[count];
            normals = new Vector3[count];
            var uvs = new Vector2[count];
            var triangles = new int[(columns - 1) * (rows - 1) * 6];
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int i = z * columns + x;
                    float u = x / (float)(columns - 1), v = z / (float)(rows - 1);
                    uvs[i] = new Vector2(u, v);
                    vertices[i] = new Vector3((u - 0.5f) * size.x, 0f, (v - 0.5f) * size.y);
                    normals[i] = Vector3.up;
                    // A soft absorbing border, so ripples fade out at the edges instead of bouncing back hard.
                    int border = Mathf.Min(Mathf.Min(x, columns - 1 - x), Mathf.Min(z, rows - 1 - z));
                    sponge[i] = border >= 5 ? 1f : Mathf.Lerp(0.82f, 1f, border / 5f);
                }
            }
            int t = 0;
            for (int z = 0; z < rows - 1; z++)
            {
                for (int x = 0; x < columns - 1; x++)
                {
                    int i = z * columns + x;
                    triangles[t++] = i; triangles[t++] = i + columns; triangles[t++] = i + 1;
                    triangles[t++] = i + 1; triangles[t++] = i + columns; triangles[t++] = i + columns + 1;
                }
            }
            mesh = new Mesh { name = "Pool surface" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size.x, 0.2f, size.y));

            surface = new GameObject("Pool").transform;
            surface.SetParent(transform, false);
            surface.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            surfaceRenderer = surface.gameObject.AddComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterial = waterMaterial;
            surfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            surfaceRenderer.receiveShadows = false;
            surface.gameObject.SetActive(false);

            splashes = Effects.MakeSparkSystem("Splashes", transform, glowMaterial);
            var main = splashes.main;
            main.gravityModifier = 0.5f;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
            fade = 0f;
            spawned.Clear();
            pending.Clear();
            System.Array.Clear(wasUnder, 0, wasUnder.Length);
            if (height != null)
            {
                System.Array.Clear(height, 0, height.Length);
                System.Array.Clear(previous, 0, previous.Length);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (surface != null) surface.gameObject.SetActive(false);
            cutoff = 22000f;
            if (lowPass != null) lowPass.enabled = false;
            if (splashes != null) splashes.Clear();
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            if (anchored && ShouldReanchor(view, dt)) anchored = false;
            if (!anchored)
            {
                var forward = Effects.FlatForward(view);
                var center = view.transform.position + forward * distance + Vector3.down * belowEyes;
                center.y = Mathf.Max(center.y, Effects.FloorY(view) + 0.45f);
                surface.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
                surface.gameObject.SetActive(true);
                MarkAnchored(view);
                anchored = true;
            }
            songTime = music.SongTime;
            fade = Mathf.MoveTowards(fade, 1f, dt * 2f);
            HandSkin.FocusPosition = surface.position;
            HandSkin.FocusStrength = fade;

            if (music.Beat) Disturb(new Vector2(0.5f, 0.5f), 0.05f, -0.006f - music.Kick * 0.004f);
            if (music.Upcoming != null && music.Upcoming.Count > 0) SpawnFromNotes(music);
            else SpawnFromAnalysis(music, dt);
            LandDroplets();

            float dip = TouchWithHands(rig, dt);
            Simulate(dt);
            UpdateMesh(music);
            Muffle(music.Bus, dip, dt);

            block.Clear();
            block.SetFloat(KickId, music.Kick);
            block.SetFloat(AlphaId, fade);
            surfaceRenderer.SetPropertyBlock(block);
        }

        /// <summary>Each melody note falls in as a droplet, timed to hit the surface as it is sung.</summary>
        void SpawnFromNotes(MusicFrame music)
        {
            foreach (var note in music.Upcoming)
            {
                float ahead = note.Time - songTime;
                if (ahead > dropTime || ahead < 0f || spawned.Contains(note.Time)) continue;
                spawned.Add(note.Time);
                var spot = new Vector2(0.15f + Mathf.Clamp01((note.Midi - 60f) / 20f) * 0.7f, 0.5f + Mathf.Sin(note.Time * 12.9898f) * 0.28f);
                SpawnDroplet(spot, note.Time, ahead, DropColors[Mathf.Abs(note.Midi) % DropColors.Length]);
            }
            if (spawned.Count > 256) spawned.Clear();
        }

        /// <summary>Without a note track, a sudden rise in a band drops a droplet in.</summary>
        void SpawnFromAnalysis(MusicFrame music, float dt)
        {
            onsetCooldown -= dt;
            for (int b = 2; b < MusicBus.BandCount; b++)
            {
                float level = music.Band(b);
                if (level - lastBands[b] > 0.16f && level > 0.45f && onsetCooldown <= 0f)
                {
                    var spot = new Vector2(Random.Range(0.15f, 0.85f), Random.Range(0.25f, 0.75f));
                    SpawnDroplet(spot, songTime + dropTime, dropTime, DropColors[b % DropColors.Length]);
                    onsetCooldown = 0.15f;
                }
                lastBands[b] = level;
            }
        }

        void SpawnDroplet(Vector2 spot, float landTime, float ahead, Color color)
        {
            var target = SurfacePoint(spot);
            const float fall = 0.28f;
            float lifetime = Mathf.Max(0.05f, ahead);
            splashes.Emit(new ParticleSystem.EmitParams
            {
                position = target + Vector3.up * fall,
                // Under the system's gravity, fall exactly onto the spot in the time left.
                velocity = Vector3.up * (-fall / lifetime + 0.5f * Physics.gravity.magnitude * 0.5f * lifetime),
                startColor = color,
                startSize = 0.016f,
                startLifetime = lifetime
            }, 1);
            pending.Add((landTime, spot));
        }

        void LandDroplets()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var (time, spot) = pending[i];
                // Song time jumps back when the song loops; drop anything left over from before.
                if (time - songTime > dropTime + 0.5f) { pending.RemoveAt(i); continue; }
                if (songTime < time) continue;
                Disturb(spot, 0.03f, -0.008f);
                Splash(SurfacePoint(spot), DropColors[i % DropColors.Length], 10, 0.35f);
                pending.RemoveAt(i);
            }
        }

        /// <summary>
        /// Fingertips and palms under the surface push the liquid aside; moving them makes waves, and breaking the
        /// surface splashes. Returns how deep (0-1) the deepest hand is, for muffling the music.
        /// </summary>
        float TouchWithHands(HandRig rig, float dt)
        {
            float dip = 0f;
            if (rig == null) return dip;
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                for (int k = 0; k < 6; k++)
                {
                    if (!hand.IsTracked)
                    {
                        wasUnder[side, k] = false;
                        continue;
                    }
                    var point = k < 5
                        ? hand.TipJoints[k] != null ? hand.TipJoints[k].position : hand.Tips[k]
                        : hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                    var local = surface.InverseTransformPoint(point);
                    var spot = new Vector2(local.x / size.x + 0.5f, local.z / size.y + 0.5f);
                    bool inside = spot.x > 0f && spot.x < 1f && spot.y > 0f && spot.y < 1f;
                    float depth = -local.y;
                    bool under = inside && depth > 0f && depth < 0.25f;
                    if (under)
                    {
                        dip = Mathf.Max(dip, Mathf.Clamp01(depth / fullDip));
                        float radius = k < 5 ? 0.012f : 0.035f;
                        float speed = dt > 0f ? (point - lastPoint[side, k]).magnitude / dt : 0f;
                        // A dent where the hand is, deeper the faster it moves, which trails a wake behind it.
                        Disturb(spot, radius, -Mathf.Min(0.004f, depth * 0.05f + speed * 0.004f) * dt * 30f);
                        if (!wasUnder[side, k])
                        {
                            Disturb(spot, radius * 1.5f, -0.006f);
                            Splash(SurfacePoint(spot), Effects.Blue, k < 5 ? 4 : 10, 0.25f);
                        }
                    }
                    else if (wasUnder[side, k] && inside)
                    {
                        // Pulling out lifts a little liquid with the hand.
                        Disturb(spot, k < 5 ? 0.015f : 0.04f, 0.004f);
                    }
                    wasUnder[side, k] = under;
                    lastPoint[side, k] = point;
                }
            }
            return dip;
        }

        /// <summary>Pushes the surface down (negative) or up around a spot (0-1 across the pool).</summary>
        void Disturb(Vector2 spot, float radius, float amount)
        {
            float cx = spot.x * (columns - 1), cz = spot.y * (rows - 1);
            float rx = radius / size.x * (columns - 1), rz = radius / size.y * (rows - 1);
            int x0 = Mathf.Max(1, Mathf.FloorToInt(cx - rx * 2)), x1 = Mathf.Min(columns - 2, Mathf.CeilToInt(cx + rx * 2));
            int z0 = Mathf.Max(1, Mathf.FloorToInt(cz - rz * 2)), z1 = Mathf.Min(rows - 2, Mathf.CeilToInt(cz + rz * 2));
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x - cx) / Mathf.Max(rx, 0.5f), dz = (z - cz) / Mathf.Max(rz, 0.5f);
                    float w = Mathf.Exp(-(dx * dx + dz * dz) * 1.5f);
                    height[z * columns + x] += amount * w;
                }
            }
        }

        /// <summary>Two steps of the wave equation on the height grid per frame.</summary>
        void Simulate(float dt)
        {
            int steps = dt > 0.02f ? 3 : 2;
            const float c2 = 0.3f;
            for (int s = 0; s < steps; s++)
            {
                for (int z = 1; z < rows - 1; z++)
                {
                    for (int x = 1; x < columns - 1; x++)
                    {
                        int i = z * columns + x;
                        float laplacian = height[i - 1] + height[i + 1] + height[i - columns] + height[i + columns] - 4f * height[i];
                        next[i] = (2f * height[i] - previous[i] + c2 * laplacian) * damping * sponge[i];
                    }
                }
                var swap = previous;
                previous = height;
                height = next;
                next = swap;
            }
        }

        /// <summary>Copies the heights into the mesh, plus slow swells with the bass, and rebuilds the normals.</summary>
        void UpdateMesh(MusicFrame music)
        {
            float t = Now;
            float swell = music.Band(1) * 0.004f + music.Kick * 0.0015f;
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int i = z * columns + x;
                    float u = x / (float)(columns - 1), v = z / (float)(rows - 1);
                    float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 8f);
                    float roll = Mathf.Sin(u * 9f - t * 2.2f + v * 2f) * Mathf.Sin(v * 6f + t * 1.3f) * swell * edge;
                    vertices[i].y = height[i] + roll;
                }
            }
            float sx = size.x / (columns - 1), sz = size.y / (rows - 1);
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int i = z * columns + x;
                    float hl = vertices[z * columns + Mathf.Max(x - 1, 0)].y, hr = vertices[z * columns + Mathf.Min(x + 1, columns - 1)].y;
                    float hd = vertices[Mathf.Max(z - 1, 0) * columns + x].y, hu = vertices[Mathf.Min(z + 1, rows - 1) * columns + x].y;
                    normals[i] = new Vector3((hl - hr) / (2f * sx), 1f, (hd - hu) / (2f * sz)).normalized;
                }
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
        }

        /// <summary>Muffles the song with a low-pass filter while a hand is in the liquid, like hearing it under water.</summary>
        void Muffle(MusicBus bus, float dip, float dt)
        {
            if (bus == null) return;
            if (lowPass == null)
            {
                lowPass = bus.GetComponent<AudioLowPassFilter>();
                if (lowPass == null) lowPass = bus.gameObject.AddComponent<AudioLowPassFilter>();
            }
            float target = dip > 0f ? Mathf.Lerp(4000f, muffledCutoff, dip) : 22000f;
            // Glide in log space, so the change sounds even.
            cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(cutoff), Mathf.Log(target), 1f - Mathf.Exp(-(dip > 0f ? 10f : 4f) * dt)));
            lowPass.enabled = cutoff < 20000f;
            lowPass.cutoffFrequency = cutoff;
            lowPass.lowpassResonanceQ = 1.2f;
        }

        Vector3 SurfacePoint(Vector2 spot) => surface.TransformPoint(new Vector3((spot.x - 0.5f) * size.x, 0f, (spot.y - 0.5f) * size.y));

        void Splash(Vector3 at, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f + Random.value;
                var v = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * speed * Random.Range(0.2f, 0.5f) + Vector3.up * speed * Random.Range(0.6f, 1.1f);
                splashes.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Vector3.up * 0.003f,
                    velocity = v,
                    startColor = Color.Lerp(color, Color.white, 0.3f),
                    startSize = Random.Range(0.004f, 0.009f),
                    startLifetime = Random.Range(0.35f, 0.6f)
                }, 1);
            }
        }
    }
}
