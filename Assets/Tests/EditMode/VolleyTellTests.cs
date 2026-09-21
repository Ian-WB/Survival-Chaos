using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// How a volley's muzzles light before it fires.
    ///
    /// The glow is only a warning if it agrees with the volley: a rake that lit
    /// top-first and fired bottom-first would be teaching the player the wrong
    /// staircase every time. So the order the tell lights in and the order the
    /// rake fires in come from the same function, and these pin that it is its
    /// own inverse.
    /// </summary>
    public class VolleyTellTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void Together_IsDarkAtTheStartAndFullAtTheShot()
        {
            Assert.AreEqual(0f, VolleyTell.Together(0f), Tolerance);
            Assert.AreEqual(1f, VolleyTell.Together(1f), Tolerance);
        }

        [Test]
        public void Together_OnlyEverBrightens()
        {
            float previous = 0f;

            for (float p = 0f; p <= 1f; p += 0.01f)
            {
                float level = VolleyTell.Together(p);
                Assert.GreaterOrEqual(level, previous);
                previous = level;
            }
        }

        [Test]
        public void Together_ClampsOutsideTheWarning()
        {
            Assert.AreEqual(0f, VolleyTell.Together(-0.5f), Tolerance);
            Assert.AreEqual(1f, VolleyTell.Together(1.5f), Tolerance);
        }

        /// <summary>Slow to start, so it reads as a charge rather than a dimmer.</summary>
        [Test]
        public void Together_IsUnderHalfAtTheHalfway()
        {
            Assert.Less(VolleyTell.Together(0.5f), 0.5f);
        }

        [Test]
        public void InOrder_IsDarkEverywhereAtTheStart()
        {
            for (int order = 0; order < 4; order++)
            {
                Assert.AreEqual(0f, VolleyTell.InOrder(0f, order, 4), Tolerance);
            }
        }

        [Test]
        public void InOrder_IsFullEverywhereAtTheShot()
        {
            for (int order = 0; order < 4; order++)
            {
                Assert.AreEqual(1f, VolleyTell.InOrder(1f, order, 4), Tolerance);
            }
        }

        /// <summary>
        /// Half way through a four-row warning the first two rows are fully lit
        /// and the last two are still dark - the glow has swept half the bank.
        /// </summary>
        [Test]
        public void InOrder_HalfwayHasSweptHalfTheBank()
        {
            Assert.AreEqual(1f, VolleyTell.InOrder(0.5f, 0, 4), Tolerance);
            Assert.AreEqual(1f, VolleyTell.InOrder(0.5f, 1, 4), Tolerance);
            Assert.AreEqual(0f, VolleyTell.InOrder(0.5f, 2, 4), Tolerance);
            Assert.AreEqual(0f, VolleyTell.InOrder(0.5f, 3, 4), Tolerance);
        }

        [Test]
        public void InOrder_TheFirstRowIsNeverDimmerThanALaterOne()
        {
            for (float p = 0f; p <= 1f; p += 0.02f)
            {
                for (int order = 1; order < 4; order++)
                {
                    Assert.GreaterOrEqual(VolleyTell.InOrder(p, order - 1, 4), VolleyTell.InOrder(p, order, 4));
                }
            }
        }

        [Test]
        public void InOrder_WithNoRows_IsDark()
        {
            Assert.AreEqual(0f, VolleyTell.InOrder(0.5f, 0, 0));
        }

        [Test]
        public void Upward_AlternatesStartingUpward()
        {
            Assert.IsTrue(VolleyTell.Upward(0));
            Assert.IsFalse(VolleyTell.Upward(1));
            Assert.IsTrue(VolleyTell.Upward(2));
            Assert.IsFalse(VolleyTell.Upward(3));
        }

        [Test]
        public void FiringOrder_UpwardStartsAtTheBottomRow()
        {
            for (int row = 0; row < 4; row++)
            {
                Assert.AreEqual(row, VolleyTell.FiringOrder(row, 4, upward: true));
            }
        }

        [Test]
        public void FiringOrder_DownwardStartsAtTheTopRow()
        {
            Assert.AreEqual(3, VolleyTell.FiringOrder(0, 4, upward: false));
            Assert.AreEqual(0, VolleyTell.FiringOrder(3, 4, upward: false));
        }

        /// <summary>
        /// The row the rake fires at step s must be the row whose glow was lit s-th,
        /// in both directions - which is the function being its own inverse.
        /// </summary>
        [Test]
        public void FiringOrder_IsItsOwnInverse_SoTheGlowMatchesTheRake()
        {
            foreach (bool upward in new[] { true, false })
            {
                for (int step = 0; step < 4; step++)
                {
                    int row = VolleyTell.FiringOrder(step, 4, upward);
                    Assert.AreEqual(step, VolleyTell.FiringOrder(row, 4, upward));
                }
            }
        }
    }
}
