---
name: world-changes
description: >-
  Add, change, rename or remove a world in Music In Your Hands (the visual scenes in the bubble menu), safely:
  where the code lives, what to run after, Quest-only rendering traps, removing a world from the scene,
  renaming without breaking the scene, menu emblems, and how to split world work across parallel helpers.
  Use for any work on files in Assets/_Project/Scripts/Runtime/Worlds or their shaders.
---

# Working on worlds

Start with the README's "Adding a world" section: it is the up-to-date description of the `World` hooks
(`OnBuild`, `OnEnter`/`OnExit`, `OnPlace`, `OnBeat`, `OnNote`, `OnWord`, `OnPinch`/`OnRelease`, `OnStep`),
`SungWords`, `MenuOpen` and `PinchFree`, the template (**Create > Music In Your Hands > World**) and material
fields filled by name. One world is on at a time: a combining mode was tried and rejected in November 2026, so
don't bring it back unless asked.

## After any world change
1. `UpdateWorldsBatch`, then `RunBatch` (see `unity-batch`).
2. Render the world's shot (`SHOTS=w-<Class>`) and look at it.
3. If you touched a shader, build the APK once (Quest compiles shaders only in a player build), and try it on
   the headset when you can.
4. Commit the world's files, its `.meta` files and the `Main.unity` that `UpdateWorldsBatch` saved.

## Quest-only traps
- **Per-object shader arrays go outside `UnityPerMaterial`.** Flower dance's per-flower arrays sat inside the
  per-material buffer and were set through a `MaterialPropertyBlock`. They drew in the editor and not on Quest:
  every flower read size 0 and the shader hid it. Declare such arrays as plain globals outside the CBUFFER.
- **Set `mesh.bounds`, not `Renderer.bounds`,** when the vertex shader moves geometry into place; that is what
  the worlds that draw on Quest do.
- Keep per-frame code free of allocations (no LINQ, no `new` in `OnStep`); Quest 3 needs 72 fps with passthrough.

## Removing a world
`UpdateWorldsBatch` rebuilds the world list from the classes in code but never deletes scene objects, so a
deleted class leaves a missing-script object in `Main.unity`. Remove the object first (a throwaway editor
script that destroys the world's GameObject and saves the scene), then `git rm` the class, its shaders,
materials and their `.meta` files, reset its emblem slot, and run `UpdateWorldsBatch` and `RunBatch`.

## Renaming a world
`git mv` every file together with its `.meta`, so the GUIDs stay and `Main.unity` keeps its references. Keep the
material field names and rename the `[WorldMaterial]` names. The scene object keeps the old class name in
`m_Name` (and `m_EditorClassIdentifier`) until you edit those two lines.

## Menu emblems
`Assets/_Project/Shaders/Bubble.shader` draws each world's small animated emblem in `Emblem`, in slots marked
`// BEGIN emblem N` and `// END emblem N`. A world picks its slot with `MenuIcon`. Edit only between a slot's
markers; a free slot draws a plain ring (`e = Line(abs(length(p) - 0.45), 0.04);`).

## Several worlds in parallel
This has worked well for building or changing several worlds at once:
- Give each helper its own git worktree (`ftm-wt/<name>`, branch `wip/<name>`, with a copied `Library`
  minus `Bee`, `PlayerDataCache` and `TempArtifacts`), a world name, a `MenuOrder` and an emblem slot.
- Each helper edits only its world's files and its emblem lines, never `Main.unity` or shared code, doesn't
  push, and reports the commit, files, screenshots and check result.
- The coordinator brings each one into the main checkout in turn: `git cherry-pick -n` the helper's commits,
  `UpdateWorldsBatch`, `RunBatch`, render its shot, commit (one commit per world) and push.
- Give shared-code refactors to one helper only, and have someone else review the diff before it lands.
