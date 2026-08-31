using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Grouping a bank of muzzles into the rows it was modelled in.
    ///
    /// Worth pinning against the real heights off the boss, because the whole
    /// point of deriving the rows is that nobody has to notice when the model
    /// changes - which also means nobody would notice if the derivation quietly
    /// stopped agreeing with it.
    /// </summary>
    public class MuzzleRowsTests
    {
        /// <summary>
        /// The sixteen crown muzzles, in the order the attack fires them, exactly
        /// as authored on the prefab. Four rows of four, listed out of order
        /// because the pivot list is out of order.
        /// </summary>
        private static readonly float[] Crown =
        {
            5.569f, 5.580f, 5.071f, 5.091f, 4.620f, 4.620f, 4.215f, 4.215f,
            5.618f, 5.618f, 5.090f, 5.090f, 4.632f, 4.632f, 4.215f, 4.215f,
        };

        /// <summary>The twelve keel muzzles, likewise. Three rows of four.</summary>
        private static readonly float[] Keel =
        {
            -1.143f, -1.143f, -2.518f, -2.518f, -3.643f, -3.653f,
            -1.143f, -1.163f, -2.513f, -2.443f, -3.673f, -3.673f,
        };

        [Test]
        public void TheCrown_ResolvesToFourRows()
        {
            int[] rows = MuzzleRows.Assign(Crown);

            Assert.AreEqual(4, MuzzleRows.Count(rows));
        }

        [Test]
        public void TheKeel_ResolvesToThreeRows()
        {
            int[] rows = MuzzleRows.Assign(Keel);

            Assert.AreEqual(3, MuzzleRows.Count(rows));
        }

        [Test]
        public void EveryCrownRow_HoldsFourMuzzles()
        {
            int[] rows = MuzzleRows.Assign(Crown);

            int[] perRow = new int[MuzzleRows.Count(rows)];
            foreach (int row in rows)
            {
                perRow[row]++;
            }

            CollectionAssert.AreEqual(new[] { 4, 4, 4, 4 }, perRow);
        }

        [Test]
        public void RowsAreNumberedFromTheBottomUp()
        {
            // The rake climbs and the curtain's gap steps upward, so both callers
            // read a rising index as rising height.
            int[] rows = MuzzleRows.Assign(Crown);

            for (int a = 0; a < Crown.Length; a++)
            {
                for (int b = 0; b < Crown.Length; b++)
                {
                    if (rows[a] < rows[b])
                    {
                        Assert.Less(Crown[a], Crown[b], "a lower row sits lower");
                    }
                }
            }
        }

        [Test]
        public void TheAnswer_IsReturnedInTheOrderTheMuzzlesWereGiven()
        {
            // The caller indexes this against its pivot array to decide what to
            // fire, so a result sorted by height would fire the wrong muzzles.
            int[] rows = MuzzleRows.Assign(Crown);

            Assert.AreEqual(Crown.Length, rows.Length);
            Assert.AreEqual(rows[6], rows[7], "two muzzles authored at the same height");
            Assert.AreEqual(rows[0], rows[1], "and the pair at the top of the list");
            Assert.AreNotEqual(rows[0], rows[6], "which are not the same row");
        }

        [Test]
        public void TheTolerance_SpansTheWholeGapBetweenRealRows()
        {
            // The closest two rows on the model are 0.405 apart and the widest
            // scatter inside one row is 0.07, so every value in between has to
            // give the same answer. If this ever fails, the model moved.
            foreach (float tolerance in new[] { 0.1f, 0.2f, 0.25f, 0.3f, 0.4f })
            {
                Assert.AreEqual(4, MuzzleRows.Count(MuzzleRows.Assign(Crown, tolerance)),
                    "crown at tolerance " + tolerance);
                Assert.AreEqual(3, MuzzleRows.Count(MuzzleRows.Assign(Keel, tolerance)),
                    "keel at tolerance " + tolerance);
            }
        }

        [Test]
        public void ASingleHeight_IsOneRow()
        {
            // The prow: four muzzles in a line, all at the same height.
            int[] rows = MuzzleRows.Assign(new[] { 1.377f, 1.377f, 1.377f, 1.377f });

            Assert.AreEqual(1, MuzzleRows.Count(rows));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, rows);
        }

        [Test]
        public void AnEmptyBank_HasNoRows()
        {
            Assert.AreEqual(0, MuzzleRows.Count(MuzzleRows.Assign(new float[0])));
            Assert.AreEqual(0, MuzzleRows.Count(MuzzleRows.Assign(null)));
            Assert.AreEqual(0, MuzzleRows.Count(null));
        }

        [Test]
        public void AGapIsMeasuredAgainstTheMuzzleBelow_NotTheRowsFirst()
        {
            // A run of small steps is one row, however far it drifts in total.
            // Stated because the other reading - capping a row's total spread -
            // would split this into three, and both are defensible until you say
            // which one you meant.
            int[] rows = MuzzleRows.Assign(new[] { 0f, 0.2f, 0.4f, 0.6f }, 0.25f);

            Assert.AreEqual(1, MuzzleRows.Count(rows));
        }

        [Test]
        public void AGapWiderThanTheTolerance_SplitsTheRow()
        {
            int[] rows = MuzzleRows.Assign(new[] { 0f, 0.2f, 0.6f }, 0.25f);

            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, rows);
        }

        [Test]
        public void HeightsAreReadInTheBossOwnSpace()
        {
            // Local, not world. Nothing to assert beyond the null guard; the
            // property name is the contract.
            Assert.AreEqual(0, MuzzleRows.HeightsOf(null).Length);
        }

        /// <summary>Which rows the curtain leaves open, as a readable string.</summary>
        private static string Gaps(int volley, int rowCount, int openRows)
        {
            string open = string.Empty;

            for (int row = 0; row < rowCount; row++)
            {
                open += MuzzleRows.IsGap(row, volley, rowCount, openRows) ? "." : "#";
            }

            return open;
        }

        [Test]
        public void TheGap_StepsUpOneRowPerVolley()
        {
            // The keel's three rows. '.' is the hole, '#' is a row that fires.
            Assert.AreEqual(".##", Gaps(0, 3, 1));
            Assert.AreEqual("#.#", Gaps(1, 3, 1));
            Assert.AreEqual("##.", Gaps(2, 3, 1));
        }

        [Test]
        public void TheGap_WrapsBackToTheBottom()
        {
            Assert.AreEqual(".##", Gaps(3, 3, 1), "the fourth volley is the first again");
            Assert.AreEqual("#.#", Gaps(4, 3, 1));
        }

        [Test]
        public void AWiderGap_OpensAdjacentRowsAndWrapsAcrossTheTop()
        {
            Assert.AreEqual("..#", Gaps(0, 3, 2));
            Assert.AreEqual("#..", Gaps(1, 3, 2));
            Assert.AreEqual(".#.", Gaps(2, 3, 2), "wrapped: the top row and the bottom one");
        }

        [Test]
        public void TheGap_NeverOpensTheWholeBank()
        {
            // A curtain that fires nothing is indistinguishable from an attack
            // that has been switched off, which is not a thing to leave authorable
            // by typing a number one too large into the inspector.
            Assert.AreEqual("..#", Gaps(0, 3, 3));
            Assert.AreEqual("..#", Gaps(0, 3, 99));
        }

        [Test]
        public void NoGap_FiresEveryRow()
        {
            Assert.AreEqual("###", Gaps(0, 3, 0));
            Assert.AreEqual("###", Gaps(1, 3, -5));
        }

        [Test]
        public void ASingleRow_IsNeverTheGap()
        {
            // The prow. One row is not a wall, so there is nothing to leave a
            // hole in - it fires or it does not.
            Assert.AreEqual("#", Gaps(0, 1, 1));
            Assert.AreEqual("#", Gaps(7, 1, 1));
        }

        [Test]
        public void AnEmptyBank_HasNoGap()
        {
            Assert.IsFalse(MuzzleRows.IsGap(0, 0, 0, 1));
        }

        [Test]
        public void ANegativeVolleyStillLandsOnARow()
        {
            // Defensive: the volley counter only ever climbs, but a modulo that
            // hands back a negative row would index outside the bank, and the
            // failure would be a curtain that fires everything.
            Assert.AreEqual("##.", Gaps(-1, 3, 1));
            Assert.AreEqual("#.#", Gaps(-2, 3, 1));
        }

        [Test]
        public void TheCrown_CyclesThroughAllFourOfItsRows()
        {
            Assert.AreEqual(".###", Gaps(0, 4, 1));
            Assert.AreEqual("#.##", Gaps(1, 4, 1));
            Assert.AreEqual("##.#", Gaps(2, 4, 1));
            Assert.AreEqual("###.", Gaps(3, 4, 1));
            Assert.AreEqual(".###", Gaps(4, 4, 1));
        }
    }
}
