using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// A whole-number graphics slider bound to <see cref="GraphicsDirector"/>:
    /// frame cap, dynamic resolution and render scale.
    ///
    /// The same shape as <see cref="SharpnessControl"/>: it subscribes in code,
    /// and reads back from the director as well as writing to it, so the pause
    /// menu and the title screen cannot disagree about where a slider sits.
    ///
    /// It also does what the cycler rows did for an inert setting: dims the
    /// label and readout and stops the bar taking input, while the note line
    /// says why. A slider that moves and changes nothing is the dead control
    /// this screen was rebuilt to stop shipping.
    /// </summary>
    public abstract class GraphicsSliderControl : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Slider driving the setting. Its range is set here, in whole numbers.")]
        private Slider slider;

        [SerializeField]
        [Tooltip("Optional. Shows the value.")]
        private TMP_Text readout;

        [SerializeField]
        [Tooltip("Optional. Says what is holding or overriding the setting, when that is worth knowing.")]
        private TMP_Text note;

        [SerializeField]
        [Tooltip("Optional. The row's caption, dimmed with the readout while the setting is inert.")]
        private TMP_Text label;

        /// <summary>How much of its colour a row keeps once it has nothing to offer. The cyclers' value.</summary>
        private const float DimmedAlpha = 0.3f;

        /// <summary>How often a row whose value moves on its own re-reads it.</summary>
        private const float LiveRefreshInterval = 0.25f;

        private Color labelColour;
        private Color readoutColour;
        private bool capturedColours;
        private float nextLiveRefresh;

        /// <summary>The slider's leftmost and rightmost positions.</summary>
        protected abstract int First { get; }

        protected abstract int Last { get; }

        /// <summary>Where the stored setting sits on the slider.</summary>
        protected abstract int Position(GraphicsDirector director);

        /// <summary>Stores the setting a slider position stands for.</summary>
        protected abstract void Apply(GraphicsDirector director, int position);

        protected abstract string Describe(GraphicsDirector director);

        protected virtual string Note(GraphicsDirector director) => string.Empty;

        /// <summary>False when the slider can change nothing right now.</summary>
        protected virtual bool Available(GraphicsDirector director) => true;

        /// <summary>True while the value moves without the player, so the row re-reads it four times a second.</summary>
        protected virtual bool Live(GraphicsDirector director) => false;

        private void OnEnable()
        {
            if (slider != null)
            {
                slider.wholeNumbers = true;
                slider.minValue = First;
                slider.maxValue = Last;
                slider.onValueChanged.AddListener(OnSlider);
            }

            GraphicsDirector.SettingsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(OnSlider);
            }

            GraphicsDirector.SettingsChanged -= Refresh;
        }

        private void Update()
        {
            GraphicsDirector director = GraphicsDirector.Instance;
            if (director == null || !Live(director) || Time.unscaledTime < nextLiveRefresh)
            {
                return;
            }

            nextLiveRefresh = Time.unscaledTime + LiveRefreshInterval;
            Refresh();
        }

        private void OnSlider(float position)
        {
            GraphicsDirector director = GraphicsDirector.Instance;
            if (director == null)
            {
                return;
            }

            if (!Available(director))
            {
                // A drag that started before the setting went inert: put it back.
                Refresh();
                return;
            }

            // Storing raises SettingsChanged, which refreshes the labels.
            Apply(director, Mathf.RoundToInt(position));
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

            bool live = Available(director);

            if (slider != null)
            {
                slider.SetValueWithoutNotify(Position(director));
                slider.interactable = live;
            }

            if (readout != null)
            {
                readout.text = Describe(director);
            }

            if (note != null)
            {
                note.text = Note(director);
                note.gameObject.SetActive(note.text.Length > 0);
            }

            ApplyEnabledLook(live);
        }

        private void ApplyEnabledLook(bool live)
        {
            if (!capturedColours)
            {
                labelColour = label != null ? label.color : Color.white;
                readoutColour = readout != null ? readout.color : Color.white;
                capturedColours = true;
            }

            if (label != null)
            {
                label.color = live ? labelColour : Dim(labelColour);
            }

            if (readout != null)
            {
                readout.color = live ? readoutColour : Dim(readoutColour);
            }
        }

        private static Color Dim(Color colour)
        {
            return new Color(colour.r, colour.g, colour.b, colour.a * DimmedAlpha);
        }
    }
}
