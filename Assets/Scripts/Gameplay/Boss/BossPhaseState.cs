using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Which act the fight is in, and the two events that move it on, with no
    /// Unity object attached so the order can be tested directly.
    ///
    /// The same split <see cref="HealthState"/> makes. The rules here are all
    /// "once, and never backwards" rules, and those are exactly the ones that are
    /// impossible to check by playing: a phase that can be re-entered looks
    /// identical to one that cannot until the one frame where two bullets arrive
    /// together.
    ///
    /// Health is not held here. The bar shows one 300-point pool and the
    /// emplacements spend the first half of it, so there is only ever one number,
    /// and it lives with the emitter that owns the bar. This class is told what
    /// that number is rather than keeping a second copy of it.
    /// </summary>
    public sealed class BossPhaseState
    {
        private readonly int scuttleThreshold;
        private int emplacementsStanding;

        /// <param name="emplacements">
        /// How many weak points guard the hull. Zero starts the fight Exposed,
        /// which is what a boss authored without emplacements should do rather
        /// than being invulnerable forever.
        /// </param>
        /// <param name="scuttleThreshold">
        /// The health at or below which the last act begins. Only ever reached
        /// after the hull is exposed, since the armoured phase cannot take the
        /// health that low.
        /// </param>
        public BossPhaseState(int emplacements, int scuttleThreshold)
        {
            emplacementsStanding = Mathf.Max(0, emplacements);
            this.scuttleThreshold = Mathf.Max(0, scuttleThreshold);

            Phase = emplacementsStanding > 0 ? BossPhase.Armoured : BossPhase.Exposed;
        }

        /// <summary>The act the fight is in.</summary>
        public BossPhase Phase { get; private set; }

        /// <summary>How many emplacements are still firing.</summary>
        public int EmplacementsStanding => emplacementsStanding;

        /// <summary>
        /// Whether shots that hit the hull do anything.
        ///
        /// The gate the whole first act rests on: while this is false the 300
        /// points on the bar can only be spent through the emplacements, so
        /// "shoot the boss" stops being an answer and "shoot the right part of
        /// the boss" becomes one.
        /// </summary>
        public bool HullVulnerable => Phase != BossPhase.Armoured;

        /// <summary>
        /// Records one emplacement wrecked, and reports whether that was the last
        /// one - which is the moment the fight changes shape, and the caller has
        /// work to do for it that it has nothing to do on the other two.
        /// </summary>
        public bool ReportEmplacementDestroyed()
        {
            if (emplacementsStanding <= 0)
            {
                return false;
            }

            emplacementsStanding--;

            if (emplacementsStanding > 0)
            {
                return false;
            }

            return Advance(BossPhase.Exposed);
        }

        /// <summary>
        /// Offers the current health, and reports whether it started the last act.
        ///
        /// Called every time the bar moves rather than on a threshold the caller
        /// has to know about, so the "last thirty points" rule lives in one place.
        /// Refused while armoured: the hull takes no damage then, so health at the
        /// threshold during the first act would mean the emplacements were
        /// authored to cost more than the boss has, and dropping straight to the
        /// finale is not the right answer to that.
        /// </summary>
        public bool ReportHealth(int current)
        {
            if (Phase != BossPhase.Exposed || current > scuttleThreshold)
            {
                return false;
            }

            return Advance(BossPhase.Scuttle);
        }

        /// <summary>
        /// Moves to a later act, and only to a later one.
        ///
        /// Returns whether the phase actually changed, so every caller above can
        /// use it as "did I just cause this" without also having to remember what
        /// the phase was before it asked.
        /// </summary>
        private bool Advance(BossPhase next)
        {
            if (next <= Phase)
            {
                return false;
            }

            Phase = next;
            return true;
        }
    }
}
