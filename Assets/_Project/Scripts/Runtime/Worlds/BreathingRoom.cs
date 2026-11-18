using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Breathing room world: the room itself moves with the music. A faint glowing grid traces the walls and the
    /// floor around you; the walls breathe in and out with the bass and brighten on every kick, and rings on the floor
    /// flow in towards your feet. Push your palm towards a wall (or touch it, if you walk up to it) and a wave rolls
    /// out across it from that spot with a low note. With a room scan the grid traces your real walls and floor;
    /// until then the walls stand a couple of metres around where you started.
    /// </summary>
    public class BreathingRoom : World
    {
        public override string DisplayName => "Breathing room";

        public override Color Color => new Color(0.75f, 0.6f, 1f);

        public override int MenuOrder => 100;

        public override int MenuIcon => 16;

        public override string Hint => "Push your palm towards a wall";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.14f, 0.1f, 0.26f, 0.5f),
            Edge = new Color(0.62f, 0.56f, 1f),
            Glow = new Color(1f, 0.45f, 0.75f),
            Pattern = HandTheme.Effect.Ripples,
            PatternColor = new Color(0.75f, 0.6f, 1f)
        };

        [Header("Materials")]
        [WorldMaterial("RoomGrid")]
        [SerializeField] Material gridMaterial;

        [Header("Room")]
        [Tooltip("Half the width and depth of the stand-in room (m), and its height.")]
        [SerializeField] Vector2 halfSize = new Vector2(1.8f, 1.8f);
        [SerializeField] float roomHeight = 2.6f;
        [Tooltip("How far the walls breathe with the bass (share of their distance).")]
        [SerializeField] float breathe = 0.04f;

        [Header("Touch")]
        [Tooltip("Palm speed towards a wall (m/s) that sends a wave.")]
        [SerializeField] float pushSpeed = 0.7f;

        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int FloorId = Shader.PropertyToID("_Floor");
        static readonly int CenterId = Shader.PropertyToID("_Center");
        static readonly int BreathId = Shader.PropertyToID("_Breath");
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int WavesId = Shader.PropertyToID("_Waves");

        readonly List<Renderer> walls = new List<Renderer>();
        // The walls at rest, facing into the room: the scanned walls or four stand-ins.
        readonly List<Room.Surface> surfaces = new List<Room.Surface>();
        Renderer floor;
        Room.Surface floorRect;
        readonly Vector4[] waves = new Vector4[8];
        int nextWave;
        readonly float[] cooldown = new float[2];
        MaterialPropertyBlock block;
        AudioPool voices;
        MusicFrame frame;
        Vector3 centre, forward, right;
        float floorY, breath;
        bool scanned;

        protected override float FadeInSeconds => 1f / 1.2f;

        protected override int SungWordCount => 6;

        protected override float SungWordHeight => 0.11f;

        protected override float SungWordsAlpha => FadeIn;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            floor = Effects.MakeQuad("Floor", transform, gridMaterial);
            floor.gameObject.SetActive(false);
            voices = new AudioPool(transform, 4);
            for (int i = 0; i < waves.Length; i++) waves[i] = new Vector4(0f, 0f, 0f, -1f);
        }

        protected override void OnExit()
        {
            foreach (var w in walls) w.gameObject.SetActive(false);
            floor.gameObject.SetActive(false);
        }

        /// <summary>Finds the walls and floor around where you stand and shows the grid on them.</summary>
        protected override void OnPlace(Camera view)
        {
            centre = view.transform.position;
            forward = Effects.FlatForward(view);
            right = Vector3.Cross(Vector3.up, forward).normalized;
            floorY = Effects.FloorY(view);
            MeasureRoom();
            for (int i = 0; i < walls.Count; i++) walls[i].gameObject.SetActive(i < surfaces.Count);
            floor.gameObject.SetActive(true);
        }

        /// <summary>Each kick sends a soft ring out from under your feet.</summary>
        protected override void OnBeat(MusicFrame music) => AddWave(new Vector3(centre.x, floorY, centre.z), false);

        /// <summary>
        /// Each sung word lights up on the wall in front of you, a little further along for each word of the line,
        /// and sends a wave out across the wall from where it lands.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            var eye = View.transform.position;
            float angle = ((Sung.Index % 5) - 2) * 11f;
            var dir = Quaternion.AngleAxis(angle, Vector3.up) * Effects.FlatForward(View);
            if (!Hit(eye + Vector3.down * 0.1f, dir, out var hit, out float distance)) return;
            var wall = surfaces[0];
            foreach (var su in surfaces)
                if (Mathf.Abs(Vector3.Dot(hit - su.Center, su.Normal)) < Mathf.Abs(Vector3.Dot(hit - wall.Center, wall.Normal))) wall = su;
            var w = SungWords.Add(word, Color.Lerp(new Color(1f, 0.45f, 0.75f), Color.white, 0.35f), 3.2f);
            w.Position = hit + wall.Normal * 0.02f;
            w.Rotation = Quaternion.LookRotation(-wall.Normal, Vector3.up);
            w.Size = Mathf.Clamp(distance / 1.8f, 0.6f, 1.8f);
            AddWave(hit, false);
        }

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            frame = music;
            breath = Mathf.Lerp(breath, Mathf.Clamp01(music.Band(1) * 0.8f + music.Kick * 0.5f), 1f - Mathf.Exp(-8f * dt));

            for (int i = 0; i < waves.Length; i++) if (waves[i].w >= 0f) waves[i].w += dt;
            if (rig != null) Push(rig, dt);
            Place(music);
            if (SungWords == null) return;
            foreach (var w in SungWords.Words)
                if (w.Active) w.Halo = 0.3f + breath * 0.5f;
        }

        /// <summary>
        /// Finds the walls: the scanned ones, or four stand-ins around you (front, right, back, left). The floor
        /// grid covers the space between them.
        /// </summary>
        void MeasureRoom()
        {
            scanned = Room.Around(centre, forward, floorY, halfSize, roomHeight, surfaces, out floorRect);
            while (walls.Count < surfaces.Count)
            {
                var quad = Effects.MakeQuad($"Wall {walls.Count}", transform, gridMaterial);
                quad.gameObject.SetActive(false);
                walls.Add(quad);
            }
        }

        /// <summary>The walls stand where they are, pushed out a little with each breath.</summary>
        void Place(MusicFrame music)
        {
            float s = 1f + breathe * breath;
            for (int i = 0; i < surfaces.Count; i++)
            {
                var surface = surfaces[i];
                // Along the wall's normal, by a share of its distance from where you stand: stand-in walls breathe out,
                // real walls breathe in from their surface (they cannot go behind it).
                float away = Mathf.Max(0.3f, Vector3.Dot(centre - surface.Center, surface.Normal));
                float shift = scanned ? 0.01f + away * breathe * breath : -away * breathe * breath;
                var size = new Vector2(surface.Size.x * s, surface.Size.y);
                var w = walls[i];
                w.transform.SetPositionAndRotation(surface.Center + surface.Normal * shift, Quaternion.LookRotation(-surface.Normal, Vector3.up));
                w.transform.localScale = new Vector3(size.x, size.y, 1f);
                Apply(w, size, false);
            }
            var floorSize = floorRect.Size * s;
            floor.transform.SetPositionAndRotation(floorRect.Center + Vector3.up * 0.003f, Quaternion.LookRotation(Vector3.down, floorRect.Up));
            floor.transform.localScale = new Vector3(floorSize.x, floorSize.y, 1f);
            Apply(floor, floorSize, true);
        }

        void Apply(Renderer r, Vector2 size, bool isFloor)
        {
            block.Clear();
            block.SetVector(SizeId, size);
            block.SetFloat(FloorId, isFloor ? 1f : 0f);
            block.SetVector(CenterId, centre);
            block.SetFloat(BreathId, breath);
            block.SetFloat(ClockId, Now);
            block.SetFloat(AlphaId, FadeIn);
            block.SetVectorArray(WavesId, waves);
            r.SetPropertyBlock(block);
        }

        /// <summary>
        /// A palm pushed quickly towards a wall, or touching it, sends a wave out across the wall from where it points.
        /// </summary>
        void Push(HandRig rig, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                cooldown[side] -= dt;
                var hand = rig.Get(side == 0);
                // A palm pressing at the open menu isn't pushing a wall.
                if (!hand.IsTracked || MenuOpen || cooldown[side] > 0f || hand.PalmNormal.sqrMagnitude < 0.5f) continue;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                var normal = hand.PalmNormal;
                if (Mathf.Abs(normal.y) > 0.7f) continue;
                float towards = Vector3.Dot(hand.Velocity, normal);
                bool pushing = towards > pushSpeed;
                if (!Hit(palm, normal, out var hit, out float distance)) continue;
                if (!pushing && distance > 0.12f) continue;
                AddWave(hit, true);
                cooldown[side] = 0.45f;
                // The pushing hand lights up; the wave's note is the sound.
                Feedback(GestureKind.Touch, side == 0, palm, 1f, false, Fingers.All);
                Acted("wall wave");
            }
        }

        /// <summary>Where a ray from the palm meets a wall.</summary>
        bool Hit(Vector3 from, Vector3 direction, out Vector3 hit, out float distance) =>
            Room.Hit(surfaces, from, direction, out hit, out distance);

        void AddWave(Vector3 at, bool pressed)
        {
            waves[nextWave] = new Vector4(at.x, at.y, at.z, 0f);
            nextWave = (nextWave + 1) % waves.Length;
            if (pressed) Musical.Play(voices, frame, 57, at, 0.5f);
        }
    }
}
