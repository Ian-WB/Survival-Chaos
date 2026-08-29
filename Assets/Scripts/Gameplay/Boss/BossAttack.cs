using System;
using UnityEngine;

namespace SurvivalChaos
{
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
        [SerializeField]
        [Tooltip("Name shown in the inspector list. Has no effect on the game.")]
        private string label = "Attack";

        [SerializeField]
        [Tooltip("Muzzles this attack fires from, one projectile each.")]
        private Transform[] pivots = new Transform[0];

        [SerializeField]
        [Tooltip("Projectile used while the boss travels in its default direction.")]
        private GameObject projectileWhenLeft;

        [SerializeField]
        [Tooltip("Projectile used while travelling the other way.")]
        private GameObject projectileWhenRight;

        [SerializeField]
        [Tooltip("Seconds before this attack first fires.")]
        private float initialDelay = 1f;

        [SerializeField]
        [Tooltip("Seconds between volleys. 0 fires every frame, which is very fast - see minInterval.")]
        private float interval = 1f;

        /// <summary>Inspector-only name. Nothing reads this at runtime.</summary>
        public string Label => label;

        /// <summary>
        /// The muzzles, exposed as the array itself rather than a copy.
        ///
        /// The emitter walks it every volley, and handing back a fresh array each
        /// time would allocate once per attack per fire - the kind of steady
        /// garbage that shows up as spikes against an otherwise flat frame graph.
        /// Its contents are scene transforms the boss owns; nothing reassigns
        /// them.
        /// </summary>
        public Transform[] Pivots => pivots;

        /// <summary>Seconds before the first volley of the fight.</summary>
        public float InitialDelay => initialDelay;

        /// <summary>Seconds between volleys.</summary>
        public float Interval => interval;

        public GameObject ProjectileFor(bool travellingLeft)
        {
            return travellingLeft ? projectileWhenLeft : projectileWhenRight;
        }

        /// <summary>
        /// Both projectiles, for the warmup pass. Named for what the caller wants
        /// rather than exposing the two fields, since a direction it has not
        /// picked yet is not a question the emitter should have to ask twice.
        /// </summary>
        public void EachProjectile(System.Action<GameObject> visit)
        {
            visit(projectileWhenLeft);
            visit(projectileWhenRight);
        }
    }
}
