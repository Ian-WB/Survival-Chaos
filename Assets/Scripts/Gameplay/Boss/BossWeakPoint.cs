using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// One of the boss's gun emplacements: the only thing on it that takes
    /// damage while the hull is armoured, and the thing that silences a bank of
    /// muzzles when it dies.
    ///
    /// This is the whole of the first act. Resogun's bosses all share one rule -
    /// the hull is not the target, specific colour-coded things bolted to it are
    /// - and the reason it works is that it turns "shoot the boss" into "be
    /// somewhere particular while shooting the boss". Here that comes free from
    /// the rig: player bullets fly at the player's own height and never change
    /// it, and each emplacement sits at the height of the bank it feeds, so
    /// killing one means flying at that bank's height, which is exactly where its
    /// own fire is thickest.
    ///
    /// Mounted proud of the hull on the face the guns point out of. That is not
    /// decoration - the hull is a trigger that eats bullets, so a target buried
    /// inside it could never be hit. Sitting past the hull's own face means the
    /// bullet reaches this collider first, and the boss turns to face whoever it
    /// is chasing, so the armed face is the face the player sees.
    ///
    /// It rides the mirroring muzzle rig with the guns it belongs to, so it
    /// changes sides when the boss turns round, the same way they do.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossWeakPoint : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Name shown in the inspector and in the attack that this emplacement silences. " +
                 "Has no effect on the game.")]
        private string label = "Emplacement";

        [SerializeField]
        [Tooltip("Hit points, one per bullet. Three of these at 50 spend the first half of the " +
                 "boss's 300, which is what keeps the health bar moving through a phase where " +
                 "the hull itself is taking nothing.")]
        private int healthPoints = 50;

        [SerializeField]
        [Tooltip("Played where a shot lands without destroying this. Same role as the enemy " +
                 "spark: at 50 hit points, 49 of every 50 shots produce nothing without it.")]
        private GameObject hitEffect;

        [SerializeField]
        [Tooltip("Played once, here, when this emplacement is destroyed.")]
        private GameObject explosion;

        [SerializeField]
        [Tooltip("The visible pod. Scaled up during a telegraph and hidden once wrecked. Kept " +
                 "separate from this object so the swell cannot quietly resize the collider " +
                 "and make the pod easier to hit while it is charging.")]
        private Transform glow;

        [SerializeField]
        [Range(1f, 3f)]
        [Tooltip("How far the pod swells at the top of a telegraph, as a multiple of its " +
                 "resting size.")]
        private float telegraphScale = 1.7f;

        /// <summary>
        /// The emitter this reports to. Found upward rather than assigned, because
        /// there is exactly one answer and it is the object this is parented to.
        /// </summary>
        private BossEmitter owner;

        private HealthState health;
        private HitFlash flash;
        private Collider target;
        private Vector3 restingScale = Vector3.one;

        /// <summary>Inspector-only name. Nothing reads this at runtime.</summary>
        public string Label => label;

        /// <summary>
        /// Whether this emplacement is wrecked, and so whether the attack it feeds
        /// still fires.
        ///
        /// Kept here rather than inferred from the health, because health is
        /// rebuilt on every spawn and the attacks ask this every frame.
        /// </summary>
        public bool Destroyed { get; private set; }

        private void Awake()
        {
            owner = GetComponentInParent<BossEmitter>();
            flash = GetComponent<HitFlash>();
            target = GetComponent<Collider>();

            if (glow != null)
            {
                restingScale = glow.localScale;
            }
        }

        /// <summary>
        /// The state that belongs to a life rather than to the object.
        ///
        /// The boss is pooled, so a second one would arrive with its emplacements
        /// already wrecked and invisible - a boss that opens the fight in its
        /// second act. This runs because the boss's own root is what the pool
        /// disables, and disabling a parent sends OnDisable down the tree and
        /// re-enabling it sends OnEnable back down. Which is the reason death
        /// hides the pod rather than deactivating it: an object switched off by
        /// name stays off when its parent comes back on.
        /// </summary>
        private void OnEnable()
        {
            health = new HealthState(healthPoints);
            Destroyed = false;

            if (target != null)
            {
                target.enabled = true;
            }

            Show(true);
            Telegraph(0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Destroyed || !other.CompareTag("Shoot"))
            {
                return;
            }

            // Read before despawning, so the spark lands where the bullet struck
            // rather than at the middle of the pod.
            Vector3 impact = other.transform.position;

            ObjectPool.Despawn(other.gameObject);

            bool killed = health.TakeDamage(1);

            // Reported either way. The bar shows one pool for the whole boss and
            // the emplacements spend the first half of it, so every point that
            // lands here has to move it.
            if (owner != null)
            {
                owner.ReportEmplacementDamage(1);
            }

            if (killed)
            {
                Wreck();
                return;
            }

            if (flash != null)
            {
                flash.Strike();
            }

            if (hitEffect != null)
            {
                ObjectPool.Spawn(hitEffect, impact, transform.rotation);
            }
        }

        /// <summary>
        /// Drives the pod's swell during an attack's charge, 0 resting and 1 at
        /// the moment it fires.
        ///
        /// The whole telegraph, and it has to carry the warning on its own: the
        /// muzzles are empty transforms with nothing to light up, and the pod is
        /// the only part of an emplacement the player can see. A size change
        /// rather than a colour change because the pod's colour is already saying
        /// something - green means shoot here - and a telegraph that overwrote it
        /// would be trading a permanent signal for a temporary one.
        /// </summary>
        public void Telegraph(float charge)
        {
            if (glow == null)
            {
                return;
            }

            glow.localScale = restingScale * Mathf.Lerp(1f, telegraphScale, Mathf.Clamp01(charge));
        }

        /// <summary>
        /// Wrecks this emplacement, silencing the bank it feeds.
        ///
        /// Public because a bullet is not the only thing that should be able to
        /// end one. Nothing else calls it today.
        /// </summary>
        public void Wreck()
        {
            if (Destroyed)
            {
                return;
            }

            Destroyed = true;

            if (explosion != null)
            {
                ObjectPool.Spawn(explosion, transform.position, transform.rotation);
            }

            // Hidden and untargetable, but still here and still running, because
            // the object has to be able to come back. Turning the collider off is
            // also what stops a wrecked emplacement from swallowing shots aimed
            // at the hull behind it, once the hull is worth shooting.
            if (target != null)
            {
                target.enabled = false;
            }

            Telegraph(0f);
            Show(false);

            if (owner != null)
            {
                owner.ReportEmplacementDestroyed();
            }
        }

        private void Show(bool visible)
        {
            if (glow == null)
            {
                return;
            }

            foreach (Renderer part in glow.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                part.enabled = visible;
            }
        }
    }
}
