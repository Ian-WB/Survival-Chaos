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

    /// <summary>
    /// How much of the shadow budget a tier spends.
    ///
    /// One rung covers seven pipeline fields - shadowmask, dynamic shadows,
    /// atlas size, request count, per-shadow resolution, filtering and contact
    /// shadows - because they are not independent choices. Raising the request
    /// count without the atlas to hold them just makes every shadow smaller, and
    /// nobody picking a settings row means to do that.
    ///
    /// Named to match the quality presets so "Shadow Quality: Medium" reads as
    /// "shadows the way the Medium preset has them" with no legend needed.
    /// </summary>
    public enum ShadowQualityLevel
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        VeryHigh = 4,
        Ultra = 5
    }

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

        public static readonly string[] ShadowNames =
        {
            "Off", "Low", "Medium", "High", "Very High", "Ultra"
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

        public static string Describe(ShadowQualityLevel level)
        {
            int index = (int)level;
            return index >= 0 && index < ShadowNames.Length ? ShadowNames[index] : "-";
        }
    }
}
