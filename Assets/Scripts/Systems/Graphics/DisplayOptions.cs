using System.Collections.Generic;

namespace SurvivalChaos
{
    /// <summary>
    /// Which reconstruction technique renders the frame, if any.
    ///
    /// Separate from <see cref="UpscaleQuality"/> because they are separate
    /// questions: which upscaler a machine can run is decided by its hardware,
    /// and how hard to push it is decided by taste. A single combined list made
    /// changing one mean re-finding the other.
    /// </summary>
    public enum UpscaleMethod
    {
        Off = 0,
        Fsr = 1,
        Dlss = 2
    }

    /// <summary>How far below native the upscaler renders. Best image first.</summary>
    public enum UpscaleQuality
    {
        Quality = 0,
        Balanced = 1,
        Performance = 2,
        UltraPerformance = 3
    }

    /// <summary>
    /// How edges are resolved when no upscaler is doing it instead.
    ///
    /// DLAA is in this list rather than the upscale list because that is what it
    /// is — DLSS at 100% scale, spending its budget on anti-aliasing rather than
    /// on reconstruction. There is deliberately no FSR equivalent: Unity's
    /// <c>AMD.FSR2Quality</c> has no native-resolution mode to expose.
    /// </summary>
    public enum AntiAliasingMode
    {
        Off = 0,
        Fxaa = 1,
        Smaa = 2,
        Taa = 3,
        Dlaa = 4
    }

    /// <summary>One selectable screen size.</summary>
    public readonly struct DisplaySize
    {
        public readonly int Width;
        public readonly int Height;

        public DisplaySize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return Width + " x " + Height;
        }

        public bool Equals(DisplaySize other)
        {
            return Width == other.Width && Height == other.Height;
        }
    }

    /// <summary>
    /// Turns the raw list of modes a monitor reports into the one a person should
    /// be offered.
    ///
    /// Unity reports every refresh rate separately, so a 165 Hz monitor returns
    /// the same handful of sizes eight or nine times over. Presented unfiltered
    /// that is a list of forty entries where four are distinct — the player has to
    /// scroll past duplicates to find anything.
    ///
    /// Kept free of UnityEngine so the filtering can be tested against awkward
    /// input rather than against whatever monitor happens to be attached.
    /// </summary>
    public static class DisplayOptions
    {
        /// <summary>Below this, the interface stops being usable at all.</summary>
        public const int MinimumWidth = 1024;
        public const int MinimumHeight = 576;

        /// <summary>
        /// Distinct sizes, smallest first, with anything unusably small dropped.
        /// Refresh rate is deliberately not offered: it belongs to the display,
        /// and VSync plus the frame cap already cover what a player wants from it.
        /// </summary>
        public static List<DisplaySize> Distinct(IEnumerable<DisplaySize> reported)
        {
            List<DisplaySize> result = new List<DisplaySize>();

            if (reported == null)
            {
                return result;
            }

            foreach (DisplaySize size in reported)
            {
                if (size.Width < MinimumWidth || size.Height < MinimumHeight)
                {
                    continue;
                }

                if (Contains(result, size))
                {
                    continue;
                }

                result.Add(size);
            }

            result.Sort((a, b) =>
            {
                int byWidth = a.Width.CompareTo(b.Width);
                return byWidth != 0 ? byWidth : a.Height.CompareTo(b.Height);
            });

            return result;
        }

        private static bool Contains(List<DisplaySize> list, DisplaySize size)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Equals(size))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The index of <paramref name="wanted"/>, or the closest smaller entry.
        ///
        /// A saved resolution can vanish — a different monitor, a driver change —
        /// and falling back to the nearest size the display actually supports is
        /// better than either failing or resetting to the largest available.
        /// </summary>
        public static int IndexOf(List<DisplaySize> sizes, DisplaySize wanted)
        {
            if (sizes == null || sizes.Count == 0)
            {
                return -1;
            }

            for (int i = 0; i < sizes.Count; i++)
            {
                if (sizes[i].Equals(wanted))
                {
                    return i;
                }
            }

            int best = 0;
            long target = (long)wanted.Width * wanted.Height;

            for (int i = 0; i < sizes.Count; i++)
            {
                long area = (long)sizes[i].Width * sizes[i].Height;
                if (area <= target)
                {
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// Frame rate caps offered, with 0 meaning uncapped.
        ///
        /// A cap below the display's rate is the cheapest way to stop a laptop
        /// cooking itself on a menu screen, which is why 30 stays on the list even
        /// though nobody chooses it for play.
        /// </summary>
        public static readonly int[] FrameRateCaps = { 0, 30, 60, 90, 120, 144, 165, 240 };

        public static string DescribeCap(int cap)
        {
            return cap <= 0 ? "Uncapped" : cap + " FPS";
        }

        // ---------- upscaling ----------

        /// <summary>
        /// Which reconstruction runs, not how hard it pushes. The two were one
        /// nine-item list once, which meant "DLSS Balanced" and "FSR Balanced"
        /// were unrelated entries a player had to scroll between to compare.
        /// </summary>
        public static readonly string[] UpscaleMethodNames = { "Off", "FSR", "DLSS" };

        /// <summary>
        /// How hard the upscaler pushes, in the order every other game lists it:
        /// best image first, cheapest last.
        /// </summary>
        public static readonly string[] UpscaleQualityNames =
        {
            "Quality", "Balanced", "Performance", "Ultra Performance"
        };

        /// <summary>
        /// DLAA sits at the end because it is the most expensive, not because it
        /// is a kind of upscaling: it is DLSS rendering at full resolution, doing
        /// only the anti-aliasing half of its job. It belongs here rather than in
        /// the upscale list for the same reason — it makes the image cost more,
        /// not less.
        /// </summary>
        public static readonly string[] AntiAliasingNames =
        {
            "Off", "FXAA", "SMAA", "TAA", "DLAA"
        };

        /// <summary>
        /// The quality value to hand HDRP for DLSS.
        ///
        /// Unity's <c>NVIDIA.DLSSQuality</c> is not ordered the way a person would
        /// order it — MaximumPerformance is 0 and MaximumQuality is 2 — so casting
        /// the player's choice straight across gives them the opposite of what they
        /// picked. Verified against the enum ordering HDRP itself relies on in
        /// HDRenderPipelineUI.Skin.cs.
        ///
        /// Returned as a raw number because that is what
        /// <c>HDAdditionalCameraData.deepLearningSuperSamplingQuality</c> takes;
        /// naming the type would drag in the NVIDIA module, which is not present
        /// on every platform this has to compile for.
        /// </summary>
        public const uint DlssMaximumPerformance = 0;
        public const uint DlssBalanced = 1;
        public const uint DlssMaximumQuality = 2;
        public const uint DlssUltraPerformance = 3;
        public const uint DlssDlaa = 4;

        public static uint DlssQualityValue(int quality)
        {
            switch (quality)
            {
                case 0: return DlssMaximumQuality;
                case 1: return DlssBalanced;
                case 2: return DlssMaximumPerformance;
                default: return DlssUltraPerformance;
            }
        }

        /// <summary>
        /// The quality value to hand HDRP for FSR2.
        ///
        /// <c>AMD.FSR2Quality</c> happens to run best-to-cheapest already, so this
        /// is the identity — but it is written out rather than assumed, because the
        /// two vendors disagreeing about ordering is exactly the trap above.
        /// </summary>
        public static uint Fsr2QualityValue(int quality)
        {
            if (quality < 0)
            {
                return 0;
            }

            int last = UpscaleQualityNames.Length - 1;
            return (uint)(quality > last ? last : quality);
        }

        /// <summary>
        /// Roughly what fraction of native each quality mode renders at, for the
        /// Render Scale row to display while an upscaler owns the number.
        ///
        /// Approximate on purpose: the upscalers negotiate their own exact scale
        /// with the driver, so this is what to *show*, never what to apply.
        /// </summary>
        public static float ApproximateScale(int quality)
        {
            switch (quality)
            {
                case 0: return 0.67f;
                case 1: return 0.58f;
                case 2: return 0.5f;
                default: return 0.33f;
            }
        }
    }
}
