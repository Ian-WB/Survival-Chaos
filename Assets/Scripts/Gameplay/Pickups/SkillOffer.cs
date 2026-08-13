using System.Collections.Generic;

namespace SurvivalChaos
{
    /// <summary>
    /// The set of pickups put on the ring by one level-up, and the rule that
    /// binds them: taking one gives up the rest.
    ///
    /// This is what turns a reward into a choice. Without it three pickups are
    /// just three rewards spread out, and the player collects all of them on a
    /// lap - slower than an automatic grant but no more interesting.
    ///
    /// Offers are independent of each other. Levelling again while one is still
    /// live puts a second set out rather than replacing the first, because
    /// replacing it would quietly delete an upgrade the player had earned.
    /// </summary>
    public sealed class SkillOffer
    {
        private readonly List<Pickup> members = new List<Pickup>();

        /// <summary>True once a member has been taken or the whole set has run out.</summary>
        public bool Resolved { get; private set; }

        /// <summary>How many of this offer's pickups are still on the ring.</summary>
        public int LiveCount => members.Count;

        public void Add(Pickup pickup)
        {
            if (pickup == null)
            {
                return;
            }

            pickup.Offer = this;
            members.Add(pickup);
        }

        /// <summary>
        /// Drops one member from the set without resolving the offer. Used when a
        /// single pickup expires on its own - the rest of the offer is still
        /// live, and the player can still answer it.
        /// </summary>
        /// <returns>True when that was the last one, so the offer is now spent.</returns>
        public bool Remove(Pickup pickup)
        {
            members.Remove(pickup);

            if (members.Count == 0)
            {
                Resolved = true;
            }

            return Resolved;
        }

        /// <summary>
        /// Marks the offer answered and hands back the members that were not
        /// taken, for the caller to clear off the ring.
        ///
        /// The taken pickup is excluded rather than assumed to be absent, so this
        /// is correct whether it has already been removed or not.
        /// </summary>
        public IEnumerable<Pickup> Claim(Pickup taken)
        {
            Resolved = true;

            var forfeited = new List<Pickup>();

            foreach (Pickup member in members)
            {
                if (member != null && member != taken)
                {
                    forfeited.Add(member);
                }
            }

            members.Clear();
            return forfeited;
        }
    }
}
