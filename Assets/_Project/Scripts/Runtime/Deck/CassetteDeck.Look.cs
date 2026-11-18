using UnityEngine;

namespace MusicInYourHands
{
    public sealed partial class CassetteDeck
    {
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int AccentId = Shader.PropertyToID("_Accent");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        static readonly int MarkId = Shader.PropertyToID("_Mark");
        static readonly int IconId = Shader.PropertyToID("_Icon");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int InnerGlowId = Shader.PropertyToID("_InnerGlow");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int EdgeId = Shader.PropertyToID("_Edge");
        static readonly int HoverId = Shader.PropertyToID("_Hover");

        static readonly Color Ink = new Color(0.12f, 0.1f, 0.16f);
        static readonly Color Paper = new Color(0.97f, 0.94f, 0.87f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.78f);
        static readonly Color Lyrics = new Color(0.55f, 0.95f, 0.7f);
        static readonly Color Warning = new Color(1f, 0.8f, 0.45f);
        // Shell colours: warm for the songs that come with the app, fresh greens for your own.
        static readonly Color[] Warm =
        {
            new Color(0.95f, 0.38f, 0.34f), new Color(0.98f, 0.66f, 0.22f), new Color(0.92f, 0.36f, 0.58f),
            new Color(0.96f, 0.52f, 0.3f), new Color(0.86f, 0.28f, 0.36f)
        };
        static readonly Color[] Fresh =
        {
            new Color(0.2f, 0.72f, 0.58f), new Color(0.46f, 0.78f, 0.3f), new Color(0.16f, 0.6f, 0.72f), new Color(0.62f, 0.82f, 0.36f)
        };
        static readonly Color BlankShell = new Color(0.9f, 0.93f, 0.96f);

        const string BlankTitle = "+  Add a song";
        // The front cassette's words lie on a plate over the middle of the turntable, about 70 cm from your eyes.
        const float TitleHeight = 0.021f, InfoHeight = 0.0135f, HintHeight = 0.012f, StatusHeight = 0.014f;
        /// <summary>How wide the words over the turntable may be (m), so the plate stays clear of the cassettes round it.</summary>
        const float PlateWidth = 0.18f;
        const string NoPicker = "The file picker opens on the headset";

        enum Status { Empty, Listening, Blank, NoPicker, Loading, Failed, Playing, Paused, Stopped }
        enum Hint { Pick, Edge, Turning, Carry, Drop, Blank }

        TypeLabel title, info, hint, status, tooltip, listLabel, closeLabel;
        TypeLabel[] steadyLabels;
        Renderer[] speakers;
        Renderer edgeGlow, floorShadow, frontPlate, radioPlate;
        float frontWidth, frontWidthBare, radioWidth, hintFade = 1f, titleScale = 1f, infoScale = 1f, hintScale = 1f;
        /// <summary>Set once a cassette has gone into the radio by hand: the how-to line has done its job.</summary>
        static bool learned;
        Renderer[] tableShadows;
        readonly Renderer[] dots = new Renderer[2];
        Vector3 radioCaption;
        float captionFade, edgeAlpha, ghostAlpha, doorGlow = -1f, lastLight = -1f, lastSpeaker = -1f;
        Cassette shownFront;
        bool shownInRadio, shownLyrics;
        SongAsset shownStatusSong;
        Status shownStatus = (Status)(-1);
        Hint shownHint = (Hint)(-1);
        int shownTooltip = -2;

        void BuildLook(Material glowMaterial, float halfWidth, float halfDepth, float turntableRadius, Bounds radioBody, float radioTop)
        {
            // Soft shadows: under the table on the real floor, and under the turntable and radio on the table top.
            var shadow = DeckParts.Material("Shadow");
            floorShadow = Flat("Floor Shadow", root, shadow, new Vector3(0f, 0.003f, 0f), new Vector2(halfWidth * 2f + 0.34f, halfDepth * 2f + 0.34f),
                new Vector4(halfWidth / (halfWidth + 0.17f), halfDepth / (halfDepth + 0.17f), 0.12f, 0.32f), 0.42f);
            float tr = turntableRadius + 0.035f;
            var radioCenter = radioAt + new Vector3(radioBody.center.x, 0f, radioBody.center.z);
            var radioHalf = new Vector2(radioBody.extents.x + 0.03f, radioBody.extents.z + 0.03f);
            tableShadows = new[]
            {
                Flat("Turntable Shadow", onTable, shadow, turntableAt + Vector3.up * 0.0008f, Vector2.one * tr * 2f,
                    new Vector4(turntableRadius / tr, turntableRadius / tr, turntableRadius / tr, 0.16f), 0.5f),
                Flat("Radio Shadow", onTable, shadow, radioCenter + Vector3.up * 0.0008f, radioHalf * 2f,
                    new Vector4(radioBody.extents.x / radioHalf.x, radioBody.extents.z / radioHalf.y, 0.1f, 0.22f), 0.5f)
            };

            // A glowing ring round the turntable's edge: faint to show it can be turned, bright while it is.
            edgeGlow = Flat("Edge Glow", turntable, DeckParts.Material("Rim"), new Vector3(0f, rimHeight + 0.0015f, 0f), Vector2.one * (platterRadius + 0.015f) * 2f,
                Vector4.zero, 0f);
            block.Clear();
            block.SetColor(ColorId, new Color(0.6f, 0.85f, 1f));
            block.SetFloat(RadiusId, platterRadius / (platterRadius + 0.015f));
            block.SetFloat(ThicknessId, 0.02f);
            block.SetFloat(InnerGlowId, 0f);
            block.SetFloat(AlphaId, 0f);
            edgeGlow.SetPropertyBlock(block);

            var glow = glowMaterial != null ? glowMaterial : DeckParts.Material("DeckGlow");
            for (int s = 0; s < 2; s++)
            {
                dots[s] = Effects.MakeQuad(s == 0 ? "Fingertip L" : "Fingertip R", root, glow);
                dots[s].gameObject.SetActive(false);
            }

            // Dark glass plates behind the floating words, so they read over the deck and any room.
            frontPlate = Effects.MakeQuad("Front Plate", onTable, DeckParts.Material("Plate"));
            radioPlate = Effects.MakeQuad("Radio Plate", onTable, DeckParts.Material("Plate"));
            title = Caption("Front Title", TitleHeight);
            info = Caption("Front Info", InfoHeight);
            // A soft dark halo round the front cassette's title and what it is, on top of their plate.
            title.Halo = info.Halo = 0.8f;
            hint = Caption("Hint", HintHeight);
            status = Caption("Radio Status", StatusHeight);
            title.IsCaption = status.IsCaption = true;
            tooltip = Caption("Key Name", 0.0095f);
            listLabel = Caption("Song List Label", 0.0105f);
            listLabel.Text = "Song list";
            closeLabel = Caption("Close Label", 0.0105f);
            closeLabel.Text = "Close";
            steadyLabels = new[] { title, info, hint, status, listLabel, closeLabel };
            speakers = new[] { speakerLeft != null ? speakerLeft.GetComponent<Renderer>() : null, speakerRight != null ? speakerRight.GetComponent<Renderer>() : null };
            radioCaption = radioAt + new Vector3(radioBody.center.x, radioTop + 0.03f, radioBody.center.z);

            // Keys' symbols and sizes.
            foreach (var k in keys) StyleKey(k, true);
            StyleSpeaker(0f, true);
        }

        TypeLabel Caption(string name, float height)
        {
            var label = new TypeLabel(name, onTable, height);
            label.GameObject.SetActive(false);
            return label;
        }

        Renderer Flat(string name, Transform parent, Material material, Vector3 at, Vector2 size, Vector4 shape, float strength)
        {
            var r = Effects.MakeQuad(name, parent, material);
            r.transform.localPosition = at;
            r.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            r.transform.localScale = new Vector3(size.x, size.y, 1f);
            if (strength > 0f)
            {
                block.Clear();
                block.SetColor(ColorId, new Color(0f, 0f, 0f, strength));
                block.SetVector(ShapeId, shape);
                block.SetFloat(AlphaId, 0f);
                r.SetPropertyBlock(block);
            }
            return r;
        }

        static Color SongColor(SongAsset song)
        {
            if (song == null) return BlankShell;
            int hash = 7;
            var key = song.title ?? "";
            for (int i = 0; i < key.Length; i++) hash = hash * 31 + key[i];
            hash &= 0x7fffffff;
            var palette = song.IsAdded ? Fresh : Warm;
            return palette[hash % palette.Length];
        }

        static bool HasLyrics(SongAsset song) => song != null && (song.lyrics != null || !string.IsNullOrEmpty(song.lyricsPath));

        /// <summary>
        /// Writes a cassette's title on its label, in the app's lettering: one line when it fits, else two, smaller,
        /// shortened with an ellipsis only if it must be. Songs with lyrics leave room for the label's music note.
        /// </summary>
        void FitLabelText(Cassette c)
        {
            string text = c.Blank ? BlankTitle : c.Song != null ? c.Song.title ?? "" : "";
            bool lyrics = !c.Blank && HasLyrics(c.Song);
            c.ShownTitle = text;
            c.ShownLyrics = lyrics;
            c.Color = c.Blank ? BlankShell : SongColor(c.Song);

            float width = labelSize.x * (lyrics ? 0.72f : 0.86f);
            float x = labelCenter.x - (lyrics ? labelSize.x * 0.07f : 0f);
            float y = labelCenter.y - labelSize.y * 0.12f;
            var face = TypeFace.Bold;
            float one = Mathf.Min(labelSize.y * 0.5f, 0.0105f);
            if (face.Width(text) * one <= width || text.IndexOf(' ') < 0)
            {
                float h = Mathf.Max(Mathf.Min(one, width / Mathf.Max(face.Width(text), 0.01f)), 0.0068f);
                SetLine(c.Line1, Fit(text, h, width), h, new Vector3(x, y, labelCenter.z));
                c.Line2.GameObject.SetActive(false);
            }
            else
            {
                // Break at the space nearest the middle.
                int split = -1;
                for (int i = 0; i < text.Length; i++)
                    if (text[i] == ' ' && (split < 0 || Mathf.Abs(i - text.Length / 2) < Mathf.Abs(split - text.Length / 2))) split = i;
                string a = text.Substring(0, split).TrimEnd(), b = text.Substring(split + 1).TrimStart();
                float h = Mathf.Clamp(width / Mathf.Max(face.Width(a), face.Width(b), 0.01f), 0.0062f, labelSize.y * 0.36f);
                SetLine(c.Line1, Fit(a, h, width), h, new Vector3(x, y + h * 0.62f, labelCenter.z));
                SetLine(c.Line2, Fit(b, h, width), h, new Vector3(x, y - h * 0.62f, labelCenter.z));
                c.Line2.GameObject.SetActive(true);
            }
            c.Line1.GameObject.SetActive(true);

            block.Clear();
            block.SetColor(TintId, c.Blank ? new Color(0.9f, 0.98f, 0.94f) : Paper);
            block.SetColor(AccentId, c.Blank ? new Color(0.3f, 0.8f, 0.6f) : Color.Lerp(c.Color, Color.black, 0.12f));
            block.SetFloat(MarkId, lyrics ? 1f : 0f);
            block.SetVector(SizeId, new Vector4(labelSize.x, labelSize.y, 0f, 0f));
            if (c.Label != null) c.Label.SetPropertyBlock(block);
            c.LastGlow = -1f;
        }

        static void SetLine(TypeLabel line, string text, float height, Vector3 at)
        {
            line.Text = text;
            line.Transform.localPosition = at;
            line.Transform.localRotation = Quaternion.identity;
            line.Transform.localScale = Vector3.one * (height / 0.01f);
        }

        /// <summary>Shortens text with an ellipsis to fit a width (m) at a letter height (m).</summary>
        static string Fit(string text, float height, float width)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var face = TypeFace.Bold;
            if (face.Width(text) * height <= width) return text;
            for (int length = text.Length - 1; length > 0; length--)
            {
                var cut = text.Substring(0, length).TrimEnd() + "…";
                if (face.Width(cut) * height <= width) return cut;
            }
            return "…";
        }

        /// <summary>
        /// Everything that shows state: each cassette's colour, label and glow (the front one lit, the one under a hand
        /// brighter), the snap outline, the door and light, the turntable's edge, the fingertip dots over the keys, and
        /// the words: the front cassette's title, what it is, a hint, and what the radio is doing.
        /// </summary>
        void StepLook(MusicBus bus, Camera view, float dt)
        {
            int frontIndex = turning.Front;
            Cassette held = null;
            foreach (var h in hands)
                if (h.Holding != null && (held == null || h.Holding.InRange)) held = h.Holding;

            for (int i = 0; i < ring.Count; i++)
            {
                var c = ring[i];
                string text = c.Blank ? BlankTitle : c.Song != null ? c.Song.title ?? "" : "";
                if (!ReferenceEquals(text, c.ShownTitle) && text != c.ShownTitle || HasLyrics(c.Song) != c.ShownLyrics && !c.Blank) FitLabelText(c);
                // Cassettes facing away round the back show only their shells.
                bool away = c.Place == Place.Ring && Mathf.Abs(c.RingAngle) > 1.8f;
                if (away != c.ShownAway)
                {
                    c.ShownAway = away;
                    foreach (var r in c.Inside)
                        if (r != null) r.enabled = !away;
                }
                bool atFront = i == frontIndex && c.Place == Place.Ring;
                float glow = Mathf.Max(c.Hover * 0.5f, Mathf.Max(c.Flash * 0.9f, c.Place == Place.Held ? (c.InRange ? 0.6f : 0.35f) : atFront ? 0.2f : 0f));
                if (Mathf.Abs(glow - c.LastGlow) > 0.01f && c.Shell != null)
                {
                    c.LastGlow = glow;
                    block.Clear();
                    block.SetColor(TintId, c.Color);
                    block.SetFloat(GlowId, glow);
                    block.SetColor(GlowColorId, Color.Lerp(c.Color, Color.white, 0.65f));
                    c.Shell.SetPropertyBlock(block);
                }
            }

            // The snap outline in the radio, in the held cassette's colour.
            bool snap = held != null && held.InRange;
            ghostAlpha = Mathf.MoveTowards(ghostAlpha, snap ? 1f : 0f, dt * 8f);
            if (ghostRoot.gameObject.activeSelf != ghostAlpha > 0.01f) ghostRoot.gameObject.SetActive(ghostAlpha > 0.01f);
            if (ghost != null && ghostAlpha > 0.01f)
            {
                block.Clear();
                block.SetColor(ColorId, Color.Lerp(held != null ? held.Color : Lyrics, Color.white, 0.35f));
                block.SetFloat(AlphaId, ghostAlpha);
                ghost.SetPropertyBlock(block);
            }

            // The door glows to invite a held cassette, more when it's ready to drop in.
            float wantDoor = held == null ? 0f : snap ? 0.75f : 0.25f + 0.1f * Mathf.Sin(clock * 5f);
            if (doorRenderer != null && Mathf.Abs(wantDoor - doorGlow) > 0.01f)
            {
                doorGlow = wantDoor;
                block.Clear();
                block.SetFloat(GlowId, wantDoor);
                block.SetColor(GlowColorId, held != null ? Color.Lerp(held.Color, Color.white, 0.5f) : Color.white);
                doorRenderer.SetPropertyBlock(block);
            }

            if (light != null && Mathf.Abs(lightGlow - lastLight) > 0.01f)
            {
                lastLight = lightGlow;
                block.Clear();
                block.SetFloat(GlowId, lightGlow);
                bool blankIn = inserted != null && inserted.Blank;
                block.SetColor(GlowColorId, blankIn ? new Color(1f, 0.35f, 0.3f) : IsPlaying(bus) ? new Color(0.35f, 1f, 0.5f) : new Color(1f, 0.7f, 0.3f));
                light.SetPropertyBlock(block);
            }
            StyleSpeaker(speakerGlow, false);
            foreach (var k in keys) StyleKey(k, false);

            // The turntable's edge: a faint glow at first to show it turns, brighter under a hand, bright while turned.
            bool dragging = hands[0].Dragging || hands[1].Dragging;
            bool sweeping = hands[0].Sweeping || hands[1].Sweeping;
            bool nearEdge = hands[0].NearEdge || hands[1].NearEdge;
            float idle = openClock < 6f ? 0.12f + 0.08f * Mathf.Sin(clock * 3f) : 0.06f;
            float wantEdge = dragging ? 1f : sweeping ? 0.7f : nearEdge ? 0.45f : idle;
            edgeAlpha = Mathf.MoveTowards(edgeAlpha, wantEdge * lift, dt * 5f);
            block.Clear();
            block.SetColor(ColorId, new Color(0.6f, 0.85f, 1f));
            block.SetFloat(RadiusId, platterRadius / (platterRadius + 0.015f));
            block.SetFloat(ThicknessId, dragging ? 0.028f : 0.02f);
            block.SetFloat(InnerGlowId, 0f);
            block.SetFloat(AlphaId, edgeAlpha);
            edgeGlow.SetPropertyBlock(block);

            float shade = lift * lift;
            SetShadow(floorShadow, shade);
            SetShadow(tableShadows[0], shade);
            SetShadow(tableShadows[1], shade);

            StepDots(view, dt);
            StepCaptions(bus, view, dt);
        }

        void SetShadow(Renderer r, float fade)
        {
            block.Clear();
            r.GetPropertyBlock(block);
            block.SetFloat(AlphaId, fade);
            r.SetPropertyBlock(block);
        }

        void StyleKey(Key k, bool force)
        {
            if (k.Renderer == null) return;
            bool lit = k == playKey ? IsPlaying(bus) && inserted != null : k == stopKey ? inserted != null && !IsPlaying(bus) && !inserted.Blank : false;
            // A key without a drawn symbol (the radio's, shaped as their symbols) shows that it is on with a soft glow.
            float glow = k.Hover * 0.3f + k.Flash * 0.9f + (lit && k.Icon < 0.5f ? 0.2f : 0f);
            float mark = (lit ? 0.7f : 0f) + k.Flash;
            if (!force && Mathf.Abs(glow - k.LastGlow) < 0.01f && Mathf.Abs(mark - k.LastMark) < 0.01f) return;
            k.LastGlow = glow;
            k.LastMark = mark;
            block.Clear();
            block.SetFloat(GlowId, glow);
            block.SetColor(GlowColorId, k.Glow);
            block.SetFloat(IconId, k.Icon);
            block.SetFloat(MarkId, mark);
            block.SetVector(SizeId, k.Size);
            k.Renderer.SetPropertyBlock(block);
        }

        void StyleSpeaker(float glow, bool force)
        {
            if (!force && Mathf.Abs(glow - lastSpeaker) < 0.02f) return;
            lastSpeaker = glow;
            block.Clear();
            block.SetFloat(GlowId, glow * 0.7f);
            block.SetColor(GlowColorId, inserted != null ? Color.Lerp(inserted.Color, Color.white, 0.55f) : Color.white);
            foreach (var r in speakers)
            {
                if (r == null) continue;
                block.SetVector(SizeId, r.GetComponent<MeshFilter>().sharedMesh.bounds.size);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// A soft dot on a key under a fingertip that comes within reach, shrinking and brightening as the finger
        /// nears (as on the menu); and a dot where a hand pinches the turntable's edge.
        /// </summary>
        void StepDots(Camera view, float dt)
        {
            for (int s = 0; s < 2; s++)
            {
                var h = hands[s];
                var dot = dots[s];
                bool onKey = h.Key >= 0 && IsOpen;
                bool onEdge = (h.Dragging || h.NearEdge) && IsOpen;
                bool show = onKey || onEdge;
                if (dot.gameObject.activeSelf != show) dot.gameObject.SetActive(show);
                if (!show) continue;
                block.Clear();
                if (onKey)
                {
                    float near = Mathf.Clamp01(h.KeyDepth / HoverEnter);
                    dot.transform.position = h.KeyAt;
                    dot.transform.localScale = Vector3.one * Mathf.Lerp(0.008f, 0.026f, near);
                    block.SetColor(ColorId, Color.white);
                    block.SetFloat(IntensityId, Mathf.Lerp(1.4f, 0.45f, near));
                }
                else
                {
                    dot.transform.position = h.PinchPoint;
                    dot.transform.localScale = Vector3.one * (h.Dragging ? 0.03f : 0.022f);
                    block.SetColor(ColorId, new Color(0.6f, 0.85f, 1f));
                    block.SetFloat(IntensityId, h.Dragging ? 1.2f : 0.6f);
                }
                Effects.Face(dot.transform, view);
                dot.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// The words. Above the front cassette: its title, what it is (and whether it has lyrics), and a hint for what
        /// to do next. Above the radio: what it is doing. Each line is only rewritten when what it says changes.
        /// </summary>
        void StepCaptions(MusicBus bus, Camera view, float dt)
        {
            captionFade = Mathf.MoveTowards(captionFade, IsOpen && lift > 0.97f ? 1f : 0f, dt * (IsOpen ? 4f : 10f));
            bool show = captionFade > 0.01f;
            foreach (var label in steadyLabels)
                if (label.GameObject.activeSelf != show) label.GameObject.SetActive(show);
            if (frontPlate.gameObject.activeSelf != show)
            {
                frontPlate.gameObject.SetActive(show);
                radioPlate.gameObject.SetActive(show);
            }
            if (!show)
            {
                tooltip.GameObject.SetActive(false);
                return;
            }

            // The front cassette (or the one from the front that is now in the radio or a hand).
            var c = ring.Count > 0 && turning.Front >= 0 ? ring[turning.Front] : null;
            bool inRadio = c != null && (c == inserted || c.Place == Place.Inserting);
            bool lyrics = c != null && HasLyrics(c.Song);
            if (c != shownFront || inRadio != shownInRadio || lyrics != shownLyrics)
            {
                shownFront = c;
                shownInRadio = inRadio;
                shownLyrics = lyrics;
                // A long title gets a little smaller before it is shortened.
                string name = c == null ? "" : c.Blank ? "Add a song" : c.Song != null ? c.Song.title ?? "" : "";
                titleScale = Mathf.Clamp(PlateWidth / Mathf.Max(TypeFace.Bold.Width(name) * TitleHeight, 1e-4f), 0.72f, 1f);
                title.Text = Fit(name, TitleHeight * titleScale, PlateWidth);
                title.Transform.localScale = Vector3.one * titleScale;
                string about = c == null ? "" : InfoLine(c, inRadio, lyrics);
                infoScale = Mathf.Clamp(PlateWidth / Mathf.Max(TypeFace.Bold.Width(about) * InfoHeight, 1e-4f), 0.8f, 1f);
                info.Text = Fit(about, InfoHeight * infoScale, PlateWidth);
                info.Transform.localScale = Vector3.one * infoScale;
                MeasureFront();
            }

            // The how-to line shows until the first cassette goes into the radio; the hints for what a hand is doing,
            // always.
            var hintNow = HintNow(c);
            bool hinting = hintNow != Hint.Pick || !learned;
            hintFade = Mathf.MoveTowards(hintFade, hinting ? 1f : 0f, dt * 2f);
            if (hint.GameObject.activeSelf != hintFade > 0.01f) hint.GameObject.SetActive(hintFade > 0.01f);
            if (hintNow != shownHint)
            {
                shownHint = hintNow;
                hint.Text = hintNow switch
                {
                    Hint.Carry => "Bring it to the radio's door",
                    Hint.Drop => "Let go to put it in",
                    Hint.Edge => "Pinch the edge and drag to turn",
                    Hint.Turning => "Let go mid-turn to spin it",
                    Hint.Blank => "Put it in the radio to add a song",
                    _ => "Pinch a cassette to pick it up"
                };
                hintScale = Mathf.Clamp(PlateWidth / Mathf.Max(TypeFace.Bold.Width(hint.Text) * HintHeight, 1e-4f), 0.8f, 1f);
                hint.Text = Fit(hint.Text, HintHeight * hintScale, PlateWidth);
                hint.Transform.localScale = Vector3.one * hintScale;
                MeasureFront();
            }

            // The words stand on the middle of the turntable, a little towards the radio, leaning back to face your
            // eyes: seen from your seat they fill the gap between the front cassettes below them and the back of the
            // ring above, so they never cover a label you are choosing by, and they stay inside the narrow view.
            float low = -InfoHeight * 0.75f, y = 0f;
            float hintStep = 0.019f * hintFade;
            if (hintFade > 0.01f) low = Mathf.Lerp(low, -HintHeight * 0.75f, hintFade);
            float infoY = hintStep, titleY = infoY + 0.026f * Mathf.Lerp(0.85f, 1f, titleScale);
            float high = titleY + TitleHeight * titleScale * 0.75f;
            var foot = onTable.TransformPoint(turntableAt + new Vector3(frontRight.x, 0f, frontRight.y) * PlateShift - new Vector3(front.x, 0f, front.y) * PlateBack
                + Vector3.up * (platterTop + 0.007f));
            var facingYou = Facing(foot);
            var anchor = foot - facingYou * new Vector3(0f, low - 0.008f, 0f);
            if (hintFade > 0.01f) Line(hint, anchor, facingYou, y, new Color(1f, 1f, 1f, 0.66f * hintFade));
            Line(info, anchor, facingYou, infoY, lyrics ? Color.Lerp(Soft, Lyrics, 0.55f) : Soft);
            Line(title, anchor, facingYou, titleY, c != null ? Color.Lerp(Color.white, c.Color, 0.25f) : Color.white);
            Plate(frontPlate, anchor, facingYou, low, high, Mathf.Lerp(frontWidthBare, frontWidth, hintFade));

            // The radio.
            var now = StatusNow(bus, out var song);
            if (now != shownStatus || song != shownStatusSong)
            {
                shownStatus = now;
                shownStatusSong = song;
                string name = song != null ? song.title : "";
                status.Text = Fit(now switch
                {
                    Status.Playing => $"Playing  ·  {name}",
                    Status.Paused => $"Paused  ·  {name}",
                    Status.Stopped => $"Stopped  ·  {name}",
                    Status.Loading => $"Loading {name}…",
                    Status.Failed => $"Couldn't play {name}",
                    Status.Blank => "Choose a song on the headset…",
                    Status.NoPicker => NoPicker,
                    Status.Listening => "Listening to the room",
                    _ => "Put a cassette in the radio"
                }, StatusHeight, 0.42f);
                radioWidth = -1f;
            }
            if (radioWidth < 0f)
                radioWidth = TypeFace.Bold.Width(status.Text) * StatusHeight;
            var radioAnchor = onTable.TransformPoint(radioCaption);
            var facingRadio = Facing(radioAnchor);
            Line(status, radioAnchor, facingRadio, 0f, now == Status.Failed || now == Status.NoPicker ? Warning : Color.white);
            Plate(radioPlate, radioAnchor, facingRadio, -StatusHeight * 0.75f, StatusHeight * 0.75f, radioWidth);

            // The name of a radio key under a fingertip, just above it.
            int keyIndex = hands[0].Key >= 0 ? hands[0].Key : hands[1].Key;
            var key = keyIndex >= 0 ? keys[keyIndex] : null;
            bool tip = key != null && !key.OnTable;
            if (tooltip.GameObject.activeSelf != tip) tooltip.GameObject.SetActive(tip);
            if (tip)
            {
                int which = key == playKey ? (IsPlaying(bus) && inserted != null ? 1 : 0) : key == stopKey ? 2 : 3;
                if (which != shownTooltip)
                {
                    shownTooltip = which;
                    tooltip.Text = which switch { 0 => "Play", 1 => "Pause", 2 => "Stop", _ => "Eject" };
                }
                var at = key.Space.TransformPoint(key.Face) + Vector3.up * 0.028f;
                Line(tooltip, at, Facing(at), 0f, Ink);
            }

            // The table's buttons are named in dark ink just above them, over the light wood.
            var listAt = listKey.Space.TransformPoint(listKey.Face) + Vector3.up * 0.026f;
            Line(listLabel, listAt, Facing(listAt), 0f, Color.Lerp(new Color(Ink.r, Ink.g, Ink.b, 0.8f), Ink, listKey.Hover));
            var closeAt = closeKey.Space.TransformPoint(closeKey.Face) + Vector3.up * 0.026f;
            Line(closeLabel, closeAt, Facing(closeAt), 0f, Color.Lerp(new Color(Ink.r, Ink.g, Ink.b, 0.8f), Ink, closeKey.Hover));
        }

        /// <summary>Measures how wide the front words are (m), without and with the hint, when they change.</summary>
        void MeasureFront()
        {
            var face = TypeFace.Bold;
            float w = Mathf.Max(face.Width(title.Text) * TitleHeight * titleScale, face.Width(info.Text) * InfoHeight * infoScale);
            frontWidthBare = w;
            frontWidth = Mathf.Max(w, face.Width(hint.Text) * HintHeight * hintScale);
        }

        /// <summary>A rotation that turns words at <paramref name="at"/> (world) to face your eyes, upright.</summary>
        Quaternion Facing(Vector3 at)
        {
            var away = at - head;
            return away.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(away, Vector3.up) : onTable.rotation;
        }

        /// <summary>Places a line <paramref name="y"/> up a block of words that starts at <paramref name="origin"/>, faded in with the deck.</summary>
        void Line(TypeLabel label, Vector3 origin, Quaternion rotation, float y, Color color)
        {
            label.Transform.SetPositionAndRotation(origin + rotation * new Vector3(0f, y, 0f), rotation);
            color.a *= captionFade;
            label.Color = color;
        }

        /// <summary>The dark glass plate behind a block of words, from <paramref name="low"/> to <paramref name="high"/> up it.</summary>
        void Plate(Renderer plate, Vector3 origin, Quaternion rotation, float low, float high, float width)
        {
            var size = new Vector2(width + 0.03f, high - low + 0.016f);
            plate.transform.SetPositionAndRotation(origin + rotation * new Vector3(0f, (low + high) * 0.5f, 0.004f), rotation);
            plate.transform.localScale = new Vector3(size.x, size.y, 1f);
            block.Clear();
            block.SetColor(ColorId, new Color(0.05f, 0.055f, 0.09f, 0.74f));
            block.SetColor(EdgeId, new Color(1f, 1f, 1f, 0.16f));
            block.SetVector(SizeId, size);
            block.SetFloat(RadiusId, 0.012f);
            block.SetFloat(HoverId, 0f);
            block.SetFloat(AlphaId, captionFade);
            plate.SetPropertyBlock(block);
        }

        static string InfoLine(Cassette c, bool inRadio, bool lyrics)
        {
            if (c.Blank) return "Put it in the radio, then choose a song file";
            var song = c.Song;
            if (song == null) return "";
            string what = !string.IsNullOrEmpty(song.artist) ? song.artist
                : song.IsAdded ? "Your song" : "Comes with the app";
            string words = lyrics ? "  ·  with lyrics" : "  ·  no lyrics";
            return inRadio ? $"In the radio  ·  {what}{words}" : what + words;
        }

        Hint HintNow(Cassette frontCassette)
        {
            foreach (var h in hands)
                if (h.Holding != null) return h.Holding.InRange ? Hint.Drop : Hint.Carry;
            if (hands[0].Dragging || hands[1].Dragging) return Hint.Turning;
            if (hands[0].NearEdge || hands[1].NearEdge) return Hint.Edge;
            if (frontCassette != null && frontCassette.Blank) return Hint.Blank;
            return Hint.Pick;
        }

        Status StatusNow(MusicBus bus, out SongAsset song)
        {
            song = inserted != null ? inserted.Song : null;
            if (clock < noPickerUntil) return Status.NoPicker;
            var failed = bus != null && previewSongs == null ? bus.Failed : null;
            if (failed != null)
            {
                song = failed;
                return Status.Failed;
            }
            if (inserted == null) return bus != null && bus.IsListening && previewSongs == null ? Status.Listening : Status.Empty;
            if (inserted.Blank) return Status.Blank;
            if (bus != null && previewSongs == null && bus.Loading == inserted.Song) return Status.Loading;
            if (IsPlaying(bus)) return Status.Playing;
            if (bus != null && previewSongs == null && bus.SongTime < 0.05f) return Status.Stopped;
            return Status.Paused;
        }
    }
}
