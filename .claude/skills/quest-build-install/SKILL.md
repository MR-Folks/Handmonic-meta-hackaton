---
name: quest-build-install
description: >-
  Build Music In Your Hands (com.musicinyourhands.app) as a Quest APK and put it on a Quest 3: Unity batch
  build, the adb server, checking whether the app is in use before installing, signing keys that differ per
  machine, and launching without leaving the app muted. Use whenever the request is to build, install,
  reinstall, update or launch the app on a headset, or to prepare the competition upload.
---

# Build and install on the Quest

## Build
- Build with `MusicInYourHands.EditorTools.QuestBuild.BuildApk` in batch mode (see the `unity-batch` skill for
  the command). It writes `Builds/MusicInYourHands.apk` (about 62 MB; `*.apk` is gitignored) and takes 2 to 5
  minutes.
- The log line `[Build] Succeeded: … (1207 MB …)` reports Unity's total build size, not the APK. Read the
  APK's real size from the file.
- A batch build can't open a project that a Unity editor has open. Don't close the person's editor. Build
  from a second git worktree at the same commit instead (see `unity-batch`).
- The editor's screenshots render with Direct3D; Quest shaders (Vulkan) only compile in a player build. After
  shader changes, build once and search the log for `Shader error` and `Shader warning in 'MusicInYourHands/`.

## Reach the headset
- Use the adb that ships with the Unity editor:
  `<Unity editor>/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`. Run `adb start-server` first,
  and leave the server running; `metavr` reaches the Quest only through it on some PCs (direct USB gives
  "Access denied").
- Unity batch runs stop the adb server when they exit. Start it again before installing.
- `adb devices` should list the Quest 3 as `device`. If it isn't listed, the headset was unplugged or went to
  sleep off the cable. Don't run `logcat -d` against a missing device: it waits for one forever.

## Install
1. **Check first, in its own command:** `adb shell pidof com.musicinyourhands.app` and
   `adb shell dumpsys power | grep mWakefulness`. `install -r` force-closes the running app. If it is running
   and the headset is `Awake`, someone is in it: say so and ask before installing. (Installs have closed the
   app on the wearer more than once.)
2. `adb install -r Builds/MusicInYourHands.apk`.
3. Confirm with `adb shell dumpsys package com.musicinyourhands.app | grep lastUpdateTime`.

**Signing differs per machine.** Debug builds are signed with that machine's `~/.android/debug.keystore`.
Installing over a copy built on another computer fails with `INSTALL_FAILED_UPDATE_INCOMPATIBLE`. Uninstalling
wipes the app's files, including people's own songs in `Android/data/com.musicinyourhands.app/files/Songs`, so
first `adb pull` that folder (and anything else in `files/`), then uninstall, install, and push the songs back.
Say that a fresh install asks for permissions again.

## Launch
- Launch from adb only while someone is wearing the headset:
  `adb shell am start -n com.musicinyourhands.app/com.unity3d.player.UnityPlayerGameActivity`.
- An app started while the headset is idle stays silent after it is put on: Horizon OS sets its `PLAY_AUDIO`
  app op to `ignore`. Check with `adb shell cmd appops get com.musicinyourhands.app PLAY_AUDIO`. Don't change
  the app op; force-stop the app and let the person open it from the Library (Unknown sources).
- If an app looks silent, check `dumpsys audio` (mutedState) and the app op before touching code.

## Don't
- Don't start screen recordings, background logcat captures or performance captures on the headset unless the
  person asks; they record the headset themselves. A one-off read of the crash buffer
  (`adb logcat -b crash -d -t 40`) is fine.

## Competition upload
- Meta's upload check refuses the debug key. The release keystore, the upload to a release channel named
  "Competition" on the Developer Dashboard, and the store listing are the team's own steps (their accounts and
  credentials). `QuestBuild` has no release-signing settings yet.
- For Meta store and platform questions, verify against Meta's docs first (`hz-quest-verify-first`,
  `hz-store-submit`).
