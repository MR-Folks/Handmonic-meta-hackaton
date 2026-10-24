# Music In Your Hands

A hand-tracked mixed reality music visualizer for Meta Quest 3. You play with the music using your bare hands, inside your own room.

## Requirements
- Unity 6000.3.16f1 with Android Build Support
- Meta Quest 3 in developer mode
- Packages (already in `Packages/manifest.json`): Meta XR Core SDK, Interaction SDK, MR Utility Kit, URP, OpenXR, XR Hands

## Project layout
```
Assets/_Project/
  Audio/      demo song "Glass Room" (WAV) and its word-timed lyrics (LRC)
  Songs/      SongAsset files: audio + lyrics + offset
  Scenes/     Main.unity
  Scripts/
    Runtime/Music/   MusicBus (bands, beats, song time), SongAsset
    Runtime/Lyrics/  LyricsTrack (LRC parser and current line/word lookup)
    Runtime/Hands/   HandRig (palm, fingertips, pinch, fist, palm-up for every world)
    Runtime/Debug/   MusicDebugView and HandDebugView (in-headset checks)
    Meta/            OVRHandFeed (Meta hand tracking into HandRig)
    Editor/          Quest setup, scene builder, APK build, LRC importer
Docs/
  concept-book.html      the interactive concept page with all worlds and lyric styles
Tools/
  render_glass_room.py   renders the demo song and its LRC
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

## Listen mode
Poke the coral microphone bubble below the world menu to follow music playing in the room: Spotify on a phone, a speaker, a live instrument. The headset microphone replaces the song, so the bands, beats and every world follow whatever you hear. There are no lyrics or melody notes while listening: Note rain falls from the bands instead, and Air strings play freely without guide beads. Poke it again ("Play song") to go back to the song. The centre bubble pauses listening.

- The first time, the headset asks for microphone permission. If it's refused, the app goes back to the song.
- Each band learns the room's own hum and counts as music once it rises well above it, so normal-volume music works and a quiet room stays calm. **Listen Noise Margin** on `MusicBus` sets how far above the hum that is: lower follows quieter music, higher ignores more background noise. The music debug view shows the live mic level.
- **Start Source** on `MusicBus` makes the app start in listen mode.
- The app's own plucks and chimes reach the microphone too, so they nudge the visuals a little.
- On the headset the mic records through `Assets/Plugins/Android/RoomMicrophone.java` with Android's `UNPROCESSED` input. Unity's own `Microphone` uses the voice input, which hears you but leaves music in the room almost silent. For a new headset, `adb shell touch /sdcard/Android/data/com.musicinyourhands.app/files/mic-probe` makes the next Listen log how loud each input hears the room (`adb logcat -s RoomMicrophone`). A `mic-source.txt` there with an Android audio source number forces one.

## Adding songs
1. Put the audio file and its `.lrc` lyrics in `Assets/_Project/Audio`.
2. Create a song with **Create > Music In Your Hands > Song** and assign both.
3. If the lyrics are a little early or late, adjust **Lyrics Offset** on the song.

Only ship songs you have the rights to, including the rights to show their lyrics.
