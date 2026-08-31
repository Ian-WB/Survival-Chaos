namespace SurvivalChaos
{
    /// <summary>
    /// What one volley of a <see cref="BossAttack"/> actually does.
    ///
    /// Every attack the boss had fired every muzzle it owned at the same instant,
    /// which is why all three read as the same attack at different heights. The
    /// patterns here are all the same muzzles - none of them adds a gun - and the
    /// difference between them is entirely in when each one goes off. That is the
    /// cheapest place to buy a boss fight: a slab of sixteen bullets and a
    /// staircase of sixteen bullets are the same volley and completely different
    /// problems.
    /// </summary>
    public enum BossFirePattern
    {
        /// <summary>
        /// Every muzzle at once. What all three attacks used to do, kept for the
        /// last act, where a wall of everything at once is the point.
        /// </summary>
        Simultaneous = 0,

        /// <summary>
        /// Every muzzle except one row, with the open row stepping upward each
        /// volley.
        ///
        /// Built on the fact the arena hands over for free: a boss bullet laps the
        /// ring in 4.5 seconds and the player in 12.3, so nothing the boss fires
        /// is ever really gone - it comes back round and arrives from behind.
        /// Cycled slower than a lap, the previous curtain returns just before the
        /// next one leaves, and the gap the player is threading is the gap in a
        /// wall they already passed.
        /// </summary>
        Curtain = 1,

        /// <summary>
        /// The same muzzles as Simultaneous, one row at a time from the bottom up,
        /// with the direction alternating each volley.
        ///
        /// A slab becomes a staircase - one you dive under, or one you climb over,
        /// depending on which way it is running. The fix for a volley that reads
        /// as a single mass was never a change in size.
        /// </summary>
        Sequence = 2,

        /// <summary>
        /// A charge, then a dense stream from every muzzle in the bank.
        ///
        /// The only attack the boss aims. It cannot elevate, so it aims by holding
        /// the altitude it has - which is the altitude it took from chasing the
        /// player - and the charge is the window in which that altitude stops
        /// following them. It punishes standing still and nothing else.
        /// </summary>
        Lance = 3,

        /// <summary>
        /// No bullets at all: a telegraph, then the boss itself accelerated round
        /// the ring at the player.
        ///
        /// A volley in the sense that it goes off on a cadence and has to be
        /// dodged; the pattern decides what leaves the ship, and here the answer
        /// is the ship. Faster than the player can run and taller than they can
        /// climb, so the only answer is to go through it - which is what the dash
        /// was built and measured for.
        /// </summary>
        Ram = 4,
    }
}
