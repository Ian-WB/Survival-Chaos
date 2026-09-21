using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// How brightly each muzzle glows while a volley is being announced, with
    /// no Unity object attached so the warning can be tested directly.
    ///
    /// The lance and the ram have always warned you. The curtain and the rake,
    /// which are most of the armoured act, arrived unannounced, and the
    /// playtest's answer to a boss that reads as scenery was clearer warnings
    /// rather than more damage. So the muzzles that are about to fire light up
    /// first - and because which muzzles fire is the whole shape of these two
    /// attacks, the glow says more than "something is coming". A curtain's dark
    /// row is the gap, visible before the wall leaves. A rake lights in the order
    /// it will fire, so the direction of the staircase is visible too.
    /// </summary>
    public static class VolleyTell
    {
        /// <summary>
        /// Every muzzle that will fire, building together. For the curtain,
        /// which fires all its rows at once.
        /// </summary>
        public static float Together(float progress)
        {
            return Build(Mathf.Clamp01(progress));
        }

        /// <summary>
        /// One muzzle of a volley that fires row by row, lighting in its turn:
        /// the row that fires first lights first, and the last reaches full at
        /// the end of the warning.
        /// </summary>
        /// <param name="order">When this muzzle's row fires, 0 first.</param>
        public static float InOrder(float progress, int order, int count)
        {
            if (count <= 0)
            {
                return 0f;
            }

            return Build(Mathf.Clamp01(Mathf.Clamp01(progress) * count - order));
        }

        /// <summary>
        /// Whether a rake climbs from the bottom row this volley or comes down
        /// from the top. Alternates, starting upward, so it is a staircase to
        /// dive under as often as one to climb over.
        /// </summary>
        public static bool Upward(int volley)
        {
            return (volley & 1) == 0;
        }

        /// <summary>
        /// The step at which a row fires. Its own inverse: the row fired at a
        /// given step is <c>FiringOrder(step, count, upward)</c> too.
        /// </summary>
        public static int FiringOrder(int row, int count, bool upward)
        {
            return upward ? row : count - 1 - row;
        }

        /// <summary>
        /// Slow to start and quick to finish, so the glow reads as charging
        /// rather than as a lamp being turned up - and the moment it peaks is
        /// the moment it fires.
        /// </summary>
        private static float Build(float t)
        {
            return t * t;
        }
    }
}
