using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// The frame cap slider, bound to <see cref="GraphicsDirector"/>.
    ///
    /// The same shape as <see cref="SharpnessControl"/>: it subscribes in code,
    /// and reads back from the director as well as writing to it, so the pause
    /// menu and the title screen cannot disagree about where the slider sits.
    ///
    /// It was a cycler over nine fixed rates until 16 November 2026. A cap has
    /// no performance cliff to step around - 73 is as good a cap as 72 or 75,
    /// and the right one depends on the display and the machine - so every
    /// whole number from 30 to 300 is a real choice. The two that are not
    /// numbers keep an end each: Just under display on the left, uncapped on
    /// the right (<see cref="DisplayOptions.CapToSlider"/>).
    /// </summary>
    [AddComponentMenu("Survival Chaos/Frame Cap Control")]
    public sealed class FrameCapControl : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Slider driving the cap. Its range is set here, in whole numbers.")]
        private Slider slider;

        [SerializeField]
        [Tooltip("Optional. Shows the cap.")]
        private TMP_Text readout;

        [SerializeField]
        [Tooltip("Optional. Says what is holding the cap when that is worth knowing.")]
        private TMP_Text note;

        private void OnEnable()
        {
            if (slider != null)
            {
                slider.wholeNumbers = true;
                slider.minValue = DisplayOptions.CapSliderFirst;
                slider.maxValue = DisplayOptions.CapSliderLast;
                slider.onValueChanged.AddListener(Apply);
            }

            GraphicsDirector.SettingsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(Apply);
            }

            GraphicsDirector.SettingsChanged -= Refresh;
        }

        private void Apply(float position)
        {
            GraphicsDirector director = GraphicsDirector.Instance;
            if (director == null)
            {
                return;
            }

            int cap = DisplayOptions.SliderToCap(Mathf.RoundToInt(position));

            // Setting the cap raises SettingsChanged, which refreshes the labels.
            if (cap != director.FrameCap)
            {
                director.FrameCap = cap;
            }
        }

        /// <summary>
        /// Pulls the stored value back onto the slider. Set without notifying, or
        /// writing the value would raise the change that wrote it.
        /// </summary>
        private void Refresh()
        {
            GraphicsDirector director = GraphicsDirector.Instance;
            if (director == null)
            {
                return;
            }

            if (slider != null)
            {
                slider.SetValueWithoutNotify(DisplayOptions.CapToSlider(director.FrameCap));
            }

            if (readout != null)
            {
                readout.text = DisplayOptions.DescribeCap(director.FrameCap);
            }

            if (note != null)
            {
                note.text = Note(director);
                note.gameObject.SetActive(note.text.Length > 0);
            }
        }

        private static string Note(GraphicsDirector director)
        {
            return DisplayOptions.DescribeCapHolder(director.FrameCap, director.ResolvedFrameCap,
                director.CapSyncInterval);
        }
    }
}
