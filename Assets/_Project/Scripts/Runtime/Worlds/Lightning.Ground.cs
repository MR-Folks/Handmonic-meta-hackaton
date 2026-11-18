using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// What Lightning's weather leaves on the ground: a grid over the floor around you, and one over each of the four
    /// nearest pieces of furniture once the room is scanned, holding how much snow and how much water lies at each
    /// point. Snow builds up where the flakes land, thin and patchy at first, then a white blanket (up to a cap: past it
    /// the snow melts back); rain wets the floor and the water runs together into puddles in its low spots (a fixed
    /// pattern per room). When the rain comes, the snow melts into water over a few seconds; when the snow comes, the
    /// puddles freeze over white and the rest dries. A strike melts a round hole into the snow. Each grid is a small
    /// texture laid over its surface on one quad, drawn by the LightningGround shader.
    /// </summary>
    public partial class Lightning
    {
        /// <summary>One grid of snow and water over a flat surface, drawn as a texture on one quad.</summary>
        sealed class Patch
        {
            // The grid's first point, and its two directions along the surface.
            public Vector3 Origin, Right, Forward;
            public float StepX, StepZ;
            public int Nx, Nz;
            public float[] Snow, Water, Low, Edge;
            public Color32[] Texels;
            public Texture2D Texture;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public bool Used, Floor;
        }

        // The floor grid: 52 x 52 points 10 cm apart round you; furniture grids are 12 x 12 points over the top.
        const int FloorGrid = 52, TopGrid = 12, TopPatches = 4;
        const float FloorStep = 0.1f;
        // Snow and water at a point: 0 none; SnowFull snow is a solid white cover, SnowMost the deepest it gets; water
        // past WaterFull is a puddle.
        const float SnowFull = 1.6f, SnowMost = 2.5f, WaterFull = 1.4f, WaterMost = 1.6f;
        // How much deeper water stands in the lowest spots than on the highest (in water units), so it runs there.
        const float WaterSink = 0.7f;
        // How far the ground floats over its surface (m).
        const float GroundLift = 0.003f;
        // The ground is worked out and drawn this often (per second), not every frame.
        const float GroundRate = 24f;

        static readonly int CoverId = Shader.PropertyToID("_Cover");
        readonly Patch[] patches = new Patch[1 + TopPatches];
        readonly Vector3[] quadCorners = new Vector3[4];
        Material groundInstance;
        Vector3 floorCentre;
        float groundClock, snowTotal;
        int groundRoom = -1;
        bool floorPlaced;

        void BuildGround()
        {
            if (groundMaterial == null) return;
            groundInstance = new Material(groundMaterial) { name = "Weather Ground" };
            var props = new MaterialPropertyBlock();
            for (int i = 0; i < patches.Length; i++)
            {
                int n = i == 0 ? FloorGrid : TopGrid;
                var p = new Patch { Nx = n, Nz = n, Floor = i == 0 };
                int count = n * n;
                p.Snow = new float[count];
                p.Water = new float[count];
                p.Low = new float[count];
                p.Edge = new float[count];
                p.Texels = new Color32[count];
                var go = new GameObject(i == 0 ? "Weather Floor" : $"Weather Top {i}");
                go.transform.SetParent(transform, false);
                p.Texture = new Texture2D(n, n, TextureFormat.RGBA32, false, true)
                {
                    name = go.name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                p.Mesh = new Mesh { name = go.name };
                p.Mesh.MarkDynamic();
                p.Mesh.SetVertices(quadCorners);
                p.Mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
                p.Mesh.SetTriangles(new[] { 0, 2, 3, 0, 3, 1 }, 0);
                go.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
                p.Renderer = go.AddComponent<MeshRenderer>();
                p.Renderer.sharedMaterial = groundInstance;
                p.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.Renderer.receiveShadows = false;
                p.Renderer.enabled = false;
                props.Clear();
                props.SetTexture(CoverId, p.Texture);
                p.Renderer.SetPropertyBlock(props);
                patches[i] = p;
            }
        }

        /// <summary>
        /// The floor grid is centred on you; it stays put while you shift about (the snow stays where it fell) and is
        /// laid out afresh once you have walked over a metre away, or the floor or the room scan changes. The furniture
        /// grids go over the four nearest tops.
        /// </summary>
        void PlaceGround(Camera view)
        {
            if (groundInstance == null) return;
            var head = view.transform.position;
            float floor = Effects.FloorY(view);
            var floorPatch = patches[0];
            var flat = new Vector2(head.x - floorCentre.x, head.z - floorCentre.z);
            if (!floorPlaced || flat.magnitude > 1.2f || Mathf.Abs(floor - floorPatch.Origin.y) > 0.03f || groundRoom != Room.Version)
            {
                floorCentre = new Vector3(head.x, floor, head.z);
                float half = (FloorGrid - 1) * FloorStep * 0.5f;
                Lay(floorPatch, new Vector3(head.x - half, floor, head.z - half), Vector3.right, Vector3.forward, FloorStep, FloorStep);
                floorPlaced = true;
            }
            if (groundRoom == Room.Version) return;
            groundRoom = Room.Version;
            // The nearest tops (tables, sofas, shelves) within reach of the weather.
            for (int i = 1; i < patches.Length; i++) Unlay(patches[i]);
            for (int slot = 1; slot < patches.Length; slot++)
            {
                int best = -1;
                float bestDistance = weatherReach + 0.5f;
                for (int k = 0; k < Room.Tops.Count; k++)
                {
                    var top = Room.Tops[k];
                    if (top.Center.y < floor + 0.15f || TopTaken(top.Center)) continue;
                    float d = new Vector2(top.Center.x - head.x, top.Center.z - head.z).magnitude;
                    if (d < bestDistance) { bestDistance = d; best = k; }
                }
                if (best < 0) break;
                var s = Room.Tops[best];
                var corner = s.Center - s.Right * (s.Size.x * 0.5f) - s.Up * (s.Size.y * 0.5f);
                Lay(patches[slot], corner, s.Right, s.Up, s.Size.x / (TopGrid - 1), s.Size.y / (TopGrid - 1));
            }
        }

        bool TopTaken(Vector3 centre)
        {
            for (int i = 1; i < patches.Length; i++)
                if (patches[i].Used && Vector3.Distance(Centre(patches[i]), centre) < 0.01f) return true;
            return false;
        }

        static Vector3 Centre(Patch p) =>
            p.Origin + p.Right * (p.StepX * (p.Nx - 1) * 0.5f) + p.Forward * (p.StepZ * (p.Nz - 1) * 0.5f);

        /// <summary>
        /// Lays a grid over a surface, empty, with its low spots (where puddles gather) and its soft edge, and puts its
        /// quad there: half a step past the first and last points, so each texel's middle sits on its point.
        /// </summary>
        void Lay(Patch p, Vector3 origin, Vector3 right, Vector3 forward, float stepX, float stepZ)
        {
            p.Origin = origin;
            p.Right = right.normalized;
            p.Forward = forward.normalized;
            p.StepX = stepX;
            p.StepZ = stepZ;
            p.Used = true;
            float half = (p.Nx - 1) * stepX * 0.5f;
            for (int z = 0; z < p.Nz; z++)
                for (int x = 0; x < p.Nx; x++)
                {
                    int i = z * p.Nx + x;
                    var at = origin + p.Right * (x * stepX) + p.Forward * (z * stepZ);
                    p.Snow[i] = p.Water[i] = 0f;
                    // The floor's low spots: two layers of smooth noise, the same each time for the same place.
                    float low = Mathf.PerlinNoise(at.x * 1.15f + 31.7f, at.z * 1.15f + 12.3f) * 0.7f
                              + Mathf.PerlinNoise(at.x * 3.1f + 5.1f, at.z * 3.1f + 47.9f) * 0.3f;
                    p.Low[i] = Mathf.Clamp01((low - 0.3f) * 1.8f);
                    if (p.Floor)
                    {
                        // A round blanket, fading out towards the grid's edge.
                        float d = new Vector2(x * stepX - half, z * stepZ - half).magnitude;
                        p.Edge[i] = Mathf.Clamp01((half - 0.05f - d) / 0.35f);
                    }
                    else p.Edge[i] = x == 0 || z == 0 || x == p.Nx - 1 || z == p.Nz - 1 ? 0f : 1f;
                }
            var a = origin - p.Right * (stepX * 0.5f) - p.Forward * (stepZ * 0.5f) + Vector3.up * GroundLift;
            var across = p.Right * (stepX * p.Nx);
            var along = p.Forward * (stepZ * p.Nz);
            quadCorners[0] = a;
            quadCorners[1] = a + across;
            quadCorners[2] = a + along;
            quadCorners[3] = a + across + along;
            p.Mesh.SetVertices(quadCorners);
            var min = Vector3.Min(Vector3.Min(quadCorners[0], quadCorners[1]), Vector3.Min(quadCorners[2], quadCorners[3]));
            var max = Vector3.Max(Vector3.Max(quadCorners[0], quadCorners[1]), Vector3.Max(quadCorners[2], quadCorners[3]));
            p.Mesh.bounds = new Bounds((min + max) * 0.5f, max - min + new Vector3(0.05f, 0.05f, 0.05f));
            Upload(p);
        }

        static void Unlay(Patch p)
        {
            p.Used = false;
            p.Renderer.enabled = false;
        }

        /// <summary>The grid a thing landing at <paramref name="at"/> lands on: the top it is over, or the floor.</summary>
        Patch PatchAt(Vector3 at)
        {
            for (int i = 1; i < patches.Length; i++)
            {
                var p = patches[i];
                if (p == null || !p.Used || Mathf.Abs(at.y - p.Origin.y) > 0.08f) continue;
                float lx = Vector3.Dot(at - p.Origin, p.Right) / p.StepX, lz = Vector3.Dot(at - p.Origin, p.Forward) / p.StepZ;
                if (lx >= 0f && lz >= 0f && lx <= p.Nx - 1 && lz <= p.Nz - 1) return p;
            }
            var floor = patches[0];
            return floor != null && floor.Used ? floor : null;
        }

        /// <summary>Adds snow (or water) at a point, spread softly over the grid points round it.</summary>
        void AddGround(Vector3 at, float snow, float water)
        {
            var p = PatchAt(at);
            if (p == null) return;
            float lx = Vector3.Dot(at - p.Origin, p.Right) / p.StepX, lz = Vector3.Dot(at - p.Origin, p.Forward) / p.StepZ;
            int cx = Mathf.RoundToInt(lx), cz = Mathf.RoundToInt(lz);
            if (cx < 1 || cz < 1 || cx > p.Nx - 2 || cz > p.Nz - 2) return;
            // A soft round spot about a grid step across, so the snow and the water have no straight edges.
            float sum = 0f;
            for (int z = -1; z <= 1; z++)
                for (int x = -1; x <= 1; x++)
                {
                    float dx = cx + x - lx, dz = cz + z - lz;
                    sum += Mathf.Exp(-(dx * dx + dz * dz) * 0.9f);
                }
            for (int z = -1; z <= 1; z++)
                for (int x = -1; x <= 1; x++)
                {
                    float dx = cx + x - lx, dz = cz + z - lz;
                    Splat(p, (cz + z) * p.Nx + cx + x, Mathf.Exp(-(dx * dx + dz * dz) * 0.9f) / sum, snow, water);
                }
        }

        void Splat(Patch p, int i, float weight, float snow, float water)
        {
            float before = p.Snow[i];
            p.Snow[i] = Mathf.Min(SnowMost, before + snow * weight);
            snowTotal += p.Snow[i] - before;
            p.Water[i] = Mathf.Min(WaterMost, p.Water[i] + water * weight);
        }

        /// <summary>
        /// Round <paramref name="at"/>, within <paramref name="radius"/>: melts the snow into water (a lightning strike,
        /// a zap), and wets the ground by up to <paramref name="wet"/> more.
        /// </summary>
        void MeltAround(Vector3 at, float radius, float wet)
        {
            var p = PatchAt(at);
            if (p == null) return;
            float lx = Vector3.Dot(at - p.Origin, p.Right), lz = Vector3.Dot(at - p.Origin, p.Forward);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((lx - radius) / p.StepX)), x1 = Mathf.Min(p.Nx - 1, Mathf.CeilToInt((lx + radius) / p.StepX));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((lz - radius) / p.StepZ)), z1 = Mathf.Min(p.Nz - 1, Mathf.CeilToInt((lz + radius) / p.StepZ));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x * p.StepX - lx, dz = z * p.StepZ - lz;
                    float d = Mathf.Sqrt(dx * dx + dz * dz) / radius;
                    if (d >= 1f) continue;
                    // All of it in the middle, less towards the rim, with a wet ring round the hole.
                    float melt = Mathf.Clamp01((1f - d) * 2.5f);
                    int i = z * p.Nx + x;
                    float m = p.Snow[i] * melt;
                    p.Snow[i] -= m;
                    snowTotal -= m;
                    p.Water[i] = Mathf.Min(WaterMost, p.Water[i] + m * 0.9f + wet * (1f - d));
                }
        }

        /// <summary>
        /// The ground's own changes, a few times a second: snow melts in the rain (into water) and slowly in the snow,
        /// faster past the cap; after the snow comes the puddles freeze over and the wet dries; then each grid is drawn.
        /// </summary>
        void StepGround(float dt)
        {
            if (groundInstance == null) return;
            groundClock += dt;
            if (groundClock < 1f / GroundRate) return;
            float step = groundClock;
            groundClock = 0f;
            float over = Mathf.Max(0f, snowTotal / Mathf.Max(snowBudget, 1f) - 1f);
            // Rain melts a thick cover in about four seconds; in the snow it only creeps away (faster when past the cap).
            float melt = snowing ? (0.004f + over * 0.6f) * step : 0.6f * step;
            bool freezing = snowing && changeAge < 3f;
            float total = 0f;
            foreach (var p in patches)
            {
                if (p == null || !p.Used) continue;
                for (int i = 0; i < p.Snow.Length; i++)
                {
                    float s = p.Snow[i], w = p.Water[i];
                    if (s > 0f)
                    {
                        float m = Mathf.Min(s, snowing ? s * melt + 0.002f * step : melt);
                        s -= m;
                        if (!snowing) w += m * 0.85f;
                    }
                    if (w > 0f)
                    {
                        if (freezing)
                        {
                            // The puddles freeze over: their water turns to a white crust, the deepest first.
                            float f = Mathf.Min(w, (0.25f + p.Low[i]) * 0.6f * step);
                            w -= f;
                            s += f * (0.6f + p.Low[i]);
                        }
                        else w -= (snowing ? 0.05f : 0.004f) * step;
                    }
                    p.Snow[i] = Mathf.Clamp(s, 0f, SnowMost);
                    p.Water[i] = Mathf.Clamp(w, 0f, WaterMost);
                    total += p.Snow[i];
                }
                Settle(p);
                Upload(p);
            }
            snowTotal = total;
            groundInstance.SetFloat(RainId, 1f - Mathf.Clamp01(snowing ? changeAge / 1.5f : 1f - changeAge / 1.5f));
        }

        /// <summary>
        /// Snow evens out a little between neighbouring points, so the cover is soft rather than spotty; water runs
        /// downhill, towards the low spots, so it gathers into puddles there.
        /// </summary>
        static void Settle(Patch p)
        {
            int n = p.Nx;
            var snow = p.Snow;
            var water = p.Water;
            var low = p.Low;
            for (int z = 1; z < p.Nz - 1; z++)
                for (int x = 1; x < n - 1; x++)
                {
                    int i = z * n + x;
                    float s = snow[i];
                    snow[i] = s + ((snow[i - 1] + snow[i + 1] + snow[i - n] + snow[i + n]) * 0.25f - s) * 0.1f;
                    float w = water[i];
                    if (w <= 0f && water[i - 1] + water[i + 1] + water[i - n] + water[i + n] <= 0f) continue;
                    // The water's surface: its depth on a floor whose low spots sit lower.
                    float level = w - low[i] * WaterSink;
                    float around = (water[i - 1] - low[i - 1] * WaterSink + water[i + 1] - low[i + 1] * WaterSink
                                  + water[i - n] - low[i - n] * WaterSink + water[i + n] - low[i + n] * WaterSink) * 0.25f;
                    water[i] = Mathf.Clamp(w + (around - level) * 0.2f, 0f, WaterMost);
                }
        }

        /// <summary>Puts a grid's snow, water, low spots and edge into its texture (r, g, b, a).</summary>
        static void Upload(Patch p)
        {
            bool any = false;
            for (int i = 0; i < p.Snow.Length; i++)
            {
                float s = p.Snow[i], w = p.Water[i];
                any |= s > 0.01f || w > 0.01f;
                p.Texels[i] = new Color32((byte)(Mathf.Clamp01(s / SnowFull) * 255f), (byte)(Mathf.Clamp01(w / WaterFull) * 255f),
                    (byte)(p.Low[i] * 255f), (byte)(p.Edge[i] * 255f));
            }
            p.Renderer.enabled = any;
            if (!any) return;
            p.Texture.SetPixelData(p.Texels, 0);
            p.Texture.Apply(false, false);
        }

        void ClearGround()
        {
            snowTotal = 0f;
            foreach (var p in patches)
            {
                if (p == null) continue;
                System.Array.Clear(p.Snow, 0, p.Snow.Length);
                System.Array.Clear(p.Water, 0, p.Water.Length);
                p.Renderer.enabled = false;
            }
        }

        void DestroyGround()
        {
            foreach (var p in patches)
            {
                if (p == null) continue;
                if (p.Mesh != null) Discard(p.Mesh);
                if (p.Texture != null) Discard(p.Texture);
            }
            if (groundInstance != null) Discard(groundInstance);
        }
    }
}
