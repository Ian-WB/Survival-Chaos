using System;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>When an attack is allowed to fire.</summary>
    public enum AttackTrigger
    {
        /// <summary>Fires on its own timer for the whole fight.</summary>
        Repeating = 0,

        /// <summary>Fires only while the player is inside the laser trigger.</summary>
        WhileLaserActive = 1
    }

    /// <summary>
    /// One volley: a set of muzzles, the projectile to use in each travel
    /// direction, and how often it goes off.
    ///
    /// This replaces three hardcoded methods that between them held roughly 90
    /// Instantiate calls against 32 individually named pivot fields. Adding or
    /// retuning an attack is now an inspector edit.
    /// </summary>
    [Serializable]
    public class BossAttack
    {
        [Tooltip("Name shown in the inspector list. Has no effect on the game.")]
        public string label = "Attack";

        [Tooltip("Muzzles this attack fires from, one projectile each.")]
        public Transform[] pivots = new Transform[0];

        [Tooltip("Projectile used while the boss travels in its default direction.")]
        public GameObject projectileWhenLeft;

        [Tooltip("Projectile used while travelling the other way.")]
        public GameObject projectileWhenRight;

        public AttackTrigger trigger = AttackTrigger.Repeating;

        [Tooltip("Seconds before this attack first fires.")]
        public float initialDelay = 1f;

        [Tooltip("Seconds between volleys. 0 fires every frame, which is very fast - see minInterval.")]
        public float interval = 1f;

        public GameObject ProjectileFor(bool travellingLeft)
        {
            return travellingLeft ? projectileWhenLeft : projectileWhenRight;
        }
    }
}
