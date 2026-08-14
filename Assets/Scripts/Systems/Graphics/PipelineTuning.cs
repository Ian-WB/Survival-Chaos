using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>
    /// What the quality rungs actually mean, in pipeline asset terms.
    ///
    /// One definition, read by two callers that would otherwise drift: the editor
    /// tool that bakes the seven presets, and the runtime director that rebuilds
    /// a pipeline when the player changes something on the Custom preset. If each
    /// carried its own numbers, "Shadow Quality: High" would mean one thing when
    /// it came from a preset and another when the player chose it, which is the
    /// exact failure this whole ladder exists to remove.
    /// </summary>
    public static class PipelineTuning
    {
        /// <summary>
        /// Every shadow field one rung sets.
        ///
        /// Grouped rather than exposed separately because they are not
        /// independent. Raising the request count without the atlas to hold them
        /// divides the same pixels into more, smaller tiles - every shadow gets
        /// blurrier, which is the opposite of what asking for more shadows means.
        /// </summary>
        public struct ShadowRung
        {
            public bool Shadowmask;

            /// <summary>
            /// Distance shadowmask re-renders static shadows in real time inside
            /// the shadow distance and only falls back to the baked mask beyond
            /// it. In this arena that is close to pure waste - the geometry is
            /// static and baked, and only the ships move - so it is reserved for
            /// the top rung where the near-field crispness is affordable.
            /// </summary>
            public bool DistanceShadowmask;

            public bool DynamicShadows;
            public int Atlas;
            public int Requests;
            public int Resolution;
            public HDShadowFilteringQuality Filtering;
            public bool ContactShadows;
        }

        /// <summary>
        /// Indexed by <see cref="ShadowQualityLevel"/>.
        ///
        /// Each rung adds something visible rather than only scaling a number:
        /// Low brings the baked shadowmask back, Medium sharpens filtering, High
        /// adds contact shadows, Ultra switches to distance shadowmask.
        ///
        /// Off keeps one request and a 256 atlas rather than zero of either.
        /// HDShadowManager.InitShadowManager returns before allocating m_Atlas at
        /// maxShadowRequests 0, and the job data is then dereferenced unguarded -
        /// a NullReferenceException every frame, on shadows that never draw.
        /// Verified in HDRP 17.5.0.
        /// </summary>
        public static readonly ShadowRung[] Shadows =
        {
            new ShadowRung
            {
                Shadowmask = false, DistanceShadowmask = false, DynamicShadows = false,
                Atlas = 256, Requests = 1, Resolution = 256,
                Filtering = HDShadowFilteringQuality.Low, ContactShadows = false
            },
            new ShadowRung
            {
                Shadowmask = true, DistanceShadowmask = false, DynamicShadows = true,
                Atlas = 1024, Requests = 4, Resolution = 512,
                Filtering = HDShadowFilteringQuality.Low, ContactShadows = false
            },
            new ShadowRung
            {
                Shadowmask = true, DistanceShadowmask = false, DynamicShadows = true,
                Atlas = 2048, Requests = 8, Resolution = 1024,
                Filtering = HDShadowFilteringQuality.Medium, ContactShadows = false
            },
            new ShadowRung
            {
                Shadowmask = true, DistanceShadowmask = false, DynamicShadows = true,
                Atlas = 4096, Requests = 16, Resolution = 1024,
                Filtering = HDShadowFilteringQuality.Medium, ContactShadows = true
            },
            new ShadowRung
            {
                Shadowmask = true, DistanceShadowmask = false, DynamicShadows = true,
                Atlas = 4096, Requests = 24, Resolution = 2048,
                Filtering = HDShadowFilteringQuality.High, ContactShadows = true
            },
            new ShadowRung
            {
                Shadowmask = true, DistanceShadowmask = true, DynamicShadows = true,
                Atlas = 8192, Requests = 128, Resolution = 2048,
                Filtering = HDShadowFilteringQuality.High, ContactShadows = true
            }
        };

        /// <summary>
        /// The area light shadow atlas, at its floor on every rung.
        ///
        /// There is not one area light in the project - five lights, all point.
        /// The atlas is reserved up front by setting rather than by what exists,
        /// so sizing it with the tier meant the top preset held an 8192 square
        /// for zero lights.
        /// </summary>
        public const int AreaShadowAtlas = 256;

        /// <summary>
        /// One step down the cube reflection ladder, floored at 128.
        ///
        /// The middle rung of the scalable setting, so a probe authored at Medium
        /// still scales with the tier instead of sitting at whatever the stock
        /// base shipped.
        /// </summary>
        private static CubeReflectionResolution ScaleDown(CubeReflectionResolution resolution)
        {
            switch (resolution)
            {
                case CubeReflectionResolution.CubeReflectionResolution1024:
                    return CubeReflectionResolution.CubeReflectionResolution512;
                case CubeReflectionResolution.CubeReflectionResolution512:
                    return CubeReflectionResolution.CubeReflectionResolution256;
                default:
                    return CubeReflectionResolution.CubeReflectionResolution128;
            }
        }

        public static ShadowRung ShadowsFor(ShadowQualityLevel level)
        {
            int index = (int)level;
            return Shadows[index < 0 ? 0 : (index >= Shadows.Length ? Shadows.Length - 1 : index)];
        }

        /// <summary>
        /// Writes a shadow rung into a pipeline settings block.
        ///
        /// The caller owns the struct copy - RenderPipelineSettings is a value
        /// type all the way down, so this takes it by reference and the caller
        /// has to assign it back onto the asset afterwards.
        /// </summary>
        public static void ApplyShadows(ref RenderPipelineSettings settings, ShadowQualityLevel level)
        {
            ShadowRung rung = ShadowsFor(level);

            settings.supportShadowMask = rung.Shadowmask;

            HDShadowInitParameters shadows = settings.hdShadowInitParams;

            shadows.maxShadowRequests = rung.Requests;
            shadows.punctualShadowFilteringQuality = rung.Filtering;
            shadows.directionalShadowFilteringQuality = rung.Filtering;
            shadows.supportContactShadows = rung.ContactShadows;
            shadows.maxPunctualShadowMapResolution = rung.Resolution;

            HDShadowInitParameters.HDShadowAtlasInitParams punctual = shadows.punctualLightShadowAtlas;
            punctual.shadowAtlasResolution = rung.Atlas;
            shadows.punctualLightShadowAtlas = punctual;

            HDShadowInitParameters.HDShadowAtlasInitParams area = shadows.areaLightShadowAtlas;
            area.shadowAtlasResolution = AreaShadowAtlas;
            shadows.areaLightShadowAtlas = area;

            settings.hdShadowInitParams = shadows;
        }

        /// <summary>
        /// Writes everything a preset decides into a pipeline settings block.
        ///
        /// Shared between the editor tool that bakes the presets and the runtime
        /// director that rebuilds a pipeline for the Custom level, so the two
        /// cannot disagree about what a preset means.
        /// </summary>
        public static void ApplyPreset(ref RenderPipelineSettings settings, GraphicsPreset preset)
        {
            ApplyShadows(ref settings, preset.Shadows);
            ApplyLightingQuality(ref settings);

            settings.supportRayTracing = preset.RayTracing;

            // Screen-space shadows are what ray-traced shadows are delivered
            // through, so the flag rides with that row rather than with the
            // shadow rung.
            HDShadowInitParameters shadows = settings.hdShadowInitParams;
            shadows.supportScreenSpaceShadows = preset.RayTracedShadows;
            settings.hdShadowInitParams = shadows;

            // A support flag is what makes an effect reachable at all; the rung
            // then decides how much it spends. Off at the rung means off here, so
            // the tier stops paying for shader variants it cannot use.
            settings.supportSSAO = QualityLadder.IsOn(preset.AmbientOcclusion);
            settings.supportSSR = QualityLadder.IsOn(preset.Reflections);
            settings.supportSSGI = QualityLadder.IsOn(preset.GlobalIllumination);
            settings.supportVolumetrics = QualityLadder.IsOn(preset.VolumetricFog);

            settings.supportSubsurfaceScattering = preset.SubsurfaceScattering;
            settings.supportDecals = preset.Decals;

            // Off at every tier and absent from the menu. The arena sits inside a
            // cubemap; there is no sky for a cloud layer to occupy.
            settings.supportVolumetricClouds = false;

            // The reflection atlas and both things packed into it, sized together.
            //
            // Game.unity has one baked reflection probe, and HDRP puts the sky
            // reflection in the same atlas - and this arena is lit entirely by an
            // HDRI cubemap, so the sky is always present. The atlas has to hold
            // both. Sized below them, HDRP fails the allocation and logs that the
            // atlas is full, once per frame, forever.
            //
            // The cube rung is tiered alongside because the probe is authored at
            // High: leaving the stock table at [256, 512, 1024] meant every tier
            // asked for a 1024 probe, which does not fit the 512 atlas the bottom
            // three carry.
            // Replaced wholesale rather than written per rung: the scalable
            // setting's indexer is read-only, so the only way in is a new one.
            settings.cubeReflectionResolution =
                new RenderPipelineSettings.ReflectionProbeResolutionScalableSetting(
                    new[]
                    {
                        CubeReflectionResolution.CubeReflectionResolution128,
                        ScaleDown(preset.CubeReflection),
                        preset.CubeReflection
                    },
                    ScalableSettingSchemaId.With3Levels);

            GlobalLightLoopSettings lights = settings.lightLoopSettings;
            lights.skyReflectionSize = preset.SkyReflection;
            lights.reflectionProbeTexCacheSize = preset.ReflectionCache;
            settings.lightLoopSettings = lights;

            // Adaptive Probe Volumes at every tier, because that is what the Game
            // scene bakes: two ProbeVolumePerSceneData and no LightProbeGroup
            // anywhere in it. Performant and Balanced ship set to LegacyLightProbes,
            // so four of the seven tiers would have gone looking for probe groups
            // that do not exist. That is not a cheaper kind of probe lighting, it
            // is none - dynamic objects fall back to flat ambient while the top
            // tiers light correctly, which reads as the low tiers being broken
            // rather than cheap.
            settings.lightProbeSystem = RenderPipelineSettings.LightProbeSystem.AdaptiveProbeVolumes;

            // The GPU resident drawer is switched off in the preset builder rather
            // than here. Its mode enum lives in Unity.RenderPipelines.GPUDriven,
            // which the runtime assembly does not reference, and there is no
            // reason for it to: the runtime only ever clones an asset the builder
            // already wrote, so the clone inherits the setting.
        }

        /// <summary>
        /// One definition of Low, Medium and High for every scalable effect.
        ///
        /// Written into all seven presets rather than inherited, because the
        /// presets are cut from three different stock bases and each base ships
        /// its own tables. Measured before this existed: AO High resolved to 6
        /// steps on the Performant-derived tiers and 16 on the High Fidelity
        /// ones, and Performant's High (6) was identical to Balanced's Medium
        /// (6) - so the ladder crossed over itself between presets and a row
        /// reading "High" meant four different amounts of work.
        ///
        /// The tier picks the rung; the rung means one thing everywhere.
        /// </summary>
        public static void ApplyLightingQuality(ref RenderPipelineSettings settings)
        {
            GlobalLightingQualitySettings quality = settings.lightingQualitySettings;

            quality.AOStepCount[0] = 4;
            quality.AOStepCount[1] = 8;
            quality.AOStepCount[2] = 16;

            quality.ContactShadowSampleCount[0] = 6;
            quality.ContactShadowSampleCount[1] = 10;
            quality.ContactShadowSampleCount[2] = 16;

            quality.SSRMaxRaySteps[0] = 16;
            quality.SSRMaxRaySteps[1] = 32;
            quality.SSRMaxRaySteps[2] = 64;

            quality.Fog_Budget[0] = 0.125f;
            quality.Fog_Budget[1] = 0.33f;
            quality.Fog_Budget[2] = 0.66f;

            // Already identical across all three stock bases. Written anyway so a
            // future base change cannot silently reintroduce the divergence the
            // rest of this method exists to remove.
            quality.SSGIRaySteps[0] = 32;
            quality.SSGIRaySteps[1] = 64;
            quality.SSGIRaySteps[2] = 128;

            quality.RTAOSampleCount[0] = 1;
            quality.RTAOSampleCount[1] = 2;
            quality.RTAOSampleCount[2] = 8;

            settings.lightingQualitySettings = quality;
        }
    }
}
