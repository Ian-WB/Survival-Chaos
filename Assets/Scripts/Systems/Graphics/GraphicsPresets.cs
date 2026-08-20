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
    /// The four tiers, in quality level order. Three of them are Unity's own
    /// stock HDRP assets - Performant, Balanced and High Fidelity, renamed Low,
    /// Medium and Ultra. High is the exception: a copy of High Fidelity, stepped
    /// down where the cost sits rather than where the feature list does.
    ///
    /// It used to be three, on the reasoning that those were the assets Unity
    /// ships and keeps self-consistent, and that anything else was an asset to
    /// author by hand rather than a table of numbers to invent here. That still
    /// holds - High *is* hand-authored now, and the numbers separating it from
    /// Ultra live in the asset where they can be measured, not in this file.
    ///
    /// **High and Ultra compile the same features.** Both carry SSR, SSR on
    /// transparents, SSGI, volumetrics, volumetric clouds and ray tracing. What
    /// separates them is budget: shadow atlas 4096 against 8192, probe volumes
    /// at L1 against L2 spherical harmonics, sky reflection and cookie atlas
    /// 2048 against 4096. None of that is a volume override, which is why the
    /// two rows below are nearly identical - the tier does the work, not the
    /// table.
    ///
    /// **Reflections are off on Low and on above it.** Low's asset ships
    /// `supportSSR: false`, which is a hard gate - a volume override cannot
    /// switch on an effect the pipeline never compiled - so the row greys
    /// itself out there instead of pretending. Everything above Low compiles it,
    /// and each gets the rung its tier can afford. Reflections top out at High
    /// on both of the upper tiers because that is the last rung before the
    /// ray-traced ones, which no tier selects by default.
    ///
    /// **Volumetric clouds arrive at High.** Medium's asset ships
    /// `supportVolumetricClouds: false`, the same kind of hard gate as SSR on
    /// Low. There is no row for clouds, so like SSR on transparents it simply
    /// comes with the tier.
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

            // A copy of High Fidelity with its budgets stepped down. First tier
            // to compile volumetric clouds, which is the visible difference from
            // Medium; the rest of the gap is resolution rather than features.
            new GraphicsPreset(
                name: "High",
                reflections: EffectQuality.High,
                globalIllumination: EffectQuality.Off,
                volumetricFog: EffectQuality.Medium),

            // Unity's HDRP High Fidelity, unedited. Only the fog rung separates
            // this row from High's - reflections are already on their last
            // non-ray-traced step, and GI is off everywhere for the reason
            // above. The tier earns its name in the asset, not here.
            new GraphicsPreset(
                name: "Ultra",
                reflections: EffectQuality.High,
                globalIllumination: EffectQuality.Off,
                volumetricFog: EffectQuality.High)
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
