using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// One volume slider, bound to one channel.
    ///
    /// The original options screen had a slider with no listeners at all, so
    /// moving it did nothing. This subscribes in code rather than through an
    /// inspector event, where a connection can be lost silently and look fine.
    ///
    /// It reads back from <see cref="AudioDirector"/> as well as writing to it,
    /// so two screens showing the same channel — the pause menu and the title
    /// screen both have one — cannot disagree about its level.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Volume Control")]
    public sealed class VolumeControl : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Which channel this slider adjusts.")]
        private AudioChannel channel = AudioChannel.Master;

        [SerializeField]
        [Tooltip("Slider driving the level. Its 0..1 travel is the channel's full range.")]
        private Slider slider;

        [SerializeField]
        [Tooltip("Optional. Shows the level as a percentage next to the slider.")]
        private TMP_Text readout;

        private void OnEnable()
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.onValueChanged.AddListener(Apply);
            }

            AudioDirector.LevelsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(Apply);
            }

            AudioDirector.LevelsChanged -= Refresh;
        }

        private void Apply(float value)
        {
            if (AudioDirector.Instance != null)
            {
                AudioDirector.Instance.SetLevel(channel, value);

                // Master and Effects make no sound of their own in a menu, so
                // the gun answers each step at the new level. Music is already
                // playing, and the Interface channel has the menu's own clicks.
                if (PreviewsOnChange(channel) && GameSounds.Instance != null)
                {
                    AudioDirector.Preview(GameSounds.Instance.PlayerShot);
                }
            }

            UpdateReadout(value);
        }

        /// <summary>
        /// Pulls the current level back onto the slider. Set without notifying,
        /// or writing the value would raise the change that wrote it.
        /// </summary>
        /// <summary>Whether moving this channel's slider plays a sample.</summary>
        public static bool PreviewsOnChange(AudioChannel channel)
        {
            return channel == AudioChannel.Master || channel == AudioChannel.Sfx;
        }

        private void Refresh()
        {
            if (AudioDirector.Instance == null)
            {
                return;
            }

            float level = AudioDirector.Instance.GetLevel(channel);

            if (slider != null)
            {
                slider.SetValueWithoutNotify(level);
            }

            UpdateReadout(level);
        }

        private void UpdateReadout(float value)
        {
            if (readout != null)
            {
                readout.text = Mathf.RoundToInt(value * 100f) + "%";
            }
        }
    }
}
