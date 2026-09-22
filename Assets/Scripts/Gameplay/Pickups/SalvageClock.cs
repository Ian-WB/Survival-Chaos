using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Paces salvage: how often something the player destroys leaves repair
    /// scrap behind.
    ///
    /// Charges while the player is hurt, faster the more health is missing, and
    /// is spent by the next wreck once full. Paced by time rather than by odds
    /// on each kill, because kills are not a steady supply: the two full bot runs
    /// on 22 September averaged two a second and one a second. Odds per kill
    /// would pay out on how fast the player kills rather than on how hurt they
    /// are, and bury the thickest waves in scrap.
    ///
    /// No Unity object attached, so the pacing can be tested directly.
    /// </summary>
    public sealed class SalvageClock
    {
        /// <summary>0 just spent, 1 due.</summary>
        public float Charge { get; private set; }

        /// <summary>Whether the next wreck should leave a piece.</summary>
        public bool Ready => Charge >= 1f;

        /// <summary>
        /// Runs the clock over a stretch of play at the given health.
        ///
        /// In proportion to the share of health missing, so the wait between
        /// pieces is <paramref name="secondsAtHalfHealth"/> at half health, twice
        /// that with a quarter missing and two thirds of it with three quarters
        /// missing. Nothing at full health, and nothing once dead.
        ///
        /// Stops at full rather than banking. A hurt player who destroys nothing
        /// for a minute is owed one piece by the next wreck, not a pile of them.
        /// </summary>
        public void Advance(float seconds, int current, int max, float secondsAtHalfHealth)
        {
            if (seconds <= 0f || current <= 0 || current >= max)
            {
                return;
            }

            float missing = (max - current) / (float)max;
            float rate = missing / (0.5f * Mathf.Max(0.01f, secondsAtHalfHealth));
            Charge = Mathf.Min(1f, Charge + seconds * rate);
        }

        /// <summary>
        /// Spends a full clock on a wreck.
        ///
        /// Refused, with nothing spent, when the clock is not full or there is
        /// nothing to repair. A Max HP pick heals as it raises the ceiling, so a
        /// player can be put back at full health by a pickup while the clock is
        /// due; the charge waits for the next time they need it rather than
        /// being thrown away on a piece that would restore nothing.
        /// </summary>
        public bool TrySpend(int current, int max)
        {
            if (!Ready || current <= 0 || current >= max)
            {
                return false;
            }

            Charge = 0f;
            return true;
        }
    }
}
