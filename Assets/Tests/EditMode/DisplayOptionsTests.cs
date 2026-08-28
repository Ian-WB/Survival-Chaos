using System.Collections.Generic;
using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Resolution handling is easy to get subtly wrong and hard to notice: the
    /// duplicates only show on a high refresh monitor, and the fallback only
    /// matters to someone who changed display since they last played.
    /// </summary>
    public class DisplayOptionsTests
    {
        private static List<DisplaySize> Sizes(params (int w, int h)[] entries)
        {
            List<DisplaySize> list = new List<DisplaySize>();
            foreach ((int w, int h) in entries)
            {
                list.Add(new DisplaySize(w, h));
            }

            return list;
        }

        [Test]
        public void Distinct_CollapsesTheRepeatsUnityReportsPerRefreshRate()
        {
            // A 165 Hz monitor reports each size once per rate. Unfiltered, this
            // is the list that makes a resolution picker unusable.
            List<DisplaySize> reported = Sizes(
                (1920, 1080), (1920, 1080), (1920, 1080),
                (2560, 1440), (2560, 1440));

            List<DisplaySize> result = DisplayOptions.Distinct(reported);

            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void Distinct_DropsSizesTooSmallToPlayAt()
        {
            List<DisplaySize> result = DisplayOptions.Distinct(
                Sizes((640, 480), (800, 600), (1920, 1080)));

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1920, result[0].Width);
        }

        [Test]
        public void Distinct_SortsSmallestFirst()
        {
            List<DisplaySize> result = DisplayOptions.Distinct(
                Sizes((2560, 1440), (1280, 720), (1920, 1080)));

            Assert.AreEqual(1280, result[0].Width);
            Assert.AreEqual(1920, result[1].Width);
            Assert.AreEqual(2560, result[2].Width);
        }

        [Test]
        public void Distinct_WithNothingUsable_ReturnsEmptyRatherThanThrowing()
        {
            Assert.AreEqual(0, DisplayOptions.Distinct(Sizes((640, 480))).Count);
            Assert.AreEqual(0, DisplayOptions.Distinct(null).Count);
        }

        [Test]
        public void IndexOf_FindsAnExactMatch()
        {
            List<DisplaySize> sizes = DisplayOptions.Distinct(
                Sizes((1280, 720), (1920, 1080), (2560, 1440)));

            Assert.AreEqual(1, DisplayOptions.IndexOf(sizes, new DisplaySize(1920, 1080)));
        }

        [Test]
        public void IndexOf_FallsBackToTheLargestThatStillFits()
        {
            // The saved resolution is gone - a different monitor, or a driver
            // change. Dropping down beats jumping to the largest available.
            List<DisplaySize> sizes = DisplayOptions.Distinct(
                Sizes((1280, 720), (1920, 1080), (2560, 1440)));

            Assert.AreEqual(1, DisplayOptions.IndexOf(sizes, new DisplaySize(2048, 1152)));
        }

        [Test]
        public void IndexOf_WithNothingSmallEnough_ReturnsTheSmallest()
        {
            List<DisplaySize> sizes = DisplayOptions.Distinct(Sizes((1920, 1080), (2560, 1440)));

            Assert.AreEqual(0, DisplayOptions.IndexOf(sizes, new DisplaySize(1280, 720)));
        }

        [Test]
        public void IndexOf_WithNoSizes_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, DisplayOptions.IndexOf(new List<DisplaySize>(), new DisplaySize(1920, 1080)));
            Assert.AreEqual(-1, DisplayOptions.IndexOf(null, new DisplaySize(1920, 1080)));
        }

        [Test]
        public void FrameRateCaps_StartWithUncapped()
        {
            Assert.AreEqual(0, DisplayOptions.FrameRateCaps[0]);
            Assert.AreEqual("Uncapped", DisplayOptions.DescribeCap(0));
            Assert.AreEqual("60 FPS", DisplayOptions.DescribeCap(60));
        }

        /// <summary>
        /// The whole point of this cap is to not land on the refresh rate, since
        /// that is where frame pacing falls apart. A rounding slip that returned
        /// the rate itself would reintroduce the exact problem it exists to avoid,
        /// and would look like it was working.
        /// </summary>
        /// <summary>
        /// The margin is a proportion, not a fixed number of frames. A flat two
        /// frames is 3.3% at 60 Hz and 1% at 200 Hz - and at 200 Hz the whole
        /// refresh interval is 5 ms, so 1% is a margin of 0.05 ms and buys
        /// nothing. Asserting a percentage band is what actually holds across the
        /// range; asserting "within 4 frames" only held for slow displays.
        /// </summary>
        [Test]
        public void ResolveCap_LeavesTheSameProportionOnEveryDisplay()
        {
            foreach (int rate in new[] { 60, 75, 120, 144, 165, 200, 240, 360 })
            {
                int resolved = DisplayOptions.ResolveCap(DisplayOptions.MatchDisplay, rate);
                float margin = (rate - resolved) * 100f / rate;

                Assert.Less(resolved, rate, "must not sit on the refresh rate of " + rate);
                Assert.GreaterOrEqual(margin, 2.5f, rate + " Hz was shaved too little to matter");
                Assert.LessOrEqual(margin, 6f, rate + " Hz gave away more rate than it needed to");
            }
        }

        /// <summary>
        /// The slider replaced a four-item cycler, and the promise was that the
        /// four named positions would land on exactly the values they already had.
        /// An interpolation that drifted by a little at the anchors would change
        /// how the game looks for anyone who had already picked one.
        /// </summary>
        [Test]
        public void Sharpness_AnchorsKeepTheValuesTheCyclerHad()
        {
            float[] expectedTaa = { 0f, 0.25f, 0.5f, 1f };
            float[] expectedHistory = { 0f, 0.18f, 0.35f, 0.55f };
            float[] expectedUpscaler = { 0f, 0.2f, 0.4f, 0.7f };

            for (int level = 0; level < DisplayOptions.SharpnessNames.Length; level++)
            {
                float at = DisplayOptions.SharpnessAnchor(level);

                Assert.AreEqual(expectedTaa[level], DisplayOptions.TaaSharpenStrength(at), 0.0001f,
                    DisplayOptions.SharpnessNames[level] + " TAA sharpen moved");
                Assert.AreEqual(expectedHistory[level], DisplayOptions.TaaHistorySharpening(at), 0.0001f,
                    DisplayOptions.SharpnessNames[level] + " TAA history moved");
                Assert.AreEqual(expectedUpscaler[level], DisplayOptions.UpscalerSharpness(at), 0.0001f,
                    DisplayOptions.SharpnessNames[level] + " upscaler sharpness moved");
            }
        }

        [Test]
        public void Sharpness_RisesWithoutDippingBetweenAnchors()
        {
            float previous = -1f;

            for (int i = 0; i <= 40; i++)
            {
                float amount = i / 40f;
                float value = DisplayOptions.TaaSharpenStrength(amount);

                Assert.GreaterOrEqual(value, previous, "sharpening dipped at " + amount);
                previous = value;
            }
        }

        [Test]
        public void Sharpness_ClampsOutsideItsTravel()
        {
            Assert.AreEqual(0f, DisplayOptions.TaaSharpenStrength(-1f), 0.0001f);
            Assert.AreEqual(1f, DisplayOptions.TaaSharpenStrength(2f), 0.0001f);
        }

        [Test]
        public void Sharpness_NamesTheLevelOnlyWhenSittingOnIt()
        {
            Assert.AreEqual("0%  (Off)", DisplayOptions.DescribeSharpness(0f));
            Assert.AreEqual("100%  (High)", DisplayOptions.DescribeSharpness(1f));

            // Between anchors it is just a number - claiming "Medium" at 55% would
            // be a readout that disagrees with what is actually applied.
            Assert.AreEqual("55%", DisplayOptions.DescribeSharpness(0.55f));
        }

        [Test]
        public void ResolveCap_LeavesOrdinaryCapsAlone()
        {
            Assert.AreEqual(60, DisplayOptions.ResolveCap(60, 144));
            Assert.AreEqual(0, DisplayOptions.ResolveCap(0, 144));
        }

        [Test]
        public void ResolveCap_WithNoUsableRefreshRate_LeavesItUncapped()
        {
            // Better uncapped than pinned to a number nobody reported.
            Assert.AreEqual(0, DisplayOptions.ResolveCap(DisplayOptions.MatchDisplay, 0));
            Assert.AreEqual(0, DisplayOptions.ResolveCap(DisplayOptions.MatchDisplay, -1));
        }

        [Test]
        public void ResolveCap_AtAVeryLowRate_DoesNotUndercutIntoUnplayability()
        {
            // Subtracting headroom from an already low rate is worse than matching
            // it; 30 Hz minus 2 is a worse experience than 30.
            Assert.AreEqual(30, DisplayOptions.ResolveCap(DisplayOptions.MatchDisplay, 30));
        }

        /// <summary>
        /// The one that would go wrong silently. NVIDIA's quality enum runs
        /// cheapest-first and AMD's runs best-first, so a straight cast hands the
        /// player picking Quality the fastest, ugliest mode instead — and it still
        /// renders, still upscales, and still looks like the setting works.
        /// </summary>
        [Test]
        public void DlssQualityValue_MapsBestFirstOntoNvidiasCheapestFirstEnum()
        {
            Assert.AreEqual(DisplayOptions.DlssMaximumQuality,
                DisplayOptions.DlssQualityValue((int)UpscaleQuality.Quality));
            Assert.AreEqual(DisplayOptions.DlssBalanced,
                DisplayOptions.DlssQualityValue((int)UpscaleQuality.Balanced));
            Assert.AreEqual(DisplayOptions.DlssMaximumPerformance,
                DisplayOptions.DlssQualityValue((int)UpscaleQuality.Performance));
            Assert.AreEqual(DisplayOptions.DlssUltraPerformance,
                DisplayOptions.DlssQualityValue((int)UpscaleQuality.UltraPerformance));
        }

        [Test]
        public void DlssQualityValue_IsNeverDlaa()
        {
            // DLAA is reached from the anti-aliasing row, not by cycling upscale
            // quality past the end of it. Landing on it here would silently pin
            // the render scale to native and make the whole row do nothing.
            for (int i = -2; i < 8; i++)
            {
                Assert.AreNotEqual(DisplayOptions.DlssDlaa, DisplayOptions.DlssQualityValue(i));
            }
        }

        [Test]
        public void Fsr2QualityValue_KeepsAmdsOrderingAndClampsOffTheEnds()
        {
            Assert.AreEqual(0u, DisplayOptions.Fsr2QualityValue((int)UpscaleQuality.Quality));
            Assert.AreEqual(3u, DisplayOptions.Fsr2QualityValue((int)UpscaleQuality.UltraPerformance));

            // A settings file written by a later build with more modes must not
            // index past the enum the driver actually accepts.
            Assert.AreEqual(0u, DisplayOptions.Fsr2QualityValue(-1));
            Assert.AreEqual(3u, DisplayOptions.Fsr2QualityValue(99));
        }

        [Test]
        public void QualityNames_CoverEveryModeBothVendorsOffer()
        {
            // Five names but four vendor modes: Custom is ours, not theirs. Both
            // drivers' quality enums stop at four, so anything clamping a stored
            // quality has to clamp against PresetCount rather than this length.
            Assert.AreEqual(4, DisplayOptions.PresetCount);
            Assert.AreEqual(DisplayOptions.PresetCount + 1,
                DisplayOptions.UpscaleQualityNames.Length);
            Assert.AreEqual("Custom",
                DisplayOptions.UpscaleQualityNames[(int)UpscaleQuality.Custom]);
            Assert.AreEqual(3, DisplayOptions.UpscaleMethodNames.Length);
            Assert.AreEqual(5, DisplayOptions.AntiAliasingNames.Length);

            // DLAA must stay last: the anti-aliasing row drops exactly one entry
            // off the end when DLSS is unavailable.
            Assert.AreEqual("DLAA",
                DisplayOptions.AntiAliasingNames[DisplayOptions.AntiAliasingNames.Length - 1]);
            Assert.AreEqual((int)AntiAliasingMode.Dlaa, DisplayOptions.AntiAliasingNames.Length - 1);
        }

        [Test]
        public void PresetScale_FallsAsQualityDrops()
        {
            // Bounded by PresetCount, not by the name count. Custom is on the end
            // of that list and is not a rung of this ladder - PresetScale would
            // answer for it out of its default branch and the ordering check would
            // fail against a number that means nothing.
            float previous = 1f;
            for (int i = 0; i < DisplayOptions.PresetCount; i++)
            {
                float scale = DisplayOptions.PresetScale(i);
                Assert.Less(scale, previous, "Quality mode " + i + " should render lower than the one above it");
                Assert.Greater(scale, 0f);
                previous = scale;
            }
        }

        /// <summary>
        /// Exact rather than rounded, because these are applied now instead of
        /// only shown. Handing DLSS 0.58 where its Balanced mode is 0.5882 asks
        /// for an input resolution a few pixels off the one it was built for, and
        /// being off its expected input is what costs a feature rebuild.
        /// </summary>
        [Test]
        public void PresetScale_MatchesTheRatiosBothVendorsPublish()
        {
            Assert.AreEqual(1f / 1.5f,
                DisplayOptions.PresetScale((int)UpscaleQuality.Quality), 0.0001f);
            Assert.AreEqual(1f / 1.7f,
                DisplayOptions.PresetScale((int)UpscaleQuality.Balanced), 0.0001f);
            Assert.AreEqual(0.5f,
                DisplayOptions.PresetScale((int)UpscaleQuality.Performance), 0.0001f);
            Assert.AreEqual(1f / 3f,
                DisplayOptions.PresetScale((int)UpscaleQuality.UltraPerformance), 0.0001f);
        }

        /// <summary>
        /// Not arithmetic so much as a coupling with nowhere else to live. HDRP
        /// clamps whatever the scaler returns to the pipeline asset's
        /// minPercentage, and the assets shipped a floor of 50 while nothing here
        /// supplied its own scale. Ultra Performance renders at a third, so that
        /// floor would silently have made it identical to Performance.
        /// </summary>
        [Test]
        public void CheapestPresetRendersBelowFiftyPercent()
        {
            Assert.Less(DisplayOptions.PresetScale((int)UpscaleQuality.UltraPerformance), 0.5f);
        }

        [Test]
        public void PresetForScale_PicksTheNearestVendorMode()
        {
            // Sitting exactly on a published ratio has to return that preset, or
            // a custom scale dialled onto one would run inside a neighbour's band.
            for (int i = 0; i < DisplayOptions.PresetCount; i++)
            {
                Assert.AreEqual(i, DisplayOptions.PresetForScale(DisplayOptions.PresetScale(i)));
            }

            // Past either end, the nearest is the end.
            Assert.AreEqual((int)UpscaleQuality.Quality, DisplayOptions.PresetForScale(1f));
            Assert.AreEqual((int)UpscaleQuality.UltraPerformance, DisplayOptions.PresetForScale(0.1f));
        }

        /// <summary>
        /// Custom is not a mode either driver has. Returning it from here would go
        /// straight into DlssQualityValue, whose default branch reads a fifth
        /// entry as Ultra Performance - dropping the player three modes without
        /// anything appearing to fail.
        /// </summary>
        [Test]
        public void PresetForScale_IsNeverCustom()
        {
            for (int percent = 0; percent <= 100; percent++)
            {
                int preset = DisplayOptions.PresetForScale(percent / 100f);
                Assert.GreaterOrEqual(preset, 0);
                Assert.Less(preset, DisplayOptions.PresetCount);
            }
        }

        [Test]
        public void ResolvePreset_PassesPresetsThroughAndResolvesOnlyCustom()
        {
            // A real preset is its own answer whatever scale is passed beside it.
            for (int i = 0; i < DisplayOptions.PresetCount; i++)
            {
                Assert.AreEqual(i, DisplayOptions.ResolvePreset(i, 0.83f));
            }

            Assert.AreEqual((int)UpscaleQuality.Performance,
                DisplayOptions.ResolvePreset((int)UpscaleQuality.Custom, 0.5f));
            Assert.AreEqual((int)UpscaleQuality.Quality,
                DisplayOptions.ResolvePreset((int)UpscaleQuality.Custom, 0.9f));
        }

        [Test]
        public void ResolvePreset_ClampsAModeStoredByANewerBuild()
        {
            Assert.AreEqual(0, DisplayOptions.ResolvePreset(-1, 0.7f));
            Assert.AreEqual(DisplayOptions.PresetCount - 1, DisplayOptions.ResolvePreset(99, 0.7f));
        }

        /// <summary>
        /// The trap adding Custom set. This clamped against the number of names,
        /// which grew to five, so Custom would have reached AMD as a quality mode
        /// its enum does not define.
        /// </summary>
        [Test]
        public void Fsr2QualityValue_NeverNamesAModeAmdDoesNotHave()
        {
            for (int i = -2; i < 8; i++)
            {
                Assert.Less(DisplayOptions.Fsr2QualityValue(i), (uint)DisplayOptions.PresetCount);
            }
        }
    }
}
