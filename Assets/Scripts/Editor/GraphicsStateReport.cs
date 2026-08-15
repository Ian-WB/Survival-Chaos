using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Shows what the renderer is actually doing right now, as opposed to what
    /// the menu says it asked for.
    ///
    /// Exists because the two can disagree in ways neither end reveals. A row can
    /// set a pipeline flag that no light opts into, so the control looks live and
    /// changes nothing - which is how the Ray Traced Shadows row shipped doing
    /// nothing in the editor and producing an undriven screen-space shadow buffer
    /// in a build.
    ///
    /// Three layers have to agree for a setting to be real, and this prints all
    /// three side by side: what the director stored, what the pipeline asset
    /// compiled in, and what the volume stack resolved for the camera. A row is
    /// only working when all three line up.
    /// </summary>
    public static class GraphicsStateReport
    {
        /// <summary>
        /// Selects the live pipeline asset so it opens in the Inspector.
        ///
        /// On the Custom level this is a runtime clone rather than a project
        /// asset, so it will never appear in the Project window - selecting it
        /// directly is the only way to see it.
        /// </summary>
        [MenuItem("Survival Chaos/Graphics/Inspect Live Pipeline", priority = 41)]
        public static void InspectLivePipeline()
        {
            RenderPipelineAsset asset = QualitySettings.renderPipeline;

            if (asset == null)
            {
                Debug.LogWarning("No render pipeline on the current quality level.");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"Selected '{asset.name}'. " +
                      (EditorUtility.IsPersistent(asset)
                          ? "This is the project asset."
                          : "This is a runtime clone - it exists only while playing, and " +
                            "edits to it are discarded when play mode ends."));
        }

        [MenuItem("Survival Chaos/Graphics/Report Graphics State", priority = 42)]
        public static void Report()
        {
            StringBuilder sb = new StringBuilder();

            int level = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            RenderPipelineAsset asset = QualitySettings.renderPipeline;

            sb.AppendLine("=== GRAPHICS STATE ===");
            sb.AppendLine($"quality level : {names[level]}");
            sb.AppendLine($"pipeline asset: {(asset != null ? asset.name : "(none)")}" +
                          (asset != null && !EditorUtility.IsPersistent(asset)
                              ? "   [runtime clone]"
                              : string.Empty));
            sb.AppendLine($"playing       : {EditorApplication.isPlaying}");
            sb.AppendLine();

            ReportRows(sb);
            ReportPipeline(sb, asset as HDRenderPipelineAsset);
            ReportVolumeStack(sb);

            Debug.Log(sb.ToString());
        }

        /// <summary>What the menu believes, which only exists while playing.</summary>
        private static void ReportRows(StringBuilder sb)
        {
            GraphicsDirector director = GraphicsDirector.Instance;

            if (director == null)
            {
                sb.AppendLine("--- MENU ROWS --- (no director; enter play mode)");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("--- MENU ROWS ---");
            sb.AppendLine($"  custom          : {director.IsCustom}");
            sb.AppendLine($"  shadow quality  : {QualityLadder.Describe(director.Shadows)}");
            sb.AppendLine($"  ambient occl.   : {QualityLadder.Describe(director.AmbientOcclusion)}");
            sb.AppendLine($"  reflections     : {QualityLadder.Describe(director.Reflections)}");
            sb.AppendLine($"  global illum.   : {QualityLadder.Describe(director.GlobalIlluminationQuality)}");
            sb.AppendLine($"  volumetric fog  : {QualityLadder.Describe(director.VolumetricFog)}");
            sb.AppendLine($"  motion blur     : {QualityLadder.Describe(director.MotionBlurQuality)}");
            sb.AppendLine($"  texture mip     : {director.TextureMipLimit} (0 full, 1 half)");
            sb.AppendLine($"  anisotropic     : {director.Anisotropic}");
            sb.AppendLine();
        }

        /// <summary>What the pipeline compiled in, which is the hard gate.</summary>
        private static void ReportPipeline(StringBuilder sb, HDRenderPipelineAsset asset)
        {
            if (asset == null)
            {
                sb.AppendLine("--- PIPELINE --- (not an HDRP asset)");
                sb.AppendLine();
                return;
            }

            RenderPipelineSettings s = asset.currentPlatformRenderPipelineSettings;
            HDShadowInitParameters shadows = s.hdShadowInitParams;

            sb.AppendLine("--- PIPELINE (a flag off here means the effect cannot run at all) ---");
            sb.AppendLine($"  supportSSAO           : {s.supportSSAO}");
            sb.AppendLine($"  supportSSR            : {s.supportSSR}");
            sb.AppendLine($"  supportSSGI           : {s.supportSSGI}");
            sb.AppendLine($"  supportVolumetrics    : {s.supportVolumetrics}");
            sb.AppendLine($"  supportRayTracing     : {s.supportRayTracing}");
            sb.AppendLine($"  supportShadowMask     : {s.supportShadowMask}");
            sb.AppendLine($"  supportContactShadows : {shadows.supportContactShadows}");
            sb.AppendLine($"  screenSpaceShadows    : {shadows.supportScreenSpaceShadows}" +
                          "   <- only useful if a light sets useRayTracedShadows");
            sb.AppendLine($"  shadow atlas / req    : {shadows.punctualLightShadowAtlas.shadowAtlasResolution} / {shadows.maxShadowRequests}");
            sb.AppendLine($"  punctual shadow res   : {shadows.maxPunctualShadowMapResolution}");
            sb.AppendLine($"  filtering             : {shadows.punctualShadowFilteringQuality}");
            sb.AppendLine($"  sky / cube / atlas    : {s.lightLoopSettings.skyReflectionSize} / " +
                          $"{s.cubeReflectionResolution[2]} / {s.lightLoopSettings.reflectionProbeTexCacheSize}");
            sb.AppendLine();

            sb.AppendLine("--- LIGHTS (ray-traced shadows are per-light, not a pipeline setting) ---");
            foreach (Light light in Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                HDAdditionalLightData data = light.GetComponent<HDAdditionalLightData>();
                sb.AppendLine($"  {light.gameObject.name}: {light.type}, {light.lightmapBakeType}, " +
                              (data != null
                                  ? $"useRayTracedShadows={data.useRayTracedShadows}"
                                  : "no HDAdditionalLightData"));
            }
            sb.AppendLine();
        }

        /// <summary>
        /// What the volume stack actually resolved, which is the only layer that
        /// says whether an effect is really on.
        /// </summary>
        private static void ReportVolumeStack(StringBuilder sb)
        {
            VolumeStack stack = VolumeManager.instance != null ? VolumeManager.instance.stack : null;

            if (stack == null)
            {
                sb.AppendLine("--- RESOLVED VOLUME STACK --- (none; enter play mode)");
                return;
            }

            sb.AppendLine("--- RESOLVED VOLUME STACK (what is actually rendering) ---");

            ScreenSpaceAmbientOcclusion ao = stack.GetComponent<ScreenSpaceAmbientOcclusion>();
            if (ao != null)
            {
                sb.AppendLine($"  AO        : intensity={ao.intensity.value} quality={ao.quality.value} rayTracing={ao.rayTracing.value}");
            }

            ScreenSpaceReflection ssr = stack.GetComponent<ScreenSpaceReflection>();
            if (ssr != null)
            {
                sb.AppendLine($"  SSR       : enabled={ssr.enabled.value} quality={ssr.quality.value} tracing={ssr.tracing.value}");
            }

            GlobalIllumination gi = stack.GetComponent<GlobalIllumination>();
            if (gi != null)
            {
                sb.AppendLine($"  GI        : enable={gi.enable.value} quality={gi.quality.value} tracing={gi.tracing.value}");
            }

            Fog fog = stack.GetComponent<Fog>();
            if (fog != null)
            {
                sb.AppendLine($"  Fog       : enabled={fog.enabled.value} volumetric={fog.enableVolumetricFog.value} quality={fog.quality.value}");
            }

            MotionBlur blur = stack.GetComponent<MotionBlur>();
            if (blur != null)
            {
                sb.AppendLine($"  MotionBlur: intensity={blur.intensity.value} quality={blur.quality.value}");
            }

            ContactShadows contact = stack.GetComponent<ContactShadows>();
            if (contact != null)
            {
                sb.AppendLine($"  Contact   : enable={contact.enable.value} quality={contact.quality.value}");
            }

            HDShadowSettings shadowSettings = stack.GetComponent<HDShadowSettings>();
            if (shadowSettings != null)
            {
                sb.AppendLine($"  Shadows   : maxDistance={shadowSettings.maxShadowDistance.value}");
            }
        }
    }
}
