# Music In Your Hands

A hand-tracked mixed reality music visualizer for Meta Quest 3. You play with the music using your bare hands, inside your own room.

## Requirements
- Unity 6000.3.16f1 with Android Build Support
- Meta Quest 3 in developer mode
- Packages (already in `Packages/manifest.json`): Meta XR Core SDK, Interaction SDK, MR Utility Kit, URP, OpenXR, XR Hands

## Project layout
```
Assets/_Project/
  Audio/      four demo songs (WAV), their word-timed lyrics (LRC) and melody notes (.notes)
  Songs/      SongAsset files: audio + lyrics + offset
  Resources/  AudiusSongs.txt (the songs streamed from Audius) and the baked lettering
  Scenes/     Main.unity
  Scripts/
    Runtime/Music/   MusicBus (bands, beats, song time), SongAsset, AddedSongs, AudiusSongs
    Runtime/Lyrics/  LyricsTrack (LRC parser and current line/word lookup)
    Runtime/Hands/   HandRig (palm, fingertips, pinch, fist, palm-up for every world)
    Runtime/Worlds/  World (the base every world builds on), the worlds, the menu and its song page
    Runtime/Debug/   MusicDebugView and HandDebugView (in-headset checks)
    Meta/            OVRHandFeed (Meta hand tracking into HandRig)
    Editor/          Quest setup, scene builder, APK build, LRC importer
Docs/
  concept-book.html      the interactive concept page with all worlds and lyric styles
Tools/
  render_songs.py        renders the demo songs, their LRC lyrics and .notes melodies
  songkit.py             the instruments, voice and file writers the songs share
  songs/                 one script per demo song
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
- Meta's menu gesture (left palm up, pinch) opens the world menu 44 cm in front of you, within reach (Meta suggests 42 to 46 cm for UI you touch). Touch a bubble or a button on the song page with your index fingertip, or point at it from farther away and pinch. Within 15 cm of the menu the pointer ray gives way to a dot under your fingertip that shrinks as you close in; a press lands when the fingertip reaches the menu, and the next one needs the finger 2 cm back out. Past 20 cm the ray returns.
- Worlds that use different parts of the room and your body can be on together, up to three: choosing a world adds it, or swaps it in for the worlds it clashes with, and choosing one that is on turns it off. A ring marks each world that is on, and the caption says what a choice will do. The menu stays open until you make the menu gesture again (see "Spaces and combining" below).

## Listen mode
Poke the coral microphone bubble below the world menu to follow music playing in the room: Spotify on a phone, a speaker, a live instrument. The headset microphone replaces the song, so the bands, beats and every world follow whatever you hear. There are no lyrics or melody notes while listening: Note rain falls from the bands instead, and Air strings play freely without guide beads. Poke it again ("Play song") to go back to the song. The centre bubble pauses listening.

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

## People's own songs
People can play their own music: they copy an audio file (`.mp3`, `.ogg` or `.wav`) into the app's `Songs` folder on the headset, with an optional `.lrc` lyrics file of the same name, and it joins the songs the arrows step through. Lines show at the times in the `.lrc`; words too when it has a time per word, otherwise they are spread across the line. Name files `Artist - Title`, or use `[ti:]` and `[ar:]` tags in the `.lrc`.

- The folder is `Android/data/com.musicinyourhands.app/files/Songs`. The app creates it, with a short how-to, the first time it runs. Copy files with Meta Quest Developer Hub's file manager or `adb push "Artist - Title.mp3" /sdcard/Android/data/com.musicinyourhands.app/files/Songs/`.
- The menu looks for new files each time it opens. An added song's audio is loaded only while it plays, and MP3 and OGG files stay compressed, so a song starts without a pause.
- They have no melody notes, so the worlds that use them behave as in Listen mode.
- The app only plays these files and shows their lyrics to the person wearing the headset; nothing is uploaded or shared.

## Songs from Audius
The app also streams a short, hand-picked list of songs from [Audius](https://audius.co), a free music service for independent artists. They come after your own songs in the arrow bubbles, and the song page has an **Audius** tab that lists them, each with its credit.

- The list is `Assets/_Project/Resources/AudiusSongs.txt`: one track per line, its id and then its page. The first time the menu opens, the app asks Audius for those tracks in one request. A song's audio, a whole MP3 of 5 to 15 MB, streams in when it is chosen, stays compressed while it plays (decoding a whole song at once froze the headset for 0.7 s), and is freed when another song plays. The headset's log notes each one (`[Audius] Streamed <title>: <size> in <time>`). No account or API key is needed, and each play counts towards the track's plays on Audius.
- **Picking songs.** A track's id is `data.id` at `https://api.audius.co/v1/resolve?url=<the song's page>&app_name=MusicInYourHands`. Only pick tracks published by their own artist or label, with no remixes, mashups, covers, DJ mixes, samples of other people's music or explicit lyrics: anyone can upload to Audius, and its license only holds if the publisher owns the song. Listen to each one all the way through before a release. The first ten picks (1 Nov 2026) are electronic, house, lo-fi, ambient, jazz, drum and bass and trance tracks from verified artists and labels, chosen by these rules from Audius's track details alone; nobody has listened to them yet.
- **Credits.** Audius's [Open Music License](https://openaudiofoundation.org/open-music-license.pdf) lets music players stream its tracks. For commercial use (§1.5) it asks for the publisher's name, a copyright notice, a mention of the license and a link to the track. Each song on the Audius tab shows "© publisher", its **Credits** page shows all four, and while a song from Audius plays the menu shows a credit line under the song row. If an artist asks to be left out, take their song off the list.
- Without internet, the Audius tab says it couldn't reach Audius and offers **Try again**, and a song that can't stream says so in the menu. Everything else works offline. If an artist removes or locks a track, it drops off the list; **Run Editor Checks** warns when a picked song can't play any more, and fails if a streamed song takes 100 ms or more to get ready.
- The app sends Audius nothing about the person, but Audius and the servers its music comes from see each request, including the headset's IP address, so the app's privacy policy should mention it. The `INTERNET` permission this needs is in `Assets/Plugins/Android/AndroidManifest.xml`.

## Adding a world
Each world is one class in `Assets/_Project/Scripts/Runtime/Worlds` that extends `World`. The base class runs what every world shares, so a world describes itself and overrides the hooks it needs:

| Hook | When it runs |
| --- | --- |
| `OnBuild()` | Once, to create the world's objects. |
| `OnEnter()` / `OnExit()` | When the world is turned on or off. Undo in `OnExit` anything it changed outside itself. |
| `OnPlace(view)` | When the world starts, and again after you sit, stand, walk away or recenter, or the room scan changes. |
| `OnBeat(music)` | On each beat. `Beats` counts them since the world started. |
| `OnNote(note)` | When a note of the song's melody starts. |
| `OnWord(word, newLine)` | When a sung word starts, for the one world on that shows the words. Lyric worlds also get the title's words when a song has no lyrics. |
| `OnPinch(hand, left)` / `OnRelease(hand, left)` | When a hand closes or opens a pinch. |
| `OnStep(rig, music, view, dt)` | Every frame, after the hooks above. |

A world can also read `FadeIn` (0 to 1 as it starts), `Sung` (the current word and where it is in its line), and `View`, `Rig` and `Music` (this frame's camera, hands and music). Setting `SungWordCount` gives it `SungWords`, a ready-made layer of sung words it only has to place.

### Spaces and combining
Several worlds can be on at once, as long as they don't get in each other's way: flowers on the floor go well with planets circling you, but not with words rising out of the same floor. So each world says which parts of the room and of your body it uses, with `Spaces`:

| Space | What it covers |
| --- | --- |
| `Ground` | The floor and the tops of furniture: things that stand, grow, land or lie there. |
| `Walls` | The walls. |
| `Ceiling` | The ceiling, or a sky drawn over it. |
| `AroundYou` | The room's air all around you, out of reach, and things that circle you. |
| `InFront` | Within arm's reach in front of you: things that stand there for you to touch. |
| `Hands` | On and between your hands: what they carry or hold, and how they look beyond their colours. |
| `Pinch` | What a pinch does (drawing, spraying, pouring, holding). |
| `Fists` | What clenching your fists does (clearing, switching, pulling). |

```csharp
public override Space Spaces => Space.Ground | Space.Hands;
```

Claim a space when the world's things are there or its gesture means something there; a faint spill of light (a glow on the floor under something) doesn't count. Reacting to where the hands are (leaning flowers, splitting a school of fish) doesn't need `Hands`; carrying, holding or wearing something does. A world that doesn't set `Spaces` is on its own: it combines with nothing, so choosing it turns every other world off.

Two worlds can be on together when they share no space and are not both lyric worlds. In the menu, choosing a world adds it beside the ones that are on, or swaps it in for those it shares a space with; choosing one that is on turns it off, though one always stays on. At most three are on at once (**Max Worlds** on `WorldManager`), and past that the one chosen longest ago gives way. In the menu a ring marks each world that is on, and the caption says what a choice will do ("Add Flower dance", "Swap Cube field for Flower dance", "Turn off Solar system"). The menu stays open after each choice; the menu gesture closes it.

`World` shares what several worlds would fight over, so a world needs nothing extra to combine:
- **Hooks.** Every hook reaches every world that is on, except `OnWord`.
- **Sung words.** Only one world shows them and gets `OnWord`: the lyric world if one is on, else the one chosen last that shows words (`SungWordCount`). When it turns off, the next one picks up at the word being sung.
- **Hands.** Only one world dresses the hands: the one that uses `Hands`, else the one chosen last. It sets their `Theme` and may set `HandSkin`'s focus point and charge and nudge the visible hands (`Effects.OffsetHand`); whatever the other worlds' hooks do to these is undone right after them.
- **Menu caption.** With nothing under a finger it names the worlds that are on, in the colour of the one chosen last.

Undo in `OnExit` anything else the world changes outside itself (Heartbeat orb puts the volume back, Sound pool takes its filter off the song), and keep per-frame work light: three worlds share the frame. **Run Editor Checks** runs every pair of worlds that can be on together for a moment, and lists the worlds that are still on their own.

### Steps
To add one:
1. In the Project window, right-click `Assets/_Project/Scripts/Runtime/Worlds`, choose **Create > Music In Your Hands > World** and name it, say `PulseRings`. The new class already works: a ring of sparks on each beat and each pinch, with the sung words above it.
2. Set its `DisplayName`, `Color`, `MenuOrder` (its place in the menu; the built-in worlds use 0 to 180 in steps of 10), `MenuIcon`, `Hint`, `Spaces`, and `IsLyric` if it is built around the lyrics.
3. Give it `Material` fields named after materials in `Assets/_Project/Materials` (`ringMaterial` gets `Ring.mat`), or name one with `[WorldMaterial("OrbDots")]`. `glowMaterial` and `letteringMaterial` come with `World`.
4. Run **Music In Your Hands > Update Worlds in Main Scene** (Configure Project for Quest does it too, with the rest of the setup). It finds every World class, adds the new one to the scene in menu order, fills its materials and saves the scene. Then **Run Editor Checks**: it confirms every world is in the scene with its materials, that the hooks fire as described, and that the world runs beside every world it can be on with.

Nothing else keeps a list of worlds: the menu, the hints, the demo tour and the menu previews pick a new world up by themselves.

## Adding built-in songs
1. Put the audio file and its `.lrc` lyrics in `Assets/_Project/Audio`.
2. Create a song with **Create > Music In Your Hands > Song** and assign both.
3. If the lyrics are a little early or late, adjust **Lyrics Offset** on the song.
4. Add it to **Songs** on `MusicBus`.

Only ship songs you have the rights to, including the rights to show their lyrics.
