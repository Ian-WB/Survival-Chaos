using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The arithmetic of the ship's gun muzzles: which barrels fire at each
    /// Hydra stage, how rounds share the rows without crossing paths, how the
    /// mouths heat before a volley, and how a round fans out to its lane.
    /// </summary>
    public class ShipMuzzlesTests
    {
        private const float Tolerance = 0.0001f;

        /// <summary>
        /// Hydra adds rows as its barrels are bolted on, and stages outside
        /// 0..3 clamp to the bare nose or the full rack.
        /// </summary>
        [TestCase(-1, 1)]
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(9, 3)]
        public void RowCount_MatchesTheGunsForEachStage_AndClampsOutOfRange(int stage, int expected)
        {
            Assert.AreEqual(expected, ShipMuzzles.RowCount(stage));
        }

        /// <summary>
        /// Every stage lists its rows from lowest to highest on the middle
        /// plane, and every muzzle sits out ahead of the hull's nose so no
        /// round is born inside the ship.
        /// </summary>
        [Test]
        public void Rows_RiseInHeight_SitOnTheMiddlePlane_AndClearTheNose()
        {
            for (int stage = 0; stage <= 3; stage++)
            {
                int count = ShipMuzzles.RowCount(stage);

                Assert.AreEqual(ShipMuzzles.Row(stage, 0), ShipMuzzles.Row(stage, -1));
                Assert.AreEqual(ShipMuzzles.Row(stage, count - 1), ShipMuzzles.Row(stage, 99));

                for (int row = 0; row < count; row++)
                {
                    Vector3 point = ShipMuzzles.Row(stage, row);

                    Assert.Greater(point.x, 0.19f, "stage " + stage + " row " + row + " must sit ahead of the nose");
                    Assert.AreEqual(0f, point.z, Tolerance, "stage " + stage + " row " + row + " must lie on z = 0");

                    if (row > 0)
                    {
                        Vector3 below = ShipMuzzles.Row(stage, row - 1);
                        Assert.Greater(point.y, below.y, "stage " + stage + " row " + row + " must sit above the row before it");
                    }
                }
            }
        }

        /// <summary>
        /// Rounds are dealt across the active rows from lowest rank to highest
        /// so their paths never cross as they fan out.
        /// </summary>
        [Test]
        public void RowFor_SplitsVolleysAcrossRowsInOrder_AndGuardsZeroCounts()
        {
            Assert.AreEqual(0, ShipMuzzles.RowFor(0, 1, 1));

            Assert.AreEqual(0, ShipMuzzles.RowFor(0, 2, 1));
            Assert.AreEqual(0, ShipMuzzles.RowFor(1, 2, 1));

            Assert.AreEqual(0, ShipMuzzles.RowFor(0, 4, 2));
            Assert.AreEqual(0, ShipMuzzles.RowFor(1, 4, 2));
            Assert.AreEqual(1, ShipMuzzles.RowFor(2, 4, 2));
            Assert.AreEqual(1, ShipMuzzles.RowFor(3, 4, 2));

            int[] counts = new int[3];
            int previous = 0;

            for (int rank = 0; rank < 8; rank++)
            {
                int row = ShipMuzzles.RowFor(rank, 8, 3);
                Assert.GreaterOrEqual(row, previous);
                counts[row]++;
                previous = row;
            }

            Assert.AreEqual(3, counts[0]);
            Assert.AreEqual(3, counts[1]);
            Assert.AreEqual(2, counts[2]);

            Assert.AreEqual(0, ShipMuzzles.RowFor(0, 0, 2));
            Assert.AreEqual(0, ShipMuzzles.RowFor(2, 4, 0));
            Assert.AreEqual(0, ShipMuzzles.RowFor(1, -2, 3));
            Assert.AreEqual(0, ShipMuzzles.RowFor(1, 4, -1));
        }

        /// <summary>
        /// A round's rank is how many rounds in the volley end up lower than
        /// it, regardless of the order the pattern lists them in.
        /// </summary>
        [Test]
        public void Rank_OrdersOffsetsFromLowestToHighest()
        {
            float[] offsets = { 0.5f, -0.5f, 1.5f, -1.5f };

            Assert.AreEqual(2, ShipMuzzles.Rank(offsets, 0));
            Assert.AreEqual(1, ShipMuzzles.Rank(offsets, 1));
            Assert.AreEqual(3, ShipMuzzles.Rank(offsets, 2));
            Assert.AreEqual(0, ShipMuzzles.Rank(offsets, 3));
        }

        /// <summary>
        /// The pre-shot glow is capped at <see cref="ShipMuzzles.LeadSeconds"/>
        /// for slow fire, shrinks to a share of fast intervals, and vanishes
        /// for non-positive intervals.
        /// </summary>
        [Test]
        public void Lead_CapsAtLeadSeconds_ScalesShortIntervals_AndClampsNegative()
        {
            Assert.AreEqual(ShipMuzzles.LeadSeconds, ShipMuzzles.Lead(10f), Tolerance);
            Assert.AreEqual(0.06f, ShipMuzzles.Lead(0.1f), Tolerance);
            Assert.AreEqual(0f, ShipMuzzles.Lead(0f), Tolerance);
            Assert.AreEqual(0f, ShipMuzzles.Lead(-1f), Tolerance);
        }

        /// <summary>
        /// Mouths stay cold until the lead window opens, then rise on a square
        /// curve to full heat on the shot.
        /// </summary>
        [Test]
        public void Heat_RisesOnASquareCurveTowardTheShot()
        {
            const float lead = 0.2f;

            Assert.AreEqual(0f, ShipMuzzles.Heat(lead, lead), Tolerance);
            Assert.AreEqual(0f, ShipMuzzles.Heat(lead + 0.5f, lead), Tolerance);
            Assert.AreEqual(0f, ShipMuzzles.Heat(float.PositiveInfinity, lead), Tolerance);

            Assert.AreEqual(0.25f, ShipMuzzles.Heat(lead * 0.5f, lead), Tolerance);

            Assert.AreEqual(1f, ShipMuzzles.Heat(0f, lead), Tolerance);
            Assert.AreEqual(1f, ShipMuzzles.Heat(-0.1f, lead), Tolerance);

            Assert.AreEqual(0f, ShipMuzzles.Heat(0f, 0f), Tolerance);
            Assert.AreEqual(0f, ShipMuzzles.Heat(0.05f, -1f), Tolerance);

            float previous = ShipMuzzles.Heat(lead, lead);

            for (int step = 1; step <= 10; step++)
            {
                float untilNext = lead * (1f - step / 10f);
                float current = ShipMuzzles.Heat(untilNext, lead);

                Assert.Greater(current, previous);
                previous = current;
            }
        }

        /// <summary>
        /// A round leaves at the muzzle's height, breaks fast toward its lane
        /// past halfway by half time, and settles onto the pattern height.
        /// </summary>
        [Test]
        public void FanHeight_EasesOutFromMuzzleToPatternHeight()
        {
            const float from = -0.5f;
            const float to = 1.5f;
            const float seconds = 0.09f;
            const float halfway = (from + to) * 0.5f;

            Assert.AreEqual(from, ShipMuzzles.FanHeight(from, to, 0f, seconds), Tolerance);
            Assert.AreEqual(from, ShipMuzzles.FanHeight(from, to, -0.02f, seconds), Tolerance);
            Assert.AreEqual(to, ShipMuzzles.FanHeight(from, to, seconds, seconds), Tolerance);
            Assert.AreEqual(to, ShipMuzzles.FanHeight(from, to, seconds + 0.1f, seconds), Tolerance);
            Assert.AreEqual(to, ShipMuzzles.FanHeight(from, to, 0.02f, 0f), Tolerance);
            Assert.AreEqual(to, ShipMuzzles.FanHeight(from, to, 0.02f, -1f), Tolerance);

            float atHalfTime = ShipMuzzles.FanHeight(from, to, seconds * 0.5f, seconds);
            Assert.Greater(atHalfTime, halfway);
            Assert.Less(atHalfTime, to);

            float previous = from;

            for (int sample = 1; sample <= 10; sample++)
            {
                float elapsed = seconds * sample / 10f;
                float height = ShipMuzzles.FanHeight(from, to, elapsed, seconds);

                Assert.GreaterOrEqual(height, previous);
                previous = height;
            }
        }

        /// <summary>
        /// A ship with no flare mesh or material still tracks its stage,
        /// accepts charge and fire calls, and places its muzzles in the world.
        /// </summary>
        [Test]
        public void UnbuiltComponent_ClampsStage_AcceptsChargeAndFire_AndTransformsWorldPoint()
        {
            var host = new GameObject("muzzles");

            try
            {
                host.transform.position = new Vector3(3f, -2f, 5f);
                host.transform.rotation = Quaternion.Euler(0f, 35f, 10f);

                ShipMuzzles muzzles = host.AddComponent<ShipMuzzles>();
                muzzles.SetStage(9);

                Assert.AreEqual(3, muzzles.Stage);
                Assert.AreEqual(3, muzzles.Rows);

                muzzles.Charge(0.05f, 0.25f);
                muzzles.Fire();

                Vector3 expected = host.transform.TransformPoint(ShipMuzzles.Row(3, 0));
                Vector3 actual = muzzles.WorldPoint(0);

                Assert.AreEqual(expected.x, actual.x, Tolerance);
                Assert.AreEqual(expected.y, actual.y, Tolerance);
                Assert.AreEqual(expected.z, actual.z, Tolerance);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
