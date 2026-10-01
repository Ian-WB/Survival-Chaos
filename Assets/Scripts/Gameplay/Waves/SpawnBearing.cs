using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Where round the ring an enemy may arrive.
    ///
    /// Until 30 September 2026 any bearing at all: measured with the bot, 57
    /// of 85 spawns were already in view on their first frame, and one
    /// appeared 3.9 units from the ship, too close to dodge. Now the stretch of
    /// ring either side of the ship is kept clear, and everywhere else stays
    /// equally likely.
    ///
    /// Kept free of scene types so it can be tested directly, like
    /// <see cref="SpawnMath"/>.
    /// </summary>
    public static class SpawnBearing
    {
        /// <summary>
        /// A bearing in degrees, -180 to 180, at least <paramref name="clearDegrees"/>
        /// from <paramref name="shipBearing"/> either way, spread evenly over the rest.
        /// </summary>
        /// <param name="random01">A random number in 0..1, taken as an argument so tests can pick it.</param>
        public static float Pick(float random01, float shipBearing, float clearDegrees)
        {
            float clear = Mathf.Clamp(clearDegrees, 0f, 179f);
            float open = 360f - 2f * clear;
            float bearing = shipBearing + clear + Mathf.Clamp01(random01) * open;
            return Mathf.DeltaAngle(0f, bearing);
        }
    }
}
