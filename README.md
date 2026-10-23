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
    Runtime/Debug/   MusicDebugView (in-headset check of bands, beats and lyric sync)
    Editor/          LRC importer, one-click project setup
Tools/
  render_glass_room.py   renders the demo song and its LRC
```

## Setup
1. Open the project in Unity 6000.3.16f1.
2. Run **Music In Your Hands > Run Project Setup** (safe to run again).
3. Run **Meta > Tools > Project Setup Tool** and apply the fixes for Android.
4. Open `Assets/_Project/Scenes/Main.unity` and add the Building Blocks listed in the next section.

## Adding songs
1. Put the audio file and its `.lrc` lyrics in `Assets/_Project/Audio`.
2. Create a song with **Create > Music In Your Hands > Song** and assign both.
3. If the lyrics are a little early or late, adjust **Lyrics Offset** on the song.

Only ship songs you have the rights to, including the rights to show their lyrics.
