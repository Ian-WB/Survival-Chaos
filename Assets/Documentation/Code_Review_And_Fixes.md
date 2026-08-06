# Code Review & Fixes Summary

This document details all shader compilation errors, gameplay logic bugs, performance risks, and code quality issues identified and resolved in the project.

---

## 1. Shader Compilation Error

### **File:** `Assets/UI/Shaders/HoloPanel.shader`
- **Severity:** Critical (Compilation Error)
- **Problem:** The shader failed to compile on D3D11 with the error:
  `Shader error in 'Survival Chaos/Holo Panel': 'clip': identifier represents a variable, not a function at line 179`
  When both `UNITY_UI_CLIP_RECT` and `UNITY_UI_ALPHACLIP` keywords were active, a local float variable named `clip` (`float clip = UnityGet2DClipping(...)`) shadowed the built-in HLSL function `clip(...)`.
- **Fix:** Renamed the local variable `clip` to `clipFactor` inside the `UNITY_UI_CLIP_RECT` block to avoid shadowing the built-in function:
  ```hlsl
  #ifdef UNITY_UI_CLIP_RECT
  float clipFactor = UnityGet2DClipping(i.world.xy, _ClipRect);
  rgb *= clipFactor;
  alpha *= clipFactor;
  #endif

  #ifdef UNITY_UI_ALPHACLIP
  clip(alpha - 0.001);
  #endif
  ```

---

## 2. Gameplay & Logic Bugs

### **File:** `Assets/Scripts/PlayerScrpts/Player.cs`
- **Severity:** Critical (Broken Feature)
- **Problem:** The player initialized automatic shooting in `Awake()` via `InvokeRepeating(nameof(Shoot), initialDelay, spawnDelay)`. When picking the Attack Speed upgrade skill (`AttackSpeedSkill`), `IncreaseAttackSpeed()` reduced `spawnDelay`, but modifying `spawnDelay` alone does not alter an already running `InvokeRepeating` schedule in Unity. As a result, attack speed upgrades had no effect in gameplay.
- **Fix:** Restarted the repeating invoke timer inside `IncreaseAttackSpeed()`:
  ```csharp
  public void IncreaseAttackSpeed()
  {
      spawnDelay = spawnDelay - (0.40f * spawnDelay);
      CancelInvoke(nameof(Shoot));
      InvokeRepeating(nameof(Shoot), spawnDelay, spawnDelay);
  }
  ```

---

### **Files:** `Assets/Scripts/EnemyScripts/EnemyMovement.cs` & `ObstacleScript.cs`
- **Severity:** High (Console Spam / Runtime Exception)
- **Problem:** Both scripts cached `player = GameObject.Find("Player").transform;` in `Start()` and accessed `player.position` directly in `Update()`. When the player died and was destroyed, `player` became `null` (destroyed object reference), causing continuous `NullReferenceException` / `MissingReferenceException` spam every frame for all active enemies and obstacles.
- **Fix:** Added null checks in `Start()` and `Update()`:
  ```csharp
  // Start
  GameObject playerObj = GameObject.Find("Player");
  if (playerObj != null)
  {
      player = playerObj.transform;
  }

  // Update
  if (player == null)
  {
      return;
  }
  ```

---

### **File:** `Assets/Scripts/EXP.cs`
- **Severity:** Medium (Orphaned GameObjects)
- **Problem:** In the Singleton `Awake()` method:
  ```csharp
  if (Instance != null && Instance != this)
  {
      Destroy(this); // Only destroyed the C# component script
  }
  ```
  `Destroy(this)` removed only the script component, leaving orphaned duplicate `GameObject` instances in the scene hierarchy upon scene reloads.
- **Fix:** Updated `Destroy(this)` to `Destroy(gameObject)` to properly clean up duplicate GameObjects.

---

## 3. Code Quality & Defensive Programming

### **File:** `Assets/Scripts/PlayerScrpts/Player.cs`
- **Severity:** Low (Variable Shadowing)
- **Problem:** In `Start()`, `GameObject instantiatedChild = Instantiate(...)` created a local variable that shadowed the private class field `instantiatedChild`.
- **Fix:** Replaced the local variable declaration with direct assignment to the existing class field:
  `instantiatedChild = Instantiate(childPrefab, childObject);`

---

### **File:** `Assets/Scripts/PlayerScrpts/PlayerMovement.cs`
- **Severity:** Low (Potential Null Pointer)
- **Problem:** `Update()` accessed `center.position` without verifying if `center` was assigned in the Inspector. If unassigned, it threw `NullReferenceException` every frame.
- **Fix:** Added a null check at the start of `Update()`:
  ```csharp
  if (center == null)
  {
      return;
  }
  ```

---

## Verification
- All edited scripts and shaders compile cleanly in the Unity Editor with 0 errors and 0 warnings.
