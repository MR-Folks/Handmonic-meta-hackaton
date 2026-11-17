using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The cassette deck: choosing a song by hand. The menu's song bubble opens it, and a small table rises out of the
    /// floor in front of you, within reach whether you sit or stand. On its left a round turntable carries one
    /// cassette per song, standing round its rim with their labels facing out: the cassette nearest you is the front
    /// one, lit up with its title floating above it, and the rest go round into the distance. It turns like a heavy,
    /// well-oiled turntable (DeckRing): pinch its edge and it follows the hand closely, and spins on at the hand's speed
    /// when let go; slide an open hand across the cassettes and it is pulled softly along, a quick slide flicking it
    /// into a spin. It glides for a few seconds, ticking as each cassette passes, and settles with one at the front,
    /// unless a hand put back into it catches it first. On the right stands a radio with its cassette door open. Pinch
    /// a cassette (or close your hand round it) to pick it up (taking one off a spinning turntable catches it too);
    /// bring it to the door and a glowing outline shows where it will go; let go and it drops in, the door shuts with
    /// a clunk, the light comes on and the song plays while the reels turn and the speakers pump with the bass. Let go
    /// anywhere else and it floats home. Putting in another pops the first one out, back to its place. The radio's
    /// keys play or pause, stop and eject, pressed with a fingertip like the menu's buttons. A blank cassette at the
    /// end of the ring adds a song from the headset (the file picker), and a button on the table opens the song list
    /// for lyrics, removing songs and Audius credits. Close it with the table's close button or Meta's menu gesture;
    /// the world plays on behind it the whole time.
    /// Everything is built here from code and Resources (DeckParts); the world menu keeps the turntable's tuning.
    /// </summary>
    public sealed partial class CassetteDeck
    {
        // ---- Tuning ----
        /// <summary>The rim length a full-size cassette needs, the gap to the next included (m).</summary>
        const float Pitch = 0.114f;
        /// <summary>How far ahead of your eyes the front cassette stands, measured along the floor (m).</summary>
        const float FrontDistance = 0.41f;
        /// <summary>How far a held cassette may be from the radio's door for it to snap in, and to stay ready to (m).</summary>
        const float SnapRange = 0.1f, SnapKeep = 0.12f;
        /// <summary>How far the door tilts open (degrees, top towards you).</summary>
        const float DoorAngle = 30f;
        /// <summary>How far a cassette leans back on the turntable, so its label tips up towards your eyes (degrees).</summary>
        const float Lean = 9f;
        const int MaxCassettes = 24;
        const float RiseSeconds = 0.6f, SinkSeconds = 0.4f;
        /// <summary>
        /// Where the words over the turntable stand: this far from its middle towards the radio, to keep them in the
        /// narrow view, and this far back, away from you (m).
        /// </summary>
        const float PlateShift = 0.11f, PlateBack = 0.03f;

        enum Place { Ring, Held, Returning, Inserting, Inserted, Ejecting }

        /// <summary>One cassette: a song (or the blank), its parts, and where it is.</summary>
        sealed class Cassette
        {
            public Transform Root;
            public Renderer Shell, Label;
            public Renderer[] Inside;
            public Transform ReelLeft, ReelRight;
            public Quaternion ReelLeftRest, ReelRightRest;
            public TypeLabel Line1, Line2;
            public SongAsset Song;
            public bool Blank, Used;
            public string ShownTitle;
            public bool ShownLyrics, ShownAway = true;
            public Color Color;
            public Place Place;
            public bool PlayOnSeat;
            public float T, Duration;
            public Vector3 FromPosition;
            public Quaternion FromRotation;
            public float FromScale, Scale = 1f;
            public Vector3 GrabLocal;
            public bool InRange;
            public float Hover, Flash, LastGlow = -1f, Reel;
            // Its place on the ring this frame, in the table's space.
            public Vector3 RingPosition;
            public Quaternion RingRotation;
            public float RingScale = 1f, RingAngle, Appear;
        }

        // Built once.
        readonly Transform root, rise, onTable, tableModel, legsStretch, topLift;
        readonly Transform turntable, platter, radio, door, slot;
        readonly Transform speakerLeft, speakerRight;
        readonly Renderer doorRenderer, light;
        readonly Quaternion platterRest, doorRest;
        readonly Vector3 speakerLeftRest, speakerRightRest;
        readonly Renderer ghost;
        readonly Transform ghostRoot;
        readonly AudioPool voices, ticks;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly List<Cassette> pool = new List<Cassette>();
        readonly List<Cassette> ring = new List<Cassette>();
        readonly Cassette blank;
        readonly DeckRing turning;
        readonly ParticleSystem sparks;

        // Measured from the models.
        readonly float modelTop, rimRadius, rimHeight, platterRadius, platterTop;
        readonly Vector3 cassetteSize, labelCenter, labelSize;
        readonly Vector3 turntableAt, radioAt;
        readonly Vector2 headAt, front, frontRight;
        readonly float facing;

        // State.
        Vector3 head;
        float floorY, top, lift = 1f, openClock, sinkClock;
        bool visible, closing;
        Cassette inserted;
        SongAsset lastBusSong;
        bool busKnown;
        float doorOpen, reelSpeed, lightGlow, blankWait = -1f, noPickerUntil = -1f;
        bool doorWanted = true, faced;
        int lastFront = -1;
        float clock;
        int roomVersion;
        IReadOnlyList<SongAsset> previewSongs;
        SongAsset previewPlaying;
        float[] previewBands;
        float previewKick;

        /// <summary>True from opening until it is closed (it then sinks back into the floor).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Raised by the table's song list button: show the song page (lyrics, removing songs, credits).</summary>
        public event Action SongListRequested;

        /// <summary>Raised when the blank cassette has opened the headset's file picker; the deck has stepped aside.</summary>
        public event Action PickerOpened;

        /// <summary>Builds the deck, hidden, under <paramref name="parent"/>; <paramref name="feel"/> is how its turntable turns.</summary>
        public CassetteDeck(Transform parent, Material glowMaterial, DeckRing.Tuning feel = null)
        {
            turning = new DeckRing(feel);
            root = new GameObject("Cassette Deck").transform;
            root.SetParent(parent, false);
            rise = new GameObject("Rise").transform;
            rise.SetParent(root, false);

            // The table. Its legs stretch and everything on it lifts when you stand, so it meets your hands.
            tableModel = DeckParts.Make(DeckParts.Table, rise).transform;
            legsStretch = new GameObject("Legs Stretch").transform;
            legsStretch.SetParent(tableModel, false);
            topLift = new GameObject("Top Lift").transform;
            topLift.SetParent(tableModel, false);
            var legs = DeckParts.Find(tableModel, "Legs");
            var tableTop = DeckParts.Find(tableModel, "Top");
            var surface = DeckParts.Find(tableModel, "TopSurface");
            if (legs != null) legs.SetParent(legsStretch, true);
            if (tableTop != null) tableTop.SetParent(topLift, true);
            if (surface != null) surface.SetParent(topLift, true);
            modelTop = surface != null ? tableModel.InverseTransformPoint(surface.position).y : 0.68f;
            var topBounds = tableTop != null ? DeckParts.LocalBounds(tableModel, tableTop) : new Bounds(new Vector3(0f, modelTop, 0f), new Vector3(0.9f, 0.03f, 0.55f));
            float halfWidth = topBounds.extents.x, halfDepth = topBounds.extents.z;

            onTable = new GameObject("On Table").transform;
            onTable.SetParent(rise, false);

            // The turntable at the left of the table, as far back as it fits.
            turntable = DeckParts.Make(DeckParts.Turntable, onTable).transform;
            platter = DeckParts.Find(turntable, "Platter");
            platterRest = platter != null ? platter.localRotation : Quaternion.identity;
            var baseBounds = DeckParts.ModelBounds(turntable);
            float turntableRadius = Mathf.Max(baseBounds.extents.x, baseBounds.extents.z);
            var platterBounds = platter != null ? DeckParts.LocalBounds(turntable, platter) : baseBounds;
            platterRadius = Mathf.Max(platterBounds.extents.x, 0.05f);
            platterTop = platterBounds.max.y;
            var rimMark = DeckParts.Find(turntable, "RimHeight");
            var rimLocal = rimMark != null ? turntable.InverseTransformPoint(rimMark.position) : new Vector3(0f, 0.03f, -0.23f);
            rimRadius = new Vector2(rimLocal.x, rimLocal.z).magnitude;
            rimHeight = rimLocal.y;
            turntableAt = new Vector3(-halfWidth + turntableRadius, modelTop, Mathf.Min(0.01f, halfDepth - turntableRadius));
            turntable.localPosition = turntableAt;

            // The radio at the front right corner, clear of the turntable.
            radio = DeckParts.Make(DeckParts.Radio, onTable).transform;
            var body = DeckParts.Find(radio, "Body");
            var radioBounds = body != null ? DeckParts.LocalBounds(radio, body) : DeckParts.ModelBounds(radio);
            radioAt = new Vector3(halfWidth - radioBounds.max.x, modelTop, -halfDepth - radioBounds.min.z + 0.008f);
            radio.localPosition = radioAt;
            door = DeckParts.Find(radio, "Door");
            doorRest = door != null ? door.localRotation : Quaternion.identity;
            doorRenderer = door != null ? door.GetComponent<Renderer>() : null;
            slot = DeckParts.Find(radio, "Slot");
            if (slot == null)
            {
                slot = new GameObject("Slot").transform;
                slot.SetParent(radio, false);
                slot.localPosition = new Vector3(0f, radioBounds.center.y, radioBounds.min.z + 0.015f);
            }
            speakerLeft = DeckParts.Find(radio, "SpeakerLeft");
            speakerRight = DeckParts.Find(radio, "SpeakerRight");
            speakerLeftRest = speakerLeft != null ? speakerLeft.localScale : Vector3.one;
            speakerRightRest = speakerRight != null ? speakerRight.localScale : Vector3.one;
            var lightPart = DeckParts.Find(radio, "Light");
            light = lightPart != null ? lightPart.GetComponent<Renderer>() : null;
            var radioTop = DeckParts.ModelBounds(radio).max.y;

            // Measure a cassette from the first one.
            blank = MakeCassette();
            blank.Blank = true;
            var shell = blank.Shell != null ? DeckParts.LocalBounds(blank.Root, blank.Shell.transform) : new Bounds(Vector3.zero, new Vector3(0.1f, 0.064f, 0.012f));
            cassetteSize = shell.size;
            var label = blank.Label != null ? DeckParts.LocalBounds(blank.Root, blank.Label.transform) : new Bounds(new Vector3(0f, 0.0185f, -0.0062f), new Vector3(0.09f, 0.021f, 0f));
            labelCenter = new Vector3(label.center.x, label.center.y, label.min.z - 0.0004f);
            labelSize = label.size;
            FitLabelText(blank);

            // The snap preview: a glowing outline of a cassette in the radio's slot.
            ghostRoot = new GameObject("Snap Preview").transform;
            ghostRoot.SetParent(slot, false);
            if (blank.Shell != null)
            {
                var g = new GameObject("Ghost");
                g.transform.SetParent(ghostRoot, false);
                g.transform.localPosition = blank.Shell.transform.localPosition;
                g.transform.localRotation = blank.Shell.transform.localRotation;
                g.transform.localScale = blank.Shell.transform.localScale * 1.04f;
                g.AddComponent<MeshFilter>().sharedMesh = blank.Shell.GetComponent<MeshFilter>().sharedMesh;
                ghost = g.AddComponent<MeshRenderer>();
                ghost.sharedMaterial = DeckParts.Material("Ghost");
                ghost.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ghost.receiveShadows = false;
            }
            ghostRoot.gameObject.SetActive(false);

            // Where you sit or stand: the front cassette about 41 cm ahead and everything you touch close to the
            // middle of your view.
            BuildKeys(radioBounds, halfDepth);
            FindSeat(out headAt, out facing);
            front = (headAt - new Vector2(turntableAt.x, turntableAt.z)).normalized;
            frontRight = new Vector2(-front.y, front.x);
            // Unity turns clockwise seen from above; "right" here is the player's right, looking from the seat.
            if (Vector2.Dot(frontRight, Vector2.right) < 0f) frontRight = -frontRight;

            BuildLook(glowMaterial, halfWidth, halfDepth, turntableRadius, radioBounds, radioTop);
            sparks = Effects.MakeSparkSystem("Sparks", radio, glowMaterial != null ? glowMaterial : DeckParts.Material("DeckGlow"));
            var main = sparks.main;
            main.maxParticles = 80;
            voices = new AudioPool(root, 4);
            // The turntable's ticks have voices of their own, so a long spin never moves or retunes the deck's other sounds.
            ticks = new AudioPool(root, 2);
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Picks the seat: the point in front of the table, measured along the floor, from which the front cassette
        /// is about 41 cm away and the front cassette, the radio's door and keys and the table's buttons sit closest together in
        /// your view. <paramref name="bisector"/> is the direction (degrees) that splits them evenly, which the deck
        /// turns to face along your gaze.
        /// </summary>
        void FindSeat(out Vector2 seat, out float bisector)
        {
            var c = new Vector2(turntableAt.x, turntableAt.z);
            var s = new Vector2(radioAt.x + slot.localPosition.x, radioAt.z + slot.localPosition.z);
            seat = new Vector2(0f, -0.6f);
            bisector = 0f;
            float best = float.MaxValue;
            for (float z = -0.95f; z <= -0.35f; z += 0.005f)
                for (float x = -0.25f; x <= 0.25f; x += 0.005f)
                {
                    var h = new Vector2(x, z);
                    var toCenter = c - h;
                    float reach = toCenter.magnitude - rimRadius;
                    if (Mathf.Abs(reach - FrontDistance) > 0.03f) continue;
                    var f = c - toCenter.normalized * rimRadius;
                    float a = Bearing(f - h), b = Bearing(s - h);
                    float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
                    for (int k = 0; k < keys.Count; k++)
                    {
                        float p = Bearing(keys[k].TableXZ - h);
                        lo = Mathf.Min(lo, p);
                        hi = Mathf.Max(hi, p);
                    }
                    float cost = hi - lo + 60f * Mathf.Max(0f, (s - h).magnitude - 0.47f) + 30f * Mathf.Abs(reach - FrontDistance);
                    if (cost >= best) continue;
                    best = cost;
                    seat = h;
                    bisector = (lo + hi) * 0.5f;
                }
        }

        static float Bearing(Vector2 d) => Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;

        Cassette MakeCassette()
        {
            var go = DeckParts.Make(DeckParts.Cassette, onTable);
            var c = new Cassette { Root = go.transform };
            var shell = DeckParts.Find(c.Root, "Shell");
            var label = DeckParts.Find(c.Root, "Label");
            c.Shell = shell != null ? shell.GetComponent<Renderer>() : null;
            c.Label = label != null ? label.GetComponent<Renderer>() : null;
            c.ReelLeft = DeckParts.Find(c.Root, "ReelLeft");
            c.ReelRight = DeckParts.Find(c.Root, "ReelRight");
            c.ReelLeftRest = c.ReelLeft != null ? c.ReelLeft.localRotation : Quaternion.identity;
            c.ReelRightRest = c.ReelRight != null ? c.ReelRight.localRotation : Quaternion.identity;
            var inside = new List<Renderer>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (r != c.Shell) inside.Add(r);
            c.Line1 = new TypeLabel("Title", c.Root, 0.01f);
            c.Line2 = new TypeLabel("Title 2", c.Root, 0.01f);
            c.Line1.Color = c.Line2.Color = Ink;
            inside.Add(c.Line1.GameObject.GetComponent<Renderer>());
            inside.Add(c.Line2.GameObject.GetComponent<Renderer>());
            c.Inside = inside.ToArray();
            pool.Add(c);
            return c;
        }

        // ---- Opening and closing ----

        /// <summary>Opens the deck in front of the viewer: the table rises out of the floor.</summary>
        public void Open(Camera view, MusicBus bus)
        {
            if (view == null) return;
            this.bus = bus;
            bool wasVisible = visible;
            IsOpen = true;
            closing = false;
            visible = true;
            openClock = wasVisible ? Mathf.Max(openClock, RiseSeconds * (1f - sinkClock / SinkSeconds)) : 0f;
            sinkClock = 0f;
            StandAt(view);
            ResetHands();
            ReadSongs(bus);
            SyncRadio(bus, true);
            if (!wasVisible)
                for (int i = 0; i < ring.Count; i++) ring[i].Appear = 0f;
            // The first time, the song after the one playing waits at the front; later it is where you left it.
            if (!faced)
            {
                faced = true;
                FaceNext(bus);
            }
            root.gameObject.SetActive(true);
            voices.Play(Synth.Soft(Synth.Pentatonic[2]), root.TransformPoint(new Vector3(0f, top, 0f)), 0.35f);
        }

        /// <summary>Closes the deck: it sinks back into the floor, and a cassette in a hand goes home.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closing = true;
            sinkClock = 0f;
            // Whatever was moving finishes at once: a cassette in a hand or in the air is back in its place, and one
            // on its way into the radio is in (and plays).
            foreach (var h in hands)
            {
                h.Holding = h.Hovered = null;
                h.Dragging = h.Spun = false;
            }
            turning.Release(false);
            foreach (var c in pool)
            {
                if (c.Place == Place.Ejecting || c.Place == Place.Returning || c.Place == Place.Held) c.Place = Place.Ring;
                if (c.Place == Place.Inserting) Seat(c, false);
            }
            blankWait = -1f;
            if (inserted != null && inserted.Blank)
            {
                inserted.Place = Place.Ring;
                inserted = null;
            }
            ghostRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Stands the table on the floor with the seat (FindSeat) where your head is and the deck turned to your gaze.
        /// Seated, the top is 68 cm high; standing, the table grows to about 98 cm so it stays within easy reach.
        /// </summary>
        void StandAt(Camera view)
        {
            var eyes = view.transform.position;
            floorY = Effects.FloorY(view);
            roomVersion = Room.Version;
            float eyeHeight = eyes.y - floorY;
            top = modelTop + Mathf.Clamp01((eyeHeight - 1.3f) / 0.35f) * 0.3f;
            legsStretch.localScale = new Vector3(1f, top / Mathf.Max(modelTop, 0.1f), 1f);
            topLift.localPosition = new Vector3(0f, top - modelTop, 0f);
            onTable.localPosition = new Vector3(0f, top - modelTop, 0f);

            var forward = Effects.FlatForward(view);
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg - facing;
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var position = new Vector3(eyes.x, floorY, eyes.z) - rotation * new Vector3(headAt.x, 0f, headAt.y);
            root.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Turns the turntable so the song after the one playing is at the front, ready to pick.</summary>
        void FaceNext(MusicBus bus)
        {
            var playing = PlayingSong(bus);
            int index = 0;
            for (int i = 0; i < ring.Count; i++)
                if (playing != null && ring[i].Song == playing) index = i + 1;
            turning.Face(turning.Wrap(index));
            lastFront = turning.Front;
        }

        // ---- One frame ----

        /// <summary>One frame of the deck. Public so the menu (and the editor checks) can drive it.</summary>
        public void Step(HandRig rig, MusicBus bus, Camera view, float dt)
        {
            if (!visible) return;
            this.bus = bus;
            clock += dt;
            if (closing)
            {
                sinkClock += dt;
                if (sinkClock >= SinkSeconds)
                {
                    visible = closing = false;
                    root.gameObject.SetActive(false);
                    Shader.SetGlobalVector(ClipId, Vector4.zero);
                    return;
                }
            }
            else openClock += dt;
            if (view != null && IsOpen && Room.Version != roomVersion) StandAt(view);
            float up = closing ? 1f - Cube(sinkClock / SinkSeconds) : 1f - Cube(1f - Mathf.Clamp01(openClock / RiseSeconds));
            lift = up;
            rise.localPosition = new Vector3(0f, -(1f - up) * (top + 0.3f), 0f);
            Shader.SetGlobalVector(ClipId, new Vector4(floorY + 0.0005f, 1f, 0f, 0f));
            if (view != null) head = view.transform.position;

            ReadSongs(bus);
            if (IsOpen) SyncRadio(bus, false);
            bool ready = IsOpen && openClock > RiseSeconds * 0.8f;
            StepHands(rig, ready, dt);
            StepKeys(rig, ready, dt);
            turning.Step(dt);
            if (turning.Front != lastFront && turning.Count > 0)
            {
                // A tick as each cassette passes the front, a little softer the faster it spins.
                float tick = Mathf.Lerp(0.35f, 0.2f, Mathf.Abs(turning.Velocity) / turning.Feel.TopSpeed);
                if (lastFront >= 0 && ready) ticks.Play(DeckSounds.Tick, CassettePoint(turning.Front), tick, 0.9f + 0.03f * (turning.Front % 7));
                lastFront = turning.Front;
            }
            StepCassettes(bus, dt);
            StepRadio(bus, dt);
            StepBlank(dt);
            StepLook(bus, view, dt);
        }

        static float Cube(float x) => x * x * x;

        static readonly int ClipId = Shader.PropertyToID("_CassetteDeckClip");

        // ---- Songs ----

        /// <summary>The songs on the turntable: the playlist, or the made-up songs of an editor screenshot.</summary>
        IReadOnlyList<SongAsset> Songs(MusicBus bus) => previewSongs ?? (bus != null ? bus.Songs : null);

        /// <summary>The song the radio should hold: playing, paused or on its way, or none while listening to the room.</summary>
        SongAsset PlayingSong(MusicBus bus)
        {
            if (previewSongs != null) return previewPlaying;
            if (bus == null || bus.IsListening) return null;
            return bus.Loading != null ? bus.Loading : bus.CurrentSong;
        }

        bool IsPlaying(MusicBus bus) => previewSongs != null ? previewPlaying != null : bus != null && !bus.IsListening && bus.IsPlaying;

        /// <summary>
        /// Keeps one cassette per song, with the blank at the end, and the same cassette at the front when songs come or
        /// go (Audius's songs arriving, a song added or removed). Cheap when nothing changed.
        /// </summary>
        void ReadSongs(MusicBus bus)
        {
            var songs = Songs(bus);
            int count = songs != null ? Mathf.Min(songs.Count, MaxCassettes - 1) : 0;
            bool same = ring.Count == count + 1;
            for (int i = 0; same && i < count; i++) same = ring[i].Song == songs[i] && !ring[i].Blank;
            if (same) return;

            var oldFront = ring.Count > 0 && turning.Front >= 0 && turning.Front < ring.Count ? ring[turning.Front] : null;
            foreach (var c in pool) c.Used = c.Blank;
            ring.Clear();
            for (int i = 0; i < count; i++)
            {
                var song = songs[i];
                Cassette found = null;
                foreach (var c in pool)
                    if (!c.Used && !c.Blank && c.Song == song) { found = c; break; }
                if (found == null)
                    foreach (var c in pool)
                        if (!c.Used && !c.Blank && (c.Song == null || !Contains(songs, c.Song))) { found = c; break; }
                if (found == null) found = MakeCassette();
                if (found.Song != song)
                {
                    found.Song = song;
                    found.ShownTitle = null;
                    found.Place = Place.Ring;
                    found.Appear = 1f;
                    if (inserted == found) inserted = null;
                }
                found.Used = true;
                ring.Add(found);
            }
            ring.Add(blank);
            foreach (var c in pool)
            {
                if (c.Used) continue;
                c.Song = null;
                c.Place = Place.Ring;
                if (inserted == c) inserted = null;
                foreach (var h in hands)
                    if (h.Holding == c) h.Holding = null;
                if (c.Root.gameObject.activeSelf) c.Root.gameObject.SetActive(false);
            }
            turning.Layout(ring.Count, Mathf.PI * 2f * rimRadius, Pitch);
            int keep = oldFront != null ? ring.IndexOf(oldFront) : -1;
            if (keep >= 0) turning.Renumber(keep);
            else turning.Face(Mathf.Clamp(turning.Front, 0, ring.Count - 1));
            lastFront = turning.Front;
        }

        static bool Contains(IReadOnlyList<SongAsset> songs, SongAsset song)
        {
            for (int i = 0; i < songs.Count; i++)
                if (songs[i] == song) return true;
            return false;
        }

        Cassette CassetteOf(SongAsset song)
        {
            if (song == null) return null;
            for (int i = 0; i < ring.Count; i++)
                if (ring[i].Song == song) return ring[i];
            return null;
        }

        /// <summary>
        /// Follows the music: when the song changes by other means (a song added on the page, Audius failing to stream,
        /// listening to the room), the radio swaps cassettes to match. With <paramref name="instant"/> (opening) the
        /// right cassette is simply in the radio.
        /// </summary>
        void SyncRadio(MusicBus bus, bool instant)
        {
            if (bus == null && previewSongs == null) return;
            var song = PlayingSong(bus);
            if (busKnown && song == lastBusSong && !instant) return;
            busKnown = true;
            lastBusSong = song;
            var going = Going();
            var holder = going ?? inserted;
            if (holder != null && holder.Blank) return;
            if (holder != null && holder.Song == song && !instant) return;
            var want = CassetteOf(song);
            if (instant)
            {
                if (inserted != null && inserted != want) inserted.Place = Place.Ring;
                inserted = null;
                if (want != null && want.Place != Place.Held)
                {
                    want.PlayOnSeat = false;
                    Seat(want, false);
                    doorOpen = 0f;
                    doorWanted = false;
                }
                return;
            }
            if (inserted != null && inserted != want) Eject(inserted);
            if (want != null && want.Place != Place.Held && want != inserted) Insert(want, false);
        }

        /// <summary>The cassette on its way into the radio, if one is.</summary>
        Cassette Going()
        {
            foreach (var c in pool)
                if (c.Place == Place.Inserting) return c;
            return null;
        }

        // ---- What the cassettes do ----

        /// <summary>Starts a cassette into the radio, popping out the one there.</summary>
        void Insert(Cassette c, bool play)
        {
            var going = Going();
            if (going != null && going != c) Return(going);
            if (inserted != null && inserted != c) Eject(inserted);
            c.Place = Place.Inserting;
            c.PlayOnSeat = play;
            c.T = 0f;
            c.FromPosition = c.Root.position;
            c.FromRotation = c.Root.rotation;
            c.FromScale = c.Scale;
            c.Duration = Mathf.Clamp(Vector3.Distance(c.FromPosition, slot.position) / 0.9f, 0.22f, 0.55f);
            c.InRange = false;
        }

        /// <summary>A cassette arrives in the slot: the clunk, a puff of sparks, and its song plays.</summary>
        void Seat(Cassette c, bool feedback)
        {
            c.Place = Place.Inserted;
            c.Scale = 1f;
            c.Root.SetPositionAndRotation(slot.position, slot.rotation);
            c.Root.localScale = Vector3.one;
            inserted = c;
            bool play = c.PlayOnSeat;
            c.PlayOnSeat = false;
            if (feedback)
            {
                if (!c.Blank) learned = true;
                voices.Play(DeckSounds.Clunk, slot.position, 0.8f);
                c.Flash = 1f;
                EmitSparks(c.Blank ? new Color(1f, 0.55f, 0.45f) : c.Color);
            }
            if (c.Blank)
            {
                if (!feedback) return;
                blankWait = 0.55f;
                voices.Play(DeckSounds.Whirr, slot.position, 0.5f);
                return;
            }
            if (play) PlaySong(c.Song);
        }

        void PlaySong(SongAsset song)
        {
            if (song == null) return;
            if (previewSongs != null)
            {
                previewPlaying = song;
                lastBusSong = song;
                return;
            }
            if (bus == null) return;
            if (PlayingSong(bus) == song)
            {
                if (!bus.IsPlaying) bus.TogglePause();
            }
            else bus.Play(song);
            lastBusSong = PlayingSong(bus);
        }

        /// <summary>Pops a cassette out of the radio and sends it home to the turntable.</summary>
        void Eject(Cassette c)
        {
            if (c == null) return;
            if (inserted == c) inserted = null;
            c.Place = Place.Ejecting;
            c.T = 0f;
            c.FromPosition = c.Root.position;
            c.FromRotation = c.Root.rotation;
            c.FromScale = c.Scale;
        }

        /// <summary>Sends a cassette back to its place on the turntable, in a gentle arc.</summary>
        void Return(Cassette c)
        {
            c.Place = Place.Returning;
            c.T = 0f;
            c.FromPosition = c.Root.position;
            c.FromRotation = c.Root.rotation;
            c.FromScale = c.Scale;
            c.Duration = Mathf.Clamp(Vector3.Distance(c.FromPosition, CassetteHome(c)) / 0.7f, 0.35f, 0.75f);
            voices.Play(DeckSounds.Whoosh, c.FromPosition, 0.4f);
        }

        Vector3 CassetteHome(Cassette c) => onTable.TransformPoint(c.RingPosition);

        /// <summary>The blank cassette, once in: opens the file picker on the headset, or says it only opens there.</summary>
        void StepBlank(float dt)
        {
            if (blankWait < 0f) return;
            blankWait -= dt;
            if (blankWait > 0f) return;
            blankWait = -1f;
            if (inserted != blank) return;
            if (SongPicker.Open(SongPicker.Kind.Song, null))
            {
                Debug.Log("[Deck] The blank cassette opened the file picker.");
                PickerOpened?.Invoke();
                return;
            }
            noPickerUntil = clock + 4f;
            Eject(blank);
            // The song that was playing goes back in, since it is still playing.
            busKnown = false;
        }

        /// <summary>One frame of every cassette: on the ring, in a hand, or on its way somewhere.</summary>
        void StepCassettes(MusicBus bus, float dt)
        {
            // The platter turns with the cassettes at the front, where the hand is (further round, when there are
            // many, the cassettes crowd closer together than the platter turns).
            if (platter != null) platter.localRotation = Quaternion.AngleAxis(turning.Scroll * turning.FrontPitch * Mathf.Rad2Deg, Vector3.up) * platterRest;
            for (int i = 0; i < ring.Count; i++)
            {
                var c = ring[i];
                if (!c.Root.gameObject.activeSelf) c.Root.gameObject.SetActive(true);
                float offset = turning.Offset(i);
                float angle = turning.Angle(offset);
                float scale = turning.Scale(offset);
                float frontness = Mathf.Clamp01(1f - Mathf.Abs(offset) * 1.4f);
                // Cassettes pop onto the turntable one after another as it rises, the front ones first.
                float delay = 0.35f + Mathf.Abs(offset) * 0.04f;
                c.Appear = Mathf.MoveTowards(c.Appear, IsOpen && openClock > delay ? 1f : c.Appear, dt * 4f);
                var dir = front * Mathf.Cos(angle) + frontRight * Mathf.Sin(angle);
                var dir3 = new Vector3(dir.x, 0f, dir.y);
                var rotation = Quaternion.LookRotation(-dir3, Vector3.up) * Quaternion.Euler(Lean + frontness * 5f, 0f, 0f);
                float size = scale * (1f + frontness * 0.05f) * EaseOutBack(c.Appear);
                var bottom = turntableAt + dir3 * rimRadius + Vector3.up * (rimHeight + frontness * 0.004f);
                c.RingPosition = bottom + rotation * new Vector3(0f, cassetteSize.y * 0.5f * size, 0f);
                c.RingRotation = rotation;
                c.RingScale = Mathf.Max(size, 0.001f);
                c.RingAngle = angle;
                c.Hover = Mathf.MoveTowards(c.Hover, HoveredBy(c) ? 1f : 0f, dt * 8f);
                c.Flash = Mathf.MoveTowards(c.Flash, 0f, dt * 2.5f);

                switch (c.Place)
                {
                    case Place.Ring:
                        c.Scale = c.RingScale;
                        c.Root.localPosition = c.RingPosition;
                        c.Root.localRotation = c.RingRotation;
                        c.Root.localScale = Vector3.one * c.Scale;
                        break;
                    case Place.Returning:
                        c.T += dt / Mathf.Max(c.Duration, 0.05f);
                        float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(c.T));
                        var home = CassetteHome(c);
                        var homeRotation = onTable.rotation * c.RingRotation;
                        c.Scale = Mathf.Lerp(c.FromScale, c.RingScale, e);
                        c.Root.SetPositionAndRotation(Vector3.Lerp(c.FromPosition, home, e) + Vector3.up * (Mathf.Sin(Mathf.PI * Mathf.Clamp01(c.T)) * 0.05f),
                            Quaternion.Slerp(c.FromRotation, homeRotation, e));
                        c.Root.localScale = Vector3.one * c.Scale;
                        if (c.T >= 1f) c.Place = Place.Ring;
                        break;
                    case Place.Inserting:
                        c.T += dt / Mathf.Max(c.Duration, 0.05f);
                        // In through the open door: to just in front of the slot, then down into it.
                        var entry = slot.position - radio.forward * 0.028f + radio.up * 0.012f;
                        float k = Mathf.Clamp01(c.T);
                        var position = k < 0.6f ? Vector3.Lerp(c.FromPosition, entry, Mathf.SmoothStep(0f, 1f, k / 0.6f))
                            : Vector3.Lerp(entry, slot.position, Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.4f));
                        c.Scale = Mathf.Lerp(c.FromScale, 1f, Mathf.SmoothStep(0f, 1f, k));
                        c.Root.SetPositionAndRotation(position, Quaternion.Slerp(c.FromRotation, slot.rotation, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 1.4f))));
                        c.Root.localScale = Vector3.one * c.Scale;
                        if (c.T >= 1f) Seat(c, true);
                        break;
                    case Place.Inserted:
                        c.Root.SetPositionAndRotation(slot.position, slot.rotation);
                        break;
                    case Place.Ejecting:
                        // Waits for the door, then pops up and out of it before floating home.
                        c.Root.SetPositionAndRotation(c.FromPosition, c.FromRotation);
                        if (doorOpen < 0.6f) break;
                        if (c.T == 0f) voices.Play(DeckSounds.Pop, slot.position, 0.7f);
                        c.T += dt / 0.2f;
                        var pop = slot.position - radio.forward * 0.07f + radio.up * 0.06f;
                        float p = 1f - (1f - Mathf.Clamp01(c.T)) * (1f - Mathf.Clamp01(c.T));
                        c.Root.SetPositionAndRotation(Vector3.Lerp(c.FromPosition, pop, p), c.FromRotation * Quaternion.Euler(-18f * p, 0f, 0f));
                        if (c.T >= 1f) Return(c);
                        break;
                }
            }
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        /// <summary>The radio: its door, its light, the reels of the cassette in it, and speakers that pump with the bass.</summary>
        void StepRadio(MusicBus bus, float dt)
        {
            bool heldNear = false;
            foreach (var h in hands)
                if (h.Holding != null && Vector3.Distance(h.Holding.Root.position, slot.position) < 0.3f) heldNear = true;
            bool moving = false;
            foreach (var c in pool)
                if (c.Place == Place.Inserting || c.Place == Place.Ejecting) moving = true;
            bool want = heldNear || moving || (inserted == null && IsOpen);
            if (want != doorWanted && lift > 0.9f) voices.Play(DeckSounds.Door, door != null ? door.position : slot.position, 0.45f);
            doorWanted = want;
            doorOpen = Mathf.MoveTowards(doorOpen, want ? 1f : 0f, dt / 0.22f);
            if (door != null) door.localRotation = doorRest * Quaternion.Euler(-DoorAngle * Mathf.SmoothStep(0f, 1f, doorOpen), 0f, 0f);

            bool playing = inserted != null && !inserted.Blank && inserted.Song == PlayingSong(bus) && IsPlaying(bus);
            bool loading = inserted != null && bus != null && previewSongs == null && bus.Loading == inserted.Song;
            reelSpeed = Mathf.MoveTowards(reelSpeed, playing ? 1.2f : loading || (inserted != null && inserted.Blank) ? 0.5f : 0f, dt * 2f);
            if (inserted != null && reelSpeed > 0f)
            {
                inserted.Reel += reelSpeed * 360f * dt;
                if (inserted.ReelLeft != null) inserted.ReelLeft.localRotation = Quaternion.AngleAxis(-inserted.Reel, Vector3.forward) * inserted.ReelLeftRest;
                if (inserted.ReelRight != null) inserted.ReelRight.localRotation = Quaternion.AngleAxis(-inserted.Reel * 1.15f, Vector3.forward) * inserted.ReelRightRest;
            }

            float bass = 0f, kick = 0f;
            if (previewSongs != null && previewBands != null)
            {
                bass = previewBands[1];
                kick = previewKick;
            }
            else if (bus != null)
            {
                bass = bus.Bands[1];
                kick = bus.Kick;
            }
            float pump = playing ? 1f + bass * 0.035f + kick * 0.03f : 1f;
            if (speakerLeft != null) speakerLeft.localScale = speakerLeftRest * pump;
            if (speakerRight != null) speakerRight.localScale = speakerRightRest * pump;
            speakerGlow = playing ? kick : 0f;
            lightGlow = playing ? 0.9f + kick * 0.3f : loading || (inserted != null && inserted.Blank) ? (Mathf.Sin(clock * 9f) > 0f ? 1f : 0.15f)
                : inserted != null ? 0.3f : 0.06f;
        }

        float speakerGlow;

        void EmitSparks(Color color)
        {
            if (sparks == null) return;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < 22; i++)
            {
                float a = i / 22f * Mathf.PI * 2f;
                emit.position = slot.position + radio.right * (Mathf.Cos(a) * 0.055f) + radio.up * (Mathf.Sin(a) * 0.035f) - radio.forward * 0.03f;
                emit.velocity = (radio.right * Mathf.Cos(a) + radio.up * (Mathf.Sin(a) + 0.6f) - radio.forward * 0.6f) * 0.12f;
                emit.startColor = Color.Lerp(color, Color.white, 0.4f);
                emit.startSize = 0.012f;
                emit.startLifetime = 0.6f;
                sparks.Emit(emit, 1);
            }
        }

        // ---- For the editor checks and screenshots ----

        public Transform Root => root;

        /// <summary>The song of the cassette at the front of the turntable (null for the blank).</summary>
        public SongAsset FrontSong => ring.Count > 0 && turning.Front >= 0 ? ring[turning.Front].Song : null;

        /// <summary>The song of the cassette in the radio, if there is one.</summary>
        public SongAsset InsertedSong => inserted != null ? inserted.Song : null;

        /// <summary>True while the turntable is still turning or settling.</summary>
        public bool Turning => turning.Moving;

        /// <summary>How fast the turntable turns, in cassettes per second (positive brings later songs to the front).</summary>
        public float TurnSpeed => turning.Velocity;

        /// <summary>Where the turntable is, in cassettes: whole numbers put a cassette squarely at the front.</summary>
        public float TurnPosition => turning.Scroll;

        /// <summary>Where the front cassette stands on the turntable (or would, while it is in the radio or a hand), in the world.</summary>
        public Vector3 FrontPlace => ring.Count > 0 && turning.Front >= 0 ? CassetteHome(ring[turning.Front]) : slot.position;

        /// <summary>The angle between the front cassette and the next, in degrees.</summary>
        public float TurnPitch => turning.FrontPitch * Mathf.Rad2Deg;

        /// <summary>How the turntable turns under the hands.</summary>
        public DeckRing.Tuning TurnFeel => turning.Feel;

        /// <summary>True while the glowing outline shows that a held cassette would snap into the radio.</summary>
        public bool ShowsSnapPreview => ghostRoot.gameObject.activeSelf;

        /// <summary>Whether a song's cassette stands in its place on the turntable.</summary>
        public bool OnTurntable(SongAsset song)
        {
            var c = CassetteOf(song);
            return c != null && c.Place == Place.Ring;
        }

        /// <summary>Whether a song's cassette is in a hand.</summary>
        public bool InHand(SongAsset song)
        {
            var c = CassetteOf(song);
            return c != null && c.Place == Place.Held;
        }

        /// <summary>The middle of a song's cassette, in the world.</summary>
        public Vector3 CassetteCenter(SongAsset song)
        {
            var c = CassetteOf(song);
            return c != null ? c.Root.position : Vector3.zero;
        }

        /// <summary>A point on a song's cassette, given in the cassette's own space (metres from its middle), in the world.</summary>
        public Vector3 PointOnCassette(SongAsset song, Vector3 local)
        {
            var c = CassetteOf(song);
            return c != null ? c.Root.TransformPoint(local) : Vector3.zero;
        }

        /// <summary>The song <paramref name="steps"/> places round from the front (positive to the right).</summary>
        public SongAsset SongBeside(int steps) => ring.Count > 0 ? ring[turning.Wrap(turning.Front + steps)].Song : null;

        /// <summary>The plate of words over the turntable (a quad 1 x 1 in its own space), for the editor checks.</summary>
        public Transform FrontPlate => frontPlate.transform;

        /// <summary>The middle of the blank cassette, in the world.</summary>
        public Vector3 BlankCenter => blank.Root.position;

        /// <summary>Whether the blank cassette is in the radio.</summary>
        public bool BlankInRadio => inserted == blank;

        /// <summary>Whether the blank cassette stands in its place on the turntable.</summary>
        public bool BlankOnTurntable => blank.Place == Place.Ring;

        /// <summary>The height of the table top above the floor (m).</summary>
        public float TableHeight => top;

        /// <summary>The middle of the radio's slot, where a cassette sits once it's in.</summary>
        public Vector3 SlotPosition => slot.position;

        /// <summary>A point on the turntable's edge, <paramref name="degrees"/> to the right of the front, at the height of its top.</summary>
        public Vector3 EdgePoint(float degrees, float inset = 0.01f)
        {
            float a = degrees * Mathf.Deg2Rad;
            var dir = front * Mathf.Cos(a) + frontRight * Mathf.Sin(a);
            return onTable.TransformPoint(turntableAt + new Vector3(dir.x, 0f, dir.y) * (platterRadius - inset) + Vector3.up * (rimHeight + 0.004f));
        }

        Vector3 CassettePoint(int index) => index >= 0 && index < ring.Count ? ring[index].Root.position : slot.position;

        /// <summary>
        /// Shows made-up songs on the turntable, for the editor screenshots: <paramref name="playing"/> is in the radio
        /// and playing, and the speakers follow <paramref name="bands"/> and <paramref name="kick"/>.
        /// </summary>
        public void Preview(IReadOnlyList<SongAsset> songs, SongAsset playing, float[] bands, float kick)
        {
            previewSongs = songs;
            previewPlaying = playing;
            previewBands = bands;
            previewKick = kick;
        }

        /// <summary>Turns the turntable at once so that <paramref name="song"/> is at the front (editor screenshots).</summary>
        public void PreviewFront(SongAsset song)
        {
            ReadSongs(null);
            var c = CassetteOf(song);
            if (c != null) turning.Face(ring.IndexOf(c));
            lastFront = turning.Front;
            faced = true;
        }
    }
}
