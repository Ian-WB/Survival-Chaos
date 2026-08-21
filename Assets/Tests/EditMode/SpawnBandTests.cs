using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The case these exist for is a real one: the player's floor was raised from
    /// 1.103 to 4.421 and four streams authored at 2.9, spawning over 1.8 to 3.7,
    /// were left entirely beneath it. Nothing reported it, and the enemies that
    /// carry ObstacleScript never climb, so they stayed there.
    ///
    /// The numbers below are the shipped ones, so a future change to the arena
    /// that reintroduces the gap fails here rather than ten minutes into a run.
    /// </summary>
    public class SpawnBandTests
    {
        private const float Tolerance = 0.0001f;

        private const float Floor = 4.421f;
        private const float Ceiling = 13.318f;

        [Test]
        public void RangeOf_AddsBothEndsOfTheOffsetToTheHeight()
        {
            SpawnBand.RangeOf(6.016f, new Vector2(-1.1f, 0.8f), out float low, out float high);

            Assert.AreEqual(4.916f, low, Tolerance);
            Assert.AreEqual(6.816f, high, Tolerance);
        }

        [Test]
        public void RangeOf_ReadsABackwardsOffsetAsTheSameSpan()
        {
            // Random.Range does not care which way round its arguments are, so a
            // stream authored (0.8, -1.1) spawns over exactly the same heights.
            SpawnBand.RangeOf(6.016f, new Vector2(0.8f, -1.1f), out float low, out float high);

            Assert.AreEqual(4.916f, low, Tolerance);
            Assert.AreEqual(6.816f, high, Tolerance);
        }

        [Test]
        public void TheStreamsThatWereStranded_AreReportedAsWhollyOutside()
        {
            // y 2.9 with the authored -1.1..0.8, against the raised floor.
            SpawnBand.RangeOf(2.9f, new Vector2(-1.1f, 0.8f), out float low, out float high);

            Assert.IsTrue(SpawnBand.IsWhollyOutside(low, high, Floor, Ceiling));
            Assert.IsFalse(SpawnBand.IsFullyInside(low, high, Floor, Ceiling));

            // Measured from the bottom of the span: the deepest one sat 2.621
            // under the floor, and even the shallowest was 0.721 under it.
            Assert.AreEqual(2.621f, SpawnBand.BelowFloorBy(low, Floor), Tolerance);
            Assert.AreEqual(0.721f, SpawnBand.BelowFloorBy(high, Floor), Tolerance);
        }

        [Test]
        public void TheSameStreams_WereInsideTheOldBand()
        {
            SpawnBand.RangeOf(2.9f, new Vector2(-1.1f, 0.8f), out float low, out float high);

            Assert.IsTrue(SpawnBand.IsFullyInside(low, high, 1.103f, 11.129f));
        }

        [Test]
        public void AfterTheRescale_EveryShippedHeightIsInside()
        {
            var wide = new Vector2(-1.1f, 1.1f);
            var narrow = new Vector2(-1.1f, 0.8f);

            AssertInside(6.016f, narrow);
            AssertInside(7.791f, wide);
            AssertInside(8.537f, narrow);
            AssertInside(9.459f, wide);
            AssertInside(9.513f, wide);
            AssertInside(10.719f, narrow);
        }

        [Test]
        public void AStreamStraddlingTheFloor_IsOutsideButNotWhollyOutside()
        {
            // The pre-rescale boss height: 4.9 +/- 1.1 put its lower end under the
            // floor while the rest of the span stayed reachable.
            SpawnBand.RangeOf(4.9f, new Vector2(-1.1f, 1.1f), out float low, out float high);

            Assert.IsFalse(SpawnBand.IsFullyInside(low, high, Floor, Ceiling));
            Assert.IsFalse(SpawnBand.IsWhollyOutside(low, high, Floor, Ceiling));
            Assert.AreEqual(0.621f, SpawnBand.BelowFloorBy(low, Floor), Tolerance);
        }

        [Test]
        public void ClearancesAreZero_RatherThanNegative_WhenInside()
        {
            Assert.AreEqual(0f, SpawnBand.BelowFloorBy(9f, Floor), Tolerance);
            Assert.AreEqual(0f, SpawnBand.AboveCeilingBy(9f, Ceiling), Tolerance);
        }

        [Test]
        public void AStreamAboveTheCeiling_IsMeasuredFromTheTop()
        {
            SpawnBand.RangeOf(20f, new Vector2(-1.1f, 1.1f), out float low, out float high);

            Assert.IsTrue(SpawnBand.IsWhollyOutside(low, high, Floor, Ceiling));
            Assert.AreEqual(0f, SpawnBand.BelowFloorBy(low, Floor), Tolerance);
            Assert.AreEqual(7.782f, SpawnBand.AboveCeilingBy(high, Ceiling), Tolerance);
        }

        [Test]
        public void TouchingTheFloorOrCeilingExactly_Counts_AsInside()
        {
            Assert.IsTrue(SpawnBand.IsFullyInside(Floor, Ceiling, Floor, Ceiling));
        }

        private static void AssertInside(float height, Vector2 offset)
        {
            SpawnBand.RangeOf(height, offset, out float low, out float high);

            Assert.IsTrue(
                SpawnBand.IsFullyInside(low, high, Floor, Ceiling),
                "height " + height + " spawns " + low + " to " + high
                + ", outside " + Floor + " to " + Ceiling);
        }
    }
}
