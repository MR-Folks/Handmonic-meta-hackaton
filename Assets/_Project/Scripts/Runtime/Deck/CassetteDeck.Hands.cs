using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    public sealed partial class CassetteDeck
    {
        /// <summary>Pinch strength (0-1) that grabs, and the strength it must drop below to let go (and before it can grab again).</summary>
        static readonly Vector2 PinchThresholds = new Vector2(0.85f, 0.5f);
        /// <summary>The same for closing the whole hand round a cassette.</summary>
        static readonly Vector2 FistThresholds = new Vector2(0.78f, 0.45f);
        /// <summary>How far outside a cassette a pinch may close and still take it (m); a closed hand gets a little more.</summary>
        const float GrabMargin = 0.016f, FistMargin = 0.03f;
        // Meta's touch specs, as the menu uses them: a fingertip within 15 cm of a key hovers it (until 20 cm away), a
        // press lands when the 5 mm fingertip reaches the key's face, and the next needs the finger 2 cm back out.
        const float HoverEnter = 0.15f, HoverExit = 0.2f, TouchContact = 0.005f, TouchRearm = 0.02f;

        /// <summary>What one hand is doing at the deck.</summary>
        sealed class HandState
        {
            public bool PinchArmed, FistArmed;
            public Vector3 PinchPoint, FistPoint, Tip;
            public Cassette Holding, Hovered;
            public bool ByFist, Dragging, NearEdge, Sweeping;
            public float DragAngle, DragFrom, DragTotal, Lost;
            /// <summary>The key the fingertip hovers (or -1), how far above its face, and the point under the fingertip.</summary>
            public int Key = -1;
            public float KeyDepth;
            public Vector3 KeyAt;
        }

        /// <summary>A key you press with a fingertip: the radio's play, stop and eject keys and the table's two buttons.</summary>
        sealed class Key
        {
            public string Name;
            public Transform Part, Space;
            public Renderer Renderer;
            /// <summary>In Space (the part's parent): its rest position, the way it is pressed (out of its face), the middle of its face and the face's two directions.</summary>
            public Vector3 Rest, Axis, Face, U, V;
            public Vector2 Half;
            public Vector3 Size;
            public float Travel, Icon;
            public Action Press;
            public bool OnTable, Lit;
            public Vector2 TableXZ;
            public Color Glow;
            public float Hover, Flash, Sink, LastGlow = -1f, LastMark = -1f;
            public readonly bool[] Armed = new bool[2], Over = new bool[2], Near = new bool[2];
            public readonly float[] Depth = { 1f, 1f };
        }

        readonly HandState[] hands = { new HandState(), new HandState() };
        readonly List<Key> keys = new List<Key>();
        Key playKey, stopKey, ejectKey, listKey, closeKey;
        MusicBus bus;

        static Vector3 TipOf(HandRig.Hand hand, int finger) => hand.TipJoints[finger] != null ? hand.TipJoints[finger].position : hand.Tips[finger];

        void ResetHands()
        {
            foreach (var h in hands)
            {
                h.PinchArmed = h.FistArmed = false;
                h.Holding = h.Hovered = null;
                h.Dragging = h.NearEdge = h.Sweeping = false;
                h.Key = -1;
            }
            foreach (var k in keys)
                for (int s = 0; s < 2; s++) k.Armed[s] = k.Over[s] = k.Near[s] = false;
        }

        /// <summary>
        /// The top of the band round the turntable's edge where a pinch turns it (above the base): the cassettes stand
        /// in a slot 12 mm deep, so the edge reaches a little above their feet. Higher up, a pinch takes a cassette.
        /// </summary>
        float EdgeTop => rimHeight + 0.015f;

        bool AnyDragging() => hands[0].Dragging || hands[1].Dragging;

        bool HoveredBy(Cassette c) => (hands[0].Hovered == c && hands[0].Holding == null) || (hands[1].Hovered == c && hands[1].Holding == null);

        /// <summary>
        /// Each hand: carries what it holds, turns the turntable while it pinches its edge, or else looks for a
        /// cassette to pick up (a pinch on it, or the whole hand closing round it), an edge to pinch, or a sweep of an
        /// open hand across the cassettes. A pinch must open before it can grab, so the pinch that opened the deck
        /// doesn't, and nothing is grabbed while Meta's menu gesture shows.
        /// </summary>
        void StepHands(HandRig rig, bool ready, float dt)
        {
            for (int s = 0; s < 2; s++)
            {
                var h = hands[s];
                var hand = rig != null ? rig.Get(s == 0) : null;
                h.NearEdge = h.Sweeping = false;
                h.Hovered = null;
                if (hand == null || !hand.IsTracked)
                {
                    // A hand lost for a moment keeps its cassette; one lost for longer lets it float home.
                    h.Lost += dt;
                    if (h.Lost > 0.3f)
                    {
                        LetGo(h, false);
                        h.Dragging = false;
                        h.PinchArmed = h.FistArmed = false;
                    }
                    continue;
                }
                h.Lost = 0f;
                var thumb = TipOf(hand, 0);
                var index = TipOf(hand, 1);
                h.Tip = index;
                h.PinchPoint = (thumb + index) * 0.5f;
                h.FistPoint = hand.Palm.position + hand.PalmNormal * 0.035f;
                if (!ready)
                {
                    h.PinchArmed = h.FistArmed = false;
                    continue;
                }
                float pinch = hand.Pinch, fist = hand.Fist;
                if (h.Holding != null)
                {
                    bool open = h.ByFist ? fist < FistThresholds.y : pinch < PinchThresholds.y;
                    if (open) LetGo(h, true);
                    else Carry(h, hand, dt);
                    continue;
                }
                if (h.Dragging)
                {
                    if (pinch < PinchThresholds.y || hand.InSystemGesture) h.Dragging = false;
                    else Drag(h, dt);
                    continue;
                }
                if (hand.InSystemGesture)
                {
                    h.PinchArmed = h.FistArmed = false;
                    continue;
                }
                if (pinch < PinchThresholds.y) h.PinchArmed = true;
                if (fist < FistThresholds.y) h.FistArmed = true;
                h.Hovered = Nearest(h.PinchPoint, GrabMargin * 2.5f) ?? Nearest(h.FistPoint, FistMargin * 1.5f);
                h.NearEdge = h.Hovered == null && OnEdge(h.PinchPoint);
                if (h.PinchArmed && pinch >= PinchThresholds.x)
                {
                    h.PinchArmed = false;
                    var c = Nearest(h.PinchPoint, GrabMargin);
                    if (c != null) Grab(h, c, false);
                    else if (OnEdge(h.PinchPoint)) StartDrag(h);
                }
                else if (h.FistArmed && fist >= FistThresholds.x)
                {
                    h.FistArmed = false;
                    var c = Nearest(h.FistPoint, FistMargin);
                    if (c != null) Grab(h, c, true);
                }
                else if (pinch < 0.45f && fist < 0.5f) Sweep(h, hand, dt);
            }
        }

        /// <summary>
        /// The cassette a pinch (or closed hand) at <paramref name="point"/> would take: the nearest one standing on the
        /// turntable or floating home, if the point is within <paramref name="margin"/> of it. Low down at the
        /// turntable's edge belongs to turning it instead.
        /// </summary>
        Cassette Nearest(Vector3 point, float margin)
        {
            var onDeck = onTable.InverseTransformPoint(point);
            var rel = new Vector2(onDeck.x - turntableAt.x, onDeck.z - turntableAt.z);
            bool edgeZone = onDeck.y < turntableAt.y + EdgeTop && rel.magnitude > rimRadius - 0.07f;
            Cassette best = null;
            float bestScore = float.MaxValue;
            var half = cassetteSize * 0.5f;
            for (int i = 0; i < ring.Count; i++)
            {
                var c = ring[i];
                if (c.Place != Place.Ring && c.Place != Place.Returning) continue;
                if (c.Appear < 0.9f || (c.Place == Place.Ring && edgeZone)) continue;
                var local = c.Root.InverseTransformPoint(point);
                float scale = Mathf.Max(c.Scale, 0.2f);
                var outside = new Vector3(Mathf.Max(0f, Mathf.Abs(local.x) - half.x), Mathf.Max(0f, Mathf.Abs(local.y) - half.y),
                    Mathf.Max(0f, Mathf.Abs(local.z) - half.z));
                if (outside.x * scale > margin || outside.y * scale > margin || outside.z * scale > margin * 1.8f) continue;
                float score = outside.magnitude * scale + Vector3.Distance(point, c.Root.position) * 0.1f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = c;
            }
            return best;
        }

        /// <summary>Whether a point is at the near side of the turntable's edge, low down, where a pinch turns it.</summary>
        bool OnEdge(Vector3 point)
        {
            var onDeck = onTable.InverseTransformPoint(point);
            var rel = new Vector2(onDeck.x - turntableAt.x, onDeck.z - turntableAt.z);
            float r = rel.magnitude, y = onDeck.y - turntableAt.y;
            if (r < rimRadius - 0.07f || r > platterRadius + 0.045f) return false;
            if (y < -0.012f || y > EdgeTop) return false;
            return Vector2.Dot(rel / Mathf.Max(r, 1e-4f), front) > -0.35f;
        }

        /// <summary>The angle round the turntable (radians, positive to the player's right) of a point above it.</summary>
        float EdgeAngle(Vector3 point)
        {
            var onDeck = onTable.InverseTransformPoint(point);
            var rel = new Vector2(onDeck.x - turntableAt.x, onDeck.z - turntableAt.z);
            return Mathf.Atan2(Vector2.Dot(rel, frontRight), Vector2.Dot(rel, front));
        }

        void Grab(HandState h, Cassette c, bool byFist)
        {
            foreach (var other in hands)
                if (other.Holding == c) other.Holding = null;
            h.Holding = c;
            h.ByFist = byFist;
            c.Place = Place.Held;
            c.InRange = false;
            c.Flash = 1f;
            var grab = byFist ? h.FistPoint : h.PinchPoint;
            // Hold it where the fingers closed, kept on the cassette itself.
            var local = c.Root.InverseTransformPoint(grab);
            var half = cassetteSize * 0.5f;
            c.GrabLocal = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
            voices.Play(DeckSounds.Click, grab, 0.6f, 1.15f);
        }

        /// <summary>
        /// A held cassette follows the hand, turned so its label faces you and swaying a little as the hand moves. Near
        /// the radio's door it lines itself up with the slot and is drawn gently towards it, and within reach a soft
        /// note sounds and the outline in the slot lights up.
        /// </summary>
        void Carry(HandState h, HandRig.Hand hand, float dt)
        {
            var c = h.Holding;
            var grab = h.ByFist ? h.FistPoint : h.PinchPoint;
            var toward = c.Root.position - head;
            var facingYou = toward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(toward, Vector3.up) : c.Root.rotation;
            var side = facingYou * Vector3.right;
            float sway = Mathf.Clamp(-Vector3.Dot(hand.Velocity, side) * 30f, -14f, 14f);
            var want = facingYou * Quaternion.Euler(0f, 0f, sway);
            var snapPoint = SnapPoint;
            float near = Vector3.Distance(c.Root.position, snapPoint);
            want = Quaternion.Slerp(want, slot.rotation, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.07f, near)));
            c.Root.rotation = Quaternion.Slerp(c.Root.rotation, want, 1f - Mathf.Exp(-12f * dt));
            c.Scale = Mathf.MoveTowards(c.Scale, 1f, dt * 2.5f);
            c.Root.localScale = Vector3.one * c.Scale;
            var position = grab - c.Root.rotation * (c.GrabLocal * c.Scale);
            bool inRange = near < (c.InRange ? SnapKeep : SnapRange);
            if (inRange) position = Vector3.Lerp(position, snapPoint, 0.3f * Mathf.Clamp01(1f - near / SnapKeep));
            c.Root.position = Vector3.Lerp(c.Root.position, position, 1f - Mathf.Exp(-28f * dt));
            if (inRange && !c.InRange) voices.Play(Synth.Soft(Synth.Pentatonic[7]), slot.position, 0.5f);
            c.InRange = inRange;
        }

        /// <summary>Where a held cassette is measured from to snap in: just in front of the slot, in the door's opening.</summary>
        Vector3 SnapPoint => slot.position - radio.forward * 0.025f + radio.up * 0.01f;

        /// <summary>Where a held cassette's middle is brought to drop it into the radio (editor checks).</summary>
        public Vector3 SnapPosition => SnapPoint;

        /// <summary>Opens the hand: the cassette drops into the radio if it was ready to, else it floats home.</summary>
        void LetGo(HandState h, bool mayInsert)
        {
            var c = h.Holding;
            h.Holding = null;
            if (c == null || c.Place != Place.Held) return;
            if (mayInsert && c.InRange) Insert(c, true);
            else Return(c);
            c.InRange = false;
        }

        void StartDrag(HandState h)
        {
            h.Dragging = true;
            h.DragAngle = EdgeAngle(h.PinchPoint);
            h.DragFrom = turning.Scroll;
            h.DragTotal = 0f;
            voices.Play(DeckSounds.Click, h.PinchPoint, 0.35f, 0.8f);
        }

        /// <summary>The pinched edge follows the hand round, carrying the cassettes with it.</summary>
        void Drag(HandState h, float dt)
        {
            float angle = EdgeAngle(h.PinchPoint);
            float step = Mathf.DeltaAngle(h.DragAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            h.DragAngle = angle;
            h.DragTotal += step;
            turning.Drag(h.DragFrom - h.DragTotal / Mathf.Max(turning.FrontPitch, 1e-3f), dt);
        }

        /// <summary>An open hand brushing across the front of the cassettes sets the turntable going its way.</summary>
        void Sweep(HandState h, HandRig.Hand hand, float dt)
        {
            var onDeck = onTable.InverseTransformPoint(hand.Palm.position);
            var rel = new Vector2(onDeck.x - turntableAt.x, onDeck.z - turntableAt.z);
            float r = rel.magnitude, y = onDeck.y - turntableAt.y;
            if (r < rimRadius - 0.1f || r > rimRadius + 0.13f) return;
            if (y < rimHeight - 0.03f || y > rimHeight + cassetteSize.y + 0.09f) return;
            var u = rel / Mathf.Max(r, 1e-4f);
            if (Vector2.Dot(u, front) < -0.2f) return;
            float angle = Mathf.Atan2(Vector2.Dot(u, frontRight), Vector2.Dot(u, front));
            var tangent = -front * Mathf.Sin(angle) + frontRight * Mathf.Cos(angle);
            var velocity = onTable.InverseTransformDirection(hand.Velocity);
            float along = velocity.x * tangent.x + velocity.z * tangent.y;
            if (Mathf.Abs(along) < 0.15f) return;
            h.Sweeping = true;
            turning.Push(-along / (r * Mathf.Max(turning.FrontPitch, 1e-3f)), dt);
        }

        // ---- Keys ----

        /// <summary>The radio's three keys, and the table's song list and close buttons at its front edge.</summary>
        void BuildKeys(Bounds radioBody, float halfDepth)
        {
            // The radio's keys are shaped as their symbols, so nothing is drawn on them; the table's buttons get one.
            bool shaped = DeckParts.Model(DeckParts.Radio) != null;
            playKey = RadioKey("PlayButton", shaped ? 0f : 1f, new Color(1f, 0.85f, 0.45f), radioBody, PlayPause);
            stopKey = RadioKey("StopButton", shaped ? 0f : 2f, new Color(1f, 0.85f, 0.45f), radioBody, StopKey);
            ejectKey = RadioKey("EjectButton", shaped ? 0f : 3f, new Color(1f, 0.85f, 0.45f), radioBody, EjectKey);

            // Two round buttons on the table's front edge, between the turntable and the radio.
            const float radius = 0.0225f;
            float z = -halfDepth + radius + 0.006f;
            float left = radioAt.x + radioBody.min.x;
            var close = new Vector3(left - radius - 0.012f, modelTop, z);
            var list = close + Vector3.left * (radius * 2f + 0.012f);
            // Keep both clear of the turntable.
            var center = new Vector2(turntableAt.x, turntableAt.z);
            float clear = rimRadius + 0.03f + radius + 0.004f;
            for (int i = 0; i < 40 && Vector2.Distance(new Vector2(list.x, list.z), center) < clear; i++)
            {
                list.x += 0.002f;
                close.x += 0.002f;
            }
            listKey = Puck("Song List Button", list, 5f, new Color(0.65f, 0.8f, 1f), () => SongListRequested?.Invoke());
            closeKey = Puck("Close Button", close, 4f, new Color(1f, 0.55f, 0.5f), Close);
        }

        Key RadioKey(string name, float icon, Color glow, Bounds radioBody, Action press)
        {
            var part = DeckParts.Find(radio, name);
            if (part == null)
            {
                Debug.LogWarning($"[Deck] The radio has no {name}; that key won't work.");
                return null;
            }
            var inRadio = DeckParts.LocalBounds(radio, part);
            // Keys set back from the front face are on top and press down; keys on the front press in.
            bool onTop = inRadio.center.z - radioBody.min.z > 0.02f;
            var axis = part.parent.InverseTransformDirection(onTop ? radio.up : -radio.forward).normalized;
            return AddKey(name, part, part.GetComponent<Renderer>(), axis, icon, glow, press);
        }

        Key Puck(string name, Vector3 at, float icon, Color glow, Action press)
        {
            var go = DeckParts.MakePuck(name, onTable, 0.0225f);
            go.transform.localPosition = at;
            var cap = DeckParts.Find(go.transform, "Cap");
            var key = AddKey(name, cap, cap.GetComponent<Renderer>(), Vector3.up, icon, glow, press);
            key.OnTable = true;
            return key;
        }

        Key AddKey(string name, Transform part, Renderer renderer, Vector3 axis, float icon, Color glow, Action press)
        {
            var space = part.parent;
            var b = DeckParts.LocalBounds(space, part);
            var abs = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
            float along = Vector3.Dot(b.extents, abs);
            var u = Mathf.Abs(axis.x) > 0.7f ? Vector3.forward : Vector3.right;
            var v = Vector3.Cross(axis, u).normalized;
            u = Vector3.Cross(v, axis).normalized;
            var key = new Key
            {
                Name = name, Part = part, Space = space, Renderer = renderer, Rest = part.localPosition, Axis = axis,
                Face = b.center + axis * along, U = u, V = v,
                Half = new Vector2(Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(u.x), Mathf.Abs(u.y), Mathf.Abs(u.z)))),
                    Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z))))),
                Travel = Mathf.Min(0.01f, along * 2f * 0.7f), Icon = icon, Glow = glow, Press = press,
                Size = renderer != null ? renderer.GetComponent<MeshFilter>().sharedMesh.bounds.size : Vector3.one * 0.03f
            };
            var onDeck = onTable.InverseTransformPoint(space.TransformPoint(key.Face));
            key.TableXZ = new Vector2(onDeck.x, onDeck.z);
            keys.Add(key);
            return key;
        }

        /// <summary>
        /// Each key against each fingertip: hover within 15 cm, a press as the fingertip reaches its face from above
        /// (once, until the finger is 2 cm back out), the key sinking under the finger and flashing when pressed.
        /// </summary>
        void StepKeys(HandRig rig, bool ready, float dt)
        {
            for (int s = 0; s < 2; s++) hands[s].Key = -1;
            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                float follow = 0f, nearest = float.MaxValue;
                bool hovered = false;
                for (int s = 0; s < 2; s++)
                {
                    var hand = rig != null ? rig.Get(s == 0) : null;
                    var h = hands[s];
                    if (!ready || hand == null || !hand.IsTracked || hand.InSystemGesture || h.Holding != null)
                    {
                        k.Armed[s] = k.Over[s] = k.Near[s] = false;
                        continue;
                    }
                    var rel = k.Space.InverseTransformPoint(h.Tip) - k.Face;
                    float depth = Vector3.Dot(rel, k.Axis), du = Vector3.Dot(rel, k.U), dv = Vector3.Dot(rel, k.V);
                    bool over = Mathf.Abs(du) <= k.Half.x + 0.006f && Mathf.Abs(dv) <= k.Half.y + 0.006f;
                    if (k.Armed[s] && k.Depth[s] > TouchContact && depth <= TouchContact && over)
                    {
                        k.Armed[s] = false;
                        Fire(k);
                    }
                    if (depth > TouchRearm) k.Armed[s] = true;
                    k.Depth[s] = depth;
                    k.Over[s] = over;
                    k.Near[s] = Mathf.Abs(du) <= k.Half.x + 0.025f && Mathf.Abs(dv) <= k.Half.y + 0.025f && depth > -0.03f
                                && depth < (k.Near[s] ? HoverExit : HoverEnter);
                    if (!k.Near[s]) continue;
                    hovered = true;
                    nearest = Mathf.Min(nearest, Mathf.Max(depth, 0f));
                    if (over) follow = Mathf.Max(follow, Mathf.Clamp(TouchContact - depth, 0f, k.Travel));
                    if (h.Key < 0 || depth < h.KeyDepth)
                    {
                        h.Key = i;
                        h.KeyDepth = depth;
                        h.KeyAt = k.Space.TransformPoint(k.Face + k.U * Mathf.Clamp(du, -k.Half.x, k.Half.x) + k.V * Mathf.Clamp(dv, -k.Half.y, k.Half.y) + k.Axis * 0.0015f);
                    }
                }
                k.Flash = Mathf.MoveTowards(k.Flash, 0f, dt * 3.5f);
                k.Sink = Mathf.Max(follow, Mathf.Lerp(k.Sink, k.Flash * k.Travel * 0.9f, 1f - Mathf.Exp(-30f * dt)));
                k.Part.localPosition = k.Rest - k.Axis * k.Sink;
                k.Hover = Mathf.MoveTowards(k.Hover, hovered ? 1f - Mathf.Clamp01(nearest / HoverEnter) * 0.6f : 0f, dt * 8f);
            }
        }

        void Fire(Key k)
        {
            k.Flash = 1f;
            voices.Play(DeckSounds.Click, k.Space.TransformPoint(k.Face), 0.65f, k == closeKey ? 0.8f : k == listKey ? 1.25f : 1f);
            voices.Play(Synth.Chime(Synth.Pentatonic[k == playKey ? 6 : k == stopKey ? 4 : k == ejectKey ? 5 : k == listKey ? 7 : 3]),
                k.Space.TransformPoint(k.Face), 0.25f);
            k.Press?.Invoke();
        }

        /// <summary>Play/pause: plays or pauses the cassette in the radio; with none in, puts in the front one and plays it.</summary>
        void PlayPause()
        {
            if (inserted == null)
            {
                var c = ring.Count > 0 && turning.Front >= 0 ? ring[turning.Front] : null;
                if (c != null && c.Place == Place.Ring && !c.Blank) Insert(c, true);
                return;
            }
            if (inserted.Blank) return;
            if (previewSongs != null)
            {
                previewPlaying = previewPlaying == inserted.Song ? null : inserted.Song;
                return;
            }
            if (bus == null) return;
            if (PlayingSong(bus) != inserted.Song) bus.Play(inserted.Song);
            else bus.TogglePause();
            lastBusSong = PlayingSong(bus);
        }

        /// <summary>Stop: the song stops and goes back to its start; the cassette stays in.</summary>
        void StopKey()
        {
            if (inserted == null || inserted.Blank || bus == null) return;
            if (PlayingSong(bus) == inserted.Song) bus.Stop();
        }

        /// <summary>Eject: the song stops and the cassette pops out and goes home.</summary>
        void EjectKey()
        {
            if (inserted == null) return;
            if (bus != null && !inserted.Blank && PlayingSong(bus) == inserted.Song) bus.Stop();
            if (previewSongs != null && previewPlaying == inserted.Song) previewPlaying = null;
            Eject(inserted);
        }

        // ---- For the editor checks ----

        /// <summary>The middle of a key's face, in the world: "PlayButton", "StopButton", "EjectButton", "Song List Button" or "Close Button".</summary>
        public Vector3 KeyFace(string name)
        {
            foreach (var k in keys)
                if (k.Name == name) return k.Space.TransformPoint(k.Face);
            return Vector3.zero;
        }

        /// <summary>The way a key is pressed, in the world (into it).</summary>
        public Vector3 KeyInward(string name)
        {
            foreach (var k in keys)
                if (k.Name == name) return -k.Space.TransformDirection(k.Axis).normalized;
            return Vector3.down;
        }
    }
}
