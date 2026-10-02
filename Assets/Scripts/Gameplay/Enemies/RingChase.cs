using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Which way round the ring an enemy should travel to close on the player.
    ///
    /// This replaces six trigger volumes per enemy prefab. Each was a box that
    /// set <see cref="EnemyMovement.TravellingLeft"/> when the player entered
    /// it, three to a side, and between them they answered the question badly:
    /// two of the six could never fire at all, because they sat 8.7 units along
    /// the tangent from a point on a 13.7-unit circle and by that distance the
    /// arc has bowed some 2.6 units clear of a box only 1.65 deep. The four that
    /// did fire covered 25.6 degrees of the 360 between them. For the rest of
    /// the ring an enemy simply kept whatever direction it last had.
    ///
    /// None of that was rescale damage, and the world has since been ten times
    /// this size and back - radius, offsets and box sizes all scale together, so
    /// the coverage was identical at every scale. It was a proximity test
    /// standing in for an angle comparison.
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

        /// <summary>
        /// Whether the enemy and the player went past each other between two
        /// frames, given the signed separation <see cref="Mathf.DeltaAngle"/>
        /// measured on each.
        ///
        /// The sign flips in two places, and only one of them is a pass. Close
        /// in it means one overtook the other; at the antipode it means the
        /// player drifted across the far side of the ring, and nobody went past
        /// anybody. A quarter of the ring either side tells them apart.
        /// </summary>
        public static bool Passed(float previousDelta, float delta)
        {
            if (Mathf.Abs(previousDelta) >= 90f || Mathf.Abs(delta) >= 90f)
            {
                return false;
            }

            return (previousDelta > 0f && delta < 0f) || (previousDelta < 0f && delta > 0f);
        }

        /// <summary>
        /// The separation to compare the next frame's against: this frame's,
        /// unless it is exactly zero. Dead level is neither side, so a crossing
        /// sampled as +1, 0, -1 was two comparisons that each saw no change of
        /// side, and the pass went uncounted (scan of 2 October 2026). Keeping
        /// the side it came from counts it once, and one that touches level and
        /// goes back the way it came is still no pass.
        /// </summary>
        public static float SideToRemember(float previousDelta, float delta)
        {
            return delta == 0f ? previousDelta : delta;
        }

        /// <summary>
        /// Whether an enemy the player last went past at
        /// <paramref name="lastPass"/> may turn round at <paramref name="now"/>.
        ///
        /// The deadband above decides how far past the player an enemy goes
        /// before it turns; this decides how long after a pass it holds course.
        /// They are different questions. A boss faster than the player, with
        /// only the band, passes them, goes 20 degrees on, turns, passes them
        /// again and turns again - at 20 degrees a second, a turn every two
        /// seconds for as long as the player holds still, and the player is
        /// never out from under it. A cooldown longer than that carries it
        /// further on after each pass, which is the room a player who got past
        /// it has earned.
        ///
        /// Counted from the pass, not from the last turn. It was the turn until
        /// 1 October 2026, and that gave the first pass of a fight no room at
        /// all, there being no turn to count from, and every pass after it 20
        /// and 40 degrees alternately.
        ///
        /// An enemy nobody has passed gives <see cref="float.NegativeInfinity"/>,
        /// and may turn at once: one that arrives facing the wrong way is not
        /// holding course for anyone.
        /// </summary>
        public static bool MayTurn(float now, float lastPass, float cooldownSeconds)
        {
            return now - lastPass >= Mathf.Max(0f, cooldownSeconds);
        }
    }
}
