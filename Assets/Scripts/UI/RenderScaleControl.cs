using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The render scale slider: any whole percent from 50 to 100.
    ///
    /// It stepped in tens as a cycler until 17 November 2026, on the argument
    /// that 73% is not a choice anybody means to make. Ian asked for the slider
    /// anyway, and the stored scale was always a fraction, so nothing about the
    /// setting changed but the control.
    ///
    /// It decides the scale only with no upscaler or with one on Custom, and
    /// never while dynamic resolution is moving it (<see
    /// cref="GraphicsDirector.RenderScaleDecides"/>). The rest of the time it
    /// goes dim and shows what the game is actually rendering at, re-read four
    /// times a second while dynamic resolution moves it.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Render Scale Control")]
    public sealed class RenderScaleControl : GraphicsSliderControl
    {
        protected override int First => DisplayOptions.MinimumRenderScalePercent;

        protected override int Last => DisplayOptions.MaximumRenderScalePercent;

        protected override int Position(GraphicsDirector director) =>
            DisplayOptions.RenderScaleToPercent(Available(director)
                ? director.RenderScale
                : director.EffectiveRenderScale);

        protected override void Apply(GraphicsDirector director, int position)
        {
            if (position != DisplayOptions.RenderScaleToPercent(director.RenderScale))
            {
                director.RenderScale = DisplayOptions.PercentToRenderScale(position);
            }
        }

        // What the game is actually rendering at, which an upscaler or dynamic
        // resolution decides rather than this control.
        protected override string Describe(GraphicsDirector director) =>
            Mathf.RoundToInt(director.EffectiveRenderScale * 100f) + "%";

        protected override bool Available(GraphicsDirector director) =>
            director.DynamicResolutionSupported && director.RenderScaleDecides;

        protected override bool Live(GraphicsDirector director) => director.DynamicResolutionOn;

        protected override string Note(GraphicsDirector director)
        {
            if (!director.DynamicResolutionSupported)
            {
                return "Dynamic resolution is off in this quality tier";
            }

            // Most specific first, because more than one of these can be true at
            // once. Dynamic resolution moves the scale every frame whatever else
            // is running, so it answers before the upscaler.
            if (director.DynamicResolutionOn)
            {
                return "Set by dynamic resolution";
            }

            if (director.Method == UpscaleMethod.Off && director.AntiAliasing == AntiAliasingMode.Dlaa)
            {
                return "DLAA renders at native, so there is nothing to scale";
            }

            // Custom is the mode where an upscaler is running and this row still
            // decides, so it says what the number means rather than why it is dark.
            if (director.Method != UpscaleMethod.Off && director.CustomScale)
            {
                return "What " + DisplayOptions.UpscaleMethodNames[(int)director.Method] +
                    " renders at before reconstructing to native";
            }

            return director.Method != UpscaleMethod.Off
                ? "Set by the upscaler - pick Custom quality to set it here"
                : string.Empty;
        }
    }
}
