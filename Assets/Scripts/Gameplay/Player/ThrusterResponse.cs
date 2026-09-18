using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// How hard each of the ship's flares burns, as arithmetic with no Unity
    /// object attached.
    ///
    /// The same split <see cref="DashCycle"/> and <see cref="ShipMotion"/> make.
    /// A thruster is a visual, so the only way to check it is to look at it - but
    /// "does a backward dash light the retros rather than the main engine" is a
    /// question about a sign, and answering that by playing the game is how a
    /// flipped ship ends up flying with its exhaust out of the nose.
    /// </summary>
    public static class ThrusterResponse
    {
        /// <summary>
        /// The throttle as the model's own nose sees it.
        ///
        /// The flip turns the model around without changing which way the input
        /// drives the ship, so after it the same key that used to mean "forward"
        /// means "backward" to the hull the flares are bolted to. This is the
        /// same mirror <see cref="SpaceShipPitch"/> applies to the lean, for the
        /// same reason, and the two have to agree: a ship that leans into a
        /// thrust its engine is not making reads as broken even when nobody can
        /// say why.
        /// </summary>
        public static float Noseward(float horizontal, bool flipped)
        {
            return flipped ? -horizontal : horizontal;
        }

        /// <summary>
        /// How hard one nozzle burns, 0 to 1 at the throttle and higher during a
        /// dash. Fed <see cref="Noseward"/> for the main engine and its negative
        /// for the retros, so one rule covers both and they cannot disagree.
        /// </summary>
        /// <param name="towardNozzle">
        /// Throttle in the direction this nozzle pushes against: positive means
        /// this nozzle is the one doing the work.
        /// </param>
        /// <param name="dashFloor">
        /// What a nozzle burns at during a dash that is not asking it for
        /// anything. A straight climb or dive leaves both throttle axes reading
        /// zero, and a dash that lit nothing would be the one manoeuvre in the
        /// game with no engine behind it.
        ///
        /// The floor fades out as the burst turns against this nozzle, so a dash
        /// along the ring burns the engine it would actually burn and leaves the
        /// other dark. Seen in play on 18 September 2026 without that: a forward
        /// dash fired the retros as well, and two green spikes out of the nose
        /// read as a weapon rather than as thrust.
        /// </param>
        /// <param name="dashBoost">
        /// Multiplies the whole burst. Above 1 on purpose: the flare is allowed
        /// to overshoot its authored length, which is what makes a dash look like
        /// more than a hard press of the throttle.
        /// </param>
        public static float NozzleLevel(float towardNozzle, bool dashing, float dashFloor, float dashBoost)
        {
            float held = Mathf.Clamp01(towardNozzle);

            if (!dashing)
            {
                return held;
            }

            // How much the burst is aimed the other way, which is how much of the
            // floor this nozzle gives up.
            float opposed = Mathf.Clamp01(-towardNozzle);
            float floor = Mathf.Clamp01(dashFloor) * (1f - opposed);

            return Mathf.Max(held, floor) * Mathf.Max(1f, dashBoost);
        }

        /// <summary>
        /// The ready light's level, from <see cref="PlayerDash.ReadyFraction"/>.
        ///
        /// Dark the instant a dash is spent, filling as the cooldown comes back,
        /// and full only once it is actually ready. The fill is deliberately dim -
        /// it says how long is left, and the thing the player has to read at a
        /// glance is the difference between "not yet" and "now", not the exact
        /// remainder. The bar in the corner already carries the remainder, and
        /// the whole point of putting this on the hull is that it is read without
        /// looking away from the ship.
        /// </summary>
        /// <param name="carry">
        /// How bright the light gets at the very end of the cooldown, as a
        /// fraction of ready. 0 keeps it dark until the moment it comes back.
        /// </param>
        public static float ReadyLevel(float readyFraction, float carry)
        {
            float f = Mathf.Clamp01(readyFraction);
            return f >= 1f ? 1f : Mathf.Clamp01(carry) * f;
        }

        /// <summary>
        /// The flash on the frame the dash comes back, decaying to nothing over
        /// <paramref name="popSeconds"/>. A multiplier of 1 or more, so it can be
        /// applied to <see cref="ReadyLevel"/> without a branch.
        ///
        /// A step from dark to lit is legible in a still and easy to miss in
        /// motion, because the eye is on the boss. An overshoot that decays is
        /// the same event with a leading edge, which is what gets noticed at the
        /// edge of vision.
        /// </summary>
        public static float ReadyPop(float secondsSinceReady, float popSeconds, float popStrength)
        {
            if (popSeconds <= 0f || secondsSinceReady < 0f || secondsSinceReady >= popSeconds)
            {
                return 1f;
            }

            return 1f + Mathf.Max(0f, popStrength) * (1f - secondsSinceReady / popSeconds);
        }
    }
}
