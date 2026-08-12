using System;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// An on-screen performance readout that works in a normal, non-development
    /// build, so testers can report how the game runs on their own hardware.
    ///
    /// It creates itself - there is nothing to place in a scene and nothing to
    /// wire up, which matters because a tester cannot be asked to set anything
    /// up. It starts hidden and is toggled with F3.
    ///
    /// Some of what it reports is only available on some machines and some
    /// graphics APIs. Everything here degrades to "n/a" rather than reporting a
    /// zero, because a confident zero in a performance report is worse than an
    /// admission of ignorance.
    ///
    /// Define SURVIVAL_CHAOS_NO_OVERLAY to leave it out of a build entirely.
    /// </summary>
    public sealed class PerformanceOverlay : MonoBehaviour
    {
        /// <summary>Frames held for the rolling statistics - about 17s at 60 FPS.</summary>
        private const int WindowSize = 1024;

        /// <summary>
        /// How often the readout is rebuilt. Every frame would be unreadable and
        /// would put this overlay's own string building into the numbers it is
        /// measuring; four times a second is legible and nearly free.
        /// </summary>
        private const float RefreshInterval = 0.25f;

        private const float NoticeDuration = 4f;

        private readonly FrameTimeStats stats = new FrameTimeStats(WindowSize);
        private readonly StringBuilder builder = new StringBuilder(1024);

        private bool visible;
        private float nextRefresh;

        // Held rather than rebuilt per repaint. An overlay that allocates every
        // frame would show up in the very measurements it exists to report.
        private readonly GUIContent readout = new GUIContent();
        private readonly GUIContent notice = new GUIContent();
        private Vector2 readoutSize;
        private Vector2 noticeSize;
        private bool layoutDirty = true;

        private float noticeUntil;

        // Frame timings, in milliseconds, when the platform supplies them.
        private FrameTiming[] timings = new FrameTiming[1];
        private double cpuFrameMs = -1d;
        private double gpuFrameMs = -1d;

        private ProfilerRecorder systemMemory;
        private ProfilerRecorder graphicsMemory;

        private System.Diagnostics.Process process;
        private TimeSpan lastProcessorTime;
        private float lastProcessorSampleTime;
        private float processorPercent = -1f;
        private long workingSet = -1L;

        private GUIStyle style;
        private Texture2D background;

#if !SURVIVAL_CHAOS_NO_OVERLAY
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            GameObject host = new GameObject("Performance Overlay");
            host.AddComponent<PerformanceOverlay>();
            DontDestroyOnLoad(host);
        }
#endif

        private void Awake()
        {
            // IMGUI allocates a layout context per OnGUI event unless this is
            // off, and this component is DontDestroyOnLoad - so that cost was
            // being paid every frame by every player whether or not F3 was ever
            // pressed. The early return inside OnGUI cannot help: the allocation
            // happens before the callback is reached. Nothing here uses
            // GUILayout, so there is nothing to give up.
            useGUILayout = false;

            // Counter names have shifted between Unity versions, and not every
            // counter is exposed to a release player. Try the known spellings and
            // use whichever the runtime actually recognises.
            systemMemory = TryRecord(ProfilerCategory.Memory, "System Used Memory", "Total Used Memory");
            graphicsMemory = TryRecord(ProfilerCategory.Memory, "Gfx Used Memory", "Graphics Used Memory");

            try
            {
                process = System.Diagnostics.Process.GetCurrentProcess();
                lastProcessorTime = process.TotalProcessorTime;
                lastProcessorSampleTime = Time.realtimeSinceStartup;
            }
            catch (Exception)
            {
                // Restricted platforms and some scripting backends refuse this.
                // Frame times below are the better measure anyway.
                process = null;
            }
        }

        private static ProfilerRecorder TryRecord(ProfilerCategory category, params string[] names)
        {
            foreach (string name in names)
            {
                try
                {
                    ProfilerRecorder recorder = ProfilerRecorder.StartNew(category, name);
                    if (recorder.Valid)
                    {
                        return recorder;
                    }

                    recorder.Dispose();
                }
                catch (Exception)
                {
                    // Unknown counter on this version; try the next spelling.
                }
            }

            return default;
        }

        private void OnDestroy()
        {
            if (systemMemory.Valid)
            {
                systemMemory.Dispose();
            }

            if (graphicsMemory.Valid)
            {
                graphicsMemory.Dispose();
            }

            if (background != null)
            {
                Destroy(background);
            }
        }

        private void Update()
        {
            stats.Add(Time.unscaledDeltaTime);

            if (GameInput.DebugOverlayTogglePressed)
            {
                visible = !visible;
                nextRefresh = 0f;
            }

            if (visible && GameInput.DebugCopyReportPressed)
            {
                ShareReport();
            }

            if (!visible || Time.unscaledTime < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.unscaledTime + RefreshInterval;
            SampleCounters();
            readout.text = BuildReadout();
            layoutDirty = true;
        }

        private void SampleCounters()
        {
            // Frame Timing Stats must be enabled in Player Settings for this to
            // return anything; without it both values stay unavailable.
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
            {
                cpuFrameMs = timings[0].cpuFrameTime;
                gpuFrameMs = timings[0].gpuFrameTime;
            }

            if (process == null)
            {
                return;
            }

            try
            {
                process.Refresh();

                // Zero is not a measurement. Some Mono builds and restricted
                // platforms answer WorkingSet64 with 0 rather than failing, and
                // "Memory 0 KB process" in a report reads as a real figure - the
                // one thing this overlay exists not to do. A process that is
                // running has a working set, so 0 means the counter is
                // unavailable and n/a is the honest answer.
                long resident = process.WorkingSet64;
                workingSet = resident > 0L ? resident : -1L;

                TimeSpan processorTime = process.TotalProcessorTime;
                float now = Time.realtimeSinceStartup;
                float elapsed = now - lastProcessorSampleTime;

                if (elapsed > 0f)
                {
                    double used = (processorTime - lastProcessorTime).TotalSeconds;
                    processorPercent = (float)(used / (elapsed * SystemInfo.processorCount) * 100d);
                    lastProcessorTime = processorTime;
                    lastProcessorSampleTime = now;
                }
            }
            catch (Exception)
            {
                process = null;
                workingSet = -1L;
                processorPercent = -1f;
            }
        }

        private string BuildReadout()
        {
            builder.Clear();

            builder.AppendLine("SURVIVAL CHAOS - PERFORMANCE        F3 hide   F4 copy report");
            builder.AppendLine();

            AppendRate(builder, "FPS avg ", stats.AverageFps, stats.AverageMs);
            AppendRate(builder, "1% low  ", stats.LowFps(0.01f), stats.LowMs(0.01f));
            AppendRate(builder, "0.1% low", stats.LowFps(0.001f), stats.LowMs(0.001f));
            AppendRate(builder, "worst   ", 0f, stats.WorstMs);
            builder.Append("        over ").Append(stats.Count).AppendLine(" frames");
            builder.AppendLine();

            builder.Append("CPU      ").Append(Milliseconds(cpuFrameMs));
            if (processorPercent >= 0f)
            {
                builder.Append("  |  ").Append(processorPercent.ToString("0")).Append("% of all cores");
            }

            builder.AppendLine();
            builder.Append("GPU      ").AppendLine(Milliseconds(gpuFrameMs));

            builder.Append("Memory   ")
                .Append(workingSet >= 0L ? SystemProfile.FormatBytes(workingSet) : "n/a")
                .Append(" process  |  ")
                .Append(SystemProfile.FormatBytes(GC.GetTotalMemory(false)))
                .AppendLine(" managed");

            builder.Append("VRAM     ")
                .Append(graphicsMemory.Valid ? SystemProfile.FormatBytes(graphicsMemory.LastValue) : "n/a")
                .Append(" used of ")
                .Append(SystemInfo.graphicsMemorySize)
                .AppendLine(" MB");

            if (systemMemory.Valid)
            {
                builder.Append("Unity    ")
                    .Append(SystemProfile.FormatBytes(systemMemory.LastValue))
                    .AppendLine(" total");
            }

            AppendVSyncVerdict(builder);

            builder.AppendLine();
            SystemProfile.AppendGraphicsSettings(builder);
            builder.AppendLine();
            SystemProfile.AppendHardware(builder);

            return builder.ToString();
        }

        /// <summary>
        /// Says so when VSync is what is holding the frame rate down.
        ///
        /// This is the difference between "your machine is too slow" and "your
        /// machine is one toggle away from being faster", and a tester cannot tell
        /// them apart from a frame rate alone. The tell is a frame time sitting on
        /// an exact multiple of the refresh interval while the GPU is measurably
        /// finishing sooner - the spare time is spent waiting for a vblank that
        /// has already been missed.
        /// </summary>
        private void AppendVSyncVerdict(StringBuilder text)
        {
            if (QualitySettings.vSyncCount <= 0)
            {
                return;
            }

            float refreshHz = (float)Screen.currentResolution.refreshRateRatio.value;
            float averageMs = stats.AverageMs;
            int multiple = FrameTimeStats.VSyncMultiple(averageMs, refreshHz);

            if (multiple < 2)
            {
                return;
            }

            text.AppendLine();
            text.Append("VSync is holding this to 1/").Append(multiple)
                .Append(" of ").Append(refreshHz.ToString("0")).Append(" Hz  (")
                .Append((refreshHz / multiple).ToString("0")).AppendLine(" FPS)");

            // Only worth suggesting when there is headroom to reclaim. A GPU
            // already at the frame time is not being held back by anything.
            if (gpuFrameMs > 0d && gpuFrameMs < averageMs * 0.9d)
            {
                text.Append("  GPU finishes in ").Append(Milliseconds(gpuFrameMs))
                    .Append(" - VSync off should give about ")
                    .Append((1000d / gpuFrameMs).ToString("0")).AppendLine(" FPS");
            }
        }

        private static void AppendRate(StringBuilder text, string label, float fps, float milliseconds)
        {
            text.Append(label).Append("  ");

            if (fps > 0f)
            {
                text.Append(fps.ToString("0").PadLeft(4)).Append("     ");
            }
            else
            {
                text.Append("          ");
            }

            text.Append(milliseconds.ToString("0.0")).AppendLine(" ms");
        }

        private static string Milliseconds(double value)
        {
            // Frame Timing Stats is off, or this graphics API does not report it.
            return value < 0d ? "n/a  (enable Frame Timing Stats)" : value.ToString("0.0") + " ms";
        }

        /// <summary>
        /// Puts the full report on the clipboard and writes it to a file, so a
        /// tester can paste it back without needing to retype a screenshot.
        /// </summary>
        private void ShareReport()
        {
            StringBuilder report = new StringBuilder(1024);
            SystemProfile.AppendBuild(report);
            report.AppendLine();
            report.Append(BuildReadout());

            string text = report.ToString();
            string path = null;

            try
            {
                GUIUtility.systemCopyBuffer = text;
            }
            catch (Exception)
            {
                // Clipboard is unavailable on some platforms; the file still works.
            }

            try
            {
                path = Path.Combine(Application.persistentDataPath, "survival-chaos-performance.txt");
                File.WriteAllText(path, text);
            }
            catch (Exception)
            {
                path = null;
            }

            notice.text = path != null
                ? "Copied to clipboard, and saved to\n" + path
                : "Copied to clipboard.";
            noticeUntil = Time.unscaledTime + NoticeDuration;
            layoutDirty = true;
        }

        private void OnGUI()
        {
            if (!visible || Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureStyle();

            float margin = 12f * Mathf.Max(1f, Screen.height / 1080f);

            // Measuring text is not free, so it happens when the text changes -
            // four times a second - rather than on every repaint.
            if (layoutDirty)
            {
                readoutSize = style.CalcSize(readout);
                noticeSize = style.CalcSize(notice);
                layoutDirty = false;
            }

            Rect panel = new Rect(margin, margin, readoutSize.x + margin * 2f, readoutSize.y + margin * 2f);
            GUI.DrawTexture(panel, background);
            style.Draw(new Rect(panel.x + margin, panel.y + margin, readoutSize.x, readoutSize.y),
                readout, false, false, false, false);

            if (Time.unscaledTime >= noticeUntil || notice.text.Length == 0)
            {
                return;
            }

            Rect toast = new Rect(margin, panel.yMax + margin, noticeSize.x + margin * 2f, noticeSize.y + margin * 2f);
            GUI.DrawTexture(toast, background);
            style.Draw(new Rect(toast.x + margin, toast.y + margin, noticeSize.x, noticeSize.y),
                notice, false, false, false, false);
        }

        private void EnsureStyle()
        {
            if (style != null && background != null)
            {
                return;
            }

            background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.78f));
            background.Apply();
            background.hideFlags = HideFlags.HideAndDontSave;

            style = new GUIStyle(GUI.skin.label)
            {
                font = Font.CreateDynamicFontFromOSFont("Consolas", 14),
                fontSize = Mathf.RoundToInt(14f * Mathf.Max(1f, Screen.height / 1080f)),
                richText = false,
                wordWrap = false,
                alignment = TextAnchor.UpperLeft
            };

            style.normal.textColor = Color.white;
        }
    }
}
