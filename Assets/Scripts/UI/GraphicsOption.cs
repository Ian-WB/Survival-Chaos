using TMPro;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>Which setting a row drives.</summary>
    public enum GraphicsOptionKind
    {
        Quality = 0,
        Resolution = 1,
        ScreenMode = 2,
        VSync = 3,
        FrameCap = 4,
        RenderScale = 5,
        Lighting = 6,
        Reflections = 7,
        AmbientOcclusion = 8,
        VolumetricFog = 9,
        MotionBlur = 10,
        Upscaling = 11
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

        /// <summary>Anti-aliasing first, then upscalers by how hard they push.</summary>
        private static readonly string[] UpscalingNames =
        {
            "Off", "SMAA", "TAA",
            "FSR Quality", "FSR Balanced", "FSR Performance",
            "DLSS Quality", "DLSS Balanced", "DLSS Performance"
        };

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
                case GraphicsOptionKind.Lighting:
                    return director.RayTracingAvailable;

                // Inert while an upscaler is driving the resolution.
                case GraphicsOptionKind.RenderScale:
                    return !GraphicsDirector.IsUpscaler(director.Upscaling);

                default:
                    return true;
            }
        }

        private int Count(GraphicsDirector director)
        {
            switch (kind)
            {
                case GraphicsOptionKind.Quality: return director.QualityNames.Length;
                case GraphicsOptionKind.Resolution: return director.Sizes.Count;
                case GraphicsOptionKind.ScreenMode: return ScreenModes.Length;
                case GraphicsOptionKind.FrameCap: return DisplayOptions.FrameRateCaps.Length;
                case GraphicsOptionKind.RenderScale: return RenderScales.Length;
                case GraphicsOptionKind.Upscaling: return director.DlssAvailable ? UpscalingNames.Length : 6;
                case GraphicsOptionKind.Lighting: return 2;
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
                case GraphicsOptionKind.Upscaling: return (int)director.Upscaling;
                case GraphicsOptionKind.Lighting: return (int)director.Lighting;
                case GraphicsOptionKind.Reflections: return director.Reflections ? 1 : 0;
                case GraphicsOptionKind.AmbientOcclusion: return director.AmbientOcclusion ? 1 : 0;
                case GraphicsOptionKind.VolumetricFog: return director.VolumetricFog ? 1 : 0;
                case GraphicsOptionKind.MotionBlur: return director.MotionBlurEnabled ? 1 : 0;
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
                case GraphicsOptionKind.Upscaling: director.Upscaling = (UpscalingMode)index; break;
                case GraphicsOptionKind.Lighting: director.Lighting = (LightingMode)index; break;
                case GraphicsOptionKind.Reflections: director.Reflections = index == 1; break;
                case GraphicsOptionKind.AmbientOcclusion: director.AmbientOcclusion = index == 1; break;
                case GraphicsOptionKind.VolumetricFog: director.VolumetricFog = index == 1; break;
                case GraphicsOptionKind.MotionBlur: director.MotionBlurEnabled = index == 1; break;
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
                    return DisplayOptions.DescribeCap(director.FrameCap);

                case GraphicsOptionKind.RenderScale:
                    // Shows what the game is actually rendering at, which an
                    // upscaler decides rather than this control.
                    return Mathf.RoundToInt(director.EffectiveRenderScale * 100f) + "%";

                case GraphicsOptionKind.Upscaling:
                    return UpscalingNames[Mathf.Clamp((int)director.Upscaling, 0, UpscalingNames.Length - 1)];

                case GraphicsOptionKind.Lighting:
                    if (!director.RayTracingAvailable)
                    {
                        return "Baked";
                    }

                    return director.Lighting == LightingMode.RayTraced ? "Ray traced" : "Baked";

                case GraphicsOptionKind.Reflections: return director.Reflections ? "On" : "Off";
                case GraphicsOptionKind.AmbientOcclusion: return director.AmbientOcclusion ? "On" : "Off";
                case GraphicsOptionKind.VolumetricFog: return director.VolumetricFog ? "On" : "Off";
                case GraphicsOptionKind.MotionBlur: return director.MotionBlurEnabled ? "On" : "Off";
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
                case GraphicsOptionKind.Lighting when !director.RayTracingAvailable:
                    return "This GPU has no ray tracing support";

                case GraphicsOptionKind.FrameCap when director.VSync:
                    return "Ignored while VSync is on";

                // An upscaler owns the render scale, so this control is inert
                // while one is selected. Saying so beats letting someone change a
                // number that has no effect.
                case GraphicsOptionKind.RenderScale when GraphicsDirector.IsUpscaler(director.Upscaling):
                    return "Set by the upscaler";

                case GraphicsOptionKind.Upscaling when !director.DlssAvailable:
                    return "DLSS needs an NVIDIA RTX card";

                case GraphicsOptionKind.Upscaling when GraphicsDirector.IsUpscaler(director.Upscaling):
                    return "Replaces anti-aliasing";

                case GraphicsOptionKind.Quality:
                    return director.QualityLevel == 0 ? "Dynamic shadows are off at this level" : string.Empty;

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

        private static int IndexOfScale(float scale)
        {
            int best = RenderScales.Length - 1;
            for (int i = 0; i < RenderScales.Length; i++)
            {
                if (Mathf.Abs(RenderScales[i] - scale) < 0.01f)
                {
                    return i;
                }
            }

            return best;
        }
    }
}
