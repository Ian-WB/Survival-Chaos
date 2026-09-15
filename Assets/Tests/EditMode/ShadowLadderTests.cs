using System;
using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The Shadows row reaches two lights and the clouds through one ladder, and
    /// the two ways it can go wrong are both silent: a point light asked for a
    /// level its atlas cannot hold is quietly scaled down by HDRP, and a tier
    /// defaulting to Off takes the sun's shadow away from a fresh install.
    /// </summary>
    public class ShadowLadderTests
    {
        [Test]
        public void LightLevel_StepsThroughTheTiersFirstThreeLevels()
        {
            Assert.AreEqual(0, ShadowLadder.LightLevel(EffectQuality.Low));
            Assert.AreEqual(1, ShadowLadder.LightLevel(EffectQuality.Medium));
            Assert.AreEqual(2, ShadowLadder.LightLevel(EffectQuality.High));
        }

        [Test]
        public void LightLevel_NeverReachesUltra_EvenFromAStoredRayTracedRung()
        {
            // Six point-light faces at Ultra outgrow the punctual atlas on every
            // tier, and a settings file can carry any rung the enum has.
            foreach (EffectQuality quality in Enum.GetValues(typeof(EffectQuality)))
            {
                Assert.Less(ShadowLadder.LightLevel(quality), 3, quality.ToString());
            }
        }

        [Test]
        public void CloudResolution_DoublesWithEachRung()
        {
            Assert.AreEqual(128, ShadowLadder.CloudResolution(EffectQuality.Low));
            Assert.AreEqual(256, ShadowLadder.CloudResolution(EffectQuality.Medium));
            Assert.AreEqual(512, ShadowLadder.CloudResolution(EffectQuality.High));
        }

        [Test]
        public void CloudResolution_Medium_IsWhatTheSceneWasAuthoredWith()
        {
            // Scene Volume Profile carries 256, so Medium changes nothing about
            // the clouds.
            Assert.AreEqual(256, ShadowLadder.CloudResolution(EffectQuality.Medium));
        }

        [Test]
        public void Presets_NoTierTurnsShadowsOff_AndNoneLowersThemGoingUp()
        {
            GraphicsPreset[] tiers = GraphicsPresets.All;

            for (int i = 0; i < tiers.Length; i++)
            {
                Assert.AreNotEqual(EffectQuality.Off, tiers[i].Shadows, tiers[i].Name);
                Assert.LessOrEqual(tiers[i].Shadows, ShadowLadder.Highest, tiers[i].Name);

                if (i > 0)
                {
                    Assert.GreaterOrEqual((int)tiers[i].Shadows, (int)tiers[i - 1].Shadows, tiers[i].Name);
                }
            }
        }
    }
}
