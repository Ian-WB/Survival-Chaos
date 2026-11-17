using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>The two sliders on the HUD options tab.</summary>
    public enum HudSliderKind
    {
        Scale,
        Opacity
    }

    /// <summary>
    /// HUD SCALE (70-130%) or HUD OPACITY (40-100%) on the HUD options tab
    /// (17 November 2026 redesign), in steps of 5%.
    ///
    /// The same shape as the volume and graphics sliders: it subscribes in
    /// code, writes through <see cref="HudSettings"/>, and reads back on
    /// <see cref="HudSettings.Changed"/>, so the HUD follows while the slider
    /// moves.
    /// </summary>
    [AddComponentMenu("Survival Chaos/HUD Option Control")]
    public sealed class HudOptionControl : MonoBehaviour
    {
        /// <summary>Percent per slider step.</summary>
        public const int StepPercent = 5;

        [SerializeField]
        private HudSliderKind kind = HudSliderKind.Scale;

        [SerializeField]
        [Tooltip("Slider driving the setting. Its range is set here, in whole steps.")]
        private Slider slider;

        [SerializeField]
        [Tooltip("Optional. Shows the value as a percentage.")]
        private TMP_Text readout;

        [SerializeField]
        [Tooltip("Optional. One line under the row.")]
        private TMP_Text note;

        public HudSliderKind Kind => kind;

        /// <summary>The lowest and highest percent this kind allows.</summary>
        public static void RangeOf(HudSliderKind kind, out int min, out int max)
        {
            if (kind == HudSliderKind.Scale)
            {
                min = HudSettings.MinScalePercent;
                max = HudSettings.MaxScalePercent;
            }
            else
            {
                min = HudSettings.MinOpacityPercent;
                max = HudSettings.MaxOpacityPercent;
            }
        }

        /// <summary>The slider's last position: the range in steps.</summary>
        public static int LastPosition(HudSliderKind kind)
        {
            RangeOf(kind, out int min, out int max);
            return (max - min) / StepPercent;
        }

        /// <summary>The percent a slider position stands for.</summary>
        public static int PercentAt(HudSliderKind kind, int position)
        {
            RangeOf(kind, out int min, out int max);
            return Mathf.Clamp(min + position * StepPercent, min, max);
        }

        /// <summary>The slider position nearest a percent.</summary>
        public static int PositionOf(HudSliderKind kind, int percent)
        {
            RangeOf(kind, out int min, out _);
            return Mathf.Clamp(Mathf.RoundToInt((percent - min) / (float)StepPercent), 0, LastPosition(kind));
        }

        private void OnEnable()
        {
            if (slider != null)
            {
                slider.wholeNumbers = true;
                slider.minValue = 0f;
                slider.maxValue = LastPosition(kind);
                slider.onValueChanged.AddListener(OnSlider);
            }

            HudSettings.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(OnSlider);
            }

            HudSettings.Changed -= Refresh;
        }

        private void OnSlider(float position)
        {
            int percent = PercentAt(kind, Mathf.RoundToInt(position));
            if (kind == HudSliderKind.Scale)
            {
                HudSettings.ScalePercent = percent;
            }
            else
            {
                HudSettings.OpacityPercent = percent;
            }

            // Writing raises Changed, which refreshes; this covers a value
            // that was already stored.
            Refresh();
        }

        private int Current => kind == HudSliderKind.Scale ? HudSettings.ScalePercent : HudSettings.OpacityPercent;

        private void Refresh()
        {
            int percent = Current;

            if (slider != null)
            {
                slider.SetValueWithoutNotify(PositionOf(kind, percent));
            }

            if (readout != null)
            {
                readout.text = percent + "%";
            }

            if (note != null)
            {
                note.text = string.Empty;
                note.gameObject.SetActive(false);
            }
        }
    }
}
