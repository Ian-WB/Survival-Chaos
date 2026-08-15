using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>
    /// What one quality preset sets every row to.
    ///
    /// This is the thing "Custom" is defined against: picking a preset stamps all
    /// of these, and changing any single row afterwards leaves the rest where the
    /// preset put them and renames the selection to Custom.
    /// </summary>
    public struct GraphicsPreset
    {
        public string Name;
        public ShadowQualityLevel Shadows;
        public EffectQuality AmbientOcclusion;
        public EffectQuality Reflections;
        public EffectQuality GlobalIllumination;
        /// <summary>
        /// Capped at Medium, and only Ultra reaches even that.
        ///
        /// High is deliberately out of reach of every preset. Volumetric fog at
        /// the top rung costs more than its visible difference is worth in an
        /// arena this size, so nobody should arrive at it by picking a preset -
        /// but the row still offers it, for anyone who wants to spend the frame
        /// time knowingly.
        /// </summary>
        public EffectQuality VolumetricFog;

        /// <summary>0 renders textures at full resolution, 1 at half.</summary>
        public int TextureMipLimit;

        public AnisotropicFiltering Anisotropic;

        /// <summary>
        /// Whether the pipeline compiles ray tracing support in at all.
        ///
        /// Separate from the per-effect rungs because it is a build-time cost -
        /// shader variants and memory - paid whether or not any effect is
        /// currently using it. Only the minimum-spec tier declines it.
        /// </summary>
        public bool RayTracing;

        public bool SubsurfaceScattering;
        public bool Decals;

        /// <summary>How sharply the sky is reflected off the ships.</summary>
        public SkyResolution SkyReflection;

        /// <summary>
        /// The High rung of the cube reflection resolution table, which is what
        /// the arena's one baked reflection probe is authored to use.
        /// </summary>
        public CubeReflectionResolution CubeReflection;

        /// <summary>
        /// The atlas the sky reflection and the reflection probe are both packed
        /// into.
        ///
        /// Has to stay ahead of the two of them together. Sized below, HDRP fails
        /// the allocation and logs that the atlas is full once per frame forever -
        /// which is exactly what a flat 512 here did.
        ///
        /// This looked like a dead lever twice over and is not. The comment this
        /// file inherited said the project has no reflection probes; Game.unity
        /// has had one since before any of this work. And even with none, HDRP
        /// packs the sky reflection into the same atlas, and this arena is lit
        /// entirely by an HDRI cubemap. Check the scene, not the comment.
        /// </summary>
        public ReflectionProbeTextureCacheResolution ReflectionCache;

        /// <summary>
        /// Applies the extra video-memory austerity in the builder's
        /// ApplyMinimumSpec on top of everything else.
        ///
        /// Its own field rather than inferred from ray tracing being off. They
        /// happen to coincide on the one tier that sets both, and a reader who
        /// took that for a rule would be wrong the moment a tier wants one
        /// without the other.
        /// </summary>
        public bool MinimumSpec;
    }

    /// <summary>
    /// The seven presets, in quality level order.
    ///
    /// Every ladder here is monotonic on purpose. The previous arrangement was
    /// not: LOD bias ran 1.0 on the bottom tier against 0.3 on the one above it,
    /// and the shadowmask mode alternated 1, 0, 0, 0, 1, 1, 1 - values nobody
    /// chose, left over from duplicating quality levels.
    ///
    /// Volumetric clouds appear nowhere. There is no sky worth clouding in an
    /// arena that sits inside a cubemap, so they are off at every tier and have
    /// no row.
    ///
    /// **No preset turns on global illumination, and no preset selects a
    /// ray-traced rung.** Both are reachable from the rows; neither is a default,
    /// for two separate reasons.
    ///
    /// GI is not an addition here, it is a replacement. This scene's indirect
    /// light comes from baked lightmaps and Adaptive Probe Volumes, carefully
    /// enough that a bake costs tens of megabytes of LFS quota. Switching on
    /// screen-space or ray-traced GI hands that job to a solver that has never
    /// seen the bake, and dynamic objects - the ships, which are the only things
    /// the player actually looks at - lose their indirect light entirely and
    /// render as black silhouettes against a terrain that kept its lightmap.
    /// That shipped once in a build and looked like the shadow rows were broken.
    ///
    /// Ray tracing is off by default because it is untested rather than because
    /// it is wrong. Shipped games treat RT as opt-in for the same reason.
    ///
    /// **Motion blur is not here at all.** It is taste rather than fidelity -
    /// plenty of players turn it off on hardware that could run it at maximum,
    /// and some cannot stand it at any setting. A preset stamping over that
    /// choice every time the player changes quality would be the settings screen
    /// arguing with them. GraphicsDirector keeps it outside the preset system:
    /// picking a preset leaves it alone, and changing it does not make the
    /// selection Custom.
    /// </summary>
    public static class GraphicsPresets
    {
        public static readonly GraphicsPreset[] All =
        {
            new GraphicsPreset
            {
                // Below Very Low, for hardware below what HDRP is built for. The
                // reference machine is an Intel HD Graphics 4000 with 128 MB of
                // dedicated video memory. What separates it from Very Low is not
                // effects - those are already at their floor - but memory.
                Name = "Ubirajara",
                Shadows = ShadowQualityLevel.Off,
                AmbientOcclusion = EffectQuality.Off,
                Reflections = EffectQuality.Off,
                GlobalIllumination = EffectQuality.Off,
                VolumetricFog = EffectQuality.Off,
                TextureMipLimit = 1, Anisotropic = AnisotropicFiltering.Disable,
                RayTracing = false, SubsurfaceScattering = false, Decals = false,
                SkyReflection = SkyResolution.SkyResolution128,
                CubeReflection = CubeReflectionResolution.CubeReflectionResolution128,
                ReflectionCache = ReflectionProbeTextureCacheResolution.Resolution512x512,
                MinimumSpec = true
            },
            new GraphicsPreset
            {
                Name = "Very Low",
                Shadows = ShadowQualityLevel.Off,
                AmbientOcclusion = EffectQuality.Off,
                Reflections = EffectQuality.Off,
                GlobalIllumination = EffectQuality.Off,
                VolumetricFog = EffectQuality.Off,
                TextureMipLimit = 1, Anisotropic = AnisotropicFiltering.Disable,
                RayTracing = true, SubsurfaceScattering = false, Decals = false,
                SkyReflection = SkyResolution.SkyResolution256,
                CubeReflection = CubeReflectionResolution.CubeReflectionResolution256,
                ReflectionCache = ReflectionProbeTextureCacheResolution.Resolution512x512
            },
            new GraphicsPreset
            {
                Name = "Low",
                Shadows = ShadowQualityLevel.Low,
                AmbientOcclusion = EffectQuality.Low,
                Reflections = EffectQuality.Off,
                GlobalIllumination = EffectQuality.Off,
                VolumetricFog = EffectQuality.Off,
                TextureMipLimit = 0, Anisotropic = AnisotropicFiltering.Disable,
                RayTracing = true, SubsurfaceScattering = false, Decals = false,
                SkyReflection = SkyResolution.SkyResolution256,
                CubeReflection = CubeReflectionResolution.CubeReflectionResolution256,
                ReflectionCache = ReflectionProbeTextureCacheResolution.Resolution512x512
            },
            new GraphicsPreset
            {
                Name = "Medium",
                Shadows = ShadowQualityLevel.Medium,
                AmbientOcclusion = EffectQuality.Medium,
                Reflections = EffectQuality.Off,
                GlobalIllumination = EffectQuality.Off,
                VolumetricFog = EffectQuality.Low,
                TextureMipLimit = 0, Anisotropic = AnisotropicFiltering.Enable,
                RayTracing = true, SubsurfaceScattering = true, Decals = true,
                SkyReflection = SkyResolution.SkyResolution512,
                CubeReflection = CubeReflectionResolution.CubeReflectionResolution512,
                ReflectionCache = ReflectionProbeTextureCacheResolution.Resolution1024x1024
            },
            new GraphicsPreset
            {
                Name = "High",
                Shadows = ShadowQualityLevel.High,
                AmbientOcclusion = EffectQuality.High,
                Reflections = EffectQuality.Medium,
                GlobalIllumination = EffectQuality.Off,
                VolumetricFog = EffectQuality.Low,
                TextureMipLimit = 0, Anisotropic = AnisotropicFiltering.Enable,
                RayTracing = true, SubsurfaceScattering = true, Decals = true,
                SkyReflection = SkyResolution.SkyResolution512,
                CubeReflection = CubeReflectionResolution.CubeReflectionResolution512,
                ReflectionCache = ReflectionProbeTextureCacheResolution.Resolution1024x1024
            },
            new GraphicsPreset
            {
                Name = "Very High",
                Shadows = ShadowQualityLevel.VeryHigh,
                AmbientOcclusion = EffectQuality.High,
                Reflections = EffectQuality.High,
                GlobalIllumination = EffectQuality.Off,
                VolumetricFog = EffectQuality.Low,
                TextureMipLimit = 0, Anisotropic = AnisotropicFiltering.ForceEnable,
                RayTracing = true, SubsurfaceScattering = true, Decals = true,
                SkyReflection = SkyResolution.SkyResolution1024,
                CubeReflection = CubeReflectionResolution.CubeReflectionResolution1024,
                ReflectionCache = ReflectionProbeTextureCacheResolution.Resolution2048x2048
            },
            new GraphicsPreset
            {
                Name = "Ultra",
                Shadows = ShadowQualityLevel.Ultra,
                AmbientOcclusion = EffectQuality.High,
                Reflections = EffectQuality.High,
                GlobalIllumination = EffectQuality.Off,
                VolumetricFog = EffectQuality.Medium,
                TextureMipLimit = 0, Anisotropic = AnisotropicFiltering.ForceEnable,
                RayTracing = true, SubsurfaceScattering = true, Decals = true,
                SkyReflection = SkyResolution.SkyResolution1024,
                CubeReflection = CubeReflectionResolution.CubeReflectionResolution1024,
                ReflectionCache = ReflectionProbeTextureCacheResolution.Resolution2048x2048
            }
        };

        /// <summary>
        /// The name the quality level carries for anything the player has tuned
        /// themselves.
        ///
        /// Appended after the seven presets, so preset indices stay stable and a
        /// saved choice keeps meaning the tier it meant when it was written.
        /// </summary>
        public const string CustomName = "Custom";

        /// <summary>
        /// The preset anything ambiguous falls back to: Medium.
        ///
        /// One constant rather than a 3 written in each place that needs it. The
        /// director and the preset builder both used their own, which is how two
        /// numbers that must agree drift apart.
        ///
        /// Deliberately not Ubirajara. Index 0 is the tempting default because it
        /// is first, but it is a machine-specific fallback for a 2012 integrated
        /// GPU - landing there by accident is a severe downgrade nobody chose.
        /// </summary>
        public const int DefaultIndex = 3;

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

        /// <summary>
        /// True when <paramref name="index"/> is the Custom level rather than one
        /// of the seven presets.
        /// </summary>
        public static bool IsCustom(int index)
        {
            return index >= All.Length;
        }
    }
}
