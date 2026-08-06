using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// A volume curve fails quietly: nobody notices that a slider does nothing
    /// over its top half until they try to use it. The value that must never
    /// escape is negative infinity — log10(0) produces it, and feeding it to a
    /// mixer parameter or an AudioSource poisons the channel permanently.
    /// </summary>
    public class AudioLevelsTests
    {
        [Test]
        public void FullSlider_IsUnityGain()
        {
            Assert.AreEqual(0f, AudioLevels.ToDecibels(1f), 0.001f);
            Assert.AreEqual(1f, AudioLevels.ToAmplitude(1f), 0.001f);
        }

        [Test]
        public void HalfSlider_IsAboutSixDecibelsDown()
        {
            // The textbook figure, and the one that makes a slider feel right.
            Assert.AreEqual(-6.02f, AudioLevels.ToDecibels(0.5f), 0.01f);
        }

        [Test]
        public void Silence_IsTheFloorRatherThanNegativeInfinity()
        {
            float decibels = AudioLevels.ToDecibels(0f);

            Assert.AreEqual(AudioLevels.MinimumDecibels, decibels, 0.001f);
            Assert.IsFalse(float.IsNegativeInfinity(decibels));
            Assert.IsFalse(float.IsNaN(decibels));
        }

        [Test]
        public void Decibels_NeverFallBelowTheFloor()
        {
            Assert.GreaterOrEqual(AudioLevels.ToDecibels(0.00000001f), AudioLevels.MinimumDecibels);
        }

        [Test]
        public void OutOfRangeInput_IsClamped()
        {
            Assert.AreEqual(0f, AudioLevels.ToDecibels(5f), 0.001f);
            Assert.AreEqual(AudioLevels.MinimumDecibels, AudioLevels.ToDecibels(-3f), 0.001f);
            Assert.AreEqual(0f, AudioLevels.ToAmplitude(-3f), 0.001f);
            Assert.AreEqual(1f, AudioLevels.ToAmplitude(5f), 0.001f);
        }

        [Test]
        public void SliderAndDecibels_RoundTrip()
        {
            // A saved level has to come back onto the control that set it.
            foreach (float value in new[] { 0.05f, 0.25f, 0.5f, 0.75f, 1f })
            {
                float back = AudioLevels.ToSlider(AudioLevels.ToDecibels(value));
                Assert.AreEqual(value, back, 0.001f, "round trip failed at " + value);
            }
        }

        [Test]
        public void FloorConvertsBackToSilence()
        {
            Assert.AreEqual(0f, AudioLevels.ToSlider(AudioLevels.MinimumDecibels), 0.0001f);
            Assert.AreEqual(0f, AudioLevels.ToSlider(-120f), 0.0001f);
        }

        [Test]
        public void AmplitudeAndDecibels_DescribeTheSameLevel()
        {
            // The two paths - a mixer parameter and an AudioSource volume - must
            // agree, or the same slider position would sound different depending
            // on whether a mixer happened to be assigned.
            foreach (float value in new[] { 0.1f, 0.4f, 0.9f })
            {
                float viaDecibels = AudioLevels.ToSlider(AudioLevels.ToDecibels(value));
                Assert.AreEqual(AudioLevels.ToAmplitude(value), viaDecibels, 0.001f);
            }
        }

        [Test]
        public void Combine_AppliesMasterOnTopOfTheChannel()
        {
            Assert.AreEqual(0.25f, AudioLevels.Combine(0.5f, 0.5f), 0.001f);
            Assert.AreEqual(0.5f, AudioLevels.Combine(0.5f, 1f), 0.001f);
        }

        [Test]
        public void Combine_WithMasterSilent_IsSilent()
        {
            Assert.AreEqual(0f, AudioLevels.Combine(1f, 0f), 0.0001f);
        }
    }
}
