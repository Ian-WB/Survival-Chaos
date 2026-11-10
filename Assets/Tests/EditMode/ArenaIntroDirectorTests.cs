using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    public class ArenaIntroDirectorTests
    {
        [TearDown]
        public void TearDown()
        {
            ArenaIntroDirector.Enabled = true;
        }

        [Test]
        public void Intro_IsOffByDefaultOutsidePlay()
        {
            Assert.IsFalse(ArenaIntroDirector.IsPlaying);
        }

        [Test]
        public void Enabled_CanBeToggled()
        {
            ArenaIntroDirector.Enabled = false;
            Assert.IsFalse(ArenaIntroDirector.Enabled);

            ArenaIntroDirector.Enabled = true;
            Assert.IsTrue(ArenaIntroDirector.Enabled);
        }

        [Test]
        public void Enabled_IsTrueByDefault()
        {
            Assert.IsTrue(ArenaIntroDirector.Enabled);
        }

        [Test]
        public void WaveDirector_ResetStartTime_UpdatesElapsedClock()
        {
            float initialStart = 100f;
            float now = 105f;

            Assert.AreEqual(5f, WaveDirector.ElapsedAt(now, initialStart, true), 1e-4f);

            float resetStart = now;
            Assert.AreEqual(0f, WaveDirector.ElapsedAt(now, resetStart, true), 1e-4f);
        }

        [Test]
        public void WaveDirector_ElapsedAt_ZeroWhenNotStarted()
        {
            Assert.AreEqual(0f, WaveDirector.ElapsedAt(100f, 50f, false));
        }

        [Test]
        public void ArenaGeometry_LaneRadius_IsPositiveAndReasonable()
        {
            float laneRadius = ArenaGeometry.LaneRadius;
            Assert.Greater(laneRadius, 10f);
            Assert.Less(laneRadius, 50f);
        }

        [Test]
        public void CameraFraming_DefaultIndex_IsValid()
        {
            Assert.GreaterOrEqual(CameraFraming.DefaultIndex, 0);
            Assert.Less(CameraFraming.DefaultIndex, CameraFraming.Presets.Length);
            Assert.NotNull(CameraFraming.Default.Name);
            Assert.Greater(CameraFraming.Default.Distance, 0f);
        }

        [Test]
        public void Rumble_Strength_Enum_ContainsExpectedLevels()
        {
            Assert.AreEqual(0, (int)Rumble.Strength.Light);
            Assert.AreEqual(1, (int)Rumble.Strength.Block);
            Assert.AreEqual(2, (int)Rumble.Strength.Hit);
            Assert.AreEqual(3, (int)Rumble.Strength.Heavy);
            Assert.AreEqual(4, (int)Rumble.Strength.Death);
        }
    }
}
