# Project Structure Reorganization

## Project Overview
- **Game Title:** Survival Chaos
- **High-Level Concept:** A 3D arena survival shooter (HDRP) where the player orbits a central arena, fights waves of enemies and a boss, and levels up by picking skills.
- **Goal of this task:** Reorganize the `Assets/` folder hierarchy so the project is easy to navigate. This is a *structural/maintenance* task — no gameplay behavior changes.
- **Render Pipeline:** HDRP
- **Target Platform:** StandaloneWindows64
- **Unity Version:** 6000.5.7f1

## Scope (confirmed with user)
- **Full reorganization** of both `Assets/Scripts` and root-level asset folders into a conventional top-level taxonomy.
- Loose gameplay UI scripts go **under `Scripts/UI`**.
- Third-party / generated folders are tidied where safe; mandatory-location folders are kept in place (see Constraints).

## Critical Constraints (why moves are safe / what cannot move)
1. **Reference safety via `AssetDatabase.MoveAsset`.** Every move is performed with `AssetDatabase.MoveAsset` (or `AssetDatabase.RenameAsset`) inside an Editor script. This preserves each asset's GUID and its paired `.meta`, so **all scene, prefab, and material references remain intact**. Never move files/folders by raw file system operations (that orphans `.meta` files and breaks references).
2. **Assembly-definition boundaries are respected.**
   - Runtime assembly `SurvivalChaos.asmdef` lives at `Assets/Scripts/`. All runtime scripts must remain **within the `Assets/Scripts/` tree**. Reorganizing subfolders inside it is safe (folder location does not affect C# namespaces in Unity).
   - Editor assembly `SurvivalChaos.Editor.asmdef` at `Assets/Scripts/Editor/` — its scripts must stay under an Editor-asmdef folder. It keeps its location.
   - Editor assembly `SurvivalChaos.UI.Editor.asmdef` at `Assets/Scripts/UI/Editor/` — keeps its location.
   - Test assembly `SurvivalChaos.Tests.EditMode.asmdef` at `Assets/Tests/EditMode/` — keeps its location.
   - **No `.asmdef` files are moved.** This guarantees no assembly re-scoping and no compile breakage.
3. **Folders that MUST stay in place (excluded from moves), with rationale:**
   - `Assets/TextMesh Pro/` — TMP essential resources and settings; the package expects this layout and can regenerate/relink it. Moving risks breaking TMP settings lookups.
   - `Assets/Unity.VisualScripting.Generated/` — auto-generated; Visual Scripting recreates it at the root. Moving is pointless and churny.
   - `Assets/Scenes/` lightmap/baking sub-assets stay with their scenes.
4. **Namespaces unaffected.** Scripts use `namespace SurvivalChaos` (or global namespace); Unity does not tie namespace to folder, so no code edits are required for moves. `.asmdef rootNamespace` only affects newly created files.

## Current Problems (inventory)
- **Stray files at `Assets/` root:** `EnemyHit.prefab`, `Explosion.prefab`, `PlayerHit.prefab`, `shoot.prefab`, `shoot 1.prefab`, `Transparent.mat`.
- **Mislabeled `Assets/Sprites/`:** actually holds 3D `.fbx` spaceship models, materials, animator controllers, and `PlayerSpaceShip.prefab`.
- **Scattered art:** `Boss/` (fbx + prefabs), `Enemy/` (prefabs), `Scenario/` (fbx, materials, textures, shader graph), `Smoke 1/` (single texture+material), `Materials/`, `HUD/` (textures, sprites, render textures, `TitleScreen.mp4`), `Starfield Skybox/`.
- **Audio split:** `Sound/` (music `.asset` SOs + `Music/` raw clips).
- **`Assets/Scripts/` root has 11 loose scripts** plus a stray `New Lighting Settings.lighting`.
- **Redundant script folders:** `Enemies/` (data) vs `EnemyScripts/` (behaviours); typo folder `PlayerScrpts/`.
- **`_Recovery/`** backup scene at root.

## Game Mechanics / Controls / UI
- Not applicable — this task changes only folder organization, not runtime behavior, input, or UI layout.

## Target Structure

### `Assets/` root (target)
```
Assets/
  Art/
    Models/        <- all .fbx (ships from Sprites/, Boss/rosca.fbx, Scenario/*.fbx)
    Materials/     <- Transparent.mat, Materials/*, Sprites materials, Scenario materials, Smoke material
    Textures/      <- HUD textures/sprites, Smoke texture, Scenario textures
    Animations/    <- animator controllers from Sprites/
    Skybox/        <- Starfield Skybox contents
    VFX/           <- VFX materials/assets not tied to UI
  Audio/
    Music/         <- .mp3/.ogg raw clips (from Sound/Music)
    Definitions/   <- GameplayMusic.asset, MenuMusic.asset (music/sound SOs from Sound/)
  Data/            <- (renamed from Content/) ScriptableObject instances: Enemies, Skills, Waves
  Prefabs/
    Player/        <- PlayerSpaceShip.prefab
    Enemies/       <- Enemy 1-3 prefabs
    Boss/          <- boss shot prefabs (from Boss/)
    Projectiles/   <- shoot.prefab, shoot 1.prefab, enemy_shoot prefabs
    VFX/           <- EnemyHit.prefab, Explosion.prefab, PlayerHit.prefab
  Scenes/          <- Game.unity, Menu.unity + lightmaps (unchanged)
    Lighting/      <- New Lighting Settings.lighting (moved out of Scripts)
  Scripts/         <- reorganized (see below)
  Settings/        <- HDRPDefaultResources contents (HDRP pipeline asset, etc.)
  UI/              <- existing UI shaders/materials; + HUD render textures; TitleScreen.mp4 -> UI/Video/
  Documentation/   <- unchanged
  Plans/           <- unchanged (this file)
  Tests/           <- unchanged
  _Archive/        <- (renamed from _Recovery/) backup scenes, clearly marked
  TextMesh Pro/            <- UNCHANGED (mandatory location)
  Unity.VisualScripting.Generated/  <- UNCHANGED (auto-generated)
```

### `Assets/Scripts/` (target)
```
Scripts/
  SurvivalChaos.asmdef        <- stays at Scripts root (do not move)
  Core/
    EXP.cs, Timer.cs, ApplyBounds.cs, RunOutcome.cs
  Gameplay/
    Player/       <- (from PlayerScrpts) Player, PlayerMovement, ShipMotion, ShootScript, SpaceShipPitch
    Enemies/      <- merged Enemies + EnemyScripts: EnemyDefinition, HealthState, ColliderScript,
                     ColliderScript_3, DestroyAfterTime, Enemy, EnemyMovement, EnemySpaceShip,
                     Enemy_1, ObstacleScript
    Boss/         <- BossAttack, BossEmitter, VolleyTimer
    Arena/        <- ArenaGeometry, BulletLightPool, SnapToOrbit
    Waves/        <- SpawnMath, SpawnStream, WaveDefinition, WaveDirector
    Skills/       <- AttackSpeedSkill, HealSkill, ISkillTarget, MaxHealthSkill, ShotUpgradeSkill,
                     SkillDefinition, SkillPool
  Systems/
    Audio/        <- AudioDirector, AudioLevels, MusicSource, SoundDefinition
    Pooling/      <- ObjectPool, PooledInstance
    Input/        <- GameInput, IGameInput, InputSystemGameInput, LegacyGameInput
    Diagnostics/  <- FrameTimeStats, PerformanceOverlay, SystemProfile
  UI/
    (existing) BarMotion, HoloBar, HoloButtonHighlight, HoloMenuEntry, HoloRectData,
               MenuScreen, VolumeControl
    (moved in) DeathMenu, PauseMenu, MainMenu, HealthBar, ExpBar, BossHpBar,
               SkillSelect, Tutorial, VictoryMenu
    Editor/     <- SurvivalChaos.UI.Editor.asmdef + HoloMainMenuBuilder, HoloMenuBuilder,
                   HoloUiBuilder, HoloUiFactory, MenuVideoFixer  (UNCHANGED location)
  Editor/       <- SurvivalChaos.Editor.asmdef + DefaultEnemyAssets, DefaultSkillAssets,
                   HdrpVfxMaterialFixup, LavaLightPlacer, StarfieldCubemapBaker (UNCHANGED location)
```
Note: `Run/` is dissolved — `RunOutcome.cs` -> `Core/`, `VictoryMenu.cs` -> `UI/`.

## Key Asset & Context
- **Assembly defs (do not move):** `Assets/Scripts/SurvivalChaos.asmdef`, `Assets/Scripts/Editor/SurvivalChaos.Editor.asmdef`, `Assets/Scripts/UI/Editor/SurvivalChaos.UI.Editor.asmdef`, `Assets/Tests/EditMode/SurvivalChaos.Tests.EditMode.asmdef`.
- **Move mechanism:** A one-shot Editor utility (e.g. `Assets/Scripts/Editor/ProjectReorganizer.cs`, run from a menu item) that:
  1. Creates target folders with `AssetDatabase.CreateFolder`.
  2. Moves each asset with `AssetDatabase.MoveAsset(src, dst)`, checking the returned error string for each call.
  3. Renames folders with `AssetDatabase.RenameAsset` where applicable (`Content`->`Data`, `_Recovery`->`_Archive`).
  4. Calls `AssetDatabase.DeleteAsset` on emptied stray folders (`Smoke 1`, old `Sprites`, `Sound`, `Boss`, `Enemy`, `Enemies`, `EnemyScripts`, `PlayerScrpts`, `Run`, `Materials`, `Scenario`, `Starfield Skybox`, `HDRPDefaultResources`, `Content`) once confirmed empty.
  5. Ends with `AssetDatabase.Refresh()`.
  - The reorganizer script itself is deleted after a successful run (it is a scaffold, not shipped code).
- **Reference-carrying assets to watch:** `Game.unity`/`Menu.unity` reference `PlayerSpaceShip.prefab`, enemy/boss prefabs, projectile prefabs, VFX prefabs, materials, audio SOs, HDRP pipeline asset (via Graphics settings). All are GUID-linked, so `MoveAsset` keeps them valid — but they are the priority items to verify after the run.

## Implementation Steps

1. **Create the reorganizer Editor utility.**
   - Description: Add `Assets/Scripts/Editor/ProjectReorganizer.cs` implementing the move mechanism above, driven by a `[MenuItem]`. All moves via `AssetDatabase.MoveAsset`; log every non-empty error string; abort-and-report on first failure without leaving a half-done state where avoidable.
   - Assigned role: developer
   - Dependencies: None
   - Parallelizable: No (foundation for all following steps)

2. **Reorganize `Assets/Scripts` (runtime + UI).**
   - Description: Create `Core`, `Gameplay/{Player,Enemies,Boss,Arena,Waves,Skills}`, `Systems/{Audio,Pooling,Input,Diagnostics}`, and move scripts per the target tree. Merge `Enemies`+`EnemyScripts` -> `Gameplay/Enemies`; rename intent of `PlayerScrpts` -> `Gameplay/Player`; move 11 loose root scripts + `VictoryMenu` into `UI`/`Core`; move `New Lighting Settings.lighting` -> `Scenes/Lighting`. Do NOT move any `.asmdef` or the `Editor`/`UI/Editor` folders.
   - Assigned role: developer
   - Dependencies: Depends on Step 1
   - Parallelizable: No (shares the asset database with other move steps; run sequentially to keep logs clean)

3. **Reorganize art assets.**
   - Description: Build `Art/{Models,Materials,Textures,Animations,Skybox,VFX}` and move `.fbx`, materials, textures, animator controllers from `Sprites/`, `Boss/`, `Enemy/`, `Scenario/`, `Smoke 1/`, `Materials/`, `Starfield Skybox/`, `HUD/` (art portions).
   - Assigned role: developer
   - Dependencies: Depends on Step 1
   - Parallelizable: No

4. **Reorganize prefabs, audio, data, settings, UI, misc.**
   - Description: Build `Prefabs/{Player,Enemies,Boss,Projectiles,VFX}` and move all prefabs (including stray root prefabs) in. Build `Audio/{Music,Definitions}` from `Sound/`. Rename `Content` -> `Data`. Move `HDRPDefaultResources` contents -> `Settings/`. Move UI render textures + `TitleScreen.mp4` under `UI/`. Rename `_Recovery` -> `_Archive`. Delete emptied stray folders.
   - Assigned role: developer
   - Dependencies: Depends on Step 1
   - Parallelizable: No

5. **Cleanup.**
   - Description: Remove now-empty source folders and their `.meta`; delete the `ProjectReorganizer.cs` scaffold; `AssetDatabase.Refresh()`.
   - Assigned role: developer
   - Dependencies: Depends on Steps 2-4
   - Parallelizable: No

## Verification & Testing
1. **No compile errors:** Console is clean after the reorg and domain reload (script moves preserve GUIDs, so this should hold).
2. **Assemblies intact:** All four `.asmdef` still compile; `SurvivalChaos`, `SurvivalChaos.Editor`, `SurvivalChaos.UI.Editor`, `SurvivalChaos.Tests.EditMode` present with unchanged references.
3. **EditMode tests still pass:** Run the existing tests under `Assets/Tests/EditMode` — they exercise SpawnMath, ShipMotion, SkillPool, HealthState, etc., and validate that moved runtime scripts are still resolvable.
4. **Scene reference spot-check:** Open `Game.unity` and `Menu.unity`; confirm no missing (None) references on the Player, enemies, boss, HUD bars, and audio director — i.e. prefab/material/SO links survived.
5. **No missing-reference / missing-script warnings** in either scene.
6. **Graphics settings:** HDRP render pipeline asset still assigned after moving `HDRPDefaultResources` -> `Settings/`.
7. **Empty-folder check:** No leftover empty source folders or orphaned `.meta` files.
8. **Play mode smoke test:** Enter Play in `Game.unity` — player shoots, enemies spawn/move, audio plays, HUD updates.
