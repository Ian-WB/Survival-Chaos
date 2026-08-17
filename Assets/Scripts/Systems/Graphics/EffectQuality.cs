namespace SurvivalChaos
{
    /// <summary>
    /// One rung on an effect's cost ladder, from off to ray traced.
    ///
    /// Screen-space and ray-traced levels share a single ladder rather than
    /// living in two rows. They are the same decision - how much am I willing to
    /// spend on this effect - and splitting them creates a state where both are
    /// set to something and the player has to work out which one the renderer
    /// actually used.
    ///
    /// The three ray-traced rungs sit at the top because that is where they are
    /// on the cost curve, and they disappear from the row entirely on hardware or
    /// a pipeline that cannot run them.
    /// </summary>
    public enum EffectQuality
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        RayTracedLow = 4,
        RayTracedMedium = 5,
        RayTracedHigh = 6
    }

    // ShadowQualityLevel was here: a six-rung ladder covering shadowmask,
    // dynamic shadows, atlas size, request count, per-shadow resolution and
    // filtering. Every one of those is a field in the pipeline asset, and the
    // asset is no longer written from code - the stock HDRP tier decides them
    // now. What survived is contact shadows, which is a volume override, and it
    // rides the ordinary EffectQuality ladder like every other effect.

    /// <summary>
    /// Names and conversions for the quality ladders.
    ///
    /// Kept free of any render pipeline type so it can be read from the menu
    /// without dragging HDRP into the UI assembly.
    /// </summary>
    public static class QualityLadder
    {
        public static readonly string[] EffectNames =
        {
            "Off", "Low", "Medium", "High", "RT Low", "RT Medium", "RT High"
        };

        /// <summary>The first ray-traced rung; everything below it is screen space.</summary>
        public const EffectQuality FirstRayTraced = EffectQuality.RayTracedLow;

        /// <summary>How many rungs a row offers when ray tracing is unavailable.</summary>
        public const int ScreenSpaceCount = 4;

        public static bool IsRayTraced(EffectQuality quality)
        {
            return quality >= FirstRayTraced;
        }

        public static bool IsOn(EffectQuality quality)
        {
            return quality != EffectQuality.Off;
        }

        /// <summary>
        /// The 0-2 index HDRP's scalable settings use, for either half of the
        /// ladder.
        ///
        /// Both halves map onto the same three rungs because HDRP keeps separate
        /// tables for the screen-space and ray-traced form of each effect - the
        /// ray-traced ones are read only when the effect is in a ray-traced mode,
        /// so Low means "the low rung of whichever table applies".
        /// </summary>
        public static int ScalableLevel(EffectQuality quality)
        {
            switch (quality)
            {
                case EffectQuality.Low:
                case EffectQuality.RayTracedLow:
                    return 0;

                case EffectQuality.Medium:
                case EffectQuality.RayTracedMedium:
                    return 1;

                case EffectQuality.High:
                case EffectQuality.RayTracedHigh:
                    return 2;

                // Off has no rung. Low is the cheapest thing to leave configured
                // behind a disabled effect.
                default:
                    return 0;
            }
        }

        public static string Describe(EffectQuality quality)
        {
            int index = (int)quality;
            return index >= 0 && index < EffectNames.Length ? EffectNames[index] : "-";
        }
    }
}
