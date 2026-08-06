# Survival Chaos - Project Overview

## 1. Project Description
Survival Chaos is a high-octane 3D space-shooter survival game built with Unity and the High Definition Render Pipeline (HDRP). Players control a starship orbiting a central axis, fending off waves of robotic enemies while collecting experience to unlock and upgrade skills. The game focuses on circular arena combat, where movement is constrained to an orbital path, creating a unique "orbital bullet-hell" experience. Core pillars include tight responsive movement, scalable difficulty via wave-based spawning, and a roguelite-inspired skill progression system.

## 2. Gameplay Flow / User Loop
1.  **Boot & Menu:** The game starts in the `Menu` scene, providing access to volume settings and starting a run.
2.  **The Run:** Upon starting, the player is placed in the `Game` scene. The ship orbits a central point, and `WaveDirector` begins spawning enemies based on `WaveDefinition` assets.
3.  **Survival & XP:** Players destroy enemies to earn XP. Collecting enough XP triggers a Level Up, pausing the game to present a selection of random `SkillDefinition` rewards.
4.  **Scaling Difficulty:** Waves ramp up in intensity (spawn frequency and enemy types) over time until a Boss encounter occurs.
5.  **Shutdown/Outcome:** If health reaches zero, the `DeathMenu` appears, allowing for a restart or return to the menu. Victory occurs upon defeating the final boss or surviving the full wave duration.

## 3. Architecture
The project follows a modular, data-driven approach leveraging `ScriptableObjects` for configuration and an `ObjectPool` system for performance optimization.

-   **Core Lifecycle:** The `Game` scene serves as the primary simulation space. Systems are mostly decoupled, communicating via C# events (e.g., `EXP.OnEXPChange`) or singleton access for global services like input and pooling.
-   **Static Geometry & Movement:** Logic for the circular arena is centralized in `ArenaGeometry`, ensuring that the player, enemies, and camera all respect the `OrbitRadius`.
-   **Data-Driven Design:** Skills, enemies, and waves are defined as assets. This allows designers to tune the experience without modifying source code.
-   **Performance:** Extensive use of `ObjectPool` for projectiles and VFX to minimize GC pressure during intense combat sequences.

Location: `Assets/Scripts/Core`, `Assets/Scripts/Systems`

## 4. Game Systems & Domain Concepts

### Orbital Combat System
Governs movement and positioning within the circular arena.
-   `ArenaGeometry`: Static utility defining the `OrbitRadius` (13.72 units) and projection math.
-   `PlayerMovement`: Handles player-controlled orbital speed and direction flipping.
-   `EnemyMovement`: Implements "Orbit and Chase" logic, where enemies converge on the orbit radius before maneuvering.
-   `ShipMotion`: Provides frame-rate independent easing (Approach) for smooth ship rotations and flips.

Location: `Assets/Scripts/Gameplay/Arena`, `Assets/Scripts/Gameplay/Player`

### Skill & Progression System
A roguelite upgrade loop that modifies player capabilities during a run.
-   `SkillDefinition`: Abstract `ScriptableObject` base for all upgrades (e.g., `HealSkill`, `ShotUpgradeSkill`).
-   `SkillPool`: Manages the collection of available skills and handles random selection logic.
-   `ISkillTarget`: Interface implemented by the `Player` class to allow skills to modify stats like health, attack speed, and fire patterns.
-   `EXP`: A singleton manager that tracks experience points and fires level-up events.

Location: `Assets/Scripts/Gameplay/Skills`, `Assets/Scripts/Core/EXP.cs`

### Wave & Spawning System
Controls the pacing and composition of enemy encounters.
-   `WaveDirector`: The central controller that executes a `WaveDefinition`.
-   `WaveDefinition`: Data asset containing a list of `SpawnStream` configurations.
-   `SpawnStream`: Defines what to spawn, when to start, and how to ramp up the spawn frequency over time.
-   `SpawnMath`: Utility for calculating intervals based on elapsed time and ramp scales.

Location: `Assets/Scripts/Gameplay/Waves`

### Health & Damage System
A decoupled logic layer for handling entity life.
-   `HealthState`: A pure C# class (non-MonoBehaviour) that manages HP and ensures death logic (like XP rewards) only fires once.
-   `Enemy`: Component that bridges Unity collisions with `HealthState` and triggers explosions/rewards.
-   `EnemyDefinition`: `ScriptableObject` defining base HP and XP rewards for different enemy types.

Location: `Assets/Scripts/Gameplay/Enemies`

## 5. Scene Overview
-   **Menu:** The entry point (`Assets/Scenes/Menu.unity`). Contains the main UI, background visuals, and menu music logic.
-   **Game:** The primary gameplay scene (`Assets/Scenes/Game.unity`). It houses the `WaveDirector`, the player prefab, the orbital arena geometry, and HDRP lighting setups.
-   **Lighting Scenes:** The project uses specific lighting assets like `Game Baking Set.asset` and `New Lighting Settings.lighting` to maintain HDRP visual quality.

## 6. UI System
The project uses the standard Unity UI (UGUI) system, often enhanced with custom "Holo" visual styles.
-   **HUD:** Displays current health (`HealthBar`), experience (`ExpBar`), and active skill slots.
-   **Menus:** `MainMenu`, `PauseMenu`, `DeathMenu`, and `VictoryMenu` manage screen transitions.
-   **Skill Selection:** `SkillSelect` handles the UI popup during level-ups, displaying 3 random skill choices to the player.
-   **Motion:** `BarMotion` and `HoloBar` provide animated feedback for UI elements.

Location: `Assets/Scripts/UI`

## 7. Asset & Data Model
-   **ScriptableObjects:** All game balance is stored here.
    -   `EnemyDefinition`: `Assets/Data/Enemies/`
    -   `SkillDefinition`: `Assets/Data/Skills/`
    -   `WaveDefinition`: `Assets/Data/Waves/`
-   **Prefabs:** Entities are fully encapsulated as prefabs (Player, Enemies, Projectiles, VFX) located in `Assets/Prefabs`.
-   **Naming Convention:** Folders are organized by functional type (Art, Data, Prefabs, Scripts), with scripts further subdivided by system (Gameplay, Systems, UI).
-   **Assembly Definitions:** Uses `SurvivalChaos.asmdef` for the main codebase and `SurvivalChaos.Tests.EditMode.asmdef` for unit tests.

## 8. Notes, Caveats & Gotchas
-   **Orbital Radius:** The value `13.72f` in `ArenaGeometry` is critical. Changing this without updating the environment models will cause entities to fly through or outside the intended play area.
-   **Frame-Rate Independence:** Always use `ShipMotion.Approach` or `Time.deltaTime` for movement logic to ensure consistent behavior across different hardware.
-   **Object Pooling:** New projectiles or VFX must be "warmed" in `Player.Start` or `WaveDirector` to prevent frame-spikes when they are first spawned.
-   **Input:** The project uses the "New Input System". Global inputs are accessed via the `GameInput` wrapper.
-   **Health Logic:** `HealthState` is a plain class used to avoid "double-death" bugs. Do not bypass it by destroying objects directly based on triggers without calling `TakeDamage`.