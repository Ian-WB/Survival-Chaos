using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The weave a boss round flies through height.
    ///
    /// Pinned against the rows on the real boss and the numbers BuildBossRig
    /// authors for them, because the constraint that matters is spatial: a route
    /// has to leave from its muzzle, stay inside its own bank, and - for the
    /// curtain - keep its gap the size it was fired at. Each of those can be
    /// broken by a number that looks harmless on its own.
    /// </summary>
    public class RoundRouteTests
    {
        /// <summary>Row heights off the boss, lowest first - see MuzzleRowsTests.</summary>
        private static readonly float[] KeelRows = { -3.66f, -2.50f, -1.15f };
        private static readonly float[] CrownRows = { 4.215f, 4.626f, 5.085f, 5.60f };

        /// <summary>The prow's single row, the middle of the band.</summary>
        private const float ProwRow = 1.38f;

        // What BuildBossRig writes onto the two volleys.
        private const float KeelAmplitude = 0.4f;
        private const float KeelPeriod = 1.5f;
        private const float CrownAmplitude = 0.3f;
        private const float CrownPeriod = 1.2f;

        private const float Tolerance = 1e-4f;

        [Test]
        public void EveryRoundLeavesFromItsMuzzle_WhicheverWayItSetsOff()
        {
            Assert.AreEqual(0f, RoundRoute.Offset(0.4f, 1.5f, 0f, 0f), Tolerance);
            Assert.AreEqual(0f, RoundRoute.Offset(0.4f, 1.5f, Mathf.PI, 0f), Tolerance);
        }

        [Test]
        public void TheTwoPhases_SetOffInOppositeDirections()
        {
            float early = 0.05f;

            Assert.Greater(RoundRoute.Offset(0.3f, 1.2f, 0f, early), 0f);
            Assert.Less(RoundRoute.Offset(0.3f, 1.2f, Mathf.PI, early), 0f);
        }

        [Test]
        public void NeverStraysFurtherThanItsAmplitude()
        {
            for (float t = 0f; t < 6f; t += 0.01f)
            {
                Assert.LessOrEqual(Mathf.Abs(RoundRoute.Offset(0.4f, 1.5f, 0f, t)), 0.4f + Tolerance);
            }
        }

        [Test]
        public void ReachesItsAmplitude_AQuarterOfTheWayThrough()
        {
            Assert.AreEqual(0.4f, RoundRoute.Offset(0.4f, 1.5f, 0f, 1.5f / 4f), Tolerance);
        }

        [Test]
        public void RepeatsEveryPeriod()
        {
            Assert.AreEqual(
                RoundRoute.Offset(0.3f, 1.2f, 0f, 0.37f),
                RoundRoute.Offset(0.3f, 1.2f, 0f, 0.37f + 1.2f * 3f),
                Tolerance);
        }

        [Test]
        public void NoAmplitudeOrNoPeriod_HoldsHeight()
        {
            Assert.AreEqual(0f, RoundRoute.Offset(0f, 1.5f, 0f, 0.4f));
            Assert.AreEqual(0f, RoundRoute.Offset(0.4f, 0f, 0f, 0.4f));
            Assert.AreEqual(0f, RoundRoute.Offset(0.4f, -1f, 0f, 0.4f));
        }

        [Test]
        public void WithoutCrossing_EveryRowTakesTheSamePhase()
        {
            for (int row = 0; row < 4; row++)
            {
                Assert.AreEqual(0f, RoundRoute.PhaseForRow(row, crossing: false));
            }
        }

        [Test]
        public void WithCrossing_AlternateRowsAreOpposed()
        {
            Assert.AreEqual(0f, RoundRoute.PhaseForRow(0, crossing: true));
            Assert.AreEqual(Mathf.PI, RoundRoute.PhaseForRow(1, crossing: true));
            Assert.AreEqual(0f, RoundRoute.PhaseForRow(2, crossing: true));
            Assert.AreEqual(Mathf.PI, RoundRoute.PhaseForRow(3, crossing: true));
        }

        /// <summary>
        /// The curtain weaves as one shape, so the distance between any two of
        /// its rows - and so the gap - is the same at every moment of its flight.
        /// </summary>
        [Test]
        public void TheCurtain_KeepsItsGapTheSameSize()
        {
            float fired = KeelRows[2] - KeelRows[0];

            for (float t = 0f; t < 4.5f; t += 0.05f)
            {
                float low = KeelRows[0] + Height(KeelAmplitude, KeelPeriod, 0, false, t);
                float high = KeelRows[2] + Height(KeelAmplitude, KeelPeriod, 2, false, t);

                Assert.AreEqual(fired, high - low, Tolerance, "at t = " + t);
            }
        }

        /// <summary>
        /// The keel is the floor of the band and the prow its middle. A weave that
        /// carried the discs up to the prow's height would be the thing the
        /// armoured act cannot afford: fire from one bank arriving at another's.
        /// </summary>
        [Test]
        public void TheCurtain_NeverReachesTheProw()
        {
            float highest = KeelRows[KeelRows.Length - 1] + KeelAmplitude;

            Assert.Less(highest, ProwRow);
        }

        /// <summary>
        /// The whole point of crossing: each pair of rows swaps order part way
        /// through the weave. Row 0 starts under row 1 and a quarter weave later
        /// is above it, and the same for rows 2 and 3.
        /// </summary>
        [Test]
        public void TheRake_CrossesEachPairOfRows()
        {
            float quarter = CrownPeriod / 4f;

            for (int low = 0; low < 4; low += 2)
            {
                float lowAt = CrownRows[low] + Height(CrownAmplitude, CrownPeriod, low, true, quarter);
                float highAt = CrownRows[low + 1] + Height(CrownAmplitude, CrownPeriod, low + 1, true, quarter);

                Assert.Greater(lowAt, highAt, "rows " + low + " and " + (low + 1) + " never cross");
            }
        }

        /// <summary>
        /// The rake stays inside the crown: no row goes below the crown's bottom
        /// row by more than the weave, and so never down into the prow's middle
        /// of the band.
        /// </summary>
        [Test]
        public void TheRake_StaysAboveTheProw()
        {
            float lowest = CrownRows[0] - CrownAmplitude;

            Assert.Greater(lowest, ProwRow);
        }

        private static float Height(float amplitude, float period, int row, bool crossing, float t)
        {
            return RoundRoute.Offset(amplitude, period, RoundRoute.PhaseForRow(row, crossing), t);
        }
    }
}
