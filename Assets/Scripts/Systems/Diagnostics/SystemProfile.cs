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

            text.Append("VSync    ")
                .Append(QualitySettings.vSyncCount == 0
                    ? "off"
                    : "every " + QualitySettings.vSyncCount + " blank")
                .Append("  |  target ")
                .AppendLine(Application.targetFrameRate < 0
                    ? "uncapped"
                    : Application.targetFrameRate + " FPS");

            text.Append("Textures ")
                .Append("mipmap limit ").Append(QualitySettings.globalTextureMipmapLimit)
                .Append("  |  aniso ").AppendLine(QualitySettings.anisotropicFiltering.ToString());
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
