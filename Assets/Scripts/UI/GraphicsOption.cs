using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Which setting a row drives.
    ///
    /// Numbers are fixed rather than reordered to match the screens, because the
    /// value is serialised into every row the menu builder has already placed —
    /// renumbering would silently repoint existing rows at other settings.
    /// </summary>
    public enum GraphicsOptionKind
    {
        Quality = 0,
        Resolution = 1,
        ScreenMode = 2,
        VSync = 3,
        FrameCap = 4,
        RenderScale = 5,
        Reflections = 7,
        VolumetricFog = 9,
        MotionBlur = 10,
        UpscaleMethod = 12,
        UpscaleQuality = 13,
        AntiAliasing = 14,
        DynamicResolution = 16,
        GlobalIllumination = 17,

        // 15 was Sharpness, before it became a slider rather than a cycler. The
        // number stays retired rather than reused: a row serialised as 15 by an
        // older build would otherwise silently become whatever took its place.
        //
        // 6 was Lighting, a two-way Baked/Ray traced switch whose only job was
        // toggling global illumination. GlobalIllumination replaced it with a
        // ladder that carries the same choice plus how much to spend on it, so 6
        // is retired for the same reason.
        //
        // 11 was Volumetric Clouds. There is no sky to cloud in an arena that
        // sits inside a cubemap, so the row is gone and the tier is off.
        //
        // 19 was Ray Traced Shadows, which could not work as built. HDRP delivers
        // ray-traced shadows per light and the row only set a pipeline flag, so
        // it allocated a screen-space shadow buffer nothing wrote to - no effect
        // in the editor, haze in a build.
        //
        // 20 was Texture Quality and 21 Anisotropic Filtering. Both are
        // QualitySettings values that every quality level already carries, so the
        // rows were overriding the chosen tier with numbers the menu invented.
        // The tier decides them now, and both numbers stay retired.
        //
        // 8 was Ambient Occlusion and 18 Contact Shadows. Both rows worked, and
        // both stopped being reachable when the tiers stopped compiling
        // supportSSAO and supportContactShadows - leaving them would have meant
        // two permanently grey rows, which is worse than no row at all. Re-tick
        // either flag and the row has to come back with its number.
    }

    /// <summary>
    /// One row of the graphics screen: a label, a value, and a step either way.
    ///
    /// Every setting is a cycler, including the on/off ones. A toggle and a
    /// two-item cycler do the same job, and having one control type means one
    /// place to fix, one visual language, and no argument about which settings
    /// deserve which widget. It also suits settings with performance cliffs —
    /// render scale steps in tens rather than sliding continuously, because 73%
    /// is not a choice anybody means to make.
    ///
    /// The row reads its value back from <see cref="GraphicsDirector"/> rather
    /// than remembering one, so two screens showing the same setting cannot
    /// disagree.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Graphics Option")]
    public sealed class GraphicsOption : MonoBehaviour
    {
        private static readonly FullScreenMode[] ScreenModes =
        {
            FullScreenMode.ExclusiveFullScreen,
            FullScreenMode.FullScreenWindow,
            FullScreenMode.Windowed
        };

        private static readonly string[] ScreenModeNames =
        {
            "Fullscreen", "Borderless", "Windowed"
        };

        /// <summary>Render scale in whole tens; anything finer is false precision.</summary>
        private static readonly float[] RenderScales = { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f };

        [SerializeField]
        private GraphicsOptionKind kind = GraphicsOptionKind.Quality;

        [SerializeField]
        [Tooltip("Shows the current value.")]
        private TMP_Text value;

        [SerializeField]
        [Tooltip("Shown under the row when the setting needs explaining.")]
        private TMP_Text note;

        [SerializeField]
        [Tooltip("The two steppers, dimmed and blocked while the row cannot do anything.")]
        private Button previousButton;

        [SerializeField]
        private Button nextButton;

        /// <summary>
        /// How much of its colour a row keeps once it has nothing to offer.
        ///
        /// Dimmed rather than hidden. A row that vanishes moves everything below
        /// it and reads as a setting the build does not have; a row that stays put
        /// and goes quiet reads as a setting that is unavailable right now, which
        /// is what it is - and its note line is sitting underneath saying why.
        /// </summary>
        private const float DimmedAlpha = 0.3f;

        /// <summary>
        /// The colours the menu builder gave this row, captured before anything
        /// dims them.
        ///
        /// Read from the objects rather than hardcoded, so a change to the holo
        /// palette does not leave every disabled row restoring itself to a colour
        /// that is no longer used anywhere.
        /// </summary>
        private Color labelColour;
        private Color valueColour;
        private bool capturedColours;

        private TMP_Text label;

        private void OnEnable()
        {
            GraphicsDirector.SettingsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GraphicsDirector.SettingsChanged -= Refresh;
        }

        /// <summary>How often the one live row re-reads its value.</summary>
        private const float LiveRefreshInterval = 0.25f;

        private float nextLiveRefresh;

        /// <summary>
        /// Keeps the Render Scale row honest while dynamic resolution is moving.
        ///
        /// Every other row changes only when the player changes it, so
        /// SettingsChanged is enough. This one has a value that moves on its own,
        /// and a row that reports a number it read once is worse than one that
        /// reports nothing. Four times a second is legible without turning a
        /// settings row into a per-frame string build.
        /// </summary>
        private void Update()
        {
            // An enum compare, not a disable: setting enabled false here would
            // raise OnDisable and drop this row's SettingsChanged subscription,
            // and it would never update again.
            if (kind != GraphicsOptionKind.RenderScale)
            {
                return;
            }

            GraphicsDirector director = GraphicsDirector.Instance;
            if (director == null || !director.DynamicResolutionOn)
            {
                return;
            }

            if (Time.unscaledTime < nextLiveRefresh)
            {
                return;
            }

            nextLiveRefresh = Time.unscaledTime + LiveRefreshInterval;
            Refresh();
        }

        /// <summary>Wired to the row's two buttons. +1 and -1.</summary>
        public void Step(int direction)
        {
            GraphicsDirector director = GraphicsDirector.Instance;
            if (director == null || !Available(director))
            {
                return;
            }

            int count = Count(director);
            if (count <= 1)
            {
                return;
            }

            // Wraps rather than clamping: with three or four entries, running into
            // an invisible end is more annoying than looping.
            int next = (Index(director) + direction + count) % count;
            Apply(director, next);
        }

        public void Next() => Step(1);

        public void Previous() => Step(-1);

        private void Refresh()
        {
            GraphicsDirector director = GraphicsDirector.Instance;

            if (value != null)
            {
                value.text = director == null ? "-" : Describe(director);
            }

            if (note != null)
            {
                note.text = director == null ? string.Empty : Note(director);
                note.gameObject.SetActive(note.text.Length > 0);
            }

            // A row with one entry cannot be stepped either, so it greys out for
            // the same reason an unavailable one does - the arrows would do
            // nothing. That is how the ray-traced rungs disappearing on a machine
            // without DXR can leave a row with nothing left to choose.
            bool live = director != null && Available(director) && Count(director) > 1;
            ApplyEnabledLook(live);
        }

        /// <summary>
        /// Dims the row and blocks its steppers when there is nothing to change.
        ///
        /// Until this existed an inert row looked exactly like a working one and
        /// simply swallowed the click, which is the same failure as a setting that
        /// silently does nothing - the thing this whole screen was rebuilt to stop
        /// doing.
        /// </summary>
        private void ApplyEnabledLook(bool live)
        {
            CaptureColours();

            if (label != null)
            {
                label.color = live ? labelColour : Dim(labelColour);
            }

            if (value != null)
            {
                value.color = live ? valueColour : Dim(valueColour);
            }

            // interactable rather than disabling the object: it stops the click,
            // takes the button through its own disabled tint, and leaves the
            // arrows in place so the row keeps its shape.
            if (previousButton != null)
            {
                previousButton.interactable = live;
            }

            if (nextButton != null)
            {
                nextButton.interactable = live;
            }
        }

        private void CaptureColours()
        {
            if (capturedColours)
            {
                return;
            }

            // The label lives on this same object - the menu builder adds this
            // component to the caption.
            label = GetComponent<TMP_Text>();

            labelColour = label != null ? label.color : Color.white;
            valueColour = value != null ? value.color : Color.white;
            capturedColours = true;
        }

        private static Color Dim(Color colour)
        {
            return new Color(colour.r, colour.g, colour.b, colour.a * DimmedAlpha);
        }

        /// <summary>False when the machine cannot offer the setting at all.</summary>
        private bool Available(GraphicsDirector director)
        {
            switch (kind)
            {
                // Inert while something else is driving the resolution, and also
                // when the tier never compiled the handler that applies any of it.
                //
                // The director owns that first test rather than this row, because
                // it is the same rule as the one deciding where the scale comes
                // from - and an upscaler no longer always wins it. On a vendor
                // preset the ratio is fixed and this row is inert as before; on
                // Custom the row is what decides, which is the whole point of it.
                case GraphicsOptionKind.RenderScale:
                    return director.DynamicResolutionSupported && director.RenderScaleDecides;

                case GraphicsOptionKind.DynamicResolution:
                    return director.DynamicResolutionSupported;

                // Nothing to set the quality of until an upscaler is chosen.
                case GraphicsOptionKind.UpscaleQuality:
                    return director.Method != UpscaleMethod.Off;

                // An upscaler replaces anti-aliasing rather than running beside it.
                case GraphicsOptionKind.AntiAliasing:
                    return director.Method == UpscaleMethod.Off;

                // The pipeline asset is a hard gate above every volume override:
                // an effect the chosen tier did not compile cannot be switched on
                // from a volume, however high its priority. So the row asks the
                // asset rather than sitting there looking live and swallowing the
                // click - which is the dead-control failure this screen was
                // rebuilt to stop shipping.
                case GraphicsOptionKind.Reflections:
                    return director.ReflectionsSupported;

                case GraphicsOptionKind.GlobalIllumination:
                    return director.GlobalIlluminationSupported;

                case GraphicsOptionKind.VolumetricFog:
                    return director.VolumetricFogSupported;

                default:
                    return true;
            }
        }

        /// <summary>
        /// The upscalers this machine can actually run, always including Off.
        ///
        /// Unsupported ones are left out of the cycle rather than shown and
        /// refused: the director falls back to Off for anything it cannot run, so
        /// a selectable DLSS on an AMD card would snap back to Off the moment it
        /// was chosen and read as a broken control.
        /// </summary>
        private static List<UpscaleMethod> Methods(GraphicsDirector director)
        {
            List<UpscaleMethod> available = new List<UpscaleMethod> { UpscaleMethod.Off };

            if (director.FsrAvailable)
            {
                available.Add(UpscaleMethod.Fsr);
            }

            if (director.DlssAvailable)
            {
                available.Add(UpscaleMethod.Dlss);
            }

            return available;
        }

        /// <summary>
        /// How many rungs an effect row offers.
        ///
        /// The ray-traced half is left out entirely rather than shown and
        /// refused, for the same reason the upscaler list omits DLSS on an AMD
        /// card: the director clamps a stored RT rung back to screen space on
        /// hardware that cannot run it, so a selectable RT High would snap back
        /// the moment it was chosen and read as a broken control.
        /// </summary>
        private static int EffectRungs(GraphicsDirector director)
        {
            return director.RayTracingAvailable
                ? QualityLadder.EffectNames.Length
                : QualityLadder.ScreenSpaceCount;
        }

        private int Count(GraphicsDirector director)
        {
            switch (kind)
            {
                // Every level is steppable now. There is no Custom to exclude -
                // tuning a row overrides the tier through a volume rather than
                // moving the selection onto a level of its own.
                case GraphicsOptionKind.Quality:
                    return Mathf.Max(1, director.QualityNames.Length);

                case GraphicsOptionKind.Reflections:
                case GraphicsOptionKind.GlobalIllumination:
                    return EffectRungs(director);

                // No ray-traced form, so these stop at High.
                case GraphicsOptionKind.VolumetricFog:
                case GraphicsOptionKind.MotionBlur:
                    return QualityLadder.ScreenSpaceCount;

                case GraphicsOptionKind.Resolution: return director.Sizes.Count;
                case GraphicsOptionKind.ScreenMode: return ScreenModes.Length;
                case GraphicsOptionKind.FrameCap: return DisplayOptions.FrameRateCaps.Length;
                case GraphicsOptionKind.RenderScale: return RenderScales.Length;
                case GraphicsOptionKind.DynamicResolution: return DisplayOptions.DynamicTargets.Length;
                case GraphicsOptionKind.UpscaleMethod: return Methods(director).Count;
                case GraphicsOptionKind.UpscaleQuality: return DisplayOptions.UpscaleQualityNames.Length;

                // DLAA is the last entry and only exists where DLSS does.
                case GraphicsOptionKind.AntiAliasing:
                    return director.DlssAvailable
                        ? DisplayOptions.AntiAliasingNames.Length
                        : DisplayOptions.AntiAliasingNames.Length - 1;

                // VSync, the only two-way row left.
                default: return 2;
            }
        }

        private int Index(GraphicsDirector director)
        {
            switch (kind)
            {
                case GraphicsOptionKind.Quality: return director.QualityLevel;
                case GraphicsOptionKind.Resolution: return Mathf.Max(0, director.SizeIndex);
                case GraphicsOptionKind.ScreenMode: return IndexOfMode(director.ScreenMode);
                case GraphicsOptionKind.VSync: return director.VSync ? 1 : 0;
                case GraphicsOptionKind.FrameCap: return IndexOfCap(director.FrameCap);
                case GraphicsOptionKind.RenderScale: return IndexOfScale(director.RenderScale);
                case GraphicsOptionKind.DynamicResolution: return IndexOfDynamic(director.DynamicTarget);
                case GraphicsOptionKind.UpscaleMethod: return Mathf.Max(0, Methods(director).IndexOf(director.Method));
                case GraphicsOptionKind.UpscaleQuality: return (int)director.Quality;
                case GraphicsOptionKind.AntiAliasing: return (int)director.AntiAliasing;
                case GraphicsOptionKind.Reflections: return (int)director.Reflections;
                case GraphicsOptionKind.GlobalIllumination: return (int)director.GlobalIlluminationQuality;
                case GraphicsOptionKind.VolumetricFog: return (int)director.VolumetricFog;
                case GraphicsOptionKind.MotionBlur: return (int)director.MotionBlurQuality;
                default: return 0;
            }
        }

        private void Apply(GraphicsDirector director, int index)
        {
            switch (kind)
            {
                case GraphicsOptionKind.Quality: director.SetQuality(index); break;
                case GraphicsOptionKind.Resolution: director.SizeIndex = index; break;
                case GraphicsOptionKind.ScreenMode: director.ScreenMode = ScreenModes[index]; break;
                case GraphicsOptionKind.VSync: director.VSync = index == 1; break;
                case GraphicsOptionKind.FrameCap: director.FrameCap = DisplayOptions.FrameRateCaps[index]; break;
                case GraphicsOptionKind.RenderScale: director.RenderScale = RenderScales[index]; break;
                case GraphicsOptionKind.DynamicResolution: director.DynamicTarget = DisplayOptions.DynamicTargets[index]; break;
                case GraphicsOptionKind.UpscaleMethod: director.Method = Methods(director)[index]; break;
                case GraphicsOptionKind.UpscaleQuality: director.Quality = (UpscaleQuality)index; break;
                case GraphicsOptionKind.AntiAliasing: director.AntiAliasing = (AntiAliasingMode)index; break;
                case GraphicsOptionKind.Reflections: director.Reflections = (EffectQuality)index; break;
                case GraphicsOptionKind.GlobalIllumination: director.GlobalIlluminationQuality = (EffectQuality)index; break;
                case GraphicsOptionKind.VolumetricFog: director.VolumetricFog = (EffectQuality)index; break;
                case GraphicsOptionKind.MotionBlur: director.MotionBlurQuality = (EffectQuality)index; break;
            }
        }

        private string Describe(GraphicsDirector director)
        {
            switch (kind)
            {
                case GraphicsOptionKind.Quality:
                    string[] names = director.QualityNames;
                    int level = Mathf.Clamp(director.QualityLevel, 0, names.Length - 1);
                    return names.Length == 0 ? "-" : names[level];

                case GraphicsOptionKind.Resolution:
                    return director.Sizes.Count == 0
                        ? "-"
                        : director.Sizes[Mathf.Clamp(director.SizeIndex, 0, director.Sizes.Count - 1)].ToString();

                case GraphicsOptionKind.ScreenMode:
                    return ScreenModeNames[IndexOfMode(director.ScreenMode)];

                case GraphicsOptionKind.VSync:
                    return director.VSync ? "On" : "Off";

                case GraphicsOptionKind.FrameCap:
                    // The derived cap shows the number it resolved to, so it is
                    // not a promise the player has to take on trust.
                    return director.FrameCap == DisplayOptions.MatchDisplay && director.ResolvedFrameCap > 0
                        ? director.ResolvedFrameCap + " FPS"
                        : DisplayOptions.DescribeCap(director.FrameCap);

                case GraphicsOptionKind.RenderScale:
                    // Shows what the game is actually rendering at, which an
                    // upscaler decides rather than this control.
                    return Mathf.RoundToInt(director.EffectiveRenderScale * 100f) + "%";

                case GraphicsOptionKind.DynamicResolution:
                    return DisplayOptions.DescribeDynamicTarget(director.DynamicTarget);

                case GraphicsOptionKind.UpscaleMethod:
                    return DisplayOptions.UpscaleMethodNames[(int)director.Method];

                case GraphicsOptionKind.UpscaleQuality:
                    // Reads as a plain dash rather than a stale quality nobody
                    // asked for, when there is no upscaler to qualify.
                    return director.Method == UpscaleMethod.Off
                        ? "-"
                        : DisplayOptions.UpscaleQualityNames[(int)director.Quality];

                case GraphicsOptionKind.AntiAliasing:
                    return director.Method == UpscaleMethod.Off
                        ? DisplayOptions.AntiAliasingNames[(int)director.AntiAliasing]
                        : DisplayOptions.UpscaleMethodNames[(int)director.Method];

                case GraphicsOptionKind.Reflections:
                    return QualityLadder.Describe(director.Reflections);

                case GraphicsOptionKind.GlobalIllumination:
                    return QualityLadder.Describe(director.GlobalIlluminationQuality);

                case GraphicsOptionKind.VolumetricFog:
                    return QualityLadder.Describe(director.VolumetricFog);

                case GraphicsOptionKind.MotionBlur:
                    return QualityLadder.Describe(director.MotionBlurQuality);

                default: return "-";
            }
        }

        /// <summary>
        /// Says the things a settings screen normally leaves the player to
        /// discover: that a frame cap does nothing while VSync is on, and that
        /// ray tracing needs hardware this machine may not have.
        /// </summary>
        private string Note(GraphicsDirector director)
        {
            switch (kind)
            {
                // Why a row is dark. The tier's pipeline asset decides whether the
                // effect was compiled at all, and no volume override reaches past
                // that - so the honest thing is to name the tier as the reason
                // rather than let the row look broken.
                //
                // Checked before the ray-tracing note below, because a row whose
                // effect does not exist has a more basic problem than which rungs
                // of it are reachable.
                case GraphicsOptionKind.Reflections when !director.ReflectionsSupported:
                case GraphicsOptionKind.GlobalIllumination when !director.GlobalIlluminationSupported:
                case GraphicsOptionKind.VolumetricFog when !director.VolumetricFogSupported:
                    return "Not compiled into this quality tier";

                case GraphicsOptionKind.RenderScale when !director.DynamicResolutionSupported:
                case GraphicsOptionKind.DynamicResolution when !director.DynamicResolutionSupported:
                    return "Dynamic resolution is off in this quality tier";

                // Says why the ladder stops at High rather than leaving the
                // ray-traced rungs to be looked for and not found. Two things can
                // withhold them and the row cannot tell which, so it names both.
                case GraphicsOptionKind.Reflections when !director.RayTracingAvailable:
                case GraphicsOptionKind.GlobalIllumination when !director.RayTracingAvailable:
                    return "Ray traced levels need a DXR GPU and a tier that compiled them";

                case GraphicsOptionKind.FrameCap when director.VSync:
                    return "Ignored while VSync is on";

                case GraphicsOptionKind.FrameCap when director.FrameCap == DisplayOptions.MatchDisplay:
                    return "Stays clear of the refresh rate, where pacing gets rough";

                // Sitting exactly on the display's rate is the one cap that paces
                // badly, so the row says so rather than leaving it to be found.
                case GraphicsOptionKind.VSync when director.VSync:
                    return "If frames hitch, try Just Under Display instead";

                // Ordered most specific first, because more than one of these can
                // be true at once. Dynamic resolution moves the scale every frame
                // whatever else is running, so it answers before the upscaler.
                case GraphicsOptionKind.RenderScale when director.DynamicResolutionOn:
                    return "Set by dynamic resolution";

                case GraphicsOptionKind.RenderScale
                    when director.Method == UpscaleMethod.Off &&
                        director.AntiAliasing == AntiAliasingMode.Dlaa:
                    return "DLAA renders at native, so there is nothing to scale";

                // Custom is the mode where an upscaler is running and this row
                // still decides, so it gets a note about what the number means
                // rather than one about why it is dark.
                case GraphicsOptionKind.RenderScale
                    when director.Method != UpscaleMethod.Off && director.CustomScale:
                    return "What " + DisplayOptions.UpscaleMethodNames[(int)director.Method] +
                        " renders at before reconstructing to native";

                case GraphicsOptionKind.RenderScale when director.Method != UpscaleMethod.Off:
                    return "Set by the upscaler - pick Custom quality to set it here";

                // The two used to be mutually exclusive: each upscaler installed
                // its own scaler into a slot HDRP prefers, so the controller ran
                // and was discarded. They compose now, and this is the pairing
                // both vendors actually build for.
                case GraphicsOptionKind.DynamicResolution when director.DynamicFeedsUpscaler:
                    return "Moves the scale " +
                        DisplayOptions.UpscaleMethodNames[(int)director.Method] +
                        " reconstructs from";

                case GraphicsOptionKind.DynamicResolution when director.DynamicResolutionOn:
                    return "Drops resolution to hold the target, never below 50%";

                case GraphicsOptionKind.UpscaleMethod when !director.DlssAvailable:
                    return "DLSS needs an NVIDIA RTX card";

                case GraphicsOptionKind.UpscaleQuality when director.Method == UpscaleMethod.Off:
                    return "Choose an upscaler first";

                // Custom is not a mode either driver has, so it runs in whichever
                // preset sits nearest the scale asked for. Worth naming: it is what
                // decides how far the scale can travel before DLSS has to rebuild.
                case GraphicsOptionKind.UpscaleQuality when director.CustomScale:
                    return "Set by render scale, running as " +
                        DisplayOptions.UpscaleQualityNames[director.ResolvedPreset];

                // Every vendor preset renders below native and reconstructs back
                // up, so the render scale row goes quiet and this one has to say
                // what the trade actually is. The number is what is being asked
                // for rather than the published ratio, so a scale the driver
                // clamped reads as the value it was clamped to.
                case GraphicsOptionKind.UpscaleQuality:
                    return "Renders at " +
                        Mathf.RoundToInt(director.EffectiveRenderScale * 100f) + "%";

                case GraphicsOptionKind.AntiAliasing when director.Method != UpscaleMethod.Off:
                    return "Replaced by " + DisplayOptions.UpscaleMethodNames[(int)director.Method];

                case GraphicsOptionKind.AntiAliasing when director.AntiAliasing == AntiAliasingMode.Dlaa:
                    return "DLSS quality at full resolution; costs more than TAA";


                // Asked of QualitySettings rather than compared against index 0.
                // Two tiers now disable shadows rather than one, and a third
                // inserted below would have moved the answer again - the level
                // itself already records this, so read it instead of counting.
                case GraphicsOptionKind.Quality:
                    return QualitySettings.shadows == ShadowQuality.Disable
                        ? "Dynamic shadows are off at this level"
                        : string.Empty;

                default:
                    return string.Empty;
            }
        }

        private static int IndexOfMode(FullScreenMode mode)
        {
            for (int i = 0; i < ScreenModes.Length; i++)
            {
                if (ScreenModes[i] == mode)
                {
                    return i;
                }
            }

            return 1;
        }

        private static int IndexOfCap(int cap)
        {
            for (int i = 0; i < DisplayOptions.FrameRateCaps.Length; i++)
            {
                if (DisplayOptions.FrameRateCaps[i] == cap)
                {
                    return i;
                }
            }

            return 0;
        }

        private static int IndexOfDynamic(int target)
        {
            for (int i = 0; i < DisplayOptions.DynamicTargets.Length; i++)
            {
                if (DisplayOptions.DynamicTargets[i] == target)
                {
                    return i;
                }
            }

            // A target from a settings file this build no longer offers reads as
            // off rather than as whatever happens to sit at index 0 by accident.
            return 0;
        }

        /// <summary>
        /// The step matching a stored scale, falling back to native.
        ///
        /// A saved value can sit between steps - written by an older build, or by
        /// dynamic resolution having moved it - and native is the safe reading.
        /// </summary>
        private static int IndexOfScale(float scale)
        {
            for (int i = 0; i < RenderScales.Length; i++)
            {
                if (Mathf.Abs(RenderScales[i] - scale) < 0.01f)
                {
                    return i;
                }
            }

            return RenderScales.Length - 1;
        }
    }
}
