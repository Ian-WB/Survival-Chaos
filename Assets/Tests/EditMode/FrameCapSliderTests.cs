using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The frame cap became a slider on 16 November 2026: any whole number from
    /// 30 to 300, Just under display at the left end and Uncapped at the right.
    /// The cap is still stored as the frame rate itself, so these pin that a
    /// value saved by the old cycler loads as what it was.
    /// </summary>
    public class FrameCapSliderTests
    {
        [Test]
        public void ClampCap_KeepsEveryWholeNumberInRange()
        {
            for (int cap = DisplayOptions.MinimumFrameCap; cap <= DisplayOptions.MaximumFrameCap; cap++)
            {
                Assert.AreEqual(cap, DisplayOptions.ClampCap(cap));
            }

            Assert.AreEqual(30, DisplayOptions.MinimumFrameCap);
            Assert.AreEqual(300, DisplayOptions.MaximumFrameCap);
        }

        [Test]
        public void ClampCap_PullsHandEditedNumbersIntoRange()
        {
            Assert.AreEqual(30, DisplayOptions.ClampCap(1));
            Assert.AreEqual(30, DisplayOptions.ClampCap(29));
            Assert.AreEqual(300, DisplayOptions.ClampCap(301));
            Assert.AreEqual(300, DisplayOptions.ClampCap(100000));
            Assert.AreEqual(0, DisplayOptions.ClampCap(-7), "a negative other than Just under display is uncapped");
        }

        [Test]
        public void ClampCap_KeepsUncappedAndJustUnderDisplay()
        {
            Assert.AreEqual(0, DisplayOptions.ClampCap(0));
            Assert.AreEqual(DisplayOptions.MatchDisplay, DisplayOptions.ClampCap(DisplayOptions.MatchDisplay));
        }

        [Test]
        public void EveryValueTheOldCyclerSaved_LoadsAsItself_AndRoundTripsThroughTheSlider()
        {
            foreach (int saved in DisplayOptions.FormerFrameRateCaps)
            {
                Assert.AreEqual(saved, DisplayOptions.ClampCap(saved), saved + " changed on load");
                int position = DisplayOptions.CapToSlider(saved);
                Assert.AreEqual(saved, DisplayOptions.SliderToCap(position), saved + " did not survive the slider");
            }
        }

        [Test]
        public void Slider_PositionIsTheFrameRate_BetweenItsEnds()
        {
            Assert.AreEqual(60, DisplayOptions.CapToSlider(60));
            Assert.AreEqual(73, DisplayOptions.SliderToCap(73));
            Assert.AreEqual(300, DisplayOptions.SliderToCap(300));
            Assert.AreEqual(30, DisplayOptions.SliderToCap(30));
        }

        [Test]
        public void Slider_RightEndIsUncapped_LeftEndIsJustUnderDisplay()
        {
            Assert.AreEqual(DisplayOptions.CapSliderLast, DisplayOptions.CapToSlider(0));
            Assert.AreEqual(0, DisplayOptions.SliderToCap(DisplayOptions.CapSliderLast));
            Assert.AreEqual(0, DisplayOptions.SliderToCap(DisplayOptions.CapSliderLast + 50));

            Assert.AreEqual(DisplayOptions.CapSliderFirst, DisplayOptions.CapToSlider(DisplayOptions.MatchDisplay));
            Assert.AreEqual(DisplayOptions.MatchDisplay, DisplayOptions.SliderToCap(DisplayOptions.CapSliderFirst));
            Assert.AreEqual(DisplayOptions.MatchDisplay, DisplayOptions.SliderToCap(0));
        }

        [Test]
        public void Uncapped_ReachesNeitherTheLimiterNorVSync()
        {
            Assert.AreEqual(0, DisplayOptions.ResolveCap(DisplayOptions.SliderToCap(DisplayOptions.CapSliderLast), 200));
            Assert.AreEqual(0, DisplayOptions.SyncInterval(0, 200));
            Assert.AreEqual("Uncapped", DisplayOptions.DescribeCap(0));
        }

        [Test]
        public void Stepping_IsOneAPress_ThenSpeedsUpWhileHeld()
        {
            Assert.AreEqual(61, DisplayOptions.NextCapSliderPosition(60, 1, 0));
            Assert.AreEqual(59, DisplayOptions.NextCapSliderPosition(60, -1, 0));

            // Held: lands on the round numbers rather than stepping off them.
            Assert.AreEqual(65, DisplayOptions.NextCapSliderPosition(61, 1, 5));
            Assert.AreEqual(60, DisplayOptions.NextCapSliderPosition(61, -1, 5));
            Assert.AreEqual(70, DisplayOptions.NextCapSliderPosition(63, 1, 20));
            Assert.AreEqual(60, DisplayOptions.NextCapSliderPosition(63, -1, 20));
        }

        [Test]
        public void Stepping_StopsAtBothEnds()
        {
            Assert.AreEqual(DisplayOptions.CapSliderLast,
                DisplayOptions.NextCapSliderPosition(DisplayOptions.CapSliderLast, 1, 30));
            Assert.AreEqual(DisplayOptions.CapSliderLast,
                DisplayOptions.NextCapSliderPosition(DisplayOptions.MaximumFrameCap, 1, 0));
            Assert.AreEqual(DisplayOptions.CapSliderFirst,
                DisplayOptions.NextCapSliderPosition(DisplayOptions.CapSliderFirst, -1, 30));
            Assert.AreEqual(DisplayOptions.CapSliderFirst,
                DisplayOptions.NextCapSliderPosition(DisplayOptions.MinimumFrameCap, -1, 0));
        }

        [Test]
        public void HolderNote_SaysWhenVSyncHoldsTheCap_AndWhatJustUnderResolvedTo()
        {
            Assert.AreEqual("Held by VSync on its own", DisplayOptions.DescribeCapHolder(200, 200, 1));
            Assert.AreEqual("Held by VSync, one frame every 2 refreshes", DisplayOptions.DescribeCapHolder(100, 100, 2));
            Assert.AreEqual(string.Empty, DisplayOptions.DescribeCapHolder(73, 73, 0));
            StringAssert.StartsWith("192 FPS", DisplayOptions.DescribeCapHolder(DisplayOptions.MatchDisplay, 192, 0));
        }
    }
}
