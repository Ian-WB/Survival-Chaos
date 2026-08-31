namespace SurvivalChaos
{
    /// <summary>
    /// Which of the fight's three acts the boss is in.
    ///
    /// The order is the order they happen in and nothing goes backwards, which is
    /// what lets <see cref="BossPhaseState"/> compare them as numbers.
    /// </summary>
    public enum BossPhase
    {
        /// <summary>
        /// The hull is invulnerable and the three emplacements are the only
        /// targets. Every attack the boss has is tied to one of them.
        /// </summary>
        Armoured = 0,

        /// <summary>
        /// Every emplacement is wrecked, so every gun is silent and the hull
        /// itself takes damage. The boss stops shooting and starts charging.
        /// </summary>
        Exposed = 1,

        /// <summary>
        /// The last of its health. Every magazine still aboard goes off at once,
        /// wrecked emplacements included.
        /// </summary>
        Scuttle = 2,
    }

    /// <summary>
    /// The phases an attack is allowed to fire in.
    ///
    /// A mask rather than a single phase because the interesting authoring is
    /// "this one carries over" - and because the alternative, one attack list per
    /// phase, would mean three inspector lists that mostly repeat each other.
    /// </summary>
    [System.Flags]
    public enum BossPhaseMask
    {
        None = 0,
        Armoured = 1 << 0,
        Exposed = 1 << 1,
        Scuttle = 1 << 2,
        All = Armoured | Exposed | Scuttle,
    }

    public static class BossPhaseMasks
    {
        /// <summary>Whether a mask allows a phase.</summary>
        public static bool Includes(this BossPhaseMask mask, BossPhase phase)
        {
            return (mask & Of(phase)) != 0;
        }

        /// <summary>The single-phase mask for a phase.</summary>
        public static BossPhaseMask Of(BossPhase phase)
        {
            switch (phase)
            {
                case BossPhase.Exposed: return BossPhaseMask.Exposed;
                case BossPhase.Scuttle: return BossPhaseMask.Scuttle;
                default: return BossPhaseMask.Armoured;
            }
        }
    }
}
