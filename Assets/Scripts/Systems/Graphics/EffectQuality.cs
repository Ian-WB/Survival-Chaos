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
    // now. Shadows came back as a row that writes none of them: see ShadowLadder.

    /// <summary>
    /// What the Shadows row sets at each rung.
    ///
    /// It reaches the scene's authored casters - the sun, the lava light that
    /// casts, and the volumetric clouds - and nothing in the pipeline asset. The
    /// lights take one of the tier's own resolution levels, so a tier still
    /// decides what Low means on it; the clouds take a resolution directly,
    /// because theirs is a volume parameter with no per-tier table behind it.
    ///
    /// Off, Low, Medium and High only. The row has no ray-traced form, and a
    /// stored rung above High - a corrupt or foreign settings file - reads as
    /// High rather than as whatever the ladder's arithmetic would make of it.
    /// </summary>
    public static class ShadowLadder
    {
        public const EffectQuality Highest = EffectQuality.High;

        public static EffectQuality Clamp(EffectQuality quality)
        {
            if (quality < EffectQuality.Off)
            {
                return EffectQuality.Off;
            }

            return quality > Highest ? Highest : quality;
        }

        /// <summary>
        /// The HDRP shadow resolution level, 0 to 2. Never 3: a point light draws
        /// six faces, and six at the tier's Ultra level outgrow its punctual atlas.
        /// </summary>
        public static int LightLevel(EffectQuality quality)
        {
            return QualityLadder.ScalableLevel(Clamp(quality));
        }

        /// <summary>
        /// The cloud shadow map's size, matching HDRP's CloudShadowResolution
        /// values. Medium is the 256 the scene was authored with.
        ///
        /// Measured, and the rungs do not show. Holding the lights still and
        /// moving only this, 128 against 512 changed 5.0% of the frame by under
        /// 8 of 255 on every pixel it touched - and two captures taken at the
        /// same 512 changed 4.5% of it by the same amount, because the clouds
        /// reproject across frames. The step is quieter than the renderer's own
        /// noise, at this camera, on this tier, in a night scene.
        ///
        /// Kept rather than collapsed to one number, because that is one set of
        /// conditions and a rung costing a smaller map is not a rung costing
        /// anything. What it is not is a reason to reach for this row: the
        /// on/off is the part of the clouds that reads. CloudShadowDistance has
        /// the arithmetic for why the map is coarse to begin with.
        /// </summary>
        public static int CloudResolution(EffectQuality quality)
        {
            switch (Clamp(quality))
            {
                case EffectQuality.Medium:
                    return 256;

                case EffectQuality.High:
                    return 512;

                // Off turns cloud shadows off rather than using this; Low is the
                // cheapest thing to leave configured behind it.
                default:
                    return 128;
            }
        }

        /// <summary>
        /// How far from the camera cloud shadows are computed, in units.
        ///
        /// Not a box size, which is what the name suggests and what this was
        /// first written against. HDRP takes the camera frustum's four far
        /// corners, shortens each to at most this distance, and fits the shadow
        /// map to the light-space bounds of what is left - so the number caps
        /// the frustum rather than describing the region.
        ///
        /// Against this camera - 80 degree field of view, far plane 1000 - the
        /// stock 8000 never binds on anything: the region comes out 2983 units
        /// on its long axis, which puts one texel of a 512 map across 5.8 units
        /// of an island that is 37 units wide. The whole arena lands in about
        /// six texels, and in under two at 128.
        ///
        /// 1000 is HDRP's own floor for the parameter and the most this row can
        /// do about it. It binds on the far corners and brings the long axis to
        /// 1965, a third off the texel size for no cost. The real ceiling is the
        /// camera's far plane, which is a rendering decision rather than a
        /// quality one, so it is recorded here and left alone.
        /// </summary>
        public const float CloudShadowDistance = 1000f;
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
