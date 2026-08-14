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
    /// Each preset is a copy of one of Unity's three stock tiers with the fields
    /// in <see cref="GraphicsPresets"/> and <see cref="PipelineTuning"/> written
    /// over the top, so anything neither of those names - colour buffer format,
    /// lit shader mode, decal settings - is whatever that stock tier ships with.
    ///
    /// Two exceptions. The bottom tier touches the colour buffer format and lit
    /// shader mode as well; see <see cref="ApplyMinimumSpec"/>. And every tier
    /// gets the same lighting quality tables, because the three stock bases each
    /// ship their own and inheriting them meant "Medium" was four different
    /// amounts of work depending on which preset you were standing on.
    /// </summary>
    public static class HdrpQualityPresetBuilder
    {
        /// <summary>
        /// The three assets Unity ships with a new HDRP project, used here as the
        /// starting point each tier is cut down from.
        ///
        /// They replaced a single hand-authored base. The reason is that the old
        /// base had drifted: ray tracing on, a 2048 cookie atlas, an 8192 cached
        /// area shadow atlas, a probe volume budget reserving 416 MB. Every preset
        /// inherited all of it, and the tier list could only undo what it
        /// explicitly named. Starting from Unity's own tiers means the parts this
        /// file says nothing about are already sane rather than already inflated.
        /// </summary>
        /// <remarks>
        /// Under an Editor folder deliberately. Unity excludes anything below one
        /// from player builds by rule rather than by whether something happens to
        /// reference it, and these are inputs to a menu item rather than assets the
        /// game ever loads.
        /// </remarks>
        private const string BasePerformant = "Assets/Settings/Quality/Editor/HDRP Performant.asset";
        private const string BaseBalanced = "Assets/Settings/Quality/Editor/HDRP Balanced.asset";
        private const string BaseHighFidelity = "Assets/Settings/Quality/Editor/HDRP High Fidelity.asset";

        private const string PresetFolder = "Assets/Settings/Quality";

        [MenuItem("Survival Chaos/Graphics/Build Quality Presets", priority = 40)]
        public static void Build()
        {
            foreach (string path in new[] { BasePerformant, BaseBalanced, BaseHighFidelity })
            {
                if (AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path) == null)
                {
                    Debug.LogError("No HDRP asset at " + path + ". All three stock tiers have to be " +
                                   "present before presets can be built from them.");
                    return;
                }
            }

            EnsureFolder();
            EnsureLevel("Ubirajara", 0);
            EnsureLevel(GraphicsPresets.CustomName, QualitySettings.names.Length);

            var built = new System.Collections.Generic.Dictionary<string, HDRenderPipelineAsset>();

            for (int i = 0; i < GraphicsPresets.Count; i++)
            {
                GraphicsPreset preset = GraphicsPresets.All[i];
                built[preset.Name] = BuildPreset(preset, BaseFor(i));
            }

            // Custom rests on a copy of Medium. The director replaces it with a
            // runtime clone the moment anything is tuned, but the level needs a
            // real asset on disk so it is never left pointing at nothing.
            built[GraphicsPresets.CustomName] =
                BuildPreset(MediumAsCustom(), BaseFor(MediumIndex));

            AssetDatabase.SaveAssets();
            int assigned = Assign(built);

            Debug.Log("Built " + built.Count + " HDRP quality presets in " + PresetFolder +
                      " and assigned them to " + assigned + " quality levels, plus one Custom level " +
                      "the graphics menu writes into. Every preset carries the same lighting quality " +
                      "tables, so Low, Medium and High mean one thing across all of them.");
        }

        private const int MediumIndex = 3;

        /// <summary>
        /// Custom starts life as Medium under a different name, so a player who
        /// opens the menu on a fresh install and immediately changes one row gets
        /// a sane starting point rather than whatever happened to be first.
        /// </summary>
        private static GraphicsPreset MediumAsCustom()
        {
            GraphicsPreset preset = GraphicsPresets.At(MediumIndex);
            preset.Name = GraphicsPresets.CustomName;
            return preset;
        }

        /// <summary>
        /// Which stock tier each preset is cut from: Performant below Medium,
        /// Balanced at Medium, High Fidelity above it.
        /// </summary>
        private static string BaseFor(int index)
        {
            if (index < MediumIndex)
            {
                return BasePerformant;
            }

            return index == MediumIndex ? BaseBalanced : BaseHighFidelity;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(PresetFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Quality");
            }
        }

        /// <summary>
        /// Adds a quality level if the project has none by that name, so new tiers
        /// can be built without anyone opening Project Settings first.
        ///
        /// QualitySettings offers no API for adding a level - names is read-only -
        /// so this goes at the serialised asset directly. Inserting duplicates the
        /// neighbouring element, which is the behaviour wanted here: the new tier
        /// starts as a copy and Assign then points it at its own preset.
        ///
        /// Inserting anywhere but the end shifts every level below it up by one.
        /// Two places store an index rather than a name and both have to move with
        /// it: the per-platform defaults below, and the player's saved choice,
        /// which GraphicsDirector migrates on next launch.
        /// </summary>
        private static bool EnsureLevel(string levelName, int at)
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
                    "in Project Settings > Quality, then run this again.");
                return false;
            }

            int index = Mathf.Clamp(at, 0, levels.arraySize);
            levels.InsertArrayElementAtIndex(index);
            levels.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue = levelName;

            // Only levels below the insertion point move. Appending at the end
            // shifts nothing, which is why Custom goes there.
            SerializedProperty defaults = serialized.FindProperty("m_PerPlatformDefaultQuality");
            if (defaults != null && defaults.isArray && index < levels.arraySize - 1)
            {
                for (int i = 0; i < defaults.arraySize; i++)
                {
                    SerializedProperty stored = defaults.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("second");

                    if (stored != null && stored.intValue >= index)
                    {
                        stored.intValue += 1;
                    }
                }
            }

            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Debug.Log("Added quality level '" + levelName + "' at index " + index + ".");
            return true;
        }

        /// <summary>
        /// Turns dynamic resolution on for every tier.
        ///
        /// Applied unconditionally because the three stock bases all ship it off,
        /// at 100% minimum, and this game drives it directly: GraphicsDirector
        /// registers a scaler through DynamicResolutionHandler.SetDynamicResScaler,
        /// DynamicResolutionController decides the scale, and the graphics menu
        /// exposes a render scale row. All of that goes quiet if the pipeline asset
        /// says the feature is disabled - no error, no warning, the scaler simply
        /// never takes effect.
        /// </summary>
        private static void ApplyDynamicResolution(ref RenderPipelineSettings settings)
        {
            GlobalDynamicResolutionSettings resolution = settings.dynamicResolutionSettings;

            resolution.enabled = true;
            resolution.minPercentage = 50f;
            resolution.maxPercentage = 100f;
            resolution.upsampleFilter = DynamicResUpscaleFilter.TAAU;
            resolution.useMipBias = true;

            settings.dynamicResolutionSettings = resolution;
        }

        /// <summary>
        /// The bottom tier's extra austerity, which is about video memory rather
        /// than about effects.
        ///
        /// The reference machine reports 128 MB. Everything the preset table
        /// controls is already off by the time Very Low is reached, and none of it
        /// is what fills that budget - the render targets are.
        ///
        /// Deferred shading keeps a full-screen GBuffer alive for the whole frame,
        /// several render targets wide. Forward drops it outright, and this game
        /// has few enough lights per pixel that forward is the cheaper shape
        /// anyway. R11G11B10 then halves the main colour buffer against the
        /// R16G16B16A16 the other tiers use, at a cost in banding that matters
        /// less than launching does.
        /// </summary>
        private static void ApplyMinimumSpec(ref RenderPipelineSettings settings)
        {
            settings.supportedLitShaderMode = RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly;
            settings.colorBufferFormat = RenderPipelineSettings.ColorBufferFormat.R11G11B10;

            // Motion vectors are a full-screen target that only TAA, motion blur
            // and the advanced upscalers read. None of the three is reachable at
            // this tier.
            settings.supportMotionVectors = false;

            settings.supportDistortion = false;
            settings.supportSSRTransparent = false;
            settings.supportTransparentBackface = false;
            settings.supportTransparentDepthPrepass = false;
            settings.supportTransparentDepthPostpass = false;

            settings.supportVFXRayTracing = false;

            // The probe volume pool is sized by this setting rather than by how
            // much was baked, and the editor is blunt about the result: at
            // MemoryBudgetHigh it reserves 416 MB to hold roughly 2.4 MB of actual
            // probe data. On a 128 MB card that one line is fatal on its own.
            settings.probeVolumeMemoryBudget = ProbeVolumeTextureMemoryBudget.MemoryBudgetLow;
            settings.supportProbeVolumeDiskStreaming = false;

            // These size GPU-side buffers whether or not the lights exist. The
            // arena runs three mixed point lights and a bullet light pool, which
            // is well inside 32.
            GlobalLightLoopSettings lights = settings.lightLoopSettings;
            lights.maxPunctualLightsOnScreen = 32;
            lights.maxAreaLightsOnScreen = 1;
            lights.maxDecalsOnScreen = 1;
            lights.maxCubeReflectionOnScreen = 2;
            lights.maxPlanarReflectionOnScreen = 1;
            settings.lightLoopSettings = lights;

            HDShadowInitParameters shadows = settings.hdShadowInitParams;
            shadows.cachedPunctualLightShadowAtlas = 256;
            shadows.cachedAreaLightShadowAtlas = 256;
            shadows.maxDirectionalShadowMapResolution = 256;
            shadows.maxAreaShadowMapResolution = 256;

            // The shadow rung covers punctual and directional filtering. Area
            // shadows have their own quality enum and their own default, and this
            // asset inherited High from the base while Unity's default is Medium.
            shadows.areaShadowFilteringQuality = HDAreaShadowFilteringQuality.Medium;
            settings.hdShadowInitParams = shadows;

            // CatmullRom rather than the inherited TAAU. It is the cheap one, and
            // TAAU is temporal: it needs the motion vectors this method switched
            // off thirty lines ago, so leaving it selected would ask for an
            // upscaler that cannot run.
            GlobalDynamicResolutionSettings resolution = settings.dynamicResolutionSettings;
            resolution.upsampleFilter = DynamicResUpscaleFilter.CatmullRom;
            resolution.useMipBias = false;
            settings.dynamicResolutionSettings = resolution;
        }

        /// <summary>
        /// Copies the base asset and writes the preset over it, so every setting
        /// neither names keeps whatever the stock tier authored.
        /// </summary>
        private static HDRenderPipelineAsset BuildPreset(GraphicsPreset preset, string basePath)
        {
            string path = PresetFolder + "/HDRP " + preset.Name + ".asset";

            if (AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CopyAsset(basePath, path);
            HDRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);

            RenderPipelineSettings settings = asset.currentPlatformRenderPipelineSettings;

            PipelineTuning.ApplyPreset(ref settings, preset);
            ApplyDynamicResolution(ref settings);

            // Off, which is what this project's own base asset had. The stock
            // tiers turn it on, and it needs DOTS instancing shader variants kept
            // in the build. That costs build size and shader compile time to
            // accelerate draw submission, and the profiler puts this game's main
            // thread at 99% idle waiting on present - a cost with nothing on the
            // other side of it.
            //
            // Here rather than in PipelineTuning because the mode enum is in
            // Unity.RenderPipelines.GPUDriven, which only the editor assembly
            // references.
            GlobalGPUResidentDrawerSettings drawer = settings.gpuResidentDrawerSettings;
            drawer.mode = GPUResidentDrawerMode.Disabled;
            settings.gpuResidentDrawerSettings = drawer;

            // Last, so it reads the shadow and resolution parameters the preset
            // just wrote rather than the base asset's.
            if (preset.MinimumSpec)
            {
                ApplyMinimumSpec(ref settings);
            }

            asset.currentPlatformRenderPipelineSettings = settings;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// Points each quality level at its preset, and writes the handful of
        /// things that still live in QualitySettings rather than the pipeline.
        ///
        /// QualitySettings.renderPipeline reads and writes the <em>current</em>
        /// level only, so this has to walk the levels and put the original back
        /// afterwards rather than leaving the editor on whatever it finished on.
        /// </summary>
        private static int Assign(System.Collections.Generic.Dictionary<string, HDRenderPipelineAsset> built)
        {
            string[] names = QualitySettings.names;
            int original = QualitySettings.GetQualityLevel();
            int assigned = 0;

            for (int i = 0; i < names.Length; i++)
            {
                if (!built.TryGetValue(names[i], out HDRenderPipelineAsset asset))
                {
                    Debug.LogWarning("Quality level '" + names[i] + "' has no matching preset, so it " +
                                     "still falls back to the project-wide HDRP asset.");
                    continue;
                }

                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = asset;

                GraphicsPreset preset = names[i] == GraphicsPresets.CustomName
                    ? GraphicsPresets.At(MediumIndex)
                    : GraphicsPresets.At(i);

                ApplyQualitySettings(preset);
                assigned++;
            }

            QualitySettings.SetQualityLevel(original, applyExpensiveChanges: false);
            AssetDatabase.SaveAssets();
            return assigned;
        }

        /// <summary>
        /// The few things HDRP still reads from QualitySettings, plus the ones it
        /// does not read that were previously left to drift.
        ///
        /// Writes the current level, so the caller has to be standing on it.
        /// </summary>
        private static void ApplyQualitySettings(GraphicsPreset preset)
        {
            PipelineTuning.ShadowRung rung = PipelineTuning.ShadowsFor(preset.Shadows);

            // HDRP does not read this for its own rendering, but it is the
            // standard place Unity records the intent, and it lets anything else
            // in the game - the bullet lights, for one - find out without taking a
            // dependency on the render pipeline.
            QualitySettings.shadows = rung.DynamicShadows
                ? ShadowQuality.All
                : ShadowQuality.Disable;

            QualitySettings.shadowmaskMode = rung.DistanceShadowmask
                ? ShadowmaskMode.DistanceShadowmask
                : ShadowmaskMode.Shadowmask;

            QualitySettings.globalTextureMipmapLimit = preset.TextureMipLimit;
            QualitySettings.anisotropicFiltering = preset.Anisotropic;

            // Normalised rather than tiered, because nothing reads them and the
            // ladders they were carrying were noise. LOD bias ran 1.0 on the
            // bottom tier against 0.3 on the one above it - inverted - and there
            // is not one LODGroup in the project for either value to act on.
            QualitySettings.lodBias = 1f;
            QualitySettings.maximumLODLevel = 0;

            // No reflection probe exists in any scene or prefab, cube or planar.
            QualitySettings.realtimeReflectionProbes = false;

            // HDRP reads neither of these - shadow distance and cascade count come
            // from HDShadowSettings on the volume stack. Left at a neutral value
            // so nobody reads the old 15/20/40/70/150 ladder as meaningful.
            QualitySettings.shadowDistance = 60f;
            QualitySettings.shadowCascades = 1;
        }
    }
}
