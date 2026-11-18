using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The menu's song page, for the songs people add themselves. Your
    /// songs are listed with whether each has lyrics; touching or pinching a row plays that song, and each row can add or change its
    /// lyrics or remove the song. Adding a song is a guided step at a time: pick the song from the headset's files, it
    /// starts playing straight away, then the page offers to add its lyrics. The menu steps aside while the file
    /// picker's window is open and comes back when it is done.
    /// Making a song (SongMaker) is a step at a time too: connect an ElevenLabs account once (type the API key on the
    /// headset's keyboard, or copy a key file over), pick a theme emoji, pick a genre emoji, confirm what it costs,
    /// and the page shows how it's going. It keeps going with the menu closed, and the new song plays when it's ready.
    /// The page is reached from the cassette deck's song list button, and its Back button goes back to the deck.
    /// </summary>
    public sealed class SongLibrary
    {
        // Numbered for the editor screenshots (Preview), so new views go at the end.
        enum View { List, AskLyrics, ConfirmRemove, Connect, Theme, Genre, Confirm, Making, Failed }

        /// <summary>An emoji drawn on the page (null texture in the editor, where there's no emoji font).</summary>
        sealed class Picture
        {
            public Renderer Quad;
            public Texture2D Texture;
            public Vector2 At;
            public float Size;
            public bool Shown;
        }

        sealed class Button
        {
            public Renderer Back;
            public TypeLabel Label;
            public Vector2 Center, Size;
            public Action Click;
            public Color Fill;
            public float Hover;
            /// <summary>1 just after a press, fading to 0: the button sinks a little and lights up.</summary>
            public float Press;
            public bool Shown;
        }

        sealed class Text
        {
            public TypeLabel Label;
            public Color Color;
            public bool Shown;
        }

        sealed class Row
        {
            public Button Play, Lyrics, Remove;
            public Text Title, Status, Artist;
        }

        /// <summary>The smallest letters on the page (m, before the page's scale): the artists and statuses.</summary>
        public const float SmallText = 0.0125f;
        const int RowsPerPage = 4;
        const float Width = 0.6f, Height = 0.43f;
        const float RowStep = 0.062f, FirstRow = 0.105f;
        const float TitleWidth = 0.3f, ArtistWidth = 0.21f;
        const float ToastSeconds = 4f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EdgeId = Shader.PropertyToID("_Edge");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int PictureId = Shader.PropertyToID("_Picture");
        static readonly Color PanelFill = new Color(0.05f, 0.055f, 0.09f, 0.88f);
        static readonly Color PanelEdge = new Color(1f, 1f, 1f, 0.2f);
        static readonly Color Primary = new Color(0.16f, 0.55f, 0.4f, 0.96f);
        static readonly Color Secondary = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color Danger = new Color(0.72f, 0.2f, 0.26f, 0.96f);
        static readonly Color RowFill = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color PlayingFill = new Color(0.42f, 0.38f, 1f, 0.26f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.72f);
        static readonly Color HasLyrics = new Color(0.55f, 0.95f, 0.7f);
        static readonly Color NoLyrics = new Color(1f, 0.8f, 0.45f);
        static readonly Color MakeColor = new Color(0.42f, 0.38f, 1f, 0.9f);

        readonly Transform page;
        readonly Renderer panel;
        readonly Vector2 center;
        readonly float scale;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly List<Button> buttons = new List<Button>();
        readonly List<Text> texts = new List<Text>();
        readonly List<SongAsset> songs = new List<SongAsset>();
        readonly Row[] rows = new Row[RowsPerPage];
        readonly Material material;
        readonly Button add, make, back, previous, next, confirm, decline, forget;
        readonly Button[] choices = new Button[5];
        readonly Picture[] pictures = new Picture[5];
        readonly Text heading, count, pageNumber, line1, line2, line3, line4, detail1, detail2, toast;

        View view;
        SongAsset focus, playing;
        IReadOnlyList<SongAsset> previewSongs;
        int pageIndex;
        float fade, toastLeft, keyFileCheck;
        int theme, genre;
        TouchScreenKeyboard keyboard;
        SongMaker.Status previewStatus;
        string previewError;

        /// <summary>True while the page is showing in the menu.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>The page's rectangle in the menu's space, where a fingertip can touch it.</summary>
        public Rect Area => new Rect(center.x - Width * scale * 0.5f, center.y - Height * scale * 0.5f, Width * scale, Height * scale);

        /// <summary>Raised on any press, for the menu's sound.</summary>
        public event Action Pressed;

        /// <summary>Raised when the file picker opens, so the menu steps out of its way.</summary>
        public event Action PickerOpened;

        /// <summary>Raised by the page's Back button, after it hides: the menu goes back to the cassette deck.</summary>
        public event Action BackPressed;

        /// <param name="center">Where the middle of the page goes in the menu's space.</param>
        /// <param name="scale">How large the page is drawn in the menu (1 is full size); presses are scaled to match.</param>
        public SongLibrary(Transform parent, Material panelMaterial, Vector2 center, float scale = 1f)
        {
            this.center = center;
            this.scale = scale;
            material = panelMaterial;
            page = new GameObject("Song Library").transform;
            page.SetParent(parent, false);
            page.localPosition = new Vector3(center.x, center.y, 0.003f);
            page.localScale = Vector3.one * scale;
            panel = Effects.MakeQuad("Panel", page, material);
            panel.transform.localScale = new Vector3(Width, Height, 1f);
            panel.sortingOrder = -2;

            heading = MakeText("Heading", 0.024f, true);
            count = MakeText("Count", 0.0125f, true);
            pageNumber = MakeText("Page", 0.0135f, false);
            line1 = MakeText("Line 1", 0.022f, false);
            line2 = MakeText("Line 2", 0.0155f, false);
            line3 = MakeText("Line 3", 0.0125f, false);
            line4 = MakeText("Line 4", 0.0125f, false);
            detail1 = MakeText("Detail 1", 0.0145f, false);
            detail2 = MakeText("Detail 2", 0.0145f, false);
            toast = MakeText("Message", 0.0135f, false);
            for (int i = 0; i < RowsPerPage; i++)
                rows[i] = new Row
                {
                    Play = MakeButton($"Row {i}", 0.0125f),
                    Title = MakeText($"Row {i} Title", 0.0175f, true),
                    Status = MakeText($"Row {i} Lyrics Status", SmallText, true),
                    Artist = MakeText($"Row {i} Artist", SmallText, true),
                    Lyrics = MakeButton($"Row {i} Lyrics", 0.0125f),
                    Remove = MakeButton($"Row {i} Remove", 0.018f)
                };
            add = MakeButton("Add", 0.0155f);
            make = MakeButton("Make", 0.0155f);
            back = MakeButton("Back", 0.0145f);
            previous = MakeButton("Previous Page", 0.018f);
            next = MakeButton("Next Page", 0.018f);
            confirm = MakeButton("Confirm", 0.0155f);
            decline = MakeButton("Decline", 0.0155f);
            forget = MakeButton("Forget Key", 0.0125f);
            for (int i = 0; i < choices.Length; i++) choices[i] = MakeButton($"Choice {i}", 0.0135f);
            for (int i = 0; i < pictures.Length; i++)
            {
                pictures[i] = new Picture { Quad = Effects.MakeQuad($"Emoji {i}", page, material) };
                pictures[i].Quad.gameObject.SetActive(false);
            }
            page.gameObject.SetActive(false);
        }

        Button MakeButton(string name, float textHeight)
        {
            var b = new Button { Back = Effects.MakeQuad(name, page, material), Label = new TypeLabel($"{name} Label", page, textHeight) };
            b.Back.sortingOrder = -1;
            buttons.Add(b);
            return b;
        }

        Text MakeText(string name, float height, bool left)
        {
            var t = new Text { Label = new TypeLabel(name, page, height, 0.5f, left) };
            t.Label.Transform.localPosition = Vector3.back * 0.002f;
            texts.Add(t);
            return t;
        }

        public void Show()
        {
            IsOpen = true;
            view = View.List;
            focus = null;
        }

        public void Hide() => IsOpen = false;

        void Back()
        {
            Hide();
            BackPressed?.Invoke();
        }

        /// <summary>Fills the page with made-up songs, for the editor screenshots.</summary>
        public void Preview(IReadOnlyList<SongAsset> list, SongAsset current, int previewView)
        {
            previewSongs = list;
            playing = current;
            IsOpen = true;
            view = (View)previewView;
            focus = list != null && list.Count > 0 ? list[0] : null;
            if (view == View.List) toastLeft = ToastSeconds;
            if (view == View.List) toast.Label.Text = "Lyrics added to Sunday Morning";
            // The song maker's steps, as if a Happy rock song were chosen and being composed.
            theme = 1;
            genre = 1;
            previewStatus = view == View.Failed ? SongMaker.Status.Failed : SongMaker.Status.Composing;
            previewError = SongMaker.Explain(401, "{\"detail\":{\"status\":\"missing_permissions\",\"message\":\"The API key you used is missing the permission music_generation to execute this operation.\"}}", null, out _);
        }

        SongMaker.Status MakerState => previewSongs != null ? previewStatus : SongMaker.State;
        string MakerError => previewSongs != null ? previewError : SongMaker.Error;
        bool MakerKeyProblem => previewSongs != null || SongMaker.KeyProblem;

        /// <summary>
        /// Takes in what the file picker did. True when the menu should come back to show it; <paramref name="outcome"/>
        /// says what happened.
        /// </summary>
        public bool Poll(MusicBus bus, out SongPicker.Outcome outcome)
        {
            outcome = SongPicker.Outcome.Cancelled;
            if (bus == null || !SongPicker.TryTake(out var result)) return false;
            outcome = result.Outcome;
            Show();
            switch (result.Outcome)
            {
                case SongPicker.Outcome.Cancelled:
                    break;
                case SongPicker.Outcome.Unsupported:
                    Say(result.Kind == SongPicker.Kind.Song ? "That file type can't play. Choose an .mp3, .ogg or .wav file." : "Lyrics need to be an .lrc or .txt file.");
                    break;
                case SongPicker.Outcome.Failed:
                    Say("Couldn't add that file. Please try again.");
                    break;
                default:
                    if (result.Kind == SongPicker.Kind.Song) SongAdded(bus, result.File);
                    else LyricsAdded(bus, result.Song);
                    break;
            }
            return true;
        }

        void SongAdded(MusicBus bus, string file)
        {
            bus.RefreshSongs();
            var path = Path.Combine(AddedSongs.Folder, file);
            foreach (var song in bus.Songs)
            {
                if (song == null || song.audioPath != path) continue;
                Debug.Log($"[Songs] Added {song.title}.");
                bus.Play(song);
                if (song.lyricsPath != null)
                {
                    Say($"{song.title} is playing, with its lyrics");
                    return;
                }
                focus = song;
                view = View.AskLyrics;
                return;
            }
            Say("Couldn't find the new song. Please try again.");
        }

        void LyricsAdded(MusicBus bus, SongAsset song)
        {
            if (song == null) return;
            bus.RefreshSongs();
            try
            {
                // A song in memory read its lyrics when it was loaded, so it needs the new ones now.
                if (song.clip != null && song.lyricsPath != null)
                {
                    if (song.lyrics != null) UnityEngine.Object.Destroy(song.lyrics);
                    song.lyrics = new TextAsset(File.ReadAllText(song.lyricsPath));
                }
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[Songs] Couldn't read the lyrics of {song.title}: {e.Message}");
            }
            Debug.Log($"[Songs] Added lyrics to {song.title}.");
            bus.Play(song);
            Say($"Lyrics added to {song.title}");
        }

        void Pick(SongPicker.Kind kind, SongAsset song)
        {
            if (SongPicker.Open(kind, song))
            {
                PickerOpened?.Invoke();
                return;
            }
            Say("The file picker opens on the headset");
        }

        void Say(string message)
        {
            toast.Label.Text = Fit(message, 0.0135f, Width - 0.06f);
            toastLeft = ToastSeconds;
        }

        void Remove(MusicBus bus, SongAsset song)
        {
            view = View.List;
            if (bus == null || song == null) return;
            string title = song.title;
            // Move on first: the song being played can't go until the next one has taken over.
            if (bus.CurrentSong == song) bus.StepSong(1);
            try
            {
                File.Delete(song.audioPath);
                if (song.lyricsPath != null) File.Delete(song.lyricsPath);
                Say($"Removed {title}");
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[Songs] Couldn't remove {title}: {e.Message}");
                Say($"Couldn't remove {title}");
            }
            bus.RefreshSongs();
        }

        /// <summary>
        /// One frame of the page. <paramref name="aims"/> are where the two hands point or hover, in the menu's space,
        /// and <paramref name="presses"/> where each touched or pinched this frame.
        /// </summary>
        public void Step(MusicBus bus, bool menuOpen, float dt, Vector2?[] aims, Vector2?[] presses)
        {
            fade = Mathf.MoveTowards(fade, menuOpen && IsOpen ? 1f : 0f, dt * 6f);
            bool show = fade > 0.001f;
            if (page.gameObject.activeSelf != show) page.gameObject.SetActive(show);
            toastLeft -= dt;
            PollMaker(bus);
            PollKeyboard();
            if (!show) return;

            Gather(bus);
            foreach (var b in buttons) b.Shown = false;
            foreach (var t in texts) t.Shown = false;
            foreach (var p in pictures) p.Shown = false;
            switch (view)
            {
                case View.AskLyrics: LayOutAskLyrics(); break;
                case View.ConfirmRemove: LayOutConfirmRemove(bus); break;
                case View.Connect: LayOutConnect(); break;
                case View.Theme: LayOutChoices(SongMaker.Themes, theme, true); break;
                case View.Genre: LayOutChoices(SongMaker.Genres, genre, false); break;
                case View.Confirm: LayOutConfirm(bus); break;
                case View.Making: LayOutMaking(); break;
                case View.Failed: LayOutFailed(bus); break;
                default: LayOutList(bus); break;
            }
            if (toastLeft > 0f && (view == View.List || view == View.Connect)) Show(toast, null, new Vector2(0f, -0.14f), Soft);

            Style(panel, new Vector2(Width, Height), 0.022f, PanelFill, PanelEdge, 0f);
            var pressed = Topmost(presses[0]) ?? Topmost(presses[1]);
            foreach (var b in buttons)
            {
                if (b.Back.gameObject.activeSelf != b.Shown)
                {
                    b.Back.gameObject.SetActive(b.Shown);
                    b.Label.GameObject.SetActive(b.Shown);
                }
                if (!b.Shown) continue;
                b.Hover = Mathf.MoveTowards(b.Hover, Topmost(aims[0]) == b || Topmost(aims[1]) == b ? 1f : 0f, dt * 8f);
                // A pressed button sinks a little and lights up for a moment, whether touched or pinched.
                b.Press = Mathf.MoveTowards(b.Press, 0f, dt * 4f);
                var size = b.Size * (1f - 0.06f * b.Press);
                b.Back.transform.localScale = new Vector3(size.x, size.y, 1f);
                Style(b.Back, size, Mathf.Min(0.012f, size.y * 0.5f), b.Fill, Color.clear, Mathf.Max(b.Hover, b.Press));
                b.Label.Color = new Color(1f, 1f, 1f, fade);
            }
            foreach (var t in texts)
            {
                if (t.Label.GameObject.activeSelf != t.Shown) t.Label.GameObject.SetActive(t.Shown);
                if (t.Shown) t.Label.Color = new Color(t.Color.r, t.Color.g, t.Color.b, t.Color.a * fade);
            }
            foreach (var p in pictures)
            {
                bool shown = p.Shown && p.Texture != null;
                if (p.Quad.gameObject.activeSelf != shown) p.Quad.gameObject.SetActive(shown);
                if (!shown) continue;
                p.Quad.transform.localPosition = new Vector3(p.At.x, p.At.y, -0.001f);
                p.Quad.transform.localScale = new Vector3(p.Size, p.Size, 1f);
                Style(p.Quad, new Vector2(p.Size, p.Size), 0f, Color.clear, Color.clear, 0f);
                block.SetTexture(MainTexId, p.Texture);
                block.SetFloat(PictureId, 1f);
                p.Quad.SetPropertyBlock(block);
            }
            if (pressed == null) return;
            pressed.Press = 1f;
            Pressed?.Invoke();
            pressed.Click();
        }

        void Gather(MusicBus bus)
        {
            songs.Clear();
            var all = previewSongs ?? bus?.Songs;
            if (all != null)
                foreach (var song in all)
                    if (song != null && song.IsAdded) songs.Add(song);
            if (previewSongs == null) playing = bus != null && !bus.IsListening ? bus.CurrentSong : null;
            pageIndex = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, Pages - 1));
        }

        int Pages => (songs.Count + RowsPerPage - 1) / RowsPerPage;

        void LayOutPages()
        {
            if (Pages <= 1) return;
            Show(pageNumber, $"{pageIndex + 1} / {Pages}", new Vector2(0.165f, -0.178f), Soft);
            if (pageIndex > 0) Put(previous, new Vector2(0.1f, -0.178f), new Vector2(0.05f, 0.042f), "<", Secondary, () => pageIndex--);
            if (pageIndex < Pages - 1) Put(next, new Vector2(0.23f, -0.178f), new Vector2(0.05f, 0.042f), ">", Secondary, () => pageIndex++);
        }

        void LayOutList(MusicBus bus)
        {
            Show(heading, "Your songs", new Vector2(-Width * 0.5f + 0.03f, 0.172f), Color.white);
            if (songs.Count > 0) Show(count, songs.Count == 1 ? "1 song" : $"{songs.Count} songs", new Vector2(-Width * 0.5f + 0.03f, 0.145f), Soft);
            Put(add, new Vector2(0.2f, 0.165f), new Vector2(0.15f, 0.05f), "+  Add a song", Primary, () => Pick(SongPicker.Kind.Song, null));
            var state = MakerState;
            string makeLabel = previewSongs == null && SongMaker.Busy ? "Making…" : previewSongs == null && state == SongMaker.Status.Failed ? "Song failed" : "Make a song";
            Put(make, new Vector2(0.035f, 0.165f), new Vector2(0.15f, 0.05f), makeLabel, MakeColor, StartMaking);
            Put(back, new Vector2(-0.215f, -0.178f), new Vector2(0.11f, 0.042f), "Back", Secondary, Back);

            if (songs.Count == 0)
            {
                Show(line1, "No songs yet", new Vector2(0f, 0.05f), Color.white);
                Show(line2, "Add an .mp3, .ogg or .wav file from this headset, or make one.", new Vector2(0f, 0.012f), Soft);
                Show(line3, "Make a song needs your own ElevenLabs account.", new Vector2(0f, -0.018f), Soft);
                return;
            }

            for (int i = 0; i < RowsPerPage; i++)
            {
                int index = pageIndex * RowsPerPage + i;
                if (index < songs.Count) LayOutRow(rows[i], songs[index], FirstRow - i * RowStep, bus);
            }
            LayOutPages();
        }

        void LayOutRow(Row row, SongAsset song, float y, MusicBus bus)
        {
            bool isPlaying = song == playing;
            bool lyrics = song.lyricsPath != null;
            const float left = -Width * 0.5f + 0.042f;
            Put(row.Play, new Vector2(0f, y), new Vector2(Width - 0.04f, 0.056f), "", isPlaying ? PlayingFill : RowFill, () => bus?.Play(song));
            Show(row.Title, Fit(song.title, 0.0175f, TitleWidth), new Vector2(left, y + 0.0095f), Color.white);
            Show(row.Status, lyrics ? "Has lyrics" : "No lyrics", new Vector2(left, y - 0.0135f), lyrics ? HasLyrics : NoLyrics);
            string detail = isPlaying ? (string.IsNullOrEmpty(song.artist) ? "Playing now" : $"Playing now  ·  {song.artist}") : song.artist;
            if (!string.IsNullOrEmpty(detail)) Show(row.Artist, Fit(detail, SmallText, ArtistWidth), new Vector2(left + 0.078f, y - 0.0135f), Soft);
            Put(row.Lyrics, new Vector2(0.135f, y), new Vector2(0.125f, 0.038f), lyrics ? "Change lyrics" : "Add lyrics", lyrics ? Secondary : Primary,
                () => Pick(SongPicker.Kind.Lyrics, song));
            Put(row.Remove, new Vector2(0.24f, y), new Vector2(0.04f, 0.038f), "×", Secondary, () =>
            {
                focus = song;
                view = View.ConfirmRemove;
            });
        }

        void LayOutAskLyrics()
        {
            Show(heading, "Song added", new Vector2(-Width * 0.5f + 0.03f, 0.172f), HasLyrics);
            Show(line1, Fit(focus != null ? focus.title : "", 0.022f, Width - 0.08f), new Vector2(0f, 0.095f), Color.white);
            Show(line2, "It's playing now. Do you have its lyrics?", new Vector2(0f, 0.055f), Soft);
            Put(confirm, new Vector2(-0.08f, -0.01f), new Vector2(0.22f, 0.056f), "Choose lyrics file", Primary, () => Pick(SongPicker.Kind.Lyrics, focus));
            Put(decline, new Vector2(0.125f, -0.01f), new Vector2(0.13f, 0.056f), "Not now", Secondary, () => view = View.List);
            Show(line3, "An .lrc file follows the song's timing. A .txt file spreads its lines across the song.", new Vector2(0f, -0.075f), Soft);
        }

        void LayOutConfirmRemove(MusicBus bus)
        {
            Show(heading, "Remove this song?", new Vector2(-Width * 0.5f + 0.03f, 0.172f), Color.white);
            Show(line1, Fit(focus != null ? focus.title : "", 0.022f, Width - 0.08f), new Vector2(0f, 0.095f), Color.white);
            Show(line2, "This deletes the app's copy of the song and its lyrics.", new Vector2(0f, 0.055f), Soft);
            Put(confirm, new Vector2(-0.08f, -0.01f), new Vector2(0.17f, 0.056f), "Remove", Danger, () => Remove(bus, focus));
            Put(decline, new Vector2(0.1f, -0.01f), new Vector2(0.13f, 0.056f), "Keep", Secondary, () => view = View.List);
        }

        // --- Making a song ----------------------------------------------------------------------------------------

        /// <summary>The list's Make a song button: picks up where the song maker is, or starts at the key or the theme.</summary>
        void StartMaking()
        {
            var state = MakerState;
            if (SongMaker.Busy) view = View.Making;
            else if (state == SongMaker.Status.Failed) view = View.Failed;
            else view = SongMaker.HasKey ? View.Theme : View.Connect;
        }

        void LayOutConnect()
        {
            const float wide = Width - 0.06f;
            Show(heading, "Make a song", new Vector2(-Width * 0.5f + 0.03f, 0.172f), Color.white);
            Show(line1, "Connect your ElevenLabs account", new Vector2(0f, 0.105f), Color.white);
            Show(line2, Fit("Use your own API key, with Music and Speech to Text allowed.", 0.0155f, wide), new Vector2(0f, 0.068f), Soft);
            Show(line3, Fit("It stays on this headset and is only ever sent to ElevenLabs.", 0.0125f, wide), new Vector2(0f, 0.04f), Soft);
            Put(confirm, new Vector2(0f, -0.022f), new Vector2(0.2f, 0.056f), "Type my key", Primary, OpenKeyboard);
            Show(line4, Fit($"Or copy a file named \"{SongMaker.KeyFileName}\" into the app's folder.", 0.0125f, wide), new Vector2(0f, -0.085f), Soft);
            Put(back, new Vector2(-0.215f, -0.178f), new Vector2(0.11f, 0.042f), "Back", Secondary, () => view = View.List);

            // A key file copied over: take it, and go on to the themes.
            if (previewSongs != null || Time.unscaledTime < keyFileCheck) return;
            keyFileCheck = Time.unscaledTime + 1f;
            if (!SongMaker.ImportKeyFile()) return;
            Say($"Key added from {SongMaker.KeyFileName}");
            view = View.Theme;
        }

        void OpenKeyboard()
        {
            if (!TouchScreenKeyboard.isSupported)
            {
                Say("The keyboard opens on the headset");
                return;
            }
            keyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default, false, false, true, false, "Your ElevenLabs API key");
        }

        void PollKeyboard()
        {
            if (keyboard == null) return;
            switch (keyboard.status)
            {
                case TouchScreenKeyboard.Status.Done:
                    if (SongMaker.SetKey(keyboard.text))
                    {
                        Say("Connected to ElevenLabs");
                        if (view == View.Connect) view = View.Theme;
                    }
                    keyboard = null;
                    break;
                case TouchScreenKeyboard.Status.Canceled:
                case TouchScreenKeyboard.Status.LostFocus:
                    keyboard = null;
                    break;
            }
        }

        /// <summary>Five emoji tiles to pick from: the themes, then the genres.</summary>
        void LayOutChoices(SongMaker.Choice[] list, int selected, bool themes)
        {
            Show(heading, themes ? "Pick a theme" : "Pick a genre", new Vector2(-Width * 0.5f + 0.03f, 0.172f), Color.white);
            Show(count, themes ? "Step 1 of 2" : $"Step 2 of 2  ·  {SongMaker.Themes[theme].Name}", new Vector2(-Width * 0.5f + 0.03f, 0.145f), Soft);
            for (int i = 0; i < choices.Length && i < list.Length; i++)
            {
                var c = list[i];
                int pick = i;
                var at = new Vector2(-0.22f + i * 0.11f, 0.025f);
                var fill = new Color(c.Color.r, c.Color.g, c.Color.b, i == selected ? 0.42f : 0.18f);
                Put(choices[i], at, new Vector2(0.1f, 0.13f), c.Name, fill, () =>
                {
                    if (themes)
                    {
                        theme = pick;
                        view = View.Genre;
                    }
                    else
                    {
                        genre = pick;
                        view = View.Confirm;
                    }
                });
                // With its emoji the name goes underneath; without (in the editor) it stays in the middle.
                if (ShowPicture(i, c.Emoji, at + new Vector2(0f, 0.014f), 0.068f))
                    choices[i].Label.Transform.localPosition = new Vector3(at.x, at.y - 0.044f, -0.002f);
            }
            Show(line3, themes ? "What's your song about?" : "How should it sound?", new Vector2(0f, -0.085f), Soft);
            Put(back, new Vector2(-0.215f, -0.178f), new Vector2(0.11f, 0.042f), "Back", Secondary, () => view = themes ? View.List : View.Theme);
            if (themes)
                Put(forget, new Vector2(0.205f, -0.178f), new Vector2(0.15f, 0.042f), "Forget my key", Secondary, () =>
                {
                    SongMaker.ForgetKey();
                    view = View.Connect;
                });
        }

        void LayOutConfirm(MusicBus bus)
        {
            const float wide = Width - 0.06f;
            var t = SongMaker.Themes[theme];
            var g = SongMaker.Genres[genre];
            Show(heading, "Make this song?", new Vector2(-Width * 0.5f + 0.03f, 0.172f), Color.white);
            ShowPicture(0, t.Emoji, new Vector2(-0.045f, 0.1f), 0.075f);
            ShowPicture(1, g.Emoji, new Vector2(0.045f, 0.1f), 0.075f);
            Show(line1, $"{t.Name}  ·  {g.Name}", new Vector2(0f, 0.035f), Color.white);
            Show(line2, Fit($"About {SongMaker.SongSeconds / 60} minutes, with lyrics. Takes a minute or two to make.", 0.0155f, wide), new Vector2(0f, 0.002f), Soft);
            Show(line3, Fit($"Uses about {SongMaker.ApproxCredits:N0} of your ElevenLabs credits.", 0.0125f, wide), new Vector2(0f, -0.026f), NoLyrics);
            Put(confirm, new Vector2(-0.08f, -0.09f), new Vector2(0.2f, 0.056f), "Make my song", Primary, () => Make(bus));
            Put(decline, new Vector2(0.1f, -0.09f), new Vector2(0.13f, 0.056f), "Back", Secondary, () => view = View.Genre);
        }

        void Make(MusicBus bus)
        {
            if (previewSongs != null) return;
            if (SongMaker.Start(bus, theme, genre)) view = View.Making;
            else if (!SongMaker.HasKey) view = View.Connect;
            else if (SongMaker.Busy) view = View.Making;
        }

        void LayOutMaking()
        {
            var state = MakerState;
            int t = previewSongs != null ? theme : SongMaker.Theme, g = previewSongs != null ? genre : SongMaker.Genre;
            Show(heading, "Making your song", new Vector2(-Width * 0.5f + 0.03f, 0.172f), Color.white);
            // The emojis bob gently while it works.
            float bob = 0.004f * Mathf.Sin(Time.unscaledTime * 3f);
            ShowPicture(0, SongMaker.Themes[t].Emoji, new Vector2(-0.045f, 0.1f + bob), 0.075f);
            ShowPicture(1, SongMaker.Genres[g].Emoji, new Vector2(0.045f, 0.1f - bob), 0.075f);
            var (what, step) = state switch
            {
                SongMaker.Status.Writing => ("Writing the lyrics…", 1),
                SongMaker.Status.Composing => ("Composing the music…", 2),
                SongMaker.Status.Timing => ("Timing the lyrics…", 3),
                SongMaker.Status.Saving => ("Saving your song…", 3),
                _ => ("Getting ready…", 1)
            };
            Show(line1, what, new Vector2(0f, 0.035f), Color.white);
            Show(line2, $"Step {step} of 3  ·  {SongMaker.Themes[t].Name} {SongMaker.Genres[g].Name}", new Vector2(0f, 0.002f), Soft);
            Show(line3, "It keeps going if you close the menu, and plays when it's ready.", new Vector2(0f, -0.026f), Soft);
            Put(confirm, new Vector2(0f, -0.09f), new Vector2(0.17f, 0.056f), "Your songs", Secondary, () => view = View.List);
        }

        void LayOutFailed(MusicBus bus)
        {
            Show(heading, "Couldn't make the song", new Vector2(-Width * 0.5f + 0.03f, 0.172f), NoLyrics);
            Wrap(MakerError ?? "Something went wrong.", 0.0145f, Width - 0.08f, out var first, out var second);
            Show(detail1, first, new Vector2(0f, 0.085f), Color.white);
            if (second.Length > 0) Show(detail2, second, new Vector2(0f, 0.058f), Color.white);
            Show(line3, "Nothing was saved.", new Vector2(0f, 0.022f), Soft);
            Put(confirm, new Vector2(-0.08f, -0.03f), new Vector2(0.15f, 0.056f), "Try again", Primary, () =>
            {
                if (previewSongs != null) return;
                if (SongMaker.Start(bus, SongMaker.Theme, SongMaker.Genre)) view = View.Making;
                else if (!SongMaker.HasKey) view = View.Connect;
            });
            if (MakerKeyProblem)
                Put(decline, new Vector2(0.1f, -0.03f), new Vector2(0.15f, 0.056f), "Change key", Secondary, () =>
                {
                    SongMaker.Dismiss();
                    SongMaker.ForgetKey();
                    view = View.Connect;
                });
            Put(back, new Vector2(-0.215f, -0.178f), new Vector2(0.11f, 0.042f), "Back", Secondary, () =>
            {
                SongMaker.Dismiss();
                view = View.List;
            });
        }

        /// <summary>The finished song plays and joins the list; a failure shows on the page if it's waiting for it.</summary>
        void PollMaker(MusicBus bus)
        {
            if (bus == null || previewSongs != null) return;
            if (SongMaker.TryTakeMade(out var path, out var title))
            {
                bus.RefreshSongs();
                foreach (var song in bus.Songs)
                    if (song != null && song.audioPath == path)
                    {
                        bus.Play(song);
                        break;
                    }
                Say($"{title} is ready and playing");
                if (view == View.Making) view = View.List;
            }
            else if (SongMaker.State == SongMaker.Status.Failed && view == View.Making) view = View.Failed;
        }

        /// <summary>Shows an emoji. False where it can't be drawn (in the editor).</summary>
        bool ShowPicture(int index, string emoji, Vector2 at, float size)
        {
            var p = pictures[index];
            p.Texture = EmojiPictures.Get(emoji);
            p.At = at;
            p.Size = size;
            p.Shown = true;
            return p.Texture != null;
        }

        /// <summary>Splits text into two lines that fit a width; the second is shortened if it still doesn't fit.</summary>
        public static void Wrap(string text, float height, float width, out string first, out string second)
        {
            var face = TypeFace.Bold;
            var words = (text ?? "").Split(' ');
            int split = words.Length;
            for (int n = 1; n <= words.Length; n++)
                if (face.Width(string.Join(" ", words, 0, n)) * height > width)
                {
                    split = Mathf.Max(1, n - 1);
                    break;
                }
            first = Fit(string.Join(" ", words, 0, split), height, width);
            second = split < words.Length ? Fit(string.Join(" ", words, split, words.Length - split), height, width) : "";
        }

        void Show(Text text, string value, Vector2 at, Color color)
        {
            if (value != null) text.Label.Text = value;
            text.Label.Transform.localPosition = new Vector3(at.x, at.y, -0.002f);
            text.Color = color;
            text.Shown = true;
        }

        void Put(Button b, Vector2 at, Vector2 size, string label, Color fill, Action click)
        {
            b.Shown = true;
            b.Center = at;
            b.Size = size;
            b.Fill = fill;
            b.Click = click;
            b.Label.Text = label;
            b.Back.transform.localPosition = new Vector3(at.x, at.y, 0f);
            b.Back.transform.localScale = new Vector3(size.x, size.y, 1f);
            b.Label.Transform.localPosition = new Vector3(at.x, at.y, -0.002f);
        }

        /// <summary>The button under a point; the small ones on a row sit above the row itself.</summary>
        Button Topmost(Vector2? point)
        {
            if (point == null) return null;
            var p = (point.Value - center) / scale;
            for (int i = buttons.Count - 1; i >= 0; i--)
            {
                var b = buttons[i];
                if (!b.Shown) continue;
                var d = p - b.Center;
                if (Mathf.Abs(d.x) <= b.Size.x * 0.5f && Mathf.Abs(d.y) <= b.Size.y * 0.5f) return b;
            }
            return null;
        }

        void Style(Renderer r, Vector2 size, float radius, Color fill, Color edge, float hover)
        {
            block.Clear();
            block.SetColor(ColorId, fill);
            block.SetColor(EdgeId, edge);
            block.SetVector(SizeId, size);
            block.SetFloat(RadiusId, radius);
            block.SetFloat(HoverId, hover);
            block.SetFloat(AlphaId, fade);
            r.SetPropertyBlock(block);
        }

        /// <summary>Shortens text with an ellipsis to fit a width (m) at a letter height (m).</summary>
        internal static string Fit(string text, float height, float width)
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
    }
}
