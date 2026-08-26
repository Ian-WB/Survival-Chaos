using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>
    /// Holds the graphics settings and applies them, the same shape as
    /// <see cref="AudioDirector"/>: it creates itself, survives scene loads, and
    /// persists every choice, so a setting made on the title screen is still in
    /// force in the game.
    ///
    /// The quality tier does most of the work — under HDRP the shadows, the
    /// atlases and every support flag live in the pipeline asset, one per level.
    /// Those assets are Unity's own stock HDRP tiers and this class never writes
    /// to one. Selecting a tier is a single SetQualityLevel call; what is here is
    /// everything the asset cannot express: display settings, and per-effect
    /// overrides the player wants on top of their chosen tier.
    ///
    /// Those overrides go through a Volume this class owns, at a priority above
    /// anything in the scene. Overriding is the only way to force an effect off:
    /// disabling a component on a lower-priority volume just lets the scene's own
    /// setting win.
    ///
    /// A volume override cannot switch an effect *on* that the pipeline asset did
    /// not compile, though — the support flags are a hard gate above everything
    /// here. Rows whose effect is gated off in the current tier grey themselves
    /// out rather than appearing to work.
    /// </summary>
    public sealed class GraphicsDirector : MonoBehaviour
    {
        private const string Prefix = "SurvivalChaos.Graphics.";

        /// <summary>
        /// Bumped whenever quality level indices move, so a saved choice can be
        /// corrected once rather than silently meaning a different tier.
        ///
        /// 1: the Ubirajara tier was inserted below Very Low, shifting every
        /// existing level up by one. Without this, anyone who had picked Low would
        /// come back to Very Low, and anyone on Very Low would come back to a tier
        /// built for a 2012 integrated GPU - a real graphics downgrade that
        /// nobody asked for and that reads as the update having broken something.
        ///
        /// 2: the effect rows stopped being on/off switches and became Off, Low,
        /// Medium, High and three ray-traced rungs. The keys did not change name,
        /// so a saved "on" - stored as 1 - would come back as Low, the weakest
        /// rung there is, while the quality row still read whatever preset the
        /// player had picked. A silent downgrade wearing the right label.
        ///
        /// 3: the top two presets briefly shipped with ray-traced global
        /// illumination on. GI replaces this scene's baked indirect rather than
        /// adding to it, so every dynamic object rendered as a black silhouette.
        /// Rebuilding the presets fixes what a preset hands out, but not a row
        /// already frozen into a player's prefs, so those are cleared too.
        ///
        /// 4: the eight generated quality levels were replaced by Unity's three
        /// stock HDRP tiers. Every saved index now means a different tier, and
        /// five of the eight no longer exist at all - a saved Ultra, index 6,
        /// would land past the end of a three-entry list. The old indices are
        /// folded onto the nearest surviving tier rather than cleared, so a
        /// player who had chosen the top of the range still comes back to the top
        /// of the range.
        ///
        /// 5: a fourth tier was inserted at index 2. What had been High is Ultra
        /// now, and the new High beneath it is a lighter asset entirely, so a
        /// saved 2 has to become 3 or the player is quietly moved down a rung -
        /// the same shift epoch 1 made, for the same reason. Low and Medium keep
        /// their indices and are left alone, and the rows are kept too: the tier
        /// a player had still exists and still means what it meant, so there is
        /// nothing for a row to have drifted against.
        /// </summary>
        private const int QualityEpoch = 5;

        public static GraphicsDirector Instance { get; private set; }

        /// <summary>Raised after anything changes, so controls can follow along.</summary>
        public static event Action SettingsChanged;

        private Volume overrides;
        private ScreenSpaceReflection reflections;
        private Fog fog;
        private MotionBlur motionBlur;
        private GlobalIllumination globalIllumination;
        private HDShadowSettings shadowSettings;

        // A Custom quality level used to live here, backed by a runtime clone of
        // whichever preset asset it rested on, rebuilt from the rows on a settle
        // timer. All of it is gone with the generated assets: there is nothing to
        // clone, because no row writes a pipeline field any more.
        //
        // Three bugs went with it, and they are worth remembering as the cost of
        // that design rather than as history. Destroying a swapped-out clone
        // raced HDRP's lazy teardown and produced fourteen to sixteen "referenced
        // script is missing" warnings per row change. Assigning a clone to
        // QualitySettings.renderPipeline wrote it into a persistent project
        // setting, so the Custom level came back next session pointing at a
        // destroyed object. And rebuilding the pipeline hitched visibly enough to
        // need debouncing. None of the three is reachable now.

        private List<DisplaySize> sizes;

        /// <summary>
        /// What the render scale control is asking for, as a percentage. Held in a
        /// field because HDRP reads it through a delegate every frame rather than
        /// being told once.
        /// </summary>
        private float requestedPercentage = 100f;

        /// <summary>How often to notice the display changed. Nothing here is urgent.</summary>
        private const float RefreshCheckInterval = 1f;

        private float nextRefreshCheck;

        /// <summary>The refresh rate the current frame cap was derived from.</summary>
        private int appliedRefreshRate;

        private readonly DynamicResolutionController dynamic = new DynamicResolutionController();

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
            MigrateQualityIndex();
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

        /// <summary>
        /// Re-derives the frame cap when the display's refresh rate changes under
        /// us.
        ///
        /// Only "just under display" is affected - a typed cap is a fixed number
        /// and does not care what the monitor is doing. But that one is computed
        /// in Apply, which runs when a *setting* changes and not when the *display*
        /// does, so dragging the game to a second monitor or unplugging a laptop
        /// would leave it capping to a refresh rate that is no longer there.
        ///
        /// Polled rather than event-driven because Unity offers no display-change
        /// callback, and throttled because nothing here needs to react within a
        /// frame.
        /// </summary>
        private void Update()
        {
            DriveDynamicResolution();

            if (FrameCap != DisplayOptions.MatchDisplay || Time.unscaledTime < nextRefreshCheck)
            {
                return;
            }

            nextRefreshCheck = Time.unscaledTime + RefreshCheckInterval;

            if (RefreshRate != appliedRefreshRate)
            {
                Apply();
            }
        }

        /// <summary>
        /// Feeds the controller and hands its answer to the scaler.
        ///
        /// Unscaled time throughout, and skipped entirely while the game is
        /// stopped: a menu renders nothing like the game does, and letting it
        /// steer would drive the resolution somewhere wrong before the player has
        /// even unpaused.
        /// </summary>
        private void DriveDynamicResolution()
        {
            if (!DynamicResolutionOn || Time.timeScale <= 0f)
            {
                return;
            }

            float frameMs = Time.unscaledDeltaTime * 1000f;
            float targetMs = DisplayOptions.TargetFrameMs(DynamicTarget);

            requestedPercentage =
                dynamic.Update(frameMs, targetMs, Time.unscaledDeltaTime) * 100f;
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

            // Nothing to hand back. Every quality level points at a real asset in
            // the project, and this class never replaced one.

            if (Instance == this)
            {
                Instance = null;

                // The scaler is a closure over this object, parked in a static
                // slot inside HDRP. Left in place it outlives the director that
                // owns it and keeps answering with a value nothing is updating
                // any more. Handing back a constant 100 is the honest reading
                // once there is no director to ask.
                DynamicResolutionHandler.SetDynamicResScaler(
                    () => 100f, DynamicResScalePolicyType.ReturnsPercentage);
            }
        }

        /// <summary>
        /// Drops every subscriber to the static event, for the same reason
        /// <see cref="AudioDirector"/> does: static state outlives play mode when
        /// domain reload is disabled, and every other static in the project is
        /// already reset this way.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            SettingsChanged = null;
            Instance = null;
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
        /// Whether ray tracing can be offered at all, which takes both a DXR GPU
        /// and a pipeline asset that compiled support for it.
        ///
        /// The asset half is not academic, and it does not answer the same way
        /// on every tier: Low ships supportRayTracing false, so the rungs cannot
        /// render there whatever the GPU is, while Medium, High and Ultra all
        /// compile it and the rungs are live on a DXR card. Asking only
        /// SystemInfo would offer three rungs on Low that the pipeline then
        /// ignores - the dead control this screen exists to stop shipping.
        /// </summary>
        public bool RayTracingAvailable =>
            SystemInfo.supportsRayTracing &&
            TryPipelineSettings(out RenderPipelineSettings s) && s.supportRayTracing;

        // ---------- stored settings ----------

        /// <summary>
        /// The tier in force: the player's saved choice, or the project default
        /// if they have never made one.
        ///
        /// The fallback is <see cref="GraphicsPresets.DefaultIndex"/> rather than
        /// QualitySettings.GetQualityLevel, which is what it used to be. That
        /// asked ProjectSettings, where the per-platform default is a bare index
        /// with nothing tying it to the tiers that exist: it still held 6 for
        /// Standalone long after the eight generated levels became four, and an
        /// index past the end resolves to the last entry rather than failing, so
        /// a fresh install booted on the most expensive tier in the list. Nothing
        /// reported it, because 6 is not an error - it is an index that used to
        /// mean something else.
        ///
        /// One constant naming the default, in the file that owns the tiers, is
        /// the whole of the fix. The table in ProjectSettings has been corrected
        /// to agree with it, but nothing here depends on it staying that way.
        /// </summary>
        public int QualityLevel
        {
            get
            {
                int top = Mathf.Max(0, QualityNames.Length - 1);

                return Mathf.Clamp(
                    GetInt("Quality", Mathf.Clamp(GraphicsPresets.DefaultIndex, 0, top)), 0, top);
            }

            // Through SetQuality rather than a bare write, so selecting a preset
            // always clears the per-row keys. A direct write would leave the rows
            // set while the label read the preset's name.
            set => SetQuality(value);
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
                SettingsStore.MarkDirty();
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
        /// Frame rate dynamic resolution chases, or 0 when it is off.
        ///
        /// Off by default: it trades sharpness for frame rate without being asked,
        /// and that should be a decision rather than a surprise.
        /// </summary>
        public int DynamicTarget
        {
            get => GetInt("DynamicTarget", 0);
            set
            {
                dynamic.Reset();
                SetInt("DynamicTarget", value);
            }
        }

        public bool DynamicResolutionOn => DynamicTarget > 0;

        /// <summary>
        /// True when an upscaler has taken the resolution away from us.
        ///
        /// DLSS and FSR2 with optimal settings install their own scaler in the
        /// System slot and HDRP prefers it, so dynamic resolution silently stops
        /// applying. The row says so rather than appearing to work.
        /// </summary>
        public bool DynamicOverriddenByUpscaler => Method != UpscaleMethod.Off;

        /// <summary>
        /// Whether DLSS can actually run. The NVIDIA module being installed is
        /// not the same as an RTX card being present, and HDRP does the real
        /// detection, so this asks it rather than guessing from a device name.
        /// </summary>
        public bool DlssAvailable =>
            HDDynamicResolutionPlatformCapabilities.DLSSDetected && PipelineOffers("DLSS");

        /// <summary>Same question for FSR2, which most cards can run but not all.</summary>
        public bool FsrAvailable =>
            HDDynamicResolutionPlatformCapabilities.FSR2Detected && PipelineOffers("FSR2");

        /// <summary>
        /// Whether the pipeline asset lists an upscaler at all.
        ///
        /// Hardware support is not enough: HDRP only walks the upscalers named in
        /// the asset, so one the card can run but the asset omits will simply
        /// never activate. Without this check, taking FSR2 out of the asset leaves
        /// the menu still offering it and doing nothing - a dead control, which is
        /// the failure this project keeps finding.
        ///
        /// Reads the asset; never writes to it.
        /// </summary>
        private static bool PipelineOffers(string upscaler)
        {
            HDRenderPipelineAsset asset =
                GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;

            List<string> names =
                asset?.currentPlatformRenderPipelineSettings.dynamicResolutionSettings.advancedUpscalerNames;

            return names != null && names.Contains(upscaler);
        }

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
        /// How hard the image is sharpened, over a continuous 0..1.
        ///
        /// Continuous rather than stepped because sharpening is the one graphics
        /// setting with no performance cliff behind it. Render scale steps in tens
        /// because 73% is not a choice anyone means to make; sharpness costs the
        /// same everywhere, so it is pure taste and a slider is the honest control.
        ///
        /// Defaults to the Low anchor rather than Medium. Medium is HDRP's own
        /// value, and HDRP's own value is two sharpening passes stacked - which is
        /// the over-sharpened look this control exists to fix.
        /// </summary>
        public float Sharpness
        {
            get => Mathf.Clamp01(GetFloat("Sharpness", DisplayOptions.DefaultSharpness));
            set => SetFloat("Sharpness", Mathf.Clamp01(value));
        }

        /// <summary>
        /// False when nothing on screen is being sharpened, so the row can say so
        /// rather than sitting there doing nothing. Only TAA and FSR sharpen;
        /// FXAA, SMAA and no anti-aliasing at all do not.
        /// </summary>
        public bool SharpeningApplies =>
            Method == UpscaleMethod.Fsr || AntiAliasing == AntiAliasingMode.Taa;

        /// <summary>
        /// The scale the pipeline resolved for the last frame, which is not always
        /// what was asked for - an upscaler negotiates its own, and the handler
        /// clamps to the asset's range.
        ///
        /// Lives here rather than in SystemProfile because reading it needs an SRP
        /// package type, and that file is deliberately kept free of them so it
        /// survives a merge back to URP.
        /// </summary>
        public float ActualRenderScale
        {
            get
            {
                try
                {
                    float scale = DynamicResolutionHandler.instance.GetResolvedScale().x;
                    return scale > 0f ? scale : 1f;
                }
                catch (Exception)
                {
                    // No pipeline instance yet. Native is the honest answer.
                    return 1f;
                }
            }
        }

        /// <summary>
        /// The scale currently being asked for, whoever is deciding it.
        ///
        /// Three things can own this number and only one of them is the Render
        /// Scale control. An upscaler owns it outright; dynamic resolution moves
        /// it every frame; otherwise it is the stored value.
        ///
        /// The dynamic case used to be missing, so with a target set and the
        /// controller holding 60% the menu row and the F3 report both still read
        /// the stored 100%. The row said "Set by dynamic resolution" underneath
        /// while showing a number that was not it.
        /// </summary>
        public float EffectiveRenderScale
        {
            get
            {
                if (Method != UpscaleMethod.Off)
                {
                    return DisplayOptions.ApproximateScale((int)Quality);
                }

                return DynamicResolutionOn ? RequestedRenderScale : RenderScale;
            }
        }

        /// <summary>
        /// What the scaler is handing HDRP right now, as a fraction.
        ///
        /// Distinct from <see cref="ActualRenderScale"/>, which is what the
        /// pipeline resolved. Keeping both is the point: they can legitimately
        /// disagree, and a report showing only one hides which of them is wrong.
        /// </summary>
        public float RequestedRenderScale => Mathf.Clamp01(requestedPercentage / 100f);

        /// <summary>
        /// The tier the per-effect rows fall back to when the player has not set
        /// one themselves.
        ///
        /// Picking a tier clears every row's stored value, so they all resolve
        /// through here and a tier is genuinely one decision rather than six
        /// writes that can half-fail.
        /// </summary>
        private GraphicsPreset CurrentPreset => GraphicsPresets.At(QualityLevel);

        public EffectQuality Reflections
        {
            get => Effect("SSR", CurrentPreset.Reflections);
            set => SetRow("SSR", (int)value);
        }

        public EffectQuality GlobalIlluminationQuality
        {
            get => Effect("GI", CurrentPreset.GlobalIllumination);
            set => SetRow("GI", (int)value);
        }

        public EffectQuality VolumetricFog
        {
            get => Effect("Fog", CurrentPreset.VolumetricFog);
            set => SetRow("Fog", (int)value);
        }

        /// <summary>
        /// What motion blur is set to when the player has never said.
        ///
        /// Medium rather than off: it is what most of the presets used to hand
        /// out before this row left the preset system, so an existing player's
        /// picture does not change underneath them.
        /// </summary>
        private const EffectQuality DefaultMotionBlur = EffectQuality.Medium;

        /// <summary>
        /// Motion blur, which is outside the tier system entirely.
        ///
        /// It is taste, not fidelity. Players turn it off on hardware that could
        /// run it at maximum, and a tier stamping over that every time quality
        /// changes would be the settings screen arguing with them. So picking a
        /// tier leaves this row alone, which is why its key is the one absent
        /// from RowKeys.
        ///
        /// It is also the one row with no support flag to check: motion vectors
        /// are compiled in for TAA and the upscalers regardless, so every tier can
        /// run it and the row never greys out.
        /// </summary>
        public EffectQuality MotionBlurQuality
        {
            get => Effect("MotionBlur", DefaultMotionBlur);
            set
            {
                PlayerPrefs.SetInt(Prefix + "MotionBlur", (int)value);
                SettingsStore.MarkDirty();
                Apply();
            }
        }

        // Texture Quality and Anisotropic Filtering were rows here. Both are
        // QualitySettings fields rather than volume overrides - globalTextureMipmapLimit
        // and anisotropicFiltering - and each quality level carries its own pair.
        // Writing them from here overrode whatever the tier's asset said, which is
        // exactly what this class no longer does. They are the tier's to set.

        // ---------- what the tier's asset actually compiled ----------

        /// <summary>
        /// The pipeline asset in force, or null before one resolves.
        ///
        /// Read only, always. The support flags below are the hard gate above
        /// every volume override this class writes: an effect the asset did not
        /// compile cannot be switched on by a volume, however high its priority.
        /// Asking the asset is what lets a gated row grey itself out instead of
        /// sitting there swallowing clicks - the dead-control failure this screen
        /// keeps finding.
        /// </summary>
        private static HDRenderPipelineAsset PipelineAsset =>
            GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;

        private static bool TryPipelineSettings(out RenderPipelineSettings settings)
        {
            HDRenderPipelineAsset asset = PipelineAsset;

            if (asset == null)
            {
                settings = default;
                return false;
            }

            settings = asset.currentPlatformRenderPipelineSettings;
            return true;
        }

        /// <summary>False in all three stock tiers, which ship supportSSR off.</summary>
        public bool ReflectionsSupported =>
            TryPipelineSettings(out RenderPipelineSettings s) && s.supportSSR;

        /// <summary>True only in High Fidelity among the stock tiers.</summary>
        public bool GlobalIlluminationSupported =>
            TryPipelineSettings(out RenderPipelineSettings s) && (s.supportSSGI || s.supportRayTracing);

        /// <summary>False in Performant, which ships supportVolumetrics off.</summary>
        public bool VolumetricFogSupported =>
            TryPipelineSettings(out RenderPipelineSettings s) && s.supportVolumetrics;

        /// <summary>
        /// Whether the tier compiled dynamic resolution at all.
        ///
        /// False in all three stock tiers, which ship dynamicResolutionSettings
        /// disabled and no advanced upscalers listed. It gates Render Scale as
        /// well as the Dynamic Resolution row: HDRP resolves both through the
        /// same handler, so with this off a render scale below 100% is requested
        /// every frame and discarded every frame.
        /// </summary>
        public bool DynamicResolutionSupported =>
            TryPipelineSettings(out RenderPipelineSettings s) && s.dynamicResolutionSettings.enabled;

        /// <summary>
        /// A stored rung, clamped back to screen space where ray tracing cannot
        /// run.
        ///
        /// A settings file can outlive the graphics card it was written on, and a
        /// row offering RT High on a machine with no DXR is the dead control this
        /// project keeps finding. Same treatment DLAA already gets.
        /// </summary>
        private EffectQuality Effect(string key, EffectQuality fallback)
        {
            int stored = GetInt(key, -1);

            EffectQuality quality = stored < 0
                ? fallback
                : (EffectQuality)Mathf.Clamp(stored, 0, QualityLadder.EffectNames.Length - 1);

            if (QualityLadder.IsRayTraced(quality) && !RayTracingAvailable)
            {
                // Same rung, screen-space half. Dropping to Off instead would
                // silently turn the effect off on a machine that can render it.
                return (EffectQuality)(QualityLadder.ScalableLevel(quality) + 1);
            }

            return quality;
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

            // Recorded so Update can tell when the display has moved out from
            // under a display-derived cap.
            appliedRefreshRate = RefreshRate;

            // Read every frame by the scaler registered in Awake. Not applied by
            // calling ScalableBufferManager directly: HDRP drives that itself from
            // the dynamic resolution handler each frame, so a direct call is
            // overwritten the moment anything else changes the resolution.
            //
            // Left alone while dynamic resolution is running, or every settings
            // change would yank the scale back to the fixed value and the
            // controller would have to climb down again.
            if (!DynamicResolutionOn)
            {
                requestedPercentage = RenderScale * 100f;
            }

            // globalTextureMipmapLimit and anisotropicFiltering were written here.
            // They are per-quality-level values that the chosen level already
            // carries, so writing them from here overrode the tier with a number
            // this class invented. Left to the tier now.

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
            fog = overrides.profile.Add<Fog>();
            motionBlur = overrides.profile.Add<MotionBlur>();
            globalIllumination = overrides.profile.Add<GlobalIllumination>();
            shadowSettings = overrides.profile.Add<HDShadowSettings>();

            // Set once rather than tiered. maxShadowDistance is chiefly the
            // directional cascade split, and there is no directional light in this
            // project - five lights, all point - so what it still does is fade
            // shadows out, which wants one sane number rather than a ladder.
            //
            // 500 against an arena 274.4 units across with camera trailing at radius
            // 212.2 (75 units behind the player lane at radius 137.2).
            shadowSettings.maxShadowDistance.overrideState = true;
            shadowSettings.maxShadowDistance.value = 500f;
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
            //
            // Dynamic resolution needs it too: without this the scaler runs and
            // its answer is discarded, which is what "enabled in the asset but
            // nothing moves" looked like.
            data.allowDynamicResolution =
                method != UpscaleMethod.Off || dlaa || RenderScale < 1f || DynamicResolutionOn;

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
            float amount = Sharpness;

            data.taaSharpenMode = HDAdditionalCameraData.TAASharpenMode.PostSharpen;
            data.taaSharpenStrength = DisplayOptions.TaaSharpenStrength(amount);
            data.taaHistorySharpening = DisplayOptions.TaaHistorySharpening(amount);

            float upscalerSharpness = DisplayOptions.UpscalerSharpness(amount);

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

        /// <summary>
        /// Writes every effect row onto the override volume.
        ///
        /// Authoritative now, where it used to only ever subtract - the old code
        /// overrode a setting when the player turned it off and stood aside when
        /// they turned it on, so the scene's authored value decided the on-state.
        /// That is why two rows were dead in both directions: the scene authored
        /// reflections off, and the default profile authored AO at intensity 0, so
        /// "On" resolved to the same nothing as "Off".
        ///
        /// What is deliberately still not overridden is intensity where the effect
        /// is on. Quality is how much work the effect does - step counts, ray
        /// counts - and that is what these rows sell. How strong it looks is an
        /// authored value, and a settings menu has no business replacing it.
        /// </summary>
        private void ApplyOverrides()
        {
            if (overrides == null)
            {
                return;
            }

            ApplyReflections();
            ApplyGlobalIllumination();
            ApplyFog();
            ApplyMotionBlur();
        }

        private void ApplyReflections()
        {
            EffectQuality quality = Reflections;

            reflections.enabled.overrideState = true;
            reflections.enabled.value = QualityLadder.IsOn(quality);

            reflections.quality.overrideState = true;
            reflections.quality.value = QualityLadder.ScalableLevel(quality);

            reflections.tracing.overrideState = true;
            reflections.tracing.value = QualityLadder.IsRayTraced(quality)
                ? RayCastingMode.RayTracing
                : RayCastingMode.RayMarching;
        }

        private void ApplyGlobalIllumination()
        {
            EffectQuality quality = GlobalIlluminationQuality;

            globalIllumination.enable.overrideState = true;
            globalIllumination.enable.value = QualityLadder.IsOn(quality);

            globalIllumination.quality.overrideState = true;
            globalIllumination.quality.value = QualityLadder.ScalableLevel(quality);

            globalIllumination.tracing.overrideState = true;
            globalIllumination.tracing.value = QualityLadder.IsRayTraced(quality)
                ? RayCastingMode.RayTracing
                : RayCastingMode.RayMarching;
        }

        private void ApplyFog()
        {
            EffectQuality quality = VolumetricFog;

            // Only the volumetric half. Fog itself stays as the scene authored it,
            // because turning the row off should thin the scene out rather than
            // delete the atmosphere the arena was lit against.
            fog.enableVolumetricFog.overrideState = true;
            fog.enableVolumetricFog.value = QualityLadder.IsOn(quality);

            fog.quality.overrideState = true;
            fog.quality.value = QualityLadder.ScalableLevel(quality);
        }

        private void ApplyMotionBlur()
        {
            EffectQuality quality = MotionBlurQuality;

            motionBlur.quality.overrideState = true;
            motionBlur.quality.value = QualityLadder.ScalableLevel(quality);

            motionBlur.intensity.overrideState = !QualityLadder.IsOn(quality);
            motionBlur.intensity.value = 0f;
        }

        // ---------- storage ----------

        /// <summary>
        /// Shifts a saved quality index when tiers have been inserted below it.
        ///
        /// Runs before anything reads QualityLevel, writes through PlayerPrefs
        /// directly rather than through SetInt - that one calls Apply, and there
        /// is nothing to apply to yet this early in Awake - and does nothing at
        /// all for a player who has never touched the setting, whose absent key
        /// should stay absent so the project default still decides.
        /// </summary>
        private static void MigrateQualityIndex()
        {
            int epoch = PlayerPrefs.GetInt(Prefix + "QualityEpoch", 0);

            if (epoch >= QualityEpoch)
            {
                return;
            }

            if (epoch < 1 && PlayerPrefs.HasKey(Prefix + "Quality"))
            {
                PlayerPrefs.SetInt(Prefix + "Quality", PlayerPrefs.GetInt(Prefix + "Quality") + 1);
            }

            if (epoch < 4)
            {
                // Folded before the rows are cleared, because the Custom case
                // reads CustomBase and the clear takes it away.
                if (PlayerPrefs.HasKey(Prefix + "Quality"))
                {
                    PlayerPrefs.SetInt(Prefix + "Quality",
                        FoldedTier(PlayerPrefs.GetInt(Prefix + "Quality")));
                }

                // Dropped rather than converted, for every epoch that needs it.
                // An old on/off carried no rung to convert to, a frozen ray-traced
                // rung is the thing being taken back, and a rung chosen against
                // one of the old generated tiers means nothing against a stock
                // one. Clearing lets every row fall back to the chosen tier, which
                // is the closest honest reading of what the player had.
                foreach (string key in RowKeys)
                {
                    PlayerPrefs.DeleteKey(Prefix + key);
                }

                foreach (string key in RetiredRowKeys)
                {
                    PlayerPrefs.DeleteKey(Prefix + key);
                }
            }

            if (epoch < 5 && PlayerPrefs.HasKey(Prefix + "Quality")
                && PlayerPrefs.GetInt(Prefix + "Quality") >= 2)
            {
                // Inserted at index 2, so only a tier at or above it moves. After
                // the epoch-4 fold rather than before it, because that fold reads
                // and writes the old three-entry indices.
                PlayerPrefs.SetInt(Prefix + "Quality", PlayerPrefs.GetInt(Prefix + "Quality") + 1);
            }

            PlayerPrefs.SetInt(Prefix + "QualityEpoch", QualityEpoch);
            SettingsStore.MarkDirty();
        }

        /// <summary>
        /// Which of the three tiers an index from the old eight-level scheme
        /// becomes.
        ///
        /// Folded onto the nearest survivor rather than reset to the default. The
        /// eight levels were Ubirajara, Very Low, Low, Medium, High, Very High,
        /// Ultra and Custom; someone who had chosen Ultra wanted the top of the
        /// range, and landing them on Medium because the list got shorter would
        /// read as the update having quietly downgraded them.
        /// </summary>
        private static int FoldedTier(int saved)
        {
            // Custom was index 7 and meant "whatever the rows say", which is not a
            // tier at all. The preset it was derived from is the honest answer,
            // and CustomBase is still in prefs at this point.
            if (saved >= 7)
            {
                saved = PlayerPrefs.GetInt(Prefix + "CustomBase", 3);
            }

            if (saved <= 2)
            {
                // Ubirajara, Very Low and Low.
                return 0;
            }

            return saved == 3 ? 1 : 2;
        }

        private static int GetInt(string key, int fallback)
        {
            return PlayerPrefs.GetInt(Prefix + key, fallback);
        }

        private void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            SettingsStore.MarkDirty();
            Apply();
        }

        private static float GetFloat(string key, float fallback)
        {
            return PlayerPrefs.GetFloat(Prefix + key, fallback);
        }

        private void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            SettingsStore.MarkDirty();
            Apply();
        }

        /// <summary>
        /// Every per-row key, so a preset can clear the lot in one place.
        ///
        /// A list rather than a loop over the properties because forgetting one
        /// is silent: the row would keep its old value while the label said the
        /// preset's name, which is exactly the lying-label problem this design
        /// exists to avoid.
        /// </summary>
        private static readonly string[] RowKeys =
        {
            "SSR", "GI", "Fog"

            // MotionBlur is deliberately absent. It sits outside the tier
            // system, so picking a tier must not clear it - that is the whole
            // of what makes it independent.
        };

        /// <summary>
        /// Keys no row writes any more, cleared once by the migration.
        ///
        /// Separate from <see cref="RowKeys"/> because picking a tier should not
        /// keep paying to delete keys nothing has written since the build that
        /// retired them - but leaving them in the settings file forever is worse,
        /// so the migration sweeps them once.
        ///
        /// Shadows was the six-rung Shadow Quality row, TextureMip and Aniso were
        /// QualitySettings values now left to the tier, CustomBase recorded which
        /// preset a Custom selection was cut from, RTShadows was a row that
        /// allocated a buffer nothing wrote to, and Lighting was a Baked/Ray
        /// traced switch that GlobalIllumination replaced. AO and ContactShadows
        /// went when their tiers stopped compiling supportSSAO and
        /// supportContactShadows - a row that can never do anything is worse than
        /// no row, so both were removed rather than left permanently grey.
        /// </summary>
        private static readonly string[] RetiredRowKeys =
        {
            "Shadows", "TextureMip", "Aniso", "CustomBase", "RTShadows",
            "Lighting", "AO", "ContactShadows"
        };

        /// <summary>
        /// Picking a tier: clear every row so they all fall back to it, then
        /// switch level.
        ///
        /// Clearing rather than writing each row's value means one source of
        /// truth. If this wrote five keys instead, a tier whose table later
        /// changed would keep handing out the old values to anyone who had
        /// selected it before.
        /// </summary>
        public void SetQuality(int level)
        {
            level = Mathf.Clamp(level, 0, Mathf.Max(0, QualityNames.Length - 1));

            foreach (string key in RowKeys)
            {
                PlayerPrefs.DeleteKey(Prefix + key);
            }

            PlayerPrefs.SetInt(Prefix + "Quality", level);
            SettingsStore.MarkDirty();

            QualitySettings.SetQualityLevel(level, applyExpensiveChanges: true);
            Apply();
        }

        /// <summary>
        /// Writes one row and applies it.
        ///
        /// Every row is a volume override now, so this is the whole of it. There
        /// is no snapshot of the other rows to take and no pipeline asset to
        /// rebuild: moving a row changes one value on a volume, the tier keeps
        /// whichever stock asset it was pointing at, and the rows the player has
        /// not touched carry on resolving through that tier.
        /// </summary>
        private void SetRow(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            SettingsStore.MarkDirty();
            Apply();
        }

    }
}
