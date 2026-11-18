using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace MusicInYourHands
{
    /// <summary>
    /// Lightning's weather: through the whole storm it rains or it snows, one or the other, and the storm's lightning
    /// works the same in both.
    /// <list type="bullet">
    /// <item><b>Rain</b> (the storm starts with it): drops streak down all round you and splash where they land, with
    /// a little spray and a ripple, wetting the floor (and the furniture tops, once the room is scanned) until puddles
    /// gather in its low spots, ringing with small ripples. Melody drops, larger and tinted, land just as the song sings
    /// their notes. The sung words fall with the rain in front of you, land with a splash and a soft note in the song's
    /// key, and lie in their puddle for a while.</item>
    /// <item><b>Snow</b>: flakes drift down, swaying, and stick where they land: the floor and the furniture whiten in
    /// patches, then under a blanket that builds up (to a cap; past it the snow melts back). Melody flakes glint gold.
    /// The sung words drift down among the flakes, turned to you, and settle on the snow, readable, before they fade.</item>
    /// <item>Hold out an open palm, palm up: flakes gather on it (turn it over and they fall off) and melt away, drops
    /// splash in it and a melody drop rings its note, and a sung word that lands there lies on your palm for a moment.</item>
    /// <item>Keep a fist closed after its strike: a ring fills round it in the next weather's colour while frost or
    /// water gathers in, and after about two seconds the rain turns to snow (the puddles freeze over) or the snow to rain
    /// (the settled snow melts into wet ground), with a caption. Open the hand before the next change.</item>
    /// </list>
    /// Nothing is done for the hands while the menu is open.
    /// </summary>
    public partial class Lightning
    {
        [Header("Weather")]
        [Tooltip("Seconds a fist must stay closed to change the weather (rain to snow, snow to rain).")]
        [SerializeField] float holdSeconds = 2f;
        [Tooltip("Snowflakes per second in quiet moments (x), and extra at full treble (y).")]
        [SerializeField] Vector2 snowRate = new Vector2(55f, 65f);
        [Tooltip("Raindrops per second in quiet moments (x), and extra when the music is loud (y).")]
        [SerializeField] Vector2 rainRate = new Vector2(90f, 110f);
        [Tooltip("How far round you the weather falls (m).")]
        [SerializeField] float weatherReach = 2.5f;
        [Tooltip("How close to your head, sideways, anything falls (m): nothing falls into your eyes.")]
        [SerializeField] float headRoom = 0.35f;
        [Tooltip("Seconds a sung word lies on the ground before it fades.")]
        [SerializeField] float wordLieSeconds = 6f;
        [Tooltip("The most snow that lies around you at once, in units over the whole floor (a solid cover is 1.6 a point, " +
                 "the floor has 2704 points); past it the snow melts back.")]
        [SerializeField] float snowBudget = 2600f;

        const int MaxFalling = 560, MaxSettled = 240, PalmFlakes = 16, MaxRipples = 96;
        const int MaxQuads = MaxFalling + MaxSettled + PalmFlakes * 2 + MaxRipples;
        // How closed a fist must be to build a change: the same as MusicGestures' fist gate, so all the while a fist is
        // held here the thumb microgestures ignore that hand (Meta reports a thumb tap when a fist closes).
        const float HoldFist = 0.8f;
        // A held fist may ease off this long (tracking wobbles) and keep its charge: less than MusicGestures' settle
        // (0.3 s), so the hand stays in its gate the whole time.
        const float HoldSlack = 0.2f;
        // The charge shows only after this long, so a quick fist (a strike) shows none; a hand counts as open below OpenHand.
        const float HoldShow = 0.3f, OpenHand = 0.4f;
        const float CatchRadius = 0.07f, CaptionSeconds = 1.8f;
        const int ArcPoints = 49;
        // The kinds of quad in the weather mesh (LightningWeather shader).
        const float FlakeKind = 0f, DropKind = 1f, RingKind = 2f, DotKind = 3f;
        // Where a sung word is, in its Slot.
        const int WordFalling = 0, WordLying = 1, WordHeld = 2;

        static readonly Color SnowColor = new Color(0.9f, 0.94f, 1f);
        static readonly Color RainColor = new Color(0.5f, 0.68f, 1f);
        static readonly Color MelodyFlakeColor = new Color(1f, 0.88f, 0.6f);
        static readonly Color SnowWordColor = new Color(0.88f, 0.94f, 1f);
        // On white snow the words lie in a deep ice blue, with a dark edge, so they read.
        static readonly Color SnowLyingColor = new Color(0.16f, 0.32f, 0.8f);
        static readonly Color RainWordColor = new Color(0.8f, 0.9f, 1f);
        static readonly Color[] PitchColors = { Effects.Pink, Effects.Orange, Effects.Gold, Effects.Mint, Effects.Blue, Effects.Violet };
        static readonly int RainId = Shader.PropertyToID("_Rain");
        const MeshUpdateFlags NoChecks = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;

        /// <summary>A flake or a drop on its way down (Morph 0 a flake, 1 a drop; in between it is turning).</summary>
        struct Fall
        {
            public Vector3 Position, Velocity;
            public float Size, Ground, Seed, Age, Morph;
            // Melody items fall by the song clock, from Top at LandTime - Span to the ground at LandTime.
            public float LandTime, Span, Top;
            public int Midi;
            public bool Active, Synced;
        }

        struct Settled
        {
            public Vector3 Position;
            public float Age, Life, Size;
        }

        struct Ripple
        {
            public Vector3 Position, Normal;
            public float Age, Life, Size, Alpha;
            public Color Color;
        }

        struct PalmFlake
        {
            public Vector3 Local;
            public float Age, Life, Size;
            public bool Active;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WeatherVertex
        {
            public Vector3 Position;
            public Color32 Color;
            public Vector2 Uv;
            public Vector2 Shape;
        }

        readonly Fall[] falls = new Fall[MaxFalling];
        readonly Settled[] settled = new Settled[MaxSettled];
        readonly Ripple[] ripples = new Ripple[MaxRipples];
        readonly PalmFlake[] palmFlakes = new PalmFlake[PalmFlakes * 2];
        readonly WeatherVertex[] weatherVertices = new WeatherVertex[MaxQuads * 4];
        readonly float[] held = new float[2], slack = new float[2], palmWet = new float[2];
        readonly bool[] spent = new bool[2], catching = new bool[2];
        readonly Vector3[] cup = new Vector3[2], cupNormal = new Vector3[2];
        readonly Quaternion[] palmTurn = new Quaternion[2];
        readonly Renderer[] palmGlows = new Renderer[2], fistGlows = new Renderer[2];
        readonly LineStrip[] arcs = new LineStrip[2];
        readonly Vector3[] arcPoints = new Vector3[ArcPoints];
        Mesh weatherMesh;
        MeshRenderer weatherRenderer;
        Material weatherInstance;
        ParticleSystem splash;
        TypeLabel caption;
        Color captionColor;
        Vector3 captionAt, head, flatForward, wind;
        float floorY, spawnTop, snowDebt, rainDebt, changeAge = 99f, captionAge = 99f, lastNoteTime = -1f, lastSongTime, lastRim = -1f;
        int quads, nextSettled, nextRipple, fallCursor;
        // The weather now: snow, or (false) rain.
        bool snowing;

        void BuildWeather()
        {
            if (weatherMaterial != null)
            {
                weatherInstance = new Material(weatherMaterial) { name = "Weather" };
                var go = new GameObject("Weather");
                go.transform.SetParent(transform, false);
                weatherMesh = new Mesh { name = "Weather" };
                weatherMesh.MarkDynamic();
                weatherMesh.SetVertexBufferParams(MaxQuads * 4,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2));
                weatherMesh.SetVertexBufferData(weatherVertices, 0, 0, weatherVertices.Length, 0, NoChecks);
                var indices = new ushort[MaxQuads * 6];
                for (int q = 0; q < MaxQuads; q++)
                {
                    int v = q * 4, i = q * 6;
                    indices[i] = (ushort)v; indices[i + 1] = (ushort)(v + 1); indices[i + 2] = (ushort)(v + 2);
                    indices[i + 3] = (ushort)v; indices[i + 4] = (ushort)(v + 2); indices[i + 5] = (ushort)(v + 3);
                }
                weatherMesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
                weatherMesh.SetIndexBufferData(indices, 0, 0, indices.Length, NoChecks);
                weatherMesh.subMeshCount = 1;
                weatherMesh.SetSubMesh(0, new SubMeshDescriptor(0, 6), NoChecks);
                go.AddComponent<MeshFilter>().sharedMesh = weatherMesh;
                weatherRenderer = go.AddComponent<MeshRenderer>();
                weatherRenderer.sharedMaterial = weatherInstance;
                weatherRenderer.shadowCastingMode = ShadowCastingMode.Off;
                weatherRenderer.receiveShadows = false;
                weatherRenderer.enabled = false;
            }
            splash = Effects.MakeSparkSystem("Splash", transform, glowMaterial);
            var main = splash.main;
            main.gravityModifier = 0.9f;
            main.maxParticles = 500;
            var noise = splash.noise;
            noise.strength = 0.01f;
            for (int side = 0; side < 2; side++)
            {
                palmGlows[side] = Effects.MakeQuad($"Wet Palm {side}", transform, glowMaterial);
                palmGlows[side].gameObject.SetActive(false);
                fistGlows[side] = Effects.MakeQuad($"Fist Charge {side}", transform, glowMaterial);
                fistGlows[side].gameObject.SetActive(false);
                arcs[side] = new LineStrip($"Charge Ring {side}", transform, lineMaterial, ArcPoints);
                arcs[side].SetVisible(false);
            }
            caption = new TypeLabel("Weather Caption", transform, 0.04f);
            caption.GameObject.SetActive(false);
            BuildGround();
        }

        /// <summary>Each time the storm starts, it rains.</summary>
        void EnterWeather()
        {
            snowing = false;
            changeAge = 99f;
            captionAge = 99f;
            lastNoteTime = -1f;
            lastRim = -1f;
            for (int side = 0; side < 2; side++)
            {
                held[side] = slack[side] = palmWet[side] = 0f;
                spent[side] = false;
            }
            ClearWeather();
        }

        void ExitWeather()
        {
            ClearWeather();
            for (int side = 0; side < 2; side++)
            {
                HideCharge(side);
                if (palmGlows[side] != null) palmGlows[side].gameObject.SetActive(false);
            }
            if (caption != null) caption.GameObject.SetActive(false);
            if (splash != null) splash.Clear();
        }

        void ClearWeather()
        {
            for (int i = 0; i < falls.Length; i++) falls[i].Active = false;
            for (int i = 0; i < settled.Length; i++) settled[i].Life = 0f;
            for (int i = 0; i < ripples.Length; i++) ripples[i].Life = 0f;
            for (int i = 0; i < palmFlakes.Length; i++) palmFlakes[i].Active = false;
            snowDebt = rainDebt = 0f;
            if (weatherRenderer != null) weatherRenderer.enabled = false;
            ClearGround();
        }

        void DestroyWeather()
        {
            if (weatherMesh != null) Discard(weatherMesh);
            if (weatherInstance != null) Discard(weatherInstance);
            DestroyGround();
        }

        /// <summary>The weather's frame: the palms, what falls and lands, the words, the ground, the caption, then drawing.</summary>
        void StepWeather(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            head = view.transform.position;
            flatForward = Effects.FlatForward(view);
            floorY = Effects.FloorY(view);
            spawnTop = Room.Ceiling(Mathf.Max(head.y + 0.75f, floorY + 2.3f)) - 0.08f;
            if (previewSeconds >= 0f) Prefill();
            changeAge += dt;
            // A slow wind turning round the room.
            wind = new Vector3(Mathf.Sin(Clock * 0.13f), 0f, Mathf.Cos(Clock * 0.09f + 1f)) * 0.06f;
            Palms(rig, view, music, dt);
            Spawn(music, dt);
            Melody(music);
            StepFalls(music, dt);
            StepWords(rig, music, view, dt);
            StepSettled(dt);
            for (int i = 0; i < MaxRipples; i++)
                if (ripples[i].Age < ripples[i].Life) ripples[i].Age += dt;
            StepPalmFlakes(rig, dt);
            StepGround(dt);
            StepCaption(view, dt);
            DrawWeather(view);
            if (weatherInstance != null) weatherInstance.SetFloat(FlashId, flash);
            if (groundInstance != null) groundInstance.SetFloat(FlashId, flash);
        }

        /// <summary>
        /// Which palms catch the weather: open, held palm up, and not while the menu is open (the hands are choosing
        /// there, so it falls past them). A wet palm glows softly.
        /// </summary>
        void Palms(HandRig rig, Camera view, MusicFrame music, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig != null ? rig.Get(side == 0) : null;
                bool tracked = hand != null && hand.IsTracked && !hand.InSystemGesture;
                catching[side] = tracked && !MenuOpen && hand.Fist < 0.5f && hand.Pinch < 0.6f && hand.PalmNormal.y > 0.5f;
                if (tracked)
                {
                    var palm = hand.PalmJoint != null ? hand.PalmJoint.position : hand.Palm.position;
                    cupNormal[side] = hand.PalmNormal.sqrMagnitude > 0.5f ? hand.PalmNormal.normalized : Vector3.up;
                    cup[side] = palm + cupNormal[side] * 0.015f;
                    palmTurn[side] = hand.PalmJoint != null ? hand.PalmJoint.rotation : hand.Palm.rotation;
                }
                palmWet[side] = Mathf.MoveTowards(palmWet[side], 0f, dt * 0.3f);
                var glow = palmGlows[side];
                bool show = tracked && palmWet[side] > 0.02f;
                if (glow.gameObject.activeSelf != show) glow.gameObject.SetActive(show);
                if (!show) continue;
                glow.transform.position = cup[side];
                glow.transform.localScale = Vector3.one * (0.07f + palmWet[side] * 0.04f) * (1f + music.Kick * 0.15f);
                Effects.Face(glow.transform, view);
                block.Clear();
                block.SetColor(ColorId, RainColor);
                block.SetFloat(IntensityId, Mathf.Clamp01(palmWet[side]) * 0.9f);
                glow.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// New flakes or drops, from just under the ceiling all round you: more snow with the treble, more rain the
        /// louder the music and a shower on each beat. After a change the new weather comes in over a second. Now and
        /// then one falls right over a waiting palm.
        /// </summary>
        void Spawn(MusicFrame music, float dt)
        {
            float ramp = Mathf.Clamp01(changeAge / 1.2f) * FadeIn;
            if (snowing)
            {
                float treble = Mathf.Clamp01((music.Band(4) + music.Band(5)) * 0.6f);
                snowDebt = Mathf.Min(snowDebt + (snowRate.x + snowRate.y * treble) * ramp * dt, 24f);
            }
            else
            {
                float loud = Mathf.Clamp01((music.Band(1) + music.Band(2) + music.Band(3)) / 3f * 1.2f + music.Kick * 0.3f);
                rainDebt = Mathf.Min(rainDebt + (rainRate.x + rainRate.y * loud) * ramp * dt + (music.Beat ? 8f * ramp : 0f), 24f);
            }
            float debt = snowing ? snowDebt : rainDebt;
            while (debt >= 1f)
            {
                debt -= 1f;
                if (Spot(headRoom, weatherReach, out var at)) Drop(at, !snowing);
            }
            if (snowing) snowDebt = debt;
            else rainDebt = debt;
            // Over a waiting palm, a few a second, so holding one out is soon rewarded.
            for (int side = 0; side < 2; side++)
            {
                if (!catching[side] || Random.value > dt * (snowing ? 5f : 9f) * ramp || !Reachable(cup[side])) continue;
                Drop(cup[side] + Flat(Random.insideUnitCircle * 0.05f), !snowing);
            }
        }

        static Vector3 Flat(Vector2 v) => new Vector3(v.x, 0f, v.y);

        /// <summary>Whether a point is far enough from your head, sideways, for things to fall past it.</summary>
        bool Reachable(Vector3 at) => new Vector2(at.x - head.x, at.z - head.z).magnitude >= headRoom - 0.02f;

        /// <summary>A place to fall, between <paramref name="near"/> and <paramref name="far"/> metres round you, mostly ahead, inside the walls.</summary>
        bool Spot(float near, float far, out Vector3 at)
        {
            float angle = Mathf.Atan2(flatForward.x, flatForward.z) + (Random.value < 0.7f ? Random.Range(-1.3f, 1.3f) : Random.Range(-Mathf.PI, Mathf.PI));
            var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            float distance = Mathf.Sqrt(Random.Range(near * near, far * far));
            if (Room.RaycastWalls(new Ray(new Vector3(head.x, spawnTop - 0.3f, head.z), dir), distance + 0.1f, out float wall, out _))
                distance = wall - 0.1f;
            at = head + dir * distance;
            return distance >= near;
        }

        /// <summary>Starts a flake (or a drop) falling from under the ceiling above <paramref name="at"/>.</summary>
        int Drop(Vector3 at, bool rain)
        {
            int i = FreeFall();
            if (i < 0) return -1;
            ref var f = ref falls[i];
            f.Active = true;
            f.Synced = false;
            f.Midi = -1;
            f.Position = new Vector3(at.x, spawnTop - Random.value * 0.2f, at.z);
            f.Ground = Room.GroundBelow(f.Position, floorY);
            f.Seed = Random.value;
            f.Size = Random.Range(0.7f, 1.3f);
            f.Morph = rain ? 1f : 0f;
            f.Velocity = Vector3.down * (rain ? 3.5f : 0.4f);
            f.Age = 0f;
            return i;
        }

        int FreeFall()
        {
            for (int k = 0; k < MaxFalling; k++)
            {
                int i = (fallCursor + k) % MaxFalling;
                if (falls[i].Active) continue;
                fallCursor = i + 1;
                return i;
            }
            return -1;
        }

        /// <summary>
        /// The melody: each note of the song's melody falls as a larger drop (or a gold flake) in front of you, placed
        /// by pitch from left (low) to right (high), and lands just as it is sung. Not for notes made from the lyrics:
        /// the words themselves fall then.
        /// </summary>
        void Melody(MusicFrame music)
        {
            float now = music.SongTime;
            // The song jumped back (looped, restarted): carry on from here.
            if (now < lastSongTime - 0.5f) lastNoteTime = now;
            lastSongTime = now;
            if (music.Upcoming == null || music.Notes == null || music.Notes.IsFromLyrics || changeAge < 1f) return;
            float lead = snowing ? 2.6f : 1.5f;
            foreach (var note in music.Upcoming)
            {
                if (note.Time <= lastNoteTime) continue;
                float ahead = note.Time - now;
                if (ahead > lead) break;
                lastNoteTime = note.Time;
                if (ahead < 0.5f) continue;
                var right = Vector3.Cross(Vector3.up, flatForward);
                float x = (Mathf.Clamp01((note.Midi - 60f) / 20f) * 2f - 1f) * 0.45f;
                float z = 0.95f + Mathf.Sin(note.Time * 12.9898f) * 0.15f;
                int i = Drop(head + flatForward * z + right * x, !snowing);
                if (i < 0) continue;
                ref var f = ref falls[i];
                f.Midi = note.Midi;
                f.Synced = true;
                f.LandTime = note.Time;
                f.Span = ahead;
                f.Top = Mathf.Min(head.y + 0.5f, spawnTop);
                f.Position.y = f.Top;
                f.Size = 1f;
            }
        }

        /// <summary>
        /// Everything on its way down: flakes sway and drift, drops streak straight down, and each one turning from the
        /// one into the other after a change slows down or speeds up. A palm held under catches; the ground takes the rest.
        /// </summary>
        void StepFalls(MusicFrame music, float dt)
        {
            float target = snowing ? 0f : 1f;
            float now = music.SongTime, t = Clock;
            for (int i = 0; i < MaxFalling; i++)
            {
                ref var f = ref falls[i];
                if (!f.Active) continue;
                f.Age += dt;
                if (f.Morph != target) f.Morph = Mathf.MoveTowards(f.Morph, target, dt / (0.6f + f.Seed * 0.9f));
                var before = f.Position;
                if (f.Synced)
                {
                    float left = f.LandTime - now;
                    // A song that jumped back would leave it hanging: let it go.
                    if (left > f.Span + 0.5f) { f.Active = false; continue; }
                    f.Position.y = f.Ground + Mathf.Clamp01(left / Mathf.Max(f.Span, 0.05f)) * (f.Top - f.Ground);
                    f.Velocity = Vector3.down * ((f.Top - f.Ground) / Mathf.Max(f.Span, 0.05f));
                    if (f.Morph < 0.5f) f.Position += new Vector3(Mathf.Sin(t * 1.4f + f.Seed * 20f), 0f, 0f) * (0.04f * dt);
                }
                else
                {
                    float m = f.Morph * f.Morph;
                    float speed = Mathf.Lerp(Mathf.Lerp(0.32f, 0.5f, f.Seed), Mathf.Lerp(3.2f, 4.2f, f.Seed), m);
                    var sway = new Vector3(Mathf.Sin(t * 1.3f + f.Seed * 20f), 0f, Mathf.Cos(t * 1.1f + f.Seed * 31f)) * (0.07f * (1f - f.Morph));
                    f.Velocity = Vector3.down * speed + sway + wind * (1f - 0.6f * f.Morph);
                    f.Position += f.Velocity * dt;
                }
                if (CatchFall(ref f, before, music) || f.Age > 30f) { f.Active = false; continue; }
                if (f.Position.y <= f.Ground)
                {
                    Land(ref f);
                    f.Active = false;
                }
            }
        }

        /// <summary>
        /// A flake sticks where it lands: on the snow cover, and for a few seconds as a fresh flake on top. A drop
        /// splashes: water on the ground, a ripple and, now and then, a little spray. Melody drops splash bigger.
        /// </summary>
        void Land(ref Fall f)
        {
            var at = new Vector3(f.Position.x, f.Ground, f.Position.z);
            bool melody = f.Midi >= 0;
            if (f.Morph < 0.5f)
            {
                AddGround(at, (melody ? 0.6f : 0.32f) * f.Size, 0f);
                if (melody || Random.value < 0.6f) AddSettled(at + Vector3.up * (GroundLift + 0.004f), melody ? 1.6f : f.Size);
                if (melody) Glint(at, MelodyFlakeColor, 6);
            }
            else
            {
                AddGround(at, 0f, melody ? 0.8f : 0.4f);
                var colour = melody ? Color.Lerp(RainColor, PitchColors[Mathf.Abs(f.Midi) % PitchColors.Length], 0.45f) : RainColor;
                if (melody || Random.value < 0.5f)
                    AddRipple(at + Vector3.up * 0.005f, Vector3.up, melody ? 0.36f : Random.Range(0.06f, 0.16f), melody ? 1.1f : Random.Range(0.35f, 0.6f), colour, melody ? 1f : 0.5f);
                if (melody) Splash(at, colour, 14, 0.45f);
                else if (Random.value < 0.3f) Splash(at, RainColor, 3, 0.28f);
            }
        }

        /// <summary>A flake or drop meeting a waiting palm: flakes gather on it, drops splash in it, a melody one rings its note.</summary>
        bool CatchFall(ref Fall f, Vector3 before, MusicFrame music)
        {
            for (int side = 0; side < 2; side++)
            {
                if (!catching[side]) continue;
                var c = cup[side];
                if (before.y < c.y - 0.01f || f.Position.y > c.y + 0.02f) continue;
                float dx = f.Position.x - c.x, dz = f.Position.z - c.z;
                if (dx * dx + dz * dz > CatchRadius * CatchRadius) continue;
                bool melody = f.Midi >= 0;
                if (f.Morph < 0.5f) AddPalmFlake(side, f.Position, melody ? 1.5f : f.Size);
                else
                {
                    palmWet[side] = Mathf.Min(1.4f, palmWet[side] + (melody ? 0.35f : 0.12f));
                    AddRipple(c + cupNormal[side] * 0.004f, cupNormal[side], melody ? 0.14f : 0.07f, 0.5f, RainColor, 0.9f);
                    Splash(c, RainColor, melody ? 8 : 2, 0.2f);
                }
                // Soft, so it decorates the sung note rather than competing with it.
                if (melody) Musical.Play(voices, music, f.Midi, c, 0.3f);
                Acted("catch");
                return true;
            }
            return false;
        }

        /// <summary>
        /// A sung word starts: it falls with the weather a little in front of you, left or right, turned to you so you
        /// can read it on the way down; with a palm waiting, it may fall towards your palm instead.
        /// </summary>
        protected override void OnWord(string word, bool newLine)
        {
            if (SungWords == null || View == null) return;
            var eye = View.transform.position;
            var forward = Effects.FlatForward(View);
            var spot = WordSpot(eye, forward);
            float top = eye.y + (snowing ? 0.45f : 0.3f);
            for (int side = 0; side < 2; side++)
            {
                if (!catching[side] || !Reachable(cup[side]) || Random.value > 0.4f) continue;
                // Over the palm, starting below your eyes, so it never falls past your face.
                spot = cup[side] + Flat(Random.insideUnitCircle * 0.03f);
                top = Mathf.Min(eye.y - 0.12f, cup[side].y + 0.45f);
            }
            var w = SungWords.Add(word, snowing ? SnowWordColor : RainWordColor, 60f);
            w.Position = new Vector3(spot.x, top, spot.z);
            w.Velocity = Vector3.down * (snowing ? 0.28f : 0.08f);
            w.Rotation = WordLayer.Facing(w.Position, eye);
            w.Slot = WordFalling;
            w.Size = 1f;
            w.Halo = 0.3f;
        }

        /// <summary>
        /// Where a new word falls: in front of you, within about 45 degrees either side and 0.7 to 1.6 m away, at the
        /// best of a few tries, the one furthest from the words already falling or lying, so they don't land on each other.
        /// </summary>
        Vector3 WordSpot(Vector3 eye, Vector3 forward)
        {
            var right = Vector3.Cross(Vector3.up, forward);
            var best = eye + forward;
            float bestRoom = -1f;
            var words = SungWords.Words;
            for (int c = 0; c < 8; c++)
            {
                var spot = eye + Quaternion.AngleAxis(Random.Range(-45f, 45f), Vector3.up) * forward * Random.Range(0.7f, 1.6f);
                float room = 99f;
                for (int k = 0; k < words.Count; k++)
                {
                    var o = words[k];
                    if (!o.Active || o.Slot == WordHeld) continue;
                    room = Mathf.Min(room, Crowding(o.Position - spot, right, forward));
                }
                if (room > bestRoom) { bestRoom = room; best = spot; }
            }
            return best;
        }

        /// <summary>How far apart two words are, in word widths and lines: below 1 they overlap where they lie.</summary>
        static float Crowding(Vector3 apart, Vector3 right, Vector3 forward)
        {
            float x = Vector3.Dot(apart, right) / 0.34f, z = Vector3.Dot(apart, forward) / 0.13f;
            return x * x + z * z;
        }

        /// <summary>
        /// The sung words: falling (in the snow slowly, swaying; in the rain faster and faster, like a drop), then lying
        /// where they landed, reading towards you, on top of the snow or in a puddle, until they fade; or lying on a palm
        /// for a moment.
        /// </summary>
        void StepWords(HandRig rig, MusicFrame music, Camera view, float dt)
        {
            if (SungWords == null) return;
            float rim = snowing ? 0.9f : 0.45f;
            if (rim != lastRim) SungWords.Rim = lastRim = rim;
            var eye = view.transform.position;
            float ease = 1f - Mathf.Exp(-6f * dt);
            var words = SungWords.Words;
            for (int k = 0; k < words.Count; k++)
            {
                var w = words[k];
                if (!w.Active) continue;
                if (w.Slot == WordFalling)
                {
                    float speed = -w.Velocity.y;
                    speed = snowing ? Mathf.MoveTowards(speed, 0.28f, dt * 0.8f) : Mathf.Min(speed + 0.35f * dt, 2.5f);
                    w.Velocity = Vector3.down * speed;
                    var sway = snowing ? new Vector3(Mathf.Sin(w.Age * 1.3f + w.Seed * 9f), 0f, Mathf.Cos(w.Age * 1.1f + w.Seed * 13f)) * 0.05f : Vector3.zero;
                    var before = w.Position;
                    w.Position += (w.Velocity + sway) * dt;
                    w.Rotation = Quaternion.Slerp(w.Rotation, snowing ? Swaying(w, eye) : WordLayer.Facing(w.Position, eye), ease);
                    w.Color = Color.Lerp(w.Color, snowing ? SnowWordColor : RainWordColor, ease);
                    w.Halo = 0.3f + flash * 0.5f;
                    if (CatchWord(w, before, music)) continue;
                    float ground = Room.GroundBelow(w.Position, floorY);
                    if (w.Position.y <= ground + 0.006f) LandWord(w, ground, music);
                }
                else if (w.Slot == WordLying)
                {
                    // Just over the snow or the water.
                    w.Position.y = w.Spot.x + GroundLift + 0.004f;
                    var lying = WordLayer.Lying(w.Position, eye) * Quaternion.Euler(0f, 0f, (w.Seed - 0.5f) * 24f);
                    w.Rotation = Quaternion.Slerp(w.Rotation, lying, 1f - Mathf.Exp(-9f * dt));
                    w.Size = Mathf.MoveTowards(w.Size, 1.35f, dt * 1.2f);
                    w.Color = Color.Lerp(w.Color, snowing ? SnowLyingColor : RainWordColor, ease);
                    w.Halo = (snowing ? 0f : 0.22f) + flash * 0.4f;
                    w.Alpha = Mathf.Clamp01((w.Life - w.Age) / 2f);
                }
                else
                {
                    var hand = rig != null ? rig.Get(w.Spot.y < 0.5f) : null;
                    int side = w.Spot.y < 0.5f ? 0 : 1;
                    if (hand == null || !hand.IsTracked || hand.PalmNormal.y < 0.15f)
                    {
                        // Tipped off the palm: it falls again.
                        w.Slot = WordFalling;
                        w.Velocity = Vector3.down * 0.3f;
                        w.Life = Mathf.Max(w.Life, w.Age + 8f);
                        continue;
                    }
                    var n = cupNormal[side];
                    w.Position = Vector3.Lerp(w.Position, cup[side] + n * 0.008f, 1f - Mathf.Exp(-14f * dt));
                    var along = Vector3.ProjectOnPlane(w.Position - eye, n);
                    w.Rotation = Quaternion.Slerp(w.Rotation, Quaternion.LookRotation(-n, along.sqrMagnitude > 1e-6f ? along.normalized : flatForward), ease);
                    w.Size = Mathf.MoveTowards(w.Size, 0.75f, dt * 2f);
                    w.Halo = 0.35f + flash * 0.4f;
                    // It melts (or soaks) away in your hand.
                    w.Alpha = Mathf.Clamp01((w.Life - w.Age) / 1.2f);
                }
            }
        }

        /// <summary>A falling word turned to the eye, rocking a little like a leaf.</summary>
        static Quaternion Swaying(WordLayer.Word w, Vector3 eye) =>
            WordLayer.Facing(w.Position, eye) * Quaternion.Euler(Mathf.Sin(w.Age * 0.9f + w.Seed * 7f) * 12f - 6f,
                Mathf.Sin(w.Age * 0.7f + w.Seed * 11f) * 14f, Mathf.Sin(w.Age * 1.1f + w.Seed * 5f) * 8f);

        /// <summary>A word lands: on the snow with a puff, or in the rain with a splash, a puddle and a soft note.</summary>
        void LandWord(WordLayer.Word w, float ground, MusicFrame music)
        {
            w.Slot = WordLying;
            w.Spot = new Vector2(ground, 0f);
            w.Life = w.Age + wordLieSeconds + 2f;
            w.Velocity = Vector3.zero;
            var at = new Vector3(w.Position.x, ground, w.Position.z);
            // An older word lying right where this one lands sinks away, so the two never pile up unreadably.
            var right = Vector3.Cross(Vector3.up, flatForward);
            var words = SungWords.Words;
            for (int k = 0; k < words.Count; k++)
            {
                var o = words[k];
                if (o == w || !o.Active || o.Slot != WordLying || Crowding(o.Position - w.Position, right, flatForward) > 1f) continue;
                o.Life = Mathf.Min(o.Life, o.Age + 1f);
            }
            if (snowing)
            {
                AddGround(at, 0.5f, 0f);
                Puff(at + Vector3.up * 0.01f, 10, 0.25f);
            }
            else
            {
                AddGround(at, 0f, 1.2f);
                AddRipple(at + Vector3.up * 0.005f, Vector3.up, 0.5f, 1.2f, RainWordColor, 1f);
                Splash(at, RainWordColor, 18, 0.5f);
                // Further right, higher.
                int wanted = 64 + Mathf.RoundToInt(Mathf.Clamp(Vector3.Dot(at - head, right), -0.5f, 0.5f) * 14f);
                Musical.Play(voices, music, wanted, at, 0.18f, pluck: true);
            }
        }

        /// <summary>A palm held up under a falling word catches it; it lies there for a moment, with a soft note.</summary>
        bool CatchWord(WordLayer.Word w, Vector3 before, MusicFrame music)
        {
            for (int side = 0; side < 2; side++)
            {
                if (!catching[side]) continue;
                var c = cup[side];
                if (before.y < c.y - 0.02f || w.Position.y > c.y + 0.04f) continue;
                float dx = w.Position.x - c.x, dz = w.Position.z - c.z;
                if (dx * dx + dz * dz > 0.1f * 0.1f) continue;
                w.Slot = WordHeld;
                w.Spot = new Vector2(0f, side);
                w.Life = w.Age + (snowing ? 4f : 3f);
                if (snowing) Glint(c, SnowColor, 8);
                else
                {
                    palmWet[side] = Mathf.Min(1.4f, palmWet[side] + 0.4f);
                    AddRipple(c + cupNormal[side] * 0.004f, cupNormal[side], 0.16f, 0.6f, RainWordColor, 1f);
                    Splash(c, RainWordColor, 8, 0.22f);
                }
                Musical.Play(voices, music, snowing ? 79 : 74, c, 0.22f);
                Acted("catch word");
                return true;
            }
            return false;
        }

        void AddSettled(Vector3 at, float size)
        {
            ref var s = ref settled[nextSettled];
            nextSettled = (nextSettled + 1) % MaxSettled;
            s.Position = at;
            s.Size = size;
            s.Age = 0f;
            s.Life = Random.Range(4f, 7f);
        }

        /// <summary>Fresh flakes lying on the snow fade into it; when the rain comes they melt at once.</summary>
        void StepSettled(float dt)
        {
            for (int i = 0; i < MaxSettled; i++)
            {
                ref var s = ref settled[i];
                if (s.Age >= s.Life) continue;
                s.Age += dt * (snowing ? 1f : 4f);
            }
        }

        void AddRipple(Vector3 at, Vector3 normal, float size, float life, Color color, float alpha, float delay = 0f)
        {
            ref var r = ref ripples[nextRipple];
            nextRipple = (nextRipple + 1) % MaxRipples;
            r.Position = at;
            r.Normal = normal;
            r.Size = size;
            r.Life = life;
            r.Age = -delay;
            r.Color = color;
            r.Alpha = alpha;
        }

        /// <summary>A flake caught on a palm: it stays where it fell on the palm, moving with the hand, until it melts.</summary>
        void AddPalmFlake(int side, Vector3 at, float size)
        {
            int pick = side * PalmFlakes;
            for (int k = 0; k < PalmFlakes; k++)
            {
                int i = side * PalmFlakes + k;
                if (!palmFlakes[i].Active) { pick = i; break; }
                if (palmFlakes[i].Age / palmFlakes[i].Life > palmFlakes[pick].Age / palmFlakes[pick].Life) pick = i;
            }
            var n = cupNormal[side];
            var offset = Vector3.ProjectOnPlane(at - cup[side], n);
            if (offset.magnitude > 0.045f) offset = offset.normalized * 0.045f;
            ref var p = ref palmFlakes[pick];
            p.Local = Quaternion.Inverse(palmTurn[side]) * (offset + n * Random.Range(0f, 0.006f));
            p.Age = 0f;
            p.Life = Random.Range(4f, 7f);
            p.Size = size;
            p.Active = true;
        }

        /// <summary>The flakes on a palm melt after a few seconds (at once in the rain), and fall off when the palm turns over.</summary>
        void StepPalmFlakes(HandRig rig, float dt)
        {
            for (int i = 0; i < palmFlakes.Length; i++)
            {
                ref var p = ref palmFlakes[i];
                if (!p.Active) continue;
                int side = i / PalmFlakes;
                var hand = rig != null ? rig.Get(side == 0) : null;
                p.Age += dt * (snowing ? 1f : 5f);
                if (hand == null || !hand.IsTracked || p.Age >= p.Life) { p.Active = false; continue; }
                if (hand.PalmNormal.y < 0.1f)
                {
                    int f = Drop(PalmFlakeAt(side, p), false);
                    if (f >= 0)
                    {
                        falls[f].Position = PalmFlakeAt(side, p);
                        falls[f].Ground = Room.GroundBelow(falls[f].Position, floorY);
                        falls[f].Size = p.Size;
                    }
                    p.Active = false;
                }
            }
        }

        Vector3 PalmFlakeAt(int side, in PalmFlake p) => cup[side] + palmTurn[side] * p.Local;

        /// <summary>
        /// Where lightning hits the floor: in the snow it melts a round hole, wet in the middle, and throws snow up; in
        /// the rain it splashes, with ripples running out through the puddles.
        /// </summary>
        void WeatherImpact(Vector3 at, Vector3 normal, float radius, bool strike)
        {
            if (snowing)
            {
                MeltAround(at, radius, 0.4f);
                for (int i = 0; i < MaxSettled; i++)
                    if ((settled[i].Position - at).sqrMagnitude < radius * radius) settled[i].Life = 0f;
                Puff(at + Vector3.up * 0.02f, strike ? 50 : 14, strike ? 0.9f : 0.4f);
            }
            else
            {
                MeltAround(at, radius, strike ? 1.4f : 0.7f);
                Splash(at, RainColor, strike ? 44 : 14, strike ? 1.3f : 0.6f);
                AddRipple(at + normal * 0.006f, normal, radius * 2.4f, 0.9f, RainColor, 1f);
                if (!strike) return;
                AddRipple(at + normal * 0.006f, normal, radius * 3.4f, 1.2f, RainColor, 0.8f, 0.15f);
                AddRipple(at + normal * 0.006f, normal, radius * 4.4f, 1.5f, RainColor, 0.6f, 0.35f);
            }
        }

        /// <summary>
        /// A fist held after its strike builds a change of weather, shown round the fist. Only a full fist counts, and
        /// not while the menu is open; a fist that met the menu, or already changed the weather, must open first.
        /// </summary>
        void Hold(HandRig.Hand hand, int side, bool tracked, MusicFrame music, Camera view, float dt)
        {
            if (tracked && hand.Fist < OpenHand) spent[side] = false;
            if (MenuOpen && (held[side] > 0f || (tracked && hand.Fist >= HoldFist))) spent[side] = true;
            bool closed = tracked && hand.Fist >= HoldFist && !spent[side] && !MenuOpen;
            if (closed)
            {
                held[side] += dt;
                slack[side] = 0f;
            }
            else if (held[side] > 0f)
            {
                slack[side] += dt;
                if (slack[side] > HoldSlack || !tracked || spent[side] || MenuOpen || hand.Fist < 0.55f) held[side] = 0f;
            }
            var fist = hand.Palm.position + hand.PalmNormal * 0.03f;
            if (held[side] >= holdSeconds)
            {
                ChangeWeather(fist, music, view);
                return;
            }
            DrawCharge(side, fist, Mathf.InverseLerp(HoldShow, holdSeconds, held[side]), view, dt);
        }

        /// <summary>
        /// The charge round a held fist, in the next weather's colour: a ring filling clockwise from the top, a glow in
        /// the fist, and frost (or drops) drawn in towards it, more as it fills.
        /// </summary>
        void DrawCharge(int side, Vector3 fist, float fill, Camera view, float dt)
        {
            var arc = arcs[side];
            var glow = fistGlows[side];
            if (fill <= 0f || arc == null)
            {
                HideCharge(side);
                return;
            }
            var colour = snowing ? RainColor : SnowColor;
            var eye = view.transform.position;
            var toEye = (eye - fist).normalized;
            var right = Vector3.Cross(Vector3.up, toEye);
            right = right.sqrMagnitude > 1e-4f ? right.normalized : view.transform.right;
            var up = Vector3.Cross(toEye, right);
            const float Radius = 0.08f;
            int count = 2 + Mathf.FloorToInt(fill * (ArcPoints - 2));
            for (int k = 0; k < count; k++)
            {
                float a = Mathf.Min(k / (float)(ArcPoints - 1), fill) * Mathf.PI * 2f;
                arcPoints[k] = fist + (up * Mathf.Cos(a) - right * Mathf.Sin(a)) * Radius;
            }
            arc.SetVisible(true);
            arc.Set(arcPoints, count, 0.006f + fill * 0.004f, colour, eye, false);
            block.Clear();
            block.SetFloat(IntensityId, 1f + fill);
            arc.Renderer.SetPropertyBlock(block);

            if (!glow.gameObject.activeSelf) glow.gameObject.SetActive(true);
            glow.transform.position = fist + toEye * 0.04f;
            glow.transform.localScale = Vector3.one * (0.05f + fill * 0.08f);
            Effects.Face(glow.transform, view);
            block.Clear();
            block.SetColor(ColorId, colour);
            block.SetFloat(IntensityId, fill * 1.2f);
            glow.SetPropertyBlock(block);

            // Frost or water gathering in.
            if (Random.value < dt * (12f + 60f * fill))
            {
                var from = fist + Random.onUnitSphere * Random.Range(0.12f, 0.18f);
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = from,
                    velocity = (fist - from) / 0.45f,
                    startColor = Color.Lerp(colour, Color.white, Random.value * 0.4f),
                    startSize = Random.Range(0.006f, 0.012f),
                    startLifetime = 0.45f
                }, 1);
            }
        }

        void HideCharge(int side)
        {
            if (arcs[side] != null) arcs[side].SetVisible(false);
            if (fistGlows[side] != null && fistGlows[side].gameObject.activeSelf) fistGlows[side].gameObject.SetActive(false);
        }

        /// <summary>
        /// The weather changes: what is falling turns (drops slow into flakes, or flakes quicken into drops), the
        /// ground follows (StepGround: puddles freeze over, or the snow melts), the new weather bursts from the fist
        /// in a ring, with a gust and a caption. Every fist held now must open before the next change.
        /// </summary>
        void ChangeWeather(Vector3 at, MusicFrame music, Camera view)
        {
            snowing = !snowing;
            changeAge = 0f;
            for (int side = 0; side < 2; side++)
            {
                var hand = Rig != null ? Rig.Get(side == 0) : null;
                if (held[side] > 0f || (hand != null && hand.IsTracked && hand.Fist >= 0.55f)) spent[side] = true;
                held[side] = slack[side] = 0f;
                HideCharge(side);
            }
            var colour = snowing ? SnowColor : RainColor;
            var toEye = (view.transform.position - at).normalized;
            AddRipple(at, toEye, 0.5f, 0.7f, colour, 1f);
            AddRipple(at, toEye, 0.8f, 1f, colour, 0.5f, 0.12f);
            for (int i = 0; i < 40; i++)
            {
                var dir = Random.onUnitSphere;
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at + dir * 0.05f,
                    velocity = dir * (snowing ? 0.35f : 0.8f) + Vector3.up * 0.1f,
                    startColor = Color.Lerp(colour, Color.white, Random.value * 0.5f),
                    startSize = Random.Range(0.007f, 0.015f),
                    startLifetime = Random.Range(0.6f, 1.3f)
                }, 1);
            }
            flash = Mathf.Max(flash, 0.15f);
            ShowCaption(snowing ? "Snow" : "Rain", at, colour);
            voices.Play(Gust(), at, 0.5f);
            Musical.Play(voices, music, snowing ? 79 : 67, at, 0.3f);
            Acted("change weather");
        }

        void ShowCaption(string text, Vector3 at, Color colour)
        {
            if (caption == null) return;
            caption.Text = text;
            captionColor = Color.Lerp(colour, Color.white, 0.3f);
            captionAt = at + Vector3.up * 0.13f;
            captionAge = 0f;
        }

        /// <summary>The caption rises a little over the fist that changed the weather, then fades.</summary>
        void StepCaption(Camera view, float dt)
        {
            if (caption == null) return;
            captionAge += dt;
            bool show = captionAge < CaptionSeconds;
            if (caption.GameObject.activeSelf != show) caption.GameObject.SetActive(show);
            if (!show) return;
            var at = captionAt + Vector3.up * (0.04f * captionAge / CaptionSeconds);
            caption.Transform.SetPositionAndRotation(at, WordLayer.Facing(at, view.transform.position));
            float alpha = Mathf.Clamp01(Mathf.Min(captionAge / 0.15f, (CaptionSeconds - captionAge) / 0.6f));
            caption.Color = new Color(captionColor.r, captionColor.g, captionColor.b, alpha);
        }

        /// <summary>Drops thrown up and out in a crown where something splashes.</summary>
        void Splash(Vector3 at, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                splash.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Vector3.up * 0.005f,
                    velocity = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (speed * Random.Range(0.4f, 0.8f)) + Vector3.up * (speed * Random.Range(0.8f, 1.3f)),
                    startColor = Color.Lerp(color, Color.white, Random.value * 0.4f),
                    startSize = Random.Range(0.004f, 0.009f),
                    startLifetime = Random.Range(0.3f, 0.6f)
                }, 1);
            }
        }

        /// <summary>Snow thrown up, drifting back down slowly.</summary>
        void Puff(Vector3 at, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                var v = Random.insideUnitSphere * speed;
                v.y = Mathf.Abs(v.y) + speed * 0.4f;
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = v,
                    startColor = SnowColor,
                    startSize = Random.Range(0.008f, 0.016f),
                    startLifetime = Random.Range(0.8f, 1.6f)
                }, 1);
            }
        }

        /// <summary>A few sparkles where something bright lands.</summary>
        void Glint(Vector3 at, Color color, int count)
        {
            for (int i = 0; i < count; i++)
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Vector3.up * 0.01f,
                    velocity = Random.insideUnitSphere * 0.12f + Vector3.up * 0.08f,
                    startColor = color,
                    startSize = Random.Range(0.006f, 0.012f),
                    startLifetime = Random.Range(0.4f, 0.8f)
                }, 1);
        }

        /// <summary>
        /// Draws everything that falls, lies fresh on the snow, rests on a palm or ripples, as quads in one mesh (one draw
        /// call). Flakes and dots turn to the eye; drops stretch along their fall; ripples lie on their surface.
        /// Anything that comes close to the eyes fades away.
        /// </summary>
        void DrawWeather(Camera view)
        {
            if (weatherMesh == null) return;
            quads = 0;
            var eye = view.transform.position;
            var right = view.transform.right;
            var up = view.transform.up;
            float fade = FadeIn;
            for (int i = 0; i < MaxFalling; i++)
            {
                ref var f = ref falls[i];
                if (!f.Active) continue;
                float near = Vector3.Distance(f.Position, eye);
                // Rain fades from further off than snow, so no streak flicks past right before your eyes.
                float alpha = (f.Morph > 0.5f ? Mathf.Clamp01((near - 0.25f) / 0.2f) : Mathf.Clamp01((near - 0.22f) / 0.12f))
                    * Mathf.Clamp01(f.Age / (f.Morph > 0.5f ? 0.1f : 0.4f)) * fade;
                // While it turns from the one into the other it dips out and in.
                alpha *= Mathf.Clamp01(Mathf.Abs(f.Morph - 0.5f) * 5f);
                if (alpha <= 0.01f) continue;
                bool melody = f.Midi >= 0;
                if (f.Morph < 0.5f)
                {
                    float size = (melody ? 0.06f : 0.03f) * f.Size;
                    Billboard(f.Position, right, up, size, melody ? MelodyFlakeColor : SnowColor, (melody ? 1f : 0.9f) * alpha, FlakeKind, f.Seed);
                }
                else
                {
                    var colour = melody ? Color.Lerp(RainWordColor, PitchColors[Mathf.Abs(f.Midi) % PitchColors.Length], 0.4f) : RainColor;
                    float speed = f.Velocity.magnitude;
                    var along = speed > 0.01f ? -f.Velocity / speed : Vector3.up;
                    float length = Mathf.Clamp(speed * 0.055f, 0.03f, 0.24f) * (melody ? 1.3f : 1f);
                    Streak(f.Position, along, length, (melody ? 0.024f : 0.012f) * f.Size, eye, colour, (melody ? 1f : 0.65f) * alpha);
                }
            }
            for (int i = 0; i < MaxSettled; i++)
            {
                ref var s = ref settled[i];
                if (s.Age >= s.Life) continue;
                float alpha = Mathf.Clamp01(s.Age / 0.15f) * Mathf.Clamp01((s.Life - s.Age) / 2f) * fade;
                Billboard(s.Position, right, up, 0.016f * s.Size, SnowColor, alpha * 0.8f, DotKind, 0f);
            }
            for (int i = 0; i < palmFlakes.Length; i++)
            {
                ref var p = ref palmFlakes[i];
                if (!p.Active) continue;
                float alpha = Mathf.Clamp01(p.Age / 0.1f) * Mathf.Clamp01((p.Life - p.Age) / 1.5f);
                Billboard(PalmFlakeAt(i / PalmFlakes, p), right, up, 0.02f * p.Size, SnowColor, alpha, DotKind, 0f);
            }
            for (int i = 0; i < MaxRipples; i++)
            {
                ref var r = ref ripples[i];
                if (r.Life <= 0f || r.Age >= r.Life) continue;

                if (r.Age < 0f) continue;
                float u = Mathf.Clamp01(r.Age / r.Life);
                Ring(r.Position, r.Normal, r.Size, r.Color, r.Alpha * Mathf.Pow(1f - u, 1.5f) * fade, Mathf.Lerp(0.08f, 0.95f, 1f - Mathf.Pow(1f - u, 3f)));
            }
            weatherRenderer.enabled = quads > 0;
            if (quads == 0) return;
            var bounds = new Bounds(eye, new Vector3(weatherReach * 2f + 2f, 6f, weatherReach * 2f + 2f));
            weatherMesh.SetVertexBufferData(weatherVertices, 0, 0, quads * 4, 0, NoChecks);
            weatherMesh.SetSubMesh(0, new SubMeshDescriptor(0, quads * 6) { firstVertex = 0, vertexCount = quads * 4, bounds = bounds }, NoChecks);
            weatherMesh.bounds = bounds;
        }

        /// <summary>A quad turned to the eye (flake or dot).</summary>
        void Billboard(Vector3 at, Vector3 right, Vector3 up, float size, Color color, float alpha, float kind, float seed)
        {
            var r = right * (size * 0.5f);
            var u = up * (size * 0.5f);
            Quad(at - r - u, at - r + u, at + r + u, at + r - u, color, alpha, kind, seed);
        }

        /// <summary>A drop's streak: its head at <paramref name="at"/>, its tail back along <paramref name="along"/>, turned round that line to the eye.</summary>
        void Streak(Vector3 at, Vector3 along, float length, float width, Vector3 eye, Color color, float alpha)
        {
            var side = Vector3.Cross(along, eye - at);
            float m = side.magnitude;
            if (m < 1e-5f) return;
            side *= width * 0.5f / m;
            var tail = at + along * length;
            Quad(at - side, tail - side, tail + side, at + side, color, alpha, DropKind, 0f);
        }

        /// <summary>A ripple lying on its surface (or facing the way <paramref name="normal"/> points), its ring at <paramref name="radius"/> (0-1).</summary>
        void Ring(Vector3 at, Vector3 normal, float size, Color color, float alpha, float radius)
        {
            var a = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized * (size * 0.5f);
            var b = Vector3.Cross(normal, a);
            Quad(at - a - b, at - a + b, at + a + b, at + a - b, color, alpha, RingKind, radius);
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float alpha, float kind, float param)
        {
            if (quads >= MaxQuads) return;
            var tint = new Color32((byte)(Mathf.Clamp01(color.r) * 255f), (byte)(Mathf.Clamp01(color.g) * 255f), (byte)(Mathf.Clamp01(color.b) * 255f), (byte)(Mathf.Clamp01(alpha) * 255f));
            var shape = new Vector2(kind, param);
            int v = quads * 4;
            weatherVertices[v] = new WeatherVertex { Position = a, Color = tint, Uv = new Vector2(0f, 0f), Shape = shape };
            weatherVertices[v + 1] = new WeatherVertex { Position = b, Color = tint, Uv = new Vector2(0f, 1f), Shape = shape };
            weatherVertices[v + 2] = new WeatherVertex { Position = c, Color = tint, Uv = new Vector2(1f, 1f), Shape = shape };
            weatherVertices[v + 3] = new WeatherVertex { Position = d, Color = tint, Uv = new Vector2(1f, 0f), Shape = shape };
            quads++;
        }

        /// <summary>
        /// For previews: the weather set at once to snow or rain, as if it had been falling for
        /// <paramref name="seconds"/> (the ground filled in as it would be by then).
        /// </summary>
        public void PreviewWeather(bool snow, float seconds = 0f)
        {
            snowing = snow;
            changeAge = 99f;
            ClearWeather();
            previewSeconds = seconds;
        }

        float previewSeconds = -1f;

        /// <summary>For previews: the ground as so many seconds of this weather would leave it (on the next step, once placed).</summary>
        void Prefill()
        {
            int count = Mathf.RoundToInt(previewSeconds * (snowing ? snowRate.x + snowRate.y * 0.5f : rainRate.x + rainRate.y * 0.5f));
            previewSeconds = -1f;
            for (int i = 0; i < count; i++)
            {
                if (!Spot(headRoom, weatherReach, out var at)) continue;
                at.y = Room.GroundBelow(new Vector3(at.x, spawnTop, at.z), floorY);
                AddGround(at, snowing ? 0.32f : 0f, snowing ? 0f : 0.4f);
            }
        }

        // The sound of a change, made once.
        static AudioClip gust;

        /// <summary>A gust of wind: noise swelling and falling away over a second and a half, its pitch rising then sinking.</summary>
        static AudioClip Gust()
        {
            if (gust != null) return gust;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int length = (int)(rate * 1.5f);
            var data = new float[length];
            var random = new System.Random(11);
            float low = 0f, band = 0f, peak = 1e-4f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float white = (float)(random.NextDouble() * 2 - 1);
                // A filter that opens and closes with the gust.
                float open = 0.02f + 0.12f * Mathf.Sin(t * Mathf.PI);
                low += (white - low) * open;
                band += (low - band) * open * 0.5f;
                float env = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 2f);
                data[i] = (low - band) * env;
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            for (int i = 0; i < length; i++) data[i] *= 0.6f / peak;
            gust = AudioClip.Create("gust", length, 1, rate, false);
            gust.SetData(data, 0);
            return gust;
        }
    }
}
