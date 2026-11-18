# Music In Your Hands

A hand-tracked mixed reality music visualizer for Meta Quest 3. You play with the music using your bare hands, inside your own room.

## Requirements
- Unity 6000.3.16f1 with Android Build Support
- Meta Quest 3 in developer mode
- Packages (already in `Packages/manifest.json`): Meta XR Core SDK, Interaction SDK, MR Utility Kit, URP, OpenXR, XR Hands

## Project layout
```
Assets/_Project/
  Audio/      four demo songs (WAV) with melody notes (.notes) and three sung songs (MP3), all with word-timed
              lyrics (LRC)
  Songs/      SongAsset files: audio + lyrics + offset
  Resources/  the baked lettering and CassetteDeck/ (the cassette deck's four models and their materials)
  Scenes/     Main.unity
  Scripts/
    Runtime/Music/   MusicBus (bands, beats, song time), SongAsset, AddedSongs
    Runtime/Lyrics/  LyricsTrack (LRC parser and current line/word lookup)
    Runtime/Hands/   HandRig (palm, fingertips, pinch, fist, palm-up for every world)
    Runtime/Worlds/  World (the base every world builds on), the worlds, the menu and its song page
    Runtime/Deck/    CassetteDeck (choosing a song by putting its cassette in a radio)
    Runtime/Debug/   MusicDebugView and HandDebugView (in-headset checks)
    Meta/            OVRHandFeed (Meta hand tracking into HandRig)
    Editor/          Quest setup, scene builder, APK build, LRC importer
Docs/
  concept-book.html      the interactive concept page with all worlds and lyric styles
Tools/
  render_songs.py        renders the demo songs, their LRC lyrics and .notes melodies
  songkit.py             the instruments, voice and file writers the songs share
  songs/                 one script per demo song
  elevenlabs_songs.py    makes the sung songs with ElevenLabs Music and times their lyrics word by word
  blender/cassette_deck.py  builds the cassette deck's models in Blender and exports them to Resources/CassetteDeck
```

## Setup
The project is already set up for Quest, using the same setup as autism-keyboard: Android, IL2CPP/ARM64, Vulkan, OpenXR with the Meta features, hand tracking and passthrough.

1. Install [Git LFS](https://git-lfs.com) once (`git lfs install`), then clone.
2. Open the folder in Unity Hub with Unity 6000.3.16f1. The first import downloads Meta's SDK and takes several minutes.
3. Open `Assets/_Project/Scenes/Main.unity`. It has an `OVRCameraRig` with passthrough and Meta's hand prefabs, `MusicBus` playing the demo song, `HandRig`, and two debug views.
4. **Music In Your Hands > Build APK for Quest** writes `Builds/MusicInYourHands.apk`. Install it with Meta Quest Developer Hub or `adb install -r Builds/MusicInYourHands.apk`, then launch it from **Library > Unknown sources**.

If settings drift, run **Music In Your Hands > Configure Project for Quest**. It reapplies everything and runs Meta's Project Setup Tool "Fix All". **Rebuild Main Scene** recreates the scene from scratch.

## In the headset
- Bars about a metre ahead bounce with the music, and a sphere above them pulses on each beat.
- Below them, the current lyric line shows with the sung word in pink.
- Spheres follow your fingertips: they turn pink when you pinch, and the palm sphere turns yellow when your palm faces up.
- Meta's menu gesture (left palm up, pinch) opens the world menu half a metre along your gaze, within reach (things you touch stay at least 50 cm from the eyes). Touch a bubble or a button on the song page with your index fingertip, or point at it from farther away and pinch. Within 15 cm of the menu the pointer ray gives way to a dot under your fingertip that shrinks as you close in; a press lands when the fingertip reaches the menu, and the next one needs the finger 2 cm back out. Past 20 cm the ray returns.
- The menu is one honeycomb of glass bubbles, every world in menu order, with the song row under it (previous, play or pause, next, listen, lyrics, the cassette deck), the name of the bubble you point at above it, and two hint lines and, for a song from Audius, its credit below. It is built for a narrow field of view: everything on it stays inside Meta's recommended design area, 58° across and 54° high round the middle of your view (with today's worlds the bubbles take up 47° × 38°, and with the words 47° × 48°), with bubbles 66 mm across (Meta's smallest target for hands is 64 mm) and letters at least 1° tall. The honeycomb works out its rows from the number of worlds (5, 5, 5 and 4 for today's 19; 6, 6, 6 and 5 for 23; two rows of 5 for 10; one row for 6), so it shrinks and stays centred as worlds are removed. The song page shows in its place, a little smaller (56° × 41°) so it fits too. **Run Editor Checks** measures all of it from a standing player's eyes (`CheckMenuFieldOfView`), and the `7v-menu-design-area` screenshot draws the design area round it.
- The menu stays where it is while you look round it. Look more than 20° away from it for 0.4 s and it glides back in front of you round your head, easing in and out, up and down as well as sideways (never higher than 15° above or lower than 40° below level). A hand touching it or pointing at it holds it still, so it never slides away from a finger reaching for a bubble. **View Distance**, **Drop Angle**, **Bubble Diameter**, **Bubble Gap**, **Lazy Follow** and **Glide Time** on `WorldMenu` tune it.
- With either hand, a thumb tap on the side of your index finger plays or pauses the song, and a thumb swipe left or right steps to the previous or next one (Meta's microgestures, read by `MicrogestureFeed` and acted on by `MusicGestures`). A short caption and a soft note confirm each one. They are ignored while that hand pinches or makes a fist, during Meta's menu gesture, and while the menu is open; in Listen mode they only say "Listening to the room".
- While the world menu is open, the world behind it leaves your hands alone: pinching or poking a bubble doesn't also draw, spray, pour or pluck.

## The cassette deck
The menu's song bubble (the cassette) opens the cassette deck, where you choose a song by hand. The menu steps aside and a small table rises out of the floor in front of you, the world playing on behind it: 68 cm high when you sit, taller when you stand, with the front cassette about 41 cm ahead and everything you touch within about 25° of the middle of your view. You look down at it, as at any table: what you touch and read on it (the front cassette, the radio's door and keys, the table's buttons and the words over the turntable) fits Meta's 58° × 54° design area when you do (about 42° below level seated, 49° standing), though the whole turntable and radio together are wider than that (77° seated, 68° standing).

- **Choosing.** A turntable on the left holds one cassette per song, labels out, coloured by where the song comes from (warm for the demo songs, green for your own), with a music note on the label of songs with lyrics. The cassette nearest you is the front one: it lights up, and its title, artist and lyrics show on a plate over the middle of the turntable.
- **Turning.** It turns like a heavy, well-oiled turntable. Pinch its edge and drag it round: it follows your hand closely (smoothed, so tracking jitter never jerks it), and let go mid-turn it spins on at your hand's speed. Or slide an open hand across the cassettes or the platter: it doesn't lock to your hand but is pulled along softly, so a slow slide nudges it and a quick one flicks it into a spin. A spin glides for a few seconds, ticking as each cassette passes, and settles softly with one cassette at the front; put a hand back into it, or pinch its edge, and it brakes quickly but smoothly. Taking a cassette off a spinning turntable stops it too. **Turntable** on `WorldMenu` tunes the friction, how strongly a sliding hand pulls it, how quickly a hand catches it, its top speed, the grip's smoothing and the speed below which it settles.
- **Playing.** Pinch a cassette (or close your hand round it) to pick it up and bring it to the radio's open door: a glowing outline shows where it goes, and letting go drops it in. The door shuts with a clunk, the light comes on and the song plays, with the reels turning and the speakers pumping with the bass. Let go anywhere else and it floats home; a new cassette pops the old one back to its place. The radio's keys play or pause, stop and eject, pressed with a fingertip like the menu's buttons; Play with the radio empty plays the front cassette.
- **Adding a song.** Put the blank cassette at the end of the ring in the radio to open the headset's file picker; the new song gets its own cassette.
- **Song list.** The **Song list** button on the table opens the song page (adding or changing lyrics, removing songs). Its **Back** button returns to the deck.
- **Closing.** The table's **Close** button or Meta's menu gesture closes the deck and leaves you in your world. Listen mode, the lyrics switch and the previous and next song stay in the menu's song row.
- **Models.** The cassette, radio, turntable and table are made by `Tools/blender/cassette_deck.py` (`blender -b --factory-startup -P Tools/blender/cassette_deck.py`), which exports them to `Assets/_Project/Resources/CassetteDeck`. The deck finds their parts by name and gives each material slot the deck's material of the same name (`Resources/CassetteDeck/Materials`, set up by **Update Cassette Deck Materials**), so a new export needs no code change.

## Listen mode
Poke the coral microphone bubble below the world menu to follow music playing in the room: Spotify on a phone, a speaker, a live instrument. The headset microphone replaces the song, so the bands, beats and every world follow whatever you hear. There are no lyrics or melody notes while listening, so Air strings play freely without guide beads. Poke it again ("Play song") to go back to the song. The centre bubble pauses listening.

- The first time, the headset asks for microphone permission. If it's refused, the app goes back to the song.
- Each band learns the room's own hum and counts as music once it rises well above it, so normal-volume music works and a quiet room stays calm. **Listen Noise Margin** on `MusicBus` sets how far above the hum that is: lower follows quieter music, higher ignores more background noise. The music debug view shows the live mic level.
- **Start Source** on `MusicBus` makes the app start in listen mode.
- The app's own plucks and chimes reach the microphone too, so they nudge the visuals a little.
- On the headset the mic records through `Assets/Plugins/Android/RoomMicrophone.java` with Android's `UNPROCESSED` input. Unity's own `Microphone` uses the voice input, which hears you but leaves music in the room almost silent. For a new headset, `adb shell touch /sdcard/Android/data/com.musicinyourhands.app/files/mic-probe` makes the next Listen log how loud each input hears the room (`adb logcat -s RoomMicrophone`). A `mic-source.txt` there with an Android audio source number forces one.

## Demo songs
Four original songs ship with the app, so they are safe to use anywhere, including store videos:

| Song | Style |
| --- | --- |
| Glass Room | synth pop, A minor, 96 BPM |
| Neon Tide | house, F minor, 124 BPM: four on the floor, pumping offbeat bass and stabs, a build and a drop |
| Rainy Window | lo-fi hip hop, C major, 80 BPM: jazzy electric piano, swung boom-bap beat, vinyl dust |
| Paper Boat | folk waltz in 3/4, G major, 144 BPM: nylon guitar, upright bass, brushes, glockenspiel |

In the menu, the arrow bubbles on either side of the listen bubble step to the previous and next song. `python3 Tools/render_songs.py` renders them all again (or name some, like `neon_tide`); each song is a short script in `Tools/songs`, and a new one there is picked up automatically. Add its SongAsset to **Songs** on `MusicBus` (or to `DemoSongs` in `ProjectSetup`).

Three sung songs follow them, made with [ElevenLabs Music](https://elevenlabs.io/music) from original lyrics (the output is cleared for commercial use on a paid ElevenLabs plan):

| Song | Style |
| --- | --- |
| Turn It Up to Light | synth-pop, 118 BPM, female vocal, about 2:28 |
| The World Is Humming | indie folk, 92 BPM, fingerpicked guitar, male vocal with harmonies, about 2:20 |
| Slow Motion Glow | lo-fi hip hop and neo-soul, 80 BPM, rap verses with a sung hook, about 2:16 |

Their lyrics show word by word: ElevenLabs' forced alignment times each sung word. They have no `.notes` melody, so the worlds that play along with the tune behave as they do for people's own songs. `Tools/elevenlabs_songs.py` holds their lyrics and section plans and makes them again (`ELEVENLABS_API_KEY` must be set, with the Music and Forced alignment permissions; `--only SlowMotionGlow` for one song, `--align-only` to re-time the lyrics of MP3s already made). It writes to a folder you choose; listen first, then copy the `.mp3` and `.lrc` into `Assets/_Project/Audio`.

## People's own songs
People can play their own music: they copy an audio file (`.mp3`, `.ogg` or `.wav`) into the app's `Songs` folder on the headset, with an optional `.lrc` lyrics file of the same name, and it joins the songs the arrows step through. Lines show at the times in the `.lrc`; words too when it has a time per word, otherwise they are spread across the line. Name files `Artist - Title`, or use `[ti:]` and `[ar:]` tags in the `.lrc`.

- The folder is `Android/data/com.musicinyourhands.app/files/Songs`. The app creates it, with a short how-to, the first time it runs. Copy files with Meta Quest Developer Hub's file manager or `adb push "Artist - Title.mp3" /sdcard/Android/data/com.musicinyourhands.app/files/Songs/`.
- The menu looks for new files each time it opens. An added song's audio is loaded only while it plays, and MP3 and OGG files stay compressed, so a song starts without a pause.
- They have no melody notes, so the worlds that use them behave as in Listen mode.
- The app only plays these files and shows their lyrics to the person wearing the headset; nothing is uploaded or shared.

## Adding a world
Each world is one class in `Assets/_Project/Scripts/Runtime/Worlds` that extends `World`. The base class runs what every world shares, so a world describes itself and overrides the hooks it needs:

| Hook | When it runs |
| --- | --- |
| `OnBuild()` | Once, to create the world's objects. |
| `OnEnter()` / `OnExit()` | When the world is switched to, or away. Undo in `OnExit` anything it changed outside itself. |
| `OnPlace(view)` | When the world starts, and again after you sit, stand, walk away or recenter, or the room scan changes. |
| `OnBeat(music)` | On each beat. `Beats` counts them since the world started. |
| `OnNote(note)` | When a note of the song's melody starts. |
| `OnWord(word, newLine)` | When a sung word starts. Lyric worlds also get the title's words when a song has no lyrics. |
| `OnPinch(hand, left)` / `OnRelease(hand, left)` | When a hand closes or opens a pinch. |
| `OnStep(rig, music, view, dt)` | Every frame, after the hooks above. |

A world can also read `FadeIn` (0 to 1 as it starts), `Sung` (the current word and where it is in its line), and `View`, `Rig` and `Music` (this frame's camera, hands and music). Setting `SungWordCount` gives it `SungWords`, a ready-made layer of sung words it only has to place.

While the world menu is open the hands are choosing there, and `MenuOpen` is true: no `OnPinch` starts, a pinch held as it opens gets its `OnRelease`, and one held as it closes counts only after the hand opens and pinches again. A world that reads the hands itself (`hand.Pinch`, `Fist`, a fingertip or palm on something) skips its actions while `MenuOpen`, and checks `PinchFree(left)` beside its own pinch strength. Following the hands just to look nice (lights on the fingertips, a swarm drifting to a palm) may go on.

Hands have no haptics, so every gesture that does something answers it: `Feedback(kind, left, at)` lights the fingers that made it (a `GestureKind`: pinch, release, fist, touch, done...), throws a few sparks from `at` and plays a short, quiet sound there (pass `sound: false` where the world already sounds for that moment). World does it by itself for each `OnPinch` and `OnRelease` of a world that overrides them, unless the world turns `AutoPinchFeedback` off to call it only where its pinch really acts. `FeedbackOnStart` shows it once as something ongoing begins (a fingertip in a word), and `FeedbackHold` makes the fingers glow brighter while a hold builds. Outside a world, `GestureFeedback.Show`, `Light` (fingers only) and `Hold` (a steady glow, for a lesson) do the same.

Anything that suddenly brightens much of the view (the room lighting up, a flare, a burst of light) starts with `Flash(strength)` and draws at what it returns: no world flashes more than three times a second, and with **Less flashing** on (`ComfortOptions.LessFlashing`, a bubble beside the "?" at the end of the menu's honeycomb) a flash shows at a quarter. With Less flashing the kick also reaches the hooks at a quarter, so whatever brightens on `music.Kick` calms down by itself.

To add one:
1. In the Project window, right-click `Assets/_Project/Scripts/Runtime/Worlds`, choose **Create > Music In Your Hands > World** and name it, say `PulseRings`. The new class already works: a ring of sparks on each beat and each pinch, with the sung words above it.
2. Set its `DisplayName`, `Color`, `MenuOrder` (its place in the menu; the built-in worlds use 0 to 180 in steps of 10), `MenuIcon`, `Hint`, and `IsLyric` if it is built around the lyrics.
3. Give it `Material` fields named after materials in `Assets/_Project/Materials` (`ringMaterial` gets `Ring.mat`), or name one with `[WorldMaterial("OrbDots")]`. `glowMaterial` and `letteringMaterial` come with `World`.
4. Run **Music In Your Hands > Update Worlds in Main Scene** (Configure Project for Quest does it too, with the rest of the setup). It finds every World class, adds the new one to the scene in menu order, fills its materials and saves the scene. Then **Run Editor Checks**: it confirms every world is in the scene with its materials, and that the hooks fire as described.

Nothing else keeps a list of worlds: the menu, the hints, the demo tour and the menu previews pick a new world up by themselves.

## Adding built-in songs
1. Put the audio file and its `.lrc` lyrics in `Assets/_Project/Audio`.
2. Create a song with **Create > Music In Your Hands > Song** and assign both.
3. If the lyrics are a little early or late, adjust **Lyrics Offset** on the song.
4. Add it to **Songs** on `MusicBus`.

Only ship songs you have the rights to, including the rights to show their lyrics.
