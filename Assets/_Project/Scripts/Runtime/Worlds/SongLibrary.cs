using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The menu's song page, with a tab for the songs people add themselves and one for the songs from Audius. Your
    /// songs are listed with whether each has lyrics; touching or pinching a row plays that song, and each row can add or change its
    /// lyrics or remove the song. Adding a song is a guided step at a time: pick the song from the headset's files, it
    /// starts playing straight away, then the page offers to add its lyrics. The menu steps aside while the file
    /// picker's window is open and comes back when it is done. The Audius tab lists the songs the app streams, each with
    /// the credit its license asks for; a song's Credits page adds the link to its page on Audius.
    /// The page is reached from the cassette deck's song list button, and its Back button goes back to the deck.
    /// </summary>
    public sealed class SongLibrary
    {
        // Numbered for the editor screenshots (Preview), so new views go at the end.
        enum View { List, AskLyrics, ConfirmRemove, Audius, Credits }

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
            public Button Play, Lyrics, Remove, Credits;
            public Text Title, Status, Artist;
        }

        const int RowsPerPage = 4;
        const float Width = 0.6f, Height = 0.43f;
        const float RowStep = 0.062f, FirstRow = 0.105f;
        const float TitleWidth = 0.3f, ArtistWidth = 0.21f, StreamedWidth = 0.37f;
        const float ToastSeconds = 4f;
        const string StreamedNote = "Streamed from Audius under the " + AudiusSongs.License;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EdgeId = Shader.PropertyToID("_Edge");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly Color PanelFill = new Color(0.05f, 0.055f, 0.09f, 0.88f);
        static readonly Color PanelEdge = new Color(1f, 1f, 1f, 0.2f);
        static readonly Color Primary = new Color(0.16f, 0.55f, 0.4f, 0.96f);
        static readonly Color Secondary = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color Danger = new Color(0.72f, 0.2f, 0.26f, 0.96f);
        static readonly Color RowFill = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color PlayingFill = new Color(0.42f, 0.38f, 1f, 0.26f);
        static readonly Color TabOn = new Color(0.42f, 0.38f, 1f, 0.7f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.72f);
        static readonly Color HasLyrics = new Color(0.55f, 0.95f, 0.7f);
        static readonly Color NoLyrics = new Color(1f, 0.8f, 0.45f);
        static readonly Color Link = new Color(0.55f, 0.85f, 1f);

        readonly Transform page;
        readonly Renderer panel;
        readonly Vector2 center;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly List<Button> buttons = new List<Button>();
        readonly List<Text> texts = new List<Text>();
        readonly List<SongAsset> songs = new List<SongAsset>();
        readonly Row[] rows = new Row[RowsPerPage];
        readonly Material material;
        readonly Button add, back, previous, next, confirm, decline, yours, streamed, retry;
        readonly Text heading, count, pageNumber, line1, line2, line3, line4, footer, toast;

        View view, tab;
        SongAsset focus, playing;
        IReadOnlyList<SongAsset> previewSongs;
        int pageIndex;
        float fade, toastLeft;

        /// <summary>True while the page is showing in the menu.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>The page's rectangle in the menu's space, where a fingertip can touch it.</summary>
        public Rect Area => new Rect(center.x - Width * 0.5f, center.y - Height * 0.5f, Width, Height);

        /// <summary>Raised on any press, for the menu's sound.</summary>
        public event Action Pressed;

        /// <summary>Raised when the file picker opens, so the menu steps out of its way.</summary>
        public event Action PickerOpened;

        /// <summary>Raised by the page's Back button, after it hides: the menu goes back to the cassette deck.</summary>
        public event Action BackPressed;

        public SongLibrary(Transform parent, Material panelMaterial, Vector2 center)
        {
            this.center = center;
            material = panelMaterial;
            page = new GameObject("Song Library").transform;
            page.SetParent(parent, false);
            page.localPosition = new Vector3(center.x, center.y, 0.003f);
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
            footer = MakeText("Footer", 0.0118f, false);
            toast = MakeText("Message", 0.0135f, false);
            for (int i = 0; i < RowsPerPage; i++)
                rows[i] = new Row
                {
                    Play = MakeButton($"Row {i}", 0.0125f),
                    Title = MakeText($"Row {i} Title", 0.0175f, true),
                    Status = MakeText($"Row {i} Lyrics Status", 0.0118f, true),
                    Artist = MakeText($"Row {i} Artist", 0.0118f, true),
                    Lyrics = MakeButton($"Row {i} Lyrics", 0.0125f),
                    Remove = MakeButton($"Row {i} Remove", 0.018f),
                    Credits = MakeButton($"Row {i} Credits", 0.0125f)
                };
            add = MakeButton("Add", 0.0155f);
            back = MakeButton("Back", 0.0145f);
            previous = MakeButton("Previous Page", 0.018f);
            next = MakeButton("Next Page", 0.018f);
            confirm = MakeButton("Confirm", 0.0155f);
            decline = MakeButton("Decline", 0.0155f);
            yours = MakeButton("Your Songs Tab", 0.0145f);
            streamed = MakeButton("Audius Tab", 0.0145f);
            retry = MakeButton("Try Again", 0.0155f);
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

        /// <summary>Opens the page on the tab it last showed.</summary>
        public void Show()
        {
            IsOpen = true;
            view = tab;
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
            tab = view == View.Audius || view == View.Credits ? View.Audius : View.List;
            focus = list != null && list.Count > 0 ? list[0] : null;
            if (view == View.List) toastLeft = ToastSeconds;
            if (view == View.List) toast.Label.Text = "Lyrics added to Sunday Morning";
        }

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
            tab = view = View.List;
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

        void SwitchTo(View which, MusicBus bus)
        {
            tab = view = which;
            pageIndex = 0;
            if (which == View.Audius) bus?.AskAudius();
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
            if (!show) return;

            Gather(bus);
            foreach (var b in buttons) b.Shown = false;
            foreach (var t in texts) t.Shown = false;
            switch (view)
            {
                case View.AskLyrics: LayOutAskLyrics(); break;
                case View.ConfirmRemove: LayOutConfirmRemove(bus); break;
                case View.Audius: LayOutAudius(bus); break;
                case View.Credits: LayOutCredits(bus); break;
                default: LayOutList(bus); break;
            }
            if (toastLeft > 0f && view == View.List) Show(toast, null, new Vector2(0f, -0.14f), Soft);

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
            if (pressed == null) return;
            pressed.Press = 1f;
            Pressed?.Invoke();
            pressed.Click();
        }

        /// <summary>The songs of the open tab: the ones added on the headset, or the ones from Audius.</summary>
        void Gather(MusicBus bus)
        {
            songs.Clear();
            bool fromAudius = tab == View.Audius;
            var all = previewSongs ?? bus?.Songs;
            if (all != null)
                foreach (var song in all)
                    if (song != null && (fromAudius ? song.IsFromAudius : song.IsAdded)) songs.Add(song);
            if (previewSongs == null) playing = bus != null && !bus.IsListening ? bus.CurrentSong : null;
            pageIndex = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, Pages - 1));
        }

        int Pages => (songs.Count + RowsPerPage - 1) / RowsPerPage;

        void LayOutTabs(MusicBus bus)
        {
            Put(yours, new Vector2(-0.205f, 0.176f), new Vector2(0.13f, 0.034f), "Your songs", tab == View.List ? TabOn : Secondary, () => SwitchTo(View.List, bus));
            Put(streamed, new Vector2(-0.085f, 0.176f), new Vector2(0.1f, 0.034f), "Audius", tab == View.Audius ? TabOn : Secondary, () => SwitchTo(View.Audius, bus));
        }

        void LayOutPages()
        {
            if (Pages <= 1) return;
            Show(pageNumber, $"{pageIndex + 1} / {Pages}", new Vector2(0.165f, -0.178f), Soft);
            if (pageIndex > 0) Put(previous, new Vector2(0.1f, -0.178f), new Vector2(0.05f, 0.042f), "<", Secondary, () => pageIndex--);
            if (pageIndex < Pages - 1) Put(next, new Vector2(0.23f, -0.178f), new Vector2(0.05f, 0.042f), ">", Secondary, () => pageIndex++);
        }

        void LayOutList(MusicBus bus)
        {
            LayOutTabs(bus);
            if (songs.Count > 0) Show(count, songs.Count == 1 ? "1 song" : $"{songs.Count} songs", new Vector2(-Width * 0.5f + 0.03f, 0.145f), Soft);
            Put(add, new Vector2(0.17f, 0.165f), new Vector2(0.19f, 0.05f), "+  Add a song", Primary, () => Pick(SongPicker.Kind.Song, null));
            Put(back, new Vector2(-0.215f, -0.178f), new Vector2(0.11f, 0.042f), "Back", Secondary, Back);

            if (songs.Count == 0)
            {
                Show(line1, "No songs yet", new Vector2(0f, 0.05f), Color.white);
                Show(line2, "Add an .mp3, .ogg or .wav file from this headset.", new Vector2(0f, 0.012f), Soft);
                Show(line3, "Download one in the Browser, or copy it over with a USB cable.", new Vector2(0f, -0.018f), Soft);
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
            if (!string.IsNullOrEmpty(detail)) Show(row.Artist, Fit(detail, 0.0118f, ArtistWidth), new Vector2(left + 0.078f, y - 0.0135f), Soft);
            Put(row.Lyrics, new Vector2(0.135f, y), new Vector2(0.125f, 0.038f), lyrics ? "Change lyrics" : "Add lyrics", lyrics ? Secondary : Primary,
                () => Pick(SongPicker.Kind.Lyrics, song));
            Put(row.Remove, new Vector2(0.24f, y), new Vector2(0.04f, 0.038f), "×", Secondary, () =>
            {
                focus = song;
                view = View.ConfirmRemove;
            });
        }

        /// <summary>The Audius tab: the songs, or why there are none yet.</summary>
        void LayOutAudius(MusicBus bus)
        {
            LayOutTabs(bus);
            Put(back, new Vector2(-0.215f, -0.178f), new Vector2(0.11f, 0.042f), "Back", Secondary, Back);
            var status = previewSongs != null ? AudiusSongs.Status.Ready : bus != null ? bus.AudiusStatus : AudiusSongs.Status.Unreachable;
            if (status == AudiusSongs.Status.NotAsked) bus?.AskAudius();

            if (songs.Count == 0)
            {
                if (status == AudiusSongs.Status.Unreachable)
                {
                    Show(line1, "Couldn't reach Audius", new Vector2(0f, 0.06f), Color.white);
                    Show(line2, "Songs from Audius stream over the internet. Check the headset's Wi-Fi.", new Vector2(0f, 0.022f), Soft);
                    Put(retry, new Vector2(0f, -0.04f), new Vector2(0.15f, 0.05f), "Try again", Primary, () => bus?.AskAudius(true));
                }
                else if (status == AudiusSongs.Status.Ready) Show(line1, "No songs from Audius right now", new Vector2(0f, 0.05f), Color.white);
                else Show(line1, "Getting the songs from Audius…", new Vector2(0f, 0.05f), Soft);
                return;
            }

            Show(count, songs.Count == 1 ? "1 song" : $"{songs.Count} songs", new Vector2(-Width * 0.5f + 0.03f, 0.145f), Soft);
            for (int i = 0; i < RowsPerPage; i++)
            {
                int index = pageIndex * RowsPerPage + i;
                if (index < songs.Count) LayOutStreamedRow(rows[i], songs[index], FirstRow - i * RowStep, bus);
            }
            Show(footer, StreamedNote, new Vector2(0f, -0.135f), Soft);
            LayOutPages();
        }

        void LayOutStreamedRow(Row row, SongAsset song, float y, MusicBus bus)
        {
            bool isPlaying = song == playing;
            bool streaming = bus != null && bus.Loading == song;
            bool failed = bus != null && bus.Failed == song;
            const float left = -Width * 0.5f + 0.042f;
            Put(row.Play, new Vector2(0f, y), new Vector2(Width - 0.04f, 0.056f), "", isPlaying ? PlayingFill : RowFill, () => bus?.Play(song));
            Show(row.Title, Fit(song.title, 0.0175f, StreamedWidth), new Vector2(left, y + 0.0095f), Color.white);
            string detail = streaming ? "Streaming…" : failed ? "Couldn't stream it. Is the headset online?"
                : isPlaying ? $"Playing now  ·  {AudiusSongs.Byline(song)}" : AudiusSongs.Byline(song);
            Show(row.Status, Fit(detail, 0.0118f, StreamedWidth), new Vector2(left, y - 0.0135f), failed ? NoLyrics : streaming ? HasLyrics : Soft);
            Put(row.Credits, new Vector2(0.195f, y), new Vector2(0.13f, 0.038f), "Credits", Secondary, () =>
            {
                focus = song;
                view = View.Credits;
            });
        }

        /// <summary>A song's credits, as its license asks: the song, its artist, the copyright owner, the license and a link.</summary>
        void LayOutCredits(MusicBus bus)
        {
            var song = focus;
            if (song == null)
            {
                view = View.Audius;
                return;
            }
            const float wide = Width - 0.08f;
            bool isPlaying = song == playing;
            Show(heading, "Credits", new Vector2(-Width * 0.5f + 0.03f, 0.172f), Color.white);
            Show(line1, Fit(song.title, 0.022f, wide), new Vector2(0f, 0.11f), Color.white);
            Show(line2, Fit($"by {song.artist}", 0.0155f, wide), new Vector2(0f, 0.074f), Soft);
            Show(line3, Fit($"© {song.owner}", 0.0125f, wide), new Vector2(0f, 0.046f), Soft);
            Show(line4, Fit(AudiusSongs.ShortLink(song), 0.0125f, wide), new Vector2(0f, 0.02f), Link);
            Show(footer, StreamedNote, new Vector2(0f, -0.008f), Soft);
            Put(confirm, new Vector2(-0.08f, -0.075f), new Vector2(0.15f, 0.056f), isPlaying ? "Playing" : "Play", isPlaying ? Secondary : Primary,
                () => { if (song != playing) bus?.Play(song); });
            Put(decline, new Vector2(0.09f, -0.075f), new Vector2(0.13f, 0.056f), "Back", Secondary, () => view = View.Audius);
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
            var p = point.Value - center;
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
    }
}
