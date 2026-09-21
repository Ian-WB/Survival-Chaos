using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The height of a boss round thrown at an angle, bouncing between the floor
    /// and the ceiling of the band the player can fly in. No Unity object is
    /// attached, so the path can be tested directly.
    ///
    /// Every boss round used to hold the height it was fired at for its whole
    /// flight, so the keel's discs and the crown's bullets ran round the ring as
    /// parallel streams - one lane, as the playtest put it. The discs are thrown
    /// now: they climb or dive as they travel, and turn back off the band's edges
    /// the way a ball comes off a wall, so their path cuts across the bullets'
    /// instead of running beside it.
    ///
    /// And each disc of a row is thrown its own way. The keel's four muzzles to
    /// a row sit at one height, in two pairs 0.27 apart against a disc 0.7
    /// across, and they all ease onto the same lane - so a row thrown at one
    /// angle still left as one stack, which is how it looked in play on
    /// 21 September 2026. <see cref="FanSpeed"/> spreads a row from steep up to
    /// steep down.
    ///
    /// Bounced off the player's own limits, which the player's centre is clamped
    /// to, so a disc reaching the ceiling reaches a player flying at it. Hugging
    /// an edge is not a way out.
    ///
    /// Worked out from the time since the throw rather than stepped a frame at a
    /// time, so a long frame cannot carry a disc through the ceiling, and the
    /// path is the same whatever the frame rate.
    /// </summary>
    public static class RoundRoute
    {
        /// <summary>
        /// Height of a round <paramref name="elapsed"/> seconds after it was
        /// thrown from <paramref name="start"/> at <paramref name="speed"/> units
        /// a second, positive upward.
        ///
        /// A round thrown from outside the band travels straight until it enters
        /// and bounces from then on. The boss's muzzles span more height than the
        /// band does, so its lowest can sit under the floor; a disc from there is
        /// thrown upward into the band by <see cref="SpeedInto"/>, and bouncing it
        /// in the instant it was fired would teleport it.
        /// </summary>
        public static float Height(float start, float speed, float elapsed, float floor, float ceiling)
        {
            float span = ceiling - floor;

            if (span <= 0f || speed == 0f || elapsed <= 0f)
            {
                return start + speed * Mathf.Max(0f, elapsed);
            }

            // Outside and not yet in: straight until the edge it is heading for.
            if (start < floor || start > ceiling)
            {
                float edge = start < floor ? floor : ceiling;
                float toEdge = (edge - start) / speed;

                // Heading away from the band. Nothing to bounce off, ever.
                if (toEdge < 0f)
                {
                    return start + speed * elapsed;
                }

                if (elapsed < toEdge)
                {
                    return start + speed * elapsed;
                }

                return Height(edge, speed, elapsed - toEdge, floor, ceiling);
            }

            // Inside: unfold the bounces. Travel up the band and back down is one
            // period of 2 x span, a triangle wave of the distance covered.
            float travelled = start - floor + speed * elapsed;
            float period = 2f * span;
            float along = travelled - period * Mathf.Floor(travelled / period);

            return floor + (along <= span ? along : period - along);
        }

        /// <summary>
        /// The vertical speed one muzzle of a row throws at: the first of the
        /// row steepest up, the last steepest down, the rest evenly between -
        /// four to a row come out at full, a third, minus a third and minus
        /// full. A row of one is thrown at full.
        ///
        /// Mirrored on alternate volleys, so the muzzle that threw up last time
        /// throws down this time and no one muzzle's path can be memorised as
        /// safe.
        /// </summary>
        /// <param name="slot">This muzzle's place in its row, 0 first.</param>
        /// <param name="slots">How many muzzles the row has.</param>
        public static float FanSpeed(float speed, int slot, int slots, int volley)
        {
            float spread = slots > 1 ? Mathf.Lerp(1f, -1f, Mathf.Clamp01(slot / (float)(slots - 1))) : 1f;
            float mirror = (volley & 1) == 0 ? 1f : -1f;

            return Mathf.Abs(speed) * spread * mirror;
        }

        /// <summary>
        /// The throw to use from <paramref name="start"/>: as given inside the
        /// band, and turned toward the band from outside it, so no disc is
        /// thrown straight out of the fight.
        /// </summary>
        public static float SpeedInto(float start, float speed, float floor, float ceiling)
        {
            float magnitude = Mathf.Abs(speed);

            if (start < floor)
            {
                return magnitude;
            }

            if (start > ceiling)
            {
                return -magnitude;
            }

            return speed;
        }
    }
}
