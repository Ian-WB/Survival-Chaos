using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>The separately adjustable groups of sound.</summary>
    public enum AudioChannel
    {
        /// <summary>Everything. Scales the three below.</summary>
        Master = 0,

        /// <summary>Background tracks.</summary>
        Music = 1,

        /// <summary>Gameplay: weapons, impacts, explosions.</summary>
        Sfx = 2,

        /// <summary>Menus. Kept apart from Sfx so turning combat down does not
        /// make the menu feel broken.</summary>
        Ui = 3
    }

    /// <summary>
    /// Converts between a 0..1 slider and the units audio actually uses.
    ///
    /// Hearing is logarithmic: halving a slider that drives amplitude directly
    /// does not sound half as loud, and the bottom of its travel does almost
    /// nothing. Everything here works from one conversion so the mixer path and
    /// the direct path cannot disagree about what "0.5" means.
    ///
    /// Free of MonoBehaviour so the curve can be tested rather than eyeballed.
    /// </summary>
    public static class AudioLevels
    {
        /// <summary>
        /// The floor an AudioMixer treats as silence. Below this it is muted, and
        /// log10(0) is negative infinity, which would poison a mixer parameter.
        /// </summary>
        public const float MinimumDecibels = -80f;

        /// <summary>Slider value at or below which a channel is silent.</summary>
        public const float SilenceThreshold = 0.0001f;

        /// <summary>
        /// Slider position to decibels, for an AudioMixer's exposed parameter.
        /// 1 gives 0 dB, 0.5 gives about -6 dB, 0 gives silence.
        /// </summary>
        public static float ToDecibels(float slider)
        {
            slider = Mathf.Clamp01(slider);

            if (slider <= SilenceThreshold)
            {
                return MinimumDecibels;
            }

            return Mathf.Max(MinimumDecibels, Mathf.Log10(slider) * 20f);
        }

        /// <summary>
        /// Decibels back to slider position, so a saved level can be shown on the
        /// control that set it.
        /// </summary>
        public static float ToSlider(float decibels)
        {
            if (decibels <= MinimumDecibels)
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));
        }

        /// <summary>
        /// Slider position to an amplitude for <c>AudioSource.volume</c>.
        ///
        /// Amplitude and the decibel form describe the same quantity - 20·log10
        /// of this is exactly <see cref="ToDecibels"/> - which is the point: with
        /// or without a mixer, a given slider position sounds the same.
        /// </summary>
        public static float ToAmplitude(float slider)
        {
            slider = Mathf.Clamp01(slider);
            return slider <= SilenceThreshold ? 0f : slider;
        }

        /// <summary>
        /// The amplitude for one channel once Master is taken into account.
        /// Multiplying amplitudes is the same as adding decibels, so this matches
        /// what a mixer would do with the two faders in series.
        /// </summary>
        public static float Combine(float channelSlider, float masterSlider)
        {
            return ToAmplitude(channelSlider) * ToAmplitude(masterSlider);
        }
    }
}
