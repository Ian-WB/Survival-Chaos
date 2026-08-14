using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
        AmbientOcclusion = 8,
        VolumetricFog = 9,
        MotionBlur = 10,
        UpscaleMethod = 12,
        UpscaleQuality = 13,
        AntiAliasing = 14,
        DynamicResolution = 16,
        GlobalIllumination = 17,
        ShadowQuality = 18,
        RayTracedShadows = 19,
        TextureQuality = 20,
        Anisotropic = 21

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

        /// <summary>
        /// In AnisotropicFiltering order: Disable, Enable, ForceEnable.
        ///
        /// "Per-texture" rather than "On" because that is what Enable means - each
        /// texture's own import setting decides, and plenty of them ask for none.
        /// </summary>
        private static readonly string[] AnisotropicNames =
        {
            "Off", "Per-texture", "Forced"
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
        }

        /// <summary>False when the machine cannot offer the setting at all.</summary>
        private bool Available(GraphicsDirector director)
        {
            switch (kind)
            {
                // A plain toggle with nothing to toggle. The three effect ladders
                // stay available because their screen-space half still works;
                // this one is ray traced or nothing.
                case GraphicsOptionKind.RayTracedShadows:
                    return director.RayTracingAvailable;

                // Inert while anything else is driving the resolution - an
                // upscaler, or dynamic resolution moving it every frame.
                case GraphicsOptionKind.RenderScale:
                    return director.Method == UpscaleMethod.Off && !director.DynamicResolutionOn;

                // Nothing to set the quality of until an upscaler is chosen.
                case GraphicsOptionKind.UpscaleQuality:
                    return director.Method != UpscaleMethod.Off;

                // An upscaler replaces anti-aliasing rather than running beside it.
                case GraphicsOptionKind.AntiAliasing:
                    return director.Method == UpscaleMethod.Off;


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
                // Custom is a place the row can be, not one it can be stepped to -
                // there is nothing to load, since it means whatever is currently
                // set. It joins the cycle only while the player is standing on it,
                // so stepping off leads back into the presets.
                case GraphicsOptionKind.Quality:
                    return director.IsCustom
                        ? director.QualityNames.Length
                        : Mathf.Max(1, director.QualityNames.Length - 1);

                case GraphicsOptionKind.ShadowQuality: return QualityLadder.ShadowNames.Length;

                case GraphicsOptionKind.AmbientOcclusion:
                case GraphicsOptionKind.Reflections:
                case GraphicsOptionKind.GlobalIllumination:
                    return EffectRungs(director);

                // No ray-traced form, so these stop at High.
                case GraphicsOptionKind.VolumetricFog:
                case GraphicsOptionKind.MotionBlur:
                    return QualityLadder.ScreenSpaceCount;

                case GraphicsOptionKind.Anisotropic: return 3;
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

                // Ray Traced Shadows and Texture Quality are both two-way.
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
                case GraphicsOptionKind.ShadowQuality: return (int)director.Shadows;
                case GraphicsOptionKind.RayTracedShadows: return director.RayTracedShadows ? 1 : 0;
                case GraphicsOptionKind.Reflections: return (int)director.Reflections;
                case GraphicsOptionKind.AmbientOcclusion: return (int)director.AmbientOcclusion;
                case GraphicsOptionKind.GlobalIllumination: return (int)director.GlobalIlluminationQuality;
                case GraphicsOptionKind.VolumetricFog: return (int)director.VolumetricFog;
                case GraphicsOptionKind.MotionBlur: return (int)director.MotionBlurQuality;

                // Full resolution reads as the right-hand end of the row, so the
                // index is inverted against the mipmap limit it stores.
                case GraphicsOptionKind.TextureQuality: return director.TextureMipLimit == 0 ? 1 : 0;
                case GraphicsOptionKind.Anisotropic: return (int)director.Anisotropic;
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
                case GraphicsOptionKind.ShadowQuality: director.Shadows = (ShadowQualityLevel)index; break;
                case GraphicsOptionKind.RayTracedShadows: director.RayTracedShadows = index == 1; break;
                case GraphicsOptionKind.Reflections: director.Reflections = (EffectQuality)index; break;
                case GraphicsOptionKind.AmbientOcclusion: director.AmbientOcclusion = (EffectQuality)index; break;
                case GraphicsOptionKind.GlobalIllumination: director.GlobalIlluminationQuality = (EffectQuality)index; break;
                case GraphicsOptionKind.VolumetricFog: director.VolumetricFog = (EffectQuality)index; break;
                case GraphicsOptionKind.MotionBlur: director.MotionBlurQuality = (EffectQuality)index; break;
                case GraphicsOptionKind.TextureQuality: director.TextureMipLimit = index == 1 ? 0 : 1; break;
                case GraphicsOptionKind.Anisotropic: director.Anisotropic = (AnisotropicFiltering)index; break;
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

                case GraphicsOptionKind.ShadowQuality:
                    return QualityLadder.Describe(director.Shadows);

                case GraphicsOptionKind.RayTracedShadows:
                    return director.RayTracedShadows ? "On" : "Off";

                case GraphicsOptionKind.Reflections:
                    return QualityLadder.Describe(director.Reflections);

                case GraphicsOptionKind.AmbientOcclusion:
                    return QualityLadder.Describe(director.AmbientOcclusion);

                case GraphicsOptionKind.GlobalIllumination:
                    return QualityLadder.Describe(director.GlobalIlluminationQuality);

                case GraphicsOptionKind.VolumetricFog:
                    return QualityLadder.Describe(director.VolumetricFog);

                case GraphicsOptionKind.MotionBlur:
                    return QualityLadder.Describe(director.MotionBlurQuality);

                case GraphicsOptionKind.TextureQuality:
                    return director.TextureMipLimit == 0 ? "Full" : "Half";

                case GraphicsOptionKind.Anisotropic:
                    return AnisotropicNames[Mathf.Clamp((int)director.Anisotropic, 0, AnisotropicNames.Length - 1)];

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
                case GraphicsOptionKind.RayTracedShadows when !director.RayTracingAvailable:
                    return "This GPU has no ray tracing support";

                // Says why the ladder stops at High rather than leaving the
                // ray-traced rungs to be looked for and not found.
                case GraphicsOptionKind.AmbientOcclusion when !director.RayTracingAvailable:
                case GraphicsOptionKind.Reflections when !director.RayTracingAvailable:
                case GraphicsOptionKind.GlobalIllumination when !director.RayTracingAvailable:
                    return "Ray traced levels need a DXR GPU";

                // Shadowmask is baked, so the arena keeps its shadows here. Worth
                // saying, because "Off" on a shadow row reads as no shadows at all.
                case GraphicsOptionKind.ShadowQuality when director.Shadows == ShadowQualityLevel.Off:
                    return "No dynamic shadows, and no baked shadowmask either";

                case GraphicsOptionKind.ShadowQuality when director.Shadows == ShadowQualityLevel.Low:
                    return "Baked shadowmask only; moving objects cast none";

                case GraphicsOptionKind.Quality when director.IsCustom:
                    return "Pick a preset to reset every row below";

                case GraphicsOptionKind.FrameCap when director.VSync:
                    return "Ignored while VSync is on";

                case GraphicsOptionKind.FrameCap when director.FrameCap == DisplayOptions.MatchDisplay:
                    return "Stays clear of the refresh rate, where pacing gets rough";

                // Sitting exactly on the display's rate is the one cap that paces
                // badly, so the row says so rather than leaving it to be found.
                case GraphicsOptionKind.VSync when director.VSync:
                    return "If frames hitch, try Just Under Display instead";

                // An upscaler owns the render scale, so this control is inert
                // while one is selected. Saying so beats letting someone change a
                // number that has no effect.
                case GraphicsOptionKind.RenderScale when director.Method != UpscaleMethod.Off:
                    return "Set by the upscaler";

                case GraphicsOptionKind.RenderScale when director.DynamicResolutionOn:
                    return "Set by dynamic resolution";

                // The upscalers install their own scaler and HDRP prefers it, so
                // this would run and be discarded. Say so rather than appear to work.
                case GraphicsOptionKind.DynamicResolution when director.DynamicOverriddenByUpscaler:
                    return "Overridden while an upscaler is on";

                case GraphicsOptionKind.DynamicResolution when director.DynamicResolutionOn:
                    return "Drops resolution to hold the target, never below 50%";

                case GraphicsOptionKind.UpscaleMethod when !director.DlssAvailable:
                    return "DLSS needs an NVIDIA RTX card";

                case GraphicsOptionKind.UpscaleQuality when director.Method == UpscaleMethod.Off:
                    return "Choose an upscaler first";

                // Every quality mode of every upscaler renders below native and
                // reconstructs back up, so the render scale row goes quiet and
                // this one has to say what the trade actually is.
                case GraphicsOptionKind.UpscaleQuality:
                    return "Renders at about " +
                        Mathf.RoundToInt(DisplayOptions.ApproximateScale((int)director.Quality) * 100f) + "%";

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
