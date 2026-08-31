using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Sorts a bank of muzzles into the rows they were modelled in, with no Unity
    /// object attached so the grouping can be tested directly.
    ///
    /// Both timed patterns need this and neither should be authored by hand. The
    /// crown is sixteen muzzles in four rows of four and the keel is twelve in
    /// three rows of four, and those rows are already there in the heights the
    /// artist placed them at - the gaps between rows are ten times the scatter
    /// within one. Typing the grouping into the inspector as well would be a
    /// second copy of a fact the model already states, and the copy that goes
    /// stale is always the typed one.
    ///
    /// Heights are read in the boss's own space, so the answer does not change as
    /// the boss climbs, and does not change when the muzzle rig mirrors: the flip
    /// is a turn about the vertical axis, which is the one axis it leaves alone.
    /// </summary>
    public static class MuzzleRows
    {
        /// <summary>
        /// How far apart two muzzles have to be before they count as different
        /// rows, in the boss's local units.
        ///
        /// Measured off the model rather than guessed. Within a row the crown
        /// muzzles differ by up to 0.02 and the keel by up to 0.07; between rows
        /// the closest pair is 0.405 apart. Anything from about 0.1 to 0.4 gives
        /// the same answer, so this sits in the middle of a wide correct range
        /// rather than on the edge of a narrow one.
        /// </summary>
        public const float DefaultTolerance = 0.25f;

        /// <summary>
        /// The row each muzzle belongs to, numbered from the bottom up, in the
        /// same order the heights were given.
        ///
        /// Bottom-up because every caller wants it that way: the rake climbs, the
        /// curtain's gap steps upward, and a row index that counted down would
        /// have both of them subtracting from a count to say so.
        /// </summary>
        public static int[] Assign(float[] heights, float tolerance = DefaultTolerance)
        {
            if (heights == null || heights.Length == 0)
            {
                return new int[0];
            }

            // Sorted by height but carrying where each one came from, so the
            // result can be handed back in the caller's order. The caller's order
            // is the pivot array, and that is what it fires from.
            int[] order = new int[heights.Length];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            System.Array.Sort(order, (a, b) => heights[a].CompareTo(heights[b]));

            int[] rows = new int[heights.Length];
            int row = 0;
            float previous = heights[order[0]];

            for (int i = 0; i < order.Length; i++)
            {
                float height = heights[order[i]];

                // A new row starts wherever there is a gap, measured against the
                // muzzle below rather than against the row's first muzzle. A row
                // is a run of muzzles with no gap in it, and a bank wide enough
                // to drift by more than the tolerance across its own row is one
                // the model does not have.
                if (height - previous > tolerance)
                {
                    row++;
                }

                rows[order[i]] = row;
                previous = height;
            }

            return rows;
        }

        /// <summary>
        /// How many rows an assignment produced. One more than the largest index,
        /// and zero for an empty bank.
        /// </summary>
        public static int Count(int[] rows)
        {
            if (rows == null || rows.Length == 0)
            {
                return 0;
            }

            int highest = 0;
            foreach (int row in rows)
            {
                if (row > highest)
                {
                    highest = row;
                }
            }

            return highest + 1;
        }

        /// <summary>
        /// Whether a row is part of this volley's gap - the hole in the curtain
        /// that the player is meant to fly through.
        ///
        /// The gap steps up exactly one row per volley and wraps at the top, so
        /// two volleys are enough to know where the third one will be. That is
        /// the whole difference between a curtain and a coin toss: a wall with a
        /// random hole is not dodged, it is survived, and the player learns
        /// nothing either way.
        ///
        /// Split out here because the arithmetic is modular and modular
        /// arithmetic is where off-by-ones hide - a gap that skipped a row every
        /// third volley, or that sat still for two, would look like a slightly
        /// unfair attack rather than like a bug.
        /// </summary>
        /// <param name="row">The row being asked about, numbered from the bottom.</param>
        /// <param name="volley">Which volley this is. Steps the gap along.</param>
        /// <param name="rowCount">How many rows the bank has.</param>
        /// <param name="openRows">How many rows the gap is meant to be.</param>
        public static bool IsGap(int row, int volley, int rowCount, int openRows)
        {
            if (rowCount <= 0)
            {
                return false;
            }

            // Never open the whole bank. A curtain that fires nothing is
            // indistinguishable from an attack that has been switched off, and a
            // bank with only one row is not a wall in the first place.
            int open = Mathf.Clamp(openRows, 0, rowCount - 1);

            if (open <= 0)
            {
                return false;
            }

            return Wrap(row - Wrap(volley, rowCount), rowCount) < open;
        }

        /// <summary>
        /// Modulo that answers with a row number rather than with a sign. C# hands
        /// back a negative remainder for a negative left-hand side, which for a
        /// row index is never a useful answer.
        /// </summary>
        private static int Wrap(int value, int span)
        {
            int wrapped = value % span;
            return wrapped < 0 ? wrapped + span : wrapped;
        }

        /// <summary>
        /// The local heights of a set of muzzles, for handing to
        /// <see cref="Assign"/>. Missing entries read as zero, which puts them in
        /// whatever row the bottom of the bank is - a muzzle that is not there
        /// fires nothing either way, so it only has to not throw.
        /// </summary>
        public static float[] HeightsOf(Transform[] pivots)
        {
            if (pivots == null)
            {
                return new float[0];
            }

            float[] heights = new float[pivots.Length];
            for (int i = 0; i < pivots.Length; i++)
            {
                heights[i] = pivots[i] != null ? pivots[i].localPosition.y : 0f;
            }

            return heights;
        }
    }
}
