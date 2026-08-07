using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>
    /// How the image is resolved: anti-aliasing, or an upscaler that replaces it.
    ///
    /// One list rather than two controls, because these are mutually exclusive -
    /// DLSS and FSR2 do their own temporal reconstruction and take over from
    /// anti-aliasing entirely. Presented separately, a player could ask for TAA
    /// and DLSS together and get something neither of them describes.
    ///
    /// The upscalers carry their render scale with them, which is why they are
    /// listed as quality modes rather than as a bare on/off next to the separate
    /// Render Scale control - two settings owning one number is how you end up
    /// with DLSS at 100%, doing nothing at all.
    /// </summary>
    public enum UpscalingMode
    {
        Off = 0,
        Smaa = 1,
        Taa = 2,
        FsrQuality = 3,
        FsrBalanced = 4,
        FsrPerformance = 5,
        DlssQuality = 6,
        DlssBalanced = 7,
        DlssPerformance = 8
    }

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

        public UpscalingMode Upscaling
        {
            get
            {
                UpscalingMode stored = (UpscalingMode)GetInt("Upscaling", (int)UpscalingMode.Taa);
                // A settings file can outlive the graphics card it was written on.
                return IsDlss(stored) && !DlssAvailable ? UpscalingMode.Taa : stored;
            }
            set => SetInt("Upscaling", (int)value);
        }

        public static bool IsDlss(UpscalingMode mode)
        {
            return mode >= UpscalingMode.DlssQuality;
        }

        public static bool IsUpscaler(UpscalingMode mode)
        {
            return mode >= UpscalingMode.FsrQuality;
        }

        /// <summary>
        /// The scale the game actually renders at. An upscaler owns this outright;
        /// the Render Scale control only applies when none is selected.
        /// </summary>
        public float EffectiveRenderScale
        {
            get
            {
                switch (Upscaling)
                {
                    case UpscalingMode.FsrQuality:
                    case UpscalingMode.DlssQuality: return 0.67f;
                    case UpscalingMode.FsrBalanced:
                    case UpscalingMode.DlssBalanced: return 0.58f;
                    case UpscalingMode.FsrPerformance:
                    case UpscalingMode.DlssPerformance: return 0.5f;
                    default: return RenderScale;
                }
            }
        }

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
            // A frame cap is ignored while VSync is on, so the two are not
            // independent and the menu says so rather than letting one silently
            // defeat the other.
            Application.targetFrameRate = VSync ? -1 : (FrameCap <= 0 ? -1 : FrameCap);

            // No-op unless the pipeline asset has dynamic resolution enabled;
            // harmless either way, and the lever that helps weak hardware most.
            float scale = EffectiveRenderScale;
            ScalableBufferManager.ResizeBuffers(scale, scale);

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
        /// Anti-aliasing and upscaling live on the camera, not in a Volume, so
        /// this has to find the scene's camera - and find it again after a scene
        /// load, since the old one went with the old scene.
        /// </summary>
        private void ApplyCamera()
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.TryGetComponent(out HDAdditionalCameraData data))
            {
                return;
            }

            UpscalingMode mode = Upscaling;
            bool upscaling = IsUpscaler(mode);
            bool dlss = IsDlss(mode);

            // An upscaler does its own temporal reconstruction, so HDRP's
            // anti-aliasing is switched off rather than stacked on top of it.
            data.antialiasing = upscaling
                ? HDAdditionalCameraData.AntialiasingMode.None
                : Antialiasing(mode);

            data.allowDynamicResolution = upscaling || RenderScale < 1f;
            data.allowDeepLearningSuperSampling = dlss;
            data.allowFidelityFX2SuperResolution = upscaling && !dlss;
        }

        private static HDAdditionalCameraData.AntialiasingMode Antialiasing(UpscalingMode mode)
        {
            switch (mode)
            {
                case UpscalingMode.Smaa:
                    return HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                case UpscalingMode.Taa:
                    return HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
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
