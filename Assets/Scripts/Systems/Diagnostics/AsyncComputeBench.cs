using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace SurvivalChaos
{
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION || SURVIVAL_CHAOS_DEBUG_MENU
    /// <summary>
    /// What the bench was asked to do, read off the command line.
    ///
    /// <c>-asyncBench</c> times async compute on and off; <c>-benchGI N</c>
    /// runs it with the GI row at rung N (0 Off to 6 RT High) for this session
    /// only. <c>-benchFixed on|off</c> sets async once, as the scene loads,
    /// and times the holds without switching it. <c>-benchReflections</c>
    /// instead loads a saved RT Medium on the Reflections row and reports what
    /// the row makes of it.
    /// </summary>
    public readonly struct BenchRequest
    {
        public readonly bool Async;
        public readonly bool Reflections;
        public readonly int GlobalIllumination;

        /// <summary>-1 to switch async back and forth, 0 to hold it off, 1 to hold it on.</summary>
        public readonly int Fixed;

        public BenchRequest(bool async, bool reflections, int globalIllumination, int fixedAsync = -1)
        {
            Async = async;
            Reflections = reflections;
            GlobalIllumination = globalIllumination;
            Fixed = fixedAsync;
        }

        public bool Any => Async || Reflections || Fixed >= 0;

        public static BenchRequest Parse(string[] args)
        {
            bool async = false;
            bool reflections = false;
            int gi = -1;
            int held = -1;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-asyncBench": async = true; break;
                    case "-benchReflections": reflections = true; break;
                    case "-benchFixed":
                        if (i + 1 < args.Length && (args[i + 1] == "on" || args[i + 1] == "off"))
                        {
                            held = args[i + 1] == "on" ? 1 : 0;
                            i++;
                        }
                        break;
                    case "-benchGI":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int rung) &&
                            rung >= 0 && rung <= (int)EffectQuality.RayTracedHigh)
                        {
                            gi = rung;
                            i++;
                        }
                        break;
                }
            }

            return new BenchRequest(async, reflections, gi, held);
        }
    }

    /// <summary>
    /// The arithmetic behind the bench's verdict, kept apart so it can be tested.
    /// </summary>
    public static class BenchMath
    {
        public static double Mean(IReadOnlyList<double> values)
        {
            if (values.Count == 0) { return double.NaN; }
            double sum = 0d;
            for (int i = 0; i < values.Count; i++) { sum += values[i]; }
            return sum / values.Count;
        }

        public static double Median(IReadOnlyList<double> values)
        {
            if (values.Count == 0) { return double.NaN; }
            var sorted = new List<double>(values);
            sorted.Sort();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5d;
        }

        /// <summary>
        /// Phases alternate on, off, on, ... on, so there is one more on than
        /// off. Each off phase is scored against the two on phases either side
        /// of it, and the verdict takes the median: a stretch where the GPU's
        /// time jumps for reasons of its own (seen with FSR 3 on 16 October
        /// 2026: 8 ms phases next to 24 ms ones, on and off alike) spoils one
        /// or two pairs instead of the whole hold.
        ///
        /// Saving is the median of those paired savings, positive when off is
        /// faster. Noise is the median gap between neighbouring on phases,
        /// which are repeats of one setting. Off wins when the saving is
        /// larger than the noise.
        /// </summary>
        public static bool OffWins(IReadOnlyList<double> on, IReadOnlyList<double> off, out double saving, out double noise)
        {
            var savings = new List<double>(off.Count);
            var gaps = new List<double>(off.Count);
            for (int i = 0; i < off.Count && i + 1 < on.Count; i++)
            {
                savings.Add((on[i] + on[i + 1]) * 0.5d - off[i]);
                gaps.Add(System.Math.Abs(on[i + 1] - on[i]));
            }

            saving = Median(savings);
            noise = Median(gaps);
            return savings.Count > 0 && saving > noise;
        }
    }

    /// <summary>
    /// A hands-off A/B of HDRP's async compute in a player, for the 16 October
    /// 2026 performance scan's finding 6: in the editor, Asynchronous Execution
    /// off in the camera's frame settings made the frame 1.0 ms faster, and the
    /// editor schedules the GPU differently from a player.
    ///
    /// Started with <c>-asyncBench</c>, it starts a run, makes the ship safe,
    /// turns both lightnings off so every frame is lit the same, and holds a
    /// frame early, with the storm wall tall. On that frame it times the GPU
    /// with async on and off in 25 short alternating phases, through
    /// FrameTimingManager, as the F3 overlay reads it. Then it lets go, takes the debug menu's own Skip to
    /// boss, waits for the boss to fly in, holds again and repeats. The numbers
    /// go to Player.log under [AsyncBench] and the game quits.
    ///
    /// Compiled only where the debug menu is, never into a Release build. A GI
    /// rung asked for with <c>-benchGI</c> is written for this session and the
    /// saved value put back before quitting.
    /// </summary>
    public sealed class AsyncComputeBench : MonoBehaviour
    {
        private const string Tag = "[AsyncBench] ";
        private const string Prefix = "SurvivalChaos.Graphics.";

        /// <summary>Real seconds of run before the early hold: the wall is still tall.</summary>
        private const float EarlySeconds = 40f;

        /// <summary>Real seconds after the boss appears before the boss hold, for its fly-in.</summary>
        private const float BossSeconds = 20f;

        /// <summary>Frames thrown away after each switch, while the pipeline settles.</summary>
        private const int SettleFrames = 30;

        /// <summary>Frames timed per phase. Short, so a phase rarely spans one of the GPU's jumps.</summary>
        private const int SampleFrames = 120;

        /// <summary>Off phases per hold, each between two on phases.</summary>
        private const int Cycles = 12;

        private static BenchRequest request;
        private static readonly Dictionary<string, int> savedPrefs = new Dictionary<string, int>();

        private readonly FrameTiming[] timings = new FrameTiming[1];
        private readonly List<double> samples = new List<double>(SampleFrames);
        private readonly List<double> wall = new List<double>(SampleFrames);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReadArguments()
        {
            savedPrefs.Clear();
            request = BenchRequest.Parse(System.Environment.GetCommandLineArgs());
            if (!request.Any)
            {
                return;
            }

            // Before the graphics director reads its rows (BeforeSceneLoad), so
            // it starts on these values the way it starts on saved ones.
            if (request.GlobalIllumination >= 0)
            {
                Override("GI", request.GlobalIllumination);
            }

            if (request.Reflections)
            {
                Override("SSR", (int)EffectQuality.RayTracedMedium);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!request.Any)
            {
                return;
            }

            GameObject host = new GameObject("Async Compute Bench");
            host.AddComponent<AsyncComputeBench>();
            DontDestroyOnLoad(host);
        }

        private static void Override(string key, int value)
        {
            savedPrefs[key] = PlayerPrefs.GetInt(Prefix + key, -1);
            PlayerPrefs.SetInt(Prefix + key, value);
        }

        /// <summary>Puts back every row this session overrode, unset ones unset.</summary>
        private static void RestorePrefs()
        {
            foreach (KeyValuePair<string, int> saved in savedPrefs)
            {
                if (saved.Value < 0) { PlayerPrefs.DeleteKey(Prefix + saved.Key); }
                else { PlayerPrefs.SetInt(Prefix + saved.Key, saved.Value); }
            }

            PlayerPrefs.Save();
            if (savedPrefs.Count > 0)
            {
                Debug.Log(Tag + "saved rows put back: " + string.Join(", ", Describe(savedPrefs)));
            }

            savedPrefs.Clear();
        }

        private static IEnumerable<string> Describe(Dictionary<string, int> prefs)
        {
            foreach (KeyValuePair<string, int> pref in prefs)
            {
                yield return pref.Key + "=" + (pref.Value < 0 ? "unset" : pref.Value.ToString());
            }
        }

        private void OnApplicationQuit()
        {
            RestorePrefs();
        }

        private IEnumerator Start()
        {
            // Losing focus would otherwise open the pause menu over the frame.
            Application.runInBackground = true;
            Debug.Log(Tag + "start: " + string.Join(" ", System.Environment.GetCommandLineArgs()));

            yield return new WaitForSecondsRealtime(2f);
            LoadingScreen.Load("Game", "Entering the arena");

            while (SceneManager.GetActiveScene().name != "Game" || LoadingScreen.Busy)
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(1f);
            MakeSafe();
            LogSettings();

            // The whole run on one setting, as a shipped camera would be.
            if (request.Fixed >= 0 && Camera.main != null &&
                Camera.main.TryGetComponent(out HDAdditionalCameraData held))
            {
                SetAsync(held, held.customRenderingSettings, request.Fixed == 1);
            }

            if (request.Reflections)
            {
                CheckReflectionsRow();
            }

            if (request.Async || request.Fixed >= 0)
            {
                yield return Measure("early", EarlySeconds, false);
                yield return Measure("boss", BossSeconds, true);
            }

            RestorePrefs();
            Debug.Log(Tag + "done");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        private static void MakeSafe()
        {
            RunRecords.MarkAssisted();
            Player player = FindAnyObjectByType<Player>();
            if (player != null) { player.Invulnerable = true; }
            AshLightning ash = FindAnyObjectByType<AshLightning>();
            if (ash != null) { ash.enabled = false; }
            StormLightning storm = FindAnyObjectByType<StormLightning>();
            if (storm != null) { storm.enabled = false; }
        }

        private static void LogSettings()
        {
            GraphicsDirector d = GraphicsDirector.Instance;
            if (d == null)
            {
                Debug.Log(Tag + "no graphics director");
                return;
            }

            Debug.Log(Tag + $"settings: tier {d.QualityNames[d.QualityLevel]}, GI {d.GlobalIlluminationQuality}, " +
                      $"reflections {d.Reflections}, fog {d.VolumetricFog}, shadows {d.Shadows}, clouds {d.CloudQuality}, " +
                      $"upscaler {d.Method} at {d.RenderScale:0.##}, screen {Screen.width}x{Screen.height} {Screen.fullScreenMode}, " +
                      $"GPU {SystemInfo.graphicsDeviceName}, async supported {SystemInfo.supportsAsyncCompute}, " +
                      $"camera async as the scene has it {(Camera.main != null ? HDCamera.GetOrCreate(Camera.main).frameSettings.IsEnabled(FrameSettingsField.AsyncCompute).ToString() : "no camera")}");
        }

        /// <summary>
        /// 66a86ed dropped RT Medium from the Reflections row. A saved RT Medium
        /// was written before the director started, so this reads what it made
        /// of it, then steps the menu's own row once round and lists the rungs.
        /// </summary>
        private static void CheckReflectionsRow()
        {
            GraphicsDirector d = GraphicsDirector.Instance;
            int stored = PlayerPrefs.GetInt(Prefix + "SSR", -1);
            Debug.Log(Tag + $"reflections: stored {stored} ({(EffectQuality)stored}), row reads {d.Reflections} " +
                      $"({QualityLadder.Describe(d.Reflections)}), index {System.Array.IndexOf(QualityLadder.ReflectionRungs, d.Reflections)} " +
                      $"of {QualityLadder.ReflectionRungs.Length}, ray traced {QualityLadder.IsRayTraced(d.Reflections)}");

            GraphicsOption row = null;
            foreach (GraphicsOption option in FindObjectsByType<GraphicsOption>(FindObjectsInactive.Include))
            {
                if (option.Kind == GraphicsOptionKind.Reflections) { row = option; break; }
            }

            if (row == null)
            {
                Debug.Log(Tag + "reflections: no Reflections row in the Game scene");
                return;
            }

            var walk = new StringBuilder();
            EffectQuality first = d.Reflections;
            for (int i = 0; i < 8; i++)
            {
                row.Step(1);
                walk.Append(QualityLadder.Describe(d.Reflections)).Append(" (").Append(PlayerPrefs.GetInt(Prefix + "SSR", -1)).Append(") ");
                if (d.Reflections == first) { break; }
            }

            Debug.Log(Tag + "reflections: stepping the row from " + QualityLadder.Describe(first) + ": " + walk);
        }

        private IEnumerator Measure(string label, float runSeconds, bool boss)
        {
            if (boss)
            {
                DebugMenu menu = FindAnyObjectByType<DebugMenu>();
                if (menu == null)
                {
                    Debug.Log(Tag + "no debug menu, so no boss");
                    yield break;
                }

                menu.SkipToBossNow();
                float give = Time.realtimeSinceStartup + 30f;
                while (BossEmitter.Active == null && Time.realtimeSinceStartup < give)
                {
                    yield return null;
                }
            }

            yield return new WaitForSecondsRealtime(runSeconds);
            RunTime.SetHold(true);
            LogScene(label);

            Camera camera = Camera.main;
            if (camera == null || !camera.TryGetComponent(out HDAdditionalCameraData data))
            {
                Debug.Log(Tag + "no HDRP camera");
                RunTime.SetHold(false);
                yield break;
            }

            if (request.Fixed >= 0)
            {
                yield return Steady(label, camera);
                RunTime.SetHold(false);
                yield break;
            }

            bool custom = data.customRenderingSettings;
            var on = new List<double>();
            var off = new List<double>();
            for (int phase = 0; phase <= Cycles * 2; phase++)
            {
                bool async = phase % 2 == 0;
                SetAsync(data, custom, async);
                yield return Sample();
                (async ? on : off).Add(BenchMath.Median(samples));
                bool live = HDCamera.GetOrCreate(camera).frameSettings.IsEnabled(FrameSettingsField.AsyncCompute);
                Debug.Log(Tag + $"{label} {phase,2} async {(async ? "on " : "off")} (frame settings say {live}): " +
                          $"GPU median {BenchMath.Median(samples):0.000} mean {BenchMath.Mean(samples):0.000} ms, " +
                          $"wall median {BenchMath.Median(wall):0.000} ms, {samples.Count} frames, paused {PauseMenu.GameIsPaused}");
            }

            SetAsync(data, custom, true);
            bool wins = BenchMath.OffWins(on, off, out double saving, out double noise);
            Debug.Log(Tag + $"{label} verdict: off saves {saving:0.000} ms (median of {off.Count} pairs) against noise {noise:0.000} ms " +
                      $"(median gap between neighbouring on phases); on {BenchMath.Median(on):0.000}, off {BenchMath.Median(off):0.000}: " +
                      (wins ? "OFF WINS" : "no clear win"));

            RunTime.SetHold(false);
        }

        /// <summary>
        /// The fixed mode's holds: the same phases, with nothing switched
        /// between them, so any jump in them is the setting's own.
        /// </summary>
        private IEnumerator Steady(string label, Camera camera)
        {
            var phases = new List<double>();
            for (int phase = 0; phase < Cycles; phase++)
            {
                yield return Sample();
                phases.Add(BenchMath.Median(samples));
                bool live = HDCamera.GetOrCreate(camera).frameSettings.IsEnabled(FrameSettingsField.AsyncCompute);
                Debug.Log(Tag + $"{label} {phase,2} fixed async {(live ? "on " : "off")}: " +
                          $"GPU median {BenchMath.Median(samples):0.000} mean {BenchMath.Mean(samples):0.000} ms, " +
                          $"wall median {BenchMath.Median(wall):0.000} ms, {samples.Count} frames, paused {PauseMenu.GameIsPaused}");
            }

            Debug.Log(Tag + $"{label} fixed async {(request.Fixed == 1 ? "on" : "off")}: median of {phases.Count} phases {BenchMath.Median(phases):0.000} ms");
        }

        /// <summary>
        /// On puts the camera back exactly as the scene has it; off overrides
        /// the one frame setting, the way the editor test did.
        /// </summary>
        private static void SetAsync(HDAdditionalCameraData data, bool custom, bool async)
        {
            data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.AsyncCompute] = !async || custom;
            data.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.AsyncCompute, async);
            data.customRenderingSettings = custom || !async;
        }

        private IEnumerator Sample()
        {
            for (int i = 0; i < SettleFrames; i++)
            {
                yield return null;
            }

            samples.Clear();
            wall.Clear();
            ulong last = 0;
            int guard = SampleFrames * 4;
            while (samples.Count < SampleFrames && guard-- > 0)
            {
                yield return null;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) == 0) { continue; }
                FrameTiming t = timings[0];
                if (t.frameStartTimestamp == last || t.gpuFrameTime <= 0d) { continue; }
                last = t.frameStartTimestamp;
                samples.Add(t.gpuFrameTime);
                wall.Add(Time.unscaledDeltaTime * 1000d);
            }
        }

        private static void LogScene(string label)
        {
            int enemies = FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude).Length;
            StormRise rise = FindAnyObjectByType<StormRise>();
            Timer timer = FindAnyObjectByType<Timer>();
            string boss = "none";
            if (BossEmitter.Active != null && Camera.main != null)
            {
                Vector3 v = Camera.main.WorldToViewportPoint(BossEmitter.Active.transform.position);
                boss = $"at viewport {v.x:0.00},{v.y:0.00} depth {v.z:0}";
            }

            Debug.Log(Tag + $"{label} hold: run clock {(timer != null ? timer.Elapsed : -1f):0.0} s, {enemies} enemies, " +
                      $"storm rise {(rise != null ? rise.Rise : -1f):0.00}, boss {boss}");
        }
    }
#endif
}
