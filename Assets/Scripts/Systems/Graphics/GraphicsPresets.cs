namespace SurvivalChaos
{
    /// <summary>
    /// Where each per-effect row sits when the player picks a quality tier and
    /// changes nothing else.
    ///
    /// Every field here is a Volume override. Nothing in this file reaches the
    /// pipeline asset, and nothing writes one - the tier's asset is Unity's own,
    /// shipped as authored, and the only thing selecting a tier does is call
    /// QualitySettings.SetQualityLevel.
    ///
    /// That is the whole of the difference from what this file used to be. It
    /// once carried shadow atlas sizes, texture mip limits, anisotropic modes,
    /// sky and cube reflection resolutions and a reflection cache size, all of
    /// which were stamped into eight generated .asset files by a builder tool.
    /// Those numbers were invented here rather than measured, and several were
    /// wrong in ways that took a build to find: a flat 512 reflection cache
    /// failed its allocation every frame, and ray-traced GI on the top tiers
    /// rendered every dynamic object as a black silhouette.
    /// </summary>
    public struct GraphicsPreset
    {
        public string Name;

        /// <summary>
        /// Contact shadows: the short-range contact darkening HDRP traces in
        /// screen space.
        ///
        /// All that is left of what used to be a six-rung Shadow Quality row.
        /// The rest of that row - shadowmask, atlas size, request count,
        /// filtering - lives in the pipeline asset, which is no longer ours to
        /// write.
        /// </summary>
        public EffectQuality ContactShadows;

        public EffectQuality AmbientOcclusion;
        public EffectQuality Reflections;
        public EffectQuality GlobalIllumination;
        public EffectQuality VolumetricFog;
    }

    /// <summary>
    /// The three tiers, in quality level order, matching Unity's own stock HDRP
    /// assets: Performant, Balanced and High Fidelity, renamed Low, Medium and
    /// High.
    ///
    /// Three rather than seven because these are the assets Unity ships and
    /// keeps self-consistent. Anything above or below is a new asset to author
    /// by hand, not a table of numbers for this file to invent.
    ///
    /// **Reflections are off at every tier, and not by taste.** All three stock
    /// assets ship `supportSSR: false`, which is a hard gate - the volume
    /// override cannot switch on an effect the pipeline did not compile. The row
    /// still exists and greys itself out, which is the honest reading; defaulting
    /// it to Low would just be a row that claims to do something and does not.
    ///
    /// **GI is off at every tier**, for the reason it has always been off: this
    /// scene's indirect light is baked, into lightmaps and Adaptive Probe
    /// Volumes, and screen-space or ray-traced GI replaces that rather than
    /// adding to it. Dynamic objects - the ships, the only things the player
    /// actually watches - lose their indirect light entirely and render as black
    /// silhouettes against terrain that kept its lightmap. That shipped once.
    ///
    /// The ray-traced rungs are unreachable here too: `supportRayTracing` is
    /// false in all three stock assets. The rows drop to four entries and say so.
    ///
    /// **Motion blur is deliberately absent**, as it has been since it left the
    /// preset system. It is taste rather than fidelity, and a tier stamping over
    /// that choice every time quality changes would be the settings screen
    /// arguing with the player.
    /// </summary>
    public static class GraphicsPresets
    {
        public static readonly GraphicsPreset[] All =
        {
            new GraphicsPreset
            {
                // Unity's HDRP Performant. Also the only tier where volumetric
                // fog is gated off in the asset (supportVolumetrics: false), so
                // the fog row here is inert whatever it is set to.
                Name = "Low",
                ContactShadows = EffectQuality.Off,
                AmbientOcclusion = EffectQuality.Low,
                Reflections = EffectQuality.Off,
                GlobalIllumination = EffectQuality.Off,
                VolumetricFog = EffectQuality.Off
            },
            new GraphicsPreset
            {
                // Unity's HDRP Balanced.
                Name = "Medium",
                ContactShadows = EffectQuality.Low,
                AmbientOcclusion = EffectQuality.Medium,
                Reflections = EffectQuality.Off,
                GlobalIllumination = EffectQuality.Off,
                VolumetricFog = EffectQuality.Low
            },
            new GraphicsPreset
            {
                // Unity's HDRP High Fidelity. The only tier with
                // AdaptiveProbeVolumes, and so the only one that uses this
                // scene's probe volume bake.
                Name = "High",
                ContactShadows = EffectQuality.Medium,
                AmbientOcclusion = EffectQuality.High,
                Reflections = EffectQuality.Off,
                GlobalIllumination = EffectQuality.Off,
                VolumetricFog = EffectQuality.Medium
            }
        };

        /// <summary>
        /// The tier anything ambiguous falls back to: Medium.
        ///
        /// One constant rather than a 1 written in each place that needs it.
        /// </summary>
        public const int DefaultIndex = 1;

        public static int Count => All.Length;

        public static GraphicsPreset At(int index)
        {
            if (index < 0)
            {
                index = 0;
            }

            if (index >= All.Length)
            {
                index = All.Length - 1;
            }

            return All[index];
        }
    }
}
