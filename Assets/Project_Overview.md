# Survival Chaos — Technical Project Overview

## 1. Project Description
Survival Chaos is a 3D orbital bullet-hell survivor arena game built with Unity 6 and the High Definition Render Pipeline (HDRP). Players pilot a spacecraft constrained along a continuous circular orbit around a central axis, fending off escalating waves of automated enemy craft and boss encounters while collecting experience points (EXP) to unlock modular skill upgrades.

### Core Pillars
- **Orbital Space Combat:** Constrained circular movement along a fixed orbital cylinder radius (`13.72` units) requiring dynamic elevation maneuvering and directional flips.
- **Wave-Based Escalation:** Data-driven pacing powered by timeline-based spawning streams that scale enemy density, types, and intervals over time.
- **Roguelite Progression:** Extensible skill drafting system offering real-time statutory and projectile pattern upgrades on level-up.
- **High-Fidelity Visuals & Audio Pipeline:** HDRP-driven lighting, volumetric fog, dynamic resolution scaling with temporal upscalers (DLSS/FSR2), and a dedicated voice-managed audio architecture.

---

## 2. Gameplay Flow / User Loop
1. **Boot & Initialization:**
   - The game launches into `Menu.unity`.
   - `AudioDirector` and `GraphicsDirector` auto-instantiate via `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` with `DontDestroyOnLoad` to persist settings across scenes.
   - Settings are restored from `PlayerPrefs` via `SettingsStore`.
2. **Main Menu Loop:**
   - `MainMenu` handles screen navigation (Start Run, Settings, Tutorial, Exit).
   - Audio and graphics settings allow runtime adjustments to volume channels, display modes, upscale methods, and post-processing quality.
3. **The Run Loop:**
   - Loading `Game.unity` initializes `WaveDirector`, `EXP`, and the `Player` spaceship.
   - `WaveDirector` starts spawning enemy streams from the active `WaveDefinition`.
   - The player navigates horizontally (orbiting around the center) and vertically (climbing/diving within vertical bounds enforced by `ApplyBounds`), flipping firing direction as needed.
   - Projectiles and hit effects spawn and despawn continuously through `ObjectPool`.
4. **Progression & Upgrades:**
   - Destroyed enemies grant experience points, received by the `EXP` singleton.
   - Filling the EXP threshold triggers `Player.LevelUp()`, pausing the simulation and opening `SkillSelect` / in-world `Pickup` items via `SkillPool`.
   - Selecting a skill modifies stats on `ISkillTarget` (attack speed, move speed, max health, healing, multi-shot patterns).
5. **Run Outcome:**
   - **Defeat:** When player health drops to zero, `TakeHit()` halts combat and displays `DeathMenu` with run statistics recorded in `RunStats`.
   - **Victory:** Defeating the final boss or completing the wave duration triggers `VictoryMenu` with final metrics.

---

## 3. Architecture
The architecture is structured around decoupled systems communicating through C# events, interfaces, and static service accessors.

```
+--------------------------------------------------------------------------------+
|                             Core Services & Systems                            |
|  [AudioDirector]   [GraphicsDirector]   [GameInput]   [ObjectPool]   [EXP]     |
+--------------------------------------------------------------------------------+
         |                     |                 |            ^           ^
         v                     v                 v            |           |
+------------------+  +-------------------+  +------------------+         |
|   UI Subsystem   |  | Graphics/Pipeline |  |   Player & Guns  |---------+
| (Holo / Screens) |  | (VolumeOverrides) |  |   (ISkillTarget) |
+------------------+  +-------------------+  +------------------+
                                                      |
                                             +------------------+
                                             |  Wave & Enemies  |
                                             |  (HealthState)   |
                                             +------------------+
```

### Key Architectural Principles
- **Data-Driven Configuration:** ScriptableObjects define skills (`SkillDefinition`), enemies (`EnemyDefinition`), waves (`WaveDefinition`), and audio palettes (`GameSounds`, `SoundDefinition`).
- **Decoupled Combat Math:** Pure C# non-MonoBehaviour models (e.g., `HealthState`, `SkillPool`, `DynamicResolutionController`, `VolleyTimer`) handle logic independently of Unity scenes, enabling EditMode testing in `Assets/Tests/EditMode`.
- **Zero-Allocation Pooling:** Projectiles, particle hit effects, and enemy units recycle through `ObjectPool` to prevent garbage collection hitches during high projectile density.
- **Assembly Definition Boundaries:** Code is split across `SurvivalChaos.asmdef`, `SurvivalChaos.Editor.asmdef`, and `SurvivalChaos.Tests.EditMode.asmdef`.

Location: `Assets/Scripts/Core`, `Assets/Scripts/Systems`

---

## 4. Game Systems & Domain Concepts

### Orbital Geometry & Movement System
Governs the circular cylindrical arena topology, camera tracking, and coordinate mapping.
- `ArenaGeometry`: Static math utility establishing the canonical `OrbitRadius` (`13.72f`) and orbit projection algorithms (`ProjectOntoOrbit`).
- `PlayerMovement`: Orbits the ship along the arena axis at a synchronized angular velocity and applies vertical elevation controls.
- `SpaceShipPitch`: Dynamically tilts the ship mesh depending on vertical climb velocity and flips facing when reversing direction.
- `ShipMotion`: Easing and interpolation helper (`Approach`) for frame-rate-independent rotation transitions.
- `ApplyBounds`: Constrains entities within the authorized vertical ceiling and floor boundaries.
- `SnapToOrbit`: Helper component that ensures scene entities adhere to the exact arena radius.

**Extension:** To adjust arena dimension, alter `ArenaGeometry.OrbitRadius`; both player and enemy navigation scripts derive station-keeping logic from this single constant.

**Design Pattern:** Static Utility & Coordinate Projection.

Location: `Assets/Scripts/Gameplay/Arena`, `Assets/Scripts/Gameplay/Player`

---

### Combat & Health System
Handles hit registration, damage resolution, single-kill dispatching, and hit reactions.
- `HealthState`: Pure C# data model tracking current/max hitpoints, death status, and atomic damage application.
- `Enemy`: MonoBehaviour bridging Unity trigger collisions to `HealthState`, granting EXP on death, and returning instances to `ObjectPool`.
- `EnemyDefinition`: ScriptableObject specifying base HP, EXP yield, speed, and sound triggers.
- `HitFlash`: Material-based visual feedback flashing emissive colors upon taking damage.
- `ObstacleScript`: Specialized enemy behavior acting as stationary or fixed-track orbital obstacles.

**Extension:** Create custom enemy variations by adding new `EnemyDefinition` ScriptableObjects under `Assets/Data/Enemies` and assigning them to enemy prefabs.

**Design Pattern:** State Pattern (encapsulated in pure C# `HealthState`).

Location: `Assets/Scripts/Gameplay/Enemies`

---

### Wave & Spawning System
Controls enemy wave timelines, stream pacing, and spawn positioning.
- `WaveDirector`: Core timeline runner that iterates through all `SpawnStream` sequences in the active wave.
- `WaveDefinition`: ScriptableObject housing all stream configurations and global wave stop limits.
- `SpawnStream`: Serialized stream definition establishing prefab reference, start delay, base interval, interval scaling curves, and spatial spawn offsets.
- `SpawnMath`: Computes ramped spawn intervals over elapsed run time.
- `SpawnBand`: Validates that spawn stream heights fall within player reach envelopes.

**Extension:** Create new wave sequences by authoring `WaveDefinition` assets in `Assets/Data/Waves` and assigning them to `WaveDirector.wave`.

**Design Pattern:** Director Pattern with Timeline-driven Coroutine Streams.

Location: `Assets/Scripts/Gameplay/Waves`

---

### Skill & Upgrade System
Manages level-up reward drafting, selection pools, and player stat progression.
- `SkillDefinition`: Abstract ScriptableObject base for all player upgrades.
- `ISkillTarget`: Interface exposing player upgrade operations (`UpgradeShotPattern`, `AddMaxHealth`, `Heal`, `IncreaseAttackSpeed`, `IncreaseMoveSpeed`).
- `SkillPool`: Manages pick availability, exhaustion limits (`MaxPicks`), and random draws.
- `AttackSpeedSkill`: Decreases the fire delay step on `ISkillTarget`.
- `MoveSpeedSkill`: Adds speed bonuses to `PlayerMovement`.
- `MaxHealthSkill`: Increases health pool and grants matching immediate health.
- `HealSkill`: Restores lost health points.
- `ShotUpgradeSkill`: Advances projectile firing pattern stages (single, double, triple, sextuple).
- `Pickup`: In-world collectible representation of a skill offer on the orbit ring.
- `PickupSpawner`: Places skill pickups dynamically in the arena upon level-up.

**Extension:** Create a new upgrade by inheriting from `SkillDefinition`, implementing `Apply(ISkillTarget target)`, and authoring an asset in `Assets/Data/Skills`.

**Design Pattern:** Strategy Pattern (concrete `SkillDefinition` strategies applied to `ISkillTarget`).

Location: `Assets/Scripts/Gameplay/Skills`, `Assets/Scripts/Gameplay/Pickups`

---

### Boss System
Controls multi-phase boss behaviors, dynamic emitters, and attack volleys.
- `BossAttack`: Serialized attack configuration defining muzzle pivots, projectile variants for left/right travel, intervals, and trigger conditions.
- `BossEmitter`: Manages boss volley execution and directional firing patterns.
- `VolleyTimer`: Pure C# timer managing volley cadence and attack synchronization.

**Extension:** Add attack patterns directly in the Inspector of the Boss prefab by expanding the `BossAttack` array.

**Design Pattern:** Composite Attack Emitter.

Location: `Assets/Scripts/Gameplay/Boss`

---

### Object Pooling System
Optimizes allocation-heavy combat elements to eliminate runtime garbage collection spikes.
- `ObjectPool`: Static manager maintaining pooled stacks for prefabs (`Spawn`, `Despawn`, `Warm`, `Clear`).
- `PooledInstance`: Attached to pooled clones to retain prefab source linkage and suppress motion vector smears for a single frame.
- `PoolMotionReset`: Manages motion vector resets on reused instances.

**Extension:** Any prefab spawned via `ObjectPool.Spawn(prefab, pos, rot)` automatically becomes pooled. Return it using `ObjectPool.Despawn(instance)`.

**Design Pattern:** Object Pool Pattern.

Location: `Assets/Scripts/Systems/Pooling`

---

### Audio System
Manages multi-channel sound playback, voice throttling, and persistent audio levels.
- `AudioDirector`: Singleton persisting across scenes (`DontDestroyOnLoad`), managing a 24-voice `AudioSource` pool with cooldown limits and logarithmic amplitude conversions.
- `GameSounds`: Resources-backed ScriptableObject (`Resources/GameSounds.asset`) holding global sound mappings.
- `SoundDefinition`: ScriptableObject wrapping audio clips, volume, pitch variations, mixer routing, and spatial blend.
- `AudioLevels`: Utility converting linear UI slider values to logarithmic decibel amplitudes.
- `MusicSource`: Registers scene background tracks with `AudioDirector`.

**Extension:** Add sound definitions under `Assets/Audio/Definitions` and assign them to `Resources/GameSounds.asset` or specific `EnemyDefinition` assets.

**Design Pattern:** Audio Service & Voice Pool.

Location: `Assets/Scripts/Systems/Audio`

---

### Graphics & Display Pipeline
Controls HDRP render quality, volume overrides, display resolutions, anti-aliasing, and temporal upscalers.
- `GraphicsDirector`: Singleton persisting across scenes that manages HDRP quality levels, custom volume overrides (`VolumeProfile`), and platform upscaler hooks.
- `GraphicsPresets`: Encapsulates default graphical tiers.
- `DynamicResolutionController`: Smoothly modulates render scale percentage in response to measured frame times.
- `DisplayOptions`: Utility for display enumeration, refresh rate matching, and upscaler quality mapping.
- `SettingsStore`: Debounces and commits dirty settings to `PlayerPrefs`.

**Extension:** Custom visual tiers and overrides can be extended by modifying `GraphicsPresets` or adjusting volume properties in `GraphicsDirector.BuildOverrideVolume()`.

**Design Pattern:** Facade / Settings Director.

Location: `Assets/Scripts/Systems/Graphics`

---

### Input Abstraction System
Decouples gameplay scripts from specific Unity Input APIs.
- `GameInput`: Static access point exposing horizontal, vertical, flip, and debug actions.
- `IGameInput`: Backend interface for input polling.
- `InputSystemGameInput`: Unity New Input System implementation.
- `LegacyGameInput`: Unity Legacy Input implementation fallback.

**Extension:** Switch backends or mock input in tests by assigning a custom `IGameInput` implementation to `GameInput.Source`.

**Design Pattern:** Strategy / Adapter Pattern.

Location: `Assets/Scripts/Systems/Input`

---

## 5. Scene Overview

### Scenes
- `Assets/Scenes/Menu.unity`: Entry point scene containing the main user interface, holographic video background, audio setup, and settings menus.
- `Assets/Scenes/Game.unity`: Core simulation scene housing the circular arena geometry, HDRP volume environments, `WaveDirector`, `PlayerSpaceShip`, boss prefabs, and HUD overlay canvas.

### Scene Flow Rules & Lifecycle
- `Menu.unity` is indexed first in build settings.
- Global managers (`AudioDirector`, `GraphicsDirector`) boot automatically before the first scene loads via `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`.
- Transitioning between scenes uses `SceneManager.LoadScene()`.
- Static data (e.g., `ObjectPool`, `PlayerMovement.speedMultiplier`, `AudioDirector.LevelsChanged`) is cleared on scene unload hooks (`SceneManager.sceneUnloaded`) and playmode domain resets (`RuntimeInitializeLoadType.SubsystemRegistration`) to ensure zero cross-run state pollution.

---

## 6. UI System
Built using standard Unity UI (UGUI) with custom holographic styling components.

### UI Structure & Components
- `MainMenu`: Controls navigation across title screen panels (Play, Options, Tutorial, Quit).
- `PauseMenu`: Toggles pause state via `Time.timeScale` and presents resume, options, and restart actions.
- `DeathMenu`: Activated upon player destruction, displaying final survival time, waves cleared, and level reached.
- `VictoryMenu`: Activated on boss defeat or wave completion, showing run victory summaries.
- `SkillSelect`: Modal upgrade dialog displaying drafted `SkillDefinition` cards.
- `HealthBar` / `ExpBar` / `BossHpBar`: HUD gauges updated via direct API calls or events.
- `BarMotion` & `HoloBar`: Animated bar filling feedback with smooth interpolation.
- `VolumeControl` & `SharpnessControl`: Specialized sliders bound to `AudioDirector` and `GraphicsDirector` settings.
- `HoloButtonHighlight` & `HoloMenuEntry`: Holographic UI visual state decorators.

### Modifying and Adding UI Screens
1. UI scripts inherit from `MenuScreen` or standard `MonoBehaviour`.
2. Interactive controls bind events to `AudioDirector.Instance.SetLevel()` or `GraphicsDirector.Instance`.
3. Editor utilities in `Assets/Scripts/UI/Editor/` (`HoloUiFactory`, `HoloMenuBuilder`) assist in generating stylized holographic panels.

Location: `Assets/Scripts/UI`

---

## 7. Asset & Data Model

### Asset Storage Conventions
- `Assets/Data/Enemies/`: `EnemyDefinition` ScriptableObjects (`Boss.asset`, `Fighter.asset`, `Scout.asset`, etc.).
- `Assets/Data/Skills/`: `SkillDefinition` ScriptableObjects (`AttackSpeed.asset`, `Heal.asset`, `MoveSpeed.asset`, `ShotUpgrade.asset`, etc.).
- `Assets/Data/Waves/`: `WaveDefinition` ScriptableObjects (`MainRun.asset`).
- `Assets/Audio/Definitions/`: `SoundDefinition` ScriptableObjects.
- `Assets/Resources/GameSounds.asset`: Global lookup asset for named game sounds.
- `Assets/Prefabs/`: Entity and VFX prefabs organized into subdirectories (`Player/`, `Enemies/`, `Boss/`, `Projectiles/`, `VFX/`, `Pickups/`).
- `Assets/Art/`: 3D models (`Art/Models/`), Materials (`Art/Materials/`), Shaders (`Art/Shaders/`), and Textures (`Art/Textures/`).

### Naming & Organization Rules
- Runtime code is contained entirely inside `Assets/Scripts/` and namespaced under `SurvivalChaos`.
- Custom Editor tools reside in `Assets/Scripts/Editor/` or `Assets/Scripts/UI/Editor/`.
- EditMode unit tests reside in `Assets/Tests/EditMode/`.

---

## 8. Notes, Caveats & Gotchas

- **Camera-Player Station Keeping:** The Main Camera carries an instance of `PlayerMovement` to orbit and climb in tandem with the player. Any speed bonus granted to the player is applied globally via `PlayerMovement.AddSpeedBonus()`, ensuring the camera stays synchronized with the player's orbit.
- **Orbit Projection Math:** `PlayerMovement` computes angular delta by dividing world speed by `ArenaGeometry.OrbitRadius`, rather than the camera's own radius (which is positioned 7.5 units further out). Changing this formula will cause the camera to drift out of alignment.
- **Domain Reload Safety:** All singletons and static caches (`ObjectPool`, `EXP.Instance`, `GameSounds`, `PlayerMovement`, `AudioDirector`) hook `SubsystemRegistration` and `SceneManager.sceneUnloaded` to reset static fields. When adding new static fields or singletons, ensure they implement cleanup handlers to prevent state leakage across runs.
- **Wave Stream Height Bounds:** Enemy spawn stream heights authored in `WaveDefinition` must align with the `ApplyBounds` volume in the scene. `WaveDirector` performs an Editor-time validation check at `Start()` and warns if any stream spawns outside the reachable altitude band.
- **HDRP Volume Override Priority:** `GraphicsDirector` creates an override volume with priority `10000f` to ensure settings menu choices take precedence over scene-authored volume settings.
- **Resources Directory Constraint:** `GameSounds.asset` must remain located at `Assets/Resources/GameSounds.asset` as systems instantiate and locate audio definitions dynamically without direct scene references.