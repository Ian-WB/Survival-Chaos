using System;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// One attack: a bank of muzzles, the projectile to use in each travel
    /// direction, what shape the volley takes, and when it is allowed to happen.
    ///
    /// This replaced three hardcoded methods that between them held roughly 90
    /// Instantiate calls against 32 individually named pivot fields. It now also
    /// carries the fight's structure - which phase an attack belongs to, and
    /// which emplacement has to be standing for it to fire - because those are
    /// per-attack facts, and the alternative is three attack lists that mostly
    /// repeat each other.
    ///
    /// Most of the timing fields below are read by one pattern each. That is the
    /// cost of keeping every attack in one inspector list, and it is worth
    /// paying: the thing being tuned is the fight, and the fight is the list.
    /// </summary>
    [Serializable]
    public class BossAttack
    {
        [SerializeField]
        [Tooltip("Name shown in the inspector list. Has no effect on the game.")]
        private string label = "Attack";

        [SerializeField]
        [Tooltip("What one volley does. The muzzles are the same either way - the pattern is " +
                 "purely a question of when each one goes off.")]
        private BossFirePattern pattern = BossFirePattern.Simultaneous;

        [SerializeField]
        [Tooltip("Which acts of the fight this attack is allowed to fire in.")]
        private BossPhaseMask phases = BossPhaseMask.All;

        [SerializeField]
        [Tooltip("The emplacement that has to be standing for this attack to fire. Destroying " +
                 "it silences this attack for the rest of the fight. Leave empty for an attack " +
                 "nothing can switch off - the last act fires from wrecked banks too.")]
        private BossWeakPoint weakPoint;

        [SerializeField]
        [Tooltip("Muzzles this attack fires from, one projectile each.")]
        private Transform[] pivots = new Transform[0];

        [SerializeField]
        [Tooltip("Projectile used while the boss travels in its default direction.")]
        private GameObject projectileWhenLeft;

        [SerializeField]
        [Tooltip("Projectile used while travelling the other way.")]
        private GameObject projectileWhenRight;

        [Header("Cadence")]
        [SerializeField]
        [Tooltip("Seconds before this attack first fires, counted from the start of the phase " +
                 "it belongs to rather than from the start of the fight.")]
        private float initialDelay = 1f;

        [SerializeField]
        [Tooltip("Seconds between volleys. 0 fires every frame, which is very fast.")]
        private float interval = 1f;

        [Header("Sequence")]
        [SerializeField]
        [Tooltip("Seconds between rows in a Sequence volley. The rows come off the muzzle " +
                 "heights, so this times a rake across the whole bank whatever it is made of.")]
        private float stepSeconds = 0.12f;

        [Header("Curtain")]
        [SerializeField]
        [Range(1, 4)]
        [Tooltip("How many rows a Curtain leaves open. One row is the gap the player flies " +
                 "through; more than one and the wall stops being a wall.")]
        private int openRows = 1;

        [Header("Charge")]
        [SerializeField]
        [Tooltip("Seconds of telegraph before a Lance or a Ram commits. The emplacement swells " +
                 "for this whole window, and for a Lance the boss stops matching the player's " +
                 "height the instant it starts - which is what makes the warning honest.")]
        private float chargeSeconds = 1.2f;

        [SerializeField]
        [Tooltip("Seconds the committed part lasts: how long a Lance streams, or how long a " +
                 "Ram sweeps.")]
        private float burstSeconds = 0.4f;

        [SerializeField]
        [Tooltip("Seconds between shots inside a Lance stream.")]
        private float burstInterval = 0.06f;

        [SerializeField]
        [Range(1f, 6f)]
        [Tooltip("How much faster the boss travels during a Ram, as a multiple of its cruise. " +
                 "It has to beat the player's own orbit speed or running away wins.")]
        private float ramSpeedScale = 3f;

        /// <summary>Inspector-only name. Nothing reads this at runtime.</summary>
        public string Label => label;

        /// <summary>What one volley of this attack does.</summary>
        public BossFirePattern Pattern => pattern;

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

        /// <summary>Seconds before the first volley of the phase this belongs to.</summary>
        public float InitialDelay => initialDelay;

        /// <summary>Seconds between volleys.</summary>
        public float Interval => interval;

        /// <summary>Seconds between rows in a Sequence volley.</summary>
        public float StepSeconds => Mathf.Max(0f, stepSeconds);

        /// <summary>How many rows a Curtain leaves open.</summary>
        public int OpenRows => Mathf.Max(1, openRows);

        /// <summary>Seconds of telegraph before a Lance or a Ram commits.</summary>
        public float ChargeSeconds => Mathf.Max(0f, chargeSeconds);

        /// <summary>Seconds the committed part of a Lance or Ram lasts.</summary>
        public float BurstSeconds => Mathf.Max(0f, burstSeconds);

        /// <summary>
        /// Seconds between shots inside a Lance stream. Floored just above zero,
        /// because zero here is not "very fast", it is a loop that yields nothing
        /// and hangs the frame.
        /// </summary>
        public float BurstInterval => Mathf.Max(0.01f, burstInterval);

        /// <summary>How much faster the boss travels during a Ram.</summary>
        public float RamSpeedScale => Mathf.Max(1f, ramSpeedScale);

        /// <summary>
        /// The emplacement that has to survive for this attack to fire, or null
        /// for one that nothing can switch off.
        /// </summary>
        public BossWeakPoint WeakPoint => weakPoint;

        /// <summary>
        /// Whether this attack is available at all right now: the phase allows it
        /// and the emplacement that feeds it is still standing.
        ///
        /// Both halves in one place because they are one question. The caller asks
        /// it every frame per attack and should not have to remember that an
        /// attack can be switched off two different ways.
        /// </summary>
        public bool Available(BossPhase phase)
        {
            if (!phases.Includes(phase))
            {
                return false;
            }

            return weakPoint == null || !weakPoint.Destroyed;
        }

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
