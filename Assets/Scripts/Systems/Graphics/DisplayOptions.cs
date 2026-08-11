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
        /// A cap that tracks whatever display the game is on, sitting just below
        /// its refresh rate rather than on it.
        ///
        /// This exists because landing *on* the refresh rate is the one place
        /// frame pacing falls apart. A frame that takes almost exactly one refresh
        /// interval alternately makes and misses the deadline, and in a borderless
        /// window - where the desktop compositor is presenting every frame - that
        /// beat is visible as regular hitching. Staying a couple of frames under
        /// removes the boundary rather than fighting it.
        ///
        /// Derived rather than typed, because a fixed 144 is the boundary on a
        /// 144 Hz panel and nowhere near it on a 165 Hz one.
        /// </summary>
        public const int MatchDisplay = -1;

        /// <summary>How far under the display's rate <see cref="MatchDisplay"/> sits.</summary>
        public const int DisplayHeadroom = 2;

        /// <summary>
        /// Frame rate caps offered, with 0 meaning uncapped.
        ///
        /// A cap below the display's rate is the cheapest way to stop a laptop
        /// cooking itself on a menu screen, which is why 30 stays on the list even
        /// though nobody chooses it for play.
        /// </summary>
        public static readonly int[] FrameRateCaps = { 0, MatchDisplay, 30, 60, 90, 120, 144, 165, 240 };

        /// <summary>
        /// The cap to actually apply, resolving <see cref="MatchDisplay"/> against
        /// the display currently in use.
        /// </summary>
        public static int ResolveCap(int cap, int refreshRate)
        {
            if (cap != MatchDisplay)
            {
                return cap;
            }

            // A display that reports nothing usable gets left uncapped rather than
            // pinned to some invented number.
            if (refreshRate <= 0)
            {
                return 0;
            }

            return refreshRate > DisplayHeadroom + 30 ? refreshRate - DisplayHeadroom : refreshRate;
        }

        public static string DescribeCap(int cap)
        {
            if (cap == MatchDisplay)
            {
                return "Just under display";
            }

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

        // ---------- sharpening ----------

        /// <summary>
        /// Names for the four anchor positions the slider passes through. Kept
        /// because a number alone says nothing about where "the HDRP default" is.
        /// </summary>
        public static readonly string[] SharpnessNames = { "Off", "Low", "Medium", "High" };

        /// <summary>Where each named level sits on the slider's 0..1 travel.</summary>
        public static float SharpnessAnchor(int level)
        {
            int last = SharpnessNames.Length - 1;
            int clamped = level < 0 ? 0 : (level > last ? last : level);
            return clamped / (float)last;
        }

        /// <summary>The slider's default: Low, because HDRP's own value sharpens twice.</summary>
        public static readonly float DefaultSharpness = SharpnessAnchor(1);

        // Anchored rather than computed so the four named positions land on
        // exactly the values they had when this was a four-item cycler. Anything
        // already tuned to Low or Medium is unchanged; the slider only fills in
        // between them.
        private static readonly float[] TaaSharpenAnchors = { 0f, 0.25f, 0.5f, 1f };
        private static readonly float[] TaaHistoryAnchors = { 0f, 0.18f, 0.35f, 0.55f };
        private static readonly float[] UpscalerAnchors = { 0f, 0.2f, 0.4f, 0.7f };

        /// <summary>Strength of TAA's post-sharpen pass. HDRP's own default is 0.5.</summary>
        public static float TaaSharpenStrength(float amount) => Sample(TaaSharpenAnchors, amount);

        /// <summary>
        /// Sharpening applied to the history TAA samples from. Separate from the
        /// pass above and on by default too - turning one down while leaving the
        /// other alone is why sharpening can seem stuck.
        /// </summary>
        public static float TaaHistorySharpening(float amount) => Sample(TaaHistoryAnchors, amount);

        /// <summary>
        /// Sharpening for FSR, which does its own rather than using TAA's. Zero
        /// means the caller disables the pass outright.
        /// </summary>
        public static float UpscalerSharpness(float amount) => Sample(UpscalerAnchors, amount);

        /// <summary>
        /// Reads a 0..1 position off a curve defined by its anchor points.
        ///
        /// Piecewise rather than a single lerp because the anchors are not evenly
        /// spaced - TAA's post-sharpen doubles between Medium and High while the
        /// history pass barely moves, and a straight interpolation between the
        /// ends would misrepresent both.
        /// </summary>
        private static float Sample(float[] anchors, float amount)
        {
            if (amount <= 0f)
            {
                return anchors[0];
            }

            int last = anchors.Length - 1;
            if (amount >= 1f)
            {
                return anchors[last];
            }

            float position = amount * last;
            int index = (int)position;
            float within = position - index;

            return anchors[index] + (anchors[index + 1] - anchors[index]) * within;
        }

        /// <summary>
        /// The slider's value as a percentage, naming the level when it is sitting
        /// on one - "50%  (Medium)" reads better than either half alone.
        /// </summary>
        public static string DescribeSharpness(float amount)
        {
            int percent = (int)(amount * 100f + 0.5f);

            for (int i = 0; i < SharpnessNames.Length; i++)
            {
                if (amount >= SharpnessAnchor(i) - 0.005f && amount <= SharpnessAnchor(i) + 0.005f)
                {
                    return percent + "%  (" + SharpnessNames[i] + ")";
                }
            }

            return percent + "%";
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
