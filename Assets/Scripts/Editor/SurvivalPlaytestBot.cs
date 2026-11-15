using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private Batch batch;
        private string lastReport = "";
        private float reaction = .25f;
        private Vector2 scroll;
        // What the window's own batch button asks for.
        private int batchRuns = 5, batchSeed = 1;
        private float batchSpeed = 2f;
        // The commit reports are stamped with, read as a session or a batch starts.
        private static string commit = "unknown";

        [MenuItem("Survival Chaos/Playtest Bot", priority = 200)]
        public static void Open() => GetWindow<SurvivalPlaytestBot>("Playtest Bot");

        /// <summary>
        /// The game speed this session flies at. 1 except in a test started
        /// with <see cref="StartSessionAt"/> or in a batch flown faster.
        /// </summary>
        public static float TestSpeed { get; private set; } = 1f;

        /// <summary>
        /// Starts a session with the whole game running faster, to get through
        /// balance runs sooner (asked for on 12 November 2026). Everything the
        /// pilot does is timed in game seconds, so it reacts as it would at
        /// normal speed; what changes is the game time each frame covers, so
        /// keep speed times frame time near a normal frame. The run is marked
        /// assisted and can set no record.
        /// </summary>
        public static string StartSessionAt(float speed)
        {
            var window = GetWindow<SurvivalPlaytestBot>("Playtest Bot");
            if (window.batch != null) return "A batch is running";
            ReadCommit();
            string result = window.Begin(speed, "", 0);
            return result != "Started" ? result
                : "Started at x" + TestSpeed.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string StartSession()
        {
            var window = GetWindow<SurvivalPlaytestBot>("Playtest Bot");
            if (window.batch != null) return "A batch is running";
            ReadCommit();
            return window.Begin(1f, "", 0);
        }

        public static string StopSession()
        {
            var window = GetWindow<SurvivalPlaytestBot>("Playtest Bot");
            if (window.batch != null) window.EndBatch("Manual stop");
            else window.Stop("Manual stop");
            return window.lastReport;
        }

        public static string Status()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<SurvivalPlaytestBot>())
                if (w.pilot != null) return w.pilot.Status;
            return "Stopped";
        }

        /// <summary>
        /// Flies <paramref name="runs"/> whole runs one after another, each from a
        /// fresh load of the Game scene on its own seed, <paramref name="firstSeed"/>
        /// and up, so the same batch can be flown again against a changed game
        /// and meet the same arrivals and the same upgrade draws (RunRandom).
        /// Every run adds a row to Logs/PlaytestBot/runs.csv and the batch ends
        /// with a table of its own beside the reports. Needs Play mode, like a
        /// session; the first load replaces whatever run is under way.
        ///
        /// One run says little: three wins on 9 November 2026 took 22, 2 and 3
        /// damage and a fourth that afternoon took 70. Compare medians.
        /// </summary>
        public static string StartBatch(int runs, float speed = 2f, int firstSeed = 1)
        {
            var window = GetWindow<SurvivalPlaytestBot>("Playtest Bot");
            if (window.batch != null) return "A batch is running";
            if (window.pilot != null) return "Already running";
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return "Enter an unpaused Play session first.";
            ReadCommit();
            window.batch = new Batch
            {
                label = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
                runs = Mathf.Clamp(runs, 1, 50), speed = Mathf.Clamp(speed, 1f, 4f), firstSeed = firstSeed,
                background = Application.runInBackground,
                hintRuns = PlayerPrefs.HasKey(Tutorial.RunsSeenKey) ? PlayerPrefs.GetInt(Tutorial.RunsSeenKey) : -1,
            };
            window.LoadRun();
            return window.batch.Describe();
        }

        /// <summary>The batch in flight, or the last thing reported when there is none.</summary>
        public static string BatchStatus()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<SurvivalPlaytestBot>())
            {
                if (w.batch != null) return w.batch.Describe() + (w.pilot != null ? "\n" + w.pilot.Status : "");
                return "No batch running.\n" + w.lastReport;
            }
            return "No batch running.";
        }

        public static string StopBatch()
        {
            var window = GetWindow<SurvivalPlaytestBot>("Playtest Bot");
            window.EndBatch("Manual stop");
            return window.lastReport;
        }

        private sealed class Batch
        {
            public enum Stage { Loading, Flying, Ending }
            public string label, waitingOn = "";
            public int runs, firstSeed, current, hintRuns;
            public float speed;
            public bool background, loaded;
            public Stage stage;
            // Editor seconds, which keep counting while the game's clock is stopped.
            public double since;
            public readonly List<Row> rows = new List<Row>();
            public int Seed => firstSeed + current;
            public string Describe() => string.Format(CultureInfo.InvariantCulture,
                "Batch {0}: run {1} of {2}, seed {3}, x{4:0.##}, {5}{6}", label, current + 1, runs, Seed, speed,
                stage == Stage.Loading ? "loading" : stage == Stage.Flying ? "flying" : "waiting for the end card",
                waitingOn.Length > 0 && stage == Stage.Loading ? " (" + waitingOn + ")" : "");
        }

        // A load that has not produced a flyable run in this long has failed, and
        // an ending that has not reached its card is left behind.
        private const double LoadTimeout = 120, EndingTimeout = 30;

        private static void ReadCommit()
        {
            string head = BuildStampWriter.Git("rev-parse --short HEAD");
            // The mark is for anything the commit does not hold that a run could
            // feel. ProjectSettings is left out: the open editor rewrites it.
            commit = string.IsNullOrEmpty(head) ? "unknown"
                : head + (string.IsNullOrEmpty(BuildStampWriter.Git("status --porcelain -- Assets Packages")) ? "" : "+");
        }

        /// <summary>
        /// Puts a pilot in the run under way. Every flown run is marked assisted,
        /// at any speed: a best the pilot set is not the player's, and until 15
        /// November 2026 a normal-speed session could leave one in the editor's
        /// saved records.
        /// </summary>
        private string Begin(float speed, string batchLabel, int run)
        {
            if (pilot != null) return "Already running";
            float wanted = Mathf.Clamp(speed, 1f, 4f);
            try { pilot = new Pilot(reaction, wanted, commit, batchLabel, run); }
            catch (Exception e) { return e.Message; }
            TestSpeed = wanted;
            RunRecords.MarkAssisted();
            if (!Mathf.Approximately(wanted, 1f)) RunTime.SetSpeed(wanted);
            return "Started";
        }

        private void LoadRun()
        {
            batch.stage = Batch.Stage.Loading;
            batch.since = EditorApplication.timeSinceStartup;
            batch.loaded = false;
            batch.waitingOn = "";
            // Losing focus would otherwise open the pause menu over the run.
            Application.runInBackground = true;
            RunRandom.SeedNext(batch.Seed);
            LoadingScreen.Load("Game", "Entering the arena");
        }

        private void AdvanceBatch()
        {
            if (!EditorApplication.isPlaying) { EndBatch("Play mode ended"); return; }
            double waited = EditorApplication.timeSinceStartup - batch.since;
            if (batch.stage == Batch.Stage.Ending)
            {
                // The card, so the game's own ending has run its course before
                // the scene under it goes.
                if (!(RunOutcome.RunEnded && waited > 1) && waited < EndingTimeout) return;
                if (batch.current + 1 >= batch.runs) { EndBatch("Complete"); return; }
                batch.current++;
                LoadRun();
            }
            else if (batch.stage == Batch.Stage.Loading)
            {
                if (!batch.loaded) batch.waitingOn = "the Game scene";
                else if (LoadingScreen.Busy) batch.waitingOn = "the loading screen";
                else if (ArenaIntroDirector.IsPlaying) batch.waitingOn = "the arena intro";
                else
                {
                    string result = Begin(batch.speed, batch.label, batch.current + 1);
                    if (result == "Started")
                    {
                        batch.stage = Batch.Stage.Flying;
                        // A run on any other seed is not the run this row says it is.
                        if (RunRandom.Seed != batch.Seed)
                            EndBatch("Run " + (batch.current + 1) + " loaded on seed " + RunRandom.Seed + ", not " + batch.Seed);
                        return;
                    }
                    batch.waitingOn = result;
                }
                if (waited > LoadTimeout) EndBatch("Run " + (batch.current + 1) + " never started: " + batch.waitingOn);
            }
        }

        private void EndBatch(string reason)
        {
            if (batch == null) return;
            Stop("Batch stopped: " + reason);
            var done = batch;
            batch = null;
            Application.runInBackground = done.background;
            // The tutorial counts the runs it has hinted, and these were not the player's.
            if (done.hintRuns >= 0) PlayerPrefs.SetInt(Tutorial.RunsSeenKey, done.hintRuns);
            else PlayerPrefs.DeleteKey(Tutorial.RunsSeenKey);
            try { lastReport = Report.WriteBatch(done.label, done.runs, done.speed, reaction, commit, reason, done.rows); }
            catch (Exception e) { lastReport = "Batch report failed: " + e.Message; Debug.LogException(e); }
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (batch != null && scene.name == "Game") batch.loaded = true;
        }

        private void OnEnable()
        {
            EditorApplication.update += Monitor;
            EditorApplication.playModeStateChanged += PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        private void OnDisable()
        {
            EndBatch("Window closed");
            Stop("Window closed");
            EditorApplication.update -= Monitor;
            EditorApplication.playModeStateChanged -= PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            SceneManager.sceneLoaded -= SceneLoaded;
        }
        private void BeforeReload() { EndBatch("Assembly reload"); Stop("Assembly reload"); }
        private void PlayChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            EndBatch("Play mode ended");
            Stop("Play mode ended");
        }
        private void Monitor()
        {
            if (pilot != null)
            {
                pilot.CheckEnd();
                if (!EditorApplication.isPlaying || pilot.EndReason != null)
                {
                    string reason = pilot.EndReason ?? "Play mode ended";
                    Stop(reason);
                    if (batch != null)
                    {
                        // Only an ending the game reached leads on to the next run.
                        if (EditorApplication.isPlaying && reason.StartsWith("Run ended", StringComparison.Ordinal))
                        {
                            batch.stage = Batch.Stage.Ending;
                            batch.since = EditorApplication.timeSinceStartup;
                        }
                        else EndBatch("Run " + (batch.current + 1) + " stopped: " + reason);
                    }
                }
            }
            if (batch != null) AdvanceBatch();
            if (pilot != null || batch != null) Repaint();
        }
        private void Stop(string reason)
        {
            if (pilot == null) return;
            var old = pilot;
            pilot = null;
            // Input restoration must succeed even if the filesystem is unavailable.
            old.Release();
            // The game goes back at the speed a person plays it.
            if (!Mathf.Approximately(TestSpeed, 1f)) RunTime.SetSpeed(1f);
            TestSpeed = 1f;
            try
            {
                lastReport = old.Export(reason);
                Report.Append(old.Row);
                if (batch != null) batch.rows.Add(old.Row);
            }
            catch (Exception e) { lastReport = "Report failed: " + e.Message; Debug.LogException(e); }
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("A delayed, state-based pilot, not human vision. It cannot judge fog, contrast or laser readability. Start during Play mode with god mode off. Close this window to return control.", MessageType.Info);
            using (new EditorGUI.DisabledScope(pilot != null || batch != null))
            {
                reaction = EditorGUILayout.Slider("Reaction delay (seconds)", reaction, .1f, .6f);
                batchRuns = EditorGUILayout.IntSlider("Batch: runs", batchRuns, 1, 50);
                batchSpeed = EditorGUILayout.Slider("Batch: game speed", batchSpeed, 1f, 4f);
                batchSeed = EditorGUILayout.IntField("Batch: first seed", batchSeed);
            }
            if (batch != null)
            {
                EditorGUILayout.LabelField(batch.Describe(), EditorStyles.wordWrappedLabel);
                if (pilot != null) EditorGUILayout.LabelField(pilot.Status, EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Stop the batch")) EndBatch("Manual stop");
            }
            else if (pilot == null)
            {
                if (GUILayout.Button("Start in current run"))
                {
                    string result = StartSession();
                    if (result != "Started") EditorUtility.DisplayDialog("Playtest Bot", result, "OK");
                }
                if (GUILayout.Button("Fly a batch of fresh runs"))
                {
                    string result = StartBatch(batchRuns, batchSpeed, batchSeed);
                    if (!result.StartsWith("Batch ", StringComparison.Ordinal)) EditorUtility.DisplayDialog("Playtest Bot", result, "OK");
                }
            }
            else
            {
                EditorGUILayout.LabelField(pilot.Status, EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Stop and export report")) Stop("Manual stop");
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.SelectableLabel(lastReport, EditorStyles.wordWrappedLabel, GUILayout.Height(160));
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// One flown run as a line of Logs/PlaytestBot/runs.csv, which every
        /// session adds to, batch or not. Text and whole numbers as they are
        /// written; see <see cref="Report.Columns"/> for the order.
        /// </summary>
        private sealed class Row
        {
            public string file = "", time = "", commit = "", quality = "", batch = "", outcome = "", end = "", killedBy = "", bossPhase = "";
            public string damageByCause = "", damageByMinute = "", upgrades = "";
            public int run, seed, level, kills, hp, maxHp, damage, rammingDamage, healing, dashes, slowMo;
            public float speed, reaction, startedAtRunSeconds, runSeconds, botSeconds, bossAtRunSeconds, bossSeconds, frameMs, gameStepMs;
            public bool assistedBefore;
        }

        /// <summary>
        /// The run table and the batch summary. Numbers are written with a full
        /// stop whatever the machine's language: the reports were not, and on
        /// this one every decimal came out with a comma beside vectors written
        /// with full stops.
        /// </summary>
        private static class Report
        {
            public static readonly string[] Columns =
            {
                "file", "time", "commit", "unity", "quality", "batch", "run", "seed", "speed", "reaction", "assistedBefore",
                "startedAtRunSeconds", "outcome", "end", "killedBy", "runSeconds", "botSeconds", "level", "kills",
                "hp", "maxHp", "damage", "rammingDamage", "healing", "dashes", "slowMo", "bossAtRunSeconds",
                "bossSeconds", "bossPhase", "frameMs", "gameStepMs", "damageByCause", "damageByMinute", "upgrades",
            };

            private static string N(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
            private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

            private static string[] Cells(Row r) => new[]
            {
                r.file, r.time, r.commit, Application.unityVersion, r.quality, r.batch, N(r.run), N(r.seed), N(r.speed), N(r.reaction),
                r.assistedBefore ? "1" : "0", N(r.startedAtRunSeconds), r.outcome, r.end, r.killedBy, N(r.runSeconds),
                N(r.botSeconds), N(r.level), N(r.kills), N(r.hp), N(r.maxHp), N(r.damage), N(r.rammingDamage), N(r.healing),
                N(r.dashes), N(r.slowMo), N(r.bossAtRunSeconds), N(r.bossSeconds), r.bossPhase, N(r.frameMs), N(r.gameStepMs),
                r.damageByCause, r.damageByMinute, r.upgrades,
            };

            /// <summary>A cell as CSV wants it: quoted when it holds a comma, a quote or a line break.</summary>
            public static string Quote(string cell)
            {
                cell = cell ?? "";
                return cell.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? cell : "\"" + cell.Replace("\"", "\"\"") + "\"";
            }

            public static string Line(string[] cells)
            {
                var quoted = new string[cells.Length];
                for (int i = 0; i < cells.Length; i++) quoted[i] = Quote(cells[i]);
                return string.Join(",", quoted);
            }

            /// <summary>The middle value, or the mean of the middle two. 0 for none.</summary>
            public static float Median(float[] values)
            {
                if (values == null || values.Length == 0) return 0f;
                var sorted = (float[])values.Clone();
                Array.Sort(sorted);
                int mid = sorted.Length / 2;
                return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * .5f;
            }

            private static string Folder()
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/PlaytestBot"));
                Directory.CreateDirectory(dir);
                return dir;
            }

            public static void Append(Row row)
            {
                if (row == null) return;
                string path = Path.Combine(Folder(), "runs.csv");
                string header = Line(Columns);
                // A table started under other columns is set aside, not added to:
                // its rows would sit under the wrong headings.
                if (File.Exists(path))
                {
                    using (var reader = new StreamReader(path))
                        if (reader.ReadLine() == header) header = null;
                    if (header != null)
                        File.Move(path, Path.Combine(Folder(), "runs-until-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv"));
                }
                if (header != null) File.WriteAllText(path, header + "\n");
                File.AppendAllText(path, Line(Cells(row)) + "\n");
            }

            /// <summary>
            /// The batch as a table, one line a run, and what its finished runs
            /// come to. A run stopped from outside is listed but left out of the
            /// medians: how long it lasted says nothing about the game.
            /// </summary>
            public static string WriteBatch(string label, int asked, float speed, float reaction, string commit, string reason, List<Row> rows)
            {
                var text = new System.Text.StringBuilder();
                text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "BATCH {0} commit={1} unity={2} quality={3} speed={4:0.##} reaction={5:0.##} runs={6}/{7} end={8}",
                    label, commit, Application.unityVersion, rows.Count > 0 ? rows[0].quality : "unknown", speed, reaction,
                    rows.Count, asked, reason));
                text.AppendLine("run  seed outcome  runSec level kills  dmg  ram heal bossAt frameMs  ended by");
                var finished = new List<Row>();
                foreach (Row r in rows)
                {
                    if (r.outcome != "stopped") finished.Add(r);
                    text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0,3} {1,5} {2,-8} {3,6:0.0} {4,5} {5,5} {6,4} {7,4} {8,4} {9,6} {10,7:0.0}  {11}",
                        r.run, r.seed, r.outcome, r.runSeconds, r.level, r.kills, r.damage, r.rammingDamage, r.healing,
                        r.bossAtRunSeconds > 0 ? r.bossAtRunSeconds.ToString("0", CultureInfo.InvariantCulture) : "-",
                        r.frameMs, r.outcome == "won" ? "the Leviathan's fall" : r.outcome == "lost" ? r.killedBy : r.end));
                }
                if (finished.Count > 0)
                {
                    int won = 0, boss = 0, damage = 0, ramming = 0;
                    var seconds = new float[finished.Count];
                    var levels = new float[finished.Count];
                    var damages = new float[finished.Count];
                    for (int i = 0; i < finished.Count; i++)
                    {
                        Row r = finished[i];
                        seconds[i] = r.runSeconds; levels[i] = r.level; damages[i] = r.damage;
                        if (r.outcome == "won") won++;
                        if (r.bossAtRunSeconds > 0) boss++;
                        damage += r.damage; ramming += r.rammingDamage;
                    }
                    Array.Sort(seconds);
                    text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "MEDIAN of {0} finished: runSec {1:0.0} (min {2:0.0}, max {3:0.0}) | level {4:0.#} | damage {5:0.#} | ramming {6:0}% of damage | reached the boss {7}/{0} | won {8}/{0}",
                        finished.Count, Median(seconds), seconds[0], seconds[seconds.Length - 1], Median(levels), Median(damages),
                        damage > 0 ? 100f * ramming / damage : 0f, boss, won));
                }
                string path = Path.Combine(Folder(), "batch-" + label + ".txt");
                File.WriteAllText(path, text.ToString());
                return text + path;
            }
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
            // The aim diagnostic's probe: about a player round's half size (its box is
            // 0.21 by 0.11, scaled 0.54), stepped along the ring well inside a hull.
            private const float RoundRadius = .05f, AimProbeStep = .2f;
            private readonly float delay;
            private readonly IGameInput previous;
            private readonly Player player;
            private readonly PlayerMovement movement;
            private readonly PlayerDash dash;
            private readonly PlayerSlowMo slowMo;
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
            private int stepped = -1, health, maxHealth, level;
            private bool dashEdge, flipEdge, slowEdge, released;
            private float nextObserve, nextDecision, nextFlip, nextAim;
            // The target being worked on, kept so a nearer distraction has to beat
            // it by a margin rather than by a hair, and whether the pilot is on its
            // way round the ring to an emplacement's front.
            private Object goalSource;
            private bool goingRound;
            private readonly Dictionary<string, int> emplacementHealth = new Dictionary<string, int>();
            private string reason = "Searching", cameraName = "unknown";
            private string bossPhase = "not encountered";
            private BossPhase lastPhase;
            private BossEmitter podsOf;
            private BossWeakPoint[] pods;
            private float bossStarted = -1;
            private int injuries, healed, dashRequests, slowMoUses, flipRequests, flipsSeen;
            // The guns' real direction as last seen, so a press can be told from a turn.
            private bool facingFlipped;
            private float lastFailureDump = -10;
            private string lastPlan = "No plan yet";
            // For the summary and the run table: what the run was flown as, what
            // the damage came from and in which minute of the run's own clock,
            // and what a frame cost, in real time and in game time.
            private readonly float speed, startedAtRunSeconds;
            private readonly string commit, batchLabel, quality;
            private readonly int runNumber;
            private readonly bool assistedBefore;
            private readonly List<string> causes = new List<string>();
            private readonly Dictionary<string, int> damageByCause = new Dictionary<string, int>();
            private readonly List<int> damageByMinute = new List<int>();
            private float bossAtRunSeconds, realSeconds, gameSeconds;
            private int frames;
            public Row Row { get; private set; }
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
                // Where the pilot's own ship was when this was seen: height chasers
                // spend the reaction delay closing on it.
                public Vector3 playerPosition;
                public List<Item> items = new List<Item>();
            }

            /// <summary>
            /// Has every number written while it is held come out with a full
            /// stop. Held round each of the pilot's entry points and put back on
            /// the way out, so the game never runs under it: a report line is an
            /// interpolated string, which takes the machine's language at the
            /// moment it is built, and one that forgot to ask otherwise would be
            /// back to decimal commas.
            /// </summary>
            private readonly struct Invariant : IDisposable
            {
                private readonly CultureInfo was;
                private Invariant(CultureInfo was) { this.was = was; }
                public static Invariant Numbers()
                {
                    var held = new Invariant(CultureInfo.CurrentCulture);
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    return held;
                }
                public void Dispose() { CultureInfo.CurrentCulture = was; }
            }

            public Pilot(float reaction, float speed, string commit, string batchLabel, int runNumber)
            {
                if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                    throw new InvalidOperationException("Enter an unpaused Play session first.");
                player = Object.FindAnyObjectByType<Player>();
                if (player == null || player.CurrentHealth <= 0 || RunOutcome.RunEnded)
                    throw new InvalidOperationException("No living player in an active run.");
                if (player.Invulnerable) throw new InvalidOperationException("Turn god mode off first.");
                movement = player.GetComponent<PlayerMovement>();
                dash = player.GetComponent<PlayerDash>();
                slowMo = player.GetComponent<PlayerSlowMo>();
                band = player.GetComponent<ApplyBounds>();
                centre = Read<Transform>(movement, "center");
                if (movement == null || centre == null || band == null || !band.TryGetBand(out _, out _))
                    throw new InvalidOperationException("Player movement, arena centre or height bounds missing.");
                if (Camera.main == null) throw new InvalidOperationException("No gameplay camera.");
                delay = reaction;
                started = Time.time;
                health = player.CurrentHealth;
                maxHealth = player.MaxHealth;
                level = player.currentLevel;
                previous = GameInput.Source;
                this.speed = speed;
                this.commit = commit;
                this.batchLabel = batchLabel;
                this.runNumber = runNumber;
                // Before the session marks the run itself: whether the debug menu,
                // a kit or a wound-on clock had already touched it.
                assistedBefore = RunRecords.Assisted;
                startedAtRunSeconds = RunStats.Seconds;
                facingFlipped = player.DirectionFlipped;
                int held = 0;
                foreach (string skill in RunStats.SkillOrder) held += RunStats.PicksOf(skill);
                // The graphics tier, because it sets the frame time and the frame
                // time is what a fast run's steps are made of.
                string[] tiers = QualitySettings.names;
                int tier = QualitySettings.GetQualityLevel();
                quality = tier >= 0 && tier < tiers.Length ? tiers[tier] : "unknown";
                using var numbers = Invariant.Numbers();
                Log($"START hp={health}/{player.MaxHealth} level={level} reaction={delay} observation={ObserveEvery} commitment={CommitFor} horizon={Horizon}"
                    + $" speed={speed:0.##} seed={RunRandom.Seed} commit={commit} unity={Application.unityVersion} quality={quality} runClock={startedAtRunSeconds:F1}"
                    + $" assistedBefore={assistedBefore} upgradesHeld={held}" + (batchLabel.Length > 0 ? $" batch={batchLabel} run={runNumber}" : ""));
                Log("LIMITS: state perception; physics occlusion only; no contrast/fog/audio perception; no offscreen memory; prediction updates observed turns/bounces but cannot foresee them; hull flashes are ambiguous and may be damage, not ram charge; damage cause is the game's own record of the last hit. The seed fixes arrivals and upgrade draws, not the fight: the ship's position and frame timing still vary.");
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
                dashEdge = flipEdge = slowEdge = false;
                released = true;
                if (ReferenceEquals(GameInput.Source, this)) GameInput.Source = previous;
            }
            public string Export(string end)
            {
                using var numbers = Invariant.Numbers();
                int hp = player != null ? player.CurrentHealth : 0;
                // Won or lost is the game's word. A session ended from outside is
                // neither, however the ship was doing.
                string outcome = player == null ? "stopped" : RunOutcome.Decided && RunOutcome.Won ? "won"
                    : hp <= 0 || RunOutcome.Decided ? "lost" : "stopped";
                float botSeconds = Time.time - started;
                float bossSeconds = bossStarted < 0 ? 0 : Time.time - bossStarted;
                float frameMs = frames > 0 ? realSeconds / frames * 1000f : 0;
                float gameStepMs = frames > 0 ? gameSeconds / frames * 1000f : 0;
                int ramming = 0;
                var byCause = new List<string>();
                foreach (string cause in causes)
                {
                    byCause.Add($"{cause}={damageByCause[cause]}");
                    if (cause.StartsWith("ramming", StringComparison.Ordinal)) ramming += damageByCause[cause];
                }
                var upgrades = new List<string>();
                foreach (string skill in RunStats.SkillOrder) upgrades.Add($"{skill}={RunStats.PicksOf(skill)}");
                Log("END " + end);
                Log($"SUMMARY runSeconds={RunStats.Seconds:F2} botSeconds={botSeconds:F2} kills={RunStats.EnemiesDestroyed} level={RunStats.LevelReached} hp={hp} bossSeconds={bossSeconds:F2} phase={bossPhase} damageObserved={injuries} healingObserved={healed} dashRequests={dashRequests} slowMoUses={slowMoUses} flipRequests={flipRequests} flipsSeen={flipsSeen}"
                    + $" outcome={outcome} maxHp={maxHealth} rammingDamage={ramming} bossAtRunSeconds={bossAtRunSeconds:F2} frameMs={frameMs:F2} gameStepMs={gameStepMs:F2} speed={speed:0.##} seed={RunRandom.Seed} commit={commit} assistedBefore={assistedBefore}");
                if (outcome == "lost") Log("KILLED BY " + (RunStats.KilledBy ?? "unknown"));
                Log("DAMAGE BY CAUSE " + (byCause.Count > 0 ? string.Join("; ", byCause) : "none"));
                Log("DAMAGE BY MINUTE " + string.Join(" ", damageByMinute));
                foreach (string skill in RunStats.SkillOrder) Log($"UPGRADE {skill}: {RunStats.PicksOf(skill)}");
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/PlaytestBot"));
                Directory.CreateDirectory(dir);
                DateTime now = DateTime.Now;
                string path = Path.Combine(dir, now.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
                Row = new Row
                {
                    file = Path.GetFileName(path), time = now.ToString("yyyy-MM-dd HH:mm:ss"), commit = commit, quality = quality, batch = batchLabel,
                    run = runNumber, seed = RunRandom.Seed, speed = speed, reaction = delay, assistedBefore = assistedBefore,
                    startedAtRunSeconds = startedAtRunSeconds, outcome = outcome, end = end,
                    killedBy = outcome == "lost" ? RunStats.KilledBy ?? "unknown" : "",
                    runSeconds = RunStats.Seconds, botSeconds = botSeconds, level = RunStats.LevelReached,
                    kills = RunStats.EnemiesDestroyed, hp = hp, maxHp = maxHealth, damage = injuries, rammingDamage = ramming,
                    healing = healed, dashes = dashRequests, slowMo = slowMoUses, bossAtRunSeconds = bossAtRunSeconds,
                    bossSeconds = bossSeconds, bossPhase = bossPhase, frameMs = frameMs, gameStepMs = gameStepMs,
                    damageByCause = string.Join("; ", byCause), damageByMinute = string.Join(" ", damageByMinute),
                    upgrades = string.Join("; ", upgrades),
                };
                File.WriteAllLines(path, log);
                return path;
            }
            public void CheckEnd()
            {
                using var numbers = Invariant.Numbers();
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
                // The enum compared, the name written only when it changes, and
                // the pods found once per boss: this runs every editor update,
                // and its strings and arrays were garbage in the same runs used
                // to measure garbage.
                if (boss != null && (bossStarted < 0 || boss.Phase != lastPhase))
                {
                    if (bossStarted < 0) { bossStarted = Time.time; bossAtRunSeconds = RunStats.Seconds; }
                    lastPhase = boss.Phase;
                    bossPhase = lastPhase.ToString();
                    Log("BOSS PHASE " + bossPhase);
                }
                if (boss != null)
                {
                    if (podsOf != boss)
                    {
                        podsOf = boss;
                        pods = boss.GetComponentsInChildren<BossWeakPoint>(true);
                    }
                    foreach (var pod in pods)
                    {
                        if (pod == null) continue;
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
                Log($"HP {health}->{current}; cause={(current < health ? player.LastHitBy ?? "unknown" : "healing")}; at={player.transform.position}; input={axes}; dashActive={(dash != null && dash.Invincible)}");
                if (current < health)
                {
                    injuries += health - current;
                    string cause = player.LastHitBy ?? "unknown";
                    if (!damageByCause.TryGetValue(cause, out int soFar)) causes.Add(cause);
                    damageByCause[cause] = soFar + health - current;
                    // The run's clock, which is what the waves ramp on, and which
                    // has stopped at the deciding hit by the time that hit is read.
                    int minute = Mathf.Max(0, (int)(RunStats.Seconds / 60f));
                    while (damageByMinute.Count <= minute) damageByMinute.Add(0);
                    damageByMinute[minute] += health - current;
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
                                // Both, because they differ for a height chaser: plannedNow is
                                // where RouteDanger takes it to be, and the straight-line
                                // extrapolation walks it on at the height rate it was seen with.
                                Vector3 extrapolated = Predict(item, age);
                                Vector3 planned = extrapolated;
                                if (item.homeRate > 0) planned.y = ChaserHeightNow(item, age);
                                Log($"SEEN {item.kind} {item.label} at={item.position} plannedNow={planned} extrapolatedNow={extrapolated} angularRate={item.angleRate:F2} verticalRate={item.heightRate:F2}"
                                    + (item.homeRate > 0 ? $" chasesHeight={item.homeRate:F2}" : "") + (item.kind == Kind.LineOfFire ? $" fires={item.fireDirection:+0;-0}" : ""));
                                if (++count >= 12) break;
                            }
                        }
                        Log("FAILURE CONTEXT END");
                    }
                }
                // A Max HP pick raises current health with the ceiling. Only what
                // the ceiling does not explain is healing - salvage, in practice.
                else healed += Mathf.Max(0, current - health - Mathf.Max(0, player.MaxHealth - maxHealth));
                maxHealth = player.MaxHealth;
                health = current;
            }
            private void Step()
            {
                if (stepped == Time.frameCount || released) return;
                stepped = Time.frameCount;
                dashEdge = flipEdge = slowEdge = false;
                using var numbers = Invariant.Numbers();
                try
                {
                    if (player == null || RunOutcome.RunEnded || player.CurrentHealth <= 0)
                    { EndReason = "Run ended"; axes = Vector2.zero; return; }
                    if (player.Invulnerable) { EndReason = "God mode enabled; run invalidated"; axes = Vector2.zero; return; }
                    // Paused: hold still. The debug menu's speed voids the run; Slow Mo,
                    // the player's own ability, does not.
                    if (Time.timeScale <= 0f) { axes = Vector2.zero; return; }
                    if (!Mathf.Approximately(RunTime.RequestedSpeed, TestSpeed))
                    { axes = Vector2.zero; EndReason = "Nonstandard time scale; run invalidated"; return; }
                    RecordHealth();
                    if (player.DirectionFlipped != facingFlipped)
                    {
                        facingFlipped = player.DirectionFlipped;
                        flipsSeen++;
                        Log(facingFlipped ? "FACING flipped" : "FACING forward");
                    }
                    frames++;
                    realSeconds += Time.unscaledDeltaTime;
                    gameSeconds += Time.deltaTime;
                    foreach (string skill in RunStats.SkillOrder)
                    {
                        int count = RunStats.PicksOf(skill);
                        if (!upgradeCounts.TryGetValue(skill, out int before) || before != count)
                        { Log($"COLLECTED {skill} count={count}"); upgradeCounts[skill] = count; }
                    }
                    if (level != player.currentLevel) { Log($"LEVEL {level}->{player.currentLevel}"); level = player.currentLevel; }
                    // A person's looking, reacting and committing happen in real time,
                    // so under Slow Mo they take a fraction of the game's seconds. That
                    // is the whole of what the ability buys, and the pilot gets it too.
                    float real = RunTime.AbilityScale;
                    if (Time.time >= nextObserve) { Observe(); nextObserve = Time.time + ObserveEvery * real; }
                    while (pending.Count > 0 && pending.Peek().time + delay * real <= Time.time) known = pending.Dequeue();
                    if (Time.time >= nextDecision) { Decide(); nextDecision = Time.time + CommitFor * real; }
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
                // Walks the ring from the ship toward the pod at the ship's height, the
                // way a round flies, and names the first thing a round would meet. It
                // checked bounding boxes of Enemy and wreckage colliders only, so the hull
                // and the two shooting kinds never showed, and a box that spans the band
                // cannot say what a round meets first anyway.
                string blocker = "nothing";
                if (Mathf.Sign(gap) == fire)
                {
                    float radius = Radius(origin), from = Angle(origin);
                    float step = AimProbeStep / Mathf.Max(.01f, radius) * Mathf.Rad2Deg;
                    Transform target = goal.source is Component aimed && aimed != null ? aimed.transform : null;
                    bool reached = false;
                    for (float swept = step; swept < Mathf.Abs(gap) && !reached && blocker == "nothing"; swept += step)
                    {
                        foreach (var c in Physics.OverlapSphere(At(from + swept * fire, radius, origin.y), RoundRadius, ~0, QueryTriggerInteraction.Collide))
                        {
                            if (c.transform.IsChildOf(player.transform)) continue;
                            if (target != null && c.transform.IsChildOf(target)) { reached = true; break; }
                            Component owner = RoundStopper(c);
                            if (owner != null) { blocker = owner.name; break; }
                        }
                    }
                }
                Log($"AIM {goal.label} heightOff={origin.y - truth.y:+0.00;-0.00} seenHeightOff={origin.y - goal.position.y:+0.00;-0.00} angleTo={gap:F1} "
                    + $"firing={(fire == 0 ? "unknown" : Mathf.Sign(gap) == fire ? "toward" : "away")} inFront={inFront:F1} side={goal.side:+0;-0} "
                    + $"inTheWay={blocker}");
            }
            /// <summary>
            /// What a player round hitting <paramref name="c"/> would stop on: an
            /// emplacement, the hull, a wreck plate, or a ship of either kind, looked up
            /// from the collider through its parents. Null for anything rounds pass,
            /// such as pickups and enemy rounds.
            /// </summary>
            private static Component RoundStopper(Collider c)
            {
                Component owner = c.GetComponentInParent<BossWeakPoint>();
                if (owner == null) owner = c.GetComponentInParent<BossEmitter>();
                if (owner == null) owner = c.GetComponentInParent<BossWreckage>();
                if (owner == null) owner = c.GetComponentInParent<EnemyBase>();
                return owner;
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
                var snapshot = new Snapshot { time = Time.time, playerPosition = player.transform.position };
                var seen = new HashSet<Component>();
                foreach (var shot in ShootScript.Live)
                    if (shot != null && shot.isActiveAndEnabled && shot.CompareTag("enemy_Shoot")) Add(shot, Kind.Threat, 0, shot.Volley, snapshot, seen);
                // Both kinds through their shared base. Until 22 Sep the pilot looked
                // for Enemy only and never saw the two shooting ships, which carry
                // Enemy_1, so it neither aimed at them, flew round them, nor watched
                // their guns.
                foreach (var enemy in Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude))
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
                    // Salvage restores 1 and comes often when hurt: worth a detour
                    // whenever anything is missing, ahead of everything when low.
                    float priority = pickup.HealAmount > 0 ? (health < player.MaxHealth * .5f ? 12 : health < player.MaxHealth ? 4 : -1)
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
                if (obj is EnemyBase && obj.GetComponent<BossEmitter>() == null)
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
                    var gun = obj as Enemy_1;
                    if (gun == null) gun = obj.GetComponentInChildren<Enemy_1>();
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
            /// height is what counts; where the ship is at the next decision still
            /// weighs a little, so that once there, holding the height beats flying
            /// through it.
            ///
            /// That second term used to be the end of the horizon, 1.2 s out, which
            /// no route ever reaches: the pilot re-decides after CommitFor. From
            /// close under a target, a climb held to the horizon passes it and ends
            /// five or six units over, and a quarter of that cost more than staying
            /// a unit short, so on 25 Sep the pilot sat on the floor 0.96 under the
            /// keel pod for whole fights, twice, every round passing under it.
            /// </summary>
            private static float HeightMiss(Vector3[] route, float target)
            {
                float closest = float.PositiveInfinity;
                foreach (var point in route) closest = Mathf.Min(closest, Mathf.Abs(point.y - target));
                int next = Mathf.Clamp(Mathf.RoundToInt(CommitFor / PredictStep), 1, route.Length) - 1;
                return closest + .25f * Mathf.Abs(route[next].y - target);
            }
            /// <summary>
            /// How far a route stays from a pickup: the closest it comes, plus a
            /// little for where it ends. Straight-line distance, because a pickup is
            /// taken by flying into its sphere, so the ring and the height count
            /// together.
            ///
            /// The same fix as <see cref="HeightMiss"/>, for the ring. Pickups were
            /// scored on where the route ended alone, and a route held at full speed
            /// for the horizon runs about 6.7 units, so from anywhere nearer than
            /// half that, flying through the pickup ended further from it than
            /// stopping did. On 24 Sep the pilot twice parked 2 units short of an
            /// upgrade, level with it, for over 20 seconds, and three offers ran
            /// out in a 6-minute run.
            /// </summary>
            private static float PickupMiss(Vector3[] route, Vector3 target)
            {
                float closest = float.PositiveInfinity;
                foreach (var point in route) closest = Mathf.Min(closest, Vector3.Distance(point, target));
                return closest + .1f * Vector3.Distance(route[route.Length - 1], target);
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
            /// A height chaser over the reaction delay: from <paramref name="observed"/>,
            /// seen when the player was at <paramref name="playerThen"/>, to now, with the
            /// player at <paramref name="playerNow"/>. That chase has happened, so it
            /// follows where the player went, taken as a straight line between the two,
            /// and no route the pilot is weighing can change it.
            /// </summary>
            private static float CaughtUp(float observed, float playerThen, float playerNow, float rate, float elapsed)
            {
                float height = observed;
                for (float done = 0; done < elapsed; done += PredictStep)
                {
                    float step = Mathf.Min(PredictStep, elapsed - done);
                    height = HomedHeight(height, Mathf.Lerp(playerThen, playerNow, (done + step * .5f) / elapsed), rate, step);
                }
                return height;
            }
            /// <summary>
            /// The heights a height chaser takes along a route, from <paramref name="height"/>
            /// now, a step at a time so each stretch closes on where the player is then.
            /// Worked out in one go from each point's height, a chaser was forecast as if
            /// it had been after that height since it was seen: against a climb, already
            /// most of the way to its top. Out of <paramref name="chaseRadius"/> it holds its
            /// height, as EnemyMovement does. <paramref name="at"/> is where it is on the ring
            /// at each point.
            /// </summary>
            private static float[] ChaseAlong(float height, float rate, float chaseRadius, Vector3[] route, Func<int, Vector3> at)
            {
                var heights = new float[route.Length];
                for (int i = 0; i < route.Length; i++)
                {
                    Vector3 enemy = at(i); enemy.y = height;
                    if (Vector3.Distance(enemy, route[i]) <= chaseRadius) height = HomedHeight(height, route[i].y, rate, PredictStep);
                    heights[i] = height;
                }
                return heights;
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
            /// <summary>
            /// The height the planner takes a height chaser to be at now, <paramref name="age"/>
            /// seconds after it was seen: caught up on the player over that time if the
            /// player was in its reach, otherwise holding the height it was seen at, as
            /// EnemyMovement does out of reach. Shared by <see cref="RouteDanger"/> and
            /// the failure log, so the log reports what the planner acted on.
            /// </summary>
            private float ChaserHeightNow(Item item, float age)
            {
                return Vector3.Distance(item.position, known.playerPosition) <= item.chaseRadius
                    ? CaughtUp(item.position.y, known.playerPosition.y, player.transform.position.y, item.homeRate, age)
                    : item.position.y;
            }
            private float RouteDanger(Vector3[] route, float age, float invincibleFor, out float landing)
            {
                float danger=0; landing=0;
                // Each height chaser's heights along this route. The chase runs through
                // a dash too, so these are worked out before the dash's steps are skipped.
                var chased=new float[known.items.Count][];
                for(int j=0;j<known.items.Count;j++)
                {
                    var item=known.items[j];
                    if(item.homeRate<=0) continue;
                    chased[j]=ChaseAlong(ChaserHeightNow(item,age),item.homeRate,item.chaseRadius,route,i=>Predict(item,age+(i+1)*PredictStep));
                }
                for(int i=0;i<route.Length;i++)
                {
                    float t=(i+1)*PredictStep;
                    if(t<invincibleFor) continue;
                    for(int j=0;j<known.items.Count;j++)
                    {
                        var item=known.items[j];
                        if(item.kind==Kind.Pickup) continue;
                        float cost=0;
                        if(item.kind==Kind.Warning)
                            cost=Mathf.Abs(route[i].y-item.position.y)<.7f ? 12 : 0;
                        else if(item.kind==Kind.Beam)
                            cost=SegmentDistance(route[i],item.position,item.end)<item.radius+SafetyMargin ? 100 : 0;
                        else if(item.kind==Kind.LineOfFire)
                        {
                            Vector3 gun=Predict(item,age+t);
                            if(chased[j]!=null) gun.y=chased[j][i];
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
                            if(chased[j]!=null) predicted.y=chased[j][i];
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
                        if (g.kind == Kind.Pickup)
                            score += PickupMiss(route, g.position) * 2;
                        else
                        {
                            score += Mathf.Max(0, HeightMiss(route, g.position.y) - AlignmentTolerance) * 2;
                            score += x == 0 ? .5f : 0;
                        }
                    }
                    else score += x == 1 && y == 0 ? 0 : 1;
                    if (score < bestScore) { bestScore = score; best = candidate; selectedDanger = danger; selectedDash=useDash; selectedLanding=landing; selectedEnd=end; }
                }
                desired = best;
                dashEdge = selectedDash;
                if(dashEdge) dashRequests++;
                // Slow Mo is reached for when no route on the stick gets clear and
                // the dash is not there to take - the moment a person would.
                slowEdge = !selectedDash && bestWalkDanger >= 1 && slowMo != null && slowMo.Ready;
                if (slowEdge) { slowMoUses++; Log($"SLOWMO bestWalkDanger={bestWalkDanger:F2} dashReady={(dash != null && dash.ReadyFraction >= 1)}"); }
                lastPlan=$"age={age:F3} input={desired} dash={dashEdge} bestWalkDanger={bestWalkDanger:F2} chosenDanger={selectedDanger:F2} landingRisk={selectedLanding:F2} predictedEnd={selectedEnd}";
                recentDecisions.Enqueue($"{Time.time-started:F3} {lastPlan}");
                while(recentDecisions.Count>15) recentDecisions.Dequeue();
                if (goal.HasValue && goal.Value.kind == Kind.Target && Time.time >= nextFlip)
                {
                    float delta = Mathf.DeltaAngle(Angle(origin), Angle(goal.Value.position));
                    GameObject prefab = Read<GameObject>(player, player.DirectionFlipped ? "shootPrefab" : "shootPrefab1");
                    var shot = prefab.GetComponent<ShootScript>();
                    if (Mathf.Abs(delta) > 1 && shot != null && Mathf.Sign(shot.Speed) != Mathf.Sign(delta))
                    { flipEdge = true; flipRequests++; nextFlip = Time.time + .5f; }
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
            public bool SlowMoPressed { get { Step(); return slowEdge; } }
            public bool ToggleDirectionReleased { get { Step(); return flipEdge; } }
            public bool PausePressed => previous.PausePressed;
            public bool BackPressed => previous.BackPressed;
            public bool DebugLevelUpPressed => false;
            public bool DebugOverlayTogglePressed => previous.DebugOverlayTogglePressed;
            public bool DebugCopyReportPressed => previous.DebugCopyReportPressed;
            public bool DebugMenuTogglePressed => false;
            public bool ScreenshotPressed => previous.ScreenshotPressed;
            public int DebugShortcutPressed => 0;
            public int MenuTabPressed => previous.MenuTabPressed;
            public bool PadLastUsed => previous.PadLastUsed;
        }
    }
}
