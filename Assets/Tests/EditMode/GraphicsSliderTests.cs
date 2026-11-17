using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Render scale and dynamic resolution became sliders on 17 November 2026,
    /// beside the frame cap. Both settings are stored as they always were - the
    /// scale as a fraction, the target as a frame rate - so these pin that a
    /// value saved by the old cyclers loads as what it was.
    /// </summary>
    public class GraphicsSliderTests
    {
        [Test]
        public void RenderScale_IsWholePercentFromHalfToNative()
        {
            Assert.AreEqual(50, DisplayOptions.MinimumRenderScalePercent);
            Assert.AreEqual(100, DisplayOptions.MaximumRenderScalePercent);

            for (int percent = 50; percent <= 100; percent++)
            {
                Assert.AreEqual(percent, DisplayOptions.RenderScaleToPercent(DisplayOptions.PercentToRenderScale(percent)));
            }

            Assert.AreEqual(0.73f, DisplayOptions.PercentToRenderScale(73), 1e-6f);
        }

        [Test]
        public void RenderScale_ClampsBothWays()
        {
            Assert.AreEqual(50, DisplayOptions.RenderScaleToPercent(0.2f));
            Assert.AreEqual(100, DisplayOptions.RenderScaleToPercent(1.4f));
            Assert.AreEqual(0.5f, DisplayOptions.PercentToRenderScale(10), 1e-6f);
            Assert.AreEqual(1f, DisplayOptions.PercentToRenderScale(130), 1e-6f);
        }

        [Test]
        public void EveryScaleTheOldCyclerSaved_SitsOnItsOwnPercent()
        {
            foreach (float saved in DisplayOptions.FormerRenderScales)
            {
                int percent = DisplayOptions.RenderScaleToPercent(saved);
                Assert.AreEqual(UnityEngine.Mathf.RoundToInt(saved * 100f), percent, saved + " moved");
                Assert.AreEqual(saved, DisplayOptions.PercentToRenderScale(percent), 1e-6f);
            }
        }

        [Test]
        public void DynamicTarget_ClampsIntoThirtyToThreeHundred_AndKeepsOff()
        {
            Assert.AreEqual(0, DisplayOptions.ClampDynamicTarget(0));
            Assert.AreEqual(0, DisplayOptions.ClampDynamicTarget(-5));
            Assert.AreEqual(30, DisplayOptions.ClampDynamicTarget(12));
            Assert.AreEqual(300, DisplayOptions.ClampDynamicTarget(999));

            for (int target = 30; target <= 300; target++)
            {
                Assert.AreEqual(target, DisplayOptions.ClampDynamicTarget(target));
                Assert.AreEqual(target, DisplayOptions.SliderToDynamicTarget(DisplayOptions.DynamicTargetToSlider(target)));
            }
        }

        [Test]
        public void DynamicTarget_OffIsTheLeftEnd()
        {
            Assert.AreEqual(DisplayOptions.DynamicSliderFirst, DisplayOptions.DynamicTargetToSlider(0));
            Assert.AreEqual(0, DisplayOptions.SliderToDynamicTarget(DisplayOptions.DynamicSliderFirst));
            Assert.AreEqual(0, DisplayOptions.SliderToDynamicTarget(0));
            Assert.AreEqual(300, DisplayOptions.SliderToDynamicTarget(DisplayOptions.DynamicSliderLast));
            Assert.AreEqual("Off", DisplayOptions.DescribeDynamicTarget(0));
            Assert.AreEqual("Target 73 FPS", DisplayOptions.DescribeDynamicTarget(73));
        }

        [Test]
        public void EveryTargetTheOldCyclerSaved_LoadsAsItself()
        {
            foreach (int saved in DisplayOptions.FormerDynamicTargets)
            {
                Assert.AreEqual(saved, DisplayOptions.ClampDynamicTarget(saved));
                Assert.AreEqual(saved, DisplayOptions.SliderToDynamicTarget(DisplayOptions.DynamicTargetToSlider(saved)));
            }
        }

        [Test]
        public void Stepping_HoldsToEachSlidersOwnEnds()
        {
            Assert.AreEqual(51, DisplayOptions.NextSliderPosition(50, 1, 0, 50, 100));
            Assert.AreEqual(50, DisplayOptions.NextSliderPosition(50, -1, 0, 50, 100));
            Assert.AreEqual(100, DisplayOptions.NextSliderPosition(98, 1, 30, 50, 100));
            Assert.AreEqual(75, DisplayOptions.NextSliderPosition(73, 1, 5, 50, 100));

            Assert.AreEqual(DisplayOptions.DynamicSliderFirst,
                DisplayOptions.NextSliderPosition(30, -1, 0, DisplayOptions.DynamicSliderFirst, DisplayOptions.DynamicSliderLast));
            Assert.AreEqual(DisplayOptions.DynamicSliderLast,
                DisplayOptions.NextSliderPosition(299, 1, 30, DisplayOptions.DynamicSliderFirst, DisplayOptions.DynamicSliderLast));
        }
    }
}
