# Submission media

Images and loops for the store page, the competition's release-channel invite page and Devpost.

| File | What |
|---|---|
| `cover-2560x1440.png` | Landscape cover art (Solar system and the title), used on the Developer Dashboard. Title and tagline sit inside Meta's safe area (about x 10–90 %, y 22–78 %). `cover-1280x720.png` is a smaller copy. |
| `screenshots/*.png` | One 2560×1440 still per world (the store's screenshot size); the menu and the cassette deck at 1600×900. |
| `gifs/*.gif`, `gifs/*.mp4` | Four-second loops at 15 fps, 720 px wide, made smaller where needed to stay under 5 MB. The MP4s are the same loops at 960×540. |
| `contact-sheet.png` | Every still on one page. |
| `store-text.md` | The short and long store descriptions, as entered on the Dashboard. |

These are PC renders made with the project's screenshot tool, not headset captures. A virtual living room at dusk stands in for the player's own room, which the headset shows through passthrough. It has a wood floor and rug, a window, a sofa, a bookshelf, a lamp, plants and art, and it is lit by a lamp and the dusk light (`SceneMediaRoom.cs`). Its walls, ceiling and furniture are handed to the worlds as the room scan. The demo song drives the visuals. In the loops the hands move apart and together and rise and fall, ending where they started, so each GIF loops without a jump. Meta asks for in-headset screenshots for a store submission, so replace `screenshots/` with headset captures before submitting to the store.

## Making them again
1. Close any editor on the project, then run Unity in batch mode (it needs graphics, so no `-nographics`):
   `Unity.exe -batchmode -projectPath <project> -executeMethod MusicInYourHands.EditorTools.EditorChecks.CaptureMediaBatch -logFile Logs/media-capture.log`
   with `SHOTS` set to the shots wanted (for example `10-bubble-notes,24-solar-system`) and optionally `CLIP_SECONDS` (default 4).
   It writes `Logs/media/<shot>.png` (2560×1440) and `Logs/media/<shot>/f000.png…` (960×540 frames).
2. Turn a frame folder into a GIF with ffmpeg:
   `ffmpeg -framerate 15 -i Logs/media/<shot>/f%03d.png -filter_complex "scale=720:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle" -loop 0 <name>.gif`
