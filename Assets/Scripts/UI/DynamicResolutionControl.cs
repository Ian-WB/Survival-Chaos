using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The dynamic resolution slider: off at the left end, then the frame rate
    /// to hold, any whole number from 30 to 300.
    ///
    /// It was a cycler over seven fixed targets until 17 November 2026. Like
    /// the frame cap it sits beside, the right target depends on the display
    /// and the machine rather than on a list, so every whole number is offered.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Dynamic Resolution Control")]
    public sealed class DynamicResolutionControl : GraphicsSliderControl
    {
        protected override int First => DisplayOptions.DynamicSliderFirst;

        protected override int Last => DisplayOptions.DynamicSliderLast;

        protected override int Position(GraphicsDirector director) =>
            DisplayOptions.DynamicTargetToSlider(director.DynamicTarget);

        protected override void Apply(GraphicsDirector director, int position)
        {
            int target = DisplayOptions.SliderToDynamicTarget(position);

            // The setter restarts the controller, so only on a real change.
            if (target != director.DynamicTarget)
            {
                director.DynamicTarget = target;
            }
        }

        protected override string Describe(GraphicsDirector director) =>
            DisplayOptions.DescribeDynamicTarget(director.DynamicTarget);

        protected override bool Available(GraphicsDirector director) => director.DynamicResolutionSupported;

        protected override string Note(GraphicsDirector director)
        {
            if (!director.DynamicResolutionSupported)
            {
                return "Dynamic resolution is off in this quality tier";
            }

            // The two used to be mutually exclusive: each upscaler installed its
            // own scaler into a slot HDRP prefers, so the controller ran and was
            // discarded. They compose now, and this is the pairing both vendors
            // actually build for.
            if (director.DynamicFeedsUpscaler)
            {
                return "Moves the scale " + DisplayOptions.UpscaleMethodNames[(int)director.Method] +
                    " reconstructs from";
            }

            return director.DynamicResolutionOn
                ? "Drops resolution to hold the target, never below 50%"
                : string.Empty;
        }
    }
}
