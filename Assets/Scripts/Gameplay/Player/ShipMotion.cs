using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Easing helpers for ship animation, with no Unity object attached so the
    /// curves can be tested directly.
    /// </summary>
    public static class ShipMotion
    {
        /// <summary>
        /// Eases a value toward a target at a rate independent of frame rate.
        ///
        /// A plain Lerp(current, target, response * deltaTime) is the usual way
        /// to write this and is subtly wrong: the amount eased per second then
        /// depends on how many frames that second contained, so the ship feels
        /// different at 30fps and 144fps. The exponential form below settles the
        /// same amount per unit of time regardless.
        /// </summary>
        /// <param name="response">
        /// Higher is snappier. 0 or less snaps instantly, which is a convenient
        /// way to switch the easing off from the inspector.
        /// </param>
        public static float Approach(float current, float target, float response, float deltaTime)
        {
            if (response <= 0f)
            {
                return target;
            }

            if (deltaTime <= 0f)
            {
                return current;
            }

            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-response * deltaTime));
        }

        /// <summary>
        /// Turns an angle toward a target at a fixed rate, taking the short way
        /// around. Used for the 180 degree flip so it sweeps instead of snapping.
        /// </summary>
        public static float ApproachAngle(float current, float target, float degreesPerSecond, float deltaTime)
        {
            if (degreesPerSecond <= 0f || deltaTime <= 0f)
            {
                return degreesPerSecond <= 0f ? target : current;
            }

            return Mathf.MoveTowardsAngle(current, target, degreesPerSecond * deltaTime);
        }
    }
}
