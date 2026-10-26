using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Breathing room world: the room itself moves with the music. A faint glowing grid traces four walls and the
    /// floor around you; the walls breathe in and out with the bass and brighten on every kick, and rings on the floor
    /// flow in towards your feet. Push your palm towards a wall (or touch it, if you walk up to it) and a wave rolls
    /// out across it from that spot with a low note. Until the room is scanned, the walls stand a couple of metres
    /// around where you started.
    /// </summary>
    public class BreathingRoom : World
    {
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

        Renderer[] walls;
        Renderer floor;
        readonly Vector4[] waves = new Vector4[8];
        int nextWave;
        readonly float[] cooldown = new float[2];
        MaterialPropertyBlock block;
        AudioPool voices;
        MusicFrame frame;
        Vector3 centre, forward, right;
        float floorY, fade, breath;
        bool anchored, built;

        public override void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();
            walls = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                walls[i] = Effects.MakeQuad($"Wall {i}", transform, gridMaterial);
                walls[i].gameObject.SetActive(false);
            }
            floor = Effects.MakeQuad("Floor", transform, gridMaterial);
            floor.gameObject.SetActive(false);
            voices = new AudioPool(transform, 4);
            for (int i = 0; i < waves.Length; i++) waves[i] = new Vector4(0f, 0f, 0f, -1f);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            anchored = false;
            fade = 0f;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (!built) return;
            foreach (var w in walls) w.gameObject.SetActive(false);
            floor.gameObject.SetActive(false);
        }

        public override void Step(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            Build();
            if (view == null) return;
            if (anchored && ShouldReanchor(view, dt)) anchored = false;
            if (!anchored)
            {
                centre = view.transform.position;
                forward = Effects.FlatForward(view);
                right = Vector3.Cross(Vector3.up, forward).normalized;
                floorY = Effects.FloorY(view);
                foreach (var w in walls) w.gameObject.SetActive(true);
                floor.gameObject.SetActive(true);
                MarkAnchored(view);
                anchored = true;
            }
            frame = music;
            fade = Mathf.MoveTowards(fade, 1f, dt * 1.2f);
            breath = Mathf.Lerp(breath, Mathf.Clamp01(music.Band(1) * 0.8f + music.Kick * 0.5f), 1f - Mathf.Exp(-8f * dt));

            for (int i = 0; i < waves.Length; i++) if (waves[i].w >= 0f) waves[i].w += dt;
            // Each kick sends a soft ring out from under your feet.
            if (music.Beat) AddWave(new Vector3(centre.x, floorY, centre.z), false);
            if (rig != null) Push(rig, dt);
            Place(music);
        }

        /// <summary>The walls stand where they are, pushed out a little with each breath.</summary>
        void Place(MusicFrame music)
        {
            float s = 1f + breathe * breath;
            var middle = new Vector3(centre.x, floorY + roomHeight * 0.5f, centre.z);
            for (int i = 0; i < 4; i++)
            {
                // Front, right, back, left.
                var normal = Quaternion.AngleAxis(90f * i, Vector3.up) * forward;
                float reach = (i % 2 == 0 ? halfSize.y : halfSize.x) * s;
                float span = (i % 2 == 0 ? halfSize.x : halfSize.y) * 2f * s;
                var w = walls[i];
                w.transform.SetPositionAndRotation(middle + normal * reach, Quaternion.LookRotation(normal, Vector3.up));
                w.transform.localScale = new Vector3(span, roomHeight, 1f);
                Apply(w, new Vector2(span, roomHeight), false);
            }
            floor.transform.SetPositionAndRotation(new Vector3(centre.x, floorY + 0.003f, centre.z), Quaternion.LookRotation(Vector3.down, forward));
            floor.transform.localScale = new Vector3(halfSize.x * 2f * s, halfSize.y * 2f * s, 1f);
            Apply(floor, new Vector2(halfSize.x * 2f * s, halfSize.y * 2f * s), true);
        }

        void Apply(Renderer r, Vector2 size, bool isFloor)
        {
            block.Clear();
            block.SetVector(SizeId, size);
            block.SetFloat(FloorId, isFloor ? 1f : 0f);
            block.SetVector(CenterId, centre);
            block.SetFloat(BreathId, breath);
            block.SetFloat(ClockId, Now);
            block.SetFloat(AlphaId, fade);
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
                if (!hand.IsTracked || cooldown[side] > 0f || hand.PalmNormal.sqrMagnitude < 0.5f) continue;
                var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                var normal = hand.PalmNormal;
                if (Mathf.Abs(normal.y) > 0.7f) continue;
                float towards = Vector3.Dot(hand.Velocity, normal);
                bool pushing = towards > pushSpeed;
                if (!Hit(palm, normal, out var hit, out float distance)) continue;
                if (!pushing && distance > 0.12f) continue;
                AddWave(hit, true);
                cooldown[side] = 0.45f;
            }
        }

        /// <summary>Where a ray from the palm meets the stand-in walls.</summary>
        bool Hit(Vector3 from, Vector3 direction, out Vector3 hit, out float distance)
        {
            hit = Vector3.zero;
            distance = float.MaxValue;
            float s = 1f + breathe * breath;
            for (int i = 0; i < 4; i++)
            {
                var normal = Quaternion.AngleAxis(90f * i, Vector3.up) * forward;
                float reach = (i % 2 == 0 ? halfSize.y : halfSize.x) * s;
                var plane = new Plane(-normal, centre + normal * reach);
                if (!plane.Raycast(new Ray(from, direction), out float enter) || enter > distance) continue;
                distance = enter;
                hit = from + direction * enter;
            }
            return distance < float.MaxValue && hit.y > floorY && hit.y < floorY + roomHeight;
        }

        void AddWave(Vector3 at, bool pressed)
        {
            waves[nextWave] = new Vector4(at.x, at.y, at.z, 0f);
            nextWave = (nextWave + 1) % waves.Length;
            if (pressed) Musical.Play(voices, frame, 57, at, 0.5f);
        }
    }
}
