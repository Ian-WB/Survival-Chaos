using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>Which system lights the scene.</summary>
    public enum LightingMode
    {
        /// <summary>Baked lighting and probes only. Works on any hardware.</summary>
        Baked = 0,

        /// <summary>Ray-traced global illumination on top. Needs DXR hardware.</summary>
        RayTraced = 1
    }

    /// <summary>
    /// Holds the graphics settings and applies them, the same shape as
    /// <see cref="AudioDirector"/>: it creates itself, survives scene loads, and
    /// persists every choice, so a setting made on the title screen is still in
    /// force in the game.
    ///
    /// The quality preset does most of the work — under HDRP the shadows and
    /// screen-space effects live in the pipeline asset, one per level, built by
    /// the Build Quality Presets tool. What is here is everything the pipeline
    /// asset cannot express: display settings, and per-effect overrides the player
    /// wants on top of their chosen preset.
    ///
    /// Those overrides go through a Volume this class owns, at a priority above
    /// anything in the scene. Overriding is the only way to force an effect off:
    /// disabling a component on a lower-priority volume just lets the scene's own
    /// setting win.
    /// </summary>
    public sealed class GraphicsDirector : MonoBehaviour
    {
        private const string Prefix = "SurvivalChaos.Graphics.";

        public static GraphicsDirector Instance { get; private set; }

        /// <summary>Raised after anything changes, so controls can follow along.</summary>
        public static event Action SettingsChanged;

        private Volume overrides;
        private ScreenSpaceReflection reflections;
        private ScreenSpaceAmbientOcclusion occlusion;
        private Fog fog;
        private MotionBlur motionBlur;
        private GlobalIllumination globalIllumination;

        private List<DisplaySize> sizes;

        /// <summary>
        /// What the render scale control is asking for, as a percentage. Held in a
        /// field because HDRP reads it through a delegate every frame rather than
        /// being told once.
        /// </summary>
        private float requestedPercentage = 100f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            GameObject host = new GameObject("Graphics Director");
            host.AddComponent<GraphicsDirector>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildOverrideVolume();
            RegisterScaler();
            ApplyAll();

            // The camera settings live on the scene's camera, which is replaced
            // every time a scene loads. Without this they survive only the first.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            ApplyCamera();
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------- available options ----------

        /// <summary>Distinct screen sizes this display supports, smallest first.</summary>
        public List<DisplaySize> Sizes
        {
            get
            {
                if (sizes != null)
                {
                    return sizes;
                }

                List<DisplaySize> reported = new List<DisplaySize>();
                foreach (Resolution resolution in Screen.resolutions)
                {
                    reported.Add(new DisplaySize(resolution.width, resolution.height));
                }

                sizes = DisplayOptions.Distinct(reported);

                if (sizes.Count == 0)
                {
                    // A headless or unusual display reported nothing usable.
                    // Offering the current size beats offering an empty list.
                    sizes.Add(new DisplaySize(Screen.width, Screen.height));
                }

                return sizes;
            }
        }

        public string[] QualityNames => QualitySettings.names;

        /// <summary>
        /// Whether ray tracing can be offered at all. False on hardware without
        /// DXR, and the control hides rather than presenting a setting that
        /// silently does nothing.
        /// </summary>
        public bool RayTracingAvailable => SystemInfo.supportsRayTracing;

        // ---------- stored settings ----------

        public int QualityLevel
        {
            get => Mathf.Clamp(GetInt("Quality", QualitySettings.GetQualityLevel()), 0,
                Mathf.Max(0, QualityNames.Length - 1));
            set => SetInt("Quality", value);
        }

        public int SizeIndex
        {
            get
            {
                DisplaySize saved = new DisplaySize(
                    GetInt("Width", Screen.width), GetInt("Height", Screen.height));
                return DisplayOptions.IndexOf(Sizes, saved);
            }
            set
            {
                DisplaySize size = Sizes[Mathf.Clamp(value, 0, Sizes.Count - 1)];
                PlayerPrefs.SetInt(Prefix + "Width", size.Width);
                PlayerPrefs.SetInt(Prefix + "Height", size.Height);
                Apply();
            }
        }

        public FullScreenMode ScreenMode
        {
            get => (FullScreenMode)GetInt("ScreenMode", (int)FullScreenMode.FullScreenWindow);
            set => SetInt("ScreenMode", (int)value);
        }

        public bool VSync
        {
            get => GetInt("VSync", 1) != 0;
            set => SetInt("VSync", value ? 1 : 0);
        }

        public int FrameCap
        {
            get => GetInt("FrameCap", 0);
            set => SetInt("FrameCap", value);
        }

        /// <summary>The display's refresh rate, or 0 when it reports nothing usable.</summary>
        public int RefreshRate
        {
            get
            {
                double rate = Screen.currentResolution.refreshRateRatio.value;
                return double.IsNaN(rate) || rate <= 0d ? 0 : Mathf.RoundToInt((float)rate);
            }
        }

        /// <summary>
        /// The cap actually handed to Unity, with "just under display" resolved
        /// against whichever monitor the game is on right now.
        /// </summary>
        public int ResolvedFrameCap => DisplayOptions.ResolveCap(FrameCap, RefreshRate);

        /// <summary>Fraction of native resolution the game renders at, 0.5 to 1.</summary>
        public float RenderScale
        {
            get => Mathf.Clamp(GetFloat("RenderScale", 1f), 0.5f, 1f);
            set => SetFloat("RenderScale", Mathf.Clamp(value, 0.5f, 1f));
        }

        /// <summary>
        /// Whether DLSS can actually run. The NVIDIA module being installed is
        /// not the same as an RTX card being present, and HDRP does the real
        /// detection, so this asks it rather than guessing from a device name.
        /// </summary>
        public bool DlssAvailable => HDDynamicResolutionPlatformCapabilities.DLSSDetected;

        /// <summary>Same question for FSR2, which most cards can run but not all.</summary>
        public bool FsrAvailable => HDDynamicResolutionPlatformCapabilities.FSR2Detected;

        public UpscaleMethod Method
        {
            get
            {
                UpscaleMethod stored = (UpscaleMethod)GetInt("UpscaleMethod", (int)UpscaleMethod.Off);
                // A settings file can outlive the graphics card it was written on.
                return Supported(stored) ? stored : UpscaleMethod.Off;
            }
            set => SetInt("UpscaleMethod", (int)value);
        }

        public bool Supported(UpscaleMethod method)
        {
            switch (method)
            {
                case UpscaleMethod.Dlss: return DlssAvailable;
                case UpscaleMethod.Fsr: return FsrAvailable;
                default: return true;
            }
        }

        public UpscaleQuality Quality
        {
            get => (UpscaleQuality)GetInt("UpscaleQuality", (int)UpscaleQuality.Quality);
            set => SetInt("UpscaleQuality", (int)value);
        }

        public AntiAliasingMode AntiAliasing
        {
            get
            {
                AntiAliasingMode stored =
                    (AntiAliasingMode)GetInt("AntiAliasing", (int)AntiAliasingMode.Taa);
                // DLAA is DLSS underneath, so it goes the same way DLSS does on a
                // machine that cannot run it.
                return stored == AntiAliasingMode.Dlaa && !DlssAvailable
                    ? AntiAliasingMode.Taa
                    : stored;
            }
            set => SetInt("AntiAliasing", (int)value);
        }

        /// <summary>
        /// Defaults to Low rather than Medium. Medium is HDRP's own value, and
        /// HDRP's own value is two sharpening passes stacked - which is the
        /// over-sharpened look this control exists to fix.
        /// </summary>
        public SharpnessLevel Sharpness
        {
            get => (SharpnessLevel)GetInt("Sharpness", (int)SharpnessLevel.Low);
            set => SetInt("Sharpness", (int)value);
        }

        /// <summary>
        /// False when nothing on screen is being sharpened, so the row can say so
        /// rather than sitting there doing nothing. Only TAA and FSR sharpen;
        /// FXAA, SMAA and no anti-aliasing at all do not.
        /// </summary>
        public bool SharpeningApplies =>
            Method == UpscaleMethod.Fsr || AntiAliasing == AntiAliasingMode.Taa;

        /// <summary>
        /// The scale the game actually renders at. An upscaler owns this outright;
        /// the Render Scale control only applies when none is selected.
        /// </summary>
        public float EffectiveRenderScale => Method == UpscaleMethod.Off
            ? RenderScale
            : DisplayOptions.ApproximateScale((int)Quality);

        public LightingMode Lighting
        {
            get => RayTracingAvailable
                ? (LightingMode)GetInt("Lighting", (int)LightingMode.Baked)
                : LightingMode.Baked;
            set => SetInt("Lighting", (int)value);
        }

        public bool Reflections
        {
            get => GetInt("SSR", 1) != 0;
            set => SetInt("SSR", value ? 1 : 0);
        }

        public bool AmbientOcclusion
        {
            get => GetInt("AO", 1) != 0;
            set => SetInt("AO", value ? 1 : 0);
        }

        public bool VolumetricFog
        {
            get => GetInt("Fog", 1) != 0;
            set => SetInt("Fog", value ? 1 : 0);
        }

        public bool MotionBlurEnabled
        {
            get => GetInt("MotionBlur", 1) != 0;
            set => SetInt("MotionBlur", value ? 1 : 0);
        }

        // ---------- applying ----------

        private void ApplyAll()
        {
            // Quality first: it swaps the pipeline asset, and everything below
            // layers on top of whichever one ends up active.
            QualitySettings.SetQualityLevel(QualityLevel, applyExpensiveChanges: true);
            Apply();
        }

        private void Apply()
        {
            DisplaySize size = Sizes[Mathf.Clamp(SizeIndex, 0, Sizes.Count - 1)];
            if (Screen.width != size.Width || Screen.height != size.Height ||
                Screen.fullScreenMode != ScreenMode)
            {
                Screen.SetResolution(size.Width, size.Height, ScreenMode);
            }

            QualitySettings.vSyncCount = VSync ? 1 : 0;

            // Unity ignores targetFrameRate entirely while vSyncCount is non-zero,
            // so these cannot be combined however much one might want to cap just
            // below the refresh rate *and* sync. The menu says so rather than
            // letting one silently defeat the other.
            int cap = ResolvedFrameCap;
            Application.targetFrameRate = VSync ? -1 : (cap <= 0 ? -1 : cap);

            // Read every frame by the scaler registered in Awake. Not applied by
            // calling ScalableBufferManager directly: HDRP drives that itself from
            // the dynamic resolution handler each frame, so a direct call is
            // overwritten the moment anything else changes the resolution.
            requestedPercentage = RenderScale * 100f;

            ApplyCamera();
            ApplyOverrides();
            SettingsChanged?.Invoke();
        }

        /// <summary>
        /// Creates the volume this class overrides through. Priority is set well
        /// above anything authored in a scene, because a lower-priority override
        /// cannot turn an effect off — it just loses.
        /// </summary>
        private void BuildOverrideVolume()
        {
            GameObject host = new GameObject("Graphics Overrides");
            host.transform.SetParent(transform, false);

            overrides = host.AddComponent<Volume>();
            overrides.isGlobal = true;
            overrides.priority = 10000f;
            overrides.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            reflections = overrides.profile.Add<ScreenSpaceReflection>();
            occlusion = overrides.profile.Add<ScreenSpaceAmbientOcclusion>();
            fog = overrides.profile.Add<Fog>();
            motionBlur = overrides.profile.Add<MotionBlur>();
            globalIllumination = overrides.profile.Add<GlobalIllumination>();
        }

        /// <summary>
        /// Drives the render scale when no upscaler is running.
        ///
        /// Registered once, into the User slot. DLSS and FSR2 claim the System
        /// slot for themselves when they are active, and HDRP selects User again
        /// at the top of every camera, so this ends up applying exactly when
        /// nothing else is deciding the resolution — which is what the Render
        /// Scale row promises.
        ///
        /// The returned percentage is clamped by HDRP to the pipeline asset's
        /// minimum and maximum, so the asset's floor is the real lower bound
        /// whatever this asks for.
        /// </summary>
        private void RegisterScaler()
        {
            DynamicResolutionHandler.SetDynamicResScaler(
                () => requestedPercentage, DynamicResScalePolicyType.ReturnsPercentage);
        }

        /// <summary>
        /// Anti-aliasing and upscaling live on the camera, not in a Volume, so
        /// this has to find the scene's camera - and find it again after a scene
        /// load, since the old one went with the old scene.
        ///
        /// Everything here is a per-camera field on purpose. The same settings
        /// exist on the pipeline asset, but writing to that asset from a settings
        /// menu edits the project itself: asset changes made in play mode are not
        /// rolled back when play mode ends, so a player dragging a control in the
        /// editor would permanently rewrite the shipped defaults.
        /// </summary>
        private void ApplyCamera()
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.TryGetComponent(out HDAdditionalCameraData data))
            {
                return;
            }

            UpscaleMethod method = Method;
            AntiAliasingMode aa = AntiAliasing;

            // DLAA is DLSS with its scale pinned at native, so it needs the DLSS
            // pass running even though no upscaling is being asked for.
            bool dlaa = method == UpscaleMethod.Off && aa == AntiAliasingMode.Dlaa;
            bool dlss = method == UpscaleMethod.Dlss;
            bool fsr = method == UpscaleMethod.Fsr;

            // An upscaler does its own temporal reconstruction, so HDRP's
            // anti-aliasing is switched off rather than stacked on top of it.
            data.antialiasing = method == UpscaleMethod.Off
                ? HdrpAntialiasing(aa)
                : HDAdditionalCameraData.AntialiasingMode.None;

            // False here stops every advanced upscaler for this camera, including
            // ones listed in the pipeline asset that have no per-camera switch of
            // their own. It is the only honest way to offer "Off".
            data.allowDynamicResolution = method != UpscaleMethod.Off || dlaa || RenderScale < 1f;

            data.allowDeepLearningSuperSampling = dlss || dlaa;
            data.allowFidelityFX2SuperResolution = fsr;

            // Both "use custom" flags are set because HDRP reads the quality and
            // the optimal-settings toggle through different gates - DLSS checks
            // its attributes flag, FSR2 checks its quality flag - and leaving
            // either unset silently falls back to the pipeline asset's value.
            data.deepLearningSuperSamplingUseCustomQualitySettings = true;
            data.deepLearningSuperSamplingUseCustomAttributes = true;
            data.deepLearningSuperSamplingUseOptimalSettings = true;
            data.deepLearningSuperSamplingQuality = dlaa
                ? DisplayOptions.DlssDlaa
                : DisplayOptions.DlssQualityValue((int)Quality);

            data.fidelityFX2SuperResolutionUseCustomQualitySettings = true;
            data.fidelityFX2SuperResolutionUseCustomAttributes = true;
            data.fidelityFX2SuperResolutionUseOptimalSettings = true;
            data.fidelityFX2SuperResolutionQuality = DisplayOptions.Fsr2QualityValue((int)Quality);

            ApplySharpening(data);
        }

        /// <summary>
        /// Sharpening, everywhere it is decided.
        ///
        /// There are three separate knobs and they are easy to fight with. TAA
        /// sharpens twice - once as a post pass, once on the history it samples -
        /// and both are on by default, so turning down only the obvious one leaves
        /// the image still looking crunchy. FSR ignores both and does its own.
        ///
        /// The FSR 1.0 override is set even though the fallback filter is
        /// currently TAA Upscale: the pipeline asset carries fsrSharpness 0.92,
        /// near maximum, and if anything ever selects that filter an unoverridden
        /// camera would inherit it.
        /// </summary>
        private void ApplySharpening(HDAdditionalCameraData data)
        {
            int level = (int)Sharpness;

            data.taaSharpenMode = HDAdditionalCameraData.TAASharpenMode.PostSharpen;
            data.taaSharpenStrength = DisplayOptions.TaaSharpenStrength(level);
            data.taaHistorySharpening = DisplayOptions.TaaHistorySharpening(level);

            float upscalerSharpness = DisplayOptions.UpscalerSharpness(level);

            data.fidelityFX2SuperResolutionEnableSharpening = upscalerSharpness > 0f;
            data.fidelityFX2SuperResolutionSharpening = upscalerSharpness;

            data.fsrOverrideSharpness = true;
            data.fsrSharpness = upscalerSharpness;
        }

        private static HDAdditionalCameraData.AntialiasingMode HdrpAntialiasing(AntiAliasingMode mode)
        {
            switch (mode)
            {
                case AntiAliasingMode.Fxaa:
                    return HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;
                case AntiAliasingMode.Smaa:
                    return HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                case AntiAliasingMode.Taa:
                    return HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;

                // DLAA is not one of HDRP's anti-aliasing modes; the DLSS pass
                // provides it, so HDRP's own is left off.
                default:
                    return HDAdditionalCameraData.AntialiasingMode.None;
            }
        }

        private void ApplyOverrides()
        {
            if (overrides == null)
            {
                return;
            }

            // Only override what the player has actually turned off. Leaving
            // overrideState false where a setting is on means the scene's own
            // authored value still applies, rather than this quietly replacing it.
            reflections.enabled.overrideState = !Reflections;
            reflections.enabled.value = false;

            occlusion.intensity.overrideState = !AmbientOcclusion;
            occlusion.intensity.value = 0f;

            fog.enableVolumetricFog.overrideState = !VolumetricFog;
            fog.enableVolumetricFog.value = false;

            motionBlur.intensity.overrideState = !MotionBlurEnabled;
            motionBlur.intensity.value = 0f;

            // Ray-traced global illumination is the difference between the two
            // lighting modes: off leaves the baked lighting and probes doing the
            // work, which is what every machine without DXR falls back to anyway.
            bool rayTraced = Lighting == LightingMode.RayTraced && RayTracingAvailable;
            globalIllumination.enable.overrideState = true;
            globalIllumination.enable.value = rayTraced;
        }

        // ---------- storage ----------

        private static int GetInt(string key, int fallback)
        {
            return PlayerPrefs.GetInt(Prefix + key, fallback);
        }

        private void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            Apply();
        }

        private static float GetFloat(string key, float fallback)
        {
            return PlayerPrefs.GetFloat(Prefix + key, fallback);
        }

        private void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            Apply();
        }

        /// <summary>
        /// Quality changes swap the pipeline asset, which is expensive enough that
        /// it is not folded into the general Apply path.
        /// </summary>
        public void SetQuality(int level)
        {
            level = Mathf.Clamp(level, 0, Mathf.Max(0, QualityNames.Length - 1));
            PlayerPrefs.SetInt(Prefix + "Quality", level);
            QualitySettings.SetQualityLevel(level, applyExpensiveChanges: true);
            Apply();
        }
    }
}
