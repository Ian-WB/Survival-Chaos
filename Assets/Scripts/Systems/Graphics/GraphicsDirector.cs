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
        /// </summary>
        private const int QualityEpoch = 3;

        public static GraphicsDirector Instance { get; private set; }

        /// <summary>Raised after anything changes, so controls can follow along.</summary>
        public static event Action SettingsChanged;

        private Volume overrides;
        private ScreenSpaceReflection reflections;
        private ScreenSpaceAmbientOcclusion occlusion;
        private Fog fog;
        private MotionBlur motionBlur;
        private GlobalIllumination globalIllumination;
        private ContactShadows contactShadows;
        private HDShadowSettings shadowSettings;

        /// <summary>
        /// The preset asset the Custom level's clone is cut from.
        ///
        /// Held as a live reference to a real asset, never to a clone. Assigning
        /// a clone to QualitySettings.renderPipeline writes it into a persistent
        /// project setting, so once the clone is destroyed the Custom level is
        /// left pointing at a dead object - and the next SetQualityLevel makes
        /// HDRP adopt it, which is sixteen "referenced script is missing"
        /// warnings per entry. Cloning from here instead means the stale pointer
        /// is always overwritten before anything reads it.
        /// </summary>
        private HDRenderPipelineAsset customBaseAsset;

        /// <summary>The clone actually in use while the Custom level is selected.</summary>
        private HDRenderPipelineAsset customClone;

        /// <summary>Which preset asset <see cref="customClone"/> was cut from.</summary>
        private HDRenderPipelineAsset cloneSource;

        /// <summary>
        /// A clone swapped out but not yet destroyed.
        ///
        /// HDRP tears the outgoing pipeline down lazily, so destroying a clone in
        /// the same frame it stopped being current pulls the asset out from under
        /// a teardown still in progress. The symptom is fourteen to sixteen
        /// "referenced script is missing" warnings on the next repaint - measured,
        /// not guessed: swapping without the destroy produced none, and the
        /// destroy on its own produced them every time.
        ///
        /// Holding it for one generation costs one live object and removes the
        /// race entirely.
        /// </summary>
        private HDRenderPipelineAsset retiredClone;

        /// <summary>
        /// When the pending pipeline rebuild should run, or zero when none is due.
        ///
        /// Swapping the pipeline asset rebuilds render resources and hitches
        /// visibly, so the rows that need one settle after the player stops
        /// pressing rather than rebuilding on every step. Cycling five values
        /// costs one rebuild instead of five.
        /// </summary>
        private float pipelineRebuildDue;

        private const float PipelineRebuildDelay = 0.5f;

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

            // Settles after the player stops stepping, so cycling a row through
            // five values costs one pipeline rebuild rather than five.
            if (pipelineRebuildDue > 0f && Time.unscaledTime >= pipelineRebuildDue)
            {
                pipelineRebuildDue = 0f;
                RebuildCustomPipeline();
                ApplyOverrides();
                SettingsChanged?.Invoke();
            }

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

            // The Custom level is put back onto a real asset rather than a clone
            // that dies with this object. Best-effort: it only reaches the level
            // currently selected, so a player who quits from a preset leaves the
            // dead pointer behind. That is why RestoreSavedCustom exists and
            // never trusts what it finds there.
            if (customBaseAsset != null && QualitySettings.renderPipeline == customClone)
            {
                QualitySettings.renderPipeline = customBaseAsset;
            }

            if (retiredClone != null)
            {
                Destroy(retiredClone);
                retiredClone = null;
            }

            if (customClone != null)
            {
                Destroy(customClone);
                customClone = null;
                cloneSource = null;
            }

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
        /// The preset the per-effect rows fall back to when the player has not
        /// set one themselves.
        ///
        /// Picking a preset clears every row's stored value, so they all resolve
        /// through here and a preset is genuinely one decision rather than eleven
        /// writes that can half-fail.
        /// </summary>
        private GraphicsPreset CurrentPreset => GraphicsPresets.At(IsCustom ? CustomBase : QualityLevel);

        /// <summary>
        /// Which preset a Custom selection was derived from.
        ///
        /// Rows normally all have stored values by the time the selection is
        /// Custom, so this is only reached when one does not - and it has to be
        /// reached correctly, because GraphicsPresets.At clamps. Passing the
        /// Custom index straight in would clamp to the last entry and hand out
        /// Ultra's values, which on a low-end machine is a silent jump to the most
        /// expensive settings in the game.
        /// </summary>
        private int CustomBase => Mathf.Clamp(
            GetInt("CustomBase", GraphicsPresets.DefaultIndex), 0, GraphicsPresets.Count - 1);

        /// <summary>True when the player has tuned rows away from any preset.</summary>
        public bool IsCustom => GraphicsPresets.IsCustom(QualityLevel);

        /// <summary>
        /// Where the Custom level sits in the quality list.
        ///
        /// Appended after the seven presets by the preset builder, so preset
        /// indices stay stable and a saved choice keeps meaning the tier it meant
        /// when it was written.
        /// </summary>
        private int CustomLevel => Mathf.Max(0, QualityNames.Length - 1);

        public ShadowQualityLevel Shadows
        {
            get
            {
                int stored = GetInt("Shadows", -1);
                return stored < 0
                    ? CurrentPreset.Shadows
                    : (ShadowQualityLevel)Mathf.Clamp(stored, 0, QualityLadder.ShadowNames.Length - 1);
            }
            set => SetRow("Shadows", (int)value, rebuildsPipeline: true);
        }

        public EffectQuality AmbientOcclusion
        {
            get => Effect("AO", CurrentPreset.AmbientOcclusion);
            set => SetRow("AO", (int)value, rebuildsPipeline: NeedsPipeline(AmbientOcclusion, value));
        }

        public EffectQuality Reflections
        {
            get => Effect("SSR", CurrentPreset.Reflections);
            set => SetRow("SSR", (int)value, rebuildsPipeline: NeedsPipeline(Reflections, value));
        }

        public EffectQuality GlobalIlluminationQuality
        {
            get => Effect("GI", CurrentPreset.GlobalIllumination);
            set => SetRow("GI", (int)value, rebuildsPipeline: NeedsPipeline(GlobalIlluminationQuality, value));
        }

        public EffectQuality VolumetricFog
        {
            get => Effect("Fog", CurrentPreset.VolumetricFog);
            set => SetRow("Fog", (int)value, rebuildsPipeline: NeedsPipeline(VolumetricFog, value));
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
        /// Motion blur, which is outside the preset system entirely.
        ///
        /// It is taste, not fidelity. Players turn it off on hardware that could
        /// run it at maximum, and a preset stamping over that every time quality
        /// changes would be the settings screen arguing with them. So picking a
        /// preset leaves this row alone, and changing it does not make the
        /// selection Custom - it is not a departure from a preset, because no
        /// preset has an opinion about it.
        ///
        /// It can afford to be independent because it costs no pipeline rebuild:
        /// motion vectors are compiled in for TAA and the upscalers regardless,
        /// so this only ever moves a volume value.
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

        /// <summary>0 renders textures at full resolution, 1 at half.</summary>
        public int TextureMipLimit
        {
            get => Mathf.Clamp(GetInt("TextureMip", CurrentPreset.TextureMipLimit), 0, 1);
            set => SetRow("TextureMip", Mathf.Clamp(value, 0, 1), rebuildsPipeline: false);
        }

        public AnisotropicFiltering Anisotropic
        {
            get => (AnisotropicFiltering)Mathf.Clamp(
                GetInt("Aniso", (int)CurrentPreset.Anisotropic), 0, 2);
            set => SetRow("Aniso", (int)value, rebuildsPipeline: false);
        }

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

        /// <summary>
        /// Whether moving between two rungs changes something the pipeline asset
        /// compiles, rather than only a volume value.
        ///
        /// Crossing the off boundary does - a support flag decides whether the
        /// effect exists at all. Moving between Low, Medium and High does not, and
        /// neither does crossing into the ray-traced half, because ray tracing
        /// support is compiled in at every tier that has it.
        /// </summary>
        private static bool NeedsPipeline(EffectQuality from, EffectQuality to)
        {
            return QualityLadder.IsOn(from) != QualityLadder.IsOn(to);
        }

        // ---------- applying ----------

        private void ApplyAll()
        {
            // Quality first: it swaps the pipeline asset, and everything below
            // layers on top of whichever one ends up active.
            if (IsCustom)
            {
                RestoreSavedCustom();
            }
            else
            {
                QualitySettings.SetQualityLevel(QualityLevel, applyExpensiveChanges: true);
            }

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

            // The two things HDRP still leaves to QualitySettings. Written every
            // Apply rather than only on a preset change, because on the Custom
            // level they are rows the player can move.
            QualitySettings.globalTextureMipmapLimit = TextureMipLimit;
            QualitySettings.anisotropicFiltering = Anisotropic;

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
            contactShadows = overrides.profile.Add<ContactShadows>();
            shadowSettings = overrides.profile.Add<HDShadowSettings>();

            // Set once rather than tiered. maxShadowDistance is chiefly the
            // directional cascade split, and there is no directional light in this
            // project - five lights, all point - so what it still does is fade
            // shadows out, which wants one sane number rather than a ladder.
            //
            // 60 against an arena 27.44 units across. The scene volume used to pin
            // 300 and the HDRP default profile hands out 400, which spread the
            // cascades over roughly fifteen times the entire playable space.
            shadowSettings.maxShadowDistance.overrideState = true;
            shadowSettings.maxShadowDistance.value = 60f;
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

            ApplyAmbientOcclusion();
            ApplyReflections();
            ApplyGlobalIllumination();
            ApplyFog();
            ApplyMotionBlur();

            // Contact shadows come with the shadow rung rather than a row of their
            // own. The pipeline flag decides whether the pass exists; this decides
            // whether it runs.
            PipelineTuning.ShadowRung rung = PipelineTuning.ShadowsFor(Shadows);
            contactShadows.enable.overrideState = true;
            contactShadows.enable.value = rung.ContactShadows;
        }

        private void ApplyAmbientOcclusion()
        {
            EffectQuality quality = AmbientOcclusion;
            bool on = QualityLadder.IsOn(quality);

            occlusion.quality.overrideState = true;
            occlusion.quality.value = QualityLadder.ScalableLevel(quality);

            occlusion.rayTracing.overrideState = true;
            occlusion.rayTracing.value = QualityLadder.IsRayTraced(quality);

            // Intensity is the only way to silence AO - the component has no
            // enabled flag of its own.
            occlusion.intensity.overrideState = !on;
            occlusion.intensity.value = 0f;
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
        /// should stay absent so the platform default still decides.
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

            if (epoch < 3)
            {
                // Which preset a Custom selection came from, read before the loop
                // below deletes it.
                int customBase = Mathf.Clamp(
                    PlayerPrefs.GetInt(Prefix + "CustomBase", GraphicsPresets.DefaultIndex),
                    0, GraphicsPresets.Count - 1);

                // Dropped rather than converted, for both epochs that need it. An
                // old on/off carried no rung to convert to, and a frozen
                // ray-traced rung is the thing being taken back. Clearing lets
                // every row fall back to the chosen preset, which is the closest
                // honest reading of what the player had.
                foreach (string key in RowKeys)
                {
                    PlayerPrefs.DeleteKey(Prefix + key);
                }

                // Retired with the Lighting row it belonged to.
                PlayerPrefs.DeleteKey(Prefix + "Lighting");

                // A Custom selection cannot survive its own rows being cleared -
                // Custom means "these rows", and there are none left. Landing back
                // on the preset it was built from is the closest thing to what the
                // player had; leaving it on Custom would have every row fall
                // through to a preset nobody picked.
                if (PlayerPrefs.GetInt(Prefix + "Quality", -1) >= GraphicsPresets.Count)
                {
                    PlayerPrefs.SetInt(Prefix + "Quality", customBase);
                }
            }

            PlayerPrefs.SetInt(Prefix + "QualityEpoch", QualityEpoch);
            SettingsStore.MarkDirty();
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
            "Shadows", "AO", "SSR", "GI", "Fog",
            "TextureMip", "Aniso", "CustomBase",

            // Retired with the Ray Traced Shadows row. Still cleared, so a value
            // saved by a build that had the row does not sit in prefs forever.
            "RTShadows"

            // MotionBlur is deliberately absent. It sits outside the preset
            // system, so picking a preset must not clear it - that is the whole
            // of what makes it independent.
        };

        /// <summary>
        /// Picking a preset: clear every row so they all fall back to it, then
        /// switch level.
        ///
        /// Clearing rather than writing each row's value means one source of
        /// truth. If this wrote nine keys instead, a preset whose table later
        /// changed would keep handing out the old values to anyone who had
        /// selected it before.
        /// </summary>
        public void SetQuality(int level)
        {
            level = Mathf.Clamp(level, 0, Mathf.Max(0, QualityNames.Length - 1));

            // Custom is not selectable from the Quality row - there is nothing to
            // load, since it means whatever is currently set. Stepping onto it
            // carries on to the far end of the list instead.
            if (GraphicsPresets.IsCustom(level))
            {
                level = GraphicsPresets.DefaultIndex;
            }

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
        /// Brings a saved Custom selection back up, without ever letting HDRP
        /// adopt what the Custom level is pointing at.
        ///
        /// That pointer is a clone from the previous session, which no longer
        /// exists. So this stands on the preset Custom was derived from - a real
        /// asset - takes the clone source from there, and only then moves onto
        /// the Custom level, with the rebuild overwriting the dead pointer before
        /// a frame is drawn.
        /// </summary>
        private void RestoreSavedCustom()
        {
            int baseLevel = CustomBase;

            QualitySettings.SetQualityLevel(baseLevel, applyExpensiveChanges: true);
            customBaseAsset = QualitySettings.renderPipeline as HDRenderPipelineAsset;

            if (customBaseAsset == null)
            {
                // Nothing to clone from. Staying on the preset is a worse answer
                // than the player's own settings but a far better one than a
                // pipeline that does not resolve.
                PlayerPrefs.SetInt(Prefix + "Quality", baseLevel);
                SettingsStore.MarkDirty();
                return;
            }

            QualitySettings.SetQualityLevel(CustomLevel, applyExpensiveChanges: false);
            RebuildCustomPipeline();
        }

        /// <summary>
        /// Writes one row, moves the selection to Custom, and schedules a pipeline
        /// rebuild if the row needs one.
        /// </summary>
        private void SetRow(string key, int value, bool rebuildsPipeline)
        {
            SnapshotRowsIfLeavingPreset();

            PlayerPrefs.SetInt(Prefix + key, value);
            SettingsStore.MarkDirty();

            if (rebuildsPipeline)
            {
                pipelineRebuildDue = Time.unscaledTime + PipelineRebuildDelay;
            }

            Apply();
        }

        /// <summary>
        /// Freezes the preset's values into every row before the selection becomes
        /// Custom.
        ///
        /// Without this, rows the player never touched would have no stored value
        /// and would fall back through CurrentPreset - which, once the level is
        /// Custom, is no longer the preset they started from. Changing one row
        /// would silently move the other eight.
        /// </summary>
        private void SnapshotRowsIfLeavingPreset()
        {
            if (IsCustom)
            {
                return;
            }

            GraphicsPreset preset = CurrentPreset;

            // Recorded before the level moves. It is both the clone source and
            // the thing a saved Custom selection has to be rebuilt from on the
            // next launch, when there is no preset to be standing on.
            PlayerPrefs.SetInt(Prefix + "CustomBase", QualityLevel);
            customBaseAsset = QualitySettings.renderPipeline as HDRenderPipelineAsset;

            PlayerPrefs.SetInt(Prefix + "Shadows", (int)preset.Shadows);
            PlayerPrefs.SetInt(Prefix + "AO", (int)preset.AmbientOcclusion);
            PlayerPrefs.SetInt(Prefix + "SSR", (int)preset.Reflections);
            PlayerPrefs.SetInt(Prefix + "GI", (int)preset.GlobalIllumination);
            PlayerPrefs.SetInt(Prefix + "Fog", (int)preset.VolumetricFog);
            PlayerPrefs.SetInt(Prefix + "TextureMip", preset.TextureMipLimit);
            PlayerPrefs.SetInt(Prefix + "Aniso", (int)preset.Anisotropic);

            PlayerPrefs.SetInt(Prefix + "Quality", CustomLevel);

            // applyExpensiveChanges false on purpose. True would have HDRP adopt
            // whatever the Custom level is currently pointing at - which, after
            // any previous session, is a destroyed clone. Moving without
            // rebuilding lets the assignment below overwrite it first.
            QualitySettings.SetQualityLevel(CustomLevel, applyExpensiveChanges: false);

            // Immediately rather than on the settle timer: the level is pointing
            // at something dead until this runs, and a frame rendered in that
            // window is where the warnings came from. Later row changes are the
            // ones worth debouncing.
            RebuildCustomPipeline();
        }

        /// <summary>
        /// Rebuilds the Custom level's pipeline asset from the current rows.
        ///
        /// Works on a runtime clone, never on an asset. Asset edits made in play
        /// mode are not rolled back when play mode ends, so writing the real one
        /// would mean a player dragging a control in the editor permanently
        /// rewrites the shipped defaults.
        /// </summary>
        private void RebuildCustomPipeline()
        {
            if (!IsCustom || customBaseAsset == null)
            {
                return;
            }

            // Last generation's clone goes now. HDRP finished with it a rebuild
            // ago, so there is no teardown left to disturb.
            if (retiredClone != null)
            {
                Destroy(retiredClone);
                retiredClone = null;
            }

            // One clone per base preset, reused. Rebuilding by instantiating a
            // fresh asset every time meant destroying the outgoing one, which is
            // the race described on retiredClone. ApplyPreset writes every field
            // it owns from the preset rather than reading what is there, so
            // reusing the object cannot accumulate drift.
            if (customClone == null || cloneSource != customBaseAsset)
            {
                retiredClone = customClone;
                customClone = Instantiate(customBaseAsset);
                customClone.name = "HDRP Custom (runtime)";
                cloneSource = customBaseAsset;
            }

            RenderPipelineSettings settings = customClone.currentPlatformRenderPipelineSettings;
            PipelineTuning.ApplyPreset(ref settings, RowsAsPreset());
            customClone.currentPlatformRenderPipelineSettings = settings;

            if (QualitySettings.renderPipeline != customClone)
            {
                QualitySettings.renderPipeline = customClone;
                return;
            }

            // Already current, so assigning it again changes nothing HDRP would
            // notice. Atlas sizes and support flags are structural - they are
            // read when the pipeline is built - so it has to be rebuilt.
            QualitySettings.SetQualityLevel(QualitySettings.GetQualityLevel(), applyExpensiveChanges: true);
        }

        /// <summary>The current rows, in the shape the pipeline writer expects.</summary>
        private GraphicsPreset RowsAsPreset()
        {
            // Starts from the preset the Custom level rests on so the handful of
            // fields with no row - subsurface scattering, decals, minimum spec -
            // keep sane values rather than defaulting to false.
            GraphicsPreset preset = GraphicsPresets.At(GraphicsPresets.Count - 1);

            preset.Name = GraphicsPresets.CustomName;
            preset.Shadows = Shadows;
            preset.AmbientOcclusion = AmbientOcclusion;
            preset.Reflections = Reflections;
            preset.GlobalIllumination = GlobalIlluminationQuality;
            preset.VolumetricFog = VolumetricFog;
            preset.TextureMipLimit = TextureMipLimit;
            preset.Anisotropic = Anisotropic;
            preset.RayTracing = RayTracingAvailable;
            preset.MinimumSpec = false;

            return preset;
        }
    }
}
