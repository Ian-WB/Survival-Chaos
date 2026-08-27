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

            sb.AppendLine("--- MENU ROWS ---   (gated: the tier's asset never compiled it)");
            sb.AppendLine($"  reflections     : {Row(director.Reflections, director.ReflectionsSupported)}");
            sb.AppendLine($"  global illum.   : {Row(director.GlobalIlluminationQuality, director.GlobalIlluminationSupported)}");
            sb.AppendLine($"  volumetric fog  : {Row(director.VolumetricFog, director.VolumetricFogSupported)}");
            sb.AppendLine($"  motion blur     : {QualityLadder.Describe(director.MotionBlurQuality)}");
            sb.AppendLine($"  dynamic res.    : {(director.DynamicResolutionSupported ? "available" : "gated")}");
            sb.AppendLine();
        }

        /// <summary>
        /// A row's rung, marked when the pipeline gate makes it moot.
        ///
        /// The whole point of this report is catching the case where the three
        /// layers disagree, and "High" on a row whose effect was never compiled
        /// is exactly that - it reads as working right up until you look at the
        /// pipeline section below and find the support flag off.
        /// </summary>
        private static string Row(EffectQuality quality, bool supported)
        {
            return supported
                ? QualityLadder.Describe(quality)
                : QualityLadder.Describe(quality) + "   <- GATED";
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
            // No sort mode: the overload taking one is deprecated, and this only
            // ever prints the lights, so the order they come back in is nobody's
            // business.
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
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
        ///
        /// Resolved against the camera rather than read from
        /// VolumeManager.instance.stack, which is the default global stack and is
        /// not what any camera renders with. HDRP evaluates volumes per camera,
        /// honouring that camera's volumeLayerMask and its position in the scene,
        /// so a volume the camera does see can be missing from the global stack
        /// entirely.
        ///
        /// This mattered: GraphicsDirector adds a priority 10000 override capping
        /// maxShadowDistance, and the global stack does not carry it. Reading the
        /// wrong stack reported the profile's 500 while the camera was rendering
        /// against 60, so shadows past 60 units were culled and this report said
        /// the setting was fine. A diagnostic that corroborates the wrong answer
        /// is worse than no diagnostic, which is the whole reason it now builds
        /// its own stack.
        /// </summary>
        private static void ReportVolumeStack(StringBuilder sb)
        {
            if (VolumeManager.instance == null)
            {
                sb.AppendLine("--- RESOLVED VOLUME STACK --- (no VolumeManager)");
                return;
            }

            Camera camera = ResolveReportCamera();

            if (camera == null)
            {
                sb.AppendLine("--- RESOLVED VOLUME STACK --- (no camera to resolve against)");
                return;
            }

            // The trigger is what HDRP measures local volumes against, and it is
            // the anchor override when one is set rather than the camera itself.
            HDAdditionalCameraData cameraData = camera.GetComponent<HDAdditionalCameraData>();
            LayerMask mask = cameraData != null ? cameraData.volumeLayerMask : ~0;
            Transform trigger = cameraData != null && cameraData.volumeAnchorOverride != null
                ? cameraData.volumeAnchorOverride
                : camera.transform;

            VolumeStack stack = VolumeManager.instance.CreateStack();

            try
            {
                VolumeManager.instance.Update(stack, trigger, mask);

                sb.AppendLine($"--- RESOLVED VOLUME STACK for '{camera.name}' " +
                              $"(layerMask={mask.value}, what is actually rendering) ---");

                // Worth saying out loud, because the runtime-only overrides are
                // exactly the ones that catch people out - they are invisible in
                // every profile asset, so edit mode looks like the authored values
                // and play mode does not.
                if (!Application.isPlaying)
                {
                    sb.AppendLine("  NOTE: edit mode - volumes GraphicsDirector creates at runtime " +
                                  "do not exist yet, so these are the authored values only.");
                }

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
            finally
            {
                // The stack is ours, not the manager's, so it has to go back.
                stack.Dispose();
            }
        }

        /// <summary>
        /// The camera the report resolves volumes against.
        ///
        /// Camera.main first, because that is the one the player looks through and
        /// so the one whose resolved settings are the answer to "what am I seeing".
        /// It is tagged MainCamera and can be missing in a scene opened for editing,
        /// hence the fallback to any enabled camera rather than giving up - a report
        /// against the wrong camera still beats no report, and the header names
        /// which one it used so the reader can tell.
        /// </summary>
        private static Camera ResolveReportCamera()
        {
            Camera camera = Camera.main;

            if (camera != null)
            {
                return camera;
            }

            foreach (Camera candidate in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
            {
                if (candidate.isActiveAndEnabled)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
