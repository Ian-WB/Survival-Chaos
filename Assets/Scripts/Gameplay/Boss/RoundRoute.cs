using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The path a boss round weaves through height as it travels, with no Unity
    /// object attached so the shape can be tested directly.
    ///
    /// Every boss round used to hold the height it was fired at for its whole
    /// flight, so a bank's fire settled into flat horizontal streams, and two
    /// banks firing the same way round the ring read as one lane - which is what
    /// the playtest said. A route is a sine on top of that height, which keeps
    /// the two things the armoured act depends on: a round starts exactly at its
    /// muzzle, and it never wanders further than its amplitude from the height
    /// of the bank that fired it. Armoured is a fight about height, and a route
    /// that swept across the whole band would erase the reason to fly to an
    /// emplacement.
    /// </summary>
    public static class RoundRoute
    {
        /// <summary>
        /// Height above the firing height, <paramref name="elapsed"/> seconds
        /// into the flight.
        ///
        /// Zero at launch for both phases the emitter uses: 0 sets off upward
        /// and pi sets off downward from the same muzzle, which is what makes
        /// two rows of one volley cross rather than bob together.
        /// </summary>
        public static float Offset(float amplitude, float period, float phase, float elapsed)
        {
            if (period <= 0f || amplitude == 0f)
            {
                return 0f;
            }

            return amplitude * Mathf.Sin(elapsed / period * 2f * Mathf.PI + phase);
        }

        /// <summary>
        /// The phase one row of a volley flies at.
        ///
        /// Without crossing every row takes the same phase, so the volley weaves
        /// as one shape and a gap in it stays a gap. With it, alternate rows go
        /// opposite ways: rows 0 and 1 close on each other and cross, and so do 2
        /// and 3, while 1 and 2 open apart.
        /// </summary>
        public static float PhaseForRow(int row, bool crossing)
        {
            return crossing && (row & 1) == 1 ? Mathf.PI : 0f;
        }
    }
}
