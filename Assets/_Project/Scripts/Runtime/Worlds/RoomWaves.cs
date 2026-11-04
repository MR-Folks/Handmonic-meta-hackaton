using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Living skin's room: the waves that leave your palms travel across the room and land on whatever is in their way,
    /// wall, floor or ceiling, spreading there as a ring of their colour with veins lit behind it (LivingWalls shader).
    /// The veins also wake a little on the bass. With a room scan the waves land on your real walls; until then the
    /// walls stand a couple of metres around where you started.
    /// </summary>
    public sealed class LivingWalls
    {
        const int MaxWaves = 8;
        // How fast a wave crosses the room before it lands (m/s).
        const float TravelSpeed = 4f;

        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int WavesId = Shader.PropertyToID("_Waves");
        static readonly int WaveColorsId = Shader.PropertyToID("_WaveColors");

        readonly Transform parent;
        readonly Material material;
        readonly Vector2 halfSize;
        readonly float height;
        readonly List<Room.Surface> faces = new List<Room.Surface>();
        readonly List<Renderer> quads = new List<Renderer>();
        readonly Vector4[] waves = new Vector4[MaxWaves];
        readonly Vector4[] colors = new Vector4[MaxWaves];
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        int nextWave;
        float pulse, fade;

        public LivingWalls(Transform parent, Material material, Vector2 halfSize, float height)
        {
            this.parent = parent;
            this.material = material;
            this.halfSize = halfSize;
            this.height = height;
            Forget();
        }

        /// <summary>Finds the room around <paramref name="centre"/> and lays a quad on each wall, the floor and the ceiling.</summary>
        public void Place(Vector3 centre, Vector3 forward, float floorY)
        {
            Room.Around(centre, forward, floorY, halfSize, height, faces, out var floor);
            faces.Add(floor);
            var above = floor.Center;
            above.y = Room.Ceiling(floorY + height);
            faces.Add(new Room.Surface(above, Vector3.down, floor.Right, floor.Size));

            while (quads.Count < faces.Count) quads.Add(Effects.MakeQuad($"Wall {quads.Count}", parent, material));
            for (int i = 0; i < quads.Count; i++)
            {
                bool used = i < faces.Count;
                quads[i].gameObject.SetActive(used);
                if (!used) continue;
                var face = faces[i];
                quads[i].transform.SetPositionAndRotation(face.Center + face.Normal * 0.01f, Quaternion.LookRotation(-face.Normal, face.Up));
                quads[i].transform.localScale = new Vector3(face.Size.x, face.Size.y, 1f);
            }
        }

        /// <summary>Sends a wave from <paramref name="from"/>; it lands where it first meets the room.</summary>
        public void Send(Vector3 from, Vector3 direction, Color color, float strength)
        {
            if (!Room.Hit(faces, from, direction, out var hit, out float distance)) return;
            waves[nextWave] = new Vector4(hit.x, hit.y, hit.z, -distance / TravelSpeed);
            colors[nextWave] = new Vector4(color.r, color.g, color.b, strength);
            nextWave = (nextWave + 1) % MaxWaves;
        }

        /// <summary>The first wall a ray from <paramref name="from"/> meets, and where.</summary>
        public bool Aim(Vector3 from, Vector3 direction, out Room.Surface wall, out Vector3 hit, out float distance)
        {
            wall = default;
            if (!Room.Hit(faces, from, direction, out hit, out distance)) return false;
            float nearest = float.MaxValue;
            foreach (var face in faces)
            {
                float off = Mathf.Abs(Vector3.Dot(hit - face.Center, face.Normal));
                if (off < nearest) { nearest = off; wall = face; }
            }
            return true;
        }

        public void Step(MusicFrame music, float dt)
        {
            fade = Mathf.MoveTowards(fade, 1f, dt * 1.2f);
            pulse = Mathf.Lerp(pulse, Mathf.Clamp01(music.Band(1) * 0.6f + music.Kick * 0.5f), 1f - Mathf.Exp(-8f * dt));
            for (int i = 0; i < MaxWaves; i++) waves[i].w += dt;

            block.Clear();
            block.SetFloat(PulseId, pulse);
            block.SetFloat(AlphaId, fade);
            block.SetVectorArray(WavesId, waves);
            block.SetVectorArray(WaveColorsId, colors);
            for (int i = 0; i < faces.Count; i++)
            {
                block.SetVector(SizeId, faces[i].Size);
                quads[i].SetPropertyBlock(block);
            }
        }

        public void Hide()
        {
            foreach (var quad in quads) quad.gameObject.SetActive(false);
            faces.Clear();
            Forget();
        }

        void Forget()
        {
            fade = 0f;
            for (int i = 0; i < MaxWaves; i++) waves[i] = new Vector4(0f, 0f, 0f, 99f);
        }
    }
}
