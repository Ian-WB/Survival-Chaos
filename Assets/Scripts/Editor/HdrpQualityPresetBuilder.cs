using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Builds one HDRP asset per quality tier and assigns each to a quality level.
    ///
    /// This exists because the project had six quality levels and <em>one</em>
    /// pipeline asset shared between them. Under HDRP, shadows, screen-space
    /// effects and volumetrics all live in the pipeline asset - QualitySettings
    /// only still owns a few things like texture mipmap limit and anisotropic
    /// filtering. So switching from Very Low to Ultra changed almost nothing, and
    /// a quality dropdown built on top of that would have been another control
    /// that looks functional and does nothing.
    ///
    /// Each preset is a copy of the base asset with a handful of fields changed,
    /// so anything not listed here - colour buffer format, lit shader mode, decal
    /// settings, the lot - stays exactly as authored.
    ///
    /// The one exception is the bottom tier, which does touch the colour buffer
    /// format and the lit shader mode. See <see cref="ApplyMinimumSpec"/> - those
    /// two are where the video memory actually is, and that tier exists for a
    /// machine that has almost none.
    /// </summary>
    public static class HdrpQualityPresetBuilder
    {
        private const string BasePath = "Assets/Settings/HDRenderPipelineAsset.asset";
        private const string PresetFolder = "Assets/Settings/Quality";

        /// <summary>
        /// What separates one tier from the next.
        ///
        /// The levers are chosen for cost, not for the length of the list: shadow
        /// atlas size and count, then the screen-space effects in the order they
        /// get expensive. Screen-space global illumination is the single most
        /// costly thing here, so it only appears at the top two tiers.
        /// </summary>
        private struct Tier
        {
            public string Name;
            public int ShadowAtlas;
            public int MaxShadowRequests;
            public HDShadowFilteringQuality Filtering;
            public bool ContactShadows;
            public bool ScreenSpaceShadows;
            public bool Ssao;
            public bool Ssr;
            public bool Ssgi;
            public bool Volumetrics;
            public bool VolumetricClouds;
            public bool SubsurfaceScattering;
            public bool Decals;

            /// <summary>
            /// Mirrored into QualitySettings.shadows. HDRP does not read that
            /// value for its own rendering, but it is the standard place Unity
            /// records the intent, and it lets anything else in the game - the
            /// bullet lights, for one - find out without taking a dependency on
            /// the render pipeline.
            /// </summary>
            public ShadowQuality Shadows;

            /// <summary>
            /// Applies the extra block in <see cref="ApplyMinimumSpec"/> on top of
            /// everything above. Left false on every tier that does not say
            /// otherwise, so the six original presets are untouched by its
            /// existence.
            /// </summary>
            public bool MinimumSpec;
        }

        private static readonly Tier[] Tiers =
        {
            new Tier
            {
                // Below Very Low, for hardware below what HDRP is built for.
                //
                // The reference machine is an Intel HD Graphics 4000 with 128 MB
                // of dedicated video memory - a 2012 integrated part. Every lever
                // above is already at its floor by the time Very Low is reached,
                // so what separates this tier from that one is not effects but
                // memory: see ApplyMinimumSpec.
                //
                // Named after the person it was measured against, which is also
                // the honest description of what it is - a machine-specific
                // fallback rather than a tier anyone else should pick.
                Name = "Ubirajara", ShadowAtlas = 256, MaxShadowRequests = 1,
                Filtering = HDShadowFilteringQuality.Low,
                ContactShadows = false, ScreenSpaceShadows = false,
                Ssao = false, Ssr = false, Ssgi = false,
                Volumetrics = false, VolumetricClouds = false,
                SubsurfaceScattering = false, Decals = false,
                Shadows = ShadowQuality.Disable,
                MinimumSpec = true
            },
            new Tier
            {
                // Effectively no dynamic shadows: one request, the smallest
                // atlas, and ShadowQuality.Disable below, so the lava lights and
                // the bullet lights cost nothing to render.
                //
                // One rather than zero because zero is not a state HDRP survives,
                // despite reading like the obvious way to say "none".
                // HDShadowManager.InitShadowManager returns early at
                // maxShadowRequests == 0 without allocating m_Atlas, and
                // GetUnmanageDataForShadowRequestJobs then dereferences it with no
                // guard - a NullReferenceException every frame, on a screen that
                // never draws. Verified in HDRP 17.5.0.
                Name = "Very Low", ShadowAtlas = 256, MaxShadowRequests = 1,
                Filtering = HDShadowFilteringQuality.Low,
                ContactShadows = false, ScreenSpaceShadows = false,
                Ssao = false, Ssr = false, Ssgi = false,
                Volumetrics = false, VolumetricClouds = false,
                SubsurfaceScattering = false, Decals = false,
                Shadows = ShadowQuality.Disable
            },
            new Tier
            {
                Name = "Low", ShadowAtlas = 1024, MaxShadowRequests = 4,
                Filtering = HDShadowFilteringQuality.Low,
                ContactShadows = false, ScreenSpaceShadows = false,
                Ssao = false, Ssr = false, Ssgi = false,
                Volumetrics = false, VolumetricClouds = false,
                SubsurfaceScattering = false, Decals = false,
                Shadows = ShadowQuality.All
            },
            new Tier
            {
                Name = "Medium", ShadowAtlas = 2048, MaxShadowRequests = 8,
                Filtering = HDShadowFilteringQuality.Medium,
                ContactShadows = false, ScreenSpaceShadows = false,
                Ssao = true, Ssr = false, Ssgi = false,
                Volumetrics = true, VolumetricClouds = false,
                SubsurfaceScattering = true, Decals = true,
                Shadows = ShadowQuality.All
            },
            new Tier
            {
                Name = "High", ShadowAtlas = 4096, MaxShadowRequests = 16,
                Filtering = HDShadowFilteringQuality.Medium,
                ContactShadows = true, ScreenSpaceShadows = true,
                Ssao = true, Ssr = true, Ssgi = false,
                Volumetrics = true, VolumetricClouds = true,
                SubsurfaceScattering = true, Decals = true,
                Shadows = ShadowQuality.All
            },
            new Tier
            {
                Name = "Very High", ShadowAtlas = 4096, MaxShadowRequests = 24,
                Filtering = HDShadowFilteringQuality.High,
                ContactShadows = true, ScreenSpaceShadows = true,
                Ssao = true, Ssr = true, Ssgi = true,
                Volumetrics = true, VolumetricClouds = true,
                SubsurfaceScattering = true, Decals = true,
                Shadows = ShadowQuality.All
            },
            new Tier
            {
                Name = "Ultra", ShadowAtlas = 8192, MaxShadowRequests = 32,
                Filtering = HDShadowFilteringQuality.High,
                ContactShadows = true, ScreenSpaceShadows = true,
                Ssao = true, Ssr = true, Ssgi = true,
                Volumetrics = true, VolumetricClouds = true,
                SubsurfaceScattering = true, Decals = true,
                Shadows = ShadowQuality.All
            }
        };

        /// <summary>Which preset each existing quality level gets, one for one.</summary>
        private static readonly Dictionary<string, string> LevelToTier = new Dictionary<string, string>
        {
            { "Ubirajara", "Ubirajara" },
            { "Very Low", "Very Low" },
            { "Low", "Low" },
            { "Medium", "Medium" },
            { "High", "High" },
            { "Very High", "Very High" },
            { "Ultra", "Ultra" }
        };

        [MenuItem("Survival Chaos/Graphics/Build Quality Presets", priority = 40)]
        public static void Build()
        {
            HDRenderPipelineAsset baseAsset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(BasePath);
            if (baseAsset == null)
            {
                Debug.LogError("No HDRP asset at " + BasePath + ".");
                return;
            }

            EnsureFolder();
            EnsureLevel("Ubirajara");

            Dictionary<string, HDRenderPipelineAsset> built =
                new Dictionary<string, HDRenderPipelineAsset>();

            foreach (Tier tier in Tiers)
            {
                built[tier.Name] = BuildTier(tier);
            }

            AssetDatabase.SaveAssets();
            int assigned = Assign(built);

            Debug.Log("Built " + built.Count + " HDRP quality presets in " + PresetFolder +
                      " and assigned them to " + assigned + " quality levels. Ray tracing support is " +
                      "left as the base asset had it everywhere except Ubirajara, which switches it " +
                      "off - so the lighting toggle works at every tier a machine with DXR would " +
                      "plausibly be running.");
        }

        private static ShadowQuality ShadowFor(string tierName)
        {
            foreach (Tier tier in Tiers)
            {
                if (tier.Name == tierName)
                {
                    return tier.Shadows;
                }
            }

            return ShadowQuality.All;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(PresetFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Quality");
            }
        }

        /// <summary>
        /// Adds a quality level at index 0 if the project has none by that name,
        /// so the new bottom tier can be built without anyone opening Project
        /// Settings first.
        ///
        /// QualitySettings offers no API for adding a level - names is read-only -
        /// so this goes at the serialised asset directly. Inserting duplicates
        /// element 0, which is the behaviour wanted here: the new tier starts life
        /// as a copy of Very Low and Assign then points it at its own preset.
        ///
        /// Inserting at the bottom shifts every existing level's index up by one.
        /// Two places store an index rather than a name and both have to move with
        /// it: the per-platform defaults below, and the player's saved choice,
        /// which GraphicsDirector migrates on next launch.
        /// </summary>
        private static bool EnsureLevel(string levelName)
        {
            if (System.Array.IndexOf(QualitySettings.names, levelName) >= 0)
            {
                return true;
            }

            Object[] loaded = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            SerializedProperty levels = null;
            SerializedObject serialized = null;

            if (loaded != null && loaded.Length > 0)
            {
                serialized = new SerializedObject(loaded[0]);
                levels = serialized.FindProperty("m_QualitySettings");
            }

            if (levels == null || !levels.isArray)
            {
                Debug.LogError(
                    "Could not add a '" + levelName + "' quality level automatically. Add it by hand " +
                    "in Project Settings > Quality as the first entry, then run this again.");
                return false;
            }

            levels.InsertArrayElementAtIndex(0);
            levels.GetArrayElementAtIndex(0).FindPropertyRelative("name").stringValue = levelName;

            SerializedProperty defaults = serialized.FindProperty("m_PerPlatformDefaultQuality");
            if (defaults != null && defaults.isArray)
            {
                for (int i = 0; i < defaults.arraySize; i++)
                {
                    SerializedProperty index = defaults.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("second");

                    if (index != null)
                    {
                        index.intValue += 1;
                    }
                }
            }

            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Debug.Log("Added quality level '" + levelName + "' at index 0. Every level below it moved " +
                      "up one, and saved player preferences will be migrated on next launch.");
            return true;
        }

        /// <summary>
        /// The bottom tier's extra austerity, which is about video memory rather
        /// than about effects.
        ///
        /// The reference machine reports 128 MB. Everything the tier list above
        /// controls is already off by the time Very Low is reached, and none of it
        /// is what fills that budget - the render targets are. So this goes after
        /// the two allocations that dominate:
        ///
        /// Deferred shading keeps a full-screen GBuffer alive for the whole frame,
        /// several render targets wide. Forward drops it outright, and this game
        /// has few enough lights per pixel that forward is the cheaper shape
        /// anyway. R11G11B10 then halves the main colour buffer against the
        /// R16G16B16A16 the other tiers use, at a cost in banding that matters
        /// less than launching does.
        ///
        /// The rest is the long tail: passes and buffers that are individually
        /// small, allocated whether or not anything uses them, and all switched
        /// off here because at this budget the tail is the difference.
        /// </summary>
        private static void ApplyMinimumSpec(ref RenderPipelineSettings settings)
        {
            settings.supportedLitShaderMode = RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly;
            settings.colorBufferFormat = RenderPipelineSettings.ColorBufferFormat.R11G11B10;

            // No DXR on a 2012 integrated part, so this is allocation with nothing
            // on the other end of it.
            settings.supportRayTracing = false;

            // Motion vectors are a full-screen target that only TAA, motion blur
            // and the advanced upscalers read. None of the three is reachable at
            // this tier.
            settings.supportMotionVectors = false;

            settings.supportDistortion = false;
            settings.supportSSRTransparent = false;
            settings.supportTransparentBackface = false;
            settings.supportTransparentDepthPrepass = false;
            settings.supportTransparentDepthPostpass = false;

            // MSAA is deliberately not set. The base asset already carries
            // msaaSampleCount 1, which is MSAASamples.None rather than one sample,
            // so there is nothing to turn off.

            // Ray tracing has two switches and turning off only the first leaves
            // the VFX one on. Unity's own default has it off; this asset inherited
            // it on from the project's base.
            settings.supportVFXRayTracing = false;

            // The probe volume pool is sized by this setting rather than by how
            // much was baked, and the editor is blunt about the result: at
            // MemoryBudgetHigh it reserves 416 MB to hold roughly 2.4 MB of
            // actual probe data. On a 128 MB card that one line is fatal on its
            // own, well ahead of anything else in this method. GPU streaming
            // stays on precisely because the pool is now small - it pages bricks
            // in rather than needing the set resident.
            settings.probeVolumeMemoryBudget = ProbeVolumeTextureMemoryBudget.MemoryBudgetLow;

            // Disk streaming trades memory for disk reads. The reference machine
            // has 12 GB of RAM and 2.4 MB of probe data, so there is nothing to
            // trade away, and its disk is the last thing worth blocking a frame
            // on.
            settings.supportProbeVolumeDiskStreaming = false;

            // These size GPU-side buffers whether or not the lights exist. The
            // arena runs a directional light, the lava lights and the bullet
            // light pool, which is well inside 32.
            GlobalLightLoopSettings lights = settings.lightLoopSettings;
            lights.maxPunctualLightsOnScreen = 32;
            lights.maxAreaLightsOnScreen = 1;
            lights.maxDecalsOnScreen = 1;
            settings.lightLoopSettings = lights;

            // The cached atlases are reserved up front, and the area one ships at
            // 8192 - by itself larger than this machine's entire video memory.
            HDShadowInitParameters shadows = settings.hdShadowInitParams;
            shadows.cachedPunctualLightShadowAtlas = 256;
            shadows.cachedAreaLightShadowAtlas = 256;
            shadows.maxDirectionalShadowMapResolution = 256;
            shadows.maxPunctualShadowMapResolution = 256;
            shadows.maxAreaShadowMapResolution = 256;

            // The Tier struct's Filtering covers punctual and directional only.
            // Area shadows have their own quality enum and their own default, and
            // this asset inherited High from the base while Unity's default is
            // Medium - the one place the stock settings were cheaper than the
            // tier built to undercut them.
            shadows.areaShadowFilteringQuality = HDAreaShadowFilteringQuality.Medium;
            settings.hdShadowInitParams = shadows;

            // CatmullRom rather than the inherited TAAU, for two reasons. It is
            // the cheap one - the package describes it as very cheap and blurry,
            // which is the correct trade here. And TAAU is temporal: it needs the
            // motion vectors this method switched off thirty lines ago, so
            // leaving it selected would have asked for an upscaler that cannot
            // run.
            GlobalDynamicResolutionSettings resolution = settings.dynamicResolutionSettings;
            resolution.upsampleFilter = DynamicResUpscaleFilter.CatmullRom;

            // Mip bias sharpens textures when rendering below native, but it
            // relies on a temporal filter to resolve the extra aliasing it
            // introduces. With CatmullRom there is no such filter.
            resolution.useMipBias = false;
            settings.dynamicResolutionSettings = resolution;
        }

        /// <summary>
        /// Copies the base asset and changes only the tier's fields, so every
        /// setting not named above keeps whatever it was authored as.
        /// </summary>
        private static HDRenderPipelineAsset BuildTier(Tier tier)
        {
            string path = PresetFolder + "/HDRP " + tier.Name + ".asset";

            if (AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CopyAsset(BasePath, path);
            HDRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);

            RenderPipelineSettings settings = asset.currentPlatformRenderPipelineSettings;

            settings.supportSSAO = tier.Ssao;
            settings.supportSSR = tier.Ssr;
            settings.supportSSGI = tier.Ssgi;
            settings.supportVolumetrics = tier.Volumetrics;
            settings.supportVolumetricClouds = tier.VolumetricClouds;
            settings.supportSubsurfaceScattering = tier.SubsurfaceScattering;
            settings.supportDecals = tier.Decals;

            HDShadowInitParameters shadows = settings.hdShadowInitParams;
            shadows.maxShadowRequests = tier.MaxShadowRequests;
            shadows.punctualShadowFilteringQuality = tier.Filtering;
            shadows.directionalShadowFilteringQuality = tier.Filtering;
            shadows.supportContactShadows = tier.ContactShadows;
            shadows.supportScreenSpaceShadows = tier.ScreenSpaceShadows;

            HDShadowInitParameters.HDShadowAtlasInitParams punctual = shadows.punctualLightShadowAtlas;
            punctual.shadowAtlasResolution = tier.ShadowAtlas;
            shadows.punctualLightShadowAtlas = punctual;

            HDShadowInitParameters.HDShadowAtlasInitParams area = shadows.areaLightShadowAtlas;
            area.shadowAtlasResolution = tier.ShadowAtlas;
            shadows.areaLightShadowAtlas = area;

            settings.hdShadowInitParams = shadows;

            // Last, so it reads the shadow parameters the tier just wrote rather
            // than the base asset's.
            if (tier.MinimumSpec)
            {
                ApplyMinimumSpec(ref settings);
            }

            asset.currentPlatformRenderPipelineSettings = settings;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// Points each quality level at its preset.
        ///
        /// QualitySettings.renderPipeline reads and writes the <em>current</em>
        /// level only, so this has to walk the levels and put the original back
        /// afterwards rather than leaving the editor on whatever it finished on.
        /// </summary>
        private static int Assign(Dictionary<string, HDRenderPipelineAsset> built)
        {
            string[] names = QualitySettings.names;
            int original = QualitySettings.GetQualityLevel();
            int assigned = 0;

            for (int i = 0; i < names.Length; i++)
            {
                if (!LevelToTier.TryGetValue(names[i], out string tierName) ||
                    !built.TryGetValue(tierName, out HDRenderPipelineAsset asset))
                {
                    Debug.LogWarning("Quality level '" + names[i] + "' has no matching preset, so it " +
                                     "still falls back to the project-wide HDRP asset.");
                    continue;
                }

                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = asset;

                // Recorded even though HDRP does not read it, so that anything
                // outside the pipeline can find out whether shadows are wanted.
                QualitySettings.shadows = ShadowFor(tierName);
                assigned++;
            }

            QualitySettings.SetQualityLevel(original, applyExpensiveChanges: false);
            AssetDatabase.SaveAssets();
            return assigned;
        }
    }
}
