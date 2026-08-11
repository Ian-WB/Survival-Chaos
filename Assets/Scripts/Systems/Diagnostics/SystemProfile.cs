using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos
{
    /// <summary>
    /// Describes the machine the build is running on and the graphics settings
    /// it is running with, so a performance figure sent back by a tester can be
    /// read in context. A frame rate with no hardware next to it says nothing.
    ///
    /// Deliberately free of any render-pipeline package reference: it reports the
    /// pipeline asset by name rather than reaching into HDRP types, so this file
    /// survives a merge back to the URP branch unchanged.
    /// </summary>
    public static class SystemProfile
    {
        /// <summary>Turns a byte count into something readable, e.g. "1.4 GB".</summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 0)
            {
                return "n/a";
            }

            const long Kilo = 1024L;
            const long Mega = Kilo * 1024L;
            const long Giga = Mega * 1024L;

            if (bytes >= Giga)
            {
                return (bytes / (float)Giga).ToString("0.00") + " GB";
            }

            if (bytes >= Mega)
            {
                return (bytes / (float)Mega).ToString("0") + " MB";
            }

            return (bytes / (float)Kilo).ToString("0") + " KB";
        }

        /// <summary>The name of the active pipeline asset, or "Built-in".</summary>
        public static string RenderPipeline
        {
            get
            {
                RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
                return asset == null ? "Built-in" : asset.name;
            }
        }

        public static string QualityLevel
        {
            get
            {
                string[] names = QualitySettings.names;
                int level = QualitySettings.GetQualityLevel();
                return level >= 0 && level < names.Length ? names[level] : level.ToString();
            }
        }

        /// <summary>Hardware, in the order a reader cares about it.</summary>
        public static void AppendHardware(StringBuilder text)
        {
            text.Append("GPU      ").AppendLine(SystemInfo.graphicsDeviceName);
            text.Append("         ")
                .Append(SystemInfo.graphicsDeviceType)
                .Append("  |  ")
                .Append(SystemInfo.graphicsMemorySize)
                .AppendLine(" MB VRAM");
            text.Append("CPU      ").AppendLine(SystemInfo.processorType);
            text.Append("         ")
                .Append(SystemInfo.processorCount)
                .Append(" threads  |  ")
                .Append(SystemInfo.systemMemorySize)
                .AppendLine(" MB RAM");
            text.Append("OS       ").AppendLine(SystemInfo.operatingSystem);
        }

        /// <summary>
        /// The graphics settings that actually apply.
        ///
        /// Shadow distance, shadow resolution and anti-aliasing are not listed:
        /// under a scriptable pipeline those come from the pipeline asset, and the
        /// QualitySettings values sit there unused. Printing them would be worse
        /// than printing nothing, because they look authoritative.
        /// </summary>
        public static void AppendGraphicsSettings(StringBuilder text)
        {
            text.Append("Pipeline ").AppendLine(RenderPipeline);
            text.Append("Quality  ").AppendLine(QualityLevel);

            text.Append("Window   ")
                .Append(Screen.width).Append(" x ").Append(Screen.height)
                .Append("  ").Append(Screen.fullScreenMode)
                .Append("  @ ").Append(Screen.currentResolution.refreshRateRatio.value.ToString("0"))
                .AppendLine(" Hz");

            AppendRenderResolution(text);

            text.Append("VSync    ")
                .Append(QualitySettings.vSyncCount == 0
                    ? "off"
                    : "every " + QualitySettings.vSyncCount + " blank")
                .Append("  |  target ")
                .AppendLine(Application.targetFrameRate < 0
                    ? "uncapped"
                    : Application.targetFrameRate + " FPS");

            AppendReconstruction(text);

            text.Append("Textures ")
                .Append("mipmap limit ").Append(QualitySettings.globalTextureMipmapLimit)
                .Append("  |  aniso ").AppendLine(QualitySettings.anisotropicFiltering.ToString());
        }

        /// <summary>
        /// What the game is actually rendering at, as opposed to the window it
        /// ends up in.
        ///
        /// The window size on its own answers the wrong question once an upscaler
        /// or a render scale is in play - a tester reporting 1280x720 may be
        /// rendering 850x478 and reading the difference as the game looking soft.
        /// Taken from the live resolved scale rather than the requested one, so it
        /// reflects whatever the upscaler negotiated for itself.
        /// </summary>
        private static void AppendRenderResolution(StringBuilder text)
        {
            // Asked of GraphicsDirector rather than the pipeline directly, so this
            // file keeps its promise not to name a render-pipeline package type.
            GraphicsDirector director = GraphicsDirector.Instance;
            if (director == null)
            {
                return;
            }

            float asked = Mathf.Clamp(director.EffectiveRenderScale, 0.05f, 1f);
            float resolved = director.ActualRenderScale;

            int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * asked));
            int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * asked));

            text.Append("Render   ").Append(width).Append(" x ").Append(height)
                .Append("  (").Append(Mathf.RoundToInt(asked * 100f)).Append("% asked");

            // Both numbers, because they can legitimately disagree and a single
            // figure hides which one is wrong. The pipeline only recomputes its
            // scale when the active camera changes, so a stale resolved value next
            // to a moving asked value is itself the diagnosis - and an upscaler
            // negotiating its own scale shows up here too.
            if (Mathf.Abs(resolved - asked) > 0.02f)
            {
                text.Append(", ").Append(Mathf.RoundToInt(resolved * 100f)).Append("% resolved");
            }

            text.AppendLine(")");
        }

        /// <summary>
        /// Which reconstruction is running, and how hard.
        ///
        /// Without this a report cannot distinguish "slow at native" from "slow
        /// even with FSR Performance", which are different problems with different
        /// answers. Reads the player's settings rather than the pipeline asset,
        /// because the per-camera fields are what actually take effect.
        /// </summary>
        private static void AppendReconstruction(StringBuilder text)
        {
            GraphicsDirector director = GraphicsDirector.Instance;
            if (director == null)
            {
                return;
            }

            text.Append("Upscaler ")
                .Append(DisplayOptions.UpscaleMethodNames[(int)director.Method]);

            if (director.Method != UpscaleMethod.Off)
            {
                text.Append(' ').Append(DisplayOptions.UpscaleQualityNames[(int)director.Quality]);
            }

            text.Append("  |  AA ")
                .Append(DisplayOptions.AntiAliasingNames[(int)director.AntiAliasing])
                .Append("  |  sharpness ")
                .AppendLine(DisplayOptions.DescribeSharpness(director.Sharpness));
        }

        /// <summary>What produced this build, so results can be told apart.</summary>
        public static void AppendBuild(StringBuilder text)
        {
            text.Append("Build    ").Append(Application.productName)
                .Append(' ').AppendLine(Application.version);
            text.Append("         Unity ").Append(Application.unityVersion)
                .Append("  |  ").Append(Application.platform)
                .Append("  |  ")
#if ENABLE_IL2CPP
                .Append("IL2CPP")
#else
                .Append("Mono")
#endif
#if DEVELOPMENT_BUILD
                .AppendLine("  |  development build");
#else
                .AppendLine("  |  release build");
#endif
        }
    }
}
