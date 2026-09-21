using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// How a boss torpedo steers after the player's height, with no Unity object
    /// attached so the chase can be tested directly.
    ///
    /// The crown's rounds used to hold the height they were fired at, and all
    /// four of a row eased onto the same point of the lane, so a row flew as one
    /// stacked bullet. As torpedoes each one steers on its own, which pulls them
    /// apart as soon as they leave.
    ///
    /// Delayed on purpose, because a torpedo that tracks perfectly cannot be
    /// dodged, only outrun, and one that cannot be outrun is not fair. So it
    /// steers in two stages. It keeps its own idea of where the player is - the
    /// ghost - which only catches up with the real height over a few tenths of a
    /// second; and it turns toward the ghost no faster than a fixed climb rate,
    /// kept well under the player's own. A player holding a height gets hit. A
    /// player who changes height as it closes leaves it chasing where they were.
    /// </summary>
    public static class TorpedoSteer
    {
        /// <summary>
        /// Advances one torpedo by <paramref name="delta"/> seconds.
        /// </summary>
        /// <param name="height">The torpedo's height, updated.</param>
        /// <param name="ghost">Where the torpedo believes the player is, updated.</param>
        /// <param name="target">Where the player actually is.</param>
        /// <param name="perception">How quickly the belief catches up, per second.</param>
        /// <param name="steer">How hard it turns toward its belief, per second.</param>
        /// <param name="maxClimb">The fastest it may climb or dive, units a second.</param>
        /// <returns>The vertical speed it flew at this step, for pointing its nose.</returns>
        public static float Step(ref float height, ref float ghost, float target,
                                 float perception, float steer, float maxClimb,
                                 float floor, float ceiling, float delta)
        {
            if (delta <= 0f)
            {
                return 0f;
            }

            // Exponential rather than linear, so the catch-up is the same shape
            // at any frame rate.
            ghost = Mathf.Lerp(ghost, target, 1f - Mathf.Exp(-Mathf.Max(0f, perception) * delta));

            float limit = Mathf.Max(0f, maxClimb);
            float climb = Mathf.Clamp((ghost - height) * Mathf.Max(0f, steer), -limit, limit);

            float before = height;
            height += climb * delta;

            // The band's own limits, which the player's centre is clamped to as
            // well: a torpedo has no business chasing into space the player
            // cannot occupy.
            if (ceiling > floor)
            {
                height = Mathf.Clamp(height, floor, ceiling);
            }

            return (height - before) / delta;
        }

        /// <summary>
        /// Degrees to pitch a torpedo's nose, from how fast it climbs against how
        /// fast it travels round the ring. Positive noses up.
        /// </summary>
        public static float Pitch(float climb, float alongRing)
        {
            if (Mathf.Abs(alongRing) < 0.0001f)
            {
                return 0f;
            }

            return Mathf.Atan(climb / Mathf.Abs(alongRing)) * Mathf.Rad2Deg;
        }
    }
}
