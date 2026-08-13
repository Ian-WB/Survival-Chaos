using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// One collectible item on the ring: the thing the player flies into to take
    /// an upgrade.
    ///
    /// This component knows how to look like a pickup, how long it lives, and
    /// how to notice the player. It deliberately does not know what it grants -
    /// the payload is carried opaquely and handed back to the spawner on
    /// contact. Keeping the reward out of here is what lets one prefab serve
    /// every skill and the health drop as well.
    ///
    /// Spawned through ObjectPool, so all the setup lives in
    /// <see cref="Configure"/> rather than in a constructor or in Awake.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Pickup : MonoBehaviour
    {
        [Header("Motion")]
        [SerializeField]
        [Tooltip("Degrees per second the item spins about its own axis. Pure decoration - " +
                 "it makes a small object read as collectible rather than as scenery.")]
        private float spinSpeed = 120f;

        [SerializeField]
        [Tooltip("How far the item drifts up and down from where it was placed, in world units.")]
        private float bobHeight = 0.25f;

        [SerializeField]
        [Tooltip("Full up-and-down cycles per second.")]
        private float bobFrequency = 0.8f;

        [Header("Expiry")]
        [SerializeField]
        [Tooltip("Seconds of life left at which the item starts flashing, warning that " +
                 "the offer is about to be forfeited.")]
        private float warnWithin = 3f;

        [SerializeField]
        [Tooltip("Flashes per second during the warning.")]
        private float warnFlashRate = 4f;

        [Header("Look")]
        [SerializeField]
        [Tooltip("Renderer tinted to the payload's colour. Left empty, the first renderer " +
                 "found in the children is used.")]
        private Renderer tintTarget;

        /// <summary>
        /// HDRP's Unlit shader takes its emission from this property. Setting the
        /// base colour alone leaves the object lit but not glowing, which is the
        /// difference between a pickup you notice mid-fight and one you do not.
        /// </summary>
        private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");

        private static readonly int UnlitColor = Shader.PropertyToID("_UnlitColor");

        /// <summary>
        /// Applied through a property block rather than by assigning to
        /// <c>renderer.material</c>. The latter silently clones the material on
        /// first touch, so every pickup would leak an instance that outlives it
        /// and never returns to the pool with the rest of the object.
        /// </summary>
        private MaterialPropertyBlock tintBlock;

        private PickupSpawner owner;
        private Vector3 anchor;
        private float expiresAt;
        private bool collected;

        /// <summary>
        /// Where in its bob cycle this pickup starts.
        ///
        /// Without it every pickup drives off the same clock and a three-way
        /// offer rises and falls in lockstep, which reads as one animated object
        /// in three places rather than three separate things.
        /// </summary>
        private float bobPhase;

        /// <summary>What this pickup grants. Read by the spawner, opaque here.</summary>
        public SkillDefinition Skill { get; private set; }

        /// <summary>Health granted when <see cref="Skill"/> is null.</summary>
        public int HealAmount { get; private set; }

        /// <summary>The offer this belongs to, or null for a standalone drop.</summary>
        public SkillOffer Offer { get; set; }

        /// <summary>
        /// Sets the pickup up for a life. Called immediately after Spawn, before
        /// the object has had a frame to run.
        /// </summary>
        /// <param name="spawner">Told when this is taken or runs out.</param>
        /// <param name="skill">The upgrade granted, or null for a health drop.</param>
        /// <param name="healAmount">Health granted when <paramref name="skill"/> is null.</param>
        /// <param name="color">Glow colour, normally the skill's own.</param>
        /// <param name="lifetime">Seconds before the pickup gives up and expires.</param>
        public void Configure(
            PickupSpawner spawner,
            SkillDefinition skill,
            int healAmount,
            Color color,
            float lifetime)
        {
            owner = spawner;
            Skill = skill;
            HealAmount = healAmount;
            Offer = null;

            collected = false;
            anchor = transform.position;
            bobPhase = Random.value * Mathf.PI * 2f;

            // Unscaled would keep the clock running through the death screen.
            // Time.time is the right one: a paused game should not expire an
            // offer the player never got to answer.
            expiresAt = Time.time + Mathf.Max(0.1f, lifetime);

            Tint(color);
        }

        private void Update()
        {
            if (collected)
            {
                return;
            }

            float remaining = expiresAt - Time.time;

            if (remaining <= 0f)
            {
                Expire();
                return;
            }

            Animate(remaining);
        }

        /// <summary>
        /// Spin, bob, and - once the clock is nearly out - flash.
        ///
        /// The flash is the only honest way to run a forfeit timer. An offer that
        /// vanishes without warning reads as a bug; one that visibly runs down
        /// reads as a decision the player was given and did not take.
        /// </summary>
        private void Animate(float remaining)
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

            Vector3 position = anchor;
            position.y += Mathf.Sin(bobPhase + Time.time * bobFrequency * Mathf.PI * 2f) * bobHeight;
            transform.position = position;

            if (remaining <= warnWithin && tintTarget != null)
            {
                bool on = Mathf.Repeat(remaining * warnFlashRate, 1f) > 0.5f;
                tintTarget.enabled = on;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Guarded because the trigger can fire more than once in a physics
            // step, and a second entry would apply the upgrade twice.
            if (collected || !other.CompareTag("Player"))
            {
                return;
            }

            collected = true;

            if (owner != null)
            {
                owner.OnCollected(this);
            }
        }

        private void Expire()
        {
            collected = true;

            if (owner != null)
            {
                owner.OnExpired(this);
            }
        }

        /// <summary>
        /// Puts the renderer back before the object returns to the pool.
        ///
        /// The flash leaves the renderer disabled about half the time, and a
        /// pooled object keeps whatever state it died in. Without this a pickup
        /// that timed out mid-flash would come back invisible - and be collected
        /// by a player who could not see it.
        /// </summary>
        private void OnDisable()
        {
            if (tintTarget != null)
            {
                tintTarget.enabled = true;
            }
        }

        private void Tint(Color color)
        {
            if (tintTarget == null)
            {
                tintTarget = GetComponentInChildren<Renderer>();
            }

            if (tintTarget == null)
            {
                return;
            }

            tintBlock ??= new MaterialPropertyBlock();
            tintTarget.GetPropertyBlock(tintBlock);

            tintBlock.SetColor(EmissiveColor, color);

            // The unlit colour is what shows where bloom does not reach - the
            // object's own surface. Kept in step so the pickup is the same colour
            // whether or not post-processing is on, which matters because the
            // bottom quality tiers switch bloom off.
            tintBlock.SetColor(UnlitColor, color);

            tintTarget.SetPropertyBlock(tintBlock);
        }
    }
}
