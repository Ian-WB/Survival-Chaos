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
            private const float ObserveEvery = .1f, CommitFor = .2f, Horizon = 1.2f, PredictStep = .02f;
            private const float SafetyMargin = .18f, AlignmentTolerance = .15f;
            // Enemy rounds cross the ring at 80 degrees a second, about 26 units a
            // second at the lane. Between the reaction delay, the sampling interval
            // and the second sighting a velocity needs, one has flown 9-12 units
            // before the pilot could answer it - so a gun that close is dodged by
            // leaving its height, the way a person watches the ship rather than
            // the round. 45 degrees is about 14.7 units of ring.
            // An emplacement has 80 health, so it falls to sustained fire or not at
            // all. At priority 2 a nearby obstacle beat it on distance and the pilot
            // spent a 137-second fight drifting between the two. 10 outranks every
            // pickup but a heal when low; a current target is kept unless something
            // else beats it by GoalStickiness.
            private const float EmplacementPriority = 10f, GoalStickiness = 6f;
            // Going round starts once the ship is a degree behind the emplacement's
            // face and ends once it is 8 in front, so it does not flicker at the edge.
            private const float BehindDegrees = 1f, FrontDegrees = 8f;
            private const float PointBlankDegrees = 45f, FireHalfHeight = .35f, LineOfFireCost = 25f;
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
            private readonly Queue<string> recentDecisions = new Queue<string>();
            private readonly Dictionary<string, int> upgradeCounts = new Dictionary<string, int>();
            private readonly float started;
            private Snapshot known;
            private Vector2 axes, desired = Vector2.right;
            private Vector2 lastLoggedInput;
            private int stepped = -1, health, level;
            private bool dashEdge, flipEdge, released;
            private float nextObserve, nextDecision, nextFlip, nextAim;
            // The target being worked on, kept so a nearer distraction has to beat
            // it by a margin rather than by a hair, and whether the pilot is on its
            // way round the ring to an emplacement's front.
            private Object goalSource;
            private bool goingRound;
            private readonly Dictionary<string, int> emplacementHealth = new Dictionary<string, int>();
            private string reason = "Searching", cameraName = "unknown";
            private string bossPhase = "not encountered";
            private float bossStarted = -1;
            private int injuries, dashRequests;
            private float lastFailureDump = -10;
            private string lastPlan = "No plan yet";
            private readonly MaterialPropertyBlock observedBlock = new MaterialPropertyBlock();
            public string EndReason { get; private set; }
            public string Status => $"{reason}\nElapsed {Time.time - started:F1}s | HP {(player != null ? player.CurrentHealth : 0)} | observed {known?.items.Count ?? 0} | input {axes}";

            private enum Kind { Threat, Target, Pickup, Warning, RamWarning, Beam, LineOfFire }
            private sealed class Track
            {
                public Vector3 position;
                public float time, radius, height, angle, glowSize;
                public int generation;
                public float emission, lastFlash = -10, ramUntil;
            }
            private struct Item
            {
                public Kind kind;
                public Vector3 position;
                public float radius, angleRate, heightRate, radialRate, priority;
                public string label;
                public Object source;
                public Vector3 extents, end;
                // Height chasing, for enemies that follow the player's height:
                // the response of that chase and the distance it starts inside.
                public float homeRate, chaseRadius;
                // For a line of fire: which way round the ring its rounds fly, +1
                // or -1 in this pilot's angle convention.
                public float fireDirection;
                // The boss's hull, which is armoured while any emplacement stands.
                public bool hull;
                // For an emplacement: which side of the hull it sticks out of, +1 or
                // -1 in this pilot's angle convention. The hull swallows rounds, so
                // an emplacement can only be shot from that side.
                public float side;
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
                Log("LIMITS: state perception; physics occlusion only; no contrast/fog/audio perception; no offscreen memory; prediction updates observed turns/bounces but cannot foresee them; hull flashes are ambiguous and may be damage, not ram charge; damage cause unknown. Existing run randomness is not reseeded.");
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
                Log($"SUMMARY runSeconds={RunStats.Seconds:F2} botSeconds={Time.time - started:F2} kills={RunStats.EnemiesDestroyed} level={RunStats.LevelReached} hp={(player != null ? player.CurrentHealth : 0)} bossSeconds={(bossStarted < 0 ? 0 : Time.time - bossStarted):F2} phase={bossPhase} damageObserved={injuries} dashRequests={dashRequests}");
                foreach (string skill in RunStats.SkillOrder) Log($"UPGRADE {skill}: {RunStats.PicksOf(skill)}");
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/PlaytestBot"));
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
                File.WriteAllLines(path, log);
                return path;
            }
            public void CheckEnd()
            {
                // Before the early return: the run can end inside a frame's input read,
                // and the fatal hit went unlogged when it did.
                RecordHealth();
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
                if (boss != null)
                {
                    foreach (var pod in boss.GetComponentsInChildren<BossWeakPoint>(true))
                    {
                        int hp = pod.Destroyed ? 0 : pod.CurrentHealth;
                        if (emplacementHealth.TryGetValue(pod.Label, out int last) && (last == hp || (hp > 0 && last - hp < 10))) continue;
                        emplacementHealth[pod.Label] = hp;
                        Log(hp == 0 ? $"EMPLACEMENT {pod.Label} destroyed" : $"EMPLACEMENT {pod.Label} hp={hp}");
                    }
                }
            }
            private void RecordHealth()
            {
                if (player == null || health == player.CurrentHealth) return;
                int current = player.CurrentHealth;
                Log($"HP {health}->{current}; cause unknown; at={player.transform.position}; input={axes}; dashActive={(dash != null && dash.Invincible)}");
                if (current < health)
                {
                    injuries += health - current;
                    if (Time.time - lastFailureDump >= .5f || current <= 0)
                    {
                        lastFailureDump = Time.time;
                        Log("FAILURE CONTEXT BEGIN (observations only, not damage attribution)");
                        foreach (string decision in recentDecisions) Log("HISTORY " + decision);
                        Log("LAST PLAN " + lastPlan);
                        if (known != null)
                        {
                            float age = Time.time - known.time;
                            Log($"OBSERVATION age={age:F3}s count={known.items.Count}");
                            int count = 0;
                            var nearby = new List<Item>(known.items);
                            nearby.Sort((a, b) => Vector3.SqrMagnitude(a.position-player.transform.position).CompareTo(Vector3.SqrMagnitude(b.position-player.transform.position)));
                            foreach (var item in nearby)
                            {
                                if (item.kind == Kind.Pickup) continue;
                                Log($"SEEN {item.kind} {item.label} at={item.position} predictedNow={Predict(item,age)} angularRate={item.angleRate:F2} verticalRate={item.heightRate:F2}"
                                    + (item.homeRate > 0 ? $" chasesHeight={item.homeRate:F2}" : "") + (item.kind == Kind.LineOfFire ? $" fires={item.fireDirection:+0;-0}" : ""));
                                if (++count >= 12) break;
                            }
                        }
                        Log("FAILURE CONTEXT END");
                    }
                }
                health = current;
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
                    RecordHealth();
                    foreach (string skill in RunStats.SkillOrder)
                    {
                        int count = RunStats.PicksOf(skill);
                        if (!upgradeCounts.TryGetValue(skill, out int before) || before != count)
                        { Log($"COLLECTED {skill} count={count}"); upgradeCounts[skill] = count; }
                    }
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
            /// <summary>
            /// Report-only diagnosis of a shot at an emplacement: where the pilot is
            /// against where the pod really is, which way the guns point, and what
            /// on the ring ahead at the pilot's height would take the rounds first.
            /// Read from the game, never fed back into a decision. The hull, ships,
            /// obstacles and wreckage all swallow player rounds.
            /// </summary>
            private void LogAim(Item goal, Vector3 origin)
            {
                Vector3 truth = goal.source is Component pod && pod != null ? pod.transform.position : goal.position;
                GameObject prefab = Read<GameObject>(player, player.DirectionFlipped ? "shootPrefab" : "shootPrefab1");
                var shot = prefab != null ? prefab.GetComponent<ShootScript>() : null;
                float fire = shot != null ? Mathf.Sign(shot.Speed) : 0f;
                float gap = Mathf.DeltaAngle(Angle(origin), Angle(truth));
                float inFront = Mathf.DeltaAngle(Angle(truth), Angle(origin)) * goal.side;
                var blockers = new List<string>();
                if (Mathf.Sign(gap) == fire)
                {
                    foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
                    {
                        if (!c.enabled || (c.GetComponent<Enemy>() == null && c.GetComponent<BossWreckage>() == null)) continue;
                        Bounds b = c.bounds;
                        if (b.min.y > origin.y + .2f || b.max.y < origin.y - .2f) continue;
                        if (Mathf.Abs(Radius(b.center) - ArenaGeometry.LaneRadius) > Mathf.Max(b.extents.x, b.extents.z) + .3f) continue;
                        float ahead = Mathf.DeltaAngle(Angle(origin), Angle(b.center)) * fire;
                        float halfSpan = Mathf.Max(b.extents.x, b.extents.z) / ArenaGeometry.LaneRadius * Mathf.Rad2Deg;
                        if (ahead + halfSpan > 0 && ahead - halfSpan < Mathf.Abs(gap)) blockers.Add(c.name);
                    }
                }
                Log($"AIM {goal.label} heightOff={origin.y - truth.y:+0.00;-0.00} seenHeightOff={origin.y - goal.position.y:+0.00;-0.00} angleTo={gap:F1} "
                    + $"firing={(fire == 0 ? "unknown" : Mathf.Sign(gap) == fire ? "toward" : "away")} inFront={inFront:F1} side={goal.side:+0;-0} "
                    + $"inTheWay={(blockers.Count == 0 ? "nothing" : string.Join(",", blockers))}");
            }
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
                    if (PointVisible(owner, sample)) return true;
                return false;
            }
            private bool PointVisible(Component owner, Vector3 sample)
            {
                Camera cam = Camera.main;
                if (cam == null) return false;
                {
                    Vector3 vp = cam.WorldToViewportPoint(sample);
                    if (vp.z < cam.nearClipPlane || vp.z > cam.farClipPlane || vp.x < 0 || vp.x > 1 || vp.y < 0 || vp.y > 1) return false;
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
                foreach (var enemy in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude))
                    if (enemy.GetComponent<BossEmitter>() == null) Add(enemy, Kind.Target, 0, 0, snapshot, seen);
                foreach (var boss in Object.FindObjectsByType<BossEmitter>(FindObjectsInactive.Exclude)) Add(boss, Kind.Target, 1, 0, snapshot, seen);
                foreach (var pod in Object.FindObjectsByType<BossWeakPoint>(FindObjectsInactive.Exclude))
                    if (!pod.Destroyed) Add(pod, Kind.Target, EmplacementPriority, 0, snapshot, seen);
                // With no emplacement in sight the hull is the fight. At priority 1 a
                // nearby obstacle beat it on distance, and in the first full run the
                // pilot spent the Exposed phase's last seconds aiming at Enemy 3.
                bool emplacementSeen = false;
                foreach (var item in snapshot.items) if (item.side != 0) emplacementSeen = true;
                if (!emplacementSeen)
                    for (int i = 0; i < snapshot.items.Count; i++)
                        if (snapshot.items[i].hull) { var hull = snapshot.items[i]; hull.priority = EmplacementPriority; snapshot.items[i] = hull; }
                foreach (var beam in Object.FindObjectsByType<BossLanceBeam>(FindObjectsInactive.Exclude)) ObserveBeam(beam, snapshot);
                // Plates shed from destroyed emplacements in the second act. They hang
                // still in the lane and hurt on contact, and are none of the kinds above.
                foreach (var plate in Object.FindObjectsByType<BossWreckage>(FindObjectsInactive.Exclude))
                    Add(plate, Kind.Threat, 0, 0, snapshot, seen);
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
            /// <summary>
            /// Adds the stretch of ring in front of a shooting enemy's guns. It fires
            /// the way it faces, which is the way it is travelling, so which way its
            /// rounds go is as visible as the ship; each gun's own height is the line.
            /// </summary>
            private void AddLinesOfFire(Enemy_1 gun, EnemyMovement movement, Item ship, Snapshot s)
            {
                var pivots = new[] { ReadOptional<Transform>(gun, "shootPivot"), ReadOptional<Transform>(gun, "shootPivot_1") };
                GameObject prefab = ReadOptional<GameObject>(gun, movement != null && movement.TravellingLeft ? "shootPrefab" : "shootPrefab1");
                var round = prefab != null ? prefab.GetComponent<ShootScript>() : null;
                if (round == null || round.Speed == 0f) return;
                float added = float.NaN;
                foreach (var pivot in pivots)
                {
                    if (pivot == null || Mathf.Abs(pivot.position.y - added) < .05f) continue;
                    added = pivot.position.y;
                    var line = ship;
                    line.kind = Kind.LineOfFire;
                    line.position = pivot.position;
                    line.fireDirection = Mathf.Sign(round.Speed);
                    line.label = ship.label + " line of fire";
                    s.items.Add(line);
                }
            }
            private static T ReadOptional<T>(object instance, string field) where T : class
            {
                var info = instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                return info?.GetValue(instance) as T;
            }
            private void ObserveBeam(BossLanceBeam beam, Snapshot snapshot)
            {
                var renderer = beam.GetComponent<MeshRenderer>();
                var filter = beam.GetComponent<MeshFilter>();
                if (renderer == null || !renderer.enabled || filter == null || filter.sharedMesh == null
                    || Camera.main == null || (Camera.main.cullingMask & (1 << beam.gameObject.layer)) == 0) return;
                // Read the drawn ribbons, not the attack clock, hit-test or unseen full arc.
                Vector3[] v = filter.sharedMesh.vertices;
                int ribbon = v.Length / 2;
                for (int i = 0; i + 3 < ribbon; i += 2)
                {
                    Vector3 a = beam.transform.TransformPoint((v[i] + v[i+1]) * .5f);
                    Vector3 b = beam.transform.TransformPoint((v[i+2] + v[i+3]) * .5f);
                    if (!PointVisible(beam, a) || !PointVisible(beam, b)) continue;
                    float width = beam.transform.TransformVector(v[ribbon+i] - v[ribbon+i+1]).magnitude * .5f;
                    snapshot.items.Add(new Item { kind=Kind.Beam, position=a, end=b, radius=width, label="Visible lance segment" });
                }
            }
            private float VisibleEmission(Component obj)
            {
                float intensity = 0;
                foreach (var r in obj.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || !PointVisible(obj,r.bounds.center)) continue;
                    // Emplacements have their own flash. Only sample the hull's appearance.
                    if (r.GetComponentInParent<BossWeakPoint>() != null) continue;
                    var materials = r.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        r.GetPropertyBlock(observedBlock, i);
                        Color c = observedBlock.HasColor("_EmissiveColor") ? observedBlock.GetColor("_EmissiveColor")
                            : materials[i] != null && materials[i].HasProperty("_EmissiveColor") ? materials[i].GetColor("_EmissiveColor") : Color.black;
                        intensity = Mathf.Max(intensity, c.maxColorComponent);
                    }
                }
                return intensity;
            }
            private void Add(Component obj, Kind kind, float priority, int generation, Snapshot s, HashSet<Component> seen)
            {
                if (!Visible(obj, out Bounds bounds)) return;
                seen.Add(obj);
                Vector3 p = bounds.center;
                float angle = Angle(p), radius = Radius(p);
                var item = new Item { kind = kind, position = p, radius = bounds.extents.magnitude,
                    priority = priority, label = obj.name, extents = bounds.extents, source = obj };
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
                if (obj is Enemy && obj.GetComponent<BossEmitter>() == null)
                {
                    // What a practised player knows about each kind by sight: which
                    // ones come after your height, and how hard. Read from the enemy's
                    // own settings, like the other identity labels this pilot uses.
                    var chase = obj.GetComponentInChildren<EnemyMovement>();
                    if (chase != null)
                    {
                        item.homeRate = Mathf.Max(0f, Read<float>(chase, "speed"));
                        item.chaseRadius = ArenaGeometry.LaneRadius * Mathf.Max(0f, Read<float>(chase, "chaseRadiusFraction"));
                    }
                    var gun = obj.GetComponentInChildren<Enemy_1>();
                    if (gun != null) AddLinesOfFire(gun, chase, item, s);
                }
                if (obj.GetComponent<BossEmitter>() != null) item.hull = true;
                if (obj is BossWeakPoint pod)
                {
                    var owner = pod.GetComponentInParent<BossEmitter>();
                    if (owner != null)
                        item.side = Mathf.Sign(Mathf.DeltaAngle(Angle(owner.transform.position), angle));
                    Transform glow = Read<Transform>(pod, "glow");
                    track.glowSize = glow.lossyScale.magnitude;
                    // Observe visible growth, not the emitter's schedule or charge progress.
                    if (continuous && track.glowSize > old.glowSize * 1.015f && pod.Label == "Prow")
                    {
                        var warning = item; warning.kind = Kind.Warning; warning.radius = 0;
                        s.items.Add(warning);
                    }
                }
                if (obj.GetComponent<BossEmitter>() != null)
                {
                    track.emission = VisibleEmission(obj);
                    track.lastFlash = continuous ? old.lastFlash : -10;
                    track.ramUntil = continuous ? old.ramUntil : 0;
                    if (continuous && track.emission > old.emission + .2f)
                    {
                        if (s.time - track.lastFlash < .6f) track.ramUntil = s.time + .75f;
                        track.lastFlash = s.time;
                    }
                    if (s.time < track.ramUntil)
                    {
                        var warning = item; warning.kind = Kind.RamWarning;
                        warning.label = "Possible ram: repeated visible hull flashes";
                        s.items.Add(warning);
                    }
                }
                tracks[obj] = track;
                s.items.Add(item);
            }
            private Vector3 Predict(Item item, float dt) => At(Angle(item.position) + item.angleRate * dt,
                Mathf.Max(0, Radius(item.position) + item.radialRate * dt), item.position.y + item.heightRate * dt);
            // Pure model; splits the step at dash expiry so a partial final dash step
            // cannot grant extra boost. Uses the same angular sign and fixed heading as gameplay.
            private static Vector3[] PredictRoute(Vector3 origin, Vector3 centre, Vector2 ramp, Vector2 request,
                float orbit, float climb, float lane, float floor, float ceiling, Vector2 heading,
                float dashRemaining, float boost, float climbBoost)
            {
                int steps = Mathf.RoundToInt(Horizon / PredictStep);
                var points = new Vector3[steps];
                Vector3 offset = origin - centre; offset.y = 0;
                float radius = offset.magnitude;
                float angle = Mathf.Atan2(-offset.z,offset.x) * Mathf.Rad2Deg, height = origin.y;
                for (int i=0; i<steps; i++)
                {
                    ramp.x = Smooth(ramp.x,request.x,PredictStep);
                    ramp.y = Smooth(ramp.y,request.y,PredictStep);
                    float burst = Mathf.Clamp(dashRemaining - i*PredictStep,0,PredictStep);
                    float normal = PredictStep-burst;
                    angle -= (heading.x * Mathf.Max(1,boost) * burst + ramp.x * normal) * orbit / Mathf.Max(.01f,lane) * Mathf.Rad2Deg;
                    height = Mathf.Clamp(height + heading.y * climb * Mathf.Max(1,climbBoost) * burst,floor,ceiling);
                    height = Mathf.Clamp(height + ramp.y * climb * normal,floor,ceiling);
                    Vector3 point = centre + Quaternion.Euler(0,angle,0)*Vector3.right*radius;
                    point.y=height; points[i]=point;
                }
                return points;
            }
            /// <summary>
            /// How far a route misses a target height. Scored on the route's end
            /// alone, a climb held for the whole horizon carries the ship about 5.6
            /// units, so for a target in the middle of the band going there
            /// overshot it by as much as staying put missed it: on 22 Sep the pilot
            /// sat 2.86 above the Prow for 40 seconds, shooting over it into the
            /// Crown. The pilot replans every decision, so passing through the
            /// height is what counts; the end still weighs a little, so that once
            /// there, holding the height beats flying through it.
            /// </summary>
            private static float HeightMiss(Vector3[] route, float target)
            {
                float closest = float.PositiveInfinity;
                foreach (var point in route) closest = Mathf.Min(closest, Mathf.Abs(point.y - target));
                return closest + .25f * Mathf.Abs(route[route.Length - 1].y - target);
            }
            /// <summary>
            /// Where an enemy that chases the player's height will be, <paramref name="elapsed"/>
            /// seconds after it was seen at <paramref name="observed"/>: the same exponential
            /// approach EnemyMovement runs, toward the height the player will be at.
            /// </summary>
            private static float HomedHeight(float observed, float target, float rate, float elapsed)
            {
                return target + (observed - target) * Mathf.Exp(-rate * elapsed);
            }
            /// <summary>
            /// Whether <paramref name="pointAngle"/>, at <paramref name="pointHeight"/>, sits in
            /// the stretch of ring a gun at <paramref name="gunAngle"/> fires down before
            /// its rounds can be seen and answered.
            /// </summary>
            private static bool InLineOfFire(float gunAngle, float gunHeight, float direction, float pointAngle, float pointHeight)
            {
                float ahead = Mathf.DeltaAngle(gunAngle, pointAngle) * direction;
                return ahead > 0f && ahead < PointBlankDegrees && Mathf.Abs(pointHeight - gunHeight) < FireHalfHeight;
            }
            private static float SegmentDistance(Vector3 point, Vector3 a, Vector3 b)
            {
                Vector3 edge=b-a;
                return Vector3.Distance(point,a+edge*Mathf.Clamp01(Vector3.Dot(point-a,edge)/Mathf.Max(1e-8f,edge.sqrMagnitude)));
            }
            private float RouteDanger(Vector3[] route, float age, float invincibleFor, out float landing)
            {
                float danger=0; landing=0;
                for(int i=0;i<route.Length;i++)
                {
                    float t=(i+1)*PredictStep;
                    if(t<invincibleFor) continue;
                    foreach(var item in known.items)
                    {
                        if(item.kind==Kind.Pickup) continue;
                        float cost=0;
                        if(item.kind==Kind.Warning)
                            cost=Mathf.Abs(route[i].y-item.position.y)<.7f ? 12 : 0;
                        else if(item.kind==Kind.Beam)
                            cost=SegmentDistance(route[i],item.position,item.end)<item.radius+SafetyMargin ? 100 : 0;
                        else if(item.kind==Kind.LineOfFire)
                        {
                            Vector3 gun=Predict(item,age+t);
                            if(item.homeRate>0 && Vector3.Distance(gun,route[i])<=item.chaseRadius)
                                gun.y=HomedHeight(item.position.y,route[i].y,item.homeRate,age+t);
                            cost=InLineOfFire(Angle(gun),gun.y,item.fireDirection,Angle(route[i]),route[i].y) ? LineOfFireCost : 0;
                        }
                        else
                        {
                            // A world-space box is still approximate, but a tall hull no longer
                            // becomes an enormous sphere occupying the whole surrounding arena.
                            Vector3 predicted=Predict(item,age+t);
                            // A height chaser does not hold the height it was seen at: it
                            // closes on the player's, so a straight-line forecast walks the
                            // pilot into it. Enemy 2 closes most of a gap in half a second.
                            if(item.homeRate>0 && Vector3.Distance(predicted,route[i])<=item.chaseRadius)
                                predicted.y=HomedHeight(item.position.y,route[i].y,item.homeRate,age+t);
                            Vector3 delta=route[i]-predicted;
                            Vector3 outside=new Vector3(Mathf.Abs(delta.x),Mathf.Abs(delta.y),Mathf.Abs(delta.z))-item.extents;
                            float separation=new Vector3(Mathf.Max(0,outside.x),Mathf.Max(0,outside.y),Mathf.Max(0,outside.z)).magnitude-SafetyMargin;
                            cost=separation<=0 ? 100 : separation<.8f ? (.8f-separation)*2 : 0;
                            if(item.kind==Kind.RamWarning) cost*=1.5f;
                        }
                        danger+=cost*PredictStep;
                        if(t>=invincibleFor && t<=invincibleFor+.16f) landing+=cost*PredictStep;
                    }
                }
                return danger;
            }
            private void Decide()
            {
                if (known == null || !band.TryGetBand(out float floor, out float ceiling)) return;
                Vector3 origin = player.transform.position;
                float age = Time.time - known.time;
                Item? goal = null;
                float bestGoal = float.NegativeInfinity;
                // The hull takes no damage while an emplacement stands, and the orange
                // emplacements say so plainly: while one is in sight, the hull is not a
                // target. It was, and with the remaining emplacements a few units off
                // the pilot's height the nearer hull won, so after the first one fell
                // it spent the fight shooting armour.
                bool emplacementInSight = false;
                foreach (var item in known.items) if (item.kind == Kind.Target && item.side != 0) emplacementInSight = true;
                foreach (var item in known.items)
                {
                    if (item.kind != Kind.Target && item.kind != Kind.Pickup) continue;
                    if (item.hull && emplacementInSight) continue;
                    float distance = Mathf.Abs(Mathf.DeltaAngle(Angle(origin), Angle(item.position))) * Mathf.Deg2Rad * ArenaGeometry.LaneRadius + Mathf.Abs(item.position.y - origin.y);
                    float score = item.priority * 3 - distance + (item.source != null && ReferenceEquals(item.source, goalSource) ? GoalStickiness : 0);
                    if (score > bestGoal) { bestGoal = score; goal = item; }
                }
                float orbit = Read<float>(movement, "orbitSpeed"), climb = Read<float>(movement, "climbSpeed");
                var multiplier = typeof(PlayerMovement).GetField("speedMultiplier", BindingFlags.Static | BindingFlags.NonPublic);
                if (multiplier == null) throw new InvalidOperationException("Movement multiplier unavailable");
                orbit *= (float)multiplier.GetValue(null); climb *= (float)multiplier.GetValue(null);
                Vector2 best = desired;
                float bestScore = float.PositiveInfinity, selectedDanger = 0, selectedLanding=0;
                float bestWalkDanger=float.PositiveInfinity;
                bool selectedDash=false;
                // An emplacement faces one way along the ring. From behind the hull
                // every round hits armour, and the hull spans the whole band, so the
                // way to the front is the long way round.
                float wrongSide=0;
                if(goal.HasValue && goal.Value.kind==Kind.Target && goal.Value.side!=0)
                {
                    float inFront=Mathf.DeltaAngle(Angle(goal.Value.position),Angle(origin))*goal.Value.side;
                    goingRound = goingRound && ReferenceEquals(goal.Value.source,goalSource) ? inFront<FrontDegrees : inFront< -BehindDegrees;
                    if(goingRound) wrongSide=goal.Value.side;
                }
                else goingRound=false;
                goalSource = goal.HasValue ? goal.Value.source : null;
                Vector3 selectedEnd=origin;
                float duration=dash!=null ? Read<float>(dash,"duration") : 0;
                float boost=dash!=null ? Read<float>(dash,"speedMultiplier") : 1;
                float climbBoost=dash!=null ? Read<float>(dash,"climbMultiplier") : 1;
                float remaining=0;
                Vector2 committed=Vector2.zero;
                if(dash!=null && dash.Invincible)
                {
                    remaining=Mathf.Max(0,Read<float>(Read<DashCycle>(dash,"cycle"),"startedAt")+duration-Time.time);
                    committed=new Vector2(PlayerMovement.EffectiveHorizontal,PlayerMovement.EffectiveVertical);
                }
                for(int mode=0;mode<2;mode++)
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
                {
                    bool useDash=mode==1;
                    if(useDash && (dash==null || dash.ReadyFraction<1 || remaining>0 || bestWalkDanger<1)) continue;
                    Vector2 candidate = new Vector2(x, y);
                    // PlayerDash reads the smoothed axes on the press frame, not the
                    // requested digital direction. Predict precisely that captured heading.
                    Vector2 pressAxes=new Vector2(Smooth(axes.x,x,Time.deltaTime),Smooth(axes.y,y,Time.deltaTime));
                    Vector2 heading=useDash ? (pressAxes.sqrMagnitude>=.01f ? pressAxes.normalized : new Vector2(player.DirectionFlipped ? -1 : 1,0)) : committed;
                    float burst=useDash ? duration : remaining;
                    Vector3[] route=PredictRoute(origin,centre.position,axes,candidate,orbit,climb,ArenaGeometry.LaneRadius,floor,ceiling,heading,burst,boost,climbBoost);
                    Vector3 end=route[route.Length-1];
                    float danger=RouteDanger(route,age,burst,out float landing);
                    if(!useDash) bestWalkDanger=Mathf.Min(bestWalkDanger,danger);
                    float score = danger*10 + landing*20 + (candidate - desired).sqrMagnitude * .15f + (useDash ? 4 : 0);
                    if (wrongSide!=0)
                    {
                        // Input x of +1 lowers the angle, so x equal to the side carries
                        // the ship away from the hull's back and round to its front.
                        score += x == (int)wrongSide ? 0 : 3;
                    }
                    else if (goal.HasValue)
                    {
                        var g = goal.Value;
                        score += Mathf.Max(0, HeightMiss(route, g.position.y) - AlignmentTolerance) * 2;
                        if (g.kind == Kind.Pickup)
                            score += Mathf.Abs(Mathf.DeltaAngle(Angle(end), Angle(g.position))) * Mathf.Deg2Rad * ArenaGeometry.LaneRadius;
                        else score += x == 0 ? .5f : 0;
                    }
                    else score += x == 1 && y == 0 ? 0 : 1;
                    if (score < bestScore) { bestScore = score; best = candidate; selectedDanger = danger; selectedDash=useDash; selectedLanding=landing; selectedEnd=end; }
                }
                desired = best;
                dashEdge = selectedDash;
                if(dashEdge) dashRequests++;
                lastPlan=$"age={age:F3} input={desired} dash={dashEdge} bestWalkDanger={bestWalkDanger:F2} chosenDanger={selectedDanger:F2} landingRisk={selectedLanding:F2} predictedEnd={selectedEnd}";
                recentDecisions.Enqueue($"{Time.time-started:F3} {lastPlan}");
                while(recentDecisions.Count>15) recentDecisions.Dequeue();
                if (goal.HasValue && goal.Value.kind == Kind.Target && Time.time >= nextFlip)
                {
                    float delta = Mathf.DeltaAngle(Angle(origin), Angle(goal.Value.position));
                    GameObject prefab = Read<GameObject>(player, player.DirectionFlipped ? "shootPrefab" : "shootPrefab1");
                    var shot = prefab.GetComponent<ShootScript>();
                    if (Mathf.Abs(delta) > 1 && shot != null && Mathf.Sign(shot.Speed) != Mathf.Sign(delta))
                    { flipEdge = true; nextFlip = Time.time + .5f; }
                }
                string next = selectedDanger > 0 ? "Evading observed threat"
                    : wrongSide != 0 ? "Going round to the front of " + goal.Value.label
                    : goal.HasValue ? (goal.Value.kind == Kind.Pickup ? "Collecting " : "Aligning with ") + goal.Value.label : "Searching";
                if (next != reason || desired != lastLoggedInput || dashEdge || flipEdge) Log($"{next}; input={desired}; dash={dashEdge}; flip={flipEdge}");
                if (goal.HasValue && goal.Value.side != 0 && Time.time >= nextAim) { nextAim = Time.time + 1f; LogAim(goal.Value, origin); }
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
