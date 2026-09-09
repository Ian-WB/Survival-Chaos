# Working on Survival Chaos from a terminal

A guide for an AI agent with shell and file access to this machine. Read it
before touching anything. Most of the rules below exist because a specific
thing went wrong once.

Survival Chaos is an orbital survival shooter — the player's ship orbits a
ring-shaped arena, waves of enemies spawn, and a boss closes the run. It was a
2023 university game-jam project, rebuilt from scratch on **Unity 6.6 /
HDRP 17.6**, C#, Windows standalone. Version 0.9.0, in polish.

---

## 1. Two folders, and which one is which

```
E:\Work\UnityProjects\Survival-Chaos-Project\     <- outer. NOT a git repo.
├── Builds\                                       <- player builds live here
└── Survival-Chaos\                               <- inner. The git repo AND the Unity project root.
    ├── Assets\
    ├── ProjectSettings\
    ├── Packages\
    └── Docs\
```

Git commands must run in the **inner** folder. Builds must be written to the
**outer** one. Getting this backwards is the single most common mistake here,
and it fails quietly in both directions: `git status` in the outer folder says
"not a repository", and a *relative* build path resolves against the Unity
project root, so it silently puts a 900 MB build inside `Assets`' sibling
instead of next to the other builds.

Always pass build paths as absolute.

---

## 2. Git

- Repo root is `Survival-Chaos/`.
- **Ian commits himself, in GitHub Desktop.** Never run `git commit`, `git
  push`, `git reset --hard`, or anything that rewrites history. When work is
  done, hand him a **Summary** line and a **Description** body as plain text,
  as **one** commit — never propose splitting it.
- `.gitignore` excludes `/[Bb]uilds/` at any depth, so builds never reach git.
- **Empty diffs are line-ending churn, not changes.** A file can show as
  modified while `git diff -- <file>` prints no `+`/`-` content lines at all.
  That is Unity rewriting CRLF/LF. Revert those files rather than committing
  them:

  ```bash
  git checkout -- "path/to/file.mat"
  ```

---

## 3. The Unity editor is open, and that changes everything

Ian works with the editor running. Almost every rule below follows from it.

**Never edit these on disk**, even though you can see them: `.unity`,
`.prefab`, `.asset`, `.meta`, anything under `ProjectSettings/`. The editor
holds its own copy in memory and will overwrite yours without warning. Reading
them to *understand* something is fine; treat what you read as possibly stale.

**Do edit these on disk**: everything under `Assets/Scripts/**/*.cs`. Then tell
the editor (section 5).

**Check the editor is reachable before relying on any of it:**

```bash
unity pipeline list
```

The `Server Reachable` column must be true. If it is blank or false, the editor
is closed or restarting and every command in sections 4–7 will fail. Ask Ian to
bring it up rather than working around it.

---

## 4. The relay: driving the running editor

This is the main tool, and it is much larger than it first looks. List every
command it offers:

```bash
unity command                              # ~140 commands, with descriptions
unity command --query material             # search by substring
unity command --query set_serialized_field --detail full   # show its parameters
```

**Prefer a typed command over `eval`.** They validate their inputs, return
structured results, and cannot half-apply a change the way a script that throws
partway through can. Reach for `eval` only when nothing fits.

### Looking, without changing anything

| Command | Use |
| --- | --- |
| `editor_status` | `compiling`, `playMode`, `domainReloadInProgress`, Unity version. Check this **first**, every time |
| `console --tail 50 --level error` | the Unity console, including stack traces |
| `get_scene_hierarchy`, `find_gameobjects`, `find_assets`, `search` | locate things |
| `get_component_properties --target <h> --type <T>` | a component's serialized properties as JSON |
| `get_serialized_fields` | what a component actually exposes |
| `get_player_settings`, `get_quality_settings`, `get_graphics_settings`, `get_lighting_settings`, `get_build_settings` | project configuration, read correctly rather than parsed out of YAML |
| `get_performance_stats` | frame timings |
| `capture_game_view`, `capture_scene_view` | a PNG of what is on screen — the fastest way to check a visual change |

### Changing things

| Command | Use |
| --- | --- |
| `set_serialized_field --target <h> --component <T> --field <name> --value <v>` | the workhorse. Handles primitives, enums, Vector/Color/Rect/Bounds, object references, and array elements via `name.Array.data[i]` |
| `set_component_properties`, `set_transform`, `set_parent`, `set_layer`, `set_tag` | scene edits |
| `add_component`, `remove_component`, `instantiate_prefab` | composition |
| `set_material_properties`, `set_import_settings` | assets |
| `save_scene`, `save_all`, `save_prefab_contents` | persist. Nothing is on disk until you do |
| `import_asset`, `reload_file` | make the editor re-read a file you changed underneath it |

### Scripts

`read_text_file` and `write_text_file` work inside the authoring root (see
`get_authoring_root`) — outside it, use ordinary shell file access. Then
`recompile`, and poll `recompile_status`. `create_script` and `attach_script`
exist, but note a freshly created type does not exist until a recompile
finishes, so attaching it before that fails with a recoverable error.

### Play mode, tests, baking

`editor_play`, `editor_pause`, `editor_stop`, `run_tests`, `list_tests`,
`test_status`, `bake_lighting` + `lighting_bake_status`, `bake_occlusion_culling`,
`bake_navmesh`.

**Do not start play mode without asking.** Ian is often mid-session, and an
unattended player dies within about a minute anyway — the death screen then sets
`timeScale` to 0, which freezes coroutines and makes everything you measure
afterwards a lie.

### Destructive — needs `confirm=true` and Ian's explicit say-so

`delete_asset`, `delete_gameobject`, `clear_baked_lighting`, `clear_navmesh`,
`clear_occlusion_culling`, `unpack_prefab`, `revert_prefab_overrides`,
`package_remove`. A cleared bake costs hours to regenerate.

### `eval`, the escape hatch

```bash
unity command eval --code 'return "hello";'
```

Runs C# in the editor's own AppDomain with `UnityEngine`, `UnityEditor` and the
project's assemblies loaded. Rules learned the hard way:

- **It must `return` something.** `Debug.Log` goes to the console and the CLI
  reports `"result":null`, which looks exactly like a failure.
- Pull the answer out with `... 2>&1 | grep -o '"result":"[^"]*"'`.
- **Keep each call short.** Long scripts fail with a bogus "unreachable code"
  error. Split the work up. (`eval_file` exists and has the same limit.)
- **No output at all usually means an exception**, not an empty answer.
  Simplify until it speaks.
- Project types are in namespace `SurvivalChaos` (assembly `SurvivalChaos`) and
  `SurvivalChaos.EditorTools` (assembly `SurvivalChaos.Editor`) — the namespace
  and assembly names differ. If `System.Type.GetType("...")` returns null, scan:

  ```csharp
  System.Type t = null;
  foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
  { foreach (var x in a.GetTypes()) if (x.Name == "BossEmitter") { t = x; break; } if (t != null) break; }
  ```

- **`objectReferenceInstanceIDValue` throws** while walking serialized
  properties and takes the whole eval down silently. Use `objectReferenceValue`.

---

## 5. Changing a script, and proving it took

Check `editor_status` first — a recompile during play mode throws away Ian's
session.

Edit the `.cs` on disk, then:

```bash
unity command recompile
unity command recompile_status      # poll until it settles
```

`recompile` answers `up_to_date` with "No scripts needed recompilation" when
there is nothing to do — which is also what you get if you edited a file the
editor cannot see, so treat it as a signal rather than a success.
`recompile_status` carries `failed` and an `errors` array; read them, because a
compile failure leaves the previous assembly loaded and everything downstream
keeps working against stale code.

**Then verify by reflection that your change is in the loaded assembly** — that
a new method exists, or a const reads its new value. Do not assume the recompile
picked it up. A build made against unrefreshed code warns "you have uncompiled
code changes" and quietly ships the old behaviour.

```bash
unity command eval --code 'System.Type t=null; foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies()) { foreach (var x in a.GetTypes()) if (x.Name=="BuildBossRig") { t=x; break; } if (t!=null) break; } var f = t.GetField("PodHealth", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static); return "PodHealth=" + f.GetRawConstantValue();'
```

---

## 6. Changing assets and prefabs (only through the editor)

Reach for `set_serialized_field` first. Where it cannot express the change — a
walk over a prefab's whole contents, say — use `eval`:

```csharp
var path = "Assets/Prefabs/Boss/Boss.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
// ...walk root.GetComponentsInChildren<MonoBehaviour>(true) with SerializedObject...
UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
```

A ScriptableObject field the same way:

```csharp
var o = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>("Assets/Data/Enemies/Boss.asset");
var so = new UnityEditor.SerializedObject(o);
var p = so.FindProperty("maxHealth");
int old = p.intValue; p.intValue = 600;
so.ApplyModifiedProperties();
UnityEditor.EditorUtility.SetDirty(o);
UnityEditor.AssetDatabase.SaveAssets();
return "maxHealth " + old + " -> " + p.intValue;
```

Always report the old value beside the new one, so the change is auditable from
the transcript afterwards.

Be aware that `AssetDatabase.SaveAssets()` re-serializes unrelated assets and
leaves line-ending-only diffs behind. Check `git status` when you are done and
revert anything you did not mean to touch.

---

## 7. Builds

`unity build` — the plain CLI build — **cannot run while the editor is open**.
It spawns a second editor and dies with "another Unity instance is running with
this project open". Use the relay build:

```bash
unity command build --target StandaloneWindows64 \
  --outputPath "E:/Work/UnityProjects/Survival-Chaos-Project/Builds/26_0908-1416_lancebeam/Survival-Chaos-main.exe" \
  --options CleanBuildCache --confirm true
```

Naming is `YY_MMDD-HHMM_shortLabel`. It returns `{"status":"queued"}`
immediately; poll `unity command build_status` until `completed`. A healthy
build is ~900 MB and takes 10–30 seconds warm.

**Verify against the shipped assembly, not the project** —
`Builds/<stamp>/Survival-Chaos-main_Data/Managed/SurvivalChaos.dll`. Type and
method names appear as ASCII; user-facing strings appear as UTF-16LE.

**Every build leaves churn. Clean it up or it rides along in Ian's commit:**

| File | What happens | What to do |
| --- | --- | --- |
| `Assets/Settings/HDRenderPipelineGlobalSettings.asset` | gains a debugger rid, or line-ending-only diff | revert, then `AssetDatabase.ImportAsset(..., ForceUpdate)` |
| `ProjectSettings/TimeManager.asset` | re-serialized into 6.6 rational form | revert |
| assorted assets | line-ending-only diffs | revert |
| `ProjectSettings/ProjectSettings.asset` | `runInBackground` set to 0 | see below |

`runInBackground` is **not** churn to revert blindly. The wanted value is **1** —
`Assets/Scripts/Editor/RunInBackgroundOff.cs` explains that the pipeline server
sets it so an unfocused editor keeps updating, which is what makes all of this
CLI work possible, and that the same callback strips it from every player build.
A build clearing it is correct behaviour; put it **back on** afterwards. As of
2026-09-08 `HEAD` carries 1, so a plain `git checkout` of the file is right.

Finally, confirm the baked lighting survived — some builds null the adaptive
probe volume's references and the editor loses its GI silently:

```csharp
var o = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Scenes/Game/Game Baking Set.asset");
var so = new UnityEditor.SerializedObject(o); var it = so.GetIterator(); int n = 0;
while (it.NextVisible(true)) if (it.propertyType == UnityEditor.SerializedPropertyType.ObjectReference && it.objectReferenceValue != null) n++;
return "nonNullRefs=" + n;   // must be 7
```

---

## 8. Facts to know before you reason about any number

Getting these wrong produces confident, wrong answers. They are not guessable.

- **The arena is a ring.** Every position in it is really a bearing, a radius
  and a height. Problems that look like 3D geometry are usually one angle
  comparison.
- `ArenaGeometry.OrbitRadius = 13.72`. The ring is **86.2 units** around.
- **Projectile `speed` is degrees per second** about the arena axis, not units
  per second. 80 deg/s is **19.16 units/s**. Anything reasoning about spacing
  between shots must convert first.
- **Unity's `RotateAround(centre, Vector3.up, +angle)` carries +X toward −Z.**
  The textbook `(cos θ, sin θ)` parametrisation carries it toward +Z, so code
  written that way runs backwards round the ring while looking correct in every
  other respect. Write bearings as `Quaternion.Euler(0, d, 0) * Vector3.right`.
- **Two points on the orbit can be up to the diameter apart, not the radius.**
- **The player has 20 HP and no invulnerability frames.** Every hit is 1 point,
  and nothing rate-limits them, so "how many things touch you" *is* the damage.
- **The boss has one shared health pool** across all three acts — damage to its
  pods and to its hull come out of the same total. Changing a pod's health
  changes how long the *first* act lasts and nothing else.

---

## 9. Where things are

| Path | What |
| --- | --- |
| `Assets/Scripts/Gameplay/Player/` | ship, input, projectiles (`ShootScript` is every bullet in the game) |
| `Assets/Scripts/Gameplay/Boss/` | the boss: emitter, attacks, weak points, lance beam |
| `Assets/Scripts/Gameplay/Arena/` | ring geometry, bullet light pools, clouds |
| `Assets/Scripts/Gameplay/Enemies/` | ordinary enemies, health |
| `Assets/Scripts/Editor/BuildBossRig.cs` | authors the boss rig. **The design reasoning for the whole fight lives in its comments** — read it before changing any boss number |
| `Assets/Data/Enemies/*.asset` | enemy stats (the boss's `maxHealth` is authoritative here) |
| `Assets/Prefabs/Boss/`, `Assets/Prefabs/Player/` | prefabs |
| `Assets/Scenes/Game.unity` | the one gameplay scene |
| `Assets/Settings/` | HDRP pipeline asset and the four quality tiers |
| `Docs/` | published reference pages, and this file |

---

## 10. The project's own documentation, and why not to edit it

Three written pages live in `Docs/` as committed HTML. They are **the best
context on this project that exists** — better than the code for understanding
*why* something is the way it is — and you should read them before proposing
anything substantial.

| File | Size | What it is |
| --- | --- | --- |
| `rebuild-reference.html` | 204 KB | How every system works and what the rebuild changed. Start here |
| `leviathan-dossier.html` | 44 KB | The boss fight in detail: every act, every attack, the numbers and the reasoning behind them |
| `open-list.html` | 30 KB | The living list of what is left to do, grouped by whether it needs Ian, an agent, or a playthrough |

They are also **published pages**, each with a live URL on Ian's account. The
HTML in `Docs/` is a committed snapshot of a page that lives somewhere else.

**Do not edit these files.** Publishing them uses a tool only Claude Code has,
so an edit you make here either gets overwritten on the next publish or silently
diverges from what Ian actually reads. If your work makes a page wrong — you
changed a number it quotes, or closed something it lists as open — **say so in
your report** and let Ian have it updated. That is a genuinely useful thing to
flag, and it is not the same as fixing it yourself.

`open-list.html` deserves an extra note: the live page **republishes itself**
when Ian ticks an item off, so the committed snapshot is stale the moment he
uses it. Read it for *what the open questions are*, never for *what is still
open*.

The live pages are private to Ian's account, so you almost certainly cannot
fetch them; read the local snapshots instead. For reference, they are:

- Rebuild Reference — `https://claude.ai/code/artifact/2fbe095a-4519-4635-bab9-ad45852bc116`
- Leviathan Dossier — `https://claude.ai/code/artifact/ccfd2f08-33d2-42f8-a405-c97dd32d1ea7`
- Open List — `https://claude.ai/code/artifact/b3696389-86a6-4b65-9926-6288839449c9`

One more source worth more than its size suggests:
`Assets/Scripts/Editor/BuildBossRig.cs` is the boss's authoring tool, and its
comments carry the design argument for every number in the fight — what was
tried, what it felt like, and why it moved. Read it before touching any boss
value.

---

## 11. Two agents, one editor

Claude Code drives this same editor. **Only one agent should be issuing relay
commands at a time.** A recompile or a build started while the other is
mid-operation will silently corrupt its results — it will verify against a
half-loaded assembly, or build from code that is being rewritten underneath it.

If Ian has the other agent working, stay out of the relay. Reading files is
always safe.

---

## 12. Still not yours to decide

Tooling access does not change these:

- **Never commit.** Hand over the message.
- **Never guess a project number.** You can now measure instead — do that, and
  say what you measured.
- **Flag design changes rather than making them.** Anything that changes how the
  game feels — difficulty, colour, timing, what an attack does — is Ian's call.
  Bring him the measurement and a recommendation.
- **Report failures honestly.** If a build warned, say so and quote it. If you
  could not verify something, say which part is unverified. "It compiles" is not
  "it works", and neither is "it works in the editor".
