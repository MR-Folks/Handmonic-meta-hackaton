---
name: unity-batch
description: >-
  Run this project's Unity editor tasks from the command line: update the worlds in the main scene, run the
  editor checks, render the scene shots, build the APK. Covers the exact methods, the log lines that matter,
  the editor lock (and building from a second worktree), and what the shots can and can't prove. Use before
  any batch-mode Unity run in Feel-the-Music.
---

# Unity in batch mode

Editor: Unity 6000.3.16f1 with Android support. Run one Unity process per project folder at a time.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe"   # adjust per machine
$p = Start-Process -FilePath $unity -PassThru -ArgumentList @('-batchmode','-projectPath','<project>',
     '-buildTarget','Android','-executeMethod','<method>','-logFile','<log>')
$null = $p.Handle; $p.WaitForExit(); "exit $($p.ExitCode)"
```

| Method | What it does | Success looks like |
|---|---|---|
| `MusicInYourHands.EditorTools.ProjectSetup.UpdateWorldsBatch` | Finds every `World` class, adds new ones to `Main.unity` in menu order, fills their materials, saves the scene | `[Setup] …` lines, exit 0 |
| `MusicInYourHands.EditorTools.EditorChecks.RunBatch` | All editor checks: every world named, unique `MenuOrder`, real materials, in the scene; hooks fire; menu touch; spatial audio (plugin, mixer, every world sound spatialized) | Ends with `[Check] All checks passed.` |
| `MusicInYourHands.EditorTools.EditorChecks.CaptureShotsBatch` | Renders previews to `Logs/shots/` (1600x900, staged room, hands at chest height, just past the demo song's chorus) | `[Shots] Saved screenshots to Logs/shots.` |
| `MusicInYourHands.EditorTools.QuestBuild.BuildApk` | Builds `Builds/MusicInYourHands.apk` | `[Build] Succeeded` |
| `MusicInYourHands.EditorTools.SpatialAudioSetup.RunBatch` | Spatial audio's project settings: Meta XR Audio as spatializer and ambisonic decoder, stereo, Best latency buffer, Shoebox Room acoustics (also part of project setup) | `[Setup] Spatial audio: …` |

- Set `$env:SHOTS` before starting Unity to render only some shots; every world without a custom shot gets
  `w-<Class>.png` (for example `SHOTS=w-FlowerDance`). Clear it afterwards.
- Search the log with `Select-String` for `error CS\d+`, `Shader error`, `Exception:` and `\[Check\]`.
- Runs take 20 to 120 s (a build 2 to 5 min, longer after a big import). Run longer ones in the background.
- Unity sometimes crashes on exit while saving its D3D12 shader cache. That's harmless if the log already shows
  success.
- `RunBatch` writes `Logs/preview-menu.png` (the bubble menu with every emblem).

## When the project is already open
A batch run can't open a project folder that an editor has open. Don't close the person's editor. Use a second
worktree:
```powershell
git -C <repo> worktree add --detach ..\ftm-wt\build origin/main
robocopy <repo>\Library ..\ftm-wt\build\Library /E /XD Bee PlayerDataCache TempArtifacts   # optional, faster import
```
Then point `-projectPath` at the worktree. Never commit `Main.unity` changes that a test run made in a
worktree; run `UpdateWorldsBatch` in the checkout the commit comes from.

## Reading the shots
- Look at them with an image viewer or the Read tool, and compare a second capture before calling a difference
  real: shots aren't deterministic (unseeded `Random`, editor time drives some shaders).
- They render with Direct3D in a stand-in room. They prove layout and look, not Quest behaviour: a world can
  draw fine here (even with `-force-vulkan`) and not on the headset. Build and try it on the Quest for that.
