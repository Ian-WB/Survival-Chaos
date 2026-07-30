using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The ramp used to be expressed as a coroutine mutating a field, so the
    /// only way to know the cadence at a given moment was to run the game.
    /// These pin the curve directly, including the values the shipped scene uses.
    /// </summary>
    public class SpawnMathTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void BeforeTheFirstRampStep_IntervalIsUnchanged()
        {
            float interval = SpawnMath.IntervalAt(60f, 0.9f, 25f, 0.1f, 24f);

            Assert.AreEqual(60f, interval, Tolerance);
        }

        [Test]
        public void EachRampStep_MultipliesTheInterval()
        {
            Assert.AreEqual(54f, SpawnMath.IntervalAt(60f, 0.9f, 25f, 0.1f, 25f), Tolerance);
            Assert.AreEqual(48.6f, SpawnMath.IntervalAt(60f, 0.9f, 25f, 0.1f, 50f), Tolerance);
        }

        [Test]
        public void ObstacleStream_TightensFromSixtyToAboutSeventeen_OverAFullRun()
        {
            // 12 ramp steps by t=300 for the 12 obstacle spawners in Game.unity.
            float interval = SpawnMath.IntervalAt(60f, 0.9f, 25f, 0.1f, 300f);

            Assert.That(interval, Is.EqualTo(16.95f).Within(0.05f));
        }

        [Test]
        public void IntervalNeverDropsBelowTheFloor()
        {
            float interval = SpawnMath.IntervalAt(10f, 0.5f, 1f, 0.1f, 1000f);

            Assert.AreEqual(0.1f, interval, Tolerance);
        }

        [Test]
        public void ScaleOfOne_DisablesRamping()
        {
            float interval = SpawnMath.IntervalAt(8f, 1f, 20f, 0.1f, 500f);

            Assert.AreEqual(8f, interval, Tolerance);
        }

        [Test]
        public void NonPositiveRampEvery_DisablesRamping()
        {
            float interval = SpawnMath.IntervalAt(8f, 0.8f, 0f, 0.1f, 500f);

            Assert.AreEqual(8f, interval, Tolerance);
        }

        [Test]
        public void AtZeroElapsed_IntervalIsTheBase()
        {
            float interval = SpawnMath.IntervalAt(23f, 0.9f, 25f, 0.1f, 0f);

            Assert.AreEqual(23f, interval, Tolerance);
        }

        [Test]
        public void ScaleAboveOne_LoosensTheCadence()
        {
            float interval = SpawnMath.IntervalAt(10f, 2f, 10f, 0.1f, 20f);

            Assert.AreEqual(40f, interval, Tolerance);
        }

        [Test]
        public void BossStream_StaysEffectivelyOneShot()
        {
            // BossSpawn uses a 99999s interval so it fires once at t=300.
            // Even fully ramped it must stay far beyond the 301s cutoff.
            float interval = SpawnMath.IntervalAt(99999f, 0.9f, 25f, 0.1f, 300f);

            Assert.Greater(interval, 301f);
        }
    }
}
