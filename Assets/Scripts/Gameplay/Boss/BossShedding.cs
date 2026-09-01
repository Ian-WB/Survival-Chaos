namespace SurvivalChaos
{
    /// <summary>
    /// Which wrecked emplacement the next piece of hull comes off, with no Unity
    /// object attached so the arithmetic can be tested directly.
    ///
    /// The same rule the curtain's gap follows, for the same reason: stepping
    /// through the sources in order makes the second act's hazards learnable,
    /// where picking one at random would make two identical-looking fights play
    /// differently and neither of them teach anything. Here it also spreads the
    /// wreckage across the three heights rather than piling it on one, which is
    /// the whole reason the pattern exists.
    ///
    /// Counted over the wrecked entries rather than over all of them. The
    /// alternative - index by <c>volley % length</c> and skip when that lands on
    /// one still standing - looks equivalent and is not: it would shed nothing on
    /// those volleys, so a fight where the player killed the pods in a different
    /// order would produce a different amount of wreckage.
    /// </summary>
    public static class BossShedding
    {
        /// <summary>
        /// The emplacement to shed from on a given volley, or -1 when there is
        /// nothing wrecked to shed from yet.
        /// </summary>
        /// <param name="wrecked">One flag per emplacement, in rig order.</param>
        /// <param name="volley">
        /// How many volleys this attack has fired. Taken modulo the number of
        /// wrecked emplacements, and guarded against a negative because the
        /// counter is a plain int and this is the one place that would turn an
        /// overflow into an exception rather than into a wrong answer.
        /// </param>
        public static int SourceIndex(bool[] wrecked, int volley)
        {
            if (wrecked == null)
            {
                return -1;
            }

            int available = 0;

            for (int i = 0; i < wrecked.Length; i++)
            {
                if (wrecked[i])
                {
                    available++;
                }
            }

            if (available == 0)
            {
                return -1;
            }

            int target = ((volley % available) + available) % available;

            for (int i = 0; i < wrecked.Length; i++)
            {
                if (!wrecked[i])
                {
                    continue;
                }

                if (target == 0)
                {
                    return i;
                }

                target--;
            }

            return -1;
        }
    }
}
