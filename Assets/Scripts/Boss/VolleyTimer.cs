using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Decides when a volley is due, with no Unity object attached so the
    /// cadence can be tested directly.
    ///
    /// An interval of 0 fires on every call. That is what the boss laser did -
    /// it was invoked straight from Update with no timer at all, so it emitted
    /// one volley per frame.
    /// </summary>
    public sealed class VolleyTimer
    {
        private float nextFireTime;

        public VolleyTimer(float startTime, float initialDelay)
        {
            nextFireTime = startTime + Mathf.Max(0f, initialDelay);
        }

        /// <summary>When the next volley becomes due.</summary>
        public float NextFireTime => nextFireTime;

        /// <summary>
        /// Reports whether a volley is due now, and if so schedules the next one.
        /// </summary>
        /// <remarks>
        /// The next volley is scheduled relative to now rather than to the time
        /// it was due, so a frame spike delays the cadence instead of firing a
        /// burst to catch up.
        /// </remarks>
        public bool TryFire(float now, float interval)
        {
            if (now < nextFireTime)
            {
                return false;
            }

            nextFireTime = now + Mathf.Max(0f, interval);
            return true;
        }
    }
}
