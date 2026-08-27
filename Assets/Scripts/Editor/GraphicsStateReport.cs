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
            ReportShadowAtlases(sb, asset as HDRenderPipelineAsset);
            ReportVolumeStack(sb);

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// What every shadow-casting light costs, and how full each atlas is.
        ///
        /// Exists because the Rendering Debugger's atlas overlays answer a
        /// different question than the one usually being asked. They draw raw
        /// depth, which reads as near-black whatever the range sliders are set to,
        /// and even when legible they leave you estimating tile sizes by eye. The
        /// number wanted is nearly always "how much of the atlas is spoken for,
        /// and by whom", and that is arithmetic rather than a picture.
        ///
        /// The cost model is HDRP's own: a point light writes a cube, so six
        /// faces; spot, pyramid, box and area lights write one; a directional
        /// writes one per cascade. Resolution comes from each light's scalable
        /// setting resolved against the active tier's table and then clamped by
        /// that tier's maximum, which is what GetResolutionFromSettings does
        /// internally - mirrored here rather than called, because it is internal.
        ///
        /// Cached and dynamic are reported separately because they are separate
        /// atlases with separate budgets, and a light lands in one or the other
        /// purely on its shadowUpdateMode. Mixing them is how a light looks
        /// affordable while overflowing the atlas it actually occupies.
        /// </summary>
        private static void ReportShadowAtlases(StringBuilder sb, HDRenderPipelineAsset hdrp)
        {
            if (hdrp == null)
            {
                sb.AppendLine("--- SHADOW ATLASES --- (no HDRP asset)");
                sb.AppendLine();
                return;
            }

            HDShadowInitParameters init = hdrp.currentPlatformRenderPipelineSettings.hdShadowInitParams;
            int cascades = ResolveCascadeCount();

            sb.AppendLine("--- SHADOW ATLASES ---");
            sb.AppendLine($"  maxShadowRequests={init.maxShadowRequests}   directional cascades={cascades}");

            var entries = new System.Collections.Generic.List<ShadowEntry>();

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                HDAdditionalLightData data = light.GetComponent<HDAdditionalLightData>();

                if (data == null)
                {
                    continue;
                }

                // Baked lights never reach an atlas however their shadow fields
                // are set, and a disabled one is not costing anything right now.
                bool casts = light.shadows != LightShadows.None
                             && light.lightmapBakeType != LightmapBakeType.Baked
                             && light.enabled
                             && light.gameObject.activeInHierarchy;

                if (!casts)
                {
                    continue;
                }

                entries.Add(Describe(light, data, init, cascades));
            }

            entries.Sort((a, b) => b.Texels.CompareTo(a.Texels));

            long punctual = Area(init.punctualLightShadowAtlas.shadowAtlasResolution);
            long punctualCached = Area(init.cachedPunctualLightShadowAtlas);
            long area = Area(init.areaLightShadowAtlas.shadowAtlasResolution);
            long areaCached = Area(init.cachedAreaLightShadowAtlas);

            ReportAtlas(sb, entries, "punctual", false, "punctual (dynamic)", punctual);
            ReportAtlas(sb, entries, "punctual", true, "punctual (cached) ", punctualCached);
            ReportAtlas(sb, entries, "area", false, "area     (dynamic)", area);
            ReportAtlas(sb, entries, "area", true, "area     (cached) ", areaCached);

            // The directional atlas is sized to its own cascades rather than
            // shared with anything, so a percentage would be inventing a
            // denominator. Its footprint is still worth printing.
            ReportAtlas(sb, entries, "directional", null, "directional       ", 0L);

            sb.AppendLine();
        }

        private static long Area(int resolution) => (long)resolution * resolution;

        /// <summary>One shadow-casting light's claim on an atlas.</summary>
        private struct ShadowEntry
        {
            public string Name;
            public string Kind;
            public bool Cached;
            public int Resolution;
            public int Faces;
            public long Texels;
            public string Placement;
        }

        /// <summary>
        /// Works out which atlas a light lands in and what it costs there.
        ///
        /// Faces is the part people mis-count: a point light is a cube and so
        /// pays six times its resolution squared, which is why one point light at
        /// 2048 costs more than four spot lights at the same setting.
        /// </summary>
        private static ShadowEntry Describe(Light light, HDAdditionalLightData data,
                                            HDShadowInitParameters init, int cascades)
        {
            ShadowEntry entry = default;
            entry.Name = light.gameObject.name;
            entry.Cached = data.shadowUpdateMode != ShadowUpdateMode.EveryFrame;

            switch (light.type)
            {
                case LightType.Directional:
                    entry.Kind = "directional";
                    entry.Resolution = Mathf.Min(data.shadowResolution.Value(init.shadowResolutionDirectional),
                                                 init.maxDirectionalShadowMapResolution);
                    entry.Faces = Mathf.Max(1, cascades);
                    break;

                case LightType.Rectangle:
                case LightType.Disc:
                    entry.Kind = "area";
                    entry.Resolution = Mathf.Min(data.shadowResolution.Value(init.shadowResolutionArea),
                                                 init.maxAreaShadowMapResolution);
                    entry.Faces = 1;
                    break;

                case LightType.Point:
                    entry.Kind = "punctual";
                    entry.Resolution = Mathf.Min(data.shadowResolution.Value(init.shadowResolutionPunctual),
                                                 init.maxPunctualShadowMapResolution);
                    entry.Faces = 6;
                    break;

                default: // spot, pyramid, box - one map each
                    entry.Kind = "punctual";
                    entry.Resolution = Mathf.Min(data.shadowResolution.Value(init.shadowResolutionPunctual),
                                                 init.maxPunctualShadowMapResolution);
                    entry.Faces = 1;
                    break;
            }

            entry.Texels = (long)entry.Faces * entry.Resolution * entry.Resolution;

            // Only cached lights have a placement to report - the dynamic atlas
            // is rebuilt every frame and keeps no such record. Asking whether it
            // rendered matters: a light can hold a slot and still have never
            // drawn into it, which looks identical to a working light until you
            // go looking for its shadow.
            if (entry.Cached)
            {
                HDCachedShadowManager manager = HDCachedShadowManager.instance;
                bool placed = manager.LightHasBeenPlacedInAtlas(data);
                bool rendered = manager.LightHasBeenPlaceAndRenderedAtLeastOnce(
                    data, entry.Kind == "directional" ? cascades : 0);

                entry.Placement = !placed ? "NOT PLACED - would not fit"
                    : rendered ? "placed, rendered"
                    : "placed, NOT YET RENDERED";
            }
            else
            {
                entry.Placement = string.Empty;
            }

            return entry;
        }

        /// <summary>Prints one atlas: its total, then its lights biggest first.</summary>
        private static void ReportAtlas(StringBuilder sb, System.Collections.Generic.List<ShadowEntry> entries,
                                        string kind, bool? cached, string label, long capacity)
        {
            var rows = new System.Collections.Generic.List<ShadowEntry>();
            long used = 0;

            foreach (ShadowEntry entry in entries)
            {
                if (entry.Kind != kind || (cached.HasValue && entry.Cached != cached.Value))
                {
                    continue;
                }

                rows.Add(entry);
                used += entry.Texels;
            }

            if (rows.Count == 0)
            {
                sb.AppendLine($"  {label} : empty");
                return;
            }

            sb.AppendLine($"  {label} : " + (capacity > 0
                ? $"{used / 1048576f,6:F1}M of {capacity / 1048576f:F1}M texels  ({100f * used / capacity:F1}% full)"
                : $"{used / 1048576f,6:F1}M texels"));

            foreach (ShadowEntry entry in rows)
            {
                string share = capacity > 0 ? $"{100f * entry.Texels / capacity,5:F1}%" : "    -";
                sb.AppendLine($"        {share}  {entry.Resolution,5}^2 x{entry.Faces}  {entry.Name}"
                              + (entry.Placement.Length > 0 ? $"   [{entry.Placement}]" : string.Empty));
            }
        }

        /// <summary>
        /// Cascade count from the camera's resolved stack, since it decides how
        /// many maps a directional light actually pays for.
        /// </summary>
        private static int ResolveCascadeCount()
        {
            const int fallback = 4;

            if (VolumeManager.instance == null
                || !ResolveVolumeContext(out Transform trigger, out LayerMask mask, out _))
            {
                return fallback;
            }

            VolumeStack stack = VolumeManager.instance.CreateStack();

            try
            {
                VolumeManager.instance.Update(stack, trigger, mask);
                HDShadowSettings settings = stack.GetComponent<HDShadowSettings>();
                return settings != null ? settings.cascadeShadowSplitCount.value : fallback;
            }
            finally
            {
                stack.Dispose();
            }
        }

        /// <summary>
        /// The camera, and the trigger and layer mask HDRP would evaluate volumes
        /// with for it. Shared so the two sections that resolve a stack cannot
        /// drift apart and start reporting different worlds.
        /// </summary>
        private static bool ResolveVolumeContext(out Transform trigger, out LayerMask mask, out Camera camera)
        {
            trigger = null;
            mask = ~0;
            camera = ResolveReportCamera();

            if (camera == null)
            {
                return false;
            }

            HDAdditionalCameraData data = camera.GetComponent<HDAdditionalCameraData>();
            mask = data != null ? data.volumeLayerMask : ~0;
            trigger = data != null && data.volumeAnchorOverride != null
                ? data.volumeAnchorOverride
                : camera.transform;

            return true;
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

            // The trigger is what HDRP measures local volumes against, and it is
            // the anchor override when one is set rather than the camera itself.
            if (!ResolveVolumeContext(out Transform trigger, out LayerMask mask, out Camera camera))
            {
                sb.AppendLine("--- RESOLVED VOLUME STACK --- (no camera to resolve against)");
                return;
            }

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
