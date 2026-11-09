using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace SurvivalChaos
{
    /// <summary>
    /// Builds the GPU's pipeline states on worker threads so the first launch
    /// does not freeze on the title or when entering the arena.
    ///
    /// Under DirectX 12 every shader pass meeting a new mesh layout and render
    /// target needs a pipeline state, and the driver builds each one on the
    /// render thread the first time it is drawn. The first launch on a machine
    /// has none cached, and the title froze for about 11 seconds on the
    /// owner's PC while it built them one after another (9 November 2026 scan;
    /// 1.9 s under DirectX 11). Even with the driver cache warm, a launch
    /// without pre-warmed states still stalls the first title frame for 1-2 s
    /// while DirectX 12 instantiates pipeline states on the render thread.
    ///
    /// Blocking on the whole collection in BeforeSceneLoad would trade that
    /// render-thread freeze for a main-thread one while the arena's states
    /// compile before the title has even drawn. Instead, BeforeSceneLoad kicks
    /// off the first batch (which covers the 27 states Menu draws before Game
    /// loads) on worker threads while the main thread loads Menu, completes
    /// that batch in LateUpdate just before the first frame renders, and then
    /// warms the remaining arena states progressively across frames with
    /// WarmUpProgressively so no frame blocks waiting on worker jobs. If a
    /// scene load starts before all batches finish, LoadingScreen holds until
    /// Warming clears.
    ///
    /// The list is recorded, not guessed: launch a development build with
    /// -psoTrace, play, quit, and the states the game actually created are
    /// saved beside hitches.log. Copied into StreamingAssets/PipelineStates,
    /// one file per graphics API and quality level, they ship with the next
    /// build.
    /// </summary>
    public static class PipelineStateWarmup
    {
        private const string TraceArgument = "-psoTrace";
        private const string FolderName = "PipelineStates";

        // Menu draws 27 states and sits at the start of a trace recorded from
        // launch; scheduling 32 in BeforeSceneLoad lets worker threads build
        // all of Menu's states while the Menu scene loads on the main thread.
        private const int InitialStatesPerStep = 32;

        // Progressive batch size for the remaining arena states while the
        // title is interactive.
        private const int StatesPerStep = 16;

        // Held so the collections outlive the session that warmed them.
        private static readonly List<GraphicsStateCollection> Warmed = new List<GraphicsStateCollection>();
        private static readonly Queue<PendingCollection> Pending = new Queue<PendingCollection>();

        private static GraphicsStateCollection trace;
        private static PendingCollection active;
        private static JobHandle activeHandle;
        private static bool waitingForInitialBatch;

        /// <summary>True while progressive pipeline state warm-up still has batches in flight.</summary>
        public static bool Warming { get; private set; }

        private struct PendingCollection
        {
            public string FileName;
            public GraphicsStateCollection Collection;
            public Stopwatch Clock;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            if (waitingForInitialBatch || Warming)
            {
                activeHandle.Complete();
            }

            Warmed.Clear();
            Pending.Clear();
            trace = null;
            active = default;
            activeHandle = default;
            waitingForInitialBatch = false;
            Warming = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Begin()
        {
            // The editor builds its own states as it draws, and a trace taken
            // there would describe the editor rather than the game.
            if (Application.isEditor)
            {
                return;
            }

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), TraceArgument) >= 0)
            {
                StartTrace();
                return;
            }

            WarmUp();
        }

        private static void WarmUp()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, FolderName);
            if (!Directory.Exists(folder))
            {
                return;
            }

            // Only the quality level the game starts on. Unity restores the
            // player's last level before this runs (a fresh install starts on
            // Medium), and another level's states are of no use to this
            // session: warming Ultra's on a Medium start held the loading screen
            // about 0.4 s longer for nothing (9 November 2026 cold test). A
            // level with no recording simply warms nothing, as before.
            string file = Path.Combine(folder, CurrentCollectionFileName());
            if (File.Exists(file))
            {
                var collection = new GraphicsStateCollection();
                collection.hideFlags = HideFlags.DontUnloadUnusedAsset;
                if (!collection.LoadFromFile(file) ||
                    collection.graphicsDeviceType != SystemInfo.graphicsDeviceType ||
                    collection.totalGraphicsStateCount <= 0)
                {
                    Object.Destroy(collection);
                }
                else
                {
                    Warmed.Add(collection);
                    Pending.Enqueue(new PendingCollection
                    {
                        FileName = Path.GetFileName(file),
                        Collection = collection
                    });
                }
            }

            if (Pending.Count == 0)
            {
                return;
            }

            Warming = true;
            active = Pending.Dequeue();
            active.Clock = Stopwatch.StartNew();

            // Schedule the initial Menu batch now without completing it on the
            // main thread, so worker threads compile the title's states in
            // parallel with the Menu scene load.
            activeHandle = active.Collection.WarmUpProgressively(InitialStatesPerStep);
            JobHandle.ScheduleBatchedJobs();
            waitingForInitialBatch = true;

            var host = new GameObject("Pipeline State Warmup");
            host.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        private static bool StepWarmUp()
        {
            if (!Warming || active.Collection == null)
            {
                Warming = false;
                return false;
            }

            // On the very first frame's LateUpdate (just before Menu draws its
            // first frame), finish the initial batch that was scheduled in
            // BeforeSceneLoad so Menu's states are ready before rendering.
            // On subsequent frames, only complete when the worker jobs have
            // already finished, unless the loading screen is covering the view.
            if (waitingForInitialBatch)
            {
                waitingForInitialBatch = false;
                activeHandle.Complete();
            }
            else if (LoadingScreen.Busy)
            {
                activeHandle.Complete();
            }
            else if (!activeHandle.IsCompleted)
            {
                return true;
            }
            else
            {
                activeHandle.Complete();
            }

            while (active.Collection != null && active.Collection.isWarmedUp)
            {
                active.Clock.Stop();
                Debug.Log("[PipelineWarmup] " + active.FileName + ": " + active.Collection.totalGraphicsStateCount
                    + " states in " + active.Clock.ElapsedMilliseconds + " ms");

                if (Pending.Count == 0)
                {
                    active = default;
                    Warming = false;
                    return false;
                }

                active = Pending.Dequeue();
                active.Clock = Stopwatch.StartNew();
            }

            int batch = LoadingScreen.Busy ? InitialStatesPerStep : StatesPerStep;
            activeHandle = active.Collection.WarmUpProgressively(batch, activeHandle);
            JobHandle.ScheduleBatchedJobs();
            return true;
        }

        private static void StartTrace()
        {
            trace = new GraphicsStateCollection();
            trace.hideFlags = HideFlags.DontUnloadUnusedAsset;

            // If a collection for this API and quality level already ships in
            // StreamingAssets, load it before BeginTrace so newly encountered
            // states are added to the existing collection instead of starting
            // from zero.
            string fileName = CurrentCollectionFileName();
            string shipped = Path.Combine(Application.streamingAssetsPath, FolderName, fileName);
            int preloaded = 0;
            if (File.Exists(shipped) && trace.LoadFromFile(shipped) &&
                trace.graphicsDeviceType == SystemInfo.graphicsDeviceType)
            {
                preloaded = trace.totalGraphicsStateCount;
            }

            bool started = trace.BeginTrace();
            SceneManager.sceneLoaded += OnSceneLoadedDuringTrace;
            Application.quitting += SaveTrace;
            Debug.Log("[PipelineWarmup] tracing pipeline states until quit (started=" + started
                + ", preloaded=" + preloaded + ")");
        }

        private static void OnSceneLoadedDuringTrace(Scene scene, LoadSceneMode mode)
        {
            if (trace != null)
            {
                Debug.Log("[PipelineWarmup] scene " + scene.name + " loaded ("
                    + trace.totalGraphicsStateCount + " states traced so far)");
            }
        }

        private static void SaveTrace()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedDuringTrace;
            if (trace == null)
            {
                return;
            }

            trace.EndTrace();
            string path = Path.Combine(Application.persistentDataPath, CurrentCollectionFileName());
            trace.SaveToFile(path);
            Debug.Log("[PipelineWarmup] saved " + trace.totalGraphicsStateCount + " states ("
                + trace.variantCount + " variants) to " + path);
            trace = null;
        }

        private static string CurrentCollectionFileName()
        {
            return SystemInfo.graphicsDeviceType + "_"
                + QualitySettings.names[QualitySettings.GetQualityLevel()] + ".graphicsstate";
        }

        private sealed class Runner : MonoBehaviour
        {
            private void LateUpdate()
            {
                if (!StepWarmUp())
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
