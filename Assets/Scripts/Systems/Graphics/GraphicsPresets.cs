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
    /// <remarks>
    /// A readonly struct rather than four public fields. These three rows are
    /// the whole of what a tier decides, they are authored once in the table
    /// below and never anywhere else, and a preset that could be edited after
    /// the fact would be a tier quietly disagreeing with the asset it names.
    /// </remarks>
    public readonly struct GraphicsPreset
    {
        public GraphicsPreset(
            string name,
            EffectQuality reflections,
            EffectQuality globalIllumination,
            EffectQuality volumetricFog)
        {
            Name = name;
            Reflections = reflections;
            GlobalIllumination = globalIllumination;
            VolumetricFog = volumetricFog;
        }

        public string Name { get; }

        public EffectQuality Reflections { get; }

        public EffectQuality GlobalIllumination { get; }

        public EffectQuality VolumetricFog { get; }
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
    /// **Reflections are off on Low and on above it.** Low's asset ships
    /// `supportSSR: false`, which is a hard gate - a volume override cannot
    /// switch on an effect the pipeline never compiled - so the row greys
    /// itself out there instead of pretending. Medium and High do compile it,
    /// and each gets the rung its tier can afford. High also compiles SSR on
    /// transparent surfaces; that is a pipeline flag with no row of its own,
    /// so it simply comes with the tier.
    ///
    /// **GI is off at every tier**, for the reason it has always been off: this
    /// scene's indirect light is baked, into lightmaps and Adaptive Probe
    /// Volumes, and screen-space or ray-traced GI replaces that rather than
    /// adding to it. Dynamic objects - the ships, the only things the player
    /// actually watches - lose their indirect light entirely and render as black
    /// silhouettes against terrain that kept its lightmap. That shipped once.
    ///
    /// The ray-traced rungs are reachable on Medium and High, which compile
    /// `supportRayTracing`, wherever the GPU reports DXR. Low keeps four
    /// entries and its rows say why. Nothing here defaults to a ray-traced
    /// rung: they are opt-in, and ray-traced global illumination in particular
    /// is the exact setting that produced the silhouettes described above.
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
            // Unity's HDRP Performant. Also the only tier where volumetric fog is
            // gated off in the asset (supportVolumetrics: false), so the fog row
            // here is inert whatever it is set to.
            new GraphicsPreset(
                name: "Low",
                reflections: EffectQuality.Off,
                globalIllumination: EffectQuality.Off,
                volumetricFog: EffectQuality.Off),

            // Unity's HDRP Balanced.
            new GraphicsPreset(
                name: "Medium",
                reflections: EffectQuality.Medium,
                globalIllumination: EffectQuality.Off,
                volumetricFog: EffectQuality.Low),

            // Unity's HDRP High Fidelity. Compiles the same set of effects as
            // Medium does now; what still separates the two is sky reflection
            // resolution and the rungs below.
            new GraphicsPreset(
                name: "High",
                reflections: EffectQuality.High,
                globalIllumination: EffectQuality.Off,
                volumetricFog: EffectQuality.Medium)
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
