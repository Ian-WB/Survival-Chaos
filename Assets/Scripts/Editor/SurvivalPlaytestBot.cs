using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Opt-in, editor-only pilot. Decisions see delayed camera-filtered observations;
    /// diagnostics may inspect health but never grant it. This is not pixel vision.
    /// No scene objects or assets are created. Closing the window releases input.
    /// </summary>
    public sealed class SurvivalPlaytestBot : EditorWindow
    {
        private Pilot pilot;
        private string lastReport = "";
        private float reaction = .25f;
        private Vector2 scroll;

        [MenuItem("Survival Chaos/Playtest Bot")]
        public static void Open() => GetWindow<SurvivalPlaytestBot>("Playtest Bot");

        public static string StartSession()
        {
            var window = GetWindow<SurvivalPlaytestBot>("Playtest Bot");
            if (window.pilot != null) return "Already running";
            try { window.pilot = new Pilot(window.reaction); return "Started"; }
            catch (Exception e) { return e.Message; }
        }

        public static string StopSession()
        {
            var window = GetWindow<SurvivalPlaytestBot>("Playtest Bot");
            window.Stop("Manual stop");
            return window.lastReport;
        }

        public static string Status()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<SurvivalPlaytestBot>())
                if (w.pilot != null) return w.pilot.Status;
            return "Stopped";
        }

        private void OnEnable()
        {
            EditorApplication.update += Monitor;
            EditorApplication.playModeStateChanged += PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        }
        private void OnDisable()
        {
            Stop("Window closed");
            EditorApplication.update -= Monitor;
            EditorApplication.playModeStateChanged -= PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        }
        private void BeforeReload() => Stop("Assembly reload");
        private void PlayChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Stop("Play mode ended");
        }
        private void Monitor()
        {
            if (pilot == null) return;
            pilot.CheckEnd();
            if (!EditorApplication.isPlaying || pilot.EndReason != null)
                Stop(pilot.EndReason ?? "Play mode ended");
            Repaint();
        }
        private void Stop(string reason)
        {
            if (pilot == null) return;
            var old = pilot;
            pilot = null;
            // Input restoration must succeed even if the filesystem is unavailable.
            old.Release();
            try { lastReport = old.Export(reason); }
            catch (Exception e) { lastReport = "Report failed: " + e.Message; Debug.LogException(e); }
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("A delayed, state-based pilot, not human vision. It cannot judge fog, contrast or laser readability. Start during Play mode with god mode off. Close this window to return control.", MessageType.Info);
            using (new EditorGUI.DisabledScope(pilot != null))
                reaction = EditorGUILayout.Slider("Reaction delay (seconds)", reaction, .1f, .6f);
            if (pilot == null)
            {
                if (GUILayout.Button("Start in current run"))
                {
                    string result = StartSession();
                    if (result != "Started") EditorUtility.DisplayDialog("Playtest Bot", result, "OK");
                }
            }
            else
            {
                EditorGUILayout.LabelField(pilot.Status, EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Stop and export report")) Stop("Manual stop");
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.SelectableLabel(lastReport, EditorStyles.wordWrappedLabel, GUILayout.Height(100));
            EditorGUILayout.EndScrollView();
        }

        private sealed class Pilot : IGameInput
        {
            // Proposed bot parameters, not game balance values or human measurements.
            private const float ObserveEvery = .1f, CommitFor = .2f, Horizon = .8f;
            private const float SafetyMargin = .18f, AlignmentTolerance = .15f;
            private readonly float delay;
            private readonly IGameInput previous;
            private readonly Player player;
            private readonly PlayerMovement movement;
            private readonly PlayerDash dash;
            private readonly ApplyBounds band;
            private readonly Transform centre;
            private readonly Queue<Snapshot> pending = new Queue<Snapshot>();
            private readonly Dictionary<Component, Track> tracks = new Dictionary<Component, Track>();
            private readonly List<string> log = new List<string>();
            private readonly float started;
            private Snapshot known;
            private Vector2 axes, desired = Vector2.right;
            private Vector2 lastLoggedInput;
            private int stepped = -1, health, level;
            private bool dashEdge, flipEdge, released;
            private float nextObserve, nextDecision, nextFlip;
            private string reason = "Searching", cameraName = "unknown";
            private string bossPhase = "not encountered";
            private float bossStarted = -1;
            public string EndReason { get; private set; }
            public string Status => $"{reason}\nElapsed {Time.time - started:F1}s | HP {(player != null ? player.CurrentHealth : 0)} | observed {known?.items.Count ?? 0} | input {axes}";

            private enum Kind { Threat, Target, Pickup, Warning }
            private sealed class Track
            {
                public Vector3 position;
                public float time, radius, height, angle, glowSize;
                public int generation;
            }
            private struct Item
            {
                public Kind kind;
                public Vector3 position;
                public float radius, angleRate, heightRate, radialRate, priority;
                public string label;
            }
            private sealed class Snapshot
            {
                public float time;
                public List<Item> items = new List<Item>();
            }

            public Pilot(float reaction)
            {
                if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                    throw new InvalidOperationException("Enter an unpaused Play session first.");
                player = Object.FindAnyObjectByType<Player>();
                if (player == null || player.CurrentHealth <= 0 || RunOutcome.RunEnded)
                    throw new InvalidOperationException("No living player in an active run.");
                if (player.Invulnerable) throw new InvalidOperationException("Turn god mode off first.");
                movement = player.GetComponent<PlayerMovement>();
                dash = player.GetComponent<PlayerDash>();
                band = player.GetComponent<ApplyBounds>();
                centre = Read<Transform>(movement, "center");
                if (movement == null || centre == null || band == null || !band.TryGetBand(out _, out _))
                    throw new InvalidOperationException("Player movement, arena centre or height bounds missing.");
                if (Camera.main == null) throw new InvalidOperationException("No gameplay camera.");
                delay = reaction;
                started = Time.time;
                health = player.CurrentHealth;
                level = player.currentLevel;
                previous = GameInput.Source;
                Log($"START hp={health}/{player.MaxHealth} level={level} reaction={delay} observation={ObserveEvery} commitment={CommitFor} horizon={Horizon}");
                Log("LIMITS: state perception; physics occlusion only; no contrast/fog/audio perception; no offscreen memory; linear angular prediction cannot anticipate homing or bounces; pod growth warnings only, no active beam geometry or ram warning recognition; damage cause unknown. Existing run randomness is not reseeded.");
                GameInput.Source = this;
            }
            private static T Read<T>(object instance, string field)
            {
                if (instance == null) throw new InvalidOperationException("Missing owner for " + field);
                var info = instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (info == null || !(info.GetValue(instance) is T value))
                    throw new InvalidOperationException("Unsupported project field: " + field);
                return value;
            }
            private void Log(string text) => log.Add($"{Time.time - started:F3}\t{text}");
            public void Release()
            {
                axes = desired = Vector2.zero;
                dashEdge = flipEdge = false;
                released = true;
                if (ReferenceEquals(GameInput.Source, this)) GameInput.Source = previous;
            }
            public string Export(string end)
            {
                Log("END " + end);
                Log($"SUMMARY runSeconds={RunStats.Seconds:F2} botSeconds={Time.time - started:F2} kills={RunStats.EnemiesDestroyed} level={RunStats.LevelReached} hp={(player != null ? player.CurrentHealth : 0)} bossSeconds={(bossStarted < 0 ? 0 : Time.time - bossStarted):F2} phase={bossPhase}");
                foreach (string skill in RunStats.SkillOrder) Log($"UPGRADE {skill}: {RunStats.PicksOf(skill)}");
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/PlaytestBot"));
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
                File.WriteAllLines(path, log);
                return path;
            }
            public void CheckEnd()
            {
                if (EndReason != null) return;
                if (!ReferenceEquals(GameInput.Source, this)) EndReason = "Input ownership changed";
                else if (player == null) EndReason = "Player removed or scene changed";
                else if (RunOutcome.RunEnded || player.CurrentHealth <= 0) EndReason = "Run ended; final HP=" + player.CurrentHealth;
                else if (player.Invulnerable) EndReason = "God mode enabled; run invalidated";
                // Diagnostic only: these fields never enter observations or steering.
                var boss = BossEmitter.Active;
                if (boss != null && boss.Phase.ToString() != bossPhase)
                {
                    if (bossStarted < 0) bossStarted = Time.time;
                    bossPhase = boss.Phase.ToString();
                    Log("BOSS PHASE " + bossPhase);
                }
            }
            private void Step()
            {
                if (stepped == Time.frameCount || released) return;
                stepped = Time.frameCount;
                dashEdge = flipEdge = false;
                try
                {
                    if (player == null || RunOutcome.RunEnded || player.CurrentHealth <= 0)
                    { EndReason = "Run ended"; axes = Vector2.zero; return; }
                    if (player.Invulnerable) { EndReason = "God mode enabled; run invalidated"; axes = Vector2.zero; return; }
                    if (!Mathf.Approximately(Time.timeScale, 1f))
                    {
                        axes = Vector2.zero;
                        if (Time.timeScale > 0f) EndReason = "Nonstandard time scale; run invalidated";
                        return;
                    }
                    if (health != player.CurrentHealth) { Log($"HP {health}->{player.CurrentHealth}; cause unknown"); health = player.CurrentHealth; }
                    if (level != player.currentLevel) { Log($"LEVEL {level}->{player.currentLevel}"); level = player.currentLevel; }
                    if (Time.time >= nextObserve) { Observe(); nextObserve = Time.time + ObserveEvery; }
                    while (pending.Count > 0 && pending.Peek().time + delay <= Time.time) known = pending.Dequeue();
                    if (Time.time >= nextDecision) { Decide(); nextDecision = Time.time + CommitFor; }
                    axes.x = Smooth(axes.x, desired.x, Time.deltaTime);
                    axes.y = Smooth(axes.y, desired.y, Time.deltaTime);
                }
                catch (Exception e) { EndReason = "Fault: " + e; Release(); }
            }
            private static float Smooth(float current, float target, float dt)
            {
                if (target != 0f && current != 0f && Mathf.Sign(target) != Mathf.Sign(current)) current = 0f;
                return Mathf.MoveTowards(current, target, 6f * dt);
            }
            private float Angle(Vector3 p) => Mathf.Atan2(-(p.z - centre.position.z), p.x - centre.position.x) * Mathf.Rad2Deg;
            private float Radius(Vector3 p) { p -= centre.position; p.y = 0; return p.magnitude; }
            private Vector3 At(float angle, float radius, float y)
            {
                Vector3 p = centre.position + Quaternion.Euler(0, angle, 0) * Vector3.right * radius;
                p.y = y; return p;
            }
            private bool Visible(Component owner, out Bounds bounds)
            {
                bounds = new Bounds(owner.transform.position, Vector3.zero);
                Camera cam = Camera.main;
                if (cam == null) return false;
                bool found = false;
                foreach (var r in owner.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || (cam.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                    if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds);
                }
                if (!found || !GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), bounds)) return false;
                Vector3[] samples = { bounds.center, bounds.center + Vector3.up * bounds.extents.y,
                    bounds.center - Vector3.up * bounds.extents.y, bounds.center + cam.transform.right * bounds.extents.x,
                    bounds.center - cam.transform.right * bounds.extents.x };
                foreach (Vector3 sample in samples)
                {
                    Vector3 vp = cam.WorldToViewportPoint(sample);
                    if (vp.z < cam.nearClipPlane || vp.z > cam.farClipPlane || vp.x < 0 || vp.x > 1 || vp.y < 0 || vp.y > 1) continue;
                    Vector3 ray = sample - cam.transform.position;
                    bool blocked = false;
                    foreach (var hit in Physics.RaycastAll(cam.transform.position, ray.normalized, ray.magnitude, cam.cullingMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform.IsChildOf(owner.transform) || owner.transform.IsChildOf(hit.transform) || hit.transform.IsChildOf(player.transform)) continue;
                        blocked = true; break;
                    }
                    if (!blocked) return true;
                }
                return false;
            }
            private void Observe()
            {
                var snapshot = new Snapshot { time = Time.time };
                var seen = new HashSet<Component>();
                foreach (var shot in ShootScript.Live)
                    if (shot != null && shot.isActiveAndEnabled && shot.CompareTag("enemy_Shoot")) Add(shot, Kind.Threat, 0, shot.Volley, snapshot, seen);
                foreach (var enemy in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude)) Add(enemy, Kind.Target, 0, 0, snapshot, seen);
                foreach (var pod in Object.FindObjectsByType<BossWeakPoint>(FindObjectsInactive.Exclude))
                    if (!pod.Destroyed) Add(pod, Kind.Target, 2, 0, snapshot, seen);
                foreach (var pickup in Object.FindObjectsByType<Pickup>(FindObjectsInactive.Exclude))
                {
                    float priority = pickup.HealAmount > 0 ? (health < player.MaxHealth * .5f ? 12 : -1)
                        : pickup.Skill is ShotUpgradeSkill ? 8 : pickup.Skill is AttackSpeedSkill ? 7
                        : pickup.Skill is MaxHealthSkill ? 6 : 5;
                    if (priority > 0) Add(pickup, Kind.Pickup, priority, 0, snapshot, seen);
                }
                // Forget on absence. Volley also distinguishes a pooled shot reused between observations.
                var remove = new List<Component>();
                foreach (var key in tracks.Keys) if (!seen.Contains(key)) remove.Add(key);
                foreach (var key in remove) tracks.Remove(key);
                pending.Enqueue(snapshot);
                var cam = Camera.main;
                string description = cam != null ? $"{CameraPresetSwitcher.Current.Name} fov={cam.fieldOfView:F2} radius={Radius(cam.transform.position):F2}" : "none";
                if (description != cameraName) { cameraName = description; Log("CAMERA " + description); }
            }
            private void Add(Component obj, Kind kind, float priority, int generation, Snapshot s, HashSet<Component> seen)
            {
                if (!Visible(obj, out Bounds bounds)) return;
                seen.Add(obj);
                Vector3 p = bounds.center;
                float angle = Angle(p), radius = Radius(p);
                var item = new Item { kind = kind, position = p, radius = bounds.extents.magnitude,
                    priority = priority, label = obj.name };
                tracks.TryGetValue(obj, out Track old);
                bool continuous = old != null && old.generation == generation && s.time - old.time <= ObserveEvery * 2.5f;
                if (continuous)
                {
                    float dt = s.time - old.time;
                    item.angleRate = Mathf.DeltaAngle(old.angle, angle) / dt;
                    item.heightRate = (p.y - old.height) / dt;
                    item.radialRate = (radius - old.radius) / dt;
                }
                var track = new Track { position = p, angle = angle, height = p.y, radius = radius, time = s.time, generation = generation };
                if (obj is BossWeakPoint pod)
                {
                    Transform glow = Read<Transform>(pod, "glow");
                    track.glowSize = glow.lossyScale.magnitude;
                    // Observe visible growth, not the emitter's schedule or charge progress.
                    if (continuous && track.glowSize > old.glowSize * 1.015f)
                    {
                        var warning = item; warning.kind = Kind.Warning; warning.radius = 0;
                        s.items.Add(warning);
                    }
                }
                tracks[obj] = track;
                s.items.Add(item);
            }
            private Vector3 Predict(Item item, float dt) => At(Angle(item.position) + item.angleRate * dt,
                Mathf.Max(0, Radius(item.position) + item.radialRate * dt), item.position.y + item.heightRate * dt);
            private void Decide()
            {
                if (known == null || !band.TryGetBand(out float floor, out float ceiling)) return;
                Vector3 origin = player.transform.position;
                float age = Time.time - known.time;
                Item? goal = null;
                float bestGoal = float.NegativeInfinity;
                foreach (var item in known.items)
                {
                    if (item.kind != Kind.Target && item.kind != Kind.Pickup) continue;
                    float distance = Mathf.Abs(Mathf.DeltaAngle(Angle(origin), Angle(item.position))) * Mathf.Deg2Rad * ArenaGeometry.LaneRadius + Mathf.Abs(item.position.y - origin.y);
                    float score = item.priority * 3 - distance;
                    if (score > bestGoal) { bestGoal = score; goal = item; }
                }
                float orbit = Read<float>(movement, "orbitSpeed"), climb = Read<float>(movement, "climbSpeed");
                var multiplier = typeof(PlayerMovement).GetField("speedMultiplier", BindingFlags.Static | BindingFlags.NonPublic);
                if (multiplier == null) throw new InvalidOperationException("Movement multiplier unavailable");
                orbit *= (float)multiplier.GetValue(null); climb *= (float)multiplier.GetValue(null);
                Vector2 best = desired;
                float bestScore = float.PositiveInfinity, selectedDanger = 0;
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
                {
                    Vector2 candidate = new Vector2(x, y), ramp = axes;
                    float angle = Angle(origin), height = origin.y, danger = 0;
                    Vector3 end = origin;
                    for (float t = ObserveEvery; t <= Horizon + .001f; t += ObserveEvery)
                    {
                        ramp.x = Smooth(ramp.x, x, ObserveEvery); ramp.y = Smooth(ramp.y, y, ObserveEvery);
                        angle -= ramp.x * orbit / Mathf.Max(.01f, ArenaGeometry.LaneRadius) * Mathf.Rad2Deg * ObserveEvery;
                        height = Mathf.Clamp(height + ramp.y * climb * ObserveEvery, floor, ceiling);
                        end = At(angle, Radius(origin), height);
                        foreach (var item in known.items)
                        {
                            if (item.kind == Kind.Pickup) continue;
                            if (item.kind == Kind.Warning)
                            { if (Mathf.Abs(height - item.position.y) < .7f) danger += 20; continue; }
                            float separation = Vector3.Distance(end, Predict(item, age + t)) - item.radius - SafetyMargin;
                            if (separation < 0) danger += 100 * (Horizon + .2f - t);
                            else if (separation < .8f) danger += (.8f - separation) * 2;
                        }
                    }
                    float score = danger + (candidate - desired).sqrMagnitude * .15f;
                    if (goal.HasValue)
                    {
                        var g = goal.Value;
                        score += Mathf.Max(0, Mathf.Abs(end.y - g.position.y) - AlignmentTolerance) * 2;
                        if (g.kind == Kind.Pickup)
                            score += Mathf.Abs(Mathf.DeltaAngle(Angle(end), Angle(g.position))) * Mathf.Deg2Rad * ArenaGeometry.LaneRadius;
                        else score += x == 0 ? .5f : 0;
                    }
                    else score += x == 1 && y == 0 ? 0 : 1;
                    if (score < bestScore) { bestScore = score; best = candidate; selectedDanger = danger; }
                }
                desired = best;
                // Dash only along the chosen escape; ordinary gameplay enforces its cooldown.
                dashEdge = selectedDanger > 25 && best.sqrMagnitude > 0 && axes.sqrMagnitude > .25f
                    && Vector2.Dot(axes.normalized, best.normalized) > .9f && dash != null && dash.ReadyFraction >= 1;
                if (goal.HasValue && goal.Value.kind == Kind.Target && Time.time >= nextFlip)
                {
                    float delta = Mathf.DeltaAngle(Angle(origin), Angle(goal.Value.position));
                    GameObject prefab = Read<GameObject>(player, player.DirectionFlipped ? "shootPrefab" : "shootPrefab1");
                    var shot = prefab.GetComponent<ShootScript>();
                    if (Mathf.Abs(delta) > 1 && shot != null && Mathf.Sign(shot.Speed) != Mathf.Sign(delta))
                    { flipEdge = true; nextFlip = Time.time + .5f; }
                }
                string next = selectedDanger > 0 ? "Evading observed threat"
                    : goal.HasValue ? (goal.Value.kind == Kind.Pickup ? "Collecting " : "Aligning with ") + goal.Value.label : "Searching";
                if (next != reason || desired != lastLoggedInput || dashEdge || flipEdge) Log($"{next}; input={desired}; dash={dashEdge}; flip={flipEdge}");
                lastLoggedInput = desired;
                reason = next;
            }
            public float Horizontal { get { Step(); return axes.x; } }
            public float Vertical { get { Step(); return axes.y; } }
            public bool DashPressed { get { Step(); return dashEdge; } }
            public bool ToggleDirectionReleased { get { Step(); return flipEdge; } }
            public bool PausePressed => previous.PausePressed;
            public bool DebugLevelUpPressed => false;
            public bool DebugOverlayTogglePressed => previous.DebugOverlayTogglePressed;
            public bool DebugCopyReportPressed => previous.DebugCopyReportPressed;
            public bool DebugMenuTogglePressed => false;
            public int DebugShortcutPressed => 0;
        }
    }
}
