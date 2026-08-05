using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Drives the game's output volume from a slider, and remembers the setting.
    ///
    /// The old options screen had a volume slider with nothing attached to it -
    /// no listeners at all - so moving it did nothing. This connects it, and
    /// persists the value, which the old one would not have done either.
    ///
    /// Subscribes in code rather than through an inspector event, so the
    /// connection cannot be silently lost again.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Volume Control")]
    public sealed class VolumeControl : MonoBehaviour
    {
        private const string SavedKey = "SurvivalChaos.Volume";

        [SerializeField]
        [Tooltip("Slider driving the volume. Its 0..1 range maps straight to output level.")]
        private Slider slider;

        private void Awake()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(SavedKey, 1f);

            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.SetValueWithoutNotify(AudioListener.volume);
            }
        }

        private void OnEnable()
        {
            if (slider != null)
            {
                slider.onValueChanged.AddListener(SetVolume);
            }
        }

        private void OnDisable()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(SetVolume);
            }
        }

        public void SetVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SavedKey, AudioListener.volume);
        }
    }
}
