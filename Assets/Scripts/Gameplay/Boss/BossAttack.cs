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
        [Tooltip("Seconds between one muzzle and the next within a single volley. 0 fires them " +
                 "together, which is what a wall wants. A Lance wants a small value instead: its " +
                 "four prow muzzles sit inside about two units of each other, so firing them at " +
                 "one instant stacks four rounds in one place rather than streaming them.")]
        private float muzzleStagger;

        [SerializeField]
        [Range(1f, 6f)]
        [Tooltip("How much faster the boss travels during a Ram, as a multiple of its cruise. " +
                 "It has to beat the player's own orbit speed or running away wins.")]
        private float ramSpeedScale = 3f;

        [Header("Tell")]
        [SerializeField]
        [Tooltip("Seconds the muzzles glow before a Curtain or a Sequence fires. Only the muzzles " +
                 "that are about to fire light, so a curtain shows its gap and a rake shows which " +
                 "way it will climb. 0 fires unannounced. The Lance and the Ram warn through their " +
                 "own charge instead and ignore this.")]
        private float tellSeconds;

        [SerializeField]
        [Tooltip("World units across each muzzle's glow at the moment it fires. Per attack, because " +
                 "it has to fit between the bank's rows: a glow wider than the gap between two rows " +
                 "merges them, and a rake whose rows have merged no longer shows which way it climbs.")]
        private float tellSize = 0.5f;

        [Header("Route")]
        [SerializeField]
        [Tooltip("How far above and below its firing height each round weaves, in world units. " +
                 "Keep it inside the bank's own rows: the armoured act is about which height the " +
                 "fire comes from, and a weave wider than the bank erases that.")]
        private float routeAmplitude;

        [SerializeField]
        [Tooltip("Seconds for one full weave, up and back down.")]
        private float routePeriod = 1.5f;

        [SerializeField]
        [Tooltip("Alternate rows weave in opposite directions, so neighbouring rows close, cross " +
                 "and open again. Off, the whole volley weaves as one shape - which is what a " +
                 "curtain wants, since its gap has to stay a gap.")]
        private bool routeCrossing;

        [SerializeField]
        [Tooltip("Fire the other way round the ring from the way the boss is travelling. Two banks " +
                 "going the same way at the same speed read as one lane; one of them reversed is " +
                 "two routes that visibly cross.")]
        private bool reverseRoute;

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

        /// <summary>
        /// Seconds between muzzles inside one volley. Zero fires them together.
        /// </summary>
        public float MuzzleStagger => Mathf.Max(0f, muzzleStagger);

        /// <summary>How much faster the boss travels during a Ram.</summary>
        public float RamSpeedScale => Mathf.Max(1f, ramSpeedScale);

        /// <summary>Seconds the muzzles glow before a Curtain or Sequence fires.</summary>
        public float TellSeconds => Mathf.Max(0f, tellSeconds);

        /// <summary>World units across each muzzle's glow at full.</summary>
        public float TellSize => Mathf.Max(0f, tellSize);

        /// <summary>Whether rounds from this attack weave at all.</summary>
        public bool HasRoute => routeAmplitude != 0f && routePeriod > 0f;

        /// <summary>How far each round weaves either side of its firing height.</summary>
        public float RouteAmplitude => routeAmplitude;

        /// <summary>Seconds for one full weave.</summary>
        public float RoutePeriod => routePeriod;

        /// <summary>Whether alternate rows weave in opposite directions.</summary>
        public bool RouteCrossing => routeCrossing;

        /// <summary>
        /// The round a volley of this attack fires, which way round the ring
        /// included.
        ///
        /// Separate from <see cref="ProjectileFor"/> because the lance reads
        /// that one for its direction, and reversing a volley's route is not a
        /// statement about which way the lance should sweep.
        /// </summary>
        public GameObject RoundFor(bool travellingLeft)
        {
            return ProjectileFor(reverseRoute ? !travellingLeft : travellingLeft);
        }

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
