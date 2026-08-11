using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// The sharpness slider, bound to <see cref="GraphicsDirector"/>.
    ///
    /// The same shape as <see cref="VolumeControl"/>, and for the same reasons:
    /// it subscribes in code rather than through an inspector event, where a
    /// connection can be lost silently and still look wired; and it reads back
    /// from the director as well as writing to it, so the pause menu and the
    /// title screen cannot disagree about where the slider sits.
    ///
    /// This is the only graphics setting that is a slider rather than a cycler.
    /// The rest step because they have performance cliffs - render scale in tens,
    /// because 73% is not a choice anybody means to make. Sharpening costs the
    /// same at every value, so there is no cliff to step around and every position
    /// is a real choice.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Sharpness Control")]
    public sealed class SharpnessControl : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Slider driving the amount. Its 0..1 travel is the full range.")]
        private Slider slider;

        [SerializeField]
        [Tooltip("Optional. Shows the amount, naming the level when it sits on one.")]
        private TMP_Text readout;

        [SerializeField]
        [Tooltip("Optional. Says when nothing on screen is being sharpened.")]
        private TMP_Text note;

        private void OnEnable()
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
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

        private void Apply(float value)
        {
            if (GraphicsDirector.Instance != null)
            {
                GraphicsDirector.Instance.Sharpness = value;
            }

            UpdateLabels(value);
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

            float amount = director.Sharpness;

            if (slider != null)
            {
                slider.SetValueWithoutNotify(amount);
            }

            UpdateLabels(amount);
        }

        private void UpdateLabels(float amount)
        {
            if (readout != null)
            {
                readout.text = DisplayOptions.DescribeSharpness(amount);
            }

            if (note == null)
            {
                return;
            }

            // The same admission the cycler rows make: only TAA and FSR sharpen,
            // so with FXAA, SMAA or nothing selected this slider moves and nothing
            // on screen changes. Saying so beats letting it be discovered.
            GraphicsDirector director = GraphicsDirector.Instance;
            bool applies = director == null || director.SharpeningApplies;

            note.text = applies ? string.Empty : "Only TAA and FSR sharpen";
            note.gameObject.SetActive(note.text.Length > 0);
        }
    }
}
