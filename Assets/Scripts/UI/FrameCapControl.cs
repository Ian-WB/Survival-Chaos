using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The frame cap slider.
    ///
    /// It was a cycler over nine fixed rates until 16 November 2026. A cap has
    /// no performance cliff to step around - 73 is as good a cap as 72 or 75,
    /// and the right one depends on the display and the machine - so every
    /// whole number from 30 to 300 is a real choice. The two that are not
    /// numbers keep an end each: Just under display on the left, uncapped on
    /// the right (<see cref="DisplayOptions.CapToSlider"/>).
    /// </summary>
    [AddComponentMenu("Survival Chaos/Frame Cap Control")]
    public sealed class FrameCapControl : GraphicsSliderControl
    {
        protected override int First => DisplayOptions.CapSliderFirst;

        protected override int Last => DisplayOptions.CapSliderLast;

        protected override int Position(GraphicsDirector director) =>
            DisplayOptions.CapToSlider(director.FrameCap);

        protected override void Apply(GraphicsDirector director, int position)
        {
            int cap = DisplayOptions.SliderToCap(position);
            if (cap != director.FrameCap)
            {
                director.FrameCap = cap;
            }
        }

        protected override string Describe(GraphicsDirector director) =>
            DisplayOptions.DescribeCap(director.FrameCap);

        protected override string Note(GraphicsDirector director) =>
            DisplayOptions.DescribeCapHolder(director.FrameCap, director.ResolvedFrameCap,
                director.CapSyncInterval);
    }
}
