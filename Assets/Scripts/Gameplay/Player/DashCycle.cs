using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The dash's two windows - the burst itself and the cooldown behind it -
    /// with no Unity object attached so both can be tested directly.
    ///
    /// The same split <see cref="VolleyTimer"/> makes, for the same reason: the
    /// question "may I dash, and am I dashing right now" is pure arithmetic on a
    /// timestamp, and answering it inside a MonoBehaviour would mean the only way
    /// to check the timing was to play the game.
    ///
    /// One timestamp holds both windows. A dash begun at t occupies
    /// [t, t + duration) and the next one is refused until t + duration +
    /// cooldown, so the cooldown is measured from when the burst ended rather
    /// than from when it started - which is what makes the authored cooldown mean
    /// "time on the ground between dashes" instead of "time between dashes,
    /// minus however long a dash happens to last".
    /// </summary>
    public sealed class DashCycle
    {
        private readonly float duration;
        private readonly float cooldown;

        /// <summary>
        /// When the current dash began. Negative infinity rather than a flag:
        /// every window below is an offset from this, and -inf puts all of them
        /// infinitely far in the past, which is exactly "has never dashed, and is
        /// ready". A bool would be a second thing to keep in step with it.
        /// </summary>
        private float startedAt = float.NegativeInfinity;

        public DashCycle(float duration, float cooldown)
        {
            this.duration = Mathf.Max(0f, duration);
            this.cooldown = Mathf.Max(0f, cooldown);
        }

        /// <summary>How long one burst lasts, in seconds.</summary>
        public float Duration => duration;

        /// <summary>Seconds after a burst ends before another may start.</summary>
        public float Cooldown => cooldown;

        /// <summary>True while a burst is in progress.</summary>
        public bool IsDashing(float now)
        {
            return now < startedAt + duration;
        }

        /// <summary>True when a burst may be started.</summary>
        public bool IsReady(float now)
        {
            return now >= startedAt + duration + cooldown;
        }

        /// <summary>
        /// Starts a burst if one is allowed, and reports whether it did. The
        /// caller has work to do on the frame a dash actually begins - capturing
        /// the heading, handing the boost to the movement - and nothing to do on
        /// the frames where the key was pressed against a cooldown.
        /// </summary>
        public bool TryBegin(float now)
        {
            if (!IsReady(now))
            {
                return false;
            }

            startedAt = now;
            return true;
        }

        /// <summary>
        /// How far the cooldown has recovered, 0 to 1. 1 means ready; 0 holds for
        /// the whole burst and the instant the cooldown starts.
        ///
        /// For a UI bar. Exposed as a fraction rather than as seconds remaining so
        /// a bar can read it without also having to know the cooldown length.
        /// </summary>
        public float ReadyFraction(float now)
        {
            if (cooldown <= 0f)
            {
                return IsDashing(now) ? 0f : 1f;
            }

            return Mathf.Clamp01((now - (startedAt + duration)) / cooldown);
        }

        /// <summary>
        /// Puts the cycle back to never-dashed. For a life ending rather than for
        /// normal play - a pooled or respawned ship should not inherit the
        /// cooldown the previous one died on.
        /// </summary>
        public void Reset()
        {
            startedAt = float.NegativeInfinity;
        }
    }
}
