# Survival-Chaos

An orbital survival shooter. Your ship orbits a ring-shaped arena, waves of
enemies close in from all round the ring, and a boss ends the run.
It began as a 2023 university game-jam project and was rebuilt from scratch in
Unity 6.6 with HDRP. Windows only. Version 0.9.0, in polish.

## What you need

- **Unity 6000.6.3f1**, the version in `ProjectSettings/ProjectVersion.txt`.
  HDRP 17.6 comes built into it, so there is nothing else to install.
- **Git LFS.** Textures, models, audio, fonts and the other binary assets are
  stored in LFS. Cloning without it gives you pointer files in their place.

## Opening a fresh clone

1. Run `git lfs install`, then clone.
2. In Unity Hub, add this folder (the one with `Assets/` in it) and open it
   with 6000.6.3f1.
3. Bake the lighting before you judge how the game looks. Baked lighting is
   not in the repository: one bake of the Game scene is about 55 MB, and it
   changes often enough to use up the LFS quota. Everything that produces it
   is tracked, so open `Assets/Scenes/Game.unity` and choose
   **Window > Rendering > Lighting > Generate Lighting**. Until then the scene
   opens without its baked lighting.

   Before baking, check that the Lighting window's **Environment > Static
   Lighting Sky** is not None. It has come back as None once before, and a
   bake without it has no sky light, with no error to say so.

## Playing

Start from `Assets/Scenes/Menu.unity`. The game scene is `Game.unity`.

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Fly round the ring | A / D or Left / Right | Left stick or d-pad |
| Climb and dive | W / S or Up / Down | Left stick or d-pad |
| Dash | Space | Right shoulder or A |
| Turn round to fire the other way | Left Shift | Left shoulder |
| Pause | Esc | Start |

The ship fires on its own. Each level-up puts a few upgrades out on the ring:
fly into one and the others from that level-up disappear, or leave them and
they run out. Enemies you destroy sometimes leave green scrap, which repairs
the ship; the more damaged you are, the more often it drops.

In the editor, and in builds made with the debug menu, F8 opens the debug menu.

## Tests

The EditMode suite covers the logic that was separated out of the
MonoBehaviours so it could be tested. Run it from **Window > General > Test
Runner > EditMode > Run All**, or with the editor open, from a terminal:

```bash
unity command run_tests --mode EditMode --timeout 500
```

## Building

Build from **File > Build Profiles** for Windows, into a folder outside the
Unity project; `Builds/` beside this folder is where they go. **Player
Settings > Other Settings > Managed Code Variant** decides whether the debug
menu ships: Release leaves it out, and Checked puts it in.

## Further reading

All in `Docs/`:

- `rebuild-reference.html` is the project guide: what the rebuild changed and
  how each system works.
- `rebuild-lessons-codex.html` is the lessons from the rebuild.
- `leviathan-dossier.html` is the boss fight: its phases, its attacks, and why
  they are built that way.
- `open-list.html` is what is left to do.
- `playtest-bot.md` describes the editor playtest bot.
- `agent-guide.md` is for AI agents working on the project from a terminal,
  and records the traps a running editor sets.

The HTML pages are offline snapshots of pages kept up to date elsewhere, so
they can lag behind the project.
