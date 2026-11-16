# Agent skills for this project

Skills in this folder load automatically for coding agents that read `.claude/skills`, on any machine that
clones the repo.

## Written for this project
| Skill | Use it for |
|---|---|
| `quest-build-install` | Building the APK and installing or launching it on a Quest 3 without closing it on the wearer, muting it, or losing people's songs |
| `unity-batch` | Running Update Worlds, the editor checks, the scene shots and the build from the command line |
| `world-changes` | Adding, changing, renaming or removing a world, Quest-only rendering traps, parallel world work |

## Meta's Quest skills (copied)
These are unchanged copies of Meta's agent skills for Meta Quest and Horizon OS development, licensed
Apache-2.0 (see each `SKILL.md`), as installed on 2026-11-15. They are the ones that fit this project: Unity 6
with the Meta XR SDK, MR Utility Kit, hand tracking and passthrough on Quest 3.

| Skill | Use it for |
|---|---|
| `hz-quest-verify-first` | Checking Quest-specific facts against Meta's docs before relying on them |
| `hz-unity-meta-core-sdk`, `hz-unity-meta-interaction-sdk`, `hz-unity-meta-mixed-reality-utility-kit` | The Meta XR Core SDK, Interaction SDK and MRUK in Unity |
| `hz-unity-meta-quest-ui`, `hz-unity-placement`, `hz-unity-device-readiness` | World-space UI, placing objects by their bounds, narrower fields of view |
| `hz-unity-project-analyzer`, `hz-unity-code-review`, `hz-unity-sdk-upgrade` | Understanding, reviewing and upgrading the Unity project |
| `hz-immersive-designer` | Comfort, interaction and accessibility guidelines |
| `hz-vr-debug`, `hz-perfetto-debug`, `hz-simpleperf-debug` | Logs, frame-time traces and CPU profiling on the headset |
| `hz-store-submit` | Preparing a build for the Meta Horizon Store or a release channel |
| `hz-xr-simulator-control`, `hz-xr-simulator-install-and-configure` | The Meta XR Simulator |

Most of Meta's skills call the `metavr` CLI (npm package `metavr`, or its standalone binary) and its MCP server
(`metavr mcp install claude-code`). Install those on a new machine. `metavr init` installs and refreshes the
`metavr-cli` skill, which is generated per machine and so isn't copied here. Meta's skills are also published as
the `meta-vr` plugin; to refresh the copies here, copy the newer versions over these folders.
