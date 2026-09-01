using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Which emplacement each piece of wreckage comes off.
    ///
    /// Worth pinning because being one out here does not look like a bug. The
    /// player sees plates appearing at three heights either way; what a wrong
    /// index costs is the property the whole hazard rests on - that the three
    /// heights are fed evenly, so no single altitude is safe for the act.
    /// </summary>
    public class BossSheddingTests
    {
        /// <summary>Three emplacements, all wrecked, in whatever order the rig has them.</summary>
        private static bool[] All => new[] { true, true, true };

        [Test]
        public void ShedsNothingWhileEveryEmplacementStands()
        {
            Assert.AreEqual(-1, BossShedding.SourceIndex(new[] { false, false, false }, 0));
            Assert.AreEqual(-1, BossShedding.SourceIndex(new[] { false, false, false }, 7));
        }

        [Test]
        public void ShedsNothingWithNoEmplacementsAtAll()
        {
            Assert.AreEqual(-1, BossShedding.SourceIndex(null, 3));
            Assert.AreEqual(-1, BossShedding.SourceIndex(new bool[0], 3));
        }

        [Test]
        public void CyclesEveryWreckedEmplacementInTurn()
        {
            Assert.AreEqual(0, BossShedding.SourceIndex(All, 0));
            Assert.AreEqual(1, BossShedding.SourceIndex(All, 1));
            Assert.AreEqual(2, BossShedding.SourceIndex(All, 2));
            Assert.AreEqual(0, BossShedding.SourceIndex(All, 3));
        }

        /// <summary>
        /// The case the counting rule exists for. Indexing by volley % length and
        /// skipping the standing ones would shed nothing on two volleys in three
        /// here; counting over the wrecked ones keeps the cadence whole whatever
        /// order the player killed the pods in.
        /// </summary>
        [Test]
        public void KeepsShedingEveryVolleyWhenOnlyOneIsWrecked()
        {
            var one = new[] { false, true, false };

            for (int volley = 0; volley < 5; volley++)
            {
                Assert.AreEqual(1, BossShedding.SourceIndex(one, volley), "volley " + volley);
            }
        }

        [Test]
        public void AlternatesBetweenTheTwoThatAreWrecked()
        {
            var two = new[] { true, false, true };

            Assert.AreEqual(0, BossShedding.SourceIndex(two, 0));
            Assert.AreEqual(2, BossShedding.SourceIndex(two, 1));
            Assert.AreEqual(0, BossShedding.SourceIndex(two, 2));
            Assert.AreEqual(2, BossShedding.SourceIndex(two, 3));
        }

        /// <summary>
        /// The volley counter is a plain int that is never reset within a life.
        /// It cannot overflow in a fight that lasts a minute, but the modulo is
        /// the one line where the difference between a wrong answer and an
        /// exception is a sign bit.
        /// </summary>
        [Test]
        public void SurvivesANegativeVolleyCount()
        {
            Assert.AreEqual(2, BossShedding.SourceIndex(All, -1));
            Assert.AreEqual(1, BossShedding.SourceIndex(All, -2));
            Assert.AreEqual(0, BossShedding.SourceIndex(All, -3));
            Assert.AreEqual(1, BossShedding.SourceIndex(All, int.MinValue));
        }
    }
}
