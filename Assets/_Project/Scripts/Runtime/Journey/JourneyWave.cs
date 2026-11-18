using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The light that marks a change of section in Journey mode: a soft ring of light that leaves your feet and spreads
    /// out across the floor, climbs the walls as arcs and crosses the ceiling (your scanned room, or walls a couple of
    /// metres round you without a scan), in the colour of the world that is coming, while the world changes under it. A
    /// low curtain of the same light rides the ring outward, bright at the floor and gone by waist height, so the wave
    /// reads even where furniture hides the floor. During the bar before, a faint glow gathers on the floor round your
    /// feet. Nothing lights within a metre of you and everything moves away from you, so nothing comes near your head,
    /// and it stays a thin, soft band of light rather than a flash. The finale's bloom is a wider, warmer wave with a second ring after it, and golden motes that rise
    /// slowly all round the room, never nearer than a metre.
    /// </summary>
    public sealed class JourneyWave
    {
        [System.Serializable]
        public sealed class Tuning
        {
            [Tooltip("How fast the ring spreads out across the room (m/s).")]
            public float speed = 2.2f;
            [Tooltip("How wide the ring of light is (m).")]
            public float width = 0.28f;
            [Tooltip("How bright the ring gets, 0-1. Keep it soft: a wave of light, not a flash.")]
            [Range(0f, 1f)] public float strength = 0.6f;
            [Tooltip("How tall the curtain of light riding the ring is (m); it fades well before the top.")]
            public float curtainHeight = 1.2f;
            [Tooltip("How long a wave lasts (s).")]
            public float life = 2.6f;
            [Tooltip("How much brighter and wider the finale's wave is.")]
            public float bloomBoost = 1.35f;
            [Tooltip("How many golden motes the finale sends up round the room.")]
            public int bloomMotes = 220;
            [Tooltip("Where the walls stand without a room scan: half the room's width and depth around you (m).")]
            public Vector2 standInRoom = new Vector2(2.2f, 2.4f);
        }

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int OriginId = Shader.PropertyToID("_Origin");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int WidthId = Shader.PropertyToID("_Width");
        static readonly int StrengthId = Shader.PropertyToID("_Strength");
        static readonly int GatherId = Shader.PropertyToID("_Gather");
        static readonly int BloomId = Shader.PropertyToID("_Bloom");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly Color Gold = new Color(1f, 0.82f, 0.45f);
        const float RoomHeight = 2.6f;

        readonly Tuning tuning;
        readonly Transform root;
        readonly Material material;
        readonly List<Room.Surface> faces = new List<Room.Surface>(16);
        readonly List<Renderer> quads = new List<Renderer>(16);
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly ParticleSystem motes;
        readonly MeshRenderer curtain;
        Vector3 origin;
        Color color = Color.white, gatherColor = Color.white;
        float age = 99f, gather;
        bool bloom, placed;

        /// <summary>How many waves and blooms have left so far (for the editor checks).</summary>
        public int Waves { get; private set; }
        public int Blooms { get; private set; }

        /// <summary>Seconds since the last wave left.</summary>
        public float Age => age;

        /// <summary>The finale's bloom is still spreading.</summary>
        public bool Blooming => bloom && age < tuning.life * 1.3f;

        public JourneyWave(Transform parent, Material material, Material glow, Tuning tuning)
        {
            this.tuning = tuning;
            this.material = material;
            root = new GameObject("Journey Wave").transform;
            root.SetParent(parent, false);
            curtain = MakeCurtain(root, material, tuning.curtainHeight);
            if (glow != null)
            {
                motes = Effects.MakeSparkSystem("Bloom Motes", root, glow);
                var main = motes.main;
                main.maxParticles = Mathf.Max(64, tuning.bloomMotes + 20);
            }
        }

        /// <summary>Sends a wave out from the viewer's feet, in <paramref name="tint"/>; the finale's when <paramref name="finale"/>.</summary>
        public void Launch(Camera view, Color tint, bool finale)
        {
            if (view == null) return;
            Place(view);
            color = finale ? Gold : Color.Lerp(tint, Color.white, 0.25f);
            bloom = finale;
            age = 0f;
            Waves++;
            if (!finale) return;
            Blooms++;
            EmitMotes();
        }

        /// <summary>The glow gathering at the feet before a change, 0-1, in the colour of the world that is coming.</summary>
        public void Gather(Camera view, float amount, Color tint)
        {
            amount = Mathf.Clamp01(amount);
            if (amount > 0.001f && !placed && view != null) Place(view);
            gather = amount;
            gatherColor = tint;
        }

        public void Step(float dt)
        {
            age += dt;
            float life = tuning.life * (bloom ? 1.3f : 1f);
            bool waving = age < life;
            bool show = placed && (waving || gather > 0.001f);
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show || MotesAlive);
            for (int i = 0; i < quads.Count; i++)
            {
                bool used = show && i < faces.Count;
                if (quads[i].gameObject.activeSelf != used) quads[i].gameObject.SetActive(used);
            }
            if (curtain.gameObject.activeSelf != (show && waving)) curtain.gameObject.SetActive(show && waving);
            if (!show)
            {
                if (!waving && gather <= 0.001f) placed = false;
                return;
            }

            float boost = bloom ? tuning.bloomBoost : 1f;
            // Eases out as it travels, so it leaves briskly and settles as it reaches the walls.
            float t = Mathf.Clamp01(age / life);
            float radius = 0.3f + tuning.speed * life * (1f - (1f - t) * (1f - t)) * 0.62f;
            float strength = waving ? tuning.strength * boost * Mathf.SmoothStep(0f, 1f, age / 0.2f) * Mathf.Pow(1f - t, 1.4f) : 0f;
            var tint = waving ? color : gatherColor;
            block.Clear();
            block.SetColor(ColorId, tint);
            block.SetVector(OriginId, origin);
            block.SetFloat(RadiusId, waving ? radius : 0f);
            block.SetFloat(WidthId, tuning.width * (bloom ? 1.4f : 1f));
            block.SetFloat(StrengthId, strength);
            block.SetFloat(GatherId, gather);
            block.SetFloat(BloomId, bloom ? 1f : 0f);
            for (int i = 0; i < faces.Count; i++)
            {
                block.SetVector(SizeId, faces[i].Size);
                quads[i].SetPropertyBlock(block);
            }
            if (!waving) return;
            // The curtain stands on the ring: a cylinder of its radius round the feet.
            curtain.transform.SetPositionAndRotation(origin, Quaternion.identity);
            curtain.transform.localScale = new Vector3(radius, 1f, radius);
            block.SetVector(SizeId, new Vector4(100f, tuning.curtainHeight * 4f, 0f, 0f));
            block.SetFloat(GatherId, 0f);
            curtain.SetPropertyBlock(block);
        }

        /// <summary>An open cylinder of radius 1 and the given height, standing on its centre, for the curtain.</summary>
        static MeshRenderer MakeCurtain(Transform parent, Material material, float height)
        {
            const int Segments = 72;
            var vertices = new Vector3[(Segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                var round = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i * 2] = round;
                vertices[i * 2 + 1] = round + Vector3.up * height;
                uvs[i * 2] = new Vector2(i / (float)Segments, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)Segments, 1f);
                if (i == Segments) continue;
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            var mesh = new Mesh { name = "Wave Curtain", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.bounds = new Bounds(Vector3.up * height * 0.5f, new Vector3(2f, height, 2f));
            var go = new GameObject("Wave Curtain");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return r;
        }

        /// <summary>Clears the wave and the glow at once (the journey stopped).</summary>
        public void Hide()
        {
            age = 99f;
            gather = 0f;
            placed = false;
            foreach (var quad in quads) quad.gameObject.SetActive(false);
            curtain.gameObject.SetActive(false);
            if (motes != null) motes.Clear();
            root.gameObject.SetActive(false);
        }

        /// <summary>Advances the motes while the editor is not playing (screenshots).</summary>
        public void Simulate(float dt)
        {
            if (motes != null && motes.particleCount > 0) motes.Simulate(dt, true, false);
        }

        bool MotesAlive => motes != null && motes.particleCount > 0;

        /// <summary>Lays a quad on the floor, each wall and the ceiling around the viewer, and starts the waves at their feet.</summary>
        void Place(Camera view)
        {
            var head = view.transform.position;
            float floorY = Effects.FloorY(view);
            origin = new Vector3(head.x, floorY, head.z);
            Room.Around(head, Effects.FlatForward(view), floorY, tuning.standInRoom, RoomHeight, faces, out var floor);
            faces.Add(floor);
            var above = floor.Center;
            above.y = Room.Ceiling(floorY + RoomHeight);
            faces.Add(new Room.Surface(above, Vector3.down, floor.Right, floor.Size));
            while (quads.Count < faces.Count)
            {
                var quad = Effects.MakeQuad($"Wave {quads.Count}", root, material);
                quad.gameObject.SetActive(false);
                quads.Add(quad);
            }
            for (int i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                quads[i].transform.SetPositionAndRotation(face.Center + face.Normal * 0.012f, Quaternion.LookRotation(-face.Normal, face.Up));
                quads[i].transform.localScale = new Vector3(face.Size.x, face.Size.y, 1f);
            }
            placed = true;
            root.gameObject.SetActive(true);
        }

        /// <summary>Golden motes all round the room, a metre to two and a half away, rising slowly and fading.</summary>
        void EmitMotes()
        {
            if (motes == null) return;
            if (!motes.isPlaying && Application.isPlaying) motes.Play();
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < tuning.bloomMotes; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(1.1f, 2.4f);
                p.position = origin + new Vector3(Mathf.Cos(angle) * distance, Random.Range(0.1f, 1.5f), Mathf.Sin(angle) * distance);
                p.velocity = new Vector3(0f, Random.Range(0.06f, 0.2f), 0f);
                p.startColor = Color.Lerp(Gold, Color.white, Random.value * 0.45f);
                p.startSize = Random.Range(0.035f, 0.075f);
                p.startLifetime = Random.Range(2.5f, 4.5f);
                motes.Emit(p, 1);
            }
        }
    }
}
