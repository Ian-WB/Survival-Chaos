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

        /// <summary>
        /// The ceiling. Settable only through <see cref="RaiseMax"/>, so a skill
        /// cannot leave current health above a maximum that no longer allows it.
        /// </summary>
        public int Max { get; private set; }

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

        /// <summary>
        /// Restores health, never past <see cref="Max"/>.
        ///
        /// Refused once dead. A heal arriving after the killing hit - a level-up
        /// resolving in the same frame as a collision - would otherwise put the
        /// entity back above zero while the death screen was already up.
        /// </summary>
        public void Heal(int amount)
        {
            if (killReported || amount <= 0)
            {
                return;
            }

            current += amount;

            if (current > Max)
            {
                current = Max;
            }
        }

        /// <summary>
        /// Raises the ceiling, granting the same amount as current health.
        ///
        /// Both move together because that is what the Max Health skill promises:
        /// raising the ceiling alone would hand the player a bar that got longer
        /// without making them any harder to kill.
        /// </summary>
        public void RaiseMax(int amount)
        {
            if (killReported || amount <= 0)
            {
                return;
            }

            Max += amount;
            current += amount;
        }
    }
}
