using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Slow Mo's two windows - the slowdown itself and the wait before the
    /// next one - with no Unity object attached so both can be tested
    /// directly. The same split <see cref="DashCycle"/> makes for the dash.
    ///
    /// Unlike the dash, the wait is measured from the start of one use to the
    /// start of the next, not from the end: "every 30 seconds" means a use can
    /// begin 30 seconds after the last one began. A cooldown shorter than the
    /// slowdown itself counts as the slowdown's length, so a use can never
    /// begin while one is still running.
    ///
    /// The clock is the caller's. <see cref="PlayerSlowMo"/> feeds it seconds
    /// of unpaused real time, because the game's own clock is the thing being
    /// slowed: three seconds of game time at 0.4 would last seven and a half.
    /// </summary>
    public sealed class SlowMoCycle
    {
        private readonly float duration;
        private float cooldown;

        /// <summary>
        /// When the current use began. Negative infinity for never, as in
        /// DashCycle: it puts every window infinitely far in the past, which is
        /// exactly "not slowing, and ready".
        /// </summary>
        private float startedAt = float.NegativeInfinity;

        public SlowMoCycle(float duration, float cooldown)
        {
            this.duration = Mathf.Max(0f, duration);
            this.cooldown = Mathf.Max(0f, cooldown);
        }

        /// <summary>How long one slowdown lasts, in seconds.</summary>
        public float Duration => duration;

        /// <summary>Seconds from the start of one use to the start of the next.</summary>
        public float Cooldown => cooldown;

        /// <summary>
        /// Changes the wait, for a Slow Mo pick. Applies to a wait already
        /// running, which is measured from a start that does not move, so a
        /// shorter one simply ends sooner.
        /// </summary>
        public void SetCooldown(float seconds)
        {
            cooldown = Mathf.Max(0f, seconds);
        }

        /// <summary>The whole cycle: never shorter than the slowdown it contains.</summary>
        private float Period => Mathf.Max(cooldown, duration);

        /// <summary>True while the game should be slowed.</summary>
        public bool IsSlowing(float now)
        {
            return now < startedAt + duration;
        }

        /// <summary>True when a use may begin.</summary>
        public bool IsReady(float now)
        {
            return now >= startedAt + Period;
        }

        /// <summary>
        /// Begins a use if one is allowed, and reports whether it did, the way
        /// DashCycle.TryBegin does.
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
        /// What the HUD bar shows, 0 to 1: full when ready, draining over the
        /// slowdown so the player can see how much of it is left, then filling
        /// again over the rest of the wait.
        ///
        /// Two readings in one number because they never overlap, and a player
        /// in the middle of a slowdown wants to know how long it lasts, not how
        /// long until the next.
        /// </summary>
        public float Gauge(float now)
        {
            float since = now - startedAt;

            if (since >= Period)
            {
                return 1f;
            }

            if (since < duration)
            {
                return 1f - (since / duration);
            }

            float refill = Period - duration;
            return refill > 0f ? Mathf.Clamp01((since - duration) / refill) : 1f;
        }

        /// <summary>Back to never used, for a life ending.</summary>
        public void Reset()
        {
            startedAt = float.NegativeInfinity;
        }
    }
}
