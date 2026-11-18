using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The Lightning world: the room dims like a storm is coming, and your hands are charged. Small sparks crackle off
    /// your fingertips, more when the music is loud. Close a fist and lightning strikes from the ceiling to the floor in
    /// front of you, with a flash that lights the room, a ring burnt into the floor and thunder. Pinch and a bolt leaps
    /// from your pinch to where you point, ringing a note where it lands; keep pinching and it strikes again on every
    /// beat. All the while it rains or it snows, one at a time, with the sung words falling among the drops or flakes;
    /// hold a fist for a couple of seconds to turn the rain to snow or the snow to rain (see Lightning.Weather.cs).
    /// </summary>
    public partial class Lightning : World
    {
        public override string DisplayName => "Lightning";

        public override Color Color => new Color(0.62f, 0.75f, 1f);

        public override int MenuOrder => 112;

        public override int MenuIcon => 29;

        public override string Hint => "Fist for thunder, hold it to switch snow and rain; pinch and point to zap";

        public override HandTheme Theme => new HandTheme
        {
            Fill = new Color(0.06f, 0.08f, 0.16f, 0.55f),
            Edge = new Color(0.6f, 0.75f, 1f),
            Glow = new Color(0.75f, 0.65f, 1f),
            Pattern = HandTheme.Effect.Electric,
            PatternColor = new Color(0.8f, 0.9f, 1f)
        };

        [Header("Materials")]
        [SerializeField] Material lineMaterial;
        [SerializeField] Material ringMaterial;
        [SerializeField] Material veilMaterial;
        [Tooltip("The falling snow and rain, the flakes that have settled and the drops' ripples (Materials/LightningWeather).")]
        [SerializeField, WorldMaterial("LightningWeather")] Material weatherMaterial;
        [Tooltip("The snow and the water on the floor and the furniture (Materials/LightningGround).")]
        [SerializeField, WorldMaterial("LightningGround")] Material groundMaterial;

        [Header("Storm")]
        [Tooltip("How dark the room gets (0 not at all, 1 black).")]
        [SerializeField, Range(0f, 1f)] float darkness = 0.45f;
        [Tooltip("How far in front of you the fist's lightning strikes (m).")]
        [SerializeField] float strikeDistance = 1.6f;
        [Tooltip("The longest a pinch's bolt reaches when it meets nothing (m).")]
        [SerializeField] float reach = 4f;

        static readonly Color StrikeColor = new Color(0.78f, 0.86f, 1f);
        static readonly Color ZapColor = new Color(0.75f, 0.62f, 1f);
        static readonly Color SparkColor = new Color(0.62f, 0.78f, 1f);
        // In the snow the bolts turn a deep ice blue and are laid over what is behind them instead of adding light,
        // so they still show against the white ground.
        static readonly Color SnowStrikeColor = new Color(0.16f, 0.42f, 1f);
        static readonly Color SnowZapColor = new Color(0.3f, 0.34f, 1f);
        static readonly Color SnowSparkColor = new Color(0.24f, 0.52f, 1f);
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int CoreId = Shader.PropertyToID("_Core");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        Color StrikeTint => snowing ? SnowStrikeColor : StrikeColor;
        Color ZapTint => snowing ? SnowZapColor : ZapColor;
        Color SparkTint => snowing ? SnowSparkColor : SparkColor;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int DarkId = Shader.PropertyToID("_Dark");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int WallOnId = Shader.PropertyToID("_WallOn");

        const int MaxBolts = 48;
        const int MaxPoints = 65;

        sealed class Bolt
        {
            public LineStrip Line;
            public readonly Vector3[] Points = new Vector3[MaxPoints];
            public int Count, Depth;
            public Vector3 From, To;
            public float Life, Max, Width, Jag, Rejag, Brightness;
            public Color Color;
            public bool Active, Snow;
        }

        sealed class Mark
        {
            public Renderer Ring, Glow;
            public Vector3 Position, Normal;
            public float Age = 99f, Life, Size;
            public Color Color;
        }

        readonly Bolt[] bolts = new Bolt[MaxBolts];
        readonly Mark[] marks = new Mark[8];
        readonly Renderer[] tipLights = new Renderer[10];
        readonly bool[] wasFist = new bool[2];
        readonly float[] charge = new float[2];
        static readonly Vector3[] scratchA = new Vector3[MaxPoints], scratchB = new Vector3[MaxPoints];
        Renderer veil;
        Material veilInstance, snowLine;
        MaterialPropertyBlock block;
        ParticleSystem sparks;
        AudioPool voices;
        float flash, cooldown;
        int nextMark;

        // The sung words fall with the weather (OnWord in Lightning.Weather.cs).
        protected override int SungWordCount => 24;

        protected override float SungWordHeight => 0.05f;

        protected override void OnBuild()
        {
            block = new MaterialPropertyBlock();
            for (int i = 0; i < MaxBolts; i++)
            {
                bolts[i] = new Bolt { Line = new LineStrip($"Bolt {i}", transform, lineMaterial, MaxPoints) };
                bolts[i].Line.SetVisible(false);
            }
            if (lineMaterial != null)
            {
                snowLine = new Material(lineMaterial) { name = "Bolt in Snow" };
                snowLine.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                snowLine.SetFloat(CoreId, 0.3f);
            }
            for (int i = 0; i < marks.Length; i++)
            {
                marks[i] = new Mark { Ring = Effects.MakeQuad($"Burn Ring {i}", transform, ringMaterial), Glow = Effects.MakeQuad($"Burn Glow {i}", transform, glowMaterial) };
                marks[i].Ring.gameObject.SetActive(false);
                marks[i].Glow.gameObject.SetActive(false);
            }
            for (int i = 0; i < tipLights.Length; i++)
            {
                tipLights[i] = Effects.MakeQuad($"Fingertip {i}", transform, glowMaterial);
                tipLights[i].gameObject.SetActive(false);
            }
            if (veilMaterial != null)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Storm Dark";
                DebugShapes.RemoveCollider(sphere);
                sphere.transform.SetParent(transform, false);
                veil = sphere.GetComponent<Renderer>();
                veilInstance = new Material(veilMaterial) { name = "Storm Veil" };
                veilInstance.SetFloat(WallOnId, 0f);
                veil.sharedMaterial = veilInstance;
                veil.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                veil.receiveShadows = false;
            }
            sparks = Effects.MakeSparkSystem("Bolt Sparks", transform, glowMaterial);
            var main = sparks.main;
            main.gravityModifier = 0.25f;
            voices = new AudioPool(transform, 10);
            BuildWeather();
        }

        protected override void OnEnter()
        {
            flash = 0f;
            wasFist[0] = wasFist[1] = false;
            EnterWeather();
        }

        protected override void OnExit()
        {
            HandSkin.Charge[0] = HandSkin.Charge[1] = 0f;
            foreach (var b in bolts) { b.Active = false; b.Line.SetVisible(false); }
            foreach (var m in marks) { m.Age = 99f; m.Ring.gameObject.SetActive(false); m.Glow.gameObject.SetActive(false); }
            foreach (var t in tipLights) t.gameObject.SetActive(false);
            ExitWeather();
        }

        void OnDestroy()
        {
            if (veilInstance != null) Discard(veilInstance);
            DestroyWeather();
        }

        /// <summary>The snow and the puddles are laid out on the floor around you, and on the furniture once the room is scanned.</summary>
        protected override void OnPlace(Camera view) => PlaceGround(view);

        /// <summary>A pinch zaps where you point.</summary>
        protected override void OnPinch(HandRig.Hand hand, bool left)
        {
            if (hand.Fist > 0.8f) return;
            Zap(PinchPoint(hand), left ? 0 : 1, Music, View);
            // The pinching fingers light up with a burst; the zap is the sound.
            Feedback(GestureKind.Pinch, left, PinchPoint(hand), 1f, false);
            Acted("zap");
        }

        /// <summary>A pinch made inside a fist zaps nothing, so OnPinch shows its own feedback.</summary>
        protected override bool AutoPinchFeedback => false;

        /// <summary>A pinch held zaps again on every beat.</summary>
        protected override void OnBeat(MusicFrame music)
        {
            if (Rig == null) return;
            for (int side = 0; side < 2; side++)
            {
                var hand = Rig.Get(side == 0);
                if (!IsPinching(side == 0) || hand.Fist > 0.8f) continue;
                Zap(PinchPoint(hand), side, music, View);
                Acted("zap on the beat");
            }
        }

        static Vector3 PinchPoint(HandRig.Hand hand) => (Tip(hand, 0) + Tip(hand, 1)) * 0.5f;

        protected override void OnStep(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            cooldown -= dt;
            float energy = Mathf.Clamp01((music.Band(1) + music.Band(3) + music.Band(5)) / 3f * 0.8f + music.Kick * 0.4f);
            if (rig != null)
                for (int side = 0; side < 2; side++) Hand(rig.Get(side == 0), side, music, view, energy, dt);
            else
            {
                foreach (var t in tipLights) t.gameObject.SetActive(false);
                for (int side = 0; side < 2; side++) HideCharge(side);
            }
            if (previewStorm) PreviewNow(rig, music, view);

            UpdateBolts(view, dt);
            UpdateMarks(music, view, dt);
            flash = Mathf.Max(0f, flash - dt * 3f);
            StepWeather(rig, music, view, dt);
            DrawVeil(view, music);
        }

        void Hand(HandRig.Hand hand, int side, MusicFrame music, Camera view, float energy, float dt)
        {
            bool tracked = hand.IsTracked && !hand.InSystemGesture;
            bool fist = tracked && hand.Fist > 0.8f;
            bool open = tracked && hand.Fist < 0.4f && hand.Pinch < 0.6f;

            // Closing a fist calls the big strike, but not while the hands are choosing in the menu; a fist still closed
            // when the menu goes has to open first. The fingertips crackle either way. A fist kept closed strikes once,
            // then builds up a change of weather (Hold).
            if (fist && !wasFist[side] && cooldown <= 0f && !MenuOpen)
            {
                Strike(hand, music, view);
                cooldown = 0.45f;
                // The fist lights up with a burst; the thunder is the sound.
                Feedback(GestureKind.Fist, side == 0, HandFistPoint(hand), 1f, false);
                Acted("strike");
            }
            wasFist[side] = fist || (wasFist[side] && tracked && hand.Fist > 0.5f);
            Hold(hand, side, tracked, music, view, dt);

            // Crackling fingertips: more sparks the louder the music.
            charge[side] = Mathf.MoveTowards(charge[side], open ? 0.3f + energy * 0.7f : fist ? 1f : 0.2f, dt * 3f);
            HandSkin.Charge[side] = tracked ? charge[side] : 0f;
            for (int f = 0; f < 5; f++)
            {
                var light = tipLights[side * 5 + f];
                if (light.gameObject.activeSelf != open) light.gameObject.SetActive(open);
                if (!open) continue;
                var tip = Tip(hand, f);
                light.transform.position = tip;
                light.transform.localScale = Vector3.one * (0.012f + energy * 0.012f) * Random.Range(0.8f, 1.2f);
                Effects.Face(light.transform, view);
                block.Clear();
                block.SetColor(ColorId, SparkTint);
                block.SetFloat(IntensityId, 0.5f + energy * 0.8f);
                light.SetPropertyBlock(block);

                if (Random.value < dt * (3f + energy * 22f))
                {
                    var outward = (tip - hand.Palm.position).normalized;
                    var dir = (outward + Random.insideUnitSphere * 0.9f).normalized;
                    float length = Random.Range(0.025f, 0.06f) * (0.7f + energy * 0.6f);
                    Spawn(tip, tip + dir * length, 3, length * 0.5f, 0.006f, Random.Range(0.05f, 0.1f), SparkTint,1.2f);
                }
                // Now and then an arc jumps to the next finger.
                if (f < 4 && Random.value < dt * energy * 4f)
                {
                    var next = Tip(hand, f + 1);
                    Spawn(tip, next, 3, Vector3.Distance(tip, next) * 0.4f, 0.005f, 0.08f, SparkTint,1f);
                }
            }
        }

        static Vector3 Tip(HandRig.Hand hand, int finger) =>
            hand.TipJoints[finger] != null ? hand.TipJoints[finger].position : hand.Tips[finger];

        /// <summary>
        /// The fist's strike: from the ceiling to the floor a little way in front of you, in the direction of the fist
        /// (kept inside the room's walls), with branches, a flash, a burn ring and thunder. In the snow it melts a round
        /// hole into the settled snow; in the rain it throws up a big splash.
        /// </summary>
        void Strike(HandRig.Hand hand, MusicFrame music, Camera view)
        {
            var head = view.transform.position;
            var dir = Vector3.ProjectOnPlane(hand.Palm.position - head, Vector3.up);
            if (dir.sqrMagnitude < 0.01f) dir = Effects.FlatForward(view);
            dir.Normalize();
            float distance = strikeDistance;
            if (Room.RaycastWalls(new Ray(head, dir), strikeDistance + 0.4f, out float wall, out _)) distance = Mathf.Max(0.6f, wall - 0.4f);
            float floor = Effects.FloorY(view);
            float ceiling = Room.Ceiling(Mathf.Max(head.y + 0.9f, floor + 2.4f));
            var ground = new Vector3(head.x, floor, head.z) + dir * distance;
            var top = new Vector3(ground.x, ceiling, ground.z) + Random.insideUnitSphere.normalized * 0.25f;
            top.y = ceiling;

            var main = Spawn(top, ground, 6, 0.4f, 0.05f, 0.55f, StrikeTint,2.2f, rejag: 0.12f);
            for (int i = 0; i < 7; i++)
            {
                var from = main.Points[Random.Range(main.Count / 6, main.Count * 3 / 4)];
                var branch = (Vector3.down * 0.8f + Random.insideUnitSphere).normalized;
                float length = Random.Range(0.3f, 0.8f);
                Spawn(from, from + branch * length, 4, length * 0.35f, 0.018f, 0.45f, StrikeTint,1.4f, rejag: 0.15f);
            }
            flash = 1f;
            AddMark(ground + Vector3.up * 0.005f, Vector3.up, 1.1f, StrikeTint,2.5f);
            Burst(ground + Vector3.up * 0.05f, 60, StrikeTint,1.2f);
            WeatherImpact(ground, Vector3.up, 0.5f, true);
            voices.Play(Thunder(), ground, 1f);
            // The song's note underneath, low, so the thunder belongs to the music.
            Musical.Play(voices, music, 45, ground, 0.5f);
        }

        /// <summary>
        /// A bolt from the pinch to where you point: along a ray from your shoulder through the pinch, to the first
        /// wall, floor or ceiling it meets, or <see cref="reach"/> metres away. It rings a note where it lands, higher up
        /// the wall for higher notes, and on the floor it melts a little snow or splashes the water.
        /// </summary>
        void Zap(Vector3 from, int side, MusicFrame music, Camera view)
        {
            var head = view.transform.position;
            var across = Vector3.Cross(Vector3.up, Effects.FlatForward(view)).normalized;
            var shoulder = head + Vector3.down * 0.17f + across * (side == 0 ? -0.17f : 0.17f);
            var dir = (from - shoulder).normalized;
            float distance = reach;
            var normal = -dir;
            bool hit = false;
            if (Room.RaycastWalls(new Ray(from, dir), distance, out float wall, out var surface)) { distance = wall; normal = surface.Normal; hit = true; }
            float floor = Effects.FloorY(view);
            if (dir.y < -0.05f)
            {
                float t = (floor - from.y) / dir.y;
                if (t < distance) { distance = t; normal = Vector3.up; hit = true; }
            }
            float ceiling = Room.Ceiling(Mathf.Max(head.y + 0.9f, floor + 2.4f));
            if (dir.y > 0.05f)
            {
                float t = (ceiling - from.y) / dir.y;
                if (t < distance) { distance = t; normal = Vector3.down; hit = true; }
            }
            var to = from + dir * distance;
            var start = from + dir * 0.02f;

            var main = Spawn(start, to, 5, distance * 0.07f, 0.022f, 0.3f, ZapTint,1.8f, rejag: 0.08f);
            for (int i = 0; i < 3; i++)
            {
                var at = main.Points[Random.Range(main.Count / 4, main.Count - 2)];
                var branch = (dir + Random.insideUnitSphere * 1.2f).normalized;
                float length = Random.Range(0.08f, 0.25f) * Mathf.Min(distance, 2f);
                Spawn(at, at + branch * length, 3, length * 0.4f, 0.01f, 0.22f, ZapTint,1.1f);
            }
            if (hit) AddMark(to + normal * 0.005f, normal, 0.25f, ZapTint,1.2f);
            if (hit && normal.y > 0.7f) WeatherImpact(to, normal, 0.14f, false);
            Burst(to + normal * 0.03f, 18, ZapTint,0.5f);
            flash = Mathf.Max(flash, 0.25f);
            voices.Play(ZapSound(), to, 0.5f);
            // Higher on the wall, higher the note.
            int wanted = 57 + Mathf.RoundToInt(Mathf.Clamp01((to.y - floor) / 2.4f) * 22f);
            Musical.Play(voices, music, wanted, to, 0.45f, pluck: true);
        }

        /// <summary>Starts a bolt between two points.</summary>
        Bolt Spawn(Vector3 from, Vector3 to, int depth, float jag, float width, float life, Color color, float brightness, float rejag = 0f)
        {
            Bolt pick = null;
            foreach (var b in bolts) if (!b.Active) { pick = b; break; }
            if (pick == null)
                foreach (var b in bolts)
                    if (pick == null || b.Life < pick.Life) pick = b;
            pick.From = from;
            pick.To = to;
            pick.Depth = Mathf.Clamp(depth, 1, 6);
            pick.Jag = jag;
            pick.Width = width;
            pick.Life = pick.Max = life;
            pick.Color = color;
            pick.Brightness = brightness;
            pick.Rejag = rejag;
            pick.Active = true;
            pick.Snow = snowing && snowLine != null;
            pick.Line.Renderer.sharedMaterial = pick.Snow ? snowLine : lineMaterial;
            pick.Count = Jagged(from, to, pick.Depth, jag, pick.Points);
            return pick;
        }

        /// <summary>
        /// A jagged path from <paramref name="a"/> to <paramref name="b"/>: each segment's middle pushed sideways at
        /// random, <paramref name="depth"/> times over, each time by a little over half as much.
        /// </summary>
        static int Jagged(Vector3 a, Vector3 b, int depth, float jag, Vector3[] into)
        {
            var dir = b - a;
            var side1 = Vector3.Cross(dir, Mathf.Abs(dir.normalized.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var side2 = Vector3.Cross(dir, side1).normalized;
            var src = scratchA;
            var dst = scratchB;
            src[0] = a;
            src[1] = b;
            int count = 2;
            for (int level = 0; level < depth; level++)
            {
                int n = 0;
                for (int i = 0; i < count - 1; i++)
                {
                    dst[n++] = src[i];
                    var mid = (src[i] + src[i + 1]) * 0.5f;
                    dst[n++] = mid + side1 * (Random.Range(-1f, 1f) * jag) + side2 * (Random.Range(-1f, 1f) * jag);
                }
                dst[n++] = src[count - 1];
                count = n;
                (src, dst) = (dst, src);
                jag *= 0.55f;
            }
            System.Array.Copy(src, into, count);
            return count;
        }

        void UpdateBolts(Camera view, float dt)
        {
            var eye = view.transform.position;
            foreach (var b in bolts)
            {
                if (!b.Active) continue;
                b.Life -= dt;
                if (b.Life <= 0f) { b.Active = false; b.Line.SetVisible(false); continue; }
                // Long bolts flicker: they re-draw their path a few times and flash again.
                if (b.Rejag > 0f && Random.value < dt / b.Rejag)
                {
                    b.Count = Jagged(b.From, b.To, b.Depth, b.Jag, b.Points);
                    if (b.Width > 0.03f) flash = Mathf.Max(flash, 0.6f);
                }
                float t = b.Life / b.Max;
                float flicker = Random.Range(0.75f, 1f);
                b.Line.SetVisible(true);
                b.Line.Set(b.Points, b.Count, b.Width * (0.6f + 0.4f * t), b.Color, eye, false);
                block.Clear();
                // Laid over the snow, a bolt at full strength would be a solid ribbon; softer, its edges stay a glow.
                block.SetFloat(IntensityId, b.Brightness * (b.Snow ? 0.6f : 1f) * flicker * Mathf.Clamp01(t * 2.5f));
                b.Line.Renderer.SetPropertyBlock(block);
            }
        }

        void AddMark(Vector3 at, Vector3 normal, float size, Color color, float life)
        {
            var m = marks[nextMark];
            nextMark = (nextMark + 1) % marks.Length;
            m.Position = at;
            m.Normal = normal;
            m.Size = size;
            m.Color = color;
            m.Life = life;
            m.Age = 0f;
            m.Ring.gameObject.SetActive(true);
            m.Glow.gameObject.SetActive(true);
        }

        /// <summary>Where a bolt landed: a ring spreading over the surface and a glow fading away.</summary>
        void UpdateMarks(MusicFrame music, Camera view, float dt)
        {
            foreach (var m in marks)
            {
                if (m.Age >= m.Life) continue;
                m.Age += dt;
                if (m.Age >= m.Life) { m.Ring.gameObject.SetActive(false); m.Glow.gameObject.SetActive(false); continue; }
                float t = m.Age / m.Life;
                var rotation = Quaternion.LookRotation(-m.Normal);
                m.Ring.transform.SetPositionAndRotation(m.Position, rotation);
                m.Ring.transform.localScale = Vector3.one * m.Size * 2f;
                block.Clear();
                block.SetColor(ColorId, m.Color);
                block.SetFloat(RadiusId, Mathf.Lerp(0.1f, 0.95f, 1f - (1f - t) * (1f - t)));
                block.SetFloat(ThicknessId, 0.03f);
                block.SetFloat(AlphaId, (1f - t) * (0.8f + music.Kick * 0.2f));
                m.Ring.SetPropertyBlock(block);

                m.Glow.transform.SetPositionAndRotation(m.Position + m.Normal * 0.01f, rotation);
                m.Glow.transform.localScale = Vector3.one * m.Size * 0.9f;
                block.Clear();
                block.SetColor(ColorId, m.Color);
                block.SetFloat(IntensityId, Mathf.Exp(-m.Age * 4f) * 2f + (1f - t) * 0.25f);
                m.Glow.SetPropertyBlock(block);
            }
        }

        void Burst(Vector3 at, int count, Color color, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = Random.insideUnitSphere * speed + Vector3.up * speed * 0.3f,
                    startColor = Color.Lerp(color, Color.white, Random.value * 0.5f),
                    startSize = Random.Range(0.006f, 0.016f),
                    startLifetime = Random.Range(0.3f, 0.9f)
                }, 1);
            }
        }

        /// <summary>The storm's darkness around you, washed out by each flash.</summary>
        void DrawVeil(Camera view, MusicFrame music)
        {
            if (veil == null) return;
            veil.transform.position = view.transform.position;
            veil.transform.localScale = Vector3.one * 0.5f;
            // The dark breathes a little with the bass, like a storm rolling.
            veilInstance.SetFloat(DarkId, darkness * (0.92f + 0.08f * music.Band(0)));
            veilInstance.SetFloat(FlashId, flash);
        }

        /// <summary>For previews: on the next step, a strike from the left fist and a zap from the right pinch.</summary>
        public void PreviewStorm() => previewStorm = true;
        bool previewStorm;

        void PreviewNow(HandRig rig, MusicFrame music, Camera view)
        {
            previewStorm = false;
            if (rig == null) return;
            Strike(rig.Left, music, view);
            Zap(PinchPoint(rig.Right), 1, music, view);
        }

        // Sounds, made once.
        static AudioClip thunder, zap;

        /// <summary>Thunder: a sharp crack, then a long rumble that rolls in a few waves.</summary>
        static AudioClip Thunder()
        {
            if (thunder != null) return thunder;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int length = (int)(rate * 3f);
            var data = new float[length];
            var random = new System.Random(5);
            float brown = 0f, low = 0f, peak = 1e-4f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float white = (float)(random.NextDouble() * 2 - 1);
                brown = Mathf.Clamp(brown + white * 0.06f, -1f, 1f) * 0.998f;
                low += (brown - low) * 0.08f;
                float crack = white * Mathf.Exp(-t * 28f) * 0.9f;
                float roll = 0.55f + 0.45f * Mathf.Sin(t * 5.3f) * Mathf.Sin(t * 2.1f + 1f);
                float rumble = low * 3.5f * Mathf.Min(1f, t / 0.05f) * Mathf.Exp(-t * 1.1f) * roll;
                data[i] = crack + rumble;
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            for (int i = 0; i < length; i++) data[i] *= 0.8f / peak;
            thunder = AudioClip.Create("thunder", length, 1, rate, false);
            thunder.SetData(data, 0);
            return thunder;
        }

        /// <summary>A zap: a buzzing sweep down with a burst of crackle.</summary>
        static AudioClip ZapSound()
        {
            if (zap != null) return zap;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int length = (int)(rate * 0.32f);
            var data = new float[length];
            var random = new System.Random(8);
            float phase = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float frequency = Mathf.Lerp(1600f, 220f, Mathf.Sqrt(t / 0.32f));
                phase += frequency / rate;
                float buzz = Mathf.Sign(Mathf.Sin(phase * 2f * Mathf.PI)) * 0.3f + Mathf.Sin(phase * 4f * Mathf.PI) * 0.2f;
                float crackle = random.NextDouble() < 0.08 ? (float)(random.NextDouble() * 2 - 1) : 0f;
                float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 11f);
                data[i] = (buzz + crackle * 0.8f) * env * 0.5f;
            }
            zap = AudioClip.Create("zap", length, 1, rate, false);
            zap.SetData(data, 0);
            return zap;
        }
    }
}
