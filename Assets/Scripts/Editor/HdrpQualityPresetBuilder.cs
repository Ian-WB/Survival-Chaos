using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
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
        }

        private static readonly Tier[] Tiers =
        {
            new Tier
            {
                Name = "Low", ShadowAtlas = 1024, MaxShadowRequests = 4,
                Filtering = HDShadowFilteringQuality.Low,
                ContactShadows = false, ScreenSpaceShadows = false,
                Ssao = false, Ssr = false, Ssgi = false,
                Volumetrics = false, VolumetricClouds = false,
                SubsurfaceScattering = false, Decals = false
            },
            new Tier
            {
                Name = "Medium", ShadowAtlas = 2048, MaxShadowRequests = 8,
                Filtering = HDShadowFilteringQuality.Medium,
                ContactShadows = false, ScreenSpaceShadows = false,
                Ssao = true, Ssr = false, Ssgi = false,
                Volumetrics = true, VolumetricClouds = false,
                SubsurfaceScattering = true, Decals = true
            },
            new Tier
            {
                Name = "High", ShadowAtlas = 4096, MaxShadowRequests = 16,
                Filtering = HDShadowFilteringQuality.Medium,
                ContactShadows = true, ScreenSpaceShadows = true,
                Ssao = true, Ssr = true, Ssgi = false,
                Volumetrics = true, VolumetricClouds = true,
                SubsurfaceScattering = true, Decals = true
            },
            new Tier
            {
                Name = "Very High", ShadowAtlas = 4096, MaxShadowRequests = 24,
                Filtering = HDShadowFilteringQuality.High,
                ContactShadows = true, ScreenSpaceShadows = true,
                Ssao = true, Ssr = true, Ssgi = true,
                Volumetrics = true, VolumetricClouds = true,
                SubsurfaceScattering = true, Decals = true
            },
            new Tier
            {
                Name = "Ultra", ShadowAtlas = 8192, MaxShadowRequests = 32,
                Filtering = HDShadowFilteringQuality.High,
                ContactShadows = true, ScreenSpaceShadows = true,
                Ssao = true, Ssr = true, Ssgi = true,
                Volumetrics = true, VolumetricClouds = true,
                SubsurfaceScattering = true, Decals = true
            }
        };

        /// <summary>
        /// Which preset each existing quality level gets. "Very Low" has no preset
        /// of its own and shares Low, because five tiers that differ is worth more
        /// than six that barely do.
        /// </summary>
        private static readonly Dictionary<string, string> LevelToTier = new Dictionary<string, string>
        {
            { "Very Low", "Low" },
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

            Dictionary<string, HDRenderPipelineAsset> built =
                new Dictionary<string, HDRenderPipelineAsset>();

            foreach (Tier tier in Tiers)
            {
                built[tier.Name] = BuildTier(tier);
            }

            AssetDatabase.SaveAssets();
            int assigned = Assign(built);

            Debug.Log("Built " + built.Count + " HDRP quality presets in " + PresetFolder +
                      " and assigned them to " + assigned + " quality levels. Ray tracing support " +
                      "is left as the base asset had it, so the lighting toggle works at every tier.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(PresetFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Quality");
            }
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
                assigned++;
            }

            QualitySettings.SetQualityLevel(original, applyExpensiveChanges: false);
            AssetDatabase.SaveAssets();
            return assigned;
        }
    }
}
