using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The arithmetic that keeps two enemies from being drawn inside each
    /// other. Positions are the second enemy's centre measured from the first's.
    /// </summary>
    public class EnemySpacingTests
    {
        [Test]
        public void EnemiesAlreadyClearAreLeftAlone()
        {
            EnemySpacing.Clear(2f, 0f, 1f, 0.5f, out float along, out float up);
            Assert.AreEqual(0f, along);
            Assert.AreEqual(0f, up);

            EnemySpacing.Clear(0f, 0.6f, 1f, 0.5f, out along, out up);
            Assert.AreEqual(0f, along);
            Assert.AreEqual(0f, up);
        }

        [Test]
        public void TouchingEdgesCountAsClear()
        {
            EnemySpacing.Clear(1f, 0f, 1f, 0.5f, out float along, out float up);
            Assert.AreEqual(0f, along);
            Assert.AreEqual(0f, up);
        }

        [Test]
        public void FlatShipsPartInHeightBecauseThatIsTheShorterWayOut()
        {
            // Side by side they would need 0.9 along the lane, or 0.4 in height.
            EnemySpacing.Clear(0.1f, 0.1f, 1f, 0.5f, out float along, out float up);
            Assert.AreEqual(0f, along);
            Assert.AreEqual(-0.2f, up, 0.0001f, "the first moves down, away from the second above it");
        }

        [Test]
        public void EnemiesNoseToTailPartAlongTheLane()
        {
            EnemySpacing.Clear(-0.9f, 0f, 1f, 0.5f, out float along, out float up);
            Assert.AreEqual(0.05f, along, 0.0001f, "the first moves on, away from the second behind it");
            Assert.AreEqual(0f, up);
        }

        [Test]
        public void EachMovesHalfSoTheTwoTogetherCloseTheWholeOverlap()
        {
            EnemySpacing.Clear(0f, 0.3f, 1f, 0.5f, out _, out float first);
            EnemySpacing.Clear(0f, -0.3f, 1f, 0.5f, out _, out float second);

            // The second enemy's own view is the mirror of the first's.
            Assert.AreEqual(-first, second, 0.0001f);
            Assert.AreEqual(0.5f - 0.3f, System.Math.Abs(first) * 2f, 0.0001f);
        }

        [Test]
        public void EnemiesOnTheSameSpotStillPart()
        {
            EnemySpacing.Clear(0f, 0f, 1f, 0.5f, out float along, out float up);
            Assert.AreEqual(0f, along);
            Assert.AreEqual(-0.25f, up, 0.0001f);
        }
    }
}
