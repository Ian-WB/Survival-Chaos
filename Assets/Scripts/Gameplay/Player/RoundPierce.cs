using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>What a round does after striking something.</summary>
    public enum StrikeResult
    {
        /// <summary>The hit counts and the round flies on to the next target.</summary>
        PassThrough,

        /// <summary>The hit counts and the round is spent.</summary>
        Stop,

        /// <summary>
        /// Nothing happens: this round has already struck this target, or has
        /// already stopped. Contacts from the physics step a round was spent in
        /// can still arrive after it has gone back to the pool.
        /// </summary>
        Ignore
    }

    /// <summary>
    /// How many more enemies one of the player's rounds may pass through, and
    /// which it has already struck. For the Piercing Rounds upgrade.
    ///
    /// The list of what has been struck is the half that matters once a round
    /// survives a hit. A round that stopped at its first target could never meet
    /// it twice; one that flies on is still overlapping the ship it just went
    /// through, and ShootScript's sweep and the physics system both report
    /// contacts, so without it a single pass could land twice and spend the
    /// pierce on the same enemy.
    ///
    /// No Unity object attached, so the rules can be tested directly.
    /// </summary>
    public sealed class RoundPierce
    {
        private readonly List<object> struck = new List<object>(4);
        private int left;
        private bool stopped;

        /// <summary>Enemies this round may still pass through.</summary>
        public int Left => left;

        /// <summary>Starts a round's life with <paramref name="pierces"/> passes and nothing struck.</summary>
        public void Reset(int pierces)
        {
            left = Mathf.Max(0, pierces);
            stopped = false;
            struck.Clear();
        }

        /// <summary>Records a strike on <paramref name="target"/> and says what happens next.</summary>
        public StrikeResult Strike(object target)
        {
            if (stopped || (target != null && struck.Contains(target)))
            {
                return StrikeResult.Ignore;
            }

            struck.Add(target);

            if (left > 0)
            {
                left--;
                return StrikeResult.PassThrough;
            }

            stopped = true;
            return StrikeResult.Stop;
        }
    }
}
