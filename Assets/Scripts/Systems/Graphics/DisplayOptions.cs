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
    ///
    /// The numbers are what a player's settings file stores, so they never
    /// move: FSR is 3 because it came last, and 1 stays reserved for the FSR 2
    /// it replaced.
    /// </summary>
    public enum UpscaleMethod
    {
        Off = 0,

        /// <summary>
        /// Unity's own FSR 2, retired for <see cref="Fsr3"/> on 29 Sep 2026.
        /// Never offered or run; a settings file that stored it reads as FSR 3
        /// (see <see cref="DisplayOptions.StoredUpscaleMethod"/>).
        /// </summary>
        RetiredFsr2 = 1,

        Dlss = 2,

        /// <summary>
        /// AMD's FSR through AMD's own DLLs: 3.1.5, or FSR 4 on cards that
        /// have it. DirectX 12 only. See <c>Fsr3Upscaler</c>.
        /// </summary>
        Fsr3 = 3
    }

    /// <summary>How far below native the upscaler renders. Best image first.</summary>
    public enum UpscaleQuality
    {
        Quality = 0,
        Balanced = 1,
        Performance = 2,
        UltraPerformance = 3,

        /// <summary>
        /// The render scale row decides instead of the vendor.
        ///
        /// Last rather than first because it is not a rung of the same ladder.
        /// The four above are ratios both vendors publish and neither will
        /// deviate from; this one is whatever was asked for, and it exists
        /// because the four are a coarse set of choices for a control that has
        /// no reason to be coarse.
        /// </summary>
        Custom = 4
    }

    /// <summary>
    /// How edges are resolved when no upscaler is doing it instead.
    ///
    /// DLAA is in this list rather than the upscale list because that is what it
    /// is — DLSS at 100% scale, spending its budget on anti-aliasing rather than
    /// on reconstruction. There is no FSR equivalent: AMD's library has a
    /// native-resolution mode, but nothing here offers it.
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

        /// <summary>
        /// How far under the display's rate <see cref="MatchDisplay"/> sits, as a
        /// fraction of that rate.
        ///
        /// Proportional rather than a flat number of frames, because a flat one
        /// means completely different things at either end. Two frames under 60 Hz
        /// is a 3.3% margin and genuinely safe; two frames under 200 Hz is 1%, and
        /// at 200 Hz the whole refresh interval is only 5 ms - so that "margin" is
        /// 0.05 ms and buys nothing at all. A 200 Hz display was where this was
        /// found: the cap read 198 and the game still sat exactly on the boundary.
        /// </summary>
        public const float DisplayHeadroomFraction = 0.04f;

        /// <summary>Never shave less than this, however slow the display.</summary>
        public const int MinimumDisplayHeadroom = 2;

        /// <summary>
        /// The lowest cap the slider offers.
        ///
        /// A cap below the display's rate is the cheapest way to stop a laptop
        /// cooking itself on a menu screen, which is why the slider reaches down
        /// to 30 even though nobody chooses it for play. Below that the game is
        /// not playable, so there is nothing to offer.
        /// </summary>
        public const int MinimumFrameCap = 30;

        /// <summary>The highest cap the slider offers. Past it is uncapped.</summary>
        public const int MaximumFrameCap = 300;

        /// <summary>
        /// The caps the row offered while it was a cycler, until 16 November
        /// 2026. Kept so the tests can show that every value an older build
        /// could have saved still loads as itself.
        /// </summary>
        public static readonly int[] FormerFrameRateCaps = { 0, MatchDisplay, 30, 60, 90, 120, 144, 165, 240 };

        /// <summary>
        /// A stored cap brought into what the game offers: 0 for uncapped,
        /// <see cref="MatchDisplay"/>, or a whole number from
        /// <see cref="MinimumFrameCap"/> to <see cref="MaximumFrameCap"/>.
        ///
        /// The cap is saved as the frame rate itself and always was, so a value
        /// from the cycler needs no translating. This only catches a number no
        /// control could have written, such as a settings file edited by hand.
        /// </summary>
        public static int ClampCap(int cap)
        {
            if (cap == MatchDisplay)
            {
                return MatchDisplay;
            }

            if (cap <= 0)
            {
                return 0;
            }

            if (cap < MinimumFrameCap)
            {
                return MinimumFrameCap;
            }

            return cap > MaximumFrameCap ? MaximumFrameCap : cap;
        }

        /// <summary>
        /// The slider's leftmost position, one below the lowest cap: Just under
        /// display. It is the cap the game works out for itself, so it sits
        /// apart from the numbers the player types in by hand.
        /// </summary>
        public const int CapSliderFirst = MinimumFrameCap - 1;

        /// <summary>
        /// The slider's rightmost position, one above the highest cap:
        /// uncapped, which is where "more than 300" belongs.
        /// </summary>
        public const int CapSliderLast = MaximumFrameCap + 1;

        /// <summary>Where a cap sits on the slider. Between the two ends the position is the frame rate.</summary>
        public static int CapToSlider(int cap)
        {
            cap = ClampCap(cap);

            if (cap == MatchDisplay)
            {
                return CapSliderFirst;
            }

            return cap == 0 ? CapSliderLast : cap;
        }

        /// <summary>The cap a slider position stands for.</summary>
        public static int SliderToCap(int position)
        {
            if (position <= CapSliderFirst)
            {
                return MatchDisplay;
            }

            return position >= CapSliderLast ? 0 : position;
        }

        /// <summary>
        /// How far one press of left or right moves the slider: one frame at
        /// first, then five and ten while the direction is held.
        ///
        /// One a press is the point of the slider, and 271 presses is no way to
        /// cross it, so a held direction speeds up.
        /// </summary>
        /// <param name="repeats">Presses in a row in one direction with no pause, not counting the first.</param>
        public static int CapSliderStep(int repeats)
        {
            if (repeats < 5)
            {
                return 1;
            }

            return repeats < 20 ? 5 : 10;
        }

        /// <summary>
        /// The slider position one press away. A step of five or ten lands on
        /// the round numbers, so a held direction passes 60, 120 and 240 rather
        /// than 61, 121 and 241.
        /// </summary>
        public static int NextCapSliderPosition(int position, int direction, int repeats)
        {
            return NextSliderPosition(position, direction, repeats, CapSliderFirst, CapSliderLast);
        }

        /// <summary>
        /// The position one press away on any of the whole-number graphics
        /// sliders - frame cap, dynamic resolution, render scale - held between
        /// its two ends. One a press at first, five and then ten while the
        /// direction is held (<see cref="CapSliderStep"/>), landing on multiples
        /// of the step.
        /// </summary>
        public static int NextSliderPosition(int position, int direction, int repeats, int first, int last)
        {
            int step = CapSliderStep(repeats);
            int next;

            if (direction > 0)
            {
                next = (position / step + 1) * step;
            }
            else
            {
                next = ((position + step - 1) / step - 1) * step;
            }

            if (next < first)
            {
                return first;
            }

            return next > last ? last : next;
        }

        // ---------- render scale ----------

        /// <summary>The lowest render scale, in percent. Below half the image falls apart.</summary>
        public const int MinimumRenderScalePercent = 50;

        /// <summary>Native.</summary>
        public const int MaximumRenderScalePercent = 100;

        /// <summary>
        /// The steps the render scale row offered while it was a cycler, until
        /// 17 November 2026. Kept so the tests can show they still load as
        /// themselves.
        /// </summary>
        public static readonly float[] FormerRenderScales = { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f };

        /// <summary>
        /// A render scale as a slider position: whole percent, 50 to 100. The
        /// scale is stored as a fraction and always was, so this is only the
        /// rounding the slider shows.
        /// </summary>
        public static int RenderScaleToPercent(float scale)
        {
            int percent = (int)(scale * 100f + 0.5f);

            if (percent < MinimumRenderScalePercent)
            {
                return MinimumRenderScalePercent;
            }

            return percent > MaximumRenderScalePercent ? MaximumRenderScalePercent : percent;
        }

        /// <summary>The scale a slider position stands for.</summary>
        public static float PercentToRenderScale(int percent)
        {
            if (percent < MinimumRenderScalePercent)
            {
                percent = MinimumRenderScalePercent;
            }
            else if (percent > MaximumRenderScalePercent)
            {
                percent = MaximumRenderScalePercent;
            }

            return percent / 100f;
        }

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

            int headroom = (int)(refreshRate * DisplayHeadroomFraction + 0.5f);
            if (headroom < MinimumDisplayHeadroom)
            {
                headroom = MinimumDisplayHeadroom;
            }

            // Below about 30 Hz there is nothing to give away - shaving frames off
            // an already unplayable rate makes it worse, not safer.
            return refreshRate - headroom >= 30 ? refreshRate - headroom : refreshRate;
        }

        /// <summary>
        /// How many refreshes VSync should hold each frame for when that alone
        /// gives the cap: 2 for 100 FPS on a 200 Hz display, 1 for a cap on the
        /// refresh rate itself. 0 when the cap is no whole fraction of the
        /// refresh and the frame limiter has to pace it.
        ///
        /// VSync paces on the display's own clock, and nothing is more even than
        /// that. Pacing a whole fraction by the limiter's clock with VSync on
        /// would put two clocks on one boundary, and every frame would be a coin
        /// flip on which refresh it made, the thing Just under display exists to
        /// avoid. Four is the most vSyncCount takes.
        /// </summary>
        public static int SyncInterval(int cap, int refreshRate)
        {
            if (cap <= 0 || refreshRate <= 0 || refreshRate % cap != 0)
            {
                return 0;
            }

            int interval = refreshRate / cap;
            return interval <= 4 ? interval : 0;
        }

        // ---------- dynamic resolution ----------

        /// <summary>
        /// Frame rates dynamic resolution can chase, with 0 meaning off.
        ///
        /// One row rather than a toggle beside a target, because a target that is
        /// switched off is a control that does nothing - and this project already
        /// found three of those. "Dynamic Resolution: 120 FPS" says both things.
        ///
        /// These were the row's choices while it was a cycler, until 17 November
        /// 2026; it is a slider over every whole number from 30 to 300 now, with
        /// off at the left end. Kept so the tests can show they still load.
        /// </summary>
        public static readonly int[] FormerDynamicTargets = { 0, 30, 60, 90, 120, 144, 165, 240 };

        /// <summary>
        /// The lowest and highest rates the dynamic resolution slider chases,
        /// the same span as the frame cap's. A target above what the cap or the
        /// display lets through is chased at the slower rate anyway
        /// (<see cref="DynamicTargetMs"/>).
        /// </summary>
        public const int MinimumDynamicTarget = 30;

        public const int MaximumDynamicTarget = 300;

        /// <summary>The slider's leftmost position, one below the lowest target: off.</summary>
        public const int DynamicSliderFirst = MinimumDynamicTarget - 1;

        public const int DynamicSliderLast = MaximumDynamicTarget;

        /// <summary>
        /// A stored target brought into what the slider offers: 0 for off, or a
        /// whole number from 30 to 300. Every value the cycler saved is already
        /// one of those, so this only catches a hand-edited settings file.
        /// </summary>
        public static int ClampDynamicTarget(int target)
        {
            if (target <= 0)
            {
                return 0;
            }

            if (target < MinimumDynamicTarget)
            {
                return MinimumDynamicTarget;
            }

            return target > MaximumDynamicTarget ? MaximumDynamicTarget : target;
        }

        /// <summary>Where a target sits on the slider: off at the left end, else the rate itself.</summary>
        public static int DynamicTargetToSlider(int target)
        {
            target = ClampDynamicTarget(target);
            return target == 0 ? DynamicSliderFirst : target;
        }

        /// <summary>The target a slider position stands for.</summary>
        public static int SliderToDynamicTarget(int position)
        {
            return position <= DynamicSliderFirst ? 0 : ClampDynamicTarget(position);
        }

        public static string DescribeDynamicTarget(int target)
        {
            return target <= 0 ? "Off" : "Target " + target + " FPS";
        }

        /// <summary>How long a frame may take to hold a given rate.</summary>
        public static float TargetFrameMs(int targetFps)
        {
            return targetFps <= 0 ? 0f : 1000f / targetFps;
        }

        /// <summary>
        /// The most frames a second the screen will be shown, or 0 for no limit.
        /// A cap limits it, and so does VSync, to one frame a refresh.
        /// </summary>
        /// <param name="cap">The cap in force, already resolved (<see cref="ResolveCap"/>).</param>
        public static int PresentationRate(int cap, bool vSync, int refreshRate)
        {
            int display = vSync && refreshRate > 0 ? refreshRate : 0;

            if (cap <= 0)
            {
                return display;
            }

            return display > 0 && display < cap ? display : cap;
        }

        /// <summary>
        /// How long a frame may take for dynamic resolution, which is the target's
        /// frame time but never less than the screen allows a frame anyway.
        ///
        /// A target above the cap or the refresh rate cannot be reached by any
        /// resolution: the frames are held back to the slower rate whatever they
        /// cost. Chasing it only lowers the resolution for frames that will not
        /// be shown any sooner, so the controller chases the rate that will.
        /// </summary>
        public static float DynamicTargetMs(int targetFps, int presentationRate)
        {
            float target = TargetFrameMs(targetFps);

            if (target <= 0f || presentationRate <= 0)
            {
                return target;
            }

            float shown = TargetFrameMs(presentationRate);
            return shown > target ? shown : target;
        }

        public static string DescribeCap(int cap)
        {
            if (cap == MatchDisplay)
            {
                return "Just under display";
            }

            return cap <= 0 ? "Uncapped" : cap + " FPS";
        }

        /// <summary>
        /// The line under the frame cap: what is holding it, when that is worth
        /// saying. Empty for an ordinary cap paced by the frame limiter.
        /// </summary>
        /// <param name="cap">The cap as stored.</param>
        /// <param name="resolved">The cap in force (<see cref="ResolveCap"/>).</param>
        /// <param name="syncInterval">Refreshes per frame when VSync holds the cap, else 0 (<see cref="SyncInterval"/>).</param>
        public static string DescribeCapHolder(int cap, int resolved, int syncInterval)
        {
            // A whole fraction of the refresh rate goes to VSync rather than the
            // frame limiter, which is worth saying: it is the one cap that paces
            // on the display's clock.
            if (syncInterval == 1)
            {
                return "Held by VSync on its own";
            }

            if (syncInterval > 1)
            {
                return "Held by VSync, one frame every " + syncInterval + " refreshes";
            }

            if (cap != MatchDisplay)
            {
                return string.Empty;
            }

            // The derived cap shows the number it resolved to, so it is not a
            // promise the player has to take on trust.
            return resolved > 0
                ? resolved + " FPS: clear of the refresh rate, where pacing gets rough"
                : "Stays clear of the refresh rate, where pacing gets rough";
        }

        // ---------- upscaling ----------

        /// <summary>
        /// Which reconstruction runs, not how hard it pushes. The two were one
        /// nine-item list once, which meant "DLSS Balanced" and "FSR Balanced"
        /// were unrelated entries a player had to scroll between to compare.
        ///
        /// Indexed by <see cref="UpscaleMethod"/>, so the retired FSR 2 keeps its
        /// slot. FSR carries no version: AMD's loader picks 3.1.5 or FSR 4 by
        /// card, and the system profile reports which.
        /// </summary>
        public static readonly string[] UpscaleMethodNames = { "Off", "FSR 2", "DLSS", "FSR" };

        /// <summary>
        /// What a stored upscaler number means today. A player who had FSR 2
        /// keeps an FSR rather than silently losing upscaling, and a number no
        /// build ever wrote reads as Off.
        /// </summary>
        public static UpscaleMethod StoredUpscaleMethod(int stored)
        {
            switch (stored)
            {
                case (int)UpscaleMethod.RetiredFsr2:
                case (int)UpscaleMethod.Fsr3:
                    return UpscaleMethod.Fsr3;
                case (int)UpscaleMethod.Dlss:
                    return UpscaleMethod.Dlss;
                default:
                    return UpscaleMethod.Off;
            }
        }

        /// <summary>
        /// How hard the upscaler pushes, in the order every other game lists it:
        /// best image first, cheapest last, with the one that is not a vendor
        /// mode at all on the end.
        /// </summary>
        public static readonly string[] UpscaleQualityNames =
        {
            "Quality", "Balanced", "Performance", "Ultra Performance", "Custom"
        };

        /// <summary>
        /// How many of those are vendor presets rather than a scale of our own.
        ///
        /// Split out from the name count deliberately. DLSS's quality enum stops
        /// at four and so do AMD's published ratios, so clamping a stored value
        /// against the number of *names* would hand the driver a fifth it does
        /// not accept - which is exactly what adding Custom to the end once did.
        /// </summary>
        public const int PresetCount = 4;

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
            // A ladder with nothing on it has no answer to give, and zero is the
            // one value that is safe for all three callers - it reads as the
            // effect being off rather than as some arbitrary middle setting.
            if (anchors == null || anchors.Length == 0)
            {
                return 0f;
            }

            // One anchor is the whole ladder, so every amount lands on it. Handled
            // here rather than trusted to the interpolation below, which reaches
            // for anchors[index + 1] and would run off the end of a single-entry
            // array for any amount strictly between 0 and 1. Every caller passes a
            // four-entry array today; this is what keeps shortening one from being
            // an out-of-range exception instead of a shorter ladder.
            if (amount <= 0f || anchors.Length == 1)
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
        /// What fraction of native each vendor preset renders at.
        ///
        /// Both vendors publish the same four ratios - 1/1.5, 1/1.7, 1/2 and 1/3
        /// - so one table serves both.
        ///
        /// These were rounded display values when the drivers owned the scale and
        /// this number was only ever shown. It is applied now, so it is exact:
        /// handing DLSS 0.58 where its own Balanced mode is 0.5882 asks it for a
        /// resolution a few pixels off the one it wants, which is the kind of
        /// difference that costs a feature rebuild for nothing.
        ///
        /// <see cref="UpscaleQuality.Custom"/> is not a preset and is not a valid
        /// input here; resolve it through <see cref="ResolvePreset"/> first.
        /// </summary>
        public static float PresetScale(int quality)
        {
            switch (quality)
            {
                case 0: return 1f / 1.5f;
                case 1: return 1f / 1.7f;
                case 2: return 1f / 2f;
                default: return 1f / 3f;
            }
        }

        /// <summary>
        /// The vendor preset a quality selection actually runs in.
        ///
        /// Four of the five modes are the preset itself. Custom is not a preset
        /// at all - neither driver has such a mode - so it resolves to whichever
        /// real one sits nearest the scale being asked for.
        ///
        /// That choice is not cosmetic. DLSS accepts a band of render
        /// resolutions either side of the preset's own ratio and rebuilds itself
        /// when asked for one outside, so picking the nearest preset is what
        /// keeps a custom scale inside a band rather than straddling its edge.
        /// </summary>
        public static int ResolvePreset(int quality, float scale)
        {
            if (quality != (int)UpscaleQuality.Custom)
            {
                return quality < 0 ? 0 : (quality > PresetCount - 1 ? PresetCount - 1 : quality);
            }

            return PresetForScale(scale);
        }

        /// <summary>The preset whose own ratio sits nearest a requested scale.</summary>
        public static int PresetForScale(float scale)
        {
            int best = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < PresetCount; i++)
            {
                float distance = scale - PresetScale(i);
                if (distance < 0f)
                {
                    distance = -distance;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }
    }
}
