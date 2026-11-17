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
        Reflections = 7,
        VolumetricFog = 9,
        MotionBlur = 10,
        UpscaleMethod = 12,
        UpscaleQuality = 13,
        AntiAliasing = 14,
        GlobalIllumination = 17,

        // A new number rather than 18 or the old Shadow Quality row's key: this
        // one sets the scene's own casters, not the pipeline asset, and a value
        // saved by the row that used to live here must not be read as a rung.
        Shadows = 22,

        // A new number rather than 11, the old on/off Volumetric Clouds row
        // described below: this one sets how many steps the clouds take, and a
        // row still serialised as 11 must not come back as it.
        VolumetricClouds = 23,

        // 15 was Sharpness, before it became a slider rather than a cycler. The
        // number stays retired rather than reused: a row serialised as 15 by an
        // older build would otherwise silently become whatever took its place.
        //
        // 4 was Frame Cap, a cycler over nine fixed rates until 16 November
        // 2026. It is a slider now (FrameCapControl), any whole number from 30
        // to 300, and 4 is retired for the same reason.
        //
        // 5 was Render Scale and 16 Dynamic Resolution, cyclers until 17
        // November 2026 and sliders since (RenderScaleControl,
        // DynamicResolutionControl). Both numbers stay retired.
        //
        // 6 was Lighting, a two-way Baked/Ray traced switch whose only job was
        // toggling global illumination. GlobalIllumination replaced it with a
        // ladder that carries the same choice plus how much to spend on it, so 6
        // is retired for the same reason.
        //
        // 11 was Volumetric Clouds, when there was no sky to cloud in an arena
        // that sat inside a cubemap, so the row went and the tier was off. The
        // clouds came back on 24 September 2026 with the arena lifted into them,
        // and the row on 26 September as 23.
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
    /// Every setting here is a cycler, including the on/off ones. A toggle and a
    /// two-item cycler do the same job, and having one control type means one
    /// place to fix, one visual language, and no argument about which settings
    /// deserve which widget. It suits settings with performance cliffs and
    /// named rungs; the ones that are a number - frame cap, dynamic
    /// resolution, render scale, sharpness - are sliders instead
    /// (<see cref="GraphicsSliderControl"/>).
    ///
    /// The row reads its value back from <see cref="GraphicsDirector"/> rather
    /// than remembering one, so two screens showing the same setting cannot
    /// disagree.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Graphics Option")]
    public sealed class GraphicsOption : SteppedSetting
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

        [SerializeField]
        private GraphicsOptionKind kind = GraphicsOptionKind.Quality;

        /// <summary>Which setting this row steps. Read by the Checked build's bench.</summary>
        public GraphicsOptionKind Kind => kind;

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

        /// <summary>
        /// One step either way, from the row's arrows or from left and right on
        /// the row itself. False when nothing changed: the row is inert, or
        /// Shadows is already at the end it was pushed towards.
        /// </summary>
        public override bool Step(int direction)
        {
            GraphicsDirector director = GraphicsDirector.Instance;
            if (director == null || !Available(director))
            {
                return false;
            }

            int count = Count(director);
            if (count <= 1)
            {
                return false;
            }

            // Wraps rather than clamping: with three or four entries, running
            // into an invisible end is more annoying than looping.
            //
            // Shadows is the exception. Its Off rung is not the quiet one the
            // others have - it takes the sun, the lava light and the clouds out
            // together, and the island loses the contrast that reads as depth -
            // and every tier defaults at or one below High, which wrapping puts
            // a single press away from it. Three presses is the right price for
            // a rung a player has to mean.
            int current = Index(director);
            int raw = current + direction;
            int next = kind == GraphicsOptionKind.Shadows
                ? Mathf.Clamp(raw, 0, count - 1)
                : (raw + count) % count;

            if (next == current)
            {
                return false;
            }

            Apply(director, next);
            return true;
        }

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

                case GraphicsOptionKind.VolumetricClouds:
                    return director.VolumetricCloudsSupported;

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

            // Before DLSS in the list, though it is stored as 3.
            if (director.Fsr3Available)
            {
                available.Add(UpscaleMethod.Fsr3);
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

                // No RT Medium: see QualityLadder.ReflectionRungs.
                case GraphicsOptionKind.Reflections:
                    return director.RayTracingAvailable
                        ? QualityLadder.ReflectionRungs.Length
                        : QualityLadder.ScreenSpaceCount;

                case GraphicsOptionKind.GlobalIllumination:
                    return EffectRungs(director);

                // No ray-traced form, so these stop at High.
                case GraphicsOptionKind.VolumetricFog:
                case GraphicsOptionKind.MotionBlur:
                case GraphicsOptionKind.Shadows:
                    return QualityLadder.ScreenSpaceCount;

                // No Off either: Low, Medium and High (see CloudLadder).
                case GraphicsOptionKind.VolumetricClouds:
                    return CloudLadder.Count;

                case GraphicsOptionKind.Resolution: return director.Sizes.Count;
                case GraphicsOptionKind.ScreenMode: return ScreenModes.Length;
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
                case GraphicsOptionKind.UpscaleMethod: return Mathf.Max(0, Methods(director).IndexOf(director.Method));
                case GraphicsOptionKind.UpscaleQuality: return (int)director.Quality;
                case GraphicsOptionKind.AntiAliasing: return (int)director.AntiAliasing;
                case GraphicsOptionKind.Reflections: return Mathf.Max(0, System.Array.IndexOf(QualityLadder.ReflectionRungs, director.Reflections));
                case GraphicsOptionKind.GlobalIllumination: return (int)director.GlobalIlluminationQuality;
                case GraphicsOptionKind.VolumetricFog: return (int)director.VolumetricFog;
                case GraphicsOptionKind.MotionBlur: return (int)director.MotionBlurQuality;
                case GraphicsOptionKind.Shadows: return (int)director.Shadows;
                case GraphicsOptionKind.VolumetricClouds: return CloudLadder.IndexOf(director.CloudQuality);
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
                case GraphicsOptionKind.UpscaleMethod: director.Method = Methods(director)[index]; break;
                case GraphicsOptionKind.UpscaleQuality: director.Quality = (UpscaleQuality)index; break;
                case GraphicsOptionKind.AntiAliasing: director.AntiAliasing = (AntiAliasingMode)index; break;
                case GraphicsOptionKind.Reflections: director.Reflections = QualityLadder.ReflectionRungs[index]; break;
                case GraphicsOptionKind.GlobalIllumination: director.GlobalIlluminationQuality = (EffectQuality)index; break;
                case GraphicsOptionKind.VolumetricFog: director.VolumetricFog = (EffectQuality)index; break;
                case GraphicsOptionKind.MotionBlur: director.MotionBlurQuality = (EffectQuality)index; break;
                case GraphicsOptionKind.Shadows: director.Shadows = (EffectQuality)index; break;
                case GraphicsOptionKind.VolumetricClouds: director.CloudQuality = CloudLadder.At(index); break;
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

                case GraphicsOptionKind.Shadows:
                    return QualityLadder.Describe(director.Shadows);

                case GraphicsOptionKind.VolumetricClouds:
                    return QualityLadder.Describe(director.CloudQuality);

                default: return "-";
            }
        }

        /// <summary>
        /// Says the things a settings screen normally leaves the player to
        /// discover: that VSync takes over a cap it can hold by itself, and that
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
                case GraphicsOptionKind.VolumetricClouds when !director.VolumetricCloudsSupported:
                    return "Not compiled into this quality tier";

                // Says why the ladder stops at High rather than leaving the
                // ray-traced rungs to be looked for and not found. Two things can
                // withhold them and the row cannot tell which, so it names both.
                case GraphicsOptionKind.Reflections when !director.RayTracingAvailable:
                case GraphicsOptionKind.GlobalIllumination when !director.RayTracingAvailable:
                    return "Ray traced levels need a DXR GPU and a tier that compiled them";

                // Off is the one rung that changes what the island looks like
                // rather than how sharp it is, so it says what went.
                case GraphicsOptionKind.Shadows when director.Shadows == EffectQuality.Off:
                    return "The sun, the lava, the clouds and the rounds cast no shadows";

                // Sitting exactly on the display's rate is the one cap that paces
                // badly, so the row says so rather than leaving it to be found.
                case GraphicsOptionKind.VSync
                    when director.VSync && director.FrameCap != DisplayOptions.MatchDisplay:
                    return "If frames hitch, try Just Under Display instead";

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
    }
}
