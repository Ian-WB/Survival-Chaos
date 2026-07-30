using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Hit points for one entity, with no Unity object attached so the rules can
    /// be tested directly.
    ///
    /// The key rule is that only the hit which brings health to zero reports a
    /// kill. The original enemy scripts had no such guard: they destroyed the
    /// object in OnTriggerEnter but awarded experience from Update, relying on
    /// Destroy being deferred past the end of the frame for the reward to land
    /// at all - and on nothing else re-entering the death branch first.
    /// </summary>
    public sealed class HealthState
    {
        private int current;
        private bool killReported;

        public HealthState(int maxHealth)
        {
            Max = Mathf.Max(1, maxHealth);
            current = Max;
        }

        public int Max { get; }

        public int Current => current;

        public bool IsDead => current <= 0;

        /// <summary>
        /// Applies damage.
        /// </summary>
        /// <returns>
        /// True only for the hit that kills, and only once - so death effects
        /// and experience cannot fire twice.
        /// </returns>
        public bool TakeDamage(int amount)
        {
            if (killReported)
            {
                return false;
            }

            current -= Mathf.Max(0, amount);

            if (!IsDead)
            {
                return false;
            }

            killReported = true;
            return true;
        }
    }
}
