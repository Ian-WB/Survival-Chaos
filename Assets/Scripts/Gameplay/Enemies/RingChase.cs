using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Which way round the ring an enemy should travel to close on the player.
    ///
    /// This replaces six trigger volumes per enemy prefab. Each was a box that
    /// set <see cref="EnemyMovement.TravellingLeft"/> when the player entered
    /// it, three to a side, and between them they answered the question badly:
    /// two of the six could never fire at all, because they sat 87 units along
    /// the tangent from a point on a 137-unit circle and by that distance the
    /// arc has bowed some 26 units clear of a box only 16.5 deep. The four that
    /// did fire covered 25.6 degrees of the 360 between them. For the rest of
    /// the ring an enemy simply kept whatever direction it last had.
    ///
    /// None of that was rescale damage - radius, offsets and box sizes all
    /// scaled by ten together, so the coverage was the same before the arena
    /// grew. It was a proximity test standing in for an angle comparison.
    ///
    /// Both bodies are pinned to the same circle - <see cref="SnapToOrbit"/>
    /// puts the player on the enemy lane at a radius offset of zero - so the
    /// answer is exact arithmetic on two bearings, and this is where it lives
    /// so it can be tested without an arena.
    /// </summary>
    public static class RingChase
    {
        /// <summary>
        /// How far past the decision point the player has to be before an enemy
        /// commits to turning round. Roughly what the innermost pair of trigger
        /// volumes used to impose, which fired from about 20 degrees out.
        /// </summary>
        public const float DefaultDeadbandDegrees = 20f;

        /// <summary>
        /// The direction that closes the shorter way round, or the direction
        /// already held where the answer is not clear enough to act on.
        ///
        /// Two zones are ambiguous, and both would chatter without a band. On
        /// top of the player the separation passes through zero, so the shorter
        /// way flips every time the enemy overtakes; at the antipode it passes
        /// through 180 and flips every time the player drifts across the far
        /// side. Either produces an enemy that vibrates rather than turns. In
        /// both, holding course is also the honest answer - there is no shorter
        /// way round worth the name.
        /// </summary>
        /// <param name="enemyBearing">
        /// The enemy's bearing around the arena axis, as
        /// <see cref="PickupPlacement.BearingOf"/> measures it.
        /// </param>
        /// <param name="playerBearing">The player's bearing, measured the same way.</param>
        /// <param name="current">
        /// The direction being travelled now, returned unchanged inside either
        /// ambiguous zone. This is what makes the result hysteretic rather than
        /// a bare comparison.
        /// </param>
        /// <param name="deadbandDegrees">
        /// Half-width of both ambiguous zones. Clamped below 90, because at 90
        /// the two would meet and the enemy could never turn again.
        /// </param>
        public static bool ShouldTravelLeft(
            float enemyBearing,
            float playerBearing,
            bool current,
            float deadbandDegrees)
        {
            float delta = Mathf.DeltaAngle(enemyBearing, playerBearing);
            float separation = Mathf.Abs(delta);
            float band = Mathf.Clamp(deadbandDegrees, 0f, 89f);

            if (separation <= band || separation >= 180f - band)
            {
                return current;
            }

            // A positive rotation about +Y carries +Z towards +X, which is the
            // direction BearingOf counts in, so travelling left raises the
            // enemy's bearing. The player being at the higher bearing is
            // therefore what asks for it.
            return delta > 0f;
        }
    }
}
